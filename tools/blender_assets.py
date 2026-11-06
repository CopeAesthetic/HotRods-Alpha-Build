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
    for i in range(9):
        a = _m.tau * i / 9
        r = 0.10 + 0.06 * (i % 3)
        x, z = _m.cos(a) * r, _m.sin(a) * r
        h = 0.85 + 0.5 * ((i * 7) % 5) / 5.0
        w, d = 0.045, 0.045
        b = len(v)
        v += [(x - w, 0.0, z - d), (x + w, 0.0, z - d),
              (x + w, 0.0, z + d), (x - w, 0.0, z + d),
              (x - w, h, z - d), (x + w, h, z - d),
              (x + w, h, z + d), (x - w, h, z + d)]
        f += [(b, b + 1, b + 2, b + 3), (b + 4, b + 5, b + 6, b + 7),
              (b, b + 1, b + 5, b + 4), (b + 1, b + 2, b + 6, b + 5),
              (b + 2, b + 3, b + 7, b + 6), (b + 3, b, b + 4, b + 7)]
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
    print("assets built")


if __name__ == "__main__":
    main()
