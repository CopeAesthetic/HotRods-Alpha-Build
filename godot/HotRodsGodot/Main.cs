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
    public override void _Ready()
    {
        if (OS.GetCmdlineUserArgs().Contains("--shot"))
            CallDeferred(nameof(TakeShot));
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
