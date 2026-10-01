using System.Globalization;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;

namespace OpenFantasy.Hosting.Tests.Content.Cargo.Refinement;

/// <summary>
/// Fresh-genesis determinism, canonical admission framing, refused source/admission inputs, the typed opening-stock
/// binding and the retained water owners' equivalence after the cargo extension.
/// </summary>
public sealed class CargoAdmissionRefinementTests
{
    private const string Seed = "cargo-refinement";

    [Fact]
    public void Fresh_genesis_is_byte_deterministic_for_one_seed_and_differs_for_another()
    {
        byte[] first = WorldSnapshotCodec.Encode(CargoWorldAdmission.CreateEmbedded(Seed, "Traveler"));
        byte[] second = WorldSnapshotCodec.Encode(CargoWorldAdmission.CreateEmbedded(Seed, "Traveler"));
        byte[] other = WorldSnapshotCodec.Encode(CargoWorldAdmission.CreateEmbedded(Seed + ".other", "Traveler"));
        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void The_admission_version_is_framed_canonically_with_explicit_signed_coordinates()
    {
        CargoCircuitSources sources = CargoCircuitSources.LoadEmbedded();
        ContentHash basis = ContentHash.Compute([1, 2, 3]);
        TerrainRegionBounds[] negative = [new(new CellCoordinate(-64, -32, -1), 32, 32, 16)];
        (CargoSourceMerchant, CargoMerchantProvision)[] merchants =
            [(sources.Origin, CargoCircuitPolicy.BrasshollowProvision), (sources.Destination, CargoCircuitPolicy.GearfordProvision)];
        ContentHash Version() => CargoGenesisExtension.AdmissionVersion(basis, negative, merchants, sources.SharedGood,
            CargoCorridorOverlays.Version);
        CultureInfo original = CultureInfo.CurrentCulture;
        ContentHash invariant;
        ContentHash minusSign;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            invariant = Version();
            // A culture writing negatives with U+2212 would change a text-built hash; a framed one cannot change.
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NegativeSign = "−";
            CultureInfo.CurrentCulture = culture;
            Assert.NotEqual("-64", (-64L).ToString(CultureInfo.CurrentCulture));
            minusSign = Version();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
        Assert.Equal(invariant, minusSign);
        byte[] frame = CargoCanonicalFrame.Frame("t", [CargoCanonicalFrame.Signed64(-1)]);
        Assert.Equal(new byte[] { 0x4f, 0x46, 0x43, 0, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 1, 0x74, 0, 0, 0, 8,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff }, frame);
        TerrainRegionBounds[] shifted = [new(new CellCoordinate(-64, -31, -1), 32, 32, 16)];
        Assert.NotEqual(invariant, CargoGenesisExtension.AdmissionVersion(basis, shifted, merchants, sources.SharedGood,
            CargoCorridorOverlays.Version));
    }

    [Fact]
    public void A_duplicated_identity_or_a_shop_without_the_shared_good_is_refused()
    {
        using (CargoSourceDocuments duplicated = CargoSourceDocuments.Retained(npcs: text =>
            text.Replace("\"id\": \"kegga\"", "\"id\": \"marla\"")))
        {
            Assert.Contains("duplicated", Assert.Throws<InvalidDataException>(duplicated.Bind).Message);
        }
        using (CargoSourceDocuments place = CargoSourceDocuments.Retained(world: text =>
            text.Replace("{\"id\":\"tavern_gearford\"", "{\"id\":\"gearford\"")))
        {
            Assert.Contains("duplicated", Assert.Throws<InvalidDataException>(place.Bind).Message);
        }
        using (CargoSourceDocuments shop = CargoSourceDocuments.Retained(npcs: text =>
            text.Replace("\"ether\",\n    \"rations\",", "\"ether\",\n    \"bread\",")))
        {
            Assert.Contains("does not offer", Assert.Throws<InvalidDataException>(shop.Bind).Message);
        }
    }

    [Fact]
    public void A_provision_for_another_merchant_another_profile_root_or_an_oversized_corridor_is_refused()
    {
        CargoCircuitSources sources = CargoCircuitSources.LoadEmbedded();
        WorldSnapshot farm = RestorativeJourneyAdmission.CreateEmbedded(Seed, "Traveler");
        Assert.Contains("different retained merchant", Assert.Throws<InvalidDataException>(() => new CargoGenesisExtension(farm,
            sources, CargoCircuitPolicy.GearfordProvision, CargoCircuitPolicy.GearfordProvision).Build()).Message);
        // Another interpretation (the encounter farm) and an already admitted cargo world are not ordinary-care roots.
        Assert.Throws<ArgumentException>(() => CargoWorldAdmission.Compose(EncounterJourneyAdmission.CreateEmbedded(Seed, "Traveler"), sources));
        WorldSnapshot cargo = CargoWorldAdmission.Compose(farm, sources);
        Assert.Throws<ArgumentException>(() => CargoWorldAdmission.Compose(cargo, sources));
        // Corridor terrain cannot overlap retained regions or exceed the fixed sixteen-region bound.
        TerrainRegionBounds basis = farm.Space.Regions.Single(region => region.Bounds.Contains(
            farm.Space.GetActor(farm.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId).Feet.Cell)).Bounds;
        CargoCorridor corridor = CargoCorridor.CreateCircuit(basis, sources);
        TerrainRegionSnapshot basisRegion = farm.Space.Regions.Single(region => region.Bounds == basis);
        TerrainRegionSnapshot firstAdded = cargo.Space.Regions.Single(region => region.Bounds == corridor.AddedRegions[0]);
        Assert.Contains("overlap", Assert.Throws<InvalidDataException>(() => corridor.GenerateTerrain([basisRegion, firstAdded])).Message);
        Assert.Contains("bound", Assert.Throws<InvalidDataException>(() => corridor.GenerateTerrain(cargo.Space.Regions)).Message);
    }

    [Fact]
    public void The_opening_shelf_is_the_named_raw_source_role_bound_to_the_farm_rations_output()
    {
        WorldSnapshot world = CargoWorldAdmissionTests.Genesis.Value;
        CargoCircuitSources sources = CargoCircuitSources.LoadEmbedded();
        CargoOpeningStock marla = CargoWorldAdmission.OpeningStock(world, sources.Origin, CargoCircuitPolicy.BrasshollowProvision)!;
        Assert.Null(CargoWorldAdmission.OpeningStock(world, sources.Destination, CargoCircuitPolicy.GearfordProvision));
        Assert.Throws<InvalidDataException>(() => CargoWorldAdmission.OpeningStock(world, sources.Origin, CargoCircuitPolicy.GearfordProvision));
        Assert.Equal(("marla", "marla.opening-stock-source", 8UL), (marla.MerchantSourceId, marla.Role, marla.Source.UnitCapacity));
        Assert.Equal(CargoCircuitPolicy.BrasshollowProvision.Version, marla.ProvisionVersion);
        var recipe = world.Production!.Recipes.Single(value => value.PhysicalRecipe.Key == "craft_rations");
        Assert.Equal(recipe.DefinitionId, marla.RecipeId);
        Assert.Equal(recipe.ItemFor(recipe.Outputs[0]), marla.Definition);
        // The shelf is one raw counted-source interval in custody; no produced (transformed) lot claims it.
        CountedSourceLot shelf = Assert.Single(world.Inventory.Custody.Lots, lot => lot.Source.SourceId == marla.Source.SourceId);
        Assert.Equal(new SourceInterval(marla.Source.SourceId, 0, 8), shelf.Source);
        Assert.DoesNotContain(world.Production.Lots, lot => lot.Definition == marla.Definition);
    }

    [Fact]
    public void The_retained_water_owners_are_equivalent_except_for_their_inventory_pins()
    {
        WorldSnapshot farm = RestorativeJourneyAdmission.CreateEmbedded(Seed, "Traveler");
        WorldSnapshot cargo = CargoWorldAdmission.Compose(farm, CargoCircuitSources.LoadEmbedded());
        var retained = farm.Reservoirs!;
        var rebuilt = cargo.Reservoirs!;
        Assert.Equal(retained.Capacity.Domain.ContentHash, rebuilt.Capacity.Domain.ContentHash);
        Assert.Equal(retained.Capacity.Closed, rebuilt.Capacity.Closed);
        Assert.Equal((0, 0), (retained.Capacity.Pending.Count, rebuilt.Capacity.Pending.Count));
        Assert.Equal(retained.Capacity.Receivers, rebuilt.Capacity.Receivers);
        Assert.Equal(retained.Capacity.Revision, rebuilt.Capacity.Revision);
        Assert.Equal(retained.Geometries, rebuilt.Geometries);
        // Projections hold fill objects without value equality; their geometry and unit facts are compared.
        Assert.Equal(retained.Projections.Select(value => (value.Geometry, value.ExistingUnits, value.ReservedIncomingUnits)),
            rebuilt.Projections.Select(value => (value.Geometry, value.ExistingUnits, value.ReservedIncomingUnits)));
        // The pins change: the capacity now counts the merchants' stores and the extended custody.
        Assert.NotEqual(retained.Capacity.Inventory.Custody.Storages.Count, rebuilt.Capacity.Inventory.Custody.Storages.Count);
        Assert.Same(farm.ReservoirSeals, cargo.ReservoirSeals);
        var cells = farm.LiquidCells!;
        var rebound = cargo.LiquidCells!;
        Assert.Equal(cells.Quantities.OrderBy(value => value.Key), rebound.Quantities.OrderBy(value => value.Key));
        Assert.Equal(cells.Portions, rebound.Portions);
        Assert.Equal(cells.DomainCells, rebound.DomainCells);
        Assert.Equal((cells.UnitsPerCell, cells.VolumePerUnit, cells.Boundary),
            (rebound.UnitsPerCell, rebound.VolumePerUnit, rebound.Boundary));
        Assert.Equal(cells.Topology is null, rebound.Topology is null);
        if (cells.Topology is { } topology)
        {
            Assert.Equal(topology.NominalCells, rebound.Topology!.NominalCells);
            Assert.Equal(topology.OpenCells, rebound.Topology.OpenCells);
            Assert.Equal(topology.OpenCapacity, rebound.Topology.OpenCapacity);
        }
        Assert.Equal(rebuilt.ContentHash, rebound.ReservoirHash);
    }
}
