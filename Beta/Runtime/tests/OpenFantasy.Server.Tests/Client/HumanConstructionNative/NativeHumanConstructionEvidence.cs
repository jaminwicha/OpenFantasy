using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.HumanConstructionNative;

public sealed partial class NativeHumanConstructionTests
{
    private static void Verify(string output, HumanConstructionWorld world, IReadOnlyList<NativeRosterProcess> processes,
        WorldControlWitness witness)
    {
        StoredWorldCommit[] journal;
        StoredWorldIdentity[] recordedIdentities;
        using (var database = WorldDatabase.Open(world.Database, create: false))
        {
            journal = database.ReadJournal().ToArray();
            recordedIdentities = database.ReadIdentities().ToArray();
        }
        var gameplay = processes.SelectMany(process => process.Submissions).ToArray();
        var permissions = processes.SelectMany(process => process.Last.GetProperty("PermissionSubmissions").EnumerateArray())
            .Select(value => value.Clone()).ToArray();
        Assert.Equal(204, gameplay.Length);
        Assert.Equal(3, permissions.Length);
        var submitted = gameplay.Concat(permissions).ToArray();
        var native = submitted.Select(value => WorldActionCodec.Decode(Convert.FromHexString(value.GetProperty("Command").GetString()!))).ToArray();
        Assert.Equal(native.Length, native.Select(command => command.CommandId).Distinct().Count());
        var processor = new WorldStepProcessor();
        var state = world.Genesis;
        var identities = new Dictionary<EntityId, StoredWorldIdentity>();
        var actions = new List<WorldActionCommand>();
        var workActions = new List<WorldActionCommand>();
        WorldSnapshot? funded = null;
        WorldSnapshot? completed = null;
        foreach (StoredWorldCommit row in journal)
        {
            Assert.Equal(state.ContentHash, row.BeforeHash);
            var batch = WorldStepCodec.Decode(row.AcceptedBatch);
            Assert.Equal(row.AcceptedBatch, WorldStepCodec.Encode(batch));
            var decision = processor.Decide(state, batch, id => world.Genesis.IdentityIndex.ContainsEntity(id) || identities.ContainsKey(id));
            Assert.Equal(row.AfterHash, decision.Candidate.ContentHash);
            Assert.Equal(row.Outcomes, WorldReceiptCodecV1.EncodeBatch(decision.Receipts));
            foreach (var identity in decision.Identities)
                Assert.True(identities.TryAdd(identity.Identity, new(identity.Identity, row.Sequence, (WorldIdentityKind)identity.Kind)));
            foreach (var command in batch.Actions)
            {
                actions.Add(command);
                if (command.Intent is CompleteDeliveryQuestIntent)
                {
                    Assert.Equal(world.Supplier, command.ActorId);
                    funded = decision.Candidate;
                    var quest = state.Quests!.Definitions.Single(definition => definition.DefinitionId
                        == Assert.Single(state.Construction!.Projects).QuestId);
                    var earnedReward = ActorRewardPlanner.Plan(state.Progression!, world.Supplier,
                        state.Progression!.GetActor(world.Supplier).Revision, quest.Reward);
                    Assert.True(earnedReward.Succeeded);
                    Assert.Equal(earnedReward.Candidate.ContentHash, funded.Progression!.ContentHash);
                    Assert.Equal(ActorQuestStatus.Completed, funded.Quests!.FindParticipation(world.Supplier,
                        Assert.Single(funded.Construction!.Projects).QuestId)!.Status);
                }
                if (command.Intent is not WorkConstructionIntent intent) continue;
                workActions.Add(command);
                var oldProject = Assert.Single(state.Construction!.Projects);
                var project = Assert.Single(decision.Candidate.Construction!.Projects);
                Assert.Equal(world.Owner, command.ActorId);
                Assert.Equal(world.Project, intent.ProjectId);
                Assert.Equal(oldProject.Revision, intent.ExpectedProjectRevision);
                Assert.Equal(state.Inventory.Custody.GetStorage(project.ProjectCustodianId).Revision, intent.ExpectedProjectStorageRevision);
                var tool = Assert.Single(state.Inventory.Custody.Lots, lot => lot.LotId == intent.ToolLotId);
                Assert.Equal(state.GetResident(world.Owner).InventoryCustodianId, tool.CustodianId);
                Assert.Equal(project.Definition.RequiredTool.Item, tool.Definition);
                Assert.Equal(1UL, tool.Quantity);
                Assert.Equal(tool.Revision, intent.ExpectedToolLotRevision);
                Assert.Null(tool.ReservedBy);
                Assert.Equal(LotProtection.None, tool.Protection);
                Assert.Equal(tool, Assert.Single(decision.Candidate.Inventory.Custody.Lots, lot => lot.LotId == tool.LotId));
                Assert.Equal(project.WorkFeet, state.Space.GetActor(world.Owner).Feet);
                Assert.Equal(state.Space.GetActor(world.Owner).Feet, decision.Candidate.Space.GetActor(world.Owner).Feet);
                Assert.Equal(oldProject.Progress.CompletedUnits + 1, project.Progress.CompletedUnits);
                Assert.Equal(oldProject.Progress.Elapsed.TotalMilliseconds + 1800, project.Progress.Elapsed.TotalMilliseconds);
                Assert.Equal((ulong)workActions.Count, project.Revision);
                Assert.Equal(state.AdvancingSteps + 1, decision.Candidate.AdvancingSteps);
                Assert.Equal(state.Progression!.ContentHash, decision.Candidate.Progression!.ContentHash);
                if (!project.IsCompleted)
                {
                    Assert.Equal(state.Inventory.ContentHash, decision.Candidate.Inventory.ContentHash);
                    Assert.Equal(state.Production!.ContentHash, decision.Candidate.Production!.ContentHash);
                    Assert.Empty(decision.Candidate.Space.Structures);
                    Assert.Equal(6UL, state.Inventory.Custody.Lots.Where(lot => lot.ReservedBy == project.ReservationId)
                        .Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
                }
                else
                {
                    Assert.Null(completed);
                    completed = decision.Candidate;
                    Assert.Equal(200, workActions.Count);
                    Assert.Equal(360000UL, (ulong)project.Progress.Elapsed.TotalMilliseconds);
                    var installed = Assert.IsType<InstalledStructure>(project.Installed);
                    Assert.Equal(command.CommandId, installed.CompletionCommandId);
                    Assert.Equal(EntityId.DeriveSpawned(command.CommandId, 3), installed.StructureId);
                    var transform = Assert.Single(completed.Production!.Batches, value => value.CommandId == command.CommandId);
                    Assert.Equal(installed.ProductionBatchId, transform.BatchId);
                    Assert.Equal(world.Owner, transform.ActorId);
                    Assert.Equal(project.ProjectCustodianId, transform.CustodianId);
                    Assert.Equal(6UL, transform.Ingredients.Aggregate(0UL, (sum, input) => sum + input.Quantity));
                    var outputs = completed.Production.Lots.Where(lot => lot.Output.BatchId == transform.BatchId).ToArray();
                    Assert.Equal(4UL, outputs.Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
                    Assert.Equal(1UL, Assert.Single(outputs, lot => lot.LotId == installed.GuardLotId).Quantity);
                    var recipe = project.Definition.MaterialTransform;
                    ulong inputsMass = transform.Ingredients.Aggregate(0UL, (sum, input) =>
                        sum + recipe.BindingFor(input.Definition).Physical.MassPerUnit.Value * input.Quantity);
                    ulong outputsMass = outputs.Aggregate(0UL, (sum, lot) =>
                        sum + recipe.BindingFor(lot.Definition).Physical.MassPerUnit.Value * lot.Quantity);
                    Assert.Equal(300000UL, inputsMass);
                    Assert.Equal(inputsMass, outputsMass);
                    Assert.DoesNotContain(completed.Inventory.Custody.Lots, lot => lot.ReservedBy == project.ReservationId);
                    var geometry = Assert.Single(completed.Space.Structures);
                    Assert.Equal(installed.StructureId, geometry.StructureId);
                    Assert.Equal(project.Definition.Members.Count, geometry.Boxes.Count);
                    foreach (var access in project.RequiredAccess)
                        Assert.Equal(GroundRouteStatus.Reachable, completed.Space.QueryGroundRoute(access.ActorId, access.From, access.To).Status);
                    // Collision is checked against the actual installed geometry, with the actual
                    // supplier body. This is a domain query, not a claimed native collision gesture.
                    Assert.Contains(geometry.Boxes, box => completed.Space.QueryOccupancy(world.Supplier,
                        new SubunitPosition(box.Minimum.X, box.Minimum.Y, project.SiteFeet.Z)) == SpatialOccupancy.BlockedStructure);
                }
            }
            state = decision.Candidate;
        }
        Assert.NotNull(funded);
        Assert.NotNull(completed);
        Assert.Equal(200, workActions.Count);
        Assert.Null(workActions[0].ControlWitness);
        Assert.Null(workActions[^1].ControlWitness);
        Assert.All(workActions.Skip(1).Take(198), command => Assert.Equal(witness, command.ControlWitness));
        Assert.Equal(recordedIdentities, identities.Values.OrderBy(value => value.Identity));
        Assert.Equal(native.Select(command => command.CommandId).Order(), actions.Select(command => command.CommandId).Order());
        using var authority = SqliteWorldAuthority.OpenExisting(world.Database, WorldSnapshotCodec.Encode(world.Genesis));
        var final = authority.ReadStateForHost();
        Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(final));
        foreach (JsonElement value in submitted)
        {
            var command = WorldActionCodec.Decode(Convert.FromHexString(value.GetProperty("Command").GetString()!));
            if (value.TryGetProperty("IsRetry", out var retry)) Assert.False(retry.GetBoolean());
            var receipt = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command, new(command.PrincipalId, command.OriginSessionId)));
            Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            Assert.Equal(Convert.FromHexString(value.GetProperty("Receipt").GetString()!), WorldReceiptCodecV1.Encode(receipt));
            Assert.Equal(WorldActionCodec.Encode(command), WorldActionCodec.Encode(Assert.Single(actions, action => action.CommandId == command.CommandId)));
            var binding = world.Genesis.Inventory.Controls.Single(row => row.ActorId == (command.ControlWitness is null ? command.ActorId : world.Grantee));
            Assert.Equal(binding.PrincipalId, command.PrincipalId);
            Assert.Equal(binding.SessionId, command.OriginSessionId);
        }
        var grant = Assert.Single(actions, command => command.Intent is OfferControlPermissionIntent);
        var acceptance = Assert.Single(actions, command => command.Intent is AcceptControlPermissionIntent);
        Assert.Equal(WorldControlCapabilities.Production, Assert.IsType<OfferControlPermissionIntent>(grant.Intent).Capabilities);
        Assert.Equal(grant.CommandId, witness.GrantCommandId);
        Assert.Equal(acceptance.CommandId, witness.AcceptanceCommandId);
        Assert.Null(final.EffectiveControl!.Find(world.Owner)?.Witness);
        Assert.Equal(WorldControlPermissionStatus.Revoked, Assert.Single(final.ControlPermissions!.Slots).Status);
        Assert.Contains(journal.Select(row => WorldStepCodec.Decode(row.AcceptedBatch)), batch =>
            batch.ControllerEvent is { Kind: WorldControllerEventKind.AllowanceExpired } expired
            && expired.ActorId == world.Owner && expired.DisconnectId == witness.DisconnectId
            && expired.ElapsedHostMilliseconds == WorldControllerState.ReconnectAllowanceMilliseconds);
        Assert.Equal(world.Genesis.Inventory.Controls, final.Inventory.Controls);
        Assert.Equal(world.Prepared.Care!.ContentHash, final.Care!.ContentHash);
        // Delivery earns the original quest's experience reward, including its lawful level/resource gain.
        // Construction and control changes cannot award a second reward or alter those resources.
        Assert.Equal(funded.Progression!.ContentHash, final.Progression!.ContentHash);
        Assert.Equal(world.Prepared.Progression!.Actors.Where(actor => actor.ActorId != world.Supplier)
                .Select(actor => (actor.ActorId, actor.CurrentHp, actor.CurrentMp)),
            final.Progression.Actors.Where(actor => actor.ActorId != world.Supplier)
                .Select(actor => (actor.ActorId, actor.CurrentHp, actor.CurrentMp)));
        Assert.Equal(funded.Progression!.Currency.Accounts.Select(account => (account.AccountId, account.Balance, account.Revision)),
            final.Progression.Currency.Accounts.Select(account => (account.AccountId, account.Balance, account.Revision)));
        Assert.Equal(world.Prepared.Excavation!.ContentHash, final.Excavation!.ContentHash);
        Assert.Equal(world.Prepared.Space.Actors.Select(actor => (actor.ActorId, actor.Feet)), final.Space.Actors.Select(actor => (actor.ActorId, actor.Feet)));
        Assert.Equal(completed.Construction!.ContentHash, final.Construction!.ContentHash);
        authority.BackupForHost(Path.Combine(output, "world.sqlite3"));
        File.WriteAllBytes(Path.Combine(output, "final.ofc"), WorldSnapshotCodec.Encode(final));
        Write(output, "receipts.json", submitted);
        Write(output, "journal.json", journal.Select(row => new { row.Sequence, row.AdvancingSteps,
            Before = row.BeforeHash.Value, After = row.AfterHash.Value, Batch = Convert.ToHexString(row.AcceptedBatch),
            Outcomes = Convert.ToHexString(row.Outcomes) }));
        Write(output, "identities.json", recordedIdentities.Select(row => new { Identity = row.Identity.ToString(), row.Sequence, row.Kind }));
    }
}
