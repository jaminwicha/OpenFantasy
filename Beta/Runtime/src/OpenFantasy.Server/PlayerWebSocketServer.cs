using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenFantasy.Hosting;
using OpenFantasy.Hosting.Networking;

namespace OpenFantasy.Server;

/// <summary>
/// Owns one HTTPS WebSocket listener and its bounded request lifetimes. It never owns or disposes the world host,
/// provisions credentials or selects player identity. Established transport termination is confirmed by the session.
/// </summary>
public sealed class PlayerWebSocketServer : IAsyncDisposable
{
    public const string Subprotocol = "openfantasy.v1";
    public const string PlayerPath = "/player";
    public const int MaximumPlayerRequests = 8;
    private readonly object _gate = new();
    private readonly WebApplication _application;
    private readonly PlayerAdmissionService _admission;
    private readonly PlayerWebSocketRequests _requests = new();
    private readonly CancellationTokenSource _sessions = new();
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenRegistration _applicationStopping;
    private Task? _stopping;
    private Exception? _failure;
    private bool _stopRequested;
    private Uri? _endpoint;

    private PlayerWebSocketServer(WebApplication application, PlayerAdmissionService admission)
    {
        _application = application;
        _admission = admission;
        _applicationStopping = application.Lifetime.ApplicationStopping.Register(ApplicationStopping);
    }

    public Uri Endpoint => _endpoint ?? throw new InvalidOperationException("The player listener has not started.");

    /// <summary>Completes after listener and request cleanup; unexpected listener or world-host termination faults it.</summary>
    public Task Completion => _completion.Task;

    public static async Task<PlayerWebSocketServer> StartAsync(PlayerAdmissionService admission,
        PlayerWebSocketServerOptions options, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(options);
        token.ThrowIfCancellationRequested();
        WebApplication application = PlayerWebSocketApplication.Create(options);
        var server = new PlayerWebSocketServer(application, admission);
        application.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
        application.Run(server.HandleAsync);
        try
        {
            await application.StartAsync(token).ConfigureAwait(false);
            var addresses = application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException("The listener did not report its bound address.");
            Uri bound = new(addresses.Addresses.Single());
            server._endpoint = new UriBuilder("wss", options.AdvertisedHost, bound.Port, PlayerPath).Uri;
            if (admission.HostCompletion.IsCompleted)
            {
                await admission.HostCompletion.ConfigureAwait(false);
                throw new InvalidOperationException("The world host stopped before the listener was ready.");
            }
            server.ObserveHost();
            return server;
        }
        catch
        {
            // Preserve the startup exception while still cleaning a partially started listener.
            try { await server.StopAsync().ConfigureAwait(false); }
            catch { }
            throw;
        }
    }

    /// <summary>
    /// Stops admission, cancels connection work and joins all admitted request cleanup before returning.
    /// Canceling this wait never cancels or abandons the underlying cleanup; Completion still reports its result.
    /// </summary>
    public async Task StopAsync(CancellationToken token = default)
    {
        await BeginStop(null).WaitAsync(token).ConfigureAwait(false);
        await Completion.ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private async Task HandleAsync(HttpContext context)
    {
        if (!ValidateRequest(context)) return;
        if (!_requests.TryEnter(out TaskCompletionSource? finished))
        {
            Refuse(context, StatusCodes.Status503ServiceUnavailable);
            return;
        }
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, _sessions.Token);
        try
        {
            cancellation.Token.ThrowIfCancellationRequested();
            using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext
            {
                SubProtocol = Subprotocol,
                DangerousEnableCompression = false
            }).ConfigureAwait(false);
            var transport = new WebSocketPlayerTransport(socket);
            // Admission owns the transport on both success and failure. The session owns the admitted channel.
            AuthenticatedPlayerChannel channel = await _admission.AcceptAsync(transport, _sessions.Token, context.RequestAborted).ConfigureAwait(false);
            await new PlayerCommandSession(channel).RunAsync(_sessions.Token, context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            HandleConnectionFailure(error, cancellation.IsCancellationRequested);
            context.Abort();
        }
        finally
        {
            _requests.Leave(finished!);
        }
    }

    private static bool ValidateRequest(HttpContext context)
    {
        if (!string.Equals(context.Request.Path.Value, PlayerPath, StringComparison.Ordinal))
        {
            Refuse(context, StatusCodes.Status404NotFound);
            return false;
        }
        if (!context.Request.IsHttps || !HttpMethods.IsGet(context.Request.Method)
            || !string.Equals(context.Request.Protocol, "HTTP/1.1", StringComparison.Ordinal)
            || context.Request.QueryString.HasValue || context.Request.Headers.ContainsKey("Origin")
            || context.Request.ContentLength is > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding")
            || !context.WebSockets.IsWebSocketRequest
            || !context.WebSockets.WebSocketRequestedProtocols.Contains(Subprotocol, StringComparer.Ordinal))
        {
            Refuse(context, StatusCodes.Status400BadRequest);
            return false;
        }
        return true;
    }

    private static void Refuse(HttpContext context, int status)
    {
        context.Response.StatusCode = status;
        context.Response.ContentLength = 0;
    }

    internal void HandleConnectionFailure(Exception error, bool cancelled)
    {
        // Backend failures can share exception types with malformed peers. Consult the actual owner before
        // classifying a connection failure, and retain the independent host observer even when there are no peers.
        if (_admission.HostCompletion.IsFaulted)
            _ = BeginStop(_admission.HostCompletion.Exception!.GetBaseException());
        else if (!IsExpectedConnectionEnd(error, cancelled))
            _ = BeginStop(error);
    }

    private static bool IsExpectedConnectionEnd(Exception error, bool cancelled) => error is
        PlayerAdmissionException or PlayerResponseTimeoutException or AuthorityDispatchException or FormatException
        or WebSocketException or IOException or OperationCanceledException
        || cancelled && error is ObjectDisposedException;

    private Task BeginStop(Exception? failure)
    {
        lock (_gate)
        {
            _failure ??= failure;
            _stopRequested = true;
            return _stopping ??= StopCoreAsync(_requests.CloseAndJoin());
        }
    }

    private async Task StopCoreAsync(Task joined)
    {
        // Assign the shared stop task before cancellation invokes callbacks or the host starts stopping.
        await Task.Yield();
        try
        {
            try { _sessions.Cancel(); }
            catch (Exception error) { RecordFailure(error); }
            try { await _application.StopAsync(CancellationToken.None).ConfigureAwait(false); }
            catch (Exception error) { RecordFailure(error); }
            // Kestrel shutdown alone does not represent completion of an application's already-admitted history read.
            await joined.ConfigureAwait(false);
        }
        finally
        {
            try { await _application.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error) { RecordFailure(error); }
            _applicationStopping.Dispose();
            _sessions.Dispose();
            lock (_gate)
            {
                if (_admission.HostCompletion.IsFaulted)
                    _failure ??= _admission.HostCompletion.Exception!.GetBaseException();
                if (_failure is { } failure) _completion.TrySetException(failure);
                else _completion.TrySetResult();
            }
        }
    }

    private void RecordFailure(Exception failure)
    {
        lock (_gate) { _failure ??= failure; }
    }

    private void ApplicationStopping()
    {
        lock (_gate)
        {
            if (_stopRequested) return;
        }
        _ = BeginStop(new InvalidOperationException("The player listener stopped unexpectedly."));
    }

    private void ObserveHost()
    {
        // The continuation has no unobserved exception and does not retain this server after either lifetime ends.
        _ = ObserveHostAsync();
    }

    private async Task ObserveHostAsync()
    {
        Task ended = await Task.WhenAny(_admission.HostCompletion, Completion).ConfigureAwait(false);
        if (!ReferenceEquals(ended, _admission.HostCompletion)) return;
        lock (_gate) { if (_stopRequested) return; }
        Exception failure;
        try
        {
            await _admission.HostCompletion.ConfigureAwait(false);
            failure = new InvalidOperationException("The world host stopped while its player listener was running.");
        }
        catch (Exception error) { failure = error; }
        await BeginStop(failure).ConfigureAwait(false);
    }
}
