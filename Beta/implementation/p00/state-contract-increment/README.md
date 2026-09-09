# Finite custody, calendar work and historical boundaries

9 September 2026. This retained P00 increment follows the [command/package checkpoint](../contract-increment/README.md). It adds real immutable planning and selection behavior within the portable C# core. The [current progress record](../PROGRESS.md) tracks the remaining P00 work; none of the complete-game qualification gates is closed by these contracts.

## Delivered behavior

**Finite counted custody.** A complete supplied custodian group can plan one branch-local lot transfer against explicit revisions and finite capacity. Eight untransformed source wood units split into six transferred plus two remaining; source claims partition exactly, the remainder retains its identity, and the child has a deterministic identity. Full transfer retains the original lot ID. Capacity, reservation, equipment, revision and known-identity failures preserve the original state byte for byte. Candidate application verifies the entire original snapshot hash and revision. Definition/protection/source data stays attached. These are candidate replacements, not executed world transactions or durable receipts. See the [custody contract](../custody-contracts/CONTRACT.md).

**Calendar scheduling.** A queue names one world/branch/calendar scope. Immutable entries order by due instant, fixed phase, work domain, actor and event. Selection requires a matching scoped cutoff and one explicit phase, retains all unselected work and exposes backlog. Applying a completed-work selection produces a new candidate snapshot only against the original revision/hash. The strict codec preserves continuation metadata. Bounded tests drain 900 due entries across four batches while preserving 124 future entries and their ordering. These limits define bounded input/work batches, not a license to omit world actors. Tactical/exposure clocks and global transition→command→observation dispatch remain separate owner responsibilities. See the [scheduler contract](../scheduling-contracts/CONTRACT.md).

**Exact historical requests.** Requests carry world/branch/realm/signed 3D address, exact requested instant, historical origin, known head, pinned head revision and optional earlier commit cursor. The strict codec preserves those values. A bounded selector consumes already ancestry-resolved, pinned commit metadata, refusing wrong scope, duplicate sequences, future revisions and timestamps outside available chronology. Spatial coordinates locate a reconstruction request; they never filter away cross-location causes. Later live commits cannot alter the original immutable request. This is a request/commit-index contract, not an ancestry resolver, archive, prepared terrain or time-travel execution service. See the [history contract](../history-contracts/CONTRACT.md).

All runtime names remain OpenFantasy; the historical planning-folder name is excluded from runtime text and relative paths. The implementation uses narrow immutable domain values, copied collections and cohesive planners/codecs; no engine reference, new assembly or IPC layer was introduced.

## Executed evidence

| Evidence | Actual result |
|---|---|
| [Pinned Debug verification](debug-verification.json) and [Debug TRX](core-debug.trx) | **149 executed, 149 passed, zero skipped**: previous 92 + 24 custody + 20 scheduling + 13 history |
| [Release verification](release-verification.json) and [Release TRX](core-release.trx) | **149/149 passed**, zero skipped; full solution including Godot ExportRelease builds with zero warnings/errors |
| Six independent Python stdout oracles | Numeric/identity/RNG, command/receipt, packages, custody, scheduling and history reproduce frozen expectations without overwriting them |
| Existing host/source-packet checks | All 39 host cases pass; retained 52-file source packet's exact set/lengths/hashes pass |
| Existing Godot import and binding smoke | Pass, no engine error output; host/client foundation projections match |
| [Independent custody validation](../custody-review/REVIEW_VALIDATION.json) and [TRX](../custody-review/test-results/custody-independent.trx) | **24/24 passed** against stable Debug binaries with `--no-build --no-restore`; independent oracle comparison also passes |
| [Preservation](preservation.json) / [review input check](review-input-check.json) | All runtime inputs still match the integrated report; original 19 staged Alpha paths retain their exact staged diff; independently reviewed source snapshots still match |

The candidate tools remain SDK 10.0.401 and Godot 4.6.stable.mono.official.89cea1439 on the existing Apple M2/macOS machine. The core tests target net8.0; the integrated host and Godot client again report .NET 10.0.12. The pipeline records SDK, TFM and actual runtime separately. These runs do not establish another OS, a supported export, mobile readiness, a production performance floor or human art/gameplay acceptance. No new visual/presentation behavior was added in this increment.

## Independent findings and disposition

The coordinator authored custody; one critic inspected it and independently ran its 24 tests. Separate workers authored history and scheduling, then reviewed each other's final contracts and fixtures. The coordinator performed the full integrated build/test pass. Reports retain exact reviewed hashes and distinguish static review, Python execution, managed tests and unimplemented responsibilities.

- **Custody declared expectations:** the initial planner checked its four required revisions but ignored additional declared targets. It now validates every supplied expectation; unknown targets reject. Current/stale/unknown extra expectations have regressions.
- **Custody child identity:** the initial split collision check omitted locally known source/definition IDs. It now includes those identities and reservation IDs, in addition to lots/storages/world/branch/command participants. Source, definition, storage and existing-lot collisions have regressions. Whole-world registration remains required at insertion.
- **Historical chronology:** the initial commit selector could accept a candidate outside the available origin/head window before filtering. It now rejects those candidates, and Includes returns false below origin. Tests cover exact inclusive boundaries and one millisecond beyond each end, while preserving valid later-than-request commits as filtered batch inputs.

All three findings are closed within their reviewed scope. The [custody review](../custody-review/CODE_REVIEW.md), [history review](../scheduling-contracts/HISTORY_REVIEW.md) and [scheduler review](../history-contracts/SCHEDULING_REVIEW.md) preserve the evidence. Additional custody coverage exercises successive source splits near UInt64.MaxValue and overflow of either custodian revision; all failures leave original inputs unchanged.

The original source lesson is also retained: Alpha's capacity path can partially add before failure, while its ration recipe transforms two berries and one wood into two rations. Atomic candidates address the first pattern. The second requires a future heterogeneous provenance contract; treating output count as input claims would multiply or erase rights. This counted-source type explicitly refuses that claim of representability. Original source was inspected and preserved, not modified.

## Checkpoints and next work

- `904e4ae`: finite counted custody values, transfer candidates, independent oracle and 24 tests.
- `bef351b`: scoped calendar queues, historical boundaries, codecs, independent oracles, cross-reviews and 33 additional tests.
- The following evidence commit records this executed checkpoint, the custody critic and updated progress links.

Remaining P00 work includes representative actor/body/form and material/process definitions, typed exposure and observation boundaries, strict custody decoding/migration, explicit source-to-playable field transforms, measurable catalog/workload budgets and separately owned native art/audio packets. Custody admission must establish actual source budgets and complete storage contents; processing must preserve heterogeneous provenance through products and waste. History storage must resolve branch ancestry and ordered intra-commit events; the scheduler must dispatch effects in an atomic authority step. P01 must qualify durable acceptance, receipts, recovery and exports before these immutable candidates become a running world. The full 181-requirement campaign remains in force.
