using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Construction.ConstructionWorldFixture;

namespace OpenFantasy.Core.Tests.Construction;

public sealed class ConstructionQualificationTests
{
    [Fact]
    public void Qualification_preserves_owners_and_clock_until_the_ordinary_step_publishes()
    {
        var world = Create();
        byte[] original = WorldSnapshotCodec.Encode(world);
        var result = Qualify(world);
        Assert.Equal(WorldActionRejection.None, result.Rejection);
        var work = Assert.IsType<QualifiedConstructionWork>(result.Work);
        Assert.Equal(1UL, work.Progress.CompletedUnits);
        Assert.Equal(0UL, work.Project.Progress.CompletedUnits);
        Assert.Equal(world.Inventory.Custody.GetLot(KitLot), work.Tool);
        Assert.Equal(6UL, work.ReservedStock.Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
        Assert.NotNull(work.Structure);
        Assert.Empty(world.Space.Structures);
        Assert.Equal(original, WorldSnapshotCodec.Encode(world));
        var actual = Step(world, 100);
        Assert.Equal(work.Progress.Capture(), actual.Candidate.Construction!.Projects.Single().Progress);
        Assert.Equal(WorldActionRejection.None, actual.Receipts.Single().Rejection);
        Assert.Empty(actual.Candidate.Space.Structures);
        Assert.Same(world.Inventory, actual.Candidate.Inventory);
        Assert.Same(world.Production, actual.Candidate.Production);
        Assert.True(actual.Candidate.Calendar > world.Calendar);
        Assert.All(actual.Candidate.Residents, resident => Assert.True(
            resident.ExperiencedTime.TotalMilliseconds > world.GetResident(resident.ActorId).ExperiencedTime.TotalMilliseconds));
    }

    [Fact]
    public void Complete_reservation_lookup_rejects_extra_stock_in_another_holder()
    {
        var world = Create();
        var project = world.Construction!.Projects.Single();
        var extra = world.Inventory.Custody.Lots.First(lot => lot.ReservedBy is null
            && lot.LotId != KitLot && lot.CustodianId != project.ProjectCustodianId);
        var lots = world.Inventory.Custody.Lots.Select(lot => lot.LotId == extra.LotId
            ? new CountedSourceLot(lot.LotId, lot.CustodianId, lot.Definition, lot.Source, lot.Revision,
                lot.Protection, project.ReservationId) : lot).ToArray();
        var custody = new CustodySnapshot(world.WorldId, world.BranchId, world.Inventory.Custody.Revision,
            world.Inventory.Custody.Storages, lots);
        var inventory = new CountedAuthorityState(custody, world.Inventory.AdmittedUnitCapacity,
            world.Inventory.Actors, world.Inventory.Controls, world.Inventory.Policies,
            world.Inventory.Sources, world.Inventory.ReservationIds);
        Assert.Equal(6UL, custody.Lots.Where(lot => lot.CustodianId == project.ProjectCustodianId)
            .Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
        Assert.Contains(custody.Lots, lot => lot.CustodianId != project.ProjectCustodianId
            && lot.ReservedBy == project.ReservationId);
        var result = Qualify(world, inventory: inventory);
        Assert.Equal(WorldActionRejection.Unavailable, result.Rejection);
        Assert.Null(result.Work);
        var plan = new ConstructionPlanner().Plan(world, inventory, world.Production!, world.Construction!,
            world.Space, Command(world), true, false);
        Assert.Equal(result.Rejection, plan.Rejection);
        Assert.Same(world.Construction, plan.CandidateConstruction);
    }

    [Fact]
    public void Partial_work_requires_every_access_actor_and_checks_placement()
    {
        var world = Create();
        var geometry = new ObservedGeometry(world.Space);
        var result = Qualify(world, geometry: geometry);
        var work = Assert.IsType<QualifiedConstructionWork>(result.Work);
        var project = world.Construction!.Projects.Single();
        Assert.True(project.RequiredAccess.Select(value => value.ActorId).Distinct().Count() > 1);
        Assert.Equal(project.RequiredAccess.Select(value => value.ActorId).Append(Builder).Distinct().Order(),
            geometry.ReadActors.Distinct().Order());
        Assert.Equal(1, geometry.PlacementCalls);
        Assert.Equal(work.RequiredAccess, geometry.Access);
        Assert.All(project.RequiredAccess, requirement => Assert.Contains(work.RequiredAccess,
            actual => actual.ActorId == requirement.ActorId && actual.From == world.Space.GetActor(requirement.ActorId).Feet
                && actual.To == requirement.To));
        Assert.False(work.Project.IsCompleted);
    }

    [Fact]
    public void Unavailable_placement_cannot_award_even_partial_work()
    {
        var world = Create();
        var geometry = new ObservedGeometry(world.Space, rejectPlacement: true);
        var result = Qualify(world, geometry: geometry);
        Assert.Equal(WorldActionRejection.Unavailable, result.Rejection);
        Assert.Null(result.Work);
        Assert.Equal(1, geometry.PlacementCalls);
        Assert.Equal(0UL, world.Construction!.Projects.Single().Progress.CompletedUnits);
    }

    [Theory]
    [InlineData(false, WorldActionRejection.RevisionConflict)]
    [InlineData(true, WorldActionRejection.Unavailable)]
    public void Reading_hold_precedes_stale_project_rejection(bool held, WorldActionRejection expected)
    {
        var world = Create(held: held);
        var intent = (WorkConstructionIntent)Command(world).Intent;
        var command = Command(world, intent: intent with { ExpectedProjectRevision = 99 });
        var result = Qualify(world, command: command);
        Assert.Equal(expected, result.Rejection);
        Assert.Null(result.Work);
    }

    private static ConstructionWorkQualification Qualify(WorldSnapshot world, CountedAuthorityState? inventory = null,
        IConstructionGeometry? geometry = null, WorldActionCommand? command = null) =>
        ConstructionWorkQualification.Evaluate(new LiveConstructionDecisionSource(world), inventory ?? world.Inventory,
            world.Production!, world.Construction!, geometry ?? new LiveConstructionGeometry(world.Space),
            command ?? Command(world), true, false);

    private sealed class ObservedGeometry(LocalSpatialSnapshot space, bool rejectPlacement = false) : IConstructionGeometry
    {
        private readonly LiveConstructionGeometry _inner = new(space);
        internal List<EntityId> ReadActors { get; } = [];
        internal GroundAccessRequirement[] Access { get; private set; } = [];
        internal int PlacementCalls { get; private set; }
        public TerrainScope Scope => _inner.Scope;
        public GroundSupportMode GroundSupport => _inner.GroundSupport;
        public ActorPresence GetActor(EntityId actorId)
        {
            ReadActors.Add(actorId);
            return _inner.GetActor(actorId);
        }
        public SpatialInteraction QueryPointInteraction(EntityId actorId, SubunitPosition target, long range) =>
            _inner.QueryPointInteraction(actorId, target, range);
        public StructurePlacementResult PrepareStructurePlacement(SpatialStructure proposed, IEnumerable<GroundAccessRequirement> access)
        {
            PlacementCalls++;
            Access = access.ToArray();
            return rejectPlacement ? new(StructurePlacementStatus.AccessBlocked) : _inner.PrepareStructurePlacement(proposed, Access);
        }
    }
}
