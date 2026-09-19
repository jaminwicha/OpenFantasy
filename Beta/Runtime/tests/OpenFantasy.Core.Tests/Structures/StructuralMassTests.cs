using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Tests.Construction;
using OpenFantasy.Core.Tests.Gathering;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;

namespace OpenFantasy.Core.Tests.Structures;

public sealed class StructuralMassTests
{
    [Fact]
    public void Explicit_calibrations_require_positive_mass_and_nonnull_pins()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StructuralBodyMass(Id(40), AdmissionFixture.Version, new(0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StructuralItemMass(Material, new(0)));
        Assert.Throws<ArgumentNullException>(() => new StructuralBodyMass(Id(40), null!, new(1)));
        Assert.Throws<ArgumentNullException>(() => new StructuralItemMass(null!, new(1)));
    }

    [Fact]
    public void Catalog_copies_orders_and_rejects_duplicate_or_conflicting_definitions()
    {
        var first = new StructuralBodyMass(Id(40), AdmissionFixture.Version, new(100));
        var second = new StructuralBodyMass(Id(41), AdmissionFixture.Version, new(200));
        List<StructuralBodyMass> bodies = [second, first];
        List<StructuralItemMass> items = [new(Material, new(10))];
        var catalog = new StructuralMassCatalog(bodies, items);
        bodies.Clear();
        items.Clear();
        Assert.Equal(new[] { first, second }, catalog.Bodies);
        Assert.Single(catalog.SupplementalItems);
        Assert.Throws<ArgumentException>(() => new StructuralMassCatalog([first, first], []));
        Assert.Throws<ArgumentException>(() => new StructuralMassCatalog(
            [first, new(first.DefinitionId, ContentHash.Compute("other-body"u8), new(100))], []));
        Assert.Throws<ArgumentException>(() => new StructuralMassCatalog([], [new(Material, new(10)), new(Material, new(10))]));
        Assert.Throws<ArgumentException>(() => new StructuralMassCatalog([], [new(Material, new(10)), new(Material, new(11))]));
    }

    [Fact]
    public void Catalog_stops_each_overlong_enumeration_at_its_first_excess_element()
    {
        int bodyReads = 0;
        int itemReads = 0;
        IEnumerable<StructuralBodyMass> Bodies()
        {
            for (int index = 0; ; index++)
            {
                bodyReads++;
                if (bodyReads > StructuralMassCatalog.MaximumBodyDefinitions + 1)
                {
                    throw new InvalidOperationException("Read beyond the bounded rejection.");
                }
                yield return new StructuralBodyMass(Id((ulong)(1_000 + index)), AdmissionFixture.Version, new(1));
            }
        }
        IEnumerable<StructuralItemMass> Items()
        {
            for (int index = 0; ; index++)
            {
                itemReads++;
                if (itemReads > StructuralMassCatalog.MaximumSupplementalItems + 1)
                {
                    throw new InvalidOperationException("Read beyond the bounded rejection.");
                }
                yield return new StructuralItemMass(new(Id((ulong)(2_000 + index)), AdmissionFixture.Version), new(1));
            }
        }
        Assert.Throws<ArgumentException>(() => new StructuralMassCatalog(Bodies(), []));
        Assert.Throws<ArgumentException>(() => new StructuralMassCatalog([], Items()));
        Assert.Equal(StructuralMassCatalog.MaximumBodyDefinitions + 1, bodyReads);
        Assert.Equal(StructuralMassCatalog.MaximumSupplementalItems + 1, itemReads);
    }

    [Fact]
    public void Member_mass_uses_exact_volume_then_canonical_ordinal_remainders()
    {
        MillimetreBox[] members = [new(30, 0, 0, 60, 20, 20), new(0, 0, 0, 10, 20, 20), new(10, 0, 0, 30, 20, 20)];
        ConstructionDefinition definition = Definition(10, members);
        var catalog = new StructuralMassCatalog([], []);
        IReadOnlyList<MassMilligrams> masses = catalog.MemberMasses(definition);
        Assert.Equal(new ulong[] { 2, 3, 5 }, masses.Select(value => value.Value));
        Assert.Equal(10UL, masses.Aggregate(0UL, (sum, value) => checked(sum + value.Value)));
        Assert.Equal(masses, catalog.MemberMasses(Definition(10, members.Reverse().ToArray())));
        Assert.Throws<NotSupportedException>(() => ((IList<MassMilligrams>)masses)[0] = new(99));
    }

    [Fact]
    public void Real_guard_members_split_the_actual_installed_output_not_the_input_or_waste()
    {
        ConstructionDefinition definition = ConstructionFixture.Definition();
        IReadOnlyList<MassMilligrams> masses = new StructuralMassCatalog([], []).MemberMasses(definition);
        Assert.Equal(definition.Members.Count, masses.Count);
        Assert.Equal(270_000UL, masses.Aggregate(0UL, (sum, value) => checked(sum + value.Value)));
        for (int index = 0; index < masses.Count; index++)
        {
            Assert.Equal(definition.Members[index].VolumeMicrolitres / 2, masses[index].Value);
        }
    }

    [Fact]
    public void Tiny_total_mass_and_maximum_total_mass_retain_exact_totals()
    {
        var catalog = new StructuralMassCatalog([], []);
        MillimetreBox[] equal = [new(0, 0, 0, 2_000, 4_000, 4_000), new(2_000, 0, 0, 4_000, 4_000, 4_000)];
        Assert.Equal(new ulong[] { 1, 0 }, catalog.MemberMasses(Definition(1, equal)).Select(value => value.Value));
        IReadOnlyList<MassMilligrams> maximum = catalog.MemberMasses(Definition(ulong.MaxValue, equal));
        Assert.Equal(new[] { (1UL << 63), (1UL << 63) - 1 }, maximum.Select(value => value.Value));
        Assert.Equal(ulong.MaxValue, maximum[0].Add(maximum[1]).Value);
    }

    [Fact]
    public void Actor_load_requires_the_exact_body_pin_and_counts_only_primary_inventory()
    {
        WorldSnapshot world = Create();
        StructuralBodyMass body = Body(world, 10, 1_000);
        var catalog = new StructuralMassCatalog([body], [new(Material, new(10))]);
        Assert.Equal((UInt128)1_060, catalog.ActorLoad(world, Id(10)));
        Assert.Equal((UInt128)1_000, catalog.ActorLoad(world, Id(11)));
        Assert.Throws<ArgumentException>(() => new StructuralMassCatalog([], [new(Material, new(10))]).ActorLoad(world, Id(10)));
        Assert.Throws<ArgumentException>(() => new StructuralMassCatalog(
            [new(body.DefinitionId, ContentHash.Compute("wrong-body-pin"u8), body.Mass)],
            [new(Material, new(10))]).ActorLoad(world, Id(10)));
    }

    [Fact]
    public void Unknown_or_changed_carried_item_pin_rejects_instead_of_becoming_weightless()
    {
        WorldSnapshot world = Create();
        StructuralBodyMass body = Body(world, 10, 1_000);
        var unknown = new StructuralMassCatalog([body], []);
        Assert.Throws<ArgumentException>(() => unknown.ActorLoad(world, Id(10)));
        Assert.Equal((UInt128)1_000, unknown.ActorLoad(world, Id(11)));
        var wrongPin = new StructuralMassCatalog([body],
            [new(new(Material.DefinitionId, ContentHash.Compute("wrong-item-pin"u8)), new(10))]);
        Assert.Throws<ArgumentException>(() => wrongPin.ActorLoad(world, Id(10)));
    }

    [Fact]
    public void Reserved_and_equipped_actual_goods_still_contribute_their_entire_mass()
    {
        WorldSnapshot world = Create(inventory: initial => InventoryWith(initial, 6,
            LotProtection.Equipped | LotProtection.Favorite | LotProtection.UserReserved, Id(5_000)));
        var catalog = new StructuralMassCatalog([Body(world, 10, 1_000)], [new(Material, new(10))]);
        Assert.Equal((UInt128)1_060, catalog.ActorLoad(world, Id(10)));
    }

    [Fact]
    public void Maximum_unit_mass_quantity_and_body_mass_use_checked_wide_arithmetic()
    {
        WorldSnapshot world = Create(inventory: initial => InventoryWith(initial, ulong.MaxValue));
        var catalog = new StructuralMassCatalog([Body(world, 10, ulong.MaxValue)], [new(Material, new(ulong.MaxValue))]);
        UInt128 expected = checked((UInt128)ulong.MaxValue * ulong.MaxValue + ulong.MaxValue);
        Assert.Equal(expected, catalog.ActorLoad(world, Id(10)));
        Assert.True(expected > ulong.MaxValue);
    }

    [Fact]
    public void Production_calibration_counts_live_output_without_double_counting_retired_inputs()
    {
        WorldSnapshot world = ProductionWorldFixture.Create();
        var catalog = new StructuralMassCatalog([Body(world, 10, 1_000)], [new(GatheringFixture.Axe, new(7))]);
        UInt128 before = catalog.ActorLoad(world, Id(10));
        WorldStepDecision step = new WorldStepProcessor().Decide(world,
            ProductionWorldFixture.Batch(world, 1_300, ProductionWorldFixture.Craft(world)));
        Assert.Equal(WorldActionRejection.None, step.Receipts.Single().Rejection);
        Assert.Equal(2UL, step.Candidate.Production!.Batches.Single().Ingredients.Single().Quantity);
        Assert.Equal(4UL, step.Candidate.Inventory.Custody.GetLot(Id(70)).Quantity);
        Assert.Single(step.Candidate.Production.Lots);
        Assert.Equal((UInt128)1_067, before);
        Assert.Equal(before, catalog.ActorLoad(step.Candidate, Id(10)));
    }

    [Fact]
    public void Supplemental_overlap_must_match_the_production_pin_and_mass()
    {
        WorldSnapshot world = ProductionWorldFixture.Create();
        StructuralBodyMass body = Body(world, 10, 1_000);
        StructuralItemMass tool = new(GatheringFixture.Axe, new(7));
        var matching = new StructuralMassCatalog([body], [tool, new(Material, new(10))]);
        Assert.Equal((UInt128)1_067, matching.ActorLoad(world, Id(10)));
        Assert.Throws<ArgumentException>(() => new StructuralMassCatalog([body], [tool, new(Material, new(11))])
            .ActorLoad(world, Id(10)));
        Assert.Throws<ArgumentException>(() => new StructuralMassCatalog([body],
            [tool, new(new(Material.DefinitionId, ContentHash.Compute("different-pin"u8)), new(10))])
            .ActorLoad(world, Id(10)));
    }

    [Fact]
    public void Project_custody_and_installed_output_never_duplicate_the_builders_carried_load()
    {
        WorldSnapshot world = ConstructionWorldFixture.Create();
        ConstructionProject project = world.Construction!.Projects.Single();
        var catalog = new StructuralMassCatalog([Body(world, 11, 1_000)],
            [new(project.Definition.RequiredTool.Item, new(7))]);
        Assert.Contains(world.Inventory.Custody.Lots, lot => lot.CustodianId == project.ProjectCustodianId);
        Assert.Equal((UInt128)1_007, catalog.ActorLoad(world, ConstructionWorldFixture.Builder));
        for (ulong index = 0; index < 200; index++)
        {
            WorldStepDecision step = ConstructionWorldFixture.Step(world, 100 + index);
            Assert.Equal(WorldActionRejection.None, step.Receipts.Single().Rejection);
            world = step.Candidate;
        }
        Assert.True(world.Construction!.Projects.Single().IsCompleted);
        Assert.NotEmpty(world.Production!.Lots);
        Assert.All(world.Production.Lots, lot => Assert.Equal(project.ProjectCustodianId, lot.CustodianId));
        Assert.Equal((UInt128)1_007, catalog.ActorLoad(world, ConstructionWorldFixture.Builder));
        Assert.Equal(270_000UL, catalog.MemberMasses(project.Definition).Aggregate(0UL, (sum, mass) => checked(sum + mass.Value)));
    }

    private static StructuralBodyMass Body(WorldSnapshot world, ulong actor, ulong mass)
    {
        var binding = world.Space.GetActor(Id(actor)).Binding;
        return new StructuralBodyMass(binding.DefinitionId, binding.DefinitionVersion, new(mass));
    }

    private static CountedAuthorityState InventoryWith(CountedAuthorityState initial, ulong quantity,
        LotProtection protection = LotProtection.None, EntityId? reservation = null)
    {
        var custody = new CustodySnapshot(initial.WorldId, initial.BranchId, initial.Custody.Revision,
            [new(Id(60), quantity, 0), new(Id(61), 1, 0)],
            [new(Id(70), Id(60), Material, new(Id(71), 0, quantity), 0, protection, reservation)]);
        return new CountedAuthorityState(custody, quantity, initial.Actors, initial.Controls, initial.Policies,
            [new(Id(71), Material, quantity)], reservation is { } id ? [id] : []);
    }

    private static ConstructionDefinition Definition(ulong mass, MillimetreBox[] members)
    {
        ulong volume = members.Aggregate(0UL, (sum, member) => checked(sum + member.VolumeMicrolitres));
        var package = new PackageId("structural-mass-test");
        var input = new MaterialDefinition(package, Id(30_001), "input", new(mass), new(volume), new(1));
        var output = new MaterialDefinition(package, Id(30_002), "output", new(mass), new(volume), new(1));
        var inputItem = new CountedItemDefinition(Id(30_003), AdmissionFixture.Version);
        var outputItem = new CountedItemDefinition(Id(30_004), AdmissionFixture.Version);
        var physical = new MaterialRecipeDefinition(package, Id(30_005), "assemble", new(1), null,
            new MaterialCatalog([input, output]), [new(RecipeMaterialRole.Input, input.Pin(), 1), new(RecipeMaterialRole.Output, output.Pin(), 1)]);
        var recipe = new CraftRecipeDefinition(Id(30_006), physical, [new(inputItem, input), new(outputItem, output)],
            [new(inputItem, 0, new(mass))]);
        var tool = new ConstructionToolDefinition(Id(30_007), new(Id(30_008), AdmissionFixture.Version), ConstructionToolOperation.CutCrossSlots);
        return new ConstructionDefinition(Id(30_009), "test/mass", "Mass specimen", recipe, 0, tool, members,
            new(1), new ExposureRate(Id(30_010), AdmissionFixture.Version, 1, ExposurePacingV1.ExplorationMillisecondsPerStep));
    }
}
