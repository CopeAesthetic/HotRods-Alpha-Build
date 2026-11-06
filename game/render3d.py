"""A tiny flat-shaded software 3D renderer - the PS2 look.

No libraries, no z-buffer: polygons are painter-sorted by depth and flat-shaded
from a single directional light, then fogged toward the horizon colour.
"""
from __future__ import annotations

import math

import pygame


# ---------------------------------------------------------------- vectors --
def vsub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def vadd(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def vmul(a, s):
    return (a[0] * s, a[1] * s, a[2] * s)


def vdot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def vcross(a, b):
    return (a[1] * b[2] - a[2] * b[1],
            a[2] * b[0] - a[0] * b[2],
            a[0] * b[1] - a[1] * b[0])


def vlen(a):
    return math.sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2])


def vnorm(a):
    length = vlen(a)
    if length < 1e-9:
        return (0.0, 1.0, 0.0)
    return (a[0] / length, a[1] / length, a[2] / length)


def vlerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t,
            a[2] + (b[2] - a[2]) * t)


def mix(c1, c2, t):
    t = 0.0 if t < 0 else 1.0 if t > 1 else t
    return (int(c1[0] + (c2[0] - c1[0]) * t),
            int(c1[1] + (c2[1] - c1[1]) * t),
            int(c1[2] + (c2[2] - c1[2]) * t))


# ----------------------------------------------------------------- camera --
class Camera:
    __slots__ = ("eye", "right", "up", "fwd", "focal")

    def __init__(self, eye, target, fov=58.0, height=360):
        self.eye = eye
        fwd = vnorm(vsub(target, eye))
        right = vnorm(vcross(fwd, (0.0, 1.0, 0.0)))
        up = vcross(right, fwd)
        self.fwd, self.right, self.up = fwd, right, up
        self.focal = (height * 0.5) / math.tan(math.radians(fov) * 0.5)

    def view(self, p):
        d = (p[0] - self.eye[0], p[1] - self.eye[1], p[2] - self.eye[2])
        return (d[0] * self.right[0] + d[1] * self.right[1] + d[2] * self.right[2],
                d[0] * self.up[0] + d[1] * self.up[1] + d[2] * self.up[2],
                -(d[0] * self.fwd[0] + d[1] * self.fwd[1] + d[2] * self.fwd[2]))


# ------------------------------------------------------------------ mesh ---
class Mesh:
    """World-space vertices plus coloured faces (index tuples)."""

    __slots__ = ("verts", "faces", "normals")

    def __init__(self, verts, faces):
        self.verts = list(verts)
        self.faces = faces          # [(idx_tuple, (r,g,b)), ...]
        self.normals = []
        for idx, _ in faces:
            a = self.verts[idx[0]]
            b = self.verts[idx[1]]
            c = self.verts[idx[2]]
            self.normals.append(vnorm(vcross(vsub(b, a), vsub(c, a))))


def box(cx, cy, cz, sx, sy, sz, top, side, front=None):
    """Axis-aligned box split into flat-shaded faces."""
    x0, x1 = cx - sx / 2, cx + sx / 2
    y0, y1 = cy - sy / 2, cy + sy / 2
    z0, z1 = cz - sz / 2, cz + sz / 2
    v = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
         (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
    f = [((0, 1, 2, 3), front or side),   # -z face
         ((5, 4, 7, 6), front or side),   # +z face
         ((4, 0, 3, 7), side),            # -x
         ((1, 5, 6, 2), side),            # +x
         ((3, 2, 6, 7), top),             # +y
         ((4, 5, 1, 0), side)]            # -y
    return Mesh(v, f)


def quad(p0, p1, p2, p3, color):
    return Mesh([p0, p1, p2, p3], [((0, 1, 2, 3), color)])


# ----------------------------------------------------------------- scene ---
class Scene:
    def __init__(self, w, h, cam: Camera):
        self.w = w
        self.h = h
        self.cam = cam
        self.polys = []
        self.lines = []
        self.light = vnorm((-0.35, 0.72, -0.6))
        self.wireframe = False
        self.ambient = 0.5
        self.fog = (40, 50, 70)
        self.fog_near = 12.0
        self.fog_far = 62.0

    # --- low level -------------------------------------------------------
    NEAR = -0.12

    def _project_v(self, v):
        z = -v[2]
        if z < 0.06:
            z = 0.06
        return (self.w * 0.5 + v[0] * self.cam.focal / z,
                self.h * 0.5 - v[1] * self.cam.focal / z)

    def _clip(self, vs):
        """Sutherland-Hodgman against the near plane (camera z <= NEAR)."""
        near = self.NEAR
        out = []
        n = len(vs)
        for i in range(n):
            a = vs[i]
            b = vs[(i + 1) % n]
            a_in = a[2] <= near
            b_in = b[2] <= near
            if a_in:
                out.append(a)
            if a_in != b_in:
                t = (near - a[2]) / (b[2] - a[2])
                out.append((a[0] + (b[0] - a[0]) * t,
                            a[1] + (b[1] - a[1]) * t, near))
        return out

    def _emit(self, vs, color, normal, shade, fog, bias=0.0):
        if len(vs) < 3:
            return
        if any(v[2] > self.NEAR for v in vs):
            vs = self._clip(vs)
            if len(vs) < 3:
                return
        depth = sum(-v[2] for v in vs) / len(vs) + bias
        pts = [self._project_v(v) for v in vs]
        if shade:
            k = self.ambient + (1.0 - self.ambient) * max(0.0, vdot(normal, self.light))
            color = (int(color[0] * k), int(color[1] * k), int(color[2] * k))
        if fog:
            t = (depth - self.fog_near) / max(1.0, self.fog_far - self.fog_near)
            color = mix(color, self.fog, t if t > 0 else 0.0)
        self.polys.append((depth, pts, color))

    def add(self, world_pts, color, shade=True, fog=True, normal=None, bias=0.0):
        if normal is None:
            a, b, c = world_pts[0], world_pts[1], world_pts[2]
            normal = vnorm(vcross(vsub(b, a), vsub(c, a)))
        vs = [self.cam.view(p) for p in world_pts]
        self._emit(vs, color, normal, shade, fog, bias)

    def add_mesh(self, mesh: Mesh, offset=(0.0, 0.0, 0.0), shade=True, fog=True,
                 tint=None, bias=0.0):
        cam = self.cam
        ox, oy, oz = offset
        view = []
        for p in mesh.verts:
            view.append(cam.view((p[0] + ox, p[1] + oy, p[2] + oz)))
        for i, (idx, color) in enumerate(mesh.faces):
            if tint:
                color = mix(color, tint, 0.35)
            vs = [view[k] for k in idx]
            self._emit(vs, color, mesh.normals[i], shade, fog, bias)

    def line(self, p0, p1, color, width=1):
        a = self._project_v(self.cam.view(p0))
        b = self._project_v(self.cam.view(p1))
        self.lines.append((color, a, b, width))

    # --- output ----------------------------------------------------------
    def draw(self, surf):
        self.polys.sort(key=lambda p: p[0], reverse=True)
        draw = pygame.draw.polygon
        if self.wireframe:
            for _, pts, color in self.polys:
                draw(surf, color, pts, 1)
        else:
            for _, pts, color in self.polys:
                draw(surf, color, pts)
        for color, a, b, width in self.lines:
            pygame.draw.line(surf, color, a, b, width)
        self.polys = []
        self.lines = []
