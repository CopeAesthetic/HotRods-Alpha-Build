"""Catch & Chill - tuning constants, palette and time-of-day grading."""
from __future__ import annotations

import bisect

from .util import mix, smoothstep

# ---------------------------------------------------------------- window ----
W, H = 1280, 720
FPS = 60
CAPTION = "Hot Rods"
SAVE_VERSION = 1

HORIZON = 424          # waterline
DOCK_Y = 516           # dock planks (feet level)
ROD_ANCHOR = (272, 470)  # where the rod is held

# ---------------------------------------------------------------- colours ---
INK = (24, 26, 38)
INK_SOFT = (40, 45, 62)
INK_LIGHT = (64, 70, 92)
CREAM = (247, 240, 222)
CREAM_DIM = (204, 196, 178)
SAND = (235, 212, 168)
GOLD = (246, 192, 94)
GOLD_DEEP = (203, 140, 50)
GREEN = (132, 204, 132)
LEAF = (112, 178, 116)
RED = (230, 104, 94)
BLUE = (112, 170, 232)
PURPLE = (188, 138, 238)
WHITE = (255, 255, 255)
BLACK = (0, 0, 0)

PANEL_BG = (22, 26, 40)
PANEL_EDGE = (58, 64, 88)
TEXT = CREAM
TEXT_DIM = (156, 160, 176)

# ---------------------------------------------------------------- rarity ----
RARITY_ORDER = ["common", "uncommon", "rare", "epic", "legendary"]
RARITY_LABEL = {
    "common": "Common",
    "uncommon": "Uncommon",
    "rare": "Rare",
    "epic": "Epic",
    "legendary": "Legendary",
}
RARITY_COLORS = {
    "common": (158, 166, 180),
    "uncommon": (118, 206, 142),
    "rare": (104, 166, 240),
    "epic": (246, 182, 78),
    "legendary": (198, 132, 246),
}
# Market model: sale price comes from the fish's weight times a realistic
# per-kg value, softened by an exponent so a 40 kg sturgeon is a jackpot rather
# than an economy-breaker, and capped per rarity.
VALUE_K = 6
VALUE_EXP = 0.72
TROPHY_MULT = {"common": 1.0, "uncommon": 1.12, "rare": 1.30,
               "epic": 1.60, "legendary": 2.20}
VALUE_CAP = {"common": 80, "uncommon": 160, "rare": 420,
             "epic": 750, "legendary": 1500}

# Fish turned in at Sal's are worth points, not dollars.
RARITY_POINTS = {
    "common": 1,
    "uncommon": 3,
    "rare": 8,
    "epic": 18,
    "legendary": 40,
}
RARITY_INDEX = {r: i for i, r in enumerate(RARITY_ORDER)}

RARITY_VALUE = {
    "common": 1.0,
    "uncommon": 1.6,
    "rare": 2.6,
    "epic": 4.6,
    "legendary": 8.0,
}
# Baseline bite weights before lure / lure / night / high modifiers.
RARITY_WEIGHT = {
    "common": 62.0,
    "uncommon": 25.0,
    "rare": 10.0,
    "epic": 2.6,
    "legendary": 0.55,
}

# ------------------------------------------------------------- time of day --
# hour, sky stops, water stops, sun colour, ambient brightness, star amount
SKY_KEYS = [
    (0.0, dict(top=(9, 13, 32), mid=(17, 23, 51), hor=(38, 43, 78),
               water_far=(30, 43, 68), water_near=(11, 19, 36),
               sun=(226, 231, 250), ambient=0.52, stars=1.0)),
    (4.6, dict(top=(30, 38, 78), mid=(86, 82, 128), hor=(214, 140, 128),
               water_far=(74, 84, 120), water_near=(26, 38, 64),
               sun=(250, 176, 146), ambient=0.62, stars=0.45)),
    (7.2, dict(top=(104, 164, 214), mid=(176, 214, 234), hor=(250, 226, 190),
               water_far=(112, 170, 186), water_near=(30, 90, 110),
               sun=(255, 242, 206), ambient=0.94, stars=0.0)),
    (13.0, dict(top=(68, 148, 216), mid=(142, 204, 236), hor=(236, 240, 226),
                water_far=(96, 176, 190), water_near=(24, 96, 122),
                sun=(255, 250, 226), ambient=1.0, stars=0.0)),
    (17.0, dict(top=(96, 140, 202), mid=(238, 184, 138), hor=(255, 208, 152),
                water_far=(150, 162, 162), water_near=(40, 86, 106),
                sun=(255, 204, 134), ambient=0.9, stars=0.0)),
    (19.4, dict(top=(46, 50, 106), mid=(168, 96, 122), hor=(250, 132, 92),
                water_far=(92, 86, 122), water_near=(26, 46, 76),
                sun=(255, 152, 102), ambient=0.66, stars=0.25)),
    (21.6, dict(top=(11, 15, 37), mid=(19, 26, 56), hor=(44, 48, 86),
                water_far=(32, 45, 72), water_near=(12, 20, 40),
                sun=(224, 230, 250), ambient=0.55, stars=1.0)),
]


def sky_palette(hour: float) -> dict:
    """Interpolate the sky keyframes for a given hour (0..24)."""
    hour = hour % 24.0
    hours = [k[0] for k in SKY_KEYS]
    i = max(0, bisect.bisect_right(hours, hour) - 1)
    h0, p0 = SKY_KEYS[i]
    h1, p1 = SKY_KEYS[(i + 1) % len(SKY_KEYS)]
    if h1 <= h0:
        h1 += 24.0
    t = smoothstep((hour - h0) / (h1 - h0))
    out = {}
    for key, a in p0.items():
        b = p1[key]
        if isinstance(a, tuple):
            out[key] = mix(a, b, t)
        else:
            out[key] = a + (b - a) * t
    return out


def is_night(hour: float) -> bool:
    hour %= 24.0
    return hour >= 20.0 or hour < 5.5


def clock_string(hour: float) -> str:
    hour %= 24.0
    hh = int(hour)
    mm = int((hour - hh) * 60)
    suffix = "AM" if hh < 12 else "PM"
    h12 = hh % 12
    if h12 == 0:
        h12 = 12
    return "%d:%02d %s" % (h12, mm, suffix)


# ------------------------------------------------------------- gameplay -----
CHARGE_TIME = 1.15            # seconds for the power meter to swing out
FLY_TIME = 0.62
BITE_WINDOW = 0.9             # base window to set the hook
BITE_WINDOW_PER_HIGH = 0.42
WAIT_MIN, WAIT_MAX = 1.9, 4.6
HOOK_TIME = 0.34              # little "yank" animation before reeling

REEL_TENSION_RISE = 0.72     # per second while holding
REEL_TENSION_FALL = 1.18     # per second while released
SNAP_LIMIT = 1.0
REEL_PROGRESS_RATE = 0.20    # per second inside the band (rod power scales it)
REEL_DECAY_RATE = 0.035      # per second outside the band

HOUR_PER_SECOND = 1.0 / 30.0  # 1 in-game hour every 30 real seconds -> 12 min day

TOKE_AMOUNT = 42.0
CHILL_DECAY = 2.1            # per second
COUCH_LOCK_TIME = 2.6

DISCOVERY_BONUS = 1.0   # extra cash on the first catch of a species

TOTAL_SPECIES = 32
