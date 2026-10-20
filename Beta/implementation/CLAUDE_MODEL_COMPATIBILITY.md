# Claude delegation compatibility

Updated 2026-09-29. Owner-approved routes are `fable[1m]` and Opus 5.5 with one-million-token
context, both at explicit Max effort. Astra integrates and reviews bounded assignments.

## Client and model selection

The native client is **2.1.280**, installed from the native distribution after preserving and
reverifying the 2.1.268 binary. The [official configuration reference](https://code.claude.com/docs/en/model-config)
identifies `claude-opus-5-5`, requires client 2.1.280 or later, supports explicit full model IDs
with `[1m]`, and documents Max effort. The [model specification](https://platform.claude.com/docs/en/models/opus-5-5/overview)
reports native 1M context. Use `claude-opus-5-5[1m] --effort max` for an authorized Opus assignment;
verify actual returned model/context before claiming the run used it.

Backup: `GameDevAIEcosystem/backups/claude-client-20260927T191711Z/claude-2.1.268`,
SHA256 `06a96d5423f83770f120859f1c58e60d7252cc4c122aa13043b7e7cd716bc76a`.
The installed launcher is `~/.local/bin/claude`. Restoring the preserved binary requires an
explicit maintenance action when no delegated process uses the shared client.

## Observed outcomes

- Completed retained Fable reviews report `claude-fable-5-1` and a 1,000,000-token window.
  These prove those runs, not universal availability or measured subscription-credit savings.
- The [world-resource Fable implementation](p02/world-resource-integration/failed-dispatch/README.md)
  ended with denied writes, no code changes and no tests. Native `dontAsk` denied operations;
  it was not waiting for approval. The scoped permission mismatch remains unresolved.
- The [first exact-selector Opus review](p02/world-resource-integration/opus-review/README.md)
  timed out after 900 seconds with no response, error text or usage metadata. Context checks
  passed and the frozen checkout stayed unchanged. Actual account model access, context and
  cost remained unconfirmed for that run. No automatic paid retry or model substitution followed.

- The owner subsequently explicitly requested Opus for the native gift controller repair. That
  [bounded assignment](p02/native-material-gifts/OPUS_DISPATCH.json) returned canonical
  `claude-opus-5-5`, a 1,000,000-token window and successful provider status; Max was explicitly
  requested. Pre/post context checks passed and the denial list was empty. It produced scoped
  input-owner code and a causal diagnosis; Astra reviewed it, tightened a focus-return boundary,
  and owns testing. Elapsed 868.279s and a $4.4244242 client estimate show substantial overhead for
  a narrow task. They do not establish subscription billing or net credit savings. This verifies
  this actual route/run, without promising later availability or permitting automatic paid retries.

- The [retained proposal transition assignment](p02/retained-proposal-transition/DISPATCH.json)
  returned verified Opus5.5/1M metadata with requested Max and verified context checks. It delivered
  Core/Host code, new tests and architecture references; 21 focused cases passed before integration.
  The adapter's final status is `permission_denied`: an optional Glob requested the parent temporary
  directory outside the granted task directory. Its broad regression returned only the Core half;
  the Host process was absent after worker termination and left no final report. No paid retry followed.
  Astra preserves the failed/incomplete records and owns completion. Elapsed1,576.339s and a
  $21.3186486 client estimate include 73,278,983 cached-read and164,333 output tokens. This is useful
  substantial code delivery, not proof of net credit savings. Keep task-directory discovery narrow and
  do not finish an assignment while a required asynchronous check remains unresolved.

- The [retained native gift assignment](p02/retained-gift-native/DISPATCH.json) returned verified
  Opus5.5/1M with requested Max, successful adapter/provider status, no permission denials and verified
  context checks. It delivered the selector, input probe, tests and docs; all ten focused cases and the
  exported fixture completed successfully. Duration1,943.293s; client estimate$10.8476332. Astra's three
  rendered starts missed the unchanged30s opening gate; a later normal active/reopen passed. Integration
  repaired the new saved-result test's archive-context and connection-lifetime handling; runtime rules
  and gates stayed intact. The integration packet owns final acceptance, not the worker's delivery.

- The [resource encoding assignment](p02/retained-resource-encoding/DISPATCH.json) also completed with
  verified Opus5.5/1M, requested Max, verified pre/post context and no denials. Duration1,419.851s;
  client estimate$10.9619998. It delivered three production-file changes, eight new tests,104 focused
  passes and a complete-replay allocation comparison. Its initial measurement overlapped its own
  tests; it retained that result and measured again after their completion. Astra reviewed source,
  ran broader integration and strengthened future measurement locking/source pins. These are useful
  substantial deliverables, not proof of billing or net credit savings. No model request was retried.

The initial review preflight used an overly broad skill manifest outside its declared roots.
A narrow selected-skill manifest corrected this local configuration before the sole paid request.
Neither client installation nor a successful preflight proves that the model returned useful work.

## Portable guidance

Ecosystem `2e42704` refreshed standard context pins and model/effort guidance. After the worker
terminated, the guarded synchronization applied eight reviewed copies; all 71 mapped entries
verified. `aced220` adds the observed permission-denial lesson: an authorized coding assignment
should prove one owned create/edit early, stop on required-operation denial and retain the failure.
No silent permission broadening, automatic retry or second model conversation is warranted.

## Atomic exchange observations (2026-09-30)

The [implementation request](p02/spot-exchange/DISPATCH.json) returned verified Opus 5.5 / 1M,
requested Max, verified pre/post synchronization and 373 focused passes. The provider completed
in 766.361s (client estimate $3.5727244); the adapter reported `permission_denied` for one optional
Bash read outside the granted wrapper. No approval was pending. The delivery omitted continued
paid material history and required adversarial contrasts. Astra completed those integration gaps;
this is useful partial work, not evidence of net credit savings. No paid retry occurred.

The distinct [independent critic request](p02/spot-exchange/CRITIC_DISPATCH.json) requested the same
route/effort with 900s / $8 client-estimate limits and read-only tools. It timed out at 900.182s with
no returned report or model/usage metadata. Pre/post synchronization verified. Requested routing
is known; returned model/context and cost for this particular request are unavailable. The first
implementation's verified route does not fill those missing observations. Its independent gate
remained open until an explicitly announced native critic completed a static review without
actionable correctness findings; no silent substitution or
additional Claude request occurred. This timeout adds review latency without accepted review value.


## Paid-world observations (2026-09-30)

Three distinct bounded requests used `claude-opus-5-5[1m] --effort max`. All returned canonical
`claude-opus-5-5`, a 1,000,000-token context, verified pre/post context synchronization and no
permission denials. The shared context hook required no skill copies. Max is the requested setting;
the result metadata verifies the model and context.

| Assignment | Duration | Client estimate | Useful result and limitation |
|---|---:|---:|---|
| [Implementation](p02/paid-world/DISPATCH.json) | 1,565.479s | $8.9076464 | Substantial world/Host code, but missing required tests and integration details. Astra completed profile selection, identity admission, causal revisions and gift coexistence. Its time-limit claim was incorrect: the request ended inside its 3,000s bound. |
| [Independent critic](p02/paid-world/CRITIC_DISPATCH.json) | 564.942s | $3.0958764 | Found an actual shared-capacity defect and noncanonical outcome ordering, plus useful future authority/disclosure dependencies. |
| [Lifecycle repair](p02/paid-world/LIFECYCLE_REPAIR_DISPATCH.json) | 1,170.458s | $7.6397666 | Implemented supersession, authenticated withdrawal, canonical ordering and meaningful full-book/cut/reopen tests. Its 10 Core and 15 Host cases passed; lead regression and required-evidence reconciliation are separate. |

These were implementation, independent critique and a concrete repair, not automatic retries of
failed requests. The critic's initial outside-Git preparation error occurred before any model call.
The incomplete first implementation still imposed lead integration work. Estimates are neither
billing nor demonstrated net credit savings. The [integration record](p02/paid-world/INTEGRATION_REVIEW.md)
owns acceptance and residual coverage; model availability alone does not prove completion.


The distinct [boundary-test follow-up](p02/paid-world/BOUNDARY_PROOF_DISPATCH.json) returned verified
Opus5.5/1M metadata with requested Max, verified pre/post context and no denials. Duration1,169.482s;
client estimate$6.8949898 under the1,500s/$18 limits. It delivered two new test files and eight passing
cases after retaining four invalid batch-fixture failures. Astra reviewed the tests and strengthened
caller readiness and exact refusal evidence. Runtime remained unchanged. The result is useful partial
completion: capacity/protection/late-reconciliation proofs and a race between eligible counterparties
remain open. Its initial claim that the larger fixture was outside assignment scope is incorrect;
a legitimate fresh genesis was explicitly permitted. This required lead correction, not a routing or
permission repair. No automatic paid retry occurred; no billing or net credit-saving claim is made.

## PLAN-STATE exit review (2026-10-20)

One read-only [critic](plan-state-exit-review/DISPATCH.json) used `claude-opus-5-5[1m] --effort max` with Read,
Glob and Grep only. The returned metadata verified `claude-opus-5-5[1m]` with a 1,000,000-token window, provider
subtype success, no permission denials and verified pre/post context over 413 inputs, with no skill copies needed.
Duration was 887.8 s and the client estimate $3.98, under the 2,400 s and $20 limits; the reviewer's own spend
estimate of about $3.50 was low. The client also made one 18-token Haiku call costing $0.0018; the review itself
ran on the requested model, so this is not a substitution. Before dispatch, the context hook refused stale skill
pins and an output directory inside a Git checkout; both were fixed locally before any model call.

The verdict, "accept with required changes", was specific and checkable: every cited figure and line held up on
re-reading, and one stated assumption was flagged as unverified. The [disposition](plan-state-exit-review/DISPOSITION.md)
applied every required change. No automatic retry occurred and no billing or net credit-saving claim is made.
