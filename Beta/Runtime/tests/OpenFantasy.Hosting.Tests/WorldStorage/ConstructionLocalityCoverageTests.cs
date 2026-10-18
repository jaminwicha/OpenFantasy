using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ConstructionLocalityCoverageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Complete_adventure_profiles_cannot_be_imported_by_dropping_their_other_owners(bool collectiveWithdrawal)
    {
        var world = AdventureWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, ["Rowan", "Mira"]);
        if (collectiveWithdrawal) world = WorldPartyWithdrawalAdmission.CreateFresh(world);
        Assert.Equal(collectiveWithdrawal ? 41 : 40, world.ProfileVersion);
        var before = WorldSnapshotCodec.Encode(world);
        Assert.Throws<ArgumentException>(() => ConstructionLocalityState.Capture(world,
            world.Construction!.Projects.Single().ProjectId, 128));
        Assert.Equal(before, WorldSnapshotCodec.Encode(world));
        Assert.NotNull(world.Care);
        Assert.NotNull(world.EffectiveControl);
        Assert.NotNull(world.Coordination);
        Assert.NotNull(world.EncounterLifecycle);
    }
}
