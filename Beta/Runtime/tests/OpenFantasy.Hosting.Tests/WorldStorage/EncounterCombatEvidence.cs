using System.Text.Json;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

/// <summary>Optional complete inputs and closed-save witnesses for the independent Host oracle.</summary>
internal sealed class EncounterCombatEvidence
{
    private readonly string? _directory = Environment.GetEnvironmentVariable("OPENFANTASY_ENCOUNTER_ACTION_EVIDENCE");
    private readonly List<object> _states = [];
    private readonly List<object> _commands = [];
    private WorldSnapshot? _beforeStrike;
    private WorldSnapshot? _strike;
    private WorldSnapshot? _beforeLethal;
    private WorldSnapshot? _lethal;

    internal EncounterCombatEvidence(WorldSnapshot genesis)
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
            foreach (WorldActionReceipt receipt in receipts)
            {
                WorldActionCommand command = batch.Actions.Single(action => action.CommandId == receipt.CommandId);
                _commands.Add(new
                {
                    CommandId = command.CommandId.ToString(), ActorId = command.ActorId.ToString(),
                    PrincipalId = command.PrincipalId.ToString(), SessionId = command.OriginSessionId.ToString(),
                    CommandHex = Convert.ToHexString(WorldActionCodec.Encode(command)).ToLowerInvariant(),
                    ReceiptHex = Convert.ToHexString(WorldReceiptCodecV1.Encode(receipt)).ToLowerInvariant(),
                    receipt.CommitSequence
                });
            }
        }
        var actions = state.EncounterActions!;
        var timing = state.Pacing!.Encounter!;
        _states.Add(new
        {
            state.Revision, Hash = state.ContentHash.Value, StateFile = stateFile, StepFile = stepFile,
            Kind = batch is null ? 0 : (int)batch.Kind, state.Calendar, state.AdvancingSteps, state.ProcessIntervals,
            timing.ElapsedTacticalUnits, TimingRevision = timing.Revision,
            ExposureMilliseconds = state.Pacing.Progress.Elapsed.TotalMilliseconds.ToString(),
            ActionRevision = actions.Revision, ActionHash = actions.ContentHash.Value,
            RandomCounter = actions.Random.NextCounter, actions.Random.Exhausted,
            actors = state.Progression!.Actors.Select(actor => new
            {
                ActorId = actor.ActorId.ToString(), actor.Revision, actor.CurrentHp, actor.CurrentMp,
                actor.Level, actor.Experience, actor.Stats.Attack, actor.Stats.Defense, actor.Stats.Speed,
                RemainingDelay = timing.GetActor(actor.ActorId).RemainingDelay,
                NormalizedSpeed = timing.GetActor(actor.ActorId).NormalizedSpeed
            }).ToArray(),
            Pending = actions.Pending.Select(command => command.CommandId.ToString()).ToArray(),
            Decisions = timing.Decisions.Select(decision => new
            {
                ActorId = decision.ActorId.ToString(), DecisionId = decision.DecisionId.ToString(),
                Kind = (int)decision.Kind, ActionPin = decision.ActionPin.Value
            }).ToArray(),
            InventoryHash = state.Inventory.ContentHash.Value, ProductionHash = state.Production!.ContentHash.Value,
            ConstructionHash = state.Construction!.ContentHash.Value, ExcavationHash = state.Excavation!.ContentHash.Value,
            LiquidHash = state.LiquidCells!.ContentHash.Value,
            Effort = state.Gathering!.Actors.Select(actor => new { ActorId = actor.ActorId.ToString(), actor.Effort }).ToArray(),
            Resolution = actions.LastResolution is not { } result ? null : new
            {
                result.WorldSequence, result.TimingRevision, ActorId = result.ActorId.ToString(),
                DecisionCommandId = result.DecisionCommandId.ToString(), TargetActorId = result.TargetActorId?.ToString(),
                AbilityDefinitionId = result.AbilityDefinitionId?.ToString(), Status = result.Status.ToString(),
                result.Damage, result.TargetHpBefore, result.TargetHpAfter, result.ManaSpent,
                result.Delay, result.VariationPermille,
                CancelledDecisionIds = result.CancelledDecisionIds.Select(value => value.ToString()).ToArray()
            }
        });
    }

    internal void BeforeResolution(string closedPath, WorldSnapshot state, WorldStepBatch batch, bool first, bool lethal)
    {
        if (first) _beforeStrike = state;
        if (lethal) _beforeLethal = state;
        if (_directory is null) return;
        foreach (string prefix in (first ? new[] { "strike" } : []).Concat(lethal ? new[] { "lethal" } : []))
        {
            File.Copy(closedPath, Path.Combine(_directory, $"before-{prefix}.sqlite"), true);
            File.WriteAllBytes(Path.Combine(_directory, $"{prefix}-step.bin"), WorldStepCodec.Encode(batch));
        }
    }

    internal void AfterResolution(WorldSnapshot state, bool first, bool lethal)
    {
        if (first) _strike = state;
        if (lethal) _lethal = state;
    }

    internal void Complete(string closedPath, EncounterCombatWorldFixture fixture, WorldSnapshot final)
    {
        if (_directory is null) return;
        File.Copy(closedPath, Path.Combine(_directory, "world.sqlite"), true);
        File.WriteAllText(Path.Combine(_directory, "expected.json"), JsonSerializer.Serialize(new
        {
            final.ProfileVersion, EncounterCombatWorldFixture.SeedText, EncounterCombatWorldFixture.PlayerName,
            WorldId = final.WorldId.ToString(), BranchId = final.BranchId.ToString(),
            EncounterId = final.Pacing!.Encounter!.EncounterId.ToString(),
            Player = fixture.Player.ToString(), Wolf = fixture.Wolf.ToString(), Wren = fixture.Wren.ToString(),
            Strike = fixture.Strike.ToString(), Bite = fixture.Bite.ToString(),
            RngSeed = final.EncounterActions!.Seed.ToString(), RulesHash = final.EncounterActions.RulesVersion.Value,
            final.Revision, Hash = final.ContentHash.Value,
            BeforeStrikeRevision = _beforeStrike!.Revision, BeforeStrikeHash = _beforeStrike.ContentHash.Value,
            FirstStrikeRevision = _strike!.Revision, FirstStrikeHash = _strike.ContentHash.Value,
            BeforeLethalRevision = _beforeLethal!.Revision, BeforeLethalHash = _beforeLethal.ContentHash.Value,
            LethalRevision = _lethal!.Revision, LethalHash = _lethal.ContentHash.Value,
            WorldFile = "world.sqlite", GenesisFile = "genesis.bin",
            BeforeStrikeFile = "before-strike.sqlite", StrikeStepFile = "strike-step.bin",
            BeforeLethalFile = "before-lethal.sqlite", LethalStepFile = "lethal-step.bin",
            States = _states, Commands = _commands,
            Qualification = "Fresh source-admitted Squire/wolf confrontation; HP0 is incapacitation only. No death, reward, drop, respawn or native combat claim."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
