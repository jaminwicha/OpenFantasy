using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking.Client;

/// <summary>Application owner for a remote participant: cached status, bounded queries and joined connection cleanup.</summary>
public sealed class RemotePlayerSession : IPlayerSession
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);
    private readonly object _gate = new();
    private readonly PlayerProtocolClient _client;
    private readonly SemaphoreSlim _querySlot = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _completion;
    private PlayerParticipantStatus _status;
    private Task? _foreground;
    private bool _foregroundPending;
    private bool _closed;

    private RemotePlayerSession(PlayerProtocolClient client, PlayerPresentationBootstrap presentation, PlayerParticipantStatus status)
    {
        _client = client;
        Presentation = presentation;
        _status = status;
        _completion = RunAsync();
    }

    public PlayerPresentationBootstrap Presentation { get; }
    public PlayerWelcome Welcome => _client.Welcome;
    public Task Completion => _completion;
    public LocalWorldView ReadView() => _client.ReadView();
    public LocalWorldRunState ReadRunState() => Volatile.Read(ref _status).RunState;
    public LocalTradeInquiryView? ReadTradeInquiry() => Volatile.Read(ref _status).Inquiry;

    /// <summary>Takes ownership; unsupported presentation or failed startup closes the supplied connection.</summary>
    public static async Task<RemotePlayerSession> OpenAsync(PlayerProtocolClient client, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        long started = TimeProvider.System.GetTimestamp();
        using var deadline = new CancellationTokenSource(PlayerProtocolClient.StartupLimit);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            var bootstrap = await client.QueryAsync(new(PlayerQueryKind.ReadPresentationBootstrap))
                .WaitAsync(linked.Token).ConfigureAwait(false);
            var presentation = bootstrap.Presentation ?? throw new InvalidDataException("This host does not support the native presentation.");
            var status = await client.QueryAsync(new(PlayerQueryKind.ReadParticipantStatus))
                .WaitAsync(linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            if (TimeProvider.System.GetElapsedTime(started) >= PlayerProtocolClient.StartupLimit)
                throw new TimeoutException("The native session startup deadline elapsed.");
            return new(client, presentation, status.ParticipantStatus!);
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public void SetReady(bool ready) => SendControl(() => _client.SetReady(ready));
    public void SetMovement(long dx, long dy) => SendControl(() => _client.SetMovement(checked((int)dx), checked((int)dy)));
    public Task<WorldActionReceipt> SubmitAction(WorldActionCommand command) => _client.SubmitActionAsync(command);

    public async Task<LocalTradeInquiryView> SubmitTradeInquiryAsync(LocalTradeInquiry inquiry) =>
        (await QueryForeground(new(PlayerQueryKind.SubmitTradeInquiry, inquiry)).ConfigureAwait(false)).Inquiry!;
    public async Task<LocalTradeInquiryView?> CancelTradeInquiryAsync() =>
        (await QueryForeground(new(PlayerQueryKind.CancelTradeInquiry)).ConfigureAwait(false)).Inquiry;
    public async Task<PaidQuoteView?> InspectPaidQuoteAsync(EntityId quoteId) =>
        (await QueryForeground(new(PlayerQueryKind.InspectPaidQuote, subjectId: quoteId)).ConfigureAwait(false)).PaidQuote;
    public async Task<MaterialProposalView?> InspectMaterialProposalAsync(EntityId proposalId) =>
        (await QueryForeground(new(PlayerQueryKind.InspectMaterialProposal, subjectId: proposalId)).ConfigureAwait(false)).MaterialProposal;

    private Task<PlayerQueryResult> QueryForeground(PlayerQuery query)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            if (_foregroundPending)
                return Task.FromException<PlayerQueryResult>(new PlayerRequestRefusedException(PlayerRequestRefusal.Busy));
            _foregroundPending = true;
            Task<PlayerQueryResult> task = ExecuteForegroundAsync(query);
            _foreground = task;
            return task;
        }
    }

    private async Task<PlayerQueryResult> ExecuteForegroundAsync(PlayerQuery query)
    {
        try
        {
            await _querySlot.WaitAsync(_stop.Token).ConfigureAwait(false);
            try
            {
                PlayerQueryResult result = await _client.QueryAsync(query).ConfigureAwait(false);
                if (query.Kind is PlayerQueryKind.SubmitTradeInquiry or PlayerQueryKind.CancelTradeInquiry)
                    Volatile.Write(ref _status, new PlayerParticipantStatus(ReadRunState(), result.Inquiry));
                return result;
            }
            finally { _querySlot.Release(); }
        }
        finally { lock (_gate) _foregroundPending = false; }
    }

    private async Task RefreshAsync()
    {
        using var timer = new PeriodicTimer(RefreshInterval);
        while (await timer.WaitForNextTickAsync(_stop.Token).ConfigureAwait(false))
        {
            lock (_gate)
            {
                if (_foregroundPending || !_querySlot.Wait(0)) continue;
            }
            try
            {
                var result = await _client.QueryAsync(new(PlayerQueryKind.ReadParticipantStatus)).ConfigureAwait(false);
                Volatile.Write(ref _status, result.ParticipantStatus!);
            }
            finally { _querySlot.Release(); }
        }
    }

    private async Task RunAsync()
    {
        Task refresh = RefreshAsync();
        try
        {
            await await Task.WhenAny(refresh, _client.Completion).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        finally
        {
            Close();
            await _client.DisposeAsync().ConfigureAwait(false);
            try { await refresh.ConfigureAwait(false); }
            catch when (_stop.IsCancellationRequested) { }
            Task? foreground;
            lock (_gate) foreground = _foreground;
            if (foreground is not null)
            {
                try { await foreground.ConfigureAwait(false); }
                catch { /* The query's original caller retains its failure. */ }
            }
            Volatile.Write(ref _status, new PlayerParticipantStatus(LocalWorldRunState.Unavailable, ReadTradeInquiry()));
            _querySlot.Dispose();
            _stop.Dispose();
        }
    }

    private void SendControl(Action send)
    {
        try { send(); }
        catch (Exception error) when (error is IOException or FormatException or TimeoutException)
        { throw new InvalidOperationException("The player connection has stopped.", error); }
    }

    private void Close()
    {
        lock (_gate)
        {
            if (_closed) return;
            _closed = true;
            _stop.Cancel();
        }
    }

    public async ValueTask DisposeAsync()
    {
        Close();
        await _client.DisposeAsync().ConfigureAwait(false);
        try { await _completion.ConfigureAwait(false); }
        catch { /* Terminal errors remain observable through Completion. */ }
    }
}
