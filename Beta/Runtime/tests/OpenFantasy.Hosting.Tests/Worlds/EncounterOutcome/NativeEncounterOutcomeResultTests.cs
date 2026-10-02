using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.EncounterOutcome;

/// <summary>
/// Authenticates the native outcome routes named by OPENFANTASY_NATIVE_ENCOUNTER_OUTCOME_RESULTS against their complete
/// saved journals. Each fixture genesis is re-derived from its recorded seed, player name and declared calibration and
/// must match byte for byte (an edited fresh root cannot pass as a declaration). The exported prefix must read back as
/// the fixture's before-root; every later commit is replayed through the authority's processor and must reproduce its
/// stored root. After the prefix, the journal must hold exactly the native sequence: on the road routes one entry by the
/// chief's own host-owned control (the care fixture's own entry is part of its declared managed preparation), the player's
/// own ordinary choices, exactly one reading of the exact resolved outcome by the player's own control binding, and one
/// actionless zero-duration world completion. Exact selection-to-receipt correlation: every submission the probe observed
/// at the client's submission boundary — its selected offer's scope and intent, generated command identity and canonical
/// bytes, and receipt — is the journal's stored command outcome and is contained in the commit at its sequence, and every
/// player-principal command after the prefix was observed; altering any captured intent, command, receipt or identity
/// fails. The care routes' reading happened while Wren's care was actually admitted and no care was ever requested. The
/// victory route later sells the carried lot at Kegga; the defeat route keeps it and ends incapacitated. An absent
/// manifest is a failure.
/// </summary>
public sealed class NativeEncounterOutcomeResultTests
{
    /// <summary>One submission the probe reported at the client's submission boundary.</summary>
    private sealed record CapturedSubmission(string Stage, string Action, string? IntentType, string? IntentText, string CommandId,
        string CommandBytes, string ReceiptBytes, ulong CommitSequence, string Status, string Rejection, string PrincipalId,
        string SessionId, string ActorId, string WorldId, string BranchId, ulong ExpectedActorRevision);

    [Fact]
    public void Every_native_outcome_route_is_authenticated_against_its_saved_journal()
    {
        string manifestPath = Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_ENCOUNTER_OUTCOME_RESULTS") is { Length: > 0 } supplied
            ? supplied : throw new InvalidOperationException("OPENFANTASY_NATIVE_ENCOUNTER_OUTCOME_RESULTS must name the native manifest.");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement[] results = manifest.RootElement.GetProperty("results").EnumerateArray().ToArray();
        Assert.Equal(new[] { "victory", "defeat", "care", "care-wheel" }, results.Select(value => value.GetProperty("mode").GetString()));
        foreach (JsonElement result in results) Authenticate(result);
        Assert.Equal(results[1].GetProperty("save").GetString(), manifest.RootElement.GetProperty("reopen").GetProperty("save").GetString());
        using JsonDocument reopened = JsonDocument.Parse(File.ReadAllText(manifest.RootElement.GetProperty("reopen").GetProperty("report").GetString()!));
        Assert.Empty(reopened.RootElement.GetProperty("Submissions").EnumerateArray());
    }

    private static void Authenticate(JsonElement result)
    {
        string mode = result.GetProperty("mode").GetString()!;
        string fixture = result.GetProperty("fixture").GetString()!;
        Assert.Equal(mode == "care-wheel" ? "care" : mode, fixture);
        string scratch = Path.Combine(Path.GetTempPath(), "openfantasy-native-outcome-auth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            string original = result.GetProperty("save").GetString()!;
            Assert.False(File.Exists(original + "-wal") || File.Exists(original + "-shm"), "The native save must be closed.");
            string save = Path.Combine(scratch, "save.sqlite");
            File.Copy(original, save);
            byte[] genesisBytes = File.ReadAllBytes(result.GetProperty("genesis").GetString()!);
            using JsonDocument metadata = JsonDocument.Parse(File.ReadAllText(result.GetProperty("metadata").GetString()!));
            using JsonDocument report = JsonDocument.Parse(File.ReadAllText(result.GetProperty("report").GetString()!));
            JsonElement meta = metadata.RootElement;
            Assert.Equal(("native-encounter-outcome-fixture-v1", fixture), (meta.GetProperty("Kind").GetString(), meta.GetProperty("Route").GetString()));
            Assert.Equal(("native-encounter-outcome-v1", mode, "passed"), (report.RootElement.GetProperty("Kind").GetString(),
                report.RootElement.GetProperty("Mode").GetString(), report.RootElement.GetProperty("Status").GetString()));

            // The declared fresh genesis, re-derived from its recorded seed, name and calibration rows.
            Assert.Equal(Derive(meta, fixture), genesisBytes);
            WorldSnapshot genesis = WorldSnapshotCodec.Decode(genesisBytes);
            WorldSnapshot before = WorldSnapshotCodec.Decode(File.ReadAllBytes(result.GetProperty("before").GetString()!));
            Assert.Equal(meta.GetProperty("BeforeHash").GetString(), before.ContentHash.Value);
            EntityId player = before.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
            Assert.Equal(meta.GetProperty("PlayerActorId").GetString(), player.ToString());
            EntityId opponent = before.Residents.Single(value => value.ActorId.ToString() == meta.GetProperty("OpponentActorId").GetString()).ActorId;
            ActorControlBinding playerControl = before.Inventory.Controls.Single(value => value.ActorId == player);
            ActorControlBinding opponentControl = before.Inventory.Controls.Single(value => value.ActorId == opponent);
            Assert.NotEqual(playerControl.PrincipalId, opponentControl.PrincipalId);
            Assert.Equal(EncounterOutcomeState.Fresh().ContentHash, before.EncounterOutcome!.ContentHash);
            // The care fixture's declared managed preparation includes the genuine approach and the player's own entry and
            // stops at the Active first decision; the road fixtures stop in exploration before any encounter.
            bool care = fixture == "care";
            Assert.Equal(care ? (1UL, EncounterLifecyclePhase.Active) : (0UL, EncounterLifecyclePhase.Exploration),
                (before.EncounterLifecycle!.EncounterCount, before.EncounterLifecycle.Phase));
            if (care) Assert.Equal(1, before.Progression!.GetActor(player).CurrentHp);

            WorldSnapshot final;
            using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(save, genesisBytes)) final = authority.ReadStateForHost();
            Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(RecordedWorldReader.ReadForHost(save, before.Revision).State));
            List<StoredWorldCommit> journal;
            List<StoredWorldCommandOutcome> outcomes;
            using (WorldDatabase database = WorldDatabase.Open(save, create: false))
            {
                journal = database.ReadJournal(before.Revision).ToList();
                outcomes = database.ReadCommandOutcomes(before.Revision).ToList();
            }

            var processor = new WorldStepProcessor();
            WorldSnapshot state = before;
            HashSet<EntityId> commandIds = [];
            Dictionary<ulong, WorldStepBatch> batches = [];
            ulong? entered = null, read = null, completed = null;
            EntityId? entryCommand = null, readCommand = null;
            int strikes = 0, waits = 0, careRequests = 0;
            bool careAvailableAtReading = false;
            WorldSnapshot? atOutcome = null;
            foreach (StoredWorldCommit commit in journal)
            {
                WorldStepBatch batch = WorldStepCodec.Decode(commit.AcceptedBatch);
                batches[commit.Sequence] = batch;
                WorldSnapshot next = processor.Decide(state, batch, _ => false).Candidate;
                Assert.Equal(commit.AfterHash, next.ContentHash);
                foreach (WorldActionCommand command in batch.Actions)
                {
                    Assert.True(commandIds.Add(command.CommandId), $"{mode}: a command identity was reused.");
                    if (command.ActorId == player)
                    {
                        // Every player command came through the player's own control binding.
                        Assert.Equal((playerControl.PrincipalId, playerControl.SessionId), (command.PrincipalId, command.OriginSessionId));
                    }
                    switch (command.Intent)
                    {
                        case EnterEncounterIntent when next.EncounterLifecycle!.EncounterCount > state.EncounterLifecycle!.EncounterCount:
                            Assert.Null(entered);
                            EntityId expected = care ? player : opponent;
                            ActorControlBinding control = care ? playerControl : opponentControl;
                            Assert.Equal((expected, control.PrincipalId, control.SessionId),
                                (command.ActorId, command.PrincipalId, command.OriginSessionId));
                            entered = commit.Sequence;
                            entryCommand = command.CommandId;
                            break;
                        case QueueEncounterAbilityIntent when command.ActorId == player:
                            strikes++;
                            break;
                        case QueueEncounterWaitIntent when command.ActorId == player:
                            waits++;
                            break;
                        case AcknowledgeCareIntent:
                            careRequests++;
                            break;
                        case AcknowledgeEncounterOutcomeIntent reading when next.EncounterOutcome!.HasAcknowledged(player)
                            && !state.EncounterOutcome!.HasAcknowledged(player):
                            Assert.Null(read);
                            Assert.Equal(player, command.ActorId);
                            Assert.Equal(WorldBatchKind.EncounterLifecycle, batch.Kind);
                            EncounterOutcomeDetermination determination = WorldEncounterOutcomes.Determine(state)!;
                            Assert.Equal((determination.EncounterId, determination.ActionHash), (reading.EncounterId, reading.ExpectedActionHash));
                            careAvailableAtReading = LocalCareView.Project(state, player, false)?.Offers.Any(offer => offer.CanAcknowledge) == true;
                            atOutcome = state;
                            read = commit.Sequence;
                            readCommand = command.CommandId;
                            break;
                    }
                }
                if (next.EncounterOutcome!.FinalizedCount > state.EncounterOutcome!.FinalizedCount)
                {
                    // The world's own completion: actionless, zero-duration, at Outcome, right after the reading.
                    Assert.Null(completed);
                    Assert.Equal((WorldBatchKind.EncounterLifecycle, 0), (batch.Kind, batch.Actions.Count));
                    Assert.Equal(EncounterLifecyclePhase.Outcome, state.EncounterLifecycle!.Phase);
                    Assert.Equal(EncounterLifecyclePhase.Exploration, next.EncounterLifecycle!.Phase);
                    Assert.Equal((state.Calendar, state.AdvancingSteps), (next.Calendar, next.AdvancingSteps));
                    foreach (WorldResident resident in state.Residents)
                    {
                        Assert.Equal(state.Space.GetActor(resident.ActorId).Feet, next.Space.GetActor(resident.ActorId).Feet);
                        Assert.Equal(state.Progression!.GetActor(resident.ActorId).CurrentHp, next.Progression!.GetActor(resident.ActorId).CurrentHp);
                    }
                    completed = commit.Sequence;
                }
                state = next;
            }
            Assert.Equal(final.ContentHash, state.ContentHash);
            if (care) Assert.Null(entered);
            ulong begun = entered ?? before.Revision;
            Assert.True((care || entered is not null) && read > begun && completed > read,
                $"{mode}: entry, reading and completion are out of order.");
            Assert.Equal(1UL, final.EncounterOutcome!.FinalizedCount);
            EncounterOutcomeRecord record = final.EncounterOutcome.Last!;
            Assert.Equal(new[] { player }, record.AcknowledgedActorIds);
            Assert.Equal(EncounterOutcomeResult.Victory, record.Result);
            int playerSide = atOutcome!.EncounterActions!.GetCombatant(player).Side;
            int opponentSide = atOutcome.EncounterActions.GetCombatant(opponent).Side;
            Assert.Equal(report.RootElement.GetProperty("CompletedAtSequence").GetUInt64(), record.WorldSequence);
            Assert.Equal(0, careRequests);

            // Exact selection-to-receipt correlation at the client's submission boundary, against the stored command outcomes.
            CapturedSubmission[] captured = Captured(report.RootElement);
            AuthenticateSubmissions(captured, outcomes, batches, playerControl.PrincipalId);
            // The one reading is the exact captured selection; host-owned entry and the actionless completion were never
            // client submissions.
            CapturedSubmission readingRow = Assert.Single(captured, row => row.IntentType == nameof(AcknowledgeEncounterOutcomeIntent));
            Assert.Equal((readCommand.ToString(), read!.Value), (readingRow.CommandId, readingRow.CommitSequence));
            Assert.Equal(mode switch { "victory" => "continue", "defeat" => "accept", "care" => "decline", _ => "wheel-decline" }, readingRow.Stage);
            Assert.DoesNotContain(captured, row => row.CommandId == entryCommand?.ToString() || row.CommitSequence == completed);
            Assert.All(outcomes.Where(row => row.PrincipalId != playerControl.PrincipalId), row =>
                Assert.DoesNotContain(captured, value => value.CommandId == row.CommandId.ToString()));
            Assert.DoesNotContain(captured, row => row.IntentType == nameof(AcknowledgeCareIntent));
            CapturedSubmission[] combat = captured.TakeWhile(row => row != readingRow).ToArray();
            Assert.Equal(care ? waits : strikes, combat.Length);
            Assert.All(combat, row => Assert.Equal(care ? nameof(QueueEncounterWaitIntent) : nameof(QueueEncounterAbilityIntent), row.IntentType));
            AssertAlteredCapturesFail(captured, outcomes, batches, playerControl.PrincipalId);
            // Each pre-input selection, completed with the identity the boundary generated, is exactly the submitted and
            // journaled command; altering one nested selected fact fails while the actual command and receipt stay intact.
            (string CommandId, string ExpectedBytes)[] selections = [.. report.RootElement.GetProperty("Selections").EnumerateArray()
                .Select(row => (row.GetProperty("CommandId").GetString()!, row.GetProperty("ExpectedCommandBytes").GetString()!))];
            AuthenticateSelections(selections, captured, outcomes, playerControl.PrincipalId);
            AssertAlteredSelectionFails(selections, captured, outcomes, playerControl.PrincipalId);

            CapturedSubmission[] afterReading = captured.SkipWhile(row => row != readingRow).Skip(1).ToArray();
            switch (mode)
            {
                case "victory":
                    Assert.True(strikes >= 1);
                    Assert.Equal(playerSide, record.StandingSide);
                    Assert.Equal(0, final.Progression!.GetActor(opponent).CurrentHp);
                    // The carried lot left the player's custody to Kegga after the completion, for the reported total.
                    EntityId store = final.GetResident(player).InventoryCustodianId;
                    string lot = meta.GetProperty("CarriedLotId").GetString()!;
                    Assert.Contains(before.Inventory.Custody.Lots, value => value.LotId.ToString() == lot && value.CustodianId == store);
                    Assert.DoesNotContain(final.Inventory.Custody.Lots, value => value.LotId.ToString() == lot && value.CustodianId == store);
                    JsonElement trade = report.RootElement.GetProperty("Trade");
                    Assert.Equal(before.Progression!.Currency.GetActorAccount(player).Balance + trade.GetProperty("Total").GetUInt64(),
                        final.Progression.Currency.GetActorAccount(player).Balance);
                    // The post-completion sale is exactly the captured showing of the carried lot and the acceptance of Kegga's quote.
                    Assert.Equal(2, afterReading.Length);
                    Assert.All(afterReading, row => Assert.True(row.CommitSequence > completed));
                    DiscloseMarketLotIntent shown = Assert.IsType<DiscloseMarketLotIntent>(Stored(outcomes, afterReading[0]).Intent);
                    Assert.Equal(lot, shown.LotId.ToString());
                    AcceptSpotQuoteIntent accepted = Assert.IsType<AcceptSpotQuoteIntent>(Stored(outcomes, afterReading[1]).Intent);
                    Assert.Equal(trade.GetProperty("QuoteId").GetString(), accepted.Acceptance.QuoteId.ToString());
                    break;
                case "defeat":
                    Assert.True(strikes >= 1);
                    Assert.Empty(afterReading);
                    Assert.Equal(opponentSide, record.StandingSide);
                    Assert.Equal(0, final.Progression!.GetActor(player).CurrentHp);
                    Assert.Contains(final.Inventory.Custody.Lots, value => value.LotId.ToString() == meta.GetProperty("CarriedLotId").GetString()
                        && value.CustodianId == final.GetResident(player).InventoryCustodianId);
                    Assert.True(report.RootElement.GetProperty("FinalTerminal").GetBoolean());
                    break;
                default:
                    Assert.True(waits >= 1);
                    Assert.Empty(afterReading);
                    Assert.Equal(opponentSide, record.StandingSide);
                    Assert.Equal(0, final.Progression!.GetActor(player).CurrentHp);
                    // Wren's care was actually admitted at the reading; the player deliberately declined it.
                    Assert.True(careAvailableAtReading, "Care was not available when the native reading declined it.");
                    Assert.Null(final.Care!.Pending);
                    break;
            }
        }
        finally { Directory.Delete(scratch, recursive: true); }
    }

    private static CapturedSubmission[] Captured(JsonElement report) => report.GetProperty("Submissions").EnumerateArray().Select(row =>
    {
        JsonElement scope = row.GetProperty("Scope");
        JsonElement receipt = row.GetProperty("Receipt");
        Assert.Equal(JsonValueKind.Object, receipt.ValueKind);
        Assert.False(row.GetProperty("IsRetry").GetBoolean());
        return new CapturedSubmission(row.GetProperty("Stage").GetString()!, row.GetProperty("Action").GetString()!,
            row.GetProperty("IntentType").GetString(), row.GetProperty("IntentText").GetString(), row.GetProperty("CommandId").GetString()!,
            row.GetProperty("CommandBytes").GetString()!, receipt.GetProperty("Bytes").GetString()!, receipt.GetProperty("CommitSequence").GetUInt64(),
            receipt.GetProperty("Status").GetString()!, receipt.GetProperty("Rejection").GetString()!, scope.GetProperty("PrincipalId").GetString()!,
            scope.GetProperty("SessionId").GetString()!, scope.GetProperty("ActorId").GetString()!, scope.GetProperty("WorldId").GetString()!,
            scope.GetProperty("BranchId").GetString()!, scope.GetProperty("ExpectedActorRevision").GetUInt64());
    }).ToArray();

    private static WorldActionCommand Stored(IEnumerable<StoredWorldCommandOutcome> outcomes, CapturedSubmission row) =>
        WorldActionCodec.Decode(outcomes.Single(value => value.CommandId.ToString() == row.CommandId).Command);

    /// <summary>
    /// Every captured submission is the stored command outcome of the player's principal with exactly the captured command
    /// and receipt bytes; its decoded command carries the selected offer's scope and intent; its receipt names that command,
    /// its hash and the commit sequence; and the journal commit at that sequence contains exactly that command. Every stored
    /// command outcome of the player's principal after the prefix was captured, in order. Any difference is refused.
    /// </summary>
    private static void AuthenticateSubmissions(IReadOnlyList<CapturedSubmission> captured, IReadOnlyList<StoredWorldCommandOutcome> outcomes,
        IReadOnlyDictionary<ulong, WorldStepBatch> batches, EntityId principal)
    {
        StoredWorldCommandOutcome[] own = [.. outcomes.Where(row => row.PrincipalId == principal).OrderBy(row => row.Sequence)];
        Check(own.Select(row => row.CommandId.ToString()).SequenceEqual(captured.Select(row => row.CommandId)),
            "The player's journaled commands are not exactly the captured submissions.");
        for (int index = 0; index < own.Length; index++)
        {
            CapturedSubmission row = captured[index];
            StoredWorldCommandOutcome stored = own[index];
            Check(Convert.ToHexString(stored.Command) == row.CommandBytes, "Captured command bytes differ from the journal: " + row.CommandId);
            Check(Convert.ToHexString(stored.Receipt) == row.ReceiptBytes, "Captured receipt bytes differ from the journal: " + row.CommandId);
            WorldActionCommand command = WorldActionCodec.Decode(stored.Command);
            Check((command.CommandId.ToString(), command.PrincipalId.ToString(), command.OriginSessionId.ToString(), command.ActorId.ToString(),
                    command.WorldId.ToString(), command.BranchId.ToString(), command.ExpectedActorRevision)
                == (row.CommandId, row.PrincipalId, row.SessionId, row.ActorId, row.WorldId, row.BranchId, row.ExpectedActorRevision),
                "The journaled command is not the selected offer's scope: " + row.CommandId);
            Check(command.Intent.GetType().Name == row.IntentType && command.Intent.ToString() == row.IntentText,
                "The journaled command is not the selected offer's intent: " + row.CommandId);
            WorldActionReceipt receipt = WorldReceiptCodecV1.Decode(stored.Receipt);
            Check(receipt.CommandId == command.CommandId && receipt.PrincipalId == principal && receipt.CommandHash == command.ContentHash
                && receipt.CommitSequence == stored.Sequence && receipt.CommitSequence == row.CommitSequence
                && receipt.Status.ToString() == row.Status && receipt.Rejection.ToString() == row.Rejection,
                "The journaled receipt is not the captured receipt: " + row.CommandId);
            Check(batches.TryGetValue(stored.Sequence, out WorldStepBatch? batch)
                && batch.Actions.Count(action => action.CommandId == command.CommandId) == 1
                && WorldActionCodec.Encode(batch.Actions.Single(action => action.CommandId == command.CommandId)).AsSpan().SequenceEqual(stored.Command),
                "The journal commit at the receipt's sequence does not contain the captured command: " + row.CommandId);
        }
    }

    /// <summary>Counterexamples: one altered intent, command byte, receipt byte, identity or omission fails authentication.</summary>
    private static void AssertAlteredCapturesFail(CapturedSubmission[] captured, IReadOnlyList<StoredWorldCommandOutcome> outcomes,
        IReadOnlyDictionary<ulong, WorldStepBatch> batches, EntityId principal)
    {
        int reading = Array.FindIndex(captured, row => row.IntentType == nameof(AcknowledgeEncounterOutcomeIntent));
        CapturedSubmission row = captured[reading];
        CapturedSubmission other = captured[reading == 0 ? 1 : 0];
        CapturedSubmission[] alterations =
        [
            row with { IntentType = other.IntentType, IntentText = other.IntentText },
            row with { IntentText = row.IntentText + " " },
            row with { CommandBytes = FlipLastByte(row.CommandBytes) },
            row with { ReceiptBytes = FlipLastByte(row.ReceiptBytes) },
            row with { CommandId = other.CommandId },
            row with { ExpectedActorRevision = row.ExpectedActorRevision + 1 },
            row with { CommitSequence = row.CommitSequence + 1 }
        ];
        foreach (CapturedSubmission altered in alterations)
        {
            CapturedSubmission[] copy = [.. captured];
            copy[reading] = altered;
            Assert.Throws<InvalidDataException>(() => AuthenticateSubmissions(copy, outcomes, batches, principal));
        }
        Assert.Throws<InvalidDataException>(() => AuthenticateSubmissions([.. captured.Where((_, index) => index != reading)],
            outcomes, batches, principal));
    }

    /// <summary>The ordered pre-input selections are the captured submissions and the stored commands, byte for byte.</summary>
    private static void AuthenticateSelections(IReadOnlyList<(string CommandId, string ExpectedBytes)> selections,
        IReadOnlyList<CapturedSubmission> captured, IReadOnlyList<StoredWorldCommandOutcome> outcomes, EntityId principal)
    {
        Check(selections.Select(row => row.CommandId).SequenceEqual(captured.Select(row => row.CommandId)),
            "The pre-input selections are not the captured submissions in order.");
        for (int index = 0; index < selections.Count; index++)
        {
            StoredWorldCommandOutcome stored = outcomes.Single(row => row.PrincipalId == principal && row.CommandId.ToString() == selections[index].CommandId);
            Check(selections[index].ExpectedBytes == captured[index].CommandBytes && selections[index].ExpectedBytes == Convert.ToHexString(stored.Command),
                "The pre-input selection is not the submitted and journaled command: " + selections[index].CommandId);
        }
    }

    /// <summary>
    /// Counterexample on a nested selected fact, re-encoded canonically: the sale's accepted payment (victory) or the reading's
    /// expected archive hash. The actual command and receipt bytes are left intact; the selection must no longer authenticate.
    /// </summary>
    private static void AssertAlteredSelectionFails(IReadOnlyList<(string CommandId, string ExpectedBytes)> selections,
        IReadOnlyList<CapturedSubmission> captured, IReadOnlyList<StoredWorldCommandOutcome> outcomes, EntityId principal)
    {
        int index = Enumerable.Range(0, captured.Count).LastOrDefault(at => captured[at].IntentType == nameof(AcceptSpotQuoteIntent), -1);
        if (index < 0) index = Enumerable.Range(0, captured.Count).Single(at => captured[at].IntentType == nameof(AcknowledgeEncounterOutcomeIntent));
        WorldActionCommand selected = WorldActionCodec.Decode(Convert.FromHexString(selections[index].ExpectedBytes));
        WorldActionIntent altered = selected.Intent switch
        {
            AcceptSpotQuoteIntent accept => new AcceptSpotQuoteIntent(new OpenFantasy.Core.Markets.SpotAcceptance(accept.Acceptance.AcceptanceId,
                accept.Acceptance.PrincipalId, accept.Acceptance.OriginSessionId, accept.Acceptance.QuoteId, accept.Acceptance.QuoteHash,
                accept.Acceptance.Quantity, accept.Acceptance.Payment + 1, accept.Acceptance.Fees), accept.ExpectedIssuerRevision),
            AcknowledgeEncounterOutcomeIntent reading => reading with { ExpectedActionHash = OpenFantasy.Core.Content.ContentHash.Compute(new byte[] { 9, 9 }) },
            _ => throw new InvalidOperationException("No nested selected fact to alter.")
        };
        byte[] alteredBytes = WorldActionCodec.Encode(new WorldActionCommand(selected.CommandId, selected.PrincipalId, selected.OriginSessionId,
            selected.ActorId, selected.WorldId, selected.BranchId, selected.ExpectedActorRevision, altered));
        Assert.NotEqual(selections[index].ExpectedBytes, Convert.ToHexString(alteredBytes));
        (string CommandId, string ExpectedBytes)[] copy = [.. selections];
        copy[index] = (copy[index].CommandId, Convert.ToHexString(alteredBytes));
        Assert.Throws<InvalidDataException>(() => AuthenticateSelections(copy, captured, outcomes, principal));
    }

    private static string FlipLastByte(string hex)
    {
        byte[] bytes = Convert.FromHexString(hex);
        bytes[^1] ^= 0x01;
        return Convert.ToHexString(bytes);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    /// <summary>Re-derives the exact declared fresh genesis of a fixture from its recorded seed, name and calibration.</summary>
    private static byte[] Derive(JsonElement meta, string fixture)
    {
        string seed = meta.GetProperty("SeedText").GetString()!;
        string name = meta.GetProperty("PlayerName").GetString()!;
        Dictionary<string, int> wounded = meta.GetProperty("Calibration").GetProperty("WoundedHp").EnumerateObject()
            .ToDictionary(row => row.Name, row => row.Value.GetInt32());
        Assert.Empty(meta.GetProperty("Calibration").GetProperty("HumanControlled").EnumerateArray());
        WorldSnapshot derived;
        switch (fixture)
        {
            case "victory":
                KeyValuePair<string, int> chief = Assert.Single(wounded);
                // The calibration declares exactly HP 1 for the chief; the derived genesis must hold exactly that.
                Assert.Equal(1, chief.Value);
                Assert.Equal(meta.GetProperty("OpponentActorId").GetString(), chief.Key);
                derived = CargoOutcomeWorldAdmission.CreateWoundedChief(seed, name, chief.Value);
                Assert.Equal(1, derived.Progression!.GetActor(derived.Residents.Single(value => value.ActorId.ToString() == chief.Key).ActorId).CurrentHp);
                break;
            case "defeat":
                Assert.Empty(wounded);
                derived = CargoOutcomeWorldAdmission.CreateEmbedded(seed, name);
                break;
            default:
                WorldSnapshot threatened = RoadThreatWorldAdmission.CreateEmbedded(seed, name);
                EntityId player = threatened.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
                Assert.Equal(player.ToString(), Assert.Single(wounded).Key);
                Assert.Equal(1, Assert.Single(wounded).Value);
                derived = WorldEncounterOutcomeAdmission.CreateCalibratedFresh(threatened,
                    new EncounterOutcomeCalibration(new Dictionary<EntityId, int> { [player] = Assert.Single(wounded).Value }, []));
                Assert.Equal(1, derived.Progression!.GetActor(player).CurrentHp);
                break;
        }
        Assert.NotNull(CargoOutcomeWorldAdmission.Find(derived, CargoCircuitSources.LoadEmbedded(), RoadThreatSources.LoadEmbedded()));
        return WorldSnapshotCodec.Encode(derived);
    }
}
