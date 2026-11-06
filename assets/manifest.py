"""What each Blender asset is called, how it is tinted and how big it sits.

Keep this next to the .obj files: the world builder reads it instead of
hardcoding colours, and `tools/blender_assets.py` regenerates the meshes.
"""

ASSETS = {
    #  name        colour              scale   sits on
    "boat":      ((112, 74, 52),      1.00,   "water"),
    "tree_a":    ((58, 84, 52),       0.30,   "land"),
    "tree_b":    ((48, 72, 46),       0.30,   "land"),
    "reeds":     ((86, 104, 66),      0.35,   "water"),
    "lily":      ((84, 126, 72),      1.00,   "water"),
    "cooler":    ((196, 74, 64),      1.00,   "dock"),
    "bucket":    ((120, 130, 140),    1.00,   "dock"),
    "shack":     ((150, 122, 88),     1.00,   "land"),
    "dock_post": ((106, 78, 54),      1.00,   "dock"),
    "rod_rack":  ((126, 96, 66),      1.00,   "dock"),
}


def spec(name):
    return ASSETS[name]
