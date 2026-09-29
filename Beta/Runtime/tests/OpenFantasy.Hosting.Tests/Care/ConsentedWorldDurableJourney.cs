using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>One accepted pair with the exact receipts and identities its commit published.</summary>
internal sealed record DurablePair(WorldStepBatch Batch, WorldActionCommand Offer, WorldActionCommand Acceptance,
    byte[] OfferReceipt, byte[] AcceptanceReceipt, ulong Sequence, IReadOnlyList<WorldCommittedIdentity> Identities)
{
    internal TrustedCommandDelivery OfferDelivery => new(Offer.PrincipalId, Offer.OriginSessionId);
    internal TrustedCommandDelivery AcceptanceDelivery => new(Acceptance.PrincipalId, Acceptance.OriginSessionId);
    internal EntityId Child => EntityId.DeriveSpawned(Offer.CommandId, 0);
}

/// <summary>One joint archive as the live owner returned it at publication.</summary>
internal sealed record DurableCut(WorldMaterialCareArchiveReference Reference, ulong PublicationSequence, byte[] ClosingBytes);

/// <summary>
/// One fresh consented world lived through the SQLite owner. Every gift, craft, use and refusal is
/// an accepted command; both joint cuts are published by the owner under actual transfer pressure.
/// </summary>
public sealed class ConsentedWorldDurableJourney : IDisposable
{
    internal const string EvidenceVariable = "OPENFANTASY_CONSENTED_DURABLE_EVIDENCE";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "openfantasy-consented-durable-" + Guid.NewGuid().ToString("N"));
    private readonly ConsentedWorldPublicationTests.DurableAccess _access = new();
    private readonly List<DurableCut> _cuts = [];
    private string FinalPath => Path.Combine(_directory, "final.sqlite3");
    private string BeforePath => Path.Combine(_directory, "before.sqlite3");
    private string AfterPath => Path.Combine(_directory, "after.sqlite3");

    public ConsentedWorldDurableJourney()
    {
        Directory.CreateDirectory(_directory);
        var ordinary = new OrdinaryCareWorldFixture(_access.OrdinaryGenesis);
        Actions = ordinary.Journey;
        Player = Actions.Player;
        Helper = Actions.Helper;
        WorldSnapshot genesis = WorldConsentedMaterialAdmission.CreateFresh(_access.OrdinaryGenesis);
        GenesisBytes = WorldSnapshotCodec.Encode(genesis);
        CoordinatedGenesisBytes = WorldSnapshotCodec.Encode(WorldCoordinatedAdmission.CreateFresh(_access.OrdinaryGenesis));
        PlayerBag = genesis.GetResident(Player).InventoryCustodianId;
        HelperBag = genesis.GetResident(Helper).InventoryCustodianId;
        string working = Path.Combine(_directory, "working.sqlite3");
        SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(working, genesis);
        try
        {
            var session = new CareWorldSession(authority);
            // Extra actual wolf damage leaves room for meaningful use on both sides of the first cut.
            WorldSnapshot state = ordinary.ReachFullDoseInjuredExploration(session);
            Refused = RefuseByPhysicalStorage(session);
            RawGift = Gift(session, SelectRawGift(authority, session.ReadStateForHost()));
            RawChild = RawGift.Child;
            RawReturn = Gift(session, WholeRawChild(session.ReadStateForHost()));

            state = session.ReadStateForHost();
            CountedSourceLot berries = state.Inventory.Custody.Lots.Single(value => value.CustodianId == HelperBag
                && _access.IsBerries(value));
            Craft = ConsentedWorldPublicationTests.DurableAccess.CraftFor(state, Actions, Helper, _access.Bulk,
                new(IngredientLotKind.Source, berries.LotId, berries.Revision, 12));
            state = CareRewardJourney.Commit(session, Actions.Batch(state, WorldBatchKind.AdvancingStep, [Craft])).Candidate;
            ProductionBatch producer = state.Production!.Batches.Single(value => value.CommandId == Craft.CommandId);
            TransformedLot stock = state.Production.Lots.Single(value => value.Output.BatchId == producer.BatchId);
            Assert.Equal((Helper, HelperBag, 4UL), (producer.ActorId, stock.CustodianId, stock.Quantity));
            Producer = producer.BatchId;
            Stock = stock.LotId;

            ProducedGift = Gift(session, new ConsentedMaterialTerms(state.WorldId, state.BranchId, Helper, Player,
                ConsentedMaterialLotKind.Produced, stock.LotId, stock.Definition, HelperBag, PlayerBag, 3, stock.Revision));
            state = session.ReadStateForHost();
            TransformedLot child = state.Production!.Lots.Single(value => value.LotId == ProducedGift.Child);
            Assert.Equal((PlayerBag, Producer, 0UL, 3UL),
                (child.CustodianId, child.Output.BatchId, child.Output.Start, child.Output.End));
            Assert.Equal(1UL, state.Production.Lots.Single(value => value.LotId == Stock).Quantity);
            (Repacked, Use) = RecraftAndUse(session);

            Cut = PublishCutByPressure(authority, session, capture: true);
            authority.Dispose();
            // The lived world continues only from real recovery of its own journal and archive.
            authority = SqliteWorldAuthority.OpenExisting(working, GenesisBytes);
            session = new CareWorldSession(authority);
            state = session.ReadStateForHost();
            Assert.Equal(CutAfterBytes, WorldSnapshotCodec.Encode(state));

            child = state.Production!.Lots.Single(value => value.LotId == ProducedGift.Child);
            Onward = Gift(session, new ConsentedMaterialTerms(state.WorldId, state.BranchId, Player, Helper,
                ConsentedMaterialLotKind.Produced, child.LotId, child.Definition, PlayerBag, HelperBag, 1, child.Revision));
            state = session.ReadStateForHost();
            TransformedLot returned = state.Production!.Lots.Single(value => value.LotId == Onward.Child);
            Assert.Equal((Producer, 1UL, 2UL, HelperBag),
                (returned.Output.BatchId, returned.Output.Start, returned.Output.End, returned.CustodianId));
            (LaterRepacked, LaterUse) = RecraftAndUse(session);
            Assert.DoesNotContain(session.ReadStateForHost().Production!.Lots, value => value.LotId == ProducedGift.Child);

            SecondCut = PublishCutByPressure(authority, session, capture: false);
            FinalState = session.ReadStateForHost();
            FinalBytes = WorldSnapshotCodec.Encode(FinalState);
            Assert.Equal((ulong)_cuts.Count, authority.InspectForHost().Journal.MaterialCareArchiveCount);
            authority.BackupForHost(FinalPath);
        }
        finally
        {
            authority.Dispose();
        }
        Export();
    }

    internal CareRewardJourney Actions { get; }
    internal EntityId Player { get; }
    internal EntityId Helper { get; }
    internal EntityId PlayerBag { get; }
    internal EntityId HelperBag { get; }
    internal byte[] GenesisBytes { get; }
    internal byte[] CoordinatedGenesisBytes { get; }
    internal DurablePair Refused { get; }
    internal DurablePair RawGift { get; }
    internal DurablePair RawReturn { get; }
    internal EntityId RawChild { get; }
    internal WorldActionCommand Craft { get; }
    internal EntityId Producer { get; }
    internal EntityId Stock { get; }
    internal DurablePair ProducedGift { get; }
    internal EntityId Repacked { get; }
    internal WorldActionCommand Use { get; }
    internal DurablePair Cut { get; }
    internal DurablePair Onward { get; }
    internal EntityId LaterRepacked { get; }
    internal WorldActionCommand LaterUse { get; }
    internal DurablePair SecondCut { get; }
    internal IReadOnlyList<DurableCut> Cuts => _cuts;
    internal int PressureGifts { get; private set; }
    internal WorldSnapshot CutBefore { get; private set; } = null!;
    internal ulong CutOrdinal { get; private set; }
    internal byte[] CutAfterBytes { get; private set; } = [];
    internal WorldSnapshot FinalState { get; }
    internal byte[] FinalBytes { get; }

    internal StorageFiles CopyFinal() => Copy(FinalPath);
    internal StorageFiles CopyBeforeCut() => Copy(BeforePath);

    /// <summary>The raw split child's whole current lot, offered by whichever participant now holds it.</summary>
    internal ConsentedMaterialTerms WholeRawChild(WorldSnapshot state)
    {
        CountedSourceLot lot = state.Inventory.Custody.GetLot(RawChild);
        EntityId sender = lot.CustodianId == PlayerBag ? Player : Helper;
        Assert.Equal(sender == Player ? PlayerBag : HelperBag, lot.CustodianId);
        return new(state.WorldId, state.BranchId, sender, sender == Player ? Helper : Player,
            ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, lot.CustodianId,
            sender == Player ? HelperBag : PlayerBag, lot.Quantity, lot.Revision);
    }

    private DurablePair Gift(CareWorldSession session, ConsentedMaterialTerms terms)
    {
        WorldSnapshot before = session.ReadStateForHost();
        var pair = ConsentedWorldPublicationTests.DurableAccess.PairFor(before, Actions, terms);
        WorldStepBatch batch = Actions.Batch(before, WorldBatchKind.AdvancingStep, [pair.Offer, pair.Acceptance]);
        WorldStepDecision accepted = CareRewardJourney.Commit(session, batch);
        Assert.Null(accepted.SealedMaterialCareSegment);
        Assert.All(accepted.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
        return Record(batch, pair.Offer, pair.Acceptance, accepted);
    }

    private static DurablePair Record(WorldStepBatch batch, WorldActionCommand offer, WorldActionCommand acceptance,
        WorldStepDecision decision)
    {
        Assert.Equal(2, decision.Receipts.Count);
        return new(batch, offer, acceptance,
            WorldReceiptCodecV1.Encode(decision.Receipts.Single(value => value.CommandId == offer.CommandId)),
            WorldReceiptCodecV1.Encode(decision.Receipts.Single(value => value.CommandId == acceptance.CommandId)),
            decision.Candidate.Revision, decision.Identities.ToArray());
    }

    /// <summary>
    /// The host previews real candidate gifts of finite non-recipe stock and selects the first one
    /// the current world accepts. A preview reserves nothing and publishes nothing.
    /// </summary>
    private ConsentedMaterialTerms SelectRawGift(SqliteWorldAuthority authority, WorldSnapshot state)
    {
        foreach ((EntityId sender, EntityId recipient) in new[] { (Helper, Player), (Player, Helper) })
        {
            EntityId source = state.GetResident(sender).InventoryCustodianId;
            foreach (CountedSourceLot lot in state.Inventory.Custody.Lots.Where(value => value.CustodianId == source
                && value.Quantity > 1 && value.ReservedBy is null && value.Protection == LotProtection.None
                && !_access.IsBerries(value)))
            {
                var terms = new ConsentedMaterialTerms(state.WorldId, state.BranchId, sender, recipient,
                    ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, source,
                    state.GetResident(recipient).InventoryCustodianId, 1, lot.Revision);
                var pair = ConsentedWorldPublicationTests.DurableAccess.PairFor(state, Actions, terms);
                WorldStepDecision preview = authority.Preview(Actions.Batch(state, WorldBatchKind.AdvancingStep,
                    [pair.Offer, pair.Acceptance]));
                if (preview.Receipts.All(receipt => receipt.Rejection == WorldActionRejection.None)) return terms;
            }
        }
        throw new InvalidOperationException("The fresh world must hold one giftable finite raw lot beside its recipe berries.");
    }

    /// <summary>A valid exact pair whose receiver cannot physically hold the goods; both owners roll back.</summary>
    private DurablePair RefuseByPhysicalStorage(CareWorldSession session)
    {
        WorldSnapshot before = session.ReadStateForHost();
        var reservoir = before.Reservoirs!;
        EntityId receiver = reservoir.Geometries.Single().CustodianId;
        Assert.Equal(Helper, before.Inventory.Policies.Single(value => value.CustodianId == receiver).OwnerActorId);
        CountedSourceLot stock = before.Inventory.Custody.Lots.First(value => value.CustodianId == PlayerBag
            && value.Quantity > 1 && value.Protection == LotProtection.None && value.ReservedBy is null
            && !value.Definition.Equals(reservoir.Capacity.Domain.Material.Item));
        var terms = new ConsentedMaterialTerms(before.WorldId, before.BranchId, Player, Helper,
            ConsentedMaterialLotKind.CountedSource, stock.LotId, stock.Definition, PlayerBag, receiver, 1, stock.Revision);
        var pair = ConsentedWorldPublicationTests.DurableAccess.PairFor(before, Actions, terms);
        var plan = new WorldConsentedMaterialPlanner().Plan(before, pair.Offer, pair.Acceptance,
            WorldBatchKind.AdvancingStep, Actions.Delivery(pair.Offer), Actions.Delivery(pair.Acceptance));
        Assert.True(plan.IsApplied);
        WorldMovementInput[] moves = new[] { pair.Offer, pair.Acceptance }.Select(command =>
            new WorldMovementInput(Actions.Delivery(command), command.ActorId, 32, 0)).ToArray();
        WorldStepBatch selected = Actions.Batch(before, WorldBatchKind.AdvancingStep, [pair.Offer, pair.Acceptance]);
        WorldStepDecision control = new WorldStepProcessor().Decide(before, new(selected.CauseId, before.WorldId,
            before.BranchId, before.Revision, before.AdvancingSteps, WorldBatchKind.AdvancingStep, moves, [],
            before.ProfileVersion));
        var batch = new WorldStepBatch(selected.CauseId, before.WorldId, before.BranchId, before.Revision,
            before.AdvancingSteps, WorldBatchKind.AdvancingStep, moves, selected.Actions, before.ProfileVersion);
        WorldStepDecision result = session.Commit(batch);
        Assert.Equal(2, result.Receipts.Count);
        Assert.All(result.Receipts, receipt => Assert.Equal(WorldActionRejection.Unavailable, receipt.Rejection));
        Assert.Equal(before.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, result.Candidate.Production!.ContentHash);
        Assert.Equal(reservoir.ContentHash, result.Candidate.Reservoirs!.ContentHash);
        Assert.Equal(before.Construction!.ContentHash, result.Candidate.Construction!.ContentHash);
        Assert.Equal(before.Coordination!.ConsentedMaterial!.ContentHash,
            result.Candidate.Coordination!.ConsentedMaterial!.ContentHash);
        // A refusal is an ordinary elapsed step: time advances and neither movement opportunity is spent.
        Assert.Equal((before.Revision + 1, before.AdvancingSteps + 1),
            (result.Candidate.Revision, result.Candidate.AdvancingSteps));
        foreach (WorldMovementInput movement in moves)
        {
            Assert.Equal(control.Candidate.Space.GetActor(movement.ActorId).Feet,
                result.Candidate.Space.GetActor(movement.ActorId).Feet);
        }
        Assert.DoesNotContain(result.Identities, identity => identity.Identity == EntityId.DeriveSpawned(pair.Offer.CommandId, 0));
        return Record(batch, pair.Offer, pair.Acceptance, result);
    }

    private (EntityId Batch, WorldActionCommand Use) RecraftAndUse(CareWorldSession session)
    {
        WorldSnapshot state = session.ReadStateForHost();
        TransformedLot child = state.Production!.Lots.Single(value => value.LotId == ProducedGift.Child);
        WorldActionCommand recraft = ConsentedWorldPublicationTests.DurableAccess.CraftFor(state, Actions, Player,
            _access.Repack, new(IngredientLotKind.Transformed, child.LotId, child.Revision, 1));
        state = CareRewardJourney.Commit(session, Actions.Batch(state, WorldBatchKind.AdvancingStep, [recraft])).Candidate;
        ProductionBatch repacked = state.Production!.Batches.Single(value => value.CommandId == recraft.CommandId);
        RetiredIngredient input = Assert.Single(repacked.Ingredients);
        Assert.Equal((child.LotId, Producer, child.Output.Start, child.Output.Start + 1),
            (input.LotId, input.Output!.BatchId, input.Output.Start, input.Output.End));
        TransformedLot dose = state.Production.Lots.Single(value => value.Output.BatchId == repacked.BatchId);
        var prepared = Assert.IsType<UseRestorativeItemIntent>(WorldRestorativeActions.Prepare(state,
            Player, Player, _access.Self.DefinitionId, null));
        var use = new UseRestorativeItemIntent(prepared.RecipientActorId, prepared.UseDefinitionId,
            prepared.UseDefinitionVersion, prepared.PermissionId, prepared.PermissionGrantVersion,
            prepared.ExpectedPermissionRevision, prepared.ExpectedPatientPresenceRevision,
            prepared.ExpectedPatientProgressionRevision, prepared.ExpectedStorageRevision,
            prepared.ExpectedProductionRevision, new(IngredientLotKind.Transformed, dose.LotId, dose.Revision, 1));
        int beforeHp = state.Progression!.GetActor(Player).CurrentHp;
        WorldActionCommand consume = Actions.Command(state, Player, use);
        state = CareRewardJourney.Commit(session, Actions.Batch(state, WorldBatchKind.AdvancingStep, [consume])).Candidate;
        Assert.True(state.Progression!.GetActor(Player).CurrentHp > beforeHp);
        Assert.DoesNotContain(state.Production!.Lots, value => value.LotId == dose.LotId);
        var consumed = state.Production.ConsumedItems.Single(value => value.CommandId == consume.CommandId);
        Assert.Equal(repacked.BatchId, consumed.Ingredient.Output!.BatchId);
        return (repacked.BatchId, consume);
    }

    /// <summary>
    /// Real whole-lot gifts fill the bounded transfer window. The owner itself closes the complete
    /// root before the gift that no longer fits; nothing here seals, shortens or injects a segment.
    /// </summary>
    private DurablePair PublishCutByPressure(SqliteWorldAuthority authority, CareWorldSession session, bool capture)
    {
        for (int gift = 0; gift <= WorldConsentedMaterialHistory.MaximumTransfers; gift++)
        {
            WorldSnapshot before = session.ReadStateForHost();
            var pair = ConsentedWorldPublicationTests.DurableAccess.PairFor(before, Actions, WholeRawChild(before));
            WorldStepBatch batch = Actions.Batch(before, WorldBatchKind.AdvancingStep, [pair.Offer, pair.Acceptance]);
            WorldStepDecision preview = authority.Preview(batch);
            Assert.Equal(before.ContentHash, authority.ReadStateForHost().ContentHash);
            bool publishes = preview.SealedMaterialCareSegment is not null;
            if (publishes && capture)
            {
                authority.BackupForHost(BeforePath);
                CutBefore = before;
                CutOrdinal = preview.SealedMaterialCareSegment!.Reference.Ordinal;
            }
            WorldStepDecision accepted = CareRewardJourney.Commit(session, batch);
            Assert.Equal(preview.Candidate.ContentHash, accepted.Candidate.ContentHash);
            Assert.Equal(publishes, accepted.SealedMaterialCareSegment is not null);
            PressureGifts++;
            if (accepted.SealedMaterialCareSegment is not { } archive) continue;
            Assert.Equal(WorldSnapshotCodec.Encode(before), archive.Encode());
            Assert.Equal(before.Revision, archive.Reference.BoundarySequence);
            Assert.Equal((ulong)_cuts.Count, archive.Reference.Ordinal);
            _cuts.Add(new(archive.Reference, accepted.Candidate.Revision, archive.Encode()));
            if (capture)
            {
                CutAfterBytes = WorldSnapshotCodec.Encode(accepted.Candidate);
                authority.BackupForHost(AfterPath);
            }
            return Record(batch, pair.Offer, pair.Acceptance, accepted);
        }
        throw new InvalidOperationException("Actual transfer pressure must publish a joint cut within one bounded window.");
    }

    private static StorageFiles Copy(string path)
    {
        var files = new StorageFiles();
        File.Copy(path, files.Path);
        return files;
    }

    /// <summary>A disposable self-contained fixture for separate real-process loss validation.</summary>
    private void Export()
    {
        if (Environment.GetEnvironmentVariable(EvidenceVariable) is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root);
        File.Copy(BeforePath, Path.Combine(root, "before.sqlite3"), overwrite: true);
        File.Copy(AfterPath, Path.Combine(root, "after.sqlite3"), overwrite: true);
        File.Copy(FinalPath, Path.Combine(root, "final.sqlite3"), overwrite: true);
        File.WriteAllBytes(Path.Combine(root, "genesis.ofc"), GenesisBytes);
        File.WriteAllBytes(Path.Combine(root, "before.ofc"), WorldSnapshotCodec.Encode(CutBefore));
        File.WriteAllBytes(Path.Combine(root, "batch.ofc"), WorldStepCodec.Encode(Cut.Batch));
        File.WriteAllBytes(Path.Combine(root, "after.ofc"), CutAfterBytes);
        File.WriteAllBytes(Path.Combine(root, "final.ofc"), FinalBytes);
        File.WriteAllBytes(Path.Combine(root, "archive.ofc"), _cuts[(int)CutOrdinal].ClosingBytes);
        File.WriteAllBytes(Path.Combine(root, "offer-command.ofc"), WorldActionCodec.Encode(Cut.Offer));
        File.WriteAllBytes(Path.Combine(root, "acceptance-command.ofc"), WorldActionCodec.Encode(Cut.Acceptance));
        File.WriteAllBytes(Path.Combine(root, "offer-receipt.ofc"), Cut.OfferReceipt);
        File.WriteAllBytes(Path.Combine(root, "acceptance-receipt.ofc"), Cut.AcceptanceReceipt);
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(Path.Combine(root, "identities.json"), JsonSerializer.Serialize(Cut.Identities.Select(value => new
        {
            identity = Convert.ToHexString(value.Identity.ToByteArray()).ToLowerInvariant(),
            kind = value.Kind.ToString(), sequence = Cut.Sequence
        }), options));
        File.WriteAllText(Path.Combine(root, "metadata.json"), JsonSerializer.Serialize(new
        {
            profileVersion = 28, schemaVersion = WorldDatabaseSchemaEight.Version,
            genesisHash = ContentHashOf(GenesisBytes), beforeRevision = CutBefore.Revision,
            beforeHash = CutBefore.ContentHash.Value, afterRevision = Cut.Sequence,
            afterHash = ContentHashOf(CutAfterBytes), cutOrdinal = CutOrdinal,
            archiveBoundaryHash = _cuts[(int)CutOrdinal].Reference.BoundaryHash.Value,
            finalRevision = FinalState.Revision, finalHash = FinalState.ContentHash.Value,
            finalArchiveCount = _cuts.Count, pressureGifts = PressureGifts,
            offerPrincipal = Convert.ToHexString(Cut.Offer.PrincipalId.ToByteArray()).ToLowerInvariant(),
            offerCommand = Convert.ToHexString(Cut.Offer.CommandId.ToByteArray()).ToLowerInvariant(),
            acceptancePrincipal = Convert.ToHexString(Cut.Acceptance.PrincipalId.ToByteArray()).ToLowerInvariant(),
            acceptanceCommand = Convert.ToHexString(Cut.Acceptance.CommandId.ToByteArray()).ToLowerInvariant(),
            note = "Fresh profile-28 world. batch.ofc applied to before.sqlite3 publishes after.ofc, both receipts, "
                + "the listed identities and archive.ofc in one transaction. Exception injection is not process-loss proof."
        }, options));
    }

    private static string ContentHashOf(byte[] bytes) => OpenFantasy.Core.Content.ContentHash.Compute(bytes).Value;

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
