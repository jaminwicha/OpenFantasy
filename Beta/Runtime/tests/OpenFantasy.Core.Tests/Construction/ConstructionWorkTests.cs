using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Construction.ConstructionWorldFixture;

namespace OpenFantasy.Core.Tests.Construction;

public sealed class ConstructionWorkTests
{
    [Fact]
    public void Real_npc_work_finishes_once_with_retired_wood_installed_guard_and_all_offcuts()
    {
        WorldSnapshot world = Create();
        var originalLots = world.Inventory.Custody.Lots.Where(value => value.ReservedBy is not null).ToArray();
        var originalProgression = world.Progression!.ContentHash;
        for (ulong index = 0; index < 200; index++)
        {
            WorldStepDecision step = Step(world, 100 + index);
            Assert.Equal(WorldActionRejection.None, step.Receipts.Single().Rejection);
            world = step.Candidate;
            if (index < 199)
            {
                Assert.Empty(world.Space.Structures);
                Assert.Empty(world.Production!.Batches);
                Assert.Equal(originalLots, world.Inventory.Custody.Lots.Where(value => value.ReservedBy is not null));
            }
        }
        var project = Assert.Single(world.Construction!.Projects);
        Assert.True(project.IsCompleted);
        Assert.Equal(200UL, project.Progress.CompletedUnits);
        Assert.Equal(360_000UL, (ulong)project.Progress.Elapsed.TotalMilliseconds);
        Assert.Single(world.Space.Structures);
        var batch = Assert.Single(world.Production!.Batches);
        Assert.Equal(6UL, batch.Ingredients.Aggregate(0UL, (sum, value) => sum + value.Quantity));
        Assert.Equal(4UL, world.Production.Lots.Aggregate(0UL, (sum, value) => sum + value.Quantity));
        Assert.Equal(270_000UL, project.Definition.MaterialTransform.PhysicalRecipe.OutputMass.Value);
        Assert.Equal(30_000UL, project.Definition.MaterialTransform.PhysicalRecipe.WasteMass.Value);
        Assert.Equal(originalProgression, world.Progression!.ContentHash);
        Assert.DoesNotContain(world.Inventory.Custody.Lots, value => value.ReservedBy == project.ReservationId);
        WorldSnapshot restored = WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(world));
        Assert.Equal(world.ContentHash, restored.ContentHash);
        WorldStepDecision repeated = Step(restored, 900);
        Assert.Equal(WorldActionRejection.Unavailable, repeated.Receipts.Single().Rejection);
        Assert.Equal(world.Production.ContentHash, repeated.Candidate.Production!.ContentHash);
        Assert.Equal(world.Construction.ContentHash, repeated.Candidate.Construction!.ContentHash);
    }

    [Fact]
    public void Hold_and_absent_supply_never_buy_work_even_with_a_contradictory_caller_flag()
    {
        WorldSnapshot held = Create(held: true);
        var planner = new ConstructionPlanner();
        ConstructionPlan result = planner.Plan(held, held.Inventory, held.Production!, held.Construction!, held.Space,
            Command(held), true, false);
        Assert.Equal(WorldActionRejection.Unavailable, result.Rejection);
        WorldStepDecision heldStep = Step(held, 101);
        Assert.Equal(held.Calendar, heldStep.Candidate.Calendar);
        Assert.Equal(held.Construction!.ContentHash, heldStep.Candidate.Construction!.ContentHash);
        WorldSnapshot unfunded = Create(completed: false);
        Assert.Equal(WorldActionRejection.Unavailable, Step(unfunded, 102).Receipts.Single().Rejection);
        Assert.Equal(0UL, unfunded.Construction!.Revision);
    }

    [Fact]
    public void Successful_work_suppresses_same_actor_movement_and_partial_progress_recovers()
    {
        WorldSnapshot world = Create();
        ActorControlBinding binding = world.Inventory.Controls.Single(value => value.ActorId == Builder);
        var movement = new WorldMovementInput(new TrustedCommandDelivery(binding.PrincipalId, binding.SessionId), Builder, 32, 0);
        WorldSnapshot worked = Step(world, 100, movement: [movement]).Candidate;
        Assert.Equal(world.Space.GetActor(Builder).Feet, worked.Space.GetActor(Builder).Feet);
        Assert.Equal(1UL, worked.Construction!.Revision);
        Assert.True(worked.Calendar > world.Calendar);
        WorldSnapshot restored = WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(worked));
        Assert.Equal(worked.ContentHash, restored.ContentHash);
        Assert.Equal(2UL, Step(restored, 101).Candidate.Construction!.Revision);
    }

    [Fact]
    public void Restored_work_requires_both_advancing_steps_and_the_builders_experienced_time()
    {
        WorldSnapshot original = Create();
        WorldSnapshot worked = Step(original, 100).Candidate;
        Assert.Throws<ArgumentException>(() => worked.Construction!.ValidateForWorld(original));
        var residents = worked.Residents.Select(value => value.ActorId == Builder
            ? new WorldResident(value.ActorId, value.InventoryCustodianId, value.DisplayName, value.ControllerKind,
                value.HomeFeet, value.WanderRadiusCells, value.Comprehension, LocalExposureDuration.Zero, value.OpeningUtterance)
            : value);
        Assert.Throws<ArgumentException>(() => new WorldSnapshot(worked.Inventory, worked.Space, residents,
            worked.AdmissionVersion, worked.Calendar, worked.Revision, worked.AdvancingSteps, worked.Dialogues,
            worked.Gathering, worked.Production, worked.Progression, worked.Quests, worked.Construction));
    }

    [Fact]
    public void Occupied_final_identity_preserves_earned_work_and_materials()
    {
        WorldSnapshot world = Create();
        for (ulong index = 0; index < 199; index++)
        {
            world = Step(world, 100 + index).Candidate;
        }
        WorldActionCommand command = Command(world, 500);
        EntityId occupied = EntityId.DeriveSpawned(command.CommandId, 3);
        WorldStepDecision rejected = Step(world, 500, command, occupied: id => id == occupied);
        Assert.Equal(WorldActionRejection.Unavailable, rejected.Receipts.Single().Rejection);
        Assert.Equal(world.Construction!.ContentHash, rejected.Candidate.Construction!.ContentHash);
        Assert.Equal(world.Inventory.ContentHash, rejected.Candidate.Inventory.ContentHash);
        Assert.Empty(rejected.Candidate.Space.Structures);
        Assert.Empty(rejected.Candidate.Production!.Batches);
    }

    [Fact]
    public void Moved_builder_stale_tokens_and_reserved_tool_leave_work_unchanged()
    {
        WorldSnapshot world = Create();
        WorkConstructionIntent original = (WorkConstructionIntent)Command(world).Intent;
        Assert.Equal(WorldActionRejection.RevisionConflict,
            Step(world, 101, Command(world, 101, original with { ExpectedProjectRevision = 8 })).Receipts.Single().Rejection);
        Assert.Equal(WorldActionRejection.RevisionConflict,
            Step(world, 102, Command(world, 102, original with { ExpectedToolLotRevision = 8 })).Receipts.Single().Rejection);
        var moved = world.Space.PrepareMove(Builder, world.Space.GetActor(Builder).Revision, 32, 0);
        var changed = Copy(world, space: world.Space.WithActors(world.Space.Actors.Select(value => value.ActorId == Builder ? moved.Candidate! : value)));
        Assert.Equal(WorldActionRejection.OutOfReach, Step(changed, 103).Receipts.Single().Rejection);
        var lots = world.Inventory.Custody.Lots.Select(lot => lot.LotId == KitLot
            ? new CountedSourceLot(lot.LotId, lot.CustodianId, lot.Definition, lot.Source, lot.Revision, LotProtection.Favorite) : lot);
        var custody = new CustodySnapshot(world.WorldId, world.BranchId, world.Inventory.Custody.Revision, world.Inventory.Custody.Storages, lots);
        var inventory = new CountedAuthorityState(custody, world.Inventory.AdmittedUnitCapacity, world.Inventory.Actors,
            world.Inventory.Controls, world.Inventory.Policies, world.Inventory.Sources, world.Inventory.ReservationIds);
        Assert.Equal(WorldActionRejection.Unavailable, Step(Copy(world, inventory: inventory), 104).Receipts.Single().Rejection);
        Assert.Equal(0UL, world.Construction!.Revision);
    }
}
