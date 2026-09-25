using System.Text.Json;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

internal sealed class EncounterLifecycleEvidence
{
    private readonly string? _directory = Environment.GetEnvironmentVariable("OPENFANTASY_ENCOUNTER_LIFECYCLE_EVIDENCE");
    private readonly List<object> _states = [];
    private readonly List<object> _commands = [];
    private readonly List<object> _boundaries = [];

    internal EncounterLifecycleEvidence(WorldSnapshot genesis)
    {
        if (_directory is not null)
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllBytes(Path.Combine(_directory, "genesis.bin"), WorldSnapshotCodec.Encode(genesis));
        }
        Record(genesis, null, []);
    }

    internal void Record(WorldSnapshot state, WorldStepBatch? batch, IReadOnlyList<WorldActionReceipt> receipts)
    {
        if (_directory is null) return;
        string stateFile = $"state-{state.Revision}.bin";
        string? stepFile = batch is null ? null : $"step-{state.Revision}.bin";
        File.WriteAllBytes(Path.Combine(_directory, stateFile), WorldSnapshotCodec.Encode(state));
        if (batch is not null)
        {
            File.WriteAllBytes(Path.Combine(_directory, stepFile!), WorldStepCodec.Encode(batch));
            foreach (var receipt in receipts)
            {
                var command = batch.Actions.Single(value => value.CommandId == receipt.CommandId);
                _commands.Add(new
                {
                    CommandId = command.CommandId.ToString(), ActorId = command.ActorId.ToString(),
                    PrincipalId = command.PrincipalId.ToString(), SessionId = command.OriginSessionId.ToString(),
                    CommandHex = Convert.ToHexString(WorldActionCodec.Encode(command)).ToLowerInvariant(),
                    ReceiptHex = Convert.ToHexString(WorldReceiptCodecV1.Encode(receipt)).ToLowerInvariant(),
                    receipt.CommitSequence, Status = receipt.Status.ToString(), Rejection = receipt.Rejection.ToString()
                });
            }
        }
        var lifecycle = state.EncounterLifecycle!;
        var actions = lifecycle.Actions;
        var timing = state.Pacing!.Encounter;
        _states.Add(new
        {
            state.Revision, Hash = state.ContentHash.Value, StateFile = stateFile, StepFile = stepFile,
            Kind = batch is null ? 0 : (int)batch.Kind, state.Calendar, state.AdvancingSteps, state.ProcessIntervals,
            ExposureMilliseconds = state.Pacing.Progress.Elapsed.TotalMilliseconds.ToString(),
            state.Pacing.Progress.Remainder, Phase = lifecycle.Phase.ToString(), lifecycle.EncounterCount,
            lifecycle.ExplorationSteps, lifecycle.TacticalTicks, lifecycle.EntryTacticalTicks,
            ScenarioId = lifecycle.ScenarioId?.ToString(), EncounterId = actions?.Scope.EncounterId.ToString(),
            TimingRevision = timing?.Revision, ElapsedTacticalUnits = timing?.ElapsedTacticalUnits,
            LifecycleHash = lifecycle.ContentHash.Value, ActionHash = actions?.ContentHash.Value,
            RandomCounter = actions?.Random.NextCounter,
            Actors = state.Progression!.Actors.Select(actor => new
            {
                ActorId = actor.ActorId.ToString(), actor.Revision, actor.CurrentHp, actor.CurrentMp,
                actor.Level, actor.Experience, actor.Stats.Attack, actor.Stats.Defense, actor.Stats.Speed,
                RemainingDelay = lifecycle.GetDebt(actor.ActorId).RemainingDelay,
                Feet = state.Space.GetActor(actor.ActorId).Feet
            }).ToArray(),
            InventoryHash = state.Inventory.ContentHash.Value, ProductionHash = state.Production!.ContentHash.Value,
            ConstructionHash = state.Construction!.ContentHash.Value, ExcavationHash = state.Excavation!.ContentHash.Value,
            LiquidHash = state.LiquidCells!.ContentHash.Value,
            Resolution = actions?.LastResolution is not { } result ? null : new
            {
                result.WorldSequence, result.TimingRevision, ActorId = result.ActorId.ToString(),
                DecisionCommandId = result.DecisionCommandId.ToString(), TargetActorId = result.TargetActorId?.ToString(),
                AbilityDefinitionId = result.AbilityDefinitionId?.ToString(), Status = result.Status.ToString(),
                result.Damage, result.TargetHpBefore, result.TargetHpAfter, result.ManaSpent, result.Delay,
                result.VariationPermille, CancelledDecisionIds = result.CancelledDecisionIds.Select(value => value.ToString()).ToArray()
            }
        });
    }

    internal void PrepareBoundary(string label, string closedPath, WorldStepBatch step)
    {
        if (_directory is null) return;
        File.Copy(closedPath, Path.Combine(_directory, $"before-{label}.sqlite"), true);
        File.WriteAllBytes(Path.Combine(_directory, $"{label}-step.bin"), WorldStepCodec.Encode(step));
    }

    internal void CompleteBoundary(string label, WorldSnapshot before, WorldSnapshot after) => _boundaries.Add(new
    {
        Label = label, BeforeRevision = before.Revision, BeforeHash = before.ContentHash.Value,
        AfterRevision = after.Revision, AfterHash = after.ContentHash.Value,
        BeforeFile = $"before-{label}.sqlite", StepFile = $"{label}-step.bin"
    });

    internal void Complete(string closedPath, EncounterJourneyWorldFixture fixture, WorldSnapshot final)
    {
        if (_directory is null) return;
        File.Copy(closedPath, Path.Combine(_directory, "world.sqlite"), true);
        File.WriteAllText(Path.Combine(_directory, "expected.json"), JsonSerializer.Serialize(new
        {
            final.ProfileVersion, EncounterJourneyWorldFixture.SeedText, EncounterJourneyWorldFixture.PlayerName,
            WorldId = final.WorldId.ToString(), BranchId = final.BranchId.ToString(),
            Player = fixture.Player.ToString(), Wren = fixture.Wren.ToString(),
            WolfFirst = fixture.WolfFirst.ToString(), WolfSecond = fixture.WolfSecond.ToString(),
            FirstScenario = fixture.FirstScenario.ToString(), SecondScenario = fixture.SecondScenario.ToString(),
            Strike = fixture.Strike.ToString(), Bite = fixture.Bite.ToString(),
            final.Revision, Hash = final.ContentHash.Value, GenesisFile = "genesis.bin", WorldFile = "world.sqlite",
            States = _states, Commands = _commands, Boundaries = _boundaries,
            Qualification = "Complete source-backed first duel, release and immediate second episode retain debt and fractional carry. The second wolf receives one explicitly selected costed Wait to isolate recovery; this choice is not the automatic opponent policy. Separate post-release work continuation is managed-test evidence only."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
