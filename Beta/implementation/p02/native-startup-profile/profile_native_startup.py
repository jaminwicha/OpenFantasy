#!/usr/bin/env python3
"""Measure the failed coordinated native reopen (commit 1581) in a disposable instrumented copy.

Diagnostic only. The retained failed save, its sidecars, the production runtime and its assemblies are
read-only inputs that are byte-pinned before and after. Instrumentation is inserted into a copy outside
Git, built there, and run through the unchanged native probe with the original no-focus arguments.
The 30 s opening, 150 s probe and 180 s process bounds are retained; a failed probe is kept as evidence.
Inclusive stage timings overlap by design and thread allocation is valid only on the measuring thread.
"""
import argparse
import datetime
import difflib
import hashlib
import json
import os
from pathlib import Path
import re
import resource
import shutil
import subprocess
import time
import traceback

PACKET = Path(__file__).resolve().parent
REPO = PACKET.parents[3]
RUNTIME = REPO / 'Beta/Runtime'
QUALIFICATION = REPO / 'Beta/implementation/p02/coordinated-native/qualification'
FAILED = QUALIFICATION / 'native-restorative-wwx4qw_9'
FIXTURE = QUALIFICATION / 'fixtures-integration-fixture-02/coordinated-withdrawal'
SOURCE_DATABASE = FAILED / 'coordinated-revoke.sqlite3'
SOURCE_ACTIVE = FAILED / 'coordinated-revoke.active.json'
SOURCE_SHA256 = '8598a2da449339e08ef2a5c4370bbf4ea07cd34fdc645bc9ef418f974c87806d'
COMMIT = 1581
COMMIT_HASH = '63c0f4517e92fbc0c0da8e9c5fe4651b0203b87e427c6f602f0469b886e037da'
DOTNET = Path('/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet')
GODOT = Path('/Users/alucinor/.local/share/openfantasy/toolchains/godot-4.6/Godot_mono.app/Contents/MacOS/Godot')
ORIGINAL_RUN = dict(godotSha256='be1daafcc08937e4693ca310b5a1199b123b5c5ab127c13142c53c0017f4e951',
                    dotnetSha256='00c673767ee2538369f307d3c8a0ce06f16bc9e0ecf8ce767b64dd02b2230e98',
                    projectSha256='a170d5c6b9e7fde1e8d68c23fe1c3b579096e1f37e27c2d0416d28fabcd9539a',
                    reopenDurationMilliseconds=30179, reopenProcessSeconds=34.373, hostDotnet10Seconds=20.994)
LIMITS = dict(buildSeconds=180, openingSeconds=30, probeSeconds=150, processSeconds=180, hostSeconds=90)
RETAINED_LOG_BYTES = 24000
P = 'global::OpenFantasy.Core.NativeStartupProbe.'

PROBE = r'''#pragma warning disable
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace OpenFantasy.Core;

/// <summary>Disposable startup diagnostic. It exists only in the instrumented copy.</summary>
public static class NativeStartupProbe
{
    private sealed class Total
    {
        public long Calls, Ticks, Bytes, CpuNs, MaxTicks;
        public List<long>? Series;
        public readonly HashSet<int> Threads = new();
    }

    private sealed class OpenScope
    {
        public string Stage = "";
        public long Started;
        public int Thread;
    }

    private static readonly object Gate = new();
    private static readonly object FlushGate = new();
    private static readonly Dictionary<string, Total> Totals = new();
    private static readonly Dictionary<long, OpenScope> OpenScopes = new();
    private static readonly List<object> Marks = new();
    private static readonly List<object> Timeline = new();
    private static readonly Dictionary<string, long> Counters = new();
    private static readonly HashSet<string> Once = new();
    private static readonly long Origin = Stopwatch.GetTimestamp();
    private static readonly DateTime OriginUtc = DateTime.UtcNow;
    private static readonly string? ReportPath = Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_STARTUP_REPORT");
    private static long _nextId;
    private static bool _clockFailed;
    private static string? _start;

    [DllImport("libSystem.dylib")]
    private static extern ulong clock_gettime_nsec_np(int clock);

    static NativeStartupProbe()
    {
        if (ReportPath is null) return;
        try { AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush("process-exit"); } catch { }
        var flusher = new Thread(() =>
        {
            while (true)
            {
                Thread.Sleep(500);
                Sample();
                Flush("periodic");
            }
        }) { IsBackground = true, Name = "native-startup-probe" };
        flusher.Start();
    }

    // macOS clock identifiers: 12 is process CPU time and 16 is the calling thread's CPU time.
    private static long Clock(int clock)
    {
        if (_clockFailed) return -1;
        try { return (long)clock_gettime_nsec_np(clock); }
        catch { _clockFailed = true; return -1; }
    }

    private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    private static object State(string name) => new
    {
        name,
        ms = Ms(Stopwatch.GetTimestamp() - Origin),
        thread = Environment.CurrentManagedThreadId,
        processCpuMs = Clock(12) / 1e6,
        gen0 = GC.CollectionCount(0),
        gen1 = GC.CollectionCount(1),
        gen2 = GC.CollectionCount(2),
        gcPauseMs = GC.GetTotalPauseDuration().TotalMilliseconds,
        allocatedBytes = GC.GetTotalAllocatedBytes(false),
        jitMs = JitInfo.GetCompilationTime(false).TotalMilliseconds,
        jitMethods = JitInfo.GetCompiledMethodCount(false)
    };

    private static string Start()
    {
        if (_start is not null) return _start;
        try
        {
            using Process process = Process.GetCurrentProcess();
            _start = process.StartTime.ToUniversalTime().ToString("O");
        }
        catch { _start = "unavailable"; }
        return _start;
    }

    private static object Runtime() => new
    {
        framework = RuntimeInformation.FrameworkDescription,
        version = Environment.Version.ToString(),
        runtimeIdentifier = RuntimeInformation.RuntimeIdentifier,
        architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        os = RuntimeInformation.OSDescription,
        coreLibrary = typeof(object).Assembly.Location,
        processPath = Environment.ProcessPath,
        processorCount = Environment.ProcessorCount,
        serverGc = GCSettings.IsServerGC,
        gcLatency = GCSettings.LatencyMode.ToString(),
        debugger = Debugger.IsAttached,
        tieredCompilation = AppContext.TryGetSwitch("System.Runtime.TieredCompilation", out bool tiered) ? tiered.ToString() : "default",
        tieredPgo = AppContext.TryGetSwitch("System.Runtime.TieredPGO", out bool pgo) ? pgo.ToString() : "default",
        environment = new[] { "DOTNET_TieredCompilation", "DOTNET_TieredPGO", "DOTNET_TC_QuickJitForLoops",
            "DOTNET_ReadyToRun", "DOTNET_gcServer", "DOTNET_gcConcurrent" }
            .ToDictionary(name => name, name => Environment.GetEnvironmentVariable(name)),
        assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith("OpenFantasy") == true)
            .Select(assembly => new
            {
                name = assembly.GetName().Name,
                location = assembly.IsDynamic ? "" : assembly.Location,
                jitOptimizerDisabled = assembly.GetCustomAttributes(typeof(DebuggableAttribute), false)
                    .OfType<DebuggableAttribute>().FirstOrDefault()?.IsJITOptimizerDisabled
            }).ToArray()
    };

    public static void Mark(string name)
    {
        if (ReportPath is null) return;
        object state = State(name);
        lock (Gate) Marks.Add(state);
        Flush("mark");
    }

    public static void MarkOnce(string name)
    {
        if (ReportPath is null) return;
        lock (Gate)
        {
            if (!Once.Add(name)) return;
        }
        Mark(name);
    }

    public static void Tick(string counter)
    {
        if (ReportPath is null) return;
        lock (Gate) Counters[counter] = Counters.GetValueOrDefault(counter) + 1;
    }

    private static void Sample()
    {
        try
        {
            long prepared;
            Dictionary<string, long> counters;
            lock (Gate)
            {
                prepared = Totals.TryGetValue("commit-preparation", out Total? total) ? total.Calls : 0;
                counters = new Dictionary<string, long>(Counters);
            }
            object sample = new { state = State("sample"), commitsPrepared = prepared, counters,
                threadPoolThreads = ThreadPool.ThreadCount };
            lock (Gate) Timeline.Add(sample);
        }
        catch { }
    }

    public static void Flush(string reason)
    {
        if (ReportPath is null) return;
        try
        {
            lock (FlushGate)
            {
                object snapshot;
                long now = Stopwatch.GetTimestamp();
                lock (Gate)
                {
                    snapshot = new
                    {
                        kind = "native-startup-probe-v1",
                        reason,
                        originUtc = OriginUtc.ToString("O"),
                        processStartUtc = Start(),
                        state = State("flush"),
                        runtime = Runtime(),
                        gcHeapBytes = GC.GetGCMemoryInfo().HeapSizeBytes,
                        threadClockAvailable = !_clockFailed,
                        stages = Totals.OrderByDescending(pair => pair.Value.Ticks).Select(pair => new
                        {
                            stage = pair.Key,
                            calls = pair.Value.Calls,
                            inclusiveMs = Ms(pair.Value.Ticks),
                            threadCpuMs = pair.Value.CpuNs / 1e6,
                            maxMs = Ms(pair.Value.MaxTicks),
                            threadAllocatedBytes = pair.Value.Bytes,
                            threads = pair.Value.Threads.ToArray(),
                            seriesMs = pair.Value.Series?.Select(value => Math.Round(Ms(value), 3)).ToArray()
                        }).ToArray(),
                        open = OpenScopes.Values.Select(scope => new
                        {
                            stage = scope.Stage,
                            elapsedMs = Ms(now - scope.Started),
                            thread = scope.Thread
                        }).ToArray(),
                        marks = Marks.ToArray(),
                        counters = new Dictionary<string, long>(Counters),
                        timeline = Timeline.ToArray()
                    };
                }
                string temporary = ReportPath + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(snapshot));
                File.Move(temporary, ReportPath, true);
            }
        }
        catch { }
    }

    public static Scope Begin(string stage, bool tracked = false, int series = 0) => new(stage, tracked, series);

    public readonly struct Scope : IDisposable
    {
        private readonly string? _stage;
        private readonly long _started;
        private readonly long _allocated;
        private readonly long _cpu;
        private readonly long _id;
        private readonly int _series;

        public Scope(string stage, bool tracked, int series)
        {
            _stage = ReportPath is null ? null : stage;
            _series = series;
            _id = 0;
            _started = 0;
            _allocated = 0;
            _cpu = 0;
            if (_stage is null) return;
            if (tracked)
            {
                _id = Interlocked.Increment(ref _nextId);
                lock (Gate)
                {
                    OpenScopes[_id] = new OpenScope { Stage = stage, Started = Stopwatch.GetTimestamp(),
                        Thread = Environment.CurrentManagedThreadId };
                }
                Mark(stage + ":begin");
            }
            _allocated = GC.GetAllocatedBytesForCurrentThread();
            _cpu = Clock(16);
            _started = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            if (_stage is null) return;
            long ticks = Stopwatch.GetTimestamp() - _started;
            long cpu = _cpu < 0 ? -1 : Clock(16) - _cpu;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - _allocated;
            lock (Gate)
            {
                if (!Totals.TryGetValue(_stage, out Total? total)) Totals.Add(_stage, total = new Total());
                total.Calls++;
                total.Ticks += ticks;
                total.Bytes += bytes;
                if (cpu >= 0) total.CpuNs += cpu;
                if (ticks > total.MaxTicks) total.MaxTicks = ticks;
                total.Threads.Add(Environment.CurrentManagedThreadId);
                if (_series > 0)
                {
                    total.Series ??= new List<long>();
                    if (total.Series.Count < _series) total.Series.Add(ticks);
                }
                if (_id != 0) OpenScopes.Remove(_id);
            }
            if (_id != 0) Mark(_stage + ":end");
        }
    }
}
'''


def sig(prefix):
    """A block-bodied member whose signature starts with prefix; expression bodies never match."""
    return re.escape(prefix) + r'(?:(?!=>)[^;{}])*?\)\n    \{'


def scope(label, tracked=False, series=0, before=''):
    return ('append', '\n        ' + before + 'using var nativeStartupCost = ' + P
            + 'Begin("%s", %s, %d);' % (label, 'true' if tracked else 'false', series))


def call(text):
    return ('append', '\n        ' + text)


H, C, G = 'src/OpenFantasy.Hosting/', 'src/OpenFantasy.Core/', 'client/scripts/'
WORLD = 'internal static void Validate(WorldSnapshot world'
ANCHORS = [
    (H + 'Worlds/WorldJournalRecovery.cs', sig('private static ReplayedWorld Replay('), scope('full-replay', True), True),
    (H + 'Worlds/WorldJournalRecovery.cs', sig('private static ExpectedJournalEntry PrepareEntry('), scope('replay-prepare-entry'), True),
    (H + 'Worlds/WorldJournalRecovery.cs', sig('private static void VerifyCommandTable('), scope('verify-command-table'), False),
    (H + 'Worlds/WorldJournalRecovery.cs', sig('private static void VerifyIdentityTable('), scope('verify-identity-table'), False),
    (H + 'Worlds/WorldCommitPreparation.cs', sig('internal PreparedWorldCommit Prepare('), scope('commit-preparation', False, 4096), True),
    (H + 'Worlds/SqliteWorldAuthority.cs', sig('public static SqliteWorldAuthority OpenExisting(string path, ReadOnlySpan<byte> expectedGenesis'),
     scope('authority-open-existing', True), True),
    (H + 'Worlds/SqliteWorldAuthority.cs', sig('public WorldInspection InspectForHost('), scope('inspect-for-host'), True),
    (H + 'Worlds/SqliteWorldAuthority.cs', sig('internal WorldSnapshot ReadStateForHost('), scope('read-state-for-host'), False),
    (H + 'Worlds/WorldDatabase.cs', sig('internal static WorldDatabase Open('), scope('database-open'), False),
    (H + 'Worlds/WorldGenesisAdmission.cs', sig('internal static void Validate(WorldSnapshot genesis'), scope('genesis-admission'), False),
    (H + 'Worlds/WorldResourceArchiveReplay.cs', sig('internal void Verify('), scope('resource-archive-verify'), False),
    (H + 'Worlds/WorldMaterialCareArchiveReplay.cs', sig('internal void Verify('), scope('material-care-archive-verify'), False),
    (H + 'Simulation/LocalWorldSession.cs', sig('internal LocalWorldSession(SqliteWorldAuthority authority, EntityId playerActorId,'),
     scope('local-world-session-constructor', True), True),
    (H + 'Simulation/LocalWorldSession.cs', r'        _view = ProjectView\(state\);\n        _loop = Task\.Run\(RunAsync\);',
     ('replace', '        using (' + P + 'Begin("initial-view-projection", true))\n        {\n            _view = ProjectView(state);\n        }\n'
      '        _loop = Task.Run(RunAsync);'), True),
    (C + 'Worlds/WorldSnapshotCodec.cs', sig('public static byte[] Encode(WorldSnapshot state'), scope('world-encode'), True),
    (C + 'Worlds/WorldCanonicalFrame.cs', sig('public static WorldCanonicalFrame Capture(WorldSnapshot world'), scope('canonical-frame-capture'), False),
    (C + 'Worlds/WorldStepProcessor.cs', sig('private WorldStepDecision DecideSelected('), scope('step-decide'), False),
    (C + 'Worlds/WorldSnapshot.cs', sig('private WorldSnapshot('), scope('world-root'), False),
    (C + 'Worlds/WorldAdmissionValidator.cs', sig('public static WorldIdentityIndex Validate('), scope('world-identities'), False),
    (C + 'Worlds/WorldResourceAdmission.cs', sig(WORLD), scope('resource-causes'), False),
    (C + 'Worlds/WorldCareAdmission.cs', sig(WORLD), scope('care-admission'), False),
    (C + 'Worlds/WorldRestorativeAdmission.cs', sig(WORLD), scope('ordinary-care'), False),
    (C + 'Worlds/WorldCoordinatedAdmission.cs', sig(WORLD), scope('coordinated-admission'), False),
    (C + 'Worlds/WorldSourceIssuanceBaseline.cs', sig('internal void ValidatePartition(WorldSnapshot world'), scope('source-partition'), False),
    (C + 'Production/Continuation/ProductionContinuationValidation.cs', sig('internal static void Validate(ProductionSnapshot state'),
     scope('production-continuation'), False),
    (G + 'FarmClientSession.cs', r'OpenCoordinatedFixtureAsync\(string savePath, string expectedGenesisPath\) => Task\.Run\(\(\) =>\n    \{',
     scope('open-coordinated-fixture', True), True),
    (G + 'FarmClientSession.cs', sig('private static FarmClientSession Attach(SqliteWorldAuthority authority,'), scope('client-attach', True), True),
    (G + 'FarmGame.cs', sig('public override void _Ready('), scope('game-ready', True), True),
    (G + 'FarmGame.cs', sig('public override void _Process(double delta'),
     call(P + 'Tick("process-frame"); ' + P + 'MarkOnce("first-process-frame");'), True),
    (G + 'FarmGame.cs', r'if \(_opening is \{ IsCompleted: true \}\)\n        \{', call(P + 'Mark("opening-task-observed-complete");'), True),
    (G + 'FarmGame.cs', sig('private void Present(LocalWorldView view'),
     scope('present', False, 64, P + 'MarkOnce("first-present-begin"); '), True),
    (G + 'FarmGame.cs', r'        _presented = _session\.CreateOffers\(view, _targets\.Selected\);',
     ('replace', '        using (' + P + 'Begin("create-offers", false, 64))\n        {\n'
      '            _presented = _session.CreateOffers(view, _targets.Selected);\n        }\n'
      '        if (_presented.Count > 0) ' + P + 'MarkOnce("first-offers-available");\n'
      '        if (view.Restorative is not null && _presented.Count > 0) ' + P + 'MarkOnce("probe-open-condition-satisfied");'), True),
    (G + 'Bootstrap.cs', sig('public override async void _Ready('), call(P + 'Mark("bootstrap-ready-begin");'), True),
]
WINDOWS = dict(
    bootstrapToGameReady=('bootstrap-ready-begin', 'game-ready:begin'),
    openingToProbeCondition=('game-ready:begin', 'probe-open-condition-satisfied'),
    openTask=('open-coordinated-fixture:begin', 'open-coordinated-fixture:end'),
    authorityOpen=('authority-open-existing:begin', 'authority-open-existing:end'),
    fullReplay=('full-replay:begin', 'full-replay:end'),
    clientAttach=('client-attach:begin', 'client-attach:end'),
    sessionConstructor=('local-world-session-constructor:begin', 'local-world-session-constructor:end'),
    initialView=('initial-view-projection:begin', 'initial-view-projection:end'),
    taskEndToObserved=('open-coordinated-fixture:end', 'opening-task-observed-complete'),
    observedToProbeCondition=('opening-task-observed-complete', 'probe-open-condition-satisfied'))


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def digest(path):
    value = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1 << 20), b''):
            value.update(block)
    return value.hexdigest()


def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def source_files():
    roots = [(RUNTIME / 'src', ('bin', 'obj')), (RUNTIME / 'client', ('.godot',))]
    files = [RUNTIME / name for name in ('Directory.Build.props', 'global.json', 'NuGet.Config')]
    for root, excluded in roots:
        for path in root.rglob('*'):
            if path.is_file() and not any(part in excluded for part in path.relative_to(root).parts):
                files.append(path)
    return sorted(files)


def source_hashes():
    hashes = {str(path.relative_to(RUNTIME)): digest(path) for path in source_files()}
    combined = hashlib.sha256(''.join(name + '\0' + value + '\n' for name, value in sorted(hashes.items())).encode()).hexdigest()
    return hashes, combined


def pins():
    paths = [SOURCE_DATABASE, Path(str(SOURCE_DATABASE) + '-wal'), Path(str(SOURCE_DATABASE) + '-shm'), SOURCE_ACTIVE,
             FAILED / 'coordinated-revoke.reopen.failed.json', FIXTURE / 'metadata.json', FIXTURE / 'genesis.ofc',
             RUNTIME / 'client/project.godot', RUNTIME / 'client/scripts/RestorativePlayProbe.cs', GODOT, DOTNET]
    paths += [RUNTIME / directory / ('OpenFantasy.' + assembly + '.dll')
              for directory, assemblies in [('src/OpenFantasy.Host/bin/Debug/net10.0', ('Host', 'Core', 'Hosting')),
                                            ('client/.godot/mono/temp/bin/Debug', ('Client', 'Core', 'Hosting'))]
              for assembly in assemblies]
    return {str(path): dict(sha256=digest(path), bytes=path.stat().st_size) if path.exists() else None for path in paths}


def pinned_copy(destination):
    wal = Path(str(SOURCE_DATABASE) + '-wal')
    require(not wal.exists() or wal.stat().st_size == 0, 'The source WAL is nonempty; the retained save is not a closed copy.')
    require(digest(SOURCE_DATABASE) == SOURCE_SHA256, 'The retained failed save no longer has its pinned bytes.')
    shutil.copyfile(SOURCE_DATABASE, destination)
    require(digest(destination) == SOURCE_SHA256 and digest(SOURCE_DATABASE) == SOURCE_SHA256
            and (not wal.exists() or wal.stat().st_size == 0), 'The save changed while it was copied.')


def instrument(copy, output):
    applied, skipped, diffs = [], [], []
    originals = {}
    for relative, pattern, (action, payload), required in ANCHORS:
        path = copy / relative
        label = re.search(r'"([^"]+)"', payload).group(1)
        if not path.exists():
            require(not required, 'Missing instrumented source ' + relative)
            skipped.append(dict(source=relative, stage=label, reason='file absent'))
            continue
        text = path.read_text()
        originals.setdefault(relative, text)
        matches = list(re.finditer(pattern, text))
        if len(matches) != 1:
            require(not required, 'Expected one anchor for %s in %s, found %d' % (label, relative, len(matches)))
            skipped.append(dict(source=relative, stage=label, reason='%d anchor matches' % len(matches)))
            continue
        match = matches[0]
        start = match.end() if action == 'append' else match.start()
        path.write_text(text[:start] + payload + text[match.end():])
        applied.append(dict(source=relative, stage=label, action=action, anchor=pattern,
                            anchorLine=text.count('\n', 0, match.start()) + 1))
    for relative, original in originals.items():
        diffs += list(difflib.unified_diff(original.splitlines(), (copy / relative).read_text().splitlines(),
                                           'a/' + relative, 'b/' + relative, n=1, lineterm=''))
    (copy / 'src/OpenFantasy.Core/NativeStartupProbe.cs').write_text(PROBE)
    (output / 'instrumentation.diff').write_text('\n'.join(diffs) + '\n')
    return applied, skipped


def execute(report, output, label, command, cwd, env, added, bound):
    bound = min(bound, report['deadline'] - time.monotonic())
    require(bound > 5, 'The aggregate assignment bound is exhausted before ' + label)
    entry = dict(label=label, command=[str(part) for part in command], cwd=str(cwd), environmentAdded=added,
                 boundSeconds=bound, loadBefore=os.getloadavg(), startedUtc=now())
    report['commands'].append(entry)
    stdout, stderr = cwd / (label + '.stdout.log'), cwd / (label + '.stderr.log')
    before = resource.getrusage(resource.RUSAGE_CHILDREN)
    started = time.monotonic()
    try:
        with stdout.open('wb') as out, stderr.open('wb') as err:
            result = subprocess.run(entry['command'], cwd=cwd, env=env, stdout=out, stderr=err,
                                    stdin=subprocess.DEVNULL, timeout=bound)
        entry['exitCode'] = result.returncode
    except subprocess.TimeoutExpired:
        entry['timedOut'] = True
    finally:
        after = resource.getrusage(resource.RUSAGE_CHILDREN)
        entry.update(wallSeconds=time.monotonic() - started, childUserCpuSeconds=after.ru_utime - before.ru_utime,
                     childSystemCpuSeconds=after.ru_stime - before.ru_stime, childrenMaxRssBytes=after.ru_maxrss,
                     loadAfter=os.getloadavg(), finishedUtc=now())
        for source in (stdout, stderr):
            if source.exists():
                data = source.read_bytes()
                entry[source.suffixes[-2][1:] + 'Bytes'] = len(data)
                (output / source.name).write_bytes(data[-RETAINED_LOG_BYTES:])
    return entry, stdout, stderr


def summarize(path):
    if not path.exists():
        return dict(available=False)
    snapshot = json.loads(path.read_text())
    marks = {}
    for mark in snapshot['marks']:
        marks.setdefault(mark['name'], mark)
    summary = dict(available=True, lastFlushReason=snapshot['reason'], originUtc=snapshot['originUtc'],
                   processStartUtc=snapshot['processStartUtc'], runtime=snapshot['runtime'], final=snapshot['state'],
                   gcHeapBytes=snapshot['gcHeapBytes'], threadClockAvailable=snapshot['threadClockAvailable'],
                   openAtLastFlush=snapshot['open'], counters=snapshot['counters'], stages=[], marks=snapshot['marks'])
    for stage in snapshot['stages']:
        summary['stages'].append(dict(stage=stage['stage'], calls=stage['calls'], wallMs=round(stage['inclusiveMs'], 1),
                                      threadCpuMs=round(stage['threadCpuMs'], 1), maxMs=round(stage['maxMs'], 2),
                                      allocatedMB=round(stage['threadAllocatedBytes'] / 1048576, 1), threads=stage['threads']))
        series = stage.get('seriesMs')
        if series and stage['stage'] == 'commit-preparation':
            ordered, chunk = sorted(series), max(1, len(series) // 10)
            summary['commitPreparationSeries'] = dict(
                count=len(series), totalMs=round(sum(series), 1), medianMs=ordered[len(ordered) // 2],
                p95Ms=ordered[int(len(ordered) * 0.95)], maxMs=ordered[-1], firstMs=series[0],
                decileMeanMs=[round(sum(series[index * chunk:(index + 1) * chunk]) / chunk, 3) for index in range(10)])
        elif series:
            summary.setdefault('firstCallsMs', {})[stage['stage']] = series[:4]
    summary['windows'] = {}
    for name, (first, last) in WINDOWS.items():
        if first in marks and last in marks:
            a, b = marks[first], marks[last]
            summary['windows'][name] = dict(
                wallMs=round(b['ms'] - a['ms'], 1), processCpuMs=round(b['processCpuMs'] - a['processCpuMs'], 1),
                gcPauseMs=round(b['gcPauseMs'] - a['gcPauseMs'], 1), gen0=b['gen0'] - a['gen0'], gen1=b['gen1'] - a['gen1'],
                gen2=b['gen2'] - a['gen2'], allocatedMB=round((b['allocatedBytes'] - a['allocatedBytes']) / 1048576, 1),
                jitMs=round(b['jitMs'] - a['jitMs'], 1), jitMethods=b['jitMethods'] - a['jitMethods'],
                threads=[a['thread'], b['thread']])
    summary['timeline'] = [dict(ms=round(sample['state']['ms']), commitsPrepared=sample['commitsPrepared'],
                                frames=sample['counters'].get('process-frame', 0),
                                processCpuMs=round(sample['state']['processCpuMs']), gen0=sample['state']['gen0'],
                                gen2=sample['state']['gen2'], gcPauseMs=round(sample['state']['gcPauseMs']))
                           for sample in snapshot['timeline'][::4]]
    return summary


def show(name, entry, summary):
    print('== ' + name + ' ==')
    print(json.dumps({key: entry.get(key) for key in ('exitCode', 'timedOut', 'wallSeconds', 'childUserCpuSeconds',
                                                       'childSystemCpuSeconds', 'loadBefore', 'loadAfter')}))
    if not summary.get('available'):
        print('no probe report was written')
        return
    runtime = summary['runtime']
    print(json.dumps(dict(framework=runtime['framework'], coreLibrary=runtime['coreLibrary'], serverGc=runtime['serverGc'],
                          gcLatency=runtime['gcLatency'], tiered=runtime['tieredCompilation'], pgo=runtime['tieredPgo'],
                          processors=runtime['processorCount'], environment=runtime['environment'],
                          assemblies=[[a['name'], a['jitOptimizerDisabled']] for a in runtime['assemblies']],
                          lastFlush=summary['lastFlushReason'], processStartUtc=summary['processStartUtc'],
                          originUtc=summary['originUtc'], final=summary['final'], heapBytes=summary['gcHeapBytes'],
                          open=summary['openAtLastFlush'], counters=summary['counters'])))
    for stage in summary['stages']:
        print('stage %-32s calls=%-7d wallMs=%-10.1f threadCpuMs=%-10.1f maxMs=%-9.2f allocMB=%-9.1f threads=%s' % (
            stage['stage'], stage['calls'], stage['wallMs'], stage['threadCpuMs'], stage['maxMs'], stage['allocatedMB'], stage['threads']))
    for mark in summary['marks']:
        print('mark %-40s ms=%-10.1f thread=%-3d cpuMs=%-10.1f gc=%d/%d/%d pauseMs=%-8.1f jitMs=%-8.1f methods=%d allocMB=%.1f' % (
            mark['name'], mark['ms'], mark['thread'], mark['processCpuMs'], mark['gen0'], mark['gen1'], mark['gen2'],
            mark['gcPauseMs'], mark['jitMs'], mark['jitMethods'], mark['allocatedBytes'] / 1048576))
    for key, value in summary['windows'].items():
        print('window %-26s %s' % (key, json.dumps(value)))
    print('series ' + json.dumps(summary.get('commitPreparationSeries')))
    print('firstCalls ' + json.dumps(summary.get('firstCallsMs')))
    print('timeline ' + json.dumps(summary['timeline']))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--label', required=True, help='New output folder in this packet and disposable work folder.')
    parser.add_argument('--work', type=Path, required=True, help='Existing disposable directory outside Git.')
    parser.add_argument('--reuse-build', help='Label of an earlier run whose instrumented build is reused unchanged.')
    parser.add_argument('--environment', action='append', default=[], help='NAME=VALUE for the native probe only (controlled comparison).')
    parser.add_argument('--hypothesis', default=None, help='Concrete hypothesis that justifies a controlled comparison.')
    parser.add_argument('--skip-host', action='store_true')
    parser.add_argument('--budget-seconds', type=float, default=900)
    args = parser.parse_args()
    output, root = PACKET / args.label, args.work.resolve()
    require(root.is_dir() and REPO not in root.parents and root != REPO, 'The work directory must exist outside the repository.')
    work = root / args.label
    require(not output.exists() and not work.exists(), 'Use a new label; every earlier measurement is retained.')
    require(bool(args.environment) == bool(args.hypothesis), 'A changed environment requires its recorded hypothesis, and only then.')
    output.mkdir()
    work.mkdir()
    started = time.monotonic()
    report = dict(kind='native-startup-profile-v1', status='preparing', label=args.label, startedUtc=now(),
                  limits=dict(LIMITS, aggregateSeconds=args.budget_seconds), deadline=started + args.budget_seconds,
                  originalRun=ORIGINAL_RUN, source=str(SOURCE_DATABASE), commit=COMMIT, commitHash=COMMIT_HASH,
                  disposableWork=str(work), hypothesis=args.hypothesis, commands=[],
                  limitation='Instrumented diagnostic in a disposable copy; not deadline, native or production acceptance. '
                             'Inclusive stages overlap; thread CPU and allocation belong to the measuring thread only.')
    native = host = None
    try:
        report['pinsBefore'] = pins()
        hashes, combined = source_hashes()
        (output / 'source-hashes.json').write_text(json.dumps(hashes, indent=1) + '\n')
        report['sourceBefore'] = dict(files=len(hashes), combinedSha256=combined)
        sdk = DOTNET.parent
        env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get('PATH', ''),
                   DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_NOLOGO='1')
        copy = (root / args.reuse_build / 'runtime') if args.reuse_build else work / 'runtime'
        binaries = [copy / directory / ('OpenFantasy.' + assembly + '.dll')
                    for directory, assemblies in [('client/.godot/mono/temp/bin/Debug', ('Client', 'Core', 'Hosting')),
                                                  ('src/OpenFantasy.Host/bin/Debug/net10.0', ('Host', 'Core', 'Hosting'))]
                    for assembly in assemblies]
        if args.reuse_build:
            earlier = json.loads((PACKET / args.reuse_build / 'result.json').read_text())
            report['instrumentedAssemblies'] = {str(path): digest(path) for path in binaries if path.exists()}
            require(report['instrumentedAssemblies'] == earlier['instrumentedAssemblies'], 'The reused instrumented build changed.')
            report['reusedBuild'] = args.reuse_build
        else:
            copy.mkdir()
            shutil.copytree(RUNTIME / 'src', copy / 'src', ignore=shutil.ignore_patterns('bin', 'obj'))
            if (RUNTIME / 'content').is_dir():
                shutil.copytree(RUNTIME / 'content', copy / 'content')
            shutil.copytree(RUNTIME / 'client', copy / 'client',
                            ignore=lambda folder, names: ['temp'] if Path(folder).name == 'mono' else [])
            for name in ('Directory.Build.props', 'global.json', 'NuGet.Config'):
                shutil.copy2(RUNTIME / name, copy / name)
            require(not any(path.is_symlink() for path in copy.rglob('*.dll')), 'The copy must not link production assemblies.')
            config = (copy / 'client/project.godot').read_text()
            require(hashlib.sha256(config.encode()).hexdigest() == report['pinsBefore'][str(RUNTIME / 'client/project.godot')]['sha256']
                    and config.count('[display]') == 1 and 'window/size/no_focus' not in config,
                    'Disposable no-focus configuration no longer matches')
            (copy / 'client/project.godot').write_text(config.replace('[display]', '[display]\nwindow/size/no_focus=true', 1))
            report['instrumentation'], report['instrumentationSkipped'] = instrument(copy, output)
            build = ['-p:RestoreLockedMode=true', '-m:1', '-p:UseSharedCompilation=false', '--verbosity', 'quiet']
            remaining = LIMITS['buildSeconds']
            for label, project in (('build-client', 'client/OpenFantasy.Client.csproj'),
                                   ('build-host', 'src/OpenFantasy.Host/OpenFantasy.Host.csproj')):
                entry, stdout, stderr = execute(report, output, label, [DOTNET, 'build', project] + build, copy, env, {}, remaining)
                remaining -= entry['wallSeconds']
                if entry.get('exitCode') != 0:
                    print((stdout.read_text(errors='replace') + stderr.read_text(errors='replace'))[-6000:])
                    raise RuntimeError(label + ' failed; its retained output explains the instrumented build.')
            require(all(path.exists() for path in binaries), 'An instrumented assembly was not built.')
            report['instrumentedAssemblies'] = {str(path): digest(path) for path in binaries}
        production = {item['sha256'] for name, item in report['pinsBefore'].items() if name.endswith('.dll') and item}
        report['instrumentedDifferFromProduction'] = not any(
            value in production for name, value in report['instrumentedAssemblies'].items() if not name.endswith('Host.dll')
            or True)

        world, prefix = work / 'coordinated-revoke.sqlite3', work / 'coordinated-revoke.json'
        pinned_copy(world)
        shutil.copyfile(SOURCE_ACTIVE, work / 'coordinated-revoke.active.json')
        added = dict(OPENFANTASY_NATIVE_STARTUP_REPORT=str(work / 'native-costs.json'))
        for pair in args.environment:
            name, _, value = pair.partition('=')
            require(name and name not in ('DOTNET_ROOT', 'PATH', 'OPENFANTASY_NATIVE_STARTUP_REPORT'), 'Unsupported environment ' + pair)
            added[name] = value
        command = [GODOT, '--path', copy / 'client', '--', '--coordinated-restorative-probe', world, prefix,
                   FIXTURE / 'metadata.json', FIXTURE / 'genesis.ofc', 'reopen', '--background-render']
        report['status'] = 'native-probe'
        native, _, _ = execute(report, output, 'native-reopen', command, work, dict(env, **added), added, LIMITS['processSeconds'])
        observed = None
        for name in ('coordinated-revoke.reopen.json', 'coordinated-revoke.reopen.failed.json'):
            if (work / name).exists():
                observed = json.loads((work / name).read_text())
                trimmed = {key: value for key, value in observed.items() if key != 'Observations'}
                trimmed['ObservationLabels'] = [item.get('Label') for item in observed.get('Observations', [])]
                (output / ('native-' + name)).write_text(json.dumps(trimmed, indent=1) + '\n')
        active = json.loads(SOURCE_ACTIVE.read_text())
        report['nativeProbe'] = dict(
            reportWritten=observed is not None, status=observed and observed['Status'], stage=observed and observed['Stage'],
            error=observed and observed['Error'] and observed['Error'].splitlines()[0],
            durationMilliseconds=observed and observed['DurationMilliseconds'], display=observed and observed['Display'],
            focus=observed and {key: observed[key] for key in ('FocusRequests', 'Unfocusable', 'NativeFocused', 'ObservedNativeFocus',
                                                                'StartupNoFocus', 'IsLocallyPaused', 'IsWindowFocused')},
            initialHash=observed and observed['InitialHash'], finalHash=observed and observed['FinalHash'],
            originalActiveFinalHash=active['FinalHash'], copiedSaveSha256After=digest(world),
            copiedSaveUnchanged=digest(world) == SOURCE_SHA256,
            copiedWalBytesAfter=Path(str(world) + '-wal').stat().st_size if Path(str(world) + '-wal').exists() else None)
        if (work / 'native-costs.json').exists():
            shutil.copyfile(work / 'native-costs.json', output / 'native-costs.json')
        report['nativeSummary'] = summarize(work / 'native-costs.json')

        if not args.skip_host:
            report['status'] = 'host-reference'
            reference = work / 'host'
            reference.mkdir()
            pinned_copy(reference / 'diagnostic.sqlite3')
            added = dict(OPENFANTASY_NATIVE_STARTUP_REPORT=str(reference / 'host-costs.json'))
            host, stdout, _ = execute(report, output, 'host-inspect', [DOTNET, binaries[3], '--inspect-recorded-world',
                                      reference / 'diagnostic.sqlite3', str(COMMIT)], reference, dict(env, **added), added,
                                      LIMITS['hostSeconds'])
            report['hostReference'] = dict(stdoutNamesPinnedCommitHash=COMMIT_HASH in stdout.read_text(errors='replace').lower(),
                                           copiedSaveUnchanged=digest(reference / 'diagnostic.sqlite3') == SOURCE_SHA256)
            if (reference / 'host-costs.json').exists():
                shutil.copyfile(reference / 'host-costs.json', output / 'host-costs.json')
            report['hostSummary'] = summarize(reference / 'host-costs.json')
            shared = {row['stage']: row for row in report['hostSummary'].get('stages', [])}
            report['nativeToHost'] = [dict(stage=row['stage'], calls=[row['calls'], shared[row['stage']]['calls']],
                                           nativeWallMs=row['wallMs'], hostWallMs=shared[row['stage']]['wallMs'],
                                           nativeThreadCpuMs=row['threadCpuMs'], hostThreadCpuMs=shared[row['stage']]['threadCpuMs'],
                                           wallRatio=round(row['wallMs'] / shared[row['stage']]['wallMs'], 2)
                                           if shared[row['stage']]['wallMs'] and row['calls'] == shared[row['stage']]['calls'] else None)
                                      for row in report['nativeSummary'].get('stages', []) if row['stage'] in shared]
        report['status'] = 'diagnostic-complete'
    except Exception as error:
        report.update(status='failed', failure=repr(error), traceback=traceback.format_exc())
    finally:
        try:
            report['pinsAfter'] = pins()
            report['pinsUnchanged'] = report['pinsAfter'] == report.get('pinsBefore')
            hashes, combined = source_hashes()
            report['sourceAfter'] = dict(files=len(hashes), combinedSha256=combined)
            report['sourceUnchanged'] = report['sourceAfter'] == report.get('sourceBefore')
            if not report['pinsUnchanged'] or not report['sourceUnchanged']:
                report['status'] = 'failed'
                report.setdefault('failure', 'A pinned input or production source changed during the diagnostic.')
        except Exception as error:
            report.update(status='failed', preservationFailure=repr(error))
        report['elapsedSeconds'] = time.monotonic() - started
        report['finishedUtc'] = now()
        del report['deadline']
        (output / 'result.json').write_text(json.dumps(report, indent=1) + '\n')
    print(json.dumps({key: report.get(key) for key in ('status', 'failure', 'elapsedSeconds', 'pinsUnchanged', 'sourceUnchanged',
                                                       'instrumentedDifferFromProduction', 'instrumentationSkipped',
                                                       'nativeProbe', 'hostReference')}, indent=1))
    if report.get('traceback'):
        print(report['traceback'])
    for entry in report['commands']:
        if entry['label'].startswith('build'):
            print('build %s exit=%s wall=%.1fs' % (entry['label'], entry.get('exitCode'), entry['wallSeconds']))
    if native:
        show('native reopen (.NET hosted by Godot)', native, report.get('nativeSummary', {}))
    if host:
        show('standalone Host reference', host, report.get('hostSummary', {}))
        print('== native to host ==')
        for row in report.get('nativeToHost', []):
            print(json.dumps(row))
    return 0 if report['status'] == 'diagnostic-complete' else 1


if __name__ == '__main__':
    raise SystemExit(main())
