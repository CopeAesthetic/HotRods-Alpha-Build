"""Low-poly 3D lake scene built on render3d."""
from __future__ import annotations

import math
import random

import pygame

from . import config as C
from . import data
from . import util
from .render3d import Camera, Mesh, Scene, box, mix, vnorm, vsub, vcross, vlen
from . import objload
from assets.manifest import ASSETS

SMOKER_TINT = {
    "one_hitter": (198, 172, 120), "papers": (236, 232, 220),
    "backwoods": (120, 92, 62), "pipe": (150, 196, 210),
    "beaker_bong": (140, 200, 180), "heady_bong": (210, 170, 240),
    "dab_rig": (250, 210, 120), "hot_press": (226, 120, 96),
}
from . import layouts
from .util import Particles, clamp, lighten, darken, smoothstep

INTERNAL_W, INTERNAL_H = 480, 270


def segment(p0, p1, radius, color, sides=4):
    """A prism between two points - rods, limbs, posts."""
    d = vsub(p1, p0)
    if vlen(d) < 1e-6:
        d = (0.0, 1.0, 0.0)
    fwd = vnorm(d)
    up = (0.0, 1.0, 0.0) if abs(fwd[1]) < 0.95 else (1.0, 0.0, 0.0)
    right = vnorm(vcross(fwd, up))
    up2 = vcross(right, fwd)
    verts = []
    for base in (p0, p1):
        for i in range(sides):
            a = math.tau * i / sides
            verts.append((base[0] + math.cos(a) * radius * right[0] + math.sin(a) * radius * up2[0],
                          base[1] + math.cos(a) * radius * right[1] + math.sin(a) * radius * up2[1],
                          base[2] + math.cos(a) * radius * right[2] + math.sin(a) * radius * up2[2]))
    faces = []
    for i in range(sides):
        j = (i + 1) % sides
        faces.append(((i, j, sides + j, sides + i), color))
    return Mesh(verts, faces)


def cone(base, tip, radius, color, sides=6, cap_color=None):
    verts = [tip]
    for i in range(sides):
        a = math.tau * i / sides
        verts.append((base[0] + math.cos(a) * radius, base[1], base[2] + math.sin(a) * radius))
    faces = []
    for i in range(sides):
        j = 1 + (i + 1) % sides
        faces.append(((0, 1 + i, j), color))
    return Mesh(verts, faces)


def disc(cx, cy, cz, r, color, sides=8):
    verts = [(cx, cy, cz)]
    for i in range(sides):
        a = math.tau * i / sides
        verts.append((cx + math.cos(a) * r, cy, cz + math.sin(a) * r))
    faces = [((0, 1 + i, 1 + (i + 1) % sides), color) for i in range(sides)]
    return Mesh(verts, faces)


def join_meshes(meshes):
    verts, faces = [], []
    for m in meshes:
        off = len(verts)
        verts.extend(m.verts)
        for idx, col in m.faces:
            faces.append((tuple(i + off for i in idx), col))
    return Mesh(verts, faces)


class World:
    """Same surface API as the old 2D world, but everything is polygons."""

    def __init__(self, spot_index, seed=None):
        self.spot_index = spot_index
        self.spot = data.SPOTS[spot_index]
        self.t = 0.0
        self.hour = 12.0
        self.high = 0.0
        self.pal = C.sky_palette(self.hour)
        self.particles = Particles()
        self.rng = random.Random(seed if seed is not None else 1234 + spot_index * 77)
        self.rod_angle = -0.55
        self.rod_bend = 0.0
        self.rod_reach = 1.0
        self.pov = "third"
        self.free_yaw = 0.0
        self.free_pitch = 0.0
        self.orbit = 0.0
        self.orbit_pitch = 0.0
        self.orbit_max = 0.95      # the camera stays around the angler
        self.pipe_raise = 0.0
        self._bobber_screen = (INTERNAL_W * 0.6, INTERNAL_H * 0.6)
        self._tip_screen = (0.0, 0.0)
        # over-the-shoulder: 2.5 m behind the angler, a little above the hat,
        # offset right so he sits in the left third of the frame
        self.cam = Camera((-3.70, 1.92, 4.10), (1.55, 0.72, -7.00), fov=52.0,
                          height=INTERNAL_H)
        self.scene = Scene(INTERNAL_W, INTERNAL_H, self.cam)
        self.wave_phase = 0.0
        self._build_static()
        self._build_water()

    # ------------------------------------------------------------- geometry
    def prop(self, name, x, y, z, rot=0.0, scale=None):
        """Place a Blender asset (assets/*.obj) into world space."""
        col, sc, _ = ASSETS[name]
        m = objload.asset(name, col, scale=(scale or 1.0) * sc)
        if rot:
            c, sn = math.cos(rot), math.sin(rot)
            m.verts = [(vx * c - vz * sn + x, vy + y, vx * sn + vz * c + z)
                       for vx, vy, vz in m.verts]
        else:
            m.verts = [(vx + x, vy + y, vz + z) for vx, vy, vz in m.verts]
        return m

    def _build_static(self):
        s = self.spot_index
        wood = (128, 94, 62)
        wood_dark = (96, 70, 48)
        self.meshes = []
        # --- dock ---------------------------------------------------------
        deck = []
        dk = layouts.dock(s)
        for i in range(dk["n"]):
            x = dk["x0"] + i * 1.42
            c = wood if i % 2 == 0 else (138, 102, 68)
            deck.append(box(x, 0.62, dk["z"], 1.24, 0.13, dk["dz"], c, c))
        for x in (-13.2, -9.8, -6.4, -3.3):
            deck.append(box(x, -0.30, 0.6, 0.30, 1.8, 0.30, wood_dark, wood_dark))
            deck.append(box(x, 0.48, 0.6, 0.42, 0.16, 0.42, (110, 80, 54), (110, 80, 54)))
        # a cooler and a bucket
        deck.append(self.prop("cooler", -4.55, 0.72, 0.35, rot=0.25))
        deck.append(self.prop("bucket", -5.30, 0.72, -0.15, rot=0.6))
        if False:
            deck.append(box(-5.05, 0.95, 2.35, 0.9, 0.55, 0.6, (196, 74, 64), (168, 62, 54),
                        (214, 214, 206)))
            deck.append(box(-4.45, 0.86, 2.10, 0.5, 0.36, 0.5, (120, 130, 140), (96, 104, 114)))
        # the pier turns toward open water at its east end
        for i in range(dk["arm"]):
            z = -0.55 - i * 1.42
            deck.append(box(dk["arm_x"], 0.62, z, 2.5, 0.13, 1.24,
                            wood if i % 2 == 0 else (138, 102, 68), wood))
        for z in (-1.4, -4.2, -7.0):
            deck.append(box(-4.55, -0.30, z, 0.30, 1.8, 0.30, wood_dark, wood_dark))
        deck.append(box(-5.9, 0.20, -7.9, 0.24, 2.2, 0.24, wood_dark, wood_dark))
        self.dock = join_meshes(deck)

        # --- shoreline posts / reeds (low-poly) ---------------------------
        reeds = []
        rng = random.Random(7 + s)
        for i in range(26):
            side = -1 if i % 2 else 1
            x = rng.uniform(-26, -16) if side < 0 else rng.uniform(14, 26)
            z = rng.uniform(-16, 2)
            h = rng.uniform(0.9, 2.4)
            p0 = (x, -0.1, z)
            p1 = (x + rng.uniform(-0.3, 0.3), h, z + rng.uniform(-0.3, 0.3))
            reeds.append(segment(p0, p1, rng.uniform(0.03, 0.07), (58, 72, 50), 3))
        # --- sandy far shore ----------------------------------------------
        beach = []
        sand = (198, 178, 136)
        sand_d = (176, 156, 116)
        rngb = random.Random(31 + s)
        for i in range(14):
            bx = -34 + i * 5.0
            beach.append(box(bx, -0.34, -21.0, 5.2, 0.30, 9.0,
                             sand if i % 2 else sand_d, sand_d))
        for i in range(10):
            bx = -30 + i * 6.6
            beach.append(box(bx, 0.10, -17.2, 4.4, 0.36, 2.6, sand, sand_d))
        self.reeds = join_meshes(reeds)
        # opposite bank, close enough to read from the dock
        bk = layouts.bank(s)
        for i in range(bk["n"]):
            bx = bk["x0"] + i * bk["spacing"]
            beach.append(box(bx, 0.34 + 0.004 * (i % 3), bk["z"], bk["w"] + 0.6,
                             0.42, 5.6, sand, sand_d))
            beach.append(box(bx, 0.70 + 0.004 * (i % 3), -11.4, 5.0, 0.30, 2.0,
                             (186, 168, 128), sand_d))
        for i in range(11):
            bx = -40 + i * 7.4
            beach.append(box(bx, 0.72, -16.6, 6.2, 0.7, 4.2, (132, 144, 92),
                             (110, 122, 76)))
        # north shore, behind the angler: closes the pond
        for i in range(10):
            bx = -36 + i * 8.0
            beach.append(box(bx, 0.34 + 0.004 * (i % 3), 15.4, 8.6, 0.46, 6.4, sand, sand_d))
        for i in range(8):
            bx = -32 + i * 9.0
            beach.append(box(bx, 0.78, 19.2, 9.2, 0.7, 5.2, (132, 144, 92),
                             (110, 122, 76)))
        # side arms, so the bank wraps around the water
        for i in range(6):
            zz = -12.0 + i * 2.2
            beach.append(box(-31.5, 0.06, zz, 7.0, 0.36, 2.3, sand, sand_d))
            beach.append(box(24.5, 0.06, zz, 7.0, 0.36, 2.3, sand, sand_d))
        # sand bank attached to the foot of the dock
        for i in range(7):
            bx = -14.4 - i * 1.9
            beach.append(box(bx, 0.06, -1.0, 2.0, 0.22, 9.0, sand, sand_d))
            beach.append(box(bx, 0.30, -5.2, 2.0, 0.24, 3.0,
                             (186, 168, 128), sand_d))
        for i in range(5):
            bx = -15.5 - i * 2.6
            beach.append(box(bx, 0.58, -6.4, 2.4, 0.55, 5.4, (150, 146, 96),
                             (128, 124, 80)))
        # rocks and shrubs along the bank
        for i in range(9):
            rx = rngb.uniform(-26, -15)
            rz = rngb.uniform(-7.5, 2.0)
            r = rngb.uniform(0.25, 0.7)
            beach.append(disc(rx, 0.12, rz, r, (112, 110, 104), 6))
        for name, px, pz, rot, sc in layouts.props(s):
            y = layouts.GROUND.get(ASSETS[name][2], 0.30)
            beach.append(self.prop(name, px, y, pz, rot=rot, scale=sc))
        self.beach = join_meshes(beach)

        # --- lily pads -----------------------------------------------------
        pads = []
        lily_rows, reed_rows = layouts.water_props(s)
        for name, px, pz, rot, sc in lily_rows:
            pads.append(self.prop(name, px, 0.02, pz, rot=rot, scale=sc))
        self.pads = join_meshes(pads)

        # reeds thicken around the pads
        for name, rx, rz, rot, sc in reed_rows:
            reeds.append(self.prop(name, rx, -0.05, rz, rot=rot, scale=sc))
        self.reeds = join_meshes(reeds)

        # --- far ridge + trees --------------------------------------------
        ridge_verts = []
        ridge_faces = []
        n = 18
        for i in range(n + 1):
            x = -70 + i * (140.0 / n)
            h = 2.4 + 1.8 * math.sin(i * 0.55) + 1.1 * math.sin(i * 1.7 + 1.2)
            ridge_verts.append((x, -0.5, -58.0))
            ridge_verts.append((x, h, -58.0))
            ridge_verts.append((x, h * 0.4, -72.0))
        for i in range(n):
            b = i * 3
            c = (i + 1) * 3
            col = (34, 46, 44) if i % 2 else (40, 54, 50)
            ridge_faces.append(((b, c, c + 1, b + 1), col))
            ridge_faces.append(((b + 1, c + 1, c + 2, b + 2), (26, 36, 38)))
        self.ridge = Mesh(ridge_verts, ridge_faces)

        trees = []
        rng = random.Random(21 + s)
        for i in range(16):
            x = -60 + i * 7.6 + rng.uniform(-1.5, 1.5)
            h = rng.uniform(2.4, 4.2)
            z = -57.0 + rng.uniform(-2.0, 1.0)
            trees.append(cone((x, 0.2, z), (x, h, z), 1.1, (24, 34, 32), 5))
            trees.append(segment((x, -0.2, z), (x, h * 0.35, z), 0.14, (18, 24, 24), 3))
        self.trees = join_meshes(trees)

    def _build_water(self):
        self.cols, self.rows = 8, 7
        self.wx0, self.wx1 = -34.0, 34.0
        self.wz0, self.wz1 = -56.0, 13.0
        self.wverts = [(0.0, 0.0, 0.0)] * ((self.cols + 1) * (self.rows + 1))

    def water_color(self, z, p):
        t = clamp((z - self.wz0) / (self.wz1 - self.wz0))
        return mix(p["water_far"], p["water_near"], t)

    # ---------------------------------------------------------------- state
    def update(self, dt, hour, high=0.0):
        self.t += dt
        self.hour = hour
        self.high = high
        self.wave_phase += dt * 1.1
        self.pal = C.sky_palette(hour)
        self.particles.update(dt)

    def bobber_world(self, depth):
        depth = clamp(depth)
        x = 0.4 + depth * 5.2
        z = 1.1 - depth * 12.5
        y = 0.06 + math.sin(self.t * 2.2) * 0.015
        return (x, y, z)

    def bobber_pos(self, depth):
        """Screen-space float position (for markers / particles)."""
        return self._bobber_screen + (1.0 - clamp(depth) * 0.55,)

    def rod_tip_world(self):
        if self.pov == "first":
            c = self.cam
            hand = (c.eye[0] + c.right[0] * 0.26 - c.up[0] * 0.18,
                    c.eye[1] + c.right[1] * 0.26 - c.up[1] * 0.18,
                    c.eye[2] + c.right[2] * 0.26 - c.up[2] * 0.18)
            tip = (hand[0] + c.fwd[0] * 2.1 + c.up[0] * 0.32,
                   hand[1] + c.fwd[1] * 2.1 + c.up[1] * 0.32,
                   hand[2] + c.fwd[2] * 2.1 + c.up[2] * 0.32)
            return hand, tip
        hand = (-3.05, 1.02, 1.55)
        a = self.rod_angle
        length = 2.15 * self.rod_reach
        tip = (hand[0] + math.cos(a) * length * 0.80,
               hand[1] - math.sin(a) * length - self.rod_bend * 0.10,
               hand[2] - length * 0.40)
        return hand, tip

    def rod_tip(self):
        return self._tip_screen

    def splash_at(self, depth, n=14):
        x, y, s = self.bobber_pos(depth)
        self.particles.splash(x, y, max(3, int(n * 0.8)), power=0.8)
        self.particles.ripple(x, y, r=4, grow=26, life=0.9)

    def ripple_at(self, depth, n=1):
        x, y, s = self.bobber_pos(depth)
        for i in range(n):
            self.particles.ripple(x, y, r=3 + i * 4, grow=22, life=1.1,
                                  alpha=110 - i * 30, squash=0.28)

    # ------------------------------------------------------------------ sky
    def _sky(self, surf, p):
        q = round(self.hour * 8) / 8.0
        pq = C.sky_palette(q)
        grad = util.vgrad(INTERNAL_W, INTERNAL_H, pq["top"], pq["hor"],
                          stops=[(0.0, pq["top"]), (0.5, pq["mid"]), (1.0, pq["hor"])])
        surf.blit(grad, (0, 0))

        # stars
        if p["stars"] > 0.05:
            rng = random.Random(99)
            for i in range(70):
                x = rng.uniform(0, INTERNAL_W)
                y = rng.uniform(0, INTERNAL_H * 0.55)
                tw = 0.6 + 0.4 * math.sin(self.t * 1.7 + i)
                a = p["stars"] * tw
                if a > 0.1:
                    surf.set_at((int(x), int(y)), mix(pq["top"], (255, 250, 232), a))
                    if a > 0.6:
                        surf.set_at((int(x) + 1, int(y)), mix(pq["mid"], (255, 250, 232), a * 0.7))

        # sun / moon
        sun_x, sun_y, _kind = 520.0, 84.0, "sun"
        if self.hour < 5.4 or self.hour > 19.6:
            sun_x, sun_y = 486.0, 70.0
        col = pq["sun"]
        for i in range(5, 0, -1):
            rr = 14 + i * 9
            a = int(10 * (1 - i / 6.0))
            util.circle_alpha(surf, (sun_x, sun_y), rr, col, a)
        pygame.draw.circle(surf, lighten(col, 0.35), (int(sun_x), int(sun_y)), 15)
        pygame.draw.circle(surf, (255, 252, 236), (int(sun_x), int(sun_y)), 10)

        # chunky clouds
        rng = random.Random(5)
        for i in range(5):
            cx = (rng.uniform(0, 640) + self.t * rng.uniform(2, 5)) % (INTERNAL_W + 120) - 60
            cy = rng.uniform(30, 110)
            cc = mix(pq["mid"], (255, 255, 255), 0.35 if p["stars"] < 0.5 else 0.05)
            for dx, dy, w, h in ((0, 0, 46, 16), (26, -6, 30, 13), (-24, 4, 26, 11)):
                pygame.draw.ellipse(surf, cc, (cx + dx - w / 2, cy + dy - h / 2, w, h))

    # --------------------------------------------------------------- scene
    def _add_water(self, p):
        cols, rows = self.cols, self.rows
        sc = self.scene
        for j in range(rows + 1):
            zt = j / rows
            z = self.wz0 + (self.wz1 - self.wz0) * zt
            for i in range(cols + 1):
                xt = i / cols
                x = self.wx0 + (self.wx1 - self.wx0) * xt
                amp = 0.05 + 0.12 * zt
                y = (math.sin(x * 0.42 + self.wave_phase * 1.6 + z * 0.22) * amp
                     + math.sin(z * 0.55 - self.wave_phase * 1.1) * amp * 0.7)
                idx = j * (cols + 1) + i
                self.wverts[idx] = (x, y, z)
        for j in range(rows):
            for i in range(cols):
                a = j * (cols + 1) + i
                b = a + 1
                c = a + cols + 2
                d = a + cols + 1
                zmid = self.wz0 + (self.wz1 - self.wz0) * ((j + 0.5) / rows)
                base = self.water_color(zmid, p)
                if (i + j) % 2:
                    base = lighten(base, 0.05)
                # water is always the backdrop: bias it behind land and props
                sc.add((self.wverts[a], self.wverts[b], self.wverts[c]), base,
                       fog=True, bias=2.0)
                sc.add((self.wverts[a], self.wverts[c], self.wverts[d]),
                       darken(base, 0.05), fog=True, bias=2.0)

    def set_view(self, eye, target):
        """Re-aim the existing camera in place (scene holds a reference)."""
        c = self.cam
        fwd = vnorm(vsub(target, eye))
        right = vnorm(vcross(fwd, (0.0, 1.0, 0.0)))
        up = vcross(right, fwd)
        c.eye, c.fwd, c.right, c.up = eye, fwd, right, up

    def apply_pov(self):
        if self.pov == "first":
            self.set_view((-3.24, 1.60, 1.50), (3.84, 0.63, -5.50))
        elif self.pov == "free":
            tx, ty, tz = (-3.2, 1.2, 1.4)
            r = 12.0
            ex = tx + math.sin(self.free_yaw) * r
            ez = tz + math.cos(self.free_yaw) * r
            ey = 4.5 + self.free_pitch * 7.0
            self.set_view((ex, ey, ez), (tx, ty, tz))
        else:
            # third person: the camera is fixed on the angler, orbiting him
            tgt = (-3.20, 1.00 + self.orbit_pitch * 1.0, 1.50)
            ox, oy, oz = -0.50, 0.92, 2.60
            c, sn = math.cos(self.orbit), math.sin(self.orbit)
            eye = (tgt[0] + ox * c - oz * sn,
                   tgt[1] + oy + self.orbit_pitch * 2.0,
                   tgt[2] + ox * sn + oz * c)
            self.set_view(eye, tgt)

    def _add_angler(self, p, mode):
        hand, tip = self.rod_tip_world()
        body = (96, 110, 88)       # olive fishing vest
        body2 = (78, 92, 74)      # rubber waders
        skin = (206, 158, 116)
        hat = (204, 178, 130)
        parts = []
        # wader-clad legs dangling over the dock edge, rubber boots below
        for (bx, bz) in ((-2.92, 1.26), (-2.85, 1.64)):
            hip = (-3.18, 0.70, 1.38 if bz < 1.5 else 1.72)
            parts.append(segment(hip, (bx - 0.05, 0.16, bz + 0.02), 0.095,
                                 body2, 5))
            parts.append(segment((bx - 0.05, 0.16, bz + 0.02), (bx, 0.0, bz),
                                 0.075, (58, 48, 40), 5))
        # torso: worn shirt under a padded fishing vest, leaning forward
        parts.append(segment((-3.26, 0.66, 1.55), (-3.30, 1.06, 1.51), 0.150,
                             (150, 132, 108), 5))
        parts.append(segment((-3.30, 1.06, 1.51), (-3.34, 1.22, 1.50), 0.175,
                             body, 5))
        # neck, tucked into the vest collar
        parts.append(disc(-3.335, 1.225, 1.495, 0.135, body, 8))
        parts.append(segment((-3.33, 1.225, 1.495), (-3.32, 1.30, 1.49), 0.100,
                             skin, 5))
        # shoulder bar the arms hang from - spans the full shoulder width
        parts.append(segment((-3.44, 1.19, 1.455), (-3.22, 1.19, 1.57), 0.085,
                             body, 5))
        # head
        parts.append(segment((-3.32, 1.30, 1.49), (-3.31, 1.45, 1.48), 0.115, skin, 6))
        parts.append(segment((-3.315, 1.34, 1.475), (-3.315, 1.40, 1.47), 0.085,
                             (96, 74, 52), 5))
        # bucket hat
        parts.append(disc(-3.31, 1.44, 1.48, 0.24, hat, 8))
        parts.append(cone((-3.31, 1.44, 1.48), (-3.31, 1.62, 1.48), 0.145, hat, 7))
        toke = getattr(self, "pipe_raise", 0) > 0
        shoulder = (-3.23, 1.19, 1.56)
        if toke:
            # raise with both hands, hold at the face, lower again
            ph = min(1.0, max(0.0, getattr(self, "pipe_phase", 0.0)))
            if ph < 0.25:
                r = ph / 0.25
            elif ph < 0.75:
                r = 1.0
            else:
                r = max(0.0, 1.0 - (ph - 0.75) / 0.25)
            waist = (-3.13, 0.98, 2.53)
            face = (-3.26, 1.24, 2.64)      # below the chin, moved forward
            hold = (waist[0] + (face[0] - waist[0]) * r,
                    waist[1] + (face[1] - waist[1]) * r,
                    waist[2] + (face[2] - waist[2]) * r)
            # right arm raises the piece to the mouth
            sh_r = (-3.23, 1.19, 1.565)
            el_r = (sh_r[0] + (hold[0] - sh_r[0]) * 0.5,
                    sh_r[1] + (hold[1] - sh_r[1]) * 0.5,
                    sh_r[2] + (hold[2] - sh_r[2]) * 0.5)
            parts.append(segment(sh_r, el_r, 0.065, body, 4))
            parts.append(segment(el_r, hold, 0.055, skin, 4))
            # left arm brings a lighter to the tip
            lighter = (hold[0] + 0.10, hold[1] - 0.01, hold[2] + 0.04)
            sh_l = (-3.43, 1.19, 1.46)
            el_l = (sh_l[0] + (lighter[0] - sh_l[0]) * 0.5,
                    sh_l[1] + (lighter[1] - sh_l[1]) * 0.5,
                    sh_l[2] + (lighter[2] - sh_l[2]) * 0.5)
            parts.append(segment(sh_l, el_l, 0.065, body, 4))
            parts.append(segment(el_l, lighter, 0.055, skin, 4))
            parts.append(segment((lighter[0] - 0.01, lighter[1] - 0.02, lighter[2]),
                                 (lighter[0] + 0.01, lighter[1] + 0.04, lighter[2] + 0.01),
                                 0.020, (232, 186, 96), 4))
            flame_y = lighter[1] + 0.05 + math.sin(self.t * 30.0) * 0.006
            parts.append(segment((lighter[0], lighter[1] + 0.04, lighter[2] + 0.01),
                                 (lighter[0], flame_y, lighter[2] + 0.01),
                                 0.012, (255, 190, 60), 4))
            sid = getattr(self, "smoker_id", None) or "one_hitter"
            col = SMOKER_TINT.get(sid, (198, 168, 128))
            m = objload.asset("smoker_" + sid, col, scale=0.55)
            # small handheld pieces lie horizontal like a joint; glass
            # stays more upright as it rises
            lay = {"one_hitter": -1.57, "papers": -1.57,
                   "backwoods": -1.57}.get(sid, 0.0)
            yaw = {"one_hitter": -1.1, "papers": -1.1,
                   "backwoods": -1.2}.get(sid, 0.0)
            ax = lay * r                    # lie flat...
            ay = yaw * r                    # ...across the body, so it shows
            ca, sa = math.cos(ax), math.sin(ax)
            cb, sb = math.cos(ay), math.sin(ay)
            laid = []
            for vx, vy, vz in m.verts:
                y2 = vy * ca - vz * sa
                z2 = vy * sa + vz * ca
                x3 = vx * cb + z2 * sb
                z3 = -vx * sb + z2 * cb
                laid.append((x3 + hold[0] + 0.02, y2 + hold[1] + 0.10,
                             z3 + hold[2] + 0.04))
            m.verts = laid
            parts.append(m)
            # smoke rises from the mouth, clear of the held piece
            self.smoke_from = (-3.30, 1.40, 1.50)
            # the fishing rod rests beside him while he smokes
            parts.append(segment((-3.76, 0.64, 2.04), (-3.58, 1.28, 2.12),
                                 0.030, (176, 148, 96), 4))
            parts.append(segment((-3.58, 1.28, 2.12), (-3.46, 1.66, 2.16),
                                 0.021, (198, 170, 116), 4))
        else:
            parts.append(segment(shoulder, (-3.12, 1.06, 1.62), 0.065, body, 4))
            parts.append(segment((-3.12, 1.06, 1.62), (-3.03, 1.00, 1.57), 0.055,
                                 skin, 4))
            lhand = (hand[0] + (tip[0] - hand[0]) * 0.35,
                     hand[1] + (tip[1] - hand[1]) * 0.35,
                     hand[2] + (tip[2] - hand[2]) * 0.35)
            lsh = (-3.43, 1.19, 1.46)
            lel = (lsh[0] + (lhand[0] - lsh[0]) * 0.5,
                   lsh[1] + (lhand[1] - lsh[1]) * 0.5,
                   lsh[2] + (lhand[2] - lsh[2]) * 0.5)
            parts.append(segment(lsh, lel, 0.065, body, 4))
            parts.append(segment(lel, lhand, 0.055, skin, 4))
            mid = (hand[0] + (tip[0] - hand[0]) * 0.55,
                   hand[1] + (tip[1] - hand[1]) * 0.55,
                   hand[2] + (tip[2] - hand[2]) * 0.55)
            parts.append(segment(hand, mid, 0.030, (176, 148, 96), 4))
            parts.append(segment(mid, tip, 0.021, (198, 170, 116), 4))
        self.scene.add_mesh(join_meshes(parts), shade=True, fog=True)

    def _add_float(self, depth, mode, taut, fly=0.0):
        x, y, z = self.bobber_world(depth)
        if mode == "fly":
            hand, tip = self.rod_tip_world()
            t = clamp(fly)
            x = tip[0] + (x - tip[0]) * t
            z = tip[2] + (z - tip[2]) * t
            y = tip[1] + (y - tip[1]) * t + math.sin(math.pi * t) * 1.9
        if mode == "bite":
            y -= 0.05
        r = 0.19
        verts = [(x, y + r, z), (x, y - r * 0.7, z),
                 (x + r, y, z), (x - r, y, z), (x, y, z + r), (x, y, z - r)]
        faces = [((0, 2, 4), (238, 238, 234)), ((0, 4, 3), (238, 238, 234)),
                 ((0, 3, 5), (238, 238, 234)), ((0, 5, 2), (238, 238, 234)),
                 ((1, 4, 2), (212, 76, 70)), ((1, 3, 4), (212, 76, 70)),
                 ((1, 5, 3), (212, 76, 70)), ((1, 2, 5), (212, 76, 70))]
        self.scene.add_mesh(Mesh(verts, faces), shade=False, fog=True, bias=-1.4)
        ring = [(x + math.cos(math.tau * k / 8) * 0.34, 0.02,
                 z + math.sin(math.tau * k / 8) * 0.34) for k in range(8)]
        for k in range(8):
            self.scene.add((ring[k], ring[(k + 1) % 8], (x, 0.02, z)),
                           (206, 232, 236), shade=False, fog=False, bias=-1.3)
        # line from the rod tip
        hand, tip = self.rod_tip_world()
        self.scene.line(tip, (x, y + 0.03, z), (236, 236, 230), 1)
        return (x, y, z)

    # -------------------------------------------------------------- public
    def draw(self, surf, mode="idle", depth=0.55, taut=0.0, fly=0.0):
        p = self.pal
        self.scene.fog = p["hor"]
        self.scene.ambient = 0.42 + 0.30 * p["ambient"]
        self.scene.fog_near = 6.0
        self.scene.fog_far = 74.0
        self.scene.light = vnorm((-0.35, 0.80, -0.55))

        self._sky(surf, p)

        sc = self.scene
        sc.add_mesh(self.ridge)
        sc.add_mesh(self.trees)
        self._add_water(p)
        sc.add_mesh(self.dock)
        sc.add_mesh(self.reeds)
        sc.add_mesh(self.beach)
        sc.add_mesh(self.pads)

        toke = getattr(self, "pipe_raise", 0) > 0
        if mode in ("idle", "fly", "bite", "reel") and not toke:
            self._add_float(depth, mode, taut, fly)
        self.apply_pov()
        if self.pov == "first":
            h, t = self.rod_tip_world()
            mid = (h[0] + (t[0] - h[0]) * 0.55, h[1] + (t[1] - h[1]) * 0.55,
                   h[2] + (t[2] - h[2]) * 0.55)
            self.scene.add_mesh(join_meshes([
                segment(h, mid, 0.030, (176, 148, 96), 4),
                segment(mid, t, 0.021, (198, 170, 116), 4)]),
                shade=True, fog=True)
        else:
            self._add_angler(p, mode)

        sc.draw(surf)

        # screen-space anchors for the HUD / particles
        f = self.cam.focal
        v = self.cam.view(self.bobber_world(depth))
        z = max(0.1, v[2])
        self._bobber_screen = (INTERNAL_W * 0.5 + v[0] * f / z,
                               INTERNAL_H * 0.5 - v[1] * f / z)
        hand, tip = self.rod_tip_world()
        v = self.cam.view(tip)
        z = max(0.1, v[2])
        self._tip_screen = (INTERNAL_W * 0.5 + v[0] * f / z,
                            INTERNAL_H * 0.5 - v[1] * f / z)

        self.particles.draw(surf)
        self._grade(surf, p)

    def _grade(self, surf, p):
        """Colour grade + the high haze, at internal resolution."""
        if self.high > 0.03:
            layer = pygame.Surface((INTERNAL_W, INTERNAL_H), pygame.SRCALPHA)
            layer.fill((255, 190, 130, int(12 * self.high)))
            surf.blit(layer, (0, 0))
        util.vignette(surf, strength=70 + int(30 * self.high), color=(6, 8, 14))
