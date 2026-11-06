"""Developer console: backtick to open, type commands, Enter to run."""
from __future__ import annotations

import random

import pygame

from . import config as C
from . import data
from . import ui
from .util import font

GREEN = (150, 240, 160)
DIM = (110, 170, 120)
WARN = (250, 210, 120)
ERROR = (250, 120, 110)
WHITE = (222, 240, 224)

HELP = [
    ("help", "this list"),
    ("cash <n> / addcash <n>", "set or add money"),
    ("unlock all|spots|gear|jars", "open up the world"),
    ("weed <strain|all> <n>", "add grams of weed"),
    ("species all|clear", "fill or wipe the journal"),
    ("legendaries", "unlock the four Legends tab fish"),
    ("xp <n>", "add experience points"),
    ("bag fill|clear", "stock or empty the cooler"),
    ("cooler small|medium|huge", "swap the cooler"),
    ("goto <1-4|name>", "teleport to a water"),
    ("time <0-24> / dawn|day|dusk|night", "set the clock"),
    ("chill <0-100> / sober / high", "set the high meter"),
    ("toke", "smoke a gram"),
    ("instacatch on|off", "instant reel wins"),
    ("nolinesnap on|off", "the line never breaks"),
    ("debug on|off", "state overlay"),
    ("wire on|off", "wireframe world"),
    ("save / reset / quit", "session control"),
]


def _flag(app, key, value):
    app.debug[key] = value


class Console:
    def __init__(self, app):
        self.app = app
        self.open = False
        self.buf = ""
        self.log = []
        self.history = []
        self.hist = -1
        self.blink = 0.0
        self.commands = {}
        self._register()

    # ------------------------------------------------------------- registry
    def _register(self):
        r = self.commands
        r["help"] = (self.c_help, "  ".join(h[0] for h in HELP[:6]))
        r["?"] = r["help"]
        r["cash"] = (self.c_cash, "cash <n>")
        r["money"] = r["cash"]
        r["addcash"] = (self.c_addcash, "addcash <n>")
        r["unlock"] = (self.c_unlock, "unlock all|spots|gear|jars")
        r["weed"] = (self.c_weed, "weed <strain|all> <grams>")
        r["species"] = (self.c_species, "species all|clear")
        r["bag"] = (self.c_bag, "bag fill|clear")
        r["cooler"] = (self.c_cooler, "cooler small|medium|huge")
        r["goto"] = (self.c_goto, "goto <1-4|name>")
        r["time"] = (self.c_time, "time <0-24>")
        r["dawn"] = (lambda a, args, c: self._time(a, 5.8), "")
        r["day"] = (lambda a, args, c: self._time(a, 13.0), "")
        r["dusk"] = (lambda a, args, c: self._time(a, 19.2), "")
        r["night"] = (lambda a, args, c: self._time(a, 22.5), "")
        r["chill"] = (self.c_chill, "chill <0-100>")
        r["sober"] = (lambda a, args, c: self._chill(a, 0), "")
        r["high"] = (lambda a, args, c: self._chill(a, 92), "")
        r["toke"] = (self.c_toke, "toke")
        r["xp"] = (self.c_xp, "xp <n>  add experience")
        r["stoke"] = (self.c_stoke, "stoke [fish|smoke|vibes]  spend or inspect")
        r["hidden"] = (self.c_hidden, "hidden  force this water's secret fish")
        r["legendaries"] = (self.c_legends, "legendaries  unlock the Legends tab fish")
        r["legends"] = r["legendaries"]
        r["instacatch"] = (self.c_insta, "instacatch on|off")
        r["nolinesnap"] = (self.c_nosnap, "nolinesnap on|off")
        r["debug"] = (self.c_debug, "debug on|off")
        r["wire"] = (self.c_wire, "wire on|off")
        r["save"] = (self.c_save, "save")
        r["reset"] = (self.c_reset, "reset (wipes the save)")
        r["quit"] = (self.c_quit, "quit")

    # -------------------------------------------------------------- output
    def out(self, text, color=GREEN):
        for line in str(text).split("\n"):
            self.log.append((line, color))
        self.log = self.log[-9:]

    def toggle(self):
        self.open = not self.open
        self.buf = ""
        self.hist = -1
        self.blink = 0.0
        if self.open:
            self.out("dev console - 'help' for commands", WARN)

    # --------------------------------------------------------------- input
    def handle_event(self, event):
        if not self.open:
            return False
        if event.type == pygame.KEYDOWN:
            if event.key in (pygame.K_ESCAPE, pygame.K_BACKQUOTE):
                self.open = False
            elif event.key == pygame.K_RETURN:
                self.submit()
            elif event.key == pygame.K_BACKSPACE:
                self.buf = self.buf[:-1]
            elif event.key == pygame.K_TAB:
                self.complete()
            elif event.key == pygame.K_UP:
                self.recall(-1)
            elif event.key == pygame.K_DOWN:
                self.recall(1)
            elif event.unicode and event.unicode.isprintable():
                if len(self.buf) < 64:
                    self.buf += event.unicode
            return True
        return True          # swallow everything while open

    def complete(self):
        part = self.buf.split(" ")[0] if self.buf else ""
        matches = sorted(k for k in self.commands if k.startswith(part))
        if len(matches) == 1:
            self.buf = matches[0] + " "
        elif matches:
            self.out("  ".join(matches), DIM)

    def recall(self, step):
        if not self.history:
            return
        self.hist = max(-1, min(len(self.history) - 1, self.hist - step)) \
            if step < 0 else max(-1, self.hist + step)
        self.buf = self.history[self.hist] if self.hist >= 0 else ""

    def submit(self):
        line = self.buf.strip()
        self.buf = ""
        if not line:
            return
        self.history.append(line)
        self.hist = -1
        self.out("> " + line, WHITE)
        self.run(line)

    def run(self, line):
        parts = line.split()
        cmd = parts[0].lower()
        args = parts[1:]
        entry = self.commands.get(cmd)
        if not entry:
            self.out("unknown command: %s" % cmd, ERROR)
            return
        try:
            entry[0](self.app, args, self)
        except Exception as exc:      # keep the console alive whatever happens
            self.out("%s: %s" % (type(exc).__name__, exc), ERROR)

    # ------------------------------------------------------------ commands
    def c_help(self, app, args, c):
        for syntax, _ in HELP:
            c.out(syntax, DIM)
        c.out("strains: " + ", ".join(s["id"] for s in data.STRAINS), DIM)

    def c_cash(self, app, args, c):
        if not args:
            c.out("cash = %d" % app.player.cash)
            return
        app.player.cash = max(0, int(float(args[0])))
        app.save()
        c.out("cash = %d" % app.player.cash, WARN)

    def c_addcash(self, app, args, c):
        app.player.cash = max(0, app.player.cash + int(float(args[0] or 1000)))
        app.save()
        c.out("cash = %d" % app.player.cash, WARN)

    def c_pts(self, app, args, c):
        if not args:
            c.out("VP = %d" % app.player.pts)
            return
        app.player.pts = max(0, int(float(args[0])))
        app.save()
        c.out("VP = %d" % app.player.pts, WARN)

    def c_addpts(self, app, args, c):
        app.player.pts = max(0, app.player.pts + int(float(args[0] or 100)))
        app.save()
        c.out("VP = %d" % app.player.pts, WARN)

    def c_unlock(self, app, args, c):
        what = (args[0] if args else "all").lower()
        p = app.player
        if what in ("all", "spots"):
            for i in range(len(data.SPOTS)):
                p.unlocked.add(i)
            c.out("all waters unlocked", WARN)
        if what in ("all", "gear"):
            p.rods |= {"bamboo", "fiber", "carbon", "moon"}
            p.lines |= {"light", "braid", "ghost"}
            p.lures |= {"worm", "spoon", "fly", "jig", "gold"}
            p.coolers |= {"small", "medium", "huge"}
            p.rod, p.line, p.lure, p.cooler = "moon", "ghost", "gold", "huge"
            c.out("all gear owned + equipped", WARN)
        if what in ("all", "jars"):
            p.sal_tier = len(C.RARITY_ORDER) - 1
            c.out("every jar is open", WARN)
        app.save()

    def c_weed(self, app, args, c):
        p = app.player
        who = (args[0] if args else "all").lower()
        n = int(float(args[1])) if len(args) > 1 else 5
        ids = [s["id"] for s in data.STRAINS] if who == "all" else [who]
        for sid in ids:
            if sid not in data.STRAINS_BY_ID:
                c.out("no such strain: %s" % sid, ERROR)
                return
            p.weed[sid] = p.weed_count(sid) + n
        p.strain = ids[-1]
        app.save()
        c.out("weed: " + ", ".join("%s x%d" % (i, p.weed_count(i)) for i in ids), WARN)

    def c_species(self, app, args, c):
        what = (args[0] if args else "all").lower()
        p = app.player
        if what == "clear":
            p.species = {}
            c.out("journal wiped", WARN)
        else:
            for sp in data.SPECIES:
                if sp["id"] not in p.species:
                    kg = sp["kg"][1] * 0.6
                    p.species[sp["id"]] = {"count": 1, "best": kg,
                                           "best_len": data.length_cm(kg, sp["shape"])}
            c.out("journal filled: %d/%d" % (p.found_count, p.total_count), WARN)
        app.save()

    def c_bag(self, app, args, c):
        what = (args[0] if args else "fill").lower()
        p = app.player
        if what == "clear":
            p.bag = []
            c.out("cooler emptied", WARN)
        else:
            rng = random.Random(7)
            guard = 0
            while not p.bag_full and guard < 64:
                guard += 1
                sp = rng.choice(data.fish_for_spot(p.spot))
                kg = rng.uniform(*sp["kg"])
                p.add_catch(sp["id"], kg, data.length_cm(kg, sp["shape"]))
            c.out("cooler %d/%d" % (len(p.bag), p.bag_size), WARN)
        app.save()

    def c_cooler(self, app, args, c):
        p = app.player
        gid = (args[0] if args else "huge").lower()
        if gid not in data.COOLERS_BY_ID:
            c.out("coolers: small, medium, huge", ERROR)
            return
        p.coolers.add(gid)
        p.cooler = gid
        app.save()
        c.out("cooler: %s (%d slots)" % (gid, p.bag_size), WARN)

    def _time(self, app, hour):
        app.player.hour = hour % 24.0
        self.out("time %.1f" % app.player.hour, WARN)

    def c_time(self, app, args, c):
        if not args:
            c.out("time = %.1f" % app.player.hour)
            return
        self._time(app, float(args[0]))

    def _chill(self, app, value):
        app.player.chill = max(0.0, min(100.0, float(value)))
        self.out("chill %.0f" % app.player.chill, WARN)

    def c_chill(self, app, args, c):
        if not args:
            c.out("chill = %.0f" % app.player.chill)
            return
        self._chill(app, float(args[0]))

    def c_toke(self, app, args, c):
        res = app.player.toke()
        c.out("toke: %s (chill %.0f)" % (res, app.player.chill), WARN)

    def c_xp(self, app, args, c):
        if not args:
            c.out("level %d  xp %d  next in %d" % (app.player.level, app.player.xp,
                                                   app.player.xp_to_next()), WARN)
            return
        try:
            n = int(args[0])
        except ValueError:
            c.out("usage: xp <n>", ERROR)
            return
        gained = app.player.add_xp(n)
        app.save()
        c.out("level %d  xp %d%s" % (app.player.level, app.player.xp,
                                     "  (+%d stoke)" % gained if gained else ""), WARN)

    def c_stoke(self, app, args, c):
        p = app.player
        if not args:
            c.out("level %d  xp %d  points %d  fish %d smoke %d vibes %d"
                  % (p.level, p.xp, p.stoke_pts, p.stoke["fish"],
                     p.stoke["smoke"], p.stoke["vibes"]), WARN)
            return
        ok, msg = p.spend_stoke(args[0].strip().lower())
        app.save()
        c.out(msg, WARN if ok else ERROR)

    def c_legends(self, app, args, c):
        """Unlock the Legends tab only - not the legendary-rank area fish."""
        p = app.player
        added = 0
        for sp in data.HIDDEN_FISH:
            if sp["id"] in p.species:
                continue
            kg = sp["kg"][1] * 0.6
            p.species[sp["id"]] = {"count": 1, "best": kg,
                                   "best_len": data.length_cm(kg, sp["shape"])}
            added += 1
        app.save()
        have = sum(1 for sp in data.HIDDEN_FISH if sp["id"] in p.species)
        c.out("legends %d/4 (+%d)  -  area legendaries untouched" % (have, added), WARN)

    def c_hidden(self, app, args, c):
        app.debug["force_hidden"] = True
        c.out("next catch in this water is its hidden fish", WARN)

    def c_goto(self, app, args, c):
        if not args:
            c.out("waters: " + " | ".join("%d %s" % (i + 1, s["name"])
                                          for i, s in enumerate(data.SPOTS)), DIM)
            return
        raw = args[0].lower()
        idx = None
        if raw.isdigit():
            idx = int(raw) - 1
        else:
            for i, sp in enumerate(data.SPOTS):
                if sp["name"].lower().startswith(raw):
                    idx = i
        if idx is None or not (0 <= idx < len(data.SPOTS)):
            c.out("no such water: %s" % raw, ERROR)
            return
        app.player.unlocked.add(idx)
        app.new_world(idx)
        app.save()
        c.out("travelled to " + data.SPOTS[idx]["name"], WARN)
        from .screens.play import PlayScreen
        if not isinstance(app.stack[-1], PlayScreen):
            app.stack[-1] = PlayScreen(app)

    def _onoff(self, app, args, key, label):
        val = (args[0].lower() if args else "on") not in ("off", "0", "false")
        app.debug[key] = val
        self.out("%s %s" % (label, "on" if val else "off"), WARN)

    def c_insta(self, app, args, c):
        self._onoff(app, args, "instacatch", "instacatch")

    def c_nosnap(self, app, args, c):
        self._onoff(app, args, "nolinesnap", "nolinesnap")

    def c_debug(self, app, args, c):
        self._onoff(app, args, "show", "debug overlay")

    def c_wire(self, app, args, c):
        val = (args[0].lower() if args else "on") not in ("off", "0", "false")
        app.debug["wire"] = val
        try:
            app.world.scene.wireframe = val
        except AttributeError:
            pass
        self.out("wireframe %s" % ("on" if val else "off"), WARN)

    def c_save(self, app, args, c):
        app.save()
        c.out("saved", WARN)

    def c_reset(self, app, args, c):
        app.player.delete_save()
        from .player import Player
        app.player = Player()
        app.new_world(0)
        app.save()
        c.out("save wiped, fresh player", WARN)

    def c_quit(self, app, args, c):
        app.quit()

    # ---------------------------------------------------------------- draw
    def update(self, dt):
        self.blink += dt

    def _wrap(self, text, font_obj, maxw):
        """Greedy word wrap so long console lines stay on screen."""
        words = text.split(" ")
        lines, cur = [], ""
        for w in words:
            trial = w if not cur else cur + " " + w
            if font_obj.size(trial)[0] <= maxw:
                cur = trial
                continue
            if cur:
                lines.append(cur)
            while font_obj.size(w)[0] > maxw:
                cut = len(w)
                while cut > 1 and font_obj.size(w[:cut])[0] > maxw:
                    cut -= 1
                lines.append(w[:cut])
                w = w[cut:]
            cur = w
        if cur:
            lines.append(cur)
        return lines or [""]

    def draw(self, surf):
        if not self.open:
            return
        h = 410
        top = C.H - h
        panel = pygame.Surface((C.W - 16, h - 10), pygame.SRCALPHA)
        pygame.draw.rect(panel, (8, 14, 10, 242), (0, 0, C.W - 16, h - 10),
                         border_radius=12)
        pygame.draw.rect(panel, (58, 122, 74, 255), (0, 0, C.W - 16, h - 10),
                         width=2, border_radius=12)
        surf.blit(panel, (8, top + 6))

        mono = font(15, False, "mono")
        small = font(12, False, "mono")
        ui.txt(surf, "DEV CONSOLE", (24, top + 10), 13, True, (110, 200, 130),
               family="mono", shadow=None)
        ui.txt(surf, "backtick / ESC closes   TAB completes   UP recalls",
               (C.W - 24, top + 12), 12, False, (90, 140, 100), anchor="topright",
               family="mono", shadow=None)

        y = top + 34
        maxw = C.W - 56
        for line, color in self.log:
            for part in self._wrap(line, mono, maxw):
                ui.txt(surf, part, (24, y), 15, False, color, family="mono",
                       shadow=None)
                y += 19

        ui.txt(surf, "help · cash · pts · weed · goto · unlock · species · "
                     "stoke · xp · time · debug",
               (24, C.H - 88), 11, False, (86, 132, 96), family="mono", shadow=None)
        y = C.H - 34
        pygame.draw.line(surf, (40, 80, 50), (20, y - 8), (C.W - 20, y - 8), 1)
        ui.txt(surf, ">", (24, y), 17, True, (120, 220, 140), family="mono",
               shadow=None)
        ui.txt(surf, self.buf, (44, y), 17, False, (226, 240, 228), family="mono",
               shadow=None)
        if int(self.blink * 2) % 2 == 0:
            w = mono.size(self.buf)[0]
            pygame.draw.rect(surf, (140, 240, 160), (46 + w, y + 2, 9, 18))
        del small
