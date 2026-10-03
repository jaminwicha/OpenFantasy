using System.Diagnostics;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;
using OpenFantasy.Server.Tests.ConnectedControl;

namespace OpenFantasy.Server.Tests.SelectedControlNative;

public sealed partial class NativeSelectedControlJourneyTests
{
    private static JsonElement Selection(NativeRosterProcess process) => process.Last.GetProperty("Selection");
    private static EntityId SelectedActor(NativeRosterProcess process) => EntityId.Parse(Selection(process).GetProperty("Actor").GetString()!);
    private static SubunitPosition SelectedFeet(NativeRosterProcess process)
    {
        var feet = Selection(process).GetProperty("Feet");
        return new(feet.GetProperty("X").GetInt64(), feet.GetProperty("Y").GetInt64(), feet.GetProperty("Z").GetInt64());
    }
    private static int SelectedTargetHp(NativeRosterProcess process, EntityId target) =>
        Selection(process).GetProperty("EncounterActors").EnumerateArray()
            .Single(actor => actor.GetProperty("Actor").GetString() == target.ToString()).GetProperty("Hp").GetInt32();
    private static JsonElement Slot(NativeRosterProcess process, EntityId owner, EntityId grantee) =>
        Assert.Single(process.Last.GetProperty("ControlBoard").GetProperty("Slots").EnumerateArray(), row =>
            row.GetProperty("Owner").GetString() == owner.ToString() && row.GetProperty("Grantee").GetString() == grantee.ToString());

    private static void AssertHomePaused(NativeRosterProcess process, EntityId actor)
    {
        AssertSelectedPaused(process, actor, actor);
        Assert.Equal(JsonValueKind.Null, Selection(process).GetProperty("Witness").ValueKind);
    }

    private static void AssertSelectedPaused(NativeRosterProcess process, EntityId home, EntityId selected)
    {
        Assert.Equal(home.ToString(), Selection(process).GetProperty("HomeActor").GetString());
        Assert.Equal(selected, SelectedActor(process));
        Assert.Equal(home, process.View.PlayerActorId);
        Assert.True(process.Last.GetProperty("LocallyPaused").GetBoolean());
        Assert.False(Selection(process).GetProperty("Ready").GetBoolean());
    }

    private static WorldActionCommand LastAction(NativeRosterProcess process)
    {
        var submitted = process.Submissions[^1];
        Assert.False(submitted.GetProperty("IsRetry").GetBoolean());
        Assert.Equal(JsonValueKind.String, submitted.GetProperty("Receipt").ValueKind);
        return WorldActionCodec.Decode(Convert.FromHexString(submitted.GetProperty("Command").GetString()!));
    }

    private static Task Observe(IEnumerable<NativeRosterProcess> processes) =>
        Task.WhenAll(processes.Select(process => process.Send("observe")));

    private static async Task Until(Func<bool> condition, string message,
        IEnumerable<NativeRosterProcess> processes, CancellationToken token)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            await Observe(processes);
            token.ThrowIfCancellationRequested();
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5), message);
            if (condition()) return;
            await Task.Delay(20, token);
        }
    }

    private static void Verify(string output, ConnectedControlWorld world, IReadOnlyList<NativeRosterProcess> processes,
        WorldControlWitness witness, WorldActionCommand attack, IReadOnlyList<WorldActionCommand> waits, int targetHp)
    {
        StoredWorldCommit[] commits;
        using (var database = WorldDatabase.Open(world.Database, create: false)) commits = database.ReadJournal().ToArray();
        var batches = commits.Select(commit => WorldStepCodec.Decode(commit.AcceptedBatch)).ToArray();
        var gameplay = processes.SelectMany(process => process.Submissions).ToArray();
        var consent = processes.SelectMany(process => process.Last.GetProperty("PermissionSubmissions").EnumerateArray())
            .Select(value => value.Clone()).ToArray();
        Assert.Equal(5, gameplay.Length);
        Assert.Equal(3, consent.Length);
        var submitted = gameplay.Concat(consent).ToArray();
        var commands = submitted.Select(value => WorldActionCodec.Decode(Convert.FromHexString(value.GetProperty("Command").GetString()!))).ToArray();
        Assert.Equal(commands.Length, commands.Select(command => command.CommandId).Distinct().Count());
        Assert.Single(commands, command => command.Intent is EnterEncounterIntent);
        Assert.Single(commands, command => command.Intent is QueueEncounterAbilityIntent);
        Assert.Equal(3, commands.Count(command => command.Intent is QueueEncounterWaitIntent));
        var offer = Assert.Single(commands, command => command.Intent is OfferControlPermissionIntent);
        var acceptance = Assert.Single(commands, command => command.Intent is AcceptControlPermissionIntent);
        var revoke = Assert.Single(commands, command => command.Intent is RevokeControlPermissionIntent);
        Assert.Equal(world.Owner, offer.ActorId);
        Assert.Equal(world.Grantee, acceptance.ActorId);
        Assert.Equal(world.Owner, revoke.ActorId);
        Assert.Equal(witness.GrantCommandId, offer.CommandId);
        Assert.Equal(witness.AcceptanceCommandId, acceptance.CommandId);
        Assert.Equal(WorldControlCapabilities.Combat | WorldControlCapabilities.Traversal,
            Assert.IsType<OfferControlPermissionIntent>(offer.Intent).Capabilities);
        foreach (var command in new[] { offer, acceptance, revoke }) Assert.Null(command.ControlWitness);

        using var authority = SqliteWorldAuthority.OpenExisting(world.Database, WorldSnapshotCodec.Encode(world.Genesis));
        var final = authority.ReadStateForHost();
        Assert.Equal(world.Genesis.Inventory.Controls, final.Inventory.Controls);
        Assert.Equal(world.Genesis.Residents.Select(actor => (actor.ActorId, actor.ControllerKind, actor.InventoryCustodianId)),
            final.Residents.Select(actor => (actor.ActorId, actor.ControllerKind, actor.InventoryCustodianId)));
        Assert.Equal(world.Genesis.Space.Actors.Select(actor => (actor.ActorId, actor.Binding.FormInstanceId, actor.Binding.PhysicalBodyId)),
            final.Space.Actors.Select(actor => (actor.ActorId, actor.Binding.FormInstanceId, actor.Binding.PhysicalBodyId)));
        foreach (var value in submitted)
        {
            var command = WorldActionCodec.Decode(Convert.FromHexString(value.GetProperty("Command").GetString()!));
            if (value.TryGetProperty("IsRetry", out var retry)) Assert.False(retry.GetBoolean());
            var receipt = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command, new(command.PrincipalId, command.OriginSessionId)));
            Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
            Assert.Equal(Convert.FromHexString(value.GetProperty("Receipt").GetString()!), WorldReceiptCodecV1.Encode(receipt));
            Assert.Contains(batches.SelectMany(batch => batch.Actions), recorded => WorldActionCodec.Encode(recorded).AsSpan()
                .SequenceEqual(WorldActionCodec.Encode(command)));
        }
        Assert.Equal(world.Actors.Skip(1).Order(), waits.Select(command => command.ActorId).Order());
        foreach (var wait in waits)
        {
            var binding = world.Genesis.Inventory.Controls.Single(row => row.ActorId == wait.ActorId);
            Assert.Equal(binding.PrincipalId, wait.PrincipalId);
            Assert.Equal(binding.SessionId, wait.OriginSessionId);
            Assert.Null(wait.ControlWitness);
        }
        Assert.Contains(batches, batch => batch.ControllerEvent is { Kind: WorldControllerEventKind.AllowanceExpired } expired
            && expired.ActorId == world.Owner && expired.DisconnectId == witness.DisconnectId
            && expired.ElapsedHostMilliseconds == WorldControllerState.ReconnectAllowanceMilliseconds);
        Assert.Single(batches, batch => batch.ControlAssignmentEvent is { Kind: WorldControlAssignmentEventKind.Acquire } acquired
            && acquired.Witness == witness);
        Assert.Contains(batches, batch => batch.ControllerEvent is { Kind: WorldControllerEventKind.Reconnected } restored
            && restored.ActorId == world.Owner && restored.DisconnectId == witness.DisconnectId);
        var movement = batches.SelectMany(batch => batch.Movement).Where(input => input.ControlWitness is not null).ToArray();
        Assert.NotEmpty(movement);
        var grantee = world.Genesis.Inventory.Controls.Single(row => row.ActorId == world.Grantee);
        Assert.All(movement, input =>
        {
            Assert.Equal(world.Owner, input.ActorId);
            Assert.Equal(witness, input.ControlWitness);
            Assert.Equal(new TrustedCommandDelivery(grantee.PrincipalId, grantee.SessionId), input.Delivery);
            Assert.Equal(world.EntryMovement, (input.DeltaX, input.DeltaY));
        });
        var evidence = Assert.Single(final.ResourceHistory!.Evidence.OfType<WorldEncounterResourceEvidence>(),
            value => value.CommandId == attack.CommandId);
        Assert.Equal(WorldActionCodec.Encode(attack), WorldActionCodec.Encode(evidence.Request));
        Assert.Equal(world.Owner, evidence.Resolution.ActorId);
        Assert.Equal(EncounterActionResolutionStatus.Damaged, evidence.Resolution.Status);
        Assert.Equal((uint)targetHp, evidence.Resolution.TargetHpBefore);
        Assert.True(evidence.Resolution.Damage > 0);
        Assert.Equal(evidence.Resolution.TargetHpBefore - evidence.Resolution.Damage, evidence.Resolution.TargetHpAfter);
        var paid = authority.ReadRecordedStateForHost(evidence.Resolution.WorldSequence).State;
        var ability = paid.EncounterActions!.GetAbility(Assert.IsType<QueueEncounterAbilityIntent>(attack.Intent).AbilityDefinitionId);
        Assert.Equal(ability.ManaCost, evidence.Resolution.ManaSpent);
        Assert.Equal(ability.Delay(paid.Pacing!.Encounter!.GetActor(world.Owner).NormalizedSpeed), evidence.Resolution.Delay);
        Assert.True(evidence.Resolution.Delay > 0);
        Assert.Equal(evidence.Resolution.Delay, paid.Pacing.Encounter.GetActor(world.Owner).RemainingDelay);
        Assert.Equal(world.Prepared.Progression!.GetActor(world.Owner).CurrentMp - (int)ability.ManaCost,
            paid.Progression!.GetActor(world.Owner).CurrentMp);
        Assert.Equal(world.Prepared.Progression.GetActor(world.Grantee).CurrentMp, paid.Progression.GetActor(world.Grantee).CurrentMp);
        Assert.Null(final.EffectiveControl!.Find(world.Owner)?.Witness);
        Assert.Equal(final.ContentHash, authority.ReadRecordedStateForHost(final.Revision).State.ContentHash);
        authority.BackupForHost(Path.Combine(output, "world.sqlite3"));
        File.WriteAllBytes(Path.Combine(output, "final.ofc"), WorldSnapshotCodec.Encode(final));
        Write(output, "receipts.json", submitted);
        Write(output, "journal.json", commits.Select(commit => new { commit.Sequence, commit.Kind, commit.AdvancingSteps,
            Cause = commit.CauseId.ToString(), Batch = Convert.ToHexString(commit.AcceptedBatch),
            Outcomes = Convert.ToHexString(commit.Outcomes), Before = commit.BeforeHash.Value, After = commit.AfterHash.Value }));
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
