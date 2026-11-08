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

    private GameState _state;
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

    /// <summary>Rebind after boarding a save slot from the title screen.</summary>
    public void SetState(GameState s) => _state = s;

    /// <summary>Sound cues: "whoosh", "splash", "click".</summary>
    public Action<string> Sfx = _ => { };

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
                    Sfx("whoosh");
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
                    Sfx("splash");
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
                float arcMul = 1f + 0.012f * _state.StokeRank("fish");  // stoke lane
                float blue = BlueHalf * (1f + grace) * arcMul;
                float green = GreenHalf * (1f + grace) * arcMul;
                _angle = Wrap(_angle + speed * dt);      // the dial spins 360
                _strikeCd -= dt;
                if (kb.IsKeyDown(Keys.Space) && _strikeCd <= 0f)
                {
                    _strikeCd = 0.14f;
                    Sfx("click");
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
                        int slots = _state.BagSize();
                        Kept = _state.Bag.Count < slots;
                        if (!Kept)
                            _state.Cash += (int)MathF.Round(CaughtValue); // sold on spot
                        _state.OnCatch(_pending.Id, CaughtKg, Kept);
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

    /// <summary>
    /// Catch roll, ported from the Python fishing.py _choose_species:
    /// hidden fish are a flat 1-in-4 when their strain is active, shinies a
    /// 2% roll while high on the matching strain, and the normal pool is the
    /// water's 32 species gated by rarity band, time/high conditions, lure
    /// bias, night swings, high swings and cast depth.
    /// </summary>
    public FishSpec Roll()
    {
        var (lo, hi) = _state.RarityBand();
        int spot = _state.Spot;
        bool high = _state.High > 0.10f;
        bool night = _state.Hour >= 20f || _state.Hour < 5.5f;
        string strain = _state.Strain;

        // hidden: flat 1-in-4 when its strain is active
        var secret = GameData.HiddenFish.FirstOrDefault(h => h.Spot == spot);
        if (secret != null && Rng.NextDouble() < 0.25)
        {
            if (secret.Cond == null
                || (secret.Cond.StartsWith("strain:") && high
                    && strain == secret.Cond.Split(':', 2)[1]))
                return secret;
        }

        // shiny: 2% roll while high on the matching strain
        // (Python also needs the dab rig + concentrate; that system is a TODO)
        foreach (var sh in GameData.ShinyFish)
        {
            if (sh.Spot != spot || sh.Cond == null || !sh.Cond.StartsWith("strain:"))
                continue;
            string want = sh.Cond.Split(':', 2)[1];
            if (high && strain == want && Rng.NextDouble() < GameData.ShinyOdds)
                return sh;
        }

        var lure = GameData.Lures.FirstOrDefault(l => l.Id == _state.EquippedId("lure"));
        var bias = lure?.Bias;
        var nightBias = lure?.NightBias;
        float strainLuck = high ? _state.StrainData().Luck : 0f;  // smoker luck TODO
        float depthBonus = 0.55f + 0.75f * Power;

        var weights = new List<(FishSpec sp, float w)>();
        float total = 0f;
        foreach (var sp in GameData.SpeciesList.Where(f => f.Spot == spot))
        {
            int ri = GameData.RarityIndex(sp.Rarity);
            if (!(lo <= ri && ri <= hi))
                continue;
            if (sp.Cond is { } cond)
            {
                if (cond.StartsWith("strain:"))
                {
                    if (!(high && strain == cond.Split(':', 2)[1]))
                        continue;
                }
                else if (cond == "night" && !night)
                    continue;
                else if (cond == "high" && !high)
                    continue;
                else if (cond == "night+high" && !(night && high))
                    continue;
            }
            string r = sp.Rarity;
            float w = GameData.RarityWeights[ri];
            if (bias != null && bias.TryGetValue(r, out var b))
                w *= b;
            if (night)
            {
                if (nightBias != null && nightBias.TryGetValue(r, out var nb))
                    w *= nb;
                w *= r switch
                {
                    "common" => 0.85f, "uncommon" => 1.0f, "rare" => 1.3f,
                    "epic" => 1.45f, "legendary" => 1.25f, _ => 1f,
                };
            }
            if (_state.High > 0.05f)
            {
                float swing = r switch
                {
                    "common" => -0.30f, "uncommon" => 0.06f, "rare" => 0.45f,
                    "epic" => 0.85f, "legendary" => 1.30f, _ => 0f,
                };
                w *= MathF.Max(0.05f, 1f + _state.High * swing * (0.55f + strainLuck));
            }
            if (r is "rare" or "epic" or "legendary")
                w *= depthBonus;
            w = MathF.Max(0.001f, w);
            weights.Add((sp, w));
            total += w;
        }
        if (weights.Count == 0)
            return GameData.SpeciesList.First(f => f.Spot == spot && f.Rarity == "common");
        float roll = (float)Rng.NextDouble() * total;
        float acc = 0f;
        foreach (var (sp, w) in weights)
        {
            acc += w;
            if (roll <= acc)
                return sp;
        }
        return weights[^1].sp;
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
