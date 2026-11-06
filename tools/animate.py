"""Dump the smoking ceremony frame by frame.

    python tools/animate.py [smoker] [steps]

Writes ref/anim/0000.png ... so a bad pose can be referenced by number.
"""
from __future__ import annotations

import os
import sys

os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import pygame  # noqa: E402

import game.player as player_mod  # noqa: E402
player_mod.SAVE_PATH = "/tmp/hotrods_animate.json"

from game.app import App  # noqa: E402
from game.player import Player  # noqa: E402
from game.screens.play import PlayScreen  # noqa: E402


def main():
    sid = sys.argv[1] if len(sys.argv) > 1 else "backwoods"
    steps = int(sys.argv[2]) if len(sys.argv) > 2 else 90
    here = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out = os.path.join(here, "ref", "anim")
    os.makedirs(out, exist_ok=True)

    app = App()
    app.audio.set_muted(True)
    p = Player()
    app.player = p
    p.smoker = sid
    p.smokers.add(sid)
    p.weed = {"homegrown": 9}
    app.new_world(0)
    play = PlayScreen(app)
    app.stack[-1] = play
    play.toke()
    total = play.pipe_total
    for i in range(steps):
        play.update(total / steps)
        app.draw()
        pygame.image.save(app.canvas, os.path.join(out, "%04d.png" % i))
    print("wrote %d frames to %s" % (steps, out))


if __name__ == "__main__":
    main()
