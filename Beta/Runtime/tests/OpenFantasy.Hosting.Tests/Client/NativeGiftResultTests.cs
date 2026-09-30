using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Client;

/// <summary>Authenticates native gift journals; reports alone are never proof of custody or commands.</summary>
public sealed class NativeGiftResultTests
{
    [Fact]
    public void Opt_in_native_gift_results_authenticate_all_four_actions_and_exact_produced_custody()
    {
        string? manifestPath = Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_GIFT_RESULTS");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(manifestPath), "The actual native gift results manifest is required.");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath!));
        JsonElement[] results = manifest.RootElement.GetProperty("results").EnumerateArray().ToArray();
        Assert.Equal(new[] { "controller", "keyboard", "pointer" },
            results.Select(result => Text(result, "mode")).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(results.Length, results.Select(result => Text(result, "save")).Distinct(StringComparer.Ordinal).Count());
        foreach (var result in results) Verify(result);
    }

    private static void Verify(JsonElement result)
    {
        string mode = Text(result, "mode");
        bool accepts = mode != "pointer";
        string save = AbsolutePath(result, "save");
        byte[] genesisBytes = File.ReadAllBytes(AbsolutePath(result, "genesis"));
        byte[] beforeBytes = File.ReadAllBytes(AbsolutePath(result, "before"));
        WorldSnapshot genesis = WorldSnapshotCodec.Decode(genesisBytes);
        WorldSnapshot fixture = WorldSnapshotCodec.Decode(beforeBytes);
        Assert.Equal(WorldSnapshotCodec.Encode(WorldMaterialProposalAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis)),
            genesisBytes);
        Assert.Equal((ushort)30, fixture.ProfileVersion);
        using var metadataDocument = JsonDocument.Parse(File.ReadAllBytes(AbsolutePath(result, "metadata")));
        JsonElement metadata = metadataDocument.RootElement;
        Assert.Equal("native-material-gift-fixture-v1", Text(metadata, "kind"));
        Assert.Equal(Text(metadata, "genesisHash"), genesis.ContentHash.Value);
        Assert.Equal(Text(metadata, "beforeHash"), fixture.ContentHash.Value);
        Assert.Equal(metadata.GetProperty("beforeRevision").GetUInt64(), fixture.Revision);
        Assert.Equal(Text(metadata, "worldId"), fixture.WorldId.ToString());
        Assert.Equal(Text(metadata, "branchId"), fixture.BranchId.ToString());
        EntityId player = EntityId.Parse(Text(metadata, "playerId"));
        EntityId helper = EntityId.Parse(Text(metadata, "helperId"));
        EntityId recipient = EntityId.Parse(Text(metadata, "recipientId"));
        Assert.Equal(fixture.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId, player);
        Assert.Equal(fixture.Quests!.Definitions.Single().GiverActorId, helper);
        Assert.Equal(fixture.EncounterLifecycle!.Scenarios.Single(value =>
            value.Key == EncounterJourneyAdmission.SecondScenarioKey).TargetActorId, recipient);
        Assert.True(fixture.Progression!.GetActor(recipient).CurrentHp > 0);
        Assert.False(fixture.Care!.Ordinary!.Permissions.Single().IsActive);
        Assert.Empty(fixture.Coordination!.Proposals!.Open);
        Assert.Empty(fixture.Coordination.Proposals.Outcomes);

        using var activeDocument = JsonDocument.Parse(File.ReadAllBytes(AbsolutePath(result, "report")));
        using var reopenDocument = JsonDocument.Parse(File.ReadAllBytes(AbsolutePath(result, "reopenReport")));
        JsonElement active = activeDocument.RootElement;
        JsonElement reopen = reopenDocument.RootElement;
        VerifyReport(active, mode);
        VerifyReport(reopen, "reopen");
        Assert.Equal(PublicHash(fixture, player), Text(active, "InitialHash"));
        Assert.Equal(Text(active, "FinalHash"), Text(reopen, "InitialHash"));
        Assert.Equal(Text(reopen, "InitialHash"), Text(reopen, "FinalHash"));
        Assert.Equal(accepts ? "Accepted" : "Declined", Text(active, "ReplyStatus"));
        EntityId incomingId = EntityId.Parse(Text(active, "IncomingProposalId"));
        EntityId outgoingId = EntityId.Parse(Text(active, "OutgoingProposalId"));
        Assert.NotEqual(incomingId, outgoingId);
        Assert.Equal(Text(metadata, "incomingProposalId"), incomingId.ToString());
        Assert.Equal(Text(active, "IncomingProposalId"), Text(reopen, "IncomingProposalId"));
        Assert.Equal(Text(active, "OutgoingProposalId"), Text(reopen, "OutgoingProposalId"));
        Assert.Equal(Text(active, "ReplyStatus"), Text(reopen, "ReplyStatus"));

        StoredWorldCommit[] commits;
        StoredWorldCommandOutcome[] rows;
        using (var database = WorldDatabase.Open(save, create: false))
        {
            Assert.Equal(genesisBytes, database.ReadGenesis());
            commits = database.ReadJournal(fixture.Revision).ToArray();
            rows = database.ReadCommandOutcomes(fixture.Revision).ToArray();
        }
        Assert.NotEmpty(commits);
        Assert.Equal(4, rows.Length); // NPC offer, explicit reply, player's offer, explicit withdrawal.
        var commands = rows.Select(row => (Row: row, Command: WorldActionCodec.Decode(row.Command))).ToArray();
        Assert.All(commands, value => Assert.IsAssignableFrom<MaterialProposalIntent>(value.Command.Intent));
        var incoming = Assert.Single(commands, value => value.Command.Intent is ProposeMaterialIntent
            && WorldMaterialProposal.DeriveIdentity(value.Command.CommandId) == incomingId);
        var outgoing = Assert.Single(commands, value => value.Command.Intent is ProposeMaterialIntent
            && WorldMaterialProposal.DeriveIdentity(value.Command.CommandId) == outgoingId);
        var reply = Assert.Single(commands, value => value.Command.Intent is AcceptMaterialProposalIntent or DeclineMaterialProposalIntent);
        var withdrawal = Assert.Single(commands, value => value.Command.Intent is WithdrawMaterialProposalIntent);
        Assert.Equal(incomingId, reply.Command.Intent switch
        {
            AcceptMaterialProposalIntent accept => accept.ProposalId,
            DeclineMaterialProposalIntent decline => decline.ProposalId,
            _ => throw new InvalidDataException("The native incoming offer requires its exact answer.")
        });
        Assert.Equal(outgoingId, Assert.IsType<WithdrawMaterialProposalIntent>(withdrawal.Command.Intent).ProposalId);
        Assert.Equal(helper, incoming.Command.ActorId);
        Assert.Equal(player, outgoing.Command.ActorId);
        Assert.Equal(player, reply.Command.ActorId);
        Assert.Equal(player, withdrawal.Command.ActorId);
        Assert.True(incoming.Row.Sequence < reply.Row.Sequence);
        Assert.True(reply.Row.Sequence < outgoing.Row.Sequence);
        Assert.True(outgoing.Row.Sequence < withdrawal.Row.Sequence);

        byte[] closedBytes = File.ReadAllBytes(save);
        // Opening authenticates the complete genesis/journal/archive/checkpoint/identity/receipt chain.
        using (var recovered = SqliteWorldAuthority.OpenExisting(save, genesisBytes))
        {
            WorldSnapshot final = recovered.ReadStateForHost();
            Assert.Equal(beforeBytes, WorldSnapshotCodec.Encode(recovered.ReadRecordedStateForHost(fixture.Revision).State));
            Assert.Equal(commits[^1].AfterHash, final.ContentHash);
            Assert.Equal(commits[^1].Sequence, final.Revision);
            foreach (var entry in commands) VerifyCommand(recovered, commits, entry.Row, entry.Command, fixture);

            WorldSnapshot offeredBefore = recovered.ReadRecordedStateForHost(incoming.Row.Sequence - 1).State;
            WorldSnapshot offeredAfter = recovered.ReadRecordedStateForHost(incoming.Row.Sequence).State;
            WorldMaterialProposal proposal = Assert.Single(offeredAfter.Coordination!.Proposals!.Open);
            var terms = Assert.IsType<ProposeMaterialIntent>(incoming.Command.Intent).Terms;
            var policy = ScheduledMaterialNpcTests.Policy(offeredBefore, helper, player, lifetime: 1200);
            Assert.Equal(LocalMaterialGiftIdentity.Offer(offeredBefore, policy, terms), incoming.Command.CommandId);
            Assert.Equal(Text(metadata, "incomingCommandId"), incoming.Command.CommandId.ToString());
            Assert.Equal(ConsentedMaterialLotKind.Produced, terms.LotKind);
            Assert.Equal((helper, player, 1UL), (terms.SenderActorId, terms.RecipientActorId, terms.Quantity));
            Assert.Equal(Text(metadata, "incomingLotId"), terms.LotId.ToString());
            Assert.Equal(fixture.Production!.Lots.Where(value => value.CustodianId == fixture.GetResident(helper).InventoryCustodianId
                && value.Definition == fixture.Care!.Remedies.Single().Item).OrderBy(value => value.LotId).First().LotId, terms.LotId);
            Assert.Equal(1200UL, proposal.ExpiresAtAdvancingStep - proposal.CreatedAdvancingStep);
            Assert.Equal(offeredBefore.Inventory.ContentHash, offeredAfter.Inventory.ContentHash);
            Assert.Equal(offeredBefore.Production!.ContentHash, offeredAfter.Production!.ContentHash);

            WorldSnapshot replyBefore = recovered.ReadRecordedStateForHost(reply.Row.Sequence - 1).State;
            WorldSnapshot replyAfter = recovered.ReadRecordedStateForHost(reply.Row.Sequence).State;
            WorldMaterialProposalOutcome answered = replyAfter.Coordination!.Proposals!.FindOutcome(incomingId)!;
            Assert.NotNull(answered);
            Assert.Equal(accepts ? WorldMaterialProposalOutcomeKind.Accepted : WorldMaterialProposalOutcomeKind.Declined, answered.Kind);
            Assert.Equal(reply.Command.CommandId, answered.TerminalCommandId);
            Assert.Equal(reply.Row.Sequence, answered.WorldSequence);
            Assert.Equal(proposal.Encode(), answered.Proposal.Encode());
            VerifyDose(fixture, replyBefore, replyAfter, final, answered, reply.Command, player, helper, accepts);

            WorldSnapshot createBefore = recovered.ReadRecordedStateForHost(outgoing.Row.Sequence - 1).State;
            WorldSnapshot createAfter = recovered.ReadRecordedStateForHost(outgoing.Row.Sequence).State;
            var rawTerms = Assert.IsType<ProposeMaterialIntent>(outgoing.Command.Intent).Terms;
            CountedSourceLot raw = fixture.Inventory.Custody.GetLot(EntityId.Parse(Text(metadata, "rawLotId")));
            Assert.Equal(metadata.GetProperty("rawQuantity").GetUInt64(), raw.Quantity);
            Assert.Equal(metadata.GetProperty("rawRevision").GetUInt64(), raw.Revision);
            Assert.Equal(Text(metadata, "rawSourceId"), raw.Source.SourceId.ToString());
            Assert.Equal(metadata.GetProperty("rawStart").GetUInt64(), raw.Source.Start);
            Assert.Equal(metadata.GetProperty("rawEnd").GetUInt64(), raw.Source.End);
            Assert.Equal((player, recipient, raw.LotId, 1UL, raw.Revision),
                (rawTerms.SenderActorId, rawTerms.RecipientActorId, rawTerms.LotId, rawTerms.Quantity, rawTerms.ExpectedLotRevision));
            Assert.Equal(ConsentedMaterialLotKind.CountedSource, rawTerms.LotKind);
            Assert.Equal(raw.Definition, rawTerms.Definition);
            Assert.Equal(raw.CustodianId, rawTerms.SourceCustodianId);
            Assert.Equal(fixture.GetResident(recipient).InventoryCustodianId, rawTerms.DestinationCustodianId);
            Assert.Equal(raw, createBefore.Inventory.Custody.GetLot(raw.LotId));
            Assert.Equal(createBefore.Inventory.ContentHash, createAfter.Inventory.ContentHash);
            Assert.Equal(createBefore.Production!.ContentHash, createAfter.Production!.ContentHash);

            WorldSnapshot withdrawBefore = recovered.ReadRecordedStateForHost(withdrawal.Row.Sequence - 1).State;
            WorldSnapshot withdrawAfter = recovered.ReadRecordedStateForHost(withdrawal.Row.Sequence).State;
            var withdrawn = withdrawAfter.Coordination!.Proposals!.FindOutcome(outgoingId)!;
            Assert.NotNull(withdrawn);
            Assert.Equal(WorldMaterialProposalOutcomeKind.Withdrawn, withdrawn.Kind);
            Assert.Equal(withdrawal.Command.CommandId, withdrawn.TerminalCommandId);
            Assert.Null(withdrawn.Settlement);
            Assert.Equal(withdrawBefore.Inventory.ContentHash, withdrawAfter.Inventory.ContentHash);
            Assert.Equal(withdrawBefore.Production!.ContentHash, withdrawAfter.Production!.ContentHash);
            Assert.Equal(raw, final.Inventory.Custody.GetLot(raw.LotId));
            Assert.Equal(fixture.Inventory.Custody.Lots, final.Inventory.Custody.Lots);
            Assert.Equal(fixture.Inventory.Sources, final.Inventory.Sources);
            Assert.Empty(final.Coordination!.Proposals!.Open);
            Assert.Equal(2, final.Coordination.Proposals.Outcomes.Count);
            Assert.Equal(fixture.Care.ContentHash, final.Care!.ContentHash);
            Assert.Equal(fixture.Progression.ContentHash, final.Progression!.ContentHash);
            Assert.Equal(fixture.Production!.ConsumedItems.Count, final.Production!.ConsumedItems.Count);
            Assert.Equivalent(fixture.Production.Batches, final.Production.Batches, strict: true);
            Assert.Equal(fixture.Space.Actors.Select(value => (value.ActorId, value.Feet, value.Revision)),
                final.Space.Actors.Select(value => (value.ActorId, value.Feet, value.Revision)));
            Assert.Equal(PublicHash(final, player), Text(active, "FinalHash"));
            Assert.Equal(PublicHash(final, player), Text(reopen, "FinalHash"));
        }
        Assert.Equal(closedBytes, File.ReadAllBytes(save));
    }

    private static void VerifyCommand(SqliteWorldAuthority recovered, StoredWorldCommit[] commits,
        StoredWorldCommandOutcome row, WorldActionCommand command, WorldSnapshot fixture)
    {
        WorldActionReceipt receipt = WorldReceiptCodecV1.Decode(row.Receipt);
        Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        Assert.Equal(command.CommandId, receipt.CommandId);
        Assert.Equal(command.ContentHash, receipt.CommandHash);
        Assert.Equal(row.Sequence, receipt.CommitSequence);
        Assert.Equal(command.PrincipalId, row.PrincipalId);
        Assert.Equal(command.CommandId, row.CommandId);
        Assert.Equal(fixture.WorldId, command.WorldId);
        Assert.Equal(fixture.BranchId, command.BranchId);
        StoredWorldCommit committed = commits.Single(value => value.Sequence == row.Sequence);
        WorldStepBatch batch = WorldStepCodec.Decode(committed.AcceptedBatch);
        Assert.Equal(WorldBatchKind.AdvancingStep, batch.Kind);
        Assert.Equal(WorldActionCodec.Encode(command), WorldActionCodec.Encode(Assert.Single(batch.Actions)));
        WorldSnapshot before = recovered.ReadRecordedStateForHost(row.Sequence - 1).State;
        WorldSnapshot after = recovered.ReadRecordedStateForHost(row.Sequence).State;
        Assert.Equal(committed.BeforeHash, before.ContentHash);
        Assert.Equal(committed.AfterHash, after.ContentHash);
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Equal(before.Calendar.AddMilliseconds(1800), after.Calendar);
        var control = before.Inventory.Controls.Single(value => value.ActorId == command.ActorId);
        Assert.Equal(control.PrincipalId, command.PrincipalId);
        Assert.Equal(control.SessionId, command.OriginSessionId);
        Assert.Equal(before.Space.GetActor(command.ActorId).Revision, command.ExpectedActorRevision);
        Assert.True(recovered.IsIdentityOccupiedForHost(command.CommandId));
        Assert.True(recovered.IsIdentityOccupiedForHost(receipt.ResultId));
        Assert.Equal(row.Receipt, WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(recovered.FindReceipt(command,
            new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId)))));
    }

    private static void VerifyDose(WorldSnapshot fixture, WorldSnapshot before, WorldSnapshot after,
        WorldSnapshot final, WorldMaterialProposalOutcome outcome, WorldActionCommand reply,
        EntityId player, EntityId helper, bool accepts)
    {
        var terms = outcome.Proposal.Terms;
        EntityId helperBag = fixture.GetResident(helper).InventoryCustodianId;
        EntityId playerBag = fixture.GetResident(player).InventoryCustodianId;
        TransformedLot dose = fixture.Production!.Lots.Single(value => value.LotId == terms.LotId);
        Assert.Equal(helperBag, dose.CustodianId);
        Assert.Equal(terms.ExpectedLotRevision, dose.Revision);
        Assert.Equal(1UL, dose.Quantity);
        Assert.Equal(dose.Definition, terms.Definition);
        Assert.Equal((helperBag, playerBag), (terms.SourceCustodianId, terms.DestinationCustodianId));
        var crafted = fixture.Production.Batches.Single(value => value.BatchId == dose.Output.BatchId);
        Assert.Equal(helper, crafted.ActorId);
        Assert.Equal(3UL, crafted.Ingredients.Aggregate(0UL, (sum, ingredient) => sum + ingredient.Quantity));
        if (accepts)
        {
            var accepted = Assert.IsType<AcceptMaterialProposalIntent>(reply.Intent);
            Assert.Equal(terms, accepted.Terms);
            Assert.Equal(outcome.Proposal.GrantHash, accepted.GrantHash);
            var settlement = Assert.IsType<WorldMaterialProposalSettlement>(outcome.Settlement);
            Assert.Equal(dose.LotId, settlement.MovedLotId);
            Assert.Equal(dose.Output.BatchId, settlement.RightsOwnerId);
            Assert.Equal(dose.Output.OutputIndex, settlement.OutputIndex);
            Assert.Equal((dose.Output.Start, dose.Output.End), (settlement.MovedStart, settlement.MovedEnd));
            Assert.Equal(before.Inventory.ContentHash, settlement.OriginalInventoryHash);
            Assert.Equal(before.Production!.ContentHash, settlement.OriginalProductionHash);
            Assert.Equal(after.Inventory.ContentHash, settlement.CandidateInventoryHash);
            Assert.Equal(after.Production!.ContentHash, settlement.CandidateProductionHash);
        }
        else
        {
            Assert.IsType<DeclineMaterialProposalIntent>(reply.Intent);
            Assert.Null(outcome.Settlement);
            Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
            Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
            Assert.Equal(fixture.Inventory.ContentHash, final.Inventory.ContentHash);
            Assert.Equal(fixture.Production.ContentHash, final.Production!.ContentHash);
        }
        Assert.Equal(fixture.Production.Lots.Count, final.Production!.Lots.Count);
        foreach (var original in fixture.Production.Lots)
        {
            var retained = final.Production.Lots.Single(value => value.LotId == original.LotId);
            bool moved = accepts && original.LotId == dose.LotId;
            Assert.Equal(moved ? playerBag : original.CustodianId, retained.CustodianId);
            Assert.Equal(original.Revision + (moved ? 1UL : 0UL), retained.Revision);
            Assert.Equal(original.Output, retained.Output);
            Assert.Equal(original.Definition, retained.Definition);
            Assert.Equal(original.Quantity, retained.Quantity);
        }
        Assert.Equal(accepts ? 1UL : 2UL, final.Production.Lots.Where(value => value.CustodianId == helperBag
            && value.Definition == dose.Definition).Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
        Assert.Equal(fixture.Progression!.ContentHash, after.Progression!.ContentHash);
        Assert.Equal(fixture.Care!.ContentHash, after.Care!.ContentHash);
    }

    private static void VerifyReport(JsonElement report, string mode)
    {
        Assert.Equal("native-gifts-v1", Text(report, "Kind"));
        Assert.Equal("passed", Text(report, "Status"));
        Assert.Equal(mode, Text(report, "Mode"));
        Assert.Equal(0, report.GetProperty("FocusRequests").GetInt32());
        Assert.True(report.GetProperty("IsLocallyPaused").GetBoolean());
        Assert.InRange(report.GetProperty("DurationMilliseconds").GetInt64(), 0, 149_999);
        Assert.NotEqual("headless", Text(report, "Display"));
    }

    private static string AbsolutePath(JsonElement value, string name)
    {
        string path = Text(value, name);
        Assert.True(Path.IsPathFullyQualified(path), $"Native evidence requires an absolute {name} path.");
        Assert.True(File.Exists(path), $"Native evidence file is absent: {path}");
        return path;
    }

    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString()
        ?? throw new InvalidDataException($"Native gift verification requires {name}.");

    private static string PublicHash(WorldSnapshot state, EntityId player) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new LocalWorldView(state, player)))));
}
