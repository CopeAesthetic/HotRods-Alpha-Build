namespace HotRodsSharp;

/// <summary>
/// Phase-1 proof: load the Blender props (manifest subset), place them with the
/// layout tables, and draw them flat-shaded with an orbit camera. This mirrors
/// the Python renderer's look: flat colours, no textures, fog by depth.
/// </summary>
public class HotRodsGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private BasicEffect _effect = null!;
    private readonly List<(ObjMesh mesh, Color color)> _props = new();
    private float _yaw, _pitch = 0.35f, _dist = 14f;
    private Matrix _view = Matrix.Identity, _proj = Matrix.Identity;
    private readonly Vector3 _focus = new(-3.2f, 1.0f, 1.5f);

    // (name, colour, base scale, ground) - a subset of assets/manifest.py
    private static readonly (string name, Color col, float scale, string ground)[] Manifest =
    {
        ("tree_a", new Color(58, 84, 52), 0.30f, "land"),
        ("tree_b", new Color(48, 72, 46), 0.30f, "land"),
        ("reeds", new Color(86, 104, 66), 0.35f, "water"),
        ("lily", new Color(52, 96, 66), 1.00f, "water"),
        ("shack", new Color(120, 92, 66), 1.00f, "land"),
        ("dock_post", new Color(96, 74, 52), 1.00f, "dock"),
        ("rod_rack", new Color(110, 84, 58), 1.00f, "dock"),
        ("cooler", new Color(200, 60, 48), 1.00f, "dock"),
        ("bucket", new Color(120, 128, 140), 1.00f, "dock"),
        ("boat", new Color(140, 150, 120), 1.00f, "water"),
        ("smoker_pipe", new Color(150, 196, 210), 0.55f, "dock"),
        ("smoker_beaker_bong", new Color(140, 200, 180), 0.55f, "dock"),
    };

    // (asset, x, z, rot, scale) - the pond layout from game/layouts.py
    private static readonly (string name, float x, float z, float rot, float scale)[] PondLayout =
    {
        ("tree_b", -34.0f, -20.0f, 0.9f, 1.40f),
        ("tree_a", -26.0f, -21.0f, 3.2f, 1.20f),
        ("tree_b", -18.0f, -19.4f, 5.4f, 1.30f),
        ("tree_a", -10.0f, -21.2f, 1.8f, 1.10f),
        ("tree_b", -2.0f, -20.1f, 4.6f, 1.35f),
        ("tree_a", 6.0f, -21.4f, 2.4f, 1.25f),
        ("tree_b", 14.0f, -19.8f, 0.7f, 1.15f),
        ("tree_a", 22.0f, -21.0f, 5.1f, 1.30f),
        ("tree_b", 30.0f, -20.2f, 3.6f, 1.40f),
        ("shack", -14.5f, -4.2f, 0.0f, 1.00f),
        ("dock_post", -5.85f, -2.05f, 0.0f, 1.00f),
        ("dock_post", -6.05f, -2.15f, 0.0f, 1.00f),
        ("rod_rack", -7.10f, 2.60f, 0.20f, 1.00f),
        ("cooler", -6.80f, 2.90f, 0.10f, 1.00f),
        ("bucket", -6.10f, 3.05f, 0.0f, 1.00f),
    };

    private static readonly Dictionary<string, float> Ground = new()
    {
        ["land"] = 0.40f, ["water"] = 0.02f, ["dock"] = 0.72f,
    };

    public HotRodsGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "Hot Rods (C# port)";
    }

    protected override void LoadContent()
    {
        _effect = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
            FogEnabled = true,
            FogColor = new Vector3(0.55f, 0.62f, 0.72f),
            FogStart = 14f,
            FogEnd = 40f,
        };
        foreach (var (name, x, z, rot, scale) in PondLayout)
        {
            var (_, col, bscale, ground) = Manifest.First(m => m.name == name);
            var mesh = ObjMesh.Load(Path.Combine("Assets", name + ".obj"),
                                    scale * bscale);
            mesh.Transform(rot, new Vector3(x, Ground[ground], z));
            _props.Add((mesh, col));
        }
    }

    protected override void Update(GameTime gameTime)
    {
        var kb = Keyboard.GetState();
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (kb.IsKeyDown(Keys.Left)) _yaw -= 1.2f * dt;
        if (kb.IsKeyDown(Keys.Right)) _yaw += 1.2f * dt;
        if (kb.IsKeyDown(Keys.Up)) _pitch = Math.Min(0.9f, _pitch + 0.5f * dt);
        if (kb.IsKeyDown(Keys.Down)) _pitch = Math.Max(-0.1f, _pitch - 0.5f * dt);
        if (kb.IsKeyDown(Keys.Escape)) Exit();

        var eye = new Vector3(
            _focus.X + _dist * MathF.Cos(_pitch) * MathF.Sin(_yaw),
            _focus.Y + _dist * MathF.Sin(_pitch),
            _focus.Z + _dist * MathF.Cos(_pitch) * MathF.Cos(_yaw));
        _view = Matrix.CreateLookAt(eye, _focus, Vector3.Up);
        _proj = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.PiOver4, GraphicsDevice.Viewport.AspectRatio, 0.1f, 200f);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(84, 108, 130));
        _effect.View = _view;
        _effect.Projection = _proj;
        _effect.World = Matrix.Identity;

        var verts = new List<VertexPositionColor>();
        foreach (var (mesh, col) in _props)
        {
            foreach (var (a, b, c) in mesh.Tris)
            {
                var n = Vector3.Normalize(Vector3.Cross(
                    mesh.Verts[b] - mesh.Verts[a], mesh.Verts[c] - mesh.Verts[a]));
                var k = 0.62f + 0.38f * MathF.Max(0f, Vector3.Dot(n, Vector3.Normalize(new Vector3(0.4f, 0.8f, -0.3f))));
                var lit = new Color((byte)(col.R * k), (byte)(col.G * k), (byte)(col.B * k));
                verts.Add(new VertexPositionColor(mesh.Verts[a], lit));
                verts.Add(new VertexPositionColor(mesh.Verts[b], lit));
                verts.Add(new VertexPositionColor(mesh.Verts[c], lit));
            }
        }

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList,
                                              verts.ToArray(), 0, verts.Count / 3);
        }
        base.Draw(gameTime);
    }
}
