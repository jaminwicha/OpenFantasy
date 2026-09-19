using OpenFantasy.Core;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

internal sealed class ReservoirTransportWorldFixture
{
    internal ReservoirWorldFixture Materials { get; }
    internal WorldSnapshot Basis { get; }
    internal ReservoirTransportState Transport { get; }
    internal WorldSnapshot Genesis => WorldReservoirTransportAdmission.CreateGenesis(Basis, Transport);
    internal EntityId Player => Transport.Reservoirs.Capacity.Domain.OwnerActorId;
    internal EntityId Other => Basis.Residents.Single(r => r.ActorId != Player).ActorId;

    internal ReservoirTransportWorldFixture(bool nearby = true, ulong incomingSupply = 0, long sill = 0)
    {
        Materials = new ReservoirWorldFixture(primaryReceiver: true, incomingSupply: incomingSupply);
        var basis = Materials.Basis;
        var space = basis.Space;
        if (nearby)
        {
            // This is a fresh authored placement. A legal route provides the nearby feet positions;
            // no lived save or history is reset or replaced by this test genesis.
            var approached = RunoffCommandPolicyTests.ApproachOther(Materials, Materials.Genesis);
            space = space.WithActors(space.Actors.Select(a => new ActorPresence(a.Binding, a.Condition, a.Scope,
                approached.Space.GetActor(a.ActorId).Feet, 0, a.HomePlaceId)));
        }
        Basis = new(basis.Inventory, space, basis.Residents, basis.AdmissionVersion, basis.Calendar, 0, 0,
            gathering: basis.Gathering, production: basis.Production, progression: basis.Progression,
            quests: basis.Quests, construction: basis.Construction);
        Transport = ReservoirTransportState.Create(Materials.Reservoirs,
            [new(ReservoirTransportTests.Connection, ReservoirWorldFixture.Id(30), ReservoirWorldFixture.Id(31),
                0, 0, 1, 0, sill, 600)], Basis.Calendar);
    }

    internal ScheduleRunoffTransportIntent Schedule(WorldSnapshot state, int steps = 2) => new(state.Transport!.ContentHash,
        state.Transport.Revision, ReservoirWorldFixture.Reservation, state.Calendar.AddMilliseconds(steps * 1800),
        [new(state.Reservoirs!.Capacity.Domain.Network.Basins[0].BasinId, ReservoirTransportTests.Connection)]);

    internal WorldStepDecision Step(WorldSnapshot state, WorldActionIntent? intent = null, EntityId? actor = null) =>
        new WorldStepProcessor().Decide(state, Materials.Actions.Batch(state,
            intent is null ? [] : [Materials.Command(state, actor ?? Player, intent)], []));
}
