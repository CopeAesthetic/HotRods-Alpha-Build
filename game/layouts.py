"""Per-water prop placement.

Each entry is (asset, x, z, rotation, scale); the world builder reads these
instead of hardcoding coordinates, so a new water is a table rather than a
hundred lines of box() calls.

  0 Bongwater Pond   1 Bass & Bake Lake   2 Munchies Marsh   3 Tokers Trench
"""
from __future__ import annotations

# (asset, x, z, rot, scale) - y is taken from where the asset sits
LAYOUTS = {
    0: [
        ("tree_a", -30.0, -17.6, 0.4, 1.30),
        ("tree_b", -22.4, -18.2, 2.1, 1.05),
        ("tree_a", -15.2, -17.1, 3.9, 1.15),
        ("tree_b", -7.8, -18.4, 1.2, 1.35),
        ("tree_a", -0.4, -17.3, 5.0, 1.00),
        ("tree_b", 7.2, -18.0, 2.8, 1.20),
        ("tree_a", 14.6, -17.5, 0.9, 1.10),
        ("tree_b", 22.0, -18.3, 4.2, 1.25),
        ("tree_a", 29.4, -17.2, 1.7, 1.05),
        ("shack", -9.4, -15.2, 0.15, 1.00),
        ("dock_post", -6.05, -2.15, 0.0, 1.00),
        ("dock_post", -9.45, -2.15, 0.0, 1.00),
        ("rod_rack", -7.10, 2.60, 0.20, 1.00),
    ],
    # Bass & Bake Lake - further out, sparser, a jetty instead of a bank shack
    1: [
        ("tree_b", -34.0, -20.0, 0.9, 1.40),
        ("tree_a", -26.0, -21.0, 3.2, 1.20),
        ("tree_b", -18.0, -19.4, 5.4, 1.30),
        ("tree_a", -10.0, -21.2, 1.8, 1.10),
        ("tree_b", -2.0, -20.1, 4.6, 1.35),
        ("tree_a", 6.0, -21.4, 2.4, 1.25),
        ("tree_b", 14.0, -19.8, 0.7, 1.15),
        ("tree_a", 22.0, -21.0, 5.1, 1.30),
        ("tree_b", 30.0, -20.2, 3.6, 1.40),
        ("dock_post", -6.05, -2.15, 0.0, 1.00),
        ("rod_rack", -7.10, 2.60, 0.20, 1.00),
    ],
    2: [],
    3: [],
}

# ground level for each asset family, so tables stay two-dimensional
GROUND = {"land": 0.40, "water": 0.02, "dock": 0.72}


import random as _random

# water dressing, seeded so the pond looks the same every run
_rng = _random.Random(5)
LILIES = [("lily", round(_rng.uniform(-8.5, 8.5), 2), round(_rng.uniform(-9.0, -1.5), 2),
           round(_rng.uniform(0, 6.28), 2), round(_rng.uniform(0.7, 1.15), 2))
          for _ in range(16)]
REEDS = [("reeds", round(_rng.uniform(-9.0, 9.0), 2), round(_rng.uniform(-10.0, -1.0), 2),
          round(_rng.uniform(0, 6.28), 2), round(_rng.uniform(0.8, 1.5), 2))
         for _ in range(22)]


def props(spot):
    return LAYOUTS.get(spot, [])


def water_props(spot):
    """Lilies and reeds - only the pond is dressed so far."""
    if spot != 0:
        return [], []
    return LILIES, REEDS
