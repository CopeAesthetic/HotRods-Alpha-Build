namespace HotRodsSharp;

public sealed record FishSpec(string Id, string Name, int Spot,
        string Rarity, float KgMin, float KgMax, float UsdKg,
        Color Body);

public sealed record Strain(string Id, string Name, int Area, int Tier,
        int GateMin, int GateMax, int Potency, float Luck, float Duration,
        int Price);

public sealed record Gear(string Id, string Name, int Cost, string Desc);

public static class GameData
{
    public static readonly string[] RarityOrder =
        { "common", "uncommon", "rare", "epic", "legendary" };

    public static readonly float[] RarityWeight = { 10f, 5f, 2.5f, 1f, 0.4f };

    public static readonly FishSpec[] Species =
    {
        new("bluegill", "Bonggill", 0, "common", 0.08f, 0.55f, 3.6f, new Color(118, 146, 120)),
        new("pumpkinseed", "Puffkinseed", 0, "common", 0.05f, 0.3f, 4.0f, new Color(150, 158, 96)),
        new("green_sunfish", "Green Sunbaked", 0, "common", 0.08f, 0.45f, 3.2f, new Color(106, 130, 86)),
        new("yellow_bullhead", "Yellow Bowlhead", 0, "uncommon", 0.2f, 1.4f, 3.8f, new Color(112, 98, 66)),
        new("largemouth_bass", "Largemouth Blazed", 0, "uncommon", 0.4f, 3.0f, 4.6f, new Color(106, 138, 88)),
        new("common_carp", "Carp Diem", 0, "rare", 1.5f, 9.0f, 2.2f, new Color(168, 140, 86)),
        new("flathead_catfish", "Catatonic Catfish", 0, "epic", 4.0f, 20.0f, 6.0f, new Color(116, 98, 70)),
        new("golden_koi", "Golden Kief", 0, "legendary", 1.0f, 5.0f, 45.0f, new Color(250, 190, 90)),
        new("yellow_perch", "Mellow Perch", 1, "common", 0.1f, 0.55f, 8.0f, new Color(196, 186, 104)),
        new("black_crappie", "Black Hashpie", 1, "common", 0.15f, 0.8f, 9.0f, new Color(176, 186, 192)),
        new("rainbow_trout", "Rainbow Runtz", 1, "uncommon", 0.4f, 2.8f, 12.0f, new Color(168, 180, 188)),
        new("smallmouth_bass", "Smallmouth Blaze", 1, "uncommon", 0.4f, 2.4f, 9.5f, new Color(150, 132, 96)),
        new("northern_pike", "Northern Pipe", 1, "rare", 1.0f, 7.0f, 7.5f, new Color(108, 138, 84)),
        new("walleye", "Wally High", 1, "rare", 0.5f, 4.0f, 15.0f, new Color(178, 166, 120)),
        new("muskellunge", "Musky Munchies", 1, "epic", 4.0f, 16.0f, 8.0f, new Color(124, 146, 110)),
        new("golden_trout", "Golden Runtz", 1, "legendary", 0.5f, 2.5f, 45.0f, new Color(250, 212, 88)),
        new("bowfin", "Bongfin", 2, "common", 0.5f, 3.0f, 4.0f, new Color(122, 116, 78)),
        new("brown_bullhead", "Brown Bowlhead", 2, "common", 0.2f, 1.6f, 4.2f, new Color(108, 84, 60)),
        new("longnose_gar", "Longnose Ganja", 2, "uncommon", 0.8f, 4.0f, 5.0f, new Color(128, 138, 96)),
        new("american_eel", "Spliff Eel", 2, "uncommon", 0.3f, 1.8f, 16.0f, new Color(110, 112, 86)),
        new("blue_catfish", "Blue Catnap", 2, "rare", 2.0f, 12.0f, 4.8f, new Color(118, 132, 150)),
        new("snakehead", "Northern Bakehead", 2, "rare", 0.8f, 4.0f, 8.5f, new Color(128, 110, 84)),
        new("alligator_gar", "Alligator Hash", 2, "epic", 6.0f, 28.0f, 5.5f, new Color(118, 120, 84)),
        new("paddlefish", "Paddle Stash", 2, "legendary", 5.0f, 20.0f, 12.0f, new Color(140, 152, 164)),
        new("cisco", "Cisco Sesh", 3, "common", 0.15f, 0.7f, 7.0f, new Color(198, 206, 214)),
        new("burbot", "Burbot Dabs", 3, "common", 0.5f, 3.5f, 10.0f, new Color(140, 132, 96)),
        new("lake_whitefish", "White Widowfish", 3, "uncommon", 0.5f, 2.4f, 9.0f, new Color(196, 202, 196)),
        new("deepwater_sculpin", "Deepwater Skunkpin", 3, "uncommon", 0.03f, 0.2f, 8.0f, new Color(196, 176, 170)),
        new("brook_trout", "Brook Terp", 3, "rare", 0.3f, 1.6f, 14.0f, new Color(108, 124, 88)),
        new("sauger", "Sour Sauger", 3, "rare", 0.4f, 2.0f, 12.0f, new Color(160, 148, 106)),
        new("lake_trout", "Lake Toke", 3, "epic", 3.0f, 12.0f, 12.0f, new Color(128, 144, 136)),
        new("lake_sturgeon", "Sturgeon Stogie", 3, "legendary", 8.0f, 35.0f, 22.0f, new Color(120, 116, 102)),
        new("bongwater_bream", "Bongwater Bream", 0, "legendary", 0.4f, 2.2f, 90f, new Color(150, 220, 180)),
        new("bassquatch", "Bassquatch", 1, "legendary", 3.0f, 12.0f, 70f, new Color(74, 96, 70)),
        new("ganja_gar", "Ganja Gar", 2, "legendary", 5.0f, 18.0f, 80f, new Color(108, 140, 84)),
        new("spliff_serpent", "Spliff Serpent", 3, "legendary", 4.0f, 14.0f, 100f, new Color(170, 214, 236)),
        new("shiny_golden_koi", "Shiny Golden Kief", 0, "legendary", 2.0f, 9.0f, 260f, new Color(255, 226, 140)),
        new("shiny_musky", "Shiny Musky Munchies", 1, "legendary", 10.0f, 26.0f, 180f, new Color(255, 232, 150)),
        new("shiny_alligator_gar", "Shiny Alligator Hash", 2, "legendary", 18.0f, 40.0f, 190f, new Color(255, 222, 128)),
        new("shiny_lake_trout", "Shiny Lake Toke", 3, "legendary", 12.0f, 30.0f, 210f, new Color(255, 228, 144)),
    };

    public static readonly Strain[] Strains =
    {
        new("homegrown", "Bluegill Bud", 0, -1, 0, 1, 1, 0.15f, 1.0f, 3),
        new("sunset", "Crappie Kush", 0, 1, 1, 3, 2, 0.25f, 1.6f, 6),
        new("pond_haze", "Sunfish Skunk", 0, 2, 2, 4, 2, 0.36f, 1.1f, 11),
        new("lake_blue", "Rainbow Roach", 1, 2, 1, 2, 3, 0.45f, 1.2f, 26),
        new("lake_gold", "Smallmouth Sativa", 1, 3, 2, 3, 3, 0.4f, 1.3f, 40),
        new("lake_deep", "Pike Punch", 1, 3, 2, 4, 4, 0.56f, 0.9f, 60),
        new("marsh_mud", "Bowfin Blunt", 2, 3, 2, 3, 4, 0.5f, 1.5f, 95),
        new("marsh_fever", "Gar Gas", 2, 4, 3, 4, 5, 0.62f, 1.0f, 135),
        new("marsh_wisp", "Catfish Chronic", 2, 4, 3, 4, 5, 0.58f, 1.7f, 180),
        new("trench_abyss", "Burbot Blaze", 3, 4, 3, 4, 6, 0.72f, 1.2f, 250),
        new("trench_star", "Sturgeon Stank", 3, 4, 3, 4, 6, 0.68f, 1.5f, 350),
        new("cosmic", "Cosmic Carp", 3, 4, 3, 4, 7, 0.85f, 1.3f, 480),
        new("koi_kief", "Koi Kief", 0, 4, 3, 4, 4, 0.55f, 1.5f, 55),
        new("bass_bong", "Bass Bongwater", 1, 4, 3, 4, 5, 0.65f, 1.2f, 110),
        new("gar_gasm", "Gar Gasm", 2, 4, 3, 4, 6, 0.72f, 1.4f, 185),
        new("coelacanth_chronic", "Coelacanth Chronic", 3, 4, 3, 4, 8, 0.95f, 1.4f, 420),
    };

    public static readonly Gear[] Rods =
    {
        new("bamboo", "Bamboo Stick", 0, "A stick with string. Honest work."),
        new("fiber", "Fiberglass Rod", 140, "Faster reel, softer tip. Forgiving on the line."),
        new("carbon", "Carbon Caster", 750, "Light, stiff, and a little bit showy."),
        new("moon", "Moonwhisper Rod", 3750, "Hums faintly. Catches things that should not be catchable."),
    };

    public static readonly Gear[] Lines =
    {
        new("light", "Old Line", 0, "Six pound test and a lot of hope."),
        new("braid", "Braided Line", 320, "Takes a beating without complaining. Much."),
        new("ghost", "Ghost Line", 2250, "Barely visible, absurdly strong. Slight air of smugness."),
    };

    public static readonly Gear[] Lures =
    {
        new("worm", "Worm on a Hook", 0, "Classic. Reliable. Ethical questions aside."),
        new("spoon", "Silver Spoon", 180, "Big flash, big fish. Smaller fish get self-conscious."),
        new("fly", "Woolly Fly", 500, "Fussy, floaty, and very good at finding interesting things."),
        new("jig", "Glow Jig", 1400, "Glows under the surface. Fish find it hard to be cool about it."),
        new("gold", "Golden Lure", 3000, "Absurd. Ostentatious. The legends cannot resist it."),
    };

    public static readonly Gear[] Coolers =
    {
        new("small", "Little Cooler", 0, "Four fish and one bag of ice. Fits in one hand."),
        new("medium", "Big Cooler", 600, "Ten fish, two bags of ice. The lid actually closes now."),
        new("huge", "Sal's Spare Cooler", 3000, "Sixteen fish and four bags of ice. Smells faintly of Sal."),
    };


    public static readonly string[] SpotNames =
        { "Bongwater Pond", "Bass & Bake Lake", "Munchies Marsh", "Tokers Trench" };

    public static int RarityIndex(string r)
    {
        for (int i = 0; i < RarityOrder.Length; i++)
            if (RarityOrder[i] == r) return i;
        return 0;
    }

    public static FishSpec[] ForSpot(int spot) =>
        Species.Where(s => s.Spot == spot).ToArray();
}
