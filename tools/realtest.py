"""Short run with the real video/audio drivers to verify the window path."""
import os, sys, time
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import pygame
from game.app import App

app = App()
t0 = time.time(); frames = 0
while time.time() - t0 < 1.6:
    dt = min(0.05, app.clock.tick(60) / 1000.0)
    pygame.event.pump()
    app.stack[-1].update(dt)
    app.draw()
    frames += 1
flat = pygame.Surface((app.screen.get_width(), app.screen.get_height()), 0, 24)
flat.blit(app.screen, (0, 0))
pygame.image.save(flat, "/tmp/real_window.png")
print("driver", pygame.display.get_driver(), "window", app.screen.get_size(),
      "frames", frames, "audio ok", app.audio.ok)
pygame.quit()
