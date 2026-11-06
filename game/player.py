"""Player state: cooler, cash, Vibe Points, weed stash and save/load."""
from __future__ import annotations

import json
import os
import random

from . import data
from .config import (CHILL_DECAY, COUCH_LOCK_TIME, DISCOVERY_BONUS, HOUR_PER_SECOND,
                     RARITY_INDEX,
                     RARITY_POINTS, SAVE_VERSION, TOKE_AMOUNT, TROPHY_MULT,
                     VALUE_CAP, VALUE_EXP, VALUE_K)

import sys as _sys

if getattr(_sys, "frozen", False):          # packaged build: write to user data
    _base = os.path.expanduser("~/Library/Application Support/Hot Rods" \
                               if _sys.platform == "darwin" else
                               os.path.join(os.environ.get("APPDATA", "."), "Hot Rods"))
    try:
        os.makedirs(_base, exist_ok=True)
    except OSError:                       # sandboxed / locked-down machine
        _base = os.path.dirname(_sys.executable)
    SAVE_PATH = os.path.join(_base, "save.json")
else:
    SAVE_PATH = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                             "save.json")


class Player:
    def __init__(self):
        self.cash = 30
        self.rod = "bamboo"
        self.line = "light"
        self.lure = "worm"
        self.rods = {"bamboo"}
        self.lines = {"light"}
        self.lures = {"worm"}
        self.cooler = "small"
        self.coolers = {"small"}

        self.bag = []              # fish waiting in the cooler
        self.pts = 0               # Vibe Points (VP)
        self.weed = {}             # strain id -> grams
        self.strain = "homegrown"  # what is loaded in the pipe
        self.sal_tier = -1         # best fish rarity Sal has ever handled
        self.hidden_sold = set()   # secret fish handed over to Sal
        self.smokers = {"one_hitter"}
        self.smoker = "one_hitter"
        self.concentrate = {}      # strain id -> grams of concentrate
        self.concentrated = False  # the current high came from concentrate
        self.dab_mode = False      # smoke concentrate instead of flower
        self.guaranteed = False    # next cast is a legendary
        self.pov = "third"
        self.ice = data.ICE_MINUTES  # starts with one bag in the cooler
        self.xp = 0
        self.stoke = {"fish": 0, "smoke": 0, "vibes": 0}
        self.stoke_pts = 0

        self.spot = 0
        self.unlocked = {0}
        self.species = {}          # id -> {"count", "best", "best_len"}
        self.chill = 0.0
        self.couch = 0.0
        self.hour = 8.0
        self.played = 0.0
        self.casts = 0
        self.catches = 0
        self.tokes = 0
        self.earned = 0
        self.sold = 0
        self.best_catch = None
        self.seen_intro = False
        self.muted = False

    # ------------------------------------------------------------- derived
    @property
    def found_count(self):
        return sum(1 for k in self.species
                   if k in data.SPECIES_BY_ID or k in data.HIDDEN_BY_ID
                   or k in data.SHINY_BY_ID)

    @property
    def legend_count(self):
        return sum(1 for k in self.species if k in data.HIDDEN_BY_ID)

    @property
    def total_count(self):
        return len(data.SPECIES) + len(data.HIDDEN_FISH) + len(data.SHINY_FISH)

    @property
    def high(self):
        return max(0.0, min(1.0, self.chill / 100.0))

    @property
    def is_high(self):
        return self.chill > 10.0

    @property
    def rod_data(self):
        return data.RODS_BY_ID[self.rod]

    @property
    def line_data(self):
        return data.LINES_BY_ID[self.line]

    @property
    def lure_data(self):
        return data.LURES_BY_ID[self.lure]

    LEVEL_XP = (0, 120, 320, 620, 1050, 1650, 2450, 3500, 4850, 6500)

    @property
    def level(self):
        lv = 1
        for i, need in enumerate(self.LEVEL_XP):
            if self.xp >= need:
                lv = i + 1
        return lv

    def xp_to_next(self):
        nxt = self.LEVEL_XP[min(self.level, len(self.LEVEL_XP) - 1)]
        return max(0, nxt - self.xp)

    def add_xp(self, amount):
        before = self.level
        self.xp += int(amount)
        gained = self.level - before
        if gained > 0:
            self.stoke_pts += gained
        return gained

    def spend_stoke(self, lane):
        if lane not in self.stoke:
            return False, "lanes: fish, smoke, vibes"
        if self.stoke_pts <= 0:
            return False, "No stoke points - catch and sell more fish"
        if self.stoke[lane] >= 8:
            return False, "That lane is maxed"
        self.stoke[lane] += 1
        self.stoke_pts -= 1
        return True, "%s stoke %d" % (lane, self.stoke[lane])

    @property
    def cooler_data(self):
        return data.COOLERS_BY_ID[self.cooler]

    @property
    def ice_cap(self):
        return self.cooler_data["ice"] * data.ICE_MINUTES

    def buy_ice(self):
        if self.ice >= self.ice_cap - 0.01:
            return False, "Cooler is already packed with ice"
        if self.cash < data.ICE_COST:
            return False, "Not enough cash"
        self.cash -= data.ICE_COST
        self.ice = min(self.ice_cap, self.ice + data.ICE_MINUTES)
        return True, "+%d min of ice" % int(data.ICE_MINUTES)

    @property
    def smoker_data(self):
        return data.SMOKERS_BY_ID.get(self.smoker, data.SMOKERS[0])

    @property
    def strain_data(self):
        return data.STRAINS_BY_ID[self.strain]

    @property
    def spot_data(self):
        return data.SPOTS[self.spot]

    # ------------------------------------------------------------- cooler
    LEVEL_XP = (0, 120, 320, 620, 1050, 1650, 2450, 3500, 4850, 6500)

    @property
    def level(self):
        lv = 1
        for i, need in enumerate(self.LEVEL_XP):
            if self.xp >= need:
                lv = i + 1
        return lv

    def xp_to_next(self):
        nxt = self.LEVEL_XP[min(self.level, len(self.LEVEL_XP) - 1)]
        return max(0, nxt - self.xp)

    def add_xp(self, amount):
        before = self.level
        self.xp += int(amount)
        gained = self.level - before
        if gained > 0:
            self.stoke_pts += gained
        return gained

    def spend_stoke(self, lane):
        if lane not in self.stoke:
            return False, "lanes: fish, smoke, vibes"
        if self.stoke_pts <= 0:
            return False, "No stoke points - catch and sell more fish"
        if self.stoke[lane] >= 8:
            return False, "That lane is maxed"
        self.stoke[lane] += 1
        self.stoke_pts -= 1
        return True, "%s stoke %d" % (lane, self.stoke[lane])

    @property
    def cooler_data(self):
        return data.COOLERS_BY_ID[self.cooler]

    @property
    def bag_size(self):
        return self.cooler_data["slots"]

    @property
    def bag_full(self):
        return len(self.bag) >= self.bag_size

    @property
    def bag_points(self):
        return sum(int(round(RARITY_POINTS[data.species(f["id"])["rarity"]]
                              * data.pay_mult(f["id"]))) for f in self.bag)

    @property
    def bag_value(self):
        return sum(f["value"] for f in self.bag)

    def bag_best_tier(self):
        best = -1
        for f in self.bag:
            best = max(best, RARITY_INDEX[data.species(f["id"])["rarity"]])
        return best

    def unlocked_tier(self):
        return max(self.sal_tier, self.bag_best_tier())

    def hidden_unlocked(self):
        return {s["id"] for s in data.STRAINS
                if s.get("hidden") and s.get("unlock_fish") in self.hidden_sold}

    def jar_available(self, sid):
        st = data.STRAINS_BY_ID[sid]
        if st.get("hidden"):
            return sid in self.hidden_unlocked()
        if st["area"] not in self.unlocked:
            return False
        return st["tier"] <= self.unlocked_tier()

    def records(self, sid):
        return self.species.get(sid)

    def caught(self, sid):
        return sid in self.species

    # ------------------------------------------------------------- travel
    def spot_found(self, index):
        return sum(1 for sp in data.fish_for_spot(index) if sp["id"] in self.species)

    def can_travel(self, index):
        sp = data.SPOTS[index]
        if index in self.unlocked:
            return True, ""
        if index > 0:
            prev = index - 1
            need = len(data.fish_for_spot(prev))
            got = self.spot_found(prev)
            if got < need:
                return False, "Catch all %d in %s (%d/%d)" % (
                    need, data.SPOTS[prev]["name"], got, need)
        if self.cash < sp["need_cash"]:
            return False, "Need $%d to get there" % sp["need_cash"]
        return True, ""

    def travel(self, index):
        ok, why = self.can_travel(index)
        if not ok:
            return False, why
        if index not in self.unlocked:
            self.cash -= data.SPOTS[index]["need_cash"]
            self.unlocked.add(index)
        self.spot = index
        return True, ""

    # ------------------------------------------------------------- update
    def update(self, dt):
        self.played += dt
        self.hour = (self.hour + dt * HOUR_PER_SECOND) % 24.0
        if self.ice > 0:
            self.ice = max(0.0, self.ice - dt / 60.0)
        elif self.bag:
            for f in self.bag:
                f.setdefault("fresh", 1.0)
                if f["fresh"] > data.SPOIL_FLOOR:
                    f["fresh"] = max(data.SPOIL_FLOOR,
                                     f["fresh"] - data.SPOIL_RATE * dt / 60.0)
                    f["value"] = max(1, int(round(
                        f.get("base_value", f["value"]) * f["fresh"])))
        if self.chill > 0:
            mult = (self.strain_data["duration"] * self.smoker_data["dur"]
                    * (1.0 + 0.05 * self.stoke["vibes"]))
            if self.concentrated:
                mult *= data.CONCENTRATE_MULT
            decay = CHILL_DECAY / max(0.3, mult)
            self.chill = max(0.0, self.chill - decay * dt)
        if self.couch > 0:
            self.couch = max(0.0, self.couch - dt)
            if self.chill <= 50.0:
                self.couch = 0.0

    # --------------------------------------------------------------- weed
    def weed_count(self, sid=None):
        return int(self.weed.get(sid or self.strain, 0))

    def any_weed(self):
        return any(v > 0 for v in self.weed.values())

    def can_toke(self):
        return self.weed_count() > 0

    def toke(self):
        """Smoke one gram of the loaded strain. Returns ok / empty / couch."""
        if self.couch > 0:
            return "couch"
        conc = self.concentrate.get(self.strain, 0)
        if self.smoker_data.get("conc"):
            # a dab rig only smokes concentrate
            if not self.dab_mode or conc <= 0:
                return "need_dab"
        elif self.dab_mode and conc > 0:
            # concentrate can only go in a rig
            return "need_rig"
        if self.dab_mode and conc > 0 and self.smoker_data.get("conc"):
            self.concentrate[self.strain] = conc - 1
            self.concentrated = True
            self.tokes += 1
            self.chill = min(100.0, self.chill + TOKE_AMOUNT
                             * self.smoker_data.get("fill", 1.0)
                             * (1.0 + 0.06 * self.stoke["smoke"]))
            return "ok"
        if self.weed_count() <= 0:
            if not self.any_weed():
                return "empty"
            # the loaded jar is empty but the stash isn't: grab the fullest
            self.strain = max(self.weed, key=lambda k: self.weed[k])
        self.weed[self.strain] = self.weed_count() - 1
        if (self.smoker == "backwoods" and self.strain == "pond_haze"
                and self.chill >= 99.9):
            self.guaranteed = True
        self.concentrated = False
        self.tokes += 1
        over = self.chill >= 78.0
        self.chill = min(100.0, self.chill + TOKE_AMOUNT * self.smoker_data.get("fill", 1.0)
                             * (1.0 + 0.06 * self.stoke["smoke"]))
        if over:
            self.couch = COUCH_LOCK_TIME
            self.chill = 100.0
            return "couch"
        if (self.smoker == "backwoods" and self.strain == "pond_haze"
                and self.chill >= 99.9):
            self.guaranteed = True
        return "ok"

    def set_strain(self, sid):
        if sid not in data.STRAINS_BY_ID:
            return False, "Unknown strain"
        if self.weed_count(sid) <= 0:
            return False, "You don't have any"
        self.strain = sid
        return True, "Loaded"

    def legendaries_caught(self):
        return sum(1 for sp in data.SPECIES
                   if sp["rarity"] == "legendary" and sp["id"] in self.species)

    def legends_complete(self):
        total = sum(1 for sp in data.SPECIES if sp["rarity"] == "legendary")
        return self.legendaries_caught() >= total

    def press_strain(self, sid):
        if "hot_press" not in self.smokers:
            return False, "You need a hot press"
        if self.weed_count(sid) < 4:
            return False, "Need 4 g of that strain"
        self.weed[sid] = self.weed_count(sid) - 4
        self.concentrate[sid] = self.concentrate.get(sid, 0) + 1
        return True, "+1 g of %s concentrate" % data.STRAINS_BY_ID[sid]["name"]

    def press_weed(self):
        return self.press_strain(self.strain)

    def buy_weed(self, sid):
        st = data.STRAINS_BY_ID.get(sid)
        if not st:
            return False, "Unknown strain"
        if not self.jar_available(sid):
            return False, "Sal wants a better fish first"
        if self.pts < st["pts"]:
            return False, "Not enough VP"
        self.pts -= st["pts"]
        self.weed[sid] = self.weed_count(sid) + 1
        self.strain = sid
        return True, "+1g " + st["name"]

    # ------------------------------------------------------------ fishing
    def add_catch(self, sid, kg, cm):
        """Record a catch and put it in the cooler."""
        sp = data.species(sid)
        # realistic market value at this weight, softened and capped per rarity
        raw = max(0.05, kg * sp["usd_kg"] * TROPHY_MULT[sp["rarity"]])
        value = int(round(VALUE_K * raw ** VALUE_EXP))
        value = min(value, VALUE_CAP[sp["rarity"]])
        if self.is_high:
            value = int(round(value * self.strain_data["cash"]))
        value = max(1, int(round(value * data.pay_mult(sid))))

        new = sid not in self.species
        rec = self.species.get(sid)
        if rec is None:
            self.species[sid] = {"count": 1, "best": kg, "best_len": cm}
        else:
            rec["count"] += 1
            if kg > rec["best"]:
                rec["best"] = kg
                rec["best_len"] = cm

        self.add_xp(6 + RARITY_INDEX[sp["rarity"]] * 4 + (12 if new else 0))
        bonus = int(round(value * DISCOVERY_BONUS)) if new else 0
        self.cash += bonus
        self.earned += bonus
        self.catches += 1
        if self.best_catch is None or kg > self.best_catch[0]:
            self.best_catch = (kg, sid)

        overflow = self.bag_full
        if overflow:
            # The cooler was already full: Sal's runner takes it off your hands.
            self.cash += value
            self.earned += value
        else:
            self.bag.append({"id": sid, "kg": kg, "cm": cm, "value": value,
                             "base_value": value, "fresh": 1.0})
        return {"new": new, "value": value, "bonus": bonus,
                "kept": len(self.bag), "capacity": self.bag_size, "overflow": overflow}

    # -------------------------------------------------------------- dealer
    def sell_fish(self, index):
        if not (0 <= index < len(self.bag)):
            return 0
        entry = self.bag.pop(index)
        sp = data.species(entry["id"])
        if entry["id"] in data.HIDDEN_BY_ID:
            self.hidden_sold.add(entry["id"])
        self.sal_tier = max(self.sal_tier, RARITY_INDEX[sp["rarity"]])
        self.cash += entry["value"]
        self.earned += entry["value"]
        self.sold += 1
        return entry["value"]

    def trade_fish(self, index):
        if not (0 <= index < len(self.bag)):
            return 0
        entry = self.bag.pop(index)
        sp = data.species(entry["id"])
        if entry["id"] in data.HIDDEN_BY_ID:
            self.hidden_sold.add(entry["id"])
        pts = int(round(RARITY_POINTS[sp["rarity"]] * data.pay_mult(entry["id"])))
        self.pts += pts
        self.sal_tier = max(self.sal_tier, RARITY_INDEX[sp["rarity"]])
        self.sold += 1
        return pts

    def sell_all(self):
        total = self.bag_value
        n = len(self.bag)
        bag_snapshot = list(self.bag)
        if n:
            self.earned += total
        self.hidden_sold |= {f["id"] for f in bag_snapshot if f["id"] in data.HIDDEN_BY_ID}
        for f in bag_snapshot:
            sp = data.species(f["id"])
            self.sal_tier = max(self.sal_tier, RARITY_INDEX[sp["rarity"]])
        self.sold += n
        self.bag = []
        self.cash += total
        return n, total

    def trade_all(self):
        pts = self.bag_points
        n = len(self.bag)
        for f in self.bag:
            sp = data.species(f["id"])
            if f["id"] in data.HIDDEN_BY_ID or f["id"] in data.SHINY_BY_ID:
                self.hidden_sold.add(f["id"])
            self.sal_tier = max(self.sal_tier, RARITY_INDEX[sp["rarity"]])
        self.sold += n
        self.bag = []
        self.pts += pts
        return n, pts

    # ------------------------------------------------------------ shopping
    def _owned(self, kind):
        return {"rod": self.rods, "line": self.lines, "lure": self.lures}[kind]

    def _table(self, kind):
        return {"rod": data.RODS, "line": data.LINES, "lure": data.LURES}[kind]

    def _equipped(self, kind):
        return {"rod": self.rod, "line": self.line, "lure": self.lure}[kind]

    def _set_equipped(self, kind, gid):
        if kind == "rod":
            self.rod = gid
        elif kind == "line":
            self.line = gid
        else:
            self.lure = gid

    def item_state(self, kind, gid):
        if kind == "smoker":
            if gid == self.smoker:
                return "equipped"
            return "owned" if gid in self.smokers else "buy"
        if kind == "cooler":
            if gid == self.cooler:
                return "equipped"
            return "owned" if gid in self.coolers else "buy"
        owned = self._owned(kind)
        if gid in owned:
            return "equipped" if self._equipped(kind) == gid else "owned"
        return "buy"

    def gear_locked(self, gid):
        need = data.GEAR_SPOT.get(gid)
        return need is not None and need not in self.unlocked

    def buy_or_equip(self, kind, gid):
        if self.gear_locked(gid):
            need = data.GEAR_SPOT[gid]
            return False, "Unlock %s first" % data.SPOTS[need]["name"]
        if kind == "smoker":
            g = data.SMOKERS_BY_ID.get(gid)
            if not g:
                return False, "Unknown item"
            if gid in ("dab_rig", "hot_press") and not self.legends_complete():
                return False, "Catch every legendary fish first (%d/%d)" % (
                    self.legendaries_caught(), 4)
            if gid in self.smokers:
                if self.smoker == gid:
                    return False, "Already in use"
                self.smoker = gid
                return True, "Equipped"
            if self.cash < g["cost"]:
                return False, "Not enough cash"
            self.cash -= g["cost"]
            self.smokers.add(gid)
            self.smoker = gid
            return True, "Bought"
        if kind == "cooler":
            return self._buy_cooler(gid)
        table = {g["id"]: g for g in self._table(kind)}
        g = table.get(gid)
        if not g:
            return False, "Unknown item"
        if gid in self._owned(kind):
            if self._equipped(kind) != gid:
                self._set_equipped(kind, gid)
                return True, "Equipped"
            return False, "Already equipped"
        if self.cash < g["cost"]:
            return False, "Not enough cash"
        self.cash -= g["cost"]
        self._owned(kind).add(gid)
        self._set_equipped(kind, gid)
        return True, "Bought"

    def _buy_cooler(self, gid):
        c = data.COOLERS_BY_ID.get(gid)
        if not c:
            return False, "Unknown cooler"
        if gid in self.coolers:
            if gid == self.cooler:
                return False, "Already in use"
            if len(self.bag) > c["slots"]:
                return False, "Too many fish for that one"
            self.cooler = gid
            return True, "Equipped"
        if self.cash < c["cost"]:
            return False, "Not enough cash"
        self.cash -= c["cost"]
        self.coolers.add(gid)
        self.cooler = gid
        return True, "Bought"

    # --------------------------------------------------------------- save
    def to_dict(self):
        return {
            "version": SAVE_VERSION,
            "cash": self.cash,
            "rod": self.rod,
            "line": self.line,
            "lure": self.lure,
            "cooler": self.cooler,
            "rods": sorted(self.rods),
            "lines": sorted(self.lines),
            "lures": sorted(self.lures),
            "coolers": sorted(self.coolers),
            "bag": self.bag,
            "pts": self.pts,
            "weed": self.weed,
            "strain": self.strain,
            "sal_tier": self.sal_tier,
            "hidden_sold": sorted(self.hidden_sold),
            "smokers": sorted(self.smokers),
            "smoker": self.smoker,
            "concentrate": self.concentrate,
            "dab_mode": self.dab_mode,
            "guaranteed": self.guaranteed,
            "pov": self.pov,
            "ice": self.ice,
            "xp": self.xp,
            "stoke": self.stoke,
            "stoke_pts": self.stoke_pts,
            "spot": self.spot,
            "unlocked": sorted(self.unlocked),
            "species": self.species,
            "hour": self.hour,
            "played": self.played,
            "casts": self.casts,
            "catches": self.catches,
            "tokes": self.tokes,
            "earned": self.earned,
            "sold": self.sold,
            "best_catch": list(self.best_catch) if self.best_catch else None,
            "seen_intro": self.seen_intro,
            "muted": self.muted,
        }

    def apply_dict(self, d):
        self.cash = float(d.get("cash", self.cash))
        self.rod = d.get("rod", self.rod)
        self.line = d.get("line", self.line)
        self.lure = d.get("lure", self.lure)
        self.cooler = d.get("cooler", self.cooler)
        self.rods = set(d.get("rods", ["bamboo"]))
        self.lines = set(d.get("lines", ["light"]))
        self.lures = set(d.get("lures", ["worm"]))
        self.coolers = set(d.get("coolers", ["small"]))
        self.spot = int(d.get("spot", 0))
        self.unlocked = set(int(i) for i in d.get("unlocked", [0]))
        self.unlocked.add(0)
        self.pts = int(d.get("pts", 0))
        self.sal_tier = int(d.get("sal_tier", -1))
        self.hidden_sold = set(d.get("hidden_sold", []))
        self.smokers = set(d.get("smokers", ["one_hitter"])) | {"one_hitter"}
        self.smoker = d.get("smoker", "one_hitter")
        if self.smoker not in data.SMOKERS_BY_ID:
            self.smoker = "one_hitter"
        self.concentrate = {k: int(v) for k, v in (d.get("concentrate") or {}).items()
                            if k in data.STRAINS_BY_ID}
        self.dab_mode = bool(d.get("dab_mode", False))
        self.guaranteed = bool(d.get("guaranteed", False))
        self.pov = d.get("pov", "third")
        self.ice = float(d.get("ice", data.ICE_MINUTES))
        self.xp = int(d.get("xp", 0))
        self.stoke = {k: int(v) for k, v in (d.get("stoke") or {}).items()
                      if k in ("fish", "smoke", "vibes")} or \
                     {"fish": 0, "smoke": 0, "vibes": 0}
        self.stoke_pts = int(d.get("stoke_pts", 0))   # saves from before ice
                                                            # start with one bag
        for f in self.bag:
            f.setdefault("base_value", f.get("value", 0))
            f.setdefault("fresh", 1.0)
        weed = {}
        for k, v in (d.get("weed") or {}).items():
            if k in data.STRAINS_BY_ID:
                weed[k] = int(v)
        self.weed = {k: v for k, v in weed.items() if v > 0}
        self.strain = d.get("strain", "homegrown")
        if self.strain not in data.STRAINS_BY_ID:
            self.strain = "homegrown"

        sp = {}
        for k, v in (d.get("species") or {}).items():
            if k in data.SPECIES_BY_ID and isinstance(v, dict):
                sp[k] = {"count": int(v.get("count", 1)),
                         "best": float(v.get("best", 0.0)),
                         "best_len": float(v.get("best_len", 0.0))}
        self.species = sp

        bag = []
        for f in d.get("bag") or []:
            if isinstance(f, dict) and f.get("id") in data.SPECIES_BY_ID:
                bag.append({"id": f["id"], "kg": float(f.get("kg", 0.1)),
                            "cm": float(f.get("cm", 10)),
                            "value": int(f.get("value", 1))})
        self.bag = bag

        self.hour = float(d.get("hour", 8.0))
        self.played = float(d.get("played", 0.0))
        self.casts = int(d.get("casts", 0))
        self.catches = int(d.get("catches", 0))
        self.tokes = int(d.get("tokes", 0))
        self.earned = int(d.get("earned", 0))
        self.sold = int(d.get("sold", 0))
        bc = d.get("best_catch")
        self.best_catch = (float(bc[0]), bc[1]) if bc else None
        self.seen_intro = bool(d.get("seen_intro", False))

        # Guard against save edits / removed content.
        self.rods.add("bamboo")
        self.lines.add("light")
        self.lures.add("worm")
        self.coolers.add("small")
        if self.rod not in self.rods:
            self.rod = "bamboo"
        if self.line not in self.lines:
            self.line = "light"
        if self.lure not in self.lures:
            self.lure = "worm"
        if self.cooler not in self.coolers:
            self.cooler = "small"
        if not (0 <= self.spot < len(data.SPOTS)):
            self.spot = 0
        self.bag = self.bag[:self.bag_size]
        self.chill = 0.0
        self.couch = 0.0

    def save(self, path=None):
        path = path or getattr(self, "path", SAVE_PATH)
        try:
            tmp = path + ".tmp"
            with open(tmp, "w", encoding="utf-8") as fh:
                json.dump(self.to_dict(), fh, indent=1)
            os.replace(tmp, path)
            return True
        except OSError:
            return False

    @classmethod
    def load(cls, path=None):
        path = path or SAVE_PATH
        p = cls()
        p.path = path
        if not os.path.exists(path):
            return p
        try:
            with open(path, "r", encoding="utf-8") as fh:
                d = json.load(fh)
            if isinstance(d, dict):
                p.apply_dict(d)
        except (OSError, ValueError, TypeError):
            pass
        return p

    def delete_save(self, path=None):
        path = path or SAVE_PATH
        try:
            if os.path.exists(path):
                os.remove(path)
        except OSError:
            pass


def roll_size(sp, size_mult=1.0):
    """Pick a weight for a species. Most fish skew small."""
    lo, hi = sp["kg"]
    t = random.random() ** 1.7
    kg = lo + (hi - lo) * t
    kg *= size_mult
    kg = max(0.01, min(hi * 1.25, kg))
    return round(kg, 2 if kg >= 1 else 3)
