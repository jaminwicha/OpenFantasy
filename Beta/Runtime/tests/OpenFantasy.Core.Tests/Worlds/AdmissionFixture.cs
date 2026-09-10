using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Language;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Worlds;

internal static class AdmissionFixture
{
    internal static EntityId Id(ulong value) => new(0, value);
    internal static readonly ContentHash Version = ContentHash.Compute("admission-test-pin"u8);
    internal static readonly CountedItemDefinition Material = new(Id(80), Version);
    internal static readonly TerrainScope Scope = new(Id(1), Id(2), Id(3));
    internal static readonly LanguageChannelKey Channel = new(Id(90), Id(91), CommunicationMode.Spoken);

    internal static WorldSnapshot Create(
        Func<CountedAuthorityState, CountedAuthorityState>? inventory = null,
        Func<LocalSpatialSnapshot, LocalSpatialSnapshot>? space = null,
        Func<WorldResident[], WorldResident[]>? residents = null,
        IEnumerable<DialogueSession>? dialogues = null, ulong revision = 2, ulong steps = 1)
    {
        CountedAuthorityState counted = Inventory();
        LocalSpatialSnapshot geometry = new(Scope, [Terrain()], [Presence(0), Presence(1)]);
        WorldResident[] people = [Resident(0), Resident(1)];
        return new WorldSnapshot(inventory?.Invoke(counted) ?? counted, space?.Invoke(geometry) ?? geometry,
            residents?.Invoke(people) ?? people, Version, new WorldInstant(0, 2000), revision, steps, dialogues);
    }

    internal static CountedAuthorityState Inventory(EntityId? sourceId = null,
        IEnumerable<ActorControlBinding>? controls = null, CountedItemDefinition? material = null,
        ulong revision = 9)
    {
        CountedItemDefinition definition = material ?? Material;
        EntityId source = sourceId ?? Id(71);
        return new CountedAuthorityState(new CustodySnapshot(Id(1), Id(2), revision,
            [new(Id(60), 10, 0), new(Id(61), 10, 0)],
            [new(Id(70), Id(60), definition, new SourceInterval(source, 0, 6), 0)]), 10,
            [Id(10), Id(11)], controls ?? [new(Id(100), Id(110), Id(10)), new(Id(101), Id(111), Id(11))],
            [new(Id(60), Id(10), CustodyDepositRule.OwnerOnly), new(Id(61), Id(11), CustodyDepositRule.AnyControlledActor)],
            [new(source, definition, 6)]);
    }

    internal static TerrainRegionSnapshot Terrain(EntityId? materialId = null)
    {
        var pin = new CountedItemDefinition(materialId ?? Material.DefinitionId, Version);
        return new StratifiedTerrainGenerator().Generate(Scope, Id(150),
            new StratifiedTerrainRules(0, 0, 1, new TerrainMaterialPalette(pin, pin, pin)),
            new TerrainRegionBounds(new CellCoordinate(-2, -2, -1), 8, 8, 5));
    }

    internal static ActorPresence Presence(ulong ordinal, EntityId? formId = null,
        IEnumerable<BodyInjury>? injuries = null, bool vocal = true)
    {
        BodyFunction[] functions = vocal
            ? [BodyFunction.GroundContact, BodyFunction.Hearing, BodyFunction.Vocalization]
            : [BodyFunction.GroundContact, BodyFunction.Hearing];
        var body = new BodyDefinition(Id(vocal ? 40UL : 41UL), BodyNature.Corporeal,
            new BodyDimensions(128, 128, 384),
            [new(new BodySlotKey("core"), null, new BodyDimensions(64, 64, 128), functions)],
            [new(LocomotionMode.Ground, 1)]);
        var binding = ActorFormBinding.Corporeal(Id(10 + ordinal), formId ?? Id(20 + ordinal), Id(30 + ordinal), body);
        return new ActorPresence(binding, new BodyConditionSnapshot(binding.FormInstanceId, body, injuries ?? []),
            Scope, new SubunitPosition((long)ordinal * 512, 0, 256), 0, Id(50 + ordinal));
    }

    internal static WorldResident Resident(ulong ordinal, LanguageChannelKey? channel = null,
        ContentHash? channelVersion = null, ContentHash? rulesVersion = null,
        ComprehensionBand band = ComprehensionBand.Conversational, ResidentUtterance? utterance = null)
    {
        LanguageChannelKey selected = channel ?? Channel;
        return new WorldResident(Id(10 + ordinal), Id(60 + ordinal), "Resident " + ordinal,
            ordinal == 0 ? ResidentControllerKind.Player : ResidentControllerKind.Host,
            new SubunitPosition((long)ordinal * 512, 0, 256), 0,
            new ActorComprehensionProfile(Id(10 + ordinal), 0, rulesVersion ?? Version,
                [new(selected, channelVersion ?? Version, band)]), default,
            utterance ?? new ResidentUtterance(Id(92), Channel, Version, "The road is quiet today."));
    }

    internal static DialogueSession Dialogue(EntityId? cause = null) =>
        new(Id(140), Id(10), Id(11), Id(92), cause ?? Id(141), new WorldInstant(0, 1000));
}
