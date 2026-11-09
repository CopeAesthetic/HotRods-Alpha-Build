using Godot;
using HotRodsSharp;

/// <summary>
/// HotRods HUD: top info line, chill/munchie panel, message line, the
/// rope reel bar, the FISH ON banner and the cast charge meter - all as
/// Control nodes, styled like the MonoGame port.
/// </summary>
public partial class Hud : CanvasLayer
{
    private GameState _state = null!;
    private FishingSim _fishing = null!;

    private Label _info = null!, _chillWord = null!, _strainName = null!,
                  _munchWord = null!, _msg = null!;
    private ColorRect _chillFill = null!, _munchieFill = null!;
    private ColorRect _panel = null!;

    private Control _reel = null!;
    private ColorRect _rope = null!, _zone = null!, _core = null!, _sweep = null!,
                      _missL = null!, _missR = null!, _prog = null!;
    private Label _mood = null!, _now = null!, _spaceHint = null!;

    private Control _fishOn = null!;
    private Label _fishOnText = null!, _fishOnSpace = null!;
    private ColorRect _fishOnBar = null!, _fishOnFill = null!;

    private ColorRect _chargeBar = null!, _chargeFill = null!;

    private float _msgT;
    private float _nowPulse;
    private readonly ColorRect[] _icons = new ColorRect[7];
    private Control _catcher = null!;
    private readonly Control[] _iconArt = new Control[7];

    /// <summary>Bottom-left icon bar: phone, store, Sal, map, journal,
    /// stoke, pouch. Clicking fires the mapped action.</summary>
    public event Action<int>? IconClicked;

    public override void _Ready()
    {
        BuildIcons();
        // ---- top info line
        _info = Label("", 22, new Color(0.93f, 0.9f, 0.8f));
        _info.Position = new Vector2(14, 10);
        AddChild(_info);

        // ---- bottom-left chill/munchie panel
        _panel = Rect(new Vector2(12, 612), new Vector2(288, 60), new Color(0.06f, 0.08f, 0.13f, 0.85f));
        AddChild(_panel);
        _chillWord = Label("SOBER", 18, new Color(0.59f, 0.75f, 0.63f));
        _chillWord.Position = new Vector2(22, 620);
        AddChild(_chillWord);
        _strainName = Label("DRY", 18, new Color(0.47f, 0.49f, 0.58f));
        _strainName.Position = new Vector2(200, 620);
        AddChild(_strainName);
        Rect(new Vector2(22, 644), new Vector2(260, 10), new Color(0.04f, 0.05f, 0.07f));
        _chillFill = Rect(new Vector2(22, 644), new Vector2(0, 10), new Color(0.59f, 0.85f, 0.63f));
        AddChild(_chillFill);
        _munchWord = Label("MUNCH", 18, new Color(0.94f, 0.59f, 0.31f));
        _munchWord.Position = new Vector2(22, 656);
        AddChild(_munchWord);
        Rect(new Vector2(130, 659), new Vector2(140, 9), new Color(0.04f, 0.05f, 0.07f));
        _munchieFill = Rect(new Vector2(130, 659), new Vector2(0, 9), new Color(0.94f, 0.59f, 0.31f));
        AddChild(_munchieFill);

        // ---- message line
        _msg = Label("", 30, new Color(1f, 0.82f, 0.35f));
        _msg.Position = new Vector2(0, 500);
        _msg.Size = new Vector2(1280, 40);
        _msg.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_msg);

        // ---- rope reel bar (hidden until a fish is on)
        _reel = new Control { Visible = false };
        _reel.Position = new Vector2(470, 598);
        _reel.Size = new Vector2(340, 120);
        AddChild(_reel);
        _rope = Rect(new Vector2(0, 26), new Vector2(340, 24), new Color(0.69f, 0.58f, 0.38f));
        _reel.AddChild(_rope);
        _missL = Rect(new Vector2(0, 28), new Vector2(0, 20), new Color(0.75f, 0.24f, 0.20f, 0.65f));
        _reel.AddChild(_missL);
        _missR = Rect(new Vector2(0, 28), new Vector2(0, 20), new Color(0.75f, 0.24f, 0.20f, 0.65f));
        _reel.AddChild(_missR);
        _zone = Rect(new Vector2(0, 27), new Vector2(0, 22), new Color(0.38f, 0.59f, 0.86f, 0.75f));
        _reel.AddChild(_zone);
        _core = Rect(new Vector2(0, 31), new Vector2(0, 14), new Color(0.47f, 0.94f, 0.51f, 0.85f));
        _reel.AddChild(_core);
        _sweep = Rect(new Vector2(0, 22), new Vector2(4, 32), new Color(1f, 0.94f, 0.55f));
        _reel.AddChild(_sweep);
        _prog = Rect(new Vector2(0, 46), new Vector2(0, 3), new Color(0.47f, 0.82f, 0.98f));
        _reel.AddChild(_prog);
        _mood = Label("PULL ON THE BLUE", 16, new Color(0.86f, 0.89f, 0.94f));
        _mood.Position = new Vector2(0, 0);
        _reel.AddChild(_mood);
        _now = Label("NOW!", 26, new Color(0.59f, 1f, 0.59f));
        _now.Position = new Vector2(145, -4);
        _reel.AddChild(_now);
        _spaceHint = Label("SPACE!", 18, new Color(0.59f, 1f, 0.59f));
        _spaceHint.Position = new Vector2(140, 52);
        _reel.AddChild(_spaceHint);

        // ---- FISH ON banner
        _fishOn = new Control { Visible = false };
        _fishOn.Position = new Vector2(490, 210);
        _fishOn.Size = new Vector2(300, 90);
        AddChild(_fishOn);
        _fishOnText = Label("FISH ON!", 40, new Color(1f, 0.82f, 0.35f));
        _fishOnText.Position = new Vector2(80, 0);
        _fishOn.AddChild(_fishOnText);
        _fishOnBar = Rect(new Vector2(50, 48), new Vector2(200, 8), new Color(0.12f, 0.13f, 0.20f, 0.9f));
        _fishOn.AddChild(_fishOnBar);
        _fishOnFill = Rect(new Vector2(51, 49), new Vector2(198, 6), new Color(1f, 0.71f, 0.31f));
        _fishOn.AddChild(_fishOnFill);
        _fishOnSpace = Label("SPACE!", 22, new Color(1f, 0.82f, 0.35f));
        _fishOnSpace.Position = new Vector2(110, 58);
        _fishOn.AddChild(_fishOnSpace);

        // ---- cast charge meter
        _chargeBar = Rect(new Vector2(540, 630), new Vector2(200, 14), new Color(0.16f, 0.18f, 0.26f, 0.85f));
        _chargeBar.Visible = false;
        AddChild(_chargeBar);
        _chargeFill = Rect(new Vector2(542, 632), new Vector2(0, 10), new Color(0.94f, 0.78f, 0.35f));
        AddChild(_chargeFill);
    }

    public void Bind(GameState state, FishingSim fishing)
    {
        _state = state;
        _fishing = fishing;
    }

    public void Flash(string msg, float t)
    {
        _msgT = t;
        _msg.Text = msg;
    }

    public void Tick(float dt, float time)
    {
        IconHover();
        _info.Text = $"{GameData.SpotNames[_state.Spot]}   {_state.Hour:00}:00   " +
                     $"CASTS {_state.Casts}   LV {_state.Level}  XP {_state.Xp}  ${_state.Cash}";

        // chill + munchie panel
        string state = _state.Couch > 0f ? "COUCH LOCKED"
            : _state.Chill <= 1f ? "SOBER" : _state.Chill < 10f ? "CHILL"
            : _state.Chill < 30f ? "HIGH" : _state.Chill < 50f ? "BAKED"
            : _state.Chill < 70f ? "BLAZED" : _state.Chill < 85f ? "STONED" : "ZONKED";
        _chillWord.Text = state;
        _chillWord.AddThemeColorOverride("font_color",
            _state.Couch > 0f ? new Color(0.90f, 0.55f, 0.47f) : new Color(0.59f, 0.75f, 0.63f));
        _strainName.Text = _state.WeedCount() > 0 ? _state.StrainData().Name.ToUpper() : "DRY";
        _chillFill.Size = new Vector2(260f * Mathf.Clamp(_state.Chill / 100f, 0f, 1f), 10);
        bool hungry = _state.Munchie >= 80f;
        _munchWord.Text = hungry ? "MUNCHIES!" : "MUNCH";
        var munCol = hungry
            ? new Color(1f, 0.63f + 0.3f * Mathf.Sin(time * 8f), 0.35f + 0.3f * Mathf.Cos(time * 8f))
            : new Color(0.94f, 0.59f, 0.31f);
        _munchWord.AddThemeColorOverride("font_color", munCol);
        _munchieFill.Size = new Vector2(140f * Mathf.Clamp(_state.Munchie / 100f, 0f, 1f), 9);
        _munchieFill.Color = munCol;

        // message
        _msgT -= dt;
        _msg.Visible = _msgT > 0f;
        if (_msgT > 0f)
            _msg.AddThemeColorOverride("font_color",
                new Color(1f, 0.82f, 0.35f, Mathf.Min(1f, _msgT)));

        // ---- reel bar
        bool reeling = _fishing.Reeling;
        _reel.Visible = reeling;
        if (reeling)
        {
            float barW = 340f;
            float zoneC = Mathf.Clamp(_fishing.Zone, 0f, 1f) * barW;
            float zoneH = Mathf.Clamp(_fishing.ZoneW, 0f, 1f) * barW;
            float zx = Mathf.Clamp(zoneC - zoneH * 0.5f, 0f, barW);
            float zw = Mathf.Min(zoneH, barW - zx);
            _zone.Position = new Vector2(zx, 27);
            _zone.Size = new Vector2(zw, 22);
            _core.Position = new Vector2(Mathf.Clamp(zoneC - zoneH * 0.045f, 0f, barW), 31);
            _core.Size = new Vector2(Mathf.Min(zoneH * 0.09f, barW), 14);
            _missL.Size = new Vector2(Mathf.Max(0f, zx - 3f), 20);
            _missR.Position = new Vector2(zx + zw, 28);
            _missR.Size = new Vector2(Mathf.Max(0f, barW - zx - zw - 3f), 20);
            _sweep.Position = new Vector2(_fishing.Sweep * barW - 2f, 22);
            _sweep.Size = new Vector2(4f + 2f * _fishing.Progress, 32);
            float d = Mathf.Abs(_fishing.Sweep - _fishing.Zone);
            float half = _fishing.ZoneW * 0.5f;
            float core = _fishing.ZoneW * 0.09f;
            var mc = d <= core ? new Color(0.59f, 1f, 0.59f)
                   : d <= half ? new Color(0.8f, 1f, 0.67f)
                   : d <= half + 0.12f ? new Color(1f, 0.92f, 0.47f)
                   : new Color(1f, 0.94f, 0.55f);
            _sweep.Color = mc;
            _now.Visible = d <= core;
            _spaceHint.Visible = d <= half;
            _nowPulse += dt;
            _now.AddThemeColorOverride("font_color",
                new Color(0.59f, 1f, 0.59f, 0.7f + 0.3f * Mathf.Sin(_nowPulse * 12f)));
            _rope.Color = new Color(0.69f, 0.58f, 0.38f).Lerp(
                new Color(0.81f, 0.33f, 0.20f), _fishing.Progress);
            _prog.Size = new Vector2(336f * _fishing.Progress, 3);
            _mood.Text = _fishing.Progress > 0.75f ? "ALMOST THERE!"
                       : _fishing.Progress > 0.34f ? "KEEP GOING!" : "PULL ON THE BLUE";
        }

        // ---- FISH ON
        bool bite = _fishing.Current == FishingSim.Phase.Bite;
        _fishOn.Visible = bite;
        if (bite)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 12f);
            _fishOnText.AddThemeColorOverride("font_color",
                new Color(1f, 0.82f, 0.35f) * (0.75f + 0.25f * pulse));
            float remain = Mathf.Clamp(1f - _fishing.BiteT / FishingSim.BiteWindow, 0f, 1f);
            _fishOnFill.Size = new Vector2(198f * remain, 6);
        }

        // ---- charge meter
        bool charging = _fishing.Current == FishingSim.Phase.Charging;
        _chargeBar.Visible = charging;
        if (charging)
            _chargeFill.Size = new Vector2(196f * _fishing.Power, 10);
    }

    private void BuildIcons()
    {
        _catcher = new Control();
        _catcher.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _catcher.MouseFilter = Control.MouseFilterEnum.Pass;
        _catcher.GuiInput += OnIconInput;
        AddChild(_catcher);
        for (int i = 0; i < 7; i++)
        {
            var r = new ColorRect
            {
                Position = new Vector2(12 + i * 44, 676),
                Size = new Vector2(40, 36),
                Color = new Color(0.07f, 0.09f, 0.14f, 0.95f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            AddChild(r);
            _icons[i] = r;
            int cx = 20 + i * 44, cy = 683;
            void Box(float x, float y, float w, float h, Color col)
            {
                var b = new ColorRect
                {
                    Position = new Vector2(x, y),
                    Size = new Vector2(w, h),
                    Color = col,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                AddChild(b);
            }
            switch (i)
            {
                case 0: // phone -> menu
                    Box(cx + 4, cy + 1, 16, 22, new Color(0.11f, 0.13f, 0.16f));
                    Box(cx + 7, cy + 5, 10, 14, new Color(0.35f, 0.91f, 0.80f));
                    break;
                case 1: // store -> price tag
                    Box(cx + 1, cy + 2, 20, 20, new Color(0.89f, 0.70f, 0.26f));
                    Box(cx + 3, cy + 4, 4, 4, new Color(0.11f, 0.09f, 0.07f));
                    break;
                case 2: // Sal -> cooler
                    Box(cx + 2, cy + 4, 20, 18, new Color(0.78f, 0.24f, 0.19f));
                    Box(cx + 1, cy + 2, 22, 3, new Color(0.91f, 0.93f, 0.94f));
                    break;
                case 3: // map
                    Box(cx + 2, cy + 2, 20, 20, new Color(0.77f, 0.66f, 0.46f));
                    Box(cx + 6, cy + 16, 3, 3, new Color(0.33f, 0.55f, 0.33f));
                    Box(cx + 12, cy + 12, 3, 3, new Color(0.33f, 0.55f, 0.33f));
                    Box(cx + 16, cy + 5, 3, 3, new Color(0.47f, 0.75f, 0.51f));
                    break;
                case 4: // journal
                    Box(cx + 3, cy + 1, 18, 21, new Color(0.93f, 0.91f, 0.83f));
                    Box(cx + 12, cy + 1, 2, 21, new Color(0.63f, 0.59f, 0.47f));
                    break;
                case 5: // stoke lanes
                    Box(cx + 3, cy + 6, 5, 12, new Color(0.47f, 0.75f, 0.51f));
                    Box(cx + 10, cy + 3, 5, 15, new Color(0.59f, 0.82f, 0.93f));
                    Box(cx + 17, cy + 8, 5, 10, new Color(0.84f, 0.59f, 0.93f));
                    break;
                case 6: // smoke pouch
                    Box(cx + 3, cy + 5, 18, 16, new Color(0.69f, 0.58f, 0.38f));
                    Box(cx + 10, cy + 3, 4, 4, new Color(0.47f, 0.37f, 0.22f));
                    Box(cx + 6, cy + 9, 3, 3, new Color(0.55f, 0.78f, 0.52f));
                    break;
            }
        }
    }

    /// <summary>GUI events arrive in window pixels; the HUD lives in the
    /// 1280x720 design space, so scale the position before hit-testing.</summary>
    private Vector2 ToViewportSpace(Vector2 p)
    {
        var vp = GetViewport();
        return p * vp.GetWindow().Size / vp.GetVisibleRect().Size;
    }

    private void OnIconInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed
            && mb.ButtonIndex == MouseButton.Left)
        {
            var pos = ToViewportSpace(mb.Position);
            for (int i = 0; i < 7; i++)
            {
                var r = new Rect2(_icons[i].Position, _icons[i].Size);
                if (r.HasPoint(pos))
                {
                    IconClicked?.Invoke(i);
                    return;
                }
            }
        }
    }

    private void IconHover()
    {
        var mouse = GetViewport().GetMousePosition();
        for (int i = 0; i < 7; i++)
        {
            var r = new Rect2(_icons[i].Position, _icons[i].Size);
            _icons[i].Color = r.HasPoint(mouse)
                ? new Color(0.15f, 0.18f, 0.27f, 0.97f)
                : new Color(0.07f, 0.09f, 0.14f, 0.95f);
        }
    }

    private static Label Label(string text, int size, Color col)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", col);
        return l;
    }

    private static ColorRect Rect(Vector2 pos, Vector2 size, Color col)
    {
        var r = new ColorRect { Position = pos, Size = size, Color = col };
        return r;
    }
}
