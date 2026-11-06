"""UI toolkit: text helpers, buttons, icons and procedural fish art."""
from __future__ import annotations

import math
import random

import pygame

from . import config as C
from . import util
from .util import clamp, font, mix, lighten, darken, alpha_color, pulse, ease_out_cubic

# ------------------------------------------------------------------- text ----


def txt(surf, msg, pos, size=22, bold=False, color=C.CREAM, anchor="topleft",
        shadow=None, family="body", alpha=255):
    if shadow is None:
        shadow = (10, 12, 22)
    return util.text(surf, msg, pos, font(size, bold, family), color, anchor,
                     shadow=shadow, alpha=alpha)


def txt_plain(surf, msg, pos, size=22, bold=False, color=C.CREAM, anchor="topleft",
              family="body"):
    return util.text(surf, msg, pos, font(size, bold, family), color, anchor)


def chip(surf, label, pos, color, size=15, bg_alpha=38, anchor="topleft", pad=(9, 4)):
    fnt = font(size, True)
    tw, th = fnt.size(label)
    w, h = tw + pad[0] * 2, th + pad[1] * 2
    rect = pygame.Rect(0, 0, w, h)
    setattr(rect, anchor, pos)
    layer = pygame.Surface((w, h), pygame.SRCALPHA)
    pygame.draw.rect(layer, alpha_color(color, bg_alpha), (0, 0, w, h), border_radius=h // 2)
    pygame.draw.rect(layer, alpha_color(color, 150), (0, 0, w, h), width=1,
                     border_radius=h // 2)
    surf.blit(layer, rect.topleft)
    util.text(surf, label, (rect.centerx, rect.centery), fnt, color, "center")
    return rect


def keycap(surf, pos, label, size=18, color=C.CREAM):
    fnt = font(size, True)
    tw, th = fnt.size(label)
    w, h = max(tw + 18, 30), th + 10
    rect = pygame.Rect(pos[0], pos[1], w, h)
    pygame.draw.rect(surf, (18, 21, 33), rect, border_radius=7)
    pygame.draw.rect(surf, (86, 94, 122), rect, width=2, border_radius=7)
    util.text(surf, label, (rect.centerx, rect.centery + 1), fnt, color, "center")
    return rect.w


# ----------------------------------------------------------------- button ----


class Button:
    def __init__(self, rect, label, style="dark", size=22, sub=None, enabled=True,
                 action=None, radius=12, bold=True):
        self.rect = pygame.Rect(rect)
        self.label = label
        self.sub = sub
        self.style = style
        self.size = size
        self.enabled = enabled
        self.action = action
        self.radius = radius
        self.bold = bold
        self.hover = 0.0

    def hit(self, pos):
        return self.rect.collidepoint(pos)

    def update(self, mouse, dt):
        target = 1.0 if (self.enabled and self.hit(mouse)) else 0.0
        self.hover = util.approach(self.hover, target, 14.0, dt)

    def click(self, event, mouse):
        if (event.type == pygame.MOUSEBUTTONDOWN and event.button == 1 and self.enabled
                and self.hit(mouse)):
            return True
        return False

    def draw(self, surf, mouse=None):
        r = self.rect
        if not self.enabled:
            base, edge, fg = (34, 38, 52), (58, 62, 80), (104, 108, 126)
        elif self.style == "primary":
            base = mix(C.GOLD, C.CREAM, self.hover * 0.35)
            edge = C.GOLD_DEEP
            fg = (44, 32, 16)
        elif self.style == "danger":
            base = mix((156, 62, 58), C.RED, self.hover * 0.6)
            edge = (208, 92, 86)
            fg = C.CREAM
        elif self.style == "tab":
            base = mix((30, 34, 50), (54, 60, 84), self.hover)
            edge = (62, 70, 96)
            fg = C.CREAM
            if getattr(self, "selected", False):
                base, edge, fg = C.GOLD, C.GOLD_DEEP, (44, 32, 16)
        else:
            base = mix((30, 34, 50), (48, 54, 76), self.hover)
            edge = mix((62, 70, 96), (108, 118, 150), self.hover)
            fg = C.CREAM

        lift = -2 if (self.enabled and self.hover > 0.05) else 0
        rect = r.move(0, lift)
        if self.style in ("primary", "danger") and self.enabled:
            glow = pygame.Surface((rect.w + 18, rect.h + 18), pygame.SRCALPHA)
            pygame.draw.rect(glow, alpha_color(base, int(48 * self.hover)),
                             (0, 0, rect.w + 18, rect.h + 18), border_radius=self.radius + 6)
            surf.blit(glow, (rect.x - 9, rect.y - 9))
        pygame.draw.rect(surf, (14, 16, 26), rect.move(0, 3), border_radius=self.radius)
        pygame.draw.rect(surf, base, rect, border_radius=self.radius)
        pygame.draw.rect(surf, lighten(base, 0.30),
                         (rect.x + 3, rect.y + 2, rect.w - 6, max(2, rect.h // 3)),
                         border_radius=max(2, self.radius // 2))
        pygame.draw.line(surf, darken(base, 0.35), (rect.x + 3, rect.bottom - 4),
                         (rect.right - 3, rect.bottom - 4), 2)
        pygame.draw.rect(surf, edge, rect, width=2, border_radius=self.radius)
        if self.sub:
            util.text(surf, self.label, (rect.centerx, rect.centery - 9),
                      font(self.size, self.bold), fg, "center")
            util.text(surf, self.sub, (rect.centerx, rect.centery + 12),
                      font(max(12, self.size - 6)), fg, "center", alpha=180)
        else:
            util.text(surf, self.label, (rect.centerx, rect.centery + 1),
                      font(self.size, self.bold), fg, "center")
        return rect


# ------------------------------------------------------------------ icons ----


def coin(surf, pos, r=11, t=0.0):
    x, y = pos
    pygame.draw.circle(surf, (176, 122, 38), (x, y + 1), r)
    pygame.draw.circle(surf, (238, 186, 78), (x, y), r)
    pygame.draw.circle(surf, (255, 222, 140), (x, y), r - 3, 2)
    pygame.draw.line(surf, (255, 236, 180), (x - r * 0.35, y - r * 0.45),
                     (x + r * 0.1, y - r * 0.62), 2)


def leaf(surf, pos, r=11, color=(126, 194, 126), flip=False):
    x, y = pos
    s = -1 if flip else 1
    pts = [(x, y + r), (x + s * r * 0.85, y - r * 0.15), (x, y - r),
           (x - s * r * 0.85, y - r * 0.15)]
    pygame.draw.polygon(surf, color, pts)
    pygame.draw.polygon(surf, darken(color, 0.25), pts, 2)
    pygame.draw.line(surf, darken(color, 0.35), (x, y + r * 0.9), (x, y - r * 0.55), 2)


def bud(surf, pos, r=12, color=(150, 200, 138)):
    x, y = pos
    pygame.draw.circle(surf, color, (int(x - r * 0.42), int(y - r * 0.1)), int(r * 0.56))
    pygame.draw.circle(surf, lighten(color, 0.14), (int(x + r * 0.38), int(y - r * 0.28)),
                       int(r * 0.5))
    pygame.draw.circle(surf, darken(color, 0.12), (int(x + r * 0.05), int(y + r * 0.4)),
                       int(r * 0.5))
    pygame.draw.circle(surf, lighten(color, 0.3), (int(x - r * 0.3), int(y - r * 0.3)),
                       int(r * 0.2))


def fish_icon(surf, pos, size=18, color=C.CREAM):
    x, y = pos
    r = size * 0.5
    pygame.draw.ellipse(surf, color, (x - r, y - r * 0.6, r * 1.7, r * 1.2))
    pygame.draw.polygon(surf, color, [(x - r * 0.9, y), (x - r * 1.7, y - r * 0.75),
                                      (x - r * 1.7, y + r * 0.75)])
    pygame.draw.circle(surf, (30, 34, 48), (int(x + r * 0.45), int(y - r * 0.15)),
                       max(1, int(r * 0.16)))


def clock_icon(surf, pos, r=9, color=C.CREAM, hour=12.0):
    x, y = pos
    pygame.draw.circle(surf, color, (x, y), r, 2)
    a = (hour / 12.0) * math.tau - math.pi / 2
    pygame.draw.line(surf, color, (x, y), (x + math.cos(a) * r * 0.62,
                                           y + math.sin(a) * r * 0.62), 2)
    pygame.draw.line(surf, color, (x, y), (x, y - r * 0.4), 2)


# ------------------------------------------------------------------- fish ----


_FISH_CACHE = {}

_SHAPES = {
    #           rx,   ry
    "minnow": (0.32, 0.19),
    "torpedo": (0.33, 0.23),
    "round": (0.30, 0.30),
    "flat": (0.35, 0.26),
    "eel": (0.36, 0.10),
    "ray": (0.30, 0.24),
    "frog": (0.30, 0.28),
    "sturgeon": (0.35, 0.21),
    "angler": (0.29, 0.28),
    "weird": (0.28, 0.25),
    "cat": (0.32, 0.24),
    "gar": (0.37, 0.15),
    "paddle": (0.32, 0.21),
}


def _stable_hash(text):
    h = 2166136261
    for ch in text:
        h = ((h ^ ord(ch)) * 16777619) & 0xFFFFFFFF
    return h


def _eel_poly(cx, cy, rx, ry, n=24):
    """Smooth tapered polygon following a sine path (head to the right)."""
    path = []
    for i in range(n):
        t = i / (n - 1.0)
        x = cx - rx * 0.95 + rx * 1.9 * t
        y = cy + math.sin(t * 3.4 + 0.4) * ry * 0.85
        path.append((x, y))
    upper, lower = [], []
    for i, (x, y) in enumerate(path):
        t = i / (n - 1.0)
        if i == 0:
            dx, dy = path[1][0] - x, path[1][1] - y
        elif i == n - 1:
            dx, dy = x - path[-2][0], y - path[-2][1]
        else:
            dx, dy = path[i + 1][0] - path[i - 1][0], path[i + 1][1] - path[i - 1][1]
        length = math.hypot(dx, dy) or 1.0
        nx, ny = -dy / length, dx / length
        r = ry * (0.22 + 0.95 * (math.sin(math.pi * min(1.0, t * 1.03)) ** 0.55))
        upper.append((x + nx * r, y + ny * r))
        lower.append((x - nx * r, y - ny * r))
    return [(int(px), int(py)) for px, py in upper + lower[::-1]]


def _seed_points(sid, n, lo, hi):
    rng = random.Random(_stable_hash(sid) & 0xFFFF)
    return [(rng.uniform(lo, hi), rng.uniform(-1, 1)) for _ in range(n)]


def _paint_fish(surf, sp, ghost=False, dim=False):
    W, H = surf.get_size()
    cx, cy = W * 0.50, H * 0.55
    rxm, rym = _SHAPES.get(sp["shape"], (0.32, 0.24))
    rx, ry = W * rxm, H * rym

    body_c = sp["body"]
    fin_c = sp["fin"]
    acc_c = sp["accent"]
    if ghost:
        body_c = fin_c = (84, 94, 122)
        acc_c = (112, 124, 156)
    elif dim:
        body_c = mix(body_c, (40, 46, 64), 0.55)
        fin_c = mix(fin_c, (34, 40, 56), 0.55)
        acc_c = mix(acc_c, (60, 68, 90), 0.55)

    shape = sp["shape"]
    pattern = sp["pattern"]

    def ell(color, ex, ey, w, h, width=0):
        pygame.draw.ellipse(surf, color, (int(ex - w / 2), int(ey - h / 2),
                                          int(w), int(h)), width)

    # --- glow halo --------------------------------------------------------
    if pattern == "glow" and not ghost:
        for i in range(5, 0, -1):
            a = int(26 * (1 - i / 6.0) + 8)
            ell(alpha_color(acc_c, a), cx, cy, rx * 2 * (1 + i * 0.28),
                ry * 2 * (1 + i * 0.5))

    # --- tail -------------------------------------------------------------
    if shape == "eel":
        tail = [(cx - rx * 0.86, cy - ry * 0.7), (cx - rx * 1.75, cy - ry * 0.35),
                (cx - rx * 1.62, cy), (cx - rx * 1.75, cy + ry * 0.35),
                (cx - rx * 0.86, cy + ry * 0.7)]
    elif shape == "ray":
        tail = [(cx - rx * 0.7, cy), (cx - rx * 1.9, cy), (cx - rx * 0.7, cy + ry * 0.12)]
        tail = [(cx - rx * 0.72, cy - ry * 0.24), (cx - rx * 2.0, cy - ry * 0.05),
                (cx - rx * 0.72, cy + ry * 0.24)]
    else:
        tail = [(cx - rx * 0.90, cy), (cx - rx * 1.52, cy - ry * 1.05),
                (cx - rx * 1.28, cy), (cx - rx * 1.52, cy + ry * 1.05)]
    pygame.draw.polygon(surf, fin_c, [(int(x), int(y)) for x, y in tail])
    if not ghost:
        pygame.draw.polygon(surf, darken(fin_c, 0.28),
                            [(int(x), int(y)) for x, y in tail], 2)

    # --- ray wings --------------------------------------------------------
    if shape == "ray":
        wing_top = [(cx - rx * 0.45, cy - ry * 0.55), (cx + rx * 0.35, cy - ry * 2.25),
                    (cx + rx * 0.95, cy - ry * 0.35)]
        wing_bot = [(cx - rx * 0.45, cy + ry * 0.55), (cx + rx * 0.35, cy + ry * 2.25),
                    (cx + rx * 0.95, cy + ry * 0.35)]
        for w in (wing_top, wing_bot):
            pygame.draw.polygon(surf, fin_c, [(int(x), int(y)) for x, y in w])
            if not ghost:
                pygame.draw.polygon(surf, darken(fin_c, 0.25),
                                    [(int(x), int(y)) for x, y in w], 2)

    # --- dorsal + pectoral fins ------------------------------------------
    if shape in ("round", "torpedo", "sturgeon", "flat", "minnow", "frog"):
        dh = 0.55 if shape == "minnow" else 1.15
        dorsal = [(cx - rx * 0.42, cy - ry * 0.72), (cx - rx * 0.08, cy - ry * (0.72 + dh)),
                  (cx + rx * 0.30, cy - ry * 0.80)]
        pygame.draw.polygon(surf, fin_c, [(int(x), int(y)) for x, y in dorsal])
        if not ghost:
            pygame.draw.polygon(surf, darken(fin_c, 0.25),
                                [(int(x), int(y)) for x, y in dorsal], 2)
    if shape == "cat":
        dorsal = [(cx - rx * 0.45, cy - ry * 0.80), (cx - rx * 0.30, cy - ry * 1.25),
                  (cx - rx * 0.05, cy - ry * 0.85)]
        pygame.draw.polygon(surf, fin_c, [(int(x), int(y)) for x, y in dorsal])
    if shape in ("gar", "paddle"):
        dorsal = [(cx - rx * 0.72, cy - ry * 0.75), (cx - rx * 0.58, cy - ry * 1.55),
                  (cx - rx * 0.28, cy - ry * 0.80)]
        pygame.draw.polygon(surf, fin_c, [(int(x), int(y)) for x, y in dorsal])
    if shape in ("eel",):
        dorsal = [(cx - rx * 0.75, cy - ry * 1.2), (cx + rx * 0.5, cy - ry * 1.55),
                  (cx + rx * 0.85, cy - ry * 0.85)]
        pygame.draw.polygon(surf, fin_c, [(int(x), int(y)) for x, y in dorsal])

    # --- body -------------------------------------------------------------
    if shape == "eel":
        poly = _eel_poly(cx, cy, rx, ry)
        pygame.draw.polygon(surf, body_c, poly)
        pygame.draw.polygon(surf, darken(body_c, 0.38), poly, 2)
    else:
        ell(body_c, cx, cy, rx * 2, ry * 2)
        if shape in ("torpedo", "sturgeon"):
            snout = [(cx + rx * 0.72, cy - ry * 0.86), (cx + rx * 1.30, cy),
                     (cx + rx * 0.72, cy + ry * 0.86)]
            pygame.draw.polygon(surf, body_c, [(int(x), int(y)) for x, y in snout])
        if shape == "angler":
            snout = [(cx + rx * 0.60, cy - ry * 0.95), (cx + rx * 1.12, cy + ry * 0.1),
                     (cx + rx * 0.62, cy + ry * 0.95)]
            pygame.draw.polygon(surf, body_c, [(int(x), int(y)) for x, y in snout])
        if shape == "cat":
            ell(body_c, cx + rx * 0.52, cy - ry * 0.02, rx * 1.10, ry * 1.80)
        if shape == "gar":
            snout = [(cx + rx * 0.60, cy - ry * 0.55), (cx + rx * 1.86, cy - ry * 0.09),
                     (cx + rx * 1.86, cy + ry * 0.09), (cx + rx * 0.60, cy + ry * 0.55)]
            pygame.draw.polygon(surf, body_c, [(int(x), int(y)) for x, y in snout])
            pygame.draw.lines(surf, darken(body_c, 0.35), True,
                              [(int(x), int(y)) for x, y in snout], 2)
        if shape == "paddle":
            snout = [(cx + rx * 0.68, cy - ry * 0.78), (cx + rx * 1.92, cy - ry * 0.34),
                     (cx + rx * 2.06, cy - ry * 0.06), (cx + rx * 2.06, cy + ry * 0.06),
                     (cx + rx * 1.92, cy + ry * 0.34), (cx + rx * 0.68, cy + ry * 0.78)]
            pygame.draw.polygon(surf, body_c, [(int(x), int(y)) for x, y in snout])
            pygame.draw.lines(surf, darken(body_c, 0.35), True,
                              [(int(x), int(y)) for x, y in snout], 2)
        if shape == "frog":
            for sy in (-1, 1):
                leg = [(cx - rx * 0.55, cy + sy * ry * 0.55),
                       (cx - rx * 1.05, cy + sy * ry * 1.55),
                       (cx - rx * 0.15, cy + sy * ry * 1.05)]
                pygame.draw.polygon(surf, fin_c, [(int(x), int(y)) for x, y in leg])

    # --- pectoral fin -----------------------------------------------------
    if shape not in ("eel", "ray") and not ghost:
        pec = [(cx + rx * 0.02, cy + ry * 0.42), (cx - rx * 0.30, cy + ry * 1.30),
               (cx + rx * 0.34, cy + ry * 0.62)]
        pygame.draw.polygon(surf, alpha_color(lighten(fin_c, 0.1), 210),
                            [(int(x), int(y)) for x, y in pec])

    # --- catfish barbels ---------------------------------------------------
    if shape == "cat" and not ghost:
        for dy, ln in ((-0.26, 0.44), (-0.08, 0.50), (0.10, 0.50), (0.28, 0.42)):
            x0 = cx + rx * 0.92
            y0 = cy + ry * dy
            pts = [(x0, y0),
                   (x0 + rx * ln * 0.55, y0 + ry * (0.16 + dy * 0.45)),
                   (x0 + rx * ln, y0 + ry * (0.40 + dy * 0.55))]
            pygame.draw.lines(surf, darken(fin_c, 0.25), False,
                              [(int(a), int(b)) for a, b in pts], 2)

    # --- patterns (masked to the body) ------------------------------------
    if not ghost and pattern != "none":
        mask = pygame.Surface(surf.get_size(), pygame.SRCALPHA)
        if shape == "eel":
            pygame.draw.polygon(mask, (255, 255, 255, 255), _eel_poly(cx, cy, rx, ry))
        else:
            pygame.draw.ellipse(mask, (255, 255, 255, 255),
                                (int(cx - rx), int(cy - ry), int(rx * 2), int(ry * 2)))
            if shape in ("torpedo", "sturgeon", "angler"):
                pygame.draw.polygon(mask, (255, 255, 255, 255),
                                    [(int(cx + rx * 0.5), int(cy - ry * 0.9)),
                                     (int(cx + rx * 1.3), int(cy)),
                                     (int(cx + rx * 0.5), int(cy + ry * 0.9))])
        pat = pygame.Surface(surf.get_size(), pygame.SRCALPHA)
        if pattern == "stripes":
            for i in range(6):
                x = cx - rx * 0.7 + i * rx * 0.30
                pygame.draw.line(pat, alpha_color(acc_c, 120), (x, cy - ry),
                                 (x + rx * 0.10, cy + ry), max(2, int(rx * 0.14)))
        elif pattern == "bands":
            for i in range(3):
                x = cx - rx * 0.55 + i * rx * 0.55
                pygame.draw.line(pat, alpha_color(darken(acc_c, 0.15), 130), (x, cy - ry),
                                 (x, cy + ry), max(3, int(rx * 0.20)))
        elif pattern == "spots":
            for u, v in _seed_points(sp["id"], 11, -0.62, 0.62):
                pygame.draw.circle(pat, alpha_color(acc_c, 170),
                                   (int(cx + u * rx), int(cy + v * ry * 0.62)),
                                   max(1, int(rx * 0.10)))
        elif pattern == "lateral":
            h = max(3, int(ry * 0.52))
            pygame.draw.rect(pat, alpha_color(acc_c, 215),
                             (int(cx - rx), int(cy - ry * 0.24), int(rx * 2), h))
        elif pattern == "scales":
            for i in range(9):
                for j in range(4):
                    x = cx - rx * 0.86 + i * rx * 0.21
                    y = cy - ry * 0.66 + j * ry * 0.42
                    pygame.draw.arc(pat, alpha_color(acc_c, 120),
                                    (x, y, rx * 0.21, ry * 0.34), 0.2, 3.1, 1)
        elif pattern == "gradient":
            for i in range(24):
                t = i / 23.0
                col = mix(acc_c, fin_c, t)
                y0 = cy - ry + t * 2 * ry
                pygame.draw.line(pat, alpha_color(col, 120), (cx - rx, y0), (cx + rx, y0),
                                 max(2, int(ry * 0.12)))
        elif pattern == "glow":
            for u, v in _seed_points(sp["id"], 7, -0.55, 0.55):
                pygame.draw.circle(pat, alpha_color(acc_c, 120),
                                   (int(cx + u * rx), int(cy + v * ry * 0.6)),
                                   max(2, int(rx * 0.13)))
        pat.blit(mask, (0, 0), special_flags=pygame.BLEND_RGBA_MULT)
        surf.blit(pat, (0, 0))

    # --- belly shading ----------------------------------------------------
    if not ghost:
        sh = pygame.Surface(surf.get_size(), pygame.SRCALPHA)
        pygame.draw.ellipse(sh, alpha_color(darken(body_c, 0.5), 90),
                            (int(cx - rx * 0.95), int(cy + ry * 0.25),
                             int(rx * 1.9), int(ry * 1.5)))
        mask = pygame.Surface(surf.get_size(), pygame.SRCALPHA)
        pygame.draw.ellipse(mask, (255, 255, 255, 255),
                            (int(cx - rx), int(cy - ry), int(rx * 2), int(ry * 2)))
        sh.blit(mask, (0, 0), special_flags=pygame.BLEND_RGBA_MULT)
        surf.blit(sh, (0, 0))

    # --- outline ----------------------------------------------------------
    if not ghost and shape != "eel":
        pygame.draw.ellipse(surf, darken(body_c, 0.42),
                            (int(cx - rx), int(cy - ry), int(rx * 2), int(ry * 2)), 2)
    else:
        pygame.draw.ellipse(surf, (126, 140, 176),
                            (int(cx - rx), int(cy - ry), int(rx * 2), int(ry * 2)), 2)

    # --- gill + mouth -----------------------------------------------------
    if shape not in ("ray",):
        gx = cx + rx * 0.10
        pts = []
        for i in range(9):
            a = -1.05 + 2.1 * i / 8.0
            pts.append((gx + math.cos(a) * rx * 0.16, cy + math.sin(a) * ry * 0.72))
        pygame.draw.lines(surf, darken(body_c, 0.35), False,
                          [(int(x), int(y)) for x, y in pts], 2)
    if shape != "weird":
        mx, my = cx + rx * 0.80, cy + ry * 0.30
        pygame.draw.line(surf, darken(body_c, 0.5), (mx - rx * 0.12, my),
                         (mx + rx * 0.14, my + ry * 0.10), 2)

    # --- eye --------------------------------------------------------------
    ex = cx + rx * 0.52
    ey = cy - ry * 0.30
    er = max(2, min(rx, ry) * 0.30)
    if not ghost:
        pygame.draw.circle(surf, (250, 250, 245), (int(ex), int(ey)), int(er))
        pygame.draw.circle(surf, darken(body_c, 0.75), (int(ex + er * 0.16), int(ey)),
                           int(er * 0.58))
        pygame.draw.circle(surf, (255, 255, 255), (int(ex - er * 0.18), int(ey - er * 0.28)),
                           max(1, int(er * 0.24)))
    else:
        pygame.draw.circle(surf, (126, 140, 176), (int(ex), int(ey)), int(er), 2)

    # --- angler lure ------------------------------------------------------
    if shape == "angler":
        tip = (cx + rx * 0.85, cy - ry * 0.62)
        bulb = (cx + rx * 1.02, cy - ry * 1.48)
        pygame.draw.lines(surf, darken(body_c, 0.2), False,
                          [(int(tip[0]), int(tip[1])),
                           (int(cx + rx * 1.28), int(cy - ry * 1.18)),
                           (int(bulb[0]), int(bulb[1]))], 3)
        if not ghost:
            for i in range(4, 0, -1):
                pygame.draw.circle(surf, alpha_color(acc_c, 24), (int(bulb[0]), int(bulb[1])),
                                   int(er * 0.55 * i))
            pygame.draw.circle(surf, acc_c, (int(bulb[0]), int(bulb[1])), int(er * 0.5))

    # --- weird tendrils ---------------------------------------------------
    if shape == "weird":
        for i in range(4):
            x0 = cx - rx * 0.6 + i * rx * 0.42
            pts = []
            for k in range(7):
                t = k / 6.0
                pts.append((x0 + math.sin(t * 3.0 + i) * rx * 0.18,
                            cy + ry * 0.8 + t * ry * 1.5))
            pygame.draw.lines(surf, alpha_color(acc_c, 120), False,
                              [(int(x), int(y)) for x, y in pts], 2)


def fish_image(sp, w, h, ghost=False, dim=False):
    from . import fish3d
    w, h = max(12, int(w)), max(12, int(h))
    key = (sp["id"], w, h, ghost, dim)
    img = _FISH_CACHE.get(key)
    if img is None:
        src = fish3d.fish_surface(sp, ghost)
        if w <= src.get_width():
            img = pygame.transform.smoothscale(src, (w, h))
        else:
            img = pygame.transform.scale(src, (w, h))
        if dim:
            img = img.copy()
            dark = pygame.Surface((w, h), pygame.SRCALPHA)
            dark.fill((16, 20, 30, 120))
            img.blit(dark, (0, 0))
        if len(_FISH_CACHE) > 120:
            _FISH_CACHE.clear()
        _FISH_CACHE[key] = img
    return img


def draw_fish(surf, rect, sp, ghost=False, dim=False, flip=False):
    rect = pygame.Rect(rect)
    img = fish_image(sp, rect.w, rect.h, ghost, dim)
    if flip:
        img = pygame.transform.flip(img, True, False)
    surf.blit(img, rect.topleft)
    return rect


# ------------------------------------------------------------ chill meter ----


def chill_meter(surf, rect, player, t):
    rect = pygame.Rect(rect)
    frac = player.high
    layer = pygame.Surface(rect.size, pygame.SRCALPHA)
    pygame.draw.rect(layer, (0, 0, 0, 110), layer.get_rect(), border_radius=rect.h // 2)
    if frac > 0:
        w = max(rect.h, int(rect.w * frac))
        col = mix((132, 204, 132), (198, 132, 246), frac)
        pygame.draw.rect(layer, col, (0, 0, w, rect.h), border_radius=rect.h // 2)
        if frac > 0.75:
            a = int(60 + 40 * pulse(t, 7.0))
            pygame.draw.rect(layer, alpha_color((255, 255, 255), a), (0, 0, w, rect.h // 2),
                             border_radius=rect.h // 2)
    pygame.draw.rect(layer, (255, 255, 255, 40), layer.get_rect(), width=1,
                     border_radius=rect.h // 2)
    surf.blit(layer, rect.topleft)
    return rect
