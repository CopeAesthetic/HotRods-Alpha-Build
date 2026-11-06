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
    ],
    1: [],
    2: [],
    3: [],
}

# ground level for each asset family, so tables stay two-dimensional
GROUND = {"land": 0.40, "water": 0.02, "dock": 0.72}


def props(spot):
    return LAYOUTS.get(spot, [])
