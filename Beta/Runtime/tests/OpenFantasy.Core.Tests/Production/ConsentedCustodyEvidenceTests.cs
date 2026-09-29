using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Production.InstantCraftFixture;

namespace OpenFantasy.Core.Tests.Production;

/// <summary>
/// Unit evidence for the custody partition replay on actually crafted stock. The moved states mirror
/// the shared decision step's result; they are not a world journey and move nothing themselves.
/// </summary>
public sealed class ConsentedCustodyEvidenceTests
{
    private const string PartitionMismatch =
        "Live produced lots must equal the partition replayed from retirements and ordered custody evidence.";
    private static readonly EntityId Maker = Id(60);
    private static readonly EntityId Recipient = Id(61);
    private static readonly EntityId Child = Id(7000);

    [Fact]
    public void Ordered_evidence_admits_a_split_gift_recipient_recraft_and_onward_movement()
    {
        var (inventory, crafted, lot) = CraftRations();
        crafted.ValidateCustodyEvidence([]);
        ProductionBatch producer = crafted.Batches.Single();
        var split = new ProducedCustodyMovement(lot.LotId, 0, Child, Maker, Recipient, 1);
        ProductionSnapshot moved = WithRations(crafted, lot, Remainder(lot, Maker, 1, 2), Moved(lot, Recipient, 0, 1));
        moved.ValidateForAuthority(inventory);
        moved.ValidateCustodyEvidence([split]);

        // The recipient recrafts the moved unit through the real planner with its own control.
        CraftRecipeDefinition repack = moved.Recipes.Single(value => value.DefinitionId == Id(5204));
        CraftPlan recraft = new InstantCraftPlanner().Plan(inventory, moved, new TrustedCommandDelivery(Id(21), Id(31)),
            Id(11), Id(6001), new CraftRequest(repack.DefinitionId, repack.Version, moved.Revision, Recipient,
                inventory.Custody.GetStorage(Recipient).Revision, [new(IngredientLotKind.Transformed, Child, 0, 1)]));
        Assert.Equal(WorldActionRejection.None, recraft.Rejection);
        ProductionSnapshot recrafted = recraft.CandidateProduction;
        ProductionBatch second = recrafted.Batches.Single(value => value.CommandId == Id(6001));
        RetiredIngredient ancestry = Assert.Single(second.Ingredients);
        Assert.Equal((Child, producer.BatchId, lot.Output.OutputIndex, 0UL, 1UL, Id(11), Recipient),
            (ancestry.LotId, ancestry.Output!.BatchId, ancestry.Output.OutputIndex, ancestry.Output.Start,
                ancestry.Output.End, second.ActorId, second.CustodianId));
        Assert.Equal((Id(10), Maker), (producer.ActorId, producer.CustodianId));
        recrafted.ValidateCustodyEvidence([split]);

        // Without the split evidence the recipient never held the unit it spent.
        Assert.Equal("Retirement must spend an actual imported, produced or moved lot.",
            Assert.Throws<ArgumentException>(() => recrafted.ValidateCustodyEvidence([])).Message);

        TransformedLot repacked = recrafted.Lots.Single(value => value.Output.BatchId == second.BatchId);
        var onward = new ProducedCustodyMovement(repacked.LotId, 0, repacked.LotId, Recipient, Maker, 1);
        ProductionSnapshot returned = WithLots(recrafted, recrafted.Lots.Select(value => value.LotId != repacked.LotId
            ? value : new TransformedLot(value.LotId, Maker, value.Definition, value.Output, 1)));
        returned.ValidateCustodyEvidence([split, onward]);
        Assert.Equal(PartitionMismatch,
            Assert.Throws<ArgumentException>(() => returned.ValidateCustodyEvidence([split])).Message);
    }

    [Fact]
    public void Balanced_custody_edits_without_matching_ordered_evidence_are_refused()
    {
        var (inventory, crafted, lot) = CraftRations();
        var split = new ProducedCustodyMovement(lot.LotId, 0, Child, Maker, Recipient, 1);
        ProductionSnapshot valid = WithRations(crafted, lot, Remainder(lot, Maker, 1, 2), Moved(lot, Recipient, 0, 1));
        valid.ValidateForAuthority(inventory);
        valid.ValidateCustodyEvidence([split]);

        ProductionSnapshot swapped = WithRations(crafted, lot, Remainder(lot, Recipient, 1, 2), Moved(lot, Maker, 0, 1));
        ProductionSnapshot suffix = WithRations(crafted, lot, Remainder(lot, Maker, 0, 1), Moved(lot, Recipient, 1, 2));
        ProductionSnapshot whole = WithRations(crafted, lot,
            new TransformedLot(lot.LotId, Recipient, lot.Definition, lot.Output, lot.Revision + 1));
        foreach (ProductionSnapshot balanced in new[] { swapped, suffix, whole })
        {
            // Each state conserves every unit and passes ordinary admission; only custody evidence refuses it.
            balanced.ValidateForAuthority(inventory);
            Assert.Equal(2UL, balanced.Lots.Where(value => value.Definition == Rations)
                .Aggregate(0UL, (total, value) => total + value.Quantity));
            Assert.Equal(PartitionMismatch,
                Assert.Throws<ArgumentException>(() => balanced.ValidateCustodyEvidence([split])).Message);
        }

        // Moved stock whose evidence is removed, and unmoved stock with evidence that never applied.
        Assert.Equal(PartitionMismatch,
            Assert.Throws<ArgumentException>(() => valid.ValidateCustodyEvidence([])).Message);
        Assert.Equal(PartitionMismatch,
            Assert.Throws<ArgumentException>(() => crafted.ValidateCustodyEvidence([split])).Message);
        whole.ValidateCustodyEvidence([new(lot.LotId, 0, lot.LotId, Maker, Recipient, 2)]);
    }

    [Fact]
    public void Evidence_must_move_the_approved_revision_from_the_actual_custodian_in_order()
    {
        var (_, crafted, lot) = CraftRations();
        ProductionSnapshot moved = WithRations(crafted, lot, Remainder(lot, Maker, 1, 2), Moved(lot, Recipient, 0, 1));
        const string unavailable =
            "Custody evidence must move available unreserved stock from its actual custodian at the approved lot revision.";
        ProducedCustodyMovement[] refused =
        [
            new(lot.LotId, 1, Child, Maker, Recipient, 1),
            new(lot.LotId, 0, Child, Recipient, Maker, 1),
            new(lot.LotId, 0, Child, Maker, Recipient, 3),
            new(lot.LotId, 0, Child, Maker, Recipient, 2),
            new(lot.LotId, 0, lot.LotId, Maker, Recipient, 1)
        ];
        Assert.All(refused, movement => Assert.Equal(unavailable,
            Assert.Throws<ArgumentException>(() => moved.ValidateCustodyEvidence([movement])).Message));

        var split = new ProducedCustodyMovement(lot.LotId, 0, Child, Maker, Recipient, 1);
        var onward = new ProducedCustodyMovement(Child, 0, Child, Recipient, Maker, 1);
        Assert.Equal("Custody evidence must name an actual imported, produced or moved lot.",
            Assert.Throws<ArgumentException>(() => moved.ValidateCustodyEvidence([onward, split])).Message);
        Assert.Equal("Produced lots and split children require distinct permanent identities.",
            Assert.Throws<ArgumentException>(() => moved.ValidateCustodyEvidence(
                [new(lot.LotId, 0, lot.LotId == Child ? Id(7001) : crafted.Lots.First(value => value.LotId != lot.LotId).LotId,
                    Maker, Recipient, 1)])).Message);
    }

    private static (CountedAuthorityState Inventory, ProductionSnapshot Production, TransformedLot Rations) CraftRations()
    {
        var (inventory, production) = Create(chained: true);
        CraftPlan plan = Craft(inventory, production);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        TransformedLot lot = plan.CandidateProduction.Lots.Single(value => value.Definition == Rations);
        Assert.Equal((Maker, 0UL, 2UL, 0UL), (lot.CustodianId, lot.Output.Start, lot.Output.End, lot.Revision));
        return (plan.CandidateInventory, plan.CandidateProduction, lot);
    }

    private static TransformedLot Remainder(TransformedLot lot, EntityId custodian, ulong start, ulong end) =>
        new(lot.LotId, custodian, lot.Definition,
            new ProductionOutputInterval(lot.Output.BatchId, lot.Output.OutputIndex, start, end), lot.Revision + 1);

    private static TransformedLot Moved(TransformedLot lot, EntityId custodian, ulong start, ulong end) =>
        new(Child, custodian, lot.Definition,
            new ProductionOutputInterval(lot.Output.BatchId, lot.Output.OutputIndex, start, end), 0);

    private static ProductionSnapshot WithRations(ProductionSnapshot state, TransformedLot original,
        params TransformedLot[] replacement) =>
        WithLots(state, state.Lots.Where(value => value.LotId != original.LotId).Concat(replacement));

    private static ProductionSnapshot WithLots(ProductionSnapshot state, IEnumerable<TransformedLot> lots) =>
        new(state.WorldId, state.BranchId, state.Revision, state.Recipes, state.Batches, lots,
            state.SupportsConsumption ? state.ConsumedItems : null);
}
