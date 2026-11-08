#!/usr/bin/env python3
"""Export game.data content to content/*.json — the single source of truth.

Run from the repo root:  python tools/export_content.py

Use this for one-shot migrations and mass content edits. At runtime,
game.data (Python) and GameData.cs (C#) both load the JSON files this
script produces, so nothing in this script ships in the game.
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

from game import data  # noqa: E402

OUT = ROOT / "content"


def dump(name: str, obj) -> None:
    path = OUT / name
    path.write_text(json.dumps(obj, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {path.relative_to(ROOT)}")


def main() -> None:
    OUT.mkdir(exist_ok=True)
    dump("spots.json", data.SPOTS)
    dump("fish.json", {
        "species": data.SPECIES,
        "hidden": data.HIDDEN_FISH,
        "shiny": data.SHINY_FISH,
    })
    dump("strains.json", data.STRAINS)
    dump("gear.json", {
        "rods": data.RODS,
        "lines": data.LINES,
        "lures": data.LURES,
        "coolers": data.COOLERS,
        "smokers": data.SMOKERS,
    })
    dump("economy.json", {
        "shiny_odds": data.SHINY_ODDS,
        "spot_pay": list(data.SPOT_PAY),
        "gear_spot": data.GEAR_SPOT,
        "ice_cost": data.ICE_COST,
        "ice_minutes": data.ICE_MINUTES,
        "spoil_rate": data.SPOIL_RATE,
        "spoil_floor": data.SPOIL_FLOOR,
        "concentrate_potency": data.CONCENTRATE_POTENCY,
        "concentrate_mult": data.CONCENTRATE_MULT,
    })
    dump("tips.json", data.TIPS)


if __name__ == "__main__":
    main()
