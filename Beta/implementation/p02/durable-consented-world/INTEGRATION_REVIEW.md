# Durable consented world integration

The bounded durable Host increment passes integration qualification. The completed Fable unit and
original failures are preserved; [final checks](qualification/FINAL_CHECKS.json) pin the evidence.
This document does not close
public paired ingress, asynchronous offers, native interaction, retained-save migration or full game
phases. The runtime binding is the existing privileged host-selected world batch.

## Runtime and reference review

Four Host files name the explicit profile-27/profile-28 family. Schema eight's unchanged layout stores
the same bounded scope, ordinal, boundary, publication and opaque-root fields for both interpretations.
Creation, archive accounting, archive publication and recovery agree on that family. Replay still
checks each step against its current root and expected-genesis opening still compares exact bytes
after complete recovery. No profile migration is added and no older world is reinterpreted.

`WorldCommitPreparation`, the one SQL publication transaction, permanent identity storage,
`WorldMaterialCareArchiveReplay` and `FindReceipt` remain unchanged. Integration review traced those
actual bindings rather than inferring durability from the test assembly name. The new final-head
profile check is defensive; no case reaches it because dispatch rejects mismatched profiles earlier
and schema eight has no migration. This is an explicit coverage limit, not an invented branch test.

The worker's updated timeless references accurately distinguish durable batches from public ingress.
Astra corrected one overstatement: streams are keyed by principal and command, with current actor
control checked before disclosure. A principal already controlling both actors may access both
authorized streams; the model does not impose one principal per actor. Separate transfer approvals
remain required. The test fixture exercises independently controlled participants.

## Verification scope and corrections

The worker's [report](WORKER_RESULT.md) and [notes](IMPLEMENTATION_NOTES.md) remain unchanged.
Its corrected build passes 17 durable cases and 38 legacy cases. The durable fixture lives through
850 accepted commits and 507 pressure gifts, with two actual owner-published coordinated cuts,
crafting, raw and produced gifts, recrafting, care, onward transfer and real reopening. Exact command,
receipt, identity, archive, source ancestry and complete-world checks are exercised; twelve corrupted
database variants fail with their specified reasons. The original 520-dose fixture is unchanged.

The first focused run retained 16 passes and one fixture failure: after two genuine gifts, a helper
looked for multi-unit stock in a bag whose lot had one unit left. Selecting the other actor's untouched
fresh stock fixes the test setup. No runtime rule, assertion or bound was relaxed.

The worker's legacy selector mistakenly named a nonexistent `WorldJournalPolicyTests` class; the
actual four policy tests are `MaterialCareJournalPolicyTests`. Astra's broad affected Host selection
includes those existing tests and two new `ConsentedMaterialCarePolicyTests`. The additions check
the exact last archive slot/byte, independent count and byte exhaustion, unchanged journal budget,
both checkpoint thresholds, resource-family refusal and rejection of unsupported joint profiles.
These are policy proofs, not a claim that the 850-commit world crossed a scheduled checkpoint.

Worker focused and legacy selections overlapped after the fixture correction, contrary to the
project cadence. Both passed their unchanged 600-second limits. Their elapsed times are not used
for production performance conclusions, and the later pass does not establish a cause for any older
timing failure. Astra ran the broad selection and process-loss qualification sequentially.

The additional policy tests change no runtime source or existing worker test. Evidence from the
worker build remains scoped to its exact inputs; final broad/process checks use the integration
build. All 821 runtime source entries and three net8 source-library assembly entries match across those
builds. Every worker test source is unchanged; the integration build adds only the two policy cases.
The final integration build passes, and the broad affected Host selection passes 200 cases, retaining
all 195 required prior managed names. Together with the 17 durable and 38 legacy cases, this provides
244 distinct passing cases after overlaps, not 255 or a newly run complete game suite.

The build harness's assembly glob covers net8 libraries/tests, not the net10 Host executable.
[Separate pins](qualification/HOST_EXECUTABLE_PINS.json) record the actual executable and confirm its
Core/Hosting DLL copies equal the integration build. The process harness pins its executable directory
before and after execution. These separate builds are not one uninterrupted run. Core behavior is
unchanged, so its retained full-suite qualification is historical evidence, not a newly executed suite.

## Independent process-loss qualification

`qualification/verify_process_recovery.py` uses the existing offline Host command and actual SIGKILL
at `BeforeCommit` and `AfterCommit`. The normal control must equal the separately exported managed
after-database in every authoritative table. Killed/recovered copies must equal the complete before
or after ledger; both exact successful receipts, command roles, archive publication and historical
selection are checked. A duplicate batch must fail without changing any authoritative table.

The harness preserves the established 30-second run/boundary deadline and five-second kill deadline.
It targets only its own child processes and uses disposable database copies. The
[executed result](qualification/process-loss-20260929T101213Z/result.json) passes the normal control,
both actual SIGKILL boundaries and recovery, exact before/after ledgers, duplicate refusals and
historical selection through the current head. Both killed processes exited with signal nine; all
run, boundary and kill deadlines held. Fixture and executable hashes stayed unchanged.

The lead first selected a nonexistent net8 Host path; preflight failed before starting a Host process.
The [original request](qualification/PROCESS_PREFLIGHT_FAILURE.json) is retained. Reading the Host
project's actual net10 target corrected the command without a runtime or toolchain change.
The [fixture archive](qualification/FIXTURE_ARCHIVE.json) preserves all 15 accepted inputs with hashes
and restore instructions. None of this substitutes for power-loss, native/device, production-scale or
retained-world acceptance.

## Delegation record and remaining work

The bounded run returned `claude-fable-5-1` with a 1,000,000 context window, requested Max effort,
verified pre/postflight context and no permission denials. Duration was 1,972.071 seconds; the client
list-price estimate was $20.17208625, within the 2,400-second/$24 cap. This is neither billing nor a
measured net credit saving. The deliverable was useful; friction was the fixture selection error,
concurrent checks and extra read-only directory listings outside the requested Bash examples.
No model substitution, paid retry, recursive delegation or runtime permission expansion occurred.

Next, implement the actual two-party submission/application boundary and persisted proposal lifecycle
under the [full integration plan](../consented-custody/INTEGRATION_PLAN.md). `LocalWorldSession` and
the interactive restorative controller/view still stop at their explicitly admitted profiles; they
do not expose profile-28 gameplay. Creation receipts, transfer outcomes, withdrawal/expiry, stale
terms, independent current deliveries and NPC decisions remain distinct required contracts.

Keep the original personal-dose saved world and 520-dose ledger intact. A fresh profile-28 SQLite
proof is not its supported transition. UI/native acceptance, full history/streaming scale, content,
art/audio and the complete P00–P14 campaign remain open. The earlier format residuals also remain:
maximum root bounds, pinned opaque-import provenance, protected stock across cut/retirement and V10
bytes under a complete-world context. No format freeze or complete phase follows from this unit.
