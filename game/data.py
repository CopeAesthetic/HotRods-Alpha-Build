"""Content: spots, 32 fish, rods, lines, lures, strains and supplies.

Pure data + tiny helpers, no pygame import.
"""

# ------------------------------------------------------------------ spots ---
SPOTS = [
    dict(
        id="pond",
        name="Bongwater Pond",
        blurb="The most fished water in the county. Everyone starts here - and "
              "Sal's shack is right on the bank.",
        need_species=0,
        need_cash=0,
        tint=(118, 150, 108),
        murk=0.18,
        waterline=0.42,
    ),
    dict(
        id="lake",
        name="Bass & Bake Lake",
        blurb="Deeper, colder, and full of moonlight. Bring a jacket.",
        need_species=5,
        need_cash=120,
        tint=(88, 132, 162),
        murk=0.14,
        waterline=0.40,
    ),
    dict(
        id="marsh",
        name="Munchies Marsh",
        blurb="The frogs know your name. The water does not want to talk about it.",
        need_species=11,
        need_cash=420,
        tint=(104, 118, 78),
        murk=0.34,
        waterline=0.44,
    ),
    dict(
        id="trench",
        name="Tokers Trench",
        blurb="Past the buoys, where the water goes down and the stars come out.",
        need_species=18,
        need_cash=900,
        tint=(78, 70, 138),
        murk=0.46,
        waterline=0.38,
    ),
]

# --------------------------------------------------------------- species ----
# shape: minnow | torpedo | round | flat | eel | ray | frog | sturgeon | angler | weird
# pattern: none | spots | stripes | bands | gradient | glow
# cond: None | "night" | "high" | "night+high"
SPECIES = [
    # ------------------------------------------------------- duckweed pond --
    dict(id="bluegill", name="Bonggill", sci="Lepomis macrochirus", spot=0, rarity="common",
         kg=(0.08, 0.55), usd_kg=3.6, diff=0.14, shape="round", pattern="bands",
         body=(118, 146, 120), fin=(86, 112, 92), accent=(66, 86, 74),
         flavor="The gateway fish of every childhood. Fights twice its size and "
                "loses to a worm.",
         hint="Warm shallows. Eats almost anything you offer."),
    dict(id="pumpkinseed", name="Puffkinseed", sci="Lepomis gibbosus", spot=0,
         rarity="common", kg=(0.05, 0.30), usd_kg=4.0, diff=0.12, shape="round",
         pattern="spots", body=(150, 158, 96), fin=(118, 128, 78),
         accent=(238, 140, 72),
         flavor="A sunfish with better branding. That red patch behind the gill "
                "is not, in fact, an ear.",
         hint="Golden-olive, orange belly, red ear. Very proud of itself."),
    dict(id="green_sunfish", name="Green Sunbaked", sci="Lepomis cyanellus", spot=0,
         rarity="common", kg=(0.08, 0.45), usd_kg=3.2, diff=0.16, shape="round",
         pattern="spots", body=(106, 130, 86), fin=(78, 98, 66),
         accent=(150, 196, 178),
         flavor="A mouth big enough to eat things it has no business eating, "
                "including your bait, your hook and your patience.",
         hint="Olive-green with electric blue speckles. Mean little face."),
    dict(id="yellow_bullhead", name="Yellow Bowlhead", sci="Ameiurus natalis", spot=0,
         rarity="uncommon", kg=(0.20, 1.40), usd_kg=3.8, diff=0.30, shape="cat",
         pattern="none", body=(112, 98, 66), fin=(84, 72, 50), accent=(186, 166, 120),
         flavor="Whiskers, no dignity, and a flavour that has fed a hundred "
                "thousand fish fries.",
         hint="Something with whiskers just bumped the line."),
    dict(id="largemouth_bass", name="Largemouth Blazed", sci="Micropterus salmoides",
         spot=0, rarity="uncommon", kg=(0.40, 3.00), usd_kg=4.6, diff=0.40,
         shape="round", pattern="lateral", body=(106, 138, 88), fin=(76, 102, 64),
         accent=(46, 66, 48),
         flavor="Everyone's favourite. Named for a mouth that is, frankly, a lot.",
         hint="Green, wide-mouthed, and sulking under the lily pads."),
    dict(id="common_carp", name="Carp Diem", sci="Cyprinus carpio", spot=0,
         rarity="rare", kg=(1.50, 9.00), usd_kg=2.2, diff=0.52, shape="torpedo",
         pattern="scales", body=(168, 140, 86), fin=(128, 102, 66),
         accent=(206, 182, 132),
         flavor="A European import with excellent PR and terrible table manners. "
                "Will eat your bait, your float and the idea of fishing.",
         hint="A bronze slab with barbels and opinions."),
    dict(id="flathead_catfish", name="Catatonic Catfish", sci="Pylodictis olivaris",
         spot=0, rarity="epic", kg=(4.0, 20.0), usd_kg=6.0, diff=0.68,
         shape="cat", pattern="spots", body=(116, 98, 70), fin=(86, 72, 52),
         accent=(160, 136, 96),
         flavor="The pond's apex predator. Has eaten things you would not believe, "
                "and one thing you would rather not.",
         hint="Whatever is making that wake is not a bluegill."),
    dict(id="golden_koi", name="Golden Kief", sci="Cyprinus rubrofuscus", spot=0,
         rarity="legendary", kg=(1.0, 5.0), usd_kg=45.0, diff=0.80, shape="round",
         pattern="gradient", body=(250, 190, 90), fin=(255, 232, 180),
         accent=(255, 250, 235), cond="high",
         flavor="Someone's ornamental pet, now feral, enormous and fabulous. "
                "Someone is still missing it.",
         hint="A flash of gold in a pond that has no business holding it."),
    # ---------------------------------------------------------- willow lake --
    dict(id="yellow_perch", name="Mellow Perch", sci="Perca flavescens", spot=1,
         rarity="common", kg=(0.10, 0.55), usd_kg=8.0, diff=0.22, shape="round",
         pattern="bands", body=(196, 186, 104), fin=(226, 142, 60),
         accent=(62, 70, 44),
         flavor="Six dark bars, orange fins, and a chip on its shoulder the size "
                "of a fillet knife.",
         hint="Yellow with dark bars, hanging around the weed edge."),
    dict(id="black_crappie", name="Black Hashpie", sci="Pomoxis nigromaculatus",
         spot=1, rarity="common", kg=(0.15, 0.80), usd_kg=9.0, diff=0.26,
         shape="round", pattern="spots", body=(176, 186, 192), fin=(140, 150, 162),
         accent=(44, 48, 58),
         flavor="Speckled, slab-sided and a genuine delight in a fryer. Says "
                "'panfish' with its whole body.",
         hint="Silver with black speckles, stacked up over structure."),
    dict(id="rainbow_trout", name="Rainbow Runtz", sci="Oncorhynchus mykiss", spot=1,
         rarity="uncommon", kg=(0.40, 2.80), usd_kg=12.0, diff=0.38,
         shape="torpedo", pattern="lateral", body=(168, 180, 188),
         fin=(148, 160, 172), accent=(232, 138, 150),
         flavor="The pink stripe is a warning to nobody. Stocked by the thousand, "
                "caught by the thousand, still beautiful.",
         hint="A pink stripe and a lot of black spots, moving fast."),
    dict(id="smallmouth_bass", name="Smallmouth Blaze", sci="Micropterus dolomieu",
         spot=1, rarity="uncommon", kg=(0.40, 2.40), usd_kg=9.5, diff=0.42,
         shape="torpedo", pattern="bands", body=(150, 132, 96), fin=(116, 98, 70),
         accent=(78, 64, 46),
         flavor="Bronze, bad-tempered, and worth every cast. Fights like it has "
                "read your technique and disagrees.",
         hint="Bronze with vertical bars, holding in the rocks."),
    dict(id="northern_pike", name="Northern Pipe", sci="Esox lucius", spot=1,
         rarity="rare", kg=(1.0, 7.0), usd_kg=7.5, diff=0.56, shape="torpedo",
         pattern="spots", body=(108, 138, 84), fin=(84, 110, 68),
         accent=(198, 214, 152),
         flavor="A freshwater barracuda with a duck's bill and a bad attitude. "
                "Has teeth where you do not expect teeth.",
         hint="A long green shadow with light spots, sitting very still."),
    dict(id="walleye", name="Wally High", sci="Sander vitreus", spot=1, rarity="rare",
         kg=(0.50, 4.00), usd_kg=15.0, diff=0.50, shape="torpedo", pattern="none",
         body=(178, 166, 120), fin=(142, 130, 92), accent=(240, 240, 228),
         cond="night",
         flavor="Nocturnal, glassy-eyed and delicious. Honestly unfair to the "
                "other fish.",
         hint="Only after dark. Eyes that catch the dock light."),
    dict(id="muskellunge", name="Musky Munchies", sci="Esox masquinongy", spot=1,
         rarity="epic", kg=(4.0, 16.0), usd_kg=8.0, diff=0.74, shape="torpedo",
         pattern="spots", body=(124, 146, 110), fin=(96, 116, 88),
         accent=(54, 70, 50),
         flavor="The fish of ten thousand casts. Some people call it a hobby; "
                "others call it a personality.",
         hint="Something in this lake is older than you and knows it."),
    dict(id="golden_trout", name="Golden Runtz", sci="Oncorhynchus mykiss",
         spot=1, rarity="legendary", kg=(0.50, 2.50), usd_kg=45.0, diff=0.78,
         shape="torpedo", pattern="gradient", body=(250, 212, 88),
         fin=(236, 178, 60), accent=(255, 246, 180), cond="high",
         flavor="A hatchery accident that became a legend. Bright as a coin and "
                "about as easy to catch sober.",
         hint="Yellow as a coin, and only visible to the properly relaxed."),
    # ---------------------------------------------------------- misty marsh --
    dict(id="bowfin", name="Bongfin", sci="Amia calva", spot=2, rarity="common",
         kg=(0.50, 3.00), usd_kg=4.0, diff=0.34, shape="sturgeon", pattern="none",
         body=(122, 116, 78), fin=(92, 88, 58), accent=(168, 160, 110),
         flavor="A living fossil with lungs, a long dorsal fin, and no patience "
                "for your nonsense.",
         hint="Long, olive and primitive. It can breathe air. It will."),
    dict(id="brown_bullhead", name="Brown Bowlhead", sci="Ameiurus nebulosus",
         spot=2, rarity="common", kg=(0.20, 1.60), usd_kg=4.2, diff=0.28,
         shape="cat", pattern="spots", body=(108, 84, 60), fin=(82, 62, 44),
         accent=(150, 124, 92),
         flavor="Mud, whiskers and a surprising amount of personality. The marsh "
                "in fish form.",
         hint="Mottled brown, whiskers first."),
    dict(id="longnose_gar", name="Longnose Ganja", sci="Lepisosteus osseus", spot=2,
         rarity="uncommon", kg=(0.80, 4.00), usd_kg=5.0, diff=0.40, shape="gar",
         pattern="spots", body=(128, 138, 96), fin=(100, 110, 76),
         accent=(60, 66, 44),
         flavor="Half fish, half tweezers. Those scales will dull a knife and your "
                "sense of optimism.",
         hint="A floating stick with teeth, wearing spots."),
    dict(id="american_eel", name="Spliff Eel", sci="Anguilla rostrata", spot=2,
         rarity="uncommon", kg=(0.30, 1.80), usd_kg=16.0, diff=0.44, shape="eel",
         pattern="none", body=(110, 112, 86), fin=(86, 88, 68),
         accent=(150, 150, 116),
         flavor="Spends its whole life getting somewhere, then turns around and "
                "goes back to the Sargasso Sea. Relatable.",
         hint="A muscular ribbon, and worth real money."),
    dict(id="blue_catfish", name="Blue Catnap", sci="Ictalurus furcatus", spot=2,
         rarity="rare", kg=(2.0, 12.0), usd_kg=4.8, diff=0.58, shape="cat",
         pattern="none", body=(118, 132, 150), fin=(90, 104, 120),
         accent=(170, 186, 200),
         flavor="Grey, enormous, and entirely unbothered by your rod or your "
                "ambitions.",
         hint="A blue-grey slab that just pulled the float under without asking."),
    dict(id="snakehead", name="Northern Bakehead", sci="Channa argus", spot=2,
         rarity="rare", kg=(0.80, 4.00), usd_kg=8.5, diff=0.62, shape="torpedo",
         pattern="spots", body=(128, 110, 84), fin=(100, 84, 64),
         accent=(66, 56, 44),
         flavor="An invasive species that can walk between ponds. Not a joke. "
                "Delicious, though, which is the only good news.",
         hint="Blotchy brown, long fins, and an expression that knows something."),
    dict(id="alligator_gar", name="Alligator Hash", sci="Atractosteus spatula",
         spot=2, rarity="epic", kg=(6.0, 28.0), usd_kg=5.5, diff=0.76,
         shape="gar", pattern="spots", body=(118, 120, 84), fin=(92, 94, 64),
         accent=(60, 62, 44),
         flavor="A fish with a crocodile's snout and an ancient grudge. Most of "
                "the marsh's old stories are about one of these.",
         hint="The water went quiet. That is not a good sign."),
    dict(id="paddlefish", name="Paddle Stash", sci="Polyodon spathula", spot=2,
         rarity="legendary", kg=(5.0, 20.0), usd_kg=12.0, diff=0.80, shape="paddle",
         pattern="none", body=(140, 152, 164), fin=(112, 124, 136),
         accent=(196, 208, 218), cond="high",
         flavor="Filters plankton with a paddle and has been doing roughly this "
                "for a hundred million years. No notes.",
         hint="A grey shape with a rostrum like a canoe paddle."),
    # ------------------------------------------------------- moonlit trench --
    dict(id="cisco", name="Cisco Sesh", sci="Coregonus artedi", spot=3, rarity="common",
         kg=(0.15, 0.70), usd_kg=7.0, diff=0.30, shape="minnow", pattern="none",
         body=(198, 206, 214), fin=(160, 170, 182), accent=(236, 242, 248),
         flavor="A cold-water sardine for lakes. Silver, nervous, and delicious "
                "straight out of the smoker.",
         hint="Silver and small, schooling deep."),
    dict(id="burbot", name="Burbot Dabs", sci="Lota lota", spot=3, rarity="common",
         kg=(0.50, 3.50), usd_kg=10.0, diff=0.36, shape="eel", pattern="bands",
         body=(140, 132, 96), fin=(110, 102, 72), accent=(190, 180, 130),
         flavor="The only cod that lives in fresh water. Ugly in a way that grows "
                "on you around three in the morning.",
         hint="Mottled, eel-shaped, and out hunting at night."),
    dict(id="lake_whitefish", name="White Widowfish", sci="Coregonus clupeaformis",
         spot=3, rarity="uncommon", kg=(0.50, 2.40), usd_kg=9.0, diff=0.34,
         shape="torpedo", pattern="none", body=(196, 202, 196), fin=(160, 168, 164),
         accent=(232, 238, 232),
         flavor="Understated, delicious, and quietly beloved by everyone who has "
                "ever eaten one.",
         hint="Silver-sided and deep-bodied, sitting low."),
    dict(id="deepwater_sculpin", name="Deepwater Skunkpin", sci="Myoxocephalus thompsonii",
         spot=3, rarity="uncommon", kg=(0.03, 0.20), usd_kg=8.0, diff=0.24,
         shape="round", pattern="spots", body=(196, 176, 170), fin=(166, 148, 146),
         accent=(226, 212, 206),
         flavor="A deep-water gargoyle the size of your thumb. Has seen the bottom "
                "of the trench and refuses to discuss it.",
         hint="A pale little thing from water you cannot see into."),
    dict(id="brook_trout", name="Brook Terp", sci="Salvelinus fontinalis", spot=3,
         rarity="rare", kg=(0.30, 1.60), usd_kg=14.0, diff=0.46, shape="torpedo",
         pattern="spots", body=(108, 124, 88), fin=(86, 100, 72),
         accent=(236, 150, 80),
         flavor="The prettiest fish in the water, and it knows it. Orange belly, "
                "white-edged fins, absolutely no humility.",
         hint="Olive with pale worm-marks and an orange belly."),
    dict(id="sauger", name="Sour Sauger", sci="Sander canadensis", spot=3, rarity="rare",
         kg=(0.40, 2.00), usd_kg=12.0, diff=0.54, shape="torpedo", pattern="bands",
         body=(160, 148, 106), fin=(128, 118, 84), accent=(70, 64, 48),
         flavor="Walleye's smaller, sandpaper cousin. Same glassy eyes, same "
                "nocturnal habits, none of the credit.",
         hint="Mottled gold with dark saddles, low and slow."),
    dict(id="lake_trout", name="Lake Toke", sci="Salvelinus namaycush", spot=3,
         rarity="epic", kg=(3.0, 12.0), usd_kg=12.0, diff=0.72,
         shape="torpedo", pattern="spots", body=(128, 144, 136),
         fin=(100, 116, 110), accent=(214, 226, 220),
         flavor="A deep-water heavyweight that takes a decade to grow up. Nobody "
                "told it the food chain was supposed to be subtle.",
         hint="Grey-green with pale spots, rising from the cold."),
    dict(id="lake_sturgeon", name="Sturgeon Stogie", sci="Acipenser fulvescens",
         spot=3, rarity="legendary", kg=(8.0, 35.0), usd_kg=22.0, diff=0.86,
         shape="sturgeon", pattern="none", body=(120, 116, 102),
         fin=(94, 90, 78), accent=(172, 166, 148), cond="night+high",
         flavor="Older than your family name, and most of them are still "
                "endangered. Catch it, weigh it, and put it back.",
         hint="Something down there predates the lake itself."),
]

SPECIES_BY_ID = {s["id"]: s for s in SPECIES}

# ------------------------------------------------------------------ gear ----
RODS = [
    dict(id="bamboo", name="Bamboo Stick", cost=0, reel=1.00, tol=0.00, power=1.00, color=(198, 172, 120),
         desc="A stick with string. Honest work."),
    dict(id="fiber", name="Fiberglass Rod", cost=140, reel=1.14, tol=0.03, power=1.12, color=(150, 186, 214),
         desc="Faster reel, softer tip. Forgiving on the line."),
    dict(id="carbon", name="Carbon Caster", cost=750, reel=1.30, tol=0.06, power=1.28, color=(120, 126, 140),
         desc="Light, stiff, and a little bit showy."),
    dict(id="moon", name="Moonwhisper Rod", cost=3750, reel=1.45, tol=0.10, power=1.50, color=(196, 176, 246),
         desc="Hums faintly. Catches things that should not be catchable."),
]

LINES = [
    dict(id="light", name="Old Line", cost=0, grace=0.22,
         desc="Six pound test and a lot of hope."),
    dict(id="braid", name="Braided Line", cost=320, grace=0.44,
         desc="Takes a beating without complaining. Much."),
    dict(id="ghost", name="Ghost Line", cost=2250, grace=0.85,
         desc="Barely visible, absurdly strong. Slight air of smugness."),
]

LURES = [
    dict(id="worm", name="Worm on a Hook", cost=0, bias={}, size=1.0, color=(196, 156, 130),
         desc="Classic. Reliable. Ethical questions aside."),
    dict(id="spoon", name="Silver Spoon", cost=180, size=1.25, color=(198, 206, 216),
         bias={"common": 0.85, "uncommon": 0.9, "rare": 1.2},
         desc="Big flash, big fish. Smaller fish get self-conscious."),
    dict(id="fly", name="Woolly Fly", cost=500, size=0.85, color=(206, 168, 118),
         bias={"common": 0.55, "uncommon": 1.6, "rare": 1.35, "epic": 0.9},
         desc="Fussy, floaty, and very good at finding interesting things."),
    dict(id="jig", name="Glow Jig", cost=1400, size=1.0, color=(120, 232, 208),
         bias={"rare": 1.6, "epic": 1.3, "legendary": 1.2},
         night_bias={"common": 0.7, "rare": 1.5, "epic": 1.4},
         desc="Glows under the surface. Fish find it hard to be cool about it."),
    dict(id="gold", name="Golden Lure", cost=3000, size=1.1, color=(246, 196, 96),
         bias={"common": 0.4, "uncommon": 0.7, "epic": 2.2, "legendary": 1.8},
         desc="Absurd. Ostentatious. The legends cannot resist it."),
]

STRAINS = [
    # --- Duckweed Pond: mild, cheap, always a starter -----------------------
    dict(id="homegrown", name="Bluegill Bud", area=0, tier=-1, gate_min=0, gate_max=1, pts=3, potency=1,
         luck=0.15, duration=1.0, wobble=1.0, cash=1.0, color=(150, 196, 140),
         desc="Honest, mellow and slightly seedy. Sal keeps a jar by the till."),
    dict(id="sunset", name="Crappie Kush", area=0, tier=1, gate_min=1, gate_max=3, pts=6, potency=2,
         luck=0.25, duration=1.6, wobble=0.9, cash=1.0, color=(246, 168, 110),
         desc="Lasts ages and makes the pond look like a postcard."),
    dict(id="pond_haze", name="Sunfish Skunk", area=0, tier=2, gate_min=2, gate_max=4, pts=11, potency=2,
         luck=0.36, duration=1.1, wobble=1.1, cash=1.0, color=(176, 210, 120),
         desc="Green, floaty, and inexplicably good at finding carp."),
    # --- Willow Lake: balanced -------------------------------------------------
    dict(id="lake_blue", name="Rainbow Roach", area=1, tier=2, pts=26, potency=3,
         luck=0.45, duration=1.2, wobble=1.0, cash=1.0, color=(118, 168, 236),
         desc="Cold-cured and clear-headed. The lake fish like it."),
    dict(id="lake_gold", name="Smallmouth Sativa", area=1, tier=3, pts=40, potency=3,
         luck=0.40, duration=1.3, wobble=1.0, cash=1.25, color=(240, 190, 96),
         desc="Tastes faintly of citrus. Sal swears by it at sunset."),
    dict(id="lake_deep", name="Pike Punch", area=1, tier=3, pts=60, potency=4,
         luck=0.56, duration=0.9, wobble=1.3, cash=1.0, color=(96, 128, 200),
         desc="Hits hard and doesn't last. Your hands will remember it."),
    # --- Misty Marsh: heavy and strange ----------------------------------------
    dict(id="marsh_mud", name="Bowfin Blunt", area=2, tier=3, pts=95, potency=4,
         luck=0.50, duration=1.5, wobble=1.15, cash=1.0, color=(138, 148, 96),
         desc="Earthy, slow, and weirdly good at pulling up monsters."),
    dict(id="marsh_fever", name="Gar Gas", area=2, tier=4, pts=135, potency=5,
         luck=0.62, duration=1.0, wobble=1.35, cash=1.0, color=(214, 128, 108),
         desc="The marsh runs hot around this one. So will you."),
    dict(id="marsh_wisp", name="Catfish Chronic", area=2, tier=4, pts=180, potency=5,
         luck=0.58, duration=1.7, wobble=1.2, cash=1.2, color=(150, 230, 206),
         desc="Burns pale green and lasts most of the night."),
    # --- Moonlit Trench: the strong stuff ---------------------------------------
    dict(id="trench_abyss", name="Burbot Blaze", area=3, tier=4, pts=250, potency=6,
         luck=0.72, duration=1.2, wobble=1.25, cash=1.0, color=(108, 96, 196),
         desc="Grown under lights, cured under pressure. Do not operate a boat."),
    dict(id="trench_star", name="Sturgeon Stank", area=3, tier=4, pts=350, potency=6,
         luck=0.68, duration=1.5, wobble=1.15, cash=1.35, color=(210, 180, 250),
         desc="The trench's cash crop. Everything bites, everything pays."),
    dict(id="cosmic", name="Cosmic Carp", area=3, tier=4, pts=480, potency=7,
         luck=0.85, duration=1.3, wobble=1.2, cash=1.15, color=(188, 138, 236),
         desc="The one the mythics are actually interested in."),
    # --- hidden: only Sal's back room, unlocked by a secret fish -----------
    dict(id="koi_kief", name="Koi Kief", area=0, tier=4, pts=55, potency=4, hidden=True,
         unlock_fish="bongwater_bream", luck=0.55, duration=1.5, wobble=1.0, cash=1.1,
         color=(250, 208, 130),
         desc="Scraped off an ornamental carp. Sparkly, floral, absurdly smooth."),
    dict(id="bass_bong", name="Bass Bongwater", area=1, tier=4, pts=110, potency=5,
         hidden=True, unlock_fish="bassquatch", luck=0.65, duration=1.2, wobble=1.2,
         cash=1.0, color=(120, 176, 232),
         desc="Whatever the lake is filtering, it works."),
    dict(id="gar_gasm", name="Gar Gasm", area=2, tier=4, pts=185, potency=6, hidden=True,
         unlock_fish="ganja_gar", luck=0.72, duration=1.4, wobble=1.15, cash=1.1,
         color=(168, 200, 120),
         desc="The marsh's oldest recipe. Tastes like the bottom of a pond."),
    dict(id="coelacanth_chronic", name="Coelacanth Chronic", area=3, tier=4, pts=420,
         potency=8, hidden=True, unlock_fish="spliff_serpent", luck=0.95, duration=1.4,
         wobble=1.25, cash=1.2, color=(150, 210, 240),
         desc="Fossil weed. Has been in the ground longer than the fish."),
]

HIDDEN_FISH = [
    dict(id="bongwater_bream", name="Bongwater Bream", sci="Abramis bongwateris", spot=0,
         rarity="legendary", kg=(0.4, 2.2), usd_kg=90, diff=0.82, shape="round",
         pattern="glow", body=(150, 220, 180), fin=(110, 180, 150),
         accent=(220, 255, 230), cond="strain:lake_deep",
         flavor="It only surfaces for pike punch. Nobody knows why. It is very relaxed.",
         hint="Something in the pond responds to the smell of the lake."),
    dict(id="bassquatch", name="Bassquatch", sci="Micropterus cryptidus", spot=1,
         rarity="legendary", kg=(3.0, 12.0), usd_kg=70, diff=0.88, shape="round",
         pattern="spots", body=(74, 96, 70), fin=(52, 72, 52), accent=(140, 160, 110),
         cond="strain:marsh_wisp",
         flavor="A bass that should not exist, photographed badly by every local.",
         hint="The marsh's chronic brings it up from the deep."),
    dict(id="ganja_gar", name="Ganja Gar", sci="Atractosteus sativus", spot=2,
         rarity="legendary", kg=(5.0, 18.0), usd_kg=80, diff=0.90, shape="gar",
         pattern="spots", body=(108, 140, 84), fin=(80, 108, 62), accent=(196, 232, 140),
         cond="strain:cosmic",
         flavor="Long, green, and completely unbothered by your presence or your rod.",
         hint="Smoke the trench's best and something old comes up to look at you."),
    dict(id="spliff_serpent", name="Spliff Serpent", sci="Anguilla somnifera", spot=3,
         rarity="legendary", kg=(4.0, 14.0), usd_kg=100, diff=0.92, shape="eel",
         pattern="glow", body=(170, 214, 236), fin=(130, 176, 206), accent=(232, 250, 255),
         cond="strain:pond_haze",
         flavor="Coils around the line and hums. Weighs more than it looks.",
         hint="The pond's skunk, down in the trench. Don't ask."),
]

SHINY_FISH = [
    dict(id="shiny_golden_koi", name="Shiny Golden Kief", sci="Cyprinus aurum",
         shiny_of="golden_koi", spot=0, rarity="legendary", kg=(2.0, 9.0),
         usd_kg=260, diff=0.95, shape="round", pattern="glow",
         body=(255, 226, 140), fin=(240, 200, 90), accent=(255, 248, 214),
         cond="strain:koi_kief",
         flavor="It glitters like it knows something you don't."),
    dict(id="shiny_musky", name="Shiny Musky Munchies", sci="Esox splendor",
         shiny_of="muskellunge", spot=1, rarity="legendary", kg=(10.0, 26.0),
         usd_kg=180, diff=0.96, shape="round", pattern="spots",
         body=(255, 232, 150), fin=(236, 196, 92), accent=(255, 250, 220),
         cond="strain:bass_bong",
         flavor="Every scale a different shade of gold. Nobody believes the photos."),
    dict(id="shiny_alligator_gar", name="Shiny Alligator Hash", sci="Atractosteus aureus",
         shiny_of="alligator_gar", spot=2, rarity="legendary", kg=(18.0, 40.0),
         usd_kg=190, diff=0.97, shape="gar", pattern="spots",
         body=(255, 222, 128), fin=(238, 194, 86), accent=(255, 246, 206),
         cond="strain:gar_gasm",
         flavor="Older than the marsh and twice as smug."),
    dict(id="shiny_lake_trout", name="Shiny Lake Toke", sci="Salvelinus lucens",
         shiny_of="lake_trout", spot=3, rarity="legendary", kg=(12.0, 30.0),
         usd_kg=210, diff=0.97, shape="trout", pattern="spots",
         body=(255, 228, 144), fin=(240, 198, 94), accent=(255, 250, 216),
         cond="strain:coelacanth_chronic",
         flavor="Comes up gold from water that has never seen the sun."),
]
SHINY_BY_ID = {s["id"]: s for s in SHINY_FISH}
SHINY_ODDS = 0.02

HIDDEN_BY_ID = {s["id"]: s for s in HIDDEN_FISH}

STRAINS_BY_AREA = {i: [s for s in STRAINS if s["area"] == i] for i in range(4)}

# ------------------------------------------------------------------ gear ----
COOLERS = [
    dict(id="small", name="Little Cooler", slots=4, ice=1, cost=0,
         desc="Four fish and one bag of ice. Fits in one hand."),
    dict(id="medium", name="Big Cooler", slots=10, ice=2, cost=600,
         desc="Ten fish, two bags of ice. The lid actually closes now."),
    dict(id="huge", name="Sal's Spare Cooler", slots=16, ice=4, cost=3000,
         desc="Sixteen fish and four bags of ice. Smells faintly of Sal."),
]

SPOT_PAY = (1.0, 3.0, 7.0, 15.0)   # later waters pay more, cash and VP


def pay_mult(sid):
    sp = SPECIES_BY_ID.get(sid) or HIDDEN_BY_ID.get(sid) or SHINY_BY_ID.get(sid)
    spot = sp["spot"] if sp else 0
    return SPOT_PAY[min(spot, len(SPOT_PAY) - 1)]


# gear that only unlocks once you have reached a water (0 pond .. 3 trench)
GEAR_SPOT = {
    "carbon": 1, "moon": 3,
    "braid": 1, "ghost": 3,
    "fly": 1, "jig": 2, "gold": 3,
    "medium": 1, "huge": 3,
    "beaker_bong": 2, "heady_bong": 3,
}

ICE_COST = 15
ICE_MINUTES = 5.0        # minutes of cold per bag
SPOIL_RATE = 0.15        # freshness lost per minute once the ice is gone
SPOIL_FLOOR = 0.35       # fish never drop below this fraction of value


SMOKERS = [
    dict(id="one_hitter", name="One-Hitter", cost=0, ceremony=1.0, potency=0, dur=1.00, fill=1.00, luck=0.00,
         conc=False, color=(198, 172, 120),
         desc="A brass one-hitter. Discreet, efficient, slightly hot on the lips."),
    dict(id="papers", name="Rolling Papers", cost=40, ceremony=3.5, potency=0, dur=1.05, fill=1.10, luck=0.03,
         conc=False, color=(236, 232, 220),
         desc="A pack of papers. Faster ceremony, same weed."),
    dict(id="backwoods", name="Backwoods", cost=120, ceremony=6.0, potency=0, dur=1.25, fill=1.20, luck=0.05,
         conc=False, color=(120, 92, 62),
         desc="A blunt wrap. Slower burn, so the high lasts longer."),
    dict(id="pipe", name="Glass Pipe", cost=220, ceremony=8.5, potency=1, dur=1.10, fill=1.35, luck=0.06,
         conc=False, color=(150, 196, 210),
         desc="A spoon pipe. Better airflow and better luck."),
    dict(id="beaker_bong", name="Beaker Bong", cost=700, ceremony=11.0, potency=1, dur=1.40, fill=1.55, luck=0.10,
         conc=False, color=(140, 200, 180),
         desc="Water filtration. Smooth, cool and long-lasting."),
    dict(id="heady_bong", name="Heady Bong", cost=1500, ceremony=13.5, potency=2, dur=1.45, fill=1.80, luck=0.16,
         conc=False, color=(210, 170, 240),
         desc="Headier glasswork. Hits harder and looks better doing it."),
    dict(id="dab_rig", name="Dab Rig", cost=3200, ceremony=16.0, potency=0, dur=1.00, fill=2.60, luck=0.00,
         conc=True, color=(250, 210, 120),
         desc="The only thing that can smoke concentrate. Worth it."),
    dict(id="hot_press", name="Hot Press", cost=4800, ceremony=1.0, potency=0, dur=1.00, fill=1.00, luck=0.00,
         conc=False, color=(226, 120, 96),
         desc="Presses flower into concentrate. Use PRESS here, then smoke it."),
]
SMOKERS_BY_ID = {g["id"]: g for g in SMOKERS}
CONCENTRATE_POTENCY = 7
CONCENTRATE_MULT = 3.0



# gear that only unlocks once you have reached a water (0 pond .. 3 trench)
GEAR_SPOT = {
    "carbon": 1, "moon": 3,
    "braid": 1, "ghost": 3,
    "fly": 1, "jig": 2, "gold": 3,
    "medium": 1, "huge": 3,
    "beaker_bong": 2, "heady_bong": 3,
}

ICE_COST = 15
ICE_MINUTES = 5.0
SPOIL_RATE = 0.15      # freshness lost per minute with no ice
SPOIL_FLOOR = 0.35

RODS_BY_ID = {g["id"]: g for g in RODS}
LINES_BY_ID = {g["id"]: g for g in LINES}
LURES_BY_ID = {g["id"]: g for g in LURES}
STRAINS_BY_ID = {g["id"]: g for g in STRAINS}
COOLERS_BY_ID = {g["id"]: g for g in COOLERS}

TIPS = [
    "Tip: hold SPACE to reel, let go to slack. Keep the needle in the gold band.",
    "Tip: a bite only lasts a moment. Wait for the bobber to go under, then tap.",
    "Tip: the deeper you cast, the better the fish. Also the harder the reel.",
    "Tip: some fish only show up at night. The clock is in the corner.",
    "Tip: being high brings rarer fish. It also makes your hands wobble.",
    "Tip: toke again too soon and you'll be stuck on the dock for a while.",
    "Tip: first-time catches pay a fat discovery bonus. Chase the ??? entries.",
]


def fish_for_spot(index):
    return [s for s in SPECIES if s["spot"] == index]


def species(sid):
    return SPECIES_BY_ID.get(sid) or HIDDEN_BY_ID.get(sid) or SHINY_BY_ID[sid]


def length_cm(kg, shape):
    factor = {
        "minnow": 46, "torpedo": 42, "round": 33, "flat": 30, "eel": 62,
        "ray": 40, "frog": 30, "sturgeon": 58, "angler": 36, "weird": 40,
        "cat": 37, "gar": 56, "paddle": 52,
    }.get(shape, 40)
    return max(3.0, (max(kg, 0.001) ** (1.0 / 3.0)) * factor)


def spot_unlocked(index, found_count, cash):
    sp = SPOTS[index]
    return found_count >= sp["need_species"] and cash >= sp["need_cash"]
