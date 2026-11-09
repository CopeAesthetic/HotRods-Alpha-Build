namespace HotRodsSharp;

/// <summary>
/// Phase-3 smoking ceremony: E raises the piece to the mouth with both hands,
/// a plume rises from the face, and the rod leans against the dock while it
/// burns. Mirrors the final Python ceremony (handhelds lie flat, glass stays
/// upright).
/// </summary>
public sealed class SmokingSim
{
    public bool Active => _t < Total;
    public float Raise
    {
        get
        {
            float ph = Math.Clamp(1f - _t / Total, 0f, 1f);
            if (ph < 0.25f) return ph / 0.25f;
            if (ph < 0.75f) return 1f;
            return MathF.Max(0f, 1f - (ph - 0.75f) / 0.25f);
        }
    }

    private const float Total = 4.2f;
    private float _t = Total;          // starts "done" (inactive)
    private int _item;
    private float _clock;

    /// <summary>Ceremony speed multiplier (stoke smoke lane: +6% per rank).</summary>
    public float Speed = 1f;

    private static readonly Vector3 Waist = new(-3.98f, 1.10f, 1.40f);
    private static readonly Vector3 Mouth = new(-3.94f, 1.26f, 1.34f);
    private static readonly Vector3 Face = new(-3.90f, 1.30f, 1.30f);

    private static readonly (string id, Color col, float lay, float yaw)[] Items =
    {
        ("smoker_one_hitter", new Color(198, 172, 120), -1.57f, -1.10f),
        ("smoker_papers", new Color(236, 232, 220), -1.57f, -1.10f),
        ("smoker_backwoods", new Color(120, 92, 62), -1.57f, -1.20f),
        ("smoker_pipe", new Color(150, 196, 210), -0.45f, 0f),
        ("smoker_beaker_bong", new Color(140, 200, 180), -0.45f, 0f),
        ("smoker_dab_rig", new Color(250, 210, 120), -0.45f, 0f),
    };

    private readonly Dictionary<string, ObjMesh> _meshes = new();

    public string CurrentItem => Items[_item].id;

    public void Start()
    {
        if (Active)
            return;
        _t = 0f;
    }

    public void CycleItem()
    {
        _item = (_item + 1) % Items.Length;
    }

    /// <summary>Switch the visible piece to a gear smoker (closest visual
    /// for pieces that share a model).</summary>
    public void SetItem(string gearId)
    {
        string want = "smoker_" + gearId;
        for (int i = 0; i < Items.Length; i++)
        {
            if (Items[i].id == want)
            {
                _item = i;
                return;
            }
        }
        _item = gearId is "heady_bong" ? IndexOf("smoker_beaker_bong")
              : gearId is "hot_press" ? IndexOf("smoker_dab_rig")
              : 0;
    }

    private int IndexOf(string id)
    {
        for (int i = 0; i < Items.Length; i++)
            if (Items[i].id == id)
                return i;
        return 0;
    }

    public void Update(float dt)
    {
        _clock += dt;
        if (Active)
            _t = MathF.Min(Total, _t + dt * Speed);
    }

    private ObjMesh Mesh(string id)
    {
        if (!_meshes.TryGetValue(id, out var m))
        {
            m = ObjMesh.Load(
                Path.Combine(AppContext.BaseDirectory, "Assets", id + ".obj"), 0.55f);
            _meshes[id] = m;
        }
        return m;
    }

    private Vector3 Hold()
    {
        float r = Raise;
        return Vector3.Lerp(Waist, Face, r);
    }

    /// <summary>The piece, raised and tilted like the Python ceremony.</summary>
    public void EmitItem(List<VertexPositionColor> v)
    {
        if (!Active)
            return;
        var (id, col, lay, yaw) = Items[_item];
        var mesh = Mesh(id);
        float r = Raise;
        float ax = lay * r;
        float ay = yaw * r;
        float ca = MathF.Cos(ax), sa = MathF.Sin(ax);
        float cb = MathF.Cos(ay), sb = MathF.Sin(ay);
        var hold = Hold();
        foreach (var (a, b, c) in mesh.Tris)
        {
            Vector3 R(Vector3 p)
            {
                float y2 = p.Y * ca - p.Z * sa;
                float z2 = p.Y * sa + p.Z * ca;
                float x3 = p.X * cb + z2 * sb;
                float z3 = -p.X * sb + z2 * cb;
                return new Vector3(x3 + hold.X + 0.02f, y2 + hold.Y + 0.10f,
                                   z3 + hold.Z + 0.04f);
            }
            Prims.AddTri(v, R(mesh.Verts[a]), R(mesh.Verts[b]), R(mesh.Verts[c]), col);
        }
    }

    /// <summary>First-person arms: the right arm raises the piece to the
    /// mouth while the left brings the lighter to the piece's lit end -
    /// the bottom tip of a cylinder, the bowl of glass - so the flame
    /// licks exactly where it should.</summary>
    public void EmitArms(List<VertexPositionColor> v, Color vest, Color skin)
    {
        if (!Active)
            return;
        var hold = Hold();
        var (id, _, lay, yaw) = Items[_item];
        float r = Raise;
        float ca = MathF.Cos(lay * r), sa = MathF.Sin(lay * r);
        float cb = MathF.Cos(yaw * r), sb = MathF.Sin(yaw * r);
        // the piece's axis under the same rotations the mesh gets
        var axis = new Vector3(ca * sb, -sa, ca * cb);
        var off = new Vector3(0.02f, 0.10f, 0.04f);
        float litZ = id switch
        {
            "smoker_one_hitter" => -0.19f,
            "smoker_papers" => -0.165f,
            "smoker_backwoods" => -0.145f,
            _ => 0f,   // pipe and glass light the bowl up top
        };
        Vector3 lit = litZ < 0f ? hold + off + axis * litZ
                    : hold + off + new Vector3(0f, 0.09f, 0f);

        // right arm raises the piece, hand wrapped on it
        var shR = new Vector3(-4.10f, 1.05f, 1.78f);
        var elR = (shR + hold) * 0.5f + new Vector3(0.03f, -0.05f, 0.02f);
        Prims.AddSegment(v, shR, elR, 0.065f, vest);
        Prims.AddSegment(v, elR, hold, 0.052f, skin);
        Prims.AddBox(v, hold + off + new Vector3(0f, -0.02f, 0.02f),
                     0.075f, 0.08f, 0.09f, skin);

        // left arm holds the lighter under the lit end
        var lighter = lit + new Vector3(0.02f, -0.04f, 0.03f);
        var shL = new Vector3(-4.38f, 1.05f, 1.00f);
        var elL = (shL + lighter) * 0.5f + new Vector3(-0.03f, -0.05f, -0.02f);
        Prims.AddSegment(v, shL, elL, 0.065f, vest);
        Prims.AddSegment(v, elL, lighter, 0.052f, skin);
        Prims.AddBox(v, lighter + new Vector3(0f, -0.02f, 0.01f),
                     0.07f, 0.075f, 0.08f, skin);
        Prims.AddBox(v, new Vector3(lighter.X, lighter.Y + 0.015f, lighter.Z + 0.01f),
                     0.02f, 0.06f, 0.02f, new Color(232, 186, 96));
        // the flame licks the tip of the piece
        float flame = lit.Y + 0.02f + MathF.Sin(_clock * 30f) * 0.004f;
        Prims.AddSegment(v, lit + new Vector3(0f, -0.02f, 0.01f),
                         new Vector3(lit.X, flame, lit.Z + 0.01f), 0.012f,
                         new Color(255, 190, 60), 4);
    }

    /// <summary>Five puffs rising from the mouth.</summary>
    public void EmitPlume(List<VertexPositionColor> v)
    {
        if (!Active)
            return;
        var col = new Color(178, 178, 184);
        for (int k = 0; k < 5; k++)
        {
            float pt = (_clock * 0.55f + k * 0.2f) % 1.0f;
            var pos = Mouth + new Vector3(MathF.Sin(pt * 7 + k) * 0.03f,
                                          0.10f + pt * 0.55f, 0.04f);
            float r = 0.03f + pt * 0.09f;
            Prims.AddBox(v, pos, r * 2, r * 2, r * 2, col);
        }
    }

    /// <summary>The rod leans against the dock while smoking.</summary>
    public void EmitRestingRod(List<VertexPositionColor> v)
    {
        if (!Active)
            return;
        Prims.AddSegment(v, new Vector3(-3.76f, 0.64f, 2.04f),
                         new Vector3(-3.58f, 1.28f, 2.12f), 0.030f,
                         new Color(176, 148, 96), 4);
        Prims.AddSegment(v, new Vector3(-3.58f, 1.28f, 2.12f),
                         new Vector3(-3.46f, 1.66f, 2.16f), 0.021f,
                         new Color(198, 170, 116), 4);
    }
}
