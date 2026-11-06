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


def main():
    os.makedirs(OUT, exist_ok=True)
    reset()
    export(boat(), "boat")
    print("assets built")


if __name__ == "__main__":
    main()
