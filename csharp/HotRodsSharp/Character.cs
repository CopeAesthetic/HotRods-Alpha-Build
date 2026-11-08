namespace HotRodsSharp;

/// <summary>
/// The stoner fisherman, rebuilt from primitives in the chunky early-Xbox
/// low-poly style: yellow rain jacket, navy waders, black boots, bucket hat,
/// shades, a proper beard and a lit joint. Anchored to the fishing/smoking
/// pose constants so the procedural arms and rod still line up.
/// </summary>
public static class Character
{
    private static readonly Color Jacket = new(226, 178, 66);
    private static readonly Color JacketD = new(198, 152, 54);
    private static readonly Color Wader = new(42, 54, 74);
    private static readonly Color WaderD = new(34, 44, 62);
    private static readonly Color Boot = new(48, 42, 38);
    private static readonly Color Skin = new(214, 166, 126);
    private static readonly Color Beard = new(110, 84, 60);
    private static readonly Color Shades = new(26, 28, 34);
    private static readonly Color HatTop = new(84, 108, 66);
    private static readonly Color HatBrim = new(70, 90, 56);

    public static void Build(List<VertexPositionNormalColor> v)
    {
        var c = new Vector3(-3.25f, 0f, 1.50f);   // body centre, facing -Z (the water)

        // ---- boots (slightly apart, toes out) and navy waders
        PrimsLit.AddBox(v, new Vector3(c.X - 0.10f, 0.78f, c.Z + 0.07f),
                        0.24f, 0.14f, 0.44f, Boot);
        PrimsLit.AddBox(v, new Vector3(c.X + 0.10f, 0.78f, c.Z + 0.07f),
                        0.24f, 0.14f, 0.44f, Boot);
        PrimsLit.AddTaperedSegment(v, new Vector3(c.X - 0.075f, 0.84f, c.Z + 0.05f),
                                   new Vector3(c.X - 0.06f, 0.99f, c.Z + 0.03f),
                                   0.10f, 0.145f, Wader);
        PrimsLit.AddTaperedSegment(v, new Vector3(c.X + 0.075f, 0.84f, c.Z + 0.05f),
                                   new Vector3(c.X + 0.06f, 0.99f, c.Z + 0.03f),
                                   0.10f, 0.145f, Wader);
        // wader suspender straps up the chest
        PrimsLit.AddSegment(v, new Vector3(c.X - 0.09f, 0.98f, c.Z + 0.10f),
                            new Vector3(c.X - 0.05f, 1.14f, c.Z + 0.12f),
                            0.022f, WaderD, 6);
        PrimsLit.AddSegment(v, new Vector3(c.X + 0.09f, 0.98f, c.Z + 0.10f),
                            new Vector3(c.X + 0.05f, 1.14f, c.Z + 0.12f),
                            0.022f, WaderD, 6);

        // ---- yellow rain jacket torso, hem to shoulders
        PrimsLit.AddTaperedSegment(v, new Vector3(c.X, 0.97f, c.Z),
                                   new Vector3(c.X, 1.24f, c.Z),
                                   0.25f, 0.205f, Jacket);
        PrimsLit.AddTaperedSegment(v, new Vector3(c.X, 0.97f, c.Z),
                                   new Vector3(c.X, 0.99f, c.Z),
                                   0.262f, 0.25f, JacketD);
        // collar band
        PrimsLit.AddTaperedSegment(v, new Vector3(c.X, 1.23f, c.Z),
                                   new Vector3(c.X, 1.25f, c.Z),
                                   0.21f, 0.195f, JacketD);

        // ---- neck and head
        PrimsLit.AddSegment(v, new Vector3(c.X, 1.24f, c.Z - 0.01f),
                            new Vector3(c.X, 1.30f, c.Z - 0.02f), 0.085f, Skin);
        PrimsLit.AddSegment(v, new Vector3(c.X, 1.29f, c.Z - 0.02f),
                            new Vector3(c.X, 1.44f, c.Z - 0.03f), 0.165f, Skin);

        // face: shades, beard, nose (front of head faces -Z)
        float fz = c.Z - 0.19f;
        PrimsLit.AddBox(v, new Vector3(c.X - 0.065f, 1.385f, fz + 0.015f),
                        0.115f, 0.055f, 0.05f, Shades);
        PrimsLit.AddBox(v, new Vector3(c.X + 0.065f, 1.385f, fz + 0.015f),
                        0.115f, 0.055f, 0.05f, Shades);
        PrimsLit.AddBox(v, new Vector3(c.X, 1.385f, fz + 0.015f),
                        0.05f, 0.03f, 0.05f, Shades);
        PrimsLit.AddBox(v, new Vector3(c.X, 1.30f, fz + 0.02f),
                        0.21f, 0.13f, 0.10f, Beard);
        PrimsLit.AddBox(v, new Vector3(c.X, 1.36f, fz + 0.02f),
                        0.055f, 0.05f, 0.045f, Skin);

        // ---- bucket hat: crown + brim
        PrimsLit.AddSegment(v, new Vector3(c.X, 1.42f, c.Z - 0.03f),
                            new Vector3(c.X, 1.50f, c.Z - 0.035f), 0.155f, HatTop);
        PrimsLit.AddDisc(v, new Vector3(c.X, 1.445f, c.Z - 0.03f), 0.27f,
                         HatBrim, Vector3.Up);

        // ---- the joint: white paper, glowing tip
        PrimsLit.AddSegment(v, new Vector3(c.X + 0.05f, 1.315f, fz + 0.02f),
                            new Vector3(c.X + 0.17f, 1.375f, fz - 0.06f),
                            0.014f, new Color(240, 240, 230), 6);
        PrimsLit.AddBox(v, new Vector3(c.X + 0.185f, 1.385f, fz - 0.07f),
                        0.03f, 0.03f, 0.03f, new Color(255, 190, 110));
    }
}
