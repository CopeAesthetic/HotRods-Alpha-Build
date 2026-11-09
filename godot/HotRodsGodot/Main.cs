using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Godot port of HotRods. Scaffold phase: a sky, sun, water sheet, dock
/// and first-person camera to prove the render pipeline; the gameplay
/// systems get ported in on top.
/// </summary>
public partial class Main : Node3D
{
    private bool _dragging;
    private float _yawDeg, _pitchDeg;

    public override void _Ready()
    {
        if (OS.GetCmdlineUserArgs().Contains("--shot"))
            CallDeferred(nameof(TakeShot));
        if (OS.GetCmdlineUserArgs().Contains("--simtest"))
            CallDeferred(nameof(RunSimTests));
    }

    /// <summary>Headless checks over the ported core: content, state, roll,
    /// chill/couch, munchie, travel.</summary>
    private void RunSimTests()
    {
        HotRodsSharp.GameData.Load();
        int passes = 0;
        void Check(bool ok, string what)
        {
            if (!ok)
                GD.PushError("FAIL: " + what);
            else
                passes++;
        }

        // content loaded with the same JSON the MonoGame build uses
        Check(HotRodsSharp.GameData.SpeciesList.Length == 32, "32 species");
        Check(HotRodsSharp.GameData.Rods.Length >= 4, "rods loaded");
        Check(HotRodsSharp.GameData.Munchies.Length == 9, "munchies loaded");

        // state + roll
        var st = HotRodsSharp.GameState.New(1);
        st.WeedCounts["homegrown"] = 5;
        var sim = new HotRodsSharp.FishingSim(st);
        for (int i = 0; i < 200; i++)
        {
            var f = sim.Roll();
            Check(f != null && f.Id.Length > 0, "roll " + i);
        }

        // chill + couch + munchie
        st.Chill = 0f;
        var msg = st.ApplyToke();
        Check(st.Chill >= 40f && st.Chill <= 44f, "toke amount");
        st.Chill = 78f;
        msg = st.ApplyToke();
        Check(st.Couch == 2.6f && msg.Contains("couch"), "couch lock");
        st.Munchie = 100f;
        Check(!st.CanToke().ok, "munchie blocks smoke");

        GD.Print($"SIMTEST {passes} checks passed");
        GetTree().Quit();
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
            _dragging = mb.Pressed;
        if (e is InputEventMouseMotion mm && _dragging)
        {
            _yawDeg = Mathf.Clamp(_yawDeg - mm.Relative.X * 0.18f, -50f, 50f);
            _pitchDeg = Mathf.Clamp(_pitchDeg + mm.Relative.Y * 0.18f, -25f, 30f);
            GetNode<Camera3D>("Camera3D").RotationDegrees =
                new Vector3(_pitchDeg, -90f + _yawDeg, 0f);
        }
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
}
