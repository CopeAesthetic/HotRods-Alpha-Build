using System.Text.Json;
using Microsoft.Xna.Framework;

namespace HotRodsSharp;

// ------------------------------------------------------------------ records --
// Full field set mirroring content/*.json (the single source of truth shared
// with the Python prototype). See tools/export_content.py for regeneration.

public sealed record FishSpec(
    string Id, string Name, string? Sci, int Spot, string Rarity,
    float KgMin, float KgMax, float UsdKg, float Diff,
    Color Body, Color Fin, Color Accent, string Shape, string? Pattern,
    string? Cond, string? Flavor, string? Hint,
    bool IsShiny = false, bool IsHidden = false, string? ShinyOf = null);

public sealed record Strain(
    string Id, string Name, int Area, int Tier, int GateMin, int GateMax,
    int Potency, float Luck, float Duration, int Price,
    float Wobble = 1f, float Cash = 1f, Color Color = default,
    string Desc = "", bool Hidden = false, string? UnlockFish = null);

public sealed record Gear(
    string Id, string Name, int Cost, string Desc,
    float Reel = 1f, float Grace = 0f, int Slots = 0, int Ice = 0,
    float Power = 1f, float Tol = 0f, float Size = 1f,
    Dictionary<string, float>? Bias = null,
    Dictionary<string, float>? NightBias = null,
    float Ceremony = 0f, float Dur = 1f, float Fill = 1f,
    int Potency = 0, float Luck = 0f, bool Conc = false,
    Color Color = default);

public sealed record Spot(
    string Id, string Name, string Blurb, int NeedSpecies, int NeedCash,
    Color Tint, float Murk, float Waterline);

// ------------------------------------------------------------------- data ---
public static class GameData
{
    public static readonly string[] RarityOrder =
        { "common", "uncommon", "rare", "epic", "legendary" };

    public static readonly float[] RarityWeight = { 10f, 5f, 2.5f, 1f, 0.4f };

    // fish: SpeciesList (32) + HiddenFish (4) + ShinyFish (4); Species is the
    // merged view the sim has always rolled from.
    public static FishSpec[] SpeciesList { get; private set; } = [];
    public static FishSpec[] HiddenFish { get; private set; } = [];
    public static FishSpec[] ShinyFish { get; private set; } = [];
    public static FishSpec[] Species { get; private set; } = [];

    public static Strain[] Strains { get; private set; } = [];
    public static Gear[] Rods { get; private set; } = [];
    public static Gear[] Lines { get; private set; } = [];
    public static Gear[] Lures { get; private set; } = [];
    public static Gear[] Coolers { get; private set; } = [];
    public static Gear[] Smokers { get; private set; } = [];
    public static Spot[] Spots { get; private set; } = [];
    public static string[] SpotNames { get; private set; } = [];
    public static string[] Tips { get; private set; } = [];

    public static float ShinyOdds { get; private set; }
    public static float[] SpotPay { get; private set; } = [];
    public static Dictionary<string, int> GearSpot { get; private set; } = new();
    public static int IceCost { get; private set; }
    public static float IceMinutes { get; private set; }
    public static float SpoilRate { get; private set; }
    public static float SpoilFloor { get; private set; }
    public static int ConcentratePotency { get; private set; }
    public static float ConcentrateMult { get; private set; }

    // ------------------------------------------------------------------ load --
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Load content/*.json from next to the executable. Call once at startup
    /// (Program.cs). Throws with a clear message if the content folder is
    /// missing so packaging mistakes fail loudly instead of at runtime.
    /// </summary>
    public static void Load()
    {
        var fish = LoadJson<FishFile>("fish.json");
        SpeciesList = fish.Species.Select(f => MapFish(f)).ToArray();
        HiddenFish = fish.Hidden.Select(f => MapFish(f, isHidden: true)).ToArray();
        ShinyFish = fish.Shiny.Select(f => MapFish(f, isShiny: true)).ToArray();
        Species = SpeciesList.Concat(HiddenFish).Concat(ShinyFish).ToArray();

        Strains = LoadJson<List<StrainDto>>("strains.json").Select(MapStrain).ToArray();
        Spots = LoadJson<List<SpotDto>>("spots.json").Select(MapSpot).ToArray();
        SpotNames = Spots.Select(s => s.Name).ToArray();
        Tips = LoadJson<List<string>>("tips.json").ToArray();

        var gear = LoadJson<GearFile>("gear.json");
        Rods = gear.Rods.Select(MapGear).ToArray();
        Lines = gear.Lines.Select(MapGear).ToArray();
        Lures = gear.Lures.Select(MapGear).ToArray();
        Coolers = gear.Coolers.Select(MapGear).ToArray();
        Smokers = gear.Smokers.Select(MapGear).ToArray();

        var econ = LoadJson<EconomyDto>("economy.json");
        ShinyOdds = econ.ShinyOdds;
        SpotPay = econ.SpotPay ?? [];
        GearSpot = econ.GearSpot ?? new Dictionary<string, int>();
        IceCost = econ.IceCost;
        IceMinutes = econ.IceMinutes;
        SpoilRate = econ.SpoilRate;
        SpoilFloor = econ.SpoilFloor;
        ConcentratePotency = econ.ConcentratePotency;
        ConcentrateMult = econ.ConcentrateMult;
    }

    private static T LoadJson<T>(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "content", name);
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Missing content file '{path}'. content/*.json is the single source " +
                "of truth - regenerate it with 'python tools/export_content.py'.");
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOpts)
               ?? throw new InvalidDataException($"content/{name} failed to parse");
    }

    private static FishSpec MapFish(FishDto f, bool isHidden = false, bool isShiny = false) =>
        new(f.Id, f.Name, f.Sci, f.Spot, f.Rarity,
            f.Kg is { Length: >= 2 } ? f.Kg[0] : 0f,
            f.Kg is { Length: >= 2 } ? f.Kg[1] : 0f,
            f.UsdKg, f.Diff,
            Rgb(f.Body), Rgb(f.Fin), Rgb(f.Accent),
            f.Shape, f.Pattern, f.Cond, f.Flavor, f.Hint,
            isShiny, isHidden, f.ShinyOf);

    private static Strain MapStrain(StrainDto s) =>
        new(s.Id, s.Name, s.Area, s.Tier, s.GateMin, s.GateMax,
            s.Potency, s.Luck, s.Duration, s.Pts,
            s.Wobble, s.Cash, Rgb(s.Color), s.Desc, s.Hidden, s.UnlockFish);

    private static Gear MapGear(GearDto g) =>
        new(g.Id, g.Name, g.Cost, g.Desc,
            g.Reel, g.Grace, g.Slots, g.Ice,
            g.Power, g.Tol, g.Size,
            g.Bias, g.NightBias,
            g.Ceremony, g.Dur, g.Fill,
            g.Potency, g.Luck, g.Conc, Rgb(g.Color));

    private static Spot MapSpot(SpotDto s) =>
        new(s.Id, s.Name, s.Blurb, s.NeedSpecies, s.NeedCash,
            Rgb(s.Tint), s.Murk, s.Waterline);

    private static Color Rgb(int[]? rgb) =>
        rgb is { Length: >= 3 } ? new Color(rgb[0], rgb[1], rgb[2]) : Color.White;

    // ------------------------------------------------------------------ misc --
    public static int RarityIndex(string r)
    {
        for (int i = 0; i < RarityOrder.Length; i++)
            if (RarityOrder[i] == r) return i;
        return 0;
    }

    public static FishSpec[] ForSpot(int spot) =>
        Species.Where(s => s.Spot == spot).ToArray();
}

// ------------------------------------------------------------------- DTOs ---
sealed class FishFile
{
    public List<FishDto> Species { get; set; } = [];
    public List<FishDto> Hidden { get; set; } = [];
    public List<FishDto> Shiny { get; set; } = [];
}

sealed class GearFile
{
    public List<GearDto> Rods { get; set; } = [];
    public List<GearDto> Lines { get; set; } = [];
    public List<GearDto> Lures { get; set; } = [];
    public List<GearDto> Coolers { get; set; } = [];
    public List<GearDto> Smokers { get; set; } = [];
}

sealed class FishDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Sci { get; set; }
    public int Spot { get; set; }
    public string Rarity { get; set; } = "";
    public float[]? Kg { get; set; }
    public float UsdKg { get; set; }
    public float Diff { get; set; }
    public int[]? Body { get; set; }
    public int[]? Fin { get; set; }
    public int[]? Accent { get; set; }
    public string Shape { get; set; } = "";
    public string? Pattern { get; set; }
    public string? Cond { get; set; }
    public string? Flavor { get; set; }
    public string? Hint { get; set; }
    public string? ShinyOf { get; set; }
}

sealed class StrainDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Area { get; set; }
    public int Tier { get; set; }
    public int GateMin { get; set; }
    public int GateMax { get; set; }
    public int Pts { get; set; }
    public int Potency { get; set; }
    public float Luck { get; set; }
    public float Duration { get; set; }
    public float Wobble { get; set; }
    public float Cash { get; set; }
    public int[]? Color { get; set; }
    public string Desc { get; set; } = "";
    public bool Hidden { get; set; }
    public string? UnlockFish { get; set; }
}

sealed class GearDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Cost { get; set; }
    public string Desc { get; set; } = "";
    public float Reel { get; set; }
    public float Grace { get; set; }
    public int Slots { get; set; }
    public int Ice { get; set; }
    public float Power { get; set; }
    public float Tol { get; set; }
    public float Size { get; set; }
    public Dictionary<string, float>? Bias { get; set; }
    public Dictionary<string, float>? NightBias { get; set; }
    public float Ceremony { get; set; }
    public float Dur { get; set; }
    public float Fill { get; set; }
    public int Potency { get; set; }
    public float Luck { get; set; }
    public bool Conc { get; set; }
    public int[]? Color { get; set; }
}

sealed class SpotDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Blurb { get; set; } = "";
    public int NeedSpecies { get; set; }
    public int NeedCash { get; set; }
    public int[]? Tint { get; set; }
    public float Murk { get; set; }
    public float Waterline { get; set; }
}

sealed class EconomyDto
{
    public float ShinyOdds { get; set; }
    public float[]? SpotPay { get; set; }
    public Dictionary<string, int>? GearSpot { get; set; }
    public int IceCost { get; set; }
    public float IceMinutes { get; set; }
    public float SpoilRate { get; set; }
    public float SpoilFloor { get; set; }
    public int ConcentratePotency { get; set; }
    public float ConcentrateMult { get; set; }
}
