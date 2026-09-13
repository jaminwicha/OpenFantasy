# Finite funded terrain material

Qualification date: 2026-09-13. This packet extends the retained dry macro mapping with a standalone
finite material allocation and representation contract. It does not close P01 or publish mining into
the playable farm. The full campaign, all 181 requirements and all existing acceptance gates remain.

Qualified code/evidence: `163a7bbc17bf07549ab2f6aa86050e7a1d9a8b9e`, preserved on
`codex/milestone-terrain-material-allocation`. Development remains on `codex/runtime-foundation`.

## Delivered scope

- An explicitly funded homogeneous slab binds one exact macro parcel/mapping to existing counted
  source units and physical calibration. Its full support, rather than a requested detail window,
  determines cell intervals. Each solid cell accounts for one cubic metre. Mass, volume and signed
  coordinates use checked bounds; no resource mint or second quantity ledger is introduced.
- Atomic refinement partitions current coarse-owned intervals into cell-bound lots. Demotion moves
  only remaining detailed stock back and coalesces eligible adjacency. Real prefix transfers through
  `CustodyTransferPlanner` leave their exact units in the destination. Partial and interior holes remain
  absent on re-refinement; full removal allows empty demotion but cannot produce an empty refinement.
- Complete state/revision pins, source/item/scope and owner checks, current/historical identity checks,
  protection/reservations and existing capacity/lot bounds reject incompatible plans without mutation.
  New lot IDs derive from accepted causes rather than reusable terrain addresses. Material entity
  revisions cannot exceed their complete root. Reconciliation preserves admission and requires an
  advancing custody result, except for identical idempotent input.
- A bounded standalone canonical archive retains exact geometry, deposit calibration, unchanged
  counted authority bytes and active cells. The real host's `--inspect-terrain-materials` operator is
  read-only. The full verifier wires retained fixtures and an independent Python interval/format oracle.
- The maintained [geography reference](../../../Runtime/docs/architecture/world-and-geography.md#domain-contract-funded-terrain-material),
  [material reference](../../../Runtime/docs/architecture/materials-and-activities.md) and runtime setup
  describe ownership, bytes, invariants and extension boundaries in timeless language.

## Qualification and failures

The final managed solution build has zero warnings and errors. **957 tests (628 Core + 329 Hosting,
zero skips)** pass in [qualification](qualification/verification-summary.json), including 21 added
material tests.
The final real-host campaign contains **24 inspections**: five valid archives (including independently
encoded coarse and fragmented-coarse states), interval/mass/volume checks, two 4,096-lot boundary
fixtures, malformed bindings/coverage, crossing fine intervals, future entity revisions, truncation,
unknown formats and oversized input. The Python oracle reuses exact retained terrain bytes; it does
not independently reimplement the watershed generator or claim an independently authored full world.

The cap fixtures prove 4,096 fully active cells can demote back to one coarse lot. A different finite
slab reaches 4,096 current lots at 4,095 active cells and rejects the next refinement without increasing
the cap. This is correctness evidence, not a production streaming latency/workload qualification.

Failures are retained: `test-first.log` reports missing APIs; `focused-first.log` reports four
`Assert.Single` analyzer violations; `focused-final.log` reports an omitted test cancellation token.
The assertion style and token propagation were corrected, not waived. The early focused/host successes
describe their own source boundaries; final full tests and host inspections use the final rebuilt
production binaries. No observed runtime assertion failure was reclassified as a pass.

The native `geography_admission_critic` performed a bounded independent read-only source review of
partitioning, custody dependencies, complete-state pinning and codecs. It found no concrete defect and
requested stronger identity, fragmented-funding/metadata, incompatible-state and capacity/overflow
tests. A subsequent review confirmed those tests address its findings and the reference accurately
describes scope. [Review record](critic-review.md) distinguishes this from human acceptance. The small
final root/entity revision guard has direct test and real-host rejection evidence and local review.

No Godot presentation, gameplay input, composed-world save interpreter or persistence transaction
changes accompany this increment. The complete verifier and native rendered journeys were not rerun;
the managed and targeted real-host campaigns are the claimed qualification. Shared desktop focus
does not affect these headless checks. No thresholds, deadlines or existing gates were weakened.

## Integration obligations

This state is a trusted standalone planning composition. It is not a new durable authority or a player
command handler. The caller must provide genuine accepted causes, complete historical occupancy,
authorization and atomic publication. Repeated pure `ApplyTo` on the same immutable basis does not
constitute durable retry handling. No actor labor, mining rights, spatial excavation or support checks
are implied by a representation change.

All funded units must remain covered in current counted custody. A transfer elsewhere is supported;
retired/transformed claims require explicit composition with Production before consumption can occur.
The material archive rejects holes rather than regenerating them. Multiple deposits need one
world-owned allocation registry that enforces spatial/source exclusivity. Branch-stable ancestry
does not grant export rights. The old stratified terrain interpreter and world profiles remain intact.

Continue the complete bounded physical macro profile (crust/history, climate/forcing, coast/reservoir/
external budgets) and explicit allocation/retirement ownership before dependent live terrain admission.
Bind a retained local interpreter without overwriting established places, occupancy, construction,
support, custody or history. Mixed soils/ores, geological changes, liquids, broad world streaming,
performance, migration, art/audio and all full game/phase acceptance remain open.

## Preservation

[Preflight](preflight.json) pins the source head, pre-edit checkpoint and unrelated predecessor index.
The final document/preservation checks verify frozen specifications, reference links, naming and exact
index checksum. Only owned successor changes are committed. The integration lead records the qualified
commit and preserved milestone in the shared handoff and milestone ledger without switching branches.
