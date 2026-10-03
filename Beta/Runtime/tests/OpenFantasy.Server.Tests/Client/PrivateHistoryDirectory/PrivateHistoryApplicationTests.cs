using OpenFantasy.Hosting.Networking.Client;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Server.Tests.PrivateHistoryDirectory;

public sealed class PrivateHistoryApplicationTests
{
    [Fact]
    public async Task Legacy_remote_profile_does_not_advertise_an_unnegotiated_directory_capture()
    {
        await using var world = await RemoteSessionWorld.Open();
        await using var application = await RemotePlayerSession.OpenAsync(await world.ConnectProtocol(), ServerWorld.Cancellation);
        IPrivateHistoryPlayerSession history = application;
        Assert.False(application.SupportsActorSelection);
        Assert.False(history.SupportsPrivateHistoryDirectory);
        Assert.Throws<InvalidOperationException>(() => history.ReadPrivateHistoryObservation());
        Assert.False(application.Completion.IsCompleted);
        Assert.Equal(LocalWorldRunState.Paused, application.ReadRunState());
    }
}
