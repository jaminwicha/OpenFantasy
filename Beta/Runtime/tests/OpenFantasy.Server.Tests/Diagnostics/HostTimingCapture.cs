using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenFantasy.Hosting.Diagnostics;

namespace OpenFantasy.Server.Tests.Diagnostics;

/// <summary>
/// Test-process-only diagnostics. The explicit environment variable grants one existing absolute
/// output directory; absence or an invalid directory never enables a listener or creates a directory.
/// </summary>
internal static class HostTimingCapture
{
    private static Capture? _capture;

    [ModuleInitializer]
    internal static void Initialize()
    {
        string? requestedDirectory = Environment.GetEnvironmentVariable("OPENFANTASY_HOST_TIMING");
        if (string.IsNullOrWhiteSpace(requestedDirectory)) return;
        try
        {
            if (!Path.IsPathFullyQualified(requestedDirectory) || !Directory.Exists(requestedDirectory))
            {
                Console.Error.WriteLine("Host timing capture disabled: OPENFANTASY_HOST_TIMING must name an existing absolute output directory.");
                return;
            }

            _capture = new Capture(Path.GetFullPath(requestedDirectory));
            AppDomain.CurrentDomain.ProcessExit += (_, _) => _capture?.Finish();
            _capture.Start();
        }
        catch (Exception exception)
        {
            _capture?.Stop();
            Console.Error.WriteLine($"Host timing capture initialization failed ({exception.GetType().Name}).");
        }
    }

    private sealed class Capture(string outputDirectory)
    {
        private const int Capacity = 131_072;
        private const int MaximumLabelLength = 128;
        private readonly CapturedEvent?[] _events = new CapturedEvent?[Capacity];
        private readonly long _startedTimestamp = Stopwatch.GetTimestamp();
        private readonly DateTimeOffset _startedUtc = DateTimeOffset.UtcNow;
        private readonly string _captureId = Guid.NewGuid().ToString("N");
        private TimingListener? _listener;
        private Timer? _timer;
        private long _claimed;
        private long _dropped;
        private long _invalid;
        private long _counterFailures;
        private int _stopped;
        private int _finished;

        internal void Start()
        {
            _listener = new TimingListener();
            _listener.Start(this);
            RecordCounters();
            _timer = new Timer(_ => RecordCounters(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        internal void Record(EventWrittenEventArgs data)
        {
            if (Volatile.Read(ref _stopped) != 0) return;
            var payload = data.Payload;
            if (data.EventId == 1 && payload is { Count: 5 } &&
                payload[0] is string owner && payload[1] is string stage &&
                payload[2] is long correlation && payload[3] is long value && payload[4] is long timestamp)
            {
                int index = Reserve();
                if (index >= 0)
                    Volatile.Write(ref _events[index], new CapturedEvent(index, "stage", timestamp,
                        Environment.CurrentManagedThreadId, Label(owner), Label(stage), null, correlation, value, null));
            }
            else if (data.EventId == 2 && payload is { Count: 3 } &&
                payload[0] is string bindingOwner && payload[1] is string related && payload[2] is long bindingTimestamp)
            {
                int index = Reserve();
                if (index >= 0)
                    Volatile.Write(ref _events[index], new CapturedEvent(index, "bind", bindingTimestamp,
                        Environment.CurrentManagedThreadId, Label(bindingOwner), null, Label(related), null, null, null));
            }
            else
            {
                Interlocked.Increment(ref _invalid);
            }
        }

        private void RecordCounters()
        {
            int index = Reserve();
            if (index < 0) return;
            try
            {
                long timestamp = Stopwatch.GetTimestamp();
                ThreadPool.GetAvailableThreads(out int availableWorkers, out int availableIo);
                ThreadPool.GetMaxThreads(out int maximumWorkers, out int maximumIo);
                var counters = new ProcessCounters(ThreadPool.ThreadCount, ThreadPool.PendingWorkItemCount,
                    ThreadPool.CompletedWorkItemCount, availableWorkers, availableIo, maximumWorkers, maximumIo,
                    GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), GC.GetTotalMemory(false),
                    GC.GetTotalAllocatedBytes(precise: false), Environment.WorkingSet);
                Volatile.Write(ref _events[index], new CapturedEvent(index, "counters", timestamp,
                    Environment.CurrentManagedThreadId, null, null, null, null, null, counters));
            }
            catch (Exception)
            {
                // A diagnostic timer must never terminate the test process if a platform counter fails.
                Interlocked.Increment(ref _counterFailures);
            }
        }

        private int Reserve()
        {
            if (Volatile.Read(ref _stopped) != 0) return -1;
            long index = Interlocked.Increment(ref _claimed) - 1;
            if (index < Capacity) return (int)index;
            Interlocked.Increment(ref _dropped);
            return -1;
        }

        private static string Label(string value) =>
            value.Length <= MaximumLabelLength ? value : value[..MaximumLabelLength];

        internal void Stop()
        {
            Interlocked.Exchange(ref _stopped, 1);
            _timer?.Dispose();
            _listener?.Dispose();
        }

        internal void Finish()
        {
            if (Interlocked.Exchange(ref _finished, 1) != 0) return;
            try
            {
                RecordCounters();
                Stop();
                long endedTimestamp = Stopwatch.GetTimestamp();
                long claimed = Volatile.Read(ref _claimed);
                var completed = new List<CapturedEvent>((int)Math.Min(claimed, Capacity));
                for (int index = 0; index < Math.Min(claimed, Capacity); index++)
                {
                    CapturedEvent? item = Volatile.Read(ref _events[index]);
                    if (item is not null) completed.Add(item);
                }

                // Sequence is reservation order, not timestamp order across concurrently executing threads.
                var report = new
                {
                    SchemaVersion = 1,
                    Source = HostTimingEvents.SourceName,
                    CaptureId = _captureId,
                    ProcessId = Environment.ProcessId,
                    ProcessName = Path.GetFileName(Environment.ProcessPath),
                    RuntimeVersion = Environment.Version.ToString(),
                    ProcessorCount = Environment.ProcessorCount,
                    StartedUtc = _startedUtc,
                    StartedTimestamp = _startedTimestamp,
                    EndedTimestamp = endedTimestamp,
                    TimestampFrequency = Stopwatch.Frequency,
                    TimestampBasis = "Stopwatch.GetTimestamp; elapsed seconds = (end - start) / TimestampFrequency",
                    Capacity,
                    MaximumLabelLength,
                    OverflowPolicy = "Keep first Capacity events; discard subsequent events",
                    ClaimedEvents = claimed,
                    WrittenEvents = completed.Count,
                    DroppedEvents = Volatile.Read(ref _dropped),
                    IncompleteEvents = Math.Min(claimed, Capacity) - completed.Count,
                    InvalidEvents = Volatile.Read(ref _invalid),
                    CounterFailures = Volatile.Read(ref _counterFailures),
                    Events = completed
                };
                string fileName = $"host-timing-{Environment.ProcessId}-{_captureId}.json";
                using var stream = new FileStream(Path.Combine(outputDirectory, fileName), FileMode.CreateNew,
                    FileAccess.Write, FileShare.Read);
                JsonSerializer.Serialize(stream, report, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
            }
            catch (Exception exception)
            {
                // Diagnostic failure must not replace the original test-process result.
                Console.Error.WriteLine($"Host timing capture output failed ({exception.GetType().Name}).");
            }
        }
    }

    private sealed class TimingListener : EventListener
    {
        private volatile Capture? _owner;

        internal void Start(Capture owner)
        {
            _owner = owner;
            foreach (EventSource source in EventSource.GetSources()) EnableMatching(source);
        }

        // EventListener's base constructor may invoke this before Start has supplied the owner.
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (_owner is not null) EnableMatching(eventSource);
        }

        private void EnableMatching(EventSource source)
        {
            if (source.Name == HostTimingEvents.SourceName)
                EnableEvents(source, EventLevel.Informational);
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData) => _owner?.Record(eventData);
    }

    private sealed record CapturedEvent(long Sequence, string Kind, long Timestamp, int ManagedThreadId,
        string? OwnerId, string? Stage, string? RelatedOwnerId, long? Correlation, long? Value, ProcessCounters? Counters);

    private sealed record ProcessCounters(int ThreadPoolThreads, long PendingWorkItems, long CompletedWorkItems,
        int AvailableWorkerThreads, int AvailableIoThreads, int MaximumWorkerThreads, int MaximumIoThreads,
        int Generation0Collections, int Generation1Collections, int Generation2Collections,
        long ManagedHeapBytes, long AllocatedBytes, long WorkingSetBytes);
}
