using OpenFantasy.Client;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Client;

public sealed class AdventureEntryTests
{
    [Fact]
    public async Task Ordinary_opening_earns_Wrens_reward_and_reopens_the_same_recorded_adventure()
    {
        using var files = new SessionFiles();
        var genesis = AdventureWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, [AdventureWorldAdmission.DefaultPlayerName]);
        ulong reward;
        await using (var client = await Open(files.Path))
        {
            Assert.True(client.IsRegional);
            Assert.Equal(2, client.CargoStalls.Count);
            Assert.Equal(0UL, client.World.ReadView().Progression!.CurrencyBalance);
            client.World.SetReady(true);
            foreach (var action in new[] { FarmSemanticAction.Speak, FarmSemanticAction.Give,
                FarmSemanticAction.Give, FarmSemanticAction.CloseDialogue })
            {
                var offer = client.CreateOffers(client.World.ReadView())[action];
                Assert.True(offer.CanExecute, offer.BlockingReason);
                var receipt = await client.SubmitOffer(offer).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
                Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            }
            client.World.SetReady(false);
            reward = client.World.ReadView().Progression!.CurrencyBalance;
            Assert.True(reward > 0);
        }
        WorldSnapshot saved;
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis)))
        {
            saved = authority.ReadStateForHost();
            Assert.Equal(ResidentControllerKind.Host, saved.GetResident(saved.Construction!.Projects.Single().BuilderActorId).ControllerKind);
        }
        await using (var reopened = await Open(files.Path))
        {
            Assert.Equal(reward, reopened.World.ReadView().Progression!.CurrencyBalance);
            Assert.Equal(saved.WorldId, reopened.World.ReadView().WorldId);
            Assert.True((await reopened.VerifyRecordedHistoryAsync().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken)).ReproducesHead);
        }
        Assert.Equal(WorldSnapshotCodec.Encode(saved), WorldSnapshotCodec.Encode(
            RecordedWorldReader.ReadForHost(files.Path, saved.Revision).State));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Different_profile_or_cooperative_roster_is_refused_without_rewriting_the_save(bool cooperative)
    {
        using var files = new SessionFiles();
        var other = cooperative
            ? AdventureWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, [AdventureWorldAdmission.DefaultPlayerName, "Mira"])
            : CargoOutcomeWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, AdventureWorldAdmission.DefaultPlayerName);
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, other)) { }
        byte[] before = File.ReadAllBytes(files.Path);
        var error = await Assert.ThrowsAsync<InvalidDataException>(async () => { await using var ignored = await Open(files.Path); });
        Assert.Contains("expected genesis", error.Message);
        Assert.Equal(before, File.ReadAllBytes(files.Path));
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(other), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
    }

    private static Task<FarmClientSession> Open(string path) => FarmClientSession.OpenAdventureAsync(path,
        AdventureWorldAdmission.SeedText, AdventureWorldAdmission.DefaultPlayerName);
}
