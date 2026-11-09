using Godot;
using System.Text.Json;

namespace HotRodsSharp;

/// <summary>
/// Game state with save slots 1-3 (save.json, save2.json, save3.json like the
/// Python prototype), xp/level/stoke lanes, Sal's jar economy and the cooler.
/// </summary>
public sealed class GameState
{
    public int Slot { get; set; } = 1;
    public int Cash { get; set; }
    public int Casts { get; set; }
    public int Catches { get; set; }
    public List<string> Bag { get; set; } = new();        // species ids in cooler
    public List<float> BagKg { get; set; } = new();       // parallel weights
    public Dictionary<string, int> Species { get; set; } = new();  // id -> count
    public int Spot { get; set; }
    public string Strain { get; set; } = "homegrown";
    public string Smoker { get; set; } = "one_hitter";    // equipped piece
    public float Chill { get; set; }                      // 0..100, the meter
    public float Couch { get; set; }                      // couch-lock seconds
    public float Munchie { get; set; }                    // 0..100, snacks bring it down

    /// <summary>Python parity: high is chill/100; legacy saves set High
    /// directly and the setter migrates them onto the chill meter.</summary>
    public float High
    {
        get => Math.Clamp(Chill / 100f, 0f, 1f);
        set => Chill = value * 100f;
    }
    public float Hour { get; set; } = 8f;
    public float Played { get; set; }                     // minutes afloat
    public int Xp { get; set; }
    public int StokePts { get; set; }
    public Dictionary<string, int> Stoke { get; set; } = new()
        { ["fish"] = 0, ["smoke"] = 0, ["vibes"] = 0 };
    public Dictionary<string, int> WeedCounts { get; set; } = new();  // grams
    public HashSet<string> HiddenJars { get; set; } = new();
    public int StrainIndex { get; set; }
    public int SalTier { get; set; } = -1;                // best rarity ever sold
    public int MaxWater { get; set; }
    /// <summary>Waters the trip has paid to reach (travel gating).</summary>
    public HashSet<int> Unlocked { get; set; } = new() { 0 };
    public Dictionary<string, HashSet<string>> OwnedGear { get; set; } = new();
    public Dictionary<string, string> Equipped { get; set; } = new();

    /// <summary>Fires once per level gained (stoke point earned). Events are
    /// not serialized by System.Text.Json, so this never touches save files.</summary>
    public event Action<int>? LeveledUp;

    // ------------------------------------------------------------------ save --
    private static string SlotPath(int slot) => slot == 1
        ? ProjectSettings.GlobalizePath("user://save.json")
        : ProjectSettings.GlobalizePath($"user://save{slot}.json");

    public static bool AnySave() =>
        Enumerable.Range(1, 3).Any(i => File.Exists(SlotPath(i)));

    public static void DeleteSlot(int slot)
    {
        try
        {
            if (File.Exists(SlotPath(slot)))
                File.Delete(SlotPath(slot));
        }
        catch
        {
            // deletion is best-effort
        }
    }

    public static int FreeSlot() =>
        Enumerable.Range(1, 3).FirstOrDefault(i => !File.Exists(SlotPath(i)));

    /// <summary>Legacy entry point: save.json is slot 1.</summary>
    public static GameState Load() => Load(1);

    public static GameState Load(int slot)
    {
        try
        {
            if (File.Exists(SlotPath(slot)))
            {
                var st = JsonSerializer.Deserialize<GameState>(
                    File.ReadAllText(SlotPath(slot)));
                if (st != null)
                {
                    st.Slot = slot;
                    // legacy saves predate travel gating: grant waters up to
                    // the furthest one visited so nobody loses access
                    for (int i = 1; i <= st.MaxWater; i++)
                        st.Unlocked.Add(i);
                    return st;
                }
            }
        }
        catch
        {
            // a corrupt save never blocks the game
        }
        return New(slot);
    }

    public static GameState New(int slot) => new() { Slot = slot };

    public void Save()
    {
        try
        {
            File.WriteAllText(SlotPath(Slot), JsonSerializer.Serialize(this));
        }
        catch
        {
            // saving is best-effort
        }
    }

    public static void Scrap(int slot)
    {
        try { File.Delete(SlotPath(slot)); }
        catch { /* best-effort */ }
    }

    // ------------------------------------------------------------- xp/stoke --
    private static readonly int[] LevelXp =
        { 0, 120, 320, 620, 1050, 1650, 2450, 3500, 4850, 6500 };

    /// <summary>Level 1..10, one stoke point per level-up.</summary>
    public int Level => LevelXp.Count(t => Xp >= t);

    public int XpToNext()
    {
        int next = LevelXp.FirstOrDefault(t => t > Xp);
        return next == 0 ? 0 : next - Xp;
    }

    public void AddXp(int amount)
    {
        int before = Level;
        Xp += amount;
        int gained = Math.Max(0, Level - before);
        StokePts += gained;
        for (int i = 1; i <= gained; i++)
            LeveledUp?.Invoke(before + i);
    }

    public int StokeRank(string lane) =>
        Stoke.TryGetValue(lane, out var r) ? r : 0;

    public (bool ok, string msg) SpendStoke(string lane)
    {
        if (!Stoke.ContainsKey(lane))
            return (false, "no such lane");
        if (StokePts <= 0)
            return (false, "no stoke points - level up");
        if (Stoke[lane] >= 8)
            return (false, "lane maxed");
        StokePts--;
        Stoke[lane]++;
        Save();
        return (true, lane + " rank " + Stoke[lane]);
    }

    // ---------------------------------------------------------------- cooler --
    public int BagSize() =>
        GameData.Coolers.FirstOrDefault(c => c.Id == EquippedId("cooler"))?.Slots ?? 4;

    public float BagEntryValue(int i)
    {
        var sp = GameData.Species.FirstOrDefault(s => s.Id == Bag[i]);
        if (sp == null)
            return 0f;
        float kg = i < BagKg.Count ? BagKg[i] : (sp.KgMin + sp.KgMax) / 2f;
        return kg * sp.UsdKg;
    }

    public float BagValue()
    {
        float total = 0f;
        for (int i = 0; i < Bag.Count; i++)
            total += BagEntryValue(i);
        return total;
    }

    private int SellCore(int index)
    {
        int v = (int)MathF.Round(BagEntryValue(index));
        var sp = GameData.Species.FirstOrDefault(s => s.Id == Bag[index]);
        if (sp != null)
            SalTier = Math.Max(SalTier, GameData.RarityIndex(sp.Rarity));
        Cash += v;
        Bag.RemoveAt(index);
        if (index < BagKg.Count)
            BagKg.RemoveAt(index);
        return v;
    }

    public int SellFish(int index)
    {
        if (index < 0 || index >= Bag.Count)
            return 0;
        int v = SellCore(index);
        Save();
        return v;
    }

    /// <summary>Sell the whole cooler; returns fish count (legacy shape).</summary>
    public int SellBag()
    {
        int n = Bag.Count;
        while (Bag.Count > 0)
            SellCore(0);
        if (n > 0)
            Save();
        return n;
    }

    public int BagBestTier()
    {
        int best = -1;
        foreach (var id in Bag)
        {
            var sp = GameData.Species.FirstOrDefault(f => f.Id == id);
            if (sp != null)
                best = Math.Max(best, GameData.RarityIndex(sp.Rarity));
        }
        return best;
    }

    /// <summary>
    /// Record a catch: species log, hidden jar unlocks, xp (Python formula
    /// 6 + rarity*4 + 12 if new), discovery bonus (1x value on first catch),
    /// cooler. Returns true when the species was new.
    /// </summary>
    public bool OnCatch(string fishId, float kg, bool toBag)
    {
        bool first = !Species.ContainsKey(fishId);
        Species[fishId] = Species.GetValueOrDefault(fishId) + 1;
        var sp = GameData.Species.FirstOrDefault(s => s.Id == fishId);
        if (sp != null)
        {
            foreach (var st in GameData.Strains.Where(s => s.UnlockFish == fishId))
                HiddenJars.Add(st.Id);
            int ri = GameData.RarityIndex(sp.Rarity);
            AddXp(6 + ri * 4 + (first ? 12 : 0));
            if (first)
                Cash += (int)MathF.Round(kg * sp.UsdKg);   // discovery bonus
        }
        if (toBag)
        {
            Bag.Add(fishId);
            BagKg.Add(kg);
        }
        return first;
    }

    // ---------------------------------------------------------------- travel --
    public bool SpotUnlocked(int index) => index == 0 || Unlocked.Contains(index);

    public int SpeciesFound(int spot) => GameData.SpeciesList
        .Count(sp => sp.Spot == spot && Species.ContainsKey(sp.Id));

    /// <summary>Python can_travel: the previous water must be fished out,
    /// then the spot's need_cash buys the trip.</summary>
    public (bool ok, string msg) TryTravel(int index)
    {
        if (index < 0 || index >= 4)
            return (false, "no such water");
        if (SpotUnlocked(index))
            return (true, "");
        if (index > 0)
        {
            int prev = index - 1;
            int need = GameData.SpeciesList.Count(sp => sp.Spot == prev);
            int got = SpeciesFound(prev);
            if (got < need)
                return (false, "Catch all " + need + " in "
                              + GameData.SpotNames[prev] + " (" + got + "/" + need + ")");
        }
        int cost = GameData.Spots[index].NeedCash;
        if (Cash < cost)
            return (false, "Need $" + cost + " to get there");
        Cash -= cost;
        Unlocked.Add(index);
        MaxWater = Math.Max(MaxWater, index);
        return (true, "");
    }

    // ------------------------------------------------------------------- gear --
    public bool Owns(string kind, string id)
    {
        if (id is "bamboo" or "light" or "worm" or "small" or "one_hitter")
            return true;
        return OwnedGear.TryGetValue(kind, out var set) && set.Contains(id);
    }

    public string EquippedId(string kind) =>
        Equipped.TryGetValue(kind, out var id) ? id
        : kind switch
        {
            "rod" => "bamboo", "line" => "light", "lure" => "worm", "cooler" => "small",
            "smoker" => "one_hitter",
            _ => "bamboo",
        };

    public Gear[] GearTable(string kind) => kind switch
    {
        "rod" => GameData.Rods, "line" => GameData.Lines,
        "lure" => GameData.Lures, "smoker" => GameData.Smokers,
        _ => GameData.Coolers,
    };

    public (bool ok, string msg) BuyGear(string kind, Gear g)
    {
        if (Owns(kind, g.Id))
        {
            Equipped[kind] = g.Id;
            Save();
            return (true, "equipped " + g.Name);
        }
        if (Cash < g.Cost)
            return (false, "need $" + g.Cost);
        Cash -= g.Cost;
        if (!OwnedGear.TryGetValue(kind, out var set))
            OwnedGear[kind] = set = new HashSet<string>();
        set.Add(g.Id);
        Equipped[kind] = g.Id;
        Save();
        return (true, "bought " + g.Name);
    }

    // ------------------------------------------------------------- Sal's jars --
    public Strain StrainData()
    {
        var sd = GameData.Strains.FirstOrDefault(s => s.Id == Strain);
        return sd ?? GameData.Strains[0];
    }

    public bool ForeignWeed(int spot) => StrainData().Area != spot;

    public (int lo, int hi) RarityBand()
    {
        if (High <= 0.10f)          // Python is_high: chill > 10 of 100
            return (0, 0);
        var sd = StrainData();
        int lo = sd.GateMin, hi = sd.GateMax;
        if (ForeignWeed(Spot))
            return (0, Math.Min(hi, 1));   // old weed: commons + uncommons only
        return (lo, hi);
    }

    /// <summary>A strain needs its water reached, its tier sold, and - when
    /// hidden - its unlock fish caught.</summary>
    public bool JarAvailable(Strain st)
    {
        if (st.Hidden)
            return HiddenJars.Contains(st.Id);
        if (st.Area > MaxWater)
            return false;
        return st.Tier <= SalTier + 1 || st.Tier <= 0;
    }

    public int WeedCount(string sid) => WeedCounts.GetValueOrDefault(sid);

    public int WeedCount() => WeedCounts.GetValueOrDefault(Strain);

    public int WeedTotal() => WeedCounts.Values.Sum();

    // ------------------------------------------------------------ chill/smoke --
    private const float TokeAmount = 42f;      // chill per gram
    private const float ChillDecay = 2.1f;     // per second
    private const float CouchLockTime = 2.6f;

    /// <summary>Python tick: decay scales with strain duration, smoker burn
    /// and the vibes stoke lane; couch lock fades and clears under 50.</summary>
    public void TickChill(float dt)
    {
        if (Couch > 0f)
        {
            Couch = MathF.Max(0f, Couch - dt);
            if (Chill <= 50f)
                Couch = 0f;
        }
        if (Chill <= 0f)
            return;
        var sd = StrainData();
        var smoker = GameData.Smokers.FirstOrDefault(g => g.Id == Smoker)
                     ?? GameData.Smokers[0];
        float mult = sd.Duration * smoker.Dur * (1f + 0.05f * StokeRank("vibes"));
        float decay = ChillDecay / MathF.Max(0.3f, mult);
        Chill = MathF.Max(0f, Chill - decay * dt);
    }

    /// <summary>Can the ceremony start? Blocks couch lock and an empty stash
    /// (auto-switching to the fullest jar when the loaded one is dry).</summary>
    public (bool ok, string msg) CanToke()
    {
        if (Munchie >= 100f)
            return (false, "MUNCHIES, go eat food to keep fishing");
        if (Couch > 0f)
            return (false, "couch locked - give it a second");
        if (WeedCount() > 0)
            return (true, "");
        var best = WeedCounts.OrderByDescending(kv => kv.Value).FirstOrDefault();
        if (best.Value <= 0)
            return (false, "stash is dry - see Sal");
        Strain = best.Key;
        StrainIndex = Array.IndexOf(GameData.Strains, GameData.Strains.First(x => x.Id == Strain));
        return (true, "");
    }

    /// <summary>Eat a snack: cash out, munchie meter down. Pricier snacks
    /// knock more off.</summary>
    public (bool ok, string msg) BuySnack(GameData.Munchie m)
    {
        if (Cash < m.Price)
            return (false, "not enough cash for " + m.Name);
        Cash -= m.Price;
        Munchie = MathF.Max(0f, Munchie - m.Relief);
        Save();
        return (true, m.Name + " - munchie " + m.Relief + " off");
    }

    /// <summary>Burn a gram when the ceremony finishes: chill climbs by the
    /// toke amount (piece fill + smoke stoke lane), and toking past 78 on an
    /// already-high meter drops you into couch lock.</summary>
    public string ApplyToke()
    {
        var smoker = GameData.Smokers.FirstOrDefault(g => g.Id == Smoker)
                     ?? GameData.Smokers[0];
        bool over = Chill >= 78f;
        WeedCounts[Strain] = Math.Max(0, WeedCount() - 1);
        Munchie = MathF.Min(100f, Munchie + 10f);   // every toke stirs the munchies
        Chill = MathF.Min(100f, Chill + TokeAmount * smoker.Fill
                            * (1f + 0.06f * StokeRank("smoke")));
        if (over)
        {
            Couch = CouchLockTime;
            Chill = 100f;
            Save();
            return "couch locked - you're zonked";
        }
        Save();
        return "smoking " + StrainData().Name;
    }

    public (bool ok, string msg) BuyWeed(Strain st)
    {
        if (!JarAvailable(st))
            return (false, "Sal wants a better fish first");
        if (Cash < st.Price)
            return (false, "need $" + st.Price);
        Cash -= st.Price;
        WeedCounts[st.Id] = WeedCount(st.Id) + 1;
        Save();
        return (true, "one gram of " + st.Name);
    }

    /// <summary>Legacy name kept for the shop's WEED tab.</summary>
    public (bool ok, string msg) BuyStrain(Strain st) => BuyWeed(st);

    public (bool ok, string msg) UseWeed(Strain st)
    {
        if (WeedCount(st.Id) <= 0)
            return (false, "no " + st.Name + " left");
        Strain = st.Id;
        StrainIndex = Array.IndexOf(GameData.Strains, st);
        Save();
        return (true, "loaded " + st.Name);
    }

    // ------------------------------------------------------------- slot info --
    public static bool SlotExists(int slot) => File.Exists(SlotPath(slot));

    /// <summary>Cheap metadata peek for the TRIPS browser (no full load).</summary>
    public static (bool exists, int level, int cash, int spot, int species, int mins)
        SlotInfo(int slot)
    {
        try
        {
            if (!File.Exists(SlotPath(slot)))
                return (false, 0, 0, 0, 0, 0);
            using var doc = JsonDocument.Parse(File.ReadAllText(SlotPath(slot)));
            var root = doc.RootElement;
            int GetInt(string name) =>
                root.TryGetProperty(name, out var e) && e.TryGetInt32(out var v) ? v : 0;
            int mins = root.TryGetProperty("played", out var p)
                       && p.TryGetSingle(out var pf) ? (int)pf : 0;
            int xp = GetInt("xp");
            int species = root.TryGetProperty("species", out var s)
                          && s.ValueKind == JsonValueKind.Object
                ? s.EnumerateObject().Count() : 0;
            return (true, LevelXp.Count(t => xp >= t), GetInt("cash"),
                    GetInt("spot"), species, mins);
        }
        catch
        {
            return (false, 0, 0, 0, 0, 0);
        }
    }
}
