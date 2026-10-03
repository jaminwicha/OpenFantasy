using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.DelegatedRecovery;

internal sealed record DelegatedRecoveryBoundary(string Name, string BeforeDatabase, string AfterDatabase,
    WorldSnapshot Before, WorldSnapshot After, WorldStepBatch Batch);

/// <summary>
/// One real original-genesis journal shared by storage, process and native recovery proofs.
/// Finite endurance pools are declared at fresh admission; no lived root, position or counter is installed.
/// </summary>
public sealed partial class DelegatedRecoveryFixture : IDisposable
{
    private readonly List<DelegatedRecoveryBoundary> _boundaries = [];
    private ulong _identity = 10_000;

    public DelegatedRecoveryFixture()
    {
        string? retained = Environment.GetEnvironmentVariable("OPENFANTASY_DELEGATED_RECOVERY_EVIDENCE");
        DirectoryPath = Path.Combine(retained ?? Path.GetTempPath(), "delegated-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
        Retained = retained is not null;
        DatabasePath = Path.Combine(DirectoryPath, "world.sqlite3");
        Genesis = CreateGenesis();
        GenesisBytes = WorldSnapshotCodec.Encode(Genesis);
        File.WriteAllBytes(Path.Combine(DirectoryPath, "genesis.ofc"), GenesisBytes);
        Owner = Genesis.Residents.Single(row => row.ControllerKind == ResidentControllerKind.Player
            && row.ActorId == Genesis.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey).InitiatingActorId).ActorId;
        var scenario = Genesis.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey);
        ScenarioId = scenario.DefinitionId;
        Opponent = scenario.TargetActorId;
        Humans = Genesis.Controllers!.Assignments.Select(row => row.ActorId).ToArray();
        Grantee = Assert.Single(Humans, actor => actor != Owner);
        Prepare();
    }

    internal string DirectoryPath { get; }
    internal string DatabasePath { get; }
    private bool Retained { get; }
    internal WorldSnapshot Genesis { get; }
    internal byte[] GenesisBytes { get; }
    internal EntityId Owner { get; }
    internal EntityId Grantee { get; }
    internal EntityId Opponent { get; }
    internal EntityId ScenarioId { get; }
    internal EntityId[] Humans { get; }
    internal WorldControlWitness Witness { get; private set; } = null!;
    internal WorldActionCommand PaidCommand { get; private set; } = null!;
    internal WorldActionReceipt PaidReceipt { get; private set; } = null!;
    internal IReadOnlyList<DelegatedRecoveryBoundary> Boundaries => _boundaries;
    internal WorldSnapshot Final { get; private set; } = null!;

    internal void CloneBoundary(string name, string destination)
    {
        var boundary = Assert.Single(Boundaries, row => row.Name == name);
        File.Copy(boundary.BeforeDatabase, destination, overwrite: false);
    }

    internal TrustedCommandDelivery Delivery(EntityId actor)
    {
        var binding = Genesis.Inventory.Controls.Single(row => row.ActorId == actor);
        return new(binding.PrincipalId, binding.SessionId);
    }

    internal EntityId NextId() => new(0xde1e6a7e40000000, _identity++);

    internal WorldActionCommand Command(WorldSnapshot state, EntityId actor, WorldActionIntent intent,
        WorldControlWitness? witness = null)
    {
        var delivery = Delivery(witness?.GranteeActorId ?? actor);
        return new(NextId(), delivery.PrincipalId, delivery.SessionId, actor, state.WorldId, state.BranchId,
            state.Space.GetActor(actor).Revision, intent, witness);
    }

    internal WorldStepBatch Batch(WorldSnapshot state, WorldBatchKind kind,
        IEnumerable<WorldActionCommand>? actions = null) => new(NextId(), state.WorldId, state.BranchId,
            state.Revision, state.AdvancingSteps, kind, [], actions ?? [], state.ProfileVersion);

    internal static WorldStepDecision Commit(SqliteWorldAuthority authority, WorldStepBatch batch)
    {
        var result = authority.Commit(batch);
        Assert.All(result.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
        return result;
    }

    private static WorldSnapshot CreateGenesis()
    {
        var basis = RoadThreatWorldAdmission.CreateEmbedded("delegated-recovery-endurance", "Traveler");
        var progression = basis.Progression!;
        var profiles = progression.Profiles.Select(profile => new ActorStatProfileDefinition(profile.DefinitionId,
            profile.Key, profile.DisplayName, profile.Kind,
            new ActorStatValues(100_000, 4096, profile.BaseStats.Attack, profile.BaseStats.Magic,
                profile.BaseStats.Defense, 10_000), profile.Growth, profile.HeritageModifiers)).ToArray();
        var endurance = new ProgressionSnapshot(progression.WorldId, progression.BranchId, 0, profiles,
            progression.Curves, progression.Actors.Select(actor =>
            {
                var profile = profiles.Single(value => value.DefinitionId == actor.Profile.DefinitionId);
                var stats = profile.Calculate(actor.Level);
                return new ActorProgressionState(actor.ActorId, 0, profile, actor.Curve, actor.Level,
                    actor.Experience, stats.MaxHp, stats.MaxMp);
            }), progression.Currency);
        var admission = ContentHash.Compute(basis.ContentHash.ToByteArray().Concat(endurance.ContentHash.ToByteArray()).ToArray());
        var authored = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, endurance, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: basis.Care, resourceHistory: new WorldResourceHistory(admission, new ActorResourceHistory(endurance, 0, []), []),
            coordination: basis.Coordination, retreat: basis.Retreat);
        EntityId observer = authored.EncounterLifecycle!.Scenarios.Single(value =>
            value.Key == EncounterJourneyAdmission.SecondScenarioKey).TargetActorId;
        var outcome = WorldEncounterOutcomeAdmission.CreateCalibratedFresh(authored, new(new Dictionary<EntityId, int>(), [observer]));
        var controllers = WorldControllerAdmission.CreateFresh(outcome);
        var living = controllers.Care!.Ordinary!.Definitions.Single(value => value.DeliveryMode == RestorativeDeliveryMode.LivingAssistance);
        var recovery = RestorativeUseDefinition.CreateIncapacitatedAssistance(new(0xde1e6a7e40000000, 1),
            living.RemedyDefinitionId, living.RemedyVersion, living.SupportedForms);
        var world = WorldDelegatedControlAdmission.CreateFresh(WorldControlPermissionAdmission.CreateFresh(
            WorldCareDispositionAdmission.CreateFresh(WorldRecoveryAdmission.CreateFresh(controllers, [recovery], []))));
        Assert.Equal((ushort)40, world.ProfileVersion);
        Assert.Equal((0UL, 0UL), (world.Revision, world.AdvancingSteps));
        Assert.Empty(world.ResourceHistory!.History.Batches);
        return world;
    }

    public void Dispose()
    {
        if (!Retained && Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, recursive: true);
    }
}
