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
    private int _spot;
    private readonly GameState _state = GameState.Load();
    private readonly FishingSim _fishing;
    private readonly SmokingSim _smoking = new();
    private bool _eHeld, _qHeld, _xHeld, _lbHeld, _rbHeld;
    private bool _wasSmoking;
    private BitmapFont _font = null!;
    private SpriteBatch _batch = null!;
    private Texture2D _pixel = null!;
    private readonly bool[] _spotHeld = new bool[4];

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

    // per-water layout tables from game/layouts.py
    private static readonly (string name, float x, float z, float rot, float scale)[][] Layouts =
    {
        // Duckweed Pond
        new (string, float, float, float, float)[]
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
        },
        // Bass & Bake Lake
        new (string, float, float, float, float)[]
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
            ("dock_post", -6.05f, -2.15f, 0.0f, 1.00f),
            ("rod_rack", -7.10f, 2.60f, 0.20f, 1.00f),
        },
        // Munchies Marsh
        new (string, float, float, float, float)[]
        {
            ("tree_b", -22.0f, -13.0f, 1.1f, 0.95f),
            ("tree_a", -16.0f, -12.4f, 3.8f, 1.05f),
            ("tree_b", -10.0f, -13.2f, 5.2f, 0.90f),
            ("tree_a", -4.0f, -12.6f, 2.2f, 1.00f),
            ("tree_b", 2.0f, -13.1f, 4.4f, 0.95f),
            ("tree_a", 8.0f, -12.3f, 0.6f, 1.05f),
            ("dock_post", -4.60f, -2.05f, 0.0f, 1.00f),
        },
        // Tokers Trench
        new (string, float, float, float, float)[]
        {
            ("tree_a", -20.0f, -11.0f, 1.6f, 1.45f),
            ("tree_b", -8.0f, -11.6f, 4.0f, 1.55f),
            ("tree_a", 6.0f, -10.8f, 2.9f, 1.40f),
            ("tree_b", 18.0f, -11.4f, 5.6f, 1.50f),
        },
    };

    // dock planks per water: n, x0, z, depth, arm boards, arm x
    private static readonly (int n, float x0, float z, float dz, int arm, float armX)[] Docks =
    {
        (8, -13.8f, 0.60f, 5.6f, 6, -4.55f),
        (5, -9.20f, 0.35f, 4.4f, 5, -3.20f),
        (6, -11.0f, 0.50f, 5.0f, 5, -3.80f),
        (4, -7.60f, 0.30f, 4.0f, 4, -2.60f),
    };

    // far bank per water: x0, slabs, z, spacing, width
    private static readonly (float x0, int n, float z, float spacing, float w)[] Banks =
    {
        (-40f, 12, -13.5f, 7.6f, 8.0f),
        (-46f, 13, -18.0f, 7.6f, 8.0f),
        (-34f, 10, -11.0f, 7.2f, 9.0f),
        (-30f, 9, -9.0f, 7.2f, 8.4f),
    };

    // water dressing density per water: (lilies, reeds)
    private static readonly (int lilies, int reeds)[] Density =
    {
        (9, 12), (6, 8), (10, 20), (0, 4),
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
        _fishing = new FishingSim(_state);
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

        BuildWorld();
        _batch = new SpriteBatch(GraphicsDevice);
        _font = new BitmapFont(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
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

    private void BuildWorld()
    {
        var s = _staticVerts;
        s.Clear();
        foreach (var (name, x, z, rot, scale) in Layouts[_spot])
            AddProp(s, name, x, z, rot, scale);
        var (nlily, nreed) = Density[_spot];
        for (int i = 0; i < nreed; i++)
            AddProp(s, "reeds", Reeds[i].x, Reeds[i].z, Reeds[i].rot,
                    Reeds[i].scale, -0.05f);
        for (int i = 0; i < nlily; i++)
            AddProp(s, "lily", Lilies[i].x, Lilies[i].z, Lilies[i].rot,
                    Lilies[i].scale, 0.02f);
        BuildDock(s, Docks[_spot]);
        BuildShore(s, Banks[_spot]);
        BuildAngler(s);
        _staticTriCount = s.Count / 3;
    }

    private void BuildDock(List<VertexPositionColor> s,
                           (int n, float x0, float z, float dz, int arm, float armX) dk)
    {
        for (int i = 0; i < dk.n; i++)
            Prims.AddBox(s, new Vector3(dk.x0 + i * 1.42f, 0.62f, dk.z),
                         1.24f, 0.13f, dk.dz, Wood);
        for (int i = 0; i < dk.arm; i++)
            Prims.AddBox(s, new Vector3(dk.armX, 0.62f, -0.55f - i * 1.42f),
                         2.5f, 0.13f, 1.24f, Wood);
    }

    private void BuildShore(List<VertexPositionColor> s,
                            (float x0, int n, float z, float spacing, float w) bk)
    {
        for (int i = 0; i < bk.n; i++)
        {
            float bx = bk.x0 + i * bk.spacing;
            Prims.AddBox(s, new Vector3(bx, 0.34f + 0.004f * (i % 3), bk.z),
                         bk.w + 0.6f, 0.42f, 5.6f, i % 2 == 0 ? Sand : SandD);
        }
        for (int i = 0; i < bk.n; i++)
        {
            float bx = bk.x0 + i * bk.spacing;
            Prims.AddBox(s, new Vector3(bx, 0.72f, bk.z - 3.1f),
                         bk.w + 0.2f, 0.7f, 4.2f, i % 2 == 0 ? Grass : GrassD);
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
        // arms and rod are pose-dependent: drawn per frame, not baked in
    }

    private void EmitFishingPose(List<VertexPositionColor> v)
    {
        var hand = new Vector3(-3.03f, 1.00f, 1.57f);
        var tip = new Vector3(-2.70f, 1.70f, -0.40f);
        Prims.AddSegment(v, new Vector3(-3.23f, 1.19f, 1.56f),
                         new Vector3(-3.12f, 1.06f, 1.62f), 0.065f, Vest);
        Prims.AddSegment(v, new Vector3(-3.12f, 1.06f, 1.62f), hand, 0.055f, Skin);
        var lhand = hand + (tip - hand) * 0.35f;
        var lsh = new Vector3(-3.43f, 1.19f, 1.46f);
        Prims.AddSegment(v, lsh, (lsh + lhand) * 0.5f, 0.065f, Vest);
        Prims.AddSegment(v, (lsh + lhand) * 0.5f, lhand, 0.055f, Skin);
        var mid = hand + (tip - hand) * 0.55f;
        Prims.AddSegment(v, hand, mid, 0.030f, RodDark, 4);
        Prims.AddSegment(v, mid, tip, 0.021f, RodLight, 4);
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
        for (int i = 0; i < 4; i++)
        {
            var key = (Keys)((int)Keys.D1 + i);
            if (kb.IsKeyDown(key) && !_spotHeld[i])
            {
                _spot = i;
                _state.Spot = i;
                _state.Save();
                BuildWorld();
            }
            _spotHeld[i] = kb.IsKeyDown(key);
        }
        // economy keys: sell the cooler, cycle strains
        if (kb.IsKeyDown(Keys.X) && !_xHeld)
        {
            int n = _state.SellBag();
            if (n > 0)
                _sellFlash = 2.0f;
            _xHeld = true;
        }
        if (kb.IsKeyUp(Keys.X)) _xHeld = false;
        if (kb.IsKeyDown(Keys.OemOpenBrackets) && !_lbHeld)
        {
            _state.StrainIndex = (_state.StrainIndex + GameData.Strains.Length - 1)
                                 % GameData.Strains.Length;
            _state.Strain = GameData.Strains[_state.StrainIndex].Id;
            _lbHeld = true;
        }
        if (kb.IsKeyUp(Keys.OemOpenBrackets)) _lbHeld = false;
        if (kb.IsKeyDown(Keys.OemCloseBrackets) && !_rbHeld)
        {
            _state.StrainIndex = (_state.StrainIndex + 1) % GameData.Strains.Length;
            _state.Strain = GameData.Strains[_state.StrainIndex].Id;
            _rbHeld = true;
        }
        if (kb.IsKeyUp(Keys.OemCloseBrackets)) _rbHeld = false;
        // the ceremony sets the high when it finishes burning
        if (_wasSmoking && !_smoking.Active)
            _state.High = 1.0f;
        _wasSmoking = _smoking.Active;
        _state.High = MathF.Max(0f, _state.High - dt / 45f);
        _state.Hour = (_state.Hour + dt / 60f) % 24f;

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
        if (kb.IsKeyDown(Keys.E) && !_eHeld)
        {
            _smoking.Start();
            _eHeld = true;
        }
        if (kb.IsKeyUp(Keys.E)) _eHeld = false;
        if (kb.IsKeyDown(Keys.Q) && !_qHeld)
        {
            _smoking.CycleItem();
            _qHeld = true;
        }
        if (kb.IsKeyUp(Keys.Q)) _qHeld = false;
        _smoking.Update(dt);
        FishingSim.TickTime(dt);
        if (!_smoking.Active)
            _fishing.Update(dt, kb, new Vector3(-2.70f, 1.70f, -0.40f),
                            new Vector3(0.18f, -0.05f, -0.98f));
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

        var dyn = new List<VertexPositionColor>();
        if (_smoking.Active)
        {
            _smoking.EmitArms(dyn, Vest, Skin);
            _smoking.EmitItem(dyn);
            _smoking.EmitPlume(dyn);
            _smoking.EmitRestingRod(dyn);
        }
        else
        {
            EmitFishingPose(dyn);
            _fishing.EmitLine(dyn, new Vector3(-2.70f, 1.70f, -0.40f));
            _fishing.EmitBobber(dyn);
        }

        // water first, then the static world over it
        DrawVerts(water, _effect);
        DrawVerts(_staticVerts.GetRange(0, _staticTriCount * 3), _effectCullNone);
        DrawVerts(dyn, _effectCullNone);
        DrawCatchCard();
        DrawHud();
        base.Draw(gameTime);
    }

    private void DrawCatchCard()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        _batch.Begin(samplerState: SamplerState.PointClamp);
        if (_fishing.Caught is { } f && _fishing.Current == FishingSim.Phase.Card)
        {
            var r = new Rectangle(w / 2 - 170, h / 2 - 60, 340, 120);
            _batch.Draw(_pixel, r, new Color(24, 28, 44, 235));
            _batch.Draw(_pixel, new Rectangle(r.X + 10, r.Y + 10, 320, 10), f.Body);
            _font.Draw(_batch, f.Name, new Vector2(r.X + 16, r.Y + 22), 3, ColorExt.Cream());
            _font.Draw(_batch, f.Rarity.ToUpper(), new Vector2(r.X + 16, r.Y + 44), 2,
                       f.Body);
            _font.Draw(_batch, string.Format("{0:0.0} KG  +${1}", _fishing.CaughtKg,
                                             (int)_fishing.CaughtValue),
                       new Vector2(r.X + 16, r.Y + 66), 2, new Color(240, 214, 120));
            _font.Draw(_batch, "X SELLS THE COOLER", new Vector2(r.X + 16, r.Y + 92), 2,
                       new Color(150, 160, 190));
        }
        if (_fishing.Reeling)
            DrawReel();
        if (_fishing.Current == FishingSim.Phase.Charging)
        {
            var meter = new Rectangle(w / 2 - 100, h - 90, 200, 14);
            _batch.Draw(_pixel, meter, new Color(40, 46, 66, 200));
            _batch.Draw(_pixel, new Rectangle(meter.X + 2, meter.Y + 2,
                        (int)(196 * _fishing.Power), 10), new Color(240, 200, 90));
        }
        _batch.End();
    }

    private void DrawReel()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        var c = new Vector2(w / 2f, h / 2f - 40f);
        float R = 90f;
        for (int i = 0; i < 36; i++)
        {
            float a0 = -MathF.PI + MathF.Tau * i / 36;
            float a1 = a0 + MathF.Tau / 36;
            float mid = MathF.Abs(Wrap(a0 + a1) / 2);
            Color col = mid < 0.10f ? new Color(90, 180, 250)
                      : mid < 0.28f ? new Color(110, 210, 120)
                      : new Color(190, 70, 70);
            var p0 = c + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * R;
            var p1 = c + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * R;
            var p2 = c + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * (R - 10);
            var p3 = c + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * (R - 10);
            FillTri(_batch, _pixel, p0, p1, p2, col);
            FillTri(_batch, _pixel, p0, p2, p3, col);
        }
        // needle: small quads along the sweep
        var end = c + new Vector2(MathF.Cos(_fishing.NeedleAngle),
                                  MathF.Sin(_fishing.NeedleAngle)) * (R - 14);
        for (int i = 0; i <= 16; i++)
        {
            var pt = Vector2.Lerp(c, end, i / 16f);
            _batch.Draw(_pixel, new Rectangle((int)pt.X - 3, (int)pt.Y - 3, 6, 6),
                        new Color(245, 245, 235));
        }
        // progress bar
        var bar = new Rectangle((int)c.X - 90, (int)c.Y + (int)R + 24, 180, 12);
        _batch.Draw(_pixel, bar, new Color(40, 46, 66, 220));
        _batch.Draw(_pixel, new Rectangle(bar.X + 1, bar.Y + 1,
                    (int)(178 * _fishing.Progress), 10), new Color(120, 210, 250));
        _batch.Draw(_pixel, new Rectangle((int)c.X - 3, (int)c.Y - (int)R + 2, 6, 8),
                    new Color(90, 180, 250));
    }

    private static float Wrap(float a)
    {
        while (a > MathF.PI) a -= MathF.Tau;
        while (a < -MathF.PI) a += MathF.Tau;
        return a;
    }

    private static void FillTri(SpriteBatch batch, Texture2D px, Vector2 a,
                                Vector2 b, Vector2 c2, Color col)
    {
        // rasterise a tiny triangle with a few horizontal strips
        var pts = new[] { a, b, c2 }.OrderBy(p => p.Y).ToArray();
        var (top, mid, bot) = (pts[0], pts[1], pts[2]);
        if (bot.Y - top.Y < 0.5f)
            return;
        for (float y = top.Y; y <= bot.Y; y += 1.5f)
        {
            float t = Math.Clamp((y - top.Y) / MathF.Max(0.001f, bot.Y - top.Y), 0f, 1f);
            var left = Vector2.Lerp(top, bot, t);
            float t2 = y < mid.Y
                ? (y - top.Y) / MathF.Max(0.001f, mid.Y - top.Y)
                : (y - mid.Y) / MathF.Max(0.001f, bot.Y - mid.Y);
            var right = Vector2.Lerp(y < mid.Y ? top : mid, y < mid.Y ? mid : bot,
                                     Math.Clamp(t2, 0f, 1f));
            if (left.X > right.X) (left, right) = (right, left);
            batch.Draw(px, new Rectangle((int)left.X, (int)y,
                        Math.Max(1, (int)(right.X - left.X)), 1), col);
        }
    }

    private float _sellFlash;

    private void DrawHud()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        _batch.Begin(samplerState: SamplerState.PointClamp);
        _font.Draw(_batch, GameData.SpotNames[_spot], new Vector2(14, 14), 3, ColorExt.Cream());
        _font.Draw(_batch, string.Format("{0:00}:00", (int)_state.Hour), new Vector2(14, 36), 2,
                   new Color(170, 190, 220));
        _font.Draw(_batch, "CASTS " + _state.Casts, new Vector2(14, 56), 2,
                   new Color(170, 190, 220));
        string sd = _state.StrainData().Name.ToUpper();
        _font.Draw(_batch, "WEED  " + sd, new Vector2(14, 76), 2, new Color(150, 216, 160));
        _font.Draw(_batch, "HIGH  " + (int)(_state.High * 100) + "%",
                   new Vector2(14, 96), 2, new Color(150, 216, 160));
        _font.Draw(_batch, "$" + _state.Cash, new Vector2(w - 100, 14), 3, new Color(240, 214, 120));
        _font.Draw(_batch, "COOLER " + _state.Bag.Count + "/8", new Vector2(w - 140, 40), 2,
                   new Color(246, 170, 120));
        if (_sellFlash > 0f)
            _font.Draw(_batch, "SOLD!", new Vector2(w - 100, 62), 2, new Color(240, 214, 120));
        _font.Draw(_batch, "SPACE CAST  E SMOKE  X SELL  [ ] WEED  Q PIECE",
                   new Vector2(14, h - 24), 2, new Color(150, 160, 190));
        _batch.End();
        _sellFlash -= 0.016f;
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
