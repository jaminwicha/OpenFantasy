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

## Next bounded implementation unit

Implement a **typed ownership-directory and coupled-write candidate for this same integrated episode**.
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
The authority still keeps the nonterrain locality resident and visits every current owner. Next bind
actual stored owner reads/reconstruction to complete directory/history dependencies in the same
saved episode; do not mistake re-encoding an admitted world for independent payload admission.
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
