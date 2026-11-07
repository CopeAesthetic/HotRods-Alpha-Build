"""Headless Blender asset builder.

    /Applications/Blender.app/Contents/MacOS/Blender --background \
        --python tools/blender_assets.py

Writes low-poly props to assets/*.obj. Kept scripted so the pipeline is
reproducible (and could run in CI).
"""
import os
import sys

import bpy

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(HERE, "assets")


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def mesh_from(name, verts, faces):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    return ob


def boat():
    """A shallow rowboat: flat bottom, four angled sides, pointed bow."""
    v = [
        (-1.60, 0.00, -0.55), (1.60, 0.00, -0.55),
        (1.60, 0.00, 0.55), (-1.60, 0.00, 0.55),
        (-1.85, 0.22, -0.72), (1.95, 0.22, -0.72),
        (1.95, 0.22, 0.72), (-1.85, 0.22, 0.72),
        (2.25, 0.30, 0.00),
    ]
    f = [
        (0, 1, 2, 3),                     # floor
        (0, 4, 5, 1), (1, 5, 8), (1, 8, 2),
        (2, 8, 6), (2, 6, 7, 3), (3, 7, 4), (3, 4, 0),
        (5, 4, 7, 6), (6, 8, 5), (8, 6, 7),
    ]
    return mesh_from("boat", v, f)


def export(ob, name):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    path = os.path.join(OUT, name + ".obj")
    bpy.ops.wm.obj_export(filepath=path, export_selected_objects=True,
                          forward_axis="NEGATIVE_Z", up_axis="Y")
    print("wrote", path)


def tree(name="tree", height=3.4, spread=1.5):
    """Low-poly conifer: a trunk plus three stacked skirted cones."""
    v = [(-0.12, 0.0, -0.12), (0.12, 0.0, -0.12), (0.12, 0.0, 0.12), (-0.12, 0.0, 0.12)]
    v += [(0.12, height * 0.45, 0.12), (0.12, height * 0.45, -0.12),
          (-0.12, height * 0.45, -0.12), (-0.12, height * 0.45, 0.12)]
    f = [(0, 1, 2, 3), (4, 5, 1, 0), (5, 6, 2, 1), (6, 7, 3, 2), (7, 4, 0, 3)]
    base = len(v)
    for tier, (y, r) in enumerate(((height * 0.35, spread),
                                   (height * 0.62, spread * 0.72),
                                   (height * 0.84, spread * 0.45))):
        ring = [(r, y, 0.0), (0.0, y, r), (-r, y, 0.0), (0.0, y, -r)]
        apex = (0.0, y + height * 0.30, 0.0)
        v += ring + [apex]
        b = base + tier * 5
        f += [(b, b + 1, b + 4), (b + 1, b + 2, b + 4),
              (b + 2, b + 3, b + 4), (b + 3, b, b + 4)]
    return mesh_from(name, v, f)


def reeds():
    """A clump of solid blades - prisms, so they read from any angle."""
    import math as _m
    v, f = [], []
    for i in range(5):
        a = _m.tau * i / 5
        r = 0.10 + 0.06 * (i % 3)
        x, z = _m.cos(a) * r, _m.sin(a) * r
        h = 0.85 + 0.5 * ((i * 7) % 5) / 5.0
        w, d = 0.045, 0.045
        b = len(v)
        v += [(x - w, 0.0, z - d), (x + w, 0.0, z - d),
              (x + w, 0.0, z + d), (x - w, 0.0, z + d),
              (x - w, h, z - d), (x + w, h, z - d),
              (x + w, h, z + d), (x - w, h, z + d)]
        f += [(b + 1, b + 2, b + 6, b + 5), (b + 2, b + 3, b + 7, b + 6),
              (b + 3, b, b + 4, b + 7)]          # sides only - no caps
    return mesh_from("reeds", v, f)


def lily():
    """Flat notched pad."""
    import math as _m
    v, f = [(0.0, 0.0, 0.0)], []
    n = 9
    for i in range(n):
        a = _m.tau * i / n + 0.22
        v.append((_m.cos(a) * 0.42, 0.0, _m.sin(a) * 0.42))
    for i in range(1, n):
        f.append((0, i, i + 1))
    return mesh_from("lily", v, f)


def crate(name, w, h, d, taper=1.0):
    """Simple box: crates, coolers, buckets."""
    x, z = w * 0.5, d * 0.5
    t = taper
    v = [(-x, 0.0, -z), (x, 0.0, -z), (x, 0.0, z), (-x, 0.0, z),
         (-x * t, h, -z * t), (x * t, h, -z * t), (x * t, h, z * t), (-x * t, h, z * t)]
    f = [(0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5),
         (2, 3, 7, 6), (3, 0, 4, 7)]
    return mesh_from(name, v, f)


def shack():
    """A small hut with a pitched roof, for the shop."""
    body = crate("shack", 4.0, 2.2, 3.2)
    v = list(body.data.vertices and [tuple(x.co) for x in body.data.vertices])
    f = [tuple(p.vertices) for p in body.data.polygons]
    base = len(v)
    v += [(-2.3, 2.2, -1.9), (2.3, 2.2, -1.9), (2.3, 2.2, 1.9), (-2.3, 2.2, 1.9),
          (-1.9, 3.3, 0.0), (1.9, 3.3, 0.0)]
    f += [(base + 0, base + 1, base + 5, base + 4),
          (base + 3, base + 2, base + 5, base + 4),
          (base + 1, base + 2, base + 5), (base + 0, base + 3, base + 4)]
    return mesh_from("shack", v, f)


def dock_post(name="dock_post", h=1.5):
    """Square pile with a capped top, used along the pier edge."""
    v = [(-0.16, 0.0, -0.16), (0.16, 0.0, -0.16), (0.16, 0.0, 0.16), (-0.16, 0.0, 0.16),
         (-0.13, h, -0.13), (0.13, h, -0.13), (0.13, h, 0.13), (-0.13, h, 0.13),
         (-0.20, h, -0.20), (0.20, h, -0.20), (0.20, h, 0.20), (-0.20, h, 0.20),
         (-0.20, h + 0.10, -0.20), (0.20, h + 0.10, -0.20),
         (0.20, h + 0.10, 0.20), (-0.20, h + 0.10, 0.20)]
    f = [(0, 1, 2, 3), (4, 5, 1, 0), (5, 6, 2, 1), (6, 7, 3, 2), (7, 4, 0, 3),
         (8, 9, 10, 11), (12, 13, 14, 15), (8, 9, 13, 12), (9, 10, 14, 13),
         (10, 11, 15, 14), (11, 8, 12, 15)]
    return mesh_from(name, v, f)


def rod_rack(name="rod_rack"):
    """Two uprights and a crossbar, for leaning rods against."""
    v, f = [], []

    def bar(x0, y0, z0, x1, y1, z1, t):
        b = len(v)
        v.extend([(x0 - t, y0, z0 - t), (x0 + t, y0, z0 - t),
                  (x0 + t, y0, z0 + t), (x0 - t, y0, z0 + t),
                  (x1 - t, y1, z1 - t), (x1 + t, y1, z1 - t),
                  (x1 + t, y1, z1 + t), (x1 - t, y1, z1 + t)])
        f.extend([(b, b + 1, b + 2, b + 3), (b + 4, b + 5, b + 6, b + 7),
                  (b, b + 1, b + 5, b + 4), (b + 1, b + 2, b + 6, b + 5),
                  (b + 2, b + 3, b + 7, b + 6), (b + 3, b, b + 4, b + 7)])

    bar(-0.55, 0.0, 0.0, -0.55, 1.25, 0.0, 0.06)
    bar(0.55, 0.0, 0.0, 0.55, 1.25, 0.0, 0.06)
    bar(-0.62, 1.05, 0.0, 0.62, 1.05, 0.0, 0.045)
    bar(-0.62, 0.55, 0.0, 0.62, 0.55, 0.0, 0.035)
    return mesh_from(name, v, f)


def lantern(name="lantern"):
    """Post with a boxy lamp head - reads at night."""
    post = crate("lantern_post", 0.12, 1.9, 0.12)
    v = [tuple(x.co) for x in post.data.vertices]
    f = [tuple(p.vertices) for p in post.data.polygons]
    b = len(v)
    v += [(-0.18, 1.85, -0.18), (0.18, 1.85, -0.18),
          (0.18, 1.85, 0.18), (-0.18, 1.85, 0.18),
          (-0.12, 2.20, -0.12), (0.12, 2.20, -0.12),
          (0.12, 2.20, 0.12), (-0.12, 2.20, 0.12)]
    f += [(b, b + 1, b + 2, b + 3), (b + 4, b + 5, b + 6, b + 7),
          (b, b + 1, b + 5, b + 4), (b + 1, b + 2, b + 6, b + 5),
          (b + 2, b + 3, b + 7, b + 6), (b + 3, b, b + 4, b + 7)]
    return mesh_from(name, v, f)


def tube(name, r0, r1, h, sides=6):
    """A capped frustum: the workhorse for pipes, papers and bong necks."""
    import math as _m
    v, f = [], []
    for i in range(sides):
        a = _m.tau * i / sides
        v.append((_m.cos(a) * r0, 0.0, _m.sin(a) * r0))
    for i in range(sides):
        a = _m.tau * i / sides
        v.append((_m.cos(a) * r1, h, _m.sin(a) * r1))
    for i in range(sides):
        j = (i + 1) % sides
        f.append((i, j, j + sides, i + sides))
    f.append(tuple(range(sides - 1, -1, -1)))
    f.append(tuple(range(sides, sides * 2)))
    return mesh_from(name, v, f)


def join_into(name, *objs):
    v, f = [], []
    for ob in objs:
        base = len(v)
        v += [tuple(x.co) for x in ob.data.vertices]
        f += [tuple(base + i for i in p.vertices) for p in ob.data.polygons]
    return mesh_from(name, v, f)


def smokers():
    """One prop per smoking method, each a distinct silhouette."""
    out = {}
    out["one_hitter"] = tube("one_hitter", 0.022, 0.022, 0.34, 6)
    out["papers"] = tube("papers", 0.016, 0.030, 0.30, 5)
    out["backwoods"] = tube("backwoods", 0.030, 0.038, 0.26, 6)
    bowl = tube("pipe_bowl", 0.045, 0.055, 0.07, 6)
    stem = tube("pipe_stem", 0.012, 0.012, 0.20, 4)
    stem.location = (0.0, 0.0, 0.0)
    for x in stem.data.vertices:
        x.co = (x.co.z, x.co.y - 0.02, x.co.x + 0.05)
    out["pipe"] = join_into("pipe", bowl, stem)
    base = tube("bong_base", 0.075, 0.045, 0.18, 6)
    neck = tube("bong_neck", 0.030, 0.030, 0.22, 6)
    for x in neck.data.vertices:
        x.co = (x.co.x, x.co.z + 0.18, x.co.y)
    out["beaker_bong"] = join_into("beaker_bong", base, neck)
    belly = tube("heady_belly", 0.070, 0.070, 0.16, 7)
    snout = tube("heady_neck", 0.026, 0.032, 0.18, 5)
    for x in snout.data.vertices:
        x.co = (x.co.x, x.co.z + 0.16, x.co.y)
    out["heady_bong"] = join_into("heady_bong", belly, snout)
    rig_base = tube("rig_base", 0.055, 0.040, 0.10, 6)
    nail = tube("rig_nail", 0.010, 0.010, 0.10, 4)
    for x in nail.data.vertices:
        x.co = (x.co.x + 0.05, x.co.z + 0.10, x.co.y)
    out["dab_rig"] = join_into("dab_rig", rig_base, nail)
    plate_a = crate("press_a", 0.20, 0.03, 0.14)
    plate_b = crate("press_b", 0.20, 0.03, 0.14)
    for x in plate_b.data.vertices:
        x.co = (x.co.x, x.co.y + 0.10, x.co.z)
    out["hot_press"] = join_into("hot_press", plate_a, plate_b)
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    reset()
    export(boat(), "boat")
    export(tree("tree_a"), "tree_a")
    export(tree("tree_b", height=4.6, spread=1.1), "tree_b")
    export(reeds(), "reeds")
    export(lily(), "lily")
    export(crate("cooler", 0.9, 0.55, 0.6), "cooler")
    export(crate("bucket", 0.5, 0.36, 0.5, taper=0.82), "bucket")
    export(shack(), "shack")
    export(fisherman(), "fisherman")
    for _name, _ob in smokers().items():
        export(_ob, "smoker_" + _name)
    export(dock_post(), "dock_post")
    export(rod_rack(), "rod_rack")
    print("assets built")


def fisherman():
    """The angler as one material-painted mesh at game-space coordinates.
    Blender Y is game Y here, so the export+load round-trip preserves it."""
    import math as _m
    mats = {}
    v, f, fm = [], [], []

    def mat(name, rgb):
        mats[name] = rgb
        return name

    def seg(a, b, r, sides, name):
        ax = (b[0] - a[0], b[1] - a[1], b[2] - a[2])
        L = _m.sqrt(ax[0]**2 + ax[1]**2 + ax[2]**2) or 1.0
        ax = (ax[0] / L, ax[1] / L, ax[2] / L)
        up = (0.0, 1.0, 0.0) if abs(ax[1]) < 0.9 else (0.0, 0.0, 1.0)
        t1 = (up[1] * ax[2] - up[2] * ax[1], up[2] * ax[0] - up[0] * ax[2],
              up[0] * ax[1] - up[1] * ax[0])
        l1 = _m.sqrt(t1[0]**2 + t1[1]**2 + t1[2]**2) or 1.0
        t1 = (t1[0] / l1, t1[1] / l1, t1[2] / l1)
        t2 = (ax[1] * t1[2] - ax[2] * t1[1], ax[2] * t1[0] - ax[0] * t1[2],
              ax[0] * t1[1] - ax[1] * t1[0])
        base = len(v)
        for i in range(sides):
            ang = _m.tau * i / sides
            ox = t1[0] * _m.cos(ang) + t2[0] * _m.sin(ang)
            oy = t1[1] * _m.cos(ang) + t2[1] * _m.sin(ang)
            oz = t1[2] * _m.cos(ang) + t2[2] * _m.sin(ang)
            v.append((a[0] + ox * r, a[1] + oy * r, a[2] + oz * r))
            v.append((b[0] + ox * r, b[1] + oy * r, b[2] + oz * r))
        for i in range(sides):
            j = (i + 1) % sides
            f.append((base + 2 * i, base + 2 * j, base + 2 * j + 1, base + 2 * i + 1))
            fm.append(name)
        # end caps: fan to the axis points so the figure reads solid
        v.append(a)
        v.append(b)
        for i in range(sides):
            f.append((base + 2 * i, base + 2 * ((i + 1) % sides), len(v) - 2))
            fm.append(name)
            f.append((base + 2 * ((i + 1) % sides) + 1, base + 2 * i + 1, len(v) - 1))
            fm.append(name)

    def box(c, w, h, d, name):
        x0, y0, z0 = c[0] - w / 2, c[1] - h / 2, c[2] - d / 2
        x1, y1, z1 = c[0] + w / 2, c[1] + h / 2, c[2] + d / 2
        p = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
             (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
        base = len(v)
        v.extend(p)
        for q in ((0, 1, 2, 3), (5, 4, 7, 6), (1, 5, 6, 2), (4, 0, 3, 7),
                  (3, 2, 6, 7), (4, 5, 1, 0)):
            f.append(tuple(base + i for i in q))
            fm.append(name)

    def disc(c, r, n, name):
        base = len(v)
        v.append(c)
        for i in range(n):
            ang = _m.tau * i / n
            v.append((c[0] + _m.cos(ang) * r, c[1], c[2] + _m.sin(ang) * r))
        for i in range(n):
            f.append((base, base + 1 + i, base + 1 + (i + 1) % n))
            fm.append(name)

    def cone(tip, b, r, sides, name):
        ax = (b[0] - tip[0], b[1] - tip[1], b[2] - tip[2])
        L = _m.sqrt(ax[0]**2 + ax[1]**2 + ax[2]**2) or 1.0
        ax = (ax[0] / L, ax[1] / L, ax[2] / L)
        up = (0.0, 1.0, 0.0) if abs(ax[1]) < 0.9 else (0.0, 0.0, 1.0)
        t1 = (up[1] * ax[2] - up[2] * ax[1], up[2] * ax[0] - up[0] * ax[2],
              up[0] * ax[1] - up[1] * ax[0])
        l1 = _m.sqrt(t1[0]**2 + t1[1]**2 + t1[2]**2) or 1.0
        t1 = (t1[0] / l1, t1[1] / l1, t1[2] / l1)
        t2 = (ax[1] * t1[2] - ax[2] * t1[1], ax[2] * t1[0] - ax[0] * t1[2],
              ax[0] * t1[1] - ax[1] * t1[0])
        base = len(v)
        ring = []
        for i in range(sides):
            ang = _m.tau * i / sides
            ring.append((b[0] + (t1[0] * _m.cos(ang) + t2[0] * _m.sin(ang)) * r,
                         b[1] + (t1[1] * _m.cos(ang) + t2[1] * _m.sin(ang)) * r,
                         b[2] + (t1[2] * _m.cos(ang) + t2[2] * _m.sin(ang)) * r))
        v.extend(ring)
        tip_idx = len(v)
        for i in range(sides):
            f.append((base + i, base + (i + 1) % sides, tip_idx))
            fm.append(name)
        v.append(tip)

    wader = mat("fisher_wader", (78, 92, 74))
    boot = mat("fisher_boot", (58, 48, 40))
    shirt = mat("fisher_shirt", (150, 132, 108))
    vest = mat("fisher_vest", (96, 110, 88))
    skin = mat("fisher_skin", (206, 158, 116))
    hat = mat("fisher_hat", (204, 178, 130))
    beard = mat("fisher_beard", (96, 74, 52))
    face = mat("fisher_face", (216, 170, 126))
    dark = mat("fisher_dark", (38, 34, 30))
    nose = mat("fisher_nose", (198, 140, 100))
    mouth = mat("fisher_mouth", (88, 52, 38))

    # waders and boots dangling over the dock edge
    seg((-3.18, 0.70, 1.38), (-2.97, 0.16, 1.28), 0.095, 5, wader)
    seg((-2.97, 0.16, 1.28), (-2.92, 0.00, 1.26), 0.075, 5, boot)
    seg((-3.18, 0.70, 1.72), (-2.90, 0.16, 1.66), 0.095, 5, wader)
    seg((-2.90, 0.16, 1.66), (-2.85, 0.00, 1.64), 0.075, 5, boot)
    # torso: shirt under the padded vest
    seg((-3.26, 0.66, 1.55), (-3.30, 1.06, 1.51), 0.150, 5, shirt)
    seg((-3.30, 1.06, 1.51), (-3.34, 1.22, 1.50), 0.175, 5, vest)
    # neck + collar + shoulder bar
    disc((-3.335, 1.225, 1.495), 0.135, 8, vest)
    seg((-3.33, 1.225, 1.495), (-3.32, 1.30, 1.49), 0.100, 5, skin)
    seg((-3.44, 1.19, 1.455), (-3.22, 1.19, 1.57), 0.085, 5, vest)
    # head + hat + beard + face plate
    seg((-3.32, 1.30, 1.49), (-3.31, 1.45, 1.48), 0.115, 6, skin)
    disc((-3.31, 1.44, 1.48), 0.24, 8, hat)
    cone((-3.31, 1.62, 1.48), (-3.31, 1.44, 1.48), 0.145, 7, hat)
    seg((-3.30, 1.26, 1.545), (-3.30, 1.33, 1.545), 0.10, 5, beard)
    box((-3.305, 1.35, 1.545), 0.05, 0.15, 0.17, face)
    box((-3.275, 1.385, 1.49), 0.02, 0.035, 0.04, dark)
    box((-3.275, 1.385, 1.60), 0.02, 0.035, 0.04, dark)
    box((-3.28, 1.345, 1.545), 0.055, 0.055, 0.05, nose)
    box((-3.275, 1.305, 1.545), 0.02, 0.03, 0.115, mouth)

    ob = mesh_from("fisherman", v, f)
    for name, rgb in mats.items():
        m = bpy.data.materials.new(name)
        m.diffuse_color = (rgb[0] / 255.0, rgb[1] / 255.0, rgb[2] / 255.0, 1.0)
        ob.data.materials.append(m)
    for poly, name in zip(ob.data.polygons, fm):
        poly.material_index = [k for k in mats][::-1].index(name) if False else list(mats).index(name)
    return ob


if __name__ == "__main__":
    main()

