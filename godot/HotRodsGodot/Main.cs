using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using HotRodsSharp;

/// <summary>
/// HotRods game controller: owns the state and sims, drives the fishing
/// loop and smoking ceremony, and keeps the visuals and HUD in sync.
/// </summary>
public partial class Main : Node3D
{
    private GameState _state = null!;
    private FishingSim _fishing = null!;
    private SmokingSim _smoking = null!;
    private Hud _hud = null!;
    private DirectionalLight3D _sun = null!;
    private Node3D _rod = null!;
    private MeshInstance3D _bobber = null!, _line = null!;
    private AudioStreamPlayer _sfx = null!;
    private readonly List<(MeshInstance3D node, float age, StandardMaterial3D mat)> _ripples = new();

    private bool _wasSmoking, _eHeld, _qHeld, _dragging;
    private float _yawDeg, _pitchDeg, _uiMsgT;
    private int _munchieLevel;
    private Camera3D _cam = null!;

    private static readonly Vector3 RodTip = new(-1.83f, 1.60f, 1.45f);
    private static readonly Vector3 Grip = new(-4.14f, 1.20f, 1.62f);
    private static readonly Vector3 CastDir = new(1f, -0.12f, -0.10f);

    public override void _Ready()
    {
        GameData.Load();
        _state = GameState.AnySave() ? GameState.Load(1) : GameState.New(1);
        _fishing = new FishingSim(_state);
        _smoking = new SmokingSim();
        _smoking.SetItem(_state.Smoker);
        _fishing.Ripple += SpawnRipple;
        _fishing.MunchieBlocked += () => Flash("MUNCHIES, go eat food to keep fishing", 3.5f);
        _fishing.Sfx = PlaySfx;

        _cam = GetNode<Camera3D>("Camera3D");
        _sun = GetNode<DirectionalLight3D>("Sun");
        _hud = GetNode<Hud>("Hud");
        _hud.Bind(_state, _fishing);
        _sfx = new AudioStreamPlayer();
        AddChild(_sfx);

        BuildFishingVisuals();

        if (OS.GetCmdlineUserArgs().Contains("--shot"))
            CallDeferred(nameof(TakeShot));
        if (OS.GetCmdlineUserArgs().Contains("--simtest"))
            CallDeferred(nameof(RunSimTests));
    }

    // ---------------------------------------------------------------- input --
    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
            _dragging = mb.Pressed;
        if (e is InputEventMouseMotion mm && _dragging)
        {
            _yawDeg = Mathf.Clamp(_yawDeg - mm.Relative.X * 0.18f, -50f, 50f);
            _pitchDeg = Mathf.Clamp(_pitchDeg + mm.Relative.Y * 0.18f, -25f, 30f);
            _cam.RotationDegrees = new Vector3(_pitchDeg, -90f + _yawDeg, 0f);
        }
    }

    // ----------------------------------------------------------------- tick --
    public override void _Process(double delta)
    {
        float dt = (float)delta;
        FishingSim.TickTime(dt);

        bool space = Input.IsKeyPressed(Key.Space);
        if (!_smoking.Active)
            _fishing.Update(dt, space, RodTip, CastDir.Normalized());
        else
            _smoking.Update(dt);
        if (_wasSmoking && !_smoking.Active)
            Flash(_state.ApplyToke(), 2.5f);
        _wasSmoking = _smoking.Active;
        _smoking.Speed = 1f + 0.06f * _state.StokeRank("smoke");

        if (Input.IsKeyPressed(Key.E) && !_eHeld)
        {
            var (ok, msg) = _state.CanToke();
            if (!ok)
                Flash(msg, 2.5f);
            else
            {
                _smoking.SetItem(_state.Smoker);
                _smoking.Start();
            }
            _eHeld = true;
        }
        if (!Input.IsKeyPressed(Key.E))
            _eHeld = false;
        if (Input.IsKeyPressed(Key.Q) && !_qHeld)
        {
            var owned = GameData.Smokers.Where(g => _state.Owns("smoker", g.Id))
                        .Select(g => g.Id).ToArray();
            if (owned.Length > 0)
            {
                int idx = Array.IndexOf(owned, _state.Smoker);
                _state.Smoker = owned[(idx + 1) % owned.Length];
                _smoking.SetItem(_state.Smoker);
                Flash("piece: " + GameData.Smokers.First(x => x.Id == _state.Smoker).Name, 2f);
            }
            _qHeld = true;
        }
        if (!Input.IsKeyPressed(Key.Q))
            _qHeld = false;

        _state.TickChill(dt);
        _uiMsgT -= dt;
        float m = _state.Munchie;
        int band = m >= 100f ? 4 : m >= 75f ? 3 : m >= 50f ? 2 : m >= 25f ? 1 : 0;
        if (band > _munchieLevel)
        {
            _munchieLevel = band;
            Flash(band == 4 ? "MUNCHIES, go eat food to keep fishing"
                : band == 3 ? "Craving some snacks bad"
                : band == 2 ? "Getting kinda hungry"
                : "getting a little snacky", 3.5f);
        }
        else if (band < _munchieLevel)
        {
            _munchieLevel = band;
        }
        _state.Hour = (_state.Hour + dt / 30f) % 24f;
        _state.Played += dt / 60f;

        SunTick();
        UpdateFishingVisuals();
        UpdateRipples(dt);
        _hud.Tick(dt, FishingSim.Clock);
    }

    // ---------------------------------------------------------------- visuals --
    private void BuildFishingVisuals()
    {
        // the rod: a simple two-tone stick from grip to tip
        _rod = new Node3D { Name = "Rod" };
        AddChild(_rod);
        var rodMesh = new CylinderMesh { TopRadius = 0.016f, BottomRadius = 0.03f, Height = 1f };
        var mid = (Grip + RodTip) * 0.5f;
        var rodInst = new MeshInstance3D { Mesh = rodMesh };
        var rodMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.75f, 0.62f, 0.42f),
            Roughness = 0.6f,
        };
        rodInst.MaterialOverride = rodMat;
        _rod.AddChild(rodInst);

        // the line: a thin cylinder stretched between tip and bobber
        var lineMesh = new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.008f, Height = 1f };
        _line = new MeshInstance3D { Mesh = lineMesh };
        var lineMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.88f, 0.92f, 0.96f),
            Roughness = 0.4f,
        };
        _line.MaterialOverride = lineMat;
        AddChild(_line);

        // the bobber: red base, white top
        _bobber = new MeshInstance3D { Name = "Bobber" };
        var red = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.05f, Height = 0.10f } };
        red.Position = new Vector3(0, -0.05f, 0);
        red.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.82f, 0.24f, 0.20f) };
        var white = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.03f, Height = 0.06f } };
        white.Position = new Vector3(0, 0.04f, 0);
        white.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.95f, 0.95f) };
        _bobber.AddChild(red);
        _bobber.AddChild(white);
        AddChild(_bobber);
    }

    private void UpdateFishingVisuals()
    {
        bool active = _fishing.Current is not (FishingSim.Phase.Idle or FishingSim.Phase.Card);
        _bobber.Visible = active;
        _line.Visible = active;

        // rod: grip -> tip, bending slightly under load
        var tip = RodTip - new Vector3(0f,
            _fishing.Current == FishingSim.Phase.Reeling ? 0.10f * _fishing.Progress
            : _fishing.Current == FishingSim.Phase.Hook ? 0.16f : 0f, 0f);
        var mid = (Grip + tip) * 0.5f;
        float len = Grip.DistanceTo(tip);
        _rod.Position = mid;
        _rod.Scale = new Vector3(1f, len, 1f);
        _rod.Rotation = Vector3.Zero;
        _rod.LookAt(tip, Vector3.Up);
        _rod.RotationDegrees = new Vector3(_rod.RotationDegrees.X + 90f, _rod.RotationDegrees.Y, _rod.RotationDegrees.Z);

        // line + bobber
        var bob = _fishing.BobberWorld;
        var lm = (bob + tip) * 0.5f;
        float llen = Mathf.Max(0.05f, bob.DistanceTo(tip));
        _line.Position = lm;
        _line.Scale = new Vector3(1f, llen, 1f);
        _line.LookAt(tip, Vector3.Up);
        _line.RotationDegrees = new Vector3(_line.RotationDegrees.X + 90f, _line.RotationDegrees.Y, _line.RotationDegrees.Z);
        _bobber.Position = bob;
    }

    private void SpawnRipple(Vector3 pos)
    {
        var mesh = new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.3f, Height = 0.02f };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.92f, 0.96f, 0.98f, 0.8f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        var node = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat };
        node.Position = pos + Vector3.Up * 0.06f;
        AddChild(node);
        _ripples.Add((node, 0f, mat));
    }

    private void UpdateRipples(float dt)
    {
        for (int i = _ripples.Count - 1; i >= 0; i--)
        {
            var (node, age, mat) = _ripples[i];
            float t = age / 1.4f;
            if (t >= 1f)
            {
                node.QueueFree();
                _ripples.RemoveAt(i);
                continue;
            }
            node.Scale = Vector3.One * (0.3f + t * 3.2f);
            mat.AlbedoColor = new Color(0.92f, 0.96f, 0.98f, 0.8f * (1f - t));
            _ripples[i] = (node, age + dt, mat);
        }
    }

    // ------------------------------------------------------------------- sun --
    private static Vector3 SunWorld(float hour)
    {
        float p = Mathf.Clamp((hour - 5f) / 15f, 0f, 1f);
        return new Vector3(Mathf.Sin(p * Mathf.Pi) * 46f,
                           24f + Mathf.Sin(p * Mathf.Pi) * 26f,
                           -16f - p * 36f);
    }

    private void SunTick()
    {
        _sun.LookAt(SunWorld(_state.Hour), Vector3.Up);
        float night = 1f - Mathf.Clamp((_state.Hour - 5f) / 3f, 0f, 1f)
                          * Mathf.Clamp((20f - _state.Hour) / 3f, 0f, 1f);
        _sun.LightEnergy = Mathf.Lerp(0.25f, 1.2f, 1f - night);
        _sun.LightColor = new Color(0.55f, 0.65f, 0.9f).Lerp(
            new Color(1f, 0.94f, 0.82f), 1f - night);
    }

    // ------------------------------------------------------------------ audio --
    private void PlaySfx(string name)
    {
        var path = name switch
        {
            "splash" => "res://assets/splash.wav",
            "big" => "res://assets/splash_big.wav",
            _ => "",
        };
        if (path.Length == 0)
            return;
        var stream = GD.Load<AudioStreamWav>(path);
        if (stream != null)
        {
            _sfx.Stream = stream;
            _sfx.Play();
        }
    }

    // ----------------------------------------------------------------- helpers --
    private void Flash(string msg, float t)
    {
        _uiMsgT = t;
        _hud.Flash(msg, t);
    }

    private async void TakeShot()
    {
        for (int i = 0; i < 8; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var img = GetViewport().GetTexture().GetImage();
        img.SavePng("user://shot.png");
        GD.Print("SHOT: " + ProjectSettings.GlobalizePath("user://shot.png"));
        GetTree().Quit();
    }

    private void RunSimTests()
    {
        int passes = 0;
        void Check(bool ok, string what)
        {
            if (!ok)
                GD.PushError("FAIL: " + what);
            else
                passes++;
        }
        Check(GameData.SpeciesList.Length == 32, "32 species");
        var st = GameState.New(1);
        st.WeedCounts["homegrown"] = 5;
        var sim = new FishingSim(st);
        for (int i = 0; i < 200; i++)
        {
            var f = sim.Roll();
            Check(f != null && f.Id.Length > 0, "roll " + i);
        }
        st.Chill = 0f;
        st.ApplyToke();
        Check(st.Chill >= 40f && st.Chill <= 44f, "toke amount");
        st.Chill = 78f;
        var msg = st.ApplyToke();
        Check(st.Couch == 2.6f && msg.Contains("couch"), "couch lock");
        st.Munchie = 100f;
        Check(!st.CanToke().ok, "munchie blocks smoke");
        GD.Print($"SIMTEST {passes} checks passed");
        GetTree().Quit();
    }
}
