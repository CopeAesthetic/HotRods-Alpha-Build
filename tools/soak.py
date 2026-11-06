"""Long simulated play session: cast, fill the cooler, sell/trade at Sal, smoke."""
import os, sys, random
os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import pygame
import game.player as player_mod
player_mod.SAVE_PATH = "/tmp/soak_save.json"
if os.path.exists("/tmp/soak_save.json"):
    os.remove("/tmp/soak_save.json")
from game.app import App
from game.screens.play import PlayScreen
from game import data

random.seed(11)
app = App(); app.audio.set_muted(True)
app.has_save = False
app.player = player_mod.Player()
app.new_world(0)
play = PlayScreen(app); app.stack[-1] = play
p = app.player

BUCKET = 300
MINUTES = 24
frames = int(MINUTES * 60 * 60)
dt = 1 / 60.0
t = 0.0
next_bucket = BUCKET
log = []
trips = 0
weed_bought = {}


def best_jar():
    avail = [s for s in data.STRAINS if p.jar_available(s["id"])]
    return max(avail, key=lambda s: s["tier"]) if avail else data.STRAINS[0]


def sal_visit():
    """Trade or sell the cooler, then stock up."""
    global trips
    if not p.bag:
        return
    trips += 1
    jar = best_jar()
    want_weed = trips % 2 == 0 or p.weed_count(jar["id"]) == 0
    if want_weed and p.pts + p.bag_points >= jar["pts"]:
        n, pts = p.trade_all()
        log.append("[%4.0fs] traded %d fish (%d pts) at Sal's" % (t, n, pts))
        while p.pts >= jar["pts"] and p.weed_count(jar["id"]) < 3:
            ok, msg = p.buy_weed(jar["id"])
            if not ok:
                break
            weed_bought[jar["id"]] = weed_bought.get(jar["id"], 0) + 1
    else:
        n, cash = p.sell_all()
        log.append("[%4.0fs] sold %d fish for $%d" % (t, n, cash))


def buy_gear():
    reserve = 0
    for i, sp in enumerate(data.SPOTS):
        if i not in p.unlocked and p.found_count >= sp["need_species"]:
            reserve = sp["need_cash"]
            break
    for kind, table in (("rod", data.RODS), ("line", data.LINES),
                        ("lure", data.LURES), ("cooler", data.COOLERS)):
        for item in table:
            owned = item["id"] in (p.coolers if kind == "cooler" else p._owned(kind))
            if owned:
                continue
            if p.cash - item["cost"] >= reserve:
                ok, msg = p.buy_or_equip(kind, item["id"])
                if ok:
                    log.append("[%4.0fs] bought %s %s for $%d" % (t, kind, item["id"],
                                                                  item["cost"]))
                    return


def maybe_travel():
    for i, sp in enumerate(data.SPOTS):
        if i != p.spot and i not in p.unlocked:
            ok, why = p.can_travel(i)
            if ok:
                p.travel(i)
                app.new_world(i)
                log.append("[%4.0fs] travelled to %s" % (t, sp["name"]))
                return


last_toke = 0.0
for f in range(frames):
    t += dt
    if play.card is not None:
        play.handle_event(pygame.event.Event(pygame.KEYDOWN, key=pygame.K_SPACE, unicode=" "))
        continue
    a = play.angler
    st = a.state
    if p.bag_full and st in ("idle", "wait"):
        sal_visit()
        play.build()
    if st == "idle" and not p.bag_full:
        play.handle_event(pygame.event.Event(pygame.KEYDOWN, key=pygame.K_SPACE, unicode=" "))
    elif st == "charge":
        if a.power > random.uniform(0.55, 0.95):
            play.handle_event(pygame.event.Event(pygame.KEYUP, key=pygame.K_SPACE, unicode=" "))
    elif st == "bite":
        if random.random() > 0.06:
            play.handle_event(pygame.event.Event(pygame.KEYDOWN, key=pygame.K_SPACE, unicode=" "))
    elif st == "reel":
        if a.tension < a.band - 0.01:
            if not a.holding:
                play.handle_event(pygame.event.Event(pygame.KEYDOWN, key=pygame.K_SPACE, unicode=" "))
        elif a.holding:
            play.handle_event(pygame.event.Event(pygame.KEYUP, key=pygame.K_SPACE, unicode=" "))
    elif st == "wait" and p.can_toke() and p.chill < 5 and t - last_toke > 30:
        play.toke(); last_toke = t
    if f % 30 == 0 and st in ("idle", "wait"):
        maybe_travel()
        buy_gear()
        jar = best_jar()
        if p.pts >= jar["pts"] and p.weed_count(jar["id"]) < 2:
            p.buy_weed(jar["id"])
            weed_bought[jar["id"]] = weed_bought.get(jar["id"], 0) + 1
    play.update(dt)
    if t >= next_bucket:
        log.append("[%4.0fs] %s | species %d/%d cash $%d pts %d cooler %d/%d weed %s"
                   % (t, p.spot_data["name"], p.found_count, p.total_count, p.cash, p.pts,
                      len(p.bag), p.bag_size, p.weed))
        next_bucket += BUCKET

print("\n".join(log))
print("\nFINAL: %.0f min sim, trips to Sal %d, species %d/%d, cash $%d, pts %d"
      % (MINUTES, trips, p.found_count, p.total_count, p.cash, p.pts))
print("spots:", [data.SPOTS[i]["name"] for i in sorted(p.unlocked)])
print("gear:", p.rod, p.line, p.lure, p.cooler)
print("weed bought:", weed_bought, "stash", p.weed, "loaded", p.strain)
missing = [s["name"] for s in data.SPECIES if not p.caught(s["id"])]
print("missing:", missing)
pygame.quit()
