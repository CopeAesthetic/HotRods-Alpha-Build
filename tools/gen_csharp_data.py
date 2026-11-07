"""Emit csharp/HotRodsSharp/GameData.cs from game/data.py, so the C# port
shares the exact fish and strain tables."""
from __future__ import annotations

import os
import sys

os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from game import data  # noqa: E402

out = []
out.append("namespace HotRodsSharp;")
out.append("")
out.append("public sealed record FishSpec(string Id, string Name, int Spot,")
out.append("        string Rarity, float KgMin, float KgMax, float UsdKg,")
out.append("        Color Body, string Shape);")
out.append("")
out.append("public sealed record Strain(string Id, string Name, int Area, int Tier,")
out.append("        int GateMin, int GateMax, int Potency, float Luck, float Duration,")
out.append("        int Price);")
out.append("")
out.append("public sealed record Gear(string Id, string Name, int Cost, string Desc,")
out.append("        float Reel, float Grace, int Slots);")
out.append("")
out.append("public static class GameData")
out.append("{")
out.append("    public static readonly string[] RarityOrder =")
out.append("        { \"common\", \"uncommon\", \"rare\", \"epic\", \"legendary\" };")
out.append("")
out.append("    public static readonly float[] RarityWeight = { 10f, 5f, 2.5f, 1f, 0.4f };")
out.append("")
out.append("    public static readonly FishSpec[] Species =")
out.append("    {")

def color(d, key, fallback):
    rgb = d.get(key)
    if not rgb:
        return fallback
    return "new Color(%d, %d, %d)" % (rgb[0], rgb[1], rgb[2])

for sp in list(data.SPECIES) + list(data.HIDDEN_FISH) + list(data.SHINY_FISH):
    kg = sp["kg"]
    out.append("        new(\"%s\", \"%s\", %d, \"%s\", %sf, %sf, %sf, %s, \"%s\"),"
               % (sp["id"], sp["name"], sp.get("spot", 0), sp["rarity"],
                  kg[0], kg[1], sp.get("usd_kg", 3.0),
                  color(sp, "body", "new Color(140, 150, 140)"),
                  sp.get("shape", "torpedo")))

out.append("    };")
out.append("")
out.append("    public static readonly Strain[] Strains =")
out.append("    {")
for st in data.STRAINS:
    out.append("        new(\"%s\", \"%s\", %d, %d, %d, %d, %d, %sf, %sf, %d),"
               % (st["id"], st["name"], st["area"], st["tier"],
                  st.get("gate_min", 0), st.get("gate_max", 4),
                  st["potency"], st.get("luck", 0.3), st.get("duration", 1.0),
                  st.get("pts", 0)))
out.append("    };")
out.append("")
for name, table in (("Rods", data.RODS), ("Lines", data.LINES),
                    ("Lures", data.LURES), ("Coolers", data.COOLERS)):
    out.append("    public static readonly Gear[] %s =" % name)
    out.append("    {")
    for g in table:
        out.append("        new(\"%s\", \"%s\", %d, \"%s\", %sf, %sf, %d),"
                   % (g["id"], g["name"], g["cost"], g.get("desc", ""),
                      g.get("reel", 1.0), g.get("grace", 0.0), g.get("slots", 0)))
    out.append("    };")
    out.append("")
out.append("")
out.append("    public static readonly string[] SpotNames =")
out.append("        { \"%s\" };" % "\", \"".join(s["name"] for s in data.SPOTS))
out.append("")
out.append("    public static int RarityIndex(string r)")
out.append("    {")
out.append("        for (int i = 0; i < RarityOrder.Length; i++)")
out.append("            if (RarityOrder[i] == r) return i;")
out.append("        return 0;")
out.append("    }")
out.append("")
out.append("    public static FishSpec[] ForSpot(int spot) =>")
out.append("        Species.Where(s => s.Spot == spot).ToArray();")
out.append("}")
open(os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                  "csharp", "HotRodsSharp", "GameData.cs"), "w").write(
    "\n".join(out) + "\n")
print("GameData.cs: %d species, %d strains" % (len(data.SPECIES) + len(data.HIDDEN_FISH) + len(data.SHINY_FISH), len(data.STRAINS)))
