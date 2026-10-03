# Failed movement sample

The first of the at-most-three predeclared traced baseline executions reproduced the original
four-player movement timeout. The sequence therefore stopped after one run: 75 passes, one failure
and the expected Windows-only ACL skip in 67.389s. Original assertions and deadlines are unchanged.

[The extracted sample](FAILED_SAMPLE.json) pins the complete application trace and its relevant
event window. PID48895 is the actual test application, not its wrapper/discovery processes. It
records 6,231 events with zero dropped, incomplete or invalid events. Timestamps, rather than
cross-thread event reservation order, establish chronology.

The fourth player's nonzero movement is queued at 12.123530s, finishes client transmission at
12.157528s, reaches server input request6 at 12.557061s and is accepted at 12.557069s. The lease
starts at host receipt; client/network time does not consume it. The first host sample at
12.709035s measures **151.9644ms**, exceeding the unchanged **150ms** lease. There are 33 age checks
from acceptance (including this first check) and no `movement.selected` event before cleanup. The player was ready. This failure
precedes movement admission/terrain and observation of a moved result; no movement was selected.

The preceding host tick ends at 12.284259s; the next tick starts waiting for serialization at
12.709001s, a **424.742125ms** gap. Lock acquisition takes only **0.003542ms** and entry-to-sampling
another **0.030541ms**. The delay lies before the host enters its serialized tick. This differs from
the [older projection-bound failure](../multiplayer-service-latency/DIAGNOSIS.md), whose sample
arrived during a long serialized projection.

During the gap, the trace records observation capture and client replica work on the same managed
thread. After input acceptance, two captures occupy 61.077250ms and 36.128416ms of wall time, followed
by a 40.140958ms replica interval before the tick. These intervals identify work to measure; they
do not distinguish active CPU work, runtime/GC pauses, thread-pool pressure or OS scheduling.
Other applications share the machine. Their causal contribution is unknown. Tracing itself may
perturb execution. No dedicated scheduler, offload, lease renewal or deadline extension is justified
by this trace alone.

The next bounded experiment measures production observation Encode, Decode and expanded-JSON
Preflight separately on four warmed real player views, retaining exact bytes and player scope.
No timing threshold is invented for the measurement, and a cheaper component does not by itself
close the multiplayer latency gate. Core rules, canonical state, runtime architecture and full
actor-selection obligations are unchanged by this diagnostic work.
