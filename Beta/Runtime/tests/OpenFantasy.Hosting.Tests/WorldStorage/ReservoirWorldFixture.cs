using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

internal sealed class ReservoirWorldFixture
{
    internal static EntityId Id(ulong value) => new(0x776f726c64776174, value);
    internal static readonly EntityId SourceStore = Id(1), Receiver = Id(2), Source = Id(3), Reservation = Id(4);
    internal static readonly CountedItemDefinition Water = new(Id(5), ContentHash.Compute("world-water"u8));
    internal static readonly ItemPhysicalBinding Material = new(Water,
        new(new PackageId("openfantasy.water"), Id(6), "water", new(1_000_000), new(1_000_000), new(0)));
    internal static readonly ItemPhysicalBinding Ice = new(new(Id(7), ContentHash.Compute("world-ice"u8)),
        new(new PackageId("openfantasy.water"), Id(8), "ice", new(1_000_000), new(1_100_000), new(1)));
    internal static readonly EntityId SecondReservation = Id(14);
    internal ConstructionUpgradeFixture Actions { get; } = new();
    internal WorldSnapshot Basis { get; }
    internal LiquidReservoirState Reservoirs { get; }
    internal WorldSnapshot Genesis => WorldReservoirAdmission.CreateGenesis(Basis, Reservoirs);

    internal ReservoirWorldFixture(bool primaryReceiver = false, bool canceled = false, bool maximum = false, ulong incomingSupply = 0, string worldSeed = "construction-recovery", bool spareBudget = false)
    {
        WorldSnapshot farm = ConstructionFarmAdmission.CreateGenesis(Actions.QuestGenesis(worldSeed));
        var player = farm.Residents.Single(r => r.ControllerKind == ResidentControllerKind.Player);
        var host = farm.Residents.Single(r => r.ControllerKind == ResidentControllerKind.Host);
        var storages = farm.Inventory.Custody.Storages.Select(s => s.CustodianId == host.InventoryCustodianId
            ? s with { UnitCapacity = checked(s.UnitCapacity + incomingSupply) } : s);
        var lots = farm.Inventory.Custody.Lots.AsEnumerable();
        if (incomingSupply != 0)
        {
            lots = lots.Append(new(Id(13), host.InventoryCustodianId, Water, new(Source, 3000, 3000 + incomingSupply), 0));
        }
        if (spareBudget)
        {
            lots = lots.Append(new(Id(15), SourceStore, Water, new(Source, 1300, 1900), 0, LotProtection.None, SecondReservation));
        }
        var inventory = new CountedAuthorityState(new(farm.WorldId, farm.BranchId, 0,
            storages.Concat([new(SourceStore, 3000, 0), new(Receiver, 3000, 0)]),
            lots.Concat([
                new(Id(10), SourceStore, Water, new(Source, 0, 600), 0, LotProtection.None, Reservation),
                new(Id(11), SourceStore, Water, new(Source, 600, 1300), 0),
                new(Id(12), Receiver, Water, new(Source, 2000, 2050), 0)])),
            checked(farm.Inventory.AdmittedUnitCapacity + 6000 + incomingSupply), farm.Inventory.Actors, farm.Inventory.Controls,
            farm.Inventory.Policies.Concat([new(SourceStore, player.ActorId, CustodyDepositRule.OwnerOnly),
                new(Receiver, player.ActorId, CustodyDepositRule.AnyControlledActor)]),
            farm.Inventory.Sources.Append(new(Source, Water, checked(3000 + incomingSupply))), farm.Inventory.ReservationIds.Append(Reservation).Concat(spareBudget ? [SecondReservation] : []));
        var residents = farm.Residents.Select(r => primaryReceiver && r.ActorId == player.ActorId
            ? new WorldResident(r.ActorId, Receiver, r.DisplayName, r.ControllerKind, r.HomeFeet,
                r.WanderRadiusCells, r.Comprehension, r.ExperiencedTime, r.OpeningUtterance) : r);
        var production = new ProductionSnapshot(farm.WorldId, farm.BranchId, 0,
            farm.Production!.Recipes.Concat([Recipe(false), Recipe(true)]), [], []);
        Basis = new(inventory, farm.Space, residents, farm.AdmissionVersion, farm.Calendar, 0, 0,
            gathering: farm.Gathering, production: production, progression: farm.Progression,
            quests: farm.Quests, construction: farm.Construction);
        var basins = Enumerable.Range(0, maximum ? 16 : 1).Select(i => new WatershedPlanner().Prepare(
            new(Basis.Space.Scope, Id(20), maximum ? new(i % 4 * 64, i / 4 * 64, 64, 64) : new(0, 0, 2, 1),
                new(0, 0), 0, Id((ulong)(100 + i))), TestContext.Current.CancellationToken)).ToArray();
        var network = new WatershedNetworkPlanner().Prepare(Id(22), basins,
            basins.Select(b => WatershedOutletClosure.ToTerminal(b.BasinId, maximum ? 4096 : 2)), TestContext.Current.CancellationToken);
        // Maximum-network qualification uses an explicitly dry commitment; held raw stock must be absent from that budget.
        var inputs = basins.Select((b, i) => maximum ? WatershedRunoffInput.Dry(b.BasinId)
            : new WatershedRunoffInput(b.BasinId, 3000, new(Source, 0, 600), SourceStore));
        if (maximum)
        {
            inventory = new CountedAuthorityState(new(inventory.WorldId, inventory.BranchId, 0, inventory.Custody.Storages,
                inventory.Custody.Lots.Select(l => l.ReservedBy == Reservation ? new CountedSourceLot(l.LotId, l.CustodianId,
                    l.Definition, l.Source, l.Revision, l.Protection) : l)), inventory.AdmittedUnitCapacity, inventory.Actors,
                inventory.Controls, inventory.Policies, inventory.Sources, inventory.ReservationIds);
            Basis = new(inventory, Basis.Space, Basis.Residents, Basis.AdmissionVersion, Basis.Calendar, 0, 0,
                gathering: Basis.Gathering, production: Basis.Production, progression: Basis.Progression,
                quests: Basis.Quests, construction: Basis.Construction);
        }
        var receiverCapacity = new VolumeMicrolitres(maximum ? 4095UL * 2_000_000_000 : 2_000_000_000);
        var request = new WatershedRunoffRequest(network, new(10, 1, 0), inventory, Basis.Production,
            Reservation, player.ActorId, Basis.Calendar, Basis.Calendar.AddMilliseconds(1), Material,
            inputs, basins.Select(b => new WatershedRunoffTerminal(b.BasinId, Receiver, receiverCapacity)));
        var capacity = WatershedRunoffCapacityState.Create(new WatershedRunoffPlanner().Prepare(request, TestContext.Current.CancellationToken));
        if (canceled)
        {
            capacity = capacity.PlanCancel(Reservation).ApplyTo(capacity);
        }
        Reservoirs = LiquidReservoirState.Create(capacity,
            [new(Basis.Space.Scope, Id(30), SourceStore, 2, [new(0, 0, 0)]),
                new(Basis.Space.Scope, Id(31), Receiver, 2, Enumerable.Range(1, maximum ? 4095 : 1).Select(x => new ReservoirColumn(x, 0, 0)))]);
    }
    internal static CraftRecipeDefinition Recipe(bool freeze)
    {
        var output = freeze ? Ice : Material;
        var catalog = new MaterialCatalog(freeze ? [Material.Physical, Ice.Physical] : [Material.Physical]);
        var physical = new MaterialRecipeDefinition(Material.Physical.Package, Id(freeze ? 42UL : 40UL),
            freeze ? "freeze_water" : "filter_water", new(1), null, catalog,
            [new(RecipeMaterialRole.Input, new(Material.Physical.DefinitionId, Material.Physical.Version), 50),
                new(RecipeMaterialRole.Output, new(output.Physical.DefinitionId, output.Physical.Version), 50)]);
        return new(Id(freeze ? 43UL : 41UL), physical, freeze ? [Material, Ice] : [Material],
            [new(Water, 0, Material.Physical.MassPerUnit)]);
    }

    internal WorldActionCommand Command(WorldSnapshot state, EntityId actor, WorldActionIntent intent)
    {
        var delivery = ConstructionUpgradeFixture.Delivery(state, actor);
        return new(Actions.NextIdentity(), delivery.PrincipalId, delivery.SessionId, actor, state.WorldId,
            state.BranchId, state.Space.GetActor(actor).Revision, intent);
    }

    internal WorldActionCommand Craft(WorldSnapshot state, bool freeze = false)
    {
        var actor = state.Residents.Single(r => r.ControllerKind == ResidentControllerKind.Player).ActorId;
        var recipe = Recipe(freeze);
        return Command(state, actor, new CraftIntent(new(recipe.DefinitionId, recipe.Version, state.Production!.Revision,
            Receiver, state.Inventory.Custody.GetStorage(Receiver).Revision, [new(IngredientLotKind.Source, Id(12), 0, 50)])));
    }

}
