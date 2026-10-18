# World partition and interpretation comparison

Working comparison for CURRENT_ROADMAP's PLAN-STATE. This is source-backed preparation, not a
selected format, completed technical design, implemented partition owner or measured feasibility
result. The [complete-root baseline](p02/partition-state/RESULT.md) supplies actual byte/allocation
measurements; the integrated prototype and decision remain required before permanent NPC cognition state.
The [review disposition](roadmap-review-20261017/LEAD_DISPOSITION.md) owns the rationale and sequencing.

The [integrated prototype contract](PARTITION_PROTOTYPE_CONTRACT.md) now makes the investigation
executable as a bounded implementation target: stable actor/custodian owners, separate spatial
membership and real cross-owner cargo transfer, admitted construction dependencies, earned exposure,
certificate invalidation, one publisher and an explicit episode/measurement matrix. Its ownership
hypothesis is not a selected production format. Shared execution and the joined durable proof remain
unimplemented; do not extend the geometry-helper series instead of that work.

## The problem to solve

Keep a persistent frontier with widely separated players and enduring individuals without requiring
all detailed state in memory or serializing the complete world for every action. Preserve exact
seed/rules, owned goods, construction work, private knowledge, clocks and arbitrary historical reads.
Remain pure C#, in process, with a single authority and atomic coupled effects. Partitioning must
improve this game without replacing its domain model with a generic property bag or a second simulator.

Concrete source constraints:

- [WorldSnapshot](../Runtime/src/OpenFantasy.Core/Worlds/WorldSnapshot.cs) is an admitted complete
  locality. Its residents, inventory, space and optional domain facets are validated together.
- [LocalSpatialSnapshot](../Runtime/src/OpenFantasy.Core/Spatial/LocalSpatialSnapshot.cs) admits at most
  16 regions and 64 actors; every supplied body needs complete supported, unoccupied space. These are
  local-query bounds, not shipping city or population targets.
- [WorldSnapshotCodecV1](../Runtime/src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV1.cs) declares a
  16 MiB complete-state envelope. More chunks in a file do not remove this semantic root limit.
- [WorldStepProcessor](../Runtime/src/OpenFantasy.Core/Worlds/WorldStepProcessor.cs) composes movement,
  counted/material custody, work, encounters and timing through a `WorldSnapshot` candidate.
  [WorldCommitPreparation](../Runtime/src/OpenFantasy.Hosting/Worlds/WorldCommitPreparation.cs)
  encodes that candidate and matches complete command/receipt identities before storage publication.
- [SqliteWorldAuthority](../Runtime/src/OpenFantasy.Hosting/Worlds/SqliteWorldAuthority.cs) owns one
  in-process state and dispatch boundary. [WorldJournalRecovery](../Runtime/src/OpenFantasy.Hosting/Worlds/WorldJournalRecovery.cs)
  verifies original genesis, admitted interpretations, archives, commands and each replayed result.
- [WorldIdentityIndex](../Runtime/src/OpenFantasy.Core/Worlds/WorldIdentityIndex.cs) indexes one local
  root and prospective namespaces. Its membership is not a global ownership directory, authentication
  or evidence of an absent historical cause.
- [LocalNpcController](../Runtime/src/OpenFantasy.Hosting/Simulation/LocalNpcController.cs) consumes
  the local world and selects ordinary movement/construction actions. Partition residency cannot
  silently redefine its time policy or make an unloaded worker forget a commitment.

## Alternatives to compare

| Alternative | What can be reused | Cost and failure risk | Evaluation |
| --- | --- | --- | --- |
| Storage chunks around the existing complete root | Existing admission, step processor and replay; content-addressed bytes may reduce storage duplication | Every decision still needs the complete locality and global root; semantic limits, full admission and resident scans remain. Partial file reads alone do not prove bounded simulation residency | Useful possible optimization/baseline; insufficient as the frontier architecture |
| Whole local aggregates as spatial partitions | Current typed facets, local collision and many transaction rules can remain cohesive | Actors, cargo, encounters and construction can span boundaries; each aggregate cannot independently own a copied actor or advance a shared clock. Transfer of a large complete aggregate could be expensive. Some current facets assume a complete roster rather than a partition | Serious lower-refactor candidate; prototype must expose coupled cross-boundary ownership rather than hide it in duplicated roots |
| Typed domain aggregates with a spatial ownership/index layer | Existing body, custody, work, clock and history rules remain typed; local spatial views can compose resident neighbours without owning their inventories | More explicit dependency closure, admission and migration work. Too-fine roots produce lookup/serialization overhead and large transactions; too-coarse roots retain the original bottleneck | Serious flexibility candidate; compare actor/household/place-sized aggregates against local aggregate reuse using the same episode |

Do not decide using file count or an empty dictionary benchmark. A hybrid may be appropriate: spatial
terrain/place aggregates, identity-stable actor aggregates, and separately owned shared encounters,
institutions and immutable definitions. That is a hypothesis, not an instruction to implement one
root per field, lot, actor or terrain cell. Aggregate granularity must follow coupled writes and measurements.

## Contracts the chosen design must specify

**Ownership and identity.** Distinguish stable entity identity, physical location, custody, partition
address and residency. A person moving east does not acquire a new identity, inventory or clock.
A canonical directory or deterministic mapping names the exact owner of each mutable fact, with a
revisioned transfer if ownership moves. Spatial neighbour references and read-only projections never
become extra owners. Shared immutable definitions use pinned content identity, not duplicate instances.
The directory itself needs bounded pages and authenticated history; one ever-growing manifest would
recreate the original bottleneck.

**Preparation and atomic publication.** Determine the read/write dependency closure, exact revisions
and relevant historical identities before deciding an action. A boundary move loads the terrain and
actual occupied bodies necessary for its complete sweep, plus the actor's commitments and cargo rules.
Missing or conflicting dependencies cause bounded preparation/deferral or a lawful refusal. They do
not count as empty space. Publish all changed owners, location/ownership indexes, command outcomes and
scheduled obligations together under one commit identity. Preserve the existing serialized authority;
there is no need for an IPC bridge or distributed consensus service.

**Composition and capacity.** Reuse local queries where their completeness and bounds are satisfied.
Do not assemble only 64 convenient actors in a crowded neighbourhood and omit the rest. Dense areas,
large bodies, vehicles, structures crossing boundaries and interacting fluids require an explicit
bounded query/dependency strategy; exceeding a preparation budget must be visible and deterministic.
Record which spatial, custody, encounter and cross-facet invariants need joint validation, and which
facet validators are safely local. A generic container cannot declare arbitrary combinations legal.

**Time and offscreen work.** Residency, relevance, decision cadence and timing domain are separate.
Persist next-due obligations and their interpretation, with deterministic tie order. Reloading cannot
restart a crop, repeat a reward, skip an encounter hold or manufacture construction progress. Reuse a
real funded construction commitment for the first detailed/offscreen comparison. Define the admitted
boundaries where batching equals detailed continuation; if order-sensitive contacts or interventions
need finer work, defer or subdivide within a bounded policy. Do not infer an unimplemented global clock
from the current locality's reading/encounter holds.

**History, save and replay.** Every historical address resolves the then-current ownership map,
partition revisions, original admission, component laws and archive ancestry. A current head upgrade
cannot erase interpreters still referenced by older states. A global commit sequence can coordinate
partition deltas, but a partial replay must prove its dependency closure and authenticated result;
loading a convenient checkpoint is not independent verification. The same concerns apply to branches,
planes, possession transfers and imported historical objects.

**Version selection.** Compare a canonical facet manifest with explicit aggregate interpretations.
Distinguish format versions, simulation law/content pins and supported combinations. Keep command,
step, root-envelope and protocol versions whenever their meaning changes. Unknown required facets or
unsupported combinations refuse explicitly. Optional data must have an admitted omission policy; it
cannot be silently dropped because one client or interpreter is old. Share readers only where their
semantics are actually identical. No version number or SQL schema is reserved by this comparison.

**Player presentation and network scope.** The Godot adapter consumes permitted observations, not
partition-store access. Ownership/loading metadata must not disclose hidden inventories, NPC memories,
unexplored locations or secret relationships through different refusal messages. Loading does not
revoke valid controls or reset a player's action; the host reports readiness and any deferred operation
through ordinary contracts. Presentation visibility and collision residency need not have the same radius.

## First integrated proof and measurement

Use actual existing character, material custody, terrain and funded-work types, with a persisted
world containing relevant and irrelevant partitions. Keep the current complete-root implementation
as a comparison baseline, not a competing production simulator. For each candidate:

1. Move a real actor with owned cargo across a boundary; retain stable actor/lot identity, quantity,
   control, position, time and references. Stable actor/custodian owner keys need not change when spatial
   membership changes. Include collision on the destination side and stale membership/owner revisions.
   Then execute an actual cross-owner cargo gift to prove atomic custody transfer, distinct from walking.
   No duplicated goods or partially published movement/transfer is acceptable.
2. Continue an actual paid NPC work commitment while its detailed region is absent. Reintroduce a
   relevant participant, compare declared detailed/offscreen boundaries and show exact remaining work,
   resources and due ordering. No fabricated cognition or blank actor payloads count as meaningful state.
3. Interrupt publication before and after its atomic boundary; reopen and retry the exact command.
   Verify complete old or complete new state, original receipts, ownership map, occupied identities and
   work obligations. Fault injection is not a substitute for any later required process-kill gate.
4. Reconstruct a historical state before and after crossing from the original admission, then replay
   the continuation. Prove unrelated detailed partitions were not silently loaded to pass the test.
5. Measure the identical workload: actual hardware/runtime, admitted actors/lots/work/terrain and
   transaction counts; bytes stored and loaded, residency/high-water memory, allocated bytes, cold/warm
   preparation, commit, replay/restore, boundary-load stalls and oldest due-work age. Record rejection
   and missing-dependency paths as well as success. Separate setup, decoding and rule execution.

Choose small initial population/partition counts from the real admitted fixture and record them before
measurement; do not imply they are production caps. Increase one dimension at a time while keeping
meaningful holdings/work. Report full source/binary/settings identity and instrumentation overhead.
The result is a reduced diagnostic. [LANG-01..04](p00/BENCHMARK_FIXTURES.md), 20,000-resident city,
10,000-actor meaningful fixtures, sustained 1–4 players and desktop exports remain separate gates.

## Measured baseline and concrete extraction boundaries

The [baseline result](p02/partition-state/RESULT.md) records current-format movement and paid
construction, legacy construction, all measured fields and original-history reopening. Current-format
movement changes 32,427 payload bytes out of an 86,668-byte root; one paid construction step changes
16,281 out of 80,752 bytes. These are different admitted populations, not an equal-workload benchmark.
The legacy construction root nests its quest basis, so its near-total outer-field change says nothing
about the fraction of individual facts that changed. Do not select aggregate boundaries from positional
fields or conclude that a smaller legacy root is cheaper to operate.

The source explains why simply storing actors in separate files is insufficient:

| Actual writer or validator | Coupling that the prototype must preserve | Extraction needed for a bounded resident world |
| --- | --- | --- |
| `WorldStepProcessor` advancing branch | Advances calendar/pacing, each resident's experienced time, gathering, liquid flow, encounter lifecycle and due offers during an ordinary step | Explicit timing-domain and due-work ownership. Measure both preparation and the due work caused by the action; movement-only timing cannot represent the transaction. Preserve the existing Full Wait/reading rules in legacy interpretations. |
| `WorldResident.ExperienceStep` | Recreates each resident with accumulated exposure, even without a positional change | Evaluate a canonical time basis plus exact domain membership/hold intervals before assuming actor roots remain unchanged. Materialize the same experienced time at reads and actions; joining, leaving, switching domains and interventions must not accrue it twice. This is a prototype candidate, not an admitted new clock law. |
| `ConstructionSnapshot.ValidateComponents` | Joins completed quest funding, inventory policy, transforms, installed lots, builder time and required access; every same-floor actor in the supplied complete space must have an access witness | A project needs an explicit dependency boundary spanning stock, builder, site, funding and access. Do not discard distant actors from the existing complete-world validator to make a sliced view pass. Any narrower access contract requires its own interpretation and proof, with the old validator retained for historical roots. |
| `ConstructionSourceFrame.ValidateSpace` | Standalone material continuation retains exact source actor/terrain bytes | Existing component continuation is not permission to substitute a partially loaded source. Reuse its material rules after supplying a deliberately admitted dependency context, not an incomplete `WorldSnapshot` cast as the original. |
| `WorldCommitPreparation` and `WorldJournalRecovery` | One authenticated batch, complete candidate, command receipts, identity occupancy and original-history replay | Retain the single Hosting publication boundary; evolve its candidate representation to a bounded set of validated owners plus exact read revisions. All directory, custody, work and receipt updates commit in the same SQLite transaction. |

Prototype the **typed aggregate plus spatial index** candidate first, while keeping whole-local
aggregate reuse as the comparison alternative. This selects an investigation order, not a shipping
format or an approved engine replacement. The data already rules out treating storage-only chunking
as sufficient frontier support. Independent review and the integrated proof still precede acceptance.

The first extraction should establish these concrete responsibilities, in the existing modules:

- **Core ownership/admission:** identity-stable actor and custody owners, place/project owners and a
  revisioned location directory. A bag's owner/custodian and its physical whereabouts remain different
  facts; moving a person must not reissue the bag's lots or clone it into both locations. Immutable
  definitions remain content-pinned. Directory entries need bounded pages, not a complete-world list
  embedded in every action. Directory history and occupied-identity history are separate obligations.
  The [carriage extraction](p02/partition-state/carriage/RESULT.md) supplies the shared physical-material
  reader now used by structural mass calculation. It selects actual carried lots without confusing
  withdrawal ownership with physical location; it neither admits partition owners nor establishes
  dependency completeness. Retain its installed-backing and legacy-interpretation distinctions in
  the prototype's owner design.
- **Core dependency preparation:** an action-specific read set and write set, including complete
  swept-body/support geometry, neighbouring actors/structures, custody and actual obligations. Exact
  source revisions belong to the plan. Missing dependencies defer/refuse before authority changes;
  a stale plan cannot be committed after a neighbour, control or custody change. Reuse actual spatial
  and material operations; never create a second simplified movement or construction simulator.
  The [terrain read boundary](p02/partition-state/terrain-reads/RESULT.md) now prepares an explicitly
  supplied volume, rejects incomplete/ambiguous references, and loads exact region archives with
  separate byte and decoded-cell budgets. Its file-backed geometry comparison omits irrelevant detail.
  The [movement footprint](p02/partition-state/movement-footprint/RESULT.md) now derives ordinary
  cardinal terrain/support coverage from the actual actor, including the full stair probe envelope.
  [Prepared spatial reads](p02/partition-state/spatial-read/RESULT.md) now select and revalidate relevant
  actors and structures from the supplied complete scene, including new arrivals, then evaluate through
  the shared ordinary query implementation. A complete action still needs authoritative directory,
  material, timing and permission dependencies; no world-level partial-residency claim follows.
  The [control dependency boundary](p02/partition-state/control-dependencies/RESULT.md) supplies exact
  current controller/binding/permission rows and their absence checks using the ordinary authority
  rules. It deliberately ignores unrelated world revisions and gameplay time; those still belong
  in the complete action transaction. Validating unchanged spatial data cannot substitute for control
  revalidation, and historical recorded authority cannot authorize new input after revocation.
  [Joined movement prerequisites](p02/partition-state/movement-prerequisites/RESULT.md) now combine
  these reads with exact carriage, readiness, experienced time and clock/hold interpretation. The
  ordinary processor remains mandatory: it applies actions and time before movement, including
  suppressing a selected movement when that actor performs work. A geometric candidate cannot bypass
  this ordering. The real region-crossing/replay case is not a partition-ownership transfer or partial
  simulation claim; complete step dependencies and atomic owner publication remain unproved.
  [Paged occupancy preparation](p02/partition-state/spatial-directory/RESULT.md) adds exact bounded
  metadata reads and selected detail loading without a complete local scene. Explicit empty page pins
  detect arrivals; inconsistent copies and missing detail refuse. The 200-presence file fixture keeps
  all directory metadata resident but loads only selected geometry; it establishes neither a global
  owner nor a memory high-water budget. Directory publication/completeness, actual ownership transfer,
  full joined world admission, absent-detail work and atomic history remain the integrated proof.
  [Complete occupancy-index updates](p02/partition-state/occupancy-publication/RESULT.md) now keep
  every cross-page copy and identity reference consistent in one immutable successor. A derived adapter
  binds exact complete world roots, and saved movement/cargo/receipt history reconstructs index pins
  through publication interruptions. This advances index mutation, not canonical owner transfer or
  partial simulation. All metadata remains resident; empty-page history consumes the explicit budget.
  Coupled actor/material/work roots and durable index publication still need the integrated design.
  [Prepared route queries](p02/partition-state/prepared-routes/RESULT.md) share ordinary ground/stair
  search with bounded selected geometry. Extra decoded terrain outside the declared occupancy boundary
  cannot supply a detour. This addresses construction's required-access query dependency; it does not
  remove complete placement/admission or authorize time-only offscreen work. Supply, tool, control,
  exposure, site, all required access and eventual material/identity publication still form one work step.
  [Prepared placement](p02/partition-state/prepared-placement/RESULT.md) now shares structure
  collision/support, known spatial identity checks and prospective access routes with ordinary
  placement. It returns geometric status without constructing a partial scene. It deliberately does
  not certify omitted access obligations, distant identity uniqueness, strength or actual work funding.
  [Shared construction execution](p02/partition-state/construction-execution/RESULT.md) now evaluates
  coupled material/project candidates through the ordinary rule kernel, using exact detached source
  facts and reusable site evidence. The final candidate can execute after source world/scene/index/
  terrain collection. Whole-world capture/freshness and unsupported terrain-retaining material contexts
  remain explicit; this is not an admitted partially resident world or a durable offscreen scheduler.
  [Authority integration](p02/partition-state/construction-authority/RESULT.md) now reuses site evidence
  within ordinary selected batches and SQLite publication. Full candidate/receipt/identity equivalence,
  hold ordering, stale dependencies and preview/commit recovery are tested. This removes repeated route
  qualification without substituting a work scheduler; full world residency and encoding remain.
  [Locality continuation](p02/partition-state/construction-locality/RESULT.md) now composes actual
  profile5 nonterrain owners with selected work, shared identity/time rules, due gathering and all
  resident exposure across an interval without retained terrain. Exact reconstruction and outcome
  comparisons address sequential execution, not merely one detached candidate. It remains a complete
  locality aggregate; stable per-owner partitioning, movement/gift, current-profile coverage and durable
  owner publication remain open. Profile40/41 refusal prevents silent facet loss.
  [Construction publication](p02/partition-state/construction-publication/RESULT.md) establishes the
  complete-locality durable baseline: the sole writer publishes these selected decisions, joined to
  real carried movement/gift and intervened work in one original history. Ordinary replay agrees at
  every selected work step and after partial/final reopening and commit faults. Complete root residency
  and encoding remain mandatory; this is not the typed-owner or absent-detail publication candidate.
- **Core time/work:** deterministic due obligations reference stable owners and the interpretation
  needed to execute them. Absence from presentation is not permission to drop a worker or change its
  clock. Start with the actual funded construction case; detailed/offscreen equivalence is checked
  at declared boundaries, including interruptions and a return before completion. Do not make the
  distant case pass by keeping all its detailed terrain resident.
- **Hosting persistence:** prepare through the existing authority, then atomically publish changed
  owners, directory/due-work changes, command outcomes and commit ancestry. Record immutable owner
  references and semantic version combinations. Original readers remain available for historical
  addresses; explicit copied-source admission must account for every legacy facet before enabling an
  upgraded live world. A successful profile-one experiment cannot silently migrate a lived profile41.

Do not start bulk cognition state or a broad codec conversion while these boundaries are unresolved.
The next executable milestone remains the crossing/funded-work/crash/replay proof above, including
irrelevant detailed state that is actually absent and measured rather than merely labelled offscreen.

## Decision and rollout boundary

Select an aggregate/manifest approach only after this comparison has concrete measurements and a
source-based assessment of all preserved writers. Specify the actual module/API and byte contracts
before publishing new persistent state. The first implementation must be a retained increment of the
intended Core/Hosting architecture with real legacy/domain consumers, not an isolated toy benchmark.

The rollout design must identify: explicit preserved-source upgrade; old historical readers; new
admission and legal combinations; atomic storage/recovery; host selection and compatibility; permitted
public views; rollback and refusal behavior. No implicit migration or destructive rewrite is approved.
Independent architectural review remains required at the substantial adaptation boundary, and is
currently unrun under the owner's orchestration pause. Preparation and measurement can proceed directly;
do not invent independent acceptance. Keep all original requirements and unproved scope visible.

The decisions still open are aggregate granularity, directory representation, resident dependency
closure, due-work ownership, canonical envelope/manifest, migration interpreter and measured budgets.
Their trigger is the integrated proof above, before permanent cognition persistence. This document
provides the comparison and proof scope; it does not close PLAN-STATE.

## Documentation review

- **Plan/spec:** Preparation for the already accepted PLAN-STATE dependency. No new phase order,
  workload threshold, format, resource grant or frozen requirement is selected. The living roadmap
  must continue to require measured implementation and decision before permanent cognition.
- **Reference:** Existing timeless partition/version obligations remain accurate; this comparison
  introduces no runtime binding. Concrete source links above distinguish present ownership from
  proposed responsibilities. Update the reference with the chosen actual contracts when implemented.
- **Accuracy/completeness:** Reviewed WorldSnapshot, local geometry, step/commit preparation, identity
  index, durable authority/recovery and NPC controller sources. Candidate interfaces are intentionally
  not represented as existing APIs. The difficult cross-owner, clock, history, density and privacy
  obligations are explicit rather than deferred behind a storage-only prototype. The measured baseline
  adds source-backed clock fan-out, nested legacy fields and construction's complete access/source
  dependencies; none is represented as an implemented partition abstraction.
- **Checks:** Initial documentation checks are recorded with the adjoining expedition continuation;
  refinement and baseline checks are in the linked partition-state result. The baseline is a local
  complete-root diagnostic, not the integrated partition benchmark or independent design acceptance.

## Resident exposure representation experiment

[Exact exposure basis](p02/partition-state/resident-exposure-basis/RESULT.md) integrates common earned
exploration exposure into legacy locality execution instead of rewriting each resident per selected
step. It preserves different personal starting ages, same-boundary overflow refusal, holds and due
processes; materialized ordinary state and saved history remain the oracle. Compare explicit copies,
one final materialization and materialization on every step on the same actual resident population.
This identifies a representation requirement, not a storage-format decision: the current publisher
still materializes every resident. Per-actor persisted roots would only avoid exposure write fan-out
if the timing-domain basis is itself admitted and persisted with membership/history. That dependency
remains part of the integrated owner prototype, with current-profile and full scale gates open.

## Terrain-separated aggregate candidate

The [storage prototype](p02/partition-state/separated-terrain-storage/RESULT.md) implements a deliberately
coarse alternative: one complete nonterrain locality image plus immutable terrain archives, retaining
original full genesis and complete replay. It reuses the existing SQLite authority/writer and ordinary
profile-five interpreter. It does not select the final typed actor/custodian owner layout. Compare its
actual residency, image/write bytes and recovery costs against the finer proposed owner design before
freezing a persistent cognition model. Every later-profile owner and historical interpreter remains
required; profile40/41 migration, directory granularity, scheduled-work fairness, cold loads and the
unchanged independent review are still open. Production checkpoint-interval qualification is separate
from the initial-checkpoint and short-history prototype evidence.
