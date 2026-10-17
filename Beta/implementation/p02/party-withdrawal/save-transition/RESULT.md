# Collective withdrawal: preserved-save transition

2026-10-17. Baseline `87c0c52a207c399761254442f67ea5cdf7ca6559`. This increment enables an explicit
copy-and-publish 40→41 save transition and schema12 recovery. It does not select41 in the ordinary
launcher, bind connected/native withdrawal offers or close P02. Work is direct; no orchestration or
independent critic was run.

## Delivered contract

- `WorldPartyWithdrawalTransition` and its strict codec pin one exact source head, world/branch,
  original admission and joint anchor. Only 40→41 is representable. The processor rejects stale,
  foreign and occupied requests and requires quiet exploration without a reading hold, active
  encounter/legacy route or pending physical continuation. It never clears work to obtain eligibility.
- The successor adds an empty withdrawal facet, advances the world/lifecycle revision once and
  preserves all other owners. No clock advance, fresh resource baseline, material/care cut, controller
  change, new actor receipt or replacement admission is introduced. Existing joint-anchor capabilities
  do not change, so copying the 27→30 migration's archive cut would be unnecessary and incorrect.
- `SqliteWorldAuthority.UpgradePartyWithdrawal` holds the original lease, copies to a private staging
  file, appends the typed migration/cause/head/checkpoint atomically and verifies complete replay before
  publishing a distinct unused destination. The existing publication helper is reused. Controller
  activation bytes, fenced generation and elapsed allowance hash are checked before and after replay.
- Schema12 retains its physical layout and admits exactly one replayed 40→41 interpretation transition;
  schema8 keeps its separate 27→30 contract. Fresh 41 uses schema12 with its own genesis. Ordinary open
  never upgrades. Inherited archive accounting and material/paid/market submission guards recognize 41;
  host session selection and native affordances remain separate work.

## Evidence

The final selected Hosting run is [regression-06](checks/regression-06.json), with its
[TRX](checks/regression-06.trx): **57 passed, zero failed/skipped**, including **12 new cases** and 45
existing withdrawal, legacy-retreat and controller-recovery cases. Build diagnostics contain no
warnings/errors. The preceding focused run [transition-04](checks/transition-04.json) passed 12;
those cases are included in 57, not additional distinct tests.

The new evidence includes:

- All unchanged public world facets compared by canonical hash or explicit value projection,
  retained lifecycle debt/clocks and old retreat evidence; same input yields the same successor.
- A nonempty controller allowance of 12,345 ms preserved through copied publication, an advancing
  post-transition commit and reopening; original source bytes unchanged.
- Fresh 41 creation/reopening without a migration row; malformed request bytes, unsupported source
  pair, foreign/stale admission/anchor, occupied cause, duplicate transition and used destination refusal.
- Five injected publication interruptions: after backup, before/after transaction commit and
  before/after publication. Only a fully published destination is selectable. These are exception
  boundaries, **not process-kill or physical filesystem durability qualification**.
- A tampered migration outcome rejected by replay, and a real active legacy encounter refused without
  cancelling it. The active fixture uses ordinary input replay, not direct mutation of encounter state.
- The retained two-player opening at revision 915, hash
  `f0781754a2a2dea1304ab1ea594ba53e7d6291820f29d61154a13508b08f33e5`, is copied and migrated to 916.
  Original genesis, all 915 prior journal entries, command receipts, occupied identities, earned crafting
  and completed construction remain. Original consented-transfer retries return byte-identical receipts
  without a new commit. The retained input file SHA256 is
  `cf21a8be1d99dc39aa2b856e8be0857c0c7b01b210a45911dd9fbab93e2c7a21`; tests read it and operate only on copies.

The additional legacy 27→30 replay check [passes](checks/legacy-transition-08.json), with zero skips;
its collection fixture also verifies the retained lived proposal-transition input. This brings the
final selected total to **58 distinct passing tests**, including 12 new cases. Final source/binary and
preservation identities are in [verification.json](verification.json). Existing full Core results
remain historical evidence within their prior scope; this increment does not claim another full sweep.

## Failures retained and remaining proof

`transition-02` failed to compile because the fixture referenced an internal frame writer and the
wrong movement property. It was corrected to alter the public codec's bytes and use the actual batch
property. `transition-03` passed 8 and failed 3 in the comparison helper: JSON cannot encode
`LiquidCellKey` dictionary keys. The helper now uses each facet's canonical digest where available;
no state-preservation assertion was removed. `regression-05` failed compilation on an incorrect
session-property name in the added receipt retry. `OriginSessionId` is the actual contract. Final
regression06 passes after these fixture corrections. No gameplay rule or gate was relaxed.
`legacy-transition-07` failed before executing its case because the required isolated-source environment
variable was absent. Run08 supplies a hash-verified temporary copy of the exact retained input; the
original file is never opened by SQLite and remains hash-identical. This was invocation setup, not a
reason to skip the legacy gate or invent a replacement source.

Still required: inherited and subsequent joint archive cuts around the transition; populated effective
assignments and relevant pending care/proposal work; genuine outcomes, opposing routes and whole-root
capacity; durable withdrawal across intermediate/arrived/failed samples and actual operator kills;
host compatibility/selection, connected own-character offers and the same saved cooperative expedition.
Paid market/commerce continuity needs actual post-transition action coverage, not only adapter admission.
Full 1–4-player, platform, latency, native/human and independent gates remain open. The original timing
failures remain unexplained. No new native game was launched or another application's focus changed.

## Documentation review

- **Plan/spec:** CURRENT_ROADMAP and AGENT_HANDOFF record the storage capability separately from the
  still-open archive/recovery/host/native qualification. RQ-029/079/099 and the party-withdrawal decision
  retain their full acceptance; no frozen spec or later roadmap scope changes.
- **Reference:** `party-withdrawal.md` describes exact request/ownership/publication/replay behavior;
  `persistence-and-history.md` adds the schema12 transition and removes the obsolete claim that only
  schema3/8 dispatch migrations; `controller-recovery.md` describes preserved operational provenance.
  The documents remain present-tense reference contracts rather than milestone narratives.
- **Accuracy/completeness:** Lead traced the processor, complete snapshot constructor, request/outcome
  codecs, journal dispatch, publication and archive accounting, controller activation/allowance owner
  and existing command guards. Actual source identity and earned-save rows are verified by tests.
  Connected/native integration and the missing durability matrix remain explicit; no independent
  assessment is claimed while orchestration is paused.
- **Checks:** The [scoped documentation checker](checks/documentation.json) passes all 7 affected pages;
  whitespace/source-preservation checks and final execution artifacts are retained in `verification.json`.
  Required selected terminal test runs pass as recorded above. These checks supplement the source
  review, not human or phase acceptance; independent review and the listed full-scope gates remain open.
  The initial whole-scope whitespace check flags two blank lines emitted by the failed legacy test
  runner (log/TRX). Those raw outputs are preserved byte-for-byte; authored code/docs/JSON pass the
  scoped whitespace check. The diagnostic is retained in `checks/whitespace.json`.

## Preservation

[CHECKPOINT.json](CHECKPOINT.json) records the pre-change ref at 87c0c52a. The accepted bounded marker is
`codex/milestone-withdrawal-save-transition-20261017`; it does not close the feature or phase.
Only the owned successor source/tests/docs/evidence enter this commit. The 19 staged predecessor files,
frozen specification and untracked October5 advisory source stay unchanged. The original shared-opening
database is not migrated in place or overwritten.
