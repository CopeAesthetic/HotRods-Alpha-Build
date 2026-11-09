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
    private Screens _screens = null!;
    private DirectionalLight3D _sun = null!;
    private Node3D _rod = null!;
    private MeshInstance3D _bobber = null!, _line = null!;
    private AudioStreamPlayer _sfx = null!;
    private readonly List<(MeshInstance3D node, float age, StandardMaterial3D mat)> _ripples = new();
    private World _world = null!;
    private Godot.Environment _env = null!;
    private MeshInstance3D _armUpper = null!, _armLower = null!, _hand = null!, _piece = null!;
    private readonly List<(MeshInstance3D node, StandardMaterial3D mat, float offset)> _plume = new();

    private bool _wasSmoking, _wasReeling, _eHeld, _qHeld, _xHeld, _dragging, _atTitle = true;
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
        _world = GetNode<World>("World");
        _env = GetNode<WorldEnvironment>("WorldEnvironment").Environment;
        ApplyWaterTint();
        _hud = GetNode<Hud>("Hud");
        _hud.Bind(_state, _fishing);
        _screens = GetNode<Screens>("Screens");
        _screens.Bind(_state, _fishing, _smoking);
        _screens.Sfx = PlaySfx;
        _screens.Flash = Flash;
        _screens.Board = BoardSlot;
        _screens.ToTitle = () =>
        {
            _atTitle = true;
            _screens.Open("title");
            _hud.Visible = false;
        };
        _screens.Traveled += ApplyWaterTint;
        _hud.IconClicked += i => _screens.Open(i switch
        {
            0 => "menu", 1 => "shop", 2 => "sal", 3 => "map",
            4 => "journal", 5 => "stoke", 6 => "pouch", _ => "",
        });
        _sfx = new AudioStreamPlayer();
        AddChild(_sfx);

        BuildFishingVisuals();
        BuildCeremonyVisuals();

        _hud.Visible = false;
        _screens.Open("title");

        if (OS.GetCmdlineUserArgs().Contains("--shot"))
            CallDeferred(nameof(TakeShot));
        if (OS.GetCmdlineUserArgs().Contains("--simtest"))
            CallDeferred(nameof(RunSimTests));
        if (OS.GetCmdlineUserArgs().Contains("--clicktest"))
            CallDeferred(nameof(ClickTest));
        if (OS.GetCmdlineUserArgs().Contains("--keytest"))
            CallDeferred(nameof(KeyTest));
        if (OS.GetCmdlineUserArgs().Contains("--icontest"))
            CallDeferred(nameof(IconTest));
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

        // the screen layer ticks every frame so it can hide its dim the
        // moment the last screen closes
        _screens.Tick(dt, FishingSim.Clock);
        // the title and screens pause the sim, like the MonoGame port
        if (_atTitle || _screens.AnyOpen)
        {
            _hud.Tick(dt, FishingSim.Clock);
            return;
        }
        if (Input.IsKeyPressed(Key.Escape))
        {
            _screens.Open("menu");
            return;
        }
        if (Input.IsKeyPressed(Key.X) && !_xHeld)
        {
            int n = _state.Bag.Count;
            int total = (int)MathF.Round(_state.BagValue());
            _state.SellBag();
            Flash(n > 0 ? n + " fish, $" + total + ". Pleasure." : "cooler is empty", 3f);
            _xHeld = true;
        }
        if (!Input.IsKeyPressed(Key.X))
            _xHeld = false;

        bool space = Input.IsKeyPressed(Key.Space);
        if (!_smoking.Active)
            _fishing.Update(dt, space, RodTip, CastDir.Normalized());
        else
            _smoking.Update(dt);
        if (_wasSmoking && !_smoking.Active)
            Flash(_state.ApplyToke(), 2.5f);
        _wasSmoking = _smoking.Active;
        if (_wasReeling && !_fishing.Reeling && _fishing.Caught != null)
        {
            var f = _fishing.Caught;
            Flash($"{f.Name.ToUpper()} - {_fishing.CaughtKg:0.0} KG  +${(int)_fishing.CaughtValue}", 3f);
        }
        _wasReeling = _fishing.Reeling;
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
        UpdateCeremonyVisuals();
        UpdateRipples(dt);
        ApplyVibrance();
        _hud.Tick(dt, FishingSim.Clock);
    }

    /// <summary>Synthesises a click on save slot 1 to prove the GUI chain.</summary>
    private async void ClickTest()
    {
        for (int i = 0; i < 10; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var ev = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
            Position = new Vector2(100, 200),
        };
        Input.ParseInputEvent(ev);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GD.Print($"CLICKTEST atTitle={_atTitle} open={_screens.Open_} hudVisible={_hud.Visible}");
        GD.Print("Screens: " + _screens.DebugInfo());
        GD.Print("Hovered: " + (GetViewport().GuiGetHoveredControl()?.Name ?? "null"));
        GetTree().Quit();
    }

    private void SynthClick(Vector2 at)
    {
        Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
            Position = at,
        });
    }

    /// <summary>Boards a slot, then clicks icon tiles 1 (store) and 2 (Sal)
    /// to prove the icon bar routes into the screens.</summary>
    private async void IconTest()
    {
        for (int i = 0; i < 10; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await KeyPress(Key.Enter);          // board slot 1
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        SynthClick(new Vector2(76, 692));   // tile 1 = store
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GD.Print($"ICONTEST store tile: open={_screens.Open_} hovered={GetViewport().GuiGetHoveredControl()?.Name}");
        _screens.FlushPool();
        _screens.Open_ = "";
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        SynthClick(new Vector2(120, 692));  // tile 2 = sal
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GD.Print($"ICONTEST sal tile: open={_screens.Open_}");
        GetTree().Quit();
    }

    private async System.Threading.Tasks.Task KeyPress(Key k)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = k, Pressed = true });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventKey { Keycode = k, Pressed = false });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>Drives the title and menu with synthesised keys to prove
    /// keyboard navigation and selection work.</summary>
    private async void KeyTest()
    {
        for (int i = 0; i < 10; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await KeyPress(Key.Down);      // select slot 2
        await KeyPress(Key.Enter);     // board it
        GD.Print($"KEYTEST title: atTitle={_atTitle}");
        _screens.Open("menu");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await KeyPress(Key.Down);      // menu -> SALS SHACK
        await KeyPress(Key.Space);     // space selects
        GD.Print($"KEYTEST menu: open={_screens.Open_}");
        _screens.Open_ = "";           // close everything
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GD.Print("KEYTEST closed: " + _screens.DebugInfo());
        GetTree().Quit();
    }

    // --------------------------------------------------------------- board --
    private void BoardSlot(int slot)
    {
        var (exists, _, _, _, _, _) = GameState.SlotInfo(slot);
        _state = exists ? GameState.Load(slot) : GameState.New(slot);
        _state.Save();
        _fishing.SetState(_state);
        _smoking.SetItem(_state.Smoker);
        _world.Rebuild(_state.Spot);
        ApplyWaterTint();
        _munchieLevel = 0;
        _atTitle = false;
        _screens.Open_ = "";
        _hud.Visible = true;
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

    // --------------------------------------------------------------- ceremony --
    private void BuildCeremonyVisuals()
    {
        CylinderFor(out _armUpper, new Color(0.89f, 0.70f, 0.26f), 0.06f);
        CylinderFor(out _armLower, new Color(0.84f, 0.65f, 0.49f), 0.05f);
        var handMesh = new BoxMesh { Size = new Vector3(0.08f, 0.08f, 0.09f) };
        _hand = new MeshInstance3D { Mesh = handMesh };
        _hand.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.84f, 0.65f, 0.49f) };
        AddChild(_hand);
        _piece = new MeshInstance3D();
        AddChild(_piece);
        for (int k = 0; k < 5; k++)
        {
            var m = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.04f, Height = 0.08f } };
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.7f, 0.7f, 0.72f, 0.5f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
            m.MaterialOverride = mat;
            AddChild(m);
            _plume.Add((m, mat, k * 0.2f));
        }
    }

    private void CylinderFor(out MeshInstance3D node, Color col, float radius)
    {
        node = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 1f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = col, Roughness = 0.8f },
        };
        AddChild(node);
    }

    private static void StickBetween(MeshInstance3D node, Vector3 a, Vector3 b)
    {
        float len = a.DistanceTo(b);
        node.Visible = len > 0.01f;
        if (!node.Visible) return;
        node.Position = (a + b) * 0.5f;
        node.Scale = new Vector3(1f, len, 1f);
        node.LookAt(b, Vector3.Up);
        node.RotationDegrees = new Vector3(node.RotationDegrees.X + 90f,
                                           node.RotationDegrees.Y, node.RotationDegrees.Z);
    }

    private static readonly Vector3 WaistP = new(-3.98f, 1.10f, 1.40f);
    private static readonly Vector3 MouthP = new(-3.94f, 1.26f, 1.34f);
    private static readonly Vector3 FaceP = new(-3.90f, 1.30f, 1.30f);

    private void UpdateCeremonyVisuals()
    {
        bool on = _smoking.Active;
        _armUpper.Visible = on;
        _armLower.Visible = on;
        _hand.Visible = on;
        _piece.Visible = on;
        foreach (var (m, _, _) in _plume)
            m.Visible = on;
        // the rod hides while you smoke
        if (on)
        {
            _rod.Visible = false;
            _line.Visible = false;
            _bobber.Visible = false;
        }
        if (!on)
        {
            _rod.Visible = true;
            return;
        }

        float r = _smoking.Raise;
        var hold = WaistP.Lerp(FaceP, r);
        var off = new Vector3(0.02f, 0.10f, 0.04f);
        var shR = new Vector3(-4.10f, 1.05f, 1.78f);
        var elR = (shR + hold) * 0.5f + new Vector3(0.03f, -0.05f, 0.02f);
        StickBetween(_armUpper, shR, elR);
        StickBetween(_armLower, elR, hold);
        _hand.Position = hold + off + new Vector3(0f, -0.02f, 0.02f);
        _hand.Visible = true;

        // the piece mesh, raised and tilted like the Python ceremony
        var pieceId = _smoking.CurrentItem;
        var mesh = GD.Load<Mesh>($"res://assets/{pieceId}.obj");
        if (mesh != null)
        {
            _piece.Mesh = mesh;
            _piece.Position = hold + off;
            _piece.Rotation = new Vector3(
                Mathf.DegToRad(-90f) * r * 0.9f, Mathf.DegToRad(-63f) * r, 0f);
            _piece.Scale = Vector3.One * 0.55f;
        }

        // five puffs rising from the mouth
        float t = FishingSim.Clock;
        foreach (var (m, mat, offset) in _plume)
        {
            float pt = (t * 0.55f + offset) % 1.0f;
            m.Position = MouthP + new Vector3(Mathf.Sin(pt * 7f + offset * 5f) * 0.03f,
                                              0.10f + pt * 0.55f, 0.04f);
            float rad = 0.03f + pt * 0.09f;
            m.Scale = Vector3.One * (rad / 0.04f);
            mat.AlbedoColor = new Color(0.7f, 0.7f, 0.72f, 0.5f * (1f - pt));
        }
    }

    // -------------------------------------------------------------- vibrance --
    private void ApplyVibrance()
    {
        float vib = _state.High;
        _env.AmbientLightColor = new Color(0.55f, 0.6f, 0.66f) * (1f + 0.4f * vib);
        _env.AdjustmentSaturation = 1f + 0.5f * vib;
        _env.AdjustmentBrightness = 1f + 0.08f * vib;
    }

    private void ApplyWaterTint()
    {
        var spot = GameData.Spots[_state.Spot];
        var tint = spot.Tint;
        var shallow = new Color(0.30f, 0.56f, 0.56f).Lerp(tint, 0.35f);
        var deep = new Color(0.05f, 0.20f, 0.30f).Lerp(tint, spot.Murk);
        _world.WaterMat.SetShaderParameter("shallow", shallow);
        _world.WaterMat.SetShaderParameter("deep", deep);
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
        _screens.FlushPool();
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

        // the Python value curve: commons land in the dollars, caps hold
        var bluegill = GameData.Species.First(x => x.Id == "bluegill");
        int v0 = st.FishValue(bluegill, 0.3f);
        Check(v0 >= 5 && v0 <= 8, $"bluegill 0.3kg value {v0}");
        var koi = GameData.Species.First(x => x.Id == "golden_koi");
        Check(st.FishValue(koi, 100f) <= 1500, "value cap holds");

        // drive a full catch through the sim: charge, cast, wait, bite,
        // hook, then time pulls inside the zone until the fish lands
        var st2 = GameState.New(1);
        var fs = new FishingSim(st2);
        var rodTip = new Vector3(-1.83f, 1.60f, 1.45f);
        var castDir = new Vector3(1f, -0.12f, -0.1f).Normalized();
        const float step = 1f / 60f;
        int guard = 0;
        fs.Update(step, true, rodTip, castDir);    // Idle -> Charging
        while (fs.Current == FishingSim.Phase.Charging && guard++ < 500)
            fs.Update(step, true, rodTip, castDir);
        fs.Update(step, false, rodTip, castDir);   // release the cast
        guard = 0;
        while (fs.Current is FishingSim.Phase.Flying or FishingSim.Phase.Waiting
               && guard++ < 3000)
            fs.Update(step, false, rodTip, castDir);
        fs.Update(step, true, rodTip, castDir);    // set the hook at the bite
        guard = 0;
        while (fs.Current == FishingSim.Phase.Hook && guard++ < 100)
            fs.Update(step, false, rodTip, castDir);
        guard = 0;
        int dbg = 0;
        while (fs.Current == FishingSim.Phase.Reeling && guard++ < 6000)
        {
            float d = Mathf.Abs(fs.Sweep - fs.Zone);
            bool press = fs.Zone > 0.01f && d <= fs.ZoneW * 0.15f && fs.Sweep <= fs.Zone;
            fs.Update(step, press, rodTip, castDir);
            if (dbg++ < 40)
                GD.Print($"  reel f{dbg} sweep={fs.Sweep:F3} zone={fs.Zone:F3} d={d:F3} press={press} hits={fs.Hits}");
        }
        GD.Print($"DRIVE end phase={fs.Current} hits={fs.Hits}/{fs.Needed} zoneW={fs.ZoneW}");
        Check(fs.Caught != null, "drove a full catch");
        Check(fs.Current == FishingSim.Phase.Card, "card phase after catch");
        GD.Print($"SIMTEST {passes} checks passed");
        GetTree().Quit();
    }
}
