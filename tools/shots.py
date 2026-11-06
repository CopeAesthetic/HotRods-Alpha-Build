"""Boot the app headless and save screenshots of screens."""
import os, sys
os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import pygame
from game.app import App

app = App()
app.audio.set_muted(True)
which = sys.argv[1] if len(sys.argv) > 1 else "title"

def render(n=2, dt=1/60.0):
    for _ in range(n):
        app.stack[-1].update(dt)
        app.draw()

if which == "play":
    from game.screens.play import PlayScreen
    app.new_world(0)
    app.stack[-1] = PlayScreen(app)
    render(40)
    pygame.image.save(app.canvas, "/tmp/shot_play.png")
    print("play ok")

if which == "title":
    render(30)
    pygame.image.save(app.canvas, "/tmp/shot_title.png")
    print("title ok")

pygame.quit()
