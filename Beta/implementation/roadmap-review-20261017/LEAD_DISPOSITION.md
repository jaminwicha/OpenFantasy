# Lead disposition of the October17 roadmap review

Reviewed 2026-10-17 against committed `8fe31828cffa55002cff1ac6cb78253df4c21da8` and the
preserved [advisory review](REVIEW.md). This updates the [living roadmap](../CURRENT_ROADMAP.md)
and relevant timeless architecture contracts. It is a direct lead review, not an independent critic,
new model assignment, phase acceptance or runtime qualification. Orchestration remains paused.

The review is useful: bounded local state is not yet a world-partition architecture, and cognition,
investigation content, ecology and audio need concrete deliverables. Its historical metrics describe
the reviewed tree, not engineering velocity or acceptance. The withdrawal Core increment excluded
from that review is now committed; its [own result](../p02/party-withdrawal/world-binding/RESULT.md)
retains the 1,913 Core and 22 selected Hosting passes, failures and unrun migration/native gates.

## Findings and actions

| ID | Disposition | Reason and concrete route |
| --- | --- | --- |
| R1 | Adopt, bounded | The complete root is capped at 16 MiB and `LocalSpatialSnapshot` at 16 regions/64 actors; finite watershed networks and material-source partitions do not supply world partitioning. Add PLAN-STATE after the selected shared expedition and before permanent cognition persistence. Compare ownership, bounded residency/dependency closure, atomic cross-partition mutation, journal/replay and measured costs using actual owners. No new authority, IPC or wholesale rewrite is authorized. |
| R2 | Adapt | Evaluate a per-facet manifest with R1, not as an already-selected format or a promise that new features never change root/step/action semantics. Canonical formats, exact rule versions, legal combinations, command interpretation and compatibility still require admission. Keep legacy readers for every referenced historical segment; a live-head upgrade cannot erase arbitrary-time-travel history. No immediate codec rewrite. |
| R3 | Adopt | The inspected controllers implement bounded amble/construction behavior, not the required cognition model. NPC-01 starts with a technical design for one actual causal farm/merchant episode, including knowledge inputs, motives, feasible commitments, deferral reasons, memory/dialogue, state bytes, cadence and local/offscreen reconciliation. Size it against PLAN-STATE; fixture counts must not cap NPC depth. |
| R4 | Adopt with ownership correction | NPC-01 is the next new inhabited-world behavior after the expedition and PLAN-STATE. STORY-01, AUDIO-01/ART-01 and ECO-01 now have first deliverables, dependencies and witnesses. The integration lead owns technical/content design and bindings. Dedicated native art production and audio authoring are unstaffed, not assumed delegated lanes. Work is sequential under the owner's no-orchestration direction. |
| R5 | Adapt; retain gates | Keep the exact withdrawal transition, host offers and shared saved expedition as the bounded unit. Preserve incapacity refusal and a playable combat/recovery path, genuine outcomes, Full Wait, 1–4 participants, cuts/restart/reconnect/control changes, independent review and human evidence. One successful native run does not replace this matrix. Carry/rescue/abandonment/surrender need separate lawful action/outcome designs; no accidental expansion of escape and no free guard/Wait. |
| R6 | Adopt | STORY-01 authors the actual incident/clue graph, culprit/motive, real cargo and testimony, two clue/skill routes, team/noncombat puzzle, timing/difficulty, accessible alternatives and recovery. Test NPC-first resolution and unavailable witnesses. Reuse source people/places while marking new content; no engine-first puzzle framework without a playable design. |
| R7 | Adopt, diagnostic only | PLAN-STATE includes a small meaningful measurement before the full LANG gates: named hardware/runtime, population/state and transaction counts, bytes/allocations, cold/warm preparation, commit/replay/restore and oldest due-work age. Empty actors or extrapolated throughput cannot qualify the prescribed 10,000-actor/city/endurance/replay workloads. Retain the 150 ms lease miss and unexplained scheduling gap; no cause is inferred and no C++/Rust work is revived. |
| R8 | Adopt as explicit P00 deliverable | CAT-PLAN assigns the lead a finite typed proposal for all 12 catalog families before bulk authoring, with legacy minima, additive coverage and staffing/capacity assumptions. Owner decision is still needed for the targets. Null remains unknown, not zero; a count of NPC identities is not interchangeable with action kits or generated variants. No invented counts are approved in this pass. |
| R9 | Adapt factual claim | There is no retained structured human acceptance for the ordinary complete journey. That does not prove nobody has played: the owner's earlier launch and visual feedback are part of the conversation. A short ordinary solo playtest has been requested for readability, action burden, pacing and HUD occlusion. No session, final approval or fresh art calibration is presumed; prior concept approvals retain their scope. Implementation can continue while scheduling is unresolved. |
| R10 | Adapt, preserve and classify | Preserve the October5 note byte-for-byte as advisory research; do not make it a frozen-spec addendum. Unsupported APIs, balance constants and tests are not adopted. Detailed disposition below. Original input remains untouched and outside this commit's frozen baseline. |
| R11 | Already applied; retain | The preceding Core commit used explicit paths and preserved the 19 unrelated staged predecessor files. This document commit follows the same discipline and verifies the predecessor index hash. No broad stage/reset/commit command or new process is needed. |
| R12 | Adapt into R2 | Large admission files are a maintainability concern, not proof of a defect. PLAN-STATE evaluates facet-owned validation with explicit cross-facet invariants. Split by real responsibility when changing that boundary; do not start a cosmetic file-count refactor or duplicate world admission. |
| R13 | Adapt measurement and scope | Retain accepted player journeys as the unit of progress and reuse proven evidence/runner components. Calendar gaps and 6.5 GB on-disk usage are not measurements of active effort, credits or tooling latency. Pilot a common runner only when duplication or measured cost justifies it; no evidence deletion, mandatory artifact migration, new governance or documentation cuts. |

All 13 findings have a disposition; their future deliverables are **scheduled, not implemented**.
This is not a convergence claim or a clean independent audit. Required independent review remains
unrun while orchestration is paused. The full 181 requirements and P00–P14 exits are unchanged.

## Dependency and evidence checks

The source-backed boundaries behind the decisions are:

- [LocalSpatialSnapshot](../../Runtime/src/OpenFantasy.Core/Spatial/LocalSpatialSnapshot.cs) declares
  `MaximumRegions = 16` and `MaximumActors = 64`; its own comment assigns residency completeness and
  transaction ownership to the world owner. These local limits are not a city population contract.
- [WorldSnapshotCodec](../../Runtime/src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV1.cs) caps the
  complete canonical state at 16 MiB. [The complete frame](../../Runtime/src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV35.cs)
  reserves component capacities, including profile41's larger retreat field. Raising that number alone
  would not solve partial residency, due work or historical transactions.
- [LocalNpcController](../../Runtime/src/OpenFantasy.Hosting/Simulation/LocalNpcController.cs) selects
  movement and delegates actual construction obligations to
  [LocalConstructionController](../../Runtime/src/OpenFantasy.Hosting/Simulation/LocalConstructionController.cs).
  This is valuable retained behavior, not evidence of persistent belief/memory or omniscient access.
- [Persistence/admission identity](../../Runtime/docs/architecture/persistence-and-history.md#interpretation-and-admission-identity)
  and [journal recovery](../../Runtime/src/OpenFantasy.Hosting/Worlds/WorldJournalRecovery.cs) require
  explicit interpretation transitions, original admission/history and matching storage dispatch.
  The review does not authorize migration by replacing a profile number or rebasing history.
- [LANG-01..04](../p00/BENCHMARK_FIXTURES.md) require meaningful population and composed due work,
  mutation/history, endurance and replay/export equivalence. A reduced PLAN-STATE probe is a diagnostic;
  it leaves those gates, named-hardware budget decisions and existing failures open.
- The [catalog register](../p00/production-catalog-register.json) has 12 null shipping targets. The
  [first-loop source plan](../p00/MIGRATION_FIRST_LOOP.md) distinguishes retained records from authored
  recruitment, investigation/puzzle, ecology and presentation. New work must keep that distinction.

The runtime source, tests, codecs, package definitions and frozen requirements are unchanged by this
disposition. No runtime suite, benchmark, Godot launch or provider request is necessary or claimed for
these documentation edits. The earlier Core evidence retains its original dependency/scope limits.

## October5 research disposition

The [preserved advisory copy](advisory-research-20261005.md) has SHA256
`7f1e1dccd2ee1a0539990bd8aed764b3999245dfc300045668754e1cb26291a0`, identical to the untracked
`spec-v1_1/RESEARCH_SPEC_PASS_2026-10-05.md` input. The copy deliberately retains the author's wording;
its claim to be an additive specification is **not ratified**. This disposition and CURRENT_ROADMAP
define its advisory status. The original remains untouched, preserving another author's input without
sweeping it into the frozen baseline. Its future removal/relocation can retain this exact archive.

| Idea group | Disposition and existing scope |
| --- | --- |
| Read-only wind/water/fog presentation | Retain as candidate art techniques for P02/P04/P11 calibration, under the existing presentation/authority split. Named `AtmosphereSnapshot`, `WaterTile`, `WorldGrid`, shader paths and shader snippets are not verified successor APIs/assets. No shader algorithm or numeric displacement is selected. |
| Tectonic history, mineral provenance and ecological populations | Retain as research leads for P04/P09/P10 and the existing geology/ecology requirements. No decision requires convex spherical plates, all deposits along one boundary type or every ore vein within four tiles. Any chosen model must support the accepted frontier, era and conservation contracts. |
| Forced movement, hazards and high ground | Retain as P05/P09 design candidates under genuine body/contact/action rules. Do not adopt +20% mitigation, the impact formula or mandatory `KnockbackTiles` on every skill. Balance and applicable abilities need a concrete design and evidence. |
| Nonlinear sites and desire paths | Existing P07/P09/P10 culture/location/history scope already covers connected sites and evolving settlements. Predecessor `SiteGenerator`, `TempleEmbedder`, `SettlementGen` and `World.Settlements` are migration/research sources, not successor runtime owners. Do not require a universal 12-step detour or automatically convert footsteps into built cobble without materials/work. |
| Research citations and proposed gates | `SR-*` identifiers and local `file:///` links are leads, not verified primary evidence in this pass. Locate the actual source, validate relevance and map requirements before a future technical decision. Proposed tests neither supersede accepted gates nor establish implementation. |

No historical idea or source content is discarded. These candidates do not create a new immediate
research queue or displace NPC delivery; their actual phase owners evaluate them when relevant.

## Preservation and verification

Before roadmap/reference edits, checkpoint
`codex/checkpoint-before-roadmap-disposition-20261017T1802Z` resolves to
`f383672ddc76efee6168c50ddb89614ed9083124`, parent `8fe31828`. The
[checkpoint manifest](LEAD_CHECKPOINT.json) records exact hashes of the four external-review files
and the original October5 note. This is a preservation snapshot, not acceptance; it used a temporary
index and did not modify unrelated staging. The preceding accepted Core checkpoint is
`codex/milestone-party-withdrawal-core-20261017` → `8fe31828`.

The document-only milestone is `codex/milestone-roadmap-review-20261017`; its commit records the accepted
documentation scope, not an engine or phase qualification. The
[preservation checks](LEAD_VERIFICATION.json) verify the source checkpoint, unchanged inputs/runtime
and frozen baseline. Preserve
the predecessor index SHA256
`356898fb582b34244aa92b10c322e076422877ccba97e3ccc68b4fd6aa253aa5` and the original advisory hashes.

## Documentation review

- **Plan/spec:** CURRENT_ROADMAP now records the accepted R1–R13 actions, specific delivery units,
  dependencies and staffing; AGENT_HANDOFF routes to them. The full P02 closure table, all 181 frozen
  requirements, later phases and acceptance thresholds remain. Core41's committed status is recorded
  without asserting host/migration/native support. No new format or catalog target is accepted.
- **Reference:** Updated `persistence-and-history.md` with explicitly labeled partition/version
  extension obligations, `world-and-geography.md` with the distinction between regions and ownership,
  and `actors-and-society.md` with cognition ownership, evidence, budgets and reconciliation. These
  present-tense design contracts do not claim implemented partitioning or cognition types.
- **Accuracy/completeness:** Lead checked the concrete sources and dependent contract links above,
  preserved review/source hashes, current handoff, withdrawal decision/result, benchmark fixtures,
  first-loop source plan and all 12 catalog entries. Corrected the stale codec count and unjustified
  velocity language in the roadmap, the human-play inference and the manifest/legacy-support proposal.
  Original review metrics remain immutable historical input. Missing systems remain explicitly open;
  source-name searches alone do not prove absence of every equivalent behavior.
- **Checks:** `python3 Beta/tools/check_documentation.py --work-record
  Beta/implementation/roadmap-review-20261017/LEAD_DISPOSITION.md --files` with the six affected live
  pages/records passes; [terminal checker output](LEAD_DOCUMENTATION_CHECKS.json) records the exact scope.
  Lead reviewed the complete authored diff and `git diff --check` passed for the authored pages.
  Frozen baseline/runtime, review/archive hashes and predecessor staging are verified in the linked
  preservation checks. The immutable advisory copy retains its original two-space Markdown hard break
  on line3 rather than altering source bytes to satisfy a whitespace check. Independent review, runtime
  and human gates are unrun by this document-only pass, not substituted by a link check.
