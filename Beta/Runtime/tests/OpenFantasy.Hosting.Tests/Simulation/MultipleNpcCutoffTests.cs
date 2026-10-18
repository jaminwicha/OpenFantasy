using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class MultipleNpcCutoffTests
{
    [Fact]
    public void Funded_two_host_builder_geometry_is_lawful_before_durable_runs()
    {
        var journey = Ready();
        WorldSnapshot world = journey.World;
        Assert.All(world.Residents, resident => Assert.Equal(ResidentControllerKind.Host, resident.ControllerKind));
        Assert.Equal(2, world.Quests!.Participations.Count);
        Assert.Equal(2, world.Construction!.Projects.Count);
        Assert.All(world.Construction.Projects, project =>
        {
            Assert.Equal(project.WorkFeet, world.Space.GetActor(project.BuilderActorId).Feet);
            Assert.Equal(0UL, project.Progress.CompletedUnits);
            Assert.Equal(200UL, project.Definition.RequiredWork.Value);
            var stock = Assert.Single(world.Inventory.Custody.Lots, lot => lot.ReservedBy == project.ReservationId);
            Assert.Equal(project.ProjectCustodianId, stock.CustodianId);
            Assert.Equal(6UL, stock.Quantity);
        });
        Assert.Equal(2, Select(journey).Count);
        Assert.Empty(new LocalNpcController().SelectMovement(world));
        Assert.All(journey.Batches.SelectMany(batch => batch.Movement), input =>
            Assert.Equal(32L, Math.Abs(input.DeltaX) + Math.Abs(input.DeltaY)));
    }

    [Fact]
    public void Both_selected_builders_work_at_each_shared_cutoff_with_one_exposure_increment()
    {
        var journey = Ready();
        WorldSnapshot supplied = journey.World;
        for (int step = 0; step < 8; step++)
        {
            WorldSnapshot before = journey.World;
            var commands = Select(journey);
            var batch = journey.Batch(commands.Reverse());
            var ordered = new WorldStepBatch(batch.CauseId, before.WorldId, before.BranchId,
                before.Revision, before.AdvancingSteps, batch.Kind, [], commands, before.ProfileVersion);
            Assert.Equal(WorldStepCodec.Encode(ordered), WorldStepCodec.Encode(batch));
            Assert.Equal(commands.Select(command => command.ActorId).Order(), batch.Actions.Select(command => command.ActorId));
            var decision = journey.Commit(batch);
            Assert.Equal(2, decision.Receipts.Count);
            Assert.All(decision.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
            AssertBoundary(before, journey.World, advancing: true);
            Assert.All(journey.World.Construction!.Projects, project =>
                Assert.Equal(before.Construction!.FindProject(project.ProjectId)!.Progress.CompletedUnits + 1,
                    project.Progress.CompletedUnits));
            Assert.Equal(before.Construction!.Revision + 2, journey.World.Construction.Revision);
            Assert.Equal(supplied.Inventory.ContentHash, journey.World.Inventory.ContentHash);
            Assert.Equal(supplied.Quests!.ContentHash, journey.World.Quests!.ContentHash);
            Assert.Equal(supplied.Progression!.ContentHash, journey.World.Progression!.ContentHash);
            Assert.Equal(supplied.Production!.ContentHash, journey.World.Production!.ContentHash);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Participation_exclusion_defers_only_that_builder_and_not_its_passive_exposure(bool excludeHelper)
    {
        var journey = Ready();
        var before = journey.World;
        EntityId excluded = excludeHelper ? journey.Helper : journey.Player;
        var commands = new LocalNpcController().SelectActions(before, journey.NextIdentity,
            new HashSet<EntityId> { excluded });
        Assert.NotEqual(excluded, Assert.Single(commands).ActorId);
        var decision = journey.Commit(journey.Batch(commands));
        Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
        AssertBoundary(before, journey.World, advancing: true);
        Assert.All(journey.World.Construction!.Projects, project =>
            Assert.Equal(project.BuilderActorId == excluded ? 0UL : 1UL, project.Progress.CompletedUnits));
        Assert.Equal(2, Select(journey).Count);
    }

    [Fact]
    public void Real_dialogue_holds_both_builders_and_release_earns_no_work_or_exposure()
    {
        var journey = Ready();
        var before = journey.World;
        var speak = journey.Command(journey.Player, new SpeakToActorIntent(journey.Helper,
            before.Space.GetActor(journey.Helper).Revision));
        Assert.Equal(WorldActionRejection.None, Assert.Single(journey.Commit(speak).Receipts).Rejection);
        AssertBoundary(before, journey.World, advancing: true);
        Assert.True(journey.World.HasReadingHold);
        int allocated = 0;
        Assert.Empty(new LocalNpcController().SelectActions(journey.World, () => { allocated++; return journey.NextIdentity(); }));
        Assert.Empty(new LocalNpcController().SelectMovement(journey.World));
        Assert.Equal(0, allocated);
        var held = journey.World;
        var close = journey.Command(journey.Player, new CloseDialogueIntent(Assert.Single(held.Dialogues).DialogueId));
        Assert.Equal(WorldActionRejection.None, Assert.Single(journey.Commit(close).Receipts).Rejection);
        AssertBoundary(held, journey.World, advancing: false);
        Assert.False(journey.World.HasReadingHold);
        Assert.All(journey.World.Construction!.Projects, project => Assert.Equal(0UL, project.Progress.CompletedUnits));
        Assert.Equal(2, Select(journey).Count);
    }

    [Fact]
    public void Ordinary_stale_work_refusal_does_not_drop_the_other_action_or_any_resident_exposure()
    {
        var journey = Ready();
        var before = journey.World;
        var commands = Select(journey).OrderBy(command => command.ActorId).ToArray();
        var original = commands[1];
        var intent = (WorkConstructionIntent)original.Intent;
        commands[1] = new WorldActionCommand(original.CommandId, original.PrincipalId, original.OriginSessionId,
            original.ActorId, original.WorldId, original.BranchId, original.ExpectedActorRevision,
            intent with { ExpectedProjectRevision = intent.ExpectedProjectRevision + 1 });
        var decision = journey.Commit(journey.Batch(commands));
        Assert.Equal(WorldActionRejection.None, decision.Receipts.Single(receipt => receipt.CommandId == commands[0].CommandId).Rejection);
        Assert.Equal(WorldActionRejection.RevisionConflict, decision.Receipts.Single(receipt => receipt.CommandId == commands[1].CommandId).Rejection);
        AssertBoundary(before, journey.World, advancing: true);
        Assert.Equal(1UL, journey.World.Construction!.FindProject(((WorkConstructionIntent)commands[0].Intent).ProjectId)!.Progress.CompletedUnits);
        Assert.Equal(0UL, journey.World.Construction.FindProject(intent.ProjectId)!.Progress.CompletedUnits);
    }

    [Theory]
    [InlineData(null)]
    [InlineData((int)AuthorityCommitBoundary.BeforeCommit)]
    [InlineData((int)AuthorityCommitBoundary.AfterCommit)]
    public void Original_funded_history_and_both_work_receipts_survive_atomic_recovery(int? faultValue)
    {
        AuthorityCommitBoundary? fault = faultValue is { } value ? (AuthorityCommitBoundary)value : null;
        var journey = Ready();
        var before = journey.World;
        var prefix = journey.Batches.ToArray();
        var commands = Select(journey);
        var batch = journey.Batch(commands);
        var expected = journey.Commit(batch);
        using var files = new CutoffFiles(fault?.ToString() ?? "ordinary");
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, journey.Genesis))
        {
            foreach (var prior in prefix) authority.Commit(prior);
            Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            if (fault is null)
            {
                var committed = authority.Commit(batch);
                Assert.Equal(WorldReceiptCodecV1.EncodeBatch(expected.Receipts), WorldReceiptCodecV1.EncodeBatch(committed.Receipts));
            }
            else
            {
                Assert.Throws<IOException>(() => authority.Commit(batch, at =>
                {
                    if (at == fault) throw new IOException("Multiple NPC cutoff publication fault");
                }));
                Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
            }
        }
        bool published = fault != AuthorityCommitBoundary.BeforeCommit;
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path);
        var recoveredWorld = recovered.ReadStateForHost();
        Assert.Equal(WorldSnapshotCodec.Encode(published ? expected.Candidate : before), WorldSnapshotCodec.Encode(recoveredWorld));
        Assert.Equal(WorldSnapshotCodec.Encode(journey.Genesis), WorldSnapshotCodec.Encode(recovered.ReadRecordedStateForHost(0).State));
        Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(recovered.ReadRecordedStateForHost(before.Revision).State));
        foreach (var command in commands)
        {
            var receipt = recovered.FindReceipt(command, ConstructionUpgradeFixture.Delivery(before, command.ActorId));
            if (!published) Assert.Null(receipt);
            else Assert.Equal(WorldReceiptCodecV1.Encode(expected.Receipts.Single(value => value.CommandId == command.CommandId)),
                WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(receipt)));
        }
        if (!published)
        {
            // An explicit game-level resubmission after a proved rollback, not a failed-test retry.
            var committed = recovered.Commit(batch);
            Assert.Equal(WorldSnapshotCodec.Encode(expected.Candidate), WorldSnapshotCodec.Encode(committed.Candidate));
        }
        Assert.Throws<AuthorityDispatchException>(() => recovered.Commit(batch));
        Assert.Equal(WorldSnapshotCodec.Encode(expected.Candidate), WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
        File.WriteAllBytes(System.IO.Path.Combine(files.Directory, "genesis.ofc"), WorldSnapshotCodec.Encode(journey.Genesis));
        File.WriteAllBytes(System.IO.Path.Combine(files.Directory, "before.ofc"), WorldSnapshotCodec.Encode(before));
        File.WriteAllBytes(System.IO.Path.Combine(files.Directory, "after.ofc"), WorldSnapshotCodec.Encode(expected.Candidate));
    }

    private static CoordinatedConstructionJourney Ready()
    {
        var journey = new CoordinatedConstructionJourney(coordinated: true, hostBuilders: true);
        journey.SupplyAndPositionBuilders();
        return journey;
    }

    private static IReadOnlyList<WorldActionCommand> Select(CoordinatedConstructionJourney journey)
    {
        var commands = new LocalNpcController().SelectActions(journey.World, journey.NextIdentity);
        Assert.Equal(2, commands.Count);
        return commands;
    }

    private static void AssertBoundary(WorldSnapshot before, WorldSnapshot after, bool advancing)
    {
        ulong intervals = advancing ? 1UL : 0UL;
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(before.AdvancingSteps + intervals, after.AdvancingSteps);
        Assert.Equal(before.Calendar.AddMilliseconds(advancing ? 1800 : 0), after.Calendar);
        Assert.Equal(before.ProcessIntervals + intervals, after.ProcessIntervals);
        Assert.Equal(after.ProcessIntervals, after.Gathering!.AdvancingStep);
        Assert.Equal(before.Residents.Select(resident => resident.ActorId), after.Residents.Select(resident => resident.ActorId));
        foreach (var resident in before.Residents)
            Assert.Equal(resident.ExperiencedTime.TotalMilliseconds + (advancing ? 1800UL : 0UL),
                after.GetResident(resident.ActorId).ExperiencedTime.TotalMilliseconds);
        Assert.Equal(after.Reservoirs!.Capacity.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(after.Reservoirs.ContentHash, after.LiquidCells!.ReservoirHash);
    }

    private sealed class CutoffFiles : IDisposable
    {
        private readonly bool _retained;

        internal CutoffFiles(string name)
        {
            string? root = Environment.GetEnvironmentVariable("OPENFANTASY_MULTIPLE_NPC_CUTOFF_ARTIFACTS");
            _retained = !string.IsNullOrWhiteSpace(root);
            Directory = _retained ? System.IO.Path.Combine(root!, name)
                : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "openfantasy-npc-cutoff-" + Guid.NewGuid().ToString("N"));
            Assert.False(System.IO.Directory.Exists(Directory), "Cutoff evidence must use a fresh case directory.");
            System.IO.Directory.CreateDirectory(Directory);
            Path = System.IO.Path.Combine(Directory, "world.sqlite3");
        }

        internal string Directory { get; }
        internal string Path { get; }
        public void Dispose()
        {
            if (!_retained) System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}
