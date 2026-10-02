# Native accepted care — integration result

Qualified bounded increment; complete campaign, phase and production acceptance remain open.
Preserved marker: `codex/milestone-native-accepted-care-20261002` (resolves to the accepted commit).

## Behavior and implementation

The real native journey starts from an unplayed production world at full health (60 HP), walks
away from and back to the encounter, enters through the existing controls, and incurs actual wolf
damage until defeat. The player acknowledges Wren's offered care. A separate helper-owned action
consumes one exact finite dose and restores 40 HP; acknowledgment itself neither heals nor spends
the dose. Combat continues in the same episode, reaches another real defeat, and completes after
explicit outcome acknowledgment. A second process reopens the same saved outcome without commands.
There is no forced damage, position shortcut, stock grant, prepared encounter or suppressed NPC rule.

The existing opt-in encounter entry and probe gain two care modes and a cohesive partial journey;
no duplicate scene or authority is introduced. New fixture/result tests authenticate production genesis,
every journal commit, exact selected commands/receipts, helper identity and dose interval, and final
recovery. Wrong-helper, wrong-dose and acknowledgment-only substitutes are rejected. The solo native
route does not prove invalidation of nonempty earlier readings; retained managed contrasts do.

Rendered review caught an actual player-facing error: the HUD still said treatment had not happened
while showing restored health. `FarmHealthFeedback.Observe` now returns only the newly observed change,
and the HUD announces a new HP increase once. Persistent sprite feedback keeps its existing lifetime;
initial/reloaded health does not replay old notices. The message describes the observed change without
inventing its medical cause. New focused tests and the fresh treated frame verify this correction.

Changed runtime paths are the existing encounter entry/probe, `NativeAcceptedCare/` probe partial,
`FarmGame`, `FarmHealthFeedback`, three new native-care test classes and `check_native_accepted_care.py`.
The README and the encounter-outcome/native-encounters architecture references describe the contracts.
Core/Hosting production authority, content, schema and game rules are unchanged.

## Verification and preservation

[Final checks](FINAL_CHECKS.json) pin the accepted reports and each qualified case by name:

- 2,354 distinct managed cases: 1,773 Core and 581 Hosting, covering all prior 2,348 qualified names.
- Fifteen fresh native processes: accepted care/reopen, outcome, road and retreat routes/reopening.
- All four current native authentication families pass; retained journals also authenticate.
- Twelve actual process kills across the two fresh durability families.
- Final build, full Core, Hosting, affected client, retained authentication and durability checks pass.
- 609 existing tests/fixtures, 23 frozen spec files, protected application/assets, original source save
  and the exact unrelated 19-file predecessor staging remain preserved; zero new compile links.

The final accepted build is `qualification/build-20261002T041616Z-d8117/report.json`.
All current affected evidence binds its source and binaries. Earlier 34 outcome cases and prepared
fixture bytes are inherited only by the explicit dependency comparison in the retained reports.
[Dependency review](LEAD_DEPENDENCY_COMPARISON.json) and [render review](RENDER_REVIEW.json) explain
these boundaries. Background/injected input does not establish physical input or human acceptance.

## Failures and review

The first care checker rejected a zero-length approach, because genesis begins beside the wolf.
The corrected route performs real native walking; genesis was not moved. A new test's mistaken API
name caused one build failure and was corrected. The first successful care image exposed the stale
HUD receipt described above; current rendered evidence verifies the fix. All failed reports remain.

Three metadata reference builds failed to reproduce older assembly bytes. Source equality therefore
did not qualify reuse: current authority checks were rerun instead. The failed comparisons remain;
generated revision/debug metadata are relevant inputs but not a proven explanation for every byte.
Existing earlier care timeouts remain unexplained. Neither new passes nor a running main loop establish
their cause or production reliability. No rule, assertion or deadline was weakened.

Astra reviewed the implementation and rendered frames. This is local integration review, not an
independent model review or a human playtest. The request changed no simulation authority contract;
prior independent outcome critiques remain in their original packets.

## Delegation economics

The direct requested Opus 5.5/1M/Max assignment ended after 179.564 seconds at the account monthly
spending limit, with verified pre/post context and only `DESIGN.md` delivered. The client list-price
estimate was $1.7358292; it is not an invoice or Astra savings measurement. No implementation or tests
were delivered. Astra implemented and qualified the increment locally. No retry, silent fallback or
account limit change occurred. [Delivery evidence](DELIVERY_RESULT.json) retains the terminal result.

Earlier bounded Opus work produced useful accepted code, including the resource serialization
optimization. Recent encounter deliveries were partial: the continuation took about 106 minutes and
the integration about 54 minutes, leaving material completion/repair work to Astra. Real offloading
occurred, but net Astra credit savings have not been measured. This latest request did not offload
implementation. The recent workflow cannot honestly be called reliably credit-efficient.

When provider access is available again, prefer self-contained deliverables with complete code,
focused tests and affected docs, and a short acceptance summary. Avoid repeated handoff/review loops
and integration work that requires the lead to reconstruct most reasoning. Judge usefulness from
accepted work and actual lead repair burden, not provider token volume or nominal dollar estimates.
The compact handoff is retained. Do not add accounting overhead disproportionate to the work.

## Remaining scope and next dependency

Actual multiple-session readiness and the no-survivor resolver route still need executable proof;
managed two-human tests do not close these native/session requirements. Lived profile34 migration,
general rescue/death/rewards, 1–4-player networking, full frontier/content/production scale and the
entire campaign remain open. Prototype art and text-heavy controls have no new human acceptance.
Use the full implementation plan for dependency selection; do not treat this milestone as phase closure.

Before-change checkpoints preserve 698778d and c74fc84. The separate WIP checkpoint
`codex/checkpoint-native-care-before-notice-20261002T041616Z` preserves 7dff05f before the HUD fix;
it is not an acceptance marker. The [local](LOCAL_CHECKPOINT.json) and [notice](NOTICE_CHECKPOINT.json)
records bind those snapshots. [Upstream efficiency sync](UPSTREAM_EFFICIENCY_SYNC.json) records the
bounded binary-investigation lesson in the ecosystem verification skill and three installed targets;
[render-wait sync](UPSTREAM_SKILL_SYNC.json) retains the separate earlier synchronization.
