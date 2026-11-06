"""The fishing state machine and the reel-the-fish minigame."""
from __future__ import annotations

import math
import random

from . import config as C
from . import data
from .config import RARITY_INDEX
from .player import roll_size
from .util import approach, clamp, ease_out_cubic, lerp, smoothstep


class Angler:
    """Owns one cast: charge -> fly -> wait -> bite -> hook -> reel -> result."""

    def __init__(self, player, world, flags=None):
        self.player = player
        self.world = world
        self.flags = flags if flags is not None else {}
        self.state = "idle"
        self.events = []
        self._hard_reset()

    # ------------------------------------------------------------------ setup
    def _hard_reset(self):
        self.state = "idle"
        self.power = 0.0
        self.charge_t = 0.0
        self.charge_dir = 1
        self.depth = 0.5
        self.fly_t = 0.0
        self.fly_from = (0.0, 0.0)
        self.cast_from = -0.55
        self.wait_t = 0.0
        self.bite_t = 0.0
        self.hook_t = 0.0
        self.holding = False
        self.tension = 0.0
        self.progress = 0.0
        self.band = 0.5
        self.band_half = 0.14
        self.band_target = 0.5
        self.over_t = 0.0
        self.wander_t = 0.0
        self.reel_t = 0.0
        self.sweep = 0.0        # marker position 0..1 around the bar
        self.sweep_drawn = 0.0  # exactly where the marker was last drawn
        self.zone_phase = 0.0
        self.zone = 0.5         # centre of the green arc
        self.zone_w = 0.18      # arc width
        self.hits = 0
        self.needed = 3
        self.idle_t = 0.0
        self.splash_t = 0.0
        self.fish = None
        self.kg = 0.0
        self.cm = 0.0
        self.result = None
        self.float_sink = 0.0
        self.nibble_t = 0.0
        self.next_nibble = -1
        self.nibble_count = 0
        self.wait_duration = 3.0
        self.bobber_screen = self.world.bobber_pos(self.depth)[:2]
        self.prompt = ""
        self.twitch = 0.0

    def reset(self):
        self._hard_reset()
        self.world.rod_bend = 0.0
        self.world.rod_angle = -0.55
        self.world.rod_reach = 1.0

    def drain_events(self):
        ev = self.events
        self.events = []
        return ev

    # ----------------------------------------------------------------- input
    def press(self):
        p = self.player
        if self.state == "idle":
            if p.bag_full:
                self.events.append("bagfull")
                return
            if p.couch > 0:
                self.events.append("locked")
                return
            self.state = "charge"
            self.charge_t = 0.0
            self.charge_dir = 1
            self.power = 0.0
            self.events.append("charge")
        elif self.state == "wait":
            self.events.append("cancel")
            self.reset()
        elif self.state == "bite":
            self._hook()
        elif self.state == "reel":
            self.pull()

    def release(self):
        if self.state == "charge":
            self._cast()
        elif self.state == "reel":
            self.holding = False

    def cancel(self):
        if self.state in ("charge", "fly", "wait", "bite"):
            self.reset()

    # ------------------------------------------------------------------ cast
    def _cast(self):
        self.depth = max(0.12, min(1.0, self.power))
        self.player.casts += 1
        self.state = "fly"
        self.fly_t = 0.0
        self.fly_from = self.world.rod_tip()
        self.cast_from = self.world.rod_angle
        self.events.append("cast")

    def _hook(self):
        self.state = "hook"
        self.hook_t = 0.0
        self.events.append("hook")
        self.world.splash_at(self.depth, 18)
        # roll the fish now
        self.fish = self._choose_species()
        self.kg = roll_size(self.fish, self.player.lure_data.get("size", 1.0))
        self.cm = data.length_cm(self.kg, self.fish["shape"])
        diff = self.fish["diff"]
        rod = self.player.rod_data
        self.band_half = max(0.048, 0.125 - 0.070 * diff + rod["tol"] * 0.28)
        self.band = 0.5
        self.band_target = 0.5
        self.tension = 0.42
        self.progress = 0.0
        self.over_t = 0.0
        self.reel_t = 0.0
        self.splash_t = 0.0
        self.wander_t = random.uniform(0.5, 2.0)
        self.twitch = 0.0

    # ------------------------------------------------------------ fish table
    def _choose_species(self):
        p = self.player
        if self.flags.get("force_hidden"):
            hid = [h for h in data.HIDDEN_FISH if h["spot"] == p.spot]
            if hid:
                self.flags["force_hidden"] = False
                return hid[0]
        # a hidden fish is a flat 1-in-4 when its strain is active
        secret = next((h for h in data.HIDDEN_FISH if h["spot"] == p.spot), None)
        if secret is not None and random.random() < 0.25:
            cond = secret.get("cond")
            if not cond or (cond.startswith("strain:")
                            and p.high > 0.10 and p.strain == cond.split(":", 1)[1]):
                return secret
        if getattr(p, "guaranteed", False):
            p.guaranteed = False
            pool = [x for x in data.fish_for_spot(p.spot) if x["rarity"] == "legendary"]
            if pool:
                return random.choice(pool)

        # a 2% roll for this water's shiny, once you dab its hidden strain
        for sh in data.SHINY_FISH:
            if sh["spot"] != p.spot:
                continue
            want = sh["cond"].split(":", 1)[1]
            if (p.is_high and p.concentrated and p.smoker == "dab_rig"
                    and p.strain == want and p.concentrate.get(want, 0) >= 0
                    and random.random() < data.SHINY_ODDS):
                return sh
        pool = list(data.fish_for_spot(p.spot))
        if p.is_high and p.concentrated:
            # concentrate smoke puts the whole lake system on the line
            have = {x["id"] for x in pool}
            pool += [x for x in data.SPECIES if x["id"] not in have]
        night = C.is_night(p.hour)
        high = p.high
        lure = p.lure_data
        strain_luck = 0.0
        if p.is_high:
            strain_luck = p.strain_data["luck"] + p.smoker_data["luck"]
            if p.concentrated:
                strain_luck *= data.CONCENTRATE_MULT
        depth_bonus = 0.55 + 0.75 * self.depth

        weights = []
        total = 0.0
        for sp in pool:
            # sober water only gives up commons; potency opens the rest
            if not p.is_high:
                lo, hi = 0, 0
            else:
                lo = p.strain_data.get("gate_min", 0)
                hi = p.strain_data.get("gate_max", 4)
            if not (lo <= RARITY_INDEX[sp["rarity"]] <= hi):
                continue
            cond = sp.get("cond")
            if cond and cond.startswith("strain:"):
                if not (high > 0.10 and p.strain == cond.split(":", 1)[1]):
                    continue
            if cond == "night" and not night:
                continue
            if cond == "high" and high <= 0.10:
                continue
            if cond == "night+high" and not (night and high > 0.10):
                continue
            r = sp["rarity"]
            w = C.RARITY_WEIGHT[r]
            w *= lure["bias"].get(r, 1.0)
            if night:
                w *= lure.get("night_bias", {}).get(r, 1.0)
                w *= {"common": 0.85, "uncommon": 1.0, "rare": 1.3,
                      "epic": 1.45, "legendary": 1.25}.get(r, 1.0)
            if high > 0.05:
                swing = {"common": -0.30, "uncommon": 0.06, "rare": 0.45,
                         "epic": 0.85, "legendary": 1.30}.get(r, 0.0)
                w *= max(0.05, 1.0 + high * swing * (0.55 + strain_luck))
            if r in ("rare", "epic", "legendary"):
                w *= depth_bonus
            w = max(0.001, w)
            weights.append((sp, w))
            total += w
        roll = random.random() * total
        acc = 0.0
        for sp, w in weights:
            acc += w
            if roll <= acc:
                return sp
        return weights[-1][0]

    # ---------------------------------------------------------------- update
    def update(self, dt):
        self._animate_rod(dt)
        if self.state == "charge":
            self._update_charge(dt)
        elif self.state == "fly":
            self._update_fly(dt)
        elif self.state == "wait":
            self._update_wait(dt)
        elif self.state == "bite":
            self._update_bite(dt)
        elif self.state == "hook":
            self._update_hook(dt)
        elif self.state == "reel":
            self._update_reel(dt)

    def _animate_rod(self, dt):
        w = self.world
        if self.state == "charge":
            # eased pull-back, so it feels like loading rather than a slider
            w.rod_angle = -0.55 - 0.52 * smoothstep(self.power)
            w.rod_bend = 0.10 + 0.14 * self.power
        elif self.state == "fly":
            # one continuous whip: fast through the first half, then settle
            t = clamp(self.fly_t / max(0.12, C.FLY_TIME))
            swing = ease_out_cubic(min(1.0, t / 0.42))
            w.rod_angle = lerp(self.cast_from, -0.26, swing)
            w.rod_bend = 0.34 * (1.0 - swing) + 0.06
        elif self.state == "hook":
            w.rod_angle = approach(w.rod_angle, -0.16, 15.0, dt)
            w.rod_bend = approach(w.rod_bend, 1.0, 11.0, dt)
        elif self.state == "reel":
            w.rod_angle = -0.30 - 0.22 * self.tension
            w.rod_bend = 0.25 + 0.75 * min(1.0, self.tension)
        elif self.state == "wait":
            w.rod_angle = approach(w.rod_angle, -0.52, 3.0, dt)
            w.rod_bend = approach(w.rod_bend, 0.10, 3.5, dt)
        else:
            w.rod_angle = approach(w.rod_angle, -0.55, 5.0, dt)
            w.rod_bend = approach(w.rod_bend, 0.0, 5.0, dt)
        w.rod_reach = 1.0

    def _update_charge(self, dt):
        self.charge_t += dt
        self.power += self.charge_dir * dt / C.CHARGE_TIME
        if self.power >= 1.0:
            self.power = 1.0
            self.charge_dir = -1
        elif self.power <= 0.0:
            self.power = 0.0
            self.charge_dir = 1

    def _update_fly(self, dt):
        self.fly_t += dt
        t = clamp(self.fly_t / C.FLY_TIME)
        x0, y0 = self.fly_from
        x1, y1, _ = self.world.bobber_pos(self.depth)
        x = lerp(x0, x1, t)
        y = lerp(y0, y1, t) - math.sin(math.pi * t) * (150 * clamp(self.depth) + 30)
        self.bobber_screen = (x, y)
        if t >= 1.0:
            self.state = "wait"
            self.wait_t = 0.0
            self.events.append("plop")
            self.world.splash_at(self.depth, 9)
            self.world.ripple_at(self.depth, 2)
            p = self.player
            self.wait_duration = random.uniform(C.WAIT_MIN, C.WAIT_MAX) * (
                1.0 - 0.16 * p.high)
            self.next_nibble = random.uniform(0.5, max(0.6, self.wait_duration - 0.4))
            if random.random() < 0.45:
                self.next_nibble = -1
            self.nibble_count = 0
            self.next_nibble = random.uniform(0.5, max(0.6, self.wait_duration - 0.4))
            if random.random() < 0.45:
                self.next_nibble = -1

    def _update_wait(self, dt):
        self.wait_t += dt
        x, y, s = self.world.bobber_pos(self.depth)
        self.bobber_screen = (x, y)
        if (self.next_nibble > 0 and self.wait_t >= self.next_nibble
                and self.wait_t < self.wait_duration - 0.25 and self.nibble_count < 2):
            self.nibble_count += 1
            self.next_nibble = self.wait_t + random.uniform(0.45, 1.2)
            self.nibble_t = 0.22
            self.events.append("nibble")
            self.world.ripple_at(self.depth, 1)
        if self.nibble_t > 0:
            self.nibble_t -= dt
        if self.wait_t >= self.wait_duration:
            self.state = "bite"
            self.bite_t = 0.0
            self.events.append("bite")
            self.world.splash_at(self.depth, 12)

    def _update_bite(self, dt):
        self.bite_t += dt
        window = C.BITE_WINDOW + C.BITE_WINDOW_PER_HIGH * self.player.high
        self.float_sink = 6.0
        if self.bite_t >= window:
            self.state = "result"
            self.result = {"fail": "escape", "fish": self.fish}
            self.events.append("escape")

    def _update_hook(self, dt):
        self.hook_t += dt
        x, y, s = self.world.bobber_pos(self.depth)
        self.bobber_screen = (x, y + 4)
        if self.hook_t >= C.HOOK_TIME:
            self.state = "reel"
            self.hits = 0
            rk = RARITY_INDEX.get(self.fish["rarity"], 0)
            self.needed = max(2, int(round(5 + rk * 1.3 + self.fish["diff"] * 2)))
            self.zone_w = clamp(0.22 - 0.030 * rk - 0.055 * self.fish["diff"]
                                + self.player.rod_data["tol"] * 0.20
                                + 0.010 * self.player.stoke["fish"], 0.04, 0.26)
            self.zone = 0.5
            self.zone_phase = 0.0
            self.sweep = 0.0
            self.idle_t = 0.0
            self.progress = 0.0
            self.events.append("reel_start")

    def pull(self):
        """Timed pull: green is best, blue counts, red does nothing."""
        if self.state != "reel":
            return
        pos = getattr(self, "sweep_drawn", self.sweep)
        d = abs(pos - self.zone)
        half = self.zone_w * 0.5
        blue_w = self.zone_w * 0.09
        if d <= blue_w:
            gain = 3
            self.events.append("pull_blue")
        elif d <= half:
            gain = 1
            self.events.append("pull_green")
        else:
            self.hits -= 2
            self.progress = max(0.0, self.hits / float(self.needed))
            self.events.append("pull_red")
            if self.hits < 0:
                self.state = "result"
                self.result = {"fail": "escape", "fish": self.fish}
                self.events.append("escape")
                self.world.splash_at(self.depth, 16)
            return
        self.hits += gain
        self.progress = min(1.0, self.hits / float(self.needed))
        self.idle_t = 0.0
        self.world.splash_at(self.depth, 6)
        if self.hits >= self.needed:
            self.state = "result"
            self.result = {"fail": None, "fish": self.fish,
                           "kg": self.kg, "cm": self.cm}
            self.events.append("landed")
            self.world.splash_at(self.depth, 24)

    def _update_reel(self, dt):
        self.reel_t += dt
        p = self.player
        diff = self.fish["diff"] if self.fish else 0.4
        speed = 0.95 + 0.55 * diff + 0.35 * (1.0 - min(1.0, p.rod_data["reel"]))
        self.sweep = (self.sweep + speed * dt) % 1.0
        rk = RARITY_INDEX.get(self.fish.get("rarity", "common"), 0) if self.fish else 0
        self.zone_phase += dt * (0.60 + 0.10 * rk + 0.30 * diff)
        self.zone = 0.5 + 0.34 * math.sin(self.zone_phase)

        x, y, s = self.world.bobber_pos(self.depth)
        jitter = math.sin(self.reel_t * 22) * 2.5
        self.bobber_screen = (x + jitter, y + 3)

        if self.flags.get("instacatch"):
            self.hits = self.needed
            self.progress = 1.0
            self.state = "result"
            self.result = {"fail": None, "fish": self.fish,
                           "kg": self.kg, "cm": self.cm}
            self.events.append("landed")
            return

        if self.progress <= 0.001:
            self.idle_t += dt
            if self.idle_t > 8.0:
                self.state = "result"
                self.result = {"fail": "escape", "fish": self.fish}
                self.events.append("escape")
                self.world.splash_at(self.depth, 16)
        else:
            self.idle_t = 0.0

    # --------------------------------------------------------------- helpers
    @property
    def is_fishing(self):
        return self.state in ("fly", "wait", "bite", "hook", "reel")

    def tension_state(self):
        if self.tension > 0.90:
            return "danger"
        if abs(self.tension - self.band) <= self.band_half:
            return "good"
        return "slack"
