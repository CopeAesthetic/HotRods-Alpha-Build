using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using HotRodsSharp;

/// <summary>
/// All HotRods screens (menu, store, Sal, jars, journal, stoke, map,
/// pouch, help) as one rebuilt-per-frame Control layer, clickable with
/// the mouse and driven by the keyboard as fallback - a faithful port
/// of the MonoGame screens.
/// </summary>
public partial class Screens : CanvasLayer
{
    private GameState _state = null!;
    private FishingSim _fishing = null!;
    private SmokingSim _smoking = null!;
    public Action<string> Sfx = _ => { };
    public Action<string, float> Flash = (_, _) => { };
    public Action? Traveled;

    private Control _root = null!;
    private ColorRect _dim = null!;
    private Label _title = null!;
    private readonly List<(Rect2 rect, Action act)> _hits = new();
    private readonly List<Control> _built = new();
    private readonly List<Control> _pool = new();

    public string Open_ = "";
    public bool AnyOpen => Open_.Length > 0;

    // selections
    public int MenuSel, ShopTab, ShopSel, DealerSel, DealerArea, ZaaSel,
               JournalSel, StokeSel, MapSel, PouchSel, DealerFocus;
    private float _msgT;
    private string _msg = "";
    private bool _upHeld, _downHeld, _leftHeld, _rightHeld, _enterHeld;

    private static readonly Vector2[] MapNodes =
    {
        new(640, 130), new(240, 420), new(900, 460), new(620, 620),
    };

    public override void _Ready()
    {
        _root = new Control { Visible = false };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.GuiInput += OnGuiInput;
        AddChild(_root);
        // one-time: dim + title stay under the rebuilt content
        _dim = R(0, 0, 1280, 720, new Color(0.04f, 0.05f, 0.10f, 0.92f));
        _root.AddChild(_dim);
        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", 30);
        _title.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.8f));
        _root.AddChild(_title);
    }

    public void Bind(GameState state, FishingSim fishing, SmokingSim smoking)
    {
        _state = state;
        _fishing = fishing;
        _smoking = smoking;
    }

    public void Open(string which)
    {
        Open_ = which;
        _msgT = 0f;
        if (which == "menu") MenuSel = 0;
        if (which == "shop") { ShopSel = 0; }
        if (which == "sal") { DealerSel = 0; DealerFocus = 0; DealerArea = _state.Spot; }
        if (which == "zaa") ZaaSel = 0;
        if (which == "journal") JournalSel = 0;
        if (which == "stoke") StokeSel = 0;
        if (which == "map") MapSel = _state.Spot;
        if (which == "pouch") PouchSel = 0;
    }

    private void OnGuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed
            && mb.ButtonIndex == MouseButton.Left)
        {
            foreach (var (r, act) in _hits)
            {
                if (r.HasPoint(mb.Position))
                {
                    act();
                    return;
                }
            }
        }
    }

    // ------------------------------------------------------------ tick --
    public void Tick(float dt, float time)
    {
        _hits.Clear();
        foreach (var c in _built)
        {
            c.Visible = false;
            _root.RemoveChild(c);
            _pool.Add(c);
        }
        _built.Clear();
        _root.Visible = AnyOpen;
        if (!AnyOpen)
            return;

        _dim.Visible = true;
        _title.Visible = false;

        _msgT -= dt;
        switch (Open_)
        {
            case "menu": DrawMenu(); break;
            case "shop": DrawShop(); break;
            case "sal": DrawSal(); break;
            case "zaa": DrawZaa(); break;
            case "journal": DrawJournal(); break;
            case "stoke": DrawStoke(); break;
            case "map": DrawMap(); break;
            case "pouch": DrawPouch(); break;
            case "help": DrawHelp(); break;
        }
        HandleKeys();
    }

    // ------------------------------------------------------------- menu --
    private static readonly string[] MenuItems =
    {
        "STORE", "SALS SHACK", "JARS", "JOURNAL", "STOKE",
        "MAP AND TRAVEL", "HOW TO PLAY", "SAVE", "SAVE AND TITLE",
    };

    private void DrawMenu()
    {
        Title("MENU");
        Text("LV " + _state.Level + "   XP " + _state.Xp + "   $" + _state.Cash
             + "   " + GameData.SpotNames[_state.Spot], 40, 78, 20,
             new Color(0.8f, 0.75f, 0.95f));
        int y = 128;
        var hover = _root.GetGlobalMousePosition();
        foreach (var item in MenuItems)
        {
            var col = hover.X > 40 && hover.X < 430 && hover.Y > y - 4 && hover.Y < y + 26
                ? new Color(1f, 1f, 0.78f) : new Color(0.67f, 0.71f, 0.82f);
            Text((MenuItems[MenuSel] == item ? "> " : "  ") + item, 40, y, 22, col);
            int i = y;
            var hit = new Rect2(40, y - 4, 390, 28);
            _hits.Add((hit, () => ActMenu(Array.IndexOf(MenuItems, item))));
            y += 34;
        }
    }

    private void ActMenu(int i)
    {
        Sfx("click");
        switch (MenuItems[i])
        {
            case "STORE": Open("shop"); break;
            case "SALS SHACK": Open("sal"); break;
            case "JARS": Open("zaa"); break;
            case "JOURNAL": Open("journal"); break;
            case "STOKE": Open("stoke"); break;
            case "MAP AND TRAVEL": Open("map"); break;
            case "HOW TO PLAY": Open("help"); break;
            case "SAVE": _state.Save(); Flash("saved", 2f); Open_ = ""; break;
            case "SAVE AND TITLE": _state.Save(); Flash("saved - title coming soon", 2f); Open_ = ""; break;
        }
    }

    // ------------------------------------------------------------- shop --
    private static readonly string[] Tabs =
        { "RODS", "LINES", "LURES", "COOLERS", "SMOKERS", "WATERS", "MUNCHIES" };

    private void DrawShop()
    {
        Title("STORE  -  $" + _state.Cash);
        for (int i = 0; i < 7; i++)
        {
            var col = i == ShopTab ? new Color(1f, 1f, 1f) : new Color(0.43f, 0.47f, 0.59f);
            Text(Tabs[i], 40 + i * 108, 62, 18, col);
            _hits.Add((new Rect2(40 + i * 108, 60, 88, 20), () => { ShopTab = i; ShopSel = 0; }));
        }
        int y = 100;
        if (ShopTab < 4)
        {
            string kind = new[] { "rod", "line", "lure", "cooler" }[ShopTab];
            var gear = _state.GearTable(kind);
            for (int i = 0; i < gear.Length; i++)
            {
                var g = gear[i];
                bool owned = _state.Owns(kind, g.Id);
                bool eq = _state.EquippedId(kind) == g.Id;
                var col = i == ShopSel ? new Color(1f, 1f, 0.78f)
                          : owned ? new Color(0.59f, 0.85f, 0.63f)
                          : new Color(0.67f, 0.71f, 0.82f);
                Text((i == ShopSel ? "> " : "  ") + g.Name.ToUpper(), 60, y, 18, col);
                Text(eq ? "EQUIPPED" : owned ? "OWNED" : "$" + g.Cost, 900, y, 18, col);
                Text(g.Desc, 80, y + 22, 16, new Color(0.47f, 0.51f, 0.63f));
                int yy = y;
                _hits.Add((new Rect2(60, yy - 4, 1000, 40), () => { ShopSel = i; BuyShopItem(); }));
                y += 46;
            }
        }
        else if (ShopTab == 4)
        {
            var gear = _state.GearTable("smoker");
            for (int i = 0; i < gear.Length; i++)
            {
                var g = gear[i];
                bool owned = _state.Owns("smoker", g.Id);
                bool eq = _state.Smoker == g.Id;
                var col = i == ShopSel ? new Color(1f, 1f, 0.78f)
                          : owned ? new Color(0.59f, 0.85f, 0.63f)
                          : new Color(0.67f, 0.71f, 0.82f);
                Text((i == ShopSel ? "> " : "  ") + g.Name.ToUpper(), 60, y, 18, col);
                Text(eq ? "EQUIPPED" : owned ? "OWNED" : "$" + g.Cost, 900, y, 18, col);
                Text(g.Desc, 80, y + 22, 16, new Color(0.47f, 0.51f, 0.63f));
                int yy = y;
                _hits.Add((new Rect2(60, yy - 4, 1000, 40), () => { ShopSel = i; BuyShopItem(); }));
                y += 46;
            }
        }
        else if (ShopTab == 5)
        {
            for (int i = 0; i < 4; i++)
            {
                bool unlocked = _state.SpotUnlocked(i);
                bool here = i == _state.Spot;
                var sp = GameData.Spots[i];
                var col = i == ShopSel ? new Color(1f, 1f, 0.78f)
                          : unlocked ? new Color(0.59f, 0.85f, 0.63f)
                          : new Color(0.43f, 0.45f, 0.55f);
                Text((i == ShopSel ? "> " : "  ") + (unlocked || here ? sp.Name.ToUpper() : "???"),
                     60, y, 18, col);
                Text(here ? "HERE" : unlocked ? "UNLOCKED" : "$" + sp.NeedCash, 900, y, 18, col);
                string sub = unlocked || here ? sp.Blurb
                    : "Catch all " + GameData.SpeciesList.Count(f => f.Spot == Math.Max(0, i - 1))
                      + " first";
                Text(sub, 80, y + 22, 16, new Color(0.47f, 0.51f, 0.63f));
                int yy = y;
                _hits.Add((new Rect2(60, yy - 4, 1000, 40), () => { ShopSel = i; BuyShopItem(); }));
                y += 46;
            }
        }
        else
        {
            for (int i = 0; i < GameData.Munchies.Length; i++)
            {
                var m = GameData.Munchies[i];
                var col = i == ShopSel ? new Color(1f, 1f, 0.78f) : new Color(1f, 0.75f, 0.47f);
                Text((i == ShopSel ? "> " : "  ") + m.Name, 60, y, 18, col);
                Text("$" + m.Price, 800, y, 18, col);
                Text("-" + m.Relief + " MUNCH", 950, y, 18, new Color(1f, 0.67f, 0.35f));
                int yy = y;
                _hits.Add((new Rect2(60, yy - 4, 1000, 24), () => { ShopSel = i; BuyShopItem(); }));
                y += 28;
            }
        }
        Footer("LEFT/RIGHT TABS  ENTER BUY  ESC BACK");
        Msg();
    }

    private void BuyShopItem()
    {
        if (ShopTab < 4)
        {
            string kind = new[] { "rod", "line", "lure", "cooler" }[ShopTab];
            var gear = _state.GearTable(kind);
            if (ShopSel >= gear.Length) return;
            var (ok, msg) = _state.BuyGear(kind, gear[ShopSel]);
            ShopMsg(msg, 2.2f);
        }
        else if (ShopTab == 4)
        {
            var gear = _state.GearTable("smoker");
            if (ShopSel >= gear.Length) return;
            var (ok, msg) = _state.BuyGear("smoker", gear[ShopSel]);
            if (ok) _smoking.SetItem(_state.Smoker);
            ShopMsg(msg, 2.2f);
        }
        else if (ShopTab == 5)
        {
            if (ShopSel >= 4) return;
            var (ok, msg) = _state.TryTravel(ShopSel);
            ShopMsg(ok ? "water unlocked - open the map" : msg, 2.6f);
        }
        else
        {
            if (ShopSel >= GameData.Munchies.Length) return;
            var (ok, msg) = _state.BuySnack(GameData.Munchies[ShopSel]);
            ShopMsg(msg, 2.2f);
        }
    }

    private void ShopMsg(string msg, float t) { _msg = msg; _msgT = t; }

    // -------------------------------------------------------------- sal --
    private void DrawSal()
    {
        Title("SALS SHACK - FISH IN, SMOKE OUT");
        Text("$" + _state.Cash, 1120, 26, 20, new Color(0.94f, 0.84f, 0.47f));
        for (int i = 0; i < 4; i++)
        {
            var col = i == DealerArea ? new Color(1f, 1f, 1f) : new Color(0.43f, 0.47f, 0.59f);
            Text(GameData.SpotNames[i].ToUpper(), 440 + i * 150, 66, 16, col);
            _hits.Add((new Rect2(440 + i * 150, 64, 130, 20), () => { DealerArea = i; DealerSel = 0; }));
        }
        Text("YOUR COOLER  " + _state.Bag.Count + "/" + _state.BagSize(), 30, 64, 18,
             DealerFocus == 0 ? new Color(1f, 1f, 0.78f) : new Color(0.67f, 0.71f, 0.82f));
        int y = 92;
        int shown = 0;
        for (int i = 0; i < _state.Bag.Count && shown < 11; i++, shown++)
        {
            var sp = GameData.Species.FirstOrDefault(f => f.Id == _state.Bag[i]);
            if (sp == null) continue;
            var col = i == DealerSel && DealerFocus == 0
                ? new Color(1f, 1f, 0.78f) : new Color(0.67f, 0.71f, 0.82f);
            Text((i == DealerSel && DealerFocus == 0 ? "> " : "  ") + sp.Name.ToUpper(), 60, y, 16, col);
            Text("$" + (int)(sp.KgMin * sp.UsdKg), 400, y, 16, col);
            int yy = y;
            _hits.Add((new Rect2(60, yy - 4, 350, 22), () => { DealerFocus = 0; DealerSel = i; ActDealer(); }));
            y += 26;
        }
        Text("JARS", 440, 92, 18, DealerFocus == 1
             ? new Color(1f, 1f, 0.78f) : new Color(0.67f, 0.71f, 0.82f));
        int y2 = 118;
        var rows = JarRows();
        for (int i = 0; i < rows.Length; i++)
        {
            var st = rows[i];
            bool open = _state.JarAvailable(st);
            bool current = _state.Strain == st.Id;
            var col = i == DealerSel && DealerFocus == 1 ? new Color(1f, 1f, 0.78f)
                      : open ? new Color(0.59f, 0.85f, 0.63f)
                      : new Color(0.43f, 0.45f, 0.55f);
            Text((i == DealerSel && DealerFocus == 1 ? "> " : "  ")
                 + (open || current ? st.Name.ToUpper() : "???  (TIER " + st.Tier + " FISH)"),
                 440, y2, 16, col);
            Text(open ? "$" + st.Price + "  X" + _state.WeedCount(st.Id) : "LOCKED", 1100, y2, 16, col);
            int yy = y2;
            _hits.Add((new Rect2(440, yy - 4, 700, 26), () => { DealerFocus = 1; DealerSel = i; ActDealer(); }));
            y2 += 42;
        }
        Text("SAL - " + SalLine(), 30, 640, 18, new Color(0.93f, 0.91f, 0.83f));
        Footer("CLICK FISH TO SELL  CLICK JAR TO BUY  ESC BACK");
        Msg();
    }

    private string SalLine() =>
        _state.High > 0.1f ? "whoa, look at you. keep 'em coming." : "fresh fish, fresh smoke.";

    private void ActDealer()
    {
        if (DealerFocus == 0)
        {
            if (DealerSel >= _state.Bag.Count) { ShopMsg("cooler is empty", 2f); return; }
            int v = _state.SellFish(DealerSel);
            ShopMsg("$" + v + ". Don't spend it all on lures.", 3f);
            DealerSel = Math.Min(DealerSel, Math.Max(0, _state.Bag.Count - 1));
            return;
        }
        var rows = JarRows();
        if (DealerSel >= rows.Length) return;
        var (ok, msg) = _state.BuyWeed(rows[DealerSel]);
        ShopMsg(msg, 3f);
    }

    private Strain[] JarRows() => GameData.Strains
        .Where(s => _state.JarAvailable(s) || _state.Strain == s.Id
                    || _state.WeedCount(s.Id) > 0).ToArray();

    // -------------------------------------------------------------- zaa --
    private void DrawZaa()
    {
        Title("YOUR JARS");
        int y = 100;
        for (int i = 0; i < GameData.Strains.Length; i++)
        {
            var st = GameData.Strains[i];
            bool open = _state.JarAvailable(st);
            bool current = _state.Strain == st.Id;
            var col = i == ZaaSel ? new Color(1f, 1f, 0.78f)
                      : open ? new Color(0.59f, 0.85f, 0.63f)
                      : new Color(0.43f, 0.45f, 0.55f);
            Text((i == ZaaSel ? "> " : "  ") + (open || current ? st.Name.ToUpper() : "???"),
                 60, y, 18, col);
            Text(open ? "X" + _state.WeedCount(st.Id) + (current ? "  IN USE" : "")
                 : "LOCKED", 800, y, 18, col);
            if (open)
                Text("P" + st.Potency + "  L+" + (int)(st.Luck * 100) + "  "
                     + (int)(st.Duration * 10) + "D  W" + (int)(st.Wobble * 100),
                     80, y + 22, 16, new Color(0.47f, 0.51f, 0.63f));
            int yy = y;
            _hits.Add((new Rect2(60, yy - 4, 900, 40), () =>
            {
                if (!open) return;
                ZaaSel = i;
                var (ok, msg) = _state.UseWeed(st);
                ShopMsg(msg, 2.5f);
            }));
            y += 44;
        }
        Footer("CLICK A JAR TO LOAD IT  ESC BACK");
        Msg();
    }

    // ---------------------------------------------------------- journal --
    private void DrawJournal()
    {
        Title("JOURNAL  -  " + _state.Species.Count + "/" + GameData.Species.Length + " species");
        int y = 80;
        for (int i = 0; i < GameData.Species.Length; i++)
        {
            var sp = GameData.Species[i];
            bool have = _state.Species.ContainsKey(sp.Id);
            var col = have ? RarityCol(sp.Rarity) : new Color(0.38f, 0.40f, 0.49f);
            Text((i == JournalSel ? "> " : "  ") + (have ? sp.Name.ToUpper() : "???")
                 + "  (" + sp.Rarity.ToUpper() + ")", 60, y, 16, col);
            if (have)
                Text("x" + _state.Species[sp.Id], 700, y, 16, col);
            y += 24;
            if (y > 640) break;
        }
        int wx = 950;
        Text("WEED STASH  " + _state.WeedTotal() + " GRAMS", wx, 80, 18, new Color(0.59f, 0.85f, 0.63f));
        int wy = 110;
        foreach (var kv in _state.WeedCounts.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value))
        {
            var st = GameData.Strains.FirstOrDefault(x => x.Id == kv.Key);
            if (st == null) continue;
            bool inUse = _state.Strain == st.Id;
            var col = inUse ? new Color(1f, 0.94f, 0.55f) : new Color(0.59f, 0.85f, 0.63f);
            Text(st.Name.ToUpper(), wx + 8, wy, 16, col);
            Text("X" + kv.Value + (inUse ? "  IN USE" : ""), wx + 220, wy, 16, col);
            wy += 26;
        }
        if (_state.WeedTotal() == 0)
            Text("DRY - SEE SAL", wx + 8, wy, 16, new Color(0.47f, 0.49f, 0.58f));
        Footer("ESC BACK");
    }

    private Color RarityCol(string r) => r switch
    {
        "common" => new Color(0.72f, 0.75f, 0.78f),
        "uncommon" => new Color(0.42f, 0.78f, 0.42f),
        "rare" => new Color(0.36f, 0.62f, 0.94f),
        "epic" => new Color(0.72f, 0.45f, 0.94f),
        _ => new Color(1f, 0.62f, 0.20f),
    };

    // ------------------------------------------------------------ stoke --
    private static readonly string[] Lanes = { "fish", "smoke", "vibes" };

    private void DrawStoke()
    {
        Title("STOKE - LEVEL " + _state.Level + "  XP " + _state.Xp
              + "   POINTS " + _state.StokePts);
        int y = 100;
        for (int i = 0; i < 3; i++)
        {
            var col = i == StokeSel ? new Color(1f, 1f, 0.78f)
                      : i == 0 ? new Color(0.47f, 0.75f, 0.51f)
                      : i == 1 ? new Color(0.59f, 0.82f, 0.93f)
                      : new Color(0.84f, 0.59f, 0.93f);
            Text((i == StokeSel ? "> " : "  ") + Lanes[i].ToUpper() + "  "
                 + new string('|', _state.StokeRank(Lanes[i])), 60, y, 20, col);
            int yy = y;
            _hits.Add((new Rect2(60, yy - 4, 500, 30), () =>
            {
                StokeSel = i;
                var (ok, msg) = _state.SpendStoke(Lanes[i]);
                ShopMsg(msg, 2f);
            }));
            y += 40;
        }
        Text("ONE STOKE POINT PER LEVEL", 60, 240, 16, new Color(0.47f, 0.51f, 0.63f));
        Footer("CLICK A LANE TO SPEND A POINT  ESC BACK");
        Msg();
    }

    // -------------------------------------------------------------- map --
    private void DrawMap()
    {
        Title("MAP AND TRAVEL");
        for (int i = 0; i < 4; i++)
        {
            var p = MapNodes[i];
            bool unlocked = _state.SpotUnlocked(i);
            bool here = i == _spot();
            var col = i == MapSel ? new Color(1f, 1f, 0.78f)
                      : unlocked ? new Color(0.59f, 0.85f, 0.63f)
                      : new Color(0.43f, 0.45f, 0.55f);
            Text(unlocked || here ? GameData.SpotNames[i].ToUpper() : "???",
                 p.X - 40, p.Y, 18, col);
            _hits.Add((new Rect2(p.X - 50, p.Y - 10, 140, 30), () => { MapSel = i; GoMap(); }));
        }
        Footer("CLICK A SPOT TO TRAVEL  ESC BACK");
        Msg();
    }

    private int _spot() => _state.Spot;

    private void GoMap()
    {
        if (_state.SpotUnlocked(MapSel))
        {
            if (MapSel != _state.Spot)
            {
                _state.Spot = MapSel;
                _state.Save();
                Flash("travelled to " + GameData.SpotNames[MapSel], 2f);
                Open_ = "";
                Traveled?.Invoke();
            }
            else
            {
                Open_ = "";
            }
            return;
        }
        if (_state.TryTravel(MapSel) is var (ok, msg) && ok)
            GoMap();
        else
            ShopMsg(msg, 3f);
    }

    // ------------------------------------------------------------ pouch --
    private void DrawPouch()
    {
        Title("SMOKE POUCH");
        Text("WEED  " + _state.WeedTotal() + " GRAMS", 60, 80, 18, new Color(0.59f, 0.85f, 0.63f));
        int y = 108;
        foreach (var kv in _state.WeedCounts.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value))
        {
            var st = GameData.Strains.FirstOrDefault(x => x.Id == kv.Key);
            if (st == null) continue;
            bool inUse = _state.Strain == st.Id;
            var col = inUse ? new Color(1f, 0.94f, 0.55f) : new Color(0.59f, 0.85f, 0.63f);
            Text((inUse ? "> " : "  ") + st.Name.ToUpper(), 60, y, 16, col);
            Text("X" + kv.Value, 300, y, 16, col);
            int yy = y;
            var sid = kv.Key;
            _hits.Add((new Rect2(60, yy - 4, 280, 24), () =>
            {
                _state.Strain = sid;
                _state.StrainIndex = Array.IndexOf(GameData.Strains,
                    GameData.Strains.First(x => x.Id == sid));
                ShopMsg("jar loaded - " + _state.StrainData().Name, 2.5f);
            }));
            y += 26;
        }
        if (_state.WeedTotal() == 0)
            Text("DRY - SEE SAL", 60, y, 16, new Color(0.47f, 0.49f, 0.58f));

        Text("PIECES", 600, 80, 18, new Color(0.59f, 0.82f, 0.93f));
        int py = 108;
        var pieces = GameData.Smokers.Where(g => _state.Owns("smoker", g.Id)).ToArray();
        for (int i = 0; i < pieces.Length; i++)
        {
            bool eq = _state.Smoker == pieces[i].Id;
            var col = eq ? new Color(1f, 1f, 0.78f) : new Color(0.67f, 0.71f, 0.82f);
            Text((eq ? "> " : "  ") + pieces[i].Name.ToUpper(), 600, py, 16, col);
            Text(eq ? "LOADED" : "", 900, py, 16, col);
            int yy = py;
            var pid = pieces[i].Id;
            _hits.Add((new Rect2(600, yy - 4, 350, 24), () =>
            {
                _state.Smoker = pid;
                _smoking.SetItem(pid);
                ShopMsg("loaded the " + GameData.Smokers.First(x => x.Id == pid).Name, 2.5f);
            }));
            py += 26;
        }
        if (pieces.Length == 0)
            Text("ONE-HITTER ONLY - BUY MORE AT THE STORE", 600, py, 16, new Color(0.47f, 0.49f, 0.58f));
        Footer("CLICK STRAIN TO LOAD  CLICK PIECE TO EQUIP  ESC BACK");
        Msg();
    }

    // ------------------------------------------------------------- help --
    private static readonly string[] HelpLines =
    {
        "SPACE  CAST - HOLD TO CHARGE, TAP AT THE BITE",
        "REEL  - TIME THE PULL WHEN THE MARKER HITS THE BLUE",
        "E  SMOKE    Q  SWITCH PIECE    ESC  MENU",
        "SMOKE TO UNLOCK RARER FISH - STRAINS GATE THE BANDS",
        "SELL FISH AT SALS, BUY GEAR AND MUNCHIES AT THE STORE",
        "FULL MUNCHIE BAR STOPS YOU FISHING - GO EAT",
    };

    private void DrawHelp()
    {
        Title("HOW TO PLAY");
        int y = 110;
        foreach (var line in HelpLines)
        {
            Text(line, 60, y, 20, new Color(0.78f, 0.82f, 0.90f));
            y += 34;
        }
        Footer("ESC BACK");
    }

    // ----------------------------------------------------------- helpers --
    private void Title(string text)
    {
        _title.Visible = true;
        _title.Text = text;
        _title.Position = new Vector2(40, 30);
        _title.Size = new Vector2(1200, 40);
        _title.AddThemeColorOverride("font_color", new Color(0.94f, 0.91f, 0.83f));
    }

    private void Text(string text, float x, float y, int size, Color col)
    {
        var l = TakePool() as Label ?? new Label();
        l.Visible = true;
        l.Text = text;
        l.Position = new Vector2(x, y);
        l.Size = new Vector2(1100, size + 8);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", col);
        _root.AddChild(l);
        _built.Add(l);
    }

    private void Footer(string text) =>
        Text(text, 40, 686, 16, new Color(0.51f, 0.55f, 0.67f));

    private void Msg()
    {
        if (_msgT > 0f)
            Text(_msg, 40, 640, 20, new Color(0.94f, 0.84f, 0.47f));
    }

    private void HandleKeys()
    {
        void Edge(Key k, ref bool held, Action a)
        {
            if (Input.IsKeyPressed(k) && !held) { a(); held = true; }
            if (!Input.IsKeyPressed(k)) held = false;
        }
        if (Open_ == "menu")
        {
            Edge(Key.Up, ref _upHeld, () => MenuSel = Math.Max(0, MenuSel - 1));
            Edge(Key.Down, ref _downHeld, () => MenuSel = Math.Min(MenuItems.Length - 1, MenuSel + 1));
            Edge(Key.Enter, ref _enterHeld, () => ActMenu(MenuSel));
            Edge(Key.Escape, ref _leftHeld, () => Open_ = "");
        }
        else
        {
            Edge(Key.Escape, ref _leftHeld, () => Open("menu"));
        }
    }

    private Control TakePool()
    {
        if (_pool.Count > 0)
        {
            var c = _pool[^1];
            _pool.RemoveAt(_pool.Count - 1);
            return c;
        }
        return new Control();
    }

    private static ColorRect R(float x, float y, float w, float h, Color col)
    {
        var r = new ColorRect { Position = new Vector2(x, y), Size = new Vector2(w, h), Color = col };
        return r;
    }
}
