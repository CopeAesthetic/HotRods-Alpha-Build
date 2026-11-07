namespace HotRodsSharp;

/// <summary>
/// Phase-2 port: the full pond scene - props from the Blender OBJs, dock and
/// shore built from the Python layout tables, animated water, the fisherman
/// model, and three camera modes (third / first / free, TAB to cycle).
/// </summary>
public class HotRodsGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private BasicEffect _effect = null!;
    private BasicEffect _effectCullNone = null!;

    // static world
    private readonly List<VertexPositionColor> _staticVerts = new();
    private int _staticTriCount;

    // water, rebuilt per frame
    private float _t;

    // camera
    private int _mode;                    // 0 third, 1 first, 2 free
    private float _yaw, _pitch = 0.45f, _dist = 2.8f;
    private Matrix _view = Matrix.Identity, _proj = Matrix.Identity;
    private readonly Vector3 _angler = new(-3.2f, 1.0f, 1.5f);
    private bool _tabHeld;

    // palette
    private static readonly Color Sand = new(200, 186, 150);
    private static readonly Color SandD = new(168, 152, 118);
    private static readonly Color Grass = new(132, 144, 92);
    private static readonly Color GrassD = new(110, 122, 76);
    private static readonly Color Wood = new(128, 94, 62);
    private static readonly Color Skin = new(206, 158, 116);
    private static readonly Color Vest = new(96, 110, 88);
    private static readonly Color Wader = new(78, 92, 74);
    private static readonly Color Shirt = new(150, 132, 108);
    private static readonly Color Hat = new(204, 178, 130);
    private static readonly Color Boot = new(58, 48, 40);
    private static readonly Color Beard = new(96, 74, 52);
    private static readonly Color Face = new(216, 170, 126);
    private static readonly Color FaceD = new(202, 158, 116);
    private static readonly Color Dark = new(38, 34, 30);
    private static readonly Color Nose = new(198, 140, 100);
    private static readonly Color Mouth = new(88, 52, 38);
    private static readonly Color RodDark = new(176, 148, 96);
    private static readonly Color RodLight = new(198, 170, 116);

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

    private static readonly Dictionary<string, float> Ground = new()
    {
        ["land"] = 0.40f, ["water"] = 0.02f, ["dock"] = 0.72f,
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

    // lily and reed dressing, seeded like layouts.py
    private static readonly (string name, float x, float z, float rot, float scale)[] Lilies =
        Seed(5, 16, -8.5f, 8.5f, -9.0f, -1.5f, 0.7f, 1.15f, "lily");
    private static readonly (string name, float x, float z, float rot, float scale)[] Reeds =
        Seed(7, 22, -9.0f, 9.0f, -10.0f, -1.0f, 0.8f, 1.5f, "reeds");

    private static (string, float, float, float, float)[] Seed(
        int seed, int n, float x0, float x1, float z0, float z1,
        float s0, float s1, string name)
    {
        var rng = new Random(seed);
        var arr = new (string, float, float, float, float)[n];
        for (int i = 0; i < n; i++)
            arr[i] = (name,
                      x0 + (float)rng.NextDouble() * (x1 - x0),
                      z0 + (float)rng.NextDouble() * (z1 - z0),
                      (float)rng.NextDouble() * MathF.Tau,
                      s0 + (float)rng.NextDouble() * (s1 - s0));
        return arr;
    }

    public HotRodsGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "Hot Rods (C# port) - TAB cycles camera";
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
            FogEnd = 42f,
        };
        _effectCullNone = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
            FogEnabled = true,
            FogColor = new Vector3(0.55f, 0.62f, 0.72f),
            FogStart = 14f,
            FogEnd = 42f,
        };

        var s = _staticVerts;
        foreach (var (name, x, z, rot, scale) in PondLayout)
            AddProp(s, name, x, z, rot, scale);
        foreach (var (name, x, z, rot, scale) in Reeds)
            AddProp(s, name, x, z, rot, scale, -0.05f);
        foreach (var (name, x, z, rot, scale) in Lilies)
            AddProp(s, name, x, z, rot, scale, 0.02f);
        BuildDock(s);
        BuildShore(s);
        BuildAngler(s);
        _staticTriCount = s.Count / 3;
    }

    private void AddProp(List<VertexPositionColor> s, string name, float x,
                         float z, float rot, float scale, float yBias = 0f)
    {
        var (_, col, bscale, ground) = Manifest.First(m => m.name == name);
        var mesh = ObjMesh.Load(
            Path.Combine(AppContext.BaseDirectory, "Assets", name + ".obj"),
            scale * bscale);
        mesh.Transform(rot, new Vector3(x, Ground[ground] + yBias, z));
        foreach (var (a, b, c) in mesh.Tris)
            Prims.AddTri(s, mesh.Verts[a], mesh.Verts[b], mesh.Verts[c], col);
    }

    private void BuildDock(List<VertexPositionColor> s)
    {
        // layouts.dock(0): 8 planks from x -13.8, depth 5.6, 6-board arm
        for (int i = 0; i < 8; i++)
            Prims.AddBox(s, new Vector3(-13.8f + i * 1.42f, 0.62f, 0.60f),
                         1.24f, 0.13f, 5.6f, Wood);
        for (int i = 0; i < 6; i++)
            Prims.AddBox(s, new Vector3(-4.55f, 0.62f, -0.55f - i * 1.42f),
                         2.5f, 0.13f, 1.24f, Wood);
    }

    private void BuildShore(List<VertexPositionColor> s)
    {
        // far bank, raised above the swell and overlapping to close the ring
        for (int i = 0; i < 12; i++)
        {
            float bx = -40f + i * 7.6f;
            Prims.AddBox(s, new Vector3(bx, 0.34f + 0.004f * (i % 3), -13.5f),
                         8.6f, 0.42f, 5.6f, i % 2 == 0 ? Sand : SandD);
        }
        // grass shelf behind the sand
        for (int i = 0; i < 12; i++)
        {
            float bx = -40f + i * 7.6f;
            Prims.AddBox(s, new Vector3(bx, 0.72f, -16.6f), 8.4f, 0.7f, 4.2f,
                         i % 2 == 0 ? Grass : GrassD);
        }
        // north shore behind the angler closes the pond
        for (int i = 0; i < 10; i++)
        {
            float bx = -36f + i * 8.0f;
            Prims.AddBox(s, new Vector3(bx, 0.34f + 0.004f * (i % 3), 15.4f),
                         8.6f, 0.46f, 6.4f, i % 2 == 0 ? Sand : SandD);
        }
        for (int i = 0; i < 8; i++)
        {
            float bx = -32f + i * 9.0f;
            Prims.AddBox(s, new Vector3(bx, 0.78f, 19.2f), 9.2f, 0.7f, 5.2f,
                         i % 2 == 0 ? Grass : GrassD);
        }
    }

    private void BuildAngler(List<VertexPositionColor> s)
    {
        // waders and boots dangling over the dock edge
        Prims.AddSegment(s, new Vector3(-3.18f, 0.70f, 1.38f),
                         new Vector3(-2.97f, 0.16f, 1.28f), 0.095f, Wader);
        Prims.AddSegment(s, new Vector3(-2.97f, 0.16f, 1.28f),
                         new Vector3(-2.92f, 0.00f, 1.26f), 0.075f, Boot);
        Prims.AddSegment(s, new Vector3(-3.18f, 0.70f, 1.72f),
                         new Vector3(-2.90f, 0.16f, 1.66f), 0.095f, Wader);
        Prims.AddSegment(s, new Vector3(-2.90f, 0.16f, 1.66f),
                         new Vector3(-2.85f, 0.00f, 1.64f), 0.075f, Boot);
        // torso: shirt under the padded vest
        Prims.AddSegment(s, new Vector3(-3.26f, 0.66f, 1.55f),
                         new Vector3(-3.30f, 1.06f, 1.51f), 0.150f, Shirt);
        Prims.AddSegment(s, new Vector3(-3.30f, 1.06f, 1.51f),
                         new Vector3(-3.34f, 1.22f, 1.50f), 0.175f, Vest);
        // neck + collar + shoulder bar
        Prims.AddDisc(s, new Vector3(-3.335f, 1.225f, 1.495f), 0.135f, Vest, 8);
        Prims.AddSegment(s, new Vector3(-3.33f, 1.225f, 1.495f),
                         new Vector3(-3.32f, 1.30f, 1.49f), 0.100f, Skin);
        Prims.AddSegment(s, new Vector3(-3.44f, 1.19f, 1.455f),
                         new Vector3(-3.22f, 1.19f, 1.57f), 0.085f, Vest);
        // head + hat + beard + face plate
        Prims.AddSegment(s, new Vector3(-3.32f, 1.30f, 1.49f),
                         new Vector3(-3.31f, 1.45f, 1.48f), 0.115f, Skin, 6);
        Prims.AddDisc(s, new Vector3(-3.31f, 1.44f, 1.48f), 0.24f, Hat, 8);
        Prims.AddCone(s, new Vector3(-3.31f, 1.62f, 1.48f),
                      new Vector3(-3.31f, 1.44f, 1.48f), 0.145f, Hat, 7);
        Prims.AddSegment(s, new Vector3(-3.30f, 1.26f, 1.545f),
                         new Vector3(-3.30f, 1.33f, 1.545f), 0.10f, Beard);
        Prims.AddBox(s, new Vector3(-3.305f, 1.35f, 1.545f), 0.05f, 0.15f, 0.17f, Face);
        Prims.AddBox(s, new Vector3(-3.275f, 1.385f, 1.49f), 0.02f, 0.035f, 0.04f, Dark);
        Prims.AddBox(s, new Vector3(-3.275f, 1.385f, 1.60f), 0.02f, 0.035f, 0.04f, Dark);
        Prims.AddBox(s, new Vector3(-3.28f, 1.345f, 1.545f), 0.055f, 0.055f, 0.05f, Nose);
        Prims.AddBox(s, new Vector3(-3.275f, 1.305f, 1.545f), 0.02f, 0.03f, 0.115f, Mouth);
        // rod arm + left hand riding the blank, and the rod itself
        var hand = new Vector3(-3.03f, 1.00f, 1.57f);
        var tip = new Vector3(-2.70f, 1.70f, -0.40f);
        Prims.AddSegment(s, new Vector3(-3.23f, 1.19f, 1.56f),
                         new Vector3(-3.12f, 1.06f, 1.62f), 0.065f, Vest);
        Prims.AddSegment(s, new Vector3(-3.12f, 1.06f, 1.62f), hand, 0.055f, Skin);
        var lhand = hand + (tip - hand) * 0.35f;
        Prims.AddSegment(s, new Vector3(-3.43f, 1.19f, 1.46f),
                         lhand * 0.5f + new Vector3(-3.43f, 1.19f, 1.46f) * 0.5f,
                         0.065f, Vest);
        Prims.AddSegment(s, lhand * 0.5f + new Vector3(-3.43f, 1.19f, 1.46f) * 0.5f,
                         lhand, 0.055f, Skin);
        var mid = hand + (tip - hand) * 0.55f;
        Prims.AddSegment(s, hand, mid, 0.030f, RodDark, 4);
        Prims.AddSegment(s, mid, tip, 0.021f, RodLight, 4);
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _t += dt;
        var kb = Keyboard.GetState();
        if (kb.IsKeyDown(Keys.Escape)) Exit();
        if (kb.IsKeyDown(Keys.Tab) && !_tabHeld)
        {
            _mode = (_mode + 1) % 3;
            _tabHeld = true;
        }
        if (kb.IsKeyUp(Keys.Tab)) _tabHeld = false;

        if (_mode == 0)                    // third: locked arc around the angler
        {
            if (kb.IsKeyDown(Keys.Left)) _yaw = MathF.Max(-0.95f, _yaw - 1.1f * dt);
            if (kb.IsKeyDown(Keys.Right)) _yaw = MathF.Min(0.95f, _yaw + 1.1f * dt);
            if (kb.IsKeyDown(Keys.Up)) _pitch = MathF.Min(0.45f, _pitch + 0.6f * dt);
            if (kb.IsKeyDown(Keys.Down)) _pitch = MathF.Max(-0.30f, _pitch - 0.6f * dt);
            _dist = 2.8f;
        }
        else if (_mode == 2)               // free: full orbit, longer arm
        {
            if (kb.IsKeyDown(Keys.Left)) _yaw -= 1.1f * dt;
            if (kb.IsKeyDown(Keys.Right)) _yaw += 1.1f * dt;
            if (kb.IsKeyDown(Keys.Up)) _pitch = MathF.Min(0.9f, _pitch + 0.6f * dt);
            if (kb.IsKeyDown(Keys.Down)) _pitch = MathF.Max(-0.2f, _pitch - 0.6f * dt);
            _dist = Math.Clamp(_dist, 2f, 30f);
        }

        if (_mode == 1)                    // first person: at the head, toward water
        {
            _view = Matrix.CreateLookAt(new Vector3(-3.31f, 1.47f, 1.52f),
                                        new Vector3(-1.5f, 0.6f, -9.0f), Vector3.Up);
        }
        else
        {
            var eye = new Vector3(
                _angler.X + _dist * MathF.Cos(_pitch) * MathF.Sin(_yaw),
                _angler.Y + _dist * MathF.Sin(_pitch),
                _angler.Z + _dist * MathF.Cos(_pitch) * MathF.Cos(_yaw));
            _view = Matrix.CreateLookAt(eye, _angler, Vector3.Up);
        }
        _proj = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.PiOver4, GraphicsDevice.Viewport.AspectRatio, 0.1f, 250f);
        base.Update(gameTime);
    }

    private List<VertexPositionColor> WaterVerts()
    {
        var v = new List<VertexPositionColor>();
        int cols = 8, rows = 7;
        float x0 = -34f, x1 = 34f, z0 = -56f, z1 = 13f;
        for (int j = 0; j < rows; j++)
            for (int i = 0; i < cols; i++)
            {
                float xt0 = (float)i / cols, xt1 = (float)(i + 1) / cols;
                float zt0 = (float)j / rows, zt1 = (float)(j + 1) / rows;
                Vector3 P(float xt, float zt)
                {
                    float x = x0 + (x1 - x0) * xt;
                    float z = z0 + (z1 - z0) * zt;
                    float y = 0.06f + 0.05f * MathF.Sin(_t * 1.6f + x * 0.35f + z * 0.25f);
                    return new Vector3(x, y, z);
                }
                var a = P(xt0, zt0);
                var b = P(xt1, zt0);
                var c = P(xt1, zt1);
                var d = P(xt0, zt1);
                float depth = Math.Clamp((a.Z - z0) / (z1 - z0), 0f, 1f);
                var col = Color.Lerp(new Color(86, 140, 150), new Color(44, 92, 110), depth);
                var colD = new Color((byte)(col.R * 0.95f), (byte)(col.G * 0.95f),
                                     (byte)(col.B * 0.95f));
                Prims.AddTri(v, a, b, c, col);
                Prims.AddTri(v, a, c, d, colD);
            }
        return v;
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(84, 108, 130));
        var water = WaterVerts();
        foreach (var effect in new[] { _effect, _effectCullNone })
        {
            effect.View = _view;
            effect.Projection = _proj;
            effect.World = Matrix.Identity;
        }

        // water first, then the static world over it
        DrawVerts(water, _effect);
        DrawVerts(_staticVerts.GetRange(0, _staticTriCount * 3), _effectCullNone);
        base.Draw(gameTime);
    }

    private void DrawVerts(List<VertexPositionColor> verts, BasicEffect effect)
    {
        if (verts.Count == 0)
            return;
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList,
                                              verts.ToArray(), 0, verts.Count / 3);
        }
    }
}
