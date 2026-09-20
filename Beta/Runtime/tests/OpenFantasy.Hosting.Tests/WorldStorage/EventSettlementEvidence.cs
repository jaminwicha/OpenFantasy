using System.Globalization;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

/// <summary>Opt-in, closed authority artifacts for the independent ordinary Host continuation probe.</summary>
internal sealed class EventSettlementEvidence
{
    private readonly string _source;
    private readonly string? _directory;
    private readonly CoupledSettlementWorldFixture _fixture;

    internal EventSettlementEvidence(string source, CoupledSettlementWorldFixture fixture,
        WorldSnapshot before, WorldStepBatch trigger)
    {
        _source = source;
        _fixture = fixture;
        string? selected = Environment.GetEnvironmentVariable("OPENFANTASY_LIVE_EVENT_DESCENT_EVIDENCE");
        if (string.IsNullOrWhiteSpace(selected)) { return; }
        _directory = selected;
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(Path.Combine(_directory, "genesis.bin"), WorldSnapshotCodec.Encode(fixture.Genesis));
        File.WriteAllBytes(Path.Combine(_directory, "trigger.bin"), WorldStepCodec.Encode(trigger));
        Copy("before.sqlite");
        File.WriteAllText(Path.Combine(_directory, "started.json"), JsonSerializer.Serialize(new
        {
            before.ProfileVersion, before.Revision, Hash = before.ContentHash.Value,
            Scope = "Actual funded common-drop journey through the live event interpreter; completion is not yet asserted"
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    internal void Copy(string name)
    {
        if (_directory is not null) { File.Copy(_source, Path.Combine(_directory, name), overwrite: true); }
    }

    internal void Complete(WorldSnapshot before, WorldSnapshot after, WorldStepBatch trigger,
        IReadOnlyList<WorldStepBatch> continuations, WorldActionReceipt receipt, IReadOnlyList<WorldSnapshot> history,
        ConstructionDescentAssessment assessment)
    {
        if (_directory is null) { return; }
        Copy("after.sqlite");
        for (int index = 0; index < continuations.Count; index++)
        {
            File.WriteAllBytes(Path.Combine(_directory, $"continuation-{index + 1:D3}.bin"), WorldStepCodec.Encode(continuations[index]));
        }
        WorldSnapshot firstPending = history.First(state => state.PendingExcavation is not null);
        File.WriteAllText(Path.Combine(_directory, "expected.json"), JsonSerializer.Serialize(new
        {
            Scope = "Two genuinely funded assemblies and one rider descend 256 subunits through saved event-ordered work; this live fixture does not qualify unequal falls",
            after.ProfileVersion, DescentMode = after.DescentMode.ToString(),
            WorldId = after.WorldId.ToString(), BranchId = after.BranchId.ToString(),
            SiteId = _fixture.Site.SiteId.ToString(), SourceId = _fixture.Site.CountedSourceId.ToString(),
            TerrainSourceId = _fixture.Site.TerrainSourceId.ToString(), Player = _fixture.Player.ToString(), Builder = _fixture.Builder.ToString(),
            BeforeRevision = before.Revision, BeforeHash = before.ContentHash.Value,
            PendingRevision = firstPending.Revision, PendingHash = firstPending.ContentHash.Value,
            AfterRevision = after.Revision, AfterHash = after.ContentHash.Value,
            PreRevision = before.Revision, PreHash = before.ContentHash.Value,
            PostRevision = after.Revision, PostHash = after.ContentHash.Value,
            TriggerCauseId = trigger.CauseId.ToString(), CommandId = receipt.CommandId.ToString(),
            ReceiptHex = Convert.ToHexString(WorldReceiptCodecV1.Encode(receipt)), Continuations = continuations.Count,
            TriggerFile = "trigger.bin", BeforeFile = "before.sqlite", AfterFile = "after.sqlite",
            ContinuationFiles = Enumerable.Range(1, continuations.Count).Select(index => $"continuation-{index:D3}.bin"),
            PendingFiles = Enumerable.Range(0, continuations.Count).Select(index => $"pending-{index}.sqlite"),
            BeforeSteps = before.AdvancingSteps, AfterSteps = after.AdvancingSteps,
            SourceLotId = EntityId.DeriveSpawned(receipt.CommandId, 0).ToString(),
            LowerStructureId = after.Construction!.FindProject(CoupledSettlementWorldFixture.LowerProjectId)!.Installed!.StructureId.ToString(),
            UpperStructureId = after.Construction.FindProject(CoupledSettlementWorldFixture.UpperProjectId)!.Installed!.StructureId.ToString(),
            BeforeInventoryHash = before.Inventory.ContentHash.Value, AfterInventoryHash = after.Inventory.ContentHash.Value,
            BeforePhysicalHash = before.ConstructionPhysical!.ContentHash.Value, AfterPhysicalHash = after.ConstructionPhysical!.ContentHash.Value,
            PolicyHash = before.StructuralPolicy!.ContentHash.Value,
            SettlementPolicyHash = before.StructuralSettlementPolicy!.ContentHash.Value,
            CoupledPolicyHash = before.CoupledSettlementPolicy!.ContentHash.Value,
            ConsequencePolicyHash = before.ConsequencePolicy!.ContentHash.Value,
            AssessmentInputPin = assessment.InputPin.Value, assessment.CompletedWorkUnits, assessment.MaximumTotalWorkUnits,
            Events = assessment.Events.Select(value => new { value.DropSubunits,
                BodyIds = value.BodyIds.Select(identity => identity.ToString()) }),
            ImpactDemands = assessment.ImpactDemands.Select(demand => new
            {
                BodyId = demand.BodyId.ToString(), demand.IsActor, demand.MassMilligrams, demand.DropSubunits,
                EnergyNumeratorPicojoules = demand.EnergyNumeratorPicojoules.ToString(CultureInfo.InvariantCulture),
                StructuralImpactDemand.EnergyDenominator, demand.FlightMilliseconds, demand.IsHarmless
            }),
            Projects = after.Construction.Projects.Select(project => new { ProjectId = project.ProjectId.ToString(),
                StructureId = project.Installed!.StructureId.ToString(), InstalledLotId = project.Installed.GuardLotId.ToString(),
                BatchId = project.Installed.ProductionBatchId.ToString() }),
            Actors = after.Space.Actors.Select(actor => new { ActorId = actor.ActorId.ToString(), actor.Feet.X, actor.Feet.Y, actor.Feet.Z, actor.Revision }),
            History = history.DistinctBy(state => state.Revision).Select(state => new { state.Revision, Hash = state.ContentHash.Value })
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
