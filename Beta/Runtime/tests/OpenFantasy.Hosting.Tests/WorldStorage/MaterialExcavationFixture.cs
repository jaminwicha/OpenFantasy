using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

/// <summary>Authored finite admission followed exclusively by genuine work and committed source-world steps.</summary>
internal sealed class MaterialExcavationFixture
{
    private readonly HashSet<EntityId> _occupied = [];
    private readonly ConstructionUpgradeFixture _actions = new();
    private ulong _next = 100_000;

    internal static EntityId Id(ulong value) => new(0x6d61746578636176, value);
    internal static readonly EntityId SecondSiteId = Id(1);
    internal static readonly EntityId SecondSourceId = Id(2);
    internal static readonly CellCoordinate SecondCell = new(1, 1, 0);

    internal MaterialExcavationFixture(ulong requiredWork = 3, bool secondSite = false,
        bool occupiedAbove = false, bool npc = false, bool secondActorOwnsSite = false, int legacyWork = 0,
        bool upperDonorActor = false, EntityId? siteId = null, ulong? primaryExtraCapacity = null,
        bool gatherBehindTarget = false)
    {
        // This variant changes only fresh authored admission. The original fixture and its
        // default archives remain untouched, and no completed work or earned clock is authored.
        var original = new ExcavationWorldFixture(npc, occupiedAbove);
        var basis = original.Basis;
        Actor = original.Player;
        OtherActor = basis.Residents.Single(resident => resident.ActorId != Actor).ActorId;
        var physical = new MaterialDefinition(new PackageId("openfantasy.excavation-continuation-tests"),
            Id(3), "finite_substrate", new(312_500_000), new(125_000_000), new(4));
        Material = new(original.Site.Material.Item, physical);
        var first = new ExcavationSite(siteId ?? original.Site.SiteId, original.Site.Cell, original.Site.TerrainSourceId,
            original.Site.CountedSourceId, Actor, Material, original.Site.RequiredTool, requiredWork, 0, original.Site.Stance);
        List<ExcavationSite> sites = [first];
        if (secondSite)
        {
            var second = basis.Space.Regions.Single(region => region.Bounds.Contains(SecondCell)).GetCell(SecondCell);
            Assert.Equal(first.Material.Item, second.Material);
            EntityId owner = secondActorOwnsSite ? OtherActor : Actor;
            var tool = MiningTool(basis, owner);
            sites.Add(new(SecondSiteId, SecondCell, second.SourceId!.Value, SecondSourceId, owner,
                Material, tool.Definition, requiredWork, 0, new(384, 128, 0)));
        }
        var sources = basis.Inventory.Sources.Where(source => source.SourceId != first.CountedSourceId)
            .Concat(sites.Select(site => new AdmittedCountedSource(site.CountedSourceId, site.Material.Item, site.Quantity))).ToArray();
        var custody = basis.Inventory.Custody;
        if (primaryExtraCapacity is { } extraCapacity)
        {
            EntityId primary = basis.GetResident(Actor).InventoryCustodianId;
            custody = new(custody.WorldId, custody.BranchId, custody.Revision,
                custody.Storages.Select(storage => storage.CustodianId == primary
                    ? storage with { UnitCapacity = checked(custody.GetStoredQuantity(primary) + extraCapacity) } : storage), custody.Lots);
        }
        var inventory = new CountedAuthorityState(custody,
            sources.Aggregate(0UL, (total, source) => checked(total + source.UnitCapacity)),
            basis.Inventory.Actors, basis.Inventory.Controls,
            basis.Inventory.Policies.Select(policy => new CustodianAccessPolicy(policy.CustodianId,
                policy.OwnerActorId, CustodyDepositRule.AnyControlledActor)), sources, basis.Inventory.ReservationIds);
        var production = new ProductionSnapshot(basis.WorldId, basis.BranchId, 0,
            basis.Production!.Recipes.Concat([Recipe(20, 4), Recipe(30, 2), Recipe(40, 1)]), [], []);
        var gathering = basis.Gathering!;
        var node = gathering.Nodes.First();
        GatherNodeId = node.NodeId;
        // The ordinary farm source, tool and unissued cursor are retained; only its fresh anchor
        // is placed beside the admitted mining stance so both actions are actually reachable.
        gathering = new(gathering.WorldId, gathering.BranchId, 0,
            gathering.Nodes.Select(value => value.NodeId == node.NodeId
                ? new GatherNodeState(value.NodeId, value.Definition, value.SourceId, value.UnitCapacity,
                    gatherBehindTarget ? new(128, 513, 0) : new(-128, 128, 0),
                    value.Revision, value.NextUnissuedUnit, value.RemainingSwings, value.DepletedAtStep)
                : value), gathering.Tools, gathering.Actors);
        var space = upperDonorActor ? basis.Space.WithActors(basis.Space.Actors.Select(actor => actor.ActorId == OtherActor
            ? new ActorPresence(actor.Binding, actor.Condition, actor.Scope, new(-128, 384, 256), actor.Revision, actor.HomePlaceId)
            : actor)) : basis.Space;
        if (gatherBehindTarget)
        {
            var anchorCell = new CellCoordinate(0, 2, 0);
            space = new(space.Scope, space.Regions.Select(region => region.Bounds.Contains(anchorCell)
                ? region.Excavate(region.Revision, [anchorCell]) : region), space.Actors,
                space.Structures, space.GroundSupport, space.StructureSupport);
        }
        var authored = new WorldSnapshot(inventory, space, basis.Residents, basis.AdmissionVersion,
            basis.Calendar, 0, 0, gathering: gathering, production: production, progression: basis.Progression,
            quests: basis.Quests, construction: basis.Construction);
        Genesis = WorldExcavationAdmission.CreateGenesis(authored, new(sites));
        SourceWorld = Genesis;
        for (int index = 0; index < legacyWork; index++)
        {
            Advance(40);
            var site = SourceWorld.Excavation!.Sites.Single(value => value.SiteId == first.SiteId);
            var tool = MiningTool(SourceWorld, Actor);
            var command = Action(SourceWorld, Actor, NextId(), new ExcavateIntent(site.SiteId, site.Revision,
                SourceWorld.Space.Regions.Single(region => region.Bounds.Contains(site.Cell)).Revision,
                tool.LotId, tool.Revision,
                SourceWorld.Inventory.Custody.GetStorage(SourceWorld.GetResident(Actor).InventoryCustodianId).Revision));
            var decision = new WorldStepProcessor().Decide(SourceWorld, _actions.Batch(SourceWorld, [command], []), Occupied);
            Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
            SourceWorld = decision.Candidate;
            _occupied.UnionWith(decision.Identities.Select(identity => identity.Identity));
        }
        InitialWorld = SourceWorld;
        State = MaterialExcavationState.Capture(InitialWorld);
    }

    internal WorldSnapshot Genesis { get; }
    internal WorldSnapshot InitialWorld { get; }
    internal WorldSnapshot SourceWorld { get; private set; }
    internal MaterialExcavationState State { get; private set; }
    internal CountedAuthorityState Inventory => State.Inventory;
    internal ProductionSnapshot Production => State.Production;
    internal GatheringSnapshot Gathering => State.Gathering;
    internal LocalSpatialSnapshot Space => State.Space;
    internal ExcavationSnapshot Excavation => State.Excavation;
    internal EntityId Actor { get; }
    internal EntityId OtherActor { get; }
    internal EntityId GatherNodeId { get; }
    internal ItemPhysicalBinding Material { get; }
    internal EntityId SiteId => Genesis.Excavation!.Sites.Single(site => site.SiteId != SecondSiteId).SiteId;
    internal EntityId Bag => SourceWorld.GetResident(Actor).InventoryCustodianId;
    internal EntityId OtherBag => SourceWorld.GetResident(OtherActor).InventoryCustodianId;
    internal List<MaterialExcavationState> Closed { get; } = [];
    internal bool Occupied(EntityId identity) => _occupied.Contains(identity);
    internal EntityId NextId() => Id(_next++);

    internal void Advance(int count = 40, IEnumerable<WorldMovementInput>? movement = null)
    {
        for (int index = 0; index < count; index++)
        {
            var decision = new WorldStepProcessor().Decide(SourceWorld,
                _actions.Batch(SourceWorld, [], movement ?? []), Occupied);
            SourceWorld = decision.Candidate;
            _occupied.UnionWith(decision.Identities.Select(identity => identity.Identity));
        }
    }

    internal void Cut()
    {
        var before = State;
        Closed.Add(before);
        State = State.PlanContinue(TestContext.Current.CancellationToken).ApplyTo(State);
        Assert.Equal(before.Inventory.ContentHash, Inventory.ContentHash);
        Assert.Equal(before.Production.Revision, Production.Revision);
        Assert.Equal(before.Production.TotalMaterialOperationCount, Production.TotalMaterialOperationCount);
        Assert.Equal(before.Gathering.ContentHash, Gathering.ContentHash);
        Assert.Equal(before.Excavation.ContentHash, Excavation.ContentHash);
        Assert.Equal(before.Space.Regions.Select(region => region.ComputeContentHash()),
            Space.Regions.Select(region => region.ComputeContentHash()));
        Assert.Empty(Production.MaterialHistory!.Operations);
    }

    internal WorldActionCommand Command(EntityId? siteId = null, EntityId? commandId = null)
    {
        var site = Excavation.Sites.Single(value => value.SiteId == (siteId ?? SiteId));
        var tool = Inventory.Custody.Lots.First(lot => lot.CustodianId == SourceWorld.GetResident(site.OwnerActorId).InventoryCustodianId
            && lot.Definition == site.RequiredTool && lot.ReservedBy is null);
        return Action(SourceWorld, site.OwnerActorId, commandId ?? NextId(), new ExcavateIntent(site.SiteId, site.Revision,
            Space.Regions.Single(region => region.Bounds.Contains(site.Cell)).Revision, tool.LotId, tool.Revision,
            Inventory.Custody.GetStorage(SourceWorld.GetResident(site.OwnerActorId).InventoryCustodianId).Revision));
    }

    internal WorldActionCommand GatherCommand(EntityId? commandId = null)
    {
        var node = Gathering.GetNode(GatherNodeId);
        var tool = Inventory.Custody.Lots.First(lot => lot.CustodianId == Bag
            && Gathering.Tools.Any(definition => definition.Definition == lot.Definition
                && definition.Kind == node.Definition.RequiredToolKind));
        return Action(SourceWorld, Actor, commandId ?? NextId(), new GatherIntent(node.NodeId, node.Revision, tool.LotId, tool.Revision));
    }

    internal MaterialExcavationPlan Plan(WorldActionCommand command, WorldSnapshot? sourceWorld = null,
        Func<EntityId, bool>? occupied = null, bool advancing = true, bool held = false) =>
        new MaterialExcavationPlanner().Plan(sourceWorld ?? SourceWorld, State, command, advancing, held, occupied ?? Occupied);

    internal MaterialExcavationPlan PlanGather(WorldActionCommand command, WorldSnapshot? sourceWorld = null,
        Func<EntityId, bool>? occupied = null) =>
        new MaterialExcavationPlanner().PlanGather(sourceWorld ?? SourceWorld, State, command, occupied ?? Occupied);

    internal MaterialExcavationPlan Work(EntityId? siteId = null)
    {
        Advance();
        var plan = Plan(Command(siteId));
        Accept(plan);
        return plan;
    }

    internal MaterialExcavationPlan Gather()
    {
        Advance();
        var plan = PlanGather(GatherCommand());
        Accept(plan);
        return plan;
    }

    internal void Accept(MaterialExcavationPlan plan)
    {
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        State = plan.ApplyTo(State);
        Remember(plan.CreatedIds);
        Production.ValidateForAuthority(Inventory);
    }

    internal void Reconcile(CountedAuthorityState inventory, ProductionSnapshot production)
    {
        State = State.PlanReconcile(inventory, production, TestContext.Current.CancellationToken).ApplyTo(State);
        Remember([]);
        Production.ValidateForAuthority(Inventory);
    }

    internal EntityId Craft(IngredientLotKind kind, EntityId lotId, ulong quantity, EntityId? commandId = null)
    {
        var (store, _, revision) = Item(kind, lotId);
        var recipe = Production.Recipes.Single(value => value.DefinitionId == Id(quantity switch
        {
            4 => 21UL,
            2 => 31UL,
            1 => 41UL,
            _ => throw new ArgumentOutOfRangeException(nameof(quantity))
        }));
        var cause = commandId ?? NextId();
        var actor = Inventory.Policies.Single(policy => policy.CustodianId == store).OwnerActorId;
        var plan = new InstantCraftPlanner().Plan(Inventory, Production,
            ConstructionUpgradeFixture.Delivery(SourceWorld, actor), actor, cause,
            new(recipe.DefinitionId, recipe.Version, Production.Revision, store,
                Inventory.Custody.GetStorage(store).Revision, [new(kind, lotId, revision, quantity)]), Occupied);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Reconcile(plan.CandidateInventory, plan.CandidateProduction);
        Remember([cause]);
        return EntityId.DeriveSpawned(cause, 1);
    }

    internal ItemConsumptionPlan PlanConsume(IngredientLotKind kind, EntityId lotId, ulong quantity = 1, EntityId? commandId = null)
    {
        var (store, item, revision) = Item(kind, lotId);
        var actor = Inventory.Policies.Single(policy => policy.CustodianId == store).OwnerActorId;
        var cause = commandId ?? NextId();
        return new ItemConsumptionPlanner().Plan(Inventory, Production,
            ConstructionUpgradeFixture.Delivery(SourceWorld, actor), actor, cause,
            new(store, Inventory.Custody.GetStorage(store).Revision, Production.Revision, actor, item,
                new(kind, lotId, revision, quantity)), Occupied);
    }

    internal EntityId Transfer(EntityId lotId, EntityId destination, ulong quantity, EntityId? commandId = null)
    {
        var lot = Inventory.Custody.GetLot(lotId);
        var actor = Inventory.Policies.Single(policy => policy.CustodianId == lot.CustodianId).OwnerActorId;
        var delivery = ConstructionUpgradeFixture.Delivery(SourceWorld, actor);
        var cause = commandId ?? NextId();
        var command = new CommandEnvelope(cause, delivery.PrincipalId, delivery.SessionId, actor,
            Inventory.WorldId, Inventory.BranchId,
            [new(RevisionTargetKind.Branch, Inventory.BranchId, Inventory.Custody.Revision),
                new(RevisionTargetKind.Entity, lot.LotId, lot.Revision),
                new(RevisionTargetKind.Entity, lot.CustodianId, Inventory.Custody.GetStorage(lot.CustodianId).Revision),
                new(RevisionTargetKind.Entity, destination, Inventory.Custody.GetStorage(destination).Revision)],
            new(lotId, lot.CustodianId, destination, quantity));
        var plan = new MaterialTransferPlanner().Plan(Inventory, Production, command, delivery, Occupied);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Reconcile(plan.CandidateInventory, plan.CandidateProduction);
        Remember([cause]);
        return quantity == lot.Quantity ? lotId : EntityId.DeriveSpawned(cause, 0);
    }

    internal CountedSourceLot Extracted(EntityId? siteId = null) => Inventory.Custody.Lots.Single(lot =>
        lot.Source.SourceId == Excavation.Sites.Single(site => site.SiteId == (siteId ?? SiteId)).CountedSourceId);

    private (EntityId Store, CountedItemDefinition Item, ulong Revision) Item(IngredientLotKind kind, EntityId lotId)
    {
        if (kind == IngredientLotKind.Source)
        {
            var lot = Inventory.Custody.GetLot(lotId);
            return (lot.CustodianId, lot.Definition, lot.Revision);
        }
        var transformed = Production.Lots.Single(lot => lot.LotId == lotId);
        return (transformed.CustodianId, transformed.Definition, transformed.Revision);
    }

    private void Remember(IEnumerable<EntityId> created)
    {
        _occupied.UnionWith(created);
        _occupied.UnionWith(Inventory.Custody.Lots.Select(lot => lot.LotId));
        _occupied.UnionWith(Production.Lots.Select(lot => lot.LotId));
        _occupied.UnionWith(Production.Batches.Select(batch => batch.BatchId));
        _occupied.UnionWith(Production.ConsumedItems.Select(claim => claim.ConsumptionId));
        _occupied.UnionWith(Production.MaterialHistory?.Operations.Select(operation => operation.CommandId) ?? []);
    }

    private CraftRecipeDefinition Recipe(ulong number, ulong quantity)
    {
        var physical = new MaterialRecipeDefinition(Material.Physical.Package, Id(number), "finite_substrate_" + quantity,
            new(1), null, new MaterialCatalog([Material.Physical]),
            [new(RecipeMaterialRole.Input, Material.Physical.Pin(), quantity),
                new(RecipeMaterialRole.Output, Material.Physical.Pin(), quantity)]);
        return new(Id(number + 1), physical, [Material], [new(Material.Item, 0, Material.Physical.MassPerUnit)]);
    }

    private static CountedSourceLot MiningTool(WorldSnapshot world, EntityId actor) => world.Inventory.Custody.Lots.First(lot =>
        lot.CustodianId == world.GetResident(actor).InventoryCustodianId && lot.ReservedBy is null
        && (world.Gathering!.Tools.Any(tool => tool.Definition == lot.Definition)
            || world.Construction!.Definitions.Any(definition => definition.RequiredTool.Item == lot.Definition)));

    private static WorldActionCommand Action(WorldSnapshot world, EntityId actor, EntityId cause, WorldActionIntent intent)
    {
        var delivery = ConstructionUpgradeFixture.Delivery(world, actor);
        return new(cause, delivery.PrincipalId, delivery.SessionId, actor, world.WorldId, world.BranchId,
            world.Space.GetActor(actor).Revision, intent);
    }
}
