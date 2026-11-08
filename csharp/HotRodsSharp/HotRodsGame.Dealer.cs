namespace HotRodsSharp;

/// <summary>
/// Sal's shack: sell fish one by one or in bulk, buy grams from the jars
/// (hidden jars unlock when their fish is caught), load a strain.
/// Ported from the Python dealer.py screen. Keyboard driven.
/// </summary>
public partial class HotRodsGame
{
    private static readonly string[] AreaTabs = { "POND", "LAKE", "MARSH", "TRENCH" };

    private string SalLine() => _state.Bag.Count == 0
        ? "Coolers empty, kid. Go fill it."
        : _state.BagBestTier() switch
        {
            >= 4 => "Is that... no. Don't tell me. Take whatever you want.",
            >= 3 => "Now THAT'S a fish. I'll open the good jar for that.",
            >= 2 => "A rare one! You're learnin'.",
            >= 1 => "Decent little fish. I can work with these.",
            _ => "Minnows. Great. I'll take 'em off your hands.",
        };

    private Strain[] JarRows() => GameData.Strains
        .Where(s => s.Area == _dealerArea
                    && (!s.Hidden || _state.HiddenJars.Contains(s.Id)))
        .ToArray();

    private void UpdateDealer(float dt, KeyboardState kb)
    {
        _menuMsgT -= dt;
        void Edge(ref bool held, Keys k, Action a)
        {
            if (kb.IsKeyDown(k) && !held) { a(); held = true; }
            if (kb.IsKeyUp(k)) held = false;
        }

        Edge(ref _tabHeld, Keys.Tab, () => _dealerFocus = 1 - _dealerFocus);
        Edge(ref _leftHeld, Keys.Left, () => { _dealerArea = (_dealerArea + 3) % 4; _dealerSel = 0; });
        Edge(ref _rightHeld, Keys.Right, () => { _dealerArea = (_dealerArea + 1) % 4; _dealerSel = 0; });
        Edge(ref _upHeld, Keys.Up, () => _dealerSel = Math.Max(0, _dealerSel - 1));
        Edge(ref _downHeld, Keys.Down, () => _dealerSel++);
        Edge(ref _enterHeld, Keys.Enter, Act);
        Edge(ref _uHeld, Keys.U, UseJar);
        Edge(ref _fHeld, Keys.F, SellAll);
        if (kb.IsKeyDown(Keys.Escape))
        {
            _dealerOpen = false;
            _menuOpen = true;
        }

        void Act()
        {
            if (_dealerFocus == 0)
            {
                if (_dealerSel >= _state.Bag.Count)
                {
                    _menuMsg = "cooler is empty";
                    _menuMsgT = 2f;
                    return;
                }
                int v = _state.SellFish(_dealerSel);
                _menuMsg = "$" + v + ". Don't spend it all on lures.";
                _menuMsgT = 3f;
                PlaySfx("click");
                _dealerSel = Math.Min(_dealerSel, Math.Max(0, _state.Bag.Count - 1));
                return;
            }
            var rows = JarRows();
            if (_dealerSel >= rows.Length)
                return;
            var (ok, msg) = _state.BuyWeed(rows[_dealerSel]);
            _menuMsg = msg;
            _menuMsgT = 3f;
            PlaySfx("click");
        }

        void UseJar()
        {
            if (_dealerFocus != 1)
                return;
            var rows = JarRows();
            if (_dealerSel >= rows.Length)
                return;
            var (ok, msg) = _state.UseWeed(rows[_dealerSel]);
            _menuMsg = ok ? "Loaded you up with " + rows[_dealerSel].Name + "." : msg;
            _menuMsgT = 3f;
        }

        void SellAll()
        {
            int n = _state.Bag.Count;
            if (n == 0)
            {
                _menuMsg = "cooler is empty";
                _menuMsgT = 2f;
                return;
            }
            int total = (int)MathF.Round(_state.BagValue());
            _state.SellBag();
            _menuMsg = n + " fish, $" + total + ". Pleasure.";
            _menuMsgT = 3.5f;
            PlaySfx("click");
        }
    }

    private void DrawDealer()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        _batch.Begin(samplerState: SamplerState.PointClamp);
        _batch.Draw(_pixel, new Rectangle(0, 0, w, h), new Color(10, 14, 26, 235));
        _font.Draw(_batch, "SALS SHACK - FISH IN, SMOKE OUT", new Vector2(30, 24), 3,
                   new Color(240, 214, 120));
        _font.Draw(_batch, "$" + _state.Cash, new Vector2(w - 120, 24), 3,
                   new Color(240, 214, 120));

        // ---- cooler column
        _font.Draw(_batch, "YOUR COOLER  " + _state.Bag.Count + "/" + _state.BagSize(),
                   new Vector2(30, 64), 2,
                   _dealerFocus == 0 ? new Color(255, 255, 200) : new Color(170, 180, 210));
        int y = 92;
        int shown = 0;
        for (int i = 0; i < _state.Bag.Count && shown < 11; i++, shown++)
        {
            var sp = GameData.Species.FirstOrDefault(f => f.Id == _state.Bag[i]);
            if (sp == null)
                continue;
            var col = i == _dealerSel && _dealerFocus == 0
                ? new Color(255, 255, 200) : RarityCol(sp.Rarity);
            string line = (i == _dealerSel && _dealerFocus == 0 ? "> " : "  ")
                          + sp.Name.ToUpper();
            _font.Draw(_batch, line, new Vector2(30, y), 2, col);
            _font.Draw(_batch, "$" + (int)MathF.Round(_state.BagEntryValue(i)),
                       new Vector2(390, y), 2, col);
            y += 26;
        }
        if (_state.Bag.Count == 0)
            _font.Draw(_batch, "EMPTY - GO FISHING", new Vector2(30, y), 2,
                       new Color(110, 120, 150));

        // ---- jars column
        _font.Draw(_batch, "SALS JARS", new Vector2(440, 64), 2,
                   _dealerFocus == 1 ? new Color(255, 255, 200) : new Color(150, 216, 160));
        for (int i = 0; i < 4; i++)
        {
            bool locked = i > _state.MaxWater;
            var col = i == _dealerArea
                ? new Color(255, 255, 255)
                : locked ? new Color(90, 96, 116) : new Color(110, 120, 150);
            _font.Draw(_batch, locked ? "???" : AreaTabs[i],
                       new Vector2(440 + i * 82, 88), 2, col);
        }
        y = 118;
        var rows = JarRows();
        for (int i = 0; i < rows.Length && i < 7; i++)
        {
            var st = rows[i];
            bool open = _state.JarAvailable(st);
            bool current = _state.Strain == st.Id;
            var col = i == _dealerSel && _dealerFocus == 1
                ? new Color(255, 255, 200)
                : current ? new Color(255, 240, 140)
                : open ? new Color(150, 216, 160) : new Color(96, 102, 124);
            string name = open || current ? st.Name.ToUpper()
                          : "???  (TIER " + st.Tier + " FISH)";
            _font.Draw(_batch, (i == _dealerSel && _dealerFocus == 1 ? "> " : "  ") + name,
                       new Vector2(440, y), 2, col);
            string right = open ? "$" + st.Price + "  X" + _state.WeedCount(st.Id)
                          : "LOCKED";
            _font.Draw(_batch, right, new Vector2(w - 150, y), 2, col);
            y += 30;
            if (open)
            {
                _font.Draw(_batch, "P" + st.Potency + "  L+" + (int)(st.Luck * 100)
                           + "  " + (int)(st.Duration * 10) + "D  W" + (int)(st.Wobble * 100),
                           new Vector2(440, y - 8), 1, new Color(120, 130, 160));
            }
            y += 12;
        }

        // ---- Sal
        _font.Draw(_batch, "SAL - " + SalLine(), new Vector2(30, h - 76), 2,
                   new Color(238, 232, 212));
        if (_menuMsgT > 0f)
            _font.Draw(_batch, _menuMsg, new Vector2(30, h - 52), 2,
                       new Color(240, 214, 120));
        _font.Draw(_batch,
                   "TAB COOLER/JARS  ENTER ACT  U USE  F SELL ALL  LEFT/RIGHT AREA  ESC BACK",
                   new Vector2(30, h - 28), 2, new Color(130, 140, 170));
        _batch.End();
    }
}
