# PLAN-STATE lead recommendation: typed owners with complete admission cohorts

**Recommend a hybrid:** persist meaningful typed actor, custodian, project/place, interaction and
timing owners under one directory and one SQLite authority; validate an action against its complete
**admission cohort**. Keep the existing domain operations and historical interpreters. Spatial
membership, storage residency and canonical ownership must remain separate.

This is the integration lead's decision proposal, supported by the integrated prototype and matched
measurement below. It is not an independently reviewed production-format selection, completed
PLAN-STATE exit or permission to freeze permanent NPC persistence. The orchestration pause remains
in force. No new schema/profile number, migration or default game selection is introduced here.

The timeless [ownership contract](../Runtime/docs/architecture/partition-ownership.md) specifies the
meaning of these boundaries. The [prototype contract](PARTITION_PROTOTYPE_CONTRACT.md) and
[current roadmap](CURRENT_ROADMAP.md) retain the acceptance requirements.

## Why this direction

The [terrain-separated prototype](p02/partition-state/separated-terrain-storage/RESULT.md) proves that
selected earned construction can use the existing rule kernel and durable authority without retaining
terrain detail. Its original-genesis episode includes real carried movement, a one-unit gift from a
two-unit holding, actual work, a return/intervention, completion, history and commit-fault recovery.
It preserves every supported profile-five owner; it explicitly refuses richer interpretations.

The [matched comparison](p02/partition-state/partition-decision/RESULT.md) executes that same earned
200-work-step episode through complete-root and terrain-separated publication in both execution
orders. Each pair has identical starting locality digest, all200 command receipts and final canonical
world bytes, with full original-history reopen. Both use internal host-selected locality bindings
(`CommitConstructionLocality` and `CommitWithoutTerrain`); the default gameplay scheduler is unchanged.
Timing excludes setup, input selection, assertions,
the intervening ordinary move and final encoding; both cases retain those actual actions.

| Observation | Complete-root publication | Terrain-separated publication |
| --- | --- | --- |
| Managed allocation across200 selected work commits | 276,628,288–276,784,272 bytes | 69,779,272–69,781,560 bytes |
| Time inside those publications | 488.828–553.413ms | 114.579–118.688ms |
| Allocation during complete original-history reopen | 267,430,136–267,435,488 bytes | 331,210,176–331,261,656 bytes |
| Reopen duration | 407.153–479.416ms | 393.303–405.356ms |
| Observed main/WAL/SHM file lengths after reopen | 618,496 bytes | 626,688 bytes |
| Completed full-world / locality-image bytes | 25,100 /24,511 | 25,100 /24,511 |

Exact observations and provenance are in [MEASUREMENTS.json](p02/partition-state/partition-decision/MEASUREMENTS.json).
These are two warm managed-process execution orders on macOS15.6.1 arm64/AppleM2, not statistical,
cold-cache, native-allocation, RSS, throughput, cross-platform or population-scale qualification.
The smaller head image is only589 bytes smaller in this fixture; the separate terrain table makes
the observed database larger. The reduced publication allocations support residency separation, but
full replay allocates more. There is no measured subscription-credit saving or production FPS claim.

## Alternatives and disposition

| Candidate | Disposition | Reason |
| --- | --- | --- |
| Keep one complete root for the expanding world | Retain as historical interpreter and correctness oracle; reject as the final frontier architecture | Full admission/serialization and local semantic limits remain tied to the complete population |
| Immutable terrain plus one nonterrain locality image | Keep as the concrete coarse comparison candidate; do not promote to default | Actual residency and publication improvement, but every stored step still serializes resident exposure and other unchanged owners; complete replay remains |
| Spatial chunks that each own their residents and cargo | Reject independent ownership by location | Walking, caravans, planar travel and cross-boundary encounters would migrate/copy unrelated facts or needlessly rewrite custody; complete query populations still cross chunks |
| One generic record/root per field or per item | Reject as the general architecture | Splits coupled invariants and provenance, increases directory/write coordination, and undermines the requested typed OO model without measured benefit |
| Typed stable owners plus operation-specific admission cohorts | Recommend for the next integrated candidate | Preserves meaningful domain responsibilities and joint admission while allowing unchanged owners and distant detail to remain unloaded |

Storage granularity is not identical to rule-validation granularity. One custodian can own a coherent
material store while a transfer validates two stores, actors, permissions, capacities and history
together. A construction project can retain one progress owner while its physical installation requires
all affected geometry and access-population evidence. Independent storage must not weaken that cohort.

## Concrete decisions to carry forward

1. **Stable actor/custodian keys are independent of place.** Use world, historical branch, typed role
   and stable subject identity. Realm/cell membership belongs to physical state/indexing, not a new
   actor identity. Terrain addresses retain their realm. Branching retains historical ancestry and
   cannot alias mutable owners across branches.
2. **Keep one authoritative fact and one publisher.** Reuse `SqliteWorldAuthority`, exact command and
   receipt identities, typed operations and SQLite atomic commit. Derived occupancy pages and cached
   execution views are not alternate owners. No Rust/C++, IPC worker, generic ECS or parallel simulator.
3. **Admission cohorts must be complete.** Preserve all current access/population and consequence laws.
   Current construction's effective16-distinct-actor access bound and the local64-actor spatial bound
   remain explicit constraints. Do not trim populations, treat missing detail as empty, or raise caps
   to make a partition demonstration pass. Dense places need their own qualified dependency strategy.
4. **Use a versioned typed envelope, not combinatorial permission.** Exact required facets, interpreter
   pins and supported combinations are validated at composition admission. Storage-only representation
   changes need explicit format dispatch and exact reconstruction; changed gameplay/time laws also
   need historical semantic versions. Old interpreters remain available for original history.
5. **Due work belongs to the world.** Persist domain membership and actual obligations; select using
   explicit deterministic causal order. A budget cannot advance shared time past unprocessed mandatory
   effects. Measure oldest due-work age per timing domain, separate from host wall time. The transient
   common exposure basis is useful evidence, not an already-persisted general clock solution.
6. **No dropped facets on migration.** The profile-five proof does not authorize migration of the
   current40/41 expedition. Preserve every nested owner, pending command, control/right, history archive,
   encounter outcome and resource witness. Unsupported compositions fail before a live write.
7. **Do not optimize recovery by skipping proof.** Keep complete original replay until checkpoint and
   dependency authentication are independently qualified. The experimental binding still needs its
   noninitial6000-step/10000-commit boundary proof before production promotion.

## Root-facet coverage required before a current-profile migration

This inventory comes from the actual public owners on `WorldSnapshot`; it is an allocation of design
responsibilities, not an implemented decomposer or an exhaustive proof of every nested field.

| Current root facts | Required disposition |
| --- | --- |
| `Inventory` | Split only through a complete nested field map: world/branch and definition pins, control bindings, custody/capacity/access, sources/holdings and reservations retain exact owners and historical identity |
| `Space`, `Residents`, `IdentityIndex` | Actor facts versus terrain/structures versus derived membership; personal experience and metadata retained; global historical reservations cannot be replaced by only currently present IDs |
| `AdmissionVersion`, `Revision`, `AdvancingSteps`, `Calendar` | Explicit manifest/admission lineage, commit sequence and timing-domain semantics; no interchangeable revision counter |
| `Dialogues`, `Progression`, `Quests` | Preserve personal progression, institutional/shared quest facts, participants, knowledge and reading-hold obligations; do not copy shared facts into each actor |
| `Gathering`, `Production`, `Construction` | Actual process/provenance/project owners, due work, reserved stock, installed backing and exact access dependencies |
| `Watershed`, `WatershedNetwork`, `Reservoirs`, `Transport`, `ReservoirSeals`, `LiquidCells` | Typed place/process ownership and conserved boundary obligations, not isolated-cell guesses or forgotten archives |
| `Excavation`, `ConstructionPhysical`, `StructuralPolicy`, `StructuralSettlementPolicy`, `CoupledSettlementPolicy`, `DescentMode` | Material/physical owner and exact pinned coupled-support/settlement laws |
| `ConsequencePolicy`, `PendingExcavation` | Original accepted command, reserved capacity/identities, bounded continuation and one terminal outcome preserved across all relevant owners |
| `Pacing`, `EncounterLifecycle`, `Retreat`, `EncounterOutcome` | Shared encounter/time owner, actor participation, actual earned costs and historical outcomes |
| `Care`, `ResourceHistory`, `Coordination` | Typed person/interaction/resource owners plus complete coordinated histories, proposals and nested market/material facts; shared facts and ancestry must not be silently flattened or discarded |
| `Controllers`, `Recovery`, `ControlPermissions`, `EffectiveControl` | Session/control/permission owners, original delivery identity, delegation and reconnect rules; no rights inferred from residency |

For each row, the migration design must enumerate nested fields, source/target bytes, owner key,
revision/clock policy, required read cohort, legal absence, visibility, replay and refusal witnesses.
Computed projections must be identified as derived. A checkbox for the parent property cannot certify
that private knowledge, a pending future consequence or a historical producer was retained.

## Integrated candidate obligations

Qualify a **typed ownership-directory and coupled-write candidate for this same integrated episode**.
The publication and selected-work portions are implemented below; this list retains their complete
comparison/migration obligations rather than assigning a duplicate implementation.
Do not start another series of isolated terrain-read helpers or a replacement save service.

- Specify stable actor/custodian/project keys, immutable exact owner revisions and complete bounded
  directory metadata. Walking changes physical/index facts while custody remains unchanged; a real
  gift changes the actual source and destination holdings together. Report all resident metadata.
- Construct operation-specific contexts through the shared rules. Keep complete-cohort admission
  where the existing rule requires it; report its actual reads rather than calling it selective.
- Publish changed owners, relevant indexes, time/due obligations, identities and receipts through the
  existing writer. Exercise first/final work and cross-owner gift faults, exact retry, partial return,
  ordinary intervention and original-history reconstruction. No duplicate sidecar owner of the facts.
- Compare changed payloads, total retained metadata, actual reads, publication and replay with the two
  existing candidates. Count complete resident materialization and history retention explicitly.
- Record the current-profile nested owner map and migration refusal boundaries before enabling live
  current saves. Keep noninitial checkpoint, due-work/fairness and cold/platform/scale proofs visible.

No production schema number is allocated by this decision. An explicit finite experimental format
may be qualified in its actual implementation; do not silently change schema13's meaning. The
independent architecture review and PLAN-STATE exit remain required before permanent NPC persistence.
NPC-01's causal behavior design can be prepared against the ownership contract, but cannot be represented
as a completed deep-NPC implementation or used to skip that review. The P02 closure table and P00–P14
scope remain unchanged.

## Implemented bounded candidate and next proof

The [typed-owner storage packet](p02/partition-state/typed-owner-storage/RESULT.md) supplies stable
scoped owner images and atomic directory publication for the legacy integrated episode. Its explicit
internal fresh schema14 preserves existing format meanings, full admission and original-history
replay; it does not change the default game or migrate40/41. The published payload roles include
both forms of custodian holdings, immutable provenance/definitions, actor/resident/control, projects
and every supported shared facet. Original history and cross-owner/first/final fault cases are retained.

This fulfills the bounded publication part of the next candidate, not the complete recommendation.
The authority still keeps the nonterrain locality resident and visits every current owner.
[Stored owner reconstruction](p02/partition-state/stored-owner-reconstruction/RESULT.md) now consumes
actual stored payloads under the replay-selected head, reconstructs and admits the complete cohort,
requires exact replay equality and supplies the recovered head. It is a complete-cohort decoder,
not selective residency or a substitute for ordinary history. The separate [selected stored publication](p02/partition-state/stored-work-publication/RESULT.md)
binds the operation-specific work and full due cohort to the admitted directory and sole writer.
It retains cross-owner invariants, full final admission and original history; general loading is
not established by this legacy interval.
General due discovery/fairness, nested current-profile coverage/migration, noninitial checkpoints,
cold/platform/scale evidence and independent architecture review remain open. The finite owner archive
limits are experimental storage stops, not accepted shipping limits or reduced campaign requirements.

## Documentation review

- **Plan/spec:** Replaces the unselected alternatives list with a lead recommendation and one concrete
  next integrated owner-directory unit; keeps migration, scheduling, review and phase gates explicit.
- **Reference:** `partition-ownership.md` records timeless ownership, cohort, timing, atomicity and
  history rules; persistence/reference indexes link to it without claiming those owners are implemented.
- **Accuracy/completeness:** Compared current root members, source dispatch and measured actual episodes;
  root-level allocation is explicitly distinct from the required nested-field migration audit.
- **Checks:** Two matched execution orders and17 existing storage/publication cases pass; documentation
  checks cover links/work-record structure, not independent architecture or human acceptance.

## Current-profile dependency review

[The current-profile map](PLAN_STATE_CURRENT_PROFILE_MAP.md) resolves an important selective-loading
ambiguity: command-local reads, published writes and mandatory due processes have different closures.
Current actor images carry resident exposure; partial construction still checks global reservation
stock and required-access actors. [Shared qualification](p02/partition-state/work-qualification/RESULT.md)
is extracted without changing the guards. [Selected stored assessment](p02/partition-state/stored-work-cohort/RESULT.md)
now uses complete source-established reservation membership and exact directory freshness. It does not
publish work or advance due processes, and retains complete site actor/structure geometry. [Stored partial publication](p02/partition-state/stored-work-publication/RESULT.md) supplies the bounded partial-work publisher, complete due closure and exact witness
renewal, widening to complete ordinary admission for final material work. The read-only qualification
remains distinct; general paging/fairness/migration and independent review are still open.

The map also records coordinated archive witnesses, pending care, private market knowledge,
controller/permission/effective-assignment distinctions and profile41 party withdrawal. These prevent
prematurely freezing legacy5 owner boundaries for the full game. This is a concrete preparation result,
not a completed nested migration or independent review; all existing PLAN-STATE/P02 exits remain.

## Noninitial checkpoint evidence

The [stored checkpoint qualification](p02/partition-state/stored-checkpoint/RESULT.md) supplies a real first advancing-step checkpoint during incomplete work,
followed by reopened owner recapture, completion and exact original-history preservation. Atomic
before/after faults include the due checkpoint row; altered/missing checkpoints and historical owner
loss refuse. This is legacy5/schema14 qualification of the existing publisher/recovery, not a new
checkpoint or interpreter design. No general/current-profile/cold/scale or independent exit closes.
Proceed to due discovery/fairness and remaining measurement/review obligations while preserving the
complete production and migration scope. Timings are local diagnostics, not a shipping budget result.

## Due-work discovery disposition — 2026-10-18

Use the [due-work/cutoff map](PLAN_STATE_DUE_WORK_MAP.md) before extending stored execution. A single
selected builder is not a proof of multi-actor scheduling. Preserve command selection, canonical
execution order and all mandatory effects in one logical advancing boundary; preparation slices do
not earn time. The existing calendar queue is a standalone component, not a bound world scheduler.
Its transition phase cannot silently move existing expiries before actions. The next executable
contrast and remaining fairness/domain/migration/review exits are explicit in that map. This source
review adds no runtime format, policy, acceptance or production-performance claim.

## Ordinary multi-actor reference qualified — 2026-10-18

The [two-host-builder proof](p02/partition-state/multiple-npc-cutoff/RESULT.md) qualifies ordinary
selection and complete-boundary effects/recovery before a partition preparation extension. Existing
legacy aggregate revision bounds differ from coordinated operation bounds; preserve both rather
than lifting a profile-five limit for a scheduling fixture. The next comparison still needs bounded
partial-residency preparation, actual due discovery and the remaining [map](PLAN_STATE_DUE_WORK_MAP.md)
contrasts. Twenty-two selected passes do not close general fairness, migration, scale or independent
review. No production source, format or rule changed in this qualification.

## Selected batches and preparation slices — 2026-10-18

[Stored-batch preparation](p02/partition-state/stored-batch-preparation/RESULT.md) extends the existing
profile-five owner interval to complete selected multi-actor batches. Dependency reads are sliced by
owner count and invalidated by a changed directory or failed read. Final evaluation and all due effects
remain one logical boundary, published atomically by the sole writer. It retains legacy aggregate
limits through whole-batch ordinary fallback. A selected project's canonical payload is decoded directly;
its revision must not be confused with the catalog's sum across projects. No new saved format or rule
is selected.611current passes include13new and fresh real-checkpoint recovery; retained production
failures and the narrower synchronous Host binding are explicit. General scheduling/current-profile
migration/measurement and independent review remain open.


## Matched three-candidate cost disposition — 2026-10-18

[The owner-cost comparison](p02/partition-state/owner-cost-comparison/RESULT.md) now executes the
same earned200-step build, carried gift, intervention, completion and original-history recovery through
complete-root, terrain-separated and selected typed-owner paths in three rotating orders. All command
bytes, receipts and final worlds match. This is warm local evidence on AppleM2/.NET8.0.31, not cold,
statistical, platform or scale qualification; capture/intervention/final/replay costs are explicit.

| Metric | Complete root | Terrain-separated locality | Selected typed owners |
| --- | ---: | ---: | ---: |
| 199 partial commits, managed MB | 275.06–275.08 | 61.90–61.90 | 102.79–103.09 |
| 199 partial commits, ms | 459.5–565.0 | 96.5–121.2 | 238.4–385.6 |
| Two captures, managed MB | 0.00–0.00 | 4.46–4.46 | 4.39–4.39 |
| Final installation, managed MB | 1.52–1.52 | 7.95–7.95 | 14.20–14.21 |
| Original-history reopen, managed MB | 267.52–267.52 | 331.29–331.30 | 364.40–364.42 |
| Original-history reopen, ms | 324.4–357.2 | 378.3–407.5 | 432.2–502.9 |
| Observed SQLite bytes including WAL/SHM | 618,496 | 626,688 | 2,846,720 |

The current typed implementation is more expensive than the coarse alternative for this small cohort.
Its independent owner-loading capability supports the architecture's residency goal; it does not
justify a general speed or space claim. Keep the recommendation conditional and the default unchanged.
Typed storage retains1,567 payloads/1,619,368 archive bytes here, while its current directory and payloads
are2,756/26,379bytes. Full ordinary world bytes are25,100. General oldest-due age is unavailable without
authoritative obligation/membership binding, not assumed zero.

Do not promote this candidate or add speculative caches to make the comparison attractive. Existing
source exposes complete-directory rebuilds, archive accounting, repeated selected reads, actor exposure
writes and full replay; profiling must attribute costs before optimizing them. Preserve exact history,
complete cohorts and final-admission work in any follow-up. The [combined due proof](p02/partition-state/combined-due-boundary/RESULT.md)
and noninitial-checkpoint qualification stand within their scopes. Live scheduling/discovery, current40/41
migration, remaining cold/platform/scale measurements and independent review still precede full acceptance.
The failed external-diagnostic lease attempt is retained as a fixture defect; no production law changed.


## Yielded authority preparation — 2026-10-18

[The yielded publication packet](p02/partition-state/yielded-publication/RESULT.md) makes stored
preparation usable across separate authority calls. The handle retains the selected batch and source
interval, releases the gate between bounded reads, refuses stale/foreign/cancelled use and publishes
all effects through the same transaction. The synchronous wrapper uses that path under its outer lock;
final/legacy-limit fallback remains ordinary. Seven new and28 related cases pass. No new format,
interpreter, default or second writer is introduced; the prior cost comparison keeps its original pins.

This authority packet alone is a prerequisite, not a completed general scheduler. The subsequent
[live construction binding](p02/partition-state/live-construction-slices/RESULT.md) supplies an explicit
internal profile-five/schema14 consumer: one selected NPC batch across ticks, readiness cancellation,
queued input, changed-source invalidation and shared committed observation. Complete preview/capture
and final projection remain, and public/default/current40/41 behavior is unchanged. General
obligation discovery/membership, oldest-due age, durable preparation, current-profile migration,
cold/platform/scale and independent review remain open. The conditional typed-owner recommendation
and all P02/production gates stand.


## Process discovery preparation — 2026-10-18

The [source-checked follow-on contract](PLAN_STATE_PROCESS_DISCOVERY.md) requires discovery from
post-action owners and the selected interpreter's clock. A blocked transport outcome is evaluated
mandatory work, not permission to discard an obligation or freeze all time until successful delivery.
Authenticated membership must distinguish missing owners from empty due sets. The next executable
contrast is regrowth plus reserved transport using their actual domain operations and lawful profiles;
this is preparation, not a saved queue/format decision or a replacement for independent review.


[Typed domain discovery](p02/partition-state/domain-process-discovery/RESULT.md) now joins the existing
gathering and transport operations without new persistence or time law. Complete-source reference affinity
and post-action invocation are explicit; this does not replace authenticated membership for a future
partial owner index. Keep the storage recommendation conditional and all remaining qualification exits.
