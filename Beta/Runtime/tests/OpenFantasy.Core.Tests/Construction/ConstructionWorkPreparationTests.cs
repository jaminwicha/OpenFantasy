using System.Runtime.CompilerServices;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Construction.ConstructionWorldFixture;

namespace OpenFantasy.Core.Tests.Construction;

public sealed class ConstructionWorkPreparationTests
{
    [Fact]
    public void Reused_site_evidence_preserves_all_200_paid_steps_and_actual_material_completion()
    {
        var world = Create();
        var site = ConstructionSiteEvidence.Capture(world, Command(world, 6000));
        var originalStock = world.Inventory.Custody.Lots.Where(value => value.ReservedBy is not null).ToArray();
        for (ulong step = 0; step < 200; step++)
        {
            var command = Command(world, 6000 + step);
            var preparation = ConstructionWorkPreparation.Capture(world, command, site, true, false);
            preparation.ValidateCurrent(world);
            var result = preparation.Evaluate();
            var repeated = preparation.Evaluate();
            Assert.True(result.Succeeded);
            Assert.Equal(result.CandidateConstruction.ContentHash, repeated.CandidateConstruction.ContentHash);
            Assert.Equal(result.CandidateProduction.ContentHash, repeated.CandidateProduction.ContentHash);
            var ordinary = Step(world, 6000 + step, command);
            Assert.Equal(WorldActionRejection.None, Assert.Single(ordinary.Receipts).Rejection);
            Assert.Equal(ordinary.Candidate.Inventory.ContentHash, result.CandidateInventory.ContentHash);
            Assert.Equal(ordinary.Candidate.Production!.ContentHash, result.CandidateProduction.ContentHash);
            Assert.Equal(ordinary.Candidate.Construction!.ContentHash, result.CandidateConstruction.ContentHash);
            Assert.Equal(ordinary.Identities.Where(value => value.Kind == WorldCommittedIdentityKind.SpawnedEntity)
                .Select(value => value.Identity).Order(), result.CreatedIds.Order());
            Assert.Throws<InvalidOperationException>(() => preparation.ValidateCurrent(ordinary.Candidate));
            world = ordinary.Candidate;
            if (step < 199)
            {
                Assert.Null(result.CandidateStructure);
                Assert.Equal(originalStock, result.CandidateInventory.Custody.Lots.Where(value => value.ReservedBy is not null));
                site.ValidateCurrent(world.Space);
            }
            else
            {
                Assert.Equal(Assert.Single(world.Space.Structures).StructureId, result.CandidateStructure!.StructureId);
                Assert.Throws<InvalidOperationException>(() => site.ValidateCurrent(world.Space));
            }
        }
        Assert.Equal(200UL, world.Construction!.Projects.Single().Progress.CompletedUnits);
        Assert.Equal(360_000UL, (ulong)world.Construction.Projects.Single().Progress.Elapsed.TotalMilliseconds);
        Assert.Single(world.Production!.Batches);
        Assert.Equal(1UL, Assert.Single(world.Production.Lots, lot => lot.ReservedBy is not null).Quantity);
        Assert.Equal(3UL, Assert.Single(world.Production.Lots, lot => lot.ReservedBy is null).Quantity);
        Assert.DoesNotContain(world.Inventory.Custody.Lots, value => value.ReservedBy is not null);
    }

    [Fact]
    public void Completion_candidate_executes_after_source_world_scene_index_and_terrain_are_collected()
    {
        var (preparation, references) = CaptureCompletionWithoutKeepingSource();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.All(references, reference => Assert.False(reference.IsAlive));
        var result = preparation.Evaluate();
        Assert.True(result.Succeeded);
        Assert.True(Assert.Single(result.CandidateConstruction.Projects).IsCompleted);
        Assert.NotNull(result.CandidateStructure);
        Assert.Single(result.CandidateProduction.Batches);
        Assert.Equal(4UL, result.CandidateProduction.Lots.Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
        Assert.DoesNotContain(result.CandidateInventory.Custody.Lots, value => value.ReservedBy is not null);
        GC.KeepAlive(preparation);
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    public void Holds_unfunded_quests_and_nonadvancing_selection_do_not_award_work(bool funded, bool readingHold, bool nonadvancing)
    {
        var world = Create(completed: funded, held: readingHold);
        var command = Command(world);
        var site = ConstructionSiteEvidence.Capture(world, command);
        var preparation = ConstructionWorkPreparation.Capture(world, command, site, !nonadvancing, false);
        var result = preparation.Evaluate();
        Assert.Equal(WorldActionRejection.Unavailable, result.Rejection);
        Assert.Equal(world.Construction!.ContentHash, result.CandidateConstruction.ContentHash);
        Assert.Equal(world.Inventory.ContentHash, result.CandidateInventory.ContentHash);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Exact_project_tool_and_storage_revisions_remain_required(int changed)
    {
        var world = Create();
        var original = Command(world);
        var intent = (WorkConstructionIntent)original.Intent;
        var command = Command(world, 7000, changed switch
        {
            0 => intent with { ExpectedProjectRevision = intent.ExpectedProjectRevision + 1 },
            1 => intent with { ExpectedToolLotRevision = intent.ExpectedToolLotRevision + 1 },
            _ => intent with { ExpectedProjectStorageRevision = intent.ExpectedProjectStorageRevision + 1 }
        });
        var result = ConstructionWorkPreparation.Capture(world, command,
            ConstructionSiteEvidence.Capture(world, command), true, false).Evaluate();
        Assert.Equal(WorldActionRejection.RevisionConflict, result.Rejection);
        Assert.Equal(world.Construction!.ContentHash, result.CandidateConstruction.ContentHash);
    }

    [Fact]
    public void Wrong_control_or_protected_tool_cannot_reuse_qualified_geometry_as_work_permission()
    {
        var world = Create();
        var command = Command(world);
        var site = ConstructionSiteEvidence.Capture(world, command);
        var foreign = new WorldActionCommand(command.CommandId, AdmissionFixture.Id(77777), command.OriginSessionId,
            command.ActorId, command.WorldId, command.BranchId, command.ExpectedActorRevision, command.Intent);
        Assert.Equal(WorldActionRejection.NotAuthorized,
            ConstructionWorkPreparation.Capture(world, foreign, site, true, false).Evaluate().Rejection);
        var lots = world.Inventory.Custody.Lots.Select(lot => lot.LotId == KitLot
            ? new CountedSourceLot(lot.LotId, lot.CustodianId, lot.Definition, lot.Source, lot.Revision, LotProtection.Favorite) : lot);
        var inventory = world.Inventory;
        var protectedInventory = new CountedAuthorityState(new CustodySnapshot(world.WorldId, world.BranchId,
            inventory.Custody.Revision, inventory.Custody.Storages, lots), inventory.AdmittedUnitCapacity,
            inventory.Actors, inventory.Controls, inventory.Policies, inventory.Sources, inventory.ReservationIds);
        var changed = Copy(world, inventory: protectedInventory);
        Assert.Equal(WorldActionRejection.Unavailable,
            ConstructionWorkPreparation.Capture(changed, command, site, true, false).Evaluate().Rejection);
    }

    [Fact]
    public void Complete_population_and_terrain_metadata_invalidate_old_site_evidence()
    {
        var world = Create();
        var site = ConstructionSiteEvidence.Capture(world, Command(world));
        var entries = world.Space.Actors.Select(SpatialOccupancyReference.Capture).ToArray();
        var directory = SpatialOccupancyDirectory.Create(world.Space.Scope, entries, 64, 128);
        var terrain = world.Space.Regions.Select(TerrainRegionReference.Capture).ToArray();
        site.ValidateCurrent(directory, terrain, world.Space.GroundSupport, world.Space.StructureSupport);
        var arrived = directory.Apply(0, [new(null, SpatialOccupancyReference.Capture(AdmissionFixture.Presence(2)))]);
        Assert.Throws<InvalidOperationException>(() => site.ValidateCurrent(arrived, terrain,
            world.Space.GroundSupport, world.Space.StructureSupport));
        var region = world.Space.Regions.Single();
        var excavated = region.Excavate(region.Revision, [new(5, 5, 0)]);
        Assert.Throws<InvalidOperationException>(() => site.ValidateCurrent(directory,
            [TerrainRegionReference.Capture(excavated)], world.Space.GroundSupport, world.Space.StructureSupport));
        Assert.Throws<InvalidOperationException>(() => site.ValidateCurrent(directory, terrain,
            world.Space.GroundSupport, StructureSupportMode.TerrainAndConstruction));
        var moved = world.Space.PrepareMove(Builder, world.Space.GetActor(Builder).Revision, 32, 0);
        var movedSpace = world.Space.WithActors(world.Space.Actors.Select(value => value.ActorId == Builder ? moved.Candidate! : value));
        Assert.Throws<InvalidOperationException>(() => site.ValidateCurrent(movedSpace));
    }

    [Fact]
    public void Final_history_collision_preserves_work_stock_and_geometry()
    {
        var world = BeforeCompletion();
        var command = Command(world, 8000);
        var preparation = ConstructionWorkPreparation.Capture(world, command,
            ConstructionSiteEvidence.Capture(world, command), true, false);
        var collision = EntityId.DeriveSpawned(command.CommandId, 0);
        var refused = preparation.Evaluate(id => id == collision);
        Assert.Equal(WorldActionRejection.Unavailable, refused.Rejection);
        Assert.Equal(world.Construction!.ContentHash, refused.CandidateConstruction.ContentHash);
        Assert.Equal(world.Inventory.ContentHash, refused.CandidateInventory.ContentHash);
        Assert.Null(refused.CandidateStructure);
    }

    [Fact]
    public void Final_identity_colliding_with_an_actual_place_is_not_lost_when_the_world_index_is_detached()
    {
        var world = BeforeCompletion();
        var command = Command(world, 8001);
        var collision = EntityId.DeriveSpawned(command.CommandId, 0);
        var other = world.Space.Actors.First(value => value.ActorId != Builder);
        var changed = new ActorPresence(other.Binding, other.Condition, other.Scope, other.Feet, other.Revision, collision);
        world = Copy(world, space: world.Space.WithActors(world.Space.Actors.Select(value => value.ActorId == other.ActorId ? changed : value)));
        Assert.True(world.IdentityIndex.Contains(collision));
        Assert.False(world.Inventory.ContainsIdentity(collision));
        Assert.False(world.Production!.ContainsIdentity(collision));
        var result = ConstructionWorkPreparation.Capture(world, command,
            ConstructionSiteEvidence.Capture(world, command), true, false).Evaluate();
        Assert.Equal(WorldActionRejection.Unavailable, result.Rejection);
        Assert.Null(result.CandidateStructure);
        Assert.Equal(world.Inventory.ContentHash, result.CandidateInventory.ContentHash);
    }

    private static WorldSnapshot BeforeCompletion()
    {
        var world = Create();
        for (ulong step = 0; step < 199; step++) world = Step(world, 9000 + step).Candidate;
        return world;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (ConstructionWorkPreparation Preparation, WeakReference[] References) CaptureCompletionWithoutKeepingSource()
    {
        var world = BeforeCompletion();
        var command = Command(world, 9300);
        var site = ConstructionSiteEvidence.Capture(world, command);
        WeakReference[] references = [new(world), new(world.Space), new(world.IdentityIndex),
            .. world.Space.Regions.Select(value => new WeakReference(value))];
        return (ConstructionWorkPreparation.Capture(world, command, site, true, false), references);
    }
}
