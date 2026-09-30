using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.ProposalMigration;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.RetainedGifts.RetainedGiftPreparation;

namespace OpenFantasy.Hosting.Tests.RetainedGifts;

/// <summary>
/// Authenticates actual native retained gift journals. The saved database is bound independently to
/// the pinned original source row by row, then recovered and read through Core/Host; reports and their
/// own hashes are never proof of custody, commands, the cut or the use.
/// </summary>
public sealed class RetainedGiftResultTests
{
    [Fact]
    public void Native_retained_gift_results_extend_the_original_through_a_carried_cut_acceptance_and_own_use()
    {
        string? manifestPath = Environment.GetEnvironmentVariable("OPENFANTASY_RETAINED_GIFT_RESULTS");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(manifestPath), "The actual native retained gift results manifest is required.");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath!));
        JsonElement result = Assert.Single(manifest.RootElement.GetProperty("results").EnumerateArray());
        Assert.Equal("keyboard", Text(result, "mode"));
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-retained-gift-results-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Verify(result, CopyPinnedOriginal(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void Verify(JsonElement result, string original)
    {
        string nativeSave = AbsolutePath(result, "save");
        byte[] closedBytes = File.ReadAllBytes(nativeSave);
        string save = Path.Combine(Path.GetDirectoryName(original)!, "native.sqlite3");
        File.WriteAllBytes(save, closedBytes);
        // SQL prefix inspection and authority recovery each require their own exclusive connection.
        string ledger = Path.Combine(Path.GetDirectoryName(original)!, "native-prefix.sqlite3");
        File.WriteAllBytes(ledger, closedBytes);
        byte[] genesisBytes = File.ReadAllBytes(AbsolutePath(result, "genesis"));
        byte[] transitionBytes = File.ReadAllBytes(AbsolutePath(result, "transition"));
        byte[] beforeBytes = File.ReadAllBytes(AbsolutePath(result, "before"));
        WorldProposalTransition request = WorldProposalTransitionCodecV1.Decode(transitionBytes);
        Assert.Equal((RetainedTransitionFixture.SourceRevision, RetainedTransitionFixture.SourceHeadHash),
            (request.ExpectedRevision, request.ExpectedHeadHash.Value));
        using var metadataDocument = JsonDocument.Parse(File.ReadAllBytes(AbsolutePath(result, "metadata")));
        JsonElement metadata = metadataDocument.RootElement;
        Assert.Equal("native-retained-gift-fixture-v1", Text(metadata, "kind"));
        // Recover the actual journal so its authenticated joint archive supplies the decode context.
        // The fixture frame alone cannot establish the archive basis of a continued world.
        using (var recovered = SqliteWorldAuthority.OpenExisting(save, genesisBytes))
        {
            WorldSnapshot fixture = recovered.ReadRecordedStateForHost(metadata.GetProperty("beforeRevision").GetUInt64()).State;
            Assert.Equal(beforeBytes, WorldSnapshotCodec.Encode(fixture));
            Assert.Equal(fixture.Revision, metadata.GetProperty("beforeRevision").GetUInt64());
            EntityId player = EntityId.Parse(Text(metadata, "playerId"));
            EntityId helper = EntityId.Parse(Text(metadata, "helperId"));
            Assert.Equal(fixture.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId, player);
            Assert.Equal(fixture.Care!.Ordinary!.Permissions.Single(value => value.PatientActorId == player).HelperActorId, helper);
            Assert.False(fixture.Care.Ordinary.Permissions.Single(value => value.PatientActorId == player).IsActive);

            StoredWorldCommandOutcome[] native = BindOriginalPrefix(original, ledger, genesisBytes, transitionBytes, fixture,
                metadata, out StoredWorldCommit[] journal, out StoredWorldMaterialCareArchive cutArchive);
            var commands = native.Select(row => (Row: row, Command: WorldActionCodec.Decode(row.Command))).ToArray();
            Assert.InRange(commands.Length, 5, 6);
            var helperOffer = commands[0];
            var playerOffer = commands[1];
            var decline = commands[2];
            var accept = commands[3];
            var use = commands[4];
            var helperTerms = Assert.IsType<ProposeMaterialIntent>(helperOffer.Command.Intent).Terms;
            var playerTerms = Assert.IsType<ProposeMaterialIntent>(playerOffer.Command.Intent).Terms;
            EntityId gift = WorldMaterialProposal.DeriveIdentity(helperOffer.Command.CommandId);
            EntityId returned = WorldMaterialProposal.DeriveIdentity(playerOffer.Command.CommandId);
            Assert.Equal((helper, helper, player), (helperOffer.Command.ActorId, helperTerms.SenderActorId, helperTerms.RecipientActorId));
            Assert.Equal((ConsentedMaterialLotKind.CountedSource, EntityId.Parse(Text(metadata, "potionLotId")), 1UL),
                (helperTerms.LotKind, helperTerms.LotId, helperTerms.Quantity));
            // The first native commit is the helper's own configured decision, under its stable semantic identity.
            Assert.Equal(fixture.Revision + 1, helperOffer.Row.Sequence);
            Assert.Equal(LocalMaterialGiftIdentity.Offer(fixture, FarmClientSession.GiftPolicy(fixture, helper, player), helperTerms),
                helperOffer.Command.CommandId);
            Assert.Equal((player, player, helper, 1UL), (playerOffer.Command.ActorId, playerTerms.SenderActorId,
                playerTerms.RecipientActorId, playerTerms.Quantity));
            Assert.Equal(Text(metadata, "playerLotId"), playerTerms.LotId.ToString());
            Assert.Equal((helper, returned), (decline.Command.ActorId, Assert.IsType<DeclineMaterialProposalIntent>(decline.Command.Intent).ProposalId));
            Assert.Equal((player, gift), (accept.Command.ActorId, Assert.IsType<AcceptMaterialProposalIntent>(accept.Command.Intent).ProposalId));
            var selfUse = Assert.IsType<UseRestorativeItemIntent>(use.Command.Intent);
            Assert.Equal((player, player), (use.Command.ActorId, selfUse.RecipientActorId));
            Assert.Null(selfUse.PermissionId);
            Assert.True(helperOffer.Row.Sequence < playerOffer.Row.Sequence && playerOffer.Row.Sequence < decline.Row.Sequence
                && decline.Row.Sequence < accept.Row.Sequence && accept.Row.Sequence < use.Row.Sequence);
            if (commands.Length == 6)
            {
                // Autonomous scheduling stays enabled: a changed injury may lead to one later configured offer.
                var later = commands[5];
                var laterTerms = Assert.IsType<ProposeMaterialIntent>(later.Command.Intent).Terms;
                Assert.Equal((helper, player, helperTerms.LotId), (later.Command.ActorId, laterTerms.RecipientActorId, laterTerms.LotId));
                Assert.True(later.Row.Sequence > use.Row.Sequence);
            }
            foreach (var entry in commands) VerifyCommand(journal, entry.Row, entry.Command, fixture);

            // The actual automatic cut closes the root just before the player's offer and carries the open gift.
            Assert.Equal(fixture.Coordination!.Anchor!.Reference.Ordinal + 1, cutArchive.Reference.Ordinal);
            Assert.Equal(playerOffer.Row.Sequence - 1, cutArchive.Reference.BoundarySequence);
            Assert.Equal(playerOffer.Row.Sequence, cutArchive.PublicationSequence);
            Assert.Equal(journal.Single(value => value.Sequence == cutArchive.Reference.BoundarySequence).AfterHash,
                cutArchive.Reference.BoundaryHash);
            Assert.True(helperOffer.Row.Sequence <= cutArchive.Reference.BoundarySequence);

            WorldSnapshot final = recovered.ReadStateForHost();
            Assert.Equal(journal[^1].AfterHash, final.ContentHash);

            WorldSnapshot afterCut = recovered.ReadRecordedStateForHost(playerOffer.Row.Sequence).State;
            WorldMaterialCareArchiveAnchor anchor = afterCut.Coordination!.Anchor!;
            Assert.Equal((cutArchive.Reference.Ordinal, cutArchive.Reference.BoundarySequence), (anchor.Reference.Ordinal, anchor.ClosingWorldSequence));
            Assert.Null(anchor.TransitionHash);
            WorldMaterialProposal carried = Assert.Single(anchor.Proposals!.Carried);
            Assert.Equal((gift, helperOffer.Command.CommandId, helperTerms), (carried.ProposalId, carried.CreationCommandId, carried.Terms));
            Assert.Equal(LocalMaterialProposalPreparation.OfferLifetimeSteps, carried.ExpiresAtAdvancingStep - carried.CreatedAdvancingStep);
            Assert.Equal(carried.ContentHash, afterCut.Coordination.Proposals!.FindOpen(gift)!.ContentHash);
            Assert.Equal(fixture.Inventory.ContentHash, afterCut.Inventory.ContentHash);

            RestorativeRemedyDefinition remedy = fixture.Care.Remedies.Single();
            WorldSnapshot received = recovered.ReadRecordedStateForHost(use.Row.Sequence - 1).State;
            WorldSnapshot healed = recovered.ReadRecordedStateForHost(use.Row.Sequence).State;
            var outcome = received.Coordination!.Proposals!.FindOutcome(gift)!;
            Assert.Equal((WorldMaterialProposalOutcomeKind.Accepted, accept.Command.CommandId, accept.Row.Sequence),
                (outcome.Kind, outcome.TerminalCommandId, outcome.WorldSequence));
            Assert.NotNull(outcome.Settlement);
            Assert.Equal(WorldMaterialProposalOutcomeKind.Declined, received.Coordination.Proposals.FindOutcome(returned)!.Kind);
            // Acceptance moved one real dose and healed nobody; the lived source is conserved.
            var before = fixture.Progression!.GetActor(player);
            Assert.Equal((before.CurrentHp, before.CurrentMp),
                (received.Progression!.GetActor(player).CurrentHp, received.Progression.GetActor(player).CurrentMp));
            Assert.Equal((1UL, 79UL, 80UL), (Potions(received, remedy, Bag(received, player)),
                Potions(received, remedy, Bag(received, helper)), Potions(received, remedy)));
            Assert.Equal(RetainedTransitionFixture.SourceOrdinaryUses, received.Care!.TotalOrdinaryUses);
            // Own use: exact health, magic, time, stock, use record and resource evidence.
            Assert.Equal(RetainedTransitionFixture.SourceOrdinaryUses + 1, healed.Care!.TotalOrdinaryUses);
            Assert.Equal(metadata.GetProperty("expectedHp").GetInt32(), healed.Progression!.GetActor(player).CurrentHp);
            Assert.Equal((int)Math.Min(before.Stats.MaxHp, (long)before.CurrentHp + remedy.RestoredHp),
                healed.Progression.GetActor(player).CurrentHp);
            Assert.Equal(before.CurrentMp, healed.Progression.GetActor(player).CurrentMp);
            Assert.Equal((0UL, 79UL, 79UL), (Potions(healed, remedy, Bag(healed, player)),
                Potions(healed, remedy, Bag(healed, helper)), Potions(healed, remedy)));
            var definition = received.Care.Ordinary!.GetDefinition(selfUse.UseDefinitionId);
            Assert.Equal(RestorativeDeliveryMode.SelfUse, definition.DeliveryMode);
            Assert.Equal(received.Calendar.AddMilliseconds(checked((long)definition.ExplorationMilliseconds)), healed.Calendar);
            Assert.Equal(received.AdvancingSteps + 1, healed.AdvancingSteps);
            var recordedUse = Assert.Single(healed.Care.Ordinary!.Uses, value => value.CommandId == use.Command.CommandId);
            Assert.Equal((player, player, definition.ExplorationMilliseconds),
                (recordedUse.ActorId, recordedUse.RecipientActorId, recordedUse.ExplorationMilliseconds));
            Assert.Equal(recordedUse.UseId, Assert.Single(healed.ResourceHistory!.Evidence
                .OfType<WorldRestorativeUseResourceEvidence>(), value => value.CommandId == use.Command.CommandId).UseId);

            Assert.Equal(RetainedTransitionFixture.SourceOrdinaryUses + 1, final.Care!.TotalOrdinaryUses);
            Assert.Equal(healed.Progression.GetActor(player).CurrentHp, final.Progression!.GetActor(player).CurrentHp);
            Assert.Equal(fixture.Inventory.Controls, final.Inventory.Controls);
            Assert.Equal(79UL, Potions(final, remedy));
            Assert.Equal(5UL, recovered.InspectForHost().Journal.MaterialCareArchiveCount);
            VerifyReports(result, fixture, final, player, gift, returned);
        }
        Assert.Equal(closedBytes, File.ReadAllBytes(save));
        Assert.Equal(closedBytes, File.ReadAllBytes(ledger));
        Assert.Equal(closedBytes, File.ReadAllBytes(nativeSave));
    }

    /// <summary>
    /// Independent SQL binding: genesis, every original commit, receipt, identity and archive stay exact,
    /// the next row is the named transition, and every later command is labeled preparation or native.
    /// </summary>
    private static StoredWorldCommandOutcome[] BindOriginalPrefix(string original, string save, byte[] genesisBytes,
        byte[] transitionBytes, WorldSnapshot fixture, JsonElement metadata, out StoredWorldCommit[] journal,
        out StoredWorldMaterialCareArchive cutArchive)
    {
        using var source = WorldDatabase.Open(original, create: false);
        using var saved = WorldDatabase.Open(save, create: false);
        Assert.Equal(source.ReadGenesis(), genesisBytes);
        Assert.Equal(genesisBytes, saved.ReadGenesis());
        StoredWorldCommit[] prior = source.ReadJournal().ToArray();
        journal = saved.ReadJournal().ToArray();
        Assert.Equal(RetainedTransitionFixture.SourceRevision, (ulong)prior.Length);
        for (int index = 0; index < prior.Length; index++)
        {
            Assert.Equal((prior[index].Sequence, prior[index].Kind, prior[index].CauseId, prior[index].AdvancingSteps,
                prior[index].BeforeHash, prior[index].AfterHash), (journal[index].Sequence, journal[index].Kind,
                journal[index].CauseId, journal[index].AdvancingSteps, journal[index].BeforeHash, journal[index].AfterHash));
            Assert.Equal(prior[index].AcceptedBatch, journal[index].AcceptedBatch);
            Assert.Equal(prior[index].Outcomes, journal[index].Outcomes);
        }
        StoredWorldCommit transition = journal[prior.Length];
        Assert.Equal((WorldCommitKind.ProfileMigration, RetainedTransitionFixture.SourceRevision + 1), (transition.Kind, transition.Sequence));
        Assert.Equal(transitionBytes, transition.AcceptedBatch);

        var sourceCommands = source.ReadCommandOutcomes().ToDictionary(value => (value.PrincipalId, value.CommandId));
        StoredWorldCommandOutcome[] commands = saved.ReadCommandOutcomes().ToArray();
        foreach (var (key, row) in sourceCommands)
        {
            StoredWorldCommandOutcome retained = commands.Single(value => (value.PrincipalId, value.CommandId) == key);
            Assert.Equal((row.Sequence, row.Command, row.Receipt), (retained.Sequence, retained.Command, retained.Receipt),
                new RowComparer());
        }
        Assert.All(commands.Where(value => !sourceCommands.ContainsKey((value.PrincipalId, value.CommandId))),
            value => Assert.True(value.Sequence > transition.Sequence));
        StoredWorldIdentity[] identities = saved.ReadIdentities().ToArray();
        Assert.Subset(identities.ToHashSet(), source.ReadIdentities().ToHashSet());
        Assert.All(identities.Except(source.ReadIdentities()), value => Assert.True(value.Sequence >= transition.Sequence));

        StoredWorldMaterialCareArchive[] priorArchives = source.ReadMaterialCareArchives().ToArray();
        StoredWorldMaterialCareArchive[] archives = saved.ReadMaterialCareArchives().ToArray();
        Assert.Equal((3, 5), (priorArchives.Length, archives.Length));
        for (int index = 0; index < priorArchives.Length; index++)
        {
            Assert.Equal((priorArchives[index].Reference, priorArchives[index].PublicationSequence),
                (archives[index].Reference, archives[index].PublicationSequence));
            Assert.Equal(priorArchives[index].State, archives[index].State);
        }
        Assert.Equal(source.ReadHead().State, archives[3].State);
        Assert.Equal(transition.Sequence, archives[3].PublicationSequence);
        cutArchive = archives[4];

        // Every commit between the transition and the fixture is exactly one labeled preparatory action.
        var labeled = metadata.GetProperty("preparation").EnumerateArray().ToArray();
        Assert.Equal(Enumerable.Range(1, labeled.Length).Select(value => transition.Sequence + (ulong)value),
            labeled.Select(value => value.GetProperty("sequence").GetUInt64()));
        Assert.Equal(fixture.Revision, labeled[^1].GetProperty("sequence").GetUInt64());
        StoredWorldCommandOutcome[] prepared = commands.Where(value => value.Sequence > transition.Sequence
            && value.Sequence <= fixture.Revision).OrderBy(value => value.Sequence).ToArray();
        var expectedCommands = labeled.Where(value => value.GetProperty("commandId").ValueKind == JsonValueKind.String).ToArray();
        Assert.Equal(expectedCommands.Select(value => (value.GetProperty("sequence").GetUInt64(), Text(value, "commandId"))),
            prepared.Select(value => (value.Sequence, value.CommandId.ToString())));
        foreach (var (label, row) in expectedCommands.Zip(prepared))
        {
            WorldActionCommand command = WorldActionCodec.Decode(row.Command);
            Assert.Equal(Text(label, "actorId"), command.ActorId.ToString());
            Assert.True(Text(label, "label") switch
            {
                "prepare-revoke-care-permission" => command.Intent is RevokeCarePermissionIntent,
                "prepare-pressure-offer" => command.Intent is ProposeMaterialIntent,
                "prepare-pressure-withdrawal" => command.Intent is WithdrawMaterialProposalIntent,
                _ => false
            }, "Preparation label does not name its actual command: " + Text(label, "label"));
        }
        return commands.Where(value => value.Sequence > fixture.Revision).OrderBy(value => value.Sequence).ToArray();
    }

    private static void VerifyCommand(StoredWorldCommit[] journal, StoredWorldCommandOutcome row, WorldActionCommand command,
        WorldSnapshot fixture)
    {
        WorldActionReceipt receipt = WorldReceiptCodecV1.Decode(row.Receipt);
        Assert.Equal((CommandReceiptStatus.Applied, WorldActionRejection.None), (receipt.Status, receipt.Rejection));
        Assert.Equal((command.CommandId, command.ContentHash, row.Sequence), (receipt.CommandId, receipt.CommandHash, receipt.CommitSequence));
        Assert.Equal((command.PrincipalId, command.CommandId), (row.PrincipalId, row.CommandId));
        Assert.Equal((fixture.WorldId, fixture.BranchId), (command.WorldId, command.BranchId));
        var control = fixture.Inventory.Controls.Single(value => value.ActorId == command.ActorId);
        Assert.Equal((control.PrincipalId, control.SessionId), (command.PrincipalId, command.OriginSessionId));
        WorldStepBatch batch = WorldStepCodec.Decode(journal.Single(value => value.Sequence == row.Sequence).AcceptedBatch);
        Assert.Equal(WorldBatchKind.AdvancingStep, batch.Kind);
        Assert.Equal(WorldActionCodec.Encode(command), WorldActionCodec.Encode(Assert.Single(batch.Actions)));
    }

    private static void VerifyReports(JsonElement result, WorldSnapshot fixture, WorldSnapshot final, EntityId player,
        EntityId gift, EntityId returned)
    {
        using var activeDocument = JsonDocument.Parse(File.ReadAllBytes(AbsolutePath(result, "report")));
        using var reopenDocument = JsonDocument.Parse(File.ReadAllBytes(AbsolutePath(result, "reopenReport")));
        JsonElement active = activeDocument.RootElement;
        JsonElement reopen = reopenDocument.RootElement;
        VerifyReport(active, "keyboard");
        VerifyReport(reopen, "reopen");
        Assert.Equal(PublicHash(fixture, player), Text(active, "InitialHash"));
        Assert.Equal(PublicHash(final, player), Text(active, "FinalHash"));
        Assert.Equal(Text(active, "FinalHash"), Text(reopen, "InitialHash"));
        Assert.Equal(Text(reopen, "InitialHash"), Text(reopen, "FinalHash"));
        foreach (JsonElement report in new[] { active, reopen })
        {
            Assert.Equal(gift.ToString(), Text(report, "HelperProposalId"));
            Assert.Equal(returned.ToString(), Text(report, "PlayerProposalId"));
        }
    }

    private static void VerifyReport(JsonElement report, string mode)
    {
        Assert.Equal(("native-retained-gifts-v1", "passed", mode), (Text(report, "Kind"), Text(report, "Status"), Text(report, "Mode")));
        Assert.Equal(0, report.GetProperty("FocusRequests").GetInt32());
        Assert.True(report.GetProperty("IsLocallyPaused").GetBoolean());
        Assert.InRange(report.GetProperty("OpeningMilliseconds").GetInt64(), 0, 29_999);
        Assert.InRange(report.GetProperty("DurationMilliseconds").GetInt64(), 0, 149_999);
        Assert.All(report.GetProperty("Waits").EnumerateArray(), wait =>
            Assert.True(wait.GetProperty("Seconds").GetDouble() < wait.GetProperty("LimitSeconds").GetDouble()));
        Assert.NotEqual("headless", Text(report, "Display"));
    }

    private static string CopyPinnedOriginal(string directory)
    {
        string? configured = Environment.GetEnvironmentVariable("OPENFANTASY_RETAINED_PROPOSAL_SOURCE");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("OPENFANTASY_RETAINED_PROPOSAL_SOURCE must name the isolated original source.");
        }
        string path = Directory.Exists(configured) ? Path.Combine(configured, "final.sqlite3") : configured;
        string copy = Path.Combine(directory, "original.sqlite3");
        File.WriteAllBytes(copy, File.ReadAllBytes(path));
        Assert.Equal(RetainedTransitionFixture.SourceSha256, RetainedTransitionFixture.Sha256(copy));
        return copy;
    }

    private static string AbsolutePath(JsonElement value, string name)
    {
        string path = Text(value, name);
        Assert.True(Path.IsPathFullyQualified(path), $"Native evidence requires an absolute {name} path.");
        Assert.True(File.Exists(path), $"Native evidence file is absent: {path}");
        return path;
    }

    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString()
        ?? throw new InvalidDataException($"Native retained gift verification requires {name}.");

    private static string PublicHash(WorldSnapshot state, EntityId player) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new LocalWorldView(state, player)))));

    private sealed class RowComparer : IEqualityComparer<(ulong Sequence, byte[] Command, byte[] Receipt)>
    {
        public bool Equals((ulong Sequence, byte[] Command, byte[] Receipt) left, (ulong Sequence, byte[] Command, byte[] Receipt) right) =>
            left.Sequence == right.Sequence && left.Command.AsSpan().SequenceEqual(right.Command)
            && left.Receipt.AsSpan().SequenceEqual(right.Receipt);

        public int GetHashCode((ulong Sequence, byte[] Command, byte[] Receipt) value) => value.Sequence.GetHashCode();
    }
}
