"""Collision audit: every solid part in each water, pairwise box overlap.

Water is the only thing allowed to intersect anything, so it is excluded.
Prints overlapping pairs; the angler sits on the dock, so angler-dock
contact is flagged as expected contact.
"""
from __future__ import annotations

import os
import sys

os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import math  # noqa: E402

from game import layouts, objload  # noqa: E402
from assets.manifest import ASSETS  # noqa: E402

EPS = 0.02


def bbox(verts):
    xs = [v[0] for v in verts]
    ys = [v[1] for v in verts]
    zs = [v[2] for v in verts]
    return (min(xs), min(ys), min(zs), max(xs), max(ys), max(zs))


def vol(a, b):
    ix = max(0.0, min(a[3], b[3]) - max(a[0], b[0]))
    iy = max(0.0, min(a[4], b[4]) - max(a[1], b[1]))
    iz = max(0.0, min(a[5], b[5]) - max(a[2], b[2]))
    return ix * iy * iz


def parts(spot):
    out = []
    for name, px, pz, rot, sc in layouts.props(spot):
        y = layouts.GROUND.get(ASSETS[name][2], 0.30)
        col, bsc, _ = ASSETS[name]
        m = objload.asset(name, col, scale=sc * bsc)
        if rot:
            c, sn = math.cos(rot), math.sin(rot)
            m.verts = [(vx * c - vz * sn + px, vy + y, vx * sn + vz * c + pz)
                       for vx, vy, vz in m.verts]
        else:
            m.verts = [(vx + px, vy + y, vz + pz) for vx, vy, vz in m.verts]
        out.append((name, bbox(m.verts)))
    dk = layouts.dock(spot)
    for i in range(dk["n"]):
        x = dk["x0"] + i * 1.42
        out.append(("plank", (x - 0.62, 0.555, dk["z"] - dk["dz"] / 2,
                              x + 0.62, 0.685, dk["z"] + dk["dz"] / 2)))
    for i in range(dk["arm"]):
        z = -0.55 - i * 1.42
        out.append(("arm_plank", (dk["arm_x"] - 1.25, 0.555, z - 0.62,
                                  dk["arm_x"] + 1.25, 0.685, z + 0.62)))
    for name, rx, rz, rot, sc in layouts.water_props(spot)[1]:
        col, bsc, _ = ASSETS[name]
        m = objload.asset(name, col, scale=sc * bsc)
        out.append((name, bbox([(vx + rx, vy - 0.05, vz + rz)
                                for vx, vy, vz in m.verts])))
    bk = layouts.bank(spot)
    for i in range(bk["n"]):
        bx = bk["x0"] + i * bk["spacing"]
        out.append(("bank", (bx - bk["w"] / 2 - 0.3, 0.13, bk["z"] - 2.8,
                             bx + bk["w"] / 2 + 0.3, 0.55, bk["z"] + 2.8)))
    for i in range(10):
        bx = -36 + i * 8.0
        out.append(("north_shore", (bx - 4.3, 0.11, 12.2, bx + 4.3, 0.80, 18.6)))
    for i in range(8):
        bx = -32 + i * 9.0
        out.append(("north_grass", (bx - 4.6, 0.43, 16.6, bx + 4.6, 1.15, 21.8)))
    for name, px, pz, rot, sc in layouts.water_props(spot)[0]:
        col, bsc, _ = ASSETS[name]
        m = objload.asset(name, col, scale=sc * bsc)
        out.append((name, bbox([(vx + px, vy + 0.02, vz + pz)
                                for vx, vy, vz in m.verts])))
    return out


def main():
    for spot in range(4):
        ps = parts(spot)
        hits = []
        for i in range(len(ps)):
            for j in range(i + 1, len(ps)):
                n1, b1 = ps[i]
                n2, b2 = ps[j]
                v = vol(b1, b2)
                if v > EPS:
                    hits.append((n1, n2, round(v, 3)))
        print("spot %d (%s): %d parts, %d overlapping pairs"
              % (spot, layouts.SPOT_NAMES[spot] if hasattr(layouts, "SPOT_NAMES") else "", len(ps), len(hits)))
        for n1, n2, v in sorted(hits, key=lambda t: -t[2]):
            print("   %-12s x %-12s  overlap %.3f" % (n1, n2, v))


if __name__ == "__main__":
    main()
