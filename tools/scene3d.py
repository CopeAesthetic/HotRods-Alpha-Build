"""Render the low-poly scene at several hours."""
import os, sys
os.environ.setdefault("SDL_VIDEODRIVER", "dummy"); os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import pygame
pygame.init(); pygame.display.set_mode((10, 10))
from game.world3d import World, INTERNAL_W, INTERNAL_H

hours = [8.0, 13.0, 18.8, 22.0]
for i, h in enumerate(hours):
    w = World(0)
    w.update(1/60.0, h, 0.0)
    for _ in range(20):
        w.update(1/60.0, h, 0.0)
    s = pygame.Surface((INTERNAL_W, INTERNAL_H))
    w.draw(s, "idle", 0.55)
    big = pygame.transform.scale(s, (1280, 720))
    pygame.image.save(big, "/tmp/lp_%d.png" % i)
    print("saved", i, h, "polys", len(w.scene.polys))
