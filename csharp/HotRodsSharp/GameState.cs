using System.Text.Json;

namespace HotRodsSharp;

/// <summary>
/// Phase-4 game state: cash, cooler, species log, current strain and the high
/// timer, with JSON save/load matching the Python save's spirit.
/// </summary>
public sealed class GameState
{
    public int Cash { get; set; }
    public int Casts { get; set; }
    public int Catches { get; set; }
    public List<string> Bag { get; set; } = new();          // species ids caught
    public Dictionary<string, int> Species { get; set; } = new();  // id -> count
    public int Spot { get; set; }
    public string Strain { get; set; } = "homegrown";
    public float High { get; set; }                         // 0..1 remaining
    public float Hour { get; set; } = 8f;
    public int StrainIndex { get; set; }
    public int SalTier { get; set; } = -1;                 // best rarity ever sold
    public int MaxWater { get; set; }
    public Dictionary<string, HashSet<string>> OwnedGear { get; set; } = new();
    public Dictionary<string, string> Equipped { get; set; } = new();

    private static readonly string SavePath =
        Path.Combine(AppContext.BaseDirectory, "save.json");

    public static GameState Load()
    {
        try
        {
            if (File.Exists(SavePath))
                return JsonSerializer.Deserialize<GameState>(
                           File.ReadAllText(SavePath)) ?? new GameState();
        }
        catch
        {
            // a corrupt save never blocks the game
        }
        return new GameState();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(SavePath, JsonSerializer.Serialize(this));
        }
        catch
        {
            // saving is best-effort
        }
    }

    public Strain StrainData()
    {
        var sd = GameData.Strains.FirstOrDefault(s => s.Id == Strain);
        return sd ?? GameData.Strains[0];
    }

    /// <summary>True when the smoked strain comes from another water.</summary>
    public bool ForeignWeed(int spot) => StrainData().Area != spot;

    /// <summary>Rarity band in reach right now (0..4 indexes).</summary>
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

    public float BagValue()
    {
        float total = 0f;
        foreach (var id in Bag)
        {
            var sp = GameData.Species.FirstOrDefault(s => s.Id == id);
            if (sp != null)
                total += (sp.KgMin + sp.KgMax) / 2f * sp.UsdKg;
        }
        return total;
    }

    public int SellBag()
    {
        int n = Bag.Count;
        foreach (var id in Bag)
        {
            var sp = GameData.Species.FirstOrDefault(f => f.Id == id);
            if (sp != null)
                SalTier = Math.Max(SalTier, GameData.RarityIndex(sp.Rarity));
        }
        Cash += (int)MathF.Round(BagValue());
        Bag.Clear();
        Save();
        return n;
    }

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

    /// <summary>Buy gear for cash. Returns false + reason on failure.</summary>
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

    /// <summary>Sal's jars: a strain needs its water reached and its tier sold.</summary>
    public bool JarOpen(Strain st)
    {
        if (st.Area > MaxWater)
            return false;
        return st.Tier <= SalTier + 1 || st.Tier <= 0;
    }

    public (bool ok, string msg) BuyStrain(Strain st)
    {
        if (!JarOpen(st))
            return (false, "Sal wants a better fish first");
        if (Cash < st.Price)
            return (false, "need $" + st.Price);
        Cash -= st.Price;
        Strain = st.Id;
        StrainIndex = Array.IndexOf(GameData.Strains, st);
        Save();
        return (true, "+" + st.Name);
    }
}
