"""End-to-end UI flows, driven by label instead of button index."""
from __future__ import annotations

import os
import sys
import tempfile

os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import pygame  # noqa: E402

import game.player as player_mod  # noqa: E402
player_mod.SAVE_PATH = os.path.join(tempfile.gettempdir(), "hotrods_flow.json")

from game.app import App  # noqa: E402
from game.player import Player  # noqa: E402
from game.screens.dealer import DealerScreen  # noqa: E402
from game.screens.play import PlayScreen  # noqa: E402
from game.screens.shop import ShopScreen  # noqa: E402
from tests.ui import click, find, press  # noqa: E402


def fresh():
    if os.path.exists(player_mod.SAVE_PATH):
        os.remove(player_mod.SAVE_PATH)
    app = App()
    app.audio.set_muted(True)
    app.player = Player()
    app.new_world(0)
    app.stack[-1] = PlayScreen(app)
    return app


def test_sell_fish():
    app = fresh()
    p = app.player
    p.add_catch("bluegill", 0.2, 12)
    p.add_catch("pumpkinseed", 0.2, 12)
    cash = p.cash
    play = app.stack[-1]
    press(app, play, pygame.K_b)
    assert isinstance(app.stack[-1], ShopScreen), (
        "shop did not open, stack: %s" % [type(x).__name__ for x in app.stack])
    click(app, app.stack[-1], "SELL")          # SELL tab -> the dealer
    assert isinstance(app.stack[-1], DealerScreen), "sell tab did not open"
    click(app, app.stack[-1], "SELL ALL")
    assert p.cash > cash, "selling paid nothing"
    assert not p.bag, "cooler not emptied"


def test_buy_ice():
    app = fresh()
    p = app.player
    p.cash = 500
    p.ice = 0.0
    p.unlocked = {0, 1}
    press(app, app.stack[-1], pygame.K_b)
    click(app, app.stack[-1], "BUY")          # switch to the tackle side
    screen = app.stack[-1]
    assert isinstance(screen, ShopScreen), "buy tab did not open"
    click(app, screen, "COOLERS")
    click(app, screen, "BUY  $15")
    assert p.ice > 0, "ice did not fill"
    assert p.cash < 500, "ice was free"


def test_gear_is_area_locked():
    app = fresh()
    p = app.player
    p.cash = 100000
    ok, why = p.buy_or_equip("rod", "carbon")
    assert not ok and "Lake" in why, "late gear was not area locked: %s" % why
    p.unlocked.add(1)
    ok, why = p.buy_or_equip("rod", "carbon")
    assert ok, "gear should unlock with the lake: %s" % why


def test_journal_tabs():
    app = fresh()
    press(app, app.stack[-1], pygame.K_j)
    screen = app.stack[-1]
    labels = [b.label for b in getattr(screen, "buttons", [])]
    assert "Bongwater Pond" in labels, "journal tabs missing: %s (%s)" % (
        labels, type(screen).__name__)


def main():
    tests = [v for k, v in sorted(globals().items()) if k.startswith("test_")]
    failed = 0
    for t in tests:
        try:
            t()
            print("%-28s PASS" % t.__name__)
        except Exception as exc:
            failed += 1
            print("%-28s FAIL  %s" % (t.__name__, exc))
    print("\n%d/%d passed" % (len(tests) - failed, len(tests)))
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
