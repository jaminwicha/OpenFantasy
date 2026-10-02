using Microsoft.Extensions.Hosting;

namespace OpenFantasy.Server;

/// <summary>The listener lifetime follows its explicit owner instead of process-wide console or POSIX signals.</summary>
internal sealed class PlayerListenerLifetime : IHostLifetime
{
    public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
