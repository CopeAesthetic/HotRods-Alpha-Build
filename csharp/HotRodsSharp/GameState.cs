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
    public float High { get; set; }                       // 0..1 remaining
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
    public Dictionary<string, HashSet<string>> OwnedGear { get; set; } = new();
    public Dictionary<string, string> Equipped { get; set; } = new();

    // ------------------------------------------------------------------ save --
    private static string SlotPath(int slot) => slot == 1
        ? Path.Combine(AppContext.BaseDirectory, "save.json")
        : Path.Combine(AppContext.BaseDirectory, $"save{slot}.json");

    public static bool AnySave() =>
        Enumerable.Range(1, 3).Any(i => File.Exists(SlotPath(i)));

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
        StokePts += Math.Max(0, Level - before);
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

    /// <summary>Record a catch: species log, hidden jar unlocks, xp, cooler.</summary>
    public void OnCatch(string fishId, float kg, bool toBag)
    {
        bool first = !Species.ContainsKey(fishId);
        Species[fishId] = Species.GetValueOrDefault(fishId) + 1;
        var sp = GameData.Species.FirstOrDefault(s => s.Id == fishId);
        if (sp != null)
        {
            foreach (var st in GameData.Strains.Where(s => s.UnlockFish == fishId))
                HiddenJars.Add(st.Id);
            AddXp((GameData.RarityIndex(sp.Rarity) + 1) * (first ? 40 : 20));
        }
        if (toBag)
        {
            Bag.Add(fishId);
            BagKg.Add(kg);
        }
    }

    // ------------------------------------------------------------------- gear --
    public bool Owns(string kind, string id)
    {
        if (id is "bamboo" or "light" or "worm" or "small")
            return true;
        return OwnedGear.TryGetValue(kind, out var set) && set.Contains(id);
    }

    public string EquippedId(string kind) =>
        Equipped.TryGetValue(kind, out var id) ? id
        : kind switch
        {
            "rod" => "bamboo", "line" => "light", "lure" => "worm", "cooler" => "small",
            _ => "bamboo",
        };

    public Gear[] GearTable(string kind) => kind switch
    {
        "rod" => GameData.Rods, "line" => GameData.Lines,
        "lure" => GameData.Lures, _ => GameData.Coolers,
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
        if (High <= 0f)
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
