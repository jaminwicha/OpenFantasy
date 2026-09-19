using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class RunoffCommandTests
{
    [Fact]
    public void CancellationIsDurableAndReleasesOnlyReceivingCapacity()
    {
        var fixture = new ReservoirWorldFixture();
        var genesis = fixture.Genesis;
        var owner = genesis.Reservoirs!;
        var action = fixture.Command(genesis, owner.Capacity.Domain.OwnerActorId,
            new CancelRunoffIntent(owner.ContentHash, owner.Capacity.Revision, ReservoirWorldFixture.Reservation));
        using var files = new StorageFiles();
        WorldSnapshot result;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            var decision = authority.Commit(fixture.Actions.Batch(genesis, [action], []));
            Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
            result = decision.Candidate;
            Assert.Empty(result.Reservoirs!.Capacity.Pending);
            Assert.Single(result.Reservoirs.Capacity.Closed);
            Assert.Equal(1UL, result.Reservoirs.Capacity.Revision);
            Assert.Equal(genesis.Inventory.ContentHash, result.Inventory.ContentHash);
            Assert.Equal(genesis.Production!.ContentHash, result.Production!.ContentHash);
            Assert.Equal(owner.Geometries.Select(g => g.ContentHash), result.Reservoirs.Geometries.Select(g => g.ContentHash));
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(result.ContentHash, reopened.InspectForHost().State.ContentHash);
        Assert.Equal(genesis.ContentHash, reopened.ReadRecordedStateForHost(0).StateHash);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RunoffCannotShareAnActiveOrClosedReservationWithQuestConstruction(bool closed)
    {
        var fixture = new ReservoirWorldFixture();
        var basis = fixture.Basis;
        EntityId questReservation = basis.Quests!.Definitions.Single().MaterialReservationId;
        var prior = basis.Inventory;
        var inventory = new CountedAuthorityState(new(prior.WorldId, prior.BranchId, 0, prior.Custody.Storages,
            prior.Custody.Lots.Select(l => l.ReservedBy == ReservoirWorldFixture.Reservation
                ? new CountedSourceLot(l.LotId, l.CustodianId, l.Definition, l.Source, l.Revision, l.Protection, questReservation) : l)),
            prior.AdmittedUnitCapacity, prior.Actors, prior.Controls, prior.Policies, prior.Sources, prior.ReservationIds);
        basis = new(inventory, basis.Space, basis.Residents, basis.AdmissionVersion, basis.Calendar, 0, 0,
            gathering: basis.Gathering, production: basis.Production, progression: basis.Progression,
            quests: basis.Quests, construction: basis.Construction);
        var request = fixture.Reservoirs.Capacity.Pending.Single().Request;
        var capacity = WatershedRunoffCapacityState.Create(new WatershedRunoffPlanner().Prepare(new(request.Network,
            request.Mapping, inventory, basis.Production, questReservation, request.OwnerActorId, request.Start,
            request.End, request.Material, request.Inputs, request.Terminals), TestContext.Current.CancellationToken));
        if (closed) capacity = capacity.PlanCancel(questReservation).ApplyTo(capacity);
        // Each domain is valid alone. Their competing reservation meanings must reject at world composition.
        var physical = LiquidReservoirState.Create(capacity, fixture.Reservoirs.Geometries);
        Assert.Throws<ArgumentException>(() => WorldReservoirAdmission.CreateGenesis(basis, physical));
    }
}
