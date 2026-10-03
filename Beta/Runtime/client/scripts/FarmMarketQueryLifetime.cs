using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>The exact displayed shopper, panel visit and stall that admitted a query.</summary>
internal sealed record FarmMarketQueryContext(FarmMarketScope Scope, ulong Lifetime, EntityId? MarketId);

/// <summary>
/// Bookkeeping for admitted market queries. The panel owns visit identities, the host owns inquiry state,
/// and this owner prevents late completions from changing a different visit or swallowing its initial read.
/// </summary>
internal sealed class FarmMarketQueryLifetime
{
    private readonly HashSet<Task> _admitted = [];
    private FarmMarketScope? _inquiryScope;
    private LocalTradeInquiryView? _inquiry;
    private long _nextRead;
    private bool _readRequested;
    private bool _refreshFailed;

    internal void OpenVisit()
    {
        _readRequested = true;
        _refreshFailed = false;
        _nextRead = 0;
    }

    internal LocalTradeInquiryView? InquiryFor(FarmMarketScope scope) => _inquiryScope == scope ? _inquiry : null;

    internal bool ShouldRead(FarmMarketScope scope, long now) => !_refreshFailed && now >= _nextRead
        && (_readRequested || _inquiryScope != scope || _inquiry?.State == LocalTradeInquiryState.Waiting);

    internal void ReadStarted(long now) => _nextRead = checked(now + 500);

    internal bool CompleteInquiry(FarmMarketQueryContext captured, FarmMarketQueryContext? displayed,
        LocalTradeInquiryView? inquiry, bool failed, long now)
    {
        if (captured != displayed) return false;
        _readRequested = false;
        _refreshFailed = failed;
        _nextRead = checked(now + 500);
        if (!failed) { _inquiryScope = captured.Scope; _inquiry = inquiry; }
        return true;
    }

    internal Task<T> Track<T>(Task<T> task)
    {
        _admitted.Add(task);
        return task;
    }

    /// <summary>The caller has obtained this task's result or observed its failure.</summary>
    internal void Observed(Task task) => _admitted.Remove(task);

    /// <summary>Captures all admitted facade work before shutdown; completion never accesses presentation.</summary>
    internal async Task DrainAsync()
    {
        Task[] admitted = _admitted.ToArray();
        try { await Task.WhenAll(admitted).ConfigureAwait(false); }
        catch
        {
            foreach (Task task in admitted) if (task.IsFaulted) _ = task.Exception;
        }
    }
}
