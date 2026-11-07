namespace HotRodsSharp;

/// <summary>
/// Draws the caught fish in 3D: one Blender asset per silhouette family,
/// tinted by the species colour. During the fight it thrashes at the bobber;
/// on the card it leaps in an arc to the angler's hand.
/// </summary>
public sealed class FishRender
{
    private readonly Dictionary<string, ObjMesh> _meshes = new();
    private static readonly Color Eye = new(18, 18, 18);

    private ObjMesh Mesh(string shape)
    {
        if (!_meshes.TryGetValue(shape, out var m))
        {
            m = ObjMesh.Load(
                Path.Combine(AppContext.BaseDirectory, "Assets", "fish_" + shape + ".obj"), 1f);
            _meshes[shape] = m;
        }
        return m;
    }

    public void Emit(List<VertexPositionColor> v, FishSpec sp, Vector3 pos,
                     float yaw, float scale)
    {
        var m = Mesh(_meshes.ContainsKey(sp.Shape) ? sp.Shape : "torpedo");
        var belly = Lighten(sp.Body, 0.42f);
        var fin = Darken(sp.Body, 0.38f);
        float c = MathF.Cos(yaw), s = MathF.Sin(yaw);
        for (int i = 0; i < m.Tris.Count; i++)
        {
            var (a, b, c2) = m.Tris[i];
            Color col = i < m.TriMats.Count ? m.TriMats[i] switch
            {
                "fish_belly" => belly,
                "fish_fin" => fin,
                "fish_eye" => Eye,
                _ => sp.Body,
            } : sp.Body;
            Prims.AddTri(v, T(m.Verts[a]), T(m.Verts[b]), T(m.Verts[c2]), col);
        }

        Vector3 T(Vector3 p)
        {
            var q = new Vector3(p.X * scale, p.Y * scale, p.Z * scale);
            return new Vector3(q.X * c - q.Z * s + pos.X, q.Y + pos.Y,
                               q.X * s + q.Z * c + pos.Z);
        }
    }

    private static Color Lighten(Color col, float k) => new(
        (byte)(col.R + (255 - col.R) * k),
        (byte)(col.G + (255 - col.G) * k),
        (byte)(col.B + (255 - col.B) * k));

    private static Color Darken(Color col, float k) => new(
        (byte)(col.R * (1 - k)), (byte)(col.G * (1 - k)), (byte)(col.B * (1 - k)));
}
