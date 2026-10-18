using System.Collections.ObjectModel;
using System.Diagnostics;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Tests.Construction;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Worlds;

public sealed class WorldResidentExposureBasisTests(ITestOutputHelper output)
{
    [Fact]
    public void Earned_basis_preserves_every_resident_field_and_distinct_age_at_every_boundary()
    {
        var world = ConstructionWorldFixture.Create();
        var residents = Array.AsReadOnly(world.Residents.Select((value, index) => WithExposure(value,
            new LocalExposureDuration((ulong)index + 7, LocalExposureDuration.MillisecondsPerDay - 900))).ToArray());
        var basis = new WorldResidentExposureBasis(residents, world.AdvancingSteps);
        var initial = basis;
        var expected = residents;
        for (ulong step = 1; step <= 200; step++)
        {
            basis = basis.AdvanceTo(world.AdvancingSteps + step);
            expected = Array.AsReadOnly(expected.Select(value => value.ExperienceStep()).ToArray());
            Assert.Equal(Encode(expected), Encode(basis.Materialize()));
            foreach (var resident in residents)
                Assert.Equal(resident.InventoryCustodianId, basis.InventoryCustodian(resident.ActorId));
        }
        Assert.Same(residents, initial.Materialize());
        Assert.Equal(Encode(residents), Encode(initial.Materialize()));
        Assert.Throws<InvalidOperationException>(() => basis.InventoryCustodian(new EntityId(111, 999)));
    }

    [Fact]
    public void Reselection_materialization_and_rebase_never_earn_or_double_charge_time()
    {
        var world = ConstructionWorldFixture.Create();
        var original = new WorldResidentExposureBasis(world.Residents, world.AdvancingSteps);
        var step = original.AdvanceTo(world.AdvancingSteps + 1);
        Assert.Equal(Encode(step.Materialize()), Encode(original.AdvanceTo(world.AdvancingSteps + 1).Materialize()));
        Assert.Equal(Encode(world.Residents), Encode(original.Materialize()));
        var actual = new WorldResidentExposureBasis(step.Materialize(), step.AdvancingSteps);
        for (int i = 0; i < 200; i++) actual = actual.AdvanceTo(actual.AdvancingSteps + 1);
        var expected = Array.AsReadOnly(world.Residents.Select(value => value.Experience(ExposurePacingV1.FromExplorationSteps(201))).ToArray());
        Assert.Equal(Encode(expected), Encode(actual.Materialize()));
        Assert.Equal(Encode(step.Materialize()), Encode(original.AdvanceTo(world.AdvancingSteps + 1).Materialize()));
    }

    [Theory]
    [InlineData(9UL)]
    [InlineData(10UL)]
    [InlineData(12UL)]
    public void Only_the_next_selected_boundary_can_advance(ulong selected)
    {
        var residents = ConstructionWorldFixture.Create().Residents;
        var basis = new WorldResidentExposureBasis(residents, 10);
        Assert.Throws<InvalidOperationException>(() => basis.AdvanceTo(selected));
        Assert.Same(residents, basis.Materialize());
        var exhausted = new WorldResidentExposureBasis(residents, ulong.MaxValue);
        Assert.Throws<OverflowException>(() => exhausted.AdvanceTo(0));
        Assert.Same(residents, exhausted.Materialize());
    }

    [Fact]
    public void Any_resident_overflow_refuses_at_selection_instead_of_being_deferred_until_read()
    {
        var source = ConstructionWorldFixture.Create().Residents;
        var residents = Array.AsReadOnly(new[] { source[0], WithExposure(source[1],
            new LocalExposureDuration(ulong.MaxValue, LocalExposureDuration.MillisecondsPerDay - 1801)) });
        var basis = new WorldResidentExposureBasis(residents, 0).AdvanceTo(1);
        var expected = Array.AsReadOnly(residents.Select(value => value.ExperienceStep()).ToArray());
        Assert.Equal(Encode(expected), Encode(basis.Materialize()));
        Assert.Throws<OverflowException>(() => expected[1].ExperienceStep());
        Assert.Throws<OverflowException>(() => basis.AdvanceTo(2));
        Assert.Equal(1UL, basis.AdvancingSteps);
        Assert.Equal(Encode(expected), Encode(basis.Materialize()));
    }

    [Fact]
    public void Actual_resident_write_comparison_includes_capture_and_final_materialization()
    {
        var residents = ConstructionWorldFixture.Create().Residents;
        const int steps = 200;
        ReadOnlyCollection<WorldResident> Explicit()
        {
            var value = residents;
            for (int i = 0; i < steps; i++) value = Array.AsReadOnly(value.Select(resident => resident.ExperienceStep()).ToArray());
            return value;
        }
        ReadOnlyCollection<WorldResident> Basis(bool materializeEveryStep)
        {
            var value = new WorldResidentExposureBasis(residents, 0);
            ReadOnlyCollection<WorldResident>? last = null;
            for (ulong i = 1; i <= steps; i++)
            {
                value = value.AdvanceTo(i);
                if (materializeEveryStep) last = value.Materialize();
            }
            return last ?? value.Materialize();
        }
        Explicit(); Basis(false); Basis(true); // Warm each exact operation; no results used as acceptance.
        foreach (var (name, operation) in new (string, Func<ReadOnlyCollection<WorldResident>>)[]
        {
            ("explicit-every-step", Explicit), ("basis-final-read", () => Basis(false)),
            ("basis-every-step-read", () => Basis(true))
        })
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread(), ticks = Stopwatch.GetTimestamp();
            var actual = operation();
            ticks = Stopwatch.GetTimestamp() - ticks;
            bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
            Assert.Equal(Encode(Explicit()), Encode(actual));
            output.WriteLine($"{name}: {residents.Count} actual residents, {steps} selected steps, {bytes} allocated bytes, {ticks * 1000.0 / Stopwatch.Frequency:F3} ms; capture/final materialization included. Not a scale or persistence benchmark.");
        }
    }

    private static byte[] Encode(IEnumerable<WorldResident> residents) => WorldSnapshotCodecV1.EncodeResidents(residents);

    private static WorldResident WithExposure(WorldResident value, LocalExposureDuration exposure) => new(
        value.ActorId, value.InventoryCustodianId, value.DisplayName, value.ControllerKind, value.HomeFeet,
        value.WanderRadiusCells, value.Comprehension, exposure, value.OpeningUtterance);
}
