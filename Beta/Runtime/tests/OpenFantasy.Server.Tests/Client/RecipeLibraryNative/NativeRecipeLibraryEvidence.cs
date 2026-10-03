using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;
using OpenFantasy.Server.Tests.SelectedProductionNative;

namespace OpenFantasy.Server.Tests.RecipeLibraryNative;

public sealed partial class NativeRecipeLibraryTests
{
    private static void Verify(string output, SelectedProductionWorld world, IReadOnlyList<NativeRosterProcess> processes,
        WorldControlWitness witness, EntityId potionRecipe)
    {
        StoredWorldCommit[] journal;
        StoredWorldIdentity[] recordedIdentities;
        using (var database = WorldDatabase.Open(world.Database, create: false))
        {
            journal = database.ReadJournal().ToArray();
            recordedIdentities = database.ReadIdentities().ToArray();
        }
        JsonElement[] gameplay = processes.SelectMany(process => process.Submissions).ToArray();
        JsonElement[] permissions = processes.SelectMany(process => process.Last.GetProperty("PermissionSubmissions").EnumerateArray())
            .Select(value => value.Clone()).ToArray();
        Assert.Equal(9, gameplay.Length);
        Assert.Equal(3, permissions.Length);
        var submitted = gameplay.Concat(permissions).ToArray();
        var native = submitted.Select(value => WorldActionCodec.Decode(Convert.FromHexString(value.GetProperty("Command").GetString()!))).ToArray();
        Assert.Equal(native.Length, native.Select(command => command.CommandId).Distinct().Count());
        var processor = new WorldStepProcessor();
        var state = world.Genesis;
        var identities = new Dictionary<EntityId, StoredWorldIdentity>();
        var boundaries = new Dictionary<EntityId, (WorldSnapshot Before, WorldSnapshot After)>();
        var actions = new List<WorldActionCommand>();
        foreach (StoredWorldCommit row in journal)
        {
            Assert.Equal(state.ContentHash, row.BeforeHash);
            WorldStepBatch batch = WorldStepCodec.Decode(row.AcceptedBatch);
            Assert.Equal(row.AcceptedBatch, WorldStepCodec.Encode(batch));
            var decision = processor.Decide(state, batch, id => world.Genesis.IdentityIndex.ContainsEntity(id) || identities.ContainsKey(id));
            Assert.Equal(row.AfterHash, decision.Candidate.ContentHash);
            Assert.Equal(row.Outcomes, WorldReceiptCodecV1.EncodeBatch(decision.Receipts));
            foreach (var identity in decision.Identities)
                Assert.True(identities.TryAdd(identity.Identity, new(identity.Identity, row.Sequence, (WorldIdentityKind)identity.Kind)));
            foreach (WorldActionCommand command in batch.Actions)
            {
                actions.Add(command);
                Assert.True(boundaries.TryAdd(command.CommandId, (state, decision.Candidate)));
            }
            state = decision.Candidate;
        }
        Assert.Equal(recordedIdentities, identities.Values.OrderBy(value => value.Identity));
        Assert.Equal(native.Select(command => command.CommandId).Order(), actions.Select(command => command.CommandId).Order());
        using var authority = SqliteWorldAuthority.OpenExisting(world.Database, WorldSnapshotCodec.Encode(world.Genesis));
        WorldSnapshot final = authority.ReadStateForHost();
        Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(final));
        foreach (JsonElement value in submitted)
        {
            WorldActionCommand command = WorldActionCodec.Decode(Convert.FromHexString(value.GetProperty("Command").GetString()!));
            if (value.TryGetProperty("IsRetry", out var retry)) Assert.False(retry.GetBoolean());
            var receipt = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command, new(command.PrincipalId, command.OriginSessionId)));
            Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            Assert.Equal(Convert.FromHexString(value.GetProperty("Receipt").GetString()!), WorldReceiptCodecV1.Encode(receipt));
            Assert.Equal(WorldActionCodec.Encode(command), WorldActionCodec.Encode(Assert.Single(actions, item => item.CommandId == command.CommandId)));
            EntityId submitter = command.ControlWitness is null ? command.ActorId : world.Grantee;
            var binding = world.Genesis.Inventory.Controls.Single(row => row.ActorId == submitter);
            Assert.Equal(binding.PrincipalId, command.PrincipalId);
            Assert.Equal(binding.SessionId, command.OriginSessionId);
            if (command.ControlWitness is not null)
            {
                Assert.Equal(world.Owner, command.ActorId);
                Assert.Equal(witness, command.ControlWitness);
            }
        }
        var grant = Assert.Single(actions, command => command.Intent is OfferControlPermissionIntent);
        var acceptance = Assert.Single(actions, command => command.Intent is AcceptControlPermissionIntent);
        Assert.Equal(WorldControlCapabilities.Production | WorldControlCapabilities.Traversal,
            Assert.IsType<OfferControlPermissionIntent>(grant.Intent).Capabilities);
        Assert.Equal(grant.CommandId, witness.GrantCommandId);
        Assert.Equal(acceptance.CommandId, witness.AcceptanceCommandId);
        Assert.Null(final.EffectiveControl!.Find(world.Owner)?.Witness);
        Assert.Equal(WorldControlPermissionStatus.Revoked, Assert.Single(final.ControlPermissions!.Slots).Status);
        Assert.Contains(journal.Select(row => WorldStepCodec.Decode(row.AcceptedBatch)), batch =>
            batch.ControllerEvent is { Kind: WorldControllerEventKind.AllowanceExpired } expired
            && expired.ActorId == world.Owner && expired.DisconnectId == witness.DisconnectId
            && expired.ElapsedHostMilliseconds == WorldControllerState.ReconnectAllowanceMilliseconds);

        EntityId custodian = world.Prepared.GetResident(world.Owner).InventoryCustodianId;
        var berry = world.Prepared.Gathering!.GetNode(world.BerryNode);
        var harvests = actions.Where(command => command.Intent is GatherIntent).ToArray();
        Assert.Equal(7, harvests.Length);
        foreach (WorldActionCommand command in harvests)
        {
            var intent = Assert.IsType<GatherIntent>(command.Intent);
            Assert.Equal(world.BerryNode, intent.NodeId);
            Assert.Equal(witness, command.ControlWitness);
            var (before, after) = boundaries[command.CommandId];
            var oldNode = before.Gathering!.GetNode(world.BerryNode);
            var newNode = after.Gathering!.GetNode(world.BerryNode);
            Assert.Equal(oldNode.Revision, intent.ExpectedNodeRevision);
            Assert.Equal(oldNode.NextUnissuedUnit + 1, newNode.NextUnissuedUnit);
            Assert.Equal(oldNode.RemainingSwings - 1, newNode.RemainingSwings);
            uint effort = before.Gathering.GetActorWork(world.Owner).Effort;
            Assert.True(effort >= GatherRulesV1.EffortPerSwing);
            Assert.Equal(effort - GatherRulesV1.EffortPerSwing + GatherRulesV1.EffortPerAdvancingStep,
                after.Gathering.GetActorWork(world.Owner).Effort);
            var tool = before.Inventory.Custody.Lots.Single(lot => lot.LotId == intent.ToolLotId);
            Assert.Equal(custodian, tool.CustodianId);
            Assert.Equal(tool, after.Inventory.Custody.Lots.Single(lot => lot.LotId == tool.LotId));
            var issued = Assert.Single(after.Inventory.Custody.Lots, lot => lot.LotId == EntityId.DeriveSpawned(command.CommandId, 0));
            Assert.Equal(berry.Definition.YieldDefinition, issued.Definition);
            Assert.Equal(custodian, issued.CustodianId);
            Assert.Equal(1UL, issued.Quantity);
            Assert.Equal(oldNode.SourceId, issued.Source.SourceId);
            Assert.Equal(oldNode.NextUnissuedUnit, issued.Source.Start);
            Assert.Equal(newNode.NextUnissuedUnit, issued.Source.End);
        }
        WorldSnapshot depleted = boundaries[harvests[3].CommandId].After;
        WorldSnapshot regrown = boundaries[harvests[4].CommandId].Before;
        ulong depletedAt = Assert.IsType<ulong>(depleted.Gathering!.GetNode(world.BerryNode).DepletedAtStep);
        Assert.True(regrown.ProcessIntervals - depletedAt >= berry.Definition.RegrowthDelaySteps);
        Assert.Equal(4, regrown.Gathering!.GetNode(world.BerryNode).RemainingSwings);
        Assert.Equal(berry.UnitCapacity, final.Gathering!.GetNode(world.BerryNode).UnitCapacity);
        Assert.Equal(berry.NextUnissuedUnit + 7, final.Gathering.GetNode(world.BerryNode).NextUnissuedUnit);

        var crafts = actions.Where(command => command.Intent is CraftIntent).ToArray();
        Assert.Equal(2, crafts.Length);
        Assert.Equal(new[] { potionRecipe, world.Recipe }, crafts.Select(command => Assert.IsType<CraftIntent>(command.Intent).Request.RecipeId));
        Assert.Equal(2, final.Production!.Batches.Count);
        foreach (WorldActionCommand command in crafts)
        {
            var request = Assert.IsType<CraftIntent>(command.Intent).Request;
            var (before, after) = boundaries[command.CommandId];
            Assert.Equal(witness, command.ControlWitness);
            Assert.Equal(before.Production!.Revision, request.ExpectedProductionRevision);
            Assert.Equal(before.Inventory.Custody.GetStorage(custodian).Revision, request.ExpectedStorageRevision);
            Assert.Equal(custodian, request.CustodianId);
            var batch = Assert.Single(after.Production!.Batches, batch => batch.CommandId == command.CommandId);
            Assert.Equal(EntityId.DeriveSpawned(command.CommandId, 0), batch.BatchId);
            Assert.Equal(world.Owner, batch.ActorId);
            Assert.Equal(custodian, batch.CustodianId);
            Assert.Equal(request.RecipeId, batch.RecipeId);
            Assert.Equal(request.Ingredients.Select(input => (input.LotId, input.ExpectedRevision, input.Quantity)),
                batch.Ingredients.Select(input => (input.LotId, input.Revision, input.Quantity)));
            var recipe = after.Production.GetRecipe(request.RecipeId);
            Assert.Equal(recipe.Version, request.RecipeVersion);
            var outputs = after.Production.Lots.Where(lot => lot.Output.BatchId == batch.BatchId).ToArray();
            Assert.All(outputs, lot => Assert.Equal(custodian, lot.CustodianId));
            ulong inputMass = batch.Ingredients.Aggregate(0UL, (sum, input) => sum + recipe.BindingFor(input.Definition).Physical.MassPerUnit.Value * input.Quantity);
            ulong outputMass = outputs.Aggregate(0UL, (sum, lot) => sum + recipe.BindingFor(lot.Definition).Physical.MassPerUnit.Value * lot.Quantity);
            Assert.Equal(inputMass, outputMass);
        }
        var consumedBerryIds = harvests.Take(5).Select(command => EntityId.DeriveSpawned(command.CommandId, 0)).ToHashSet();
        Assert.Equal(consumedBerryIds.Order(), final.Production.Batches.SelectMany(batch => batch.Ingredients)
            .Where(input => input.Definition == berry.Definition.YieldDefinition).Select(input => input.LotId).Order());
        var woodInput = Assert.Single(final.Production.Batches.SelectMany(batch => batch.Ingredients), input => input.Definition != berry.Definition.YieldDefinition);
        Assert.Equal(1UL, woodInput.Quantity);
        foreach (CountedSourceLot original in world.Prepared.Inventory.Custody.Lots)
        {
            var actual = Assert.Single(final.Inventory.Custody.Lots, lot => lot.LotId == original.LotId);
            Assert.Equal(original.LotId == woodInput.LotId
                ? new CountedSourceLot(original.LotId, original.CustodianId, original.Definition,
                    new(original.Source.SourceId, original.Source.Start + 1, original.Source.End), original.Revision + 1,
                    original.Protection, original.ReservedBy) : original, actual);
        }
        var remaining = harvests.Skip(5).Select(command => EntityId.DeriveSpawned(command.CommandId, 0)).ToArray();
        Assert.Equal(world.Prepared.Inventory.Custody.Lots.Select(lot => lot.LotId).Concat(remaining).Order(),
            final.Inventory.Custody.Lots.Select(lot => lot.LotId).Order());
        Assert.Equal(4UL, final.Production.Lots.Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
        Assert.Equal(3, final.Production.Lots.Count);
        Assert.Equal(world.Prepared.Care!.ContentHash, final.Care!.ContentHash);
        Assert.Equal(world.Prepared.Progression!.Actors.Select(actor => (actor.ActorId, actor.CurrentHp, actor.CurrentMp)),
            final.Progression!.Actors.Select(actor => (actor.ActorId, actor.CurrentHp, actor.CurrentMp)));
        Assert.Equal(world.Prepared.Progression.Currency.Accounts.Select(account => (account.AccountId, account.Balance, account.Revision)),
            final.Progression.Currency.Accounts.Select(account => (account.AccountId, account.Balance, account.Revision)));
        Assert.Equal(world.Prepared.Excavation!.ContentHash, final.Excavation!.ContentHash);
        Assert.Equal(world.Prepared.Construction!.ContentHash, final.Construction!.ContentHash);
        Assert.Equal(world.Genesis.Inventory.Controls, final.Inventory.Controls);
        Assert.Equal(world.Prepared.Space.Actors.Select(actor => (actor.ActorId, actor.Feet)), final.Space.Actors.Select(actor => (actor.ActorId, actor.Feet)));
        Assert.Empty(final.Coordination!.Proposals!.Open);
        Assert.Empty(final.Coordination.Proposals.Outcomes);
        authority.BackupForHost(Path.Combine(output, "world.sqlite3"));
        File.WriteAllBytes(Path.Combine(output, "final.ofc"), WorldSnapshotCodec.Encode(final));
        Write(output, "receipts.json", submitted);
        Write(output, "journal.json", journal.Select(row => new { row.Sequence, row.AdvancingSteps,
            Before = row.BeforeHash.Value, After = row.AfterHash.Value, Batch = Convert.ToHexString(row.AcceptedBatch),
            Outcomes = Convert.ToHexString(row.Outcomes) }));
        Write(output, "identities.json", recordedIdentities.Select(row => new { Identity = row.Identity.ToString(), row.Sequence, row.Kind }));
    }

    private static void Preserve(string output, SelectedProductionWorld world)
    {
        if (File.Exists(Path.Combine(output, "world.sqlite3"))) return;
        using var authority = SqliteWorldAuthority.OpenExisting(world.Database, WorldSnapshotCodec.Encode(world.Genesis));
        authority.BackupForHost(Path.Combine(output, "world.sqlite3"));
        File.WriteAllBytes(Path.Combine(output, "final.ofc"), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
    }
}
