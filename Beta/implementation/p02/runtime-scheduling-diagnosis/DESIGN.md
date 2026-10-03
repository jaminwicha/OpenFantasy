# Runtime scheduling discrimination

The preceding diagnostic captures a movement sample expiring before its first host sampling, with
the main gap before tick serialization. Warmed codec costs do not explain the live delay. This
experiment adds only an opt-in test-process listener; production sources and all old tests remain
unchanged. The current pre-change snapshot and protected inputs are pinned in `PRESERVATION.json`.

At relevant existing host/transport diagnostic stages, capture cumulative process CPU, cumulative
GC pause, collection counts and thread-pool size/queue, bracketed by sampling timestamps. Retain
the event's original Stopwatch timestamp separately. The counters are not an atomic snapshot and
process CPU can include parallel tests, JIT, GC and other threads. Instrumentation itself adds
work; quantify its sample duration. Never attribute process-wide CPU to one encoded frame or actor.
Existing opaque diagnostic bindings provide ownership; no credentials or gameplay payload is logged.
Both listeners are bounded, opt-in and off by default. No diagnostic timer or runtime scheduling
changes are introduced.

After a warning-free current-source build, execute the original server selection with both listeners,
under the existing heavy-check lock and unchanged 600s process limit. Stop aggregate captures at
the first reproduced movement failure or after two nonreproductions. Then one isolated execution
of the unchanged four-player case may contrast test-process concurrency; it is diagnostic only,
never a replacement for the full server gate. Keep every original five-second observation deadline,
150ms lease, fixture assertion and production cadence. No automatic rerun past these finite limits.

Correlate exact same-process host and runtime captures by original event timestamp/owner/stage,
checking actual runtime/application identity, drops, incompleteness and capture errors. Compare
CPU/pause deltas only across valid measured intervals. A lack of a reproduced failure limits the
claim. Large GC pause, active process CPU or queued work can direct the next experiment, but none
alone proves exclusive causality or a safe scheduler repair.

Counter contracts: [.NET process CPU](https://learn.microsoft.com/en-us/dotnet/api/system.environment.cpuusage?view=net-10.0)
and [cumulative GC pause](https://learn.microsoft.com/en-us/dotnet/api/system.gc.gettotalpauseduration?view=net-10.0).
Architecture impact is none until an actual production boundary changes. Keep progress here rather
than adding speculation to timeless architecture documentation.
