namespace OpenFantasy.Server;

/// <summary>Bounds accepted request work and closes admission atomically before shutdown joins its remaining work.</summary>
internal sealed class PlayerWebSocketRequests
{
    private readonly object _gate = new();
    private readonly HashSet<TaskCompletionSource> _requests = [];
    private bool _closed;

    internal bool TryEnter(out TaskCompletionSource? completion)
    {
        lock (_gate)
        {
            if (_closed || _requests.Count == PlayerWebSocketServer.MaximumPlayerRequests)
            {
                completion = null;
                return false;
            }
            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _requests.Add(completion);
            return true;
        }
    }

    internal void Leave(TaskCompletionSource completion)
    {
        lock (_gate)
        {
            _requests.Remove(completion);
            completion.TrySetResult();
        }
    }

    internal Task CloseAndJoin()
    {
        lock (_gate)
        {
            _closed = true;
            return Task.WhenAll(_requests.Select(request => request.Task));
        }
    }
}
