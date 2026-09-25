using System.Text.Json;
using OpenFantasy.Client;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Client;

public sealed class BreachSelectionTests
{
    [Theory]
    [InlineData("farm")]
    [InlineData("worksite")]
    [InlineData("watercourse")]
    [InlineData("another-seed")]
    public async Task Explicit_breach_selection_refuses_other_saves_without_replacement_or_retained_lease(string selection)
    {
        const string seed = "breach-selection";
        WorldSnapshot retained = selection switch
        {
            "farm" => GatheringFarmAdmission.CreateEmbedded(seed, "Traveler"),
            "worksite" => WorksiteFarmAdmission.CreateEmbedded(seed, "Traveler"),
            "watercourse" => WatercourseFarmAdmission.CreateEmbedded(seed, "Traveler"),
            _ => BreachFarmAdmission.CreateEmbedded("another-seed", "Traveler")
        };
        using var files = new SessionFiles();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, retained))
            Assert.Equal(retained.ContentHash, authority.ReadStateForHost().ContentHash);
        byte[] before = File.ReadAllBytes(files.Path);
        await Assert.ThrowsAsync<InvalidDataException>(() => FarmClientSession.OpenBreachAsync(files.Path, seed, "Traveler"));
        Assert.Equal(before, File.ReadAllBytes(files.Path));
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(retained.ContentHash, reopened.ReadStateForHost().ContentHash);
    }

    [Fact]
    public async Task Explicit_breach_selection_creates_and_reopens_the_same_unadvanced_public_world()
    {
        using var files = new SessionFiles();
        string before;
        await using (var client = await FarmClientSession.OpenBreachAsync(files.Path, "breach-selection", "Traveler"))
        {
            var view = client.World.ReadView();
            Assert.NotNull(view.Worksite);
            Assert.NotNull(view.Liquids);
            Assert.False(view.Worksite.HasPhysicalConstruction);
            Assert.False(view.Worksite.SupportsAssistedTraversal);
            Assert.Equal(0UL, Assert.Single(view.Worksite.ExcavationTargets).CompletedWork);
            before = JsonSerializer.Serialize(view);
        }
        byte[] closed = File.ReadAllBytes(files.Path);
        await using (var reopened = await FarmClientSession.OpenBreachAsync(files.Path, "breach-selection", "Traveler"))
            Assert.Equal(before, JsonSerializer.Serialize(reopened.World.ReadView()));
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
    }
}
