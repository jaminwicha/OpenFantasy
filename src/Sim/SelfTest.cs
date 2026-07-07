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

        log($"selftest: {passed} passed, {failed} failed");
        return failed == 0;
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
