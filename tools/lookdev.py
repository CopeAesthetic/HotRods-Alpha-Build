"""Look-dev: render the same shot at each time of day for palette tuning.

    python tools/lookdev.py [spot]

Writes ref/spot<0>_<hour>.png and a contact sheet ref/sheet.png.
"""
from __future__ import annotations

import os
import sys

os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import pygame  # noqa: E402

import game.player as player_mod  # noqa: E402
player_mod.SAVE_PATH = "/tmp/hotrods_lookdev.json"

from game.app import App  # noqa: E402
from game.player import Player  # noqa: E402
from game.screens.play import PlayScreen  # noqa: E402

HOURS = [("dawn", 6.2), ("morning", 9.0), ("day", 13.0), ("dusk", 18.6),
         ("night", 22.0)]


def main():
    spot = int(sys.argv[1]) if len(sys.argv) > 1 else 0
    here = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out = os.path.join(here, "ref")
    os.makedirs(out, exist_ok=True)

    app = App()
    app.audio.set_muted(True)
    app.player = Player()
    app.player.unlocked = {0, 1, 2, 3}
    app.new_world(spot)
    app.stack[-1] = PlayScreen(app)

    shots = []
    for tag, hour in HOURS:
        app.player.hour = hour
        app.world.hour = hour
        app.world.pal = app.world.__class__.__module__ and None
        for _ in range(3):
            app.stack[-1].update(1 / 60.0)
            app.draw()
        shot = app.canvas.copy()
        path = os.path.join(out, "spot%d_%s.png" % (spot, tag))
        pygame.image.save(shot, path)
        shots.append((tag, pygame.transform.smoothscale(shot, (320, 180))))
        print("wrote", path)

    sheet = pygame.Surface((320 * len(shots), 180))
    for i, (tag, img) in enumerate(shots):
        sheet.blit(img, (i * 320, 0))
    pygame.image.save(sheet, os.path.join(out, "sheet.png"))
    print("wrote", os.path.join(out, "sheet.png"))


if __name__ == "__main__":
    main()
