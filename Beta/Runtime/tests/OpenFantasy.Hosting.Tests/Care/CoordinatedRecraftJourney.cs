using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>
/// Fresh complete ordinary-care content with one explicitly authored, mass-conserving repack recipe.
/// Its two original raw Potions are unchanged; every produced generation is earned by a real command.
/// </summary>
internal sealed class CoordinatedRecraftJourney
{
    private ulong _identity = 100;
    private CareWorldSession _session;

    internal CoordinatedRecraftJourney()
    {
        var source = RestorativeJourneyAdmission.CreateEmbedded("coordinated-material-capacity", "Traveler");
        Assert.Equal((ushort)26, source.ProfileVersion);
        Potion = source.Care!.Remedies.Single().Item;
        var original = source.Production!.Recipes.Single(value => value.PhysicalRecipe.Key == CareJourneyAdmission.RecipeKey);
        var binding = original.BindingFor(Potion);
        var physical = new MaterialRecipeDefinition(binding.Physical.Package, Id(1), "coordinated_repack_potion",
            new(1), null, new MaterialCatalog([binding.Physical]),
            [new(RecipeMaterialRole.Input, binding.Physical.Pin(), 1), new(RecipeMaterialRole.Output, binding.Physical.Pin(), 1)]);
        Recipe = new(Id(2), physical, [binding], [new(Potion, 0, binding.Physical.MassPerUnit)]);
        Assert.False(source.IdentityIndex.Contains(Recipe.DefinitionId));
        Assert.False(source.IdentityIndex.Contains(physical.DefinitionId));
        var production = new ProductionSnapshot(source.WorldId, source.BranchId, 0,
            source.Production.Recipes.Append(Recipe), [], []);
        var admission = ContentHash.Compute(source.ContentHash.ToByteArray().Concat(Recipe.Version.ToByteArray()).ToArray());
        var basis = new WorldSnapshot(source.Inventory, source.Space, source.Residents, admission, source.Calendar, 0, 0,
            gathering: source.Gathering, production: production, progression: source.Progression,
            quests: source.Quests, construction: source.Construction);

        // Re-admit the same fresh closed reservoir against the full revised recipe catalog. No
        // material, work, clock, terrain, care, permission, encounter or resource history is earned here.
        var oldCapacity = source.Reservoirs!.Capacity;
        Assert.Empty(oldCapacity.Pending);
        var closed = Assert.Single(oldCapacity.Closed);
        var domain = oldCapacity.Domain;
        var request = new WatershedRunoffRequest(domain.Network, domain.Mapping, source.Inventory, production,
            closed, domain.OwnerActorId, source.Calendar, source.Calendar.AddMilliseconds(1), domain.Material,
            domain.Network.Basins.Select(basin => WatershedRunoffInput.Dry(basin.BasinId)), domain.Terminals);
        var capacity = WatershedRunoffCapacityState.Create(new WatershedRunoffPlanner().Prepare(request,
            TestContext.Current.CancellationToken));
        capacity = capacity.PlanCancel(closed).ApplyTo(capacity);
        var reservoirs = LiquidReservoirState.Create(capacity, source.Reservoirs.Geometries);
        var topology = TerrainLiquidTopology.Create(reservoirs, source.Space.Regions, source.ReservoirSeals!);
        var cells = LiquidCellState.Create(reservoirs, source.LiquidCells!.Profile, source.Calendar,
            source.LiquidCells.Portions, topology);
        var wet = WorldTerrainLiquidAdmission.CreateGenesis(basis, reservoirs, source.ReservoirSeals!, source.Excavation!, cells);
        var lifecycle = WorldEncounterLifecycleAdmission.CreateFresh(WorldPacingAdmission.CreateFresh(wet), source.EncounterLifecycle!);
        var care = new CareSnapshot(source.WorldId, source.BranchId, 0, source.Care.Remedies, source.Care.Agreements, [], []);
        var ordinary = WorldRestorativeAdmission.CreateFresh(WorldCareAdmission.CreateFresh(lifecycle, care), source.Care.Ordinary!);
        Genesis = WorldCoordinatedAdmission.CreateFresh(ordinary);
        Assert.Equal(source.Inventory.ContentHash, Genesis.Inventory.ContentHash);
        Assert.Equal(source.Gathering!.ContentHash, Genesis.Gathering!.ContentHash);
        Assert.Equal(source.Excavation!.ContentHash, Genesis.Excavation!.ContentHash);
        Assert.Equal(source.Construction!.ContentHash, Genesis.Construction!.ContentHash);
        Assert.Equal(source.Quests!.ContentHash, Genesis.Quests!.ContentHash);
        Assert.Equal(source.Progression!.ContentHash, Genesis.Progression!.ContentHash);
        Assert.Equal(source.LiquidCells.Portions, Genesis.LiquidCells!.Portions);
        Assert.Equal(source.Care.ContentHash, Genesis.Care!.ContentHash);
        Assert.Equal(0UL, Genesis.Revision);
        Assert.Empty(Genesis.Production!.Batches);
        Assert.Empty(Genesis.Production.Lots);
        Assert.Empty(Genesis.ResourceHistory!.History.Batches);
        var player = Genesis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        var helper = Genesis.Quests.Definitions.Single().GiverActorId;
        Actors = new[] { player, helper }.Order().ToArray();
        Assert.All(Actors, actor => Assert.Equal(1UL, Genesis.Inventory.Custody.Lots.Single(value =>
            value.CustodianId == Genesis.GetResident(actor).InventoryCustodianId && value.Definition == Potion).Quantity));
        _session = new(Genesis);
    }

    internal WorldSnapshot Genesis { get; }
    internal CountedItemDefinition Potion { get; }
    internal CraftRecipeDefinition Recipe { get; }
    internal EntityId[] Actors { get; }
    internal WorldSnapshot World => _session.ReadStateForHost();
    internal IEnumerable<EntityId> OccupiedIdentities => _session.OccupiedIdentities;

    internal WorldActionCommand Craft(EntityId actor, ulong? expectedProductionRevision = null)
    {
        var state = World;
        var bag = state.GetResident(actor).InventoryCustodianId;
        var output = state.Production!.Lots.SingleOrDefault(value => value.CustodianId == bag && value.Definition == Potion);
        var raw = output is null ? state.Inventory.Custody.Lots.Single(value => value.CustodianId == bag && value.Definition == Potion) : null;
        var ingredient = output is not null
            ? new IngredientRequest(IngredientLotKind.Transformed, output.LotId, output.Revision, 1)
            : new IngredientRequest(IngredientLotKind.Source, raw!.LotId, raw.Revision, 1);
        var request = new CraftRequest(Recipe.DefinitionId, Recipe.Version,
            expectedProductionRevision ?? state.Production.Revision, bag,
            state.Inventory.Custody.GetStorage(bag).Revision, [ingredient]);
        var control = state.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(NextId(), control.PrincipalId, control.SessionId, actor, state.WorldId, state.BranchId,
            state.Space.GetActor(actor).Revision, new CraftIntent(request));
    }

    internal WorldStepBatch Batch(params WorldActionCommand[] commands) => new(NextId(), World.WorldId, World.BranchId,
        World.Revision, World.AdvancingSteps, WorldBatchKind.AdvancingStep, [], commands, World.ProfileVersion);

    internal WorldStepDecision Commit(WorldStepBatch batch) => _session.Commit(batch);

    internal void Reopen(WorldMaterialCareArchiveAnchor? anchor)
    {
        byte[] bytes = WorldSnapshotCodec.Encode(World);
        var restored = WorldSnapshotCodec.DecodeCoordinated(bytes, anchor);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(restored));
        _session = new(restored, _session.OccupiedIdentities.ToArray());
    }

    internal WorldMaterialCareArchiveSegment ManualCut()
    {
        var before = World;
        var segment = WorldMaterialCareArchiveSegment.Seal(before);
        var retrieved = WorldMaterialCareArchiveSegment.Decode(segment.Reference, segment.Encode(), before.Coordination!.Anchor);
        _session = new(retrieved.Continued, _session.OccupiedIdentities.ToArray());
        Reopen(retrieved.Anchor);
        return retrieved;
    }

    internal CraftPlan Plan(CountedAuthorityState inventory, ProductionSnapshot production, WorldActionCommand command,
        Func<EntityId, bool> occupied) => new InstantCraftPlanner().Plan(inventory, production,
            new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId), command.ActorId,
            command.CommandId, ((CraftIntent)command.Intent).Request, occupied);

    private EntityId NextId()
    {
        var identity = Id(_identity++);
        Assert.False(Genesis.IdentityIndex.Contains(identity));
        return identity;
    }

    private static EntityId Id(ulong value) => new(0xc00dba7c00000000, value);
}
