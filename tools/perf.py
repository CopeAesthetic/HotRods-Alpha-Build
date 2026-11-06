"""Frame-time benchmark for the play screen."""
import os, sys, time
os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import pygame
from game.app import App
from game.screens.play import PlayScreen

app = App(); app.audio.set_muted(True)
app.new_world(0)
play = PlayScreen(app); app.stack[-1] = play
app.player.chill = 100.0  # high effects on = worst case

def bench(label, frames=150):
    # warm the caches
    for _ in range(10):
        play.update(1/60.0); app.draw()
    t0 = time.perf_counter()
    for _ in range(frames):
        play.update(1/60.0)
        app.draw()
    el = time.perf_counter() - t0
    print("%-24s %6.1f fps   %5.2f ms/frame" % (label, frames/el, el/frames*1000))

bench("idle + high")
# force a reel fight
play.angler.press()
for _ in range(40):
    play.update(1/60.0)
play.angler.release()
for _ in range(60):
    play.update(1/60.0)
play.angler.wait_duration = 0.2
for _ in range(40):
    play.update(1/60.0)
if play.angler.state == "bite":
    play.angler.press()
for _ in range(30):
    play.update(1/60.0)
print("state:", play.angler.state)
bench("reeling + splashes")
# card
if play.card:
    play.card_anim = 1.0
    bench("catch card")
pygame.quit()
