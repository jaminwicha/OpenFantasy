using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>
/// Qualifies the SQLite owner's binding of a fresh consented world. Receipt lookup here is local
/// authorization of an independently supplied delivery; it is not transport authentication.
/// </summary>
public sealed class ConsentedWorldDurableTests(ConsentedWorldDurableJourney journey)
    : IClassFixture<ConsentedWorldDurableJourney>
{
    private const string RefusedLookup = "The delivery cannot access this actor command stream.";
    private const string StaleBatch = "The selected batch does not match the current world revision or hold.";
    private const string RefusedBatch = "The selected batch cannot be committed in the current world.";
    private const string ReplayContract = "The local world journal violates its replay contract.";
    private const string ArchiveDiffers = "A stored material and care archive differs from its exact replay boundary.";
    private const string ArchiveHash = "Stored material and care archive bytes do not match their boundary hash.";

    private DurablePair[] Pairs => [journey.Refused, journey.RawGift, journey.RawReturn, journey.ProducedGift,
        journey.Cut, journey.Onward, journey.SecondCut];

    [Fact]
    public void Durable_crafted_gift_journey_reopens_its_exact_root_archives_receipts_and_identities()
    {
        using var files = journey.CopyFinal();
        byte[] databaseBytes = File.ReadAllBytes(files.Path);
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path, journey.GenesisBytes))
        {
            WorldSnapshot state = authority.ReadStateForHost();
            Assert.Equal(journey.FinalBytes, WorldSnapshotCodec.Encode(state));
            Assert.Equal((ushort)28, state.ProfileVersion);
            AssertJoinedOwners(journey.FinalState, state);
            WorldJournalUsage usage = authority.InspectForHost().Journal;
            Assert.Equal((2UL, 0UL, 0UL),
                (usage.MaterialCareArchiveCount, usage.ResourceArchiveCount, usage.ResourceArchiveBytes));
            Assert.Equal((ulong)journey.Cuts.Sum(cut => (long)cut.ClosingBytes.Length), usage.MaterialCareArchiveBytes);

            var closing = new List<WorldSnapshot>();
            var continued = new List<WorldSnapshot>();
            for (ulong ordinal = 0; ordinal < usage.MaterialCareArchiveCount; ordinal++)
            {
                RecordedWorldMaterialCareArchive actual = authority.ReadMaterialCareArchiveForHost(ordinal);
                DurableCut cut = journey.Cuts[(int)ordinal];
                Assert.Equal(cut.Reference, actual.Segment.Reference);
                Assert.Equal(cut.PublicationSequence, actual.PublicationSequence);
                Assert.Equal(cut.ClosingBytes, actual.Segment.Encode());
                Assert.Equal((state.Revision, state.ContentHash), (actual.SourceHeadRevision, actual.SourceHeadHash));
                // The closing root is the replayed world at that boundary, not a recomputed seal.
                RecordedWorldState boundary = authority.ReadRecordedStateForHost(cut.Reference.BoundarySequence);
                Assert.Equal(cut.ClosingBytes, WorldSnapshotCodec.Encode(boundary.State));
                Assert.Equal(cut.Reference.BoundaryHash, boundary.StateHash);
                closing.Add(boundary.State);
                continued.Add(actual.Segment.Continued);
            }
            Assert.Equal(journey.CutBefore.ContentHash, closing[(int)journey.CutOrdinal].ContentHash);
            AssertFirstSegment(closing[0], continued[0]);
            AssertSecondSegment(closing[1], continued[1]);
            AssertSurvivingStock(state);

            foreach (DurablePair pair in Pairs) AssertReceipts(authority, pair);
            var fresh = ConsentedWorldPublicationTests.DurableAccess.PairFor(state, journey.Actions,
                journey.WholeRawChild(state));
            var freshSender = new TrustedCommandDelivery(fresh.Offer.PrincipalId, fresh.Offer.OriginSessionId);
            var freshRecipient = new TrustedCommandDelivery(fresh.Acceptance.PrincipalId, fresh.Acceptance.OriginSessionId);
            // A wrong delivery learns nothing: the refusal is the same whether or not an outcome exists.
            AssertRefusedLookup(authority, fresh.Offer, freshRecipient);
            AssertRefusedLookup(authority, fresh.Acceptance, freshSender);
            Assert.Null(authority.FindReceipt(fresh.Offer, freshSender));
            Assert.Null(authority.FindReceipt(fresh.Acceptance, freshRecipient));

            foreach (DurablePair pair in Pairs)
            {
                Assert.Equal(StaleBatch, Assert.Throws<AuthorityDispatchException>(() => authority.Commit(pair.Batch)).Message);
            }
            EntityId[] closed = [journey.ProducedGift.Offer.CommandId, journey.ProducedGift.Acceptance.CommandId,
                journey.ProducedGift.Child, journey.Onward.Child, journey.Refused.Offer.CommandId,
                journey.Refused.Acceptance.CommandId, journey.Cut.Batch.CauseId];
            foreach (EntityId closedId in closed)
            {
                Assert.True(authority.IsIdentityOccupiedForHost(closedId));
                var reused = new WorldActionCommand(closedId, fresh.Offer.PrincipalId, fresh.Offer.OriginSessionId,
                    fresh.Offer.ActorId, state.WorldId, state.BranchId, fresh.Offer.ExpectedActorRevision, fresh.Offer.Intent);
                WorldActionCommand accepted = journey.Actions.Command(state, fresh.Acceptance.ActorId,
                    AcceptMaterialIntent.Approving(reused, state.Space.GetActor(fresh.Offer.ActorId).Revision));
                Assert.Equal(RefusedBatch, Assert.Throws<AuthorityDispatchException>(() => authority.Commit(
                    journey.Actions.Batch(state, WorldBatchKind.AdvancingStep, [reused, accepted]))).Message);
                Assert.Equal(journey.FinalBytes, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            }
            // The refused pair spawned nothing, and the same goods remain giftable under unused identities.
            Assert.False(authority.IsIdentityOccupiedForHost(journey.Refused.Child));
            Assert.All(authority.Preview(journey.Actions.Batch(state, WorldBatchKind.AdvancingStep,
                [fresh.Offer, fresh.Acceptance])).Receipts,
                receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
            Assert.Equal(journey.FinalBytes, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        }
        Assert.Equal(databaseBytes, File.ReadAllBytes(files.Path));

        using var database = WorldDatabase.Open(files.Path, create: false);
        Assert.Equal(WorldDatabaseSchemaEight.Version, database.SchemaVersion);
        var identities = database.ReadIdentities().ToDictionary(value => value.Identity);
        var outcomes = database.ReadCommandOutcomes().ToDictionary(value => (value.PrincipalId, value.CommandId));
        foreach (DurablePair pair in Pairs) AssertStoredRows(identities, outcomes, pair);
        foreach (DurablePair split in new[] { journey.RawGift, journey.ProducedGift, journey.Onward })
        {
            Assert.Equal(new StoredWorldIdentity(split.Child, split.Sequence, WorldIdentityKind.SpawnedEntity),
                identities[split.Child]);
        }
        Assert.False(identities.ContainsKey(journey.Refused.Child));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Durable_paired_cut_commits_or_rolls_back_head_receipts_identities_and_archive_together(bool afterCommit)
    {
        DurablePair cut = journey.Cut;
        byte[] before = WorldSnapshotCodec.Encode(journey.CutBefore);
        using var files = journey.CopyBeforeCut();
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path, journey.GenesisBytes))
        {
            Assert.Equal(before, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            Assert.Throws<IOException>(() => authority.Commit(cut.Batch, boundary =>
            {
                if (boundary == (afterCommit ? AuthorityCommitBoundary.AfterCommit : AuthorityCommitBoundary.BeforeCommit))
                    throw new IOException("Injected loss at the consented publication boundary.");
            }));
            Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
        }
        using (var database = WorldDatabase.Open(files.Path, create: false))
        {
            var identities = database.ReadIdentities().ToDictionary(value => value.Identity);
            var outcomes = database.ReadCommandOutcomes().ToDictionary(value => (value.PrincipalId, value.CommandId));
            var archives = database.ReadMaterialCareArchives().ToArray();
            StoredWorldHead head = database.ReadHead();
            Assert.Equal(afterCommit ? journey.CutAfterBytes : before, head.State);
            Assert.Equal(afterCommit ? cut.Sequence : journey.CutBefore.Revision, head.Sequence);
            Assert.Equal(journey.CutOrdinal + (afterCommit ? 1UL : 0UL), (ulong)archives.Length);
            if (afterCommit)
            {
                AssertStoredRows(identities, outcomes, cut);
                Assert.Equal(journey.Cuts[(int)journey.CutOrdinal].ClosingBytes, archives[^1].State);
                Assert.Equal((journey.Cuts[(int)journey.CutOrdinal].Reference, cut.Sequence),
                    (archives[^1].Reference, archives[^1].PublicationSequence));
            }
            else
            {
                Assert.All(cut.Identities, value => Assert.False(identities.ContainsKey(value.Identity)));
                Assert.False(outcomes.ContainsKey((cut.Offer.PrincipalId, cut.Offer.CommandId)));
                Assert.False(outcomes.ContainsKey((cut.Acceptance.PrincipalId, cut.Acceptance.CommandId)));
            }
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path, journey.GenesisBytes);
        Assert.Equal(afterCommit ? journey.CutAfterBytes : before, WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
        Assert.Equal(journey.CutOrdinal + (afterCommit ? 1UL : 0UL),
            recovered.InspectForHost().Journal.MaterialCareArchiveCount);
        Assert.All(cut.Identities, value => Assert.Equal(afterCommit, recovered.IsIdentityOccupiedForHost(value.Identity)));
        AssertRefusedLookup(recovered, cut.Offer, cut.AcceptanceDelivery);
        AssertRefusedLookup(recovered, cut.Acceptance, cut.OfferDelivery);
        if (afterCommit)
        {
            AssertReceipts(recovered, cut);
            Assert.Equal(journey.Cuts[(int)journey.CutOrdinal].ClosingBytes,
                recovered.ReadMaterialCareArchiveForHost(journey.CutOrdinal).Segment.Encode());
            Assert.Equal(StaleBatch, Assert.Throws<AuthorityDispatchException>(() => recovered.Commit(cut.Batch)).Message);
            Assert.Equal(journey.CutAfterBytes, WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
        }
        else
        {
            Assert.Null(recovered.FindReceipt(cut.Offer, cut.OfferDelivery));
            Assert.Null(recovered.FindReceipt(cut.Acceptance, cut.AcceptanceDelivery));
            Assert.Throws<ArgumentOutOfRangeException>(() => recovered.ReadMaterialCareArchiveForHost(journey.CutOrdinal));
            // Positive control: the identical batch publishes everything once on the recovered world.
            WorldStepDecision published = recovered.Commit(cut.Batch);
            Assert.Equal(journey.CutAfterBytes, WorldSnapshotCodec.Encode(published.Candidate));
            Assert.Equal(journey.Cuts[(int)journey.CutOrdinal].ClosingBytes, published.SealedMaterialCareSegment!.Encode());
            AssertReceipts(recovered, cut);
        }
    }

    [Theory]
    [InlineData("missing-archive", "The world is missing a required material and care archive.")]
    [InlineData("modified-archive", ArchiveHash)]
    [InlineData("archive-hash", ArchiveHash)]
    [InlineData("archive-ordinal", ArchiveDiffers)]
    [InlineData("archive-scope", ArchiveDiffers)]
    [InlineData("self-consistent-archive", ArchiveDiffers)]
    [InlineData("missing-command", "The retained actor-command table is incomplete.")]
    [InlineData("altered-receipt", "A stored actor command or receipt differs from its replayed commit.")]
    [InlineData("missing-identity", "The retained world-identity table is incomplete.")]
    [InlineData("altered-identity", "A retained identity differs from its replayed commit or role.")]
    [InlineData("foreign-head", "The stored world head does not match exact replay.")]
    [InlineData("substituted-genesis", "The local world journal is discontinuous.")]
    public void Durable_corruption_blocks_recovery_and_historical_selection(string corruption, string reason)
    {
        using var files = journey.CopyFinal();
        byte[] last = WorldStorageBinary.Number((ulong)journey.Cuts.Count - 1);
        DurablePair gift = journey.ProducedGift;
        switch (corruption)
        {
            case "missing-archive":
                Mutate(files, "DELETE FROM world_material_care_segments WHERE ordinal=$last", ("$last", last));
                break;
            case "modified-archive":
                Mutate(files, "UPDATE world_material_care_segments SET state=zeroblob(length(state)) WHERE ordinal=$last",
                    ("$last", last));
                break;
            case "archive-hash":
                Mutate(files, "UPDATE world_material_care_segments SET boundary_hash=zeroblob(32) WHERE ordinal=$last",
                    ("$last", last));
                break;
            case "archive-ordinal":
                Mutate(files, "UPDATE world_material_care_segments SET ordinal=$other WHERE ordinal=$last",
                    ("$other", WorldStorageBinary.Number(0xffff)), ("$last", last));
                break;
            case "archive-scope":
                // A well-formed identity of this same world that is not its world scope.
                Mutate(files, "UPDATE world_material_care_segments SET world_id=$other WHERE ordinal=$last",
                    ("$other", journey.Player.ToByteArray()), ("$last", last));
                break;
            case "self-consistent-archive":
                // A genuine complete root of this world with its own recomputed hash, at the wrong boundary.
                byte[] foreign = journey.Cuts[0].ClosingBytes;
                Mutate(files, "UPDATE world_material_care_segments SET state=$state,boundary_hash=$hash WHERE ordinal=$last",
                    ("$state", foreign), ("$hash", ContentHash.Compute(foreign).ToByteArray()), ("$last", last));
                break;
            case "missing-command":
                Mutate(files, "DELETE FROM world_command_outcomes WHERE principal=$principal AND command_id=$command",
                    ("$principal", gift.Acceptance.PrincipalId.ToByteArray()), ("$command", gift.Acceptance.CommandId.ToByteArray()));
                break;
            case "altered-receipt":
                // The other participant's genuine receipt is still not this command's receipt.
                Mutate(files, "UPDATE world_command_outcomes SET receipt=$receipt WHERE principal=$principal AND command_id=$command",
                    ("$receipt", gift.AcceptanceReceipt), ("$principal", gift.Offer.PrincipalId.ToByteArray()),
                    ("$command", gift.Offer.CommandId.ToByteArray()));
                break;
            case "missing-identity":
                Mutate(files, "DELETE FROM world_occupied_identity WHERE identity=$identity",
                    ("$identity", gift.Child.ToByteArray()));
                break;
            case "altered-identity":
                Mutate(files, "UPDATE world_occupied_identity SET kind=$kind WHERE identity=$identity",
                    ("$kind", (int)WorldIdentityKind.Command), ("$identity", gift.Child.ToByteArray()));
                break;
            case "foreign-head":
                Mutate(files, "UPDATE world_head SET state=$state,hash=$hash WHERE singleton=1",
                    ("$state", journey.CoordinatedGenesisBytes),
                    ("$hash", ContentHash.Compute(journey.CoordinatedGenesisBytes).ToByteArray()));
                break;
            case "substituted-genesis":
                // The same fresh basis under the older coordinated interpretation, with matching checksums.
                byte[] hash = ContentHash.Compute(journey.CoordinatedGenesisBytes).ToByteArray();
                Mutate(files, "UPDATE world_genesis SET state=$state,hash=$hash WHERE singleton=1",
                    ("$state", journey.CoordinatedGenesisBytes), ("$hash", hash));
                Mutate(files, "UPDATE world_checkpoints SET state=$state,hash=$hash WHERE sequence=$zero",
                    ("$state", journey.CoordinatedGenesisBytes), ("$hash", hash), ("$zero", WorldStorageBinary.Number(0)));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(corruption));
        }
        Assert.Equal(reason, Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path)).Message);
        Assert.Equal(reason, Assert.Throws<InvalidDataException>(() =>
            SqliteWorldAuthority.OpenExisting(files.Path, journey.GenesisBytes)).Message);
        using var database = WorldDatabase.Open(files.Path, create: false);
        Assert.Equal(reason, Assert.Throws<InvalidDataException>(() =>
            new WorldJournalRecovery().ReadMaterialCareArchive(database, 0)).Message);
        Assert.Equal(reason, Assert.Throws<InvalidDataException>(() =>
            new WorldJournalRecovery().ReadRecordedState(database, 1)).Message);
    }

    [Fact]
    public void Durable_mixed_step_profile_and_wrong_expected_genesis_are_refused_beside_valid_controls()
    {
        using (var control = journey.CopyFinal())
        {
            // Valid controls: the exact genesis selects this save, and its first commit is selectable.
            using (var authority = SqliteWorldAuthority.OpenExisting(control.Path, journey.GenesisBytes))
            {
                Assert.Equal(journey.FinalBytes, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            }
            Assert.Equal("This save did not begin from the expected genesis.", Assert.Throws<InvalidDataException>(() =>
                SqliteWorldAuthority.OpenExisting(control.Path, journey.CoordinatedGenesisBytes)).Message);
            using var database = WorldDatabase.Open(control.Path, create: false);
            Assert.Equal(1UL, new WorldJournalRecovery().ReadRecordedState(database, 1).State.Revision);
        }

        using var files = journey.CopyFinal();
        StoredWorldCommit first;
        using (var database = WorldDatabase.Open(files.Path, create: false))
        {
            first = database.ReadJournal().First();
        }
        WorldStepBatch accepted = WorldStepCodec.Decode(first.AcceptedBatch);
        Assert.Equal((ushort)28, accepted.ProfileVersion);
        // The same selected inputs under the older coordinated step profile.
        byte[] mixed = WorldStepCodec.Encode(new WorldStepBatch(accepted.CauseId, accepted.WorldId, accepted.BranchId,
            accepted.ExpectedRevision, accepted.ExpectedAdvancingSteps, accepted.Kind, accepted.Movement,
            accepted.Actions, 27));
        Mutate(files, "UPDATE world_commits SET accepted_batch=$batch WHERE sequence=$sequence",
            ("$batch", mixed), ("$sequence", WorldStorageBinary.Number(first.Sequence)));
        AssertMixedProfile(Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path)));
        using var damaged = WorldDatabase.Open(files.Path, create: false);
        AssertMixedProfile(Assert.Throws<InvalidDataException>(() =>
            new WorldJournalRecovery().ReadMaterialCareArchive(damaged, 0)));
        AssertMixedProfile(Assert.Throws<InvalidDataException>(() =>
            new WorldJournalRecovery().ReadRecordedState(damaged, 1)));
    }

    [Fact]
    public void Durable_company_publishes_two_pairs_and_a_refused_pair_before_and_after_reopening()
    {
        var company = new ConsentedCompanyFixture();
        var fixture = new OrdinaryCareWorldFixture(company.Create());
        CareRewardJourney actions = fixture.Journey;
        WorldSnapshot genesis = WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis);
        byte[] genesisBytes = WorldSnapshotCodec.Encode(genesis);
        using var files = new StorageFiles();
        var published = new List<(WorldStepBatch Batch, WorldStepDecision Decision)>();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            var session = new CareWorldSession(authority);
            actions.Approach(session);
            published.Add(PublishCompany(authority, session, actions, company, 501));
            published.Add(RefuseCompany(authority, session, actions, company, 502));
            foreach (var entry in published) AssertCompanyReceipts(authority, entry.Batch, entry.Decision);
        }
        byte[] closed = File.ReadAllBytes(files.Path);
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path, genesisBytes))
        {
            Assert.Equal(published[^1].Decision.Candidate.ContentHash, authority.ReadStateForHost().ContentHash);
            foreach (var entry in published)
            {
                AssertCompanyReceipts(authority, entry.Batch, entry.Decision);
                Assert.Equal(StaleBatch, Assert.Throws<AuthorityDispatchException>(() => authority.Commit(entry.Batch)).Message);
            }
            Assert.Equal(published[^1].Decision.Candidate.ContentHash, authority.ReadStateForHost().ContentHash);
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path, genesisBytes))
        {
            var session = new CareWorldSession(authority);
            published.Add(PublishCompany(authority, session, actions, company, 503));
            published.Add(RefuseCompany(authority, session, actions, company, 504));
        }
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path, genesisBytes))
        {
            WorldSnapshot state = authority.ReadStateForHost();
            Assert.Equal(WorldSnapshotCodec.Encode(published[^1].Decision.Candidate), WorldSnapshotCodec.Encode(state));
            Assert.Equal(4, state.Coordination!.ConsentedMaterial!.Transfers.Count);
            foreach (var entry in published) AssertCompanyReceipts(authority, entry.Batch, entry.Decision);
        }
        using var database = WorldDatabase.Open(files.Path, create: false);
        var identities = database.ReadIdentities().ToDictionary(value => value.Identity);
        foreach (var entry in published)
        {
            Assert.All(entry.Decision.Identities, value => Assert.Equal(new StoredWorldIdentity(value.Identity,
                entry.Decision.Candidate.Revision, (WorldIdentityKind)(int)value.Kind), identities[value.Identity]));
            foreach (WorldActionCommand offer in entry.Batch.Actions.Where(value => value.Intent is OfferMaterialIntent))
            {
                bool applied = entry.Decision.Receipts.Single(value => value.CommandId == offer.CommandId).Rejection
                    == WorldActionRejection.None;
                Assert.Equal(applied, identities.ContainsKey(EntityId.DeriveSpawned(offer.CommandId, 0)));
            }
        }
    }

    private static (WorldStepBatch Batch, WorldStepDecision Decision) PublishCompany(SqliteWorldAuthority authority,
        CareWorldSession session, CareRewardJourney actions, ConsentedCompanyFixture company, ulong cause)
    {
        WorldSnapshot before = session.ReadStateForHost();
        var first = ConsentedWorldPublicationTests.DurableAccess.PairFor(before, actions,
            ConsentedWorldPublicationTests.DurableAccess.RawTermsFor(before, actions.Helper, actions.Player));
        var second = ConsentedWorldPublicationTests.DurableAccess.PairFor(before, actions,
            ConsentedWorldPublicationTests.DurableAccess.RawTermsFor(before, company.First, company.Second));
        WorldActionCommand[] commands = [first.Offer, first.Acceptance, second.Offer, second.Acceptance];
        WorldMovementInput[] movement = commands.Select(command => new WorldMovementInput(
            actions.Delivery(command), command.ActorId, 32, 0)).ToArray();
        var batch = new WorldStepBatch(new EntityId(0xc028c04a00000000, cause), before.WorldId, before.BranchId,
            before.Revision, before.AdvancingSteps, WorldBatchKind.AdvancingStep, movement, commands.Reverse(),
            before.ProfileVersion);
        int transfers = before.Coordination!.ConsentedMaterial!.Transfers.Count;
        WorldStepDecision result = CareRewardJourney.Commit(session, batch);
        Assert.Equal(4, result.Receipts.Count);
        Assert.All(result.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
        Assert.Equal(transfers + 2, result.Candidate.Coordination!.ConsentedMaterial!.Transfers.Count);
        foreach (var pair in new[] { first, second })
        {
            ConsentedMaterialTerms terms = ((OfferMaterialIntent)pair.Offer.Intent).Terms;
            WorldConsentedTransferEvidence evidence = Assert.Single(result.Candidate.Coordination.ConsentedMaterial.Transfers,
                value => value.CommandId == pair.Offer.CommandId);
            Assert.Equal(pair.Acceptance.CommandId, evidence.AcceptanceId);
            Assert.Equal(terms.DestinationCustodianId, result.Candidate.Inventory.Custody.GetLot(evidence.MovedLotId).CustodianId);
            Assert.Equal(before.Inventory.Custody.GetLot(terms.LotId).Quantity - 1,
                result.Candidate.Inventory.Custody.GetLot(terms.LotId).Quantity);
        }
        // A successful gift spends each participant's movement opportunity of that step.
        Assert.All(commands, command => Assert.Equal(before.Space.GetActor(command.ActorId).Feet,
            result.Candidate.Space.GetActor(command.ActorId).Feet));
        Assert.Equal(result.Candidate.ContentHash, authority.ReadStateForHost().ContentHash);
        return (batch, result);
    }

    private static (WorldStepBatch Batch, WorldStepDecision Decision) RefuseCompany(SqliteWorldAuthority authority,
        CareWorldSession session, CareRewardJourney actions, ConsentedCompanyFixture company, ulong cause)
    {
        WorldSnapshot before = session.ReadStateForHost();
        // The second member's own untouched fresh stock; the first member's lot is spent by real gifts.
        ConsentedMaterialTerms current = ConsentedWorldPublicationTests.DurableAccess.RawTermsFor(before,
            company.Second, company.First);
        // Both members approve the same exact terms, which name an obsolete revision of the real lot.
        var stale = new ConsentedMaterialTerms(current.WorldId, current.BranchId, current.SenderActorId,
            current.RecipientActorId, current.LotKind, current.LotId, current.Definition, current.SourceCustodianId,
            current.DestinationCustodianId, current.Quantity, current.ExpectedLotRevision + 1);
        var pair = ConsentedWorldPublicationTests.DurableAccess.PairFor(before, actions, stale);
        WorldActionCommand[] commands = [pair.Offer, pair.Acceptance];
        WorldMovementInput[] movement = commands.Select(command => new WorldMovementInput(
            actions.Delivery(command), command.ActorId, 32, 0)).ToArray();
        var batch = new WorldStepBatch(new EntityId(0xc028c04a00000000, cause), before.WorldId, before.BranchId,
            before.Revision, before.AdvancingSteps, WorldBatchKind.AdvancingStep, movement, commands, before.ProfileVersion);
        WorldStepDecision control = new WorldStepProcessor().Decide(before, new(new EntityId(0xc028c04a00000000, cause + 1_000),
            before.WorldId, before.BranchId, before.Revision, before.AdvancingSteps, WorldBatchKind.AdvancingStep,
            movement, [], before.ProfileVersion));
        WorldStepDecision result = session.Commit(batch);
        Assert.Equal(2, result.Receipts.Count);
        Assert.All(result.Receipts, receipt => Assert.Equal(WorldActionRejection.RevisionConflict, receipt.Rejection));
        Assert.Equal(before.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, result.Candidate.Production!.ContentHash);
        Assert.Equal(before.Reservoirs!.ContentHash, result.Candidate.Reservoirs!.ContentHash);
        Assert.Equal(before.Coordination!.ConsentedMaterial!.ContentHash,
            result.Candidate.Coordination!.ConsentedMaterial!.ContentHash);
        // Normal refusal time: the step elapses and both refused members keep their selected movement.
        Assert.Equal((before.Revision + 1, before.AdvancingSteps + 1),
            (result.Candidate.Revision, result.Candidate.AdvancingSteps));
        foreach (WorldMovementInput move in movement)
        {
            Assert.NotEqual(before.Space.GetActor(move.ActorId).Feet, result.Candidate.Space.GetActor(move.ActorId).Feet);
            Assert.Equal(control.Candidate.Space.GetActor(move.ActorId).Feet,
                result.Candidate.Space.GetActor(move.ActorId).Feet);
        }
        Assert.DoesNotContain(result.Identities, value => value.Kind == WorldCommittedIdentityKind.SpawnedEntity);
        Assert.Equal(result.Candidate.ContentHash, authority.ReadStateForHost().ContentHash);
        return (batch, result);
    }

    private static void AssertCompanyReceipts(SqliteWorldAuthority authority, WorldStepBatch batch, WorldStepDecision decision)
    {
        Assert.Equal(batch.Actions.Count, decision.Receipts.Count);
        foreach (WorldActionCommand command in batch.Actions)
        {
            byte[] expected = WorldReceiptCodecV1.Encode(decision.Receipts.Single(value => value.CommandId == command.CommandId));
            var receipt = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command,
                new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId)));
            Assert.Equal(expected, WorldReceiptCodecV1.Encode(receipt));
            Assert.True(authority.IsIdentityOccupiedForHost(command.CommandId));
            // No other member's current delivery reaches this command stream.
            foreach (WorldActionCommand other in batch.Actions.Where(value => value.CommandId != command.CommandId))
            {
                AssertRefusedLookup(authority, command, new TrustedCommandDelivery(other.PrincipalId, other.OriginSessionId));
            }
        }
    }

    private static void AssertReceipts(SqliteWorldAuthority authority, DurablePair pair)
    {
        // Each wrong delivery is refused first, before the authorized lookups disclose anything.
        AssertRefusedLookup(authority, pair.Offer, pair.AcceptanceDelivery);
        AssertRefusedLookup(authority, pair.Acceptance, pair.OfferDelivery);
        AssertRefusedLookup(authority, pair.Offer,
            new TrustedCommandDelivery(pair.Offer.PrincipalId, pair.Acceptance.OriginSessionId));
        AssertRefusedLookup(authority, pair.Acceptance,
            new TrustedCommandDelivery(pair.Acceptance.PrincipalId, pair.Offer.OriginSessionId));
        var offer = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(pair.Offer, pair.OfferDelivery));
        var acceptance = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(pair.Acceptance, pair.AcceptanceDelivery));
        Assert.Equal(pair.OfferReceipt, WorldReceiptCodecV1.Encode(offer));
        Assert.Equal(pair.AcceptanceReceipt, WorldReceiptCodecV1.Encode(acceptance));
        Assert.Equal((pair.Sequence, pair.Offer.ContentHash), (offer.CommitSequence, offer.CommandHash));
        Assert.Equal((pair.Sequence, pair.Acceptance.ContentHash), (acceptance.CommitSequence, acceptance.CommandHash));
        // The sender's own stream holds no outcome under the recipient's command identity.
        var probe = new WorldActionCommand(pair.Acceptance.CommandId, pair.Offer.PrincipalId, pair.Offer.OriginSessionId,
            pair.Offer.ActorId, pair.Offer.WorldId, pair.Offer.BranchId, pair.Offer.ExpectedActorRevision, pair.Offer.Intent);
        Assert.Null(authority.FindReceipt(probe, pair.OfferDelivery));
    }

    private static void AssertRefusedLookup(SqliteWorldAuthority authority, WorldActionCommand command,
        TrustedCommandDelivery delivery) => Assert.Equal(RefusedLookup,
            Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(command, delivery)).Message);

    private static void AssertStoredRows(IReadOnlyDictionary<EntityId, StoredWorldIdentity> identities,
        IReadOnlyDictionary<(EntityId PrincipalId, EntityId CommandId), StoredWorldCommandOutcome> outcomes, DurablePair pair)
    {
        Assert.Equal(new StoredWorldIdentity(pair.Batch.CauseId, pair.Sequence, WorldIdentityKind.CommitCause),
            identities[pair.Batch.CauseId]);
        foreach ((WorldActionCommand command, byte[] receipt) in new[]
            { (pair.Offer, pair.OfferReceipt), (pair.Acceptance, pair.AcceptanceReceipt) })
        {
            Assert.Equal(new StoredWorldIdentity(command.CommandId, pair.Sequence, WorldIdentityKind.Command),
                identities[command.CommandId]);
            EntityId result = WorldReceiptCodecV1.Decode(receipt).ResultId;
            Assert.Equal(new StoredWorldIdentity(result, pair.Sequence, WorldIdentityKind.Result), identities[result]);
            StoredWorldCommandOutcome stored = outcomes[(command.PrincipalId, command.CommandId)];
            Assert.Equal(pair.Sequence, stored.Sequence);
            Assert.Equal(WorldActionCodec.Encode(command), stored.Command);
            Assert.Equal(receipt, stored.Receipt);
        }
        Assert.All(pair.Identities, value => Assert.Equal(new StoredWorldIdentity(value.Identity, pair.Sequence,
            (WorldIdentityKind)(int)value.Kind), identities[value.Identity]));
    }

    private static void AssertJoinedOwners(WorldSnapshot expected, WorldSnapshot actual)
    {
        Assert.Equal(expected.ContentHash, actual.ContentHash);
        Assert.Equal(expected.Inventory.ContentHash, actual.Inventory.ContentHash);
        Assert.Equal(expected.Production!.ContentHash, actual.Production!.ContentHash);
        Assert.Equal(expected.Progression!.ContentHash, actual.Progression!.ContentHash);
        Assert.Equal(expected.ResourceHistory!.ContentHash, actual.ResourceHistory!.ContentHash);
        Assert.Equal(expected.Care!.ContentHash, actual.Care!.ContentHash);
        Assert.Equal(expected.Reservoirs!.ContentHash, actual.Reservoirs!.ContentHash);
        Assert.Equal(expected.Construction!.ContentHash, actual.Construction!.ContentHash);
        Assert.Equal(expected.Coordination!.ConsentedMaterial!.ContentHash, actual.Coordination!.ConsentedMaterial!.ContentHash);
        Assert.Equal(expected.Calendar, actual.Calendar);
    }

    private void AssertFirstSegment(WorldSnapshot closing, WorldSnapshot successor)
    {
        var transfers = closing.Coordination!.ConsentedMaterial!.Transfers;
        Assert.Equal(WorldConsentedMaterialHistory.MaximumTransfers - 1, transfers.Count);
        WorldConsentedTransferEvidence raw = Assert.Single(transfers, value => value.CommandId == journey.RawGift.Offer.CommandId);
        Assert.Equal((journey.RawGift.Acceptance.CommandId, journey.RawChild, journey.RawGift.Sequence),
            (raw.AcceptanceId, raw.MovedLotId, raw.WorldSequence));
        WorldConsentedTransferEvidence produced = Assert.Single(transfers,
            value => value.CommandId == journey.ProducedGift.Offer.CommandId);
        Assert.Equal((journey.ProducedGift.Acceptance.CommandId, journey.ProducedGift.Child, journey.Producer, 0UL, 3UL),
            (produced.AcceptanceId, produced.MovedLotId, produced.RightsOwnerId, produced.MovedStart, produced.MovedEnd));
        Assert.DoesNotContain(transfers, value => value.CommandId == journey.Refused.Offer.CommandId
            || value.AcceptanceId == journey.Refused.Acceptance.CommandId);

        ProductionBatch producer = closing.Production!.Batches.Single(value => value.BatchId == journey.Producer);
        Assert.Equal((journey.Helper, journey.Craft.CommandId), (producer.ActorId, producer.CommandId));
        ProductionBatch repacked = closing.Production.Batches.Single(value => value.BatchId == journey.Repacked);
        RetiredIngredient input = Assert.Single(repacked.Ingredients);
        Assert.Equal((journey.ProducedGift.Child, journey.Producer, 0UL, 1UL),
            (input.LotId, input.Output!.BatchId, input.Output.Start, input.Output.End));
        Assert.Equal(journey.Repacked, closing.Production.ConsumedItems
            .Single(value => value.CommandId == journey.Use.CommandId).Ingredient.Output!.BatchId);
        Assert.Single(closing.Care!.Ordinary!.Uses);

        // The successor starts an empty segment over the actual surviving stock and original producer.
        Assert.Empty(successor.Coordination!.ConsentedMaterial!.Transfers);
        Assert.True(successor.Production!.Continuation!.SupportsConsentedWorld);
        Assert.Equal(journey.Helper, successor.Production.Continuation.Basis.BatchWitnesses
            .Single(value => value.BatchId == journey.Producer).ActorId);
        TransformedLot child = successor.Production.Lots.Single(value => value.LotId == journey.ProducedGift.Child);
        Assert.Equal((journey.PlayerBag, journey.Producer, 1UL, 3UL),
            (child.CustodianId, child.Output.BatchId, child.Output.Start, child.Output.End));
    }

    private void AssertSecondSegment(WorldSnapshot closing, WorldSnapshot successor)
    {
        var transfers = closing.Coordination!.ConsentedMaterial!.Transfers;
        Assert.Equal(WorldConsentedMaterialHistory.MaximumTransfers - 1, transfers.Count);
        // The gift that published the first cut opens the second segment.
        Assert.Equal((journey.Cut.Offer.CommandId, journey.Cut.Acceptance.CommandId, journey.Cut.Sequence),
            (transfers[0].CommandId, transfers[0].AcceptanceId, transfers[0].WorldSequence));
        WorldConsentedTransferEvidence onward = Assert.Single(transfers,
            value => value.CommandId == journey.Onward.Offer.CommandId);
        Assert.Equal((journey.Onward.Acceptance.CommandId, journey.Onward.Child, journey.Producer, 1UL, 2UL),
            (onward.AcceptanceId, onward.MovedLotId, onward.RightsOwnerId, onward.MovedStart, onward.MovedEnd));
        ProductionBatch later = closing.Production!.Batches.Single(value => value.BatchId == journey.LaterRepacked);
        RetiredIngredient input = Assert.Single(later.Ingredients);
        Assert.Equal((journey.ProducedGift.Child, journey.Producer, 2UL, 3UL),
            (input.LotId, input.Output!.BatchId, input.Output.Start, input.Output.End));
        Assert.Equal(journey.LaterRepacked, closing.Production.ConsumedItems
            .Single(value => value.CommandId == journey.LaterUse.CommandId).Ingredient.Output!.BatchId);
        Assert.DoesNotContain(closing.Production.Lots, value => value.LotId == journey.ProducedGift.Child);

        Assert.Empty(successor.Coordination!.ConsentedMaterial!.Transfers);
        Assert.True(successor.Production!.Continuation!.SupportsConsentedWorld);
        Assert.Equal((ushort)28, successor.ProfileVersion);
    }

    private void AssertSurvivingStock(WorldSnapshot state)
    {
        Assert.DoesNotContain(state.Production!.Lots, value => value.LotId == journey.ProducedGift.Child);
        TransformedLot returned = state.Production.Lots.Single(value => value.LotId == journey.Onward.Child);
        Assert.Equal((journey.Producer, 1UL, 2UL, journey.HelperBag),
            (returned.Output.BatchId, returned.Output.Start, returned.Output.End, returned.CustodianId));
        TransformedLot held = state.Production.Lots.Single(value => value.LotId == journey.Stock);
        Assert.Equal((journey.Producer, 3UL, 4UL, journey.HelperBag),
            (held.Output.BatchId, held.Output.Start, held.Output.End, held.CustodianId));
        Assert.Equal(journey.Helper, state.Production.Continuation!.Basis.BatchWitnesses
            .Single(value => value.BatchId == journey.Producer).ActorId);
        Assert.Equal((3UL, 3UL, 1UL), (state.Production.TotalBatchCount, state.Production.TotalConsumedItemCount,
            state.Care!.TotalTreatments));
        Assert.Equal(journey.Cuts[^1].Reference, state.Coordination!.Anchor!.Reference);
        WorldConsentedTransferEvidence latest = Assert.Single(state.Coordination.ConsentedMaterial!.Transfers);
        Assert.Equal((journey.SecondCut.Offer.CommandId, journey.RawChild), (latest.CommandId, latest.MovedLotId));
    }

    private static void AssertMixedProfile(InvalidDataException error)
    {
        Assert.Equal(ReplayContract, error.Message);
        Assert.Equal(StaleBatch, Assert.IsType<AuthorityDispatchException>(error.InnerException).Message);
    }

    /// <summary>Applies one deliberate edit to a disposable copy and proves it changed exactly one row.</summary>
    private static void Mutate(StorageFiles files, string sql, params (string Name, object Value)[] parameters)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = files.Path, Mode = SqliteOpenMode.ReadWrite, Pooling = false
        }.ToString());
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach ((string name, object value) in parameters) command.Parameters.AddWithValue(name, value);
        Assert.Equal(1, command.ExecuteNonQuery());
    }
}
