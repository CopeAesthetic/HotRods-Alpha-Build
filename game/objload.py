"""Tiny OBJ loader for the software renderer.

Only what our Blender export produces: `v` lines and polygon `f` lines
(1-based, `v`, `v/vt`, `v//vn` forms all accepted). Colours come from a
per-asset palette entry, since the MTL is ignored.
"""
from __future__ import annotations

import os

from .render3d import Mesh


def load(path, color, scale=1.0, offset=(0.0, 0.0, 0.0)):
    verts, faces = [], []
    with open(path, "r", encoding="utf-8") as fh:
        for line in fh:
            parts = line.split()
            if not parts:
                continue
            if parts[0] == "v":
                x, y, z = (float(v) * scale for v in parts[1:4])
                verts.append((x + offset[0], y + offset[1], z + offset[2]))
            elif parts[0] == "f":
                idx = []
                for token in parts[1:]:
                    i = int(token.split("/")[0])
                    idx.append(i - 1 if i > 0 else len(verts) + i)
                for k in range(1, len(idx) - 1):          # fan-triangulate
                    faces.append(((idx[0], idx[k], idx[k + 1]), color))
    return Mesh(verts, faces)


def asset(name, color, **kw):
    here = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    return load(os.path.join(here, "assets", name + ".obj"), color, **kw)
