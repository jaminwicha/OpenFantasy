#!/usr/bin/env python3
"""Populates Aldermere: writes data/npcs.json from data/world_meta.json.

Core cast (stable ids referenced by quests) + a generated population:
shopkeepers behind counters, bartenders and tavern patrons, villagers in
and around houses by wealth, homeless folk at the gates, farmers in the
fields, and a visiting cast at Brasshollow's tavern — a hooded figure,
a fence, a solo adventurer, and a three-strong adventuring party.

Run after gen_world.py:  python3 tools/gen_npcs.py
"""
import json
import random
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
META = json.loads((ROOT / "data" / "world_meta.json").read_text())
rng = random.Random(51105)

WORLD = json.loads((ROOT / "data" / "world.json").read_text())
WALKABLE_IDS = {t["id"] for t in WORLD["tileset"] if t["walkable"]}
GRID = []
for _row in WORLD["rows"]:
    _line = []
    _i = 0
    while _i < len(_row):
        _t, _run = _row[_i], _row[_i + 1]
        _i += 2
        _line += [_t] * _run
    GRID.append(_line)


def walkable(x, y):
    return 0 <= y < len(GRID) and 0 <= x < len(GRID[0]) and GRID[y][x] in WALKABLE_IDS

# race flavor per town (weights: human, elf, dwarf, gnome)
TOWN_RACE = {
    "brasshollow": (70, 10, 10, 10),
    "gearford": (15, 5, 70, 10),
    "sylvanglade": (15, 70, 5, 10),
    "willowick": (15, 10, 5, 70),
}
FIRST = {
    0: ["Ann", "Bram", "Cass", "Doro", "Edwin", "Fila", "Gert", "Hobb", "Ida", "Jory",
        "Kell", "Lena", "Milo", "Nan", "Osric", "Petra", "Quinn", "Rolf", "Sela", "Tam"],
    1: ["Aels", "Briar", "Cyla", "Deryn", "Elow", "Faen", "Gale", "Ilya", "Lira", "Miren",
        "Nym", "Ollo", "Sylra", "Thal", "Vael", "Wren"],
    2: ["Bardin", "Dagna", "Edrik", "Fenna", "Gimra", "Hark", "Ingra", "Jorn", "Kelda",
        "Lodin", "Marn", "Norra", "Orin", "Ragna", "Skadi", "Tor"],
    3: ["Bibbi", "Cog", "Dilly", "Fizz", "Gadget", "Hoppy", "Izzi", "Jinx", "Kip",
        "Lolo", "Momo", "Nib", "Pip", "Quill", "Squee", "Twix"],
}
VILLAGER_LINES = [
    "Fine weather for it, whatever it is you do.",
    "Mind the wisps past the gate. They look friendly. They are not.",
    "I hear the port's busy again. Good for trade, bad for quiet.",
    "The forge runs day and night lately.",
    "My grandmother remembers the war. She doesn't talk about it.",
    "You're not from here, are you? The boots give it away.",
    "The tavern's warm if the road's been long.",
    "They say the crystals hum louder before a storm.",
    "Prices being what they are, I mostly barter.",
    "Stay off the hills without a machine or a death wish.",
]
RICH_LINES = [
    "Yes, the pillars are imported. The tax was criminal.",
    "One simply cannot get reliable caravan insurance anymore.",
    "The King and I have met. Twice. Well — nearly twice.",
]
POOR_LINES = [
    "Roof leaks, but it's my leak.",
    "Work the fields in season, the docks off season. It adds up to almost enough.",
    "The rich quarter's cobbles are smoother. Same rain, though.",
]
HOMELESS_LINES = [
    "Spare a coin? Or a berry? Berries are better than coins, honestly.",
    "I sleep better under stars than most lords under silk. Colder, mind.",
    "The guards move me along, and I move along back.",
]
PATRON_LINES = [
    "Another round, and one for the stranger!",
    "I swear on my beard the fish was THIS big.",
    "Don't play cards with the hooded one. Just don't.",
    "The bard knows one song. We pretend not to notice.",
    "To the road! May it stay under our feet!",
]
FARMER_LINES = [
    "The soil's honest here. Everything else, less so.",
    "Wolves took another lamb. I'd pay good gil to whoever thins them.",
    "Rain when we need sun, sun when we need rain. Farming!",
]


def pick_race(town):
    w = TOWN_RACE.get(town, (60, 15, 15, 10))
    return rng.choices((0, 1, 2, 3), weights=w)[0]


def appearance(town=None, race=None):
    r = race if race is not None else pick_race(town or "")
    return {
        "race": r,
        "skin": rng.randrange(8),
        "hairStyle": rng.randrange(12),
        "hairColor": rng.randrange(16),
        "outfit": rng.randrange(10),
        "outfitA": rng.randrange(16),
        "outfitB": rng.randrange(16),
        "acc": rng.randrange(4),
    }


def name_for(race, suffix=None):
    base = rng.choice(FIRST[race])
    return f"{base} {suffix}" if suffix else base


NPCS = []
USED_IDS = set()


def add(npc):
    assert npc["id"] not in USED_IDS, f"duplicate npc id {npc['id']}"
    USED_IDS.add(npc["id"])
    npc.setdefault("sprite", "")
    NPCS.append(npc)


def interior_spots(b, count):
    """Distinct WALKABLE interior tiles of a building (away from walls/counters)."""
    x0, y0, x1, y1 = b["rect"]
    spots = [(x, y) for y in range(y0 + 1, y1) for x in range(x0 + 1, x1) if walkable(x, y)]
    rng.shuffle(spots)
    assert len(spots) >= count, f"building {b['kind']} too small for {count} occupants"
    return spots[:count]


# ---------------------------------------------------------------- fixed cast (castle + ports keep M4 ids)

add({"id": "king_aldric", "name": "King Aldric", "tileX": 180, "tileY": 316,
     "appearance": {"race": 0, "skin": 1, "hairStyle": 8, "hairColor": 9, "outfit": 3, "outfitA": 12, "outfitB": 6, "acc": 2},
     "dialog": "Rise. Aldermere has few heroes left; I will not waste one on ceremony.",
     "dialogs": ["The war machines we buried are waking. The Storm Warden worst among them.",
                  "My writ opens doors in Gearford. Dwarves respect paper almost as much as iron."],
     "quests": ["kings_writ", "warden_of_storms", "royal_menagerie"]})
add({"id": "captain_edda", "name": "Captain Edda", "tileX": 176, "tileY": 330, "wander": 2,
     "appearance": {"race": 0, "skin": 4, "hairStyle": 4, "hairColor": 1, "outfit": 5, "outfitA": 15, "outfitB": 7, "acc": 1},
     "dialog": "Castle guard's stretched thin. The roads aren't what they were.",
     "dialogs": ["Wolves on the east road again. My recruits won't cut it."],
     "quests": ["road_patrol", "watchtower_survey", "watchtower_clearout"]})
for pid, pname, px, py, race, stock in (
        ("portmaster_wick", "Portmaster Wick", 42, 352, 0, ["sailboat", "ship", "canoe"]),
        ("portmaster_grum", "Portmaster Grum", 334, 224, 2, ["sailboat", "canoe"]),
        ("portmaster_lys", "Portmaster Lys", 290, 372, 1, ["canoe", "sailboat"]),
        ("portmaster_dot", "Portmaster Dot", 118, 214, 3, ["canoe", "sailboat"])):
    add({"id": pid, "name": pname, "tileX": px, "tileY": py,
         "appearance": appearance(race=race),
         "dialog": "Fine hulls, fair winds. Press V by the water to launch what you own.",
         "shop": stock})

# ---------------------------------------------------------------- towns

SHOPS = {
    "brasshollow": {
        "shop_arms": ("garrick", "Garrick the Smith", ["bronze_sword", "short_bow", "iron_blade", "iron_rod", "leather_vest", "iron_mail"],
                      "Blades plain and true. The fancy ones get people killed."),
        "shop_goods": ("marla", "Marla", ["potion", "tonic", "ether", "rations", "copper_axe", "copper_pickaxe", "sickle"],
                       "Welcome to Brasshollow General. Steam-fresh stock, fair prices."),
    },
    "gearford": {
        "shop_arms": ("smelter_vora", "Smelter Vora", ["iron_blade", "steel_blade", "brass_mail", "iron_mail", "steel_mail"],
                      "Grand forge of Gearford. If it can be smelted, we smelt it."),
        "shop_goods": ("kegga", "Kegga Toolwright", ["iron_axe", "iron_pickaxe", "copper_axe", "copper_pickaxe", "sickle", "rations"],
                       "A dull tool is a slow funeral. Buy sharp."),
    },
    "sylvanglade": {
        "shop_arms": ("fenn", "Fenn", ["oak_rod", "iron_rod", "crystal_rod", "long_bow", "steel_bow", "ranger_cloak", "sage_robe"],
                      "Elven fletching, forest remedies, and no questions asked."),
        "shop_goods": ("sorrel", "Apothecary Sorrel", ["potion", "tonic", "ether", "hi_potion", "hi_ether", "rations"],
                       "Tinctures for what ails you. Mostly what ails you is wolves."),
    },
    "willowick": {
        "shop_arms": ("tinker_zizzle", "Tinker Zizzle", ["crystal_rod", "steel_rod", "mythril_pickaxe", "mythril_axe", "harvest_scythe"],
                      "Gadgets! Rods! Slightly singed maps! Everything a desert needs!"),
        "shop_goods": ("meline", "Meline of the Oasis", ["potion", "tonic", "ether", "hi_potion", "fizz_wine", "rations"],
                       "Water first, heroics second. That's oasis wisdom."),
    },
}
BARTENDERS = {
    "brasshollow": ("bartender_toma", "Toma", "The Rusted Tankard, at your service. Ale, beds, and better rumors than the crier.",
                    ["They say bats in the old mine chew lamplight. Bad for miners, good for bounty work.",
                     "The hooded one in the corner pays in real gil. That's all I know and all I ask."],
                    ["rumor_bats", "toma_pie_day"]),
    "gearford": ("bartender_hilda", "Hilda Kegkeeper", "Stone roof, strong ale. What else does a body need?",
                 ["Brutes cracked another sledge on the hill road. Thane's fuming."], ["hilda_shard_ale", "hilda_frost_hunt"]),
    "sylvanglade": ("bartender_aro", "Aro", "Moss-mead and quiet. The quiet costs extra.",
                    ["The marsh lights doubled since the crypt cracked. Liriel counts them nightly."], ["aro_gloom", "aro_stalkers"]),
    "willowick": ("bartender_juno", "Juno", "Shade, fizz-wine, and the only cold cellar in Miradel.",
                  ["The Spire hums in D minor now, says the tinker. It used to be C sharp."], ["juno_jackals"]),
}

for town, tdata in META["towns"].items():
    cx, cy = tdata["center"]
    for b in tdata["buildings"]:
        kind = b["kind"]
        if kind == "tavern":
            bid, bname, bd, bdialogs, bquests = BARTENDERS[town]
            bx, by = b["bar"]
            add({"id": bid, "name": bname, "tileX": bx, "tileY": by,
                 "appearance": appearance(town),
                 "dialog": bd, "dialogs": bdialogs, "quests": bquests,
                 "shop": ["rations", "potion"]})
            for i, (sx, sy) in enumerate(interior_spots(b, 4)):
                race = pick_race(town)
                add({"id": f"patron_{town}_{i}", "name": name_for(race), "tileX": sx, "tileY": sy,
                     "appearance": appearance(town, race), "wander": 3,
                     "dialog": rng.choice(PATRON_LINES),
                     "dialogs": rng.sample(PATRON_LINES, 2)})
        elif kind in SHOPS.get(town, {}):
            sid, sname, stock, sdialog = SHOPS[town][kind]
            sx, sy = interior_spots(b, 1)[0]
            shop_quests = {"garrick": ["garrick_fangs", "garrick_steel"], "kegga": ["kegga_timber"],
                           "sorrel": ["sorrel_venom"], "meline": ["meline_sun"],
                           "tinker_zizzle": ["zizzle_cores"]}.get(sid, [])
            add({"id": sid, "name": sname, "tileX": sx, "tileY": sy,
                 "appearance": appearance(town), "dialog": sdialog, "shop": stock,
                 "quests": shop_quests, "wander": 1})
        elif kind == "house_rich":
            race = pick_race(town)
            sx, sy = b["anchor"]
            add({"id": f"gentry_{town}", "name": name_for(race, "the Elder"), "tileX": sx, "tileY": sy,
                 "appearance": appearance(town, race), "wander": 2,
                 "dialog": rng.choice(RICH_LINES), "dialogs": rng.sample(RICH_LINES, 2),
                 "quests": ["gentry_coins"] if town == "brasshollow" else []})
        elif kind == "house_mid":
            for i, (sx, sy) in enumerate(interior_spots(b, 2)):
                race = pick_race(town)
                add({"id": f"villager_{town}_{b['rect'][0]}_{i}", "name": name_for(race),
                     "tileX": sx, "tileY": sy, "appearance": appearance(town, race), "wander": 5,
                     "dialog": rng.choice(VILLAGER_LINES), "dialogs": rng.sample(VILLAGER_LINES, 3)})
        elif kind == "house_poor":
            for i, (sx, sy) in enumerate(interior_spots(b, 1)):
                race = pick_race(town)
                add({"id": f"poorfolk_{town}_{b['rect'][0]}_{i}", "name": name_for(race),
                     "tileX": sx, "tileY": sy, "appearance": appearance(town, race), "wander": 4,
                     "dialog": rng.choice(POOR_LINES), "dialogs": rng.sample(POOR_LINES, 2)})
    # street wanderers on the plaza
    for i in range(5):
        race = pick_race(town)
        add({"id": f"street_{town}_{i}", "name": name_for(race), "tileX": cx + i - 1, "tileY": cy + 1,
             "appearance": appearance(town, race), "wander": 7,
             "dialog": rng.choice(VILLAGER_LINES), "dialogs": rng.sample(VILLAGER_LINES, 3)})

# gate guards + traveling visitors per town
GUARD_LINES = ["Move along. Politely, mind.", "Gate's open dawn to dawn. We just like standing here.",
               "Trouble outside stays outside. That's the whole job."]
VISITOR_LINES = ["Just passing through. The tavern any good?",
                 "I've walked three continents. This one's got the best bread.",
                 "Buying trophies, selling stories."]
for _town, _tdata in META["towns"].items():
    _cx, _cy = _tdata["center"]
    for gi, (gx, gy) in enumerate(((_cx - 1, _cy - 13), (_cx + 1, _cy + 13))):
        race = pick_race(_town)
        add({"id": f"guard_{_town}_{gi}", "name": name_for(race), "tileX": gx, "tileY": gy,
             "appearance": {"race": race, "skin": rng.randrange(8), "hairStyle": rng.randrange(12),
                             "hairColor": rng.randrange(16), "outfit": 5, "outfitA": 15, "outfitB": 7,
                             "acc": 1},
             "dialog": rng.choice(GUARD_LINES), "dialogs": rng.sample(GUARD_LINES, 2)})
    for vi in range(2):
        race = rng.randrange(4)
        add({"id": f"visitor_{_town}_{vi}", "name": name_for(race), "tileX": _cx + 3 + vi, "tileY": _cy - 1,
             "appearance": appearance(None, race), "wander": 8,
             "dialog": rng.choice(VISITOR_LINES), "dialogs": rng.sample(VISITOR_LINES, 2)})

# quest-giver overrides: Elder Bran walks Brasshollow's plaza; leaders elsewhere
add({"id": "elder_bran", "name": "Elder Bran", "tileX": 112, "tileY": 350, "wander": 3,
     "appearance": {"race": 0, "skin": 3, "hairStyle": 8, "hairColor": 9, "outfit": 2, "outfitA": 4, "outfitB": 6, "acc": 0},
     "dialog": "The March grows wilder by the season, traveler. We could use your blade.",
     "dialogs": ["Four continents, one crown, and a hundred old wounds. That is Aldermere.",
                  "When you are ready for larger deeds, the King himself will want to see you."],
     "quests": ["cull_wisps", "wolf_menace", "the_warden", "audience_at_castle", "foreman_falls"]})
add({"id": "thane_bruni", "name": "Thane Bruni", "tileX": 372, "tileY": 148, "wander": 1,
     "appearance": {"race": 2, "skin": 2, "hairStyle": 2, "hairColor": 5, "outfit": 5, "outfitA": 15, "outfitB": 7, "acc": 0},
     "dialog": "Hmph. Surface-folk. Speak quick, the forge won't stoke itself.",
     "dialogs": ["Emberdeep runs hotter than it should. Something below stokes it.",
                  "A king's writ, eh? Then Gearford owes you a hearing — and maybe a machine."],
     "quests": ["sunken_seal", "spire_key", "brute_iron"]})
add({"id": "archdruid_liriel", "name": "Archdruid Liriel", "tileX": 370, "tileY": 354, "wander": 2,
     "appearance": {"race": 1, "skin": 0, "hairStyle": 3, "hairColor": 11, "outfit": 2, "outfitA": 4, "outfitB": 6, "acc": 2},
     "dialog": "Sylvas remembers older names than Aldermere. Tread gently, cousin.",
     "dialogs": ["The swamp lights lure travelers from the road. They are not lights."],
     "quests": ["marsh_lights", "stones_survey", "stones_guardian", "crypt_king_falls"]})
add({"id": "matriarch_poppy", "name": "Matriarch Poppy", "tileX": 120, "tileY": 134, "wander": 2,
     "appearance": {"race": 3, "skin": 1, "hairStyle": 7, "hairColor": 14, "outfit": 4, "outfitA": 11, "outfitB": 6, "acc": 2},
     "dialog": "Willowick welcomes you! Mind the scorpions, dear. And the tower. And the wind.",
     "dialogs": ["Whoever quiets that tower eats free at my table forever."],
     "quests": ["scorpion_season"]})

# ---------------------------------------------------------------- tavern visitors (Brasshollow showcase)

tav = next(b for b in META["towns"]["brasshollow"]["buildings"] if b["kind"] == "tavern")
spots = interior_spots(tav, 10)[4:]  # skip patron seats
add({"id": "hooded_figure", "name": "Hooded Figure", "tileX": spots[0][0], "tileY": spots[0][1],
     "appearance": {"race": 0, "skin": 7, "hairStyle": 5, "hairColor": 0, "outfit": 2, "outfitA": 15, "outfitB": 15, "acc": 0},
     "dialog": "…You'll do. I need ore. Unmarked. Don't ask what for.",
     "dialogs": ["The Tankard's ale is watered. The information isn't.",
                  "We never spoke. There is no we."],
     "quests": ["quiet_delivery"]})
add({"id": "whisper", "name": "Whisper", "tileX": spots[1][0], "tileY": spots[1][1], "wander": 2,
     "appearance": {"race": 1, "skin": 6, "hairStyle": 9, "hairColor": 0, "outfit": 1, "outfitA": 15, "outfitB": 12, "acc": 3},
     "dialog": "Buying. Selling. Forgetting. Rates negotiable on all three.",
     "dialogs": ["Pelts move well right now. No provenance required."],
     "shop": ["ether", "potion", "wolf_pelt"],
     "quests": ["pelts_no_questions"]})
add({"id": "adventurer_karsa", "name": "Karsa Lone-Blade", "tileX": spots[2][0], "tileY": spots[2][1], "wander": 6,
     "appearance": {"race": 0, "skin": 5, "hairStyle": 2, "hairColor": 6, "outfit": 9, "outfitA": 10, "outfitB": 15, "acc": 1},
     "dialog": "Solo work pays better. Buries cheaper, too.",
     "dialogs": ["I cleared the mine once. It didn't stay cleared. Nothing does."],
     "quests": ["karsa_dire"]})
PARTY = [("sable", "Sable", "Party leader. We take contracts, split fair, and drink here between them.",
          ["brute_bounty", "camp_survey", "camp_chief"]),
         ("kit", "Kit", "Sable leads, I scout, Brand hits things. It works.", []),
         ("brand_the_shield", "Brand the Shield", "I hit things.", [])]
for i, (aid, aname, adialog, aquests) in enumerate(PARTY):
    sx, sy = spots[3 + i]
    add({"id": aid, "name": aname, "tileX": sx, "tileY": sy, "wander": 4,
         "appearance": {"race": (0, 1, 2)[i], "skin": (2, 0, 3)[i], "hairStyle": (4, 10, 2)[i],
                         "hairColor": (1, 15, 5)[i], "outfit": 9, "outfitA": 15, "outfitB": 10, "acc": (1, 3, 0)[i]},
         "dialog": adialog, "quests": aquests})

# ---------------------------------------------------------------- homeless

for h in META["homeless"]:
    town = h["town"]
    hx, hy = h["pos"]
    race = pick_race(town)
    hid = f"homeless_{town}"
    entry = {"id": hid, "name": name_for(race, "the Gray") if town != "brasshollow" else "Old Wex",
             "tileX": hx, "tileY": hy, "wander": 3,
             "appearance": appearance(town, race),
             "dialog": rng.choice(HOMELESS_LINES), "dialogs": rng.sample(HOMELESS_LINES, 2)}
    if town == "brasshollow":
        entry["quests"] = ["a_warm_meal"]
    add(entry)

# ---------------------------------------------------------------- farms

FARM_CAST = {
    "farm_verdance_large": ("farmer_hale", "Farmer Hale", ["livestock_wolves", "fence_mending"],
                            "Biggest farm in the March. Biggest headaches, too."),
    "farm_verdance_small": ("farmer_wren", "Wren", ["wren_orchard"],
                            "Just me, the rows, and the crows. Suits me."),
    "farm_sylvas_medium": ("farmer_oldbark", "Oldbark", ["oldbark_slimes"],
                           "Forest soil grows strange beets. Good strange, mostly."),
    "farm_miradel_small": ("farmer_sadi", "Sadi", ["oasis_baskets"],
                           "Oasis water, date rows, and patience. Farming, desert style."),
}
for farm in META["farms"]:
    fid, fname, fquests, fdialog = FARM_CAST[farm["id"]]
    hx, hy = farm["house"]["anchor"]
    add({"id": fid, "name": fname, "tileX": hx, "tileY": hy, "wander": 2,
         "appearance": appearance(farm["town"]),
         "dialog": fdialog, "dialogs": rng.sample(FARMER_LINES, 2), "quests": fquests})
    fx0, fy0, fx1, fy1 = farm["field"]
    hands = {"large": 3, "medium": 2, "small": 1}[farm["size"]]
    for i in range(hands):
        wx = rng.randrange(fx0, fx1 + 1)
        wy = rng.randrange(fy0, fy1 + 1)
        race = pick_race(farm["town"])
        add({"id": f"farmhand_{farm['id']}_{i}", "name": name_for(race), "tileX": wx, "tileY": wy,
             "appearance": appearance(farm["town"], race), "wander": 6,
             "dialog": rng.choice(FARMER_LINES), "dialogs": rng.sample(FARMER_LINES, 2)})

# ---------------------------------------------------------------- write

out = {"npcs": NPCS}
dest = ROOT / "data" / "npcs.json"
dest.write_text(json.dumps(out, indent=1) + "\n")
print(f"wrote {dest}: {len(NPCS)} npcs")
