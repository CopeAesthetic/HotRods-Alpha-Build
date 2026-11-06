"""Screenshot every overlay screen with a populated save."""
import os, sys, random
os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import pygame
import game.player as player_mod
player_mod.SAVE_PATH = '/tmp/qa_save.json'
import os as _os
if _os.path.exists('/tmp/qa_save.json'): _os.remove('/tmp/qa_save.json')
from game.app import App
from game.player import Player, roll_size
from game import data
from game.screens.play import PlayScreen

app = App(); app.audio.set_muted(True)
p = Player()
random.seed(3)
for sp in data.SPECIES[:14]:
    kg = roll_size(sp)
    p.add_catch(sp["id"], kg, data.length_cm(kg, sp["shape"]))
p.cash = 1245
p.weed = {"homegrown": 3}
p.tol = 0
app.player = p
app.new_world(1)
app.player.travel(1)
play = PlayScreen(app)
app.stack[-1] = play

def render(n=3, dt=1/60.0):
    for _ in range(n):
        app.stack[-1].update(dt)
        app.draw()

def snap(name):
    render()
    pygame.image.save(app.canvas, "/tmp/ui_%s.png" % name)
    print("saved", name, "stack", [type(s).__name__ for s in app.stack])

from game.screens.journal import JournalScreen
from game.screens.shop import ShopScreen
from game.screens.overlays import PauseScreen, HelpScreen

app.push(JournalScreen(app)); snap("journal"); app.pop()
app.push(ShopScreen(app)); snap("shop_rods")
app.stack[-1].set_tab("lure"); snap("shop_lures")
app.stack[-1].set_tab("travel"); snap("shop_travel")
app.stack[-1].set_tab("cooler"); snap("shop_coolers")
app.pop()
from game.screens.dealer import DealerScreen
for sid in ("common_carp", "largemouth_bass", "golden_koi", "green_sunfish"):
    sp = data.species(sid)
    app.player.add_catch(sid, sp["kg"][0] * 2, 30)
app.player.pts = 11
app.player.weed = {"homegrown": 2}
app.push(DealerScreen(app)); snap("dealer"); app.pop()
app.push(PauseScreen(app)); snap("pause"); app.pop()
app.push(HelpScreen(app)); snap("help"); app.pop()

# click test: buy a lure
app.push(ShopScreen(app))
s = app.stack[-1]; s.set_tab("lure")
before = app.player.cash
btn = s.buttons[7]  # tab buttons 0-6, first row button
print("btn label", btn.label, "rect", btn.rect)
app.mouse = btn.rect.center
btn.click(pygame.event.Event(pygame.MOUSEBUTTONDOWN, button=1), app.mouse)
print("cash before/after", before, app.player.cash, "lures", sorted(app.player.lures))
pygame.quit()
