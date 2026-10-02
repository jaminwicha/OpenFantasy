# Pending aftermath care disposition — design review

Status: proposal and read-only design assessment, 2026-10-02. This document records a suggested
integration design against successor HEAD `d9e6ec57`. It is not an approved rules decision,
implementation report or acceptance claim. No builds or tests were run for this assessment.

The suggested profile 38, Care V5, API names and record versions below are **proposed, not reserved**.
An implementation decision may choose different names or versions, with explicit compatibility
contracts. Preserve the existing profiles, constructors, codecs and frozen specifications.

## Basis and intended distinction

The [general recovery design](../general-recovery/DESIGN.md) and
[runtime architecture](../../../Runtime/docs/architecture/general-recovery.md) retain an unresolved
dependency: an unmatched aftermath acknowledgment still blocks completion. The current care owner
assumes every closed request has a real treatment. Removing `Pending` or manufacturing a treatment
would lose history and misstate material/resource effects.

Add an explicit terminal disposition for one request. Closing the request does not accept the encounter
outcome, consume medicine, restore HP, revoke an agreement, or grant general recovery. A later ordinary
recovery command remains a separate exploration operation with its own permission, finite dose,
resource evidence, time and receipt.

## Proposed decisions and authority

Proposed intents:

- `CancelPendingCareIntent`: authenticated patient ends its exact pending request.
- `DeclinePendingCareIntent`: the agreement's authenticated helper declines that exact request.

Both bind the acknowledgment ID, expected care revision and episode. The existing action envelope
binds actor, control, presence revision, world, branch and command identity. The authority validates
the deciding actor against the retained acknowledgment's exact agreement.

These are nonbodily, zero-time controller decisions. Patient cancellation must remain possible at HP0;
neither decision needs a dose, manipulation, reach or treatment readiness. Reading and excavation holds,
exact ownership and ordinary publication constraints still apply. If the UI says “defer care,” it must
explain that the current request ends; the label must not imply an automatic retry or a paused obligation.

Closing a request leaves its `CareAgreement` and any separate `CarePermission` unchanged. A subsequent
real defeat may admit a new aftermath request. Permanent withdrawal of an agreement is a different law.

## Proposed retained owner and invariants

Add a bounded `CareDisposition` record with disposition ID, command ID, acknowledgment ID, deciding
actor, disposition kind (`PatientCancelled` or `HelperDeclined`) and world sequence. Derive the
disposition identity from its command. Require exactly one terminal result per acknowledgment:
treatment **or** disposition, never both.

The owner relations become:

```text
TotalAcknowledgments = TotalTreatments + TotalDispositions + PendingCount
Revision = TotalAcknowledgments + TotalTreatments
         + TotalDispositions + Ordinary.Revision
ArchivedAcknowledgments = ArchivedTreatments + ArchivedDispositions
PendingCount is zero or one
```

Disposition commands and sequences participate in uniqueness, chronological append checks and
successor preservation. Every terminal record follows its acknowledgment and closes the sole active
request. Reject disposal of an unknown, already treated, already disposed or nonpending request.

The interval excluding ordinary operations ends at either treatment or disposition. The current
`CareSnapshot.Ordinary.cs` assumes that only a treatment can end that interval and must be updated.

A disposition changes no HP, resource revision, material, debt, clock, encounter phase or outcome
acknowledgments. It creates neither consumption claims nor resource-effect evidence. Outcome completion
remains a separate ready boundary; ordinary recovery remains a later distinct transaction.

Primary domain owners:

- `Runtime/src/OpenFantasy.Core/Care/CareSnapshot.cs`
- `Runtime/src/OpenFantasy.Core/Care/CareSnapshot.Continuation.cs`
- `Runtime/src/OpenFantasy.Core/Care/CareSnapshot.Ordinary.cs`
- `Runtime/src/OpenFantasy.Core/Care/CareContinuationContext.cs`
- New disposition, interpretation-policy and closed-defeat witness types

Paths in the ownership inventories are relative to `Beta/`.

## Bounded continuation and durable identity

Keep the existing latest-treatment `CarePatientWitness` for resource chronology. Add a separate bounded
**per-patient** latest-closed-defeat witness containing the request, defeat and terminal decision
identities, deciding actor, kind, sequences and record commitments. Cancellation must not overwrite
the latest treatment witness with fictional healing.

Require a later acknowledged defeat to occur after that patient's previous terminal sequence. Checking
only retained acknowledgments would permit duplicate acknowledgment after a cut. A frontier per
agreement would permit the same defeat to be acknowledged through another agreement. The bound is the
admitted patient set, not the lifetime number of requests.

Full earlier request and disposition records remain in authenticated coordinated archives. Permanent
journal identity occupation continues to prevent reuse of commands and derived entities. The compact
frontier is chronology evidence, not a substitute for complete historical identity authentication.

Update the following integration owners:

- `Runtime/src/OpenFantasy.Core/Worlds/WorldMaterialCareContinuation.cs`: disposition capacity,
  progress detection and terminal headroom.
- `Runtime/src/OpenFantasy.Core/Worlds/WorldMaterialCareArchiveAnchor.cs`: new disposition chronology
  must follow the complete closing world boundary.
- `Runtime/src/OpenFantasy.Core/Worlds/WorldMaterialCareArchiveSegment.cs` and archive replay:
  exact capture, rebase, reopening and context matching.
- `Runtime/src/OpenFantasy.Core/Worlds/WorldAdmissionValidator.cs` (`ReadCare`) and
  `WorldIdentityIndex.cs`: disposition identities and retained references.

Reserve terminal-record headroom before admitting a new request. A full active window must not prevent
its terminal disposition. Include disposition-only histories in cut selection and progress accounting.
Preserve finite journal/archive limits; exhaustion must fail atomically rather than discard pending
work or claim unlimited archival capacity.

Capacity risk: the new frontier alongside eight existing treatment witnesses may exceed the current
4,096-byte continuation bound. Give the new interpretation an independently justified bound and prove
its maximum composite encoding. Do not silently raise the legacy format's limit.

## Explicit interpretation, codecs and storage

Propose a fresh profile 38 with an explicitly pinned disposition capability and Care V5. Select that
capability at admission even when the disposition collection is empty; do not infer support from the
presence of the first cancellation. Older profiles must retain the unmatched-request law.

New aftermath and continuation formats must carry disposition totals and the closed-defeat frontier.
Care V1–V4 must reject the new interpretation while retaining their bytes and constructors. Add an
explicit action codec version for the two intents and root/step dispatch for the selected new profile.

Affected integration includes `WorldSnapshot`, `WorldSnapshotCodec*`, `WorldCanonicalFrame`,
`WorldActionCodec`, `WorldStepBatch`, `WorldStepFrameCodec`, `WorldCareAdmission`, `WorldCareActions`,
`WorldCareProcessor`, recovery/controller/outcome admissions, and explicit Hosting profile/replay gates.

Retain `WorldBatchKind.Care`: exactly one command and no movement. Existing schema nine already stores
that commit kind, so a SQL schema increment is unnecessary unless implementation adds columns or changes
accepted commit kinds. This does not remove the need for explicit root, command and care format versions.
Lived profile-37 saves need a separate authenticated migration; opening a save is not an upgrade.

## Host, control return and presentation

Two current restrictions would make the new command unreachable without deliberate integration:

- `LocalWorldSession.ValidateEncounterSelection` excludes other local actions while care is pending.
- `WorldControllerBoundaries.CanRestoreHuman` blocks human control return while any care is pending.

Propose a narrow safe restoration rule under the new interpretation when the sole blocker is the
returning actor's pending-care decision. Retain paid-choice, retreat, staging and excavation exclusions.
Restoration leaves the obligation intact and starts the port unready. A separate narrowly authenticated
disposition route is an alternative, but the implementation must choose explicitly; otherwise a
disconnected patient cannot cancel.

Extend `LocalWorldSession.Players`, `TickCare` and care-command selection so an already queued exact
cancellation or decline is adjudicated before automatic treatment. The first committed terminal
operation wins. A competing stale command receives its own durable refusal; invalid request traffic
must not indefinitely starve treatment or closure. Preserve immutable captured requests and receipt
identity across reconnects and response loss.

Extend `LocalCareView`, observation DTOs/validation, captured offers and UI wording. The current care
projection is patient-only, so helper decline also needs an appropriately scoped helper-facing view.
Distinguish requested, cancelled, declined and treated states. Explain request closure separately from
outcome completion and ordinary recovery. Any observation-format change must preserve strict version
selection and legacy defaults rather than introduce fallback decoding.

Keep automatic policy separate from legality. A Host helper may select decline only under an explicit
policy and its own control binding. Do not conscript player helpers, infer cancellation from disconnect,
or substitute decline for an occupied/refused treatment selection. Any automatic disposition requires
a stable request-bound command identity and independent no-retry handling. Restoration precedes fresh
autonomous choice; paused/no-ready hosts must not generate new choices.

## Acceptance cases

These are required proposed evidence cases, not executed results:

1. Patient cancellation at HP0 and helper decline; wrong actor, control, request, episode or revision
   is refused without publishing a disposition.
2. Treatment versus cancellation/decline in both commit orders; exactly one terminal record, with a
   separate durable refusal for the losing stale request.
3. Closure preserves all non-care owners and outcome acknowledgments. It does not complete the
   encounter in the same transaction, heal, consume a dose or create resource evidence.
4. Ordinary recovery remains blocked until request closure and encounter completion, then uses a real
   independently permitted finite dose and its own receipt/time/resource history.
5. Re-acknowledgment of the same defeat is refused before and after cuts and through a different
   agreement. A later actual defeat can be acknowledged.
6. Cut while pending, dispose after that cut, then cut again. Preserve exact totals, original
   request/decision identities, closure chronology and the separate latest-treatment witness.
7. Disposition-only capacity pressure causes actual coordinated cuts. Prove terminal headroom,
   maximum composite frames and unchanged finite archive limits.
8. Save/reopen, response loss, exact retry and transaction/process crash boundaries preserve the
   original receipt and never close twice.
9. Retained legacy frames and constructors remain unchanged. Reject foreign policy pins, wrong
   counters/markers/context, duplicate terminals, impossible chronology and forged deciding actors.
10. A returning patient regains only permitted control, starts unready and can explicitly cancel.
    Other accepted-work restrictions and readiness semantics remain in force.
11. Observation round-trips and UI/input tests distinguish request closure from treatment, outcome
    completion and potential later recovery; inspection does not submit any action.
12. Autonomous policy tests prove no implied human authorship, no player-helper conscription, no
    automatic retry after occupied/refused selection and no fresh choices with no ready gameplay port.

Full native, restart/co-player, one-to-four-player control-return, production selection, lived-save
migration and broader campaign gates remain separate acceptance obligations. This proposal closes none
of them.
