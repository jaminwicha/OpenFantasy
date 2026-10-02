using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using OpenFantasy.Hosting.Diagnostics;

namespace OpenFantasy.Server.Tests.Diagnostics;

public sealed class HostTimingEventsTests
{
    [Fact]
    public void Stage_and_binding_preserve_diagnostic_identity_and_capture_monotonic_timestamps()
    {
        string owner = Guid.NewGuid().ToString("N");
        string related = Guid.NewGuid().ToString("N");
        using var listener = new MatchingListener(owner);
        Assert.Equal("OpenFantasy-Host-Timing", HostTimingEvents.Log.Name);
        long before = Stopwatch.GetTimestamp();
        HostTimingEvents.Log.Stage(owner, "test-stage", correlation: 42, value: 17);
        HostTimingEvents.Log.Bind(owner, related);
        long after = Stopwatch.GetTimestamp();

        var stage = Assert.Single(listener.Events, item => item.Id == 1);
        Assert.Equal("StageRecorded", stage.Name);
        Assert.Equal(owner, Assert.IsType<string>(stage.Payload[0]));
        Assert.Equal("test-stage", Assert.IsType<string>(stage.Payload[1]));
        Assert.Equal(42L, Assert.IsType<long>(stage.Payload[2]));
        Assert.Equal(17L, Assert.IsType<long>(stage.Payload[3]));
        long stageTimestamp = Assert.IsType<long>(stage.Payload[4]);
        Assert.InRange(stageTimestamp, before, after);

        var binding = Assert.Single(listener.Events, item => item.Id == 2);
        Assert.Equal("BindingRecorded", binding.Name);
        Assert.Equal(owner, Assert.IsType<string>(binding.Payload[0]));
        Assert.Equal(related, Assert.IsType<string>(binding.Payload[1]));
        Assert.InRange(Assert.IsType<long>(binding.Payload[2]), stageTimestamp, after);
    }

    private sealed class MatchingListener : EventListener
    {
        private readonly string _owner;
        internal ConcurrentQueue<CapturedEvent> Events { get; } = new();

        internal MatchingListener(string owner)
        {
            _owner = owner;
            EnableEvents(HostTimingEvents.Log, EventLevel.Informational);
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.Payload is { Count: > 0 } payload && payload[0] is string owner && owner == _owner)
                Events.Enqueue(new CapturedEvent(eventData.EventId, eventData.EventName, payload.ToArray()));
        }
    }

    private sealed record CapturedEvent(int Id, string? Name, object?[] Payload);
}
