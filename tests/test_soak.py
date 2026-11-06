"""Short headless soak: hammer the UI with random input and assert it survives.

Not a balance test - this is the "does anything crash or corrupt the save"
check that runs in CI on every commit.
"""
from __future__ import annotations

import os
import random
import sys

os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import pygame  # noqa: E402

import game.player as player_mod  # noqa: E402
player_mod.SAVE_PATH = "/tmp/hotrods_soak.json"

from game.app import App  # noqa: E402
from game.player import Player  # noqa: E402
from game.screens.play import PlayScreen  # noqa: E402

KEYS = [pygame.K_SPACE, pygame.K_e, pygame.K_j, pygame.K_k, pygame.K_z,
        pygame.K_b, pygame.K_x, pygame.K_ESCAPE]
FRAMES = 900


def main():
    random.seed(7)
    if os.path.exists(player_mod.SAVE_PATH):
        os.remove(player_mod.SAVE_PATH)
    app = App()
    app.audio.set_muted(True)
    app.player = Player()
    app.player.cash = 500
    app.player.weed = {"homegrown": 8}
    app.new_world(0)
    app.stack[-1] = PlayScreen(app)

    start_cash = None
    for i in range(FRAMES):
        if random.random() < 0.04:
            key = random.choice(KEYS)
            try:
                app.stack[-1].handle_event(
                    pygame.event.Event(pygame.KEYDOWN, key=key, unicode=""))
            except Exception as exc:                     # noqa: BLE001
                raise AssertionError("frame %d: key %s crashed: %s"
                                     % (i, pygame.key.name(key), exc))
        try:
            app.stack[-1].update(1 / 60.0)
            app.draw()
        except Exception as exc:                         # noqa: BLE001
            raise AssertionError("frame %d: update/draw crashed: %s" % (i, exc))
        if start_cash is None and app.player.cash != 500:
            start_cash = app.player.cash

    p = app.player
    assert p.cash >= 0, "cash went negative: %d" % p.cash
    assert p.pts >= 0, "VP went negative: %d" % p.pts
    assert 0.0 <= p.chill <= 100.0, "chill out of range: %s" % p.chill
    assert p.xp >= 0, "xp negative"

    # save round-trip
    assert p.save(), "save failed"
    loaded = Player.load(player_mod.SAVE_PATH)
    assert loaded.cash == p.cash, "cash did not survive a save/load"
    assert loaded.xp == p.xp, "xp did not survive a save/load"
    assert loaded.level == p.level
    print("soak: %d frames, cash %d, xp %d, level %d" % (FRAMES, p.cash, p.xp, p.level))
    print("soak PASS")


if __name__ == "__main__":
    main()
