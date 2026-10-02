using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace OpenFantasy.Hosting.Diagnostics;

/// <summary>
/// Opt-in wall-clock diagnostics outside canonical state. Owners use opaque per-instance diagnostic
/// identifiers; bindings may also use actor/world identity strings. Stages must be fixed labels,
/// never credentials, display names, requests or world contents.
/// </summary>
[EventSource(Name = SourceName)]
public sealed class HostTimingEvents : EventSource
{
    public const string SourceName = "OpenFantasy-Host-Timing";
    public static HostTimingEvents Log { get; } = new();

    private HostTimingEvents() { }

    [NonEvent]
    public void Stage(string ownerId, string stage, long correlation = 0, long value = 0)
    {
        if (!IsEnabled()) return;
        StageRecorded(ownerId, stage, correlation, value, Stopwatch.GetTimestamp());
    }

    [NonEvent]
    public void Bind(string ownerId, string relatedOwnerId)
    {
        if (!IsEnabled()) return;
        BindingRecorded(ownerId, relatedOwnerId, Stopwatch.GetTimestamp());
    }

    [Event(1, Level = EventLevel.Informational)]
    private void StageRecorded(string ownerId, string stage, long correlation, long value, long timestamp) =>
        WriteEvent(1, ownerId, stage, correlation, value, timestamp);

    [Event(2, Level = EventLevel.Informational)]
    private void BindingRecorded(string ownerId, string relatedOwnerId, long timestamp) =>
        WriteEvent(2, ownerId, relatedOwnerId, timestamp);
}
