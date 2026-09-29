using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Production.ConsentedMaterialJourney;
using static OpenFantasy.Core.Tests.Production.InstantCraftFixture;
using Kind = OpenFantasy.Core.Authority.ConsentedMaterial.ConsentedMaterialLotKind;

namespace OpenFantasy.Core.Tests.Production;

/// <summary>
/// Consent names the goods, parties and approved lot revision. Every other revision is execution
/// state read from the actual current owners when the transaction is decided.
/// </summary>
public sealed class ConsentedMaterialConsentTests
{
    private const string ReplayMismatch = "Current material owners must exactly equal the ordered operation replay.";

    [Fact]
    public void Unrelated_crafting_and_bag_changes_leave_unchanged_consent_applicable()
    {
        var journey = ConsentedMaterialJourney.Create();
        EntityId approved = journey.Craft(50000);
        TransformedLot original = journey.Lot(approved);
        ConsentedMaterialApproval produced = journey.Approval(
            journey.Terms(Kind.Produced, approved, Giver, Receiver, 1), 50002, 50003);
        var before = Revisions(journey);

        // Another craft, and unrelated stock leaving the sender's bag and entering the recipient's.
        EntityId other = journey.Craft(50004);
        journey.Gift(Kind.CountedSource, BerryLot, Giver, Receiver, 1, 50005, 50006);
        var after = Revisions(journey);
        Assert.True(after.Production > before.Production);
        Assert.True(after.Custody > before.Custody);
        Assert.True(after.Source > before.Source);
        Assert.True(after.Destination > before.Destination);
        Assert.Equal(original.Revision, journey.Lot(approved).Revision);

        EntityId child = journey.Apply(produced);
        Assert.Equal(EntityId.DeriveSpawned(Id(50002), 0), child);
        Assert.Equal((original.Output.BatchId, original.Output.OutputIndex, 0UL, 1UL, 0UL, ReceiverStorage),
            (journey.Lot(child).Output.BatchId, journey.Lot(child).Output.OutputIndex, journey.Lot(child).Output.Start,
                journey.Lot(child).Output.End, journey.Lot(child).Revision, journey.Lot(child).CustodianId));
        Assert.Equal((1UL, 2UL, original.Revision + 1, GiverStorage), (journey.Lot(approved).Output.Start,
            journey.Lot(approved).Output.End, journey.Lot(approved).Revision, journey.Lot(approved).CustodianId));

        // The same holds for raw stock: only the approved lot's own revision is pinned.
        ConsentedMaterialApproval raw = journey.Approval(
            journey.Terms(Kind.CountedSource, WoodLot, Giver, Receiver, 1), 50007, 50008);
        ulong woodRevision = journey.Inventory.Custody.GetLot(WoodLot).Revision;
        journey.Gift(Kind.Produced, other, Giver, Receiver, 2, 50009, 50010);
        journey.Gift(Kind.Produced, approved, Giver, Onward, 1, 50011, 50012);
        Assert.Equal(woodRevision, journey.Inventory.Custody.GetLot(WoodLot).Revision);
        EntityId rawChild = journey.Apply(raw);
        Assert.Equal((ReceiverStorage, 1UL), (journey.Inventory.Custody.GetLot(rawChild).CustodianId,
            journey.Inventory.Custody.GetLot(rawChild).Quantity));
        journey.Cut();
    }

    [Fact]
    public void A_changed_approved_lot_or_changed_terms_prevents_application_without_partial_effects()
    {
        var journey = ConsentedMaterialJourney.Create();
        EntityId lot = journey.Craft(51000);
        ConsentedMaterialApproval produced = journey.Approval(
            journey.Terms(Kind.Produced, lot, Giver, Receiver, 1), 51001, 51002);
        ConsentedMaterialApproval raw = journey.Approval(
            journey.Terms(Kind.CountedSource, WoodLot, Giver, Receiver, 1), 51003, 51004);
        Assert.Equal(WorldActionRejection.None, journey.Plan(produced).Rejection);
        Assert.Equal(WorldActionRejection.None, journey.Plan(raw).Rejection);

        // The sender uses one approved unit and crafts from the approved raw lot.
        journey.Consume(Giver, IngredientLotKind.Transformed, lot, 51005);
        journey.Craft(51006);
        AssertRefused(journey, produced, WorldActionRejection.RevisionConflict);
        AssertRefused(journey, raw, WorldActionRejection.RevisionConflict);

        // Consent to the surviving remainder is new consent with its own identities.
        ConsentedMaterialTerms current = journey.Terms(Kind.Produced, lot, Giver, Receiver, 1);
        Assert.Equal(produced.Command.Terms.ExpectedLotRevision + 1, current.ExpectedLotRevision);
        var accepted = new ConsentedMaterialAcceptance(Id(51008), produced.Acceptance.PrincipalId,
            produced.Acceptance.OriginSessionId, Id(51007),
            new ConsentedMaterialCommand(Id(51007), produced.Command.PrincipalId,
                produced.Command.OriginSessionId, current).ComputeContentHash(), produced.Command.Terms);
        var changedTerms = new ConsentedMaterialApproval(new(Id(51007), produced.Command.PrincipalId,
            produced.Command.OriginSessionId, current), produced.SenderDelivery, accepted, produced.RecipientDelivery);
        AssertRefused(journey, changedTerms, WorldActionRejection.NotAuthorized);
        Assert.Equal(lot, journey.Gift(Kind.Produced, lot, Giver, Receiver, 1, 51009, 51010));
    }

    [Fact]
    public void Lost_control_or_ownership_withdraws_approval_for_the_same_unchanged_terms()
    {
        var journey = ConsentedMaterialJourney.Create();
        EntityId lot = journey.Craft(52000);
        ConsentedMaterialApproval approval = journey.Approval(
            journey.Terms(Kind.Produced, lot, Giver, Receiver, 1), 52001, 52002);
        CountedAuthorityState current = journey.Inventory;
        Assert.True(approval.IsApprovedByBothParticipants(current));

        CountedAuthorityState[] changed =
        [
            WithControls(current, current.Controls.Select(control => control.ActorId == Receiver
                ? new ActorControlBinding(Id(41), Id(42), Receiver) : control)),
            WithControls(current, current.Controls.Select(control => control.ActorId == Giver
                ? new ActorControlBinding(Id(43), Id(44), Giver) : control)),
            WithControls(current, current.Controls.Where(control => control.ActorId != Receiver)),
            WithPolicies(current, current.Policies.Select(policy => policy.CustodianId == ReceiverStorage
                ? new CustodianAccessPolicy(ReceiverStorage, Onward, CustodyDepositRule.OwnerOnly) : policy)),
            WithPolicies(current, current.Policies.Select(policy => policy.CustodianId == GiverStorage
                ? new CustodianAccessPolicy(GiverStorage, Onward, CustodyDepositRule.OwnerOnly) : policy))
        ];
        Assert.All(changed, other =>
        {
            Assert.False(approval.IsApprovedByBothParticipants(other));
            // The continued segment also refuses the replaced admission before deciding any approval.
            Assert.Throws<ArgumentException>(() => new ConsentedMaterialTransferPlanner().Plan(other,
                journey.Production, approval, journey.IsOccupied));
        });
        Assert.Equal(EntityId.DeriveSpawned(Id(52001), 0), journey.Apply(approval));
    }

    [Fact]
    public void Reservation_and_actual_capacity_decide_application_of_unchanged_consent()
    {
        var (free, rations) = CreateWithAdmittedStock(LotProtection.None, null);
        var (reserved, installed) = CreateWithAdmittedStock(LotProtection.None, Reservation);
        Assert.Equal(rations, installed);
        ConsentedMaterialApproval approval = free.Approval(
            free.Terms(Kind.Produced, rations, Giver, Receiver, 1), 53000, 53001);
        Assert.Equal(approval.Command.Terms, reserved.Terms(Kind.Produced, installed, Giver, Receiver, 1));
        AssertRefused(reserved, approval, WorldActionRejection.Unavailable);
        Assert.Equal(Reservation, reserved.Lot(installed).ReservedBy);
        Assert.Equal(EntityId.DeriveSpawned(Id(53000), 0), free.Apply(approval));

        var journey = ConsentedMaterialJourney.Create(receiverCapacity: 2);
        EntityId lot = journey.Craft(53002);
        ConsentedMaterialApproval whole = journey.Approval(
            journey.Terms(Kind.Produced, lot, Giver, Receiver, 2), 53003, 53004);
        Assert.Equal(WorldActionRejection.None, journey.Plan(whole).Rejection);
        EntityId berry = journey.Gift(Kind.CountedSource, BerryLot, Giver, Receiver, 1, 53005, 53006);
        AssertRefused(journey, whole, WorldActionRejection.Unavailable);

        // Once the unrelated unit leaves again the same consent fits; revisions alone never decided it.
        journey.Gift(Kind.CountedSource, berry, Receiver, Onward, 1, 53007, 53008);
        Assert.Equal(lot, journey.Apply(whole));
        Assert.Equal(2UL, journey.Production.GetCombinedStoredQuantity(journey.Inventory, ReceiverStorage));
    }

    [Fact]
    public void A_decided_candidate_stays_pinned_to_its_exact_owners_while_consent_outlives_them()
    {
        var journey = ConsentedMaterialJourney.Create();
        EntityId lot = journey.Craft(54000);
        ConsentedMaterialApproval approval = journey.Approval(
            journey.Terms(Kind.Produced, lot, Giver, Receiver, 1), 54001, 54002);
        ConsentedMaterialTransferPlan decided = journey.Plan(approval);
        Assert.True(decided.IsApplied);
        CountedAuthorityState inventory = journey.Inventory;
        ProductionSnapshot production = journey.Production;

        journey.Craft(54003);
        Assert.Equal(CustodyConflict.StalePlan, Assert.Throws<CustodyConflictException>(
            () => decided.ApplyTo(journey.Inventory, journey.Production)).Conflict);
        Assert.Equal(CustodyConflict.StalePlan, Assert.Throws<CustodyConflictException>(
            () => decided.ApplyTo(inventory, journey.Production)).Conflict);
        Assert.Equal(CustodyConflict.StalePlan, Assert.Throws<CustodyConflictException>(
            () => decided.ApplyTo(journey.Inventory, production)).Conflict);

        // The unchanged consent is decided again against the actual current owners.
        ConsentedMaterialTransferPlan current = journey.Plan(approval);
        Assert.True(current.IsApplied);
        Assert.NotEqual(decided.BaseProductionHash, current.BaseProductionHash);
        Assert.Equal(decided.MovedLotId, journey.Apply(approval));
    }

    [Fact]
    public void Authentication_precedes_the_exact_history_limit_and_a_cut_continues_consent()
    {
        var journey = ConsentedMaterialJourney.Create();
        ulong identity = 60_000;
        for (int operation = 0; operation < MaterialOperationHistory.MaximumOperations; operation++)
        {
            bool outward = operation % 2 == 0;
            journey.Gift(Kind.CountedSource, WoodLot, outward ? Giver : Receiver, outward ? Receiver : Giver, 4,
                identity++, identity++);
        }
        Assert.Equal(MaterialOperationHistory.MaximumOperations, journey.Production.MaterialHistory!.Operations.Count);
        Assert.Equal(GiverStorage, journey.Inventory.Custody.GetLot(WoodLot).CustodianId);

        ConsentedMaterialTerms terms = journey.Terms(Kind.CountedSource, WoodLot, Giver, Receiver, 4);
        ConsentedMaterialApproval valid = journey.Approval(terms, 70_000, 70_001);
        ConsentedMaterialApproval forged = journey.Approval(terms, 70_002, 70_003,
            senderDelivery: journey.DeliveryFor(Onward));
        AssertRefused(journey, forged, WorldActionRejection.NotAuthorized);
        AssertRefused(journey, valid, WorldActionRejection.Unavailable);

        journey.Cut();
        Assert.Empty(journey.Production.MaterialHistory!.Operations);
        Assert.Equal((ulong)MaterialOperationHistory.MaximumOperations, journey.Production.TotalMaterialOperationCount);
        AssertRefused(journey, forged, WorldActionRejection.NotAuthorized);

        // Identities closed by the cut stay permanently occupied in either role.
        AssertRefused(journey, journey.Approval(terms, 60_000, 70_004), WorldActionRejection.Unavailable);
        AssertRefused(journey, journey.Approval(terms, 70_005, 60_001), WorldActionRejection.Unavailable);
        Assert.Equal(WoodLot, journey.Apply(valid));
        Assert.Equal(ReceiverStorage, journey.Inventory.Custody.GetLot(WoodLot).CustodianId);
        Assert.Single(journey.Production.MaterialHistory.Operations);
    }

    [Fact]
    public void The_combined_live_lot_limit_refuses_a_split_and_admits_a_whole_movement()
    {
        var measured = ConsentedMaterialJourney.Create();
        measured.Craft(80_000);
        int crafted = measured.LiveLotCount;

        var full = ConsentedMaterialJourney.Create(fillerLots: ProductionSnapshot.MaximumLots - crafted);
        EntityId lot = full.Craft(80_000);
        Assert.Equal(ProductionSnapshot.MaximumLots, full.LiveLotCount);
        AssertRefused(full, full.Approval(full.Terms(Kind.Produced, lot, Giver, Receiver, 1), 80_001, 80_002),
            WorldActionRejection.Unavailable);
        AssertRefused(full, full.Approval(full.Terms(Kind.CountedSource, WoodLot, Giver, Receiver, 1), 80_001, 80_002),
            WorldActionRejection.Unavailable);
        Assert.Equal(lot, full.Gift(Kind.Produced, lot, Giver, Receiver, 2, 80_003, 80_004));
        Assert.Equal(WoodLot, full.Gift(Kind.CountedSource, WoodLot, Giver, Receiver, 3, 80_005, 80_006));
        Assert.Equal(ProductionSnapshot.MaximumLots, full.LiveLotCount);

        var room = ConsentedMaterialJourney.Create(fillerLots: ProductionSnapshot.MaximumLots - crafted - 1);
        EntityId divisible = room.Craft(80_000);
        Assert.Equal(ProductionSnapshot.MaximumLots - 1, room.LiveLotCount);
        EntityId child = room.Gift(Kind.Produced, divisible, Giver, Receiver, 1, 80_001, 80_002);
        Assert.Equal(EntityId.DeriveSpawned(Id(80_001), 0), child);
        Assert.Equal(ProductionSnapshot.MaximumLots, room.LiveLotCount);
        AssertRefused(room, room.Approval(room.Terms(Kind.CountedSource, WoodLot, Giver, Receiver, 1), 80_003, 80_004),
            WorldActionRejection.Unavailable);
        room.Cut();
    }

    [Fact]
    public void Raw_movement_requests_are_constructible_for_every_valid_terms_value()
    {
        var journey = ConsentedMaterialJourney.Create();
        // Terms may name scope or participant identities as the lot; the decision refuses, it never throws.
        EntityId[] aliases = [journey.Inventory.WorldId, journey.Inventory.BranchId, Giver, Receiver, Id(20), Id(30)];
        foreach (EntityId alias in aliases)
        {
            var terms = new ConsentedMaterialTerms(journey.Inventory.WorldId, journey.Inventory.BranchId, Giver, Receiver,
                Kind.CountedSource, alias, Wood, GiverStorage, ReceiverStorage, ulong.MaxValue, ulong.MaxValue);
            AssertRefused(journey, journey.Approval(terms, 81_000, 81_001), WorldActionRejection.Unavailable);
        }
        var stale = new ConsentedMaterialTerms(journey.Inventory.WorldId, journey.Inventory.BranchId, Giver, Receiver,
            Kind.CountedSource, WoodLot, Wood, GiverStorage, ReceiverStorage, 4, ulong.MaxValue);
        AssertRefused(journey, journey.Approval(stale, 81_000, 81_001), WorldActionRejection.RevisionConflict);
    }

    [Fact]
    public void Balanced_custody_edits_decode_as_structure_and_fail_the_ordered_authority_replay()
    {
        var journey = ConsentedMaterialJourney.Create();
        EntityId lot = journey.Craft(90_000);
        EntityId batch = journey.Lot(lot).Output.BatchId;
        EntityId child = journey.Gift(Kind.Produced, lot, Giver, Receiver, 1, 90_001, 90_002);
        CountedAuthorityState inventory = journey.Inventory;
        ProductionContinuationContext context = journey.Production.Continuation!;
        byte[] bytes = ProductionCodecV10.Encode(journey.Production);

        // Valid control: the unchanged frame passes both stages.
        ProductionCodecV10.Decode(bytes, context, []).ValidateForAuthority(inventory);

        byte[] swapped = ReplaceCustodian(ReplaceCustodian(bytes, lot, GiverStorage, ReceiverStorage),
            child, ReceiverStorage, GiverStorage);
        byte[] suffix = SwapIntervals(bytes, lot, child, batch);
        foreach (byte[] mutated in new[] { swapped, suffix })
        {
            Assert.NotEqual(bytes, mutated);
            Assert.Equal(bytes.Length, mutated.Length);
            ProductionSnapshot structural = ProductionCodecV10.Decode(mutated, context, []);
            Assert.Equal(2UL, structural.Lots.Where(value => value.Output.BatchId == batch && value.Definition == Rations)
                .Aggregate(0UL, (total, value) => total + value.Quantity));
            Assert.Equal(ReplayMismatch,
                Assert.Throws<ArgumentException>(() => structural.ValidateForAuthority(inventory)).Message);
        }
        ProductionSnapshot moved = ProductionCodecV10.Decode(swapped, context, []);
        Assert.Equal((ReceiverStorage, GiverStorage),
            (moved.Lots.Single(value => value.LotId == lot).CustodianId, moved.Lots.Single(value => value.LotId == child).CustodianId));
        ProductionSnapshot reversed = ProductionCodecV10.Decode(suffix, context, []);
        Assert.Equal((0UL, 1UL, 1UL, 2UL), (reversed.Lots.Single(value => value.LotId == lot).Output.Start,
            reversed.Lots.Single(value => value.LotId == lot).Output.End,
            reversed.Lots.Single(value => value.LotId == child).Output.Start,
            reversed.Lots.Single(value => value.LotId == child).Output.End));
    }

    [Fact]
    public void Moved_stock_without_its_recorded_approval_fails_the_ordered_authority_replay()
    {
        var journey = ConsentedMaterialJourney.Create();
        EntityId lot = journey.Craft(91_000);
        journey.Cut();
        CountedAuthorityState inventory = journey.Inventory;
        ProductionContinuationContext context = journey.Production.Continuation!;
        byte[] unmoved = ProductionCodecV10.Encode(journey.Production);
        Assert.Empty(journey.Production.MaterialHistory!.Operations);

        // Valid control: the actual empty segment passes both stages against its closing inventory.
        ProductionCodecV10.Decode(unmoved, context, []).ValidateForAuthority(inventory);

        byte[] moved = ReplaceCustodian(unmoved, lot, GiverStorage, ReceiverStorage);
        ProductionSnapshot structural = ProductionCodecV10.Decode(moved, context, []);
        Assert.Empty(structural.MaterialHistory!.Operations);
        Assert.Equal(ReceiverStorage, structural.Lots.Single(value => value.LotId == lot).CustodianId);
        Assert.Equal(ReplayMismatch,
            Assert.Throws<ArgumentException>(() => structural.ValidateForAuthority(inventory)).Message);

        // The genuine approval moves the same stock and its recorded evidence admits the result.
        Assert.Equal(lot, journey.Gift(Kind.Produced, lot, Giver, Receiver, 2, 91_001, 91_002));
        ProductionCodecV10.Decode(ProductionCodecV10.Encode(journey.Production), context, [])
            .ValidateForAuthority(journey.Inventory);
    }

    private static (ulong Production, ulong Custody, ulong Source, ulong Destination) Revisions(
        ConsentedMaterialJourney journey) => (journey.Production.Revision, journey.Inventory.Custody.Revision,
        journey.Inventory.Custody.GetStorage(GiverStorage).Revision,
        journey.Inventory.Custody.GetStorage(ReceiverStorage).Revision);

    private static CountedAuthorityState WithControls(CountedAuthorityState current,
        IEnumerable<ActorControlBinding> controls) => new(current.Custody, current.AdmittedUnitCapacity,
        current.Actors, controls, current.Policies, current.Sources, current.ReservationIds);

    private static CountedAuthorityState WithPolicies(CountedAuthorityState current,
        IEnumerable<CustodianAccessPolicy> policies) => new(current.Custody, current.AdmittedUnitCapacity,
        current.Actors, current.Controls, policies, current.Sources, current.ReservationIds);

    private static void AssertRefused(ConsentedMaterialJourney journey, ConsentedMaterialApproval approval,
        WorldActionRejection expected)
    {
        var inventoryHash = journey.Inventory.ContentHash;
        var productionHash = journey.Production.ContentHash;
        ConsentedMaterialTransferPlan plan = journey.Plan(approval);
        Assert.Equal(expected, plan.Rejection);
        Assert.False(plan.IsApplied);
        Assert.Null(plan.MovedLotId);
        Assert.Same(journey.Inventory, plan.CandidateInventory);
        Assert.Same(journey.Production, plan.CandidateProduction);
        Assert.Equal(inventoryHash, journey.Inventory.ContentHash);
        Assert.Equal(productionHash, journey.Production.ContentHash);
    }

    // A live lot frame holds its identity, a four-byte field length and then its custodian.
    private const int IdentityLength = 16;
    private const int FieldPrefixLength = sizeof(uint);

    private static int LotFrameIndex(byte[] frame, EntityId lotId, EntityId custodianId)
    {
        byte[] lot = lotId.ToByteArray();
        byte[] custodian = custodianId.ToByteArray();
        int found = -1;
        for (int index = 0; index + 2 * IdentityLength + FieldPrefixLength <= frame.Length; index++)
        {
            if (!frame.AsSpan(index, IdentityLength).SequenceEqual(lot)
                || !frame.AsSpan(index + IdentityLength + FieldPrefixLength, IdentityLength).SequenceEqual(custodian))
            {
                continue;
            }
            Assert.Equal(-1, found);
            found = index;
        }
        Assert.True(found >= 0);
        return found;
    }

    private static byte[] ReplaceCustodian(byte[] frame, EntityId lotId, EntityId from, EntityId to)
    {
        int index = LotFrameIndex(frame, lotId, from);
        byte[] copy = (byte[])frame.Clone();
        to.ToByteArray().CopyTo(copy, index + IdentityLength + FieldPrefixLength);
        return copy;
    }

    // The output claim holds its batch, output index, start and end as consecutive prefixed fields.
    private static byte[] SwapIntervals(byte[] frame, EntityId first, EntityId second, EntityId batchId)
    {
        const int intervalLength = sizeof(ulong) + FieldPrefixLength + sizeof(ulong);
        int Start(EntityId lotId)
        {
            EntityId custodian = lotId == first ? GiverStorage : ReceiverStorage;
            int lot = LotFrameIndex(frame, lotId, custodian);
            int batch = frame.AsSpan(lot).IndexOf(batchId.ToByteArray());
            Assert.True(batch >= 0);
            return lot + batch + IdentityLength + FieldPrefixLength + sizeof(ulong) + FieldPrefixLength;
        }
        int left = Start(first);
        int right = Start(second);
        byte[] copy = (byte[])frame.Clone();
        frame.AsSpan(left, intervalLength).CopyTo(copy.AsSpan(right));
        frame.AsSpan(right, intervalLength).CopyTo(copy.AsSpan(left));
        return copy;
    }
}
