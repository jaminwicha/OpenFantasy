using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Tests.Gathering;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;

namespace OpenFantasy.Core.Tests.Production;

/// <summary>Actual standalone material plans beside a real, independently advancing source-world reference.</summary>
internal sealed class MaterialCustodyContinuationFixture
{
    internal static readonly EntityId DeliveryReservation = Id(9000);
    internal static readonly EntityId ProtectedReservation = Id(9001);
    private readonly HashSet<EntityId> _occupied = [];

    private MaterialCustodyContinuationFixture(WorldSnapshot sourceWorld, ProductionSnapshot production)
    {
        SourceWorld = sourceWorld;
        Inventory = sourceWorld.Inventory;
        Gathering = sourceWorld.Gathering!;
        Production = production.ContinueMaterial(Inventory, Gathering);
    }

    internal WorldSnapshot SourceWorld { get; private set; }
    internal CountedAuthorityState Inventory { get; private set; }
    internal ProductionSnapshot Production { get; private set; }
    internal GatheringSnapshot Gathering { get; private set; }
    internal List<ClosedMaterialSegment> Closed { get; } = [];
    internal bool IsOccupied(EntityId identity) => _occupied.Contains(identity);

    internal static MaterialCustodyContinuationFixture Create(ulong sourceCapacity = 20, bool reserveProtectedStock = true,
        ulong storageCapacity = 64)
    {
        var initial = GatheringFixture.Create(power: 3, sourceCapacity: sourceCapacity, storageCapacity: storageCapacity);
        var prior = initial.Inventory;
        var original = prior.Custody.GetLot(Id(70));
        var protectedLot = new CountedSourceLot(original.LotId, original.CustodianId, original.Definition,
            original.Source, original.Revision, reserveProtectedStock ? LotProtection.Favorite | LotProtection.UserReserved
                : LotProtection.Favorite, reserveProtectedStock ? ProtectedReservation : null);
        // These permissions, reservation identities and protected stock are explicit fresh
        // admission, before any material operation. No earned history is fabricated.
        var inventory = new CountedAuthorityState(new(prior.WorldId, prior.BranchId, 0, prior.Custody.Storages,
            prior.Custody.Lots.Select(lot => lot.LotId == original.LotId ? protectedLot : lot)),
            prior.AdmittedUnitCapacity, prior.Actors, prior.Controls,
            prior.Policies.Select(policy => new CustodianAccessPolicy(policy.CustodianId, policy.OwnerActorId,
                CustodyDepositRule.AnyControlledActor)), prior.Sources, [DeliveryReservation, ProtectedReservation]);
        var world = new WorldSnapshot(inventory, initial.Space, initial.Residents, initial.AdmissionVersion,
            initial.Calendar, initial.Revision, initial.AdvancingSteps, gathering: initial.Gathering);
        var ledger = ProductionWorldFixture.Ledger(world);
        var production = new ProductionSnapshot(world.WorldId, world.BranchId, 0, ledger.Recipes, [], [], []);
        return new(world, production);
    }

    internal WorldActionCommand GatherCommand(ulong command)
    {
        var node = Gathering.GetNode(GatheringFixture.Node);
        var tool = Inventory.Custody.GetLot(GatheringFixture.ToolLot);
        return StepFixture.Action(SourceWorld, command,
            new GatherIntent(node.NodeId, node.Revision, tool.LotId, tool.Revision));
    }

    internal MaterialOperationPlan PlanGather(WorldActionCommand command, Func<EntityId, bool>? occupied = null) =>
        new MaterialGatherPlanner().Plan(SourceWorld, Inventory, Production, Gathering, command, occupied ?? IsOccupied);

    internal EntityId Gather(ulong command)
    {
        Accept(PlanGather(GatherCommand(command)), Id(command));
        return EntityId.DeriveSpawned(Id(command), 0);
    }

    internal CommandEnvelope TransferCommand(EntityId lotId, EntityId destination, ulong quantity, ulong command)
    {
        var lot = Inventory.Custody.GetLot(lotId);
        var policy = Inventory.Policies.Single(value => value.CustodianId == lot.CustodianId);
        var control = Inventory.Controls.Single(value => value.ActorId == policy.OwnerActorId);
        return new(Id(command), control.PrincipalId, control.SessionId, control.ActorId,
            Inventory.WorldId, Inventory.BranchId,
            [new(RevisionTargetKind.Branch, Inventory.BranchId, Inventory.Custody.Revision),
                new(RevisionTargetKind.Entity, lot.LotId, lot.Revision),
                new(RevisionTargetKind.Entity, lot.CustodianId, Inventory.Custody.GetStorage(lot.CustodianId).Revision),
                new(RevisionTargetKind.Entity, destination, Inventory.Custody.GetStorage(destination).Revision)],
            new(lot.LotId, lot.CustodianId, destination, quantity));
    }

    internal MaterialOperationPlan PlanTransfer(CommandEnvelope command, Func<EntityId, bool>? occupied = null) =>
        new MaterialTransferPlanner().Plan(Inventory, Production, command,
            new(command.PrincipalId, command.OriginSessionId), occupied ?? IsOccupied);

    internal EntityId Transfer(EntityId lotId, EntityId destination, ulong quantity, ulong command)
    {
        bool split = quantity < Inventory.Custody.GetLot(lotId).Quantity;
        Accept(PlanTransfer(TransferCommand(lotId, destination, quantity, command)), Id(command));
        return split ? EntityId.DeriveSpawned(Id(command), 0) : lotId;
    }

    internal SourceDeliveryRequest DeliveryRequest(EntityId lotId, ulong quantity)
    {
        var lot = Inventory.Custody.GetLot(lotId);
        return new(lot.CustodianId, Inventory.Custody.GetStorage(lot.CustodianId).Revision, Id(61),
            lot.Definition, quantity, DeliveryReservation, [new(lot.LotId, lot.Revision, quantity)]);
    }

    internal EntityId Deliver(EntityId lotId, ulong quantity, ulong command)
    {
        var request = DeliveryRequest(lotId, quantity);
        bool split = quantity < Inventory.Custody.GetLot(lotId).Quantity;
        Accept(new MaterialSourceDeliveryPlanner().Plan(Inventory, Production, DeliveryFor(Id(10)), Id(10),
            Id(command), request, IsOccupied), Id(command));
        return split ? EntityId.DeriveSpawned(Id(command), 0) : lotId;
    }

    internal EntityId Craft(EntityId rawLotId, ulong command)
    {
        var lot = Inventory.Custody.GetLot(rawLotId);
        EntityId actor = Inventory.Policies.Single(policy => policy.CustodianId == lot.CustodianId).OwnerActorId;
        var recipe = Assert.Single(Production.Recipes);
        var request = new CraftRequest(recipe.DefinitionId, recipe.Version, Production.Revision, lot.CustodianId,
            Inventory.Custody.GetStorage(lot.CustodianId).Revision,
            [new(IngredientLotKind.Source, lot.LotId, lot.Revision, 2)]);
        var plan = new InstantCraftPlanner().Plan(Inventory, Production, DeliveryFor(actor), actor,
            Id(command), request, IsOccupied);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Adopt(plan.CandidateInventory, plan.CandidateProduction, Id(command));
        return EntityId.DeriveSpawned(Id(command), 1);
    }

    internal ItemConsumptionPlan PlanConsume(IngredientLotKind kind, EntityId lotId, ulong command)
    {
        var raw = kind == IngredientLotKind.Source ? Inventory.Custody.GetLot(lotId) : null;
        var derived = kind == IngredientLotKind.Transformed ? Production.Lots.Single(lot => lot.LotId == lotId) : null;
        EntityId custodian = raw?.CustodianId ?? derived!.CustodianId;
        EntityId actor = Inventory.Policies.Single(value => value.CustodianId == custodian).OwnerActorId;
        var request = new ConsumeItemRequest(custodian, Inventory.Custody.GetStorage(custodian).Revision,
            Production.Revision, Id(10), raw?.Definition ?? derived!.Definition,
            new(kind, lotId, raw?.Revision ?? derived!.Revision, 1));
        return new ItemConsumptionPlanner().Plan(Inventory, Production, DeliveryFor(actor), actor,
            Id(command), request, IsOccupied);
    }

    internal void Consume(IngredientLotKind kind, EntityId lotId, ulong command)
    {
        var plan = PlanConsume(kind, lotId, command);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Adopt(plan.CandidateInventory, plan.CandidateProduction, Id(command));
    }

    internal void AdvanceSourceWorld(int count)
    {
        var processor = new WorldStepProcessor();
        for (int index = 0; index < count; index++)
        {
            var batch = new WorldStepBatch(Id(900000 + SourceWorld.AdvancingSteps), SourceWorld.WorldId,
                SourceWorld.BranchId, SourceWorld.Revision, SourceWorld.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [], [], 2);
            SourceWorld = processor.Decide(SourceWorld, batch).Candidate;
            // Independent old one-step rule supplies the actual owner. The material adapter
            // must match it, rather than replacing a caller's owner with inferred recovery.
            Gathering = Gathering.AdvanceStep(SourceWorld.ProcessIntervals);
        }
    }

    internal void Cut()
    {
        Closed.Add(new(Inventory, Production, ProductionCodecV4.Encode(Production)));
        ulong revision = Production.Revision;
        Production = Production.Continue(Inventory);
        Assert.Equal(revision, Production.Revision);
        Assert.Empty(Production.MaterialHistory!.Operations);
        Reopen();
    }

    internal void Reopen()
    {
        byte[] bytes = ProductionCodecV4.Encode(Production);
        var evidence = Production.MaterialHistory!.GatherEvidence.ToArray();
        Inventory = AuthorityEncodingV1.Decode(AuthorityEncodingV1.Encode(Inventory));
        Production = ProductionCodecV4.Decode(bytes, Production.Continuation!, evidence);
        Production.ValidateForAuthority(Inventory);
        Assert.Equal(bytes, ProductionCodecV4.Encode(Production));
    }

    internal TrustedCommandDelivery DeliveryFor(EntityId actor)
    {
        var control = Inventory.Controls.Single(value => value.ActorId == actor);
        return new(control.PrincipalId, control.SessionId);
    }

    internal static (MaterialCustodyContinuationFixture Journey, EntityId Product) FullMixedDestination()
    {
        var journey = Create(reserveProtectedStock: false, storageCapacity: 10);
        EntityId first = journey.Gather(78000);
        journey.Transfer(first, Id(61), 3, 78001);
        EntityId second = journey.Gather(78002);
        journey.Transfer(second, Id(61), 3, 78003);
        EntityId product = journey.Craft(first, 78004); // Actual recipient-owned crafting.
        journey.Transfer(Id(70), Id(61), 5, 78005);
        Assert.Equal(9UL, journey.Inventory.Custody.GetStoredQuantity(Id(61)));
        Assert.Equal(1UL, journey.Production.GetStoredQuantity(Id(61)));
        Assert.Equal(10UL, journey.Inventory.Custody.GetStorage(Id(61)).UnitCapacity);
        Assert.Equal(1UL, journey.Inventory.Custody.GetLot(Id(70)).Quantity);
        return (journey, product);
    }

    private void Accept(MaterialOperationPlan plan, EntityId command)
    {
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Adopt(plan.CandidateInventory, plan.CandidateProduction, command);
        if (plan.GatherEvidence is not null && plan.CandidateGathering is { } gathering) Gathering = gathering;
    }

    private void Adopt(CountedAuthorityState inventory, ProductionSnapshot production, EntityId command)
    {
        var priorIds = Inventory.Custody.Lots.Select(lot => lot.LotId)
            .Concat(Production.Lots.Select(lot => lot.LotId))
            .Concat(Production.Batches.Select(batch => batch.BatchId))
            .Concat(Production.ConsumedItems.Select(claim => claim.ConsumptionId)).ToHashSet();
        foreach (EntityId created in inventory.Custody.Lots.Select(lot => lot.LotId)
            .Concat(production.Lots.Select(lot => lot.LotId))
            .Concat(production.Batches.Select(batch => batch.BatchId))
            .Concat(production.ConsumedItems.Select(claim => claim.ConsumptionId)).Where(id => !priorIds.Contains(id)))
            Assert.True(_occupied.Add(created));
        Assert.True(_occupied.Add(command));
        Inventory = inventory;
        Production = production;
        production.ValidateForAuthority(inventory);
    }

    internal sealed record ClosedMaterialSegment(CountedAuthorityState Inventory, ProductionSnapshot Production, byte[] Bytes);
}
