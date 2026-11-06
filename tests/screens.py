"""Screenshot regression harness.

    python tests/screens.py --update   # write/refresh baselines
    python tests/screens.py            # compare against baselines

Every screen is rendered through the dummy video driver, so this runs
headless in CI.
"""
from __future__ import annotations

import os
import sys

os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import pygame  # noqa: E402

from game.app import App  # noqa: E402
from game.player import Player  # noqa: E402
from game.screens.dealer import DealerScreen  # noqa: E402
from game.screens.journal import JournalScreen  # noqa: E402
from game.screens.overlays import HelpScreen, PauseScreen  # noqa: E402
from game.screens.play import PlayScreen  # noqa: E402
from game.screens.shop import ShopScreen  # noqa: E402
from game.screens.stoke import StokeScreen  # noqa: E402
from game.screens.trips import TripsScreen  # noqa: E402
from game.screens.zaa import ZaaScreen  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
BASE = os.path.join(HERE, "baseline")
THRESHOLD = 0.020          # mean brightness delta after downscaling (font AA safe)
# Text-heavy screens differ more between platforms: the help panel is almost
# all glyphs, so its antialiasing noise dominates. A real layout break shows
# up at ~0.3, so 0.06 still leaves a wide margin.
PER_SCREEN = {"help": 0.060}


def build_app():
    app = App()
    app.audio.set_muted(True)
    app.player = Player()
    app.player.weed = {"homegrown": 6}
    app.new_world(0)
    app.stack[-1] = PlayScreen(app)
    return app


def screens(app):
    """(name, factory) for every full screen worth pinning."""
    return [
        ("play", None),
        ("journal", lambda: JournalScreen(app)),
        ("dealer", lambda: DealerScreen(app)),
        ("shop", lambda: ShopScreen(app)),
        ("stoke", lambda: StokeScreen(app)),
        ("zaa", lambda: ZaaScreen(app)),
        ("trips", lambda: TripsScreen(app)),
        ("pause", lambda: PauseScreen(app)),
        ("help", lambda: HelpScreen(app)),
    ]


def diff(a, b):
    """Compare downscaled frames.

    Font antialiasing differs between macOS and Windows, so a pixel-exact
    compare flags every screen. Smooth-scaling to a thumbnail averages the
    glyph edges out while still catching layout and colour changes.
    """
    if a.get_size() != b.get_size():
        return 1.0
    size = (120, 68)
    a = pygame.transform.smoothscale(a, size)
    b = pygame.transform.smoothscale(b, size)
    try:
        import numpy as np
        pa = pygame.surfarray.array3d(a).astype("int16")
        pb = pygame.surfarray.array3d(b).astype("int16")
        return float(np.abs(pa - pb).mean() / 255.0)
    except Exception:
        same = pygame.image.tostring(a, "RGB") == pygame.image.tostring(b, "RGB")
        return 0.0 if same else 1.0


def main():
    update = "--update" in sys.argv
    os.makedirs(BASE, exist_ok=True)
    app = build_app()
    fails = []
    for name, factory in screens(app):
        screen = factory() if factory else app.stack[-1]
        if screen is not app.stack[-1]:
            app.stack.append(screen)
        try:
            for _ in range(3):
                app.stack[-1].update(1 / 60.0)
                app.draw()
            shot = app.canvas.copy()
        finally:
            if screen is not app.stack[-1]:
                app.pop()
        path = os.path.join(BASE, name + ".png")
        if update or not os.path.exists(path):
            pygame.image.save(shot, path)
            print("%-10s baseline written" % name)
            continue
        ratio = diff(pygame.image.load(path), shot)
        ok = ratio <= PER_SCREEN.get(name, THRESHOLD)
        print("%-10s %s  (%.3f delta)" % (name, "PASS" if ok else "FAIL", ratio))
        if not ok:
            fails.append(name)
            pygame.image.save(shot, os.path.join(HERE, "fail-" + name + ".png"))
    if fails and not update:
        print("\n%d screen(s) changed: %s" % (len(fails), ", ".join(fails)))
        sys.exit(1)
    print("\nall screens match" if not update else "\nbaselines updated")


if __name__ == "__main__":
    main()
