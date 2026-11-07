namespace HotRodsSharp;

/// <summary>
/// Minimal OBJ loader matching game/objload.py: fan-triangulates faces and
/// applies the Blender Z-up -> renderer Y-up conversion (x, y, z) -> (x, -z, y).
/// </summary>
public sealed class ObjMesh
{
    public readonly List<Vector3> Verts = new();
    public readonly List<(int a, int b, int c)> Tris = new();
    public readonly List<string> TriMats = new();

    public static ObjMesh Load(string path, float scale = 1.0f)
    {
        var m = new ObjMesh();
        string currentMat = "";
        foreach (var raw in File.ReadLines(path))
        {
            if (raw.Length == 0 || raw[0] == '#')
                continue;
            var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;
            if (parts[0] == "usemtl" && parts.Length > 1)
            {
                currentMat = parts[1];
                continue;
            }
            if (parts[0] == "v" && parts.Length >= 4)
            {
                float bx = float.Parse(parts[1]) * scale;
                float by = float.Parse(parts[2]) * scale;
                float bz = float.Parse(parts[3]) * scale;
                // Blender is Z-up; the renderer is Y-up, exporter also flips Y.
                m.Verts.Add(new Vector3(bx, -bz, by));
            }
            else if (parts[0] == "f")
            {
                int N = parts.Length - 1;
                for (int i = 1; i + 1 < N + 1 && i + 1 <= N; i++)
                {
                    if (i + 2 > N)
                        break;
                    m.Tris.Add((Idx(parts[1]), Idx(parts[i + 1]), Idx(parts[i + 2])));
                    m.TriMats.Add(currentMat);
                }
            }
        }
        return m;

        static int Idx(string f)
        {
            var n = f.Split('/')[0];
            return int.Parse(n) - 1;
        }
    }

    /// <summary>Rotate about Y by <paramref name="rot"/> radians and translate.</summary>
    public void Transform(float rot, Vector3 offset)
    {
        float c = MathF.Cos(rot), s = MathF.Sin(rot);
        for (int i = 0; i < Verts.Count; i++)
        {
            var v = Verts[i];
            Verts[i] = rot != 0f
                ? new Vector3(v.X * c - v.Z * s + offset.X, v.Y + offset.Y,
                              v.X * s + v.Z * c + offset.Z)
                : v + offset;
        }
    }
}
