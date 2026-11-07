namespace HotRodsSharp;

/// <summary>
/// Phase-3 fishing loop: charge a cast, the bobber flies, waits, bites, and
/// the sweep minigame lands a fish. The catch roll follows the Python rules:
/// sober water gives commons only, smoking opens the strain's rarity band,
/// and foreign weed caps at uncommon.
/// </summary>
public sealed class FishingSim
{
    public enum Phase { Idle, Charging, Flying, Waiting, Bite, Reeling, Card }

    public Phase Current;
    public Vector3 Bobber = new(-3.0f, 0.06f, -4.0f);
    public float Power;
    public FishSpec? Caught;
    public float CaughtKg;
    public float CaughtValue;
    public bool Kept;
    public float CardT;
    public float Progress => _progress;
    public float BluePos => _bluePos;
    public float GreenPos => _greenPos;
    public bool Reeling => Current == Phase.Reeling;

    private readonly GameState _state;
    private Vector3 _start, _target;
    private float _flyT, _waitT;
    private FishSpec? _pending;
    public FishSpec? Pending => _pending;
    private float _angle, _progress, _strikeCd;
    private float _bluePos, _greenPos;
    private const float BlueHalf = 0.10f, GreenHalf = 0.28f;
    private static readonly Random Rng = new();
    private static float _time;

    public FishingSim(GameState state) => _state = state;

    public float NeedleAngle => _angle;
    public static float Clock => _time;
    public static void TickTime(float dt) => _time += dt;

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
                    _state.Casts++;
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
                    _pending = Roll();
                    CaughtKg = RollKg(_pending);
                    CaughtValue = CaughtKg * _pending.UsdKg;
                    _progress = 0.30f;
                    _angle = (float)(Rng.NextDouble() * MathF.Tau);
                    _bluePos = (float)(Rng.NextDouble() * MathF.Tau);
                    _greenPos = Wrap(_bluePos + 2.4f);
                    _strikeCd = 0f;
                    Current = Phase.Reeling;
                }
                break;
            case Phase.Reeling:
                float reelF = GameData.Rods.FirstOrDefault(
                    r => r.Id == _state.EquippedId("rod"))?.Reel ?? 1f;
                float grace = GameData.Lines.FirstOrDefault(
                    l => l.Id == _state.EquippedId("line"))?.Grace ?? 0f;
                float speed = (2.2f + GameData.RarityIndex(_pending!.Rarity) * 0.45f)
                              / reelF;
                float blue = BlueHalf * (1f + grace);
                float green = GreenHalf * (1f + grace);
                _angle = Wrap(_angle + speed * dt);      // the dial spins 360
                _strikeCd -= dt;
                if (kb.IsKeyDown(Keys.Space) && _strikeCd <= 0f)
                {
                    _strikeCd = 0.14f;
                    float db = MathF.Abs(Wrap(_angle - _bluePos));
                    float dg = MathF.Abs(Wrap(_angle - _greenPos));
                    if (db < blue)
                    {
                        _progress += 0.09f;
                        (_bluePos, _greenPos) = (_greenPos, _bluePos);
                    }
                    else if (dg < green)
                    {
                        _progress += 0.03f;
                        (_bluePos, _greenPos) = (_greenPos, _bluePos);
                    }
                    else
                    {
                        _progress -= 0.08f;
                    }
                    if (_progress >= 1f)
                    {
                        Caught = _pending;
                        _state.Catches++;
                        int slots = GameData.Coolers.FirstOrDefault(
                            c => c.Id == _state.EquippedId("cooler"))?.Slots ?? 4;
                        if (_state.Bag.Count >= slots)
                        {
                            Kept = false;
                            _state.Cash += (int)MathF.Round(CaughtValue);
                        }
                        else
                        {
                            Kept = true;
                            _state.Bag.Add(_pending.Id);
                        }
                        _state.Species[_pending.Id] =
                            _state.Species.GetValueOrDefault(_pending.Id) + 1;
                        _state.Cash += (int)MathF.Round(CaughtValue);
                        _state.Save();
                        CardT = 0f;
                        Current = Phase.Card;
                    }
                    else if (_progress <= 0f)
                    {
                        Caught = null;
                        _pending = null;
                        Current = Phase.Idle;
                    }
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

    /// <summary>Weighted roll inside the state's current rarity band.</summary>
    private FishSpec Roll()
    {
        var (lo, hi) = _state.RarityBand();
        var pool = GameData.ForSpot(_state.Spot)
                           .Where(f => GameData.RarityIndex(f.Rarity) >= lo
                                    && GameData.RarityIndex(f.Rarity) <= hi)
                           .ToArray();
        if (pool.Length == 0)
            pool = GameData.ForSpot(_state.Spot)
                           .Where(f => f.Rarity == "common").ToArray();
        if (pool.Length == 0)
            pool = GameData.Species;
        float total = pool.Sum(f => GameData.RarityWeight[GameData.RarityIndex(f.Rarity)]);
        float r = (float)Rng.NextDouble() * total;
        foreach (var f in pool)
        {
            r -= GameData.RarityWeight[GameData.RarityIndex(f.Rarity)];
            if (r <= 0f)
                return f;
        }
        return pool[^1];
    }

    private static float Wrap(float a)
    {
        while (a > MathF.PI) a -= MathF.Tau;
        while (a < -MathF.PI) a += MathF.Tau;
        return a;
    }

    private static float RollKg(FishSpec sp) =>
        sp.KgMin + (float)Rng.NextDouble() * (sp.KgMax - sp.KgMin);

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
            ? 0.12f * MathF.Sin(_time * 18f) : 0f);
        Prims.AddSegment(v, new Vector3(Bobber.X, y - 0.08f, Bobber.Z),
                         new Vector3(Bobber.X, y, Bobber.Z), 0.05f,
                         new Color(210, 60, 50), 5);
        Prims.AddSegment(v, new Vector3(Bobber.X, y, Bobber.Z),
                         new Vector3(Bobber.X, y + 0.06f, Bobber.Z), 0.03f,
                         new Color(240, 240, 240), 5);
    }
}
