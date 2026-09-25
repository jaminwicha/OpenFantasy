using System.Text.Json;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

internal sealed class EncounterPacingEvidence
{
    private readonly string? _directory = Environment.GetEnvironmentVariable("OPENFANTASY_ENCOUNTER_PACING_EVIDENCE");
    private readonly List<object> _states = [];
    private readonly List<object> _commands = [];
    private WorldSnapshot? _beforeQuantum;
    private WorldSnapshot? _quantum;

    internal EncounterPacingEvidence(WorldSnapshot genesis)
    {
        if (_directory is not null) { Directory.CreateDirectory(_directory); }
        Record(genesis, null, []);
    }

    internal void Record(WorldSnapshot state, WorldStepBatch? batch, IReadOnlyList<WorldActionReceipt> receipts)
    {
        if (_directory is null) { return; }
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
                    ReceiptHex = Convert.ToHexString(WorldReceiptCodecV1.Encode(receipt)).ToLowerInvariant()
                });
            }
        }
        _states.Add(new
        {
            state.Revision, Hash = state.ContentHash.Value, StateFile = stateFile, StepFile = stepFile,
            Kind = batch is null ? 0 : (int)batch.Kind, state.Calendar, state.AdvancingSteps, state.ProcessIntervals,
            state.Pacing!.Encounter!.ElapsedTacticalUnits, TimingRevision = state.Pacing.Encounter.Revision,
            ExposureMilliseconds = state.Pacing.Progress.Elapsed.TotalMilliseconds.ToString(),
            LiquidHash = state.LiquidCells!.ContentHash.Value,
            Cells = state.LiquidCells.Quantities.OrderBy(pair => pair.Key).Select(pair => new
            {
                ReservoirId = pair.Key.ReservoirId.ToString(), pair.Key.Coordinate.X, pair.Key.Coordinate.Y,
                pair.Key.Coordinate.Z, Units = pair.Value
            }).ToArray(),
            Effort = state.Gathering!.Actors.Select(actor => new { ActorId = actor.ActorId.ToString(), actor.Effort }).ToArray()
        });
    }

    internal void BeforeQuantum(string closedPath, WorldSnapshot state)
    {
        _beforeQuantum = state;
        if (_directory is not null) { File.Copy(closedPath, Path.Combine(_directory, "before-quantum.sqlite"), true); }
    }

    internal void Quantum(WorldStepBatch batch, WorldSnapshot state)
    {
        _quantum = state;
        if (_directory is not null)
        {
            File.WriteAllBytes(Path.Combine(_directory, "quantum-step.bin"), WorldStepCodec.Encode(batch));
        }
    }

    internal void Complete(string closedPath, EncounterPacingWorldFixture fixture, WorldSnapshot final)
    {
        if (_directory is null) { return; }
        File.WriteAllBytes(Path.Combine(_directory, "basis.bin"), WorldSnapshotCodec.Encode(fixture.Basis));
        File.Copy(closedPath, Path.Combine(_directory, "world.sqlite"), true);
        File.WriteAllText(Path.Combine(_directory, "expected.json"), JsonSerializer.Serialize(new
        {
            final.ProfileVersion, WorldId = final.WorldId.ToString(), BranchId = final.BranchId.ToString(),
            EncounterId = final.Pacing!.Encounter!.EncounterId.ToString(), Player = fixture.Player.ToString(),
            final.Revision, Hash = final.ContentHash.Value,
            BeforeQuantumRevision = _beforeQuantum!.Revision, BeforeQuantumHash = _beforeQuantum.ContentHash.Value,
            QuantumRevision = _quantum!.Revision, QuantumHash = _quantum.ContentHash.Value,
            BeforeQuantumFile = "before-quantum.sqlite", QuantumStepFile = "quantum-step.bin",
            WorldFile = "world.sqlite", BasisFile = "basis.bin", States = _states, Commands = _commands,
            FixtureInterpretation = "Fresh conserved [5000,6000) water interval placed at (117,329,0); all actor effort starts970; no terrain mutation or completed work."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
