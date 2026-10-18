# Integrated partition prototype contract

This is the implementation target for PLAN-STATE, refining the [comparison](PLAN_STATE_COMPARISON.md).
It specifies the next integrated experiment; it does not select a production save format, introduce
a new world interpretation or claim independent architectural acceptance. Source boundary: `7fbc9ed1`.
Pure C#, in-process publication, typed domain ownership and retained historical interpreters are fixed.

## Findings that change the implementation target

1. **Location is not ownership.** `WorldActorCarriage` correctly distinguishes physical carriage from
   access ownership and installed backing. Walking across a terrain boundary must not rewrite lot
   identities, custody or actor identity. A prototype that moves identical payloads between two files
   proves little. Use stable actor/custodian owners and update spatial membership atomically. Add an
   actual cross-owner gift to prove coupled custody changes independently of movement.
2. **Access scope is currently global to the admitted locality.** `ConstructionSnapshot.ValidateComponents`
   requires an access witness for every supplied same-floor actor. `ConstructionProject` requires
   `access.Count + distinct(actor, destination).Count <= 32`; even one witness per actor permits at most
   16 distinct actors. `LocalSpatialSnapshot` permits 64 actors. This is a genuine scaling constraint,
   not permission to truncate a query or increase a constant. Preserve the current rule for the first
   equivalence experiment. A later protected-access-domain law needs separate versioned admission.
3. **Time cannot be inferred from absence.** `WorldStepProcessor` applies actions before advancing
   time/processes and then movement. Every resident receives earned exposure on an advancing step;
   actions that open a hold can still consume that already-selected step. An elapsed host interval,
   cached `CanAct` result or unloaded region cannot award construction work.
4. **A completed project is a coupled material transaction.** The planner retires reserved inputs,
   creates the exact batch/output/structure identities and retains installed output reservation.
   Physical admission also joins load, occupants and material state. Advancing only a progress counter,
   even durably, is not the requested proof.
5. **The existing identity index retains detail.** `WorldIdentityIndex` references a terrain identity
   layer which retains `TerrainRegionSnapshot[]`. Capturing that index, a full world, or a closure over
   either would invalidate an absent-detail claim even if the API is named a read set.

These findings require an admitted transaction context and explicit owner boundaries before storage
changes. Further free-standing geometry wrappers are not the next milestone.

## Ownership hypothesis to implement and measure

Use a small number of typed aggregates, not one root per field or lot. Names below describe proposed
responsibilities, not existing classes or finalized serialized tags.

| Owner | Facts it owns | Facts it references, without copying ownership |
| --- | --- | --- |
| Actor aggregate, keyed by stable actor identity in its world/branch; realm is physical membership | Body/form and presence, actor-local state, resident behavior/language, personal exposure basis, current commitments | Primary inventory custodian, control assignments, project/encounter participation and definitions |
| Material aggregate, keyed by custodian identity | Exact source/transformed holdings, storage capacity/revision and relevant access policy | Actor/institution owner, immutable source/recipe pins, historical producer witnesses and reservations |
| Project/site aggregate | Project progress, funding/reservation links, definition pin, work/site coordinates, required-access contract, installed backing and physical members | Builder, supplied stock, affected terrain, neighboring assemblies and completed quest funding evidence |
| Terrain/place aggregate | Admitted terrain cells/edits, local physical and geographical owners according to their actual coupling | Macro geography, boundary flux obligations and semantic locations; no duplicate actor inventory |
| Timing-domain aggregate | Earned advancing boundary, calendar/process interpretation, holds and deterministic due-work frontier | Participating actors, encounters and obligations; a clock does not own those actors |
| Control/interaction aggregate | Controller/permission and genuinely shared interaction facts | Stable participants and affected owners; no capability inferred from region residency |
| Canonical directory and commit history | Exact owner addresses/revisions, identity roles/reservations, membership changes and committed command outcomes | Typed owner hashes and their interpreters |

Stable actor and custodian keys remain unchanged while walking. A spatial membership page is an index,
not another actor owner. Cargo may have more than one physical interpretation: the primary bag is
carried; reserved builder stock follows the admitted stock-location law; installed backing belongs to
the assembly. None becomes weightless or relocates merely because it is reserved or inaccessible.

The first prototype may keep complete **bounded metadata** resident, explicitly measured, while loading
only needed detail. It must not advertise global directory paging, city scalability or bounded frontier
memory on that basis. Reuse the existing occupancy pages as derived geometric references; do not
rename them a canonical custody directory. Keep owner identity/revision history separate from spatial
page revisions and actor movement revisions.

## Admission and execution boundary

Build one typed construction execution context from admitted owners. It must have a private or
otherwise validated creation boundary and exact source pins; a public collection of booleans is not
proof. Share its actual decision rules with ordinary `ConstructionPlanner`, retaining the historical
complete-world adapter. Do not implement a second simplified work planner in the probe.

| Dependency family | Required admitted facts and invalidators |
| --- | --- |
| Scope and interpretation | World/branch/realm, original admission lineage, exact supported component laws; unsupported combinations refuse before execution |
| Actor/control | Actual body/function/HP, position/revision, resident's primary custodian, permanent or delegated command authority, readiness, commitments and command identity; revocation, incapacity or another accepted action invalidates the prior selection |
| Time | The selected legal advancing boundary, reading/encounter state, actor exposure and due order; preparation alone never earns a step |
| Funding and tools | Actual completed quest/funding witness, owner policy, reserved input identities/quantities/revisions, tool definition/quantity/location/protection, project and storage revisions; actual material provenance remains present |
| Geometry/access | Work reach, proposed structure clearance/support, all required original and current-position access connections, neighboring occupancy and complete applicable access population; arrivals, changed body/terrain/support or newly affected assembly invalidate stale results |
| Completion | Exact spawned identity reservations, material batch/history capacity, storage capacity, installed-output reservation, construction/member projection and complete relevant load/physical admission |
| Publication | Current exact read revisions and absences, complete write set, spent command/receipt identity and lineage; all checks serialize with the one publisher |

Preparation records what was read and why. It must not keep `WorldSnapshot`, `LocalSpatialSnapshot`,
the terrain-retaining identity index, or callbacks that capture those objects after detail is evicted.
Detached metadata is counted in residency measurements. Opaque successful evidence must be bound to
the exact dependencies and operation, not accepted as a reusable permission token.

Missing data produces a preparation need or lawful refusal before work is awarded. It does not mean
an empty cell, absent enemy, zero mass or permission granted. Bounded geometry can be prepared using
the existing read APIs; project-level access completeness cannot be inferred from their selected actors.

### What offscreen work means in this proof

The worker, goods, commitments and elapsed simulation exposure remain real while detailed terrain is
absent from the execution resident set. There are two legitimate routes to investigate:

- Retain a certified geometry/access result whose exact terrain, occupancy, support, population and
  interpretation dependencies are checked against current authoritative metadata before each use.
  No intervening changed dependency is silently ignored. Completion must still prove material,
  identity and physical obligations; a certificate cannot authorize an unrelated command or new shape.
- Load only the dependency closure needed for an intervention or invalidated certificate, then return
  to the declared offscreen boundary. Record this load and its cost; do not label the interval as having
  absent detail when the work actually retained or loaded all of it.

The acceptance case must contain actual successful funded work during an interval with detail absent,
plus invalidation and lawful re-preparation. A refusal-only offscreen implementation does not pass.
Completion may require additional dependencies, but loading the complete world at every step fails
the partial-residency objective. An unchanged certificate is an optimization of the same rule, not a
different simulation outcome selected by camera position.

### Time representation experiment

Compare explicit per-actor exposure writes with an exact exposure basis relative to a timing domain.
Within an unchanged membership segment, materialized exposure is the retained base plus the domain's
earned exposure since the actor's joining boundary, using existing integer units and overflow rules.
Changing domains first materializes the old segment once, then pins the new base. Personal modifiers,
holds, death and historical interventions cannot be folded into a universal calendar subtraction.

Start with the existing ordinary exploration law and compare every step against `WorldResident.ExperienceStep`.
Do not add sleep acceleration, offline catch-up or a new encounter clock to obtain lower write counts.
Even if exposure writes can be deferred, actual due actions and passive processes still run in their
declared deterministic order. The experiment must report those costs, not just movement arithmetic.

The [resident exposure experiment](p02/partition-state/resident-exposure-basis/RESULT.md) supplies the
transient common-exploration basis within actual construction locality execution. Saved ordinary history
still uses materialized residents. It does not persist a timing owner or implement different personal
rates; those remain required before reducing canonical per-actor write sets. The measured comparison
includes final-read and every-step-read consumers so deferral cost is not hidden.

## Atomic persistence and historical recovery

Use the existing Hosting serialized authority and SQLite transaction mechanism. The prototype's
candidate representation is a typed set of owner replacements and exact read dependencies; it does
not introduce a second writer beside `SqliteWorldAuthority` for the same save. Ordinary saved worlds
keep their existing complete-root path until an explicit qualified transition exists.

Persist immutable owner revisions and a commit record that binds previous head, accepted input,
interpreter pins, dependency revisions, created/retired identities, changed owner hashes, membership,
due obligations and command outcomes. Canonical ordering, finite field/transaction limits and rejection
of duplicate keys are part of the format design. No numeric profile, SQL schema or byte tag is reserved
by this document; specify and qualify them with their actual integration before writing live state.

All mutable pointers and receipts advance together in one SQLite transaction. Publishing only the
directory before the body/cargo, acknowledging before durable commit, or returning a new receipt for
an exact retry is invalid. Missing or corrupted referenced payloads refuse recovery; an index hit alone
does not authenticate ownership or ancestry. The first prototype may verify the complete retained
commit chain before selective reads, but must measure and label that cost. Partial authenticated replay
and arbitrary-time travel remain separate unproved requirements.

The copied-source admission must account for every facet in its supported source interpretation.
Unsupported source profiles refuse explicitly. A successful older construction fixture is useful
domain evidence but does not authorize importing the lived profile41 expedition by dropping its
control, care, market, encounter or resource-history facets. Preserve original bytes and replay.

## One integrated episode and acceptance matrix

Use an existing admitted actor/cargo fixture and an actual funded construction fixture; connect them
under one declared admitted interpretation before claiming the joined result. Keep the ordinary
complete-root path as the equivalence oracle, not a co-authoritative production simulator. Record
initial state, definitions and population before runs. Include an irrelevant terrain region whose
detail can actually be removed from execution memory and denied by the archive reader.

| Episode | Positive evidence | Required counterexample / preservation |
| --- | --- | --- |
| Cross a spatial boundary with carried goods | Same actor, control, lot/source identity and quantities; changed body/membership and exact earned exposure | Destination collision, stale membership and revoked control; no duplicated actor or inventory and no lot revision merely for walking |
| Transfer real cargo between owners | Exact policies/consent, source debit, destination credit, lot provenance, capacity and one receipt | Stale source/destination, interrupted publication and exact retry; complete old or complete new holdings |
| Work while detail is absent | Actual reserved stock/tool/quest, paid steps and due ordering; return before completion matches detailed continuation | Arrival/terrain/support/access-population change invalidates prior proof; no progress during a hold, unearned interval or absent tool |
| Finish the same project | Actual reserved inputs retire, installed output remains reserved, offcuts retain ancestry, structure/member state appears with one outcome | Identity collision, capacity/load/access refusal and cut/reopen at the last step cannot partially install or repeat material output |
| History and recovery | Before/after movement, gift, partial work and completion reconstruct the same owners, time, obligations and command outcomes | Before/after-commit faults, missing payload, stale owner map and tampered dependency/history refuse or restore the exact committed boundary |

Measure metadata and detail separately: bytes read/written, peak resident detail, allocations, preparation,
rule evaluation, commit, recovery/replay, boundary stalls and oldest due-work age. Report actual loaded
archives and retained reference graphs. Name hardware/runtime and workload; retain timeout/failure
evidence. The existing 180-second managed-command bound is unchanged. This is reduced diagnostic
evidence, not 10,000-actor/20,000-resident, four-player endurance, desktop or full history qualification.

## Implementation sequence and stopping rules

1. **Shared admitted execution.** Extract the construction decision kernel and its true dependencies
   while preserving ordinary full admission, material continuation and failure precedence. Demonstrate
   paid progress and real completion with detached dependencies, exact ordinary equivalence and stale
   rejection. Include certificate invalidation and actor/cargo boundaries in this unit; avoid another
   free-standing geometry-only packet. The source-backed design above is ready; this executable unit
   is partially supplied by [shared construction execution](p02/partition-state/construction-execution/RESULT.md):
   ordinary and detached candidate evaluation share rules, including real material completion and site
   invalidation. Whole-source capture/freshness, terrain-retaining material contexts, earned scheduling
   and admitted owner inputs still need integration; this does not close the offscreen authority proof.
   [Authority integration](p02/partition-state/construction-authority/RESULT.md) binds reusable geometry
   to the existing selected-batch executor and durable publisher, preserving clock/action ordering and
   complete admission. The publisher still holds and encodes the complete world: this qualifies the
   execution adapter, not absent-detail commits or typed partition ownership.
   [Locality continuation](p02/partition-state/construction-locality/RESULT.md) then supplies sequential
   selected work/time/due-process candidates from the complete nonterrain owners of profile5, with
   source terrain absent throughout 200 steps and exact reconstruction afterward. This qualifies a
   legacy whole-locality aggregate execution hypothesis. It does not supply a per-actor/custodian split,
   later-profile interpretation, earned durable publication or the joined movement/gift episode.
   [Construction publication](p02/partition-state/construction-publication/RESULT.md) supplies the
   ordinary-storage baseline for that episode: carried movement, real gift, intervened work, return
   before completion and exact first/final commit recovery share original genesis and ordinary replay.
   This internal binding still retains and encodes the complete root at every commit. It does not
   satisfy absent-detail publication or canonical owner partitioning; do not relabel the baseline as
   the integrated partition candidate.
2. **Joined owner transaction.** Integrate those decisions with stable actor/material/project owners,
   membership, time and the single durable publisher. Run the entire episode, including actual gift,
   absent-detail work, final material publication and historical recovery. Wire exact admission and
   compatibility refusal before proposing migration. A storage-only split does not close this unit.
3. **Measure and decide.** Compare whole-local-aggregate and typed-aggregate costs on the same episode,
   including metadata/time fan-out and faults. Decide granularity, version envelope, access-law evolution
   and rollout from this evidence. Seek the required independent architectural review when orchestration
   is permitted; until then retain that gate as unrun. Only then freeze permanent NPC persistence.

If a candidate needs all terrain or all deep actor state for every step, record the failed hypothesis
and revise the boundary. Do not relax admission, redefine absent detail, expand deadlines or reclassify
an unchanged dependency helper as the integrated proof. Keep the full requirements and P00–P14 exits.

## Aggregate storage experiment and remaining comparison

The [terrain-separated storage experiment](p02/partition-state/separated-terrain-storage/RESULT.md)
binds the legacy integrated episode to the original full genesis, canonical nonterrain images,
immutable terrain archives and the sole SQLite writer. Partial work can release terrain residency;
final installation and recovery retain complete admission/replay. This supplies an aggregate candidate
for the prescribed comparison, not the permanent typed-owner format or a new gameplay interpreter.
The explicit internal fresh storage schema13 is scoped to profile5; all other numbered formats keep
their meanings. No migration of lived profile41 saves is implied.

Next compare this concrete coarse image with the typed owner/directory hypothesis, account for resident
materialization and identity/occupancy metadata, and define actual due-work discovery and fairness.
Qualify noninitial checkpoint boundaries and richer-profile admission before production rollout.
Preserve the integrated episode's original history and refusal/fault evidence; no raised limits,
synthetic progress, skipped due processes or second writer may stand in for the remaining proof.

## Lead recommendation and next integrated candidate

The [matched comparison and lead decision](PLAN_STATE_DECISION.md) recommend stable typed owners
with complete operation-specific admission cohorts. The coarse terrain-separated image reduces
selected publication allocation in the measured fixture but increases replay allocation and observed
database size; neither result establishes scale. The next unit is the coupled actor/custodian/project
ownership-directory candidate in the same saved episode, including cross-owner fault/retry and true
due obligations. Keep shared ordinary rules and exact original history. Independent review, nested
current-profile field coverage, noninitial checkpoint qualification and fairness/cold/scale evidence
remain open; the recommendation does not close PLAN-STATE or allocate a production format.

## Concrete bounded owner publication

The [typed-owner storage candidate](p02/partition-state/typed-owner-storage/RESULT.md) supplies the
legacy scoped actor/control/custodian/project and shared-facet directory through internal schema14.
Its current head holds directory references; immutable owner images retain one current canonical
owner per encoded fact, plus historical versions required by original-history verification. Default
schemas and schema13 retain their meanings. Full16MiB world admission is not replaced by a smaller
head, and new finite archive limits apply only to this explicitly selected development experiment.

The integrated trace checks stable carried custody, real two-custodian gift publication, absent-terrain
funded work, ordinary intervention, completion and history. Before/after faults cover gifts and first/
final work; missing, changed and extra historical images refuse recovery. Complete resident metadata
and all original supported due processes remain present. No later-profile facets can be dropped.

[Stored-owner reconstruction](p02/partition-state/stored-owner-reconstruction/RESULT.md) binds actual
payload reads to the replay-selected head, existing complete admission and exact replay comparison.
The returned recovery head uses reconstructed owners. Partial-project reopen and continued installation
remain the same saved episode. The private runtime continuation still holds the complete nonterrain
locality; this decoder loads every required owner and terrain region. Remaining work is a qualified
operation-specific loading cohort with authenticated dependency closure. Due fairness, richer-profile
nested mapping/migration, checkpoint boundaries, cold/scale/platform measurements and independent
review remain required before the corresponding production/PLAN-STATE exits.
