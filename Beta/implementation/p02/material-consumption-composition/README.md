# Material consumption composition

This increment joins direct item-consumption retirement to the existing terrain and water material
owners. It is a dependency for live uses such as medicine and food; it enables no treatment action,
complete-world consumption profile, native flow or new save migration.

The shared retirement view covers exact recipe inputs and consumed-item claims. Terrain partitions,
excavation issuance, runoff funding/capacity, reservoirs and scheduled transport retain those rights.
Consuming a produced output does not count its raw ancestors a second time. Allocated material must
leave representation custody before retirement; reserved water cannot be consumed to bypass funding.
Changed successors retain prior evidence and preserve their production interpretation in both directions.
Explicit terrain V3 and water-owner V2 archives keep production V2, while legacy archives retain their
bytes. The [architecture reference](../../../Runtime/docs/architecture/item-consumption.md) describes
ownership, codec versions and publication limits.

The [independent Fable review](fable/REVIEW.md) found and fixed a real silent-profile-upgrade defect.
Two regressions fail on the unfixed candidate and pass with the fix. A third regression proves that a
structurally valid claim on reserved forcing stock is rejected at capacity reconciliation. The focused
worker run passed 278 cases. The worker's initial compound shell command was denied; it continued with
permitted direct dotnet calls. The adapter records `permission_denied` despite the completed review;
[the sanitized result](fable-result.json) retains this distinction. No paid retry or model substitution
occurred. A duplicate JSON key in the worker summary was renamed `failedTests`; its original is retained,
and the TRX counters supply the actual failure counts.

Lead qualification isolates the selected implementation over `c2547c6`, excluding the care/world-profile
WIP. [Source hashes](qualification/isolated-source.json), [commands](qualification/commands.json) and the
[final verification record](qualification/verification.json) describe the exact tested scope. The clean
solution build and all 1,247 Core + 665 Hosting/client = 1,912 cases pass without failures/skips. A
separate 71-case care-WIP compatibility run passes, without promoting that unfinished feature. The standalone Python
[archive checker](verify_archives.py) recomputes material retirement and receiver quantities from saved
bytes, checks nested production equality and distinguishes raw retirement from produced-output ancestry.
Its first attempt had an incorrect consumed-claim field index; [that failure](qualification/archive-oracle-01.json)
is retained, and [the corrected pass](qualification/archive-oracle-02.json) changes only the checker.
[Adversarial checks](qualification/oracle-adversarial.json) reject missing/duplicated retirement and
malformed archives. This oracle does not authenticate commands or replace runtime admission.

Recreated legacy fixture bytes are compared against the existing retained originals. The
[hash report](qualification/legacy-bytes.json) records all 37 comparisons; duplicate binary copies are
ignored here. New archive fixtures and regression evidence are retained.

Remaining integration obligations: admit actual consumed gathering/excavation rights in a complete new
world profile, wire all resource writers to durable ordered history, qualify ordinary and aftermath
care with actual delivery/permission/cost rules, and prove crash recovery and native interaction. The
excavation consumed branch remains unreachable under the committed world's consumption refusal;
complete-world incomplete-site/reissued-source tests belong to that later admission increment.
