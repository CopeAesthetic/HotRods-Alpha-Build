namespace HotRodsSharp;

/// <summary>
/// Low-poly primitives matching the Python renderer's look: boxes, capped
/// tubes (segments), horizontal discs and cones, all emitted as triangles.
/// </summary>
public static class Prims
{
    public static void AddTri(List<VertexPositionColor> v, Vector3 a, Vector3 b,
                              Vector3 c, Color col)
    {
        v.Add(new VertexPositionColor(a, col));
        v.Add(new VertexPositionColor(b, col));
        v.Add(new VertexPositionColor(c, col));
    }

    public static void AddQuad(List<VertexPositionColor> v, Vector3 a, Vector3 b,
                               Vector3 c, Vector3 d, Color col)
    {
        AddTri(v, a, b, c, col);
        AddTri(v, a, c, d, col);
    }

    public static void AddBox(List<VertexPositionColor> v, Vector3 c, float w,
                              float h, float d, Color col)
    {
        var x0 = c.X - w / 2; var x1 = c.X + w / 2;
        var y0 = c.Y - h / 2; var y1 = c.Y + h / 2;
        var z0 = c.Z - d / 2; var z1 = c.Z + d / 2;
        var p = new[]
        {
            new Vector3(x0, y0, z0), new Vector3(x1, y0, z0),
            new Vector3(x1, y1, z0), new Vector3(x0, y1, z0),
            new Vector3(x0, y0, z1), new Vector3(x1, y0, z1),
            new Vector3(x1, y1, z1), new Vector3(x0, y1, z1),
        };
        AddQuad(v, p[0], p[1], p[2], p[3], col); // -z
        AddQuad(v, p[5], p[4], p[7], p[6], col); // +z
        AddQuad(v, p[1], p[5], p[6], p[2], col); // +x
        AddQuad(v, p[4], p[0], p[3], p[7], col); // -x
        AddQuad(v, p[3], p[2], p[6], p[7], col); // +y
        AddQuad(v, p[4], p[5], p[1], p[0], col); // -y
    }

    /// <summary>Capped tube from a to b.</summary>
    public static void AddSegment(List<VertexPositionColor> v, Vector3 a, Vector3 b,
                                  float r, Color col, int sides = 5)
    {
        var axis = Vector3.Normalize(b - a);
        var up = MathF.Abs(axis.Y) < 0.9f ? Vector3.Up : Vector3.Forward;
        var t1 = Vector3.Normalize(Vector3.Cross(up, axis));
        var t2 = Vector3.Cross(axis, t1);
        var ringA = new Vector3[sides];
        var ringB = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float ang = MathF.Tau * i / sides;
            var off = (t1 * MathF.Cos(ang) + t2 * MathF.Sin(ang)) * r;
            ringA[i] = a + off;
            ringB[i] = b + off;
        }
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            AddQuad(v, ringA[i], ringA[j], ringB[j], ringB[i], col);
        }
    }

    /// <summary>Flat horizontal disc.</summary>
    public static void AddDisc(List<VertexPositionColor> v, Vector3 c, float r,
                               Color col, int n = 8)
    {
        var ring = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float ang = MathF.Tau * i / n;
            ring[i] = c + new Vector3(MathF.Cos(ang) * r, 0, MathF.Sin(ang) * r);
        }
        for (int i = 0; i < n; i++)
            AddTri(v, c, ring[i], ring[(i + 1) % n], col);
    }

    /// <summary>Cone from base b (wide) to tip a.</summary>
    public static void AddCone(List<VertexPositionColor> v, Vector3 a, Vector3 b,
                               float r, Color col, int sides = 7)
    {
        var axis = Vector3.Normalize(b - a);
        var up = MathF.Abs(axis.Y) < 0.9f ? Vector3.Up : Vector3.Forward;
        var t1 = Vector3.Normalize(Vector3.Cross(up, axis));
        var t2 = Vector3.Cross(axis, t1);
        var ring = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float ang = MathF.Tau * i / sides;
            ring[i] = b + (t1 * MathF.Cos(ang) + t2 * MathF.Sin(ang)) * r;
        }
        for (int i = 0; i < sides; i++)
            AddTri(v, a, ring[i], ring[(i + 1) % sides], col);
    }
}
