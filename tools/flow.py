"""Drive the fishing loop headlessly and screenshot each state."""
import os, sys
os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import pygame
import game.player as player_mod
player_mod.SAVE_PATH = '/tmp/qa_save.json'
import os as _os
if _os.path.exists('/tmp/qa_save.json'): _os.remove('/tmp/qa_save.json')
from game.app import App
from game.screens.play import PlayScreen
from game import config as C

app = App(); app.audio.set_muted(True)
app.new_world(0)
play = PlayScreen(app)
app.stack[-1] = play

def step(sec, dt=1/60.0):
    for _ in range(int(sec / dt)):
        play.update(dt)

def key(k, down=True):
    t = pygame.KEYDOWN if down else pygame.KEYUP
    play.handle_event(pygame.event.Event(t, key=k, unicode=""))

def shot(name):
    app.draw()
    pygame.image.save(app.canvas, "/tmp/flow_%s.png" % name)
    print("shot", name, "state=", play.angler.state)

shot("idle")
key(pygame.K_SPACE); step(0.55); shot("charge")
key(pygame.K_SPACE, False); step(0.25); shot("fly")
step(0.6)
play.angler.wait_duration = 0.4
step(0.5); shot("bite")
key(pygame.K_SPACE); step(0.5)
print("after hook:", play.angler.state, play.angler.fish and play.angler.fish["name"])
# keep the needle in the band for a nice screenshot
for _ in range(240):
    a = play.angler
    if a.state != "reel": break
    a.holding = a.tension < a.band
    play.update(1/60.0)
shot("reel")
# finish the fight
for i in range(1200):
    a = play.angler
    if play.card: break
    if a.state == "result": break
    a.holding = a.tension < a.band
    play.update(1/60.0)
print("card:", play.card and (play.card["sp"]["name"], play.card["kg"], play.card["value"]))
step(0.35)
shot("card")
print("cash", app.player.cash, "found", app.player.found_count)
pygame.quit()
