import os, sys
os.environ.setdefault("SDL_VIDEODRIVER", "dummy"); os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import pygame
pygame.init(); pygame.display.set_mode((10,10))
from game import world as W, config as C
surf = pygame.Surface((C.W, C.H))
cases = [(2, 21.2, 0.0, ("marsh_night")), (3, 22.6, 0.85, ("trench_high")), (1, 17.6, 0.0, ("lake_dusk"))]
for spot, hour, high, name in cases:
    w = W.World(spot)
    w.update(1/60.0, hour, high)
    for _ in range(30):
        w.update(1/60.0, hour, high)
    w.draw_back(surf)
    w.draw_shadows(surf)
    tip = w.rod_tip()
    bx, by, s = w.bobber_pos(0.62)
    w.draw_line(surf, tip, (bx, by), taut=0.2)
    w.draw_float(surf, 0.62, "idle")
    w.draw_front(surf, bend=0.15, cast=0.0)
    pygame.image.save(surf, "/tmp/sc_%s.png" % name[0])
    print("saved", name[0])
