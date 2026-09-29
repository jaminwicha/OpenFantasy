using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Production.ConsentedMaterialJourney;
using static OpenFantasy.Core.Tests.Production.InstantCraftFixture;
using Kind = OpenFantasy.Core.Authority.ConsentedMaterial.ConsentedMaterialLotKind;

namespace OpenFantasy.Core.Tests.Production;

public sealed class ConsentedMaterialTransferTests
{
    [Fact]
    public void Crafted_gifts_recipient_use_and_onward_gifts_conserve_exact_rights_across_three_cuts()
    {
        var journey = ConsentedMaterialJourney.Create();
        EntityId first = journey.Craft(30000);
        EntityId second = journey.Craft(30001);
        TransformedLot original = journey.Lot(first);
        ProductionBatch producer = journey.Production.Batches.Single(value => value.BatchId == original.Output.BatchId);

        EntityId child = journey.Gift(Kind.Produced, first, Giver, Receiver, 1, 30002, 30003);
        Assert.Equal(EntityId.DeriveSpawned(Id(30002), 0), child);
        TransformedLot moved = journey.Lot(child);
        TransformedLot remainder = journey.Lot(first);
        Assert.Equal((original.Output.BatchId, original.Output.OutputIndex, 0UL, 1UL, 0UL, ReceiverStorage),
            (moved.Output.BatchId, moved.Output.OutputIndex, moved.Output.Start, moved.Output.End, moved.Revision, moved.CustodianId));
        Assert.Equal((original.Output.BatchId, original.Output.OutputIndex, 1UL, 2UL, 1UL, GiverStorage),
            (remainder.Output.BatchId, remainder.Output.OutputIndex, remainder.Output.Start, remainder.Output.End,
                remainder.Revision, remainder.CustodianId));
        Assert.Equal(original.Definition, moved.Definition);

        EntityId whole = journey.Gift(Kind.Produced, second, Giver, Receiver, 2, 30004, 30005);
        Assert.Equal(second, whole);
        Assert.Equal((0UL, 2UL, 1UL, ReceiverStorage), (journey.Lot(second).Output.Start,
            journey.Lot(second).Output.End, journey.Lot(second).Revision, journey.Lot(second).CustodianId));
        var recorded = journey.Production.MaterialHistory!.Operations.OfType<MaterialConsentedTransferOperation>().ToArray();
        Assert.Equal(new[] { child, second }, recorded.Select(value => value.MovedLotId).ToArray());
        Assert.Equal(new[] { Id(30003), Id(30005) }, recorded.Select(value => value.AcceptanceId).ToArray());
        Assert.Equal(4UL, journey.Production.Revision);
        Assert.Equal(4UL, journey.Inventory.Custody.Revision);
        journey.Cut();

        journey.Consume(Receiver, IngredientLotKind.Transformed, second, 30006);
        EntityId repacked = journey.Repack(Receiver, child, 30007);
        ProductionBatch recraft = journey.Production.Batches.Single(value => value.CommandId == Id(30007));
        RetiredIngredient ancestry = Assert.Single(recraft.Ingredients);
        Assert.Equal((child, original.Output.BatchId, 0UL, 1UL),
            (ancestry.LotId, ancestry.Output!.BatchId, ancestry.Output.Start, ancestry.Output.End));
        Assert.Equal((Receiver, ReceiverStorage), (recraft.ActorId, recraft.CustodianId));
        Assert.DoesNotContain(journey.Production.Lots, lot => lot.LotId == child);
        journey.Cut();

        Assert.Equal(repacked, journey.Gift(Kind.Produced, repacked, Receiver, Onward, 1, 30008, 30009));
        Assert.Equal(second, journey.Gift(Kind.Produced, second, Receiver, Onward, 1, 30010, 30011));
        Assert.Equal((1UL, 2UL, 3UL, OnwardStorage), (journey.Lot(second).Output.Start,
            journey.Lot(second).Output.End, journey.Lot(second).Revision, journey.Lot(second).CustodianId));
        journey.Cut();
        journey.Reopen();

        // Five rations were produced: three remain live, one was consumed and one was recrafted.
        Assert.Equal(3UL, journey.Production.Lots.Where(lot => lot.Definition == Rations)
            .Aggregate(0UL, (total, lot) => total + lot.Quantity));
        Assert.Equal(3UL, journey.Production.TotalBatchCount);
        Assert.Equal(1UL, journey.Production.TotalConsumedItemCount);
        Assert.Equal(8UL, journey.Production.TotalMaterialOperationCount);
        Assert.Equal(8UL, journey.Production.Revision);
        Assert.Equal(8UL, journey.Inventory.Custody.Revision);
        Assert.Equal(3, journey.Closed.Count);
        Assert.Contains(journey.Production.Continuation!.Basis.BatchWitnesses, witness =>
            witness.BatchId == producer.BatchId && witness.ActorId == Giver && witness.CustodianId == GiverStorage);
        foreach (var closed in journey.Closed)
        {
            ProductionSnapshot reopened = ProductionCodecV10.Decode(closed.Bytes, closed.Production.Continuation!, []);
            reopened.ValidateForAuthority(closed.Inventory);
            Assert.Equal(closed.Bytes, ProductionCodecV10.Encode(reopened));
            Assert.Equal(closed.Production.ContentHash, reopened.ContentHash);
        }

        // Every prior command, acceptance and retired lot identity stays unusable in either role.
        foreach (EntityId prior in new[] { Id(30002), Id(30003), Id(30007), Id(30009), child, producer.BatchId })
        {
            AssertRefused(journey, journey.Approval(journey.Terms(Kind.Produced, first, Giver, Receiver, 1),
                prior, Id(31000)), WorldActionRejection.Unavailable);
            AssertRefused(journey, journey.Approval(journey.Terms(Kind.Produced, first, Giver, Receiver, 1),
                Id(31001), prior), WorldActionRejection.Unavailable);
        }
        Assert.Equal(first, journey.Gift(Kind.Produced, first, Giver, Receiver, 1, 31002, 31003));
    }

    [Fact]
    public void Raw_stock_uses_the_same_bilateral_contract_without_changing_deposit_policy()
    {
        var journey = ConsentedMaterialJourney.Create();
        CountedSourceLot original = journey.Inventory.Custody.GetLot(BerryLot);
        var policies = journey.Inventory.Policies.ToArray();
        EntityId child = journey.Gift(Kind.CountedSource, BerryLot, Giver, Receiver, 2, 32000, 32001);
        Assert.Equal(EntityId.DeriveSpawned(Id(32000), 0), child);
        CountedSourceLot moved = journey.Inventory.Custody.GetLot(child);
        Assert.Equal((new SourceInterval(original.Source.SourceId, 0, 2), 0UL, ReceiverStorage),
            (moved.Source, moved.Revision, moved.CustodianId));
        Assert.Equal(new SourceInterval(original.Source.SourceId, 2, 6), journey.Inventory.Custody.GetLot(BerryLot).Source);
        Assert.Equal(policies, journey.Inventory.Policies);
        Assert.All(journey.Inventory.Policies, policy => Assert.Equal(CustodyDepositRule.OwnerOnly, policy.DepositRule));
        journey.Cut();
        journey.Consume(Receiver, IngredientLotKind.Source, child, 32002);
        Assert.Equal(child, journey.Gift(Kind.CountedSource, child, Receiver, Onward, 1, 32003, 32004));
        journey.Cut();
        Assert.Equal(6UL, journey.Inventory.Custody.Lots.Where(lot => lot.Definition == Berries)
            .Aggregate(0UL, (total, lot) => total + lot.Quantity) + 1UL);

        // The recipient's earlier participation grants the giver no later deposit without a new acceptance.
        var reused = journey.Approval(journey.Terms(Kind.CountedSource, BerryLot, Giver, Receiver, 1), 32005, 32001);
        AssertRefused(journey, reused, WorldActionRejection.Unavailable);
    }

    [Fact]
    public void Forged_mismatched_and_stale_approvals_leave_both_owners_untouched()
    {
        var journey = ConsentedMaterialJourney.Create();
        EntityId lot = journey.Craft(33000);
        ConsentedMaterialTerms terms = journey.Terms(Kind.Produced, lot, Giver, Receiver, 1);
        TrustedCommandDelivery giver = journey.DeliveryFor(Giver);
        TrustedCommandDelivery receiver = journey.DeliveryFor(Receiver);
        TrustedCommandDelivery onward = journey.DeliveryFor(Onward);

        ConsentedMaterialApproval[] unauthorized =
        [
            journey.Approval(terms, 33001, 33002, senderDelivery: onward),
            journey.Approval(terms, 33001, 33002, senderClaims: onward),
            journey.Approval(terms, 33001, 33002, recipientDelivery: giver),
            journey.Approval(terms, 33001, 33002, recipientDelivery: onward),
            journey.Approval(terms, 33001, 33002, recipientClaims: giver),
            journey.Approval(journey.Terms(Kind.Produced, lot, Giver, Receiver, 1, destination: OnwardStorage), 33001, 33002),
            journey.Approval(journey.Terms(Kind.Produced, lot, Onward, Receiver, 1, source: GiverStorage), 33001, 33002),
            journey.Approval(journey.Terms(Kind.Produced, lot, Giver, Receiver, 1, world: Id(991)), 33001, 33002),
            journey.Approval(journey.Terms(Kind.Produced, lot, Giver, Receiver, 1, branch: Id(992)), 33001, 33002),
            Rebound(journey.Approval(terms, 33001, 33002), journey.Terms(Kind.Produced, lot, Giver, Receiver, 2)),
            Rebound(journey.Approval(terms, 33001, 33002), terms, commandId: Id(33003))
        ];
        Assert.All(unauthorized, approval => AssertRefused(journey, approval, WorldActionRejection.NotAuthorized));

        ConsentedMaterialTerms[] stale =
        [
            journey.Terms(Kind.Produced, lot, Giver, Receiver, 1, lotRevision: 9),
            journey.Terms(Kind.CountedSource, BerryLot, Giver, Receiver, 1, lotRevision: 9)
        ];
        Assert.All(stale, value => AssertRefused(journey, journey.Approval(value, 33001, 33002),
            WorldActionRejection.RevisionConflict));

        ConsentedMaterialTerms[] unavailable =
        [
            journey.Terms(Kind.CountedSource, lot, Giver, Receiver, 1),
            journey.Terms(Kind.Produced, BerryLot, Giver, Receiver, 1),
            journey.Terms(Kind.Produced, lot, Giver, Receiver, 1, definition: Offcuts),
            journey.Terms(Kind.Produced, lot, Giver, Receiver, 1,
                definition: new CountedItemDefinition(Rations.DefinitionId, Offcuts.Version)),
            journey.Terms(Kind.CountedSource, BerryLot, Giver, Receiver, 1,
                definition: new CountedItemDefinition(Berries.DefinitionId, Wood.Version)),
            journey.Terms(Kind.Produced, lot, Giver, Receiver, 3),
            journey.Terms(Kind.Produced, Id(993), Giver, Receiver, 1),
            journey.Terms(Kind.Produced, lot, Giver, Receiver, ulong.MaxValue)
        ];
        Assert.All(unavailable, value => AssertRefused(journey, journey.Approval(value, 33001, 33002),
            WorldActionRejection.Unavailable));

        // A stale approval that was exact before another operation is refused afterwards.
        ConsentedMaterialApproval earlier = journey.Approval(terms, 33004, 33005);
        journey.Gift(Kind.Produced, lot, Giver, Receiver, 1, 33006, 33007);
        AssertRefused(journey, earlier, WorldActionRejection.RevisionConflict);
    }

    [Fact]
    public void Command_acceptance_current_historical_and_child_identities_never_alias()
    {
        var journey = ConsentedMaterialJourney.Create();
        EntityId lot = journey.Craft(34000);
        journey.Gift(Kind.Produced, lot, Giver, Receiver, 1, 34001, 34002);
        EntityId batch = journey.Production.Batches.Single().BatchId;
        ConsentedMaterialTerms Whole() => journey.Terms(Kind.Produced, lot, Giver, Receiver, 1);
        EntityId[] occupied = [Id(34000), Id(34001), Id(34002), Giver, GiverStorage, BerryLot, lot, batch,
            journey.Inventory.WorldId, journey.DeliveryFor(Receiver).SessionId, Reservation];
        foreach (EntityId identity in occupied)
        {
            // The current owners alone refuse these; no external history is consulted.
            AssertRefused(journey, journey.Approval(Whole(), identity, Id(34100)), WorldActionRejection.Unavailable, _ => false);
            AssertRefused(journey, journey.Approval(Whole(), Id(34101), identity), WorldActionRejection.Unavailable, _ => false);
        }
        AssertRefused(journey, journey.Approval(Whole(), 34102, 34102), WorldActionRejection.Unavailable);

        var (split, rations) = CreateWithAdmittedStock(LotProtection.None, null);
        ConsentedMaterialTerms partial = split.Terms(Kind.Produced, rations, Giver, Receiver, 1);
        EntityId child = EntityId.DeriveSpawned(Id(34200), 0);
        AssertRefused(split, split.Approval(partial, 34200, 34201), WorldActionRejection.Unavailable,
            identity => identity == child);
        AssertRefused(split, split.Approval(partial, Id(34200), child), WorldActionRejection.Unavailable,
            _ => false);
        ConsentedMaterialTerms rawPartial = split.Terms(Kind.CountedSource, BerryLot, Giver, Receiver, 1);
        AssertRefused(split, split.Approval(rawPartial, 34200, 34201), WorldActionRejection.Unavailable,
            identity => identity == child);
        // A whole transfer spawns nothing, so the same occupied child identity is irrelevant to it.
        Assert.Equal(WorldActionRejection.None, split.Plan(split.Approval(
            split.Terms(Kind.Produced, rations, Giver, Receiver, 2), 34200, 34201), identity => identity == child).Rejection);
    }

    [Fact]
    public void A_segment_without_permanent_history_cannot_establish_freshness_by_itself()
    {
        var journey = ConsentedMaterialJourney.Create();
        EntityId lot = journey.Craft(35000);
        journey.Gift(Kind.Produced, lot, Giver, Receiver, 1, 35001, 35002);
        journey.Cut();
        ConsentedMaterialApproval replayed = journey.Approval(
            journey.Terms(Kind.Produced, lot, Giver, Receiver, 1), 35003, 35002);
        AssertRefused(journey, replayed, WorldActionRejection.Unavailable);
        // The host's retained registry is the only owner of identities closed by an earlier cut.
        Assert.Equal(WorldActionRejection.None, journey.Plan(replayed, _ => false).Rejection);
    }

    [Fact]
    public void Shared_storage_counts_raw_and_produced_contents_together()
    {
        var journey = ConsentedMaterialJourney.Create(receiverCapacity: 2);
        EntityId lot = journey.Craft(36000);
        journey.Gift(Kind.CountedSource, BerryLot, Giver, Receiver, 1, 36001, 36002);
        AssertRefused(journey, journey.Approval(journey.Terms(Kind.Produced, lot, Giver, Receiver, 2), 36003, 36004),
            WorldActionRejection.Unavailable);
        journey.Gift(Kind.Produced, lot, Giver, Receiver, 1, 36005, 36006);
        Assert.Equal(2UL, journey.Production.GetCombinedStoredQuantity(journey.Inventory, ReceiverStorage));

        // The raw owner alone still has room for one unit; the shared storage does not.
        ConsentedMaterialApproval raw = journey.Approval(
            journey.Terms(Kind.CountedSource, BerryLot, Giver, Receiver, 1), 36007, 36008);
        Assert.Equal(1UL, journey.Inventory.Custody.GetStoredQuantity(ReceiverStorage));
        AssertRefused(journey, raw, WorldActionRejection.Unavailable);
        AssertRefused(journey, journey.Approval(journey.Terms(Kind.Produced, lot, Giver, Receiver, 1), 36009, 36010),
            WorldActionRejection.Unavailable);
        journey.Cut();
        Assert.Equal(2UL, journey.Production.GetCombinedStoredQuantity(journey.Inventory, ReceiverStorage));
    }

    [Fact]
    public void Reserved_and_equipped_produced_stock_is_unavailable_while_manual_flags_travel()
    {
        var (reserved, installed) = CreateWithAdmittedStock(LotProtection.None, Reservation);
        AssertRefused(reserved, reserved.Approval(reserved.Terms(Kind.Produced, installed, Giver, Receiver, 2), 37000, 37001),
            WorldActionRejection.Unavailable);
        AssertRefused(reserved, reserved.Approval(reserved.Terms(Kind.Produced, installed, Giver, Receiver, 1), 37000, 37001),
            WorldActionRejection.Unavailable);
        Assert.Equal(Reservation, reserved.Lot(installed).ReservedBy);

        var (equipped, worn) = CreateWithAdmittedStock(LotProtection.Equipped, null);
        AssertRefused(equipped, equipped.Approval(equipped.Terms(Kind.Produced, worn, Giver, Receiver, 2), 37002, 37003),
            WorldActionRejection.Unavailable);

        const LotProtection manual = LotProtection.Favorite | LotProtection.Heirloom;
        var (flagged, kept) = CreateWithAdmittedStock(manual, null);
        EntityId child = flagged.Gift(Kind.Produced, kept, Giver, Receiver, 1, 37004, 37005);
        Assert.Equal((manual, (EntityId?)null, ReceiverStorage),
            (flagged.Lot(child).Protection, flagged.Lot(child).ReservedBy, flagged.Lot(child).CustodianId));
        Assert.Equal((manual, (EntityId?)null, GiverStorage),
            (flagged.Lot(kept).Protection, flagged.Lot(kept).ReservedBy, flagged.Lot(kept).CustodianId));
        flagged.Cut();
    }

    [Fact]
    public void Application_pins_both_owners_and_rejects_changed_control_policy_capacity_and_history()
    {
        var journey = ConsentedMaterialJourney.Create();
        EntityId lot = journey.Craft(38000);
        ConsentedMaterialTransferPlan plan = journey.Plan(journey.Approval(
            journey.Terms(Kind.Produced, lot, Giver, Receiver, 1), 38001, 38002));
        Assert.True(plan.IsApplied);
        CountedAuthorityState current = journey.Inventory;
        CountedAuthorityState[] changed =
        [
            new(current.Custody, current.AdmittedUnitCapacity, current.Actors, current.Controls.Select(control =>
                control.ActorId == Receiver ? new ActorControlBinding(Id(41), Id(42), Receiver) : control),
                current.Policies, current.Sources, current.ReservationIds),
            new(current.Custody, current.AdmittedUnitCapacity, current.Actors, current.Controls,
                current.Policies.Select(policy => policy.CustodianId == ReceiverStorage
                    ? new CustodianAccessPolicy(ReceiverStorage, Receiver, CustodyDepositRule.AnyControlledActor) : policy),
                current.Sources, current.ReservationIds),
            new(new CustodySnapshot(current.WorldId, current.BranchId, current.Custody.Revision,
                current.Custody.Storages.Select(storage => storage.CustodianId == ReceiverStorage
                    ? storage with { UnitCapacity = 1 } : storage), current.Custody.Lots),
                current.AdmittedUnitCapacity, current.Actors, current.Controls, current.Policies, current.Sources,
                current.ReservationIds)
        ];
        foreach (CountedAuthorityState other in changed)
        {
            Assert.Equal(CustodyConflict.StalePlan,
                Assert.Throws<CustodyConflictException>(() => plan.ApplyTo(other, journey.Production)).Conflict);
            // Changed admission is refused for the continued segment before any approval is decided.
            Assert.Throws<ArgumentException>(() => new ConsentedMaterialTransferPlanner().Plan(other,
                journey.Production, plan.Approval, journey.IsOccupied));
        }

        ProductionSnapshot before = journey.Production;
        journey.Consume(Giver, IngredientLotKind.Transformed, lot, 38003);
        Assert.Equal(CustodyConflict.StalePlan, Assert.Throws<CustodyConflictException>(
            () => plan.ApplyTo(journey.Inventory, journey.Production)).Conflict);
        Assert.Equal(CustodyConflict.StalePlan, Assert.Throws<CustodyConflictException>(
            () => plan.ApplyTo(journey.Inventory, before)).Conflict);
        var applied = plan.ApplyTo(current, before);
        Assert.Same(plan.CandidateInventory, applied.Inventory);
        Assert.Same(plan.CandidateProduction, applied.Production);
    }

    [Fact]
    public void Mutated_valid_frames_decode_as_structure_but_never_pass_the_real_owners()
    {
        var journey = ConsentedMaterialJourney.Create();
        EntityId lot = journey.Craft(39000);
        EntityId child = journey.Gift(Kind.Produced, lot, Giver, Receiver, 1, 39001, 39002);
        journey.Cut();
        var closed = Assert.Single(journey.Closed);
        ProductionContinuationContext context = closed.Production.Continuation!;
        ConsentedMaterialApproval approval = closed.Production.MaterialHistory!.Operations
            .OfType<MaterialConsentedTransferOperation>().Single().Approval;
        ProductionCodecV10.Decode(closed.Bytes, context, []).ValidateForAuthority(closed.Inventory);

        ConsentedMaterialTerms terms = approval.Command.Terms;
        ConsentedMaterialTerms wholeTerms = new(terms.WorldId, terms.BranchId, terms.SenderActorId,
            terms.RecipientActorId, terms.LotKind, terms.LotId, terms.Definition, terms.SourceCustodianId,
            terms.DestinationCustodianId, 2, terms.ExpectedLotRevision);
        var wholeCommand = new ConsentedMaterialCommand(approval.Command.CommandId, approval.Command.PrincipalId,
            approval.Command.OriginSessionId, wholeTerms);
        ConsentedMaterialAcceptance wholeAcceptance = ConsentedMaterialAcceptance.Approving(wholeCommand,
            approval.Acceptance.AcceptanceId, approval.Acceptance.PrincipalId, approval.Acceptance.OriginSessionId);
        byte[] command = ConsentedMaterialCodecV1.Encode(approval.Command);
        byte[] acceptance = ConsentedMaterialCodecV1.Encode(approval.Acceptance);

        byte[] unboundCommand = ReplaceLast(closed.Bytes, command, ConsentedMaterialCodecV1.Encode(wholeCommand));
        byte[] otherResult = ReplaceLast(unboundCommand, acceptance, ConsentedMaterialCodecV1.Encode(wholeAcceptance));
        byte[] otherMovedLot = ReplaceLast(closed.Bytes, child.ToByteArray(), Id(39003).ToByteArray());
        byte[] otherRecipientDelivery = ReplaceLast(closed.Bytes, approval.RecipientDelivery.SessionId.ToByteArray(),
            approval.SenderDelivery.SessionId.ToByteArray());
        byte[] otherSenderDelivery = ReplaceLast(closed.Bytes, approval.SenderDelivery.PrincipalId.ToByteArray(),
            approval.RecipientDelivery.PrincipalId.ToByteArray());
        foreach (byte[] mutated in new[] { unboundCommand, otherResult, otherMovedLot, otherRecipientDelivery, otherSenderDelivery })
        {
            Assert.NotEqual(closed.Bytes, mutated);
            Exception? failure = Record.Exception(() =>
                ProductionCodecV10.Decode(mutated, context, []).ValidateForAuthority(closed.Inventory));
            Assert.True(failure is ArgumentException or FormatException, failure?.ToString());
        }
    }

    [Fact]
    public void Every_other_interpretation_fails_closed_for_consented_history()
    {
        var journey = ConsentedMaterialJourney.Create();
        EntityId lot = journey.Craft(40000);
        journey.Gift(Kind.Produced, lot, Giver, Receiver, 1, 40001, 40002);
        ProductionSnapshot consented = journey.Production;
        byte[] bytes = ProductionCodecV10.Encode(consented);
        ProductionContinuationContext context = consented.Continuation!;
        Assert.True(context.SupportsConsentedTransfer);
        Assert.False(context.SupportsCompleteWorld);
        Assert.Throws<ArgumentException>(() => ProductionCodecV4.Encode(consented));
        Assert.Throws<ArgumentException>(() => ProductionCodecV3.Encode(consented));
        Assert.Throws<ArgumentException>(() => ProductionCodecV9.Encode(consented));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV4.Decode(bytes, context, []));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV3.Decode(bytes, context));
        Assert.Throws<ArgumentException>(() => ProductionCodecV9.Decode(bytes, context));
        Assert.True(consented.ContinueMaterial(journey.Inventory).Continuation!.SupportsConsentedTransfer);
        Assert.True(consented.Continue(journey.Inventory).Continuation!.SupportsConsentedTransfer);

        // The established ordered interpretation keeps its bytes and cannot record or decode an approval.
        CountedAuthorityState inventory = FreshInventory();
        ProductionSnapshot ordered = FreshProduction().ContinueMaterial(inventory);
        Assert.False(ordered.Continuation!.SupportsConsentedTransfer);
        Assert.NotEqual(ordered.Continuation.ContentHash,
            FreshProduction().ContinueConsentedMaterial(inventory).Continuation!.ContentHash);
        Assert.Equal(ProductionCodecV4.Encode(ordered), ProductionCodecV4.Encode(
            ProductionCodecV4.Decode(ProductionCodecV4.Encode(ordered), ordered.Continuation, [])));
        Assert.Throws<ArgumentException>(() => ProductionCodecV10.Encode(ordered));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV10.Decode(
            ProductionCodecV4.Encode(ordered), ordered.Continuation, []));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV4.Decode(bytes, ordered.Continuation, []));
        var unsupported = ConsentedMaterialJourney.Create();
        Assert.Throws<ArgumentException>(() => new ConsentedMaterialTransferPlanner().Plan(inventory, ordered,
            unsupported.Approval(unsupported.Terms(Kind.CountedSource, BerryLot, Giver, Receiver, 1), 40003, 40004),
            _ => false));
        Assert.Throws<ArgumentException>(() => new ConsentedMaterialTransferPlanner().Plan(inventory,
            FreshProduction(), unsupported.Approval(
                unsupported.Terms(Kind.CountedSource, BerryLot, Giver, Receiver, 1), 40003, 40004), _ => false));
    }

    private static void AssertRefused(ConsentedMaterialJourney journey, ConsentedMaterialApproval approval,
        WorldActionRejection expected, Func<EntityId, bool>? occupied = null)
    {
        var inventoryHash = journey.Inventory.ContentHash;
        var productionHash = journey.Production.ContentHash;
        ConsentedMaterialTransferPlan plan = journey.Plan(approval, occupied);
        Assert.Equal(expected, plan.Rejection);
        Assert.False(plan.IsApplied);
        Assert.Null(plan.MovedLotId);
        Assert.Same(journey.Inventory, plan.CandidateInventory);
        Assert.Same(journey.Production, plan.CandidateProduction);
        Assert.Equal(inventoryHash, journey.Inventory.ContentHash);
        Assert.Equal(productionHash, journey.Production.ContentHash);
        Assert.Throws<InvalidOperationException>(() => plan.ApplyTo(journey.Inventory, journey.Production));
        journey.Production.ValidateForAuthority(journey.Inventory);
    }

    // An acceptance that restates other terms or pins another command while the sender command is unchanged.
    private static ConsentedMaterialApproval Rebound(ConsentedMaterialApproval approval,
        ConsentedMaterialTerms acceptedTerms, EntityId? commandId = null) => new(approval.Command,
        approval.SenderDelivery, new ConsentedMaterialAcceptance(approval.Acceptance.AcceptanceId,
            approval.Acceptance.PrincipalId, approval.Acceptance.OriginSessionId,
            commandId ?? approval.Command.CommandId, approval.Command.ComputeContentHash(), acceptedTerms),
        approval.RecipientDelivery);

    private static byte[] ReplaceLast(byte[] source, byte[] find, byte[] replacement)
    {
        Assert.Equal(find.Length, replacement.Length);
        int index = source.AsSpan().LastIndexOf(find);
        Assert.True(index >= 0);
        byte[] copy = (byte[])source.Clone();
        replacement.CopyTo(copy, index);
        return copy;
    }
}
