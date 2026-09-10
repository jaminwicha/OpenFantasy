using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Language;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

internal static class WorldServiceFixture
{
    internal static EntityId Id(ulong value) => new(0x1020304050607080, value);
    internal static TrustedCommandDelivery Delivery => new(Id(100), Id(110));

    internal static WorldSnapshot Genesis()
    {
        ContentHash version = ContentHash.Compute("world-service-fixture"u8);
        var scope = new TerrainScope(Id(1), Id(2), Id(3));
        var material = new CountedItemDefinition(Id(80), version);
        var inventory = new CountedAuthorityState(new CustodySnapshot(Id(1), Id(2), 7,
            [new(Id(60), 10, 0), new(Id(61), 10, 0)],
            [new(Id(70), Id(60), material, new SourceInterval(Id(71), 0, 6), 0)]), 10,
            [Id(10), Id(11)], [new(Id(100), Id(110), Id(10)), new(Id(101), Id(111), Id(11))],
            [new(Id(60), Id(10), CustodyDepositRule.OwnerOnly), new(Id(61), Id(11), CustodyDepositRule.AnyControlledActor)],
            [new(Id(71), material, 6)]);
        var body = new BodyDefinition(Id(40), BodyNature.Corporeal, new BodyDimensions(128, 128, 384),
            [new(new BodySlotKey("core"), null, new BodyDimensions(64, 64, 128),
                [BodyFunction.GroundContact, BodyFunction.Hearing, BodyFunction.Vocalization, BodyFunction.Manipulation])],
            [new(LocomotionMode.Ground, 1)]);
        var terrain = new StratifiedTerrainGenerator().Generate(scope, Id(150),
            new StratifiedTerrainRules(0, 0, 1, new TerrainMaterialPalette(material, material, material)),
            new TerrainRegionBounds(new CellCoordinate(-2, -2, -1), 8, 8, 5));
        var channel = new LanguageChannelKey(Id(90), Id(91), CommunicationMode.Spoken);
        ActorPresence[] actors = new ulong[] { 0, 1 }.Select(ordinal =>
        {
            var binding = ActorFormBinding.Corporeal(Id(10 + ordinal), Id(20 + ordinal), Id(30 + ordinal), body);
            return new ActorPresence(binding, new BodyConditionSnapshot(binding.FormInstanceId, body, []), scope,
                new SubunitPosition((long)ordinal * 512, 0, 256), 0, Id(50 + ordinal));
        }).ToArray();
        WorldResident[] residents = new ulong[] { 0, 1 }.Select(ordinal => new WorldResident(Id(10 + ordinal),
            Id(60 + ordinal), "Resident " + ordinal, ordinal == 0 ? ResidentControllerKind.Player : ResidentControllerKind.Host,
            actors[(int)ordinal].Feet, 0, new ActorComprehensionProfile(Id(10 + ordinal), 0, version,
                [new(channel, version, ComprehensionBand.Conversational)]), default,
            new ResidentUtterance(Id(92), channel, version, "The farm needs six wood."))).ToArray();
        return new WorldSnapshot(inventory, new LocalSpatialSnapshot(scope, [terrain], actors), residents,
            version, new WorldInstant(0, 1000), 0, 0);
    }

    internal static WorldActionCommand Give(WorldSnapshot state, ulong command = 200, ulong quantity = 6) =>
        new(Id(command), Id(100), Id(110), Id(10), state.WorldId, state.BranchId,
            state.Space.GetActor(Id(10)).Revision,
            new GiveItemIntent(Id(11), state.Space.GetActor(Id(11)).Revision, Id(70),
                state.Inventory.Custody.GetLot(Id(70)).Revision, Id(60), state.Inventory.Custody.GetStorage(Id(60)).Revision, quantity));

    internal static WorldStepBatch Step(WorldSnapshot state, ulong cause = 300,
        IEnumerable<WorldActionCommand>? actions = null, IEnumerable<WorldMovementInput>? movement = null) =>
        new(Id(cause), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
            state.HasReadingHold ? WorldBatchKind.HeldInteraction : WorldBatchKind.AdvancingStep,
            movement ?? [], actions ?? []);
}
