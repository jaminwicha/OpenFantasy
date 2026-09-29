# Native startup cost attribution

The retained startup failure remains open. This diagnostic identifies expensive recovery work;
it does not replace the original native acceptance run or relax its 30-second opening deadline.

## Measured result

The [disposable-copy run](run-01/result.json) opens an unchanged copy of the actual failed
commit1581 save. Every original source/binary/save pin remains unchanged. The fresh copied client and
Host builds succeed without warnings. The native probe passes in29.752s, close to its unchanged
limit; its opening condition becomes available at28.574s after game-ready begins. No window focus
is requested or observed. The full exact recorded head and original save bytes remain unchanged.

| Instrumented native scope | Inclusive wall time | Thread allocation |
| --- | ---: | ---: |
| Complete client opening task | 28.378s | 11.293GB |
| Full journal replay | 23.788s | 11.273GB |
| World root admission, 2,120 calls | 8.201s | 1.267GB |
| World encoding, 2,116 calls | 7.185s | 6.488GB |
| Database opening/schema checks | 3.934s | 0.000195GB |
| Client attachment | 35.312ms | 0.000590GB |
| Initial view projection | 25.272ms | 0.000058GB |

These scopes overlap and must not be added together. Allocation is cumulative on the measured
thread, not peak memory. Instrumentation itself adds cost. Database opening consumes only59ms of
thread CPU during3.934s wall time; its wait cause is not established. The opening window includes
about1.6s of process-wide GC pauses. The [field-size inventory](HEAD_COMPONENT_SIZES.json) shows the
final head is76,718 bytes; rebuilding intermediate histories dominates simple final-head reading.

Both native and standalone processes actually load **.NET10.0.12** from the pinned toolchain.
The client targets net8.0, which did not identify its actual hosted runtime. The previous .NET8-native
assumption is corrected here. Both builds disable JIT optimization through their Debug assemblies.
The same-copy Host reference takes40.630s, with33.847s in full replay; this diagnostic uses its stated
90s collection bound and is not a pass of any30s Host gate. Sequential timing differences on this
shared desktop do not establish rendering, runtime version or contention as the original cause.

## Decision and limits

Prioritize measured repeated encoding/allocation and root-admission costs; initial client
presentation is small in this run. Preserve full genesis/command/archive/identity validation and
all deadlines. A further profile should identify the expensive codec before changing its algorithm.
No production source, architecture contract, schema, rules or gate changes occur in this packet;
the timeless architecture reference therefore needs no behavior edit. Production scaling beyond
this1,581-commit workload remains separate.

The [script](profile_native_startup.py) instruments only a disposable source copy; its
[insertion diff](run-01/instrumentation.diff), actual runtime, wall/thread CPU, GC/JIT, source hashes
and original probe output are retained. Reproduce with a new label and an existing private work
directory outside Git. Never overwrite a prior label. Unmodified original-probe execution, actual
ledger authentication and broader platform acceptance remain required after any runtime fix.

Fable/1M/Max produced the diagnostic script and completed its measured run, but timed out after
900.326s before a final write-up or structured model/usage response. Requested selection is recorded;
returned model/context and cost are unavailable. Pre/postflight verified. Its pending
[worker note](WORKER_RESULT.md) remains unedited; this document is Astra's reviewed disposition.
There is no automatic paid retry or claim of net credit savings.

The [pre-change preservation record](PRESERVATION.json) pins the clean successor HEAD,76 earlier
milestone refs,23 frozen specs and the unrelated predecessor index. The diagnostic milestone records
measurement only; it does not close startup or production acceptance.
