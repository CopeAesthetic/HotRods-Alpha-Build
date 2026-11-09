using Godot;

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

    private Vector3 Hold()
    {
        float r = Raise;
        return Waist.Lerp(Face, r);
    }

}
