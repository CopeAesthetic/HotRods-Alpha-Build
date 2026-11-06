"""Render world scenes across the day (visual QA)."""
import os, sys
os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import pygame
pygame.init(); pygame.display.set_mode((100, 100))
from game import world as W, config as C, data

surf = pygame.Surface((C.W, C.H))
hours = [7.0, 13.0, 18.6, 22.0]
spot = int(sys.argv[1]) if len(sys.argv) > 1 else 0
high = float(sys.argv[2]) if len(sys.argv) > 2 else 0.0
for i, h in enumerate(hours):
    w = W.World(spot)
    for _ in range(int(h * 3)):
        w.update(1/60.0, h, high)
    w.hour = h
    w.pal = w.palette(h)
    w.draw_back(surf)
    w.draw_shadows(surf)
    tip = w.rod_tip()
    bx, by, s = w.bobber_pos(0.55)
    w.rod_angle = -0.55
    w.draw_line(surf, tip, (bx, by), taut=0.2)
    w.draw_float(surf, 0.55, "idle")
    w.draw_front(surf, bend=0.1, cast=0.0)
    pygame.image.save(surf, "/tmp/scene_%d_%d.png" % (spot, i))
print("saved 4 scenes for spot", spot)
