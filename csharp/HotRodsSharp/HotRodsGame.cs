namespace HotRodsSharp;

/// <summary>
/// Phase-2 port: the full pond scene - props from the Blender OBJs, dock and
/// shore built from the Python layout tables, animated water, the fisherman
/// model, and three camera modes (third / first / free, TAB to cycle).
/// </summary>
public partial class HotRodsGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private BasicEffect _effect = null!;          // unlit: water, dynamic pieces
    private BasicEffect _effectCullNone = null!;
    private BasicEffect _effectLit = null!;       // lit: static world, sun-shaded

    // static world
    private readonly List<VertexPositionNormalColor> _staticVerts = new();
    private int _staticTriCount;
    private int _spot;
    private GameState _state = GameState.Load();
    private readonly FishingSim _fishing;
    private readonly SmokingSim _smoking = new();
    private readonly FishRender _fish = new();
    private bool _eHeld, _qHeld, _xHeld, _lbHeld, _rbHeld;
    private bool _shopOpen, _enterHeld, _upHeld, _downHeld, _leftHeld, _rightHeld;
    private bool _journalOpen, _zaaOpen, _escHeld;
    private bool _atTitle = true, _tripsOpen, _dealerOpen, _stokeOpen, _helpOpen, _mapOpen, _menuOpen;
    private GameState? _boundState;          // level-up toast subscription
    private bool _hHeld, _tKeyHeld, _fHeld, _uHeld;
    private int _titleSel, _tripsSel = 1, _dealerSel, _dealerArea, _stokeSel, _mapSel, _menuSel;
    private int _dealerFocus;                 // 0 cooler, 1 jars
    private int _scrapConfirm;                // trips: slot awaiting confirm
    private float _newConfirm, _scrapConfirmT;
    private string _menuMsg = "";
    private float _menuMsgT;
    private int _journalSel, _zaaSel;
    private string _uiMsg = "";
    private float _uiMsgT;
    private int _shopTab, _shopSel;
    private string _shopMsg = "";
    private float _shopMsgT;
    private bool _consoleOpen, _tickHeld;
    private SoundEffect _splash = null!, _splashBig = null!, _click = null!,
                        _whoosh = null!;
    private SoundEffectInstance _birds = null!;
    private string _consoleInput = "", _consoleMsg = "";
    private float _consoleMsgT;
    private HashSet<Keys> _prevKeys = new();
    private bool _wasSmoking;
    private BitmapFont _font = null!;
    private SpriteBatch _batch = null!;
    private Texture2D _pixel = null!;
    // water, rebuilt per frame
    private float _t;

    // camera
    private Matrix _view = Matrix.Identity, _proj = Matrix.Identity;
    private float _lookYaw, _lookPitch;        // click-drag look offsets
    private bool _mouseDown, _mouseDragged;
    private Point _mousePrev;
    private bool _tabHeld;                  // dealer: cooler/jars focus toggle

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
        ("rock_a", new Color(128, 122, 112), 1.0f, "land"),
        ("rock_b", new Color(108, 104, 96), 1.0f, "land"),
        ("bush", new Color(86, 116, 72), 1.0f, "land"),
        ("kn_plant", new Color(74, 112, 58), 8.0f, "land"),
        ("kn_rocks-low", new Color(128, 124, 118), 3.0f, "land"),
        ("kn_rocks-high", new Color(122, 118, 112), 2.6f, "land"),
        ("kn_fence", new Color(140, 108, 74), 2.6f, "land"),
        ("kn_tent", new Color(172, 116, 72), 2.4f, "land"),
        ("kn_bridge", new Color(146, 110, 76), 2.2f, "land"),
        ("kn_flag", new Color(200, 70, 60), 3.0f, "land"),
        ("kn_tree", new Color(96, 120, 70), 2.2f, "land"),
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
            ("boat", 3.60f, -3.20f, 1.35f, 1.00f),
            ("rock_a", -20.0f, -15.4f, 0.8f, 1.00f),
            ("rock_b", -11.5f, -16.2f, 2.1f, 1.00f),
            ("rock_a", 12.0f, -15.0f, 4.2f, 0.85f),
            ("bush", -30.0f, -14.6f, 1.4f, 1.00f),
            ("bush", 18.0f, -14.2f, 5.0f, 1.15f),
            ("bush", -4.5f, -15.8f, 3.3f, 0.90f),
            ("kn_fence", -8.0f, 5.6f, 0.0f, 1.00f),
            ("kn_tent", 12.0f, 3.2f, 5.6f, 1.00f),
            ("kn_plant", -24.0f, -13.2f, 1.1f, 1.00f),
            ("kn_plant", 16.0f, -12.6f, 4.0f, 0.90f),
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
            ("kn_bridge", 0.0f, -5.5f, 1.57f, 1.00f),
            ("kn_tree", -20.0f, -14.5f, 2.2f, 1.00f),
            ("kn_plant", 14.0f, -13.0f, 5.2f, 1.00f),
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
            ("kn_plant", -14.0f, -10.4f, 2.4f, 1.10f),
            ("kn_plant", -8.0f, -10.0f, 4.4f, 1.00f),
            ("kn_plant", -2.0f, -10.6f, 0.8f, 1.15f),
            ("kn_plant", 4.0f, -9.8f, 5.6f, 1.00f),
            ("kn_rocks-low", -20.0f, -9.4f, 1.9f, 1.00f),
        },
        // Tokers Trench
        new (string, float, float, float, float)[]
        {
            ("tree_a", -20.0f, -11.0f, 1.6f, 1.45f),
            ("tree_b", -8.0f, -11.6f, 4.0f, 1.55f),
            ("tree_a", 6.0f, -10.8f, 2.9f, 1.40f),
            ("tree_b", 18.0f, -11.4f, 5.6f, 1.50f),
            ("kn_rocks-high", -14.0f, -8.8f, 3.1f, 1.00f),
            ("kn_rocks-high", 10.0f, -8.4f, 5.9f, 0.85f),
            ("kn_flag", 20.0f, -6.5f, 2.2f, 1.00f),
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
        Window.Title = "Hot Rods";
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
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        _effectCullNone = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
            FogEnabled = true,
            FogColor = new Vector3(0.55f, 0.62f, 0.72f),
            FogStart = 14f,
            FogEnd = 42f,
        };
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        _effectLit = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = true,
            LightingEnabled = true,
            PreferPerPixelLighting = false,     // per-vertex Gouraud: Xbox-era
            FogEnabled = true,
            FogColor = new Vector3(0.55f, 0.62f, 0.72f),
            FogStart = 14f,
            FogEnd = 42f,
        };
        _effectLit.DirectionalLight0.Enabled = true;
        _effectLit.DirectionalLight0.SpecularColor = new Vector3(0.15f, 0.15f, 0.15f);

        BuildWorld();
        _batch = new SpriteBatch(GraphicsDevice);
        _font = new BitmapFont(GraphicsDevice);
        LoadAudio();
        _fishing.Sfx = PlaySfx;
        _fishing.Ripple += p => _ripples.Add((p, 0f));
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    private void AddProp(List<VertexPositionNormalColor> s, string name, float x,
                         float z, float rot, float scale, float yBias = 0f)
    {
        var entry = Manifest.FirstOrDefault(m => m.name == name);
        if (entry.name == null)
        {
            Console.WriteLine($"[world] no manifest entry for {name} - skipped");
            return;
        }
        var (_, col, bscale, ground) = entry;
        bool blenderAxes = !name.StartsWith("kn_");
        ObjMesh mesh;
        try
        {
            mesh = ObjMesh.Load(
                Path.Combine(AppContext.BaseDirectory, "Assets", name + ".obj"),
                scale * bscale, blenderAxes);
        }
        catch (FileNotFoundException)
        {
            // a missing prop mesh skips the prop instead of crashing the world
            Console.WriteLine($"[world] missing asset {name}.obj - skipped");
            return;
        }
        mesh.Transform(rot, new Vector3(x, Ground[ground] + yBias, z));
        EmitSmooth(s, mesh, col);
    }

    /// <summary>Emit an OBJ mesh with per-vertex normals (averaged face
    /// normals) so the lit effect shades it smoothly, Xbox-style.</summary>
    private static void EmitSmooth(List<VertexPositionNormalColor> s, ObjMesh m,
                                   Color col)
    {
        var normals = new Vector3[m.Verts.Count];
        foreach (var (a, b, c) in m.Tris)
        {
            var n = Vector3.Cross(m.Verts[b] - m.Verts[a], m.Verts[c] - m.Verts[a]);
            if (n.LengthSquared() > 0.000001f)
            {
                n = Vector3.Normalize(n);
                normals[a] += n; normals[b] += n; normals[c] += n;
            }
        }
        for (int i = 0; i < normals.Length; i++)
            normals[i] = normals[i].LengthSquared() > 0.0001f
                ? Vector3.Normalize(normals[i]) : Vector3.Up;
        for (int i = 0; i < m.Tris.Count; i++)
        {
            var (a, b, c) = m.Tris[i];
            PrimsLit.AddTriS(s, m.Verts[a], m.Verts[b], m.Verts[c],
                             normals[a], normals[b], normals[c], col);
        }
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
        BuildCooler(s);
        _staticTriCount = s.Count / 3;
    }

    /// <summary>The cooler sits at your left with the phone on its lid;
    /// the screen glows from the dynamic pass so it reads at night too.</summary>
    private void BuildCooler(List<VertexPositionNormalColor> s)
    {
        // a small pier section bridges the planks out to the cooler
        PrimsLit.AddBox(s, new Vector3(-3.30f, 0.72f, 1.15f), 2.0f, 0.13f, 1.1f, Wood);
        var c = new Vector3(-3.20f, 0.80f, 0.95f);
        PrimsLit.AddBox(s, c, 0.55f, 0.45f, 0.75f, new Color(200, 60, 48));
        PrimsLit.AddBox(s, new Vector3(c.X, c.Y + 0.26f, c.Z), 0.57f, 0.06f, 0.77f,
                        new Color(232, 236, 240));
        PrimsLit.AddBox(s, new Vector3(c.X - 0.02f, c.Y + 0.32f, c.Z),
                        0.16f, 0.035f, 0.26f, new Color(28, 32, 40));
    }

    /// <summary>Unlit phone-screen glow so it stays bright after dark.</summary>
    private void EmitPhoneGlow(List<VertexPositionColor> v)
    {
        Prims.AddBox(v, new Vector3(-3.20f, 1.155f, 0.93f), 0.11f, 0.012f, 0.20f,
                     new Color(90, 232, 205));
    }

    private void BuildDock(List<VertexPositionNormalColor> s,
                           (int n, float x0, float z, float dz, int arm, float armX) dk)
    {
        for (int i = 0; i < dk.n; i++)
            PrimsLit.AddBox(s, new Vector3(dk.x0 + i * 1.42f, 0.62f, dk.z),
                            1.24f, 0.13f, dk.dz, Wood);
        for (int i = 0; i < dk.arm; i++)
            PrimsLit.AddBox(s, new Vector3(dk.armX, 0.62f, -0.55f - i * 1.42f),
                            2.5f, 0.13f, 1.24f, Wood);
    }

    private void BuildShore(List<VertexPositionNormalColor> s,
                            (float x0, int n, float z, float spacing, float w) bk)
    {
        for (int i = 0; i < bk.n; i++)
        {
            float bx = bk.x0 + i * bk.spacing;
            PrimsLit.AddBox(s, new Vector3(bx, 0.34f + 0.004f * (i % 3), bk.z),
                            bk.w + 0.6f, 0.42f, 5.6f, i % 2 == 0 ? Sand : SandD);
        }
        for (int i = 0; i < bk.n; i++)
        {
            float bx = bk.x0 + i * bk.spacing;
            PrimsLit.AddBox(s, new Vector3(bx, 0.72f, bk.z - 3.1f),
                            bk.w + 0.2f, 0.7f, 4.2f, i % 2 == 0 ? Grass : GrassD);
        }
        // north shore behind the angler closes the pond
        for (int i = 0; i < 10; i++)
        {
            float bx = -36f + i * 8.0f;
            PrimsLit.AddBox(s, new Vector3(bx, 0.34f + 0.004f * (i % 3), 15.4f),
                            8.6f, 0.46f, 6.4f, i % 2 == 0 ? Sand : SandD);
        }
        for (int i = 0; i < 8; i++)
        {
            float bx = -32f + i * 9.0f;
            PrimsLit.AddBox(s, new Vector3(bx, 0.78f, 19.2f), 9.2f, 0.7f, 5.2f,
                            i % 2 == 0 ? Grass : GrassD);
        }
    }

    private void EmitFishingPose(List<VertexPositionColor> v)
    {
        // first-person viewmodel: hands on the rod, angled out toward the
        // cast area. The sim's rod tip lives at the same place the line
        // draws from.
        var grip = new Vector3(-4.14f, 1.20f, 1.62f);
        var mid = new Vector3(-3.43f, 1.42f, 1.55f);
        var tip = new Vector3(-1.83f, 1.60f, 1.45f);
        // no arms in view - just the rod (the tip dips under load while
        // you fight the fish)
        float bend = _fishing.Current == FishingSim.Phase.Reeling
            ? 0.10f * _fishing.Progress : _fishing.Current == FishingSim.Phase.Hook ? 0.16f : 0f;
        var tipBent = tip - new Vector3(0f, bend, 0f);
        Prims.AddSegment(v, grip, mid, 0.034f, RodDark, 5);
        Prims.AddSegment(v, mid, tipBent, 0.026f, RodLight, 5);
        Prims.AddBox(v, grip + new Vector3(0f, -0.10f, 0.06f),
                     0.055f, 0.085f, 0.075f, new Color(70, 74, 82));
    }

    /// <summary>FP rod tip and cast direction (the bobber flies out over
    /// the lake ahead of the view).</summary>
    private static readonly Vector3 FpRodTip = new(-1.83f, 1.60f, 1.45f);

    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _t += dt;
        SteamMan.RunCallbacks();
        var kb = Keyboard.GetState();
        var ms = Mouse.GetState();
        if (ms.LeftButton == ButtonState.Pressed && !_mouseDown)
        {
            _mouseDown = true;
            _mouseDragged = false;
            _mousePrev = ms.Position;
        }
        if (_mouseDown)
        {
            var delta = ms.Position - _mousePrev;
            if (MathF.Abs(delta.X) + MathF.Abs(delta.Y) > 4)
                _mouseDragged = true;
            if (_mouseDragged)
            {
                _lookYaw = Math.Clamp(_lookYaw - delta.X * 0.0035f, -0.85f, 0.85f);
                _lookPitch = Math.Clamp(_lookPitch + delta.Y * 0.0035f, -0.45f, 0.50f);
            }
            _mousePrev = ms.Position;
            if (ms.LeftButton == ButtonState.Released)
            {
                if (!_mouseDragged)
                    HandleClick(ms.Position);
                _mouseDown = false;
            }
        }
        if (kb.IsKeyDown(Keys.OemTilde) && !_tickHeld)
        {
            _consoleOpen = !_consoleOpen;
            _consoleInput = "";
            _tickHeld = true;
        }
        if (kb.IsKeyUp(Keys.OemTilde)) _tickHeld = false;
        if (_consoleOpen)
        {
            UpdateConsole(kb);
            base.Update(gameTime);
            return;
        }
        if (_atTitle)
        {
            UpdateTitle(dt, kb);
            base.Update(gameTime);
            return;
        }
        if (_dealerOpen)
        {
            UpdateDealer(dt, kb);
            base.Update(gameTime);
            return;
        }
        if (_stokeOpen)
        {
            UpdateStoke(dt, kb);
            base.Update(gameTime);
            return;
        }
        if (_mapOpen)
        {
            UpdateMap(dt, kb);
            base.Update(gameTime);
            return;
        }
        if (_helpOpen)
        {
            if (kb.IsKeyDown(Keys.Escape) && !_escHeld)
            {
                _helpOpen = false;
                _menuOpen = true;
                _escHeld = true;
            }
            if (kb.IsKeyUp(Keys.Escape)) _escHeld = false;
            base.Update(gameTime);
            return;
        }
        if (_shopOpen)
        {
            UpdateShop(dt, kb);
            base.Update(gameTime);
            return;
        }
        if (_journalOpen || _zaaOpen)
        {
            UpdateOverlays(dt, kb);
            base.Update(gameTime);
            return;
        }
        if (_menuOpen)
        {
            UpdateMenu(dt, kb);
            base.Update(gameTime);
            return;
        }
        if (kb.IsKeyDown(Keys.Escape) && !_escHeld)
        {
            _menuOpen = true;
            _menuSel = 0;
            _escHeld = true;
        }
        if (kb.IsKeyUp(Keys.Escape)) _escHeld = false;
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
        // the ceremony burns a gram when it finishes; chill decays on the
        // Python curve (strain duration, piece burn, vibes stoke lane)
        if (_wasSmoking && !_smoking.Active)
        {
            var msg = _state.ApplyToke();
            _uiMsg = msg;
            _uiMsgT = 2.5f;
        }
        _wasSmoking = _smoking.Active;
        _smoking.Speed = 1f + 0.06f * _state.StokeRank("smoke");
        _state.TickChill(dt);
        _state.Hour = (_state.Hour + dt / 30f) % 24f;   // 12-minute day, like Python
        _state.Played += dt / 60f;

        // first person: head height at the dock, rotated 90 degrees so the
        // view runs down the length of the open water instead of across it.
        // Click-drag looks around (clamped); a proper high wobbles the view
        // and landing a fish shakes it.
        var eye = new Vector3(-4.20f, 1.42f, 1.44f);   // standing on the dock
        var fwd = Vector3.Normalize(new Vector3(24.8f, 0.82f, -1.06f) - eye);
        var right = Vector3.Normalize(Vector3.Cross(fwd, Vector3.Up));
        fwd = Vector3.Normalize(Vector3.Transform(fwd, Matrix.CreateRotationY(_lookYaw)));
        fwd = Vector3.Normalize(Vector3.Transform(
            fwd, Matrix.CreateFromAxisAngle(right, _lookPitch)));
        eye.X += _fishing.ShakeT * 0.12f * MathF.Sin(_t * 43f);
        eye.Y += _fishing.ShakeT * 0.10f * MathF.Cos(_t * 37f);
        // the fish pulls the camera as the fight builds
        float fight = _fishing.Current == FishingSim.Phase.Reeling
            ? _fishing.Progress : 0f;
        eye.X += MathF.Sin(_t * 31f) * 0.012f * fight;
        eye.Y += MathF.Cos(_t * 27f) * 0.010f * fight;
        _view = Matrix.CreateLookAt(eye, eye + fwd, Vector3.Up);
        _proj = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(62f), GraphicsDevice.Viewport.AspectRatio, 0.1f, 250f);
        if (kb.IsKeyDown(Keys.E) && !_eHeld)
        {
            var (ok, msg) = _state.CanToke();
            if (!ok)
            {
                _uiMsg = msg;
                _uiMsgT = 2.5f;
            }
            else
            {
                _state.Smoker = _smoking.CurrentItem;
                _smoking.Start();
            }
            _eHeld = true;
        }
        if (kb.IsKeyUp(Keys.E)) _eHeld = false;
        if (kb.IsKeyDown(Keys.Q) && !_qHeld)
        {
            _smoking.CycleItem();
            _state.Smoker = _smoking.CurrentItem;
            _qHeld = true;
        }
        if (kb.IsKeyUp(Keys.Q)) _qHeld = false;
        _smoking.Update(dt);
        SetDayPhase();
        FishingSim.TickTime(dt);
        for (int i = _ripples.Count - 1; i >= 0; i--)
        {
            var (pos, age) = _ripples[i];
            age += dt;
            if (age > 1.4f)
                _ripples.RemoveAt(i);
            else
                _ripples[i] = (pos, age);
        }
        if (!_smoking.Active)
            _fishing.Update(dt, kb, FpRodTip,
                            Vector3.Normalize(new Vector3(1.0f, -0.12f, -0.10f)));
        base.Update(gameTime);
    }

    private List<VertexPositionColor> WaterVerts()
    {
        var v = new List<VertexPositionColor>();
        int cols = 22, rows = 16;
        float x0 = -34f, x1 = 34f, z0 = -56f, z1 = 13f;
        var spotTint = GameData.Spots[_spot].Tint;
        float murk = GameData.Spots[_spot].Murk;
        var shallow = Color.Lerp(new Color(92, 150, 148), spotTint, 0.35f);
        var deep = Color.Lerp(new Color(34, 82, 104), spotTint, murk);
        var foam = new Color(226, 240, 240);
        float day = 1f - Nightness(_state.Hour);
        float glintX = SunDirection(_state.Hour).X;
        float WaveY(float x, float z) =>
            0.05f * MathF.Sin(_t * 1.3f + x * 0.28f + z * 0.21f)
          + 0.026f * MathF.Sin(_t * 2.1f + x * 0.55f - z * 0.42f)
          + 0.013f * MathF.Sin(_t * 3.4f + x * 0.90f + z * 0.85f);
        for (int j = 0; j < rows; j++)
            for (int i = 0; i < cols; i++)
            {
                float xt0 = (float)i / cols, xt1 = (float)(i + 1) / cols;
                float zt0 = (float)j / rows, zt1 = (float)(j + 1) / rows;
                Vector3 P(float xt, float zt)
                {
                    float x = x0 + (x1 - x0) * xt;
                    float z = z0 + (z1 - z0) * zt;
                    return new Vector3(x, 0.06f + WaveY(x, z), z);
                }
                var a = P(xt0, zt0);
                var b = P(xt1, zt0);
                var c = P(xt1, zt1);
                var d = P(xt0, zt1);
                float depth = Math.Clamp((a.Z - z0) / (z1 - z0), 0f, 1f);
                var col = Color.Lerp(shallow, deep, depth);
                float foamF = MathHelper.SmoothStep(11.0f, 12.8f, a.Z);
                col = Color.Lerp(col, foam, foamF * 0.5f);
                // daylight brightens the sheet; a soft streak follows the sun
                col = new Color(
                    (byte)(col.R * (0.55f + 0.45f * day)),
                    (byte)(col.G * (0.55f + 0.45f * day)),
                    (byte)(col.B * (0.55f + 0.45f * day)));
                float streak = MathF.Max(0f,
                    1f - MathF.Abs(a.X - glintX * 22f) / 16f);
                col = Color.Lerp(col, new Color(240, 246, 250),
                                 streak * 0.16f * day);
                if (_state.High > 0f)
                    col = Color.Lerp(col, Vivid(col), _state.High * 0.5f);
                var colD = new Color((byte)(col.R * 0.94f), (byte)(col.G * 0.94f),
                                     (byte)(col.B * 0.94f));
                Prims.AddTri(v, a, b, c, col);
                Prims.AddTri(v, a, c, d, colD);
            }
        return v;
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_atTitle)
        {
            DrawTitleScreen();
            base.Draw(gameTime);
            return;
        }
        // native-resolution Xbox look: clear, sky, then the world
        GraphicsDevice.Clear(_skyClear);
        DrawSky();
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        var water = WaterVerts();
        foreach (var effect in new[] { _effect, _effectCullNone, _effectLit })
        {
            effect.View = _view;
            effect.Projection = _proj;
            effect.World = Matrix.Identity;
        }
        SetLighting();

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
            _fishing.EmitLine(dyn, FpRodTip);
            _fishing.EmitBobber(dyn);
            EmitFightingFish(dyn);
        }
        EmitShimmer(dyn);
        EmitRipples(dyn);
        EmitPhoneGlow(dyn);

        // water first, then the lit static world, then the dynamic pieces
        DrawVerts(water, _effect);
        DrawVertsLit(_staticVerts.GetRange(0, _staticTriCount * 3), _effectLit);
        DrawVerts(dyn, _effectCullNone);
        DrawCatchCard();
        _batch.Begin();
        _batch.Draw(_pixel, new Rectangle(0, 0, GraphicsDevice.Viewport.Width,
                    GraphicsDevice.Viewport.Height), _tint.col * _tint.a);
        _batch.End();
        DrawHud();   // the HUD stays up; the rope bar sits lower-centre
        if (_shopOpen)
            DrawShop();
        if (_journalOpen)
            DrawJournal();
        if (_zaaOpen)
            DrawZaa();
        if (_menuOpen)
            DrawMenu();
        if (_dealerOpen)
            DrawDealer();
        if (_stokeOpen)
            DrawStoke();
        if (_mapOpen)
            DrawMap();
        if (_helpOpen)
            DrawHelp();
        if (_consoleOpen)
            DrawConsole();
        base.Draw(gameTime);
    }

    private void EmitShimmer(List<VertexPositionColor> dyn)
    {
        if (_state.Hour >= 20 || _state.Hour < 5)
            return;
        for (int k = 0; k < 16; k++)
        {
            float x = -30f + k * 4.1f;
            float z = 8f - k * 2.6f;
            float tw = 0.4f + 0.6f * MathF.Abs(MathF.Sin(FishingSim.Clock * 3f + k * 1.7f));
            Prims.AddBox(dyn, new Vector3(x, 0.09f, z), tw, 0.015f, tw,
                         new Color(252, 240, 186));
        }
    }

    /// <summary>Expanding ripple rings where the bobber lands or a fish bites.</summary>
    private void EmitRipples(List<VertexPositionColor> dyn)
    {
        foreach (var (pos, age) in _ripples)
        {
            float t = age / 1.4f;
            float radius = 0.15f + t * 3.2f;
            var col = new Color(235, 245, 250) * ((1f - t) * 0.8f);
            for (int k = 0; k < 10; k++)
            {
                float a0 = MathF.Tau * k / 10;
                float a1 = MathF.Tau * (k + 1) / 10;
                var p0 = new Vector3(pos.X + MathF.Cos(a0) * radius, 0.10f,
                                     pos.Z + MathF.Sin(a0) * radius);
                var p1 = new Vector3(pos.X + MathF.Cos(a1) * radius, 0.10f,
                                     pos.Z + MathF.Sin(a1) * radius);
                Prims.AddSegment(dyn, p0, p1, 0.03f, col, 2);
            }
        }
    }

    private void EmitFightingFish(List<VertexPositionColor> dyn)
    {
        if (_fishing.Pending is not { } sp)
            return;
        float t = 0f;
        if (_fishing.Current == FishingSim.Phase.Reeling)
        {
            // thrash at the bobber
            float wig = MathF.Sin(FishingSim.Clock * 6.5f);
            _fish.Emit(dyn, sp, _fishing.Bobber + new Vector3(0f, 0.05f, 0f),
                       wig * 0.9f, FishScale());
        }
        else if (_fishing.Current == FishingSim.Phase.Card)
        {
            t = Math.Clamp(_fishing.CardT / 2.6f, 0f, 1f);
            var from = _fishing.Bobber + new Vector3(0f, 0.10f, 0f);
            var to = new Vector3(-3.03f, 1.35f, 1.57f);
            var pos = Vector3.Lerp(from, to, t) + Vector3.Up * MathF.Sin(t * MathF.PI) * 1.4f;
            _fish.Emit(dyn, sp, pos, -1.25f, FishScale());
        }
    }

    private float FishScale() =>
        Math.Clamp(0.35f + _fishing.CaughtKg * 0.22f, 0.35f, 1.7f);

    private void DrawCatchCard()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        _batch.Begin(samplerState: SamplerState.PointClamp);
        if (_fishing.Caught is { } f && _fishing.Current == FishingSim.Phase.Card)
        {
            var r = new Rectangle(w / 2 - 170, h / 2 - 70, 340, 140);
            _batch.Draw(_pixel, r, new Color(24, 28, 44, 235));
            _batch.Draw(_pixel, new Rectangle(r.X + 10, r.Y + 10, 320, 10), f.Body);
            _font.Draw(_batch, f.Name, new Vector2(r.X + 16, r.Y + 22), 3, ColorExt.Cream());
            _font.Draw(_batch, f.Rarity.ToUpper(), new Vector2(r.X + 16, r.Y + 44), 2,
                       f.Body);
            _font.Draw(_batch, _fishing.Kept
                       ? string.Format("{0:0.0} KG  +${1}", _fishing.CaughtKg,
                                       (int)_fishing.CaughtValue)
                       : "COOLER FULL - SOLD ON THE SPOT",
                       new Vector2(r.X + 16, r.Y + 66), 2, new Color(240, 214, 120));
            if (_fishing.FirstCatch)
                _font.Draw(_batch, "NEW SPECIES! +$" + (int)_fishing.CaughtValue
                           + " DISCOVERY",
                           new Vector2(r.X + 16, r.Y + 92), 2, new Color(255, 210, 110));
            _font.Draw(_batch, "X SELLS THE COOLER", new Vector2(r.X + 16, r.Y + 116), 2,
                       new Color(150, 160, 190));
        }
        if (_fishing.Current == FishingSim.Phase.Bite)
        {
            float pulse = 0.5f + 0.5f * MathF.Sin(_t * 12f);
            var col = new Color(255, 210, 90) * (0.75f + 0.25f * pulse);
            _font.Draw(_batch, "FISH ON!", new Vector2(w / 2f - 64, h * 0.30f), 4, col);
            var cd = new Rectangle(w / 2 - 100, (int)(h * 0.30f) + 30, 200, 8);
            _batch.Draw(_pixel, cd, new Color(30, 34, 52, 230));
            float remain = Math.Clamp(1f - _fishing.BiteT / FishingSim.BiteWindow, 0f, 1f);
            _batch.Draw(_pixel, new Rectangle(cd.X + 1, cd.Y + 1, (int)(198 * remain), 6),
                        new Color(255, 180, 80));
            _font.Draw(_batch, "SPACE!", new Vector2(w / 2f - 18, h * 0.30f + 46), 2, col);
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

    /// <summary>Braided rope: body colour, diagonal hatch, knotted ends.</summary>
    private void DrawRope(Rectangle r, Color body)
    {
        _batch.Draw(_pixel, r, body);
        for (int x = r.X + 4; x < r.Right - 4; x += 8)
        {
            _batch.Draw(_pixel, new Rectangle(x, r.Y + r.Height - 9, 2, 4),
                        new Color(140, 112, 66));
            _batch.Draw(_pixel, new Rectangle(x + 4, r.Y + 5, 2, 4),
                        new Color(140, 112, 66));
        }
        for (int x = r.X + 3; x < r.Right - 3; x += 4)
            _batch.Draw(_pixel, new Rectangle(x, r.Y + 12, 2, 2),
                        new Color(150, 122, 74));
        // knotted ends
        _batch.Draw(_pixel, new Rectangle(r.X, r.Y - 3, 7, r.Height + 6),
                    new Color(120, 94, 56));
        _batch.Draw(_pixel, new Rectangle(r.Right - 7, r.Y - 3, 7, r.Height + 6),
                    new Color(120, 94, 56));
        // outline
        _batch.Draw(_pixel, new Rectangle(r.X, r.Y, r.Width, 1), new Color(90, 70, 45));
        _batch.Draw(_pixel, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), new Color(90, 70, 45));
    }

    /// <summary>Python-style reel as a full-width bar at the bottom of the
    /// screen: the sweep marker runs back and forth while the zone window
    /// wanders. Time the pull - green core +3, zone +1, a miss costs 2.</summary>
    private void DrawReel()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        var bar = new Rectangle(w / 2 - 170, h - 122, 340, 24);
        // the rope heats up as the fight builds
        DrawRope(bar, Color.Lerp(new Color(176, 148, 96), new Color(206, 84, 52),
                                 _fishing.Progress));
        // miss sections: everything outside the zone window goes red
        float pulse = 1f + 0.05f * MathF.Sin(_t * 9f) + 0.10f * _fishing.Progress;
        float zwEff = _fishing.ZoneW * pulse;
        int zx = bar.X + (int)(bar.Width * Math.Clamp(
                    _fishing.Zone - zwEff * 0.5f, 0f, 1f));
        int zw = (int)(bar.Width * zwEff);
        _batch.Draw(_pixel, new Rectangle(bar.X + 3, bar.Y + 2,
                    Math.Max(0, zx - bar.X - 3), bar.Height - 4),
                    new Color(190, 60, 50, 165));
        _batch.Draw(_pixel, new Rectangle(zx + zw, bar.Y + 2,
                    Math.Max(0, bar.Right - zx - zw - 3), bar.Height - 4),
                    new Color(190, 60, 50, 165));
        // zone window
        _batch.Draw(_pixel, new Rectangle(zx, bar.Y + 1, zw, bar.Height - 7),
                    new Color(96, 150, 220, 190));
        // perfect core
        int px = bar.X + (int)(bar.Width * Math.Clamp(
                    _fishing.Zone - _fishing.ZoneW * 0.09f * 0.5f, 0f, 1f));
        int pw = (int)(bar.Width * _fishing.ZoneW * 0.09f);
        _batch.Draw(_pixel, new Rectangle(px, bar.Y + 5, pw, bar.Height - 17),
                    new Color(120, 240, 130, 220));
        // sweep marker: grows with progress and signals when to press
        int sx = bar.X + (int)(bar.Width * _fishing.Sweep);
        float d = MathF.Abs(_fishing.Sweep - _fishing.Zone);
        float half = _fishing.ZoneW * 0.5f;
        float core = _fishing.ZoneW * 0.09f;
        var markerCol = d <= core ? new Color(150, 255, 150)
                      : d <= half ? new Color(205, 255, 170)
                      : d <= half + 0.12f ? new Color(255, 235, 120)
                      : new Color(255, 240, 140);
        int mw = 4 + (int)(2f * _fishing.Progress);
        _batch.Draw(_pixel, new Rectangle(sx - mw / 2, bar.Y - 4, mw, bar.Height + 2),
                    markerCol);
        if (d <= core)
            _font.Draw(_batch, "NOW!", new Vector2(w / 2f - 14, bar.Y - 30), 3,
                       new Color(150, 255, 150));
        if (d <= core || d <= half)
            _font.Draw(_batch, "SPACE!", new Vector2(w / 2f - 18, bar.Y + 28), 2,
                       new Color(150, 255, 150));
        // progress: thin fill along the bar's bottom edge
        _batch.Draw(_pixel, new Rectangle(bar.X + 3, bar.Bottom - 4, bar.Width - 6, 3),
                    new Color(10, 12, 20, 200));
        _batch.Draw(_pixel, new Rectangle(bar.X + 4, bar.Bottom - 3,
                    (int)((bar.Width - 8) * _fishing.Progress), 2),
                    new Color(120, 210, 250));
        // escalating mood label above the bar
        string mood = _fishing.Progress > 0.75f ? "ALMOST THERE!"
                    : _fishing.Progress > 0.34f ? "KEEP GOING!" : "PULL ON THE BLUE";
        _font.Draw(_batch, mood, new Vector2(bar.X, bar.Y - 26), 2,
                   new Color(220, 226, 240));
        switch (_fishing.LastPull)
        {
            case FishingSim.PullGrade.Perfect:
                _font.Draw(_batch, "PERFECT +3", new Vector2(w / 2f - 36, bar.Y - 30),
                           3, new Color(255, 220, 90));
                break;
            case FishingSim.PullGrade.Good:
                _font.Draw(_batch, "GOOD +1", new Vector2(w / 2f - 28, bar.Y - 30),
                           3, new Color(150, 220, 130));
                break;
            case FishingSim.PullGrade.Miss:
                _font.Draw(_batch, "MISS -2", new Vector2(w / 2f - 28, bar.Y - 30),
                           3, new Color(240, 120, 100));
                break;
        }
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
    private readonly List<(Vector3 pos, float age)> _ripples = new();
    private Color _skyClear = new(84, 108, 130), _skyFog = new(140, 158, 184);
    private (Color col, float a) _tint = (new Color(255, 255, 255), 0f);

    private void LoadAudio()
    {
        string P(string n) => Path.Combine(AppContext.BaseDirectory, "Audio", n);
        try
        {
            _splash = SoundEffect.FromStream(File.OpenRead(P("splash.wav")));
            _splashBig = SoundEffect.FromStream(File.OpenRead(P("splash_big.wav")));
            _click = SoundEffect.FromStream(File.OpenRead(P("click.wav")));
            _whoosh = SoundEffect.FromStream(File.OpenRead(P("whoosh.wav")));
            _birds = SoundEffect.FromStream(File.OpenRead(P("birds.wav")))
                                .CreateInstance();
            _birds.IsLooped = true;
            _birds.Volume = 0.22f;
            _birds.Play();
        }
        catch
        {
            // the game runs silently if audio files are missing
        }
    }

    private void PlaySfx(string name)
    {
        try
        {
            switch (name)
            {
                case "whoosh": _whoosh.Play(0.5f, 0f, 0f); break;
                case "splash": _splash.Play(0.6f, 0f, 0f); break;
                case "big": _splashBig.Play(0.7f, 0f, 0f); break;
                case "click": break;                      // UI clicks stay silent
                case "reel_tick": break;                   // the reel pulls are silent too
            }
        }
        catch
        {
        }
    }

    // stars live in world space on a dome ahead of the dock, so the sky
    // stays put when the camera moves
    private static readonly (float x, float y, float z, int size, float bright)[] _stars =
        MakeStars(7);

    private static (float, float, float, int, float)[] MakeStars(int seed)
    {
        var rng = new Random(seed);
        var stars = new (float, float, float, int, float)[70];
        for (int i = 0; i < stars.Length; i++)
            stars[i] = (-60f + (float)rng.NextDouble() * 120f,
                        6f + (float)rng.NextDouble() * 44f,
                        -75f + (float)rng.NextDouble() * 70f,
                        rng.Next(1, 3),
                        0.35f + (float)rng.NextDouble() * 0.65f);
        return stars;
    }

    /// <summary>Project a world-space sky point to low-res screen space,
    /// or null when it sits outside the camera frustum.</summary>
    private Vector2? SkyPoint(Vector3 world, int w, int h)
    {
        var clip = Vector4.Transform(new Vector4(world, 1f), _view * _proj);
        if (clip.W <= 0.0001f)
            return null;
        float x = clip.X / clip.W, y = clip.Y / clip.W, z = clip.Z / clip.W;
        if (z < 0f || z > 1f || x < -1f || x > 1f || y < -1f || y > 1f)
            return null;
        return new Vector2((x + 1f) * 0.5f * w, (1f - y) * 0.5f * h);
    }

    // sky keyframes around the clock (hour, sky, fog, tint colour/alpha)
    private static readonly (float hour, Color sky, Color fog, Color tint, float tintA)[] SkyKeys =
    {
        ( 5f, new Color( 88,  92, 122), new Color(214, 168, 140), new Color(255, 190, 150), 0.10f),
        ( 8f, new Color( 84, 116, 146), new Color(158, 176, 198), new Color(255, 255, 255), 0.00f),
        (17f, new Color( 96,  84, 116), new Color(196, 140,  96), new Color(255, 160,  90), 0.14f),
        (20f, new Color( 14,  20,  42), new Color( 20,  30,  56), new Color( 24,  40, 110), 0.30f),
        (29f, new Color( 88,  92, 122), new Color(214, 168, 140), new Color(255, 190, 150), 0.10f),
    };

    /// <summary>Continuous time of day: sky, fog and tint lerp between the
    /// keyframes so dawn and dusk glide instead of snapping.</summary>
    private void SetDayPhase()
    {
        float h = _state.Hour < 5f ? _state.Hour + 24f : _state.Hour;
        for (int i = 0; i < SkyKeys.Length - 1; i++)
        {
            var (h0, s0, f0, t0, a0) = SkyKeys[i];
            var (h1, s1, f1, t1, a1) = SkyKeys[i + 1];
            if (h >= h0 && h < h1)
            {
                float t = (h - h0) / (h1 - h0);
                _skyClear = Color.Lerp(s0, s1, t);
                _skyFog = Color.Lerp(f0, f1, t);
                _tint = (Color.Lerp(t0, t1, t), MathHelper.Lerp(a0, a1, t));
                // smoking saturates the sky
                _skyClear = Color.Lerp(_skyClear, Vivid(_skyClear), _state.High * 0.65f);
                _skyFog = Color.Lerp(_skyFog, Vivid(_skyFog), _state.High * 0.4f);
                break;
            }
        }
        _effect.FogColor = _skyFog.ToVector3();
        _effectCullNone.FogColor = _skyFog.ToVector3();
    }

    /// <summary>The sun's world position on its arc across the water.</summary>
    private static Vector3 SunWorld(float hour)
    {
        float p = Math.Clamp((hour - 5f) / 15f, 0f, 1f);
        return new Vector3(MathF.Sin(p * MathF.PI) * 46f,
                           24f + MathF.Sin(p * MathF.PI) * 26f,
                           -16f - p * 36f);
    }

    private static Vector3 SunDirection(float hour) =>
        Vector3.Normalize(SunWorld(hour));

    /// <summary>Point the lit effect's sun at the world per the clock:
    /// warm daylight, cool moonlight, with the ambient ramping alongside.</summary>
    private void SetLighting()
    {
        float day = 1f - Nightness(_state.Hour);
        // smoking doesn't wobble the view any more - it saturates the world
        float vib = 1f + 0.45f * _state.High;
        _effectLit.AmbientLightColor = Vector3.Lerp(
            new Vector3(0.16f, 0.18f, 0.24f), new Vector3(0.44f, 0.46f, 0.48f), day)
            * new Vector3(vib, vib * 1.03f, vib * 1.06f);
        _effectLit.DirectionalLight0.DiffuseColor = Vector3.Lerp(
            new Vector3(0.20f, 0.24f, 0.34f), new Vector3(1.00f, 0.93f, 0.80f), day)
            * vib;
        _effectLit.DirectionalLight0.Direction = -SunDirection(_state.Hour);
        _effectLit.DirectionalLight0.SpecularColor =
            new Vector3(0.15f, 0.15f, 0.15f) * day * vib;
    }

    /// <summary>0 by day, 1 in deep night; ramps through dawn and dusk.</summary>
    private static float Nightness(float h)
    {
        float a = Math.Clamp((h - 5f) / 3f, 0f, 1f);   // dawn ramp 5->8
        float b = Math.Clamp((20f - h) / 3f, 0f, 1f);  // dusk ramp 17->20
        return 1f - a * b;
    }

    /// <summary>Pushes a colour's dominant channels out - the high tint.</summary>
    private static Color Vivid(Color c)
    {
        int mn = Math.Min(c.R, Math.Min(c.G, c.B));
        return new Color(
            (byte)Math.Clamp(c.R + (c.R - mn) * 2, 0, 255),
            (byte)Math.Clamp(c.G + (c.G - mn) * 2, 0, 255),
            (byte)Math.Clamp(c.B + (c.B - mn) * 2, 0, 255),
            c.A);
    }

    private static Rectangle[] IconRects()
    {
        int h = 480;   // window is fixed 800x480; icons anchor bottom-left
        var rects = new Rectangle[7];
        for (int i = 0; i < 7; i++)
            rects[i] = new Rectangle(12 + i * 44, h - 44, 40, 36);
        return rects;
    }

    /// <summary>Pixel-art icon tiles for the menu shortcuts, bottom-left.</summary>
    private void DrawIcons()
    {
        var rects = IconRects();
        var hover = Mouse.GetState().Position;
        for (int i = 0; i < rects.Length; i++)
        {
            var r = rects[i];
            bool over = r.Contains(hover);
            _batch.Draw(_pixel, r, over ? new Color(38, 46, 68) : new Color(18, 22, 36, 242));
            _batch.Draw(_pixel, new Rectangle(r.X, r.Y, r.Width, 2),
                        over ? new Color(255, 255, 200) : new Color(66, 74, 102));
            int cx = r.X + 8, cy = r.Y + 7;
            switch (i)
            {
                case 0: // phone -> menu
                    _batch.Draw(_pixel, new Rectangle(cx + 4, cy + 1, 16, 22), new Color(28, 32, 40));
                    _batch.Draw(_pixel, new Rectangle(cx + 7, cy + 5, 10, 14), new Color(90, 232, 205));
                    break;
                case 1: // store -> price tag with a $
                    _batch.Draw(_pixel, new Rectangle(cx + 1, cy + 2, 20, 20), new Color(226, 178, 66));
                    _batch.Draw(_pixel, new Rectangle(cx + 3, cy + 4, 4, 4), new Color(28, 24, 18));
                    _font.Draw(_batch, "$", new Vector2(cx + 10, cy + 8), 2, new Color(60, 42, 12));
                    break;
                case 2: // cooler -> sal's shack
                    _batch.Draw(_pixel, new Rectangle(cx + 2, cy + 4, 20, 18), new Color(200, 60, 48));
                    _batch.Draw(_pixel, new Rectangle(cx + 1, cy + 2, 22, 3), new Color(232, 236, 240));
                    _batch.Draw(_pixel, new Rectangle(cx + 2, cy + 10, 20, 2), new Color(150, 40, 32));
                    break;
                case 3: // map
                    _batch.Draw(_pixel, new Rectangle(cx + 2, cy + 2, 20, 20), new Color(196, 168, 118));
                    _batch.Draw(_pixel, new Rectangle(cx + 6, cy + 16, 2, 2), new Color(84, 140, 84));
                    _batch.Draw(_pixel, new Rectangle(cx + 12, cy + 12, 2, 2), new Color(84, 140, 84));
                    _batch.Draw(_pixel, new Rectangle(cx + 16, cy + 5, 2, 2), new Color(120, 190, 130));
                    break;
                case 4: // jars
                    _batch.Draw(_pixel, new Rectangle(cx + 5, cy + 1, 14, 4), new Color(120, 128, 148));
                    _batch.Draw(_pixel, new Rectangle(cx + 5, cy + 5, 14, 16), new Color(120, 216, 160));
                    _batch.Draw(_pixel, new Rectangle(cx + 8, cy + 8, 8, 8), new Color(60, 150, 100));
                    break;
                case 5: // journal
                    _batch.Draw(_pixel, new Rectangle(cx + 3, cy + 1, 18, 21), new Color(238, 232, 212));
                    _batch.Draw(_pixel, new Rectangle(cx + 12, cy + 1, 2, 21), new Color(160, 150, 120));
                    _batch.Draw(_pixel, new Rectangle(cx + 6, cy + 6, 4, 1), new Color(120, 110, 90));
                    _batch.Draw(_pixel, new Rectangle(cx + 6, cy + 10, 4, 1), new Color(120, 110, 90));
                    break;
                case 6: // stoke lanes
                    _batch.Draw(_pixel, new Rectangle(cx + 3, cy + 6, 5, 12), new Color(120, 190, 130));
                    _batch.Draw(_pixel, new Rectangle(cx + 10, cy + 3, 5, 15), new Color(150, 210, 236));
                    _batch.Draw(_pixel, new Rectangle(cx + 17, cy + 8, 5, 10), new Color(214, 150, 236));
                    break;
            }
        }
    }

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
        _font.Draw(_batch, "LV " + _state.Level + "  XP " + _state.Xp,
                   new Vector2(14, 76), 2, new Color(200, 190, 240));
        // chill/sober meter + munchie meter, above the icon bar
        int py = h - 108;
        _batch.Draw(_pixel, new Rectangle(12, py, 288, 60), new Color(16, 20, 32, 205));
        string state = _state.Couch > 0f ? "COUCH LOCKED"
            : _state.Chill <= 1f ? "SOBER"
            : _state.Chill < 10f ? "CHILL"
            : _state.Chill < 30f ? "HIGH"
            : _state.Chill < 50f ? "BAKED"
            : _state.Chill < 70f ? "BLAZED"
            : _state.Chill < 85f ? "STONED" : "ZONKED";
        _font.Draw(_batch, state, new Vector2(22, py + 8), 2,
                   _state.Couch > 0f ? new Color(230, 140, 120) : new Color(150, 190, 160));
        string stash = _state.WeedCount() > 0 ? _state.StrainData().Name.ToUpper() : "DRY";
        _font.Draw(_batch, stash, new Vector2(288 - 12 - stash.Length * 8f, py + 8), 2,
                   _state.WeedCount() > 0 ? new Color(160, 220, 160) : new Color(120, 126, 148));
        _batch.Draw(_pixel, new Rectangle(22, py + 26, 260, 10), new Color(10, 12, 18));
        _batch.Draw(_pixel, new Rectangle(22, py + 26,
                    (int)(260f * Math.Clamp(_state.Chill / 100f, 0f, 1f)), 10),
                    new Color(150, 216, 160));
        if (_state.Chill <= 1f)
            _font.Draw(_batch, "SOBER", new Vector2(22 + 130f - 12f, py + 27), 1,
                       new Color(150, 170, 156));
        // munchie meter: fills when you smoke, only snacks bring it down
        bool hungry = _state.Munchie >= 80f;
        var munCol = hungry
            ? Color.Lerp(new Color(255, 160, 90), new Color(255, 235, 190),
                         0.5f + 0.5f * MathF.Sin(_t * 8f))
            : new Color(240, 150, 80);
        _font.Draw(_batch, hungry ? "MUNCHIES!" : "MUNCH", new Vector2(22, py + 44), 2, munCol);
        _batch.Draw(_pixel, new Rectangle(130, py + 47, 140, 9), new Color(10, 12, 18));
        _batch.Draw(_pixel, new Rectangle(130, py + 47,
                    (int)(140f * Math.Clamp(_state.Munchie / 100f, 0f, 1f)), 9), munCol);
        _font.Draw(_batch, "$" + _state.Cash, new Vector2(w - 100, 14), 3, new Color(240, 214, 120));
        _font.Draw(_batch, "COOLER " + _state.Bag.Count + "/8", new Vector2(w - 140, 40), 2,
                   new Color(246, 170, 120));
        if (_sellFlash > 0f)
            _font.Draw(_batch, "SOLD!", new Vector2(w - 100, 62), 2, new Color(240, 214, 120));
        _font.Draw(_batch, "SPACE CAST  E SMOKE  Q PIECE  X SELL  ESC MENU",
                   new Vector2(336, h - 24), 2, new Color(150, 160, 190));
        DrawIcons();
        _batch.End();
        _sellFlash -= 0.016f;
    }

    private void UpdateOverlays(float dt, KeyboardState kb)
    {
        _uiMsgT -= dt;
        void Edge(ref bool held, Keys k, Action a)
        {
            if (kb.IsKeyDown(k) && !held) { a(); held = true; }
            if (kb.IsKeyUp(k)) held = false;
        }
        int max = _journalOpen ? GameData.Species.Length
                : _zaaOpen ? GameData.Strains.Length : 0;
        if (_journalOpen || _zaaOpen)
        {
            Edge(ref _upHeld, Keys.Up, () =>
            {
                if (_journalOpen) _journalSel = Math.Max(0, _journalSel - 1);
                else _zaaSel = Math.Max(0, _zaaSel - 1);
            });
            Edge(ref _downHeld, Keys.Down, () =>
            {
                if (_journalOpen) _journalSel = Math.Min(max - 1, _journalSel + 1);
                else _zaaSel = Math.Min(max - 1, _zaaSel + 1);
            });
        }
        if (_zaaOpen)
            Edge(ref _enterHeld, Keys.Enter, PickStrain);
        if (kb.IsKeyDown(Keys.Escape) && !_escHeld)
        {
            _journalOpen = false;
            _zaaOpen = false;
            _menuOpen = true;
            _escHeld = true;
        }
        if (kb.IsKeyUp(Keys.Escape)) _escHeld = false;

        void PickStrain()
        {
            var st = GameData.Strains[_zaaSel];
            if (_state.WeedCount(st.Id) > 0)
            {
                _state.Strain = st.Id;
                _state.StrainIndex = _zaaSel;
                _state.Save();
                _uiMsg = "smoking " + st.Name;
                _uiMsgT = 2f;
            }
            else
            {
                _uiMsg = "jar empty - buy a gram from Sal (D)";
                _uiMsgT = 2f;
            }
        }
    }

    private static Color RarityCol(string r) => r switch
    {
        "uncommon" => new Color(130, 210, 130),
        "rare" => new Color(110, 160, 235),
        "epic" => new Color(196, 130, 235),
        "legendary" => new Color(240, 200, 90),
        _ => new Color(160, 166, 180),
    };

    private void DrawJournal()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        _batch.Begin(samplerState: SamplerState.PointClamp);
        _batch.Draw(_pixel, new Rectangle(0, 0, w, h), new Color(10, 14, 26, 235));
        int caught = _state.Species.Count;
        _font.Draw(_batch, "JOURNAL  -  " + caught + "/" + GameData.Species.Length + " species",
                   new Vector2(40, 30), 3, new Color(238, 232, 212));
        int y = 80;
        for (int i = 0; i < GameData.Species.Length; i++)
        {
            var sp = GameData.Species[i];
            bool have = _state.Species.ContainsKey(sp.Id);
            var col = have ? RarityCol(sp.Rarity) : new Color(96, 102, 124);
            string line = (i == _journalSel ? ">" : " ") + " "
                + (have ? sp.Name.ToUpper() : "???")
                + "  (" + sp.Rarity.ToUpper() + ")";
            _font.Draw(_batch, line, new Vector2(60, y), 2, col);
            if (have)
                _font.Draw(_batch, "x" + _state.Species[sp.Id], new Vector2(w - 160, y), 2, col);
            y += 24;
            if (y > h - 80)
                break;
        }
        // ---- weed stash column: what you've bought and what's loaded
        int wx = w - 330;
        _font.Draw(_batch, "WEED STASH  " + _state.WeedTotal() + " GRAMS",
                   new Vector2(wx, 30), 3, new Color(150, 216, 160));
        int wy = 80;
        foreach (var kv in _state.WeedCounts.Where(kv => kv.Value > 0)
                     .OrderByDescending(kv => kv.Value))
        {
            var st = GameData.Strains.FirstOrDefault(x => x.Id == kv.Key);
            if (st == null)
                continue;
            bool inUse = _state.Strain == st.Id;
            var col = inUse ? new Color(255, 240, 140) : new Color(150, 216, 160);
            _font.Draw(_batch, st.Name.ToUpper(), new Vector2(wx + 8, wy), 2, col);
            _font.Draw(_batch, "X" + kv.Value + (inUse ? "  IN USE" : ""),
                       new Vector2(wx + 220, wy), 2, col);
            wy += 26;
            if (wy > h - 80)
                break;
        }
        if (_state.WeedTotal() == 0)
            _font.Draw(_batch, "DRY - SEE SAL (COOLER ICON)", new Vector2(wx + 8, wy), 2,
                       new Color(120, 126, 148));
        if (_uiMsgT > 0f)
            _font.Draw(_batch, _uiMsg, new Vector2(40, h - 60), 2, new Color(150, 216, 160));
        _font.Draw(_batch, "UP/DOWN  ESC BACK", new Vector2(40, h - 30), 2, new Color(130, 140, 170));
        _batch.End();
    }

    private void DrawZaa()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        _batch.Begin(samplerState: SamplerState.PointClamp);
        _batch.Draw(_pixel, new Rectangle(0, 0, w, h), new Color(12, 18, 22, 235));
        _font.Draw(_batch, "SALS JARS  -  pick your smoke", new Vector2(40, 30), 3,
                   new Color(150, 216, 160));
        int y = 80;
        for (int i = 0; i < GameData.Strains.Length; i++)
        {
            var st = GameData.Strains[i];
            bool open = _state.JarAvailable(st);
            bool current = _state.Strain == st.Id;
            var col = current ? new Color(255, 240, 140)
                    : open ? new Color(150, 216, 160) : new Color(96, 102, 124);
            string line = (i == _zaaSel ? ">" : " ") + " "
                + (open || current ? st.Name.ToUpper() : "???  (TIER " + st.Tier + ")");
            _font.Draw(_batch, line, new Vector2(60, y), 2, col);
            if (open)
                _font.Draw(_batch, "P" + st.Potency + "  L+" + (int)(st.Luck * 100),
                           new Vector2(w - 200, y), 2, col);
            y += 24;
            if (y > h - 80)
                break;
        }
        if (_uiMsgT > 0f)
            _font.Draw(_batch, _uiMsg, new Vector2(40, h - 60), 2, new Color(150, 216, 160));
        _font.Draw(_batch, "UP/DOWN PICK  ENTER LIGHT UP  ESC BACK", new Vector2(40, h - 30),
                   2, new Color(130, 140, 170));
        _batch.End();
    }

    private static readonly string[] MenuItems =
    {
        "STORE", "SALS SHACK", "JARS", "JOURNAL", "STOKE",
        "MAP AND TRAVEL", "HOW TO PLAY", "SAVE", "SAVE AND TITLE",
    };

    /// <summary>The one dropdown menu: arrows navigate, Enter selects,
    /// Esc backs out. Every old key-bound screen opens from here.</summary>
    private void UpdateMenu(float dt, KeyboardState kb)
    {
        _menuMsgT -= dt;
        void Edge(ref bool held, Keys k, Action a)
        {
            if (kb.IsKeyDown(k) && !held) { a(); held = true; }
            if (kb.IsKeyUp(k)) held = false;
        }
        Edge(ref _upHeld, Keys.Up, () => _menuSel = Math.Max(0, _menuSel - 1));
        Edge(ref _downHeld, Keys.Down,
             () => _menuSel = Math.Min(MenuItems.Length - 1, _menuSel + 1));
        Edge(ref _enterHeld, Keys.Enter, ActMenuItem);
        Edge(ref _escHeld, Keys.Escape, () => _menuOpen = false);
    }

    private void ActMenuItem()
    {
        _menuOpen = false;
        switch (MenuItems[_menuSel])
        {
            case "STORE":
                _shopOpen = true; _shopSel = 0;
                break;
            case "SALS SHACK":
                _dealerOpen = true; _dealerSel = 0; _dealerArea = _state.Spot;
                break;
            case "JARS":
                _zaaOpen = true; _zaaSel = 0;
                break;
            case "JOURNAL":
                _journalOpen = true; _journalSel = 0;
                break;
            case "STOKE":
                _stokeOpen = true; _stokeSel = 0;
                break;
            case "MAP AND TRAVEL":
                _mapOpen = true; _mapSel = _spot;
                break;
            case "HOW TO PLAY":
                _helpOpen = true;
                break;
            case "SAVE":
                _state.Save();
                _uiMsg = "saved";
                _uiMsgT = 2f;
                break;
            case "SAVE AND TITLE":
                _state.Save();
                _atTitle = true;
                SteamMan.SetPresence("menu");
                break;
        }
    }

    // ----------------------------------------------------------------- mouse --
    /// <summary>World-item hit rectangles: project a box through the camera
    /// and return its screen bounds (null when off screen).</summary>
    private Rectangle? HitRect(Vector3 center, float hw, float hh, float hd)
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        float minX = 1e9f, minY = 1e9f, maxX = -1e9f, maxY = -1e9f;
        for (int i = 0; i < 8; i++)
        {
            var p = center + new Vector3(
                (i & 1) == 1 ? hw : -hw,
                (i & 2) == 2 ? hh : -hh,
                (i & 4) == 4 ? hd : -hd);
            if (SkyPoint(p, w, h) is not { } q)
                return null;
            minX = MathF.Min(minX, q.X); maxX = MathF.Max(maxX, q.X);
            minY = MathF.Min(minY, q.Y); maxY = MathF.Max(maxY, q.Y);
        }
        return new Rectangle((int)minX, (int)minY,
                             Math.Max(1, (int)(maxX - minX)), Math.Max(1, (int)(maxY - minY)));
    }

    /// <summary>Click routing: menu first, sub-screens, then world items.</summary>
    private void HandleClick(Point mouse)
    {
        if (_atTitle)
            return;   // the title screen stays keyboard-driven for now
        var pos = new Vector2(mouse.X, mouse.Y);
        if (_menuOpen) { MenuClick(pos); return; }
        if (_shopOpen) { ShopClick(pos); return; }
        if (_dealerOpen) { DealerClick(pos); return; }
        if (_mapOpen) { MapClick(pos); return; }
        if (_zaaOpen || _journalOpen || _stokeOpen || _helpOpen)
            return;

        // the bottom-left icon bar
        var rects = IconRects();
        for (int i = 0; i < rects.Length; i++)
        {
            if (!rects[i].Contains(mouse))
                continue;
            switch (i)
            {
                case 0: _menuOpen = true; _menuSel = 0; break;
                case 1: _shopOpen = true; _shopSel = 0; break;
                case 2: _dealerOpen = true; _dealerSel = 0; _dealerArea = _state.Spot; break;
                case 3: _mapOpen = true; _mapSel = _spot; break;
                case 4: _zaaOpen = true; _zaaSel = 0; break;
                case 5: _journalOpen = true; _journalSel = 0; break;
                case 6: _stokeOpen = true; _stokeSel = 0; break;
            }
            PlaySfx("click");
            return;
        }

        // the glowing phone on the cooler opens the menu
        if (HitRect(new Vector3(-3.20f, 1.16f, 0.95f), 0.10f, 0.05f, 0.15f)
            is { } pr && pr.Contains(mouse))
        {
            _menuOpen = true;
            _menuSel = 0;
            PlaySfx("click");
            return;
        }
        // the cooler itself opens Sal's shack
        if (HitRect(new Vector3(-3.20f, 0.80f, 0.95f), 0.32f, 0.30f, 0.42f)
            is { } cr && cr.Contains(mouse))
        {
            _dealerOpen = true;
            _dealerSel = 0;
            _dealerArea = _state.Spot;
            PlaySfx("click");
        }
    }

    private void MenuClick(Vector2 pos)
    {
        int y0 = 62 + 3 * 22 + 8;
        for (int i = 0; i < MenuItems.Length; i++)
        {
            int y = y0 + i * 32;
            if (pos.X >= 40 && pos.X <= 430 && pos.Y >= y - 4 && pos.Y <= y + 26)
            {
                _menuSel = i;
                ActMenuItem();
                PlaySfx("click");
                return;
            }
        }
    }

    private void DrawMenu()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        _batch.Begin(samplerState: SamplerState.PointClamp);
        _batch.Draw(_pixel, new Rectangle(0, 0, w, h), new Color(6, 8, 16, 236));
        _font.Draw(_batch, "MENU", new Vector2(40, 26), 4, new Color(238, 232, 212));
        string[] stats =
        {
            "TIME AFLOAT " + (int)_state.Played + " MIN   CASTS " + _state.Casts
            + "   FISH " + _state.Catches,
            "SPECIES " + _state.Species.Count + "/40   COOLER " + _state.Bag.Count
            + "/" + _state.BagSize() + "   LV " + _state.Level + "   XP " + _state.Xp,
            "STOKE POINTS " + _state.StokePts + "   CASH $" + _state.Cash,
        };
        int y = 62;
        foreach (var line in stats)
        {
            _font.Draw(_batch, line, new Vector2(40, y), 2, new Color(150, 160, 200));
            y += 22;
        }
        y += 8;
        var hover = Mouse.GetState().Position.ToVector2();
        for (int i = 0; i < MenuItems.Length; i++)
        {
            bool over = hover.X >= 40 && hover.X <= 430
                        && hover.Y >= y - 4 && hover.Y <= y + 26;
            var col = over || i == _menuSel
                ? new Color(255, 255, 200) : new Color(170, 180, 210);
            _font.Draw(_batch, (i == _menuSel ? "> " : "  ") + MenuItems[i],
                       new Vector2(40, y), 3, col);
            y += 32;
        }
        if (_uiMsgT > 0f)
            _font.Draw(_batch, _uiMsg, new Vector2(40, y + 4), 2, new Color(150, 216, 160));
        _font.Draw(_batch, "UP/DOWN  ENTER  ESC BACK", new Vector2(40, h - 28), 2,
                   new Color(130, 140, 170));
        _batch.End();
    }

    private void UpdateShop(float dt, KeyboardState kb)
    {
        _shopMsgT -= dt;
        void Edge(ref bool held, Keys k, Action a)
        {
            if (kb.IsKeyDown(k) && !held) { a(); held = true; }
            if (kb.IsKeyUp(k)) held = false;
        }
        Edge(ref _upHeld, Keys.Up, () => _shopSel = Math.Max(0, _shopSel - 1));
        Edge(ref _downHeld, Keys.Down, () => _shopSel++);
        Edge(ref _leftHeld, Keys.Left, () => { _shopTab = (_shopTab + 5) % 6; _shopSel = 0; });
        Edge(ref _rightHeld, Keys.Right, () => { _shopTab = (_shopTab + 1) % 6; _shopSel = 0; });
        Edge(ref _enterHeld, Keys.Enter, BuyShopItem);
        if (kb.IsKeyDown(Keys.Escape)) { _shopOpen = false; _menuOpen = true; }
    }

    /// <summary>Buy/act on the currently selected shop row.</summary>
    private void BuyShopItem()
    {
        if (_shopTab < 4)
        {
            string kind = new[] { "rod", "line", "lure", "cooler" }[_shopTab];
            var gear = _state.GearTable(kind);
            if (_shopSel >= gear.Length) return;
            var (ok, msg) = _state.BuyGear(kind, gear[_shopSel]);
            _shopMsg = msg; _shopMsgT = 2.2f;
        }
        else if (_shopTab == 4)
        {
            if (_shopSel >= 4) return;
            var (ok, msg) = _state.TryTravel(_shopSel);
            _shopMsg = ok ? "water unlocked - press T for the map" : msg;
            _shopMsgT = 2.6f;
            PlaySfx("click");
        }
        else
        {
            if (_shopSel >= GameData.Munchies.Length) return;
            var (ok, msg) = _state.BuySnack(GameData.Munchies[_shopSel]);
            _shopMsg = msg; _shopMsgT = 2.2f;
            if (ok) PlaySfx("click");
        }
    }

    /// <summary>Mouse: click a tab or a row to buy.</summary>
    private void ShopClick(Vector2 pos)
    {
        if (pos.Y >= 50 && pos.Y <= 76)
        {
            for (int i = 0; i < 6; i++)
            {
                if (pos.X >= 40 + i * 118 && pos.X <= 40 + i * 118 + 90)
                {
                    _shopTab = i;
                    _shopSel = 0;
                    return;
                }
            }
            return;
        }
        int y = 100;
        if (_shopTab < 4)
        {
            string kind = new[] { "rod", "line", "lure", "cooler" }[_shopTab];
            var gear = _state.GearTable(kind);
            for (int i = 0; i < gear.Length; i++)
            {
                if (pos.Y >= y - 4 && pos.Y <= y + 40)
                {
                    _shopSel = i;
                    BuyShopItem();
                    return;
                }
                y += 46;
            }
        }
        else if (_shopTab == 4)
        {
            for (int i = 0; i < 4; i++)
            {
                if (pos.Y >= y - 4 && pos.Y <= y + 40)
                {
                    _shopSel = i;
                    BuyShopItem();
                    return;
                }
                y += 46;
            }
        }
        else
        {
            for (int i = 0; i < GameData.Munchies.Length; i++)
            {
                if (pos.Y >= y - 4 && pos.Y <= y + 22)
                {
                    _shopSel = i;
                    BuyShopItem();
                    return;
                }
                y += 28;
            }
        }
    }

    private void DrawShop()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        _batch.Begin(samplerState: SamplerState.PointClamp);
        _batch.Draw(_pixel, new Rectangle(0, 0, w, h), new Color(10, 14, 26, 225));
        _font.Draw(_batch, "STORE  -  $" + _state.Cash, new Vector2(40, 30), 3,
                   new Color(240, 214, 120));
        string[] tabs = { "RODS", "LINES", "LURES", "COOLERS", "WATERS", "MUNCHIES" };
        for (int i = 0; i < 6; i++)
            _font.Draw(_batch, tabs[i], new Vector2(40 + i * 118, 60), 2,
                       i == _shopTab ? new Color(255, 255, 255)
                                     : new Color(110, 120, 150));
        int y = 100;
        if (_shopTab < 4)
        {
            string kind = new[] { "rod", "line", "lure", "cooler" }[_shopTab];
            var gear = _state.GearTable(kind);
            for (int i = 0; i < gear.Length; i++)
            {
                var g = gear[i];
                bool owned = _state.Owns(kind, g.Id);
                bool eq = _state.EquippedId(kind) == g.Id;
                var col = i == _shopSel ? new Color(255, 255, 200)
                          : owned ? new Color(150, 216, 160)
                          : new Color(170, 180, 210);
                _font.Draw(_batch, (i == _shopSel ? ">" : " ") + " " + g.Name.ToUpper(),
                           new Vector2(60, y), 2, col);
                string right = eq ? "EQUIPPED" : owned ? "OWNED"
                             : "$" + g.Cost;
                _font.Draw(_batch, right, new Vector2(w - 240, y), 2, col);
                _font.Draw(_batch, g.Desc, new Vector2(80, y + 22), 2,
                           new Color(120, 130, 160));
                y += 46;
            }
        }
        else if (_shopTab == 4)
        {
            for (int i = 0; i < 4; i++)
            {
                bool unlocked = _state.SpotUnlocked(i);
                bool here = i == _spot;
                var sp = GameData.Spots[i];
                var col = i == _shopSel ? new Color(255, 255, 200)
                          : unlocked ? new Color(150, 216, 160)
                          : new Color(110, 116, 140);
                _font.Draw(_batch, (i == _shopSel ? ">" : "  ") + " "
                           + (unlocked || here ? sp.Name.ToUpper() : "???"),
                           new Vector2(60, y), 2, col);
                string right = here ? "HERE" : unlocked ? "UNLOCKED" : "$" + sp.NeedCash;
                _font.Draw(_batch, right, new Vector2(w - 240, y), 2, col);
                string sub = unlocked || here ? sp.Blurb
                    : "Catch all " + GameData.SpeciesList.Count(f => f.Spot == Math.Max(0, i - 1))
                      + " in " + GameData.SpotNames[Math.Max(0, i - 1)] + " first";
                _font.Draw(_batch, sub, new Vector2(80, y + 22), 2,
                           new Color(120, 130, 160));
                y += 46;
            }
        }
        else
        {
            for (int i = 0; i < GameData.Munchies.Length; i++)
            {
                var m = GameData.Munchies[i];
                var col = i == _shopSel ? new Color(255, 255, 200)
                                        : new Color(255, 190, 120);
                _font.Draw(_batch, (i == _shopSel ? ">" : " ") + " " + m.Name,
                           new Vector2(60, y), 2, col);
                _font.Draw(_batch, "$" + m.Price, new Vector2(w - 240, y), 2, col);
                _font.Draw(_batch, "-" + m.Relief + " MUNCH", new Vector2(w - 160, y), 2,
                           new Color(255, 170, 90));
                y += 28;
            }
        }
        if (_shopMsgT > 0f)
            _font.Draw(_batch, _shopMsg, new Vector2(w / 2f - 120, h - 60), 2,
                       new Color(240, 214, 120));
        _font.Draw(_batch, "UP/DOWN SELECT  ENTER BUY  LEFT/RIGHT TAB  ESC BACK",
                   new Vector2(40, h - 30), 2, new Color(130, 140, 170));
        _batch.End();
    }

    private static char KeyChar(Keys k) => k switch
    {
        >= Keys.A and <= Keys.Z => (char)('a' + (k - Keys.A)),
        >= Keys.D0 and <= Keys.D9 => (char)('0' + (k - Keys.D0)),
        Keys.Space => ' ',
        Keys.OemMinus => '-',
        Keys.OemPeriod => '.',
        _ => '\0',
    };

    private void UpdateConsole(KeyboardState kb)
    {
        var pressed = kb.GetPressedKeys().ToHashSet();
        foreach (var k in pressed)
        {
            if (_prevKeys.Contains(k))
                continue;
            if (k == Keys.Back && _consoleInput.Length > 0)
                _consoleInput = _consoleInput[..^1];
            else if (k == Keys.Enter)
            {
                RunConsole(_consoleInput);
                _consoleInput = "";
            }
            else if (k == Keys.Escape)
            {
                _consoleOpen = false;
            }
            else
            {
                char ch = KeyChar(k);
                if (ch != '\0' && _consoleInput.Length < 40)
                    _consoleInput += ch;
            }
        }
        _prevKeys = pressed;
        _consoleMsgT -= 0.016f;
    }

    private void RunConsole(string cmd)
    {
        var parts = cmd.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        void Say(string m) { _consoleMsg = m; _consoleMsgT = 3f; }
        if (parts.Length == 0)
            return;
        switch (parts[0].ToLower())
        {
            case "help":
                Say("cash N | hour N | goto N | unlock | save");
                break;
            case "cash":
                _state.Cash = int.Parse(parts[1]);
                Say("cash " + _state.Cash);
                break;
            case "addcash":
                _state.Cash += int.Parse(parts[1]);
                Say("cash " + _state.Cash);
                break;
            case "hour":
                _state.Hour = float.Parse(parts[1]);
                Say("hour " + _state.Hour);
                break;
            case "goto":
                int s2 = int.Parse(parts[1]);
                if (s2 >= 0 && s2 < 4)
                {
                    _spot = s2;
                    _state.Spot = s2;
                    _state.Unlocked.Add(s2);   // dev console bypasses gating
                    _state.MaxWater = Math.Max(_state.MaxWater, s2);
                    BuildWorld();
                    Say("travelled");
                }
                break;
            case "unlock":
                UnlockAll();
                Say("everything unlocked");
                break;
            case "save":
                _state.Save();
                Say("saved");
                break;
            default:
                Say("?");
                break;
        }
        _state.Save();
    }

    private void UnlockAll()
    {
        _state.MaxWater = 3;
        for (int i = 0; i < 4; i++)
            _state.Unlocked.Add(i);
        _state.SalTier = 4;
        _state.Cash = 100000;
        foreach (var (kind, table) in new (string, Gear[])[]
                 { ("rod", GameData.Rods), ("line", GameData.Lines),
                   ("lure", GameData.Lures), ("cooler", GameData.Coolers) })
        {
            _state.OwnedGear[kind] = table.Select(g => g.Id).ToHashSet();
            _state.Equipped[kind] = table[^1].Id;
        }
        foreach (var f in GameData.Species)
            _state.Species[f.Id] = 1;
        _state.Save();
    }

    private void DrawConsole()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        _batch.Begin(samplerState: SamplerState.PointClamp);
        _batch.Draw(_pixel, new Rectangle(0, h - 46, w, 46), new Color(8, 10, 20, 235));
        if (_consoleMsgT > 0f)
            _font.Draw(_batch, _consoleMsg, new Vector2(20, h - 78), 2,
                       new Color(150, 216, 160));
        _font.Draw(_batch, "> " + _consoleInput + "_", new Vector2(20, h - 38), 2,
                   new Color(220, 226, 240));
        _font.Draw(_batch, "cash N  hour N  goto N  unlock  save", new Vector2(w - 300, h - 38),
                   2, new Color(110, 120, 150));
        _batch.End();
    }

    /// <summary>Banded sky gradient with a sun that crosses the sky, a moon
    /// and stars at night - all riding the continuous time of day.</summary>
    private void DrawSky()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        var horizon = new Color(
            (byte)(_skyClear.R + 70 > 255 ? 255 : _skyClear.R + 70),
            (byte)(_skyClear.G + 50 > 255 ? 255 : _skyClear.G + 50),
            (byte)(_skyClear.B + 40 > 255 ? 255 : _skyClear.B + 40));
        _batch.Begin();
        for (int i = 0; i < 24; i++)
        {
            float t = i / 23f;
            var col = Color.Lerp(_skyClear, horizon, t * t);
            _batch.Draw(_pixel, new Rectangle(0, h * i / 24, w, h / 24 + 1), col);
        }

        float hour = _state.Hour;
        float night = Nightness(hour);
        float sunA = 1f - night;
        if (sunA > 0.04f)
        {
            // the sun crosses the world sky, east to west over the water
            var sunWorld = SunWorld(hour);
            if (SkyPoint(sunWorld, w, h) is { } sp)
            {
                _batch.Draw(_pixel, new Rectangle((int)sp.X - 16, (int)sp.Y - 16, 32, 32),
                            new Color(255, 170, 80) * (0.30f * sunA));
                _batch.Draw(_pixel, new Rectangle((int)sp.X - 10, (int)sp.Y - 10, 20, 20),
                            new Color(255, 214, 120) * sunA);
            }
        }
        if (night > 0.45f)
        {
            float ma = (night - 0.45f) / 0.55f;
            if (SkyPoint(new Vector3(-30f, 38f, -30f), w, h) is { } mp)
            {
                int mx = (int)mp.X, my = (int)mp.Y;
                _batch.Draw(_pixel, new Rectangle(mx - 8, my - 8, 16, 16),
                            new Color(226, 232, 240) * ma);
                _batch.Draw(_pixel, new Rectangle(mx - 4, my - 12, 5, 5),
                            new Color(150, 160, 180) * ma);
                _batch.Draw(_pixel, new Rectangle(mx + 2, my + 3, 4, 4),
                            new Color(150, 160, 180) * ma);
            }
        }
        if (night > 0.06f)
        {
            foreach (var (sx, sy, sz, ssize, bright) in _stars)
            {
                if (SkyPoint(new Vector3(sx, sy, sz), w, h) is { } stp)
                {
                    float a = MathF.Min(1f, night) * bright;
                    _batch.Draw(_pixel, new Rectangle((int)stp.X, (int)stp.Y,
                                ssize, ssize), new Color(220, 230, 255) * a);
                }
            }
        }
        _batch.End();
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

    private void DrawVertsLit(List<VertexPositionNormalColor> verts, BasicEffect effect)
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
