using System.Diagnostics;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.SelectedGiftsNative;

public sealed partial class NativeSelectedGiftJourneyTests
{
    private sealed record GiftBoundary(WorldActionCommand Creation, WorldMaterialProposalOutcomeKind Kind);

    private static JsonElement Selection(NativeRosterProcess process) => process.Last.GetProperty("Selection");
    private static JsonElement Slot(NativeRosterProcess process, EntityId owner, EntityId grantee) =>
        Assert.Single(process.Last.GetProperty("ControlBoard").GetProperty("Slots").EnumerateArray(), row =>
            row.GetProperty("Owner").GetString() == owner.ToString() && row.GetProperty("Grantee").GetString() == grantee.ToString());

    private static bool HasOpenOffer(NativeRosterProcess process, EntityId proposal) =>
        process.Last.GetProperty("Gifts").GetProperty("Offers").EnumerateArray().Any(row =>
            row.GetProperty("Proposal").GetString() == proposal.ToString() && row.GetProperty("Status").GetString() == "Open");

    private static void AssertPaused(NativeRosterProcess process, EntityId home, EntityId selected)
    {
        Assert.Equal(home.ToString(), Selection(process).GetProperty("HomeActor").GetString());
        Assert.Equal(selected.ToString(), Selection(process).GetProperty("Actor").GetString());
        Assert.Equal(home, process.View.PlayerActorId);
        Assert.True(process.Last.GetProperty("LocallyPaused").GetBoolean());
        Assert.False(Selection(process).GetProperty("Ready").GetBoolean());
    }

    private static WorldActionCommand LastCommand(NativeRosterProcess process)
    {
        var value = process.Submissions[^1];
        Assert.False(value.GetProperty("IsRetry").GetBoolean());
        Assert.Equal(JsonValueKind.String, value.GetProperty("Receipt").ValueKind);
        return WorldActionCodec.Decode(Convert.FromHexString(value.GetProperty("Command").GetString()!));
    }

    private static async Task Until(Func<bool> condition, string message,
        IEnumerable<NativeRosterProcess> processes, CancellationToken token)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            await Task.WhenAll(processes.Select(process => process.Send("observe")));
            token.ThrowIfCancellationRequested();
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5), message);
            if (condition()) return;
            await Task.Delay(25, token);
        }
    }

    private static void Verify(string output, SelectedGiftWorld world, IReadOnlyList<NativeRosterProcess> processes,
        WorldControlWitness witness, IReadOnlyList<GiftBoundary> expected, WorldActionCommand refused)
    {
        StoredWorldCommit[] journal;
        using (var database = WorldDatabase.Open(world.Database, create: false)) journal = database.ReadJournal().ToArray();
        var batches = journal.Select(row => WorldStepCodec.Decode(row.AcceptedBatch)).ToArray();
        var gameplay = processes.SelectMany(process => process.Submissions).ToArray();
        var permission = processes.SelectMany(process => process.Last.GetProperty("PermissionSubmissions").EnumerateArray())
            .Select(value => value.Clone()).ToArray();
        Assert.Equal(13, gameplay.Length); // Six separate creations and decisions, plus one refused old-grant acceptance.
        Assert.Equal(3, permission.Length);
        var submitted = gameplay.Concat(permission).ToArray();
        var commands = submitted.Select(value => WorldActionCodec.Decode(Convert.FromHexString(value.GetProperty("Command").GetString()!))).ToArray();
        Assert.Equal(commands.Length, commands.Select(command => command.CommandId).Distinct().Count());
        Assert.Equal(6, commands.Count(command => command.Intent is ProposeMaterialIntent));
        Assert.Equal(3, commands.Count(command => command.Intent is AcceptMaterialProposalIntent));
        Assert.Equal(2, commands.Count(command => command.Intent is DeclineMaterialProposalIntent));
        Assert.Equal(2, commands.Count(command => command.Intent is WithdrawMaterialProposalIntent));
        var grant = Assert.Single(commands, command => command.Intent is OfferControlPermissionIntent);
        var acceptance = Assert.Single(commands, command => command.Intent is AcceptControlPermissionIntent);
        var revoke = Assert.Single(commands, command => command.Intent is RevokeControlPermissionIntent);
        Assert.Equal(WorldControlCapabilities.Inventory, Assert.IsType<OfferControlPermissionIntent>(grant.Intent).Capabilities);
        Assert.Equal(grant.CommandId, witness.GrantCommandId);
        Assert.Equal(acceptance.CommandId, witness.AcceptanceCommandId);
        Assert.Equal(WorldControlCapabilities.Inventory, witness.Capabilities);

        using var authority = SqliteWorldAuthority.OpenExisting(world.Database, WorldSnapshotCodec.Encode(world.Genesis));
        var final = authority.ReadStateForHost();
        foreach (var value in submitted)
        {
            var command = WorldActionCodec.Decode(Convert.FromHexString(value.GetProperty("Command").GetString()!));
            if (value.TryGetProperty("IsRetry", out var retry)) Assert.False(retry.GetBoolean());
            var delivery = new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId);
            var receipt = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command, delivery));
            Assert.Equal(command.CommandId == refused.CommandId ? WorldActionRejection.Unavailable : WorldActionRejection.None, receipt.Rejection);
            Assert.Equal(Convert.FromHexString(value.GetProperty("Receipt").GetString()!), WorldReceiptCodecV1.Encode(receipt));
            Assert.Contains(batches.SelectMany(batch => batch.Actions), actual =>
                WorldActionCodec.Encode(actual).AsSpan().SequenceEqual(WorldActionCodec.Encode(command)));
            EntityId principalActor = command.ControlWitness is null ? command.ActorId : world.Grantee;
            var binding = world.Genesis.Inventory.Controls.Single(row => row.ActorId == principalActor);
            Assert.Equal(binding.PrincipalId, command.PrincipalId);
            Assert.Equal(binding.SessionId, command.OriginSessionId);
            if (command.ControlWitness is not null)
            {
                Assert.Equal(world.Owner, command.ActorId);
                Assert.Equal(witness, command.ControlWitness);
                Assert.IsAssignableFrom<MaterialProposalIntent>(command.Intent);
            }
        }
        Assert.Equal(world.Owner, grant.ActorId);
        Assert.Equal(world.Grantee, acceptance.ActorId);
        Assert.Equal(world.Owner, revoke.ActorId);
        Assert.Null(grant.ControlWitness);
        Assert.Null(acceptance.ControlWitness);
        Assert.Null(revoke.ControlWitness);
        Assert.Equal(world.Genesis.Inventory.Controls, final.Inventory.Controls);
        Assert.Equal(world.Genesis.Residents.Select(actor => (actor.ActorId, actor.ControllerKind, actor.InventoryCustodianId)),
            final.Residents.Select(actor => (actor.ActorId, actor.ControllerKind, actor.InventoryCustodianId)));
        Assert.Equal(world.Genesis.Space.Actors.Select(actor => (actor.ActorId, actor.Binding.FormInstanceId, actor.Binding.PhysicalBodyId)),
            final.Space.Actors.Select(actor => (actor.ActorId, actor.Binding.FormInstanceId, actor.Binding.PhysicalBodyId)));
        Assert.Equal(world.Prepared.Space.Actors.Select(actor => (actor.ActorId, actor.Feet)),
            final.Space.Actors.Select(actor => (actor.ActorId, actor.Feet)));
        Assert.Equal(world.Prepared.Production!.ContentHash, final.Production!.ContentHash);
        Assert.Equal(world.Prepared.Care!.ContentHash, final.Care!.ContentHash);
        Assert.Equal(world.Prepared.Progression!.Currency.Revision, final.Progression!.Currency.Revision);
        Assert.Equal(world.Prepared.Progression.Currency.AdmittedSupply, final.Progression.Currency.AdmittedSupply);
        Assert.Equal(world.Prepared.Progression.Currency.Accounts.Select(account =>
                (account.AccountId, account.Kind, account.ActorId, account.Balance, account.Revision)),
            final.Progression.Currency.Accounts.Select(account =>
                (account.AccountId, account.Kind, account.ActorId, account.Balance, account.Revision)));
        Assert.Equal(world.Prepared.Progression.Actors.Select(actor => (actor.ActorId, actor.CurrentHp, actor.CurrentMp)),
            final.Progression.Actors.Select(actor => (actor.ActorId, actor.CurrentHp, actor.CurrentMp)));

        Assert.Empty(final.Coordination!.Proposals!.Open);
        Assert.Equal(6, final.Coordination.Proposals.Outcomes.Count);
        foreach (var boundary in expected)
        {
            var intent = Assert.IsType<ProposeMaterialIntent>(boundary.Creation.Intent);
            var result = Assert.IsType<WorldMaterialProposalOutcome>(final.Coordination.Proposals.FindOutcome(
                WorldMaterialProposal.DeriveIdentity(boundary.Creation.CommandId)));
            Assert.Equal(boundary.Kind, result.Kind);
            Assert.Equal(intent.Terms, result.Proposal.Terms);
            Assert.Equal(ConsentedMaterialLotKind.CountedSource, result.Proposal.Terms.LotKind);
            Assert.Equal(1UL, result.Proposal.Terms.Quantity);
            Assert.Equal(boundary.Creation.ControlWitness, result.Proposal.ControlWitness);
            Assert.Equal(new TrustedCommandDelivery(boundary.Creation.PrincipalId, boundary.Creation.OriginSessionId), result.Proposal.SenderDelivery);
            Assert.Equal(world.Prepared.GetResident(intent.Terms.SenderActorId).InventoryCustodianId, intent.Terms.SourceCustodianId);
            Assert.Equal(world.Prepared.GetResident(intent.Terms.RecipientActorId).InventoryCustodianId, intent.Terms.DestinationCustodianId);
            var before = authority.ReadRecordedStateForHost(result.WorldSequence - 1).State;
            var after = authority.ReadRecordedStateForHost(result.WorldSequence).State;
            if (result.Kind == WorldMaterialProposalOutcomeKind.Accepted)
            {
                var settlement = Assert.IsType<WorldMaterialProposalSettlement>(result.Settlement);
                var priorLot = before.Inventory.Custody.Lots.Single(lot => lot.LotId == intent.Terms.LotId);
                var movedLot = after.Inventory.Custody.Lots.Single(lot => lot.LotId == intent.Terms.LotId);
                Assert.Equal(priorLot.LotId, settlement.MovedLotId);
                Assert.Equal(intent.Terms.DestinationCustodianId, movedLot.CustodianId);
                Assert.Equal(priorLot.Source, movedLot.Source);
                Assert.Equal(priorLot.Definition, movedLot.Definition);
                Assert.Equal(priorLot.Revision + 1, movedLot.Revision);
                Assert.Equal(priorLot.Quantity, movedLot.Quantity);
                Assert.Equal(before.Inventory.Custody.Lots.Select(lot => lot.LotId), after.Inventory.Custody.Lots.Select(lot => lot.LotId));
                var terminal = Assert.Single(commands, command => command.CommandId == result.TerminalCommandId);
                Assert.Equal(terminal.ControlWitness, settlement.ControlWitness);
                Assert.Equal(new TrustedCommandDelivery(terminal.PrincipalId, terminal.OriginSessionId), settlement.RecipientDelivery);
            }
            else
            {
                Assert.Null(result.Settlement);
                Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
                Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
                var terminal = Assert.Single(commands, command => command.CommandId == result.TerminalCommandId);
                Assert.Equal(terminal.ControlWitness, result.Termination!.ControlWitness);
                Assert.Equal(new TrustedCommandDelivery(terminal.PrincipalId, terminal.OriginSessionId), result.Termination.Delivery);
            }
        }
        var finalOld = final.Coordination.Proposals.FindOutcome(WorldMaterialProposal.DeriveIdentity(expected[^1].Creation.CommandId))!;
        Assert.Equal(witness, finalOld.Proposal.ControlWitness);
        Assert.Null(finalOld.Termination!.ControlWitness);
        var originalOwner = world.Genesis.Inventory.Controls.Single(row => row.ActorId == world.Owner);
        Assert.Equal(new TrustedCommandDelivery(originalOwner.PrincipalId, originalOwner.SessionId), finalOld.Termination.Delivery);
        var refusal = authority.FindReceipt(refused, new(refused.PrincipalId, refused.OriginSessionId))!;
        var refusalBefore = authority.ReadRecordedStateForHost(refusal.CommitSequence - 1).State;
        var refusalAfter = authority.ReadRecordedStateForHost(refusal.CommitSequence).State;
        Assert.Equal(refusalBefore.Inventory.ContentHash, refusalAfter.Inventory.ContentHash);
        Assert.Equal(refusalBefore.Coordination!.Proposals!.ContentHash, refusalAfter.Coordination!.Proposals!.ContentHash);
        Assert.Equal(WorldControlPermissionStatus.Revoked, refusalBefore.ControlPermissions!.Slots.Single().Status);
        Assert.Null(refusalBefore.EffectiveControl!.Find(world.Owner)?.Witness);

        // Exactly two transfers of the same complete source interval return the original tool.
        // All other finite lot identities and quantities, including both operators' kits, stay exact.
        Assert.Equal(world.Prepared.Inventory.Custody.Lots.Count, final.Inventory.Custody.Lots.Count);
        foreach (CountedSourceLot original in world.Prepared.Inventory.Custody.Lots)
        {
            var actual = final.Inventory.Custody.Lots.Single(lot => lot.LotId == original.LotId);
            Assert.Equal(original.LotId == world.OwnerLot ? new CountedSourceLot(original.LotId, original.CustodianId,
                original.Definition, original.Source, original.Revision + 2, original.Protection, original.ReservedBy) : original, actual);
        }
        Assert.Equal(world.Prepared.Inventory.Custody.Revision + 2, final.Inventory.Custody.Revision);
        Assert.Contains(batches, batch => batch.ControllerEvent is { Kind: WorldControllerEventKind.AllowanceExpired } expired
            && expired.ActorId == world.Owner && expired.DisconnectId == witness.DisconnectId
            && expired.ElapsedHostMilliseconds == WorldControllerState.ReconnectAllowanceMilliseconds);
        Assert.Single(batches, batch => batch.ControlAssignmentEvent is { Kind: WorldControlAssignmentEventKind.Acquire } acquired
            && acquired.Witness == witness);
        Assert.Null(final.EffectiveControl!.Find(world.Owner)?.Witness);
        Assert.Equal(final.ContentHash, authority.ReadRecordedStateForHost(final.Revision).State.ContentHash);
        authority.BackupForHost(Path.Combine(output, "world.sqlite3"));
        File.WriteAllBytes(Path.Combine(output, "final.ofc"), WorldSnapshotCodec.Encode(final));
        Write(output, "receipts.json", submitted);
        Write(output, "journal.json", journal.Select(row => new { row.Sequence, row.Kind, row.AdvancingSteps,
            Cause = row.CauseId.ToString(), Batch = Convert.ToHexString(row.AcceptedBatch),
            Outcomes = Convert.ToHexString(row.Outcomes), Before = row.BeforeHash.Value, After = row.AfterHash.Value }));
    }

    private static void PreserveFinal(string output, SelectedGiftWorld world)
    {
        if (File.Exists(Path.Combine(output, "world.sqlite3"))) return;
        using var authority = SqliteWorldAuthority.OpenExisting(world.Database, WorldSnapshotCodec.Encode(world.Genesis));
        authority.BackupForHost(Path.Combine(output, "world.sqlite3"));
        File.WriteAllBytes(Path.Combine(output, "final.ofc"), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
    }

    private static void Write(string output, string name, object value) => File.WriteAllText(Path.Combine(output, name),
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
}
