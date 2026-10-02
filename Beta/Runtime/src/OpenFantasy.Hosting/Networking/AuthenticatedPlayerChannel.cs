using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking;

/// <summary>Owns one admitted player port and its transport, never the world or its scheduler.</summary>
public sealed class AuthenticatedPlayerChannel : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly Action _release;
    private Task? _closing;

    internal AuthenticatedPlayerChannel(LocalPlayerConnection player, PlayerMessageTransport transport, Action release,
        PlayerPresentationBootstrap? presentation = null)
    {
        Player = player;
        Transport = transport;
        _release = release;
        Presentation = presentation;
    }

    public LocalPlayerConnection Player { get; }
    public PlayerMessageTransport Transport { get; }
    public PlayerPresentationBootstrap? Presentation { get; }

    public ValueTask DisposeAsync()
    {
        lock (_gate) { return new ValueTask(_closing ??= CloseAsync()); }
    }

    private async Task CloseAsync()
    {
        try { await Transport.DisposeAsync().ConfigureAwait(false); }
        finally
        {
            try { await Player.DisposeAsync().ConfigureAwait(false); }
            finally { _release(); }
        }
    }
}
