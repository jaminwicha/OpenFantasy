using System.Diagnostics;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.SelectedProductionNative;

public sealed partial class NativeSelectedProductionTests
{
    private static JsonElement Selection(NativeRosterProcess process) => process.Last.GetProperty("Selection");
    private static JsonElement Slot(NativeRosterProcess process, EntityId owner, EntityId grantee) =>
        Assert.Single(process.Last.GetProperty("ControlBoard").GetProperty("Slots").EnumerateArray(), row =>
            row.GetProperty("Owner").GetString() == owner.ToString() && row.GetProperty("Grantee").GetString() == grantee.ToString());

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

    private static void Verify(string output, SelectedProductionWorld world, IReadOnlyList<NativeRosterProcess> processes,
        WorldControlWitness witness, IReadOnlyList<WorldActionCommand> production, bool excavation)
    {
        StoredWorldCommit[] journal;
        StoredWorldIdentity[] storedIdentities;
        using (var database = WorldDatabase.Open(world.Database, create: false))
        {
            journal = database.ReadJournal().ToArray();
            storedIdentities = database.ReadIdentities().ToArray();
        }
        var gameplay = processes.SelectMany(process => process.Submissions).ToArray();
        var permission = processes.SelectMany(process => process.Last.GetProperty("PermissionSubmissions").EnumerateArray())
            .Select(value => value.Clone()).ToArray();
        Assert.Equal(excavation ? 3 : 5, gameplay.Length);
        Assert.Equal(3, permission.Length);
        Assert.Equal(3, production.Count);
        var submitted = gameplay.Concat(permission).ToArray();
        var commands = submitted.Select(value => WorldActionCodec.Decode(Convert.FromHexString(value.GetProperty("Command").GetString()!))).ToArray();
        Assert.Equal(commands.Length, commands.Select(command => command.CommandId).Distinct().Count());
        var grant = Assert.Single(commands, command => command.Intent is OfferControlPermissionIntent);
        var acceptance = Assert.Single(commands, command => command.Intent is AcceptControlPermissionIntent);
        var revoke = Assert.Single(commands, command => command.Intent is RevokeControlPermissionIntent);
        Assert.Equal(witness.Capabilities, Assert.IsType<OfferControlPermissionIntent>(grant.Intent).Capabilities);
        Assert.Equal(grant.CommandId, witness.GrantCommandId);
        Assert.Equal(acceptance.CommandId, witness.AcceptanceCommandId);
        Assert.Equal(world.Owner, grant.ActorId);
        Assert.Equal(world.Grantee, acceptance.ActorId);
        Assert.Equal(world.Owner, revoke.ActorId);
        Assert.Null(grant.ControlWitness);
        Assert.Null(acceptance.ControlWitness);
        Assert.Null(revoke.ControlWitness);

        // One independent ordered replay retains only the few requested production boundaries.
        // Compare the entire committed identity set, including ordinary preparation and controller events.
        var processor = new WorldStepProcessor();
        var state = world.Genesis;
        var identities = new Dictionary<EntityId, StoredWorldIdentity>();
        var boundaries = new Dictionary<EntityId, (WorldSnapshot Before, WorldSnapshot After)>();
        var allActions = new List<WorldActionCommand>();
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
            foreach (var command in batch.Actions)
            {
                allActions.Add(command);
                Assert.True(boundaries.TryAdd(command.CommandId, (state, decision.Candidate)));
            }
            state = decision.Candidate;
        }
        Assert.Equal(storedIdentities, identities.Values.OrderBy(value => value.Identity));
        Assert.Equal(commands.Select(command => command.CommandId).Order(), allActions.Select(command => command.CommandId).Order());
        using var authority = SqliteWorldAuthority.OpenExisting(world.Database, WorldSnapshotCodec.Encode(world.Genesis));
        var final = authority.ReadStateForHost();
        Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(final));
        foreach (var value in submitted)
        {
            var command = WorldActionCodec.Decode(Convert.FromHexString(value.GetProperty("Command").GetString()!));
            if (value.TryGetProperty("IsRetry", out var retry)) Assert.False(retry.GetBoolean());
            var receipt = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command, new(command.PrincipalId, command.OriginSessionId)));
            Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            Assert.Equal(Convert.FromHexString(value.GetProperty("Receipt").GetString()!), WorldReceiptCodecV1.Encode(receipt));
            Assert.Equal(WorldActionCodec.Encode(command), WorldActionCodec.Encode(Assert.Single(allActions, actual => actual.CommandId == command.CommandId)));
            var binding = world.Genesis.Inventory.Controls.Single(row => row.ActorId == (command.ControlWitness is null ? command.ActorId : world.Grantee));
            Assert.Equal(binding.PrincipalId, command.PrincipalId);
            Assert.Equal(binding.SessionId, command.OriginSessionId);
            if (command.ControlWitness is not null)
            {
                Assert.Equal(world.Owner, command.ActorId);
                Assert.Equal(witness, command.ControlWitness);
            }
        }
        Assert.All(production, command => Assert.Equal(witness, command.ControlWitness));
        Assert.Equal(world.Genesis.Inventory.Controls, final.Inventory.Controls);
        Assert.Equal(world.Genesis.Residents.Select(actor => (actor.ActorId, actor.ControllerKind, actor.InventoryCustodianId)),
            final.Residents.Select(actor => (actor.ActorId, actor.ControllerKind, actor.InventoryCustodianId)));
        Assert.Equal(world.Genesis.Space.Actors.Select(actor => (actor.ActorId, actor.Binding.FormInstanceId, actor.Binding.PhysicalBodyId)),
            final.Space.Actors.Select(actor => (actor.ActorId, actor.Binding.FormInstanceId, actor.Binding.PhysicalBodyId)));
        Assert.Equal(world.Prepared.Space.Actors.Select(actor => (actor.ActorId, actor.Feet)),
            final.Space.Actors.Select(actor => (actor.ActorId, actor.Feet)));
        Assert.Equal(world.Prepared.Care!.ContentHash, final.Care!.ContentHash);
        Assert.Equal(world.Prepared.Progression!.Currency.Revision, final.Progression!.Currency.Revision);
        Assert.Equal(world.Prepared.Progression.Currency.AdmittedSupply, final.Progression.Currency.AdmittedSupply);
        Assert.Equal(world.Prepared.Progression.Currency.Accounts.Select(account => (account.AccountId, account.Kind, account.ActorId, account.Balance, account.Revision)),
            final.Progression.Currency.Accounts.Select(account => (account.AccountId, account.Kind, account.ActorId, account.Balance, account.Revision)));
        Assert.Equal(world.Prepared.Progression.Actors.Select(actor => (actor.ActorId, actor.CurrentHp, actor.CurrentMp)),
            final.Progression.Actors.Select(actor => (actor.ActorId, actor.CurrentHp, actor.CurrentMp)));
        Assert.Equal(world.Prepared.Construction!.ContentHash, final.Construction!.ContentHash);
        var batches = journal.Select(row => WorldStepCodec.Decode(row.AcceptedBatch)).ToArray();
        Assert.Contains(batches, batch => batch.ControllerEvent is { Kind: WorldControllerEventKind.AllowanceExpired } expired
            && expired.ActorId == world.Owner && expired.DisconnectId == witness.DisconnectId
            && expired.ElapsedHostMilliseconds == WorldControllerState.ReconnectAllowanceMilliseconds);
        Assert.Single(batches, batch => batch.ControlAssignmentEvent is { Kind: WorldControlAssignmentEventKind.Acquire } acquired && acquired.Witness == witness);
        Assert.Null(final.EffectiveControl!.Find(world.Owner)?.Witness);
        Assert.Equal(WorldControlPermissionStatus.Revoked, Assert.Single(final.ControlPermissions!.Slots).Status);
        if (excavation) VerifyExcavation(world, final, production, boundaries);
        else VerifyRations(world, final, commands, production, boundaries, witness);
        authority.BackupForHost(Path.Combine(output, "world.sqlite3"));
        File.WriteAllBytes(Path.Combine(output, "final.ofc"), WorldSnapshotCodec.Encode(final));
        Write(output, "receipts.json", submitted);
        Write(output, "identities.json", storedIdentities.Select(row => new { Id = row.Identity.ToString(), row.Sequence, row.Kind }));
        Write(output, "journal.json", journal.Select(row => new { row.Sequence, row.Kind, row.AdvancingSteps,
            Cause = row.CauseId.ToString(), Batch = Convert.ToHexString(row.AcceptedBatch), Outcomes = Convert.ToHexString(row.Outcomes),
            Before = row.BeforeHash.Value, After = row.AfterHash.Value }));
    }

    private static void VerifyRations(SelectedProductionWorld world, WorldSnapshot final, WorldActionCommand[] commands,
        IReadOnlyList<WorldActionCommand> production, IReadOnlyDictionary<EntityId, (WorldSnapshot Before, WorldSnapshot After)> boundaries,
        WorldControlWitness witness)
    {
        EntityId custodian = world.Prepared.GetResident(world.Owner).InventoryCustodianId;
        var berry = world.Prepared.Gathering!.GetNode(world.BerryNode);
        var gathered = production.Take(2).ToArray();
        foreach (var command in gathered)
        {
            var intent = Assert.IsType<GatherIntent>(command.Intent);
            Assert.Equal(world.BerryNode, intent.NodeId);
            var (before, after) = boundaries[command.CommandId];
            var tool = before.Inventory.Custody.Lots.Single(lot => lot.LotId == intent.ToolLotId);
            Assert.Equal(custodian, tool.CustodianId);
            Assert.Equal(GatherToolKind.Sickle, before.Gathering!.Tools.Single(value => value.Definition == tool.Definition).Kind);
            Assert.Equal(tool, after.Inventory.Custody.Lots.Single(lot => lot.LotId == tool.LotId));
            AssertEffort(before, after, world.Owner);
            var previousNode = before.Gathering.GetNode(world.BerryNode);
            var nextNode = after.Gathering!.GetNode(world.BerryNode);
            Assert.Equal(previousNode.NextUnissuedUnit + 1, nextNode.NextUnissuedUnit);
            Assert.Equal(previousNode.RemainingSwings - 1, nextNode.RemainingSwings);
            var lot = Assert.Single(after.Inventory.Custody.Lots, lot => lot.LotId == EntityId.DeriveSpawned(command.CommandId, 0));
            Assert.Equal(custodian, lot.CustodianId);
            Assert.Equal(berry.Definition.YieldDefinition, lot.Definition);
            Assert.Equal(1UL, lot.Quantity);
            Assert.Equal(previousNode.SourceId, lot.Source.SourceId);
            Assert.Equal(previousNode.UnitCapacity, nextNode.UnitCapacity);
            Assert.Equal(previousNode.NextUnissuedUnit, lot.Source.Start);
            Assert.Equal(nextNode.NextUnissuedUnit, lot.Source.End);
        }
        var craft = production[2];
        var request = Assert.IsType<CraftIntent>(craft.Intent).Request;
        Assert.Equal(world.Recipe, request.RecipeId);
        Assert.Equal(custodian, request.CustodianId);
        var (beforeCraft, afterCraft) = boundaries[craft.CommandId];
        Assert.Equal(beforeCraft.Production!.Revision, request.ExpectedProductionRevision);
        Assert.Equal(beforeCraft.Inventory.Custody.GetStorage(custodian).Revision, request.ExpectedStorageRevision);
        var batch = Assert.Single(final.Production!.Batches);
        Assert.Equal(EntityId.DeriveSpawned(craft.CommandId, 0), batch.BatchId);
        Assert.Equal(craft.CommandId, batch.CommandId);
        Assert.Equal(world.Owner, batch.ActorId);
        Assert.Equal(custodian, batch.CustodianId);
        Assert.Equal(world.Recipe, batch.RecipeId);
        Assert.Equal(3, batch.Ingredients.Count);
        var gatheredIds = gathered.Select(command => EntityId.DeriveSpawned(command.CommandId, 0)).ToHashSet();
        Assert.Equal(2, batch.Ingredients.Count(ingredient => gatheredIds.Contains(ingredient.LotId)));
        var wood = Assert.Single(batch.Ingredients, ingredient => !gatheredIds.Contains(ingredient.LotId));
        foreach (var ingredient in batch.Ingredients)
        {
            Assert.Equal(IngredientLotKind.Source, ingredient.Kind);
            Assert.Equal(1UL, ingredient.Quantity);
            Assert.Equal(beforeCraft.Inventory.Custody.Lots.Single(lot => lot.LotId == ingredient.LotId).Source.Start, ingredient.Source!.Start);
            Assert.Contains(request.Ingredients, selected => selected.LotId == ingredient.LotId && selected.Quantity == 1 && selected.ExpectedRevision == ingredient.Revision);
        }
        Assert.All(gatheredIds, id => Assert.DoesNotContain(final.Inventory.Custody.Lots, lot => lot.LotId == id));
        Assert.Equal(world.Prepared.Inventory.Custody.Lots.Select(lot => lot.LotId), final.Inventory.Custody.Lots.Select(lot => lot.LotId));
        foreach (var original in world.Prepared.Inventory.Custody.Lots)
        {
            var actual = final.Inventory.Custody.Lots.Single(lot => lot.LotId == original.LotId);
            Assert.Equal(original.LotId == wood.LotId ? new CountedSourceLot(original.LotId, original.CustodianId, original.Definition,
                new(original.Source.SourceId, original.Source.Start + 1, original.Source.End), original.Revision + 1, original.Protection, original.ReservedBy) : original, actual);
        }
        var recipe = final.Production.GetRecipe(world.Recipe);
        Assert.Equal(request.RecipeVersion, recipe.Version);
        ulong inputMass = batch.Ingredients.Aggregate(0UL, (sum, ingredient) => sum + recipe.BindingFor(ingredient.Definition).Physical.MassPerUnit.Value * ingredient.Quantity);
        ulong outputMass = afterCraft.Production!.Lots.Aggregate(0UL, (sum, lot) => sum + recipe.BindingFor(lot.Definition).Physical.MassPerUnit.Value * lot.Quantity);
        Assert.Equal(90_000UL, inputMass);
        Assert.Equal(inputMass, outputMass);
        var offer = Assert.Single(commands, command => command.Intent is ProposeMaterialIntent);
        var accept = Assert.Single(commands, command => command.Intent is AcceptMaterialProposalIntent);
        Assert.Equal(witness, offer.ControlWitness);
        Assert.Equal(world.Owner, offer.ActorId);
        Assert.Equal(world.Recipient, accept.ActorId);
        Assert.Null(accept.ControlWitness);
        var outcome = Assert.Single(final.Coordination!.Proposals!.Outcomes);
        Assert.Empty(final.Coordination.Proposals.Open);
        Assert.Equal(WorldMaterialProposalOutcomeKind.Accepted, outcome.Kind);
        Assert.Equal(offer.CommandId, outcome.Proposal.CreationCommandId);
        Assert.Equal(accept.CommandId, outcome.TerminalCommandId);
        var terms = Assert.IsType<ProposeMaterialIntent>(offer.Intent).Terms;
        Assert.Equal(ConsentedMaterialLotKind.Produced, terms.LotKind);
        Assert.Equal(1UL, terms.Quantity);
        Assert.Equal(EntityId.DeriveSpawned(craft.CommandId, 1), terms.LotId);
        Assert.Equal(custodian, terms.SourceCustodianId);
        Assert.Equal(world.Prepared.GetResident(world.Recipient).InventoryCustodianId, terms.DestinationCustodianId);
        Assert.Equal(terms, outcome.Proposal.Terms);
        var settlement = Assert.IsType<WorldMaterialProposalSettlement>(outcome.Settlement);
        Assert.Equal(outcome.Proposal.SplitChildId, settlement.MovedLotId);
        Assert.Equal(new TrustedCommandDelivery(accept.PrincipalId, accept.OriginSessionId), settlement.RecipientDelivery);
        Assert.Null(settlement.ControlWitness);
        Assert.Equal(3, final.Production.Lots.Count);
        var remainder = Assert.Single(final.Production.Lots, lot => lot.LotId == terms.LotId);
        var moved = Assert.Single(final.Production.Lots, lot => lot.LotId == settlement.MovedLotId);
        var offcut = Assert.Single(final.Production.Lots, lot => lot.LotId == EntityId.DeriveSpawned(craft.CommandId, 2));
        Assert.Equal(new ProductionOutputInterval(batch.BatchId, 0, 1, 2), remainder.Output);
        Assert.Equal(new ProductionOutputInterval(batch.BatchId, 0, 0, 1), moved.Output);
        Assert.Equal(new ProductionOutputInterval(batch.BatchId, 1, 0, 1), offcut.Output);
        Assert.Equal(custodian, remainder.CustodianId);
        Assert.Equal(terms.DestinationCustodianId, moved.CustodianId);
        Assert.Equal(custodian, offcut.CustodianId);
        Assert.Equal(1UL, remainder.Revision);
        Assert.Equal(0UL, moved.Revision);
        Assert.Equal(0UL, offcut.Revision);
        Assert.Equal(world.Prepared.Excavation!.ContentHash, final.Excavation!.ContentHash);
    }

    private static void VerifyExcavation(SelectedProductionWorld world, WorldSnapshot final, IReadOnlyList<WorldActionCommand> commands,
        IReadOnlyDictionary<EntityId, (WorldSnapshot Before, WorldSnapshot After)> boundaries)
    {
        var original = Assert.Single(world.Prepared.Excavation!.Sites);
        EntityId custodian = world.Prepared.GetResident(world.Owner).InventoryCustodianId;
        for (int index = 0; index < commands.Count; index++)
        {
            var command = commands[index];
            var intent = Assert.IsType<ExcavateIntent>(command.Intent);
            var (before, after) = boundaries[command.CommandId];
            var site = Assert.Single(after.Excavation!.Sites);
            Assert.Equal(original.SiteId, intent.SiteId);
            Assert.Equal((ulong)index, intent.ExpectedSiteRevision);
            Assert.Equal((ulong)index + 1, site.CompletedWork);
            AssertEffort(before, after, world.Owner);
            var tool = before.Inventory.Custody.Lots.Single(lot => lot.LotId == intent.ToolLotId);
            Assert.Equal(custodian, tool.CustodianId);
            Assert.Equal(original.RequiredTool, tool.Definition);
            Assert.Equal(tool, after.Inventory.Custody.Lots.Single(lot => lot.LotId == tool.LotId));
            var region = after.Space.Regions.Single(value => value.Bounds.Contains(original.Cell));
            Assert.Equal(index < 2, region.GetCell(original.Cell).IsSolid);
            Assert.Equal(index == 2, region.GetCell(original.Cell).IsExcavated);
            if (index < 2) Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
            else
            {
                Assert.Equal(before.LiquidCells!.Topology!.OpenCells.Count + 1, after.LiquidCells!.Topology!.OpenCells.Count);
                Assert.Equal(4UL, after.LiquidCells.Quantities.Where(pair => pair.Key.Coordinate == original.Cell).Aggregate(0UL, (sum, pair) => sum + pair.Value));
            }
        }
        var finalSite = Assert.Single(final.Excavation!.Sites);
        Assert.Equal(original.RequiredWork, finalSite.CompletedWork);
        var spawned = Assert.Single(final.Inventory.Custody.Lots, lot => lot.Source.SourceId == original.CountedSourceId);
        Assert.Equal(EntityId.DeriveSpawned(commands[^1].CommandId, 0), spawned.LotId);
        Assert.Equal(custodian, spawned.CustodianId);
        Assert.Equal(original.Quantity, spawned.Quantity);
        Assert.Equal(0UL, spawned.Source.Start);
        Assert.Equal(original.Quantity, spawned.Source.End);
        Assert.Equal(world.Prepared.Inventory.Custody.Lots.Select(lot => lot.LotId).Append(spawned.LotId).Order(), final.Inventory.Custody.Lots.Select(lot => lot.LotId).Order());
        foreach (var lot in world.Prepared.Inventory.Custody.Lots) Assert.Equal(lot, final.Inventory.Custody.Lots.Single(value => value.LotId == lot.LotId));
        Assert.Equal(world.Prepared.Production!.ContentHash, final.Production!.ContentHash);
        Assert.Contains(final.LiquidCells!.DomainCells, cell => cell.Coordinate == original.Cell);
        Assert.Equal(world.Prepared.LiquidCells!.Topology!.OpenCapacity.Value + 1_000_000_000UL, final.LiquidCells.Topology!.OpenCapacity.Value);
        ulong cursor = 0;
        Assert.Equal(world.Prepared.LiquidCells.Portions.Select(portion => portion.OriginId).Distinct(), final.LiquidCells.Portions.Select(portion => portion.OriginId).Distinct());
        foreach (var portion in final.LiquidCells.Portions.OrderBy(portion => portion.Start))
        {
            Assert.Equal(LiquidStockOrigin.RawSource, portion.Origin);
            Assert.Equal(cursor, portion.Start);
            cursor = portion.End;
        }
        Assert.Equal(6000UL, cursor);
        Assert.Equal(final.Calendar, final.LiquidCells.Boundary);
        Assert.Equal(final.Reservoirs!.ContentHash, final.LiquidCells.ReservoirHash);
        Assert.Empty(final.Coordination!.Proposals!.Open);
        Assert.Empty(final.Coordination.Proposals.Outcomes);
    }

    private static void AssertEffort(WorldSnapshot before, WorldSnapshot after, EntityId actor)
    {
        uint effort = before.Gathering!.GetActorWork(actor).Effort;
        Assert.True(effort >= GatherRulesV1.EffortPerSwing);
        Assert.Equal(effort - GatherRulesV1.EffortPerSwing + GatherRulesV1.EffortPerAdvancingStep, after.Gathering!.GetActorWork(actor).Effort);
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
    }

    private static void PreserveFinal(string output, SelectedProductionWorld world)
    {
        if (File.Exists(Path.Combine(output, "world.sqlite3"))) return;
        using var authority = SqliteWorldAuthority.OpenExisting(world.Database, WorldSnapshotCodec.Encode(world.Genesis));
        authority.BackupForHost(Path.Combine(output, "world.sqlite3"));
        File.WriteAllBytes(Path.Combine(output, "final.ofc"), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
    }

    private static void Write(string output, string name, object value) => File.WriteAllText(Path.Combine(output, name),
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
}
