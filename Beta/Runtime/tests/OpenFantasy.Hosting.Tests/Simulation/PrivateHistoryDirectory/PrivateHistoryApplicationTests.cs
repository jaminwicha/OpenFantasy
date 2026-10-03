using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Simulation.PrivateHistoryDirectory;

public sealed class PrivateHistoryApplicationTests
{
    [Fact]
    public async Task Owned_application_exposes_its_real_home_capture_without_a_separate_selected_control_interface()
    {
        using var files = new SessionFiles();
        var genesis = Genesis();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var host = new LocalWorldSession(authority, Id(10), Delivery, new ManualSessionTimeProvider());
        await using IPlayerSession application = new OwnedLocalPlayerSession(host);
        var history = Assert.IsAssignableFrom<IPrivateHistoryPlayerSession>(application);
        Assert.True(history.SupportsPrivateHistoryDirectory);
        var observed = history.ReadPrivateHistoryObservation();
        Assert.Equal(application.ReadView(), observed.HomeView);
        Assert.Equal(observed.HomeView.PlayerActorId, observed.Selection.ActorId);
        Assert.Equal(observed.Selection.HomeActorId, observed.Selection.ActorId);
        Assert.Null(observed.Selection.ControlWitness);
        Assert.Null(observed.ControlledView);
        var request = new LocalPrivateHistoryPageRequest();
        var result = await history.ReadSelectedPrivateHistoryAsync(observed.Selection, request)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.ValidateFor(history.ReadPrivateHistoryObservation(), LocalSelectedQueryOperation.ReadPrivateHistory);
        result.Value.ValidateFor(request);
        Assert.Empty(result.Value.Entries);
        Assert.Equal(genesis.Revision, result.Value.ThroughRevision);
        Assert.Equal(genesis.ContentHash, authority.ReadStateForHost().ContentHash);
    }
}
