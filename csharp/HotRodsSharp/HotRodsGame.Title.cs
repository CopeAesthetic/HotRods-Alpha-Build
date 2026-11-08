namespace HotRodsSharp;

/// <summary>
/// Title screen, the TRIPS save-slot browser, and the how-to-play overlay.
/// Ported from the Python title.py / trips.py / overlays.py screens.
/// </summary>
public partial class HotRodsGame
{
    // ---------------------------------------------------------------- board --
    private void Board(int slot)
    {
        if (_boundState != null)
            _boundState.LeveledUp -= OnLevelUp;
        _state = GameState.Load(slot);
        _fishing.SetState(_state);
        _spot = Math.Clamp(_state.Spot, 0, 3);
        _state.MaxWater = Math.Max(_state.MaxWater, _spot);
        _atTitle = false;
        _tripsOpen = false;
        _menuOpen = false;
        _helpOpen = false;
        _boundState = _state;
        _state.LeveledUp += OnLevelUp;
        _state.Save();
        BuildWorld();
        SteamMan.SetPresence("fishing at " + GameData.SpotNames[_spot]);
    }

    private void OnLevelUp(int level)
    {
        _uiMsg = "LEVEL " + level + "! +1 STOKE POINT - PRESS K";
        _uiMsgT = 4f;
        PlaySfx("click");
    }

    private void NewGame(int slot)
    {
        _state = GameState.New(slot);
        _state.Save();
        Board(slot);
    }

    // ---------------------------------------------------------------- title --
    private string[] TitleItems()
    {
        var items = new List<string>();
        if (GameState.AnySave())
            items.Add("TRIPS");
        if (GameState.FreeSlot() != 0)
            items.Add("NEW GAME");
        items.Add("HOW TO PLAY");
        items.Add("QUIT");
        return items.ToArray();
    }

    private void UpdateTitle(float dt, KeyboardState kb)
    {
        _menuMsgT -= dt;
        _newConfirm = MathF.Max(0f, _newConfirm - dt);
        _scrapConfirmT = MathF.Max(0f, _scrapConfirmT - dt);
        if (_scrapConfirmT <= 0f)
            _scrapConfirm = 0;
        void Edge(ref bool held, Keys k, Action a)
        {
            if (kb.IsKeyDown(k) && !held) { a(); held = true; }
            if (kb.IsKeyUp(k)) held = false;
        }

        if (_tripsOpen)
        {
            Edge(ref _upHeld, Keys.Up, () => _tripsSel = Math.Max(1, _tripsSel - 1));
            Edge(ref _downHeld, Keys.Down, () => _tripsSel = Math.Min(3, _tripsSel + 1));
            Edge(ref _enterHeld, Keys.Enter, () =>
            {
                if (GameState.SlotExists(_tripsSel))
                    Board(_tripsSel);
                else
                    NewGame(_tripsSel);
            });
            Edge(ref _xHeld, Keys.X, () =>
            {
                if (!GameState.SlotExists(_tripsSel))
                    return;
                if (_scrapConfirm != _tripsSel)
                {
                    _scrapConfirm = _tripsSel;
                    _scrapConfirmT = 3f;
                    return;
                }
                GameState.Scrap(_tripsSel);
                _scrapConfirm = 0;
                _menuMsg = "trip " + _tripsSel + " scrapped";
                _menuMsgT = 2.5f;
            });
            if (kb.IsKeyDown(Keys.Escape))
                _tripsOpen = false;
            return;
        }

        var items = TitleItems();
        _titleSel = Math.Clamp(_titleSel, 0, items.Length - 1);
        Edge(ref _upHeld, Keys.Up, () => _titleSel = Math.Max(0, _titleSel - 1));
        Edge(ref _downHeld, Keys.Down, () => _titleSel = Math.Min(items.Length - 1, _titleSel + 1));
        Edge(ref _enterHeld, Keys.Enter, () => Act(items[_titleSel]));
        Edge(ref _tKeyHeld, Keys.T, () => OpenTrips());
        Edge(ref _hHeld, Keys.H, () => _helpOpen = !_helpOpen);
        Edge(ref _escHeld, Keys.Escape, Exit);
        if (kb.IsKeyDown(Keys.N) && !_enterHeld)
            Edge(ref _enterHeld, Keys.N, () => Act("NEW GAME"));

        void OpenTrips()
        {
            if (!GameState.AnySave())
            {
                _menuMsg = "no trips yet - start a new game";
                _menuMsgT = 2.5f;
                return;
            }
            _tripsOpen = true;
            _tripsSel = Enumerable.Range(1, 3).FirstOrDefault(GameState.SlotExists);
            if (_tripsSel == 0)
                _tripsSel = 1;
        }

        void Act(string item)
        {
            switch (item)
            {
                case "TRIPS":
                    OpenTrips();
                    break;
                case "NEW GAME":
                {
                    int free = GameState.FreeSlot();
                    if (free == 0)
                    {
                        _menuMsg = "all three trips are full - scrap one";
                        _menuMsgT = 2.5f;
                        break;
                    }
                    if (_newConfirm <= 0f)
                    {
                        _newConfirm = 4f;
                        _menuMsg = "really? press again";
                        _menuMsgT = 2.5f;
                        break;
                    }
                    NewGame(free);
                    break;
                }
                case "HOW TO PLAY":
                    _helpOpen = true;
                    break;
                case "QUIT":
                    Exit();
                    break;
            }
        }
    }

    private void DrawTitleScreen()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        GraphicsDevice.Clear(new Color(8, 12, 24));
        _batch.Begin(samplerState: SamplerState.PointClamp);
        // dark water band at the bottom for a little backdrop life
        for (int i = 0; i < 6; i++)
            _batch.Draw(_pixel, new Rectangle(0, h / 2 + i * (h / 12), w, h / 12 + 1),
                        new Color((byte)(18 + i * 4), (byte)(26 + i * 5), (byte)(40 + i * 6)));
        _font.Draw(_batch, "HOT", new Vector2(90, 60), 14, new Color(238, 232, 212));
        _font.Draw(_batch, "RODS", new Vector2(90, 130), 14, new Color(238, 232, 212));
        _font.Draw(_batch, "A STONED FISHING COLLECT-A-THON", new Vector2(96, 218), 3,
                   new Color(240, 214, 168));
        _batch.Draw(_pixel, new Rectangle(96, 252, 372, 3), new Color(180, 140, 70));
        _font.Draw(_batch, "40 SPECIES - 4 WATERS - 1 VERY RELAXED ANGLER",
                   new Vector2(96, 268), 2, new Color(200, 192, 174));

        if (_tripsOpen)
        {
            _batch.Draw(_pixel, new Rectangle(0, 0, w, h), new Color(4, 6, 14, 235));
            _font.Draw(_batch, "TRIPS - PICK A SAVE", new Vector2(w / 2f - 110, 40), 3,
                       new Color(240, 214, 120));
            for (int i = 1; i <= 3; i++)
            {
                int y = 100 + (i - 1) * 110;
                var box = new Rectangle(w / 2 - 320, y, 640, 96);
                var (exists, level, cash, spot, species, mins) = GameState.SlotInfo(i);
                var col = i == _tripsSel ? new Color(255, 255, 200) : new Color(170, 180, 210);
                _batch.Draw(_pixel, box, new Color(22, 26, 40, 235));
                _batch.Draw(_pixel, new Rectangle(box.X, box.Y, box.Width, 2), col);
                string head = (i == _tripsSel ? "> " : "  ") + "TRIP " + i;
                _font.Draw(_batch, head, new Vector2(box.X + 20, box.Y + 12), 2,
                           exists ? new Color(240, 214, 120) : new Color(110, 120, 150));
                if (!exists)
                {
                    _font.Draw(_batch, "EMPTY SLOT - ENTER TO START", new Vector2(box.X + 20, box.Y + 40), 2,
                               new Color(110, 120, 150));
                }
                else
                {
                    _font.Draw(_batch, GameData.SpotNames[Math.Min(spot, 3)],
                               new Vector2(box.X + 20, box.Y + 36), 3, new Color(238, 232, 212));
                    _font.Draw(_batch, "LV " + level + "  $" + cash + "  " + species + "/40 FISH  " + mins + " MIN",
                               new Vector2(box.X + 20, box.Y + 66), 2, new Color(120, 130, 160));
                }
            }
            _font.Draw(_batch, "ENTER BOARD  X SCRAP  ESC BACK",
                       new Vector2(w / 2f - 130, h - 46), 2, new Color(130, 140, 170));
            if (_scrapConfirm != 0)
                _font.Draw(_batch, "SCRAP TRIP " + _scrapConfirm + "? PRESS X AGAIN",
                           new Vector2(w / 2f - 140, h - 76), 2, new Color(240, 150, 120));
        }
        else
        {
            var items = TitleItems();
            int y = 330;
            foreach (var item in items)
            {
                int i = Array.IndexOf(items, item);
                var col = i == _titleSel ? new Color(255, 255, 200) : new Color(150, 160, 200);
                _font.Draw(_batch, (i == _titleSel ? "> " : "  ") + item,
                           new Vector2(96, y), 4, col);
                y += 56;
            }
            if (_menuMsgT > 0f)
                _font.Draw(_batch, _menuMsg, new Vector2(96, y + 8), 2,
                           new Color(240, 214, 120));
            _font.Draw(_batch, "UP/DOWN  ENTER  -  T TRIPS  N NEW GAME  H HELP  ESC QUIT",
                       new Vector2(96, h - 44), 2, new Color(130, 140, 170));
            _font.Draw(_batch, "C# PORT  V1.0", new Vector2(w - 120, h - 44), 2,
                       new Color(130, 140, 170));
        }

        if (_helpOpen)
            DrawHelp();
        _batch.End();
    }

    // ------------------------------------------------------------------ help --
    private void DrawHelp()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        _batch.Draw(_pixel, new Rectangle(0, 0, w, h), new Color(6, 8, 16, 240));
        _font.Draw(_batch, "HOW TO PLAY", new Vector2(40, 30), 3, new Color(238, 232, 212));
        string[] lines =
        {
            "SPACE  CAST - HOLD TO CHARGE, TAP AT THE BITE",
            "REEL  - KEEP THE NEEDLE IN THE BLUE, THEN GREEN",
            "E  SMOKE    Q  SWITCH PIECE    X  SELL THE COOLER",
            "ESC  MENU - STORE, SALS SHACK, JARS, JOURNAL, STOKE,",
            "MAP AND TRAVEL, HOW TO PLAY, SAVE AND SAVE + TITLE",
        };
        int y = 80;
        foreach (var s in lines)
        {
            _font.Draw(_batch, s, new Vector2(40, y), 2, new Color(200, 208, 230));
            y += 30;
        }
        y += 10;
        foreach (var tip in GameData.Tips)
        {
            _font.Draw(_batch, tip, new Vector2(40, y), 2, new Color(150, 216, 160));
            y += 28;
        }
        _font.Draw(_batch, "ESC BACK", new Vector2(40, h - 40), 2,
                   new Color(130, 140, 170));
    }
}
