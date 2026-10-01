using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Tests.Client.Cargo;

/// <summary>
/// The ordinary production --cargo selection (<see cref="FarmClientSession.OpenCargoAsync"/>, the call NativeCargoEntry
/// makes through FarmGame): a fresh create, a same-world reopen, and refusal of a foreign seed (another world and branch)
/// and of another world's save, each refusal leaving the save's bytes exactly unchanged.
/// </summary>
public sealed class CargoProductionSelectionTests
{
    [Fact]
    public async Task The_production_cargo_selection_creates_reopens_and_refuses_foreign_saves_unchanged()
    {
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-cargo-selection-" + Guid.NewGuid().ToString("N"));
        string save = Path.Combine(directory, "worlds", "openfantasy-cargo.sqlite");
        try
        {
            EntityId world, branch;
            await using (FarmClientSession created = await FarmClientSession.OpenCargoAsync(save, NativeCargoWorld.SeedText, "Traveler"))
            {
                LocalWorldView view = created.World.ReadView();
                Assert.True(created.IsRegional && view.IsRegional && view.Regional is not null);
                Assert.Equal(2, view.Markets!.Stalls.Count);
                Assert.Equal(0UL, view.Progression!.CurrencyBalance);
                (world, branch) = (view.WorldId, view.BranchId);
            }
            Assert.True(File.Exists(save));
            await using (FarmClientSession reopened = await FarmClientSession.OpenCargoAsync(save, NativeCargoWorld.SeedText, "Traveler"))
            {
                LocalWorldView view = reopened.World.ReadView();
                Assert.Equal((world, branch), (view.WorldId, view.BranchId));
                Assert.NotNull(view.Regional);
            }
            byte[] closed = File.ReadAllBytes(save);
            await Assert.ThrowsAnyAsync<Exception>(() => FarmClientSession.OpenCargoAsync(save, "another-cargo-seed", "Traveler"));
            Assert.Equal(closed, File.ReadAllBytes(save));

            string foreign = Path.Combine(directory, "worlds", "wrens-markets.sqlite");
            await using (FarmClientSession markets = await FarmClientSession.OpenMarketsAsync(foreign, "encounter-lifecycle-world", "Traveler"))
            {
                Assert.False(markets.IsRegional);
                Assert.Null(markets.World.ReadView().Regional);
            }
            byte[] other = File.ReadAllBytes(foreign);
            await Assert.ThrowsAnyAsync<Exception>(() => FarmClientSession.OpenCargoAsync(foreign, NativeCargoWorld.SeedText, "Traveler"));
            Assert.Equal(other, File.ReadAllBytes(foreign));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
