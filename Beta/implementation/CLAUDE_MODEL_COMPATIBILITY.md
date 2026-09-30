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

The initial review preflight used an overly broad skill manifest outside its declared roots.
A narrow selected-skill manifest corrected this local configuration before the sole paid request.
Neither client installation nor a successful preflight proves that the model returned useful work.

## Portable guidance

Ecosystem `2e42704` refreshed standard context pins and model/effort guidance. After the worker
terminated, the guarded synchronization applied eight reviewed copies; all 71 mapped entries
verified. `aced220` adds the observed permission-denial lesson: an authorized coding assignment
should prove one owned create/edit early, stop on required-operation denial and retain the failure.
No silent permission broadening, automatic retry or second model conversation is warranted.
