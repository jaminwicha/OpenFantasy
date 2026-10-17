# Roadmap, gap and architecture review — 2026-10-17

> Owner-requested, read-only review by Claude Opus 5.5 (`claude-opus-5-5`) in a direct session. Owner
> direction, 2026-10-17: record it "for the main agent to work into its plans". It is **advisory
> input awaiting lead disposition**, not acceptance, an independent phase critic or a human gate.
> Following the 2026-10-03 no-orchestration steering, no subagents, external dispatch, builds, tests
> or games were run. Measured values and the commands behind them are in [METRICS.json](METRICS.json).

## Boundary and method

Baseline: HEAD `7f63b944` plus the uncommitted party-withdrawal world binding (checkpoint ref
`codex/checkpoint-withdrawal-world-wip-20261017T170920Z` → `3d81011a`). While this review was being
recorded, the lead was concurrently extending that binding: actor/control/collision checks through
`control-13` at 12:34 CDT, and live edits to the handoff, roadmap, decision and seven architecture
pages. Its linked `world-binding/RESULT.md` did not yet exist. **This review does not assess that
in-flight increment**; it uses only the bounded facts named below.

Inputs: [handoff](../AGENT_HANDOFF.md), [CURRENT_ROADMAP](../CURRENT_ROADMAP.md), the
[2026-10-03 audit](../roadmap-audit-20261003/RESULT.md) with its [critic](../roadmap-audit-20261003/ROADMAP_CRITIC.md)
and [Fable review](../roadmap-audit-20261003/FABLE_REVIEW.md), [efficiency disposition](../EFFICIENCY_AUDIT_DISPOSITION.md),
[remediation](../ORCHESTRATION_REMEDIATION.md), [withdrawal decision](../PARTY_WITHDRAWAL_DECISION.md),
selected architecture pages, frozen [plan](../../spec-v1_1/IMPLEMENTATION_PLAN.md)/[technical design](../../spec-v1_1/TECHNICAL_DESIGN.md),
and direct source measurement. Every finding was checked against the working tree on 2026-10-17;
the earlier audits were not taken on trust.

## Position

Agreement with the 2026-10-03 audit: P02 is active and partial, P00/P01 exits are open, and P03–P14
have not begun as phases. No completion percentage or date is supported. The authority (deterministic
C#, journal/replay, process-kill recovery, four-peer TLS) and the qualified solo and two-player
openings are real. The difference now is drift: the audit's correction to execution focus has not
yet reached the P02 rows that make the game feel inhabited.

Re-verified at the working tree: the **NPC, STORY, ECO and AUDIO** rows still have no source for
their core concepts. There are no cognition/belief/memory types, no chat, no season/predator/herbivore/lava
types, and no `AudioStream` use or audio assets in the Beta client. Each name search returned zero
matches (see METRICS for exact patterns and their limits). Fable's 2026-10-03 finding 1 therefore
still stands two weeks later.

## Findings for disposition

Each finding needs accept / adapt / reject with reasons, as the Fable disposition did. Suggested
placement names where an accepted finding would live in CURRENT_ROADMAP.

| ID | Kind | Finding and evidence | Recommendation | Suggested placement |
| --- | --- | --- | --- | --- |
| R1 | Architecture | **The complete world is one bounded root.** `WorldSnapshot` has 39 properties, 29 of them optional facets, encoded as one root under a 16 MiB bound. `LocalSpatialSnapshot` admits ≤16 regions and ≤64 actors. The spec requires a streamed frontier, the LANG-01 10,000-actor fixture and a 20,000-resident city. Streaming exists only as a design contract in [world-and-geography.md](../../Runtime/docs/architecture/world-and-geography.md#design-contract-finite-preparation-and-frontier-streaming). No design covers partitioned persistence, partial residency, cross-partition transactions or partition-aware replay/admission. Every added facet raises the cost of that change. | Before NPC cognition state is designed (R3), run a **bounded design spike** for partitioned world state with partition-scoped admission, journal and replay. Pair it with a small measured prototype, not prose alone. Keep pure C#, one owner per fact and atomic coupled effects. | New binding-decision row; dependency ahead of the P02 NPC row and the P03 LANG gates |
| R2 | Architecture | **Feature-driven profile ladder.** Snapshot versions: 38 at HEAD, 39 in the working tree (highest is 41). Step versions 38/39; action versions 23/24. The world root/step/action codec chain is 5,807 lines. All Core codec files total 16,445 lines, ≈26% of Core including legitimate domain codecs. There are 59 `*Admission` class names. Per [persistence-and-history.md](../../Runtime/docs/architecture/persistence-and-history.md#interpretation-and-admission-identity), extending one interpretation needs agreement across root/component/command codecs, admission, archive context, storage/replay, host selection and client compatibility. | Generalize the withdrawal review's "versioned facet" direction into a **per-facet version manifest under a root**: adding a facet changes the manifest, not a new root/step/action family. Add a **legacy-profile policy** (frozen read/replay with an explicit upgrade route) so supported saves don't grow without bound. Design this together with R1. | Same decision row as R1 |
| R3 | Architecture / design | **Cognition has principles but no technical design.** [TECHNICAL_DESIGN.md:133-135](../../spec-v1_1/TECHNICAL_DESIGN.md#L133-L135) and [actors-and-society.md](../../Runtime/docs/architecture/actors-and-society.md#design-contract-cognition-relationships-and-world-events) state the evidence→belief→commitment→memory principle. No document defines the data model, per-actor state/byte budget, decision cadence, local/offscreen reconciliation algorithm or knowledge-projection input. Current behavior is `LocalNpcController`/`LocalConstructionController` route policies. LANG-01's per-actor content (16 memories, 8 relations, 12 lots…) multiplied by actor counts collides with R1. | Write a technical design for one bounded causal NPC case at full applicable state, sized against the R1 outcome. Implement it as the first zero-source P02 slice (see sequence). | P02 NPC row prerequisite |
| R4 | Roadmap | **The four zero-source P02 rows remain unscheduled as concrete work.** The handoff's ordering places them after withdrawal, but nothing names their first deliverable, owner or design. Recent commits stayed on control, consent, recovery and history surfaces (Fable: most of the ~40 commits on Oct 2–3). | After the withdrawal increment, select the NPC row as the next coherent unit, with a STORY design and an AUDIO binding as concurrent lanes. Name owners for the art and audio lanes, or record that they are unstaffed. | Next execution sequence |
| R5 | Scope | **Withdrawal scope can expand indefinitely.** The decision requires carry, rescue, abandonment and surrender to be separate lawful actions, and none exist. A party with a downed member can only fight on or lose. Repeated decisions while waiting for allies are a flagged UX burden. | Time-box the remaining work to what the two-player expedition needs: clear incapacity refusal with a continuing combat/recovery path (already required by the decision), the explicit 40→41 transition, host offer and one native run. Treat carry/rescue as a separate design decision instead of extending this increment. | P02 COOP row / withdrawal decision |
| R6 | Design | **No content design for the investigation or the puzzle.** [MIGRATION_FIRST_LOOP.md](../p00/MIGRATION_FIRST_LOOP.md) gives one line: culprit, motive, evidence and at least two clue/skill routes, labelled as new content. No GDD-level incident, clue graph, testimony sources or puzzle exists. | Author a short content design (incident, culprit/motive, evidence placement, ≥2 routes, failure/recovery, team and noncombat puzzle paths) before STORY implementation. Reuse actual cargo, merchants and the road. | P02 STORY row prerequisite |
| R7 | Gate | **Performance feasibility is unmeasured.** LANG-01..04 are still planned-only in [BENCHMARK_FIXTURES.md](../p00/BENCHMARK_FIXTURES.md). The only measured four-player movement sample missed its lease: 151.9644 ms against 150 ms, with a 424.742125 ms scheduling gap ([diagnosis](../p02/movement-delivery-diagnosis/DIAGNOSIS.md)), still unexplained. | Use the R1 prototype as an early, reduced LANG-01 probe, explicitly labelled as not the gate. Keep the original failures and thresholds unchanged. | P01/P03 gate rows |
| R8 | Roadmap | **No content denominator.** All 12 entries in the [production catalog register](../p00/production-catalog-register.json) have no shipping target. | Lead proposes provisional finite targets per family for owner decision; null remains not zero. | P00 row; owner decision |
| R9 | Gate | **No human has played the game.** "Seek bounded human feedback early" recurs, but no session is scheduled, though the solo adventure is fully playable through the ordinary launcher. | Ask the owner for one short session on the ordinary solo route now. Record readability, action burden, pacing and HUD occlusion as an actual human observation, still distinct from acceptance. | P02 ACCEPT row |
| R10 | Hygiene | **Unreviewed text in the frozen spec folder.** [RESEARCH_SPEC_PASS_2026-10-05.md](../../spec-v1_1/RESEARCH_SPEC_PASS_2026-10-05.md) has been untracked since 2026-10-05. `SiteGenerator`, `TempleEmbedder` and `SettlementGen` exist only in the predecessor `src/`. `AtmosphereSnapshot`, `WaterTile`, `WorldGrid` and `KnockbackTiles` exist nowhere. It uses C++-style `TerrainType::Grass`, introduces balance numbers (+20% high-ground mitigation, impact-damage formula) and acceptance tests without requirement IDs, and links `file:///` paths. | Do not treat it as spec. Owner/lead decides whether to relocate it to an advisory research location with a disposition or discard it. This review did not move it. | Documentation hygiene |
| R11 | Hygiene | **Mixed index.** 19 unrelated predecessor files remain staged on the branch where the withdrawal work will be committed. | Commit only explicit Beta paths (never `commit -a`) and verify the staged set before each commit. | Handoff routine |
| R12 | Style | [WorldAdmissionValidator.cs](../../Runtime/src/OpenFantasy.Core/Worlds/WorldAdmissionValidator.cs) is 1,038 lines at HEAD (1,054 in the working tree), against the owner's "no giant managers" rule. | Split by owned facet when R2 changes admission; not a standalone refactor. | Folded into R2 |
| R13 | Process | **Evidence weight remains high:** 138 P02 packet directories and 6.5 GB of implementation records on disk (not comparable with the earlier 5.92 GB logical measure). No commits for 14 days, then a burst of concurrent activity. | No new governance. Apply the existing efficiency disposition: one shared runner pilot, accepted player journeys as the progress unit. | Existing efficiency doctrine |

## Recommended sequence

1. Close the in-flight withdrawal Core increment with its result record, then commit explicit Beta paths (R11).
2. Dispose of R10. Request the owner play session (R9) in parallel; it costs no implementation time.
3. Finish withdrawal only to the expedition boundary (R5), then the shared expedition run.
4. R1/R2 design spike with a measured prototype, before R3. This is design and measurement, not a rewrite.
5. NPC causal slice (R3): one person with evidence→belief→commitment→memory, dialogue that reflects the
   actual outcome, offscreen continuation and changed-place return in the profile-40 lineage. Run the
   STORY content design (R6) and a minimal audio binding (ambient, footsteps, one transition) alongside it.
6. Then recruitment/cooperative action, the investigation/puzzle, and the ECO/SPACE introductions, per the
   P02 closure table.

## Owner decisions requested

- Approve or decline the R1/R2 architecture spike ahead of P03 (it changes sequencing, not scope).
- Provisional catalog targets (R8), or direction for the lead to propose them.
- Disposition of the 2026-10-05 research file (R10).
- Availability for a short human play session (R9).
- Whether art and audio lanes should be staffed now (R4).

## Limits

Name searches prove absence of the named types/APIs, not of every equivalent concept. Line and file
counts measure surface, not quality or completeness. No build, test, native run or benchmark was
executed. Earlier packet claims are cited within their own scope. The in-flight withdrawal increment
was not reviewed. This review does not alter any requirement, gate, roadmap row or frozen document.

## Documentation review

- **Plan/spec:** Advisory input only; no frozen requirement, phase exit or CURRENT_ROADMAP row is changed. Findings R1–R13 propose roadmap placements for lead disposition. The handoff gains one routing pointer so the next lead sees this review.
- **Reference:** No runtime contract changed; no `Runtime/docs/` edit is warranted. R1–R3 identify missing design contracts (partitioned persistence, facet versioning, cognition model) that would need reference pages if accepted and implemented.
- **Accuracy/completeness:** Inspected the handoff, roadmap, 2026-10-03 audit/critic/Fable review, efficiency disposition, remediation, withdrawal decision, architecture pages (world-and-geography, persistence-and-history, actors-and-society), frozen plan/technical design, and the `WorldSnapshot`, codec, admission and validator sources. Counts were remeasured at HEAD and the working tree (METRICS.json). The concurrent in-flight withdrawal work is explicitly excluded. No independent critic is claimed.
- **Checks:** `check_documentation.py --work-record …/REVIEW.md --files …/REVIEW.md` passed ([DOCUMENTATION_CHECKS_REVIEW_ONLY.json](DOCUMENTATION_CHECKS_REVIEW_ONLY.json)). The run including `AGENT_HANDOFF.md` failed on one error ([DOCUMENTATION_CHECKS.json](DOCUMENTATION_CHECKS.json)): the lead's concurrent in-flight link to the not-yet-written `p02/party-withdrawal/world-binding/RESULT.md`, which is outside this record. The handoff pointer added here resolves. No runtime regression was run for this document-only record; no independent critic is claimed.
