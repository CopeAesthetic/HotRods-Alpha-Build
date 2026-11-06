"""End-to-end UI smoke test: title, catch, cooler, Sal, shop, travel, save/continue."""
import os, sys
os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import pygame
import game.player as player_mod
player_mod.SAVE_PATH = "/tmp/smoke_save.json"
if os.path.exists("/tmp/smoke_save.json"):
    os.remove("/tmp/smoke_save.json")
from game.app import App
from game.screens.title import TitleScreen
from game.screens.play import PlayScreen
from game.screens.journal import JournalScreen
from game.screens.shop import ShopScreen
from game.screens.dealer import DealerScreen
from game.screens.overlays import PauseScreen
from game import data

app = App(); app.audio.set_muted(True)

def click(screen, rect):
    app.mouse = rect.center
    screen.mouse = app.mouse
    screen.update(0.016)
    ev = pygame.event.Event(pygame.MOUSEBUTTONDOWN, button=1, pos=rect.center)
    screen.handle_event(ev)

def press(screen, key):
    screen.mouse = app.mouse
    screen.handle_event(pygame.event.Event(pygame.KEYDOWN, key=key, unicode=""))

assert isinstance(app.stack[-1], TitleScreen), app.stack
title = app.stack[-1]
print("title buttons:", [b.label for b in title.buttons])
click(title, title.buttons[0].rect)
assert isinstance(app.stack[-1], PlayScreen), app.stack
play = app.stack[-1]
print("started at", play.player.spot_data["name"])

# --- catch a fish into the cooler -----------------------------------------
a = play.angler
a.state = "reel"; a.depth = 0.7
a.fish = data.species("green_sunfish"); a.kg = 0.55
a.cm = data.length_cm(a.kg, a.fish["shape"])
a.band = 0.5; a.band_half = 0.12; a.tension = 0.5; a.progress = 0.99
a.over_t = 0.0; a.wander_t = 0.0; a.result = None
for _ in range(30):
    play.update(1/60.0)
    if play.card: break
assert play.card is not None, "no catch card"
assert len(play.player.bag) == 1, play.player.bag
print("caught", play.card["sp"]["name"], "cooler", play.card["kept"], "/", play.card["capacity"])
press(play, pygame.K_SPACE)
assert play.card is None and play.angler.state == "idle"

# --- journal ---------------------------------------------------------------
press(play, pygame.K_j)
assert isinstance(app.stack[-1], JournalScreen), app.stack
press(app.stack[-1], pygame.K_ESCAPE)
assert isinstance(app.stack[-1], PlayScreen)

# --- Sal: sell + trade + weed ---------------------------------------------
p = play.player
for sid in ("common_carp", "largemouth_bass", "green_sunfish", "bluegill"):
    sp = data.species(sid)
    p.add_catch(sid, sp["kg"][0] * 2, 30)
press(play, pygame.K_d)
sal = app.stack[-1]
assert isinstance(sal, DealerScreen), app.stack
cash0 = p.cash
click(sal, sal.slots()[0])
assert p.cash > cash0, (cash0, p.cash)
print("sold a fish for", p.cash - cash0, "-> cash", p.cash)
sal.set_mode("pts"); sal.build()
pts0 = p.pts
click(sal, sal.slots()[0])
assert p.pts > pts0, (pts0, p.pts)
print("traded a fish for", p.pts - pts0, "pts; sal_tier", p.sal_tier)
# trade everything else so we can afford a jar
sal.bulk()
print("after bulk trade: pts", p.pts, "bag", len(p.bag),
      "indica?", p.jar_available("pond_haze"))
sal.buy("pond_haze")
if p.weed_count("pond_haze") == 0:
    # not enough points: give the test some and retry
    p.pts += 20
    sal.buy("pond_haze")
assert p.weed_count("pond_haze") == 1, p.weed
assert p.strain == "pond_haze"
print("bought indica, weed", p.weed, "strain", p.strain)
press(sal, pygame.K_d)
assert isinstance(app.stack[-1], PlayScreen)

# --- toke uses a gram ------------------------------------------------------
chill0 = p.chill
press(play, pygame.K_e)
for _ in range(220):          # the pipe ceremony takes five seconds
    play.update(1/60.0)
assert p.chill > chill0 and p.weed_count("pond_haze") == 0, (p.chill, p.weed)
print("toked; chill", round(p.chill), "weed left", p.weed)

# --- shop: buy a cooler ----------------------------------------------------
press(play, pygame.K_b)
shop = app.stack[-1]
assert isinstance(shop, ShopScreen), app.stack
shop.set_tab("cooler"); shop.build()
p.cash = 900
shop.build()
cooler_btn = shop.buttons[9]
print("cooler button:", cooler_btn.label)
click(shop, cooler_btn.rect)
print("cooler now", p.cooler, "slots", p.bag_size)
assert p.bag_size == 12, p.bag_size
press(shop, pygame.K_ESCAPE)

# --- travel ----------------------------------------------------------------
for sp in data.SPECIES[:8]:
    if not p.caught(sp["id"]):
        p.add_catch(sp["id"], sp["kg"][0], 20)
press(play, pygame.K_b)
shop = app.stack[-1]
shop.set_tab("travel"); shop.build()
click(shop, shop.buttons[9].rect)
assert p.spot == 1, p.spot
assert app.world.spot_index == 1
print("travelled to", p.spot_data["name"])

# --- pause -> save & quit -> continue --------------------------------------
press(play, pygame.K_ESCAPE)
pause = app.stack[-1]
assert isinstance(pause, PauseScreen)
click(pause, [b for b in pause.buttons if b.label == "SAVE & QUIT"][0].rect)
assert isinstance(app.stack[-1], TitleScreen), app.stack
assert app.has_save and os.path.exists("/tmp/smoke_save.json")
title = app.stack[-1]
assert title.buttons[0].label == "CONTINUE"
cash = app.player.cash
click(title, title.buttons[0].rect)
assert isinstance(app.stack[-1], PlayScreen)
assert abs(app.player.cash - cash) < 0.01
print("resumed with cash", app.player.cash, "cooler", len(app.player.bag), "pts",
      app.player.pts)
app.save()
print("SMOKE TEST PASSED")
pygame.quit()
