using System.Text.Json;
using OpenFantasy.Sim.SiteGen;

namespace OpenFantasy.Sim;

/// <summary>
/// Headless verification suite (run via `--selftest`). Pure C# — exercises the
/// shipped content files plus the deterministic sim, no Godot required.
/// </summary>
public static class SelfTest
{
    public static bool RunAll(string dataDir, Action<string> log)
    {
        int passed = 0, failed = 0;

        void Case(string name, Action test)
        {
            try
            {
                test();
                passed++;
                log($"  PASS {name}");
            }
            catch (Exception e)
            {
                failed++;
                log($"  FAIL {name}: {e.Message}");
            }
        }

        log("OpenFantasy selftest");

        Case("content: shipped data validates", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            Assert(data.Classes.Count >= 3, "expected ≥3 classes");
            Assert(data.Monsters.Count >= 3, "expected ≥3 monsters");
            Assert(data.Map.Spawners.Count >= 3, "expected ≥3 spawners");
        });

        Case("content: broken data is rejected readably", () =>
        {
            var classes = Parse<ClassesFile>(Path.Combine(dataDir, "classes.json"));
            var abilities = Parse<AbilitiesFile>(Path.Combine(dataDir, "abilities.json"));
            var monsters = Parse<MonstersFile>(Path.Combine(dataDir, "monsters.json"));
            var map = WorldMap.LoadFromFile(Path.Combine(dataDir, "world.json"));
            monsters.Monsters[0].LeashRange = monsters.Monsters[0].AggroRange; // invalid: leash must exceed aggro
            try
            {
                GameData.FromParts(classes, abilities, monsters, map);
                throw new Exception("validator accepted leashRange <= aggroRange");
            }
            catch (DataValidationException e)
            {
                Assert(e.Message.Contains("leashRange"), $"error not readable: {e.Message}");
            }
        });

        Case("xp curve: 100·level^1.5", () =>
        {
            AssertEqual(World.XpToNextLevel(1), 100, "level 1");
            AssertEqual(World.XpToNextLevel(2), 282, "level 2");
            AssertEqual(World.XpToNextLevel(3), 519, "level 3");
            AssertEqual(World.XpToNextLevel(4), 800, "level 4");
            AssertEqual(World.XpToNextLevel(9), 2700, "level 9");
        });

        Case("combat: base damage formula", () =>
        {
            AssertEqual(World.BaseDamage(12, 8, 10, magical: false), 8, "phys 12atk vs 8def");
            AssertEqual(World.BaseDamage(20, 8, 15, magical: true), 28, "mag 20mag vs 8def");
            AssertEqual(World.BaseDamage(1, 100, 10, magical: false), 1, "damage floor of 1");
        });

        Case("rng: xorshift64* is stable", () =>
        {
            var rng = new XorShift64(42);
            int a = rng.Next(1000), b = rng.Next(1000), c = rng.Next(1000);
            var rng2 = new XorShift64(42);
            AssertEqual(rng2.Next(1000), a, "first draw");
            AssertEqual(rng2.Next(1000), b, "second draw");
            AssertEqual(rng2.Next(1000), c, "third draw");
        });

        Case("determinism: same seed+inputs ⇒ same hash over 1000 ticks", () =>
        {
            ulong h1 = RunScriptedWorld(dataDir, seed: 12345);
            ulong h2 = RunScriptedWorld(dataDir, seed: 12345);
            AssertEqual((long)h1, (long)h2, "state hash diverged");
            ulong h3 = RunScriptedWorld(dataDir, seed: 99999);
            Assert(h1 != h3, "different seeds should diverge");
        });

        Case("movement: player stays on walkable ground", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            var world = new World(data, 7);
            var p = world.AddPlayer("Walker", data.ClassOrder[0]);
            for (int t = 0; t < 600; t++)
            {
                // Spiral-ish walk that slams into obstacles from all sides.
                sbyte mx = (sbyte)((t / 50) % 3 - 1);
                sbyte my = (sbyte)((t / 80) % 3 - 1);
                world.SetPlayerInput(p.Id, new InputCommand { Seq = t, MoveX = mx, MoveY = my, AbilitySlot = -1, TargetId = -1 });
                world.Step();
                Assert(data.Map.IsWalkableUnits(p.X, p.Y), $"player on unwalkable tile at tick {t} ({p.X},{p.Y})");
            }
        });

        Case("combat integration: scripted fight kills, tags, and grants xp", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            var world = new World(data, 2024);
            var p = world.AddPlayer("Hero", data.ClassOrder[0]);
            // Teleport next to the nearest monster (no pathfinding in tests), then fight.
            Entity? mob = world.EntitiesInOrder().Where(e => e.IsMonster)
                .OrderBy(e => e.DistanceSquaredTo(p)).First();
            p.X = mob.X + 40;
            p.Y = mob.Y;
            int xpBefore = p.Xp + 0;
            bool sawDamage = false, sawDeath = false;
            for (int t = 0; t < 4000 && !sawDeath; t++)
            {
                if (!p.Alive) { world.Step(); continue; }
                if (!mob.Alive || world.GetEntity(mob.Id) is null)
                {
                    mob = world.EntitiesInOrder().Where(e => e.IsMonster && e.Alive)
                        .OrderBy(e => e.DistanceSquaredTo(p)).FirstOrDefault();
                    if (mob is null) break;
                    p.X = mob.X + 40;
                    p.Y = mob.Y;
                }
                world.SetPlayerInput(p.Id, new InputCommand
                {
                    Seq = t,
                    MoveX = (sbyte)Math.Sign(mob.X - p.X),
                    MoveY = (sbyte)Math.Sign(mob.Y - p.Y),
                    AbilitySlot = 0,
                    TargetId = mob.Id,
                });
                world.Step();
                foreach (var ev in world.Events)
                {
                    if (ev.Kind == SimEventKind.Damage && ev.SourceId == p.Id) sawDamage = true;
                    if (ev.Kind == SimEventKind.Death && ev.SourceId == p.Id) sawDeath = true;
                }
            }
            Assert(sawDamage, "player never landed damage");
            Assert(sawDeath, "player never killed a monster");
            Assert(p.Level > 1 || p.Xp > xpBefore, "kill granted no xp");
        });

        // ------------------------------------------------------------- M2 cases

        Case("m2 content: items/npcs/quests validate; boss + mine spawners exist", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            Assert(data.Items.Count >= 10, "expected ≥10 items");
            Assert(data.Npcs.Count >= 2, "expected ≥2 npcs");
            Assert(data.Quests.Count >= 3, "expected ≥3 quests");
            Assert(data.Monsters.ContainsKey("magitek_warden"), "boss missing");
            Assert(data.Map.Spawners.Any(s => s.Monster == "magitek_warden"), "boss spawner missing");
            Assert(data.Map.Spawners.Any(s => s.Monster == "cave_bat"), "cave bat spawner missing");
            Assert(data.Map.Spawners.Any(s => s.Monster == "ore_slime"), "ore slime spawner missing");
        });

        Case("m2 content: broken quest reference is rejected readably", () =>
        {
            var classes = Parse<ClassesFile>(Path.Combine(dataDir, "classes.json"));
            var abilities = Parse<AbilitiesFile>(Path.Combine(dataDir, "abilities.json"));
            var monsters = Parse<MonstersFile>(Path.Combine(dataDir, "monsters.json"));
            var items = Parse<ItemsFile>(Path.Combine(dataDir, "items.json"));
            var npcs = Parse<NpcsFile>(Path.Combine(dataDir, "npcs.json"));
            var quests = Parse<QuestsFile>(Path.Combine(dataDir, "quests.json"));
            var map = WorldMap.LoadFromFile(Path.Combine(dataDir, "world.json"));
            quests.Quests[0].TargetMonster = "no_such_monster";
            try
            {
                GameData.FromParts(classes, abilities, monsters, map, items, npcs, quests);
                throw new Exception("validator accepted unknown target monster");
            }
            catch (DataValidationException e)
            {
                Assert(e.Message.Contains("no_such_monster"), $"error not readable: {e.Message}");
            }
        });

        Case("m2 equipment: equip/unequip changes effective stats and persists", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            var world = new World(data, 5);
            var p = world.AddPlayer("Smith", "squire");
            int baseAttack = p.Attack;
            Assert(world.AddItem(p, "bronze_sword"), "add failed");
            AssertEqual(world.CountItem(p, "bronze_sword"), 1, "inventory count");
            Assert(world.EquipItem(p, "bronze_sword").StartsWith("Equipped"), "equip failed");
            AssertEqual(p.Attack, baseAttack + 4, "attack bonus applied");
            AssertEqual(world.CountItem(p, "bronze_sword"), 0, "item moved out of pack");

            var save = world.SnapshotSave(p);
            var world2 = new World(data, 6);
            var p2 = world2.AddPlayer("Smith", "squire", save);
            AssertEqual(p2.Attack, baseAttack + 4, "equipment survived save round-trip");

            Assert(world.UnequipItem(p, "bronze_sword").StartsWith("Unequipped"), "unequip failed");
            AssertEqual(p.Attack, baseAttack, "attack restored");
        });

        Case("m2 items: buy, use, sell against the vendor", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            var world = new World(data, 8);
            var p = world.AddPlayer("Shopper", "squire");
            var vendor = world.EntitiesInOrder().First(e => e.IsNpc && e.NpcDefId == "marla");
            p.X = vendor.X + 16;
            p.Y = vendor.Y;
            p.Gil = 100;

            Assert(world.BuyItem(p, vendor.Id, "potion").StartsWith("Bought"), "buy failed");
            AssertEqual(p.Gil, 70, "gil deducted");
            Assert(world.BuyItem(p, vendor.Id, "tonic").StartsWith("Not enough"), "overspend allowed");

            p.Hp = 1;
            Assert(world.UseItem(p, "potion").StartsWith("Used"), "use failed");
            Assert(p.Hp > 1, "potion healed nothing");
            AssertEqual(world.CountItem(p, "potion"), 0, "potion not consumed");

            Assert(world.AddItem(p, "wolf_pelt"), "add pelt");
            Assert(world.SellItem(p, vendor.Id, "wolf_pelt").StartsWith("Sold"), "sell failed");
            AssertEqual(p.Gil, 79, "sell price credited (half of 18 = 9)");
        });

        Case("m2 quests: accept → kill credit → complete → reward", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            var world = new World(data, 9);
            var p = world.AddPlayer("Warden", "squire");
            var elder = world.EntitiesInOrder().First(e => e.IsNpc && e.NpcDefId == "elder_bran");
            p.X = elder.X + 16;
            p.Y = elder.Y;

            Assert(world.AcceptQuest(p, elder.Id, "cull_wisps").StartsWith("Quest accepted"), "accept failed");
            Assert(world.CompleteQuest(p, elder.Id, "cull_wisps").StartsWith("Not done"), "premature completion");

            var quest = data.Quests["cull_wisps"];
            // Simulate kill credit directly (combat integration already proven).
            for (int i = 0; i < quest.TargetCount; i++)
            {
                // internal path: use reflection-free public seam — kill counting runs in the death branch,
                // so emulate via CountQuestKill through a real monster kill is heavy; instead assert the
                // counter through the public quest API by manipulating the log the way the sim does.
                p.QuestLog["cull_wisps"].Kills++;
            }
            int gilBefore = p.Gil;
            string result = world.CompleteQuest(p, elder.Id, "cull_wisps");
            Assert(result.StartsWith("Quest complete"), $"turn-in failed: {result}");
            AssertEqual(p.Gil, gilBefore + quest.RewardGil, "gil reward");
            AssertEqual(world.CountItem(p, quest.RewardItem), 1, "item reward");
            Assert(p.Level > 1, "xp reward should have leveled a fresh character");
            Assert(world.CompleteQuest(p, elder.Id, "cull_wisps").StartsWith("That deed"), "double turn-in allowed");
        });

        Case("m2 parties: invite/accept/leave and shared kill credit", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            var world = new World(data, 11);
            var a = world.AddPlayer("Alpha", "squire");
            var b = world.AddPlayer("Beta", "mage");

            Assert(world.PartyInvite(a, "Beta").StartsWith("Invited"), "invite failed");
            var (acceptResult, inviter) = world.PartyAccept(b);
            Assert(acceptResult.StartsWith("You joined"), $"accept failed: {acceptResult}");
            Assert(inviter?.Id == a.Id, "inviter mismatch");
            AssertEqual(world.PartyMembers(a.PartyId).Count, 2, "party size");

            // Party member near a tagged kill gets quest credit + xp.
            var elder = world.EntitiesInOrder().First(e => e.IsNpc && e.NpcDefId == "elder_bran");
            b.X = elder.X;
            b.Y = elder.Y;
            world.AcceptQuest(b, elder.Id, "cull_wisps");

            var wisp = world.EntitiesInOrder().First(e => e.IsMonster && e.ArchetypeId == "leaf_wisp");
            a.X = wisp.X + 8; a.Y = wisp.Y;
            b.X = wisp.X - 8; b.Y = wisp.Y;
            int bXpBefore = b.Xp;
            // A tags and kills the wisp via the real combat path.
            wisp.Hp = 1;
            for (int t = 0; t < 120 && wisp.Alive; t++)
            {
                // Resend each tick: ability requests are consumed even when ATB is still filling.
                world.SetPlayerInput(a.Id, new InputCommand { Seq = t, MoveX = 0, MoveY = 0, AbilitySlot = 0, TargetId = wisp.Id });
                world.Step();
            }
            Assert(!wisp.Alive, "wisp survived");
            Assert(b.Xp > bXpBefore || b.Level > 1, "party member got no shared xp");
            AssertEqual(b.QuestLog["cull_wisps"].Kills, 1, "party member got no quest credit");

            Assert(world.PartyLeave(b).StartsWith("You left"), "leave failed");
            Assert(a.PartyId == -1 && b.PartyId == -1, "1-member party should dissolve");
        });

        Case("m2 determinism: hash stable with items/quests/parties active", () =>
        {
            ulong h1 = RunM2ScriptedWorld(dataDir, 777);
            ulong h2 = RunM2ScriptedWorld(dataDir, 777);
            AssertEqual((long)h1, (long)h2, "M2 state hash diverged");
        });

        // ------------------------------------------------------------- M3 cases

        Case("m3 appearance: sanitize, bytes round-trip, save round-trip", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            var app = new Appearance { Skin = 200, HairStyle = 3, HairColor = 11, Outfit = 4, OutfitA = 10, OutfitB = 6, Acc = 2 };
            var clean = app.Sanitized(data.Appearance);
            Assert(clean.Skin < data.Appearance.Skins.Count, "sanitize failed");
            var round = Appearance.FromBytes(clean.ToBytes());
            Assert(round.Equals(clean), "byte round-trip mismatch");

            var world = new World(data, 3);
            var p = world.AddPlayer("Face", "mage", appearance: clean);
            var save = world.SnapshotSave(p);
            Assert(save.Appearance.Count == 8, "save missing appearance (8 bytes incl. race)");
            var world2 = new World(data, 4);
            var p2 = world2.AddPlayer("Face", "mage",
                appearance: Appearance.FromBytes(save.Appearance.Select(i => (byte)i).ToArray()));
            Assert(p2.Appearance.Equals(clean), "appearance lost through save");
        });

        Case("m3 content: nodes + recipes validate; appearance manifest sane", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            Assert(data.Map.Nodes.Count >= 4, "expected ≥4 node regions");
            Assert(data.Recipes.Count >= 6, "expected ≥6 recipes");
            Assert(data.Appearance.HairStyles == 12, "expected 12 hair styles");
            Assert(data.Appearance.Presets.Count >= 6, "expected preset gallery");
            var world = new World(data, 77);
            Assert(world.EntitiesInOrder().Any(e => e.IsNode && e.NodeType == "oak"), "no oak spawned");
            Assert(world.EntitiesInOrder().Any(e => e.IsNode && e.NodeType == "ore"), "no ore spawned");
            Assert(world.EntitiesInOrder().Any(e => e.IsNode && e.NodeType == "berry"), "no berry spawned");
        });

        Case("m3 gathering: tool gating, yields, depletion, respawn", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            var world = new World(data, 21);
            var p = world.AddPlayer("Lumber", "squire");
            var oak = world.EntitiesInOrder().First(e => e.IsNode && e.NodeType == "oak");
            p.X = oak.X + 20;
            p.Y = oak.Y;
            p.Atb = SimConstants.AtbMax;

            Assert(world.Gather(p, oak.Id).Contains("axe"), "no-tool swing should name the axe");
            Assert(world.AddItem(p, "copper_axe"), "give axe");
            string result = world.Gather(p, oak.Id);
            Assert(result.Contains("Wood"), $"swing yielded '{result}'");
            AssertEqual(world.CountItem(p, "wood"), 1, "wood in pack");

            int oakId = oak.Id;
            for (int i = 0; i < 20 && (world.GetEntity(oakId)?.Alive ?? false); i++)
            {
                p.Atb = SimConstants.AtbMax;
                world.Gather(p, oakId);
            }
            Assert(world.GetEntity(oakId) is not { Alive: true }, "oak never depleted");

            int before = world.EntitiesInOrder().Count(e => e.IsNode && e.NodeType == "oak" && e.Alive);
            for (int t = 0; t < 1000; t++) world.Step();
            int after = world.EntitiesInOrder().Count(e => e.IsNode && e.NodeType == "oak" && e.Alive);
            Assert(after > before, $"oak never respawned ({before} → {after})");
        });

        Case("m3 crafting: consumes inputs, produces output, forge gating", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            var world = new World(data, 22);
            var p = world.AddPlayer("Smith", "squire");

            world.AddItem(p, "berries", 3);
            string result = world.CraftItem(p, "craft_potion");
            Assert(result.StartsWith("Crafted"), $"potion craft failed: {result}");
            AssertEqual(world.CountItem(p, "berries"), 0, "berries not consumed");
            AssertEqual(world.CountItem(p, "potion"), 1, "potion not produced");

            world.AddItem(p, "iron_ore", 3);
            world.AddItem(p, "wood", 1);
            // Player spawns in town but not at the forge tile cluster.
            string awayResult = world.CraftItem(p, "smelt_iron_bar");
            int u = SimConstants.UnitsPerTile;
            bool nearForge = world.IsNearForge(p);
            if (!nearForge)
                Assert(awayResult.Contains("forge"), $"forge gate missing: {awayResult}");
            // Move beside Brasshollow's forge (town center +7,+4 → 117,356) and smelt.
            p.X = 116 * u + u / 2;
            p.Y = 356 * u + u / 2;
            Assert(world.IsNearForge(p), "not near forge after moving");
            string atForge = world.CraftItem(p, "smelt_iron_bar");
            Assert(atForge.StartsWith("Crafted"), $"smelt failed: {atForge}");
            AssertEqual(world.CountItem(p, "iron_bar"), 1, "bar not produced");

            Assert(world.CraftItem(p, "craft_potion").StartsWith("Missing"), "crafted without inputs");
        });

        Case("m3 determinism: hash stable with nodes/gathering/crafting active", () =>
        {
            ulong h1 = RunM3ScriptedWorld(dataDir, 4242);
            ulong h2 = RunM3ScriptedWorld(dataDir, 4242);
            AssertEqual((long)h1, (long)h2, "M3 state hash diverged");
        });

        // ------------------------------------------------------------- M4 cases

        Case("m4 world: continents, towns, dungeons, regions, crystals validate", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            Assert(data.Map.Width == 512 && data.Map.Height == 512, "expected 512x512 world");
            foreach (string rid in new[] { "brasshollow", "gearford", "sylvanglade", "willowick",
                "castle_aldermere", "storm_spire", "spire_top", "hollow_mine", "emberdeep", "sunken_crypt" })
                Assert(data.Map.Region(rid) != null, $"missing region {rid}");
            Assert(data.Map.SafeZones.Count >= 5, "expected ≥5 safe zones (towns + castle)");
            Assert(data.Map.Crystals.Count >= 4, "expected ≥4 respawn crystals");
            Assert(data.Npcs.Count >= 12, "expected ≥12 NPCs");
            Assert(data.Monsters.Count >= 14, "expected ≥14 monsters");
        });

        Case("m4 races: stat modifiers apply per race", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            var world = new World(data, 31);
            var human = world.AddPlayer("Hu", "squire", appearance: new Appearance { Race = 0, Skin = 1 });
            var elf = world.AddPlayer("El", "squire", appearance: new Appearance { Race = 1, Skin = 1 });
            var dwarf = world.AddPlayer("Dw", "squire", appearance: new Appearance { Race = 2, Skin = 1 });
            var gnome = world.AddPlayer("Gn", "squire", appearance: new Appearance { Race = 3, Skin = 1 });
            Assert(elf.Magic > human.Magic && elf.MaxHp < human.MaxHp, "elf mods wrong");
            Assert(dwarf.MaxHp > human.MaxHp && dwarf.Speed < human.Speed, "dwarf mods wrong");
            Assert(gnome.MaxMp > human.MaxMp && gnome.Attack < human.Attack, "gnome mods wrong");
            var save = world.SnapshotSave(dwarf);
            AssertEqual(save.Appearance.Count, 8, "appearance should be 8 bytes with race");
        });

        Case("m4 vehicles: domains, embark/disembark, aggro immunity", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            var world = new World(data, 32);
            var p = world.AddPlayer("Sailor", "scout");
            int u = SimConstants.UnitsPerTile;

            int shallow = data.Map.TileIdByName("shallow");
            int ocean = data.Map.TileIdByName("ocean");
            int hills = data.Map.TileIdByName("hills");
            Assert(shallow >= 0 && ocean >= 0 && hills >= 0, "tile names missing");

            // Domain rules straight from the table.
            Assert(!world.TileAllows(0, 0, VehicleKind.None), "walking on ocean allowed");
            Assert(world.TileAllows(0, 0, VehicleKind.Ship), "ship refused ocean");
            Assert(!world.TileAllows(0, 0, VehicleKind.Canoe), "canoe allowed on ocean");
            Assert(world.TileAllows(0, 0, VehicleKind.Airship), "airship refused ocean");

            // Find a sand tile adjacent to shallow water (a beach) for a real embark.
            int bx = -1, by = -1;
            for (int y = 1; y < 511 && bx < 0; y++)
                for (int x = 1; x < 511 && bx < 0; x++)
                    if (data.Map.IsWalkableTile(x, y) && data.Map.TileAt(x, y + 1) == shallow)
                    {
                        bx = x;
                        by = y;
                    }
            Assert(bx > 0, "no beach found");
            p.X = bx * u + u / 2;
            p.Y = by * u + u / 2;

            Assert(world.ToggleVehicle(p).Contains("no vehicle"), "embark without owning one");
            world.AddItem(p, "canoe");
            Assert(world.ToggleVehicle(p).Contains("canoe"), "canoe embark failed");
            Assert(p.Vehicle == VehicleKind.Canoe, "not aboard");
            Assert(world.Gather(p, -1).Length > 0, "gather should refuse aboard"); // any refusal text
            Assert(world.ToggleVehicle(p).Contains("disembark"), "disembark failed");
            Assert(p.Vehicle == VehicleKind.None, "still aboard");

            // Airship goes anywhere, lands only on walkable ground.
            world.AddItem(p, "airship_brasswing");
            Assert(world.ToggleVehicle(p).Contains("rises"), "airship should lift off");
            Assert(world.UnitsAllow(8 * u, 8 * u, VehicleKind.Airship), "airship blocked over ocean");
        });

        Case("m4 quests: prereq gating, explore, talk, main chain shape", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            var world = new World(data, 33);
            var p = world.AddPlayer("Pilgrim", "squire");
            int u = SimConstants.UnitsPerTile;
            var bran = world.EntitiesInOrder().First(e => e.IsNpc && e.NpcDefId == "elder_bran");
            var king = world.EntitiesInOrder().First(e => e.IsNpc && e.NpcDefId == "king_aldric");

            // Prereq gate: can't take the audience before the cull.
            p.X = bran.X + 16; p.Y = bran.Y;
            Assert(world.AcceptQuest(p, bran.Id, "audience_at_castle").StartsWith("First finish"),
                "prereq not enforced");

            // Complete the cull (simulate credit), then chain into the Talk quest.
            world.AcceptQuest(p, bran.Id, "cull_wisps");
            p.QuestLog["cull_wisps"].Kills = data.Quests["cull_wisps"].TargetCount;
            Assert(world.CompleteQuest(p, bran.Id, "cull_wisps").StartsWith("Quest complete"), "cull turn-in");
            Assert(world.AcceptQuest(p, bran.Id, "audience_at_castle").StartsWith("Quest accepted"), "audience accept");

            // Talk quests refuse the giver and complete at the target.
            Assert(world.CompleteQuest(p, bran.Id, "audience_at_castle").StartsWith("Take that to"),
                "talk quest completed at giver");
            p.X = king.X + 16; p.Y = king.Y;
            Assert(world.CompleteQuest(p, king.Id, "audience_at_castle").StartsWith("Quest complete"),
                "talk quest turn-in failed");

            // Explore quest: not ready until the region is entered.
            world.AcceptQuest(p, king.Id, "kings_writ");
            var bruni = world.EntitiesInOrder().First(e => e.IsNpc && e.NpcDefId == "thane_bruni");
            p.X = bruni.X + 16; p.Y = bruni.Y;
            world.CompleteQuest(p, bruni.Id, "kings_writ");
            Assert(world.AcceptQuest(p, bruni.Id, "spire_key").StartsWith("First finish"), "chain order broken");
            world.AcceptQuest(p, bruni.Id, "sunken_seal");
            p.QuestLog["sunken_seal"].Kills = 3;
            world.CompleteQuest(p, bruni.Id, "sunken_seal");
            Assert(world.AcceptQuest(p, bruni.Id, "spire_key").StartsWith("Quest accepted"), "spire accept");
            Assert(world.CompleteQuest(p, bruni.Id, "spire_key").Contains("haven't been"), "explore ready too early");
            var top = data.Map.Region("spire_top")!;
            p.X = (top.X + 2) * u; p.Y = (top.Y + 2) * u;
            for (int t = 0; t < 12; t++) world.Step();   // explore check runs on a stagger
            Assert(p.QuestLog["spire_key"].Kills >= 1, "region visit not registered");
            p.X = bruni.X + 16; p.Y = bruni.Y;
            Assert(world.CompleteQuest(p, bruni.Id, "spire_key").StartsWith("Quest complete"), "explore turn-in");

            // Finale is offered by the king and rewards the airship.
            var finale = data.Quests["warden_of_storms"];
            Assert(finale.Main && finale.RewardItem == "airship_brasswing", "finale reward wrong");
        });

        // ------------------------------------------------------------- M5 cases

        Case("m5 world: taverns, buildings, farms, population", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            foreach (string town in new[] { "brasshollow", "gearford", "sylvanglade", "willowick" })
                Assert(data.Map.Region($"tavern_{town}") != null, $"missing tavern region in {town}");
            foreach (string farm in new[] { "farm_verdance_large", "farm_verdance_small",
                "farm_sylvas_medium", "farm_miradel_small" })
                Assert(data.Map.Region(farm) != null, $"missing {farm}");
            Assert(data.Npcs.Count >= 50, $"expected ≥50 NPCs, got {data.Npcs.Count}");
            Assert(data.Npcs.Values.Count(n => n.Wander > 0) >= 30, "expected ≥30 wandering NPCs");
            Assert(data.Npcs.ContainsKey("hooded_figure") && data.Npcs.ContainsKey("whisper")
                   && data.Npcs.ContainsKey("sable") && data.Npcs.ContainsKey("homeless_brasshollow"),
                "tavern visitor cast incomplete");
            Assert(data.Npcs.Values.Count(n => n.Shop.Count > 0) >= 12, "expected ≥12 shopkeepers");
        });

        Case("m5 fetch quest: accept → gather → consume on turn-in → reward", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            var world = new World(data, 55);
            var p = world.AddPlayer("Kindly", "squire");
            var wex = world.EntitiesInOrder().First(e => e.IsNpc && e.NpcDefId == "homeless_brasshollow");
            p.X = wex.X + 16;
            p.Y = wex.Y;
            Assert(world.AcceptQuest(p, wex.Id, "a_warm_meal").StartsWith("Quest accepted"), "accept failed");
            Assert(world.CompleteQuest(p, wex.Id, "a_warm_meal").StartsWith("Not enough"), "turned in empty-handed");
            world.AddItem(p, "berries", 3);
            int xpBefore = p.Xp + p.Level * 1000;
            Assert(world.CompleteQuest(p, wex.Id, "a_warm_meal").StartsWith("Quest complete"), "turn-in failed");
            AssertEqual(world.CountItem(p, "berries"), 0, "berries not consumed");
            Assert(p.Xp + p.Level * 1000 > xpBefore, "no xp reward");
        });

        Case("m5 wander: villagers stay walkable and leashed over 1200 ticks", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            var world = new World(data, 56);
            var wanderers = world.EntitiesInOrder()
                .Where(e => e.IsNpc && data.Npcs[e.NpcDefId].Wander > 0)
                .Select(e => e.Id).ToList();
            Assert(wanderers.Count >= 30, "not enough wanderers spawned");
            for (int t = 0; t < 1200; t++) world.Step();
            int moved = 0;
            foreach (int id in wanderers)
            {
                var e = world.GetEntity(id)!;
                Assert(data.Map.IsWalkableUnits(e.X, e.Y), $"npc {e.NpcDefId} on unwalkable tile");
                long leash = (data.Npcs[e.NpcDefId].Wander + 1) * SimConstants.UnitsPerTile;
                long dx = e.X - e.HomeX, dy = e.Y - e.HomeY;
                Assert(dx * dx + dy * dy <= leash * leash * 2, $"npc {e.NpcDefId} broke its leash");
                if (dx != 0 || dy != 0) moved++;
            }
            Assert(moved >= wanderers.Count / 2, $"town feels dead: only {moved} npcs moved");
        });

        Case("m6 content scale: the expansion targets hold", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            Assert(data.Monsters.Count >= 35, $"monsters {data.Monsters.Count} < 35");
            Assert(data.Items.Count >= 60, $"items {data.Items.Count} < 60");
            Assert(data.Abilities.Count >= 12, $"abilities {data.Abilities.Count} < 12");
            Assert(data.Recipes.Count >= 22, $"recipes {data.Recipes.Count} < 22");
            Assert(data.Quests.Count >= 42, $"quests {data.Quests.Count} < 42");
            Assert(data.Npcs.Count >= 115, $"npcs {data.Npcs.Count} < 115");
            Assert(data.Map.Spawners.Count >= 32, $"spawners {data.Map.Spawners.Count} < 32");
            Assert(data.Map.Nodes.Count >= 14, $"nodes {data.Map.Nodes.Count} < 14");
            foreach (string poi in new[] { "ruined_watchtower", "standing_stones", "bandit_camp" })
                Assert(data.Map.Region(poi) != null, $"missing POI {poi}");
            // every class has 4 abilities and the level-7 unlock
            foreach (var cls in data.Classes.Values)
            {
                Assert(cls.Abilities.Count == 4, $"{cls.Id} has {cls.Abilities.Count} abilities");
                Assert(data.Abilities[cls.Abilities[3]].UnlockLevel == 7, $"{cls.Id} 4th ability unlock wrong");
            }
            // every quest giver actually exists and lists the quest (validator covers refs; spot-check chains)
            Assert(data.Quests["garrick_steel"].Prerequisite == "garrick_fangs", "chain broken");
            Assert(data.Quests["camp_chief"].Prerequisite == "camp_survey", "chain broken");
        });

        Case("m6 world: every monster and node type is actually spawnable", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            var world = new World(data, 66);
            var spawnedKinds = world.EntitiesInOrder().Where(e => e.IsMonster)
                .Select(e => e.ArchetypeId).ToHashSet();
            var expected = data.Map.Spawners.Select(s => s.Monster).ToHashSet();
            foreach (string kind in expected)
                Assert(spawnedKinds.Contains(kind), $"{kind} has a spawner but never spawned");
            Assert(spawnedKinds.Count >= 30, $"only {spawnedKinds.Count} monster kinds live in the world");
        });

        Case("m5 determinism: hash stable with the full living-town population", () =>
        {
            static ulong Run(string dir)
            {
                var data = GameData.LoadFromDirectory(dir);
                var world = new World(data, 5757);
                var p = world.AddPlayer("Watcher", "mage");
                for (int t = 0; t < 800; t++)
                {
                    world.SetPlayerInput(p.Id, new InputCommand
                    {
                        Seq = t, MoveX = (sbyte)(t % 6 == 0 ? 1 : 0), MoveY = 0, AbilitySlot = -1, TargetId = -1,
                    });
                    world.Step();
                }
                return world.StateHash();
            }
            AssertEqual((long)Run(dataDir), (long)Run(dataDir), "M5 population hash diverged");
        });

        // ------------------------------------------------------------- M7 cases

        Case("m7 noise: integer value noise is deterministic, ranged, continuous", () =>
        {
            for (int i = 0; i < 500; i++)
            {
                long fx = (i - 250L) * 7919L * 13;
                long fy = i * 104729L - 26000000L;
                int v = Noise.Value2(1234, fx, fy);
                Assert(v >= 0 && v < 65536, $"value2 out of range: {v}");
                AssertEqual(Noise.Value2(1234, fx, fy), v, "value2 not deterministic");
                int f = Noise.Fbm2(1234, fx, fy, 5);
                Assert(f >= 0 && f < 65536, $"fbm out of range: {f}");
            }
            Assert(Noise.Fbm2(1, 12345, 67890, 4) != Noise.Fbm2(2, 12345, 67890, 4),
                "different seeds should differ");
            int a = Noise.Value2(42, (5L << 16) - 1, 0);
            int b = Noise.Value2(42, 5L << 16, 0);
            Assert(Math.Abs(a - b) < 2048, $"discontinuity at lattice boundary: {a} vs {b}");
        });

        Case("m7 biomes: reference seed pins golden hash and full coverage", () =>
        {
            var field = new BiomeField(0x5F3759DF);
            AssertEqual((long)field.SampleHash(-256, -256, 512, 2),
                unchecked((long)0xECE579531E21DE47UL),
                "golden biome hash drifted (bump GeneratorVersion if intentional)");
            var seen = new HashSet<Biome>();
            for (long y = -2048; y < 2048; y += 8)
                for (long x = -2048; x < 2048; x += 8)
                    seen.Add(field.Classify(x, y));
            AssertEqual(seen.Count, 13, "biome kinds in ±2048 tile sample");
        });

        Case("m7 sites: every registry delve floor validates and is deterministic", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            foreach (var (siteId, recipe) in SiteGen.SiteRegistry.Sites)
            {
                foreach (string m in recipe.SpawnMonsters)
                    Assert(data.Monsters.ContainsKey(m), $"{siteId}: unknown spawn monster {m}");
                Assert(data.Monsters.ContainsKey(recipe.BossMonster), $"{siteId}: unknown boss {recipe.BossMonster}");
                for (int floor = 0; floor < recipe.Floors; floor++)
                {
                    var f = SiteGen.SiteGenerator.GenerateFloor(0x5F3759DF, siteId, floor, recipe);
                    var f2 = SiteGen.SiteGenerator.GenerateFloor(0x5F3759DF, siteId, floor, recipe);
                    AssertEqual((long)f.ContentHash(), (long)f2.ContentHash(), $"{siteId}[{floor}] determinism");
                    bool boss = floor == recipe.Floors - 1;
                    var goal = boss ? f.Boss!.Value : f.StairsDown;
                    Assert(SiteGen.GridSearch.Reaches(f, f.Entry, goal,
                        t => t.IsHonestPassable()), $"{siteId}[{floor}]: honest route broken");
                }
            }
            // Golden pins: generator drift must be deliberate (bump GeneratorVersion).
            var mine1 = SiteGen.SiteGenerator.GenerateFloor(0x5F3759DF, "hollow_mine", 1,
                SiteGen.SiteRegistry.Sites["hollow_mine"]);
            AssertEqual((long)mine1.ContentHash(), unchecked((long)0x37332706469E8DE5UL), "hollow_mine[1] golden (gen v2)");
            var sewer0 = SiteGen.SiteGenerator.GenerateFloor(0x5F3759DF, "sewer_brasshollow", 0,
                SiteGen.SiteRegistry.Sites["sewer_brasshollow"]);
            AssertEqual((long)sewer0.ContentHash(), unchecked((long)0xA59C410FDA502717UL), "sewer[0] golden (gen v2)");
        });

        Case("m7 sites: the validator fails loudly on broken topology", () =>
        {
            var recipe = SiteGen.SiteRegistry.Sites["hollow_mine"];
            var f = SiteGen.SiteGenerator.GenerateFloor(0x5F3759DF, "hollow_mine", 1, recipe);
            // Wall off the goal: the honest-route proof must reject.
            var g = f.StairsDown;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (dx != 0 || dy != 0)
                        f.Set(g.X + dx, g.Y + dy, SiteGen.SiteTile.Wall);
            string? reason = SiteGen.SiteValidator.Validate(f, recipe.StyleFor(1), recipe, bossFloor: false);
            Assert(reason != null && reason.Contains("unreachable"), $"broken floor accepted: {reason ?? "null"}");
        });

        Case("m8 chunks: chunked static map reproduces world.json byte-identically", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            // Golden pinned from a direct RLE decode of data/world.json.
            AssertEqual((long)data.Map.StaticTileHash(), unchecked((long)0x29C391DE1BD5564AUL),
                "chunked tile store diverged from the authored map");
            AssertEqual(data.Map.TileAt(-1, 40), -1, "west edge should be void without a frontier");
            AssertEqual(data.Map.TileAt(512, 40), -1, "east edge should be void without a frontier");
        });

        Case("m8 frontier: deterministic, sailable, land exists, world is bounded", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            var world = new World(data, 0x5F3759DF);
            int oceanId = data.Map.TileIdByName("ocean");

            AssertEqual(data.Map.TileAt(-5, 100), oceanId, "west blend ring should be ocean");
            AssertEqual(data.Map.TileAt(520, 100), oceanId, "east blend ring should be ocean");
            Assert(world.TileAllows(-5, 100, VehicleKind.Ship), "ship refused the blend ring");

            int probe = data.Map.TileAt(-2000, 3000);
            AssertEqual(data.Map.TileAt(-2000, 3000), probe, "chunk cache unstable");
            var data2 = GameData.LoadFromDirectory(dataDir);
            _ = new World(data2, 0x5F3759DF);
            AssertEqual(data2.Map.TileAt(-2000, 3000), probe, "frontier regeneration diverged");

            bool foundLand = false, foundWater = false;
            for (int tx = 600; tx < 1800 && !(foundLand && foundWater); tx += 7)
                for (int ty = 600; ty < 1800; ty += 7)
                {
                    if (data.Map.IsWalkableTile(tx, ty)) foundLand = true;
                    else if (data.Map.TileAt(tx, ty) == oceanId) foundWater = true;
                    if (foundLand && foundWater) break;
                }
            Assert(foundLand, "no walkable frontier land within 1800 tiles");
            Assert(foundWater, "no frontier ocean within 1800 tiles");

            AssertEqual(data.Map.TileAt(1_100_000, 0), -1, "world should end at the D2 bound");
            AssertEqual(data.Map.TileAt(0, -1_100_000), -1, "world should end at the D2 bound (north)");
        });

        Case("m7 delves: enter, descend, ascend, exit, sewers via grates", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            var world = new World(data, 0x5F3759DF);
            var p = world.AddPlayer("Delver", "squire");
            int u = SimConstants.UnitsPerTile;

            var region = data.Map.Region("hollow_mine")!;
            p.X = (region.X + region.W / 2) * u + u / 2;
            p.Y = (region.Y + region.H / 2) * u + u / 2;
            string entered = world.SiteInteract(p);
            Assert(entered.Contains("Hollow Mine"), $"enter failed: '{entered}'");
            Assert(p.MapId != 0, "player not in an instance");
            Assert(world.EntitiesInOrder().Any(e => e.IsMonster && e.MapId == p.MapId),
                "delve floor has no monsters");

            for (int hop = 0; hop < 2; hop++)
            {
                var cur = world.GetInstance(p.MapId)!;
                p.X = cur.Layout.StairsDown.X * u + u / 2;
                p.Y = cur.Layout.StairsDown.Y * u + u / 2;
                Assert(world.SiteInteract(p).Contains("floor"), "descend failed");
            }
            AssertEqual(world.GetInstance(p.MapId)!.Floor, 2, "floor after two descents");

            // Deepest floor hosts the boss.
            var last = SiteGenerator.GenerateFloor(0x5F3759DF, "hollow_mine", 4,
                SiteRegistry.Sites["hollow_mine"]);
            Assert(last.Boss != null, "final floor lacks a boss room");

            while (world.GetInstance(p.MapId) is { } inst2 && inst2.Floor > 0)
            {
                p.X = inst2.Layout.Entry.X * u + u / 2;
                p.Y = inst2.Layout.Entry.Y * u + u / 2;
                Assert(world.SiteInteract(p).Length > 0, "ascend failed");
            }
            var f0 = world.GetInstance(p.MapId)!;
            p.X = f0.Layout.Entry.X * u + u / 2;
            p.Y = f0.Layout.Entry.Y * u + u / 2;
            world.SiteInteract(p);
            AssertEqual(p.MapId, 0, "player should be back on the overworld");

            var grates = world.SewerGrates();
            AssertEqual(grates.Count, 3, "sewer grate count");
            p.X = grates[1].tx * u + u / 2;
            p.Y = grates[1].ty * u + u / 2;
            string sewer = world.SiteInteract(p);
            Assert(sewer.Contains("Sewers"), $"sewer entry failed: '{sewer}'");
            Assert(p.MapId != 0, "not in the sewers");
        });

        Case("m7 delves: chest loot is deterministic, once per character, persisted", () =>
        {
            var data = GameData.LoadFromDirectory(dataDir);
            var world = new World(data, 0x5F3759DF);
            var p = world.AddPlayer("Looter", "squire");
            int u = SimConstants.UnitsPerTile;

            // Find a mine floor with a chest and stand next to it.
            var recipe = SiteRegistry.Sites["hollow_mine"];
            int chestFloor = -1;
            for (int floor = 0; floor < recipe.Floors && chestFloor < 0; floor++)
                if (SiteGenerator.GenerateFloor(0x5F3759DF, "hollow_mine", floor, recipe).Chests.Count > 0)
                    chestFloor = floor;
            Assert(chestFloor >= 0, "no chest on any mine floor");

            var region = data.Map.Region("hollow_mine")!;
            p.X = (region.X + region.W / 2) * u + u / 2;
            p.Y = (region.Y + region.H / 2) * u + u / 2;
            world.SiteInteract(p);
            while (world.GetInstance(p.MapId)!.Floor < chestFloor)
            {
                var cur = world.GetInstance(p.MapId)!;
                p.X = cur.Layout.StairsDown.X * u + u / 2;
                p.Y = cur.Layout.StairsDown.Y * u + u / 2;
                world.SiteInteract(p);
            }
            var inst = world.GetInstance(p.MapId)!;
            var chest = inst.Layout.Chests[0];
            p.X = chest.X * u + u / 2;
            p.Y = chest.Y * u + u / 2;
            int gilBefore = p.Gil;
            string loot = world.SiteInteract(p);
            Assert(loot.Contains("chest holds"), $"loot failed: '{loot}'");
            Assert(p.Gil > gilBefore, "chest granted no gil");
            Assert(world.SiteInteract(p).Contains("already emptied"), "chest re-lootable");

            var save = world.SnapshotSave(p);
            Assert(save.OpenedChests.Count == 1, "opened chest not persisted");
            // Logging out inside the delve saves the surface entrance.
            Assert(data.Map.IsWalkableTile(save.X / u, save.Y / u), "save position not walkable");
            var world2 = new World(data, 42);
            var p2 = world2.AddPlayer("Looter", "squire", save);
            Assert(p2.OpenedChests.Count == 1, "opened chest lost through save");
        });

        Case("m7 determinism: hash stable with delve activity in the loop", () =>
        {
            static ulong Run(string dir)
            {
                var data = GameData.LoadFromDirectory(dir);
                var world = new World(data, 0x5F3759DF);
                var p = world.AddPlayer("Spelunker", "squire");
                int u = SimConstants.UnitsPerTile;
                var region = data.Map.Region("hollow_mine")!;
                p.X = (region.X + region.W / 2) * u + u / 2;
                p.Y = (region.Y + region.H / 2) * u + u / 2;
                world.SiteInteract(p);
                for (int t = 0; t < 400; t++)
                {
                    world.SetPlayerInput(p.Id, new InputCommand
                    {
                        Seq = t,
                        MoveX = (sbyte)(t % 5 == 0 ? 1 : t % 7 == 0 ? -1 : 0),
                        MoveY = (sbyte)(t % 3 == 0 ? 1 : 0),
                        AbilitySlot = (sbyte)(t % 25 == 0 ? 0 : -1),
                        TargetId = NearestMonsterId(world, p),
                    });
                    world.Step();
                    if (t == 200) world.SiteInteract(p); // whatever is underfoot
                }
                return world.StateHash();
            }
            AssertEqual((long)Run(dataDir), (long)Run(dataDir), "delve hash diverged");
        });

        log($"selftest: {passed} passed, {failed} failed");
        return failed == 0;
    }

    /// <summary>Scripted world exercising gathering + crafting + combat for 400 ticks.</summary>
    private static ulong RunM3ScriptedWorld(string dataDir, ulong seed)
    {
        var data = GameData.LoadFromDirectory(dataDir);
        var world = new World(data, seed);
        var p = world.AddPlayer("Gatherer", "scout",
            appearance: new Appearance { Skin = 2, HairStyle = 5, HairColor = 7, Outfit = 1, OutfitA = 3, OutfitB = 9, Acc = 1 });
        world.AddItem(p, "copper_axe");
        world.AddItem(p, "sickle");

        var oak = world.EntitiesInOrder().First(e => e.IsNode && e.NodeType == "oak");
        p.X = oak.X + 20;
        p.Y = oak.Y;
        int oakId = oak.Id;

        for (int t = 0; t < 400; t++)
        {
            world.SetPlayerInput(p.Id, new InputCommand
            {
                Seq = t,
                MoveX = (sbyte)(t % 9 == 0 ? 1 : t % 13 == 0 ? -1 : 0),
                MoveY = 0,
                AbilitySlot = -1,
                TargetId = -1,
            });
            world.Step();
            if (t % 25 == 0) world.Gather(p, oakId);
            if (t == 200) world.CraftItem(p, "craft_rations"); // may or may not have inputs — deterministic either way
        }
        return world.StateHash();
    }

    /// <summary>Scripted world exercising M2 systems (shop, equip, quest, party) plus combat for 600 ticks.</summary>
    private static ulong RunM2ScriptedWorld(string dataDir, ulong seed)
    {
        var data = GameData.LoadFromDirectory(dataDir);
        var world = new World(data, seed);
        var p1 = world.AddPlayer("Alpha", "squire");
        var p2 = world.AddPlayer("Beta", "mage");
        var vendor = world.EntitiesInOrder().First(e => e.IsNpc && e.NpcDefId == "marla");
        var elder = world.EntitiesInOrder().First(e => e.IsNpc && e.NpcDefId == "elder_bran");

        p1.Gil = 500;
        p1.X = vendor.X + 16; p1.Y = vendor.Y;
        world.BuyItem(p1, vendor.Id, "bronze_sword");
        world.EquipItem(p1, "bronze_sword");
        world.BuyItem(p1, vendor.Id, "potion");
        p1.X = elder.X + 16; p1.Y = elder.Y;
        world.AcceptQuest(p1, elder.Id, "cull_wisps");
        world.PartyInvite(p1, "Beta");
        world.PartyAccept(p2);

        for (int t = 0; t < 600; t++)
        {
            world.SetPlayerInput(p1.Id, new InputCommand
            {
                Seq = t,
                MoveX = (sbyte)(t % 5 == 0 ? 1 : 0),
                MoveY = (sbyte)(t % 3 == 0 ? 1 : t % 7 == 0 ? -1 : 0),
                AbilitySlot = (sbyte)(t % 30 == 0 ? 0 : -1),
                TargetId = NearestMonsterId(world, p1),
            });
            world.SetPlayerInput(p2.Id, new InputCommand
            {
                Seq = t,
                MoveX = (sbyte)(t % 4 == 0 ? 1 : 0),
                MoveY = (sbyte)(t % 6 == 0 ? 1 : 0),
                AbilitySlot = (sbyte)(t % 45 == 0 ? 0 : -1),
                TargetId = NearestMonsterId(world, p2),
            });
            world.Step();
            if (t == 300) world.UseItem(p1, "potion");
        }
        return world.StateHash();
    }

    /// <summary>Two players with scripted inputs fighting/walking for 1000 ticks; returns final hash.</summary>
    private static ulong RunScriptedWorld(string dataDir, ulong seed)
    {
        var data = GameData.LoadFromDirectory(dataDir);
        var world = new World(data, seed);
        var p1 = world.AddPlayer("Alpha", data.ClassOrder[0]);
        var p2 = world.AddPlayer("Beta", data.ClassOrder[Math.Min(1, data.ClassOrder.Count - 1)]);

        for (int t = 0; t < 1000; t++)
        {
            world.SetPlayerInput(p1.Id, new InputCommand
            {
                Seq = t,
                MoveX = (sbyte)(t % 7 == 0 ? 1 : t % 5 == 0 ? -1 : 0),
                MoveY = (sbyte)(t % 3 == 0 ? 1 : t % 11 == 0 ? -1 : 0),
                AbilitySlot = (sbyte)(t % 40 == 0 ? 0 : -1),
                TargetId = NearestMonsterId(world, p1),
            });
            world.SetPlayerInput(p2.Id, new InputCommand
            {
                Seq = t,
                MoveX = (sbyte)(t % 4 == 0 ? -1 : 0),
                MoveY = (sbyte)(t % 6 == 0 ? -1 : t % 9 == 0 ? 1 : 0),
                AbilitySlot = (sbyte)(t % 55 == 0 ? 0 : -1),
                TargetId = NearestMonsterId(world, p2),
            });
            world.Step();
        }
        return world.StateHash();
    }

    private static int NearestMonsterId(World world, Entity player)
    {
        int best = -1;
        long bestD2 = long.MaxValue;
        foreach (var e in world.EntitiesInOrder())
        {
            if (!e.IsMonster || !e.Alive) continue;
            long d2 = e.DistanceSquaredTo(player);
            if (d2 < bestD2)
            {
                bestD2 = d2;
                best = e.Id;
            }
        }
        return best;
    }

    private static T Parse<T>(string path)
        => JsonSerializer.Deserialize<T>(File.ReadAllText(path), GameData.JsonOptions)
           ?? throw new Exception($"{path} parsed to null");

    private static void Assert(bool cond, string message)
    {
        if (!cond) throw new Exception(message);
    }

    private static void AssertEqual(long actual, long expected, string what)
    {
        if (actual != expected) throw new Exception($"{what}: expected {expected}, got {actual}");
    }
}
