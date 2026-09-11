using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Client;

public sealed class FarmInventoryTests
{
    private static EntityId Wood(WorldSnapshot state) => state.Inventory.Sources.Single(source => source.SourceId == Id(71)).Definition.DefinitionId;
    private static LocalWorldView View(WorldSnapshot state) => new(state, Id(10));
    private static WorldSnapshot Apply(WorldSnapshot state, WorldActionCommand command)
    {
        WorldStepDecision result = new WorldStepProcessor().Decide(state, GatheringWorldFixture.Batch(state, command));
        Assert.Equal(CommandReceiptStatus.Applied, result.Receipts.Single().Status);
        return result.Candidate;
    }

    [Fact]
    public void InspectionGroupsActualOwnedLotsWithoutMutatingAuthorityAndRefreshesAfterDelivery()
    {
        WorldSnapshot state = GatheringWorldFixture.Create();
        state = Apply(state, GatheringWorldFixture.Swing(state));
        byte[] before = WorldSnapshotCodec.Encode(state);
        var pack = new FarmInventoryModel(Wood(state), null);
        pack.Refresh(View(state));
        FarmInventoryEntry wood = pack.Entries.Single(entry => entry.Name == "Wood");
        Assert.Equal(7UL, wood.Quantity);
        Assert.Equal(2, wood.LotCount);
        Assert.True(pack.Select(wood.Key));
        Assert.Equal(1U, pack.Entries.Single(entry => entry.Name == "Axe").ToolPower);
        Assert.Equal(before, WorldSnapshotCodec.Encode(state));
        state = Apply(state, Give(state));
        pack.Refresh(View(state));
        Assert.Equal(wood.Key, pack.SelectedKey);
        Assert.Equal(1UL, pack.Selected!.Quantity);
        Assert.Equal(6UL, new LocalWorldView(state, Id(11)).PlayerInventory.Quantity);
        Assert.Equal(2UL, pack.Entries.Aggregate(0UL, (sum, entry) => sum + entry.Quantity));
    }

    [Fact]
    public void ActorSwitchAndEmptyPackDoNotRetainAnotherActorsSelectionOrGoods()
    {
        WorldSnapshot state = GatheringWorldFixture.Create();
        var pack = new FarmInventoryModel(Wood(state), null);
        pack.Refresh(View(state));
        FarmInventoryKey axe = pack.Entries.Single(entry => entry.Name == "Axe").Key;
        Assert.True(pack.Select(axe));
        pack.Refresh(new LocalWorldView(state, Id(11)));
        Assert.Empty(pack.Entries);
        Assert.Null(pack.Selected);
        Assert.False(pack.Select(axe));
        state = Apply(state, Give(state));
        pack.Refresh(new LocalWorldView(state, Id(11)));
        Assert.Equal("Wood", Assert.Single(pack.Entries).Name);
        Assert.Equal(6UL, pack.Selected!.Quantity);
        pack.Refresh(View(state));
        Assert.Equal("Axe", Assert.Single(pack.Entries).Name);
    }

    [Fact]
    public void UnknownDefinitionsRemainInspectableWithoutInventedProperties()
    {
        WorldSnapshot state = GatheringWorldFixture.Create();
        var pack = new FarmInventoryModel(Id(123456), null);
        pack.Refresh(View(state));
        FarmInventoryEntry entry = pack.Entries.Single(item => item.ToolPower is null);
        Assert.Equal("Unfamiliar item", entry.Name);
        Assert.Equal(6UL, entry.Quantity);
        Assert.Contains("No further details are known", entry.Details);
    }

    [Fact]
    public void IdenticallyNamedSourceAndCraftedStockKeepSeparateGroupingAndExactTotals()
    {
        WorldSnapshot state = GatheringWorldFixture.Create();
        var wood = state.Inventory.Sources.Single(source => source.SourceId == Id(71)).Definition;
        var package = new PackageId("test.inventory-provenance");
        var material = new MaterialDefinition(package, Id(55001), "wood", new(10), new(10), new(1));
        var physical = new MaterialRecipeDefinition(package, Id(55002), "craft_rations", new(1), null,
            new MaterialCatalog([material]),
            [new(RecipeMaterialRole.Input, material.Pin(), 4), new(RecipeMaterialRole.Output, material.Pin(), 4)]);
        var recipe = new CraftRecipeDefinition(Id(55003), physical, [new(wood, material)], [new(wood, 0, new(10))]);
        var production = new ProductionSnapshot(state.WorldId, state.BranchId, 0, [recipe], [], []);
        var migration = new WorldProductionMigration(Id(55004), state.WorldId, state.BranchId,
            state.Revision, state.AdvancingSteps, state.ContentHash, state.AdmissionVersion,
            WorldProductionMigration.ComputeTargetAdmissionVersion(state.AdmissionVersion, production), production);
        state = new WorldProductionMigrationProcessor().Decide(state, migration).Candidate;
        var action = new WorldActionCommand(Id(55005), Delivery.PrincipalId, Delivery.SessionId, Id(10),
            state.WorldId, state.BranchId, state.Space.GetActor(Id(10)).Revision, new CraftIntent(FarmCrafting.RationsRequest(View(state))));
        state = Apply(state, action);
        byte[] before = WorldSnapshotCodec.Encode(state);
        var pack = new FarmInventoryModel(Wood(state), null);
        pack.Refresh(View(state));
        FarmInventoryEntry[] stocks = pack.Entries.Where(entry => entry.Name == "Wood").ToArray();
        Assert.Equal(2, stocks.Length);
        Assert.Equal(2UL, stocks.Single(entry => entry.Key.Provenance == IngredientLotKind.Source).Quantity);
        Assert.Equal(4UL, stocks.Single(entry => entry.Key.Provenance == IngredientLotKind.Transformed).Quantity);
        foreach (FarmInventoryEntry stock in stocks) Assert.True(pack.Select(stock.Key));
        Assert.Equal(before, WorldSnapshotCodec.Encode(state));
    }

    [Fact]
    public void ProtectedStockNeverAcquiresAnUnrestrictedDisplayGrouping()
    {
        WorldSnapshot state = GatheringWorldFixture.Create();
        CountedSourceLot old = state.Inventory.Custody.GetLot(Id(70));
        var lots = state.Inventory.Custody.Lots.Where(lot => lot.LotId != old.LotId).Concat(new[]
        {
            new CountedSourceLot(old.LotId, old.CustodianId, old.Definition, new SourceInterval(old.Source.SourceId, 0, 3), 0),
            new CountedSourceLot(Id(55020), old.CustodianId, old.Definition, new SourceInterval(old.Source.SourceId, 3, 6), 0, LotProtection.Favorite)
        });
        var custody = new CustodySnapshot(state.WorldId, state.BranchId, 0, state.Inventory.Custody.Storages, lots);
        var inventory = new OpenFantasy.Core.Authority.CountedAuthorityState(custody, state.Inventory.AdmittedUnitCapacity,
            state.Inventory.Actors, state.Inventory.Controls, state.Inventory.Policies, state.Inventory.Sources);
        state = new WorldSnapshot(inventory, state.Space, state.Residents, state.AdmissionVersion, state.Calendar, 0, 0, gathering: state.Gathering);
        var pack = new FarmInventoryModel(Wood(state), null);
        pack.Refresh(View(state));
        FarmInventoryEntry[] stocks = pack.Entries.Where(entry => entry.Name == "Wood").ToArray();
        Assert.Equal(2, stocks.Length);
        Assert.All(stocks, entry => Assert.Equal(3UL, entry.Quantity));
        Assert.Contains("cannot be delivered", stocks.Single(entry => !entry.Key.Deliverable).Details);
    }

    [Fact]
    public void InventoryUnwindsBeforePauseOrCraftAndPreservesExistingTimePolicy()
    {
        var world = new FarmInputSituation(true, false, false);
        foreach (bool paused in new[] { false, true })
        {
            var router = new FarmInputRouter(paused);
            Assert.True(router.Permits(world, FarmSemanticAction.OpenInventory));
            router.SetInventoryOpen(true);
            Assert.Equal(!paused, router.WorldShouldRun);
            Assert.False(router.Permits(world, FarmSemanticAction.TogglePause));
            foreach (string action in new[] { FarmInputActions.Pause, FarmInputActions.Cancel, FarmInputActions.Inventory })
            {
                Assert.Equal(FarmSemanticAction.CloseInventory, router.Route(world, FarmInputSignal.Press(action, FarmInputActions.Craft)).Action);
                router.Route(world, FarmInputSignal.Release(action, FarmInputActions.Craft));
            }
            foreach (FarmSemanticAction action in new[] { FarmSemanticAction.Speak, FarmSemanticAction.Give, FarmSemanticAction.Gather,
                FarmSemanticAction.Craft, FarmSemanticAction.NextTarget, FarmSemanticAction.ClearTarget })
                Assert.False(router.Permits(world, action));
            router.SetInventoryOpen(false);
            Assert.Equal(paused, router.Paused);
        }
        var blocked = new FarmInputRouter();
        Assert.False(blocked.Permits(world with { ReadingHold = true }, FarmSemanticAction.OpenInventory));
        Assert.False(blocked.Permits(world with { SessionOpen = false }, FarmSemanticAction.OpenInventory));
        Assert.False(blocked.Permits(world with { ActionPending = true }, FarmSemanticAction.OpenInventory));
        blocked.SetFocused(false);
        Assert.False(blocked.Permits(world, FarmSemanticAction.OpenInventory));
    }

    [Fact]
    public void ClosingBetweenPhysicsFramesDoesNotLeakHeldNavigationOrOpener()
    {
        var world = new FarmInputSituation(true, false, false);
        var router = new FarmInputRouter();
        Assert.Equal(FarmSemanticAction.OpenInventory, router.Route(world, FarmInputSignal.Press(FarmInputActions.Inventory)).Action);
        router.SetInventoryOpen(true);
        Assert.Equal(FarmSemanticAction.None, router.Route(world, FarmInputSignal.Repeat(FarmInputActions.Inventory)).Action);
        var held = new FarmDirectionalInput(1, 1, 0, 0);
        router.CaptureBoundary(world, held);
        router.SetInventoryOpen(false);
        Assert.True(router.Movement(world, held with { Left = 0 }).IsNeutral);
        Assert.Equal(FarmSemanticAction.None, router.Route(world, FarmInputSignal.Press(FarmInputActions.Inventory)).Action);
        router.Route(world, FarmInputSignal.Release(FarmInputActions.Inventory));
        Assert.Equal(FarmSemanticAction.OpenInventory, router.Route(world, FarmInputSignal.Press(FarmInputActions.Inventory)).Action);
        router.Movement(world, FarmDirectionalInput.Neutral);
        Assert.False(router.Movement(world, held with { Left = 0 }).IsNeutral);
    }

    [Fact]
    public async Task TimingStatusReportsActualReadinessReadingHoldAndShutdown()
    {
        using var files = new SessionFiles();
        WorldSnapshot state = Genesis();
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(SqliteWorldAuthority.CreateNew(files.Path, state), Id(10), Delivery, time);
        Assert.Equal(LocalWorldRunState.Paused, session.ReadRunState());
        session.SetReady(true);
        Assert.Equal(LocalWorldRunState.Running, session.ReadRunState());
        Task<WorldActionReceipt> opening = session.SubmitAction(Speak(state));
        await Tick(time);
        Assert.Equal(CommandReceiptStatus.Applied, (await opening).Status);
        Assert.Equal(LocalWorldRunState.Waiting, session.ReadRunState());
        session.SetReady(false);
        Assert.Equal(LocalWorldRunState.Paused, session.ReadRunState());
        session.SetReady(true);
        Assert.Equal(LocalWorldRunState.Waiting, session.ReadRunState());
        await session.StopAsync();
        Assert.Equal(LocalWorldRunState.Unavailable, session.ReadRunState());
    }
}
