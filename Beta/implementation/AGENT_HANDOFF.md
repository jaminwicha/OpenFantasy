# Current shared agent handoff

Updated: 2026-09-13. This is the current-state index; evidence and failure dispositions remain in
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

5. [Architecture reference](../Runtime/docs/architecture/README.md): timeless ownership, source
   bindings and domain/design contracts. Read affected subjects, not the entire encyclopedia, for
   an implementation task. The [maintenance hook](../AGENTS.md#architecture-reference-and-maintenance-hook)
   requires same-change updates for architectural changes and a bounded source review at phase handoff.

## Architecture documentation

The [backfill record](architecture-reference/README.md) reconciles the reference with the retained
runtime. It separates executable domain contracts, actual application bindings and full design
obligations, especially history, Wait, local visibility and macro geography. Progress/evidence remain
here and in packets; frozen specs remain preserved. Documentation coverage does not close game gates.
Commit `2f91983`, preserved on `codex/milestone-architecture-reference`, records the reference and
project hook. Ecosystem commit `4e83086` records the portable hook and verified five-entrypoint sync.
All 123 reference links resolve; the bounded factual review corrections are retained in the packet.

## Latest increment

Code/evidence: terrain material/Production composition; the checkpoint is recorded in the
[milestone ledger](GIT_MILESTONES.md) and [packet](p01/material-production/README.md).

The registry explicitly attaches the existing Production ledger. Live raw claims plus direct raw
retirement cover every allocation once; recrafting outputs does not restore or recount inputs.
Actual instant craft requires transferred stock outside terrain representation custody. Reconciliation
and archive validation also reject direct coarse/fine consumption, retain exact prior batches/recipes,
and enforce production/calibration/identity and combined storage/lot limits. Detail changes preserve
consumed holes and complete state pins. New explicit v2 archives carry production proof; raw-only v1
bytes remain exact and cannot silently omit evidence.

[Qualification](p01/material-production/qualification/verification-summary.json): clean managed build,
**983 tests (654 Core + 329 Hosting, zero skips)**, 12 added cases, 20 new real-host inspections plus
26 registry and 24 prior-material inspections on the same binaries. Four valid archives receive an
independent direct-retirement/spatial coverage check; recipe mass and derived-output DAG proofs remain
managed. Native design/source critique found no remaining issues in scope. Missing APIs, compiler/test
helper errors and an incorrect test mass-allocation fixture remain retained, corrected without changing
gates. Full verifier/native journeys were not part of this additive standalone campaign.

This does not enable playable mining, admit material registries to durable worlds, compact long-lived
history or close a game/phase/feasibility gate. Timeless architecture references and maintained verifier
hooks are updated. Frozen specification, predecessor work and prior milestones remain preserved.

Prior dependencies remain preserved:

- [Shared registry](p01/material-registry/README.md), `b47fec7`, milestone
  `codex/milestone-material-registry`: 1–64 disjoint allocations, one inventory/terrain, normalized archive
  and shared immutable macro data. Its 971-test and maximum-network allocation evidence describes that
  checkpoint; it does not establish production performance or durable ownership.
- [Funded slabs](p01/terrain-materials/README.md), `163a7bb`, milestone
  `codex/milestone-terrain-material-allocation`: exact full-support source partitioning, physical calibration,
  remaining-stock demotion and raw-only v1 archive. Old single-state bytes remain pinned by fixture.
- [Dry terrain mapping](p01/terrain-mapping/README.md), `41865ed`, milestone
  `codex/milestone-watershed-terrain-mapping`: exact network/scale/datum/window geometry and ancestral
  addresses, separate from the stratified terrain interpreter and unable to allocate material.
- [Durable networks](p01/network-admission/README.md), `b2176e5`, milestone
  `codex/milestone-watershed-network-persistence`: fresh profile seven preserves complete networks through
  actions, process-kill recovery and exact history. [Network preparation](p01/watershed-network/README.md),
  `23851fa`, and [single-basin persistence](p01/watershed-admission/README.md), `bf96ed9`, remain preserved.

Next dependencies include complete physical macro profiles (crust/history, climate/forcing,
coast/reservoir/external budgets) and a retained local terrain interpreter. Integrate complete allocation
ownership, occupancy, construction, support, custody and exact history before live terrain admission.
Never reroll established facts, overwrite lived places or implicitly migrate saves. Follow the full
P01/P02 plan and [geological contracts](../spec-v1_1/GEOLOGICAL_WORLD_DESIGN.md). Development remains
on `codex/runtime-foundation`.

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
