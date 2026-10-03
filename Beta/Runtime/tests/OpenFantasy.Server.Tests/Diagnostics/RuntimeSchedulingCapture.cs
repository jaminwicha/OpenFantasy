using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenFantasy.Hosting.Diagnostics;

namespace OpenFantasy.Server.Tests.Diagnostics;

/// <summary>
/// Opt-in test-process sampling at existing timing events. Counters describe the whole process,
/// not the emitting thread or stage; they are cumulative and are read separately, not atomically.
/// Sampling itself adds overhead. This listener neither schedules a timer nor changes host policy.
/// </summary>
internal static class RuntimeSchedulingCapture
{
    private static Capture? _capture;

    [ModuleInitializer]
    internal static void Initialize()
    {
        try
        {
            string? directory = Environment.GetEnvironmentVariable("OPENFANTASY_RUNTIME_SCHEDULING");
            if (string.IsNullOrWhiteSpace(directory)) return;
            if (!Path.IsPathFullyQualified(directory) || !Directory.Exists(directory))
            {
                ReportFailure("disabled: OPENFANTASY_RUNTIME_SCHEDULING must name an existing absolute output directory");
                return;
            }

            _capture = new Capture(Path.GetFullPath(directory));
            AppDomain.CurrentDomain.ProcessExit += (_, _) => _capture?.Finish();
            _capture.Start();
        }
        catch (Exception exception)
        {
            _capture?.Stop();
            ReportFailure($"initialization failed ({exception.GetType().Name})");
        }
    }

    private static void ReportFailure(string message)
    {
        try { Console.Error.WriteLine($"Runtime scheduling capture {message}."); }
        catch (Exception) { /* Diagnostic output must not replace a test-process result. */ }
    }

    private sealed class Capture(string outputDirectory)
    {
        private const int Capacity = 32_768;
        private const int MaximumOwnerLength = 128;
        private readonly CapturedEvent?[] _events = new CapturedEvent?[Capacity];
        private readonly string _captureId = Guid.NewGuid().ToString("N");
        private readonly DateTimeOffset _startedUtc = DateTimeOffset.UtcNow;
        private readonly long _startedTimestamp = Stopwatch.GetTimestamp();
        private TimingListener? _listener;
        private long _claimed;
        private long _dropped;
        private long _invalid;
        private long _samplingFailures;
        private long _listenerFailures;
        private int _stopped;
        private int _finished;

        internal void Start()
        {
            _listener = new TimingListener();
            _listener.Start(this);
        }

        internal void Record(EventWrittenEventArgs data)
        {
            if (Volatile.Read(ref _stopped) != 0 || data.EventId != 1) return;
            try
            {
                if (data.Payload is not { Count: 5 } payload || payload[0] is not string owner
                    || payload[1] is not string stage || payload[2] is not long correlation
                    || payload[3] is not long value || payload[4] is not long timestamp
                    || owner.Length > MaximumOwnerLength)
                {
                    Interlocked.Increment(ref _invalid);
                    return;
                }
                if (!Selected(stage)) return;

                long index = Interlocked.Increment(ref _claimed) - 1;
                if (index >= Capacity)
                {
                    Interlocked.Increment(ref _dropped);
                    return;
                }

                long samplingStarted = Stopwatch.GetTimestamp();
                int thread = Environment.CurrentManagedThreadId;
                long processCpuTicks = Environment.CpuUsage.TotalTime.Ticks;
                long pauseTicks = GC.GetTotalPauseDuration().Ticks;
                int generation0 = GC.CollectionCount(0);
                int generation1 = GC.CollectionCount(1);
                int generation2 = GC.CollectionCount(2);
                int poolThreads = ThreadPool.ThreadCount;
                long pending = ThreadPool.PendingWorkItemCount;
                long samplingCompleted = Stopwatch.GetTimestamp();
                Volatile.Write(ref _events[(int)index], new CapturedEvent(index, owner, stage, correlation,
                    value, timestamp, samplingStarted, samplingCompleted, thread, processCpuTicks,
                    pauseTicks, generation0, generation1, generation2, poolThreads, pending));
            }
            catch (Exception)
            {
                // An unfilled reserved slot is reported as incomplete; no partial counter tuple is published.
                Interlocked.Increment(ref _samplingFailures);
            }
        }

        private static bool Selected(string stage) => stage is
            "tick.wait" or "tick.begin" or "tick.end"
            or "movement.accepted" or "movement.age" or "movement.selected"
            or "observation.begin" or "observation.captured"
            or "replica.begin" or "replica.end"
            or "client.send.begin" or "client.send.end" or "server.input"
            or "server.send.begin" or "server.send.end";

        internal void ListenerFailed() => Interlocked.Increment(ref _listenerFailures);

        internal void Stop()
        {
            Interlocked.Exchange(ref _stopped, 1);
            try { _listener?.Dispose(); }
            catch (Exception) { ListenerFailed(); }
        }

        internal void Finish()
        {
            if (Interlocked.Exchange(ref _finished, 1) != 0) return;
            try
            {
                Stop();
                long endedTimestamp = Stopwatch.GetTimestamp();
                long claimed = Volatile.Read(ref _claimed);
                int retained = (int)Math.Min(claimed, Capacity);
                var completed = new List<CapturedEvent>(retained);
                for (int index = 0; index < retained; index++)
                {
                    CapturedEvent? item = Volatile.Read(ref _events[index]);
                    if (item is not null) completed.Add(item);
                }

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
                    TimeSpanTicksPerSecond = TimeSpan.TicksPerSecond,
                    TimestampBasis = "Event and sampling timestamps use Stopwatch.GetTimestamp; divide differences by TimestampFrequency",
                    CounterScope = "Process-wide cumulative CPU time and GC pause duration in TimeSpan ticks; collection counts are cumulative; thread-pool counts are instantaneous separate reads",
                    SamplingCaveat = "Counters are not an atomic snapshot or stage/thread CPU attribution. Sampling timestamp span excludes event delivery, filtering, record allocation and publication overhead",
                    ExitCaveat = "Reservation order is not timestamp order across threads. Process-exit copying does not wait for in-flight callbacks; incomplete slots and counters describe the observed copy",
                    Capacity,
                    MaximumOwnerLength,
                    OverflowPolicy = "Keep first Capacity selected events; discard subsequent selected events",
                    ClaimedEvents = claimed,
                    WrittenEvents = completed.Count,
                    DroppedEvents = Volatile.Read(ref _dropped),
                    IncompleteEvents = retained - completed.Count,
                    InvalidEvents = Volatile.Read(ref _invalid),
                    SamplingFailures = Volatile.Read(ref _samplingFailures),
                    ListenerFailures = Volatile.Read(ref _listenerFailures),
                    Events = completed
                };
                string fileName = $"runtime-scheduling-{Environment.ProcessId}-{_captureId}.json";
                using var stream = new FileStream(Path.Combine(outputDirectory, fileName), FileMode.CreateNew,
                    FileAccess.Write, FileShare.Read);
                JsonSerializer.Serialize(stream, report);
            }
            catch (Exception exception)
            {
                ReportFailure($"output failed ({exception.GetType().Name})");
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

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            // The base constructor may invoke this before Start supplies the capture.
            if (_owner is not null) EnableMatching(eventSource);
        }

        private void EnableMatching(EventSource source)
        {
            if (source.Name != HostTimingEvents.SourceName) return;
            try { EnableEvents(source, EventLevel.Informational); }
            catch (Exception) { _owner?.ListenerFailed(); }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData) => _owner?.Record(eventData);
    }

    private sealed record CapturedEvent(long Sequence, string OwnerId, string Stage, long Correlation,
        long Value, long EventTimestamp, long SamplingStartedTimestamp, long SamplingCompletedTimestamp,
        int ManagedThreadId, long ProcessCpuTimeTicks, long GcPauseTimeTicks, int Generation0Collections,
        int Generation1Collections, int Generation2Collections, int ThreadPoolThreads, long PendingWorkItems);
}
