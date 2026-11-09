using Godot;
using System;

/// <summary>
/// Builds the HotRods world: animated water, the dock, the pond banks
/// with grass and wet sand, and the prop dressing (trees, rocks, shack,
/// reeds, lilies) - all from the OBJ pack shared with the MonoGame port.
/// </summary>
public partial class World : Node3D
{
    public ShaderMaterial WaterMat = null!;

    private static readonly Color Sand = new(0.76f, 0.66f, 0.47f);
    private static readonly Color SandD = new(0.70f, 0.60f, 0.42f);
    private static readonly Color Grass = new(0.42f, 0.55f, 0.30f);
    private static readonly Color GrassD = new(0.36f, 0.48f, 0.26f);
    private static readonly Color Wood = new(0.50f, 0.37f, 0.24f);

    private Node3D _land = null!;

    private static readonly (int n, float x0, float z, float dz, int arm, float armX)[] Docks =
    {
        (8, -13.8f, 0.60f, 5.6f, 6, -4.55f),
        (5, -9.20f, 0.35f, 4.4f, 5, -3.20f),
        (6, -11.0f, 0.50f, 5.0f, 5, -3.80f),
        (4, -7.60f, 0.30f, 4.0f, 4, -2.60f),
    };

    private static readonly (float x0, int n, float z, float spacing, float w)[] Banks =
    {
        (-40f, 12, -13.5f, 7.6f, 8.0f),
        (-46f, 13, -18.0f, 7.6f, 8.0f),
        (-34f, 10, -11.0f, 7.2f, 9.0f),
        (-30f, 9, -9.0f, 7.2f, 8.4f),
    };

    public override void _Ready()
    {
        BuildWater();
        _land = new Node3D { Name = "Land" };
        AddChild(_land);
    }

    /// <summary>Clears and rebuilds dock, banks and props for a water.</summary>
    public void Rebuild(int spot)
    {
        foreach (var c in _land.GetChildren())
            c.QueueFree();
        BuildDock(spot);
        BuildBanks(spot);
        BuildProps(spot);
    }

    private void BuildWater()
    {
        var shader = GD.Load<Shader>("res://water.gdshader");
        WaterMat = new ShaderMaterial { Shader = shader };
        var mat = WaterMat;
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

    private void BuildDock(int spot)
    {
        var dk = Docks[spot];
        for (int i = 0; i < dk.n; i++)
            Box(new Vector3(dk.x0 + i * 1.42f, 0.62f, dk.z),
                new Vector3(1.24f, 0.13f, dk.dz), Wood);
        for (int i = 0; i < dk.arm; i++)
            Box(new Vector3(dk.armX, 0.62f, -0.55f - i * 1.42f),
                new Vector3(2.5f, 0.13f, 1.24f), Wood);
    }

    private void BuildBanks(int spot)
    {
        var bk = Banks[spot];
        for (int i = 0; i < bk.n; i++)
        {
            float bx = bk.x0 + i * bk.spacing;
            Box(new Vector3(bx, 0.34f + 0.004f * (i % 3), bk.z),
                new Vector3(bk.w + 0.6f, 0.42f, 5.6f), i % 2 == 0 ? Sand : SandD);
        }
        for (int i = 0; i < bk.n; i++)
        {
            float bx = bk.x0 + i * bk.spacing;
            Box(new Vector3(bx, 0.72f, bk.z - 3.1f), new Vector3(bk.w + 0.2f, 0.7f, 4.2f),
                i % 2 == 0 ? Grass : GrassD);
        }
        // wet sand at the waterline
        for (int i = 0; i < bk.n; i++)
        {
            float bx = bk.x0 + i * bk.spacing;
            Box(new Vector3(bx, 0.24f, bk.z + 2.2f), new Vector3(bk.w + 0.4f, 0.14f, 1.6f),
                new Color(0.48f, 0.38f, 0.27f));
        }
        // grass tufts and pebbles along the waterside
        for (int i = 0; i < bk.n * 3; i++)
        {
            float bx = bk.x0 + (i % bk.n) * bk.spacing + MathF.Sin(i * 12.9898f) * bk.spacing * 0.28f;
            float bz = bk.z + 1.2f + (i * 7 % 5) * 0.5f;
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

    private static readonly (string, float, float, float, float)[][] Layouts =
    {
        // Duckweed Pond
        new (string, float, float, float, float)[]
        {
            ("tree_b", -34f, -20f, 0.9f, 1.40f), ("tree_a", -26f, -21f, 3.2f, 1.20f),
            ("tree_b", -18f, -19.4f, 5.4f, 1.30f), ("tree_a", -10f, -21.2f, 1.8f, 1.10f),
            ("tree_b", -2f, -20.1f, 4.6f, 1.35f), ("tree_a", 6f, -21.4f, 2.4f, 1.25f),
            ("tree_b", 14f, -19.8f, 0.7f, 1.15f), ("tree_a", 22f, -21f, 5.1f, 1.30f),
            ("tree_b", 30f, -20.2f, 3.6f, 1.40f), ("shack", -14.5f, -4.2f, 0f, 1f),
            ("dock_post", -5.85f, -2.05f, 0f, 1f), ("dock_post", -6.05f, -2.15f, 0f, 1f),
            ("rod_rack", -7.10f, 2.60f, 0.2f, 1f), ("cooler", -6.80f, 2.90f, 0.1f, 1f),
            ("bucket", -6.10f, 3.05f, 0f, 1f), ("boat", 3.60f, -3.20f, 1.35f, 1f),
            ("rock_a", -20f, -15.4f, 0.8f, 1f), ("rock_b", -11.5f, -16.2f, 2.1f, 1f),
            ("rock_a", 12f, -15f, 4.2f, 0.85f), ("bush", -30f, -14.6f, 1.4f, 1f),
            ("bush", 18f, -14.2f, 5.0f, 1.15f), ("bush", -4.5f, -15.8f, 3.3f, 0.90f),
            ("kn_fence", -8f, 5.6f, 0f, 1f), ("kn_tent", 12f, 3.2f, 5.6f, 1f),
            ("kn_plant", -24f, -13.2f, 1.1f, 1f), ("kn_plant", 16f, -12.6f, 4.0f, 0.90f),
        },
        // Bass & Bake Lake
        new (string, float, float, float, float)[]
        {
            ("tree_b", -34f, -20f, 0.9f, 1.40f), ("tree_a", -26f, -21f, 3.2f, 1.20f),
            ("tree_b", -18f, -19.4f, 5.4f, 1.30f), ("tree_a", -10f, -21.2f, 1.8f, 1.10f),
            ("tree_b", -2f, -20.1f, 4.6f, 1.35f), ("tree_a", 6f, -21.4f, 2.4f, 1.25f),
            ("tree_b", 14f, -19.8f, 0.7f, 1.15f), ("tree_a", 22f, -21f, 5.1f, 1.30f),
            ("tree_b", 30f, -20.2f, 3.6f, 1.40f), ("dock_post", -6.05f, -2.15f, 0f, 1f),
            ("rod_rack", -7.10f, 2.60f, 0.2f, 1f), ("kn_bridge", 0f, -5.5f, 1.57f, 1f),
            ("kn_tree", -20f, -14.5f, 2.2f, 1f), ("kn_plant", 14f, -13f, 5.2f, 1f),
        },
        // Munchies Marsh
        new (string, float, float, float, float)[]
        {
            ("tree_b", -22f, -13f, 1.1f, 0.95f), ("tree_a", -16f, -12.4f, 3.8f, 1.05f),
            ("tree_b", -10f, -13.2f, 5.2f, 0.90f), ("tree_a", -4f, -12.6f, 2.2f, 1f),
            ("tree_b", 2f, -13.1f, 4.4f, 0.95f), ("tree_a", 8f, -12.3f, 0.6f, 1.05f),
            ("dock_post", -4.60f, -2.05f, 0f, 1f), ("kn_plant", -14f, -10.4f, 2.4f, 1.10f),
            ("kn_plant", -8f, -10f, 4.4f, 1f), ("kn_plant", -2f, -10.6f, 0.8f, 1.15f),
            ("kn_plant", 4f, -9.8f, 5.6f, 1f), ("kn_rocks-low", -20f, -9.4f, 1.9f, 1f),
        },
        // Tokers Trench
        new (string, float, float, float, float)[]
        {
            ("tree_a", -20f, -11f, 1.6f, 1.45f), ("tree_b", -8f, -11.6f, 4.0f, 1.55f),
            ("tree_a", 6f, -10.8f, 2.9f, 1.40f), ("tree_b", 18f, -11.4f, 5.6f, 1.50f),
            ("kn_rocks-high", -14f, -8.8f, 3.1f, 1f), ("kn_rocks-high", 10f, -8.4f, 5.9f, 0.85f),
            ("kn_flag", 20f, -6.5f, 2.2f, 1f),
        },
    };

    private void BuildProps(int spot)
    {
        foreach (var (n, x, z, rot, s) in Layouts[spot])
            Prop(n, new Vector3(x, 0f, z), rot, s);
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
        BoxInto(_land, pos, size, col);
    }

    private static void BoxInto(Node3D parent, Vector3 pos, Vector3 size, Color col)
    {
        var mesh = new BoxMesh { Size = size };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = col,
            Roughness = 0.9f,
        };
        var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat };
        mi.Position = pos;
        parent.AddChild(mi);
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
        _land.AddChild(mi);
    }
}
