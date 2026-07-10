using System.Text.Json;

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
            // Teleport-adjacent test: walk the player at the nearest monster until something dies.
            Entity? mob = world.EntitiesInOrder().Where(e => e.IsMonster)
                .OrderBy(e => e.DistanceSquaredTo(p)).First();
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
            Assert(world.BuyItem(p, vendor.Id, "bronze_sword").StartsWith("Not enough"), "overspend allowed");

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
            Assert(save.Appearance.Count == 7, "save missing appearance");
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
            // Move beside the forge (33,68) and smelt.
            p.X = 32 * u + u / 2;
            p.Y = 68 * u + u / 2;
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
