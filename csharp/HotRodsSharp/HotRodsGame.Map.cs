namespace HotRodsSharp;

/// <summary>
/// Travel menu: a stylized map of the four waters. Pick a water, Enter
/// travels there (paying the unlock if the trip is ready). Unlocks are
/// bought here directly, or at the Trading Post's WATERS tab.
/// </summary>
public partial class HotRodsGame
{
    // node layout on the 800x480 map panel
    private static readonly (int x, int y)[] MapNodes =
    {
        (170, 330),   // pond
        (350, 250),   // lake
        (540, 150),   // marsh
        (430, 70),    // trench
    };

    private void UpdateMap(float dt, KeyboardState kb)
    {
        _menuMsgT -= dt;
        void Edge(ref bool held, Keys k, Action a)
        {
            if (kb.IsKeyDown(k) && !held) { a(); held = true; }
            if (kb.IsKeyUp(k)) held = false;
        }

        Edge(ref _leftHeld, Keys.Left, () => _mapSel = (_mapSel + 3) % 4);
        Edge(ref _rightHeld, Keys.Right, () => _mapSel = (_mapSel + 1) % 4);
        Edge(ref _enterHeld, Keys.Enter, GoMap);
        if (kb.IsKeyDown(Keys.Escape))
        {
            _mapOpen = false;
            _menuOpen = true;
        }
    }

    /// <summary>Travel to the selected water (paying the unlock when ready).</summary>
    private void GoMap()
    {
        if (_state.SpotUnlocked(_mapSel))
        {
            if (_mapSel != _spot)
            {
                _spot = _mapSel;
                _state.Spot = _mapSel;
                _state.Save();
                BuildWorld();
                SteamMan.SetPresence("fishing at " + GameData.SpotNames[_spot]);
            }
            _mapOpen = false;
            return;
        }
        if (_state.TryTravel(_mapSel) is var (ok, msg) && ok)
        {
            GoMap();   // unlock paid - travel
        }
        else
        {
            _menuMsg = msg;
            _menuMsgT = 3f;
            PlaySfx("click");
        }
    }

    /// <summary>Mouse: click a map node to travel there.</summary>
    private void MapClick(Vector2 pos)
    {
        for (int i = 0; i < 4; i++)
        {
            var (nx, ny) = MapNodes[i];
            if (MathF.Abs(pos.X - nx) <= 22 && MathF.Abs(pos.Y - ny) <= 22)
            {
                _mapSel = i;
                GoMap();
                return;
            }
        }
    }

    private void DrawMap()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        _batch.Begin(samplerState: SamplerState.PointClamp);
        _batch.Draw(_pixel, new Rectangle(0, 0, w, h), new Color(8, 11, 20, 242));
        _font.Draw(_batch, "MAP - WHERE TO?", new Vector2(40, 26), 3,
                   new Color(238, 232, 212));
        _font.Draw(_batch, "$" + _state.Cash, new Vector2(w - 120, 26), 3,
                   new Color(240, 214, 120));

        // winding dirt path between the waters, pond to trench
        DrawMapPath(new[]
        {
            new Vector2(MapNodes[0].x, MapNodes[0].y),
            new Vector2(MapNodes[1].x, MapNodes[1].y),
            new Vector2(MapNodes[2].x, MapNodes[2].y),
            new Vector2(MapNodes[3].x, MapNodes[3].y),
        });

        for (int i = 0; i < 4; i++)
        {
            var (nx, ny) = MapNodes[i];
            bool unlocked = _state.SpotUnlocked(i);
            bool here = i == _spot;
            bool sel = i == _mapSel;
            var sp = GameData.Spots[i];

            // node: coloured square in the water's tint, dark when locked
            var nodeCol = unlocked || here
                ? new Color(sp.Tint.R, sp.Tint.G, sp.Tint.B) : new Color(56, 62, 84);
            _batch.Draw(_pixel, new Rectangle(nx - 12, ny - 12, 24, 24), nodeCol);
            _batch.Draw(_pixel, new Rectangle(nx - 14, ny - 14, 28, 2),
                        sel ? new Color(255, 255, 200) : new Color(40, 46, 66));
            _batch.Draw(_pixel, new Rectangle(nx - 14, ny + 12, 28, 2),
                        sel ? new Color(255, 255, 200) : new Color(40, 46, 66));
            _batch.Draw(_pixel, new Rectangle(nx - 14, ny - 14, 2, 28),
                        sel ? new Color(255, 255, 200) : new Color(40, 46, 66));
            _batch.Draw(_pixel, new Rectangle(nx + 12, ny - 14, 2, 28),
                        sel ? new Color(255, 255, 200) : new Color(40, 46, 66));
            if (here)
                _batch.Draw(_pixel, new Rectangle(nx - 5, ny - 5, 10, 10),
                            new Color(255, 240, 140));

            string label = unlocked || here ? sp.Name.ToUpper() : "???";
            _font.Draw(_batch, label, new Vector2(nx - label.Length * 4f, ny + 20), 2,
                       sel ? new Color(255, 255, 200)
                           : unlocked ? new Color(170, 180, 210)
                           : new Color(96, 102, 124));
        }

        // info card for the selected water
        var spSel = GameData.Spots[_mapSel];
        bool uSel = _state.SpotUnlocked(_mapSel);
        int ix = 40, iy = h - 170;
        _batch.Draw(_pixel, new Rectangle(ix, iy, w - 80, 130), new Color(22, 26, 40, 235));
        _batch.Draw(_pixel, new Rectangle(ix, iy, w - 80, 2),
                    new Color(spSel.Tint.R, spSel.Tint.G, spSel.Tint.B));
        _font.Draw(_batch, (uSel ? spSel.Name.ToUpper() : "???") + (_mapSel == _spot ? "  (HERE)" : ""),
                   new Vector2(ix + 16, iy + 10), 3, new Color(238, 232, 212));
        if (uSel)
        {
            _font.Draw(_batch, spSel.Blurb, new Vector2(ix + 16, iy + 40), 2,
                       new Color(150, 160, 190));
            _font.Draw(_batch, _state.SpeciesFound(_mapSel) + "/"
                       + GameData.SpeciesList.Count(f => f.Spot == _mapSel) + " SPECIES",
                       new Vector2(ix + 16, iy + 78), 2, new Color(150, 216, 160));
        }
        else
        {
            int prev = Math.Max(0, _mapSel - 1);
            _font.Draw(_batch, "Catch all "
                       + GameData.SpeciesList.Count(f => f.Spot == prev) + " in "
                       + GameData.SpotNames[prev] + " then pay $" + spSel.NeedCash,
                       new Vector2(ix + 16, iy + 40), 2, new Color(196, 148, 120));
            _font.Draw(_batch, "Unlocks also sell at the Trading Post (B key)",
                       new Vector2(ix + 16, iy + 78), 2, new Color(120, 130, 160));
        }
        if (_menuMsgT > 0f)
            _font.Draw(_batch, _menuMsg, new Vector2(ix + 16, iy + 104), 2,
                       new Color(240, 214, 120));
        _font.Draw(_batch, "LEFT/RIGHT PICK  ENTER TRAVEL  ESC BACK",
                   new Vector2(40, h - 28), 2, new Color(130, 140, 170));
        _batch.End();
    }

    /// <summary>Dotted dirt path between the map nodes.</summary>
    private void DrawMapPath(Vector2[] pts)
    {
        for (int seg = 0; seg < pts.Length - 1; seg++)
        {
            var a = pts[seg];
            var b = pts[seg + 1];
            int steps = (int)(Vector2.Distance(a, b) / 5f);
            for (int i = 0; i <= steps; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)steps);
                _batch.Draw(_pixel, new Rectangle((int)p.X - 1, (int)p.Y - 1, 3, 3),
                            new Color(96, 82, 62));
            }
        }
    }
}
