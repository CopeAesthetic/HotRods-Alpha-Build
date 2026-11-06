"""Small helpers: maths, colours, fonts, panels and particles."""
from __future__ import annotations

import math
import random
import time

import pygame

# A 32-bit surface with no alpha channel (see app.py for why).
_OPAQUE_MASKS = (0xFF0000, 0xFF00, 0xFF, 0)

# ---------------------------------------------------------------- maths -----


def clamp(v, lo=0.0, hi=1.0):
    return lo if v < lo else hi if v > hi else v


def lerp(a, b, t):
    return a + (b - a) * t


def inv_lerp(a, b, v):
    if b == a:
        return 0.0
    return (v - a) / (b - a)


def remap(v, a, b, c, d):
    return lerp(c, d, clamp(inv_lerp(a, b, v)))


def smoothstep(t):
    t = clamp(t)
    return t * t * (3.0 - 2.0 * t)


def ease_out_cubic(t):
    t = clamp(t) - 1.0
    return t * t * t + 1.0


def ease_in_cubic(t):
    t = clamp(t)
    return t * t * t


def ease_out_back(t, s=1.9):
    t = clamp(t) - 1.0
    return t * t * ((s + 1.0) * t + s) + 1.0


def pulse(t, speed=6.0):
    return 0.5 + 0.5 * math.sin(t * speed)


def approach(value, target, rate, dt):
    """Frame-rate independent exponential approach."""
    return target + (value - target) * math.exp(-rate * dt)


def damp(a, b, rate, dt):
    return approach(a, b, rate, dt)


def wrap_angle(a):
    while a > math.pi:
        a -= 2 * math.pi
    while a < -math.pi:
        a += 2 * math.pi
    return a


def chance(p):
    return random.random() < p


def randf(a, b):
    return random.uniform(a, b)


def randi(a, b):
    return random.randint(a, b)


def pick(seq):
    return random.choice(seq)


# --------------------------------------------------------------- colours ----


def mix(c1, c2, t):
    t = clamp(t)
    return (
        int(c1[0] + (c2[0] - c1[0]) * t),
        int(c1[1] + (c2[1] - c1[1]) * t),
        int(c1[2] + (c2[2] - c1[2]) * t),
    )


def lighten(c, t):
    return mix(c, (255, 255, 255), t)


def darken(c, t):
    return mix(c, (0, 0, 0), t)


def alpha(c, a):
    return (c[0], c[1], c[2], int(clamp(a, 0, 255)))


def with_alpha(c, a):
    return (c[0], c[1], c[2], int(clamp(a, 0, 255)))


def hsv(h, s, v):
    """h in 0..360, s/v in 0..1 -> rgb tuple."""
    h = h % 360.0
    c = v * s
    x = c * (1 - abs((h / 60.0) % 2 - 1))
    m = v - c
    if h < 60:
        r, g, b = c, x, 0
    elif h < 120:
        r, g, b = x, c, 0
    elif h < 180:
        r, g, b = 0, c, x
    elif h < 240:
        r, g, b = 0, x, c
    elif h < 300:
        r, g, b = x, 0, c
    else:
        r, g, b = c, 0, x
    return (int((r + m) * 255), int((g + m) * 255), int((b + m) * 255))


# ----------------------------------------------------------------- fonts ----

_FAMILIES = {
    "body": "Avenir Next,AvenirNext,Helvetica Neue,Helvetica,Verdana,Arial",
    "title": "Futura,Avenir Next,Helvetica Neue,Verdana",
    "mono": "Menlo,Courier New,Courier,DejaVu Sans Mono",
}
_FONT_CACHE = {}


def font(size, bold=False, family="body"):
    key = (family, int(size), bool(bold))
    f = _FONT_CACHE.get(key)
    if f is None:
        f = pygame.font.SysFont(_FAMILIES.get(family, family), int(size), bold=bold)
        _FONT_CACHE[key] = f
    return f


_TEXT_CACHE = {}
_TEXT_CACHE_MAX = 900


def render(text, fnt, color):
    key = (text, id(fnt), color)
    surf = _TEXT_CACHE.get(key)
    if surf is None:
        surf = fnt.render(text, True, color)
        if len(_TEXT_CACHE) > _TEXT_CACHE_MAX:
            _TEXT_CACHE.clear()
        _TEXT_CACHE[key] = surf
    return surf


def _fade(img, a):
    """Scale a surface's per-pixel alpha without touching its colours."""
    out = img.copy()
    out.fill((255, 255, 255, int(clamp(a, 0, 255))), special_flags=pygame.BLEND_RGBA_MULT)
    return out


def text(surf, msg, pos, fnt, color, anchor="topleft", shadow=None, shadow_off=(1, 2),
         alpha=255):
    """Draw text with an anchor. Returns the blit rect."""
    if msg is None:
        return pygame.Rect(pos, (0, 0))
    msg = str(msg)
    img = render(msg, fnt, color)
    rect = img.get_rect(**{anchor: pos})
    if shadow is not None:
        sh = render(msg, fnt, shadow)
        if alpha < 255:
            sh = _fade(sh, alpha)
        surf.blit(sh, rect.move(*shadow_off))
    if alpha < 255:
        img = _fade(img, alpha)
    surf.blit(img, rect)
    return rect


def text_size(msg, fnt):
    return fnt.size(str(msg))


def wrap_text(msg, fnt, width):
    """Greedy word wrap -> list of lines."""
    words = str(msg).split(" ")
    lines = []
    cur = ""
    for w in words:
        trial = w if not cur else cur + " " + w
        if fnt.size(trial)[0] <= width:
            cur = trial
        else:
            if cur:
                lines.append(cur)
            cur = w
    if cur:
        lines.append(cur)
    return lines


def text_wrapped(surf, msg, pos, fnt, color, width, line_gap=4, align="left",
                 shadow=None, shadow_off=(1, 2), max_lines=None):
    lines = wrap_text(msg, fnt, width)
    if max_lines is not None and len(lines) > max_lines:
        lines = lines[:max_lines]
        if lines:
            lines[-1] = lines[-1] + "..."
    x, y = pos
    for ln in lines:
        if align == "center":
            r = pygame.Rect(0, 0, width, fnt.get_height())
            r.midtop = (x + width // 2, y)
            text(surf, ln, r.topleft, fnt, color, shadow=shadow, shadow_off=shadow_off)
        elif align == "right":
            r = pygame.Rect(0, 0, width, fnt.get_height())
            r.topright = (x + width, y)
            text(surf, ln, r.topleft, fnt, color, shadow=shadow, shadow_off=shadow_off)
        else:
            text(surf, ln, (x, y), fnt, color, shadow=shadow, shadow_off=shadow_off)
        y += fnt.get_height() + line_gap
    return y - pos[1]


def truncate(msg, fnt, width):
    msg = str(msg)
    if fnt.size(msg)[0] <= width:
        return msg
    out = msg
    while out and fnt.size(out + "...")[0] > width:
        out = out[:-1]
    return out + "..."


# -------------------------------------------------------------- drawing -----

_PANEL_CACHE = {}
_GRAD_CACHE = {}


def panel(surf, rect, fill, radius=16, border=None, border_w=2, alpha=232,
          shadow=True, shadow_off=(0, 6), shadow_alpha=70):
    """Translucent rounded panel, cached by geometry."""
    rect = pygame.Rect(rect)
    w, h = rect.size
    if w <= 0 or h <= 0:
        return
    key = (w, h, radius, fill, border, border_w, alpha)
    surf_p = _PANEL_CACHE.get(key)
    if surf_p is None:
        surf_p = pygame.Surface((w + 4, h + 4), pygame.SRCALPHA)
        body = pygame.Rect(2, 2, w, h)
        pygame.draw.rect(surf_p, alpha_color(fill, alpha), body, border_radius=radius)
        if border is not None:
            pygame.draw.rect(surf_p, border, body, width=border_w, border_radius=radius)
        if len(_PANEL_CACHE) > 160:
            _PANEL_CACHE.clear()
        _PANEL_CACHE[key] = surf_p
    if shadow:
        sh = pygame.Surface((w + 30, h + 30), pygame.SRCALPHA)
        pygame.draw.rect(sh, (0, 0, 0, shadow_alpha), (12, 12, w, h), border_radius=radius + 2)
        surf.blit(sh, (rect.x - 15 + shadow_off[0], rect.y - 15 + shadow_off[1]))
    surf.blit(surf_p, (rect.x - 2, rect.y - 2))


def alpha_color(c, a):
    return (c[0], c[1], c[2], int(clamp(a, 0, 255)))


def hgrad(w, h, left, right):
    key = ("h", w, h, left, right)
    s = _GRAD_CACHE.get(key)
    if s is None:
        s = pygame.Surface((w, max(2, h)))
        for x in range(w):
            pygame.draw.line(s, mix(left, right, x / max(1, w - 1)), (x, 0), (x, h))
        if len(_GRAD_CACHE) > 40:
            _GRAD_CACHE.clear()
        _GRAD_CACHE[key] = s
    return s


def vgrad(w, h, top, bottom, stops=None):
    """Vertical gradient. `stops` may be a list of (t, colour) for multi-stop."""
    key = ("v", w, h, top, bottom, tuple(stops) if stops else None)
    s = _GRAD_CACHE.get(key)
    if s is None:
        small_h = 96
        s_small = pygame.Surface((2, small_h))
        for y in range(small_h):
            t = y / (small_h - 1)
            if stops:
                c = stops[0][1]
                for i in range(len(stops) - 1):
                    t0, c0 = stops[i]
                    t1, c1 = stops[i + 1]
                    if t0 <= t <= t1:
                        c = mix(c0, c1, inv_lerp(t0, t1, t))
                        break
                else:
                    c = stops[-1][1]
            else:
                c = mix(top, bottom, t)
            pygame.draw.line(s_small, c, (0, y), (1, y))
        s = pygame.transform.smoothscale(s_small, (w, h))
        if len(_GRAD_CACHE) > 40:
            _GRAD_CACHE.clear()
        _GRAD_CACHE[key] = s
    return s


def bar(surf, rect, frac, color, bg=(0, 0, 0, 120), radius=6, border=None):
    rect = pygame.Rect(rect)
    frac = clamp(frac)
    layer = pygame.Surface(rect.size, pygame.SRCALPHA)
    pygame.draw.rect(layer, alpha_color(bg, bg[3] if len(bg) > 3 else 120), layer.get_rect(),
                     border_radius=radius)
    if frac > 0:
        fw = max(radius * 2, int(rect.w * frac))
        pygame.draw.rect(layer, color[:3], (0, 0, fw, rect.h), border_radius=radius)
    if border:
        pygame.draw.rect(layer, border, layer.get_rect(), width=2, border_radius=radius)
    surf.blit(layer, rect.topleft)


def glow_circle(surf, pos, radius, color, strength=140):
    """Cheap additive glow using a few concentric circles."""
    size = radius * 2 + 4
    s = pygame.Surface((size, size), pygame.SRCALPHA)
    for i in range(6, 0, -1):
        r = int(radius * i / 6.0)
        a = int(strength * (1.0 - i / 7.0) ** 1.4)
        pygame.draw.circle(s, alpha_color(color, a), (size // 2, size // 2), r)
    surf.blit(s, (pos[0] - size // 2, pos[1] - size // 2))


def vignette(surf, strength=90, color=(0, 0, 0)):
    """Darkened edges using cached corner gradients."""
    w, h = surf.get_size()
    key = ("vig", w, h, strength, color)
    s = _GRAD_CACHE.get(key)
    if s is None:
        s = pygame.Surface((w, h), pygame.SRCALPHA)
        band = 170
        for i in range(band):
            a = int(strength * (1 - i / band) ** 2)
            if a <= 0:
                continue
            pygame.draw.rect(s, alpha_color(color, a), (i, i, w - 2 * i, h - 2 * i), width=1)
        if len(_GRAD_CACHE) > 40:
            _GRAD_CACHE.clear()
        _GRAD_CACHE[key] = s
    surf.blit(s, (0, 0))


_GLOW_CACHE = {}


def soft_glow(surf, pos, radius, color, strength=110, steps=8):
    """Smooth radial glow (cached, built small and scaled up)."""
    radius = int(radius)
    key = (radius, tuple(color), int(strength))
    glow = _GLOW_CACHE.get(key)
    if glow is None:
        size = 96
        small = pygame.Surface((size, size), pygame.SRCALPHA)
        c = size // 2
        for i in range(c, 0, -1):
            t = 1.0 - i / float(c)
            a = int(clamp(strength * (t ** 1.6), 0, 255))
            pygame.draw.circle(small, (color[0], color[1], color[2], a), (c, c), i)
        glow = pygame.transform.smoothscale(small, (radius * 2 + 2, radius * 2 + 2))
        if len(_GLOW_CACHE) > 60:
            _GLOW_CACHE.clear()
        _GLOW_CACHE[key] = glow
    surf.blit(glow, (pos[0] - radius - 1, pos[1] - radius - 1))


def circle_alpha(surf, pos, radius, color, a):
    layer = pygame.Surface((radius * 2, radius * 2), pygame.SRCALPHA)
    pygame.draw.circle(layer, alpha_color(color, a), (radius, radius), radius)
    surf.blit(layer, (pos[0] - radius, pos[1] - radius))


# ---------------------------------------------------------------- grain ----
_GRAIN = []


def grain(surf, strength=16, seed=0):
    """Film grain: a few cached noise tiles blitted at random offsets."""
    global _GRAIN
    if not _GRAIN:
        rng = random.Random(1234)
        for _ in range(4):
            t = pygame.Surface((192, 192), pygame.SRCALPHA)
            for _ in range(900):
                x, y = rng.randrange(192), rng.randrange(192)
                a = rng.randrange(18, 60)
                v = 255 if rng.random() < 0.5 else 0
                t.set_at((x, y), (v, v, v, a))
            _GRAIN.append(t)
    tile = _GRAIN[seed % len(_GRAIN)]
    w, h = surf.get_size()
    ox = int(time.time() * 37) % 192
    oy = int(time.time() * 53) % 192
    tile.set_alpha(strength)
    for y in range(-oy, h, 192):
        for x in range(-ox, w, 192):
            surf.blit(tile, (x, y))


# ------------------------------------------------------------ particles -----


class Particles:
    """Tiny particle pool used for splashes, ripples, motes and smoke."""

    __slots__ = ("items", "layer")

    def __init__(self):
        self.items = []
        self.layer = None

    def clear(self):
        self.items.clear()

    def add(self, x, y, vx, vy, life, color, size, kind="dot", grav=0.0, drag=0.0,
            grow=0.0, alpha=255):
        self.items.append([x, y, vx, vy, life, life, color, size, kind, grav, drag, grow, alpha])

    # --- recipes -----------------------------------------------------------
    def splash(self, x, y, n=16, color=(220, 240, 250), power=1.0):
        for _ in range(n):
            a = random.uniform(-math.pi * 0.92, -math.pi * 0.08)
            sp = random.uniform(40, 190) * power
            self.add(x, y, math.cos(a) * sp * 0.6, math.sin(a) * sp, random.uniform(0.3, 0.7),
                     color, random.uniform(1.6, 3.4), "dot", grav=620, drag=0.6)

    def ripple(self, x, y, r=8, grow=90, life=1.1, color=(220, 240, 250), alpha=110,
               squash=0.32):
        self.add(x, y, 0, 0, life, color, r, ("ring", squash), grow=grow, alpha=alpha)

    def bubbles(self, x, y, n=6, color=(210, 235, 245)):
        for _ in range(n):
            self.add(x + random.uniform(-12, 12), y, random.uniform(-8, 8),
                     random.uniform(-40, -18), random.uniform(0.5, 1.2), color,
                     random.uniform(1.5, 3.0), "dot", drag=0.9, alpha=150)

    def motes(self, x, y, n=1, color=(255, 236, 190)):
        for _ in range(n):
            self.add(x, y, random.uniform(-14, 14), random.uniform(-30, -8),
                     random.uniform(1.4, 2.6), color, random.uniform(1.6, 3.0), "soft",
                     drag=0.2, alpha=140)

    def smoke(self, x, y, n=8, color=(214, 220, 230), power=1.0):
        for _ in range(n):
            self.add(x + random.uniform(-6, 6), y + random.uniform(-4, 4),
                     random.uniform(-26, 26) * power, random.uniform(-46, -12) * power,
                     random.uniform(0.9, 1.8), color, random.uniform(5, 12), "soft",
                     drag=0.9, grow=10, alpha=120)

    def sparkle(self, x, y, n=10, color=(255, 226, 150)):
        for _ in range(n):
            a = random.uniform(0, math.tau)
            sp = random.uniform(30, 150)
            self.add(x, y, math.cos(a) * sp, math.sin(a) * sp, random.uniform(0.3, 0.8),
                     color, random.uniform(1.5, 3.0), "dot", drag=1.6)

    # --- simulation --------------------------------------------------------
    def update(self, dt):
        live = []
        for p in self.items:
            p[4] -= dt
            if p[4] <= 0:
                continue
            p[0] += p[2] * dt
            p[1] += p[3] * dt
            p[3] += p[9] * dt
            if p[10]:
                k = max(0.0, 1.0 - p[10] * dt)
                p[2] *= k
                p[3] *= k
            p[7] += p[11] * dt
            live.append(p)
        self.items = live

    def draw(self, surf):
        if not self.items:
            return
        if self.layer is None:
            self.layer = pygame.Surface(surf.get_size(), pygame.SRCALPHA)
        layer = self.layer
        layer.fill((0, 0, 0, 0))
        for p in self.items:
            t = p[4] / p[5]
            col = p[6]
            a = int(p[12] * (t ** 0.75))
            if a <= 2:
                continue
            x, y, size, kind = p[0], p[1], p[7], p[8]
            if isinstance(kind, tuple):
                _, squash = kind
                r = max(1, int(size))
                rect = pygame.Rect(0, 0, r * 2, max(1, int(r * 2 * squash)))
                rect.center = (int(x), int(y))
                pygame.draw.ellipse(layer, alpha_color(col, a), rect, 2)
            elif kind == "dot":
                pygame.draw.circle(layer, alpha_color(col, a), (int(x), int(y)),
                                   max(1, int(size)))
            elif kind == "soft":
                r = max(2, int(size))
                pygame.draw.circle(layer, alpha_color(col, int(a * 0.55)), (int(x), int(y)), r)
                pygame.draw.circle(layer, alpha_color(col, int(a * 0.3)), (int(x), int(y)),
                                   int(r * 0.6))
            elif kind == "ring":
                pygame.draw.circle(layer, alpha_color(col, a), (int(x), int(y)),
                                   max(1, int(size)), 2)
        surf.blit(layer, (0, 0))
