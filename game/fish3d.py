"""Low-poly 3D fish meshes, rendered once per species and cached."""
from __future__ import annotations

import math
import random

import pygame

from .render3d import Camera, Mesh, Scene, mix, vnorm

RENDER_W, RENDER_H = 256, 128
_CACHE = {}

TAIL = {"minnow": 0.34, "torpedo": 0.46, "round": 0.40, "flat": 0.38, "eel": 0.30,
        "gar": 0.34, "cat": 0.40, "sturgeon": 0.44, "paddle": 0.46, "angler": 0.40,
        "ray": 0.42, "frog": 0.38, "weird": 0.36}


def _profile(shape, t):
    s = math.sin(math.pi * max(0.0, min(1.0, t)))
    if shape == "round":
        h = 0.44 * s ** 0.55 + 0.05
    elif shape == "minnow":
        h = 0.21 * s ** 0.80 + 0.04
    elif shape == "eel":
        h = 0.12 + 0.035 * s
    elif shape == "gar":
        h = 0.17 * s ** 0.50 + 0.05
    elif shape == "cat":
        h = 0.30 * s ** 0.40 + 0.06
        if t > 0.72:
            h += 0.10 * (t - 0.72) / 0.28
    elif shape == "paddle":
        h = 0.28 * s ** 0.55 + 0.05
    elif shape == "sturgeon":
        h = 0.28 * s ** 0.60 + 0.05
    elif shape == "flat":
        h = 0.40 * s ** 0.40 + 0.06
    else:
        h = 0.31 * s ** 0.60 + 0.05
    w = h * (0.95 if shape == "eel" else 0.52)
    return h, w


def build_fish(sp, ghost=False):
    shape = sp["shape"]
    body_c = sp["body"]
    fin_c = sp["fin"]
    acc_c = sp["accent"]
    if ghost:
        body_c = fin_c = (78, 88, 114)
        acc_c = (100, 110, 140)
    pattern = sp["pattern"]
    rng = random.Random(sum(ord(c) * (i + 3) for i, c in enumerate(sp["id"])))
    L = 2.0
    rings = 8
    verts, faces = [], []
    for i in range(rings):
        t = i / (rings - 1.0)
        x = -L * 0.5 + L * t
        h, w = _profile(shape, t)
        for k in range(6):
            a = math.tau * k / 6
            verts.append((x, math.sin(a) * h, math.cos(a) * w))
    for i in range(rings - 1):
        for k in range(6):
            a = i * 6 + k
            b = i * 6 + (k + 1) % 6
            c = (i + 1) * 6 + (k + 1) % 6
            d = (i + 1) * 6 + k
            mid_y = (verts[a][1] + verts[c][1]) * 0.5
            if ghost:
                col = body_c
            elif mid_y < -0.04:
                col = mix(body_c, (246, 244, 236), 0.30)
            elif mid_y > 0.10:
                col = mix(body_c, (14, 20, 24), 0.16)
            else:
                col = body_c
            col = mix(col, (255, 255, 255), 0.10)
            if not ghost:
                if pattern == "bands" and i % 2 == 0:
                    col = mix(col, acc_c, 0.42)
                elif pattern == "spots" and rng.random() < 0.24:
                    col = mix(col, acc_c, 0.50)
                elif pattern == "lateral" and abs(mid_y) < 0.055:
                    col = mix(col, acc_c, 0.55)
                elif pattern == "gradient":
                    col = mix(col, acc_c, max(0.0, min(0.55, 0.30 + mid_y * 1.4)))
            faces.append(((a, b, c, d), col))
    # tail fin
    tx = -L * 0.5
    span = TAIL.get(shape, 0.4)
    base = len(verts)
    verts += [(tx, 0.0, 0.0), (tx - 0.34, span, 0.0), (tx - 0.16, 0.0, 0.0),
              (tx - 0.34, -span, 0.0)]
    faces += [((base, base + 1, base + 2), fin_c), ((base, base + 2, base + 3), fin_c)]
    # dorsal fin
    hh, _ = _profile(shape, 0.55)
    base = len(verts)
    verts += [(-0.15, hh, 0.0), (-0.42, hh + 0.26, 0.0), (0.28, hh * 0.92, 0.0)]
    faces.append(((base, base + 1, base + 2), fin_c))
    # snout
    if shape in ("gar", "paddle"):
        long = 0.85 if shape == "gar" else 0.80
        thick = 0.045 if shape == "gar" else 0.075
        base = len(verts)
        hd, wd = _profile(shape, 1.0)
        verts += [(L * 0.5, hd * 0.35, wd * 0.5),
                  (L * 0.5 + long, 0.0, 0.0),
                  (L * 0.5, -hd * 0.35, wd * 0.5),
                  (L * 0.5, hd * 0.35, -wd * 0.5),
                  (L * 0.5, -hd * 0.35, -wd * 0.5)]
        faces += [((base, base + 1, base + 2), body_c),
                  ((base + 3, base + 4, base + 1), body_c)]
        del thick
    return Mesh(verts, faces)


def fish_surface(sp, ghost=False, dim=False):
    key = (sp["id"], ghost)
    surf = _CACHE.get(key)
    if surf is not None:
        return surf
    surf = pygame.Surface((RENDER_W, RENDER_H), pygame.SRCALPHA)
    cam = Camera((0.55, 0.42, 3.15), (0.0, 0.0, 0.0), fov=26.0, height=RENDER_H)
    scene = Scene(RENDER_W, RENDER_H, cam)
    scene.ambient = 0.72
    scene.fog = (0, 0, 0)
    scene.light = vnorm((0.25, 0.55, 0.80))
    scene.add_mesh(build_fish(sp, ghost), shade=not ghost, fog=False)
    scene.draw(surf)
    if not ghost:
        # eye
        ex, ey = int(RENDER_W * 0.70), int(RENDER_H * 0.43)
        pygame.draw.circle(surf, (250, 248, 240), (ex, ey), 7)
        pygame.draw.circle(surf, (22, 24, 32), (ex + 1, ey), 4)
        pygame.draw.circle(surf, (255, 255, 255), (ex - 1, ey - 2), 2)
    if len(_CACHE) > 90:
        _CACHE.clear()
    _CACHE[key] = surf
    return surf


def random_patches(sid):
    import random
    rng = random.Random(sum(ord(c) * (i + 7) for i, c in enumerate(sid)))
    return [(rng.uniform(70, 215), rng.uniform(42, 92), rng.uniform(3, 7))
            for _ in range(9)]
