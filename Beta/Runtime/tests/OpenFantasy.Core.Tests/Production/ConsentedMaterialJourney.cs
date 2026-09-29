using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Production.InstantCraftFixture;

namespace OpenFantasy.Core.Tests.Production;

/// <summary>
/// Three controlled owners with OwnerOnly storages and actual crafted stock in a consented ordered
/// segment. The external identity set models the permanent registry a later host must retain.
/// </summary>
internal sealed class ConsentedMaterialJourney
{
    internal static readonly EntityId Giver = Id(10);
    internal static readonly EntityId Receiver = Id(11);
    internal static readonly EntityId Onward = Id(12);
    internal static readonly EntityId GiverStorage = Id(60);
    internal static readonly EntityId ReceiverStorage = Id(61);
    internal static readonly EntityId OnwardStorage = Id(62);
    internal static readonly EntityId BerryLot = Id(70);
    internal static readonly EntityId WoodLot = Id(72);
    internal static readonly EntityId Reservation = Id(9100);
    private readonly HashSet<EntityId> _occupied = [];

    private ConsentedMaterialJourney(CountedAuthorityState inventory, ProductionSnapshot production)
    {
        Inventory = inventory;
        Production = production;
    }

    internal CountedAuthorityState Inventory { get; private set; }
    internal ProductionSnapshot Production { get; private set; }
    internal List<ClosedConsentedSegment> Closed { get; } = [];
    internal bool IsOccupied(EntityId identity) => _occupied.Contains(identity);

    internal static readonly CountedItemDefinition Filler =
        new(Id(5005), OpenFantasy.Core.Content.ContentHash.Compute("craft-test-filler"u8));

    /// <param name="fillerLots">Single-unit raw lots of one finite admitted source in the onward storage.</param>
    internal static CountedAuthorityState FreshInventory(ulong receiverCapacity = 64, int fillerLots = 0)
    {
        ulong fillers = (ulong)fillerLots;
        var lots = new List<CountedSourceLot>
        {
            new(BerryLot, GiverStorage, Berries, new SourceInterval(Id(71), 0, 6), 0),
            new(WoodLot, GiverStorage, Wood, new SourceInterval(Id(73), 0, 4), 0)
        };
        for (ulong unit = 0; unit < fillers; unit++)
            lots.Add(new(Id(100_000 + unit), OnwardStorage, Filler, new SourceInterval(Id(75), unit, unit + 1), 0));
        var custody = new CustodySnapshot(Id(1), Id(2), 0,
            [new(GiverStorage, 64, 0), new(ReceiverStorage, receiverCapacity, 0), new(OnwardStorage, 64 + fillers, 0)],
            lots);
        List<AdmittedCountedSource> sources = [new(Id(71), Berries, 6), new(Id(73), Wood, 4)];
        if (fillers != 0) sources.Add(new(Id(75), Filler, fillers));
        return new CountedAuthorityState(custody, 10 + fillers, [Giver, Receiver, Onward],
            [new(Id(20), Id(30), Giver), new(Id(21), Id(31), Receiver), new(Id(22), Id(32), Onward)],
            [new(GiverStorage, Giver, CustodyDepositRule.OwnerOnly),
                new(ReceiverStorage, Receiver, CustodyDepositRule.OwnerOnly),
                new(OnwardStorage, Onward, CustodyDepositRule.OwnerOnly)],
            sources, [Reservation]);
    }

    internal static ProductionSnapshot FreshProduction()
    {
        CraftRecipeDefinition recipe = Recipe();
        return new ProductionSnapshot(Id(1), Id(2), 0, [recipe, InstantCraftFixture.Repack(recipe)], [], [], []);
    }

    internal static ConsentedMaterialJourney Create(ulong receiverCapacity = 64, int fillerLots = 0)
    {
        CountedAuthorityState inventory = FreshInventory(receiverCapacity, fillerLots);
        return new(inventory, FreshProduction().ContinueConsentedMaterial(inventory));
    }

    internal int LiveLotCount => Inventory.Custody.Lots.Count + Production.Lots.Count;

    /// <summary>
    /// Actual crafted rations whose protection or reservation is explicit admission before the
    /// consented segment starts, as an installed construction output reserves its produced lot.
    /// </summary>
    internal static (ConsentedMaterialJourney Journey, EntityId Rations) CreateWithAdmittedStock(
        LotProtection protection, EntityId? reservedBy)
    {
        CountedAuthorityState inventory = FreshInventory();
        ProductionSnapshot plain = FreshProduction();
        CraftPlan crafted = new InstantCraftPlanner().Plan(inventory, plain, new(Id(20), Id(30)), Giver, Id(29000),
            RationsRequest(inventory, plain));
        Assert.Equal(WorldActionRejection.None, crafted.Rejection);
        ProductionSnapshot earned = crafted.CandidateProduction;
        TransformedLot rations = earned.Lots.Single(lot => lot.Definition == Rations);
        var admitted = new ProductionSnapshot(earned.WorldId, earned.BranchId, earned.Revision, earned.Recipes,
            earned.Batches, earned.Lots.Select(lot => lot.LotId != rations.LotId ? lot
                : new TransformedLot(lot.LotId, lot.CustodianId, lot.Definition, lot.Output, lot.Revision,
                    protection, reservedBy)), earned.ConsumedItems);
        admitted.ValidateForAuthority(crafted.CandidateInventory);
        var journey = new ConsentedMaterialJourney(crafted.CandidateInventory,
            admitted.ContinueConsentedMaterial(crafted.CandidateInventory));
        foreach (EntityId identity in earned.Lots.Select(lot => lot.LotId)
            .Concat(earned.Batches.Select(batch => batch.BatchId)).Append(Id(29000)))
            journey._occupied.Add(identity);
        return (journey, rations.LotId);
    }

    internal TransformedLot Lot(EntityId lotId) => Production.Lots.Single(lot => lot.LotId == lotId);

    internal EntityId StorageOf(EntityId actor) =>
        Inventory.Policies.Single(policy => policy.OwnerActorId == actor).CustodianId;

    internal TrustedCommandDelivery DeliveryFor(EntityId actor)
    {
        ActorControlBinding control = Inventory.Controls.Single(value => value.ActorId == actor);
        return new(control.PrincipalId, control.SessionId);
    }

    internal EntityId Craft(ulong command)
    {
        CraftPlan plan = new InstantCraftPlanner().Plan(Inventory, Production, DeliveryFor(Giver), Giver,
            Id(command), RationsRequest(Inventory, Production), IsOccupied);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Adopt(plan.CandidateInventory, plan.CandidateProduction, Id(command));
        EntityId batch = Production.Batches.Single(value => value.CommandId == Id(command)).BatchId;
        return Production.Lots.Single(lot => lot.Output.BatchId == batch && lot.Definition == Rations).LotId;
    }

    internal EntityId Repack(EntityId actor, EntityId lotId, ulong command)
    {
        TransformedLot lot = Lot(lotId);
        CraftRecipeDefinition recipe = Production.GetRecipe(Id(5204));
        var request = new CraftRequest(recipe.DefinitionId, recipe.Version, Production.Revision, lot.CustodianId,
            Inventory.Custody.GetStorage(lot.CustodianId).Revision,
            [new(IngredientLotKind.Transformed, lot.LotId, lot.Revision, 1)]);
        CraftPlan plan = new InstantCraftPlanner().Plan(Inventory, Production, DeliveryFor(actor), actor,
            Id(command), request, IsOccupied);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Adopt(plan.CandidateInventory, plan.CandidateProduction, Id(command));
        EntityId batch = Production.Batches.Single(value => value.CommandId == Id(command)).BatchId;
        return Production.Lots.Single(value => value.Output.BatchId == batch).LotId;
    }

    internal void Consume(EntityId actor, IngredientLotKind kind, EntityId lotId, ulong command)
    {
        CountedSourceLot? raw = kind == IngredientLotKind.Source ? Inventory.Custody.GetLot(lotId) : null;
        TransformedLot? derived = kind == IngredientLotKind.Transformed ? Lot(lotId) : null;
        EntityId custodian = raw?.CustodianId ?? derived!.CustodianId;
        var request = new ConsumeItemRequest(custodian, Inventory.Custody.GetStorage(custodian).Revision,
            Production.Revision, actor, raw?.Definition ?? derived!.Definition,
            new(kind, lotId, raw?.Revision ?? derived!.Revision, 1));
        ItemConsumptionPlan plan = new ItemConsumptionPlanner().Plan(Inventory, Production, DeliveryFor(actor),
            actor, Id(command), request, IsOccupied);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Adopt(plan.CandidateInventory, plan.CandidateProduction, Id(command));
    }

    /// <summary>Terms a participant would capture by observing the current owners, with explicit overrides.</summary>
    internal ConsentedMaterialTerms Terms(ConsentedMaterialLotKind kind, EntityId lotId, EntityId sender,
        EntityId recipient, ulong quantity, CountedItemDefinition? definition = null, EntityId? source = null,
        EntityId? destination = null, ulong? lotRevision = null, EntityId? world = null, EntityId? branch = null)
    {
        EntityId from = source ?? StorageOf(sender);
        EntityId to = destination ?? StorageOf(recipient);
        TransformedLot? produced = Production.Lots.FirstOrDefault(value => value.LotId == lotId);
        CountedSourceLot? raw = Inventory.Custody.Lots.FirstOrDefault(value => value.LotId == lotId);
        return new ConsentedMaterialTerms(world ?? Inventory.WorldId, branch ?? Inventory.BranchId, sender, recipient,
            kind, lotId, definition ?? produced?.Definition ?? raw?.Definition ?? Rations, from, to, quantity,
            lotRevision ?? produced?.Revision ?? raw?.Revision ?? 0UL);
    }

    /// <summary>Both approvals as actually delivered, with explicit forged claims or deliveries.</summary>
    internal ConsentedMaterialApproval Approval(ConsentedMaterialTerms terms, ulong command, ulong acceptance,
        TrustedCommandDelivery? senderDelivery = null, TrustedCommandDelivery? recipientDelivery = null,
        TrustedCommandDelivery? senderClaims = null, TrustedCommandDelivery? recipientClaims = null) =>
        Approval(terms, Id(command), Id(acceptance), senderDelivery, recipientDelivery, senderClaims, recipientClaims);

    internal ConsentedMaterialApproval Approval(ConsentedMaterialTerms terms, EntityId command, EntityId acceptance,
        TrustedCommandDelivery? senderDelivery = null, TrustedCommandDelivery? recipientDelivery = null,
        TrustedCommandDelivery? senderClaims = null, TrustedCommandDelivery? recipientClaims = null)
    {
        TrustedCommandDelivery sender = DeliveryFor(terms.SenderActorId);
        TrustedCommandDelivery recipient = DeliveryFor(terms.RecipientActorId);
        TrustedCommandDelivery claimedSender = senderClaims ?? sender;
        TrustedCommandDelivery claimedRecipient = recipientClaims ?? recipient;
        var submitted = new ConsentedMaterialCommand(command, claimedSender.PrincipalId,
            claimedSender.SessionId, terms);
        ConsentedMaterialAcceptance accepted = ConsentedMaterialAcceptance.Approving(submitted, acceptance,
            claimedRecipient.PrincipalId, claimedRecipient.SessionId);
        return new(submitted, senderDelivery ?? sender, accepted, recipientDelivery ?? recipient);
    }

    internal ConsentedMaterialTransferPlan Plan(ConsentedMaterialApproval approval,
        Func<EntityId, bool>? occupied = null) =>
        new ConsentedMaterialTransferPlanner().Plan(Inventory, Production, approval, occupied ?? IsOccupied);

    internal EntityId Gift(ConsentedMaterialLotKind kind, EntityId lotId, EntityId sender, EntityId recipient,
        ulong quantity, ulong command, ulong acceptance) =>
        Apply(Approval(Terms(kind, lotId, sender, recipient, quantity), command, acceptance));

    /// <summary>Decides an approval captured earlier against the current owners and commits its candidate.</summary>
    internal EntityId Apply(ConsentedMaterialApproval approval)
    {
        ConsentedMaterialTransferPlan plan = Plan(approval);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Assert.True(plan.IsApplied);
        var applied = plan.ApplyTo(Inventory, Production);
        Assert.True(_occupied.Add(approval.Acceptance.AcceptanceId));
        Adopt(applied.Inventory, applied.Production, approval.Command.CommandId);
        return plan.MovedLotId!.Value;
    }

    internal void Cut()
    {
        Closed.Add(new(Inventory, Production, ProductionCodecV10.Encode(Production)));
        ulong revision = Production.Revision;
        Production = Production.Continue(Inventory);
        Assert.True(Production.Continuation!.SupportsConsentedTransfer);
        Assert.Equal(revision, Production.Revision);
        Assert.Empty(Production.MaterialHistory!.Operations);
        Reopen();
    }

    internal void Reopen()
    {
        byte[] bytes = ProductionCodecV10.Encode(Production);
        Inventory = AuthorityEncodingV1.Decode(AuthorityEncodingV1.Encode(Inventory));
        Production = ProductionCodecV10.Decode(bytes, Production.Continuation!, []);
        Production.ValidateForAuthority(Inventory);
        Assert.Equal(bytes, ProductionCodecV10.Encode(Production));
    }

    private static CraftRequest RationsRequest(CountedAuthorityState inventory, ProductionSnapshot production)
    {
        CraftRecipeDefinition recipe = production.GetRecipe(Id(5202));
        return new CraftRequest(recipe.DefinitionId, recipe.Version, production.Revision, GiverStorage,
            inventory.Custody.GetStorage(GiverStorage).Revision,
            [new(IngredientLotKind.Source, BerryLot, inventory.Custody.GetLot(BerryLot).Revision, 2),
                new(IngredientLotKind.Source, WoodLot, inventory.Custody.GetLot(WoodLot).Revision, 1)]);
    }

    private void Adopt(CountedAuthorityState inventory, ProductionSnapshot production, EntityId command)
    {
        var priorIds = Identities(Inventory, Production).ToHashSet();
        foreach (EntityId created in Identities(inventory, production).Where(id => !priorIds.Contains(id)))
            Assert.True(_occupied.Add(created));
        Assert.True(_occupied.Add(command));
        Inventory = inventory;
        Production = production;
        production.ValidateForAuthority(inventory);
    }

    private static IEnumerable<EntityId> Identities(CountedAuthorityState inventory, ProductionSnapshot production) =>
        inventory.Custody.Lots.Select(lot => lot.LotId).Concat(production.Lots.Select(lot => lot.LotId))
            .Concat(production.Batches.Select(batch => batch.BatchId))
            .Concat(production.ConsumedItems.Select(claim => claim.ConsumptionId));

    internal sealed record ClosedConsentedSegment(CountedAuthorityState Inventory, ProductionSnapshot Production,
        byte[] Bytes);
}
