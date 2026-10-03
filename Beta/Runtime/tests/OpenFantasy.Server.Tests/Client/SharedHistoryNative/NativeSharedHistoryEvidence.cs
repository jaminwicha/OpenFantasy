using System.Diagnostics;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;
using OpenFantasy.Server.Tests.ConnectedControl;

namespace OpenFantasy.Server.Tests.SharedHistoryNative;

public sealed partial class NativeSharedHistoryTests
{
    private sealed record HistoryBaseline(ulong Revision, WorldInstant Calendar, int BoardResponseSequence);

    private static async Task<HistoryBaseline> CaptureReadOnlyBaseline(NativeRosterProcess owner,
        IReadOnlyDictionary<EntityId, NativeRosterProcess> active, CancellationToken token)
    {
        foreach (var process in active.Values) await process.Send("pause");
        // Pause acknowledgement can precede an already-admitted tick. The authenticated board read
        // crosses the host serialization gate; a second read pins the same durable boundary.
        await owner.Send("control-open");
        ulong revision = owner.Last.GetProperty("ControlBoard").GetProperty("WorldRevision").GetUInt64();
        await Until(() => active.Values.All(process => !Selection(process).GetProperty("Ready").GetBoolean()
                && process.Last.GetProperty("LocallyPaused").GetBoolean() && process.View.Calendar == owner.View.Calendar),
            "All native peers did not observe the settled post-preparation pause.", active.Values, token);
        WorldInstant calendar = owner.View.Calendar;
        await owner.Send("control-refresh");
        Assert.Equal(revision, owner.Last.GetProperty("ControlBoard").GetProperty("WorldRevision").GetUInt64());
        Assert.Equal(calendar, owner.View.Calendar);
        var baseline = new HistoryBaseline(revision, calendar, owner.Last.GetProperty("Sequence").GetInt32());
        await owner.Send("control-close");
        Assert.Equal(calendar, owner.View.Calendar);
        return baseline;
    }

    private static JsonElement Selection(NativeRosterProcess process) => process.Last.GetProperty("Selection");
    private static JsonElement Slot(NativeRosterProcess process, EntityId owner, EntityId grantee) =>
        Assert.Single(process.Last.GetProperty("ControlBoard").GetProperty("Slots").EnumerateArray(), row =>
            row.GetProperty("Owner").GetString() == owner.ToString() && row.GetProperty("Grantee").GetString() == grantee.ToString());

    private static JsonElement History(NativeRosterProcess process)
    {
        int sequence = process.Last.GetProperty("Sequence").GetInt32();
        using var value = JsonDocument.Parse(File.ReadAllText(Path.Combine(process.DirectoryPath, $"{sequence:D4}.history.json")));
        Assert.Equal(sequence, value.RootElement.GetProperty("Sequence").GetInt32());
        Assert.Equal(process.Submissions.Count, value.RootElement.GetProperty("Submissions").GetInt32());
        return value.RootElement.GetProperty("History").Clone();
    }

    private static void AssertRecord(NativeRosterProcess process, EntityId actor, EntityId counterparty,
        EntityId subject, ConsentedMaterialTerms terms)
    {
        JsonElement history = History(process);
        Assert.True(history.GetProperty("Open").GetBoolean());
        Assert.False(history.GetProperty("IsBusy").GetBoolean());
        Assert.Equal(actor.ToString(), history.GetProperty("Actor").GetString());
        Assert.Equal(JsonValueKind.Null, history.GetProperty("Before").ValueKind);
        Assert.Equal(JsonValueKind.Null, history.GetProperty("Next").ValueKind);
        JsonElement entry = Assert.Single(history.GetProperty("Entries").EnumerateArray());
        Assert.Equal("MaterialProposal", entry.GetProperty("Kind").GetString());
        Assert.Equal(subject.ToString(), entry.GetProperty("Subject").GetString());
        Assert.Equal(counterparty.ToString(), entry.GetProperty("Counterparty").GetString());
        Assert.Equal("Withdrawn", entry.GetProperty("MaterialProposalStatus").GetString());
        Assert.True(JsonElement.DeepEquals(entry, history.GetProperty("Selected")),
            "The selected History row differs from the complete discovered entry.");
        var detail = history.GetProperty("Detail");
        Assert.True(JsonElement.DeepEquals(entry, detail.GetProperty("Entry")),
            "The inspected History row differs from the complete discovered entry.");
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("Quote").ValueKind);
        var proposal = detail.GetProperty("Proposal");
        Assert.Equal(subject.ToString(), proposal.GetProperty("Subject").GetString());
        Assert.Equal("Withdrawn", proposal.GetProperty("Status").GetString());
        Assert.Equal(entry.GetProperty("ClosedAtSequence").GetUInt64(), proposal.GetProperty("ClosedAtSequence").GetUInt64());
        Assert.Equal(JsonSerializer.Serialize(new
        {
            World = terms.WorldId.ToString(), Branch = terms.BranchId.ToString(), Sender = terms.SenderActorId.ToString(),
            Recipient = terms.RecipientActorId.ToString(), Kind = terms.LotKind.ToString(), Lot = terms.LotId.ToString(),
            Definition = terms.Definition.DefinitionId.ToString(), Version = terms.Definition.Version.Value,
            Source = terms.SourceCustodianId.ToString(), Destination = terms.DestinationCustodianId.ToString(),
            terms.Quantity, terms.ExpectedLotRevision
        }), JsonSerializer.Serialize(proposal.GetProperty("Terms")));
    }

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

    private static void Verify(string output, ConnectedControlWorld world, IReadOnlyList<NativeRosterProcess> processes,
        WorldControlWitness witness, WorldActionCommand creation, HistoryBaseline baseline)
    {
        StoredWorldCommit[] journal;
        using (var database = WorldDatabase.Open(world.Database, create: false)) journal = database.ReadJournal().ToArray();
        var batches = journal.Select(row => WorldStepCodec.Decode(row.AcceptedBatch)).ToArray();
        var gameplay = processes.SelectMany(process => process.Submissions).ToArray();
        var permission = processes.SelectMany(process => process.Last.GetProperty("PermissionSubmissions").EnumerateArray())
            .Select(value => value.Clone()).ToArray();
        Assert.Equal(2, gameplay.Length); // Explicit proposal and withdrawal; every History operation is read-only.
        Assert.Equal(3, permission.Length);
        var submitted = gameplay.Concat(permission).ToArray();
        var commands = submitted.Select(value => WorldActionCodec.Decode(Convert.FromHexString(value.GetProperty("Command").GetString()!))).ToArray();
        Assert.Equal(commands.Length, commands.Select(command => command.CommandId).Distinct().Count());
        Assert.Single(commands, command => command.Intent is ProposeMaterialIntent);
        Assert.Single(commands, command => command.Intent is WithdrawMaterialProposalIntent);
        var grant = Assert.Single(commands, command => command.Intent is OfferControlPermissionIntent);
        var acceptance = Assert.Single(commands, command => command.Intent is AcceptControlPermissionIntent);
        Assert.Single(commands, command => command.Intent is RevokeControlPermissionIntent);
        Assert.Equal(WorldControlCapabilities.PrivateHistory, Assert.IsType<OfferControlPermissionIntent>(grant.Intent).Capabilities);
        Assert.Equal(grant.CommandId, witness.GrantCommandId);
        Assert.Equal(acceptance.CommandId, witness.AcceptanceCommandId);
        Assert.Equal(WorldControlCapabilities.PrivateHistory, witness.Capabilities);

        using var authority = SqliteWorldAuthority.OpenExisting(world.Database, WorldSnapshotCodec.Encode(world.Genesis));
        var final = authority.ReadStateForHost();
        var browsingStart = authority.ReadRecordedStateForHost(baseline.Revision).State;
        Assert.Equal(baseline.Calendar, browsingStart.Calendar);
        Assert.Equal(baseline.Calendar, final.Calendar);
        Assert.Equal(browsingStart.AdvancingSteps, final.AdvancingSteps);
        Assert.All(journal.Where(row => row.Sequence > baseline.Revision), row => Assert.Equal(browsingStart.AdvancingSteps, row.AdvancingSteps));
        Assert.All(journal.Where(row => row.Sequence > baseline.Revision), row =>
        {
            var batch = WorldStepCodec.Decode(row.AcceptedBatch);
            Assert.Contains(batch.Kind, new[] { WorldBatchKind.ControlPermission, WorldBatchKind.ControllerLifecycle, WorldBatchKind.ControlAssignment });
            Assert.Empty(batch.Movement);
        });
        Assert.Equal(permission.Select(value => WorldActionCodec.Decode(Convert.FromHexString(value.GetProperty("Command").GetString()!)).CommandId).Order(),
            journal.Where(row => row.Sequence > baseline.Revision).Select(row => WorldStepCodec.Decode(row.AcceptedBatch))
                .SelectMany(batch => batch.Actions).Select(command => command.CommandId).Order());
        foreach (var value in submitted)
        {
            var command = WorldActionCodec.Decode(Convert.FromHexString(value.GetProperty("Command").GetString()!));
            if (value.TryGetProperty("IsRetry", out var retry)) Assert.False(retry.GetBoolean());
            var receipt = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command, new(command.PrincipalId, command.OriginSessionId)));
            Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
            Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            Assert.Equal(Convert.FromHexString(value.GetProperty("Receipt").GetString()!), WorldReceiptCodecV1.Encode(receipt));
            Assert.Contains(batches.SelectMany(batch => batch.Actions), actual =>
                WorldActionCodec.Encode(actual).AsSpan().SequenceEqual(WorldActionCodec.Encode(command)));
            Assert.Null(command.ControlWitness); // The delegate only reads history; it never commits on behalf of the owner.
            var binding = world.Genesis.Inventory.Controls.Single(row => row.ActorId == command.ActorId);
            Assert.Equal(binding.PrincipalId, command.PrincipalId);
            Assert.Equal(binding.SessionId, command.OriginSessionId);
        }
        var intent = Assert.IsType<ProposeMaterialIntent>(creation.Intent);
        Assert.Empty(final.Coordination!.Proposals!.Open);
        var outcome = Assert.Single(final.Coordination.Proposals.Outcomes);
        Assert.Equal(WorldMaterialProposal.DeriveIdentity(creation.CommandId), outcome.Proposal.ProposalId);
        Assert.Equal(WorldMaterialProposalOutcomeKind.Withdrawn, outcome.Kind);
        Assert.Equal(intent.Terms, outcome.Proposal.Terms);
        Assert.Null(outcome.Settlement);
        Assert.True(outcome.WorldSequence <= baseline.Revision, "The actual terminal gift must precede all History browsing.");
        var before = authority.ReadRecordedStateForHost(outcome.WorldSequence - 1).State;
        var after = authority.ReadRecordedStateForHost(outcome.WorldSequence).State;
        Assert.Equal(ConsentedMaterialLotKind.CountedSource, intent.Terms.LotKind);
        Assert.Equal(before.Inventory.Custody.Lots.Single(lot => lot.LotId == intent.Terms.LotId),
            after.Inventory.Custody.Lots.Single(lot => lot.LotId == intent.Terms.LotId));

        // Original admission remains the replay root. The owner deliberately resumed during gift
        // preparation, so lawful earlier NPC work is retained; exact read-only conservation begins
        // at the authenticated, serialized post-pause snapshot rather than at genesis.
        Assert.Equal(browsingStart.Inventory.ContentHash, final.Inventory.ContentHash);
        Assert.Equal(world.Genesis.Residents.OrderBy(row => row.ActorId).Select(row => (row.ActorId, row.ControllerKind, row.InventoryCustodianId)),
            final.Residents.OrderBy(row => row.ActorId).Select(row => (row.ActorId, row.ControllerKind, row.InventoryCustodianId)));
        Assert.Equal(world.Genesis.Space.Actors.OrderBy(row => row.ActorId).Select(row => (row.ActorId, row.Binding.FormInstanceId, row.Binding.PhysicalBodyId)),
            browsingStart.Space.Actors.OrderBy(row => row.ActorId).Select(row => (row.ActorId, row.Binding.FormInstanceId, row.Binding.PhysicalBodyId)));
        Assert.Equal(browsingStart.Space.Actors.OrderBy(row => row.ActorId).Select(row => (row.ActorId, row.Binding.FormInstanceId, row.Binding.PhysicalBodyId, row.Feet)),
            final.Space.Actors.OrderBy(row => row.ActorId).Select(row => (row.ActorId, row.Binding.FormInstanceId, row.Binding.PhysicalBodyId, row.Feet)));
        Assert.Equal(browsingStart.Progression!.Actors.OrderBy(row => row.ActorId).Select(row => (row.ActorId, row.CurrentHp, row.CurrentMp)),
            final.Progression!.Actors.OrderBy(row => row.ActorId).Select(row => (row.ActorId, row.CurrentHp, row.CurrentMp)));
        Assert.Equal(browsingStart.Progression.Currency.Accounts.OrderBy(row => row.AccountId).Select(row => (row.AccountId, row.Kind, row.ActorId, row.Balance, row.Revision)),
            final.Progression.Currency.Accounts.OrderBy(row => row.AccountId).Select(row => (row.AccountId, row.Kind, row.ActorId, row.Balance, row.Revision)));
        Assert.Equal(browsingStart.Production!.ContentHash, final.Production!.ContentHash);
        Assert.Equal(browsingStart.Care!.ContentHash, final.Care!.ContentHash);
        RecordPreparationActivity(output, world, authority, browsingStart, journal,
            commands.Where(command => command.Intent is not ControlPermissionIntent).ToArray());
        Assert.Contains(batches, batch => batch.ControllerEvent is { Kind: WorldControllerEventKind.AllowanceExpired } expired
            && expired.ActorId == world.Owner && expired.DisconnectId == witness.DisconnectId
            && expired.ElapsedHostMilliseconds == WorldControllerState.ReconnectAllowanceMilliseconds);
        Assert.Single(batches, batch => batch.ControlAssignmentEvent is { Kind: WorldControlAssignmentEventKind.Acquire } acquired
            && acquired.Witness == witness);
        Assert.Equal(WorldControlPermissionStatus.Revoked, Assert.Single(final.ControlPermissions!.Slots).Status);
        Assert.Null(final.EffectiveControl!.Find(world.Owner)?.Witness);
        Assert.Equal(final.ContentHash, authority.ReadRecordedStateForHost(final.Revision).State.ContentHash);
        authority.BackupForHost(Path.Combine(output, "world.sqlite3"));
        File.WriteAllBytes(Path.Combine(output, "browsing-start.ofc"), WorldSnapshotCodec.Encode(browsingStart));
        File.WriteAllBytes(Path.Combine(output, "final.ofc"), WorldSnapshotCodec.Encode(final));
        Write(output, "receipts.json", submitted);
        Write(output, "journal.json", journal.Select(row => new { row.Sequence, row.Kind, row.AdvancingSteps,
            Cause = row.CauseId.ToString(), Batch = Convert.ToHexString(row.AcceptedBatch),
            Outcomes = Convert.ToHexString(row.Outcomes), Before = row.BeforeHash.Value, After = row.AfterHash.Value }));
    }

    private static void RecordPreparationActivity(string output, ConnectedControlWorld world,
        SqliteWorldAuthority authority, WorldSnapshot browsingStart, IReadOnlyList<StoredWorldCommit> journal,
        IReadOnlyList<WorldActionCommand> submittedGameplay)
    {
        var preparation = journal.Where(row => row.Sequence <= browsingStart.Revision)
            .Select(row => (row.Sequence, Batch: WorldStepCodec.Decode(row.AcceptedBatch))).ToArray();
        var npcActions = preparation.SelectMany(row => row.Batch.Actions.Select(command => (row.Sequence, Command: command)))
            .Where(row => world.Genesis.GetResident(row.Command.ActorId).ControllerKind == ResidentControllerKind.Host).ToArray();
        var movement = preparation.SelectMany(row => row.Batch.Movement.Select(sample => (row.Sequence, Sample: sample))).ToArray();
        Assert.Equal(submittedGameplay.Select(command => Convert.ToHexString(WorldActionCodec.Encode(command))).Order(),
            preparation.SelectMany(row => row.Batch.Actions).Where(command => world.Actors.Contains(command.ActorId))
                .Select(command => Convert.ToHexString(WorldActionCodec.Encode(command))).Order());
        foreach (var row in npcActions)
        {
            var binding = world.Genesis.Inventory.Controls.Single(value => value.ActorId == row.Command.ActorId);
            Assert.Equal(binding.PrincipalId, row.Command.PrincipalId);
            Assert.Equal(binding.SessionId, row.Command.OriginSessionId);
            Assert.Null(row.Command.ControlWitness);
            Assert.NotNull(authority.FindReceipt(row.Command, new(row.Command.PrincipalId, row.Command.OriginSessionId)));
        }
        foreach (var row in movement)
        {
            Assert.Equal(ResidentControllerKind.Host, world.Genesis.GetResident(row.Sample.ActorId).ControllerKind);
            var binding = world.Genesis.Inventory.Controls.Single(value => value.ActorId == row.Sample.ActorId);
            Assert.Equal(new TrustedCommandDelivery(binding.PrincipalId, binding.SessionId), row.Sample.Delivery);
            Assert.Null(row.Sample.ControlWitness);
        }
        foreach (EntityId human in world.Actors)
            Assert.Equal(world.Genesis.Space.GetActor(human).Feet, browsingStart.Space.GetActor(human).Feet);
        Write(output, "preparation-observed.json", new
        {
            ThroughRevision = browsingStart.Revision, world.Genesis.Calendar, BaselineCalendar = browsingStart.Calendar,
            OriginalAdvancingSteps = world.Genesis.AdvancingSteps, BaselineAdvancingSteps = browsingStart.AdvancingSteps,
            InventoryBefore = world.Genesis.Inventory.ContentHash.Value, InventoryAfter = browsingStart.Inventory.ContentHash.Value,
            ProductionBefore = world.Genesis.Production!.ContentHash.Value, ProductionAfter = browsingStart.Production!.ContentHash.Value,
            CareBefore = world.Genesis.Care!.ContentHash.Value, CareAfter = browsingStart.Care!.ContentHash.Value,
            NpcActions = npcActions.Select(row => new { row.Sequence, Actor = row.Command.ActorId.ToString(),
                Principal = row.Command.PrincipalId.ToString(), Session = row.Command.OriginSessionId.ToString(),
                Intent = row.Command.Intent.GetType().Name, Command = Convert.ToHexString(WorldActionCodec.Encode(row.Command)) }).ToArray(),
            Movement = movement.Select(row => new { row.Sequence, Actor = row.Sample.ActorId.ToString(),
                Principal = row.Sample.Delivery.PrincipalId.ToString(), Session = row.Sample.Delivery.SessionId.ToString(),
                row.Sample.DeltaX, row.Sample.DeltaY }).ToArray(),
            ChangedFeet = browsingStart.Space.Actors.Where(actor => actor.Feet != world.Genesis.Space.GetActor(actor.ActorId).Feet)
                .Select(actor => new { Actor = actor.ActorId.ToString(), Before = world.Genesis.Space.GetActor(actor.ActorId).Feet, After = actor.Feet }).ToArray()
        });
    }

    private static void PreserveFinal(string output, ConnectedControlWorld world)
    {
        if (File.Exists(Path.Combine(output, "world.sqlite3"))) return;
        using var authority = SqliteWorldAuthority.OpenExisting(world.Database, WorldSnapshotCodec.Encode(world.Genesis));
        authority.BackupForHost(Path.Combine(output, "world.sqlite3"));
        File.WriteAllBytes(Path.Combine(output, "final.ofc"), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
    }

    private static void Write(string output, string name, object value) => File.WriteAllText(Path.Combine(output, name),
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
}
