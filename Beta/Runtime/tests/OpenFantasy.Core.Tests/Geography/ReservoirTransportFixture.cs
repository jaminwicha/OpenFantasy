using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Geography;

internal sealed class ReservoirTransportFixture
{
    internal static EntityId Id(ulong id) => new(0x6879647261756c69, id);
    internal static readonly EntityId Budget = Id(10), SecondBudget = Id(11), SourceStore = Id(12), Receiver = Id(13),
        Source = Id(14), Owner = Id(15), Gate = Id(16), SourceGeometry = Id(17), ReceiverGeometry = Id(18),
        HeldLot = Id(19), Stage = Id(20);
    internal static readonly CountedItemDefinition Water = new(Id(21), ContentHash.Compute("microlitre-water"u8));
    internal ReservoirTransportState State { get; }
    internal PreparedWatershedRunoff? Second { get; }
    internal WorldInstant Due => new(0, 1800);
    internal RunoffTransportRoute[] Routes => State.Reservoirs.Capacity.Pending[0].Request.Inputs
        .Select((i, index) => new RunoffTransportRoute(i.BasinId, index == 0 ? Gate : Id(25))).ToArray();

    internal ReservoirTransportFixture(ulong freeSource = 1000, ulong receiverUnits = 1000, bool splitDestinations = false,
        bool secondBudget = false, ulong maximumUnits = 1000)
    {
        var scope = new TerrainScope(Id(1), Id(2), Id(3));
        var basins = Enumerable.Range(0, splitDestinations ? 2 : 1).Select(i => new WatershedPlanner().Prepare(
            new(scope, Id(4), new(i * 2, 0, 1, 1), new(0, 0), 0, Id((ulong)(100 + i))))).OrderBy(b => b.BasinId).ToArray();
        var network = new WatershedNetworkPlanner().Prepare(Id(5), basins, basins.Select(b => WatershedOutletClosure.ToTerminal(b.BasinId, 1)));
        var material = new ItemPhysicalBinding(Water, new(new PackageId("openfantasy.hydraulics"), Id(6), "microlitre-water", new(1), new(1), new(0)));
        var storages = new List<CustodianStorage> { new(SourceStore, 100000, 0), new(Receiver, 100000, 0) };
        var lots = new List<CountedSourceLot> { new(HeldLot, SourceStore, Water, new(Source, 0, splitDestinations ? 2000UL : 1000UL), 0, LotProtection.None, Budget) };
        if (freeSource > 0) lots.Add(new(Id(22), SourceStore, Water, new(Source, 10000, 10000 + freeSource), 0));
        if (receiverUnits > 0) lots.Add(new(Id(23), Receiver, Water, new(Source, 30000, 30000 + receiverUnits), 0));
        ulong secondSourceStart = splitDestinations ? 2000UL : 1000UL;
        if (secondBudget) lots.Add(new(Id(24), SourceStore, Water, new(Source, secondSourceStart, secondSourceStart + 1000), 0, LotProtection.None, SecondBudget));
        if (splitDestinations) storages.Add(new(Id(26), 100000, 0));
        var inventory = new CountedAuthorityState(new(scope.WorldId, scope.BranchId, 0, storages, lots), 100000,
            [Owner], [new(Id(7), Id(8), Owner)], storages.Select(s => new CustodianAccessPolicy(s.CustodianId, Owner, CustodyDepositRule.OwnerOnly)),
            [new(Source, Water, 100000)], [Budget, SecondBudget]);
        var terminals = basins.Select((b, i) => new WatershedRunoffTerminal(b.BasinId, i == 0 ? Receiver : Id(26),
            new(i == 0 ? 2_000_000_000UL : 1_000_000_000UL))).ToArray();
        var request = new WatershedRunoffRequest(network, new(1, 1, 0), inventory, null, Budget, Owner, new(0, 0), new(0, 1), material,
            basins.Select((b, i) => new WatershedRunoffInput(b.BasinId, 1, new(Source, (ulong)i * 1000, (ulong)(i + 1) * 1000), SourceStore)), terminals);
        var capacity = WatershedRunoffCapacityState.Create(new WatershedRunoffPlanner().Prepare(request));
        if (secondBudget)
        {
            Second = new WatershedRunoffPlanner().Prepare(new(network, request.Mapping, inventory, null, SecondBudget, Owner,
                request.Start, request.End, material, basins.Select((basin, index) => index == 0
                    ? new WatershedRunoffInput(basin.BasinId, 1, new(Source, secondSourceStart, secondSourceStart + 1000), SourceStore)
                    : WatershedRunoffInput.Dry(basin.BasinId)), terminals));
            capacity = capacity.PlanAdmit(Second).ApplyTo(capacity);
        }
        var geometry = new List<ReservoirGeometry> { new(scope, SourceGeometry, SourceStore, 1, [new(0, 0, 0)]),
            new(scope, ReceiverGeometry, Receiver, 1, [new(1, 0, 0), new(1, 1, 0)]) };
        var gates = new List<ReservoirConnection> { new(Gate, SourceGeometry, ReceiverGeometry, 0, 0, 1, 0, 0, maximumUnits) };
        if (splitDestinations)
        {
            geometry.Add(new(scope, Id(27), Id(26), 1, [new(0, -1, 0)]));
            gates.Add(new(Id(25), SourceGeometry, Id(27), 0, 0, 0, -1, 0, maximumUnits));
        }
        State = ReservoirTransportState.Create(LiquidReservoirState.Create(capacity, geometry), gates, new(0, 0));
    }

    internal ReservoirTransportState Scheduled() => State.PlanSchedule(Stage, Budget, Due, Routes).ApplyTo(State);
}
