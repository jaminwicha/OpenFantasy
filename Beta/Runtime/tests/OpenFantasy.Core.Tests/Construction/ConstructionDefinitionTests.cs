using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Tests.Construction;

public sealed class ConstructionDefinitionTests
{
    [Fact]
    public void Cross_slot_members_retain_exact_material_volume_and_permutation_independence()
    {
        ConstructionDefinition definition = ConstructionFixture.Definition();
        Assert.Equal(540_000UL, definition.Members.Aggregate(0UL, (sum, member) => sum + member.VolumeMicrolitres));
        Assert.Equal(270_000UL, definition.MaterialTransform.PhysicalRecipe.OutputMass.Value);
        Assert.Equal(30_000UL, definition.MaterialTransform.PhysicalRecipe.WasteMass.Value);
        Assert.Equal(definition.Version, ConstructionFixture.Definition(definition.Members.Reverse()).Version);
        Assert.Equal(10, definition.CreateStructure(ConstructionFixture.Id(99), new(0, 0, 256)).Boxes.Count);
    }

    [Fact]
    public void Disconnected_or_merely_corner_touching_material_is_not_a_supported_assembly()
    {
        Assert.Throws<ArgumentException>(() => ConstructionFixture.Definition(
            [new(0, 0, 0, 60, 60, 75), new(60, 60, 75, 120, 120, 150)]));
        Assert.Throws<ArgumentException>(() => ConstructionFixture.Definition(
            [new(0, 0, 0, 60, 60, 75), new(80, 0, 0, 140, 60, 75)]));
        Assert.Throws<ArgumentException>(() => ConstructionFixture.Definition(
            ConstructionFixture.Members().Append(new(0, 0, 0, 1, 1, 1))));
    }

    [Fact]
    public void Outward_quantization_preserves_signed_bounds_without_claiming_exact_material_volume()
    {
        var member = new MillimetreBox(-5, -5, 0, 5, 5, 5);
        SubunitBox box = member.At(new(-1, -1, 256));
        Assert.Equal(new SubunitPosition(-3, -3, 256), box.Minimum);
        Assert.Equal(new SubunitPosition(1, 1, 258), box.Maximum);
        Assert.Equal(500UL, member.VolumeMicrolitres);
        Assert.Throws<OverflowException>(() => member.At(new(long.MaxValue, 0, 0)));
    }
}
