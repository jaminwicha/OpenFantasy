using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Hosting.Tests.Care.WorldResourceJourneyTests;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed partial class ConsentedWorldPublicationTests
{
    [Theory]
    [InlineData(false)] // The two equal live lots exchange custodians.
    [InlineData(true)]  // The gifted child claims the suffix and the remainder the moved prefix.
    public void Balanced_live_lot_mutation_is_refused_by_the_custody_partition(bool movedSuffix)
    {
        var fixture = new ConsentedPotionFixture();
        var journey = fixture.Journey;
        var session = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        journey.Approach(session);
        WorldSnapshot state = session.ReadStateForHost();
        EntityId helperBag = state.GetResident(journey.Helper).InventoryCustodianId;
        EntityId playerBag = state.GetResident(journey.Player).InventoryCustodianId;
        var berries = state.Inventory.Custody.GetLot(fixture.BerryLot);
        WorldActionCommand craft = CraftCommand(state, journey, journey.Helper, fixture.Bulk,
            new(IngredientLotKind.Source, berries.LotId, berries.Revision, 12));
        state = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.AdvancingStep, [craft])).Candidate;
        TransformedLot stock = Assert.Single(state.Production!.Lots);
        var pair = Pair(state, journey, new ConsentedMaterialTerms(state.WorldId, state.BranchId, journey.Helper,
            journey.Player, ConsentedMaterialLotKind.Produced, stock.LotId, stock.Definition, helperBag, playerBag, 2,
            stock.Revision));
        WorldSnapshot gifted = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.AdvancingStep,
            [pair.Offer, pair.Acceptance])).Candidate;
        ProductionSnapshot actual = gifted.Production!;
        TransformedLot kept = actual.Lots.Single(value => value.LotId == stock.LotId);
        TransformedLot child = actual.Lots.Single(value => value.LotId == EntityId.DeriveSpawned(pair.Offer.CommandId, 0));
        Assert.Equal((helperBag, 2UL, 4UL, playerBag, 0UL, 2UL), (kept.CustodianId, kept.Output.Start, kept.Output.End,
            child.CustodianId, child.Output.Start, child.Output.End));

        // Positive control: an equal separately constructed owner passes the same complete admission.
        byte[] original = WorldSnapshotCodec.Encode(gifted);
        Assert.Equal(original, WorldSnapshotCodec.Encode(Readmit(gifted, Production(actual, actual.Lots))));

        TransformedLot[] edited = movedSuffix
            ? [new(kept.LotId, kept.CustodianId, kept.Definition, child.Output, kept.Revision, kept.Protection, kept.ReservedBy),
                new(child.LotId, child.CustodianId, child.Definition, kept.Output, child.Revision, child.Protection, child.ReservedBy)]
            : [new(kept.LotId, child.CustodianId, kept.Definition, kept.Output, kept.Revision, kept.Protection, kept.ReservedBy),
                new(child.LotId, kept.CustodianId, child.Definition, child.Output, child.Revision, child.Protection, child.ReservedBy)];
        ProductionSnapshot balanced = Production(actual, edited);
        // Counts, identities, producer rights and every storage total are unchanged and admitted.
        balanced.ValidateForAuthority(gifted.Inventory);
        Assert.Equal(actual.Lots.Select(value => value.LotId), balanced.Lots.Select(value => value.LotId));
        foreach (EntityId bag in new[] { helperBag, playerBag })
        {
            Assert.Equal(actual.GetStoredQuantity(bag), balanced.GetStoredQuantity(bag));
        }

        var root = ReadFrame(original);
        byte[] actualBytes = ProductionCodecV2.Encode(actual);
        byte[] balancedBytes = ProductionCodecV2.Encode(balanced);
        Assert.Equal(actualBytes, root[12]);
        root[12] = balancedBytes;
        // The physical owner carries the same production owner and the liquid cells pin that owner,
        // so both are rebound to the edited lots. Inventory, evidence and every other frame are kept.
        byte[] reservoirBytes = root[16].ToArray();
        Assert.True(ReplaceAll(reservoirBytes, actualBytes, balancedBytes) > 0);
        root[16] = reservoirBytes;
        LiquidReservoirState reservoirs = LiquidReservoirCodecV2.Decode(reservoirBytes,
            TestContext.Current.CancellationToken);
        Assert.Equal(balanced.ContentHash, reservoirs.Capacity.Production!.ContentHash);
        var topology = TerrainLiquidTopology.Create(reservoirs, gifted.Space.Regions, gifted.ReservoirSeals!);
        byte[] liquidBytes = root[18].ToArray();
        Assert.True(ReplaceAll(liquidBytes, gifted.Reservoirs!.ContentHash.ToByteArray(),
            reservoirs.ContentHash.ToByteArray()) > 0);
        ReplaceAll(liquidBytes, gifted.LiquidCells!.Topology!.ContentHash.ToByteArray(), topology.ContentHash.ToByteArray());
        root[18] = liquidBytes;

        var error = Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(WriteFrame(root)));
        var refusal = Assert.IsType<ArgumentException>(error.InnerException);
        Assert.Equal("Live produced lots must equal the partition replayed from retirements and ordered custody evidence.",
            refusal.Message);
        Assert.Equal(original, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(original)));
        Assert.Equal(gifted.ContentHash, session.ReadStateForHost().ContentHash);
    }

    private static ProductionSnapshot Production(ProductionSnapshot owner, IEnumerable<TransformedLot> lots) =>
        new(owner.WorldId, owner.BranchId, owner.Revision, owner.Recipes, owner.Batches, lots, owner.ConsumedItems);

    /// <summary>Replaces every exact occurrence with an equally long value, keeping all frame lengths.</summary>
    private static int ReplaceAll(byte[] bytes, byte[] prior, byte[] next)
    {
        Assert.Equal(prior.Length, next.Length);
        int count = 0;
        int from = 0;
        while (from <= bytes.Length - prior.Length)
        {
            int found = bytes.AsSpan(from).IndexOf(prior);
            if (found < 0)
            {
                break;
            }
            next.CopyTo(bytes, from + found);
            from += found + prior.Length;
            count++;
        }
        return count;
    }

    /// <summary>Complete world admission of the lived root with one equal replaced production owner.</summary>
    private static WorldSnapshot Readmit(WorldSnapshot world, ProductionSnapshot production) =>
        new(world.Inventory, world.Space, world.Residents, world.AdmissionVersion, world.Calendar,
            world.Revision, world.AdvancingSteps, world.Dialogues, world.Gathering, production, world.Progression,
            world.Quests, world.Construction, reservoirs: world.Reservoirs, reservoirSeals: world.ReservoirSeals,
            liquidCells: world.LiquidCells, excavation: world.Excavation, pacing: world.Pacing,
            encounterLifecycle: world.EncounterLifecycle, care: world.Care, resourceHistory: world.ResourceHistory,
            coordination: world.Coordination);
}
