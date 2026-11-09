using Godot;

namespace HotRodsSharp;

/// <summary>
/// Fishing loop, ported from the Python angler: charge, fly, wait (with
/// nibbles), a bite window, a hook yank, and the reel - a sweep marker that
/// oscillates while a zone window wanders; time your pulls (perfect +3,
/// good +1, miss -2, too many misses and the fish escapes). The catch roll
/// follows the Python rules (hidden 1-in-4, shiny 2%, rarity bands and
/// time/high conditions).
/// </summary>
public sealed class FishingSim
{
    public enum Phase { Idle, Charging, Flying, Waiting, Bite, Hook, Reeling, Card }

    public enum PullGrade { None, Perfect, Good, Miss }

    public Phase Current;
    public Vector3 Bobber = new(-3.0f, 0.06f, -4.0f);
    public float Power;
    public FishSpec? Caught;
    public float CaughtKg;
    public float CaughtValue;
    public bool Kept;
    public bool FirstCatch;      // species was new - card shows the discovery bonus
    public float CardT;
    public float ShakeT;         // camera shake on landing a fish
    public float Progress => _progress;
    public bool Reeling => Current == Phase.Reeling;

    // the reel: a sweep marker chasing a wandering zone
    public float Sweep;
    public float Zone;
    public float ZoneW;
    public int Hits;
    public int Needed;
    public float IdleT;
    public PullGrade LastPull;
    public const float BiteWindow = 2.0f;   // FISH ON! - hook it or lose it
    public float BiteT => _biteT;

    /// <summary>Fired when the bobber lands, nibbles or a fish bites.</summary>
    public event Action<Vector3>? Ripple;

    /// <summary>Fired when a cast is blocked by a full munchie bar.</summary>
    public event Action? MunchieBlocked;

    private GameState _state;
    private Vector3 _start, _target;
    private float _flyT, _waitT, _waitDur;
    private float _biteT, _hookT;
    private float _nibbleT, _nextNibble;
    private int _nibbleCount;
    private FishSpec? _pending;
    public FishSpec? Pending => _pending;
    private float _progress, _strikeCd, _zonePhase;
    private static readonly Random Rng = new();
    private static float _time;

    public FishingSim(GameState state) => _state = state;

    /// <summary>Rebind after boarding a save slot from the title screen.</summary>
    public void SetState(GameState s) => _state = s;

    /// <summary>Sound cues: "whoosh", "splash", "click".</summary>
    public Action<string> Sfx = _ => { };

    public static float Clock => _time;
    public static void TickTime(float dt) => _time += dt;

    public void Update(float dt, bool spaceDown, Vector3 rodTip, Vector3 castDir)
    {
        ShakeT = MathF.Max(0f, ShakeT - dt);
        switch (Current)
        {
            case Phase.Idle:
                if (spaceDown && _state.Couch <= 0f)
                {
                    if (_state.Munchie >= 100f)
                    {
                        MunchieBlocked?.Invoke();
                        break;
                    }
                    Current = Phase.Charging;
                    Power = 0f;
                }
                break;
            case Phase.Charging:
                Power = MathF.Min(1f, Power + dt * 1.1f);
                if (!spaceDown)
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
                Bobber = _start.Lerp(_target, t) + Vector3.Up * arc;
                if (t >= 1f)
                {
                    Bobber = _target;
                    Sfx("splash");
                    Ripple?.Invoke(Bobber);
                    // Python wait: 1.9-4.6s, shorter when you're high
                    _waitDur = _waitT = (1.9f + (float)Rng.NextDouble() * 2.7f)
                                       * (1f - 0.16f * _state.High);
                    _nibbleCount = 0;
                    _nextNibble = 0.5f + (float)Rng.NextDouble()
                                       * MathF.Max(0.1f, _waitT - 0.9f);
                    if (Rng.NextDouble() < 0.45f)
                        _nextNibble = -1f;
                    Current = Phase.Waiting;
                }
                break;
            case Phase.Waiting:
                _waitT -= dt;
                // nibbles: the bobber twitches once or twice before the bite
                if (_nibbleT > 0f)
                    _nibbleT -= dt;
                if (_nextNibble > 0f && _waitDur - _waitT >= _nextNibble
                    && _waitT > 0.25f && _nibbleCount < 2)
                {
                    _nibbleCount++;
                    _nextNibble = _waitDur - _waitT
                                  + 0.45f + (float)Rng.NextDouble() * 0.75f;
                    _nibbleT = 0.22f;
                    Ripple?.Invoke(Bobber);
                }
                if (_waitT <= 0f)
                {
                    Current = Phase.Bite;
                    _biteT = 0f;
                    Ripple?.Invoke(Bobber);
                }
                break;
            case Phase.Bite:
                _biteT += dt;
                // FISH ON! - two seconds to set the hook or the fish escapes
                if (_biteT >= BiteWindow)
                {
                    Current = Phase.Idle;
                    _pending = null;
                    Ripple?.Invoke(Bobber);
                    break;
                }
                if (spaceDown)
                {
                    _pending = Roll();
                    CaughtKg = RollKg(_pending);
                    CaughtValue = CaughtKg * _pending.UsdKg;
                    _hookT = 0f;
                    Current = Phase.Hook;
                }
                break;
            case Phase.Hook:
                _hookT += dt;
                if (_hookT >= 0.34f)
                {
                    SetupReel();
                    Current = Phase.Reeling;
                }
                break;
            case Phase.Reeling:
                UpdateReel(dt, spaceDown);
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

    /// <summary>Python reel: the sweep runs, the zone wanders, and timed
    /// pulls land perfect/good/miss. Too many misses or eight idle seconds
    /// and the fish escapes.</summary>
    private void UpdateReel(float dt, bool spaceDown)
    {
        float diff = _pending!.Diff;
        int rk = GameData.RarityIndex(_pending.Rarity);
        float reel = GameData.Rods.FirstOrDefault(
            r => r.Id == _state.EquippedId("rod"))?.Reel ?? 1f;
        float speed = 0.95f + 0.55f * diff + 0.35f * (1f - MathF.Min(1f, reel));
        Sweep = (Sweep + speed * dt) % 1f;
        _zonePhase += dt * (0.60f + 0.10f * rk + 0.30f * diff);
        Zone = 0.5f + 0.34f * MathF.Sin(_zonePhase);
        _strikeCd -= dt;

        if (spaceDown && _strikeCd <= 0f)
        {
            _strikeCd = 0.14f;
            Sfx("reel_tick");
            float d = MathF.Abs(Sweep - Zone);
            float half = ZoneW * 0.5f;
            float blue = ZoneW * 0.09f;
            if (d <= blue)
            {
                Hits += 3;
                LastPull = PullGrade.Perfect;
            }
            else if (d <= half)
            {
                Hits += 1;
                LastPull = PullGrade.Good;
            }
            else
            {
                Hits -= 2;
                LastPull = PullGrade.Miss;
                if (Hits < 0)
                {
                    Current = Phase.Idle;
                    _pending = null;
                    Caught = null;
                    Ripple?.Invoke(Bobber);
                    return;
                }
            }
            _progress = Math.Clamp(Hits / (float)Needed, 0f, 1f);
            IdleT = 0f;
            if (Hits >= Needed)
            {
                Caught = _pending;
                _state.Catches++;
                int slots = _state.BagSize();
                Kept = _state.Bag.Count < slots;
                if (!Kept)
                    _state.Cash += (int)MathF.Round(CaughtValue); // sold on spot
                FirstCatch = _state.OnCatch(_pending.Id, CaughtKg, Kept);
                _state.Cash += (int)MathF.Round(CaughtValue);
                _state.Save();
                CardT = 0f;
                ShakeT = 0.45f;
                Current = Phase.Card;
            }
        }
        if (_progress <= 0.001f)
        {
            IdleT += dt;
            if (IdleT > 8f)
            {
                Current = Phase.Idle;
                _pending = null;
                Caught = null;
                Ripple?.Invoke(Bobber);
            }
        }
        else
        {
            IdleT = 0f;
        }
    }

    /// <summary>Python hook setup: hits needed, zone width, fresh sweep.</summary>
    private void SetupReel()
    {
        int rk = GameData.RarityIndex(_pending!.Rarity);
        Needed = Math.Max(2, (int)MathF.Round(5 + rk * 1.3f + _pending.Diff * 2f));
        float tol = GameData.Rods.FirstOrDefault(
            r => r.Id == _state.EquippedId("rod"))?.Tol ?? 0f;
        ZoneW = Math.Clamp(0.22f - 0.030f * rk - 0.055f * _pending.Diff
                           + tol * 0.20f + 0.010f * _state.StokeRank("fish"),
                           0.04f, 0.26f);
        Sweep = 0f;
        _zonePhase = 0f;
        Hits = 0;
        IdleT = 0f;
        _progress = 0f;
        LastPull = PullGrade.None;
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

    private static float RollKg(FishSpec sp) =>
        sp.KgMin + (float)Rng.NextDouble() * (sp.KgMax - sp.KgMin);

}
