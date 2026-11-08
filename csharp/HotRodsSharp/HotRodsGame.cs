namespace HotRodsSharp;

/// <summary>
/// Phase-2 port: the full pond scene - props from the Blender OBJs, dock and
/// shore built from the Python layout tables, animated water, the fisherman
/// model, and three camera modes (third / first / free, TAB to cycle).
/// </summary>
public partial class HotRodsGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private BasicEffect _effect = null!;
    private BasicEffect _effectCullNone = null!;

    // static world
    private readonly List<VertexPositionColor> _staticVerts = new();
    private int _staticTriCount;
    private int _spot;
    private GameState _state = GameState.Load();
    private readonly FishingSim _fishing;
    private readonly SmokingSim _smoking = new();
    private readonly FishRender _fish = new();
    private bool _eHeld, _qHeld, _xHeld, _lbHeld, _rbHeld;
    private bool _shopOpen, _bHeld, _enterHeld, _upHeld, _downHeld, _leftHeld, _rightHeld;
    private bool _journalOpen, _zaaOpen, _paused, _jHeld, _zHeld, _escHeld;
    private bool _atTitle = true, _tripsOpen, _dealerOpen, _stokeOpen, _helpOpen;
    private bool _dHeld, _kHeld, _hHeld, _tKeyHeld, _fHeld, _uHeld;
    private int _titleSel, _tripsSel = 1, _dealerSel, _dealerArea, _stokeSel;
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
        ("rock_a", new Color(128, 122, 112), 1.0f, "land"),
        ("rock_b", new Color(108, 104, 96), 1.0f, "land"),
        ("bush", new Color(86, 116, 72), 1.0f, "land"),
        ("kn_plant", new Color(74, 112, 58), 8.0f, "land"),
        ("kn_rocks_low", new Color(128, 124, 118), 3.0f, "land"),
        ("kn_rocks_high", new Color(122, 118, 112), 2.6f, "land"),
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
            ("kn_rocks_low", -20.0f, -9.4f, 1.9f, 1.00f),
        },
        // Tokers Trench
        new (string, float, float, float, float)[]
        {
            ("tree_a", -20.0f, -11.0f, 1.6f, 1.45f),
            ("tree_b", -8.0f, -11.6f, 4.0f, 1.55f),
            ("tree_a", 6.0f, -10.8f, 2.9f, 1.40f),
            ("tree_b", 18.0f, -11.4f, 5.6f, 1.50f),
            ("kn_rocks_high", -14.0f, -8.8f, 3.1f, 1.00f),
            ("kn_rocks_high", 10.0f, -8.4f, 5.9f, 0.85f),
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

        BuildWorld();
        _batch = new SpriteBatch(GraphicsDevice);
        _font = new BitmapFont(GraphicsDevice);
        LoadAudio();
        _fishing.Sfx = PlaySfx;
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    private void AddProp(List<VertexPositionColor> s, string name, float x,
                         float z, float rot, float scale, float yBias = 0f)
    {
        var (_, col, bscale, ground) = Manifest.First(m => m.name == name);
        bool blenderAxes = !name.StartsWith("kn_");
        var mesh = ObjMesh.Load(
            Path.Combine(AppContext.BaseDirectory, "Assets", name + ".obj"),
            scale * bscale, blenderAxes);
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

    private static readonly Dictionary<string, Color> FisherMats = new()
    {
        ["fisher_wader"] = Wader, ["fisher_boot"] = Boot,
        ["fisher_shirt"] = Shirt, ["fisher_vest"] = Vest,
        ["fisher_skin"] = Skin, ["fisher_hat"] = Hat,
        ["fisher_beard"] = Beard, ["fisher_face"] = Face,
        ["fisher_dark"] = Dark, ["fisher_nose"] = Nose,
        ["fisher_mouth"] = Mouth,
    };

    private void BuildAngler(List<VertexPositionColor> s)
    {
        // the fisherman is a single Blender asset, material-painted
        var m = ObjMesh.Load(
            Path.Combine(AppContext.BaseDirectory, "Assets", "fisherman.obj"), 1f);
        for (int i = 0; i < m.Tris.Count; i++)
        {
            var (a, b, c) = m.Tris[i];
            Color col = i < m.TriMats.Count && FisherMats.TryGetValue(m.TriMats[i], out var mc)
                ? mc : Skin;
            Prims.AddTri(s, m.Verts[a], m.Verts[b], m.Verts[c], col);
        }
        // arms and rod stay procedural: they swap between the fishing and
        // smoking poses every frame
    }

    private void EmitFishingPose(List<VertexPositionColor> v)
    {
        var hand = new Vector3(-3.03f, 1.00f, 1.57f);
        var tip = hand + new Vector3(1.15f, 0.75f, 0.10f);   // along the face
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
        if (kb.IsKeyDown(Keys.D) && !_dHeld)
        {
            _dealerOpen = !_dealerOpen;
            _dealerSel = 0;
            _dealerArea = _state.Spot;
            _dHeld = true;
        }
        if (kb.IsKeyUp(Keys.D)) _dHeld = false;
        if (_dealerOpen)
        {
            UpdateDealer(dt, kb);
            base.Update(gameTime);
            return;
        }
        if (kb.IsKeyDown(Keys.K) && !_kHeld)
        {
            _stokeOpen = !_stokeOpen;
            _stokeSel = 0;
            _kHeld = true;
        }
        if (kb.IsKeyUp(Keys.K)) _kHeld = false;
        if (_stokeOpen)
        {
            UpdateStoke(dt, kb);
            base.Update(gameTime);
            return;
        }
        if (kb.IsKeyDown(Keys.H) && !_hHeld)
        {
            _helpOpen = !_helpOpen;
            _hHeld = true;
        }
        if (kb.IsKeyUp(Keys.H)) _hHeld = false;
        if (_helpOpen)
        {
            if (kb.IsKeyDown(Keys.Escape))
                _helpOpen = false;
            base.Update(gameTime);
            return;
        }
        if (kb.IsKeyDown(Keys.B) && !_bHeld)
        {
            _shopOpen = !_shopOpen;
            _shopSel = 0;
            _bHeld = true;
        }
        if (kb.IsKeyUp(Keys.B)) _bHeld = false;
        if (_shopOpen)
        {
            UpdateShop(dt, kb);
            base.Update(gameTime);
            return;
        }
        if (kb.IsKeyDown(Keys.J) && !_jHeld)
        {
            _journalOpen = !_journalOpen;
            _journalSel = 0;
            _jHeld = true;
        }
        if (kb.IsKeyUp(Keys.J)) _jHeld = false;
        if (kb.IsKeyDown(Keys.Z) && !_zHeld)
        {
            _zaaOpen = !_zaaOpen;
            _zaaSel = 0;
            _zHeld = true;
        }
        if (kb.IsKeyUp(Keys.Z)) _zHeld = false;
        if (kb.IsKeyDown(Keys.Escape) && !_escHeld)
        {
            _paused = !_paused;
            _escHeld = true;
        }
        if (kb.IsKeyUp(Keys.Escape)) _escHeld = false;
        if (_journalOpen || _zaaOpen || _paused)
        {
            UpdateOverlays(dt, kb);
            base.Update(gameTime);
            return;
        }
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
                _state.MaxWater = Math.Max(_state.MaxWater, i);
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
        _smoking.Speed = 1f + 0.06f * _state.StokeRank("smoke");
        _state.High = MathF.Max(0f, _state.High
            - dt / (45f * (1f + 0.05f * _state.StokeRank("vibes"))));
        _state.Hour = (_state.Hour + dt / 60f) % 24f;
        _state.Played += dt / 60f;

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

        // a proper high wobbles the camera
        if (_mode != 1)
        {
            _yaw += _state.High * 0.05f * MathF.Sin(_t * 1.15f) * dt;
            _pitch += _state.High * 0.04f * MathF.Cos(_t * 0.9f) * dt;
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
        SetDayPhase();
        FishingSim.TickTime(dt);
        if (!_smoking.Active)
            _fishing.Update(dt, kb, new Vector3(-1.88f, 1.75f, 1.67f),
                            Vector3.Normalize(new Vector3(1.0f, -0.1f, 0.05f)));
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
        if (_atTitle)
        {
            DrawTitleScreen();
            base.Draw(gameTime);
            return;
        }
        GraphicsDevice.Clear(_skyClear);
        DrawSky();
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
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
            _fishing.EmitLine(dyn, new Vector3(-1.88f, 1.75f, 1.67f));
            _fishing.EmitBobber(dyn);
            EmitFightingFish(dyn);
        }
        EmitShimmer(dyn);

        // water first, then the static world over it
        DrawVerts(water, _effect);
        DrawVerts(_staticVerts.GetRange(0, _staticTriCount * 3), _effectCullNone);
        DrawVerts(dyn, _effectCullNone);
        DrawCatchCard();
        DrawHud();
        _batch.Begin();
        _batch.Draw(_pixel, new Rectangle(0, 0, GraphicsDevice.Viewport.Width,
                    GraphicsDevice.Viewport.Height), _tint.col * _tint.a);
        _batch.End();
        if (_shopOpen)
            DrawShop();
        if (_journalOpen)
            DrawJournal();
        if (_zaaOpen)
            DrawZaa();
        if (_paused)
            DrawPause();
        if (_dealerOpen)
            DrawDealer();
        if (_stokeOpen)
            DrawStoke();
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
            var r = new Rectangle(w / 2 - 170, h / 2 - 60, 340, 120);
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
        float blue = _fishing.BluePos, green = _fishing.GreenPos;
        for (int i = 0; i < 48; i++)
        {
            float a0 = -MathF.PI + MathF.Tau * i / 48;
            float a1 = a0 + MathF.Tau / 48;
            float mid = MathF.Abs(Wrap((a0 + a1) / 2 - blue));
            float midg = MathF.Abs(Wrap((a0 + a1) / 2 - green));
            Color col = mid < 0.10f ? new Color(90, 180, 250)
                      : midg < 0.28f ? new Color(110, 210, 120)
                      : new Color(190, 70, 70);
            var p0 = c + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * R;
            var p1 = c + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * R;
            var p2 = c + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * (R - 10);
            var p3 = c + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * (R - 10);
            FillTri(_batch, _pixel, p0, p1, p2, col);
            FillTri(_batch, _pixel, p0, p2, p3, col);
        }
        // needle: full 360 sweep
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
                case "click": _click.Play(0.35f, 0f, 0f); break;
            }
        }
        catch
        {
        }
    }

    private void SetDayPhase()
    {
        float h = _state.Hour;
        // dawn 5-8, day 8-17, dusk 17-20, night 20-5
        if (h >= 8 && h < 17)
        {
            _skyClear = new Color(84, 116, 146); _skyFog = new Color(158, 176, 198);
            _tint = (new Color(255, 255, 255), 0f);
        }
        else if (h >= 17 && h < 20)
        {
            _skyClear = new Color(96, 84, 116); _skyFog = new Color(196, 140, 96);
            _tint = (new Color(255, 160, 90), 0.14f);
        }
        else if (h >= 5 && h < 8)
        {
            _skyClear = new Color(88, 92, 122); _skyFog = new Color(214, 168, 140);
            _tint = (new Color(255, 190, 150), 0.10f);
        }
        else
        {
            _skyClear = new Color(14, 20, 42); _skyFog = new Color(20, 30, 56);
            _tint = (new Color(24, 40, 110), 0.30f);
        }
        _effect.FogColor = _skyFog.ToVector3();
        _effectCullNone.FogColor = _skyFog.ToVector3();
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
        string sd = _state.StrainData().Name.ToUpper();
        _font.Draw(_batch, "WEED  " + sd, new Vector2(14, 76), 2, new Color(150, 216, 160));
        _font.Draw(_batch, "HIGH  " + (int)(_state.High * 100) + "%",
                   new Vector2(14, 96), 2, new Color(150, 216, 160));
        _font.Draw(_batch, "$" + _state.Cash, new Vector2(w - 100, 14), 3, new Color(240, 214, 120));
        _font.Draw(_batch, "COOLER " + _state.Bag.Count + "/8", new Vector2(w - 140, 40), 2,
                   new Color(246, 170, 120));
        if (_sellFlash > 0f)
            _font.Draw(_batch, "SOLD!", new Vector2(w - 100, 62), 2, new Color(240, 214, 120));
        _font.Draw(_batch, "SPACE CAST  E SMOKE  B SHOP  D SAL  J LOG  Z JARS  K STOKE",
                   new Vector2(14, h - 24), 2, new Color(150, 160, 190));
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
        if (_paused)
        {
            void ToTitle()
            {
                _state.Save();
                _atTitle = true;
                _paused = false;
            }
            Edge(ref _enterHeld, Keys.Enter, ToTitle);
            Edge(ref _tKeyHeld, Keys.T, ToTitle);
            if (kb.IsKeyDown(Keys.S) && !_leftHeld)
            {
                _state.Save();
                _uiMsg = "saved";
                _uiMsgT = 2f;
                _leftHeld = true;
            }
            if (kb.IsKeyUp(Keys.S)) _leftHeld = false;
        }

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
        if (_uiMsgT > 0f)
            _font.Draw(_batch, _uiMsg, new Vector2(40, h - 60), 2, new Color(150, 216, 160));
        _font.Draw(_batch, "J CLOSE", new Vector2(40, h - 30), 2, new Color(130, 140, 170));
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
        _font.Draw(_batch, "UP/DOWN PICK  ENTER LIGHT UP  Z CLOSE", new Vector2(40, h - 30),
                   2, new Color(130, 140, 170));
        _batch.End();
    }

    private void DrawPause()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        _batch.Begin(samplerState: SamplerState.PointClamp);
        _batch.Draw(_pixel, new Rectangle(0, 0, w, h), new Color(6, 8, 16, 220));
        _font.Draw(_batch, "PAUSED", new Vector2(w / 2f - 40, 40), 4,
                   new Color(238, 232, 212));
        string[] stats =
        {
            "TIME AFLOAT  " + (int)_state.Played + " MIN",
            "CASTS  " + _state.Casts,
            "FISH LANDED  " + _state.Catches,
            "SPECIES  " + _state.Species.Count + "/40",
            "COOLER  " + _state.Bag.Count + "/" + _state.BagSize(),
            "STOKE POINTS  " + _state.StokePts,
        };
        int y = 90;
        foreach (var s in stats)
        {
            _font.Draw(_batch, s, new Vector2(w / 2f - 120, y), 2,
                       new Color(170, 180, 210));
            y += 26;
        }
        _font.Draw(_batch, "S  SAVE", new Vector2(w / 2f - 120, y + 6), 2,
                   new Color(170, 180, 210));
        _font.Draw(_batch, "T OR ENTER  SAVE + TITLE", new Vector2(w / 2f - 120, y + 32), 2,
                   new Color(170, 180, 210));
        _font.Draw(_batch, "ESC  RESUME", new Vector2(w / 2f - 120, y + 58), 2,
                   new Color(170, 180, 210));
        if (_uiMsgT > 0f)
            _font.Draw(_batch, _uiMsg, new Vector2(w / 2f - 120, y + 94), 2,
                       new Color(150, 216, 160));
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
        Edge(ref _leftHeld, Keys.Left, () => { _shopTab = (_shopTab + 4) % 5; _shopSel = 0; });
        Edge(ref _rightHeld, Keys.Right, () => { _shopTab = (_shopTab + 1) % 5; _shopSel = 0; });
        Edge(ref _enterHeld, Keys.Enter, Buy);
        if (kb.IsKeyDown(Keys.Escape)) { _shopOpen = false; }

        void Buy()
        {
            if (_shopTab < 4)
            {
                string kind = new[] { "rod", "line", "lure", "cooler" }[_shopTab];
                var gear = _state.GearTable(kind);
                if (_shopSel >= gear.Length) return;
                var (ok, msg) = _state.BuyGear(kind, gear[_shopSel]);
                _shopMsg = msg; _shopMsgT = 2.2f;
            }
            else
            {
                if (_shopSel >= GameData.Strains.Length) return;
                var (ok, msg) = _state.BuyStrain(GameData.Strains[_shopSel]);
                _shopMsg = msg; _shopMsgT = 2.2f;
            }
        }
    }

    private void DrawShop()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        _batch.Begin(samplerState: SamplerState.PointClamp);
        _batch.Draw(_pixel, new Rectangle(0, 0, w, h), new Color(10, 14, 26, 225));
        _font.Draw(_batch, "TRADING POST  -  $" + _state.Cash, new Vector2(40, 30), 3,
                   new Color(240, 214, 120));
        string[] tabs = { "RODS", "LINES", "LURES", "COOLERS", "WEED" };
        for (int i = 0; i < 5; i++)
            _font.Draw(_batch, tabs[i], new Vector2(40 + i * 130, 60), 2,
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
        else
        {
            for (int i = 0; i < GameData.Strains.Length; i++)
            {
                var st = GameData.Strains[i];
                bool open = _state.JarAvailable(st);
                bool current = _state.Strain == st.Id;
                var col = i == _shopSel ? new Color(255, 255, 200)
                          : open ? new Color(150, 216, 160)
                          : new Color(110, 116, 140);
                string name = open || current ? st.Name.ToUpper()
                              : "???  (TIER " + st.Tier + " FISH)";
                _font.Draw(_batch, (i == _shopSel ? ">" : " ") + " " + name,
                           new Vector2(60, y), 2, col);
                _font.Draw(_batch, open ? "$" + st.Price : "LOCKED",
                           new Vector2(w - 240, y), 2, col);
                y += 28;
            }
        }
        if (_shopMsgT > 0f)
            _font.Draw(_batch, _shopMsg, new Vector2(w / 2f - 120, h - 60), 2,
                       new Color(240, 214, 120));
        _font.Draw(_batch, "UP/DOWN SELECT  ENTER BUY  LEFT/RIGHT TAB  B CLOSE",
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

    /// <summary>Simple banded sky gradient, zenith to horizon.</summary>
    private void DrawSky()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        var horizon = new Color(
            (byte)(_skyClear.R + 70 > 255 ? 255 : _skyClear.R + 70),
            (byte)(_skyClear.G + 50 > 255 ? 255 : _skyClear.G + 50),
            (byte)(_skyClear.B + 40 > 255 ? 255 : _skyClear.B + 40));
        if (_state.Hour >= 20 || _state.Hour < 5)
            horizon = new Color(30, 44, 86);
        _batch.Begin();
        for (int i = 0; i < 12; i++)
        {
            float t = i / 11f;
            var col = Color.Lerp(_skyClear, horizon, t * t);
            _batch.Draw(_pixel, new Rectangle(0, h * i / 12, w, h / 12 + 1), col);
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
}
