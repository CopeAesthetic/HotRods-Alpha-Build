"""Content: spots, 32 fish, rods, lines, lures, strains and supplies.

SINGLE SOURCE OF TRUTH: the data lives in content/*.json (edit those files,
not this module). This module loads them at import time and exposes the same
module-level names the rest of the game has always used.

To regenerate the JSON from scratch after bulk edits, run:
    python tools/export_content.py
(It exports FROM this module's old dict shape — see that script's docstring.)

Pure data + tiny helpers, no pygame import.
"""
import json
import sys
from pathlib import Path

if getattr(sys, "frozen", False) and hasattr(sys, "_MEIPASS"):
    # PyInstaller bundle: content/ ships via --add-data "content:content"
    _CONTENT = Path(sys._MEIPASS) / "content"
else:
    _CONTENT = Path(__file__).resolve().parent.parent / "content"


def _load(name: str):
    path = _CONTENT / name
    try:
        with open(path, encoding="utf-8") as fh:
            return json.load(fh)
    except FileNotFoundError as exc:
        raise RuntimeError(
            f"Missing content file {path}. Content lives in content/*.json — "
            "restore it or regenerate with: python tools/export_content.py"
        ) from exc


_FISH = _load("fish.json")
_GEAR = _load("gear.json")
_ECON = _load("economy.json")

# ------------------------------------------------------------------ spots ---
SPOTS = _load("spots.json")

# --------------------------------------------------------------- species ----
# shape: minnow | torpedo | round | flat | eel | ray | frog | sturgeon | angler | weird
# pattern: none | spots | stripes | bands | gradient | glow
# cond: None | "night" | "high" | "night+high" | "strain:<id>"
SPECIES = _FISH["species"]
HIDDEN_FISH = _FISH["hidden"]
SHINY_FISH = _FISH["shiny"]
SHINY_ODDS = _ECON["shiny_odds"]

SPECIES_BY_ID = {s["id"]: s for s in SPECIES}
HIDDEN_BY_ID = {s["id"]: s for s in HIDDEN_FISH}
SHINY_BY_ID = {s["id"]: s for s in SHINY_FISH}

# ------------------------------------------------------------------ gear ----
RODS = _GEAR["rods"]
LINES = _GEAR["lines"]
LURES = _GEAR["lures"]
COOLERS = _GEAR["coolers"]
SMOKERS = _GEAR["smokers"]

RODS_BY_ID = {g["id"]: g for g in RODS}
LINES_BY_ID = {g["id"]: g for g in LINES}
LURES_BY_ID = {g["id"]: g for g in LURES}
COOLERS_BY_ID = {g["id"]: g for g in COOLERS}
SMOKERS_BY_ID = {g["id"]: g for g in SMOKERS}

# ---------------------------------------------------------------- strains ---
STRAINS = _load("strains.json")
STRAINS_BY_ID = {g["id"]: g for g in STRAINS}
STRAINS_BY_AREA = {i: [s for s in STRAINS if s["area"] == i] for i in range(4)}

# classic stoner snacks for the store's munchies tab - price climbs with
# how much of the munchie meter each one knocks off
MUNCHIES = [
    {"id": "chips", "name": "BAG OF CHIPS", "price": 4, "relief": 15},
    {"id": "soda", "name": "SODA POP", "price": 6, "relief": 20},
    {"id": "candy", "name": "CANDY BAR", "price": 8, "relief": 25},
    {"id": "donuts", "name": "DONUTS", "price": 10, "relief": 30},
    {"id": "pizza", "name": "PIZZA SLICE", "price": 12, "relief": 40},
    {"id": "nachos", "name": "NACHOS", "price": 15, "relief": 45},
    {"id": "burrito", "name": "BURRITO", "price": 20, "relief": 60},
    {"id": "burger", "name": "CHEESEBURGER", "price": 25, "relief": 75},
    {"id": "platter", "name": "BBQ PLATTER", "price": 40, "relief": 100},
]

# --------------------------------------------------------------- economy ----
SPOT_PAY = tuple(_ECON["spot_pay"])   # later waters pay more, cash and VP
GEAR_SPOT = _ECON["gear_spot"]        # gear unlock water (0 pond .. 3 trench)

ICE_COST = _ECON["ice_cost"]
ICE_MINUTES = _ECON["ice_minutes"]    # minutes of cold per bag
SPOIL_RATE = _ECON["spoil_rate"]      # freshness lost per minute once the ice is gone
SPOIL_FLOOR = _ECON["spoil_floor"]    # fish never drop below this fraction of value

CONCENTRATE_POTENCY = _ECON["concentrate_potency"]
CONCENTRATE_MULT = _ECON["concentrate_mult"]

# ------------------------------------------------------------------- text ---
TIPS = _load("tips.json")


def pay_mult(sid):
    sp = SPECIES_BY_ID.get(sid) or HIDDEN_BY_ID.get(sid) or SHINY_BY_ID.get(sid)
    spot = sp["spot"] if sp else 0
    return SPOT_PAY[min(spot, len(SPOT_PAY) - 1)]


def fish_for_spot(index):
    return [s for s in SPECIES if s["spot"] == index]


def species(sid):
    return SPECIES_BY_ID.get(sid) or HIDDEN_BY_ID.get(sid) or SHINY_BY_ID[sid]


def length_cm(kg, shape):
    factor = {
        "minnow": 46, "torpedo": 42, "round": 33, "flat": 30, "eel": 62,
        "ray": 40, "frog": 30, "sturgeon": 58, "angler": 36, "weird": 40,
        "cat": 37, "gar": 56, "paddle": 52,
    }.get(shape, 40)
    return max(3.0, (max(kg, 0.001) ** (1.0 / 3.0)) * factor)


def spot_unlocked(index, found_count, cash):
    sp = SPOTS[index]
    return found_count >= sp["need_species"] and cash >= sp["need_cash"]
