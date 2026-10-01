using System.Globalization;
using System.Text.Json;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Progression;
using OpenFantasy.Hosting.Content.Cargo;

namespace OpenFantasy.Hosting.Content.RoadThreat;

/// <summary>One retained drop entry of the source monster; retained only, never rolled or issued.</summary>
public sealed record RoadThreatSourceDrop(string ItemId, int ChancePercent);

/// <summary>
/// The retained <c>bandit_chief</c> monster record. The whole record is kept (payload hash over its canonical form);
/// <see cref="RoadThreatSources"/> documents which fields execute in this binding and which are retained only.
/// </summary>
public sealed record RoadThreatSourceMonster(string SourceId, string Name, int Level, ActorStatValues Stats,
    int MoveSpeedUnits, int ExperienceReward, int GilMinimum, int GilMaximum, int AggroRangeUnits, int LeashRangeUnits,
    int AttackPower, int AttackRangeUnits, int AttackAtbCost, IReadOnlyList<RoadThreatSourceDrop> Drops, string Pointer,
    ContentHash PayloadHash);

/// <summary>The retained spawner placing that monster in its camp rectangle (source tiles, ticks at the source rate).</summary>
public sealed record RoadThreatSourceSpawner(int SpawnerId, string MonsterId, CargoTileRect Tiles, int Count,
    int RespawnTicks, string Pointer, ContentHash PayloadHash);

/// <summary>
/// The retained source binding of the road threat: the Bandit Chief monster (<c>monsters.json</c> /monsters/33), its
/// camp place (<c>world.json</c> /regions/16) and its spawner (/spawners/33). Files are read by exact digest; records
/// are bound by source identity at their pinned pointers and every executed raw field is checked against the values
/// this binding was reviewed against, so a changed byte, moved record or edited field is refused, never repaired.
/// <para>
/// Unit conversion: the source simulation measures distance in units of 1/32 tile (<c>SimConstants.UnitsPerTile</c>,
/// a hash-checked witness the accepted wolf binding already reads), while one runtime cell is 256 spatial subunits, so one
/// source unit is eight subunits. The attack range 50 therefore maps to 400 subunits, exactly as the accepted Bite maps
/// its range. ATB cost 480 becomes the explicit readiness ratio 480/300 against the standard 300 action, never a raw
/// tactical duration. Aggro (140), leash (400), move speed (4), experience/gil rewards, drops and respawn ticks are
/// retained but not executed: this binding has no exploration pursuit, leash return, victory reward or respawn.
/// </para>
/// </summary>
public sealed class RoadThreatSources
{
    public const string MonsterId = "bandit_chief";
    public const string CampPlaceId = "bandit_camp";
    public const string MonsterPointer = "/monsters/33";
    public const string CampPointer = "/regions/16";
    public const string SpawnerPointer = "/spawners/33";

    /// <summary>Spatial subunits per source distance unit: 256 subunits per cell over 32 source units per tile.</summary>
    public const long SubunitsPerSourceUnit = 8;

    /// <summary>The standard source action cost; readiness ratios are expressed against it.</summary>
    public const uint StandardAtbCost = 300;

    private const string Packet = "legacy/aldermere-first-loop/source-support/data/";
    internal const string MonstersDigest = "8a3599545e5ea3e1a8ecfdf396b4bd107d2f2f3c696fcaaa2ed76d9dab511493";

    private RoadThreatSources(RoadThreatSourceMonster monster, CargoSourcePlace camp, RoadThreatSourceSpawner spawner)
    {
        Monster = monster;
        Camp = camp;
        Spawner = spawner;
        Version = CargoCanonicalFrame.Hash("road-threat.sources.v1",
        [
            new ContentHash(MonstersDigest).ToByteArray(), new ContentHash(CargoCircuitSources.WorldDigest).ToByteArray(),
            CargoCanonicalFrame.Text(monster.Pointer), monster.PayloadHash.ToByteArray(),
            CargoCanonicalFrame.Text(camp.Pointer), camp.PayloadHash.ToByteArray(),
            CargoCanonicalFrame.Text(spawner.Pointer), spawner.PayloadHash.ToByteArray()
        ]);
    }

    public RoadThreatSourceMonster Monster { get; }
    public CargoSourcePlace Camp { get; }
    public RoadThreatSourceSpawner Spawner { get; }

    /// <summary>The source binding identity: both file digests, every pointer and every payload hash.</summary>
    public ContentHash Version { get; }

    /// <summary>The executed attack reach in spatial subunits.</summary>
    public long AttackRangeSubunits => checked(Monster.AttackRangeUnits * SubunitsPerSourceUnit);

    public static RoadThreatSources LoadEmbedded() => Load(FarmContentFiles.Embedded());

    public static RoadThreatSources Load(string contentRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        return Load(new FarmContentFiles(contentRoot));
    }

    internal static RoadThreatSources Load(FarmContentFiles files)
    {
        ArgumentNullException.ThrowIfNull(files);
        using JsonDocument monsters = FarmContentFiles.Parse(files.Read(Packet + "monsters.json", 32_768, MonstersDigest));
        using JsonDocument world = FarmContentFiles.Parse(files.Read(Packet + "world.json", 131_072, CargoCircuitSources.WorldDigest));
        return Bind(monsters.RootElement, world.RootElement);
    }

    /// <summary>Binds parsed documents; any missing, duplicated, moved or edited record is refused.</summary>
    internal static RoadThreatSources Bind(JsonElement monsters, JsonElement world)
    {
        RoadThreatSourceMonster monster = BindMonster(FindUnique(monsters, "monsters", "id", MonsterId));
        CargoSourcePlace camp = BindCamp(FindUnique(world, "regions", "id", CampPlaceId));
        RoadThreatSourceSpawner spawner = BindSpawner(FindUnique(world, "spawners", "monster", MonsterId), camp);
        return new RoadThreatSources(monster, camp, spawner);
    }

    private static RoadThreatSourceMonster BindMonster((JsonElement Record, string Pointer) found)
    {
        (JsonElement record, string pointer) = found;
        JsonElement attack = record.GetProperty("attack");
        RoadThreatSourceDrop[] drops = record.GetProperty("drops").EnumerateArray()
            .Select(drop => new RoadThreatSourceDrop(drop.GetProperty("item").GetString()!, drop.GetProperty("chancePercent").GetInt32()))
            .ToArray();
        var monster = new RoadThreatSourceMonster(record.GetProperty("id").GetString()!, record.GetProperty("name").GetString()!,
            record.GetProperty("level").GetInt32(), EncounterFarmSources.Stats(record.GetProperty("stats")),
            record.GetProperty("moveSpeed").GetInt32(), record.GetProperty("xpReward").GetInt32(),
            record.GetProperty("gilMin").GetInt32(), record.GetProperty("gilMax").GetInt32(),
            record.GetProperty("aggroRange").GetInt32(), record.GetProperty("leashRange").GetInt32(),
            attack.GetProperty("power").GetInt32(), attack.GetProperty("range").GetInt32(), attack.GetProperty("atbCost").GetInt32(),
            Array.AsReadOnly(drops), pointer, FarmCanonicalJson.Hash(record));
        // The reviewed raw fields; the binding refuses rather than reinterprets a different record.
        if (pointer != MonsterPointer || monster.Name != "Bandit Chief" || monster.Level != 7
            || monster.Stats != new ActorStatValues(800, 0, 27, 0, 15, 10) || monster.MoveSpeedUnits != 4
            || monster.ExperienceReward != 1400 || monster.GilMinimum != 260 || monster.GilMaximum != 480
            || monster.AggroRangeUnits != 140 || monster.LeashRangeUnits != 400 || monster.AttackPower != 15
            || monster.AttackRangeUnits != 50 || monster.AttackAtbCost != 480
            || !drops.SequenceEqual([new RoadThreatSourceDrop("ancient_coin", 70), new RoadThreatSourceDrop("steel_blade", 10),
                new RoadThreatSourceDrop("hi_potion", 40)]))
        {
            throw new InvalidDataException("The retained bandit_chief record differs from its reviewed source binding.");
        }
        return monster;
    }

    private static CargoSourcePlace BindCamp((JsonElement Record, string Pointer) found)
    {
        (JsonElement record, string pointer) = found;
        var camp = new CargoSourcePlace(CampPlaceId, record.GetProperty("x").GetInt64(), record.GetProperty("y").GetInt64(),
            record.GetProperty("w").GetInt32(), record.GetProperty("h").GetInt32(), pointer, FarmCanonicalJson.Hash(record));
        if (pointer != CampPointer || camp.Tiles != new CargoTileRect(345, 305, 360, 317))
        {
            throw new InvalidDataException("The retained bandit_camp place differs from its reviewed source binding.");
        }
        return camp;
    }

    private static RoadThreatSourceSpawner BindSpawner((JsonElement Record, string Pointer) found, CargoSourcePlace camp)
    {
        (JsonElement record, string pointer) = found;
        JsonElement region = record.GetProperty("region");
        long x = region.GetProperty("x").GetInt64();
        long y = region.GetProperty("y").GetInt64();
        var tiles = new CargoTileRect(x, y, x + region.GetProperty("w").GetInt64(), y + region.GetProperty("h").GetInt64());
        var spawner = new RoadThreatSourceSpawner(record.GetProperty("id").GetInt32(), record.GetProperty("monster").GetString()!,
            tiles, record.GetProperty("count").GetInt32(), record.GetProperty("respawnTicks").GetInt32(), pointer,
            FarmCanonicalJson.Hash(record));
        if (pointer != SpawnerPointer || spawner.SpawnerId != 34 || spawner.Count != 1 || spawner.RespawnTicks != 3600
            || spawner.Tiles != camp.Tiles)
        {
            throw new InvalidDataException("The retained bandit_chief spawner differs from its reviewed source binding.");
        }
        return spawner;
    }

    /// <summary>Finds exactly one record whose string property equals the identity, with its JSON pointer.</summary>
    private static (JsonElement Record, string Pointer) FindUnique(JsonElement root, string collection, string property,
        string value)
    {
        (JsonElement Record, string Pointer)? found = null;
        int index = 0;
        foreach (JsonElement entry in root.GetProperty(collection).EnumerateArray())
        {
            if (entry.TryGetProperty(property, out JsonElement id) && id.ValueKind == JsonValueKind.String && id.GetString() == value)
            {
                if (found is not null)
                {
                    throw new InvalidDataException("A retained source identity is duplicated: " + collection + "/" + value);
                }
                found = (entry.Clone(), "/" + collection + "/" + index.ToString(CultureInfo.InvariantCulture));
            }
            index++;
        }
        return found ?? throw new InvalidDataException("A retained source identity is missing: " + collection + "/" + value);
    }
}
