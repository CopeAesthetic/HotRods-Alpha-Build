namespace HotRodsSharp;

/// <summary>
/// Phase-3 fishing loop: charge a cast, the bobber flies, waits, bites, and
/// reeling lands a fish. Rarity rolls are a placeholder until the strain
/// gates are ported with the smoking system.
/// </summary>
public sealed class FishingSim
{
    public enum Phase { Idle, Charging, Flying, Waiting, Bite, Hooked, Reeling, Card }

    public Phase Current;
    public Vector3 Bobber = new(-3.0f, 0.06f, -4.0f);
    public float Power;
    public Fish? Caught;
    public float CardT;

    private Vector3 _start, _target;
    private float _flyT, _waitT, _reel;
    private static readonly Random Rng = new();

    public sealed record Fish(string Name, string Rarity, Color Color, float KgMin, float KgMax);

    public static readonly Fish[] Table =
    {
        new("Bluegill", "common", new Color(120, 150, 170), 0.1f, 0.4f),
        new("Crappie", "common", new Color(140, 160, 150), 0.2f, 0.6f),
        new("Sunfish", "uncommon", new Color(235, 160, 60), 0.3f, 0.8f),
        new("Smallmouth Bass", "uncommon", new Color(110, 150, 80), 0.4f, 1.4f),
        new("Walleye", "rare", new Color(210, 190, 110), 0.8f, 2.5f),
        new("Carp", "rare", new Color(190, 150, 90), 1.0f, 4.0f),
        new("Channel Catfish", "epic", new Color(130, 110, 90), 2.0f, 9.0f),
        new("Muskie", "epic", new Color(120, 140, 110), 3.0f, 11.0f),
        new("Northern Pike", "legendary", new Color(90, 130, 100), 5.0f, 15.0f),
        new("Lake Sturgeon", "legendary", new Color(100, 100, 110), 8.0f, 30.0f),
    };

    public void Update(float dt, KeyboardState kb, Vector3 rodTip, Vector3 castDir)
    {
        switch (Current)
        {
            case Phase.Idle:
                if (kb.IsKeyDown(Keys.Space))
                {
                    Current = Phase.Charging;
                    Power = 0f;
                }
                break;
            case Phase.Charging:
                Power = MathF.Min(1f, Power + dt * 1.1f);
                if (kb.IsKeyUp(Keys.Space))
                {
                    _start = rodTip;
                    _target = rodTip + castDir * (5f + Power * 11f);
                    _target.Y = 0.06f;
                    _flyT = 0f;
                    Current = Phase.Flying;
                }
                break;
            case Phase.Flying:
                _flyT += dt * 2.2f;
                float t = MathF.Min(1f, _flyT);
                float arc = MathF.Sin(t * MathF.PI) * 1.6f;
                Bobber = Vector3.Lerp(_start, _target, t) + Vector3.Up * arc;
                if (t >= 1f)
                {
                    Bobber = _target;
                    _waitT = 2f + (float)Rng.NextDouble() * 4f;
                    Current = Phase.Waiting;
                }
                break;
            case Phase.Waiting:
                _waitT -= dt;
                if (_waitT <= 0f)
                    Current = Phase.Bite;
                break;
            case Phase.Bite:
                if (kb.IsKeyDown(Keys.Space))
                {
                    Current = Phase.Hooked;
                    _reel = 0f;
                }
                break;
            case Phase.Hooked:
            case Phase.Reeling:
                if (kb.IsKeyDown(Keys.Space))
                {
                    _reel += dt / 1.6f;
                    Current = Phase.Reeling;
                    if (_reel >= 1f)
                    {
                        Caught = Roll();
                        CardT = 0f;
                        Current = Phase.Card;
                    }
                }
                else if (Current == Phase.Reeling)
                {
                    _reel = MathF.Max(0f, _reel - dt * 0.8f);
                }
                break;
            case Phase.Card:
                CardT += dt;
                if (CardT > 2.6f)
                {
                    Current = Phase.Idle;
                    Caught = null;
                }
                break;
        }
    }

    private static Fish Roll()
    {
        double r = Rng.NextDouble();
        string rarity = r < 0.50 ? "common"
            : r < 0.75 ? "uncommon"
            : r < 0.90 ? "rare"
            : r < 0.98 ? "epic"
            : "legendary";
        var pool = Table.Where(f => f.Rarity == rarity).ToArray();
        return pool[Rng.Next(pool.Length)];
    }

    /// <summary>Line from the rod tip down to the bobber.</summary>
    public void EmitLine(List<VertexPositionColor> v, Vector3 tip)
    {
        if (Current == Phase.Idle || Current == Phase.Card)
            return;
        Prims.AddSegment(v, tip, Bobber + Vector3.Up * 0.05f, 0.012f,
                         new Color(220, 230, 240), 3);
    }

    /// <summary>Red/white bobber.</summary>
    public void EmitBobber(List<VertexPositionColor> v)
    {
        if (Current == Phase.Idle || Current == Phase.Card)
            return;
        float y = 0.06f + (Current == Phase.Bite
            ? 0.12f * MathF.Sin((float)DateTime.Now.Ticks * 0.000001f) : 0f);
        Prims.AddSegment(v, new Vector3(Bobber.X, y - 0.08f, Bobber.Z),
                         new Vector3(Bobber.X, y, Bobber.Z), 0.05f,
                         new Color(210, 60, 50), 5);
        Prims.AddSegment(v, new Vector3(Bobber.X, y, Bobber.Z),
                         new Vector3(Bobber.X, y + 0.06f, Bobber.Z), 0.03f,
                         new Color(240, 240, 240), 5);
    }
}
