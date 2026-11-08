namespace HotRodsSharp;

/// <summary>
/// Stoke: the skill tree - three lanes, one point per level.
/// fish  +1.2% hit arc per rank (FishingSim), smoke +6% ceremony speed per
/// rank (SmokingSim), vibes +5% high length per rank (HotRodsGame decay).
/// Ported from the Python stoke.py screen. Keyboard driven.
/// </summary>
public partial class HotRodsGame
{
    private static readonly (string key, string title, Color col, string eff, string blurb)[]
        Lanes =
        {
            ("fish", "FISHING", new Color(120, 190, 130),
             "+1.2% HIT ARC PER RANK", "Wider green arc on every pull."),
            ("smoke", "SMOKING", new Color(150, 210, 236),
             "+6% CEREMONY SPEED PER RANK", "Every toke finishes faster."),
            ("vibes", "VIBES", new Color(214, 150, 236),
             "+5% HIGH LENGTH PER RANK", "The good mood lasts longer."),
        };

    private void UpdateStoke(float dt, KeyboardState kb)
    {
        _menuMsgT -= dt;
        void Edge(ref bool held, Keys k, Action a)
        {
            if (kb.IsKeyDown(k) && !held) { a(); held = true; }
            if (kb.IsKeyUp(k)) held = false;
        }

        Edge(ref _leftHeld, Keys.Left, () => _stokeSel = (_stokeSel + 2) % 3);
        Edge(ref _rightHeld, Keys.Right, () => _stokeSel = (_stokeSel + 1) % 3);
        Edge(ref _enterHeld, Keys.Enter, () =>
        {
            var (ok, msg) = _state.SpendStoke(Lanes[_stokeSel].key);
            _menuMsg = msg;
            _menuMsgT = 3f;
            PlaySfx(ok ? "click" : "click");
        });
        if (kb.IsKeyDown(Keys.Escape))
        {
            _stokeOpen = false;
            _menuOpen = true;
        }
    }

    private void DrawStoke()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        _batch.Begin(samplerState: SamplerState.PointClamp);
        _batch.Draw(_pixel, new Rectangle(0, 0, w, h), new Color(8, 11, 20, 238));
        _font.Draw(_batch, "STOKE - LEVEL " + _state.Level + "  XP " + _state.Xp
                   + "  POINTS " + _state.StokePts,
                   new Vector2(40, 30), 3, new Color(238, 232, 212));

        for (int i = 0; i < Lanes.Length; i++)
        {
            var (key, title, col, eff, blurb) = Lanes[i];
            int x = 40 + i * 250;
            var box = new Rectangle(x, 90, 230, 280);
            var boxCol = i == _stokeSel ? new Color(40, 48, 70) : new Color(24, 28, 44);
            _batch.Draw(_pixel, box, boxCol);
            _batch.Draw(_pixel, new Rectangle(box.X, box.Y, box.Width, 2),
                        i == _stokeSel ? new Color(255, 255, 200) : new Color(60, 68, 96));
            _font.Draw(_batch, title, new Vector2(box.X + 16, box.Y + 14), 3, col);
            int rank = _state.StokeRank(key);
            for (int j = 0; j < 8; j++)
            {
                var pip = new Rectangle(box.X + 16 + j * 26, box.Y + 52, 14, 14);
                _batch.Draw(_pixel, pip, j < rank ? col : new Color(44, 48, 66));
            }
            _font.Draw(_batch, rank + "/8", new Vector2(box.Right - 40, box.Y + 48), 2,
                       new Color(120, 130, 160));
            _font.Draw(_batch, eff, new Vector2(box.X + 16, box.Y + 84), 2,
                       new Color(196, 202, 220));
            _font.Draw(_batch, blurb, new Vector2(box.X + 16, box.Y + 112), 2,
                       new Color(120, 130, 160));
            _font.Draw(_batch, "SPEND" + (i == _stokeSel ? "  <" : ""),
                       new Vector2(box.X + 16, box.Y + 216), 3,
                       i == _stokeSel ? new Color(255, 255, 200) : new Color(110, 120, 150));
        }

        if (_menuMsgT > 0f)
            _font.Draw(_batch, _menuMsg, new Vector2(40, h - 76), 2,
                       new Color(240, 214, 120));
        _font.Draw(_batch,
                   "ONE STOKE POINT PER LEVEL  -  LEFT/RIGHT LANE  ENTER SPEND  ESC BACK",
                   new Vector2(40, h - 40), 2, new Color(130, 140, 170));
        _batch.End();
    }
}
