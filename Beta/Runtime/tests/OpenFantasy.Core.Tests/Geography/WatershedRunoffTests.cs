using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class WatershedRunoffTests
{
    private static EntityId Id(ulong value) => new(0x72756e6f6666, value);
    private static readonly EntityId Budget = Id(400), Owner = Id(401), Atmosphere = Id(402), Receiver = Id(403), Source = Id(404);
    private static readonly CountedItemDefinition Water = new(Id(405), ContentHash.Compute("runoff-water"u8));
    private static readonly ItemPhysicalBinding Material = new(Water, new(new PackageId("openfantasy.runoff"), Id(406), "water",
        new(1_000_000), new(1_000_000), new(0)));

    [Fact]
    public void UnequalRunoffRoutesThroughExactReceivingColumnsAndDryBasinWithoutMovingStock()
    {
        var request = Create();
        byte[] before = AuthorityEncodingV1.Encode(request.Inventory);
        var result = new WatershedRunoffPlanner().Prepare(request, TestContext.Current.CancellationToken);
        Assert.Equal(600UL, result.TotalInputUnits);
        Assert.Equal(600_000_000UL, result.TotalInputVolume.Value);
        Assert.Equal(100UL, result.ContributingUnits(request.Network.Basins.Single(b => b.Request.Bounds.MinimumX == -4).BasinId, 0));
        var middle = request.Network.Basins.Single(b => b.Request.Bounds.MinimumX == -2);
        Assert.Equal(400UL, result.ContributingUnits(middle.BasinId, 0));
        Assert.Equal(600UL, result.ContributingUnits(middle.BasinId, 1));
        Assert.Equal(600UL, Assert.Single(result.Terminals).IncomingUnits);
        Assert.Equal(650UL, Assert.Single(result.Receivers).ProjectedUnits);
        Assert.Equal(before, AuthorityEncodingV1.Encode(request.Inventory));
        byte[] bytes = WatershedRunoffCodecV1.Encode(result);
        Assert.Equal(bytes, WatershedRunoffCodecV1.Encode(WatershedRunoffCodecV1.Decode(bytes, TestContext.Current.CancellationToken)));
        Retain(result, "unequal-runoff");
    }

    [Fact]
    public void PhysicalHeadroomAndReservedFundingAreRequiredBeforePreparation()
    {
        var request = Create();
        var terminal = request.Terminals[0];
        var tooSmall = Copy(request, terminals: [new(terminal.BasinId, terminal.CustodianId, new(649_999_999))]);
        Assert.Throws<ArgumentException>(() => new WatershedRunoffPlanner().Prepare(tooSmall, TestContext.Current.CancellationToken));
        var inventory = Rebuild(request.Inventory, request.Inventory.Custody.Lots.Select(l =>
            new CountedSourceLot(l.LotId, l.CustodianId, l.Definition, l.Source, l.Revision)));
        Assert.Throws<ArgumentException>(() => new WatershedRunoffPlanner().Prepare(Copy(request, inventory: inventory), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SharedReceiversCountCompetingTerminalsTogetherAndRejectConflictingCapacity()
    {
        var request = Create();
        var network = new WatershedNetworkPlanner().Prepare(request.Network.ProvinceId, request.Network.Basins,
            request.Network.Basins.Select(b => WatershedOutletClosure.ToTerminal(b.BasinId, b.Columns.Count)), TestContext.Current.CancellationToken);
        var terminals = network.Basins.Select(b => new WatershedRunoffTerminal(b.BasinId, Receiver, new(650_000_000))).ToArray();
        request = new(network, request.Mapping, request.Inventory, null, Budget, Owner, request.Start, request.End, Material, request.Inputs, terminals);
        var result = new WatershedRunoffPlanner().Prepare(request, TestContext.Current.CancellationToken);
        Assert.Equal(3, result.Terminals.Count);
        Assert.Equal(650UL, Assert.Single(result.Receivers).ProjectedUnits);
        Assert.Throws<ArgumentException>(() => new WatershedRunoffPlanner().Prepare(Copy(request,
            terminals: terminals.Select(t => new WatershedRunoffTerminal(t.BasinId, Receiver, new(500_000_000)))), TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => new WatershedRunoffPlanner().Prepare(Copy(request,
            terminals: terminals.Select((t, i) => new WatershedRunoffTerminal(t.BasinId, Receiver, new((ulong)(650_000_000 + i))))), TestContext.Current.CancellationToken));
        Retain(result, "shared-receiver");
    }

    [Fact]
    public void FragmentedSourceFundingIsExactAndCannotHideExtraReservedStock()
    {
        var request = Create();
        var split = new[] { new SourceInterval(Source, 0, 73), new(Source, 73, 199), new(Source, 199, 601) };
        var lots = split.Select((range, i) => new CountedSourceLot(Id((ulong)(700 + i)), Atmosphere, Water, range, 0, LotProtection.None, Budget));
        Assert.Throws<ArgumentException>(() => new WatershedRunoffPlanner().Prepare(Copy(request,
            inventory: Rebuild(request.Inventory, lots)), TestContext.Current.CancellationToken));
        lots = split.Select((range, i) => new CountedSourceLot(Id((ulong)(700 + i)), Atmosphere, Water,
            new(Source, range.Start, Math.Min(600UL, range.End)), 0, LotProtection.None, Budget));
        var good = Copy(request, inventory: Rebuild(request.Inventory, lots));
        Assert.Equal(600UL, new WatershedRunoffPlanner().Prepare(good, TestContext.Current.CancellationToken).TotalInputUnits);
        var overlap = request.Inputs.Select(i => i.Source?.Start == 200 ? new WatershedRunoffInput(i.BasinId, i.DepthMicrometres,
            new(Source, 199, 599), Atmosphere) : i);
        Assert.Throws<ArgumentException>(() => new WatershedRunoffPlanner().Prepare(Copy(request, inputs: overlap), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void WrongFundingPinsProtectedUnitsAndIncompleteBasinContractsReject()
    {
        var request = Create();
        var wrongMaterial = new ItemPhysicalBinding(new(Water.DefinitionId, ContentHash.Compute("wrong"u8)), Material.Physical);
        Assert.Throws<ArgumentException>(() => new WatershedRunoffPlanner().Prepare(Copy(request, material: wrongMaterial), TestContext.Current.CancellationToken));
        var protectedLots = request.Inventory.Custody.Lots.Select(l => new CountedSourceLot(l.LotId, l.CustodianId, l.Definition,
            l.Source, l.Revision, LotProtection.Favorite, l.ReservedBy));
        Assert.Throws<ArgumentException>(() => new WatershedRunoffPlanner().Prepare(Copy(request,
            inventory: Rebuild(request.Inventory, protectedLots)), TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => new WatershedRunoffPlanner().Prepare(Copy(request, inputs: request.Inputs.Skip(1)), TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => new WatershedRunoffPlanner().Prepare(Copy(request, terminals: []), TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => new WatershedRunoffPlanner().Prepare(Copy(request,
            terminals: [new(request.Terminals[0].BasinId, Atmosphere, new(1_000_000_000))]), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ExactConversionAndCheckedVolumeRejectFractionalOrExcessiveForcing()
    {
        var request = Create();
        var wet = request.Inputs.Single(i => i.DepthMicrometres == 1000);
        var fractional = request.Inputs.Select(i => i == wet ? new WatershedRunoffInput(i.BasinId, 1, i.Source!, Atmosphere) : i);
        Assert.Throws<ArgumentException>(() => new WatershedRunoffPlanner().Prepare(Copy(request, inputs: fractional), TestContext.Current.CancellationToken));
        var excessive = request.Inputs.Select(i => i == wet ? new WatershedRunoffInput(i.BasinId, ulong.MaxValue - 5, i.Source!, Atmosphere) : i);
        // This multiple of ten converts exactly but exceeds a whole-basin funding interval.
        Assert.Throws<ArgumentException>(() => new WatershedRunoffPlanner().Prepare(Copy(request, inputs: excessive), TestContext.Current.CancellationToken));
        var huge = new MaterialDefinition(Material.Physical.Package, Material.Physical.DefinitionId, "heavy_water",
            new(ulong.MaxValue), Material.Physical.VolumePerUnit, Material.Physical.Hardness);
        Assert.Throws<OverflowException>(() => new WatershedRunoffPlanner().Prepare(Copy(request, material: new(Water, huge)), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ExplicitDryBudgetAndNegativeCalendarIntervalRetainZeroFlux()
    {
        var request = Create();
        var inventory = Rebuild(request.Inventory, request.Inventory.Custody.Lots.Where(l => l.ReservedBy is null));
        var dry = Copy(request, inventory: inventory, inputs: request.Inputs.Select(i => WatershedRunoffInput.Dry(i.BasinId)));
        var result = new WatershedRunoffPlanner().Prepare(dry, TestContext.Current.CancellationToken);
        Assert.Equal(0UL, result.TotalInputUnits);
        Assert.Equal(50UL, Assert.Single(result.Receivers).ProjectedUnits);
        Assert.True(result.Request.Start < new WorldInstant(0, 0));
        Assert.Throws<ArgumentException>(() => new WatershedRunoffRequest(request.Network, request.Mapping, inventory,
            null, Budget, Owner, request.End, request.Start, Material, dry.Inputs, dry.Terminals));
        Retain(result, "dry");
    }

    [Fact]
    public void CompleteBasisIncludesOccupancyProductionMappingAndForcingTime()
    {
        var request = Create();
        var result = new WatershedRunoffPlanner().Prepare(request, TestContext.Current.CancellationToken);
        result.EnsureCurrent(Copy(request));
        Assert.Throws<InvalidOperationException>(() => result.EnsureCurrent(Copy(request,
            inventory: Rebuild(request.Inventory, request.Inventory.Custody.Lots, 1))));
        var proof = new ProductionSnapshot(request.Inventory.WorldId, request.Inventory.BranchId, 0, [], [], []);
        Assert.Throws<InvalidOperationException>(() => result.EnsureCurrent(Copy(request, production: proof)));
        Assert.Throws<InvalidOperationException>(() => result.EnsureCurrent(new(request.Network, new(5, 1, 0), request.Inventory,
            null, Budget, Owner, request.Start, request.End, Material, request.Inputs, request.Terminals)));
        Assert.Throws<InvalidOperationException>(() => result.EnsureCurrent(new(request.Network, request.Mapping, request.Inventory,
            null, Budget, Owner, request.Start.AddMilliseconds(1), request.End, Material, request.Inputs, request.Terminals)));
    }

    [Fact]
    public void CraftedReceiverContentsConsumeRealPhysicalAndCountedHeadroom()
    {
        var request = Create();
        var physical = new MaterialRecipeDefinition(Material.Physical.Package, Id(800), "repack_water", new(1), null,
            new MaterialCatalog([Material.Physical]), [new(RecipeMaterialRole.Input, Material.Physical.Pin(), 50),
                new(RecipeMaterialRole.Output, Material.Physical.Pin(), 50)]);
        var recipe = new CraftRecipeDefinition(Id(801), physical, [Material], [new(Water, 0, Material.Physical.MassPerUnit)]);
        var inventory = new CountedAuthorityState(request.Inventory.Custody, 1000, [Owner], [new(Id(802), Id(803), Owner)],
            request.Inventory.Policies, request.Inventory.Sources, [Budget]);
        var production = new ProductionSnapshot(inventory.WorldId, inventory.BranchId, 0, [recipe], [], []);
        var craft = new InstantCraftPlanner().Plan(inventory, production, new(Id(802), Id(803)), Owner, Id(804),
            new(recipe.DefinitionId, recipe.Version, 0, Receiver, 0, [new(IngredientLotKind.Source, Id(501), 0, 50)]));
        Assert.Equal(OpenFantasy.Core.Worlds.WorldActionRejection.None, craft.Rejection);
        request = Copy(request, inventory: craft.CandidateInventory, production: craft.CandidateProduction);
        var result = new WatershedRunoffPlanner().Prepare(request, TestContext.Current.CancellationToken);
        Assert.Equal(50UL, Assert.Single(result.Receivers).ExistingUnits);
        Assert.Throws<ArgumentException>(() => new WatershedRunoffPlanner().Prepare(Copy(request,
            terminals: [new(request.Terminals[0].BasinId, Receiver, new(649_999_999))]), TestContext.Current.CancellationToken));
        var conflicting = new MaterialDefinition(Material.Physical.Package, Material.Physical.DefinitionId, "changed_water",
            new(1_000_001), Material.Physical.VolumePerUnit, Material.Physical.Hardness);
        Assert.Throws<ArgumentException>(() => new WatershedRunoffPlanner().Prepare(Copy(request,
            material: new(Water, conflicting)), TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => new WatershedRunoffPlanner().Prepare(Copy(request,
            inputs: request.Inputs.Select(i => i.DepthMicrometres == 1000
                ? new WatershedRunoffInput(i.BasinId, 1000, new(Source, 800, 1000), Atmosphere) : i)), TestContext.Current.CancellationToken));
        Retain(result, "crafted-occupancy");
    }

    [Fact]
    public void CanonicalOrderingAndWorkerCountCannotChangeBudgetResults()
    {
        var request = Create();
        byte[] expected = WatershedRunoffCodecV1.Encode(new WatershedRunoffPlanner().Prepare(request, TestContext.Current.CancellationToken));
        foreach (int workers in new[] { 1, 2, 4 })
            Parallel.For(0, 4, new ParallelOptions { MaxDegreeOfParallelism = workers, CancellationToken = TestContext.Current.CancellationToken }, _ =>
            {
                var reversed = Copy(request, inputs: request.Inputs.Reverse(), terminals: request.Terminals.Reverse());
                Assert.Equal(expected, WatershedRunoffCodecV1.Encode(new WatershedRunoffPlanner().Prepare(reversed, TestContext.Current.CancellationToken)));
            });
    }

    [Fact]
    public void CompleteMaximumNetworkHasFiniteSharedReceiverAndArchive()
    {
        var original = Create();
        var basins = Enumerable.Range(0, 16).Select(i => new WatershedPlanner().Prepare(new(original.Network.Scope, original.Network.Seed,
            new(i % 4 * 64, i / 4 * 64, 64, 64), new(0, 0), 0, Id((ulong)(1000 + i))), TestContext.Current.CancellationToken)).ToArray();
        var network = new WatershedNetworkPlanner().Prepare(Id(200), basins,
            basins.Select(b => WatershedOutletClosure.ToTerminal(b.BasinId, 4096)), TestContext.Current.CancellationToken);
        var inventory = new CountedAuthorityState(new(original.Inventory.WorldId, original.Inventory.BranchId, 0,
            [new(Atmosphere, 100000, 0), new(Receiver, 100000, 0)],
            [new(Id(500), Atmosphere, Water, new(Source, 0, 65536), 0, LotProtection.None, Budget),
                new(Id(501), Receiver, Water, new(Source, 90000, 90050), 0)]), 100000, [Owner], [], original.Inventory.Policies,
            [new(Source, Water, 100000)], [Budget]);
        var request = new WatershedRunoffRequest(network, new(1, 1, 0), inventory, null, Budget, Owner, original.Start, original.End,
            Material, basins.Select((b, i) => new WatershedRunoffInput(b.BasinId, 1000, new(Source, (ulong)i * 4096, (ulong)(i + 1) * 4096), Atmosphere)),
            basins.Select(b => new WatershedRunoffTerminal(b.BasinId, Receiver, new(100_000_000_000))));
        var result = new WatershedRunoffPlanner().Prepare(request, TestContext.Current.CancellationToken);
        Assert.Equal(65536UL, result.TotalInputUnits);
        Assert.Equal(65586UL, Assert.Single(result.Receivers).ProjectedUnits);
        byte[] bytes = WatershedRunoffCodecV1.Encode(result);
        Assert.True(bytes.Length < WatershedRunoffCodecV1.MaximumEncodedByteLength);
        Assert.Equal(result.ContentHash, WatershedRunoffCodecV1.Decode(bytes, TestContext.Current.CancellationToken).ContentHash);
        Retain(result, "maximum-network");
    }

    [Fact]
    public void DecoderRejectsChangedWitnessesTruncationOversizedInputAndCancellation()
    {
        var result = new WatershedRunoffPlanner().Prepare(Create(), TestContext.Current.CancellationToken);
        byte[] bytes = WatershedRunoffCodecV1.Encode(result);
        byte[] corrupt = (byte[])bytes.Clone(); corrupt[^1] ^= 1;
        foreach (byte[] bad in new[] { bytes[..^1], bytes.Concat(new byte[] { 0 }).ToArray(), corrupt,
            new byte[WatershedRunoffCodecV1.MaximumEncodedByteLength + 1] })
            Assert.Throws<FormatException>(() => WatershedRunoffCodecV1.Decode(bad, TestContext.Current.CancellationToken));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => WatershedRunoffCodecV1.Decode(bytes, cancellation.Token));
    }

    internal static WatershedRunoffRequest Create()
    {
        var scope = new TerrainScope(new(0, 1), new(0, 2), new(0, 3));
        var basins = Enumerable.Range(0, 3).Select(i => new WatershedPlanner().Prepare(new(scope, new(0, 4),
            new(-4 + i * 2, 0, 2, 1), new(20 - i * 10, 0), 1, Id((ulong)(100 + i))), TestContext.Current.CancellationToken)).ToArray();
        var network = new WatershedNetworkPlanner().Prepare(Id(200), basins,
            [WatershedOutletClosure.ToBasin(basins[0].BasinId, basins[1].BasinId, 0),
                WatershedOutletClosure.ToBasin(basins[1].BasinId, basins[2].BasinId, 0), WatershedOutletClosure.ToTerminal(basins[2].BasinId, 6)],
            TestContext.Current.CancellationToken);
        var inventory = new CountedAuthorityState(new(scope.WorldId, scope.BranchId, 0,
            [new(Atmosphere, 1000, 0), new(Receiver, 1000, 0)],
            [new(Id(500), Atmosphere, Water, new(Source, 0, 600), 0, LotProtection.None, Budget),
                new(Id(501), Receiver, Water, new(Source, 800, 850), 0)]), 1000, [Owner], [],
            [new(Atmosphere, Owner, CustodyDepositRule.OwnerOnly), new(Receiver, Owner, CustodyDepositRule.OwnerOnly)],
            [new(Source, Water, 1000)], [Budget]);
        return new(network, new(10, 1, 0), inventory, null, Budget, Owner, new(-10, 0), new(-9, 0), Material,
            [new(basins[0].BasinId, 1000, new(Source, 0, 200), Atmosphere),
                new(basins[1].BasinId, 2000, new(Source, 200, 600), Atmosphere), WatershedRunoffInput.Dry(basins[2].BasinId)],
            [new(basins[2].BasinId, Receiver, new(650_000_000))]);
    }

    private static WatershedRunoffRequest Copy(WatershedRunoffRequest basis, CountedAuthorityState? inventory = null,
        IEnumerable<WatershedRunoffInput>? inputs = null, IEnumerable<WatershedRunoffTerminal>? terminals = null,
        ProductionSnapshot? production = null, ItemPhysicalBinding? material = null) =>
        new(basis.Network, basis.Mapping, inventory ?? basis.Inventory, production ?? basis.Production, basis.ReservationId,
            basis.OwnerActorId, basis.Start, basis.End, material ?? basis.Material, inputs ?? basis.Inputs, terminals ?? basis.Terminals);

    private static CountedAuthorityState Rebuild(CountedAuthorityState basis, IEnumerable<CountedSourceLot> lots, ulong? revision = null) =>
        new(new(basis.WorldId, basis.BranchId, revision ?? basis.Custody.Revision, basis.Custody.Storages, lots),
            basis.AdmittedUnitCapacity, basis.Actors, basis.Controls, basis.Policies, basis.Sources, basis.ReservationIds);

    private static void Retain(PreparedWatershedRunoff state, string name)
    {
        string? path = Environment.GetEnvironmentVariable("OPENFANTASY_RUNOFF_EVIDENCE");
        if (string.IsNullOrWhiteSpace(path)) return;
        Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path, name + ".bin"), WatershedRunoffCodecV1.Encode(state));
    }
}
