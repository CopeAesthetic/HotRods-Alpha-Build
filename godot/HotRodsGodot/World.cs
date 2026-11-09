using Godot;
using System;

/// <summary>
/// Builds the HotRods world: animated water, the dock, the pond banks
/// with grass and wet sand, and the prop dressing (trees, rocks, shack,
/// reeds, lilies) - all from the OBJ pack shared with the MonoGame port.
/// </summary>
public partial class World : Node3D
{
    private static readonly Color Sand = new(0.76f, 0.66f, 0.47f);
    private static readonly Color SandD = new(0.70f, 0.60f, 0.42f);
    private static readonly Color Grass = new(0.42f, 0.55f, 0.30f);
    private static readonly Color GrassD = new(0.36f, 0.48f, 0.26f);
    private static readonly Color Wood = new(0.50f, 0.37f, 0.24f);

    public override void _Ready()
    {
        BuildWater();
        BuildDock();
        BuildBanks();
        BuildProps();
    }

    private void BuildWater()
    {
        var shader = GD.Load<Shader>("res://water.gdshader");
        var mat = new ShaderMaterial { Shader = shader };
        mat.SetShaderParameter("shallow", new Color(0.30f, 0.56f, 0.56f));
        mat.SetShaderParameter("deep", new Color(0.05f, 0.20f, 0.30f));
        var pm = new PlaneMesh
        {
            Size = new Vector2(68f, 69f),
            SubdivideWidth = 60,
            SubdivideDepth = 60,
        };
        var mi = new MeshInstance3D { Mesh = pm, MaterialOverride = mat };
        mi.Position = new Vector3(0f, 0.06f, -21.5f);
        AddChild(mi);
    }

    private void BuildDock()
    {
        // 8 planks, then the arm reaching into the water
        for (int i = 0; i < 8; i++)
            Box(new Vector3(-13.8f + i * 1.42f, 0.62f, 0.60f),
                new Vector3(1.24f, 0.13f, 5.6f), Wood);
        for (int i = 0; i < 6; i++)
            Box(new Vector3(-4.55f, 0.62f, -0.55f - i * 1.42f),
                new Vector3(2.5f, 0.13f, 1.24f), Wood);
        Prop("dock_post", new Vector3(-5.85f, 0.0f, -2.05f), 0f, 1.0f);
        Prop("dock_post", new Vector3(-6.05f, 0.0f, -2.15f), 0f, 1.0f);
    }

    private void BuildBanks()
    {
        // south bank: sand slabs + grass ridge behind
        for (int i = 0; i < 12; i++)
        {
            float bx = -40f + i * 7.6f;
            Box(new Vector3(bx, 0.34f + 0.004f * (i % 3), -13.5f),
                new Vector3(8.6f, 0.42f, 5.6f), i % 2 == 0 ? Sand : SandD);
        }
        for (int i = 0; i < 12; i++)
        {
            float bx = -40f + i * 7.6f;
            Box(new Vector3(bx, 0.72f, -16.6f), new Vector3(8.2f, 0.7f, 4.2f),
                i % 2 == 0 ? Grass : GrassD);
        }
        // wet sand at the waterline
        for (int i = 0; i < 12; i++)
        {
            float bx = -40f + i * 7.6f;
            Box(new Vector3(bx, 0.24f, -11.3f), new Vector3(8.4f, 0.14f, 1.6f),
                new Color(0.48f, 0.38f, 0.27f));
        }
        // grass tufts and pebbles along the waterside
        for (int i = 0; i < 36; i++)
        {
            float bx = -40f + (i % 12) * 7.6f + MathF.Sin(i * 12.9898f) * 2.1f;
            float bz = -12.3f + (i * 7 % 5) * 0.5f;
            Box(new Vector3(bx, 0.62f, bz), new Vector3(0.09f, 0.5f, 0.09f),
                i % 2 == 0 ? Grass : GrassD);
            if (i % 3 == 1)
                Box(new Vector3(bx + 0.55f, 0.58f, bz + 0.7f),
                    new Vector3(0.26f, 0.17f, 0.24f), new Color(0.38f, 0.41f, 0.45f));
        }
        // north shore behind the angler
        for (int i = 0; i < 10; i++)
            Box(new Vector3(-36f + i * 8.0f, 0.34f, 15.4f),
                new Vector3(8.6f, 0.46f, 6.4f), i % 2 == 0 ? Sand : SandD);
        for (int i = 0; i < 8; i++)
            Box(new Vector3(-32f + i * 9.0f, 0.78f, 19.2f),
                new Vector3(9.2f, 0.7f, 5.2f), i % 2 == 0 ? Grass : GrassD);
    }

    private void BuildProps()
    {
        // the tree line behind the south bank (pond layout)
        var trees = new (string n, float x, float z, float rot, float s)[]
        {
            ("tree_b", -34f, -20f, 0.9f, 1.4f), ("tree_a", -26f, -21f, 3.2f, 1.2f),
            ("tree_b", -18f, -19.4f, 5.4f, 1.3f), ("tree_a", -10f, -21.2f, 1.8f, 1.1f),
            ("tree_b", -2f, -20.1f, 4.6f, 1.35f), ("tree_a", 6f, -21.4f, 2.4f, 1.25f),
            ("tree_b", 14f, -19.8f, 0.7f, 1.15f), ("tree_a", 22f, -21f, 5.1f, 1.3f),
            ("tree_b", 30f, -20.2f, 3.6f, 1.4f),
        };
        foreach (var (n, x, z, rot, s) in trees)
            Prop(n, new Vector3(x, 0f, z), rot, s);
        Prop("shack", new Vector3(-14.5f, 0f, -4.2f), 0f, 1.0f);
        Prop("rock_a", new Vector3(-8f, 0f, -9.5f), 0.6f, 1.0f);
        Prop("rock_b", new Vector3(10f, 0f, -8.0f), 2.2f, 1.0f);
        Prop("kn_rocks-low", new Vector3(18f, 0f, -5.5f), 1.2f, 1.0f);
        Prop("bush", new Vector3(-30f, 0f, -12.2f), 0f, 1.0f);
        Prop("bush", new Vector3(26f, 0f, -13.0f), 2.8f, 1.0f);
        Prop("boat", new Vector3(-24.0f, 0f, 3.2f), -0.6f, 1.0f);
        Prop("cooler", new Vector3(-9.5f, 0f, 2.4f), 0f, 1.0f);
        Prop("bucket", new Vector3(-8.6f, 0f, 2.7f), 0.5f, 1.0f);
        Prop("rod_rack", new Vector3(-10.9f, 0f, 2.6f), 0f, 1.0f);
        // reeds and lilies on the water
        for (int i = 0; i < 9; i++)
            Prop("reeds", new Vector3(-20f + i * 5f, 0f, -7.0f + (i % 3) * 0.8f),
                 (i * 0.7f) % 6.28f, 1.0f);
        for (int i = 0; i < 6; i++)
            Prop("lily", new Vector3(-24f + i * 6.5f, 0f, -3.5f + (i % 2) * 1.6f),
                 (i * 1.3f) % 6.28f, 1.0f);
    }

    private void Box(Vector3 pos, Vector3 size, Color col)
    {
        var mesh = new BoxMesh { Size = size };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = col,
            Roughness = 0.9f,
        };
        var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat };
        mi.Position = pos;
        AddChild(mi);
    }

    private void Prop(string name, Vector3 pos, float rotY, float scale)
    {
        var mesh = GD.Load<Mesh>($"res://assets/{name}.obj");
        if (mesh == null)
        {
            GD.PushWarning("missing asset " + name);
            return;
        }
        var mi = new MeshInstance3D { Mesh = mesh };
        mi.Position = pos;
        mi.RotationDegrees = new Vector3(0f, Mathf.RadToDeg(rotY), 0f);
        mi.Scale = Vector3.One * scale;
        AddChild(mi);
    }
}
