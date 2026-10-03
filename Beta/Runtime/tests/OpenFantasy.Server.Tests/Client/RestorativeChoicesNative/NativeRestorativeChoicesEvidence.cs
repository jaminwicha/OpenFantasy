using System.Diagnostics;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.RestorativeChoicesNative;

public sealed partial class NativeRestorativeChoicesTests
{
    private static JsonElement Selection(NativeRosterProcess process) => process.Last.GetProperty("Selection");
    private static JsonElement Care(NativeRosterProcess process) => process.Last.GetProperty("CareLibrary");
    private static JsonElement Slot(NativeRosterProcess process, EntityId owner, EntityId grantee) =>
        Assert.Single(process.Last.GetProperty("ControlBoard").GetProperty("Slots").EnumerateArray(), row =>
            row.GetProperty("Owner").GetString() == owner.ToString() && row.GetProperty("Grantee").GetString() == grantee.ToString());
    private static void AssertPaused(NativeRosterProcess process, EntityId home, EntityId selected)
    {
        Assert.Equal(home, process.View.PlayerActorId);
        Assert.Equal(home.ToString(), Selection(process).GetProperty("HomeActor").GetString());
        Assert.Equal(selected.ToString(), Selection(process).GetProperty("Actor").GetString());
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
    private static async Task Until(Func<bool> predicate, string message, IEnumerable<NativeRosterProcess> processes, CancellationToken token)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            await Task.WhenAll(processes.Select(process => process.Send("observe")));
            token.ThrowIfCancellationRequested();
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5), message);
            if (predicate()) return;
            await Task.Delay(25, token);
        }
    }

    private static void Verify(string output, RestorativeChoicesWorld world, IReadOnlyList<NativeRosterProcess> processes,
        WorldControlWitness witness, WorldActionCommand withdrawal, WorldActionCommand dose, JsonElement stale)
    {
        StoredWorldCommit[] journal;
        StoredWorldIdentity[] storedIdentities;
        using (var database = WorldDatabase.Open(world.Database, create: false))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(world.Genesis), database.ReadGenesis());
            journal = database.ReadJournal().ToArray();
            storedIdentities = database.ReadIdentities().ToArray();
        }
        var gameplay = processes.SelectMany(process => process.Submissions).ToArray();
        var permissions = processes.SelectMany(process => process.Last.GetProperty("PermissionSubmissions").EnumerateArray())
            .Select(value => value.Clone()).ToArray();
        Assert.Equal(2, gameplay.Length);
        Assert.Equal(3, permissions.Length);
        Assert.All(gameplay, value => Assert.False(value.GetProperty("IsRetry").GetBoolean()));
        var submitted = gameplay.Concat(permissions).ToArray();
        var native = submitted.Select(value => WorldActionCodec.Decode(Convert.FromHexString(value.GetProperty("Command").GetString()!))).ToArray();
        Assert.Equal(native.Length, native.Select(command => command.CommandId).Distinct().Count());
        var offer = Assert.Single(native, command => command.Intent is OfferControlPermissionIntent);
        var accepted = Assert.Single(native, command => command.Intent is AcceptControlPermissionIntent);
        var revoked = Assert.Single(native, command => command.Intent is RevokeControlPermissionIntent);
        Assert.Equal(WorldControlCapabilities.Recovery | WorldControlCapabilities.CareDisposition,
            Assert.IsType<OfferControlPermissionIntent>(offer.Intent).Capabilities);
        Assert.Equal(offer.CommandId, witness.GrantCommandId);
        Assert.Equal(accepted.CommandId, witness.AcceptanceCommandId);
        Assert.Equal(world.Owner, offer.ActorId);
        Assert.Equal(world.Grantee, accepted.ActorId);
        Assert.Equal(world.Owner, revoked.ActorId);
        Assert.Null(offer.ControlWitness);
        Assert.Null(accepted.ControlWitness);
        Assert.Null(revoked.ControlWitness);

        // Replay every preparation and native boundary from the one retained original genesis.
        var processor = new WorldStepProcessor();
        var state = world.Genesis;
        var identities = new Dictionary<EntityId, StoredWorldIdentity>();
        var batches = new List<WorldStepBatch>();
        var recorded = new Dictionary<ulong, WorldSnapshot>();
        foreach (var row in journal)
        {
            Assert.Equal(state.ContentHash, row.BeforeHash);
            var batch = WorldStepCodec.Decode(row.AcceptedBatch);
            Assert.Equal(row.AcceptedBatch, WorldStepCodec.Encode(batch));
            var decision = processor.Decide(state, batch, id => world.Genesis.IdentityIndex.ContainsEntity(id) || identities.ContainsKey(id));
            Assert.Equal(row.AfterHash, decision.Candidate.ContentHash);
            Assert.Equal(row.Outcomes, WorldReceiptCodecV1.EncodeBatch(decision.Receipts));
            foreach (var identity in decision.Identities)
                Assert.True(identities.TryAdd(identity.Identity, new(identity.Identity, row.Sequence, (WorldIdentityKind)identity.Kind)));
            if (batch.Actions.Any(command => command.CommandId == withdrawal.CommandId || command.CommandId == dose.CommandId))
            {
                recorded[state.Revision] = state;
                recorded[decision.Candidate.Revision] = decision.Candidate;
            }
            if (decision.Candidate.Revision == world.Prepared.Revision)
                Assert.Equal(world.Prepared.ContentHash, decision.Candidate.ContentHash);
            batches.Add(batch);
            state = decision.Candidate;
        }
        Assert.Equal(identities.Values.OrderBy(value => value.Identity), storedIdentities.OrderBy(value => value.Identity));
        using var authority = SqliteWorldAuthority.OpenExisting(world.Database, WorldSnapshotCodec.Encode(world.Genesis));
        var final = authority.ReadStateForHost();
        Assert.Equal(state.ContentHash, final.ContentHash);
        foreach (var value in submitted)
        {
            var command = WorldActionCodec.Decode(Convert.FromHexString(value.GetProperty("Command").GetString()!));
            var receipt = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command, new(command.PrincipalId, command.OriginSessionId)));
            Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
            Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            Assert.Equal(Convert.FromHexString(value.GetProperty("Receipt").GetString()!), WorldReceiptCodecV1.Encode(receipt));
            Assert.Single(batches.SelectMany(batch => batch.Actions), actual => actual.CommandId == command.CommandId
                && WorldActionCodec.Encode(actual).AsSpan().SequenceEqual(WorldActionCodec.Encode(command)));
            var binding = world.Genesis.Inventory.Controls.Single(control => control.ActorId == (command.ControlWitness is null ? command.ActorId : world.Grantee));
            Assert.Equal(binding.PrincipalId, command.PrincipalId);
            Assert.Equal(binding.SessionId, command.OriginSessionId);
            if (command.ControlWitness is not null)
            {
                Assert.Equal(world.Owner, command.ActorId);
                Assert.Equal(witness, command.ControlWitness);
                Assert.True(command.Intent is RevokeCarePermissionIntent or UseRestorativeItemIntent);
            }
        }
        var withdrawalReceipt = authority.FindReceipt(withdrawal, new(withdrawal.PrincipalId, withdrawal.OriginSessionId))!;
        var beforeWithdrawal = recorded[withdrawalReceipt.CommitSequence - 1];
        var afterWithdrawal = recorded[withdrawalReceipt.CommitSequence];
        var permissionBefore = beforeWithdrawal.Care!.Ordinary!.GetPermission(world.Permission);
        var permissionAfter = afterWithdrawal.Care!.Ordinary!.GetPermission(world.Permission);
        var withdrawalIntent = Assert.IsType<RevokeCarePermissionIntent>(withdrawal.Intent);
        Assert.True(permissionBefore.IsActive);
        Assert.Equal(permissionBefore.PermissionId, withdrawalIntent.PermissionId);
        Assert.Equal(permissionBefore.GrantVersion, withdrawalIntent.ExpectedPermissionGrantVersion);
        Assert.Equal(permissionBefore.Revision, withdrawalIntent.ExpectedPermissionRevision);
        Assert.False(permissionAfter.IsActive);
        Assert.Equal(permissionBefore.GrantVersion, permissionAfter.GrantVersion);
        Assert.Equal(permissionBefore.Revision + 1, permissionAfter.Revision);
        Assert.Equal(withdrawal.CommandId, permissionAfter.Revocation!.CommandId);
        Assert.Equal(world.Owner, permissionAfter.Revocation.PatientActorId);
        Assert.Equal(withdrawalReceipt.CommitSequence, permissionAfter.Revocation.WorldSequence);
        Assert.Equal(beforeWithdrawal.Inventory.ContentHash, afterWithdrawal.Inventory.ContentHash);
        Assert.Equal(beforeWithdrawal.Production!.ContentHash, afterWithdrawal.Production!.ContentHash);
        Assert.Equal(beforeWithdrawal.Progression!.ContentHash, afterWithdrawal.Progression!.ContentHash);

        var use = Assert.Single(final.Care!.Ordinary!.Uses);
        var doseIntent = Assert.IsType<UseRestorativeItemIntent>(dose.Intent);
        var doseBefore = recorded[use.WorldSequence - 1];
        var doseAfter = recorded[use.WorldSequence];
        Assert.Equal(WorldActionRejection.None, WorldRestorativeActions.Evaluate(doseBefore, world.Owner, doseIntent));
        Assert.Equal(dose.CommandId, use.CommandId);
        Assert.Equal(world.Owner, use.ActorId);
        Assert.Equal(world.Owner, use.RecipientActorId);
        Assert.Equal(world.UseDefinition, use.UseDefinitionId);
        Assert.Equal(doseIntent.UseDefinitionVersion, use.UseDefinitionVersion);
        Assert.Null(use.PermissionId);
        Assert.Null(use.PermissionGrantVersion);
        Assert.Null(use.PermissionRevision);
        Assert.InRange(use.HpBefore, 1, 19);
        Assert.Equal(use.HpBefore + 40, use.HpAfter);
        Assert.InRange(use.HpAfter, 41, 59);
        Assert.Equal(doseBefore.Progression!.GetActor(world.Owner).Revision, use.PatientRevisionBefore);
        Assert.Equal(doseAfter.Progression!.GetActor(world.Owner).Revision, use.PatientRevisionAfter);
        Assert.Equal(RestorativeUseDefinition.ExplorationDurationMilliseconds, use.ExplorationMilliseconds);
        var claim = Assert.Single(final.Production!.ConsumedItems);
        Assert.Equal(EntityId.DeriveSpawned(dose.CommandId, 0), claim.ConsumptionId);
        Assert.Equal(use.ConsumptionId, claim.ConsumptionId);
        Assert.Equal(dose.CommandId, claim.CommandId);
        Assert.Equal(world.Owner, claim.ActorId);
        Assert.Equal(world.Owner, claim.RecipientActorId);
        EntityId bag = world.Prepared.GetResident(world.Owner).InventoryCustodianId;
        Assert.Equal(bag, claim.CustodianId);
        var raw = world.Prepared.Inventory.Custody.GetLot(doseIntent.Ingredient.LotId);
        Assert.Equal(IngredientLotKind.Source, claim.Ingredient.Kind);
        Assert.Equal(raw.LotId, claim.Ingredient.LotId);
        Assert.Equal(raw.Definition, claim.Ingredient.Definition);
        Assert.Equal(raw.Revision, claim.Ingredient.Revision);
        Assert.Equal(raw.Source, claim.Ingredient.Source);
        Assert.Equal(1UL, raw.Quantity);
        Assert.Equal(1UL, claim.Ingredient.Quantity);
        Assert.Equal(bag, raw.CustodianId);
        Assert.Equal(world.Prepared.Inventory.Custody.Lots.Where(lot => lot.LotId != raw.LotId), final.Inventory.Custody.Lots);
        Assert.Equal(world.Prepared.Inventory.Custody.Storages.Select(storage => storage.CustodianId),
            final.Inventory.Custody.Storages.Select(storage => storage.CustodianId));
        Assert.Equal(world.Prepared.Inventory.Sources, final.Inventory.Sources);
        Assert.Equal(world.Prepared.Inventory.Controls, final.Inventory.Controls);
        Assert.Equal(world.Prepared.Production!.Lots.Select(lot =>
                (lot.LotId, lot.CustodianId, lot.Definition, lot.Output, lot.Revision, lot.Protection, lot.ReservedBy)),
            final.Production.Lots.Select(lot =>
                (lot.LotId, lot.CustodianId, lot.Definition, lot.Output, lot.Revision, lot.Protection, lot.ReservedBy)));
        Assert.Equal(JsonSerializer.Serialize(world.Prepared.Production.Batches), JsonSerializer.Serialize(final.Production.Batches));
        Assert.Equal(world.Prepared.Production.Revision + 1, final.Production.Revision);
        var crafted = Assert.Single(final.Production.Lots, lot => lot.CustodianId == bag && lot.Definition == raw.Definition);
        Assert.Equal(crafted.LotId, JsonId(stale.GetProperty("Use").GetProperty("Ingredient").GetProperty("LotId")));
        Assert.Equal(crafted.Revision, stale.GetProperty("Use").GetProperty("Ingredient").GetProperty("ExpectedRevision").GetUInt64());
        Assert.Equal(1UL, crafted.Quantity);
        Assert.Equal(world.Owner.ToString(), stale.GetProperty("Actor").GetString());
        Assert.Equal(WorldControlWitnessCodecV1.Encode(witness), Convert.FromBase64String(stale.GetProperty("Witness").GetString()!));
        Assert.True(stale.GetProperty("CanExecute").GetBoolean());
        Assert.Equal(use.PatientRevisionAfter, stale.GetProperty("Use").GetProperty("ExpectedPatientProgressionRevision").GetUInt64());
        Assert.Equal(final.Production.Revision, stale.GetProperty("Use").GetProperty("ExpectedProductionRevision").GetUInt64());

        Assert.Equal(world.Prepared.Progression!.Currency.AdmittedSupply, final.Progression!.Currency.AdmittedSupply);
        Assert.Equal(world.Prepared.Progression.Currency.Revision, final.Progression.Currency.Revision);
        Assert.Equal(world.Prepared.Progression.Currency.Accounts.Select(account => (account.AccountId, account.Kind, account.ActorId, account.Balance, account.Revision)),
            final.Progression.Currency.Accounts.Select(account => (account.AccountId, account.Kind, account.ActorId, account.Balance, account.Revision)));
        Assert.Equal(world.Prepared.Progression.Actors.Select(actor => (actor.ActorId, actor.Level, actor.Experience, actor.CurrentMp, actor.Profile.Version, actor.Curve.Version)),
            final.Progression.Actors.Select(actor => (actor.ActorId, actor.Level, actor.Experience, actor.CurrentMp, actor.Profile.Version, actor.Curve.Version)));
        foreach (var actor in world.Prepared.Progression.Actors)
        {
            var current = final.Progression.GetActor(actor.ActorId);
            Assert.Equal(actor.CurrentHp + (actor.ActorId == world.Owner ? 40 : 0), current.CurrentHp);
            Assert.Equal(actor.Revision + (actor.ActorId == world.Owner ? 1UL : 0UL), current.Revision);
        }
        Assert.Equal(world.Prepared.Care!.Agreements.Select(agreement => agreement.Version), final.Care.Agreements.Select(agreement => agreement.Version));
        Assert.Equal(world.Prepared.Care.Remedies.Select(remedy => remedy.Version), final.Care.Remedies.Select(remedy => remedy.Version));
        Assert.Empty(final.Care.Treatments);
        Assert.Empty(final.Care.Acknowledgments);
        Assert.Empty(final.Care.Dispositions);
        Assert.Equal(world.Prepared.Care.Ordinary!.Definitions.Select(definition => (definition.DefinitionId, definition.Version)),
            final.Care.Ordinary.Definitions.Select(definition => (definition.DefinitionId, definition.Version)));
        Assert.Equal(world.Prepared.Care.Ordinary.Permissions.Select(permission => (permission.PermissionId, permission.GrantVersion)),
            final.Care.Ordinary.Permissions.Select(permission => (permission.PermissionId, permission.GrantVersion)));
        foreach (var permission in world.Prepared.Care.Ordinary.Permissions)
            Assert.Equal(permission.PermissionId == world.Permission ? permissionAfter.Version : permission.Version,
                final.Care.Ordinary.GetPermission(permission.PermissionId).Version);
        Assert.Equal(world.Prepared.Construction!.ContentHash, final.Construction!.ContentHash);
        Assert.Equal(world.Prepared.Quests!.ContentHash, final.Quests!.ContentHash);
        Assert.Equal(world.Prepared.Excavation!.ContentHash, final.Excavation!.ContentHash);
        Assert.Equal(world.Prepared.Residents.Select(actor => (actor.ActorId, actor.ControllerKind, actor.InventoryCustodianId)),
            final.Residents.Select(actor => (actor.ActorId, actor.ControllerKind, actor.InventoryCustodianId)));
        Assert.Equal(world.Prepared.Space.Actors.Select(actor => (actor.ActorId, actor.Binding.FormInstanceId, actor.Binding.PhysicalBodyId)),
            final.Space.Actors.Select(actor => (actor.ActorId, actor.Binding.FormInstanceId, actor.Binding.PhysicalBodyId)));
        Assert.Equal(world.Prepared.Space.Actors.Where(actor => world.Actors.Contains(actor.ActorId)).Select(actor => (actor.ActorId, actor.Feet)),
            final.Space.Actors.Where(actor => world.Actors.Contains(actor.ActorId)).Select(actor => (actor.ActorId, actor.Feet)));
        foreach (var movement in batches.Where(batch => batch.ExpectedRevision >= world.Prepared.Revision).SelectMany(batch => batch.Movement))
        {
            Assert.Equal(ResidentControllerKind.Host, world.Prepared.GetResident(movement.ActorId).ControllerKind);
            var binding = world.Prepared.Inventory.Controls.Single(control => control.ActorId == movement.ActorId);
            Assert.Equal(new TrustedCommandDelivery(binding.PrincipalId, binding.SessionId), movement.Delivery);
            Assert.Equal(WorldMovementInput.DisplacementPerStep, Math.Abs(movement.DeltaX) + Math.Abs(movement.DeltaY));
            Assert.True(movement.DeltaX == 0 || movement.DeltaY == 0);
        }
        Assert.Contains(batches, batch => batch.ControllerEvent is { Kind: WorldControllerEventKind.AllowanceExpired } expired
            && expired.ActorId == world.Owner && expired.DisconnectId == witness.DisconnectId
            && expired.ElapsedHostMilliseconds == WorldControllerState.ReconnectAllowanceMilliseconds);
        Assert.Single(batches, batch => batch.ControlAssignmentEvent is { Kind: WorldControlAssignmentEventKind.Acquire } acquired && acquired.Witness == witness);
        Assert.Null(final.EffectiveControl!.Find(world.Owner)?.Witness);
        Assert.Equal(WorldControlPermissionStatus.Revoked, final.ControlPermissions!.Slots.Single().Status);
        authority.BackupForHost(Path.Combine(output, "world.sqlite3"));
        File.WriteAllBytes(Path.Combine(output, "final.ofc"), WorldSnapshotCodec.Encode(final));
        Write(output, "receipts.json", submitted);
        Write(output, "unconsumed-review.json", stale);
        Write(output, "journal.json", journal.Select(row => new { row.Sequence, row.Kind, row.AdvancingSteps,
            Cause = row.CauseId.ToString(), Batch = Convert.ToHexString(row.AcceptedBatch), Outcomes = Convert.ToHexString(row.Outcomes),
            Before = row.BeforeHash.Value, After = row.AfterHash.Value }));
    }

    private static EntityId JsonId(JsonElement value) => new(value.GetProperty("High").GetUInt64(), value.GetProperty("Low").GetUInt64());
    private static void PreserveFinal(string output, RestorativeChoicesWorld world)
    {
        if (File.Exists(Path.Combine(output, "world.sqlite3"))) return;
        using var authority = SqliteWorldAuthority.OpenExisting(world.Database, WorldSnapshotCodec.Encode(world.Genesis));
        authority.BackupForHost(Path.Combine(output, "world.sqlite3"));
        File.WriteAllBytes(Path.Combine(output, "final.ofc"), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
    }
    private static void Write(string output, string name, object value) => File.WriteAllText(Path.Combine(output, name),
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
}
