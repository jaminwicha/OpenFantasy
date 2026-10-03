using System.Diagnostics;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.SelectedTradingNative;

public sealed partial class NativeSelectedTradingTests
{
    private static JsonElement Selection(NativeRosterProcess process) => process.Last.GetProperty("Selection");
    private static JsonElement Trading(NativeRosterProcess process) => process.Last.GetProperty("Trading");
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

    private static void Verify(string output, SelectedTradingWorld world, IReadOnlyList<NativeRosterProcess> processes,
        WorldControlWitness witness, WorldActionCommand buy, WorldActionCommand showing, WorldActionCommand sell,
        EntityId receivedLot, EntityId staleQuote)
    {
        StoredWorldCommit[] journal;
        using (var database = WorldDatabase.Open(world.Database, create: false))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(world.Genesis), database.ReadGenesis());
            journal = database.ReadJournal().ToArray();
        }
        var batches = journal.Select(row => WorldStepCodec.Decode(row.AcceptedBatch)).ToArray();
        var gameplay = processes.SelectMany(process => process.Submissions).ToArray();
        var permission = processes.SelectMany(process => process.Last.GetProperty("PermissionSubmissions").EnumerateArray())
            .Select(value => value.Clone()).ToArray();
        Assert.Equal(5, gameplay.Length);
        Assert.Equal(3, permission.Length);
        var submitted = gameplay.Concat(permission).ToArray();
        var commands = submitted.Select(value => WorldActionCodec.Decode(Convert.FromHexString(value.GetProperty("Command").GetString()!))).ToArray();
        Assert.Equal(commands.Length, commands.Select(command => command.CommandId).Distinct().Count());
        var offer = Assert.Single(commands, command => command.Intent is OfferControlPermissionIntent);
        var accepted = Assert.Single(commands, command => command.Intent is AcceptControlPermissionIntent);
        var revoked = Assert.Single(commands, command => command.Intent is RevokeControlPermissionIntent);
        Assert.Equal(WorldControlCapabilities.Trade, Assert.IsType<OfferControlPermissionIntent>(offer.Intent).Capabilities);
        Assert.Equal(offer.CommandId, witness.GrantCommandId);
        Assert.Equal(accepted.CommandId, witness.AcceptanceCommandId);
        Assert.Equal(world.Owner, offer.ActorId);
        Assert.Equal(world.Grantee, accepted.ActorId);
        Assert.Equal(world.Owner, revoked.ActorId);
        Assert.Null(offer.ControlWitness);
        Assert.Null(accepted.ControlWitness);
        Assert.Null(revoked.ControlWitness);

        using var authority = SqliteWorldAuthority.OpenExisting(world.Database, WorldSnapshotCodec.Encode(world.Genesis));
        var final = authority.ReadStateForHost();
        var recorded = new Dictionary<ulong, WorldSnapshot>();
        WorldSnapshot State(ulong sequence) => recorded.TryGetValue(sequence, out var existing) ? existing
            : recorded[sequence] = authority.ReadRecordedStateForHost(sequence).State;
        Assert.Equal(world.Prepared.ContentHash, State(world.Prepared.Revision).ContentHash);
        foreach (var value in submitted)
        {
            var command = WorldActionCodec.Decode(Convert.FromHexString(value.GetProperty("Command").GetString()!));
            var receipt = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command, new(command.PrincipalId, command.OriginSessionId)));
            Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
            Assert.Equal(Convert.FromHexString(value.GetProperty("Receipt").GetString()!), WorldReceiptCodecV1.Encode(receipt));
            Assert.Contains(batches.SelectMany(batch => batch.Actions), actual =>
                WorldActionCodec.Encode(actual).AsSpan().SequenceEqual(WorldActionCodec.Encode(command)));
            var control = world.Genesis.Inventory.Controls.Single(row => row.ActorId == (command.ControlWitness is null ? command.ActorId : world.Grantee));
            Assert.Equal(control.PrincipalId, command.PrincipalId);
            Assert.Equal(control.SessionId, command.OriginSessionId);
            if (command.ControlWitness is not null)
            {
                Assert.Equal(world.Owner, command.ActorId);
                Assert.Equal(witness, command.ControlWitness);
                Assert.True(command.Intent is AcceptSpotQuoteIntent or DiscloseMarketLotIntent or WithdrawMarketLotDisclosureIntent);
            }
        }
        var stopped = Assert.Single(commands, command => command.Intent is WithdrawMarketLotDisclosureIntent);
        var stoppedReceipt = authority.FindReceipt(stopped, new(stopped.PrincipalId, stopped.OriginSessionId))!;
        var beforeStop = State(stoppedReceipt.CommitSequence - 1);
        var afterStop = State(stoppedReceipt.CommitSequence);
        var stopProjectBefore = Assert.Single(beforeStop.Construction!.Projects);
        var stopProjectAfter = Assert.Single(afterStop.Construction!.Projects);
        if (stopProjectBefore.IsCompleted == stopProjectAfter.IsCompleted)
        {
            Assert.Equal(beforeStop.Inventory.ContentHash, afterStop.Inventory.ContentHash);
            Assert.Equal(beforeStop.Production!.ContentHash, afterStop.Production!.ContentHash);
        }
        else
        {
            // A real simultaneous completion has its own retained material cause, checked in full below.
            Assert.False(stopProjectBefore.IsCompleted);
            Assert.True(stopProjectAfter.IsCompleted);
            Assert.Contains(batches.Single(batch => batch.ExpectedRevision + 1 == stoppedReceipt.CommitSequence).Actions,
                command => command.CommandId == stopProjectAfter.Installed!.CompletionCommandId
                    && command.Intent is WorkConstructionIntent && command.ActorId == stopProjectAfter.BuilderActorId);
            Assert.Equal(beforeStop.Inventory.Custody.Lots.Where(lot => lot.ReservedBy != stopProjectBefore.ReservationId),
                afterStop.Inventory.Custody.Lots);
        }
        Assert.Equal(beforeStop.Progression!.Currency.Revision, afterStop.Progression!.Currency.Revision);
        Assert.Equal(world.Markets.Origin.MarketId, ((WithdrawMarketLotDisclosureIntent)stopped.Intent).MarketId);
        Assert.NotNull(beforeStop.Coordination!.Markets!.FindDisclosure(world.Owner, world.Markets.Origin.MarketId));
        Assert.Null(afterStop.Coordination!.Markets!.FindDisclosure(world.Owner, world.Markets.Origin.MarketId));
        var buyAcceptance = Assert.IsType<AcceptSpotQuoteIntent>(buy.Intent).Acceptance;
        var sellAcceptance = Assert.IsType<AcceptSpotQuoteIntent>(sell.Intent).Acceptance;
        var disclosure = Assert.IsType<DiscloseMarketLotIntent>(showing.Intent);
        var book = final.Coordination!.PaidExchange!;
        Assert.Equal(2, book.Outcomes.Count);
        var bought = Assert.Single(book.Outcomes, row => row.Quote.QuoteId == buyAcceptance.QuoteId);
        var sold = Assert.Single(book.Outcomes, row => row.Quote.QuoteId == sellAcceptance.QuoteId);
        Assert.All(book.Outcomes, row => Assert.Equal(WorldPaidQuoteOutcomeKind.Filled, row.Kind));
        Assert.Equal(staleQuote, Assert.Single(book.Open).QuoteId);
        Assert.DoesNotContain(batches.SelectMany(batch => batch.Actions), command =>
            command.Intent is AcceptSpotQuoteIntent acceptance && acceptance.Acceptance.QuoteId == staleQuote);
        foreach (var pair in new[] { (Outcome: bought, Acceptance: buyAcceptance), (Outcome: sold, Acceptance: sellAcceptance) })
        {
            var quote = pair.Outcome.Quote.Quote;
            Assert.True(pair.Acceptance.Binds(quote));
            Assert.Equal(1UL, pair.Acceptance.Quantity);
            Assert.Equal(0UL, pair.Acceptance.Fees);
            Assert.Equal(world.Markets.Origin.MarketId, final.Coordination.Markets!.FindQuote(quote.QuoteId)!.MarketId);
            Assert.Equal(world.Markets.Origin.MerchantActorId, quote.IssuerActorId);
            Assert.Equal(world.Owner, quote.CounterpartyActorId);
            var settlement = Assert.IsType<WorldPaidSettlement>(pair.Outcome.Settlement);
            Assert.Equal(witness, settlement.ControlWitness);
            Assert.Equal(receivedLot, settlement.MovedLotId);
            Assert.NotNull(WorldMarketLocality.FindAvailable(State(pair.Outcome.WorldSequence - 1), world.Owner, world.Markets.Origin.MarketId));
        }
        Assert.Equal(SpotQuoteSide.IssuerSells, bought.Quote.Quote.Side);
        Assert.Equal(SpotQuoteSide.IssuerBuys, sold.Quote.Quote.Side);
        Assert.Equal(20UL, buyAcceptance.Payment);
        Assert.Equal(14UL, sellAcceptance.Payment);
        Assert.Equal(receivedLot, disclosure.LotId);
        Assert.Equal(1UL, disclosure.Quantity);
        Assert.Equal(sold.Quote.Quote.LotId, disclosure.LotId);
        Assert.Equal(disclosure.ExpectedLotRevision, sold.Quote.Quote.ExpectedLotRevision);
        var afterBuy = State(bought.WorldSequence);
        var beforeSell = State(sold.WorldSequence - 1);
        var afterSell = State(sold.WorldSequence);
        var shelf = world.Prepared.Inventory.Custody.Lots.Single(lot => lot.CustodianId == world.Markets.Origin.CustodianId
            && lot.Definition == world.Markets.Origin.Commodity);
        var carried = afterBuy.Inventory.Custody.GetLot(receivedLot);
        Assert.Equal(new SourceInterval(shelf.Source.SourceId, shelf.Source.Start, shelf.Source.Start + 1), carried.Source);
        Assert.Equal(world.Prepared.GetResident(world.Owner).InventoryCustodianId, carried.CustodianId);
        Assert.Equal(carried, beforeSell.Inventory.Custody.GetLot(receivedLot));
        var returned = afterSell.Inventory.Custody.GetLot(receivedLot);
        Assert.Equal(carried.Source, returned.Source);
        Assert.Equal(carried.Definition, returned.Definition);
        Assert.Equal(carried.Revision + 1, returned.Revision);
        Assert.Equal(world.Markets.Origin.CustodianId, returned.CustodianId);
        Assert.Equal(returned, final.Inventory.Custody.GetLot(receivedLot));
        Assert.Equal(new SourceInterval(shelf.Source.SourceId, shelf.Source.Start + 1, shelf.Source.End), final.Inventory.Custody.GetLot(shelf.LotId).Source);
        EntityId[] retired = VerifyNpcConstruction(world, final, journal, authority);
        Assert.Equal(world.Prepared.Inventory.Custody.Lots.Select(lot => lot.LotId).Except(retired).Append(receivedLot).Order(),
            final.Inventory.Custody.Lots.Select(lot => lot.LotId).Order());
        foreach (var lot in world.Prepared.Inventory.Custody.Lots.Where(value => value.LotId != shelf.LotId && !retired.Contains(value.LotId)))
            Assert.Equal(lot, final.Inventory.Custody.GetLot(lot.LotId));
        Assert.Equal(world.Prepared.Inventory.Sources, final.Inventory.Sources);
        Assert.Equal(world.Prepared.Inventory.Controls, final.Inventory.Controls);
        Assert.Equal(world.Prepared.Progression!.Currency.AdmittedSupply, final.Progression!.Currency.AdmittedSupply);
        Assert.Equal(world.Prepared.Progression.Currency.Accounts.Select(account => (account.AccountId, account.Kind, account.ActorId)),
            final.Progression.Currency.Accounts.Select(account => (account.AccountId, account.Kind, account.ActorId)));
        foreach (var account in world.Prepared.Progression.Currency.Accounts)
        {
            var actual = final.Progression.Currency.GetAccount(account.AccountId);
            ulong expected = account.ActorId == world.Owner ? account.Balance - 6
                : account.ActorId == world.Markets.Origin.MerchantActorId ? account.Balance + 6 : account.Balance;
            Assert.Equal(expected, actual.Balance);
        }
        Assert.Equal(world.Prepared.Progression.Actors.Select(actor => (actor.ActorId, actor.CurrentHp, actor.CurrentMp)),
            final.Progression.Actors.Select(actor => (actor.ActorId, actor.CurrentHp, actor.CurrentMp)));
        Assert.Equal(world.Prepared.Care!.ContentHash, final.Care!.ContentHash);
        Assert.Equal(world.Prepared.Residents.Select(actor => (actor.ActorId, actor.ControllerKind, actor.InventoryCustodianId)),
            final.Residents.Select(actor => (actor.ActorId, actor.ControllerKind, actor.InventoryCustodianId)));
        Assert.Equal(world.Prepared.Space.Actors.Select(actor => (actor.ActorId, actor.Binding.FormInstanceId, actor.Binding.PhysicalBodyId)),
            final.Space.Actors.Select(actor => (actor.ActorId, actor.Binding.FormInstanceId, actor.Binding.PhysicalBodyId)));
        Assert.Equal(world.Prepared.Space.Actors.Where(actor => world.Actors.Contains(actor.ActorId)).Select(actor => (actor.ActorId, actor.Feet)),
            final.Space.Actors.Where(actor => world.Actors.Contains(actor.ActorId)).Select(actor => (actor.ActorId, actor.Feet)));
        foreach (var movement in journal.Where(row => row.Sequence > world.Prepared.Revision)
            .SelectMany(row => WorldStepCodec.Decode(row.AcceptedBatch).Movement))
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
        Assert.Equal(final.ContentHash, State(final.Revision).ContentHash);
        authority.BackupForHost(Path.Combine(output, "world.sqlite3"));
        File.WriteAllBytes(Path.Combine(output, "final.ofc"), WorldSnapshotCodec.Encode(final));
        Write(output, "receipts.json", submitted);
        Write(output, "journal.json", journal.Select(row => new { row.Sequence, row.Kind, row.AdvancingSteps,
            Cause = row.CauseId.ToString(), Batch = Convert.ToHexString(row.AcceptedBatch), Outcomes = Convert.ToHexString(row.Outcomes),
            Before = row.BeforeHash.Value, After = row.AfterHash.Value }));
    }

    /// <summary>Unrelated NPC work remains live: every allowed material difference must have its real construction cause.</summary>
    private static EntityId[] VerifyNpcConstruction(SelectedTradingWorld world, WorldSnapshot final,
        IReadOnlyList<StoredWorldCommit> journal, SqliteWorldAuthority authority)
    {
        var original = Assert.Single(world.Prepared.Construction!.Projects);
        var current = Assert.Single(final.Construction!.Projects);
        Assert.Equal(original.ProjectId, current.ProjectId);
        Assert.Equal(original.Definition.Version, current.Definition.Version);
        Assert.Equal(original.BuilderActorId, current.BuilderActorId);
        Assert.Equal(ResidentControllerKind.Host, final.GetResident(current.BuilderActorId).ControllerKind);
        var work = journal.Where(row => row.Sequence > world.Prepared.Revision)
            .SelectMany(row => WorldStepCodec.Decode(row.AcceptedBatch).Actions.Select(command => (row.Sequence, Command: command)))
            .Where(value => value.Command.Intent is WorkConstructionIntent).ToArray();
        Assert.Equal(original.Revision + (ulong)work.Length, current.Revision);
        Assert.Equal(original.Progress.CompletedUnits + (ulong)work.Length, current.Progress.CompletedUnits);
        Assert.Equal(original.Progress.Elapsed.TotalMilliseconds + (UInt128)1800 * (ulong)work.Length, current.Progress.Elapsed.TotalMilliseconds);
        var binding = world.Prepared.Inventory.Controls.Single(control => control.ActorId == current.BuilderActorId);
        foreach (var item in work.Select((value, index) => (value.Sequence, value.Command, Index: index)))
        {
            var intent = (WorkConstructionIntent)item.Command.Intent;
            Assert.Equal(current.ProjectId, intent.ProjectId);
            Assert.Equal(current.BuilderActorId, item.Command.ActorId);
            Assert.Equal(binding.PrincipalId, item.Command.PrincipalId);
            Assert.Equal(binding.SessionId, item.Command.OriginSessionId);
            Assert.Null(item.Command.ControlWitness);
            Assert.Equal(original.Revision + (ulong)item.Index, intent.ExpectedProjectRevision);
            var tool = world.Prepared.Inventory.Custody.GetLot(intent.ToolLotId);
            Assert.Equal(world.Prepared.GetResident(current.BuilderActorId).InventoryCustodianId, tool.CustodianId);
            Assert.Equal(current.Definition.RequiredTool.Item, tool.Definition);
            Assert.Equal(tool.Revision, intent.ExpectedToolLotRevision);
            Assert.Equal(tool, final.Inventory.Custody.GetLot(tool.LotId));
            var receipt = authority.FindReceipt(item.Command, new(binding.PrincipalId, binding.SessionId));
            Assert.NotNull(receipt);
            Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
            Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            Assert.Equal(item.Sequence, receipt.CommitSequence);
        }
        if (!current.IsCompleted)
        {
            Assert.Equal(world.Prepared.Production!.ContentHash, final.Production!.ContentHash);
            Assert.Equal(world.Prepared.Space.Structures, final.Space.Structures);
            return [];
        }
        Assert.Equal(current.Definition.RequiredWork.Value, current.Progress.CompletedUnits);
        var installed = Assert.IsType<InstalledStructure>(current.Installed);
        Assert.Equal(work[^1].Command.CommandId, installed.CompletionCommandId);
        var transform = Assert.Single(final.Production!.Batches, batch =>
            world.Prepared.Production!.Batches.All(previous => previous.BatchId != batch.BatchId));
        Assert.Equal(installed.ProductionBatchId, transform.BatchId);
        Assert.Equal(installed.CompletionCommandId, transform.CommandId);
        Assert.Equal(current.BuilderActorId, transform.ActorId);
        Assert.Equal(current.ProjectCustodianId, transform.CustodianId);
        Assert.Equal(current.Definition.MaterialTransform.DefinitionId, transform.RecipeId);
        Assert.Equal(current.Definition.MaterialTransform.Version, transform.RecipeVersion);
        var funding = world.Prepared.Inventory.Custody.Lots.Where(lot => lot.ReservedBy == original.ReservationId).ToArray();
        Assert.Equal(funding.Select(lot => lot.LotId).Order(), transform.Ingredients.Select(input => input.LotId).Order());
        foreach (var ingredient in transform.Ingredients)
        {
            var input = Assert.Single(funding, lot => lot.LotId == ingredient.LotId);
            Assert.Equal(IngredientLotKind.Source, ingredient.Kind);
            Assert.Equal(input.Source, ingredient.Source);
            Assert.Equal(input.Definition, ingredient.Definition);
            Assert.Equal(input.Revision, ingredient.Revision);
            Assert.Equal(input.Quantity, ingredient.Quantity);
            Assert.DoesNotContain(final.Inventory.Custody.Lots, lot => lot.LotId == input.LotId);
        }
        Assert.Equal(6UL, transform.Ingredients.Aggregate(0UL, (sum, input) => sum + input.Quantity));
        var outputs = final.Production.Lots.Where(lot => lot.Output.BatchId == transform.BatchId).ToArray();
        Assert.Equal(1UL, Assert.Single(outputs, lot => lot.LotId == installed.GuardLotId).Quantity);
        Assert.Equal(4UL, outputs.Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
        Assert.Equal(world.Prepared.Production!.Lots.Select(lot => lot.LotId).Concat(outputs.Select(lot => lot.LotId)).Order(),
            final.Production.Lots.Select(lot => lot.LotId).Order());
        Assert.Equal(world.Prepared.Production.Batches.Select(batch => batch.BatchId).Append(transform.BatchId).Order(),
            final.Production.Batches.Select(batch => batch.BatchId).Order());
        foreach (var lot in world.Prepared.Production.Lots)
            Assert.Equal(lot, final.Production.Lots.Single(value => value.LotId == lot.LotId));
        var recipe = current.Definition.MaterialTransform;
        ulong inputMass = transform.Ingredients.Aggregate(0UL, (sum, input) =>
            sum + recipe.BindingFor(input.Definition).Physical.MassPerUnit.Value * input.Quantity);
        ulong outputMass = outputs.Aggregate(0UL, (sum, lot) =>
            sum + recipe.BindingFor(lot.Definition).Physical.MassPerUnit.Value * lot.Quantity);
        Assert.Equal(300000UL, inputMass);
        Assert.Equal(inputMass, outputMass);
        Assert.Equal(world.Prepared.Space.Structures.Select(value => value.StructureId).Append(installed.StructureId).Order(),
            final.Space.Structures.Select(value => value.StructureId).Order());
        Assert.DoesNotContain(final.Inventory.Custody.Lots, lot => lot.ReservedBy == original.ReservationId);
        return funding.Select(lot => lot.LotId).ToArray();
    }

    private static void PreserveFinal(string output, SelectedTradingWorld world)
    {
        if (File.Exists(Path.Combine(output, "world.sqlite3"))) return;
        using var authority = SqliteWorldAuthority.OpenExisting(world.Database, WorldSnapshotCodec.Encode(world.Genesis));
        authority.BackupForHost(Path.Combine(output, "world.sqlite3"));
        File.WriteAllBytes(Path.Combine(output, "final.ofc"), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
    }
    private static void Write(string output, string name, object value) => File.WriteAllText(Path.Combine(output, name),
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
}
