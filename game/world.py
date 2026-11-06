"""The lake: sky, water, dock, angler, wildlife and weather-grade effects."""
from __future__ import annotations

import math
import random

import pygame

from . import config as C
from . import data
from . import util
from .util import Particles, clamp, mix, lighten, darken, alpha_color, smoothstep

HORIZON = C.HORIZON


class World:
    def __init__(self, spot_index, seed=None):
        self.spot_index = spot_index
        self.spot = data.SPOTS[spot_index]
        rng = random.Random(seed if seed is not None else 1234 + spot_index * 77)
        self.rng = rng
        self.t = 0.0
        self.particles = Particles()
        self.high = 0.0
        self.hour = 12.0
        self.pal = self.palette(self.hour)

        self.rod_angle = -0.55
        self.rod_bend = 0.0
        self.rod_reach = 1.0

        # drifting clouds
        self.clouds = []
        for i in range(6):
            self.clouds.append([rng.uniform(0, C.W), rng.uniform(40, 250),
                                rng.uniform(0.6, 1.4), rng.uniform(3, 9), rng.uniform(0, 6)])

        # stars
        self.stars = [(rng.uniform(0, C.W), rng.uniform(0, HORIZON * 0.72),
                       rng.uniform(0.7, 2.0), rng.uniform(0, 6.28), rng.uniform(0.5, 1.0))
                      for _ in range(110)]

        # fish shadows under the water
        self.shadows = []
        for i in range(9):
            self.shadows.append(dict(
                depth=rng.uniform(0.05, 0.95), x=rng.uniform(0, C.W),
                speed=rng.uniform(8, 30) * rng.choice([-1, 1]),
                size=rng.uniform(0.5, 1.25), phase=rng.uniform(0, 6.28)))

        # lily pads / rocks / reeds
        self.lilies = []
        n_lily = {0: 7, 1: 4, 2: 9, 3: 0}.get(spot_index, 4)
        for i in range(n_lily):
            self.lilies.append([rng.uniform(0, C.W), rng.uniform(HORIZON + 40, C.H - 40),
                                rng.uniform(14, 30), rng.uniform(0, 6.28),
                                rng.uniform(0.4, 1.0)])

        self.reeds = []
        for side in (0, 1):
            for i in range(9):
                x = rng.uniform(0, 190) if side == 0 else rng.uniform(C.W - 150, C.W)
                self.reeds.append(dict(x=x, h=rng.uniform(130, 275), sway=rng.uniform(0, 6.28),
                                       lean=rng.uniform(-0.5, 0.5), w=rng.uniform(2.5, 5.0),
                                       seed=rng.random() < 0.4))

        self.fireflies = []
        for i in range(14):
            self.fireflies.append([rng.uniform(0, C.W), rng.uniform(HORIZON - 60, C.H - 60),
                                   rng.uniform(0, 6.28), rng.uniform(0.6, 1.6),
                                   rng.uniform(6, 20)])

        self._glow_layer = None
        self._fire_layer = None
        self.wave_phase = 0.0
        self.glitter_seed = rng.random() * 1000
        self._horizon_haze = 0

    # ------------------------------------------------------------- state --
    def update(self, dt, hour, high=0.0):
        self.t += dt
        self.hour = hour
        self.high = high
        self.wave_phase += dt * 0.55
        self.pal = self.palette(hour)
        for c in self.clouds:
            c[0] += c[3] * dt
            if c[0] > C.W + 200:
                c[0] = -200
        for s in self.shadows:
            s["x"] += s["speed"] * dt
            if s["x"] > C.W + 90:
                s["x"] = -90
            elif s["x"] < -90:
                s["x"] = C.W + 90
        for f in self.fireflies:
            f[0] += math.cos(f[2] + self.t * 0.4) * f[4] * dt
            f[1] += math.sin(f[2] * 1.3 + self.t * 0.5) * f[4] * 0.5 * dt
        self.particles.update(dt)

    # ------------------------------------------------------------ palette --
    def palette(self, hour=None):
        hour = self.hour if hour is None else hour
        p = C.sky_palette(hour)
        murk = self.spot["murk"]
        tint = self.spot["tint"]
        p["water_far"] = mix(p["water_far"], tint, murk)
        p["water_near"] = mix(p["water_near"], tint, murk * 0.75)
        p["hill_far"] = mix(p["hor"], mix((36, 48, 74), tint, 0.35), 0.42)
        p["hill_near"] = mix(p["hor"], mix((20, 30, 48), tint, 0.30), 0.72)
        p["tree"] = mix(p["hill_near"], (10, 14, 24), 0.55)
        amb = p["ambient"]
        p["dock"] = mix((96, 70, 48), (int(60 * amb), int(62 * amb), int(70 * amb)), 0.25)
        p["figure"] = (24, 26, 40)
        return p

    def water_color(self, y, p=None):
        p = p or self.pal
        t = clamp((y - HORIZON) / float(C.H - HORIZON))
        return mix(p["water_far"], p["water_near"], t ** 0.85)

    # -------------------------------------------------------------- back --
    def draw_back(self, surf):
        p = self.pal
        hour = self.hour
        q = round(hour * 8) / 8.0
        pq = self.palette(q)

        # sky
        sky_rect = pygame.Rect(0, 0, C.W, HORIZON + 2)
        grad = util.vgrad(C.W, HORIZON + 2, pq["top"], pq["hor"],
                          stops=[(0.0, pq["top"]), (0.52, pq["mid"]), (1.0, pq["hor"])])
        surf.blit(grad, (0, 0))
        del sky_rect

        # stars
        if p["stars"] > 0.03:
            for x, y, r, ph, br in self.stars:
                tw = 0.55 + 0.45 * math.sin(self.t * 1.6 + ph)
                a = p["stars"] * tw * br
                if a <= 0.04:
                    continue
                col = mix(pq["top"], (255, 250, 235), a)
                pygame.draw.circle(surf, col, (int(x), int(y)), int(r))

        # sun / moon
        cx, cy, kind = self._celestial(hour)
        if cy < HORIZON - 6:
            if kind == "sun":
                util.soft_glow(surf, (cx, cy), 150, pq["sun"], 60, steps=10)
                util.soft_glow(surf, (cx, cy), 52, lighten(pq["sun"], 0.3), 150, steps=6)
                pygame.draw.circle(surf, lighten(pq["sun"], 0.35), (int(cx), int(cy)), 30)
                pygame.draw.circle(surf, (255, 252, 240), (int(cx), int(cy)), 22)
            else:
                util.soft_glow(surf, (cx, cy), 110, (200, 214, 250), 46, steps=10)
                util.soft_glow(surf, (cx, cy), 34, (235, 240, 255), 90, steps=5)
                pygame.draw.circle(surf, (226, 232, 246), (int(cx), int(cy)), 22)
                pygame.draw.circle(surf, darken((226, 232, 246), 0.08),
                                   (int(cx - 7), int(cy - 6)), 5)
                pygame.draw.circle(surf, darken((226, 232, 246), 0.06),
                                   (int(cx + 6), int(cy + 7)), 3)

        # clouds
        cloud_base = mix(pq["mid"], (255, 255, 255), 0.42)
        if p["stars"] > 0.5:
            cloud_base = mix(pq["mid"], (10, 14, 30), 0.45)
        for x, y, s, spd, ph in self.clouds:
            self._cloud(surf, x, y, s, cloud_base, ph)

        # distant hills
        self._hills(surf, HORIZON - 4, 46, 0.9, pq["hill_far"], 0.0042)
        self._hills(surf, HORIZON + 2, 30, 2.1, pq["hill_near"], 0.0068)

        # treeline
        self._treeline(surf, pq)

        # water
        water_h = C.H - HORIZON
        wgrad = util.vgrad(C.W, water_h, pq["water_far"], pq["water_near"],
                           stops=[(0.0, pq["water_far"]),
                                  (0.35, mix(pq["water_far"], pq["water_near"], 0.55)),
                                  (1.0, pq["water_near"])])
        surf.blit(wgrad, (0, HORIZON))

        self._glitter(surf, pq, cx, kind)
        self._waves(surf, pq)
        self._shoreline(surf, pq)

    def _celestial(self, hour):
        if 5.4 <= hour <= 19.6:
            t = (hour - 5.4) / 14.2
            kind = "sun"
        else:
            t = ((hour - 19.6) % 24.0) / 9.8
            kind = "moon"
        t = clamp(t)
        x = 90 + t * 1100
        y = 330 - math.sin(math.pi * t) * 250
        return x, y, kind

    def _cloud(self, surf, x, y, s, color, ph):
        puffs = [(0, 0, 42, 20), (34, -9, 34, 17), (-36, 6, 30, 14), (12, 10, 30, 13),
                 (58, 4, 24, 11)]
        for i, (dx, dy, w, h) in enumerate(puffs):
            wob = math.sin(self.t * 0.35 + ph + i) * 3
            pygame.draw.ellipse(surf, color,
                                (int(x + dx * s - w * s / 2), int(y + dy * s - h * s / 2 + wob),
                                 int(w * s), int(h * s)))

    def _hills(self, surf, base_y, amp, phase, color, freq):
        pts = [(0, C.H)]
        x = 0
        while x <= C.W + 20:
            y = (base_y - amp * math.sin(x * freq + phase)
                 - amp * 0.55 * math.sin(x * freq * 2.7 + phase * 1.7)
                 - amp * 0.25 * math.sin(x * freq * 6.1 + phase))
            pts.append((x, y))
            x += 16
        pts.append((C.W, C.H))
        pygame.draw.polygon(surf, color, pts)

    def _treeline(self, surf, p):
        base = HORIZON + 4
        color = p["tree"]
        rng = random.Random(40 + self.spot_index * 13)
        marsh = self.spot_index == 2

        # a living forest band rather than a row of clones
        pts = [(0, base + 4)]
        x = 0.0
        while x <= C.W + 8:
            h = 24 + 15 * math.sin(x * 0.011 + 0.6) + 9 * math.sin(x * 0.039 + 1.9) \
                + 5 * math.sin(x * 0.098 + 0.3)
            if marsh:
                h *= 0.55
            pts.append((x, base - max(7.0, h)))
            x += 11
        pts.append((C.W + 8, base + 4))
        pygame.draw.polygon(surf, color, [(int(a), int(b)) for a, b in pts])

        # individual trees poking above the band
        for i in range(16):
            tx = rng.uniform(-30, C.W + 30)
            th = rng.uniform(34, 86) * (0.6 if marsh else 1.0)
            if marsh and i % 2:
                continue
            if i % 3 == 0:
                # conifer
                for k in range(3):
                    w = th * (0.42 - k * 0.09)
                    top = base - th + th * 0.24 * k
                    pygame.draw.polygon(surf, color, [
                        (int(tx - w), int(top + th * 0.30)),
                        (int(tx), int(top)),
                        (int(tx + w), int(top + th * 0.30))])
            else:
                trunk = th * 0.34
                pygame.draw.rect(surf, color, (int(tx - 2), int(base - trunk), 5,
                                               int(trunk)))
                for dx, dy, rr in ((0, 0, 0.30), (-0.22, 0.10, 0.22), (0.22, 0.12, 0.20)):
                    pygame.draw.circle(surf, color,
                                       (int(tx + dx * th), int(base - trunk - th * 0.12
                                                               - dy * th)),
                                       int(th * rr))

    def _waves(self, surf, p):
        y = HORIZON + 4
        i = 0
        while y < C.H + 20:
            depth = (y - HORIZON) / float(C.H - HORIZON)
            base = self.water_color(y, p)
            amp = 1.0 + depth * depth * 9.0
            speed = 0.5 + depth * 0.8
            col = mix(base, lighten(p["hor"], 0.55), 0.13 + 0.16 * (1 - depth))
            pts = []
            x = -20
            step = 24 if depth < 0.45 else 30
            while x <= C.W + 20:
                yy = (y + math.sin(x * 0.011 + self.t * speed + i * 0.7) * amp
                      + math.sin(x * 0.029 - self.t * speed * 1.3 + i) * amp * 0.4)
                pts.append((x, yy))
                x += step
            pygame.draw.lines(surf, col, False, pts, 2 if depth < 0.55 else 3)
            # a darker trough under each crest adds depth
            if depth > 0.3:
                dark = mix(base, (0, 0, 0), 0.14)
                pygame.draw.lines(surf, dark, False,
                                  [(int(px), int(py + 3 + depth * 4)) for px, py in pts], 2)
            y += 6 + depth * 30
            i += 1

    def _glitter(self, surf, p, sun_x, kind):
        if kind != "sun" and p["stars"] > 0.4:
            sun_x = sun_x
        elif kind != "sun":
            return
        rng = random.Random(int(self.glitter_seed) + int(self.t * 6))
        for i in range(26):
            depth = i / 25.0
            y = HORIZON + 8 + (depth ** 0.8) * (C.H - HORIZON - 20)
            spread = 26 + depth * 120
            x = sun_x + rng.uniform(-spread, spread) + math.sin(self.t * 1.2 + i) * 8
            w = rng.uniform(8, 30) * (0.7 + depth)
            col = mix(self.water_color(y, p), p["sun"], 0.42 + 0.32 * (1 - depth))
            pygame.draw.line(surf, col, (x - w / 2, y), (x + w / 2, y),
                             max(1, int(1 + depth * 3)))

    def _shoreline(self, surf, p):
        # thin bright line where the water meets the trees
        pygame.draw.line(surf, mix(p["hor"], (255, 255, 255), 0.25), (0, HORIZON),
                         (C.W, HORIZON), 2)
        # lily pads
        if not self.lilies:
            return
        for x, y, r, ph, alpha in self.lilies:
            drift = math.sin(self.t * 0.18 + ph) * 9
            px = x + drift
            py = y + math.sin(self.t * 0.5 + ph) * 1.5
            col = mix(self.water_color(py, p), (40, 78, 48), 0.62)
            pygame.draw.ellipse(surf, col, (int(px - r), int(py - r * 0.48),
                                            int(r * 2), int(r * 0.96)))
            pygame.draw.polygon(surf, self.water_color(py, p),
                                [(px, py), (px + r * 0.9, py - r * 0.16),
                                 (px + r * 0.9, py + r * 0.16)])
            pygame.draw.ellipse(surf, lighten(col, 0.18),
                                (int(px - r * 0.92), int(py - r * 0.44),
                                 int(r * 1.8), int(r * 0.88)), 2)

    # -------------------------------------------------------------- mid ---
    def draw_shadows(self, surf):
        p = self.pal
        for s in self.shadows:
            y = HORIZON + 44 + s["depth"] * (C.H - HORIZON - 80)
            wob = math.sin(self.t * 0.9 + s["phase"]) * 3
            w = 30 * s["size"] * (1 + s["depth"] * 0.7)
            h = w * 0.42
            col = mix(self.water_color(y, p), (4, 12, 20), 0.30)
            rect = pygame.Rect(0, 0, int(w), int(h))
            rect.center = (int(s["x"]), int(y + wob))
            pygame.draw.ellipse(surf, col, rect)
            tail = [(rect.left + 2, rect.centery),
                    (rect.left - w * 0.34, rect.centery - h * 0.55),
                    (rect.left - w * 0.34, rect.centery + h * 0.55)]
            pygame.draw.polygon(surf, col, [(int(a), int(b)) for a, b in tail])
            if self.high > 0.15:
                aura = mix(col, (150, 255, 220), 0.35 * self.high)
                pygame.draw.ellipse(surf, aura, rect.inflate(6, 6), 1)

    def bobber_pos(self, depth):
        depth = clamp(depth)
        x = 372 + depth * 748
        y = 682 - depth * 208
        scale = 1.0 - depth * 0.42
        return x, y, scale

    def rod_hand(self):
        return (318.0, 468.0)

    def rod_tip(self):
        bx, by = self.rod_hand()
        a = self.rod_angle
        length = 196 * self.rod_reach
        tx = bx + math.cos(a) * length
        ty = by + math.sin(a) * length
        ty += self.rod_bend * 46
        tx -= self.rod_bend * 22
        return tx, ty

    def _quad(self, a, b, bow, n=16):
        ax, ay = a
        bx, by = b
        mx, my = (ax + bx) / 2.0, (ay + by) / 2.0
        dx, dy = bx - ax, by - ay
        ln = math.hypot(dx, dy) or 1.0
        nx, ny = -dy / ln, dx / ln
        cx, cy = mx + nx * bow, my + ny * bow
        pts = []
        for i in range(n + 1):
            t = i / n
            x = (1 - t) ** 2 * ax + 2 * (1 - t) * t * cx + t * t * bx
            y = (1 - t) ** 2 * ay + 2 * (1 - t) * t * cy + t * t * by
            pts.append((x, y))
        return pts

    def draw_line(self, surf, tip, bobber, taut=0.0, slack=1.0):
        bow = (14 + 22 * slack) - 30 * taut
        pts = self._quad(tip, bobber, -bow, 18)
        pygame.draw.lines(surf, (222, 224, 230), False,
                          [(int(x), int(y)) for x, y in pts], 1)
        if self.high > 0.5:
            pygame.draw.lines(surf, mix((222, 224, 230), (255, 240, 200), self.high * 0.5),
                              False, [(int(x), int(y)) for x, y in pts], 1)

    def draw_float(self, surf, depth, mode="idle", t=None, sink=0.0):
        """mode: idle | fly | bite"""
        x, y, s = self.bobber_pos(depth)
        if mode == "bite":
            y += 6 * s
        self.draw_float_at(surf, (x, y), s)

    def draw_float_at(self, surf, pos, s=1.0):
        x, y = pos
        bob = math.sin(self.t * 2.6) * 1.6 * s
        y = y + bob
        r = 9 * s
        for i in range(2):
            ph = (self.t * 0.55 + i * 0.5) % 1.0
            rr = r * (1.6 + ph * 3.4)
            a = int(70 * (1 - ph))
            if a > 3:
                util.circle_alpha(surf, (x, y + r * 0.6), int(rr), (235, 245, 250), a)
        pygame.draw.ellipse(surf, (30, 34, 44), (int(x - r), int(y + r * 0.2),
                                                 int(r * 2), int(r * 0.7)))
        pygame.draw.circle(surf, (240, 240, 240), (int(x), int(y - r * 0.2)), int(r))
        pygame.draw.circle(surf, (228, 92, 84), (int(x), int(y - r * 1.05)), int(r * 0.62))
        pygame.draw.circle(surf, (255, 210, 200), (int(x - r * 0.3), int(y - r * 1.2)),
                           max(1, int(r * 0.18)))
        pygame.draw.circle(surf, (60, 60, 70), (int(x), int(y - r * 0.2)), int(r), 1)
        pygame.draw.line(surf, (235, 235, 235), (int(x), int(y - r * 1.6)),
                         (int(x), int(y - r * 2.6)), max(1, int(s)))

    def splash_at(self, depth, n=14):
        x, y, s = self.bobber_pos(depth)
        self.particles.splash(x, y, n, power=s)
        self.particles.ripple(x, y, r=6, grow=110, life=1.2)

    def ripple_at(self, depth, n=1):
        x, y, s = self.bobber_pos(depth)
        for i in range(n):
            self.particles.ripple(x, y, r=6 + i * 8, grow=90, life=1.3,
                                  alpha=120 - i * 30)

    # ------------------------------------------------------------- front --
    def draw_front(self, surf, bend=0.0, cast=0.0):
        p = self.pal
        self._dock(surf, p)
        self._angler(surf, p, bend, cast)
        self.particles.draw(surf)
        self._fireflies(surf, p)
        self._reeds(surf, p)
        if self.high > 0.02:
            self._high_layer(surf, p)
        util.vignette(surf, strength=76 + int(24 * self.high), color=(6, 8, 16))

    def _dock(self, surf, p):
        amb = p["ambient"]
        base = mix(mix((112, 84, 58), (34, 38, 52), 1.0 - amb), (24, 26, 38), 0.18)
        dark = darken(base, 0.35)
        top = C.DOCK_Y - 6
        # posts
        for x in (46, 152, 246, 330):
            w = 16 if x < 330 else 10
            pygame.draw.rect(surf, dark, (x - w // 2, top, w, 150))
            pygame.draw.rect(surf, darken(base, 0.15), (x - w // 2, top, w // 3, 150))
            pygame.draw.ellipse(surf, alpha_color((10, 14, 22), 90),
                                (x - w, top + 140, w * 2, 14))
        # deck
        pygame.draw.polygon(surf, base, [(-10, top), (336, top - 2), (344, top + 16),
                                         (-10, top + 20)])
        pygame.draw.polygon(surf, darken(base, 0.25), [(-10, top + 20), (344, top + 16),
                                                       (346, top + 28), (-10, top + 34)])
        for i in range(9):
            x = -10 + i * 46
            pygame.draw.line(surf, darken(base, 0.18), (x, top), (x + 4, top + 20), 2)
        pygame.draw.line(surf, lighten(base, 0.25), (-10, top + 1), (352, top - 1), 2)
        # cooler
        cx, cy = 96, top - 34
        pygame.draw.rect(surf, (188, 72, 62), (cx, cy, 46, 32), border_radius=4)
        pygame.draw.rect(surf, (232, 232, 226), (cx, cy - 6, 46, 10), border_radius=3)
        pygame.draw.line(surf, (150, 50, 44), (cx + 8, cy), (cx + 8, cy + 32), 2)
        # bucket
        pygame.draw.rect(surf, (120, 130, 140), (196, top - 26, 26, 24), border_radius=3)
        pygame.draw.line(surf, (90, 98, 108), (196, top - 26), (222, top - 26), 3)
        # lantern on a post
        lx, ly = 46, top - 58
        pygame.draw.line(surf, dark, (lx, top), (lx, ly + 12), 3)
        pygame.draw.rect(surf, (60, 64, 78), (lx - 8, ly, 16, 16), border_radius=3)
        if p["stars"] > 0.35:
            util.circle_alpha(surf, (lx, ly + 8), 26, (255, 210, 120), 40)
            pygame.draw.rect(surf, (255, 220, 140), (lx - 5, ly + 3, 10, 10), border_radius=2)

    def _angler(self, surf, p, bend, cast):
        hx, hy = self.rod_hand()
        fig = p["figure"]
        hip = (296.0, 504.0)
        shoulder = (286.0, 458.0)
        head = (289.0, 437.0)

        # legs dangling over the edge
        for i, (kx, ky, fx, fy) in enumerate(((320, 534, 334, 552),
                                              (306, 538, 318, 558))):
            pts = [(hip[0] - 4 + i * 6, hip[1] - 4), (kx, ky), (fx, fy)]
            pygame.draw.lines(surf, fig, False, [(int(a), int(b)) for a, b in pts], 8)
            pygame.draw.circle(surf, fig, (int(fx), int(fy)), 5)
        # torso
        pygame.draw.polygon(surf, fig, [
            (hip[0] - 15, hip[1] - 2), (shoulder[0] - 12, shoulder[1]),
            (shoulder[0] + 6, shoulder[1] - 6), (hip[0] + 12, hip[1] - 8),
            (hip[0] + 14, hip[1] + 6), (hip[0] - 10, hip[1] + 8)])
        # neck + head
        pygame.draw.circle(surf, fig, (int(head[0]), int(head[1])), 15)
        # bucket hat
        pygame.draw.ellipse(surf, (198, 176, 132), (int(head[0] - 22), int(head[1] - 14),
                                                    46, 12))
        pygame.draw.ellipse(surf, (178, 156, 112), (int(head[0] - 15), int(head[1] - 28),
                                                    32, 22))
        pygame.draw.ellipse(surf, (146, 124, 88), (int(head[0] - 15), int(head[1] - 28),
                                                   32, 22), 2)
        # arm reaching to the rod
        pygame.draw.lines(surf, fig, False, [(int(shoulder[0] + 2), int(shoulder[1] + 8)),
                                             (int(shoulder[0] + 20), int(shoulder[1] + 16)),
                                             (int(hx), int(hy))], 9)
        pygame.draw.circle(surf, fig, (int(hx), int(hy)), 6)

        # rod
        tip = self.rod_tip()
        ctrl_bow = 26 + bend * 34
        pts = self._quad((hx, hy), tip, -ctrl_bow + cast * 40, 14)
        pygame.draw.lines(surf, (28, 24, 22), False,
                          [(int(x), int(y)) for x, y in pts], 5)
        pygame.draw.lines(surf, (206, 176, 116), False,
                          [(int(x), int(y)) for x, y in pts], 3)
        pygame.draw.lines(surf, (238, 216, 168), False,
                          [(int(x), int(y)) for x, y in pts[:6]], 1)
        pygame.draw.circle(surf, (222, 196, 142), (int(tip[0]), int(tip[1])), 3)

    def _reeds(self, surf, p):
        col = mix(p["tree"], (10, 16, 18), 0.45)
        for r in self.reeds:
            sway = math.sin(self.t * 0.9 + r["sway"]) * 0.10 + r["lean"] * 0.10
            x0 = r["x"]
            base = C.H + 10
            pts = []
            for i in range(7):
                t = i / 6.0
                x = x0 + math.sin(sway * t * 1.4) * r["h"] * 0.35 * t + sway * 30 * t
                y = base - r["h"] * t
                pts.append((x, y))
            pygame.draw.lines(surf, col, False, [(int(a), int(b)) for a, b in pts],
                              int(r["w"]))
            for k in range(3):
                t = 0.35 + k * 0.2
                i = min(len(pts) - 1, int(t * len(pts)))
                bx, by = pts[i]
                dirn = 1 if (k % 2 == 0) else -1
                tipx = bx + dirn * (26 + k * 8) + sway * 40
                tipy = by - 22 - k * 6
                leaf = [(int(bx), int(by)),
                        (int(bx + dirn * 8), int(by - 20 - k * 3)),
                        (int(tipx), int(tipy)),
                        (int(bx + dirn * 14), int(by - 4))]
                pygame.draw.polygon(surf, col, leaf)
            if r["seed"]:
                pygame.draw.ellipse(surf, col, (int(pts[-1][0] - 5), int(pts[-1][1] - 12),
                                                10, 22))

    def _fireflies(self, surf, p):
        if p["stars"] < 0.28:
            return
        if self._fire_layer is None:
            self._fire_layer = pygame.Surface((C.W, C.H), pygame.SRCALPHA)
        layer = self._fire_layer
        layer.fill((0, 0, 0, 0))
        any_dot = False
        for x, y, ph, spd, amp in self.fireflies:
            blink = max(0.0, math.sin(self.t * spd + ph))
            if blink < 0.15:
                continue
            any_dot = True
            a = int(blink * p["stars"] * 150)
            pygame.draw.circle(layer, (255, 236, 160, a // 5), (int(x), int(y)), 6)
            pygame.draw.circle(layer, (255, 240, 180, a), (int(x), int(y)), 2)
        if any_dot:
            surf.blit(layer, (0, 0))

    def _high_layer(self, surf, p):
        h = self.high
        if self._glow_layer is None:
            self._glow_layer = pygame.Surface((C.W, C.H), pygame.SRCALPHA)
        layer = self._glow_layer
        layer.fill((0, 0, 0, 0))
        # pygame.draw replaces pixels instead of blending on SRCALPHA surfaces,
        # so the wash goes down first and the bands paint over it.
        pygame.draw.rect(layer, (255, 186, 128, int(7 * h)), (0, 0, C.W, C.H))
        for band in range(3):
            pts = [(0, C.H)]
            ph = self.t * 0.25 + band * 2.1
            base_y = 250 + band * 120
            for i in range(0, C.W + 40, 40):
                y = base_y + math.sin(i * 0.004 + ph) * 60 + math.sin(i * 0.011 - ph) * 22
                pts.append((i, y))
            pts.append((C.W, C.H))
            col = [(255, 210, 140), (210, 150, 240), (140, 230, 220)][band]
            pygame.draw.polygon(layer, alpha_color(col, int(16 + 26 * h)), pts)
        # drifting motes, on the same layer
        for i in range(int(10 + 22 * h)):
            ph = i * 1.7
            x = (i * 197 + self.t * (12 + i % 7 * 4)) % C.W
            y = (C.H * 0.45 + math.sin(self.t * 0.6 + ph) * 90 + i * 13) % C.H
            a = int(60 + 60 * math.sin(self.t * 1.3 + ph))
            if a > 10:
                r = 3 + i % 3
                pygame.draw.circle(layer, (255, 238, 190, max(4, a // 2)),
                                   (int(x), int(y)), r)

        surf.blit(layer, (0, 0))
