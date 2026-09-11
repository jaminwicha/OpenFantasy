# Current shared agent handoff

Updated: 2026-09-11. This is the current-state index; evidence and failure dispositions remain in
linked packets. The preserved specification is authoritative. Read only task-relevant contracts.

## Start here

1. [Implementation instructions](../AGENTS.md): ownership, stable `OpenFantasy` runtime names,
   modular object-oriented C#, preservation, verification and shared-desktop discipline.
2. [Runtime setup and controls](../Runtime/README.md) for builds/runs.
3. [Full plan](../spec-v1_1/IMPLEMENTATION_PLAN.md), [requirements](../spec-v1_1/REQUIREMENTS.json),
   [technical design](../spec-v1_1/TECHNICAL_DESIGN.md) and
   [baseline handoff](../spec-v1_1/IMPLEMENTATION_HANDOFF.md) for the next dependency.
4. [Living UI supplement](PLAYER_INTERFACE_AND_NAVIGATION.md) and
   [partial coverage](ui-management/COVERAGE.json) for interface work. These refine the baseline,
   without reprioritizing phases. Later owner naming instructions override source naming only.

## Latest increment

[Finite watershed preparation](p01/watershed-preparation/README.md) adds a portable C# macro
candidate: explicit seed/scope, bounded grid/outlet, deterministic minimum spill routes, acyclic
plateau ranks, contribution counts, substrate/formation identities and identical shared faces.
Immutable windows and a versioned archive/host inspector expose the result. Maximum preparation
is 4,096 columns; that is a work bound, not the world's size. The farm does not consume it yet.

[Qualification](p01/watershed-preparation/qualification/verification-summary.json) retains 553 Core
and 317 Hosting tests (870 total, zero skips), independent Python watershed vectors, 74 real host
inspections and native foundation/farm evidence. The integrated run failed on a stale Talk command
in the legacy farm probe: expected player revision 29, actual 30. The corrected probe waits for
stationary arrival and matching presented revisions before one press; no action retry or timeout
extension. Only this probe changed after the broad tests; all five non-client/test binaries remained
identical. The original failure/save and the separate corrected native run remain recorded.
This is bounded combined evidence, not an uninterrupted full-verifier pass or a rendered review.

One [native critic](p01/watershed-preparation/REVIEW.md) found duplicate width-one neighbors in
the independent Python reference. Deduplication, a thin-grid vector and exact boundary counts address
it. No scoped runtime defect remained. This review does not close a full feasibility gate.

Next geography dependencies: authoritative candidate admission/persistence and compatible province/
basin composition; then explicit mapping into local terrain and conservative resource/fluid custody.
Do not regenerate published terrain from changed rules, mistake catchment counts for physical water,
or treat internal shared faces as proof of cross-basin seams. See the packet's remaining work and
[geological contracts](../spec-v1_1/GEOLOGICAL_WORLD_DESIGN.md) before extending generation.

## Existing playable scope and prior evidence

The authored farm connects movement, Wren's dialogue, finite gathering, ration crafting, a funded
six-wood quest, XP/reward and Wren's routed 200-work-action construction of a physical seedling guard.
This is one inhabited-place loop, not a general society or complete construction system.
Captured action offers carry exact target/cost/revision/intent data; rejection never silently retries.

- [Quick wheel and shared-desktop qualification](p02/shared-desktop-validation/README.md), milestone
  `codex/milestone-quick-wheel` at `c8989ae`: V/right-stick/Actions opens four pinned farm actions with
  inspection and explicit confirmation. Running time continues, pauses persist, and cancel/focus loss/
  disconnect do not act. Controlled rendered fixtures and the removed legacy refocus loops have their
  own evidence. Real OS/device acceptance remains open. The recurring Godot editor-settings Android
  SDK startup error has an unconfirmed cause; its failed integrated run is retained, not waived.
- [Daylight](p02/daylight/README.md), `80bdda0`: public-calendar outdoor light with independently bright
  UI and target marker. The nominal 40-minute day is unchanged. Weather, seasons, rest/camp and
  production art remain pending. Separate probe-only night followups preserve exact binary boundaries.
- [Recorded history](p01/recorded-history/README.md), `913a5f2`: privileged exact recorded-commit
  reconstruction, source-head pinning and read-only inspection. Synchronous full-journal baseline;
  arbitrary interior time, prehistory, branch travel and production streaming remain unqualified.
  The prior quest-probe movement/Talk race was fixed separately at `1d26210`.
- [Pack inspection](p02/inventory-inspection/README.md), `3eed739`: actual owned lots, stock meanings,
  recipe/tool inspection and scheduler status. I/View/Pack opens; cancel closes only that layer.
  Equipment, use and transfer remain pending. Its earlier rendered travel timeout remains unexplained.
- Other dependency packets: [explicit targets](p01/target-selection/README.md),
  [action offers](p01/action-offers/README.md), [input ownership](p01/input-focus/README.md),
  [construction](p01/construction/README.md), [quest content](p01/quest-construction/CONTENT_CONTRACT.md)
  and [older progress ledger](p00/PROGRESS.md). Their counts describe those checkpoints, not current code.

## Scope and acceptance

All **181 baseline requirements**, **14 unrun full UI cases**, full P00/P01/P02 exits and the campaign
through P14 remain open. Desktop production requires Windows/macOS/Linux and 1–4 hosted players;
mobile follows through the same core. Replay/recovery, temporal/co-op behavior, representative workloads,
exports/devices, content migration, art/audio, independent reviews and human acceptance need real proofs.

Keep one deterministic portable C# Core, an in-process durable host and Godot presentation. No new IPC,
parallel authority, Rust rewrite or framework without qualification. Preserve source/custody/command/save
contracts. Public projections are the UI boundary; the farm's sight-complete view is not full visibility.

The owner's concern about basic gameplay and poor graphics remains open. Production needs the accepted
restrained historical pixel art, varied sprites, expressive animation and state-driven weather/wear/audio.
Artists own asset production separately; [art calibration](../art-style-calibration/README.md) and
[presentation contracts](../spec-v1_1/PRESENTATION_CONTRACTS.md) require human style acceptance.
UI stays icon-led with rich inspection, curated wheels and capability-relevant screens. Current textual
pack/four-action wheel do not complete that design. The full spec retains the expansive world/history/
society/ecology/magic/construction/narrative scope; this index does not replace it.

## Execution discipline

Astra works directly by default. Each optional Claude task requires fresh explicit approval for its
bounded deliverable, benefit and finite usage limit; exactly `fable[1m]` if approved. No paid retry,
substitution or relay. Earlier permission and “continue” do not authorize another assignment.
[Prior evidence](p01/action-offers/delegation.json) records overhead and unverified credit savings.
Approved work uses [DELEGATION_CONTEXT.json](DELEGATION_CONTEXT.json) and the ecosystem's local
skill/docs/spec/steering synchronization hook. Assign file ownership; shared docs remain authoritative.

Build useful increments with focused checks; broaden at coherent integration/shared-behavior changes.
Critics address consequential risks and required gates. Preserve thresholds and failures; avoid duplicate
ceremony. Other tasks share desktop focus and resources: use headless checks or explicit background
fixtures, coordinate actual foreground acceptance, never repeatedly reclaim focus/close their games,
and never assume every failure is environmental. Ecosystem commit `eb7a836` persists this doctrine.

Preserve unrelated staged predecessor files (19 at this checkpoint), original saves, source archives and
frozen designs. Commit only owned coherent changes; follow [milestones](GIT_MILESTONES.md). Development
stays on `codex/runtime-foundation`; preserve previous markers without force-moving or switching them.
Choose remaining P01/P02 dependencies from the full plan, not automatic HUD expansion. Reusable skills
belong in the [ecosystem](../../../GameDevAIEcosystem/AGENTS.md); project facts stay here and in packets.
