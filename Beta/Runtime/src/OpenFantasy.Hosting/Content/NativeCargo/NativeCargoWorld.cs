using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Content.NativeCargo;

/// <summary>
/// The retained paper-doll fields of one source merchant, copied unchanged from its <c>appearance</c> record:
/// race, skin ramp, hair style and ramp, outfit sheet, both outfit ramps and accessory. Presentation only.
/// </summary>
public sealed record NativeCargoAppearance(int Race, int Skin, int HairStyle, int HairColor, int Outfit, int OutfitA,
    int OutfitB, int Accessory);

/// <summary>
/// One stall of the native cargo world as presentation may name it: the admitted market and merchant identities,
/// the merchant's retained source identity and name, the retained place it was admitted at and the merchant's source
/// appearance. Labels only; quotes are associated with a stall through the canonical market quote record, never here.
/// </summary>
public sealed record NativeCargoStall(EntityId MarketId, EntityId MerchantActorId, string MerchantSourceId,
    string MerchantName, string PlaceSourceId, string PlaceName, NativeCargoAppearance Appearance);

/// <summary>
/// The explicit native selection of the accepted two-market cargo world. It reuses the admitted composition and
/// trading advice unchanged, names both stalls and binds each merchant to the appearance fields of its retained
/// source record (authenticated against the same digest-pinned packet the admission reads). It never opens, upgrades
/// or relabels an existing save; that decision belongs to the opener.
/// </summary>
public static class NativeCargoWorld
{
    /// <summary>The production seed of a fresh native cargo world.</summary>
    public const string SeedText = "cargo-circuit-world";

    private const string NpcPacketPath = "legacy/aldermere-first-loop/source-support/data/npcs.json";
    private static readonly Lazy<CargoCircuitSources> _sources = new(CargoCircuitSources.LoadEmbedded);
    private static readonly Lazy<IReadOnlyDictionary<string, NativeCargoAppearance>> _appearances = new(LoadAppearances);

    public static CargoCircuitSources Sources => _sources.Value;

    public static WorldSnapshot CreateEmbedded(string seedText, string playerName) =>
        CargoWorldAdmission.CreateEmbedded(seedText, playerName);

    /// <summary>Whether the world is this cargo composition of the embedded sources.</summary>
    public static bool IsCargoWorld(WorldSnapshot world) => CargoWorldAdmission.Find(world, Sources) is not null;

    /// <summary>The merchants' opt-in answers, supplied again on every opening and never saved.</summary>
    public static LocalTradingConfiguration Configuration(WorldSnapshot world) => CargoWorldAdmission.Configuration(world, Sources);

    /// <summary>Both stalls in market identity order, or an empty list for any other world.</summary>
    public static IReadOnlyList<NativeCargoStall> Stalls(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (CargoWorldAdmission.Find(world, Sources) is not { } markets)
        {
            return [];
        }
        NativeCargoStall Stall(Core.Markets.MarketBinding market, CargoSourceMerchant merchant) => new(market.MarketId,
            market.MerchantActorId, merchant.SourceId, merchant.Name, merchant.Place.SourceId, PlaceName(merchant.Place),
            _appearances.Value[merchant.SourceId]);
        return new[] { Stall(markets.Origin, Sources.Origin), Stall(markets.Destination, Sources.Destination) }
            .OrderBy(stall => stall.MarketId).ToArray();
    }

    /// <summary>
    /// The authored corridor of this world, built from its actual basis region (the region where the player was
    /// admitted). It is route advice for travel and its regions are the finite admitted cargo geography.
    /// </summary>
    public static CargoCorridor Corridor(WorldSnapshot genesis)
    {
        ArgumentNullException.ThrowIfNull(genesis);
        EntityId[] players = genesis.Residents.Where(value => value.ControllerKind == ResidentControllerKind.Player)
            .Select(value => value.ActorId).ToArray();
        EntityId player = players.Length == 1 ? players[0]
            : genesis.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey).InitiatingActorId;
        return Corridor(genesis, player);
    }

    /// <summary>Route advice anchored in an explicitly admitted human's starting region.</summary>
    public static CargoCorridor Corridor(WorldSnapshot genesis, EntityId player)
    {
        ArgumentNullException.ThrowIfNull(genesis);
        if (genesis.GetResident(player).ControllerKind != ResidentControllerKind.Player)
            throw new ArgumentException("Cargo route advice requires an admitted human anchor.", nameof(player));
        CellCoordinate standing = genesis.Space.GetActor(player).Feet.Cell;
        TerrainRegionBounds basis = genesis.Space.Regions.Single(region => region.Bounds.Contains(standing)).Bounds;
        return CargoCorridor.CreateCircuit(basis, Sources);
    }

    /// <summary>A retained place identity as a reader names it: "brasshollow" reads "Brasshollow".</summary>
    internal static string PlaceName(CargoSourcePlace place) =>
        string.Join(' ', place.SourceId.Split('_', '-').Select(word => word.Length == 0 ? word
            : char.ToUpperInvariant(word[0]) + word[1..]));

    /// <summary>
    /// Reads both merchants' appearance records from the digest-pinned packet and checks each against the appearance
    /// hash the source binding already authenticated. A missing, malformed or different record is refused.
    /// </summary>
    private static IReadOnlyDictionary<string, NativeCargoAppearance> LoadAppearances()
    {
        byte[] bytes = FarmContentFiles.Embedded().Read(NpcPacketPath, 65_536, CargoCircuitSources.NpcsDigest);
        using JsonDocument npcs = FarmContentFiles.Parse(bytes);
        Dictionary<string, NativeCargoAppearance> found = [];
        foreach (CargoSourceMerchant merchant in new[] { Sources.Origin, Sources.Destination })
        {
            JsonElement record = npcs.RootElement.GetProperty("npcs").EnumerateArray()
                .Single(entry => entry.GetProperty("id").GetString() == merchant.SourceId);
            JsonElement appearance = record.GetProperty("appearance");
            if (FarmCanonicalJson.Hash(appearance) != merchant.AppearanceHash)
            {
                throw new InvalidDataException("A merchant's appearance differs from its authenticated source record.");
            }
            int Field(string name) => appearance.GetProperty(name).GetInt32();
            found.Add(merchant.SourceId, new NativeCargoAppearance(Field("race"), Field("skin"), Field("hairStyle"),
                Field("hairColor"), Field("outfit"), Field("outfitA"), Field("outfitB"), Field("acc")));
        }
        return found;
    }
}
