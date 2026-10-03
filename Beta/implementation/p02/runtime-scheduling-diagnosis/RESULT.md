# Runtime scheduling capture

The finite experiment is complete. Two instrumented executions of the original server selection
and one isolated execution of its unchanged four-player case passed. Neither aggregate reproduced
the retained failure. **The multiplayer latency gate remains open; no scheduler repair is claimed.**
No production runtime code, old test, cadence, lease, deadline or assertion changed.

## Evidence

| Execution | Outcome | Wall time | Evidence |
|---|---|---:|---|
| Solution build | Zero warnings/errors | 18.431s | [report](qualification/build-20261003T030714016736Z/report.json) |
| Original server selection, first capture | 76 passed; one expected Windows ACL skip | 32.192s | [report](qualification/server-compatibility-20261003T030909399048Z/report.json), [analysis](qualification/server-compatibility-20261003T030909399048Z/ANALYSIS.json) |
| Original server selection, second capture | 76 passed; one expected Windows ACL skip | 30.541s | [report](qualification/server-compatibility-20261003T030952521084Z/report.json), [analysis](qualification/server-compatibility-20261003T030952521084Z/ANALYSIS.json) |
| Isolated original four-player case | One passed | 6.809s | [report](qualification/roster-only-20261003T031035328606Z/report.json), [analysis](qualification/roster-only-20261003T031035328606Z/ANALYSIS.json) |

The isolated case overlaps the aggregates: this is **76 distinct passing cases**, not153.
The [predeclared limit](DESIGN.md) allowed two nonreproducing aggregates and one isolated contrast;
no further unchanged runs follow. The [previous failed trace](../movement-delivery-diagnosis/DIAGNOSIS.md)
remains authoritative evidence of the unresolved failure, not erased by these passes.

Each analysis selects the actual .NET10.0.12 test application, matches its host/runtime captures
by PID and exact original event keys, authenticates their raw hashes, and checks zero dropped,
incomplete, invalid or failed counter samples. Interpreted intervals require nonoverlapping
counter reads and monotonic CPU/pause/collection counters. All twelve player movement samples
were selected, with first sampling ages below45ms.

## What the additional counters establish

The first aggregate's recorded event window spans26.361s with5.862s of cumulative process GC pause;
the second spans25.370s with5.721s of pause. The isolated case's event window spans2.738s with64.643ms
of pause. These windows cover different workloads and durations: aggregate counters include other
concurrent tests, JIT, GC and runtime work. They are **not** a controlled attribution of the earlier
failed sample to parallel tests, nor evidence that a production four-player session has that pause rate.

Long observation intervals overlap real GC pauses. The first aggregate's longest capture lasts
269.051ms with83.527ms cumulative pause and959.882ms process CPU; the second reaches281.450ms with
90.997ms pause and867.116ms process CPU. CPU can exceed wall time because it sums parallel threads;
it is not the encoder's exclusive CPU. Even the isolated case has a132.838ms capture with6.619ms
pause and130.675ms process CPU. Cold initialization and scheduling remain relevant distinctions
from the earlier warmed component probe. No interval contains a reproduced expired movement sample.

Counter-read medians are2.58–3.13µs, with95th percentiles no greater than19µs. The largest measured
span is5.045ms. These brackets exclude event dispatch, filtering, record allocation and publication;
they neither measure all observer overhead nor prove zero perturbation. Counter values are separate,
non-atomic reads. Event-to-counter-read delay also matters: the second aggregate contains a34.795ms
delay before one sample starts. Event-to-event wall intervals and counter intervals therefore are
not identical. Analysis reports their separate timing brackets and retains accepted movement with
no sampling event explicitly, rather than silently dropping it. [Independent review](REVIEW.md)
verified the evidence and prompted these refinements. Limits remain in source and outputs.

[Timer source review](TIMER_REVIEW.md) confirms that awaited timer completion can inline observation
or host work, but additional due callbacks are queued before one callback fires inline. That rules
out claiming a single timer thread was necessarily blocked by encoding. Source inspection and these
successful runs do not establish a safe production scheduler rewrite.

## Integration and next work

The new [opt-in listener](../../../Runtime/tests/OpenFantasy.Server.Tests/Diagnostics/RuntimeSchedulingCapture.cs)
is test-process-only, requires an existing absolute output directory, retains at most32,768 selected
events, creates no timer and catches diagnostic failures without replacing the original test result.
It logs fixed stages, opaque IDs and process counters; no credentials or gameplay body is collected.
The original host listener and all755 old tests/fixtures remain unchanged. There is no default-on
runtime profiling or changed project target. [Preservation](PRESERVATION.json) also pins23 frozen
specifications and exact unrelated predecessor staging.

Before-change `codex/checkpoint-before-runtime-scheduling-20261003T030339Z` preserves
`7ab81d44da5c922aa24dca1147adeb8c75a301af`. Work remains on `codex/runtime-foundation`.
Architecture impact: none; diagnostic progress stays in this packet. Native, device, human,
full selection, latency and production acceptance remain separate and open. No Core or storage
regression is relabelled as freshly rerun.

The shared verification skill carries the portable runtime/counter-interval lessons in ecosystem
commit `b37a36f`; [synchronization evidence](SKILL_SYNC.json) pins the maintained source, three installed
copies and recoverable backups. Metadata stays byte-identical. The bundled skill validator could not
run because PyYAML is absent; focused prose/diff/source-identity checks passed without installing tools.

Resume explicit actor selection on the existing custody foundation, preserving scoped views,
home-player decisions and A→B→A generation invalidation. Use the added counters when a changed
implementation requires relevant server qualification, rather than repeating unchanged aggregates.
If the original failure recurs, inspect that exact sample before selecting an allocation or
scheduling repair. The unresolved gate must still pass its required eventual qualification.
