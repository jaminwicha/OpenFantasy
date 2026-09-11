# Finite watershed preparation

11 September 2026. A bounded P01 geography increment supporting parts of RQ-151/RQ-159 and the dependency
contracts in [geological design](../../../spec-v1_1/GEOLOGICAL_WORLD_DESIGN.md). This supplies one
macro preparation primitive; it does not complete any requirement, GEOR-02 or a phase exit.

## Implemented contract

`WatershedPlanner.Prepare` accepts an explicit world/branch/realm scope, seed, finite macro grid,
versioned relief rules, boundary outlet and named external terminal. The initial envelope is
1–64 columns on each axis. It visits at most 4,096 columns and records at most 8,064 shared internal
faces. It does not recursively discover upstream or downstream basins. Invalid extents, overflow,
interior outlets, unavailable windows and nonadjacent face requests fail instead of being clamped.

Seeded natural elevations and an outlet at the profile's lower elevation bound feed a finite
priority-flood traversal. The priority is `(spill elevation, canonical column index)`. First
discovery fixes the downstream parent; deterministic pop rank makes equal-height routes acyclic.
Reverse rank accumulation measures each column's contributing catchment. Every column has a finite
route to the declared outlet. Spill elevations describe potential drainage through depressions;
they neither raise the ground nor imply a physical lake or an already flowing river.

The immutable result contains a basin identity, one initial formation identity, distinct substrate
parcel identities, natural/spill elevations, parents/ranks, contribution counts and shared faces.
`ReadWindow` returns established columns. Reading either side of an internal face returns the same
identity and values. Genesis derivation excludes branch ID; the canonical preparation includes its
explicit branch scope. A branch's later lived changes must be retained separately, never regenerated
from this initial candidate. Changing seed, rules, basin extent or outlet describes a new candidate,
not permission to replace previously published facts.

`WatershedCodecV1` preserves the complete request and derived column records in a bounded archive.
Decoding verifies lengths and supported tags before reconstruction, then compares every canonical
byte against the pinned algorithm. Corrupted routes, counts, identities or unknown versions refuse.
Changing derivation requires a new supported version; old archives cannot be silently reinterpreted.
The existing host's `--inspect-watershed <prepared-basin.bin>` reads and verifies such an archive
without creating a world, granting material rights, writing a save or executing a gameplay action.

The application uses ordinary engine-free C# objects: request/bounds/rules, a stateless planner,
immutable preparation and value records, derivation and canonical codec. No new assembly, engine
dependency, IPC bridge, parallel authority or generation framework was introduced. Existing terrain,
world codecs, farm profiles, source claims and action behavior remain unchanged.

## Evidence and review

The [qualification summary](qualification/verification-summary.json) records the actual final results,
source/binary hashes and original failed build logs. Focused tests cover complete routes, independent
minimax relaxation across 24 seeds, immutable partition queries, shared-face symmetry, parallel
request limits of 1/2/4 workers, branch scope, different seeds, corruption, cancellation, numeric
limits and the largest supported grid. Maximum concurrency is a scheduler setting, not a claim
that every configured worker ran simultaneously or that one basin is internally parallelized.

A separate Python implementation supplies canonical fixtures for a negative-address grid, a flat
plateau, a one-column basin and a one-cell grid at signed-coordinate/elevation limits. It uses a
sorted frontier instead of the runtime heap. Tests compare every archived byte and every expected
face identity/count. A different relaxation algorithm independently checks the minimum spill
heights; the reference implementation is not the sole correctness oracle.

The first test build failed because the new types were not implemented yet. The next build caught
xUnit's required test-cancellation forwarding; the tests now pass their actual cancellation token.
Local review corrected `minimum + width - 1` to `minimum + (width - 1)` so a valid one-cell extent
at `Int64.MaxValue` does not overflow an intermediate expression. The extreme fixture covers it.

One bounded native critic reviewed the new core contract independently while implementation work
continued. It found that Python's neighbor candidates duplicated vertical neighbors when width was
one. The reference now deduplicates candidates; the new thin-basin fixture and exact boundary-count
assertion guard the correction. The critic found no further runtime defect in scope. Its
[review](REVIEW.md) is specific to this contract, not approval of the overall geography architecture,
production performance or a full phase gate. No external model or Claude delegation was used.

The coherent regression ran **553 Core + 317 Hosting tests** with zero skips, the independent
watershed generator, **74 host inspections** and the headless foundation smoke successfully. It then
failed on the older native farm walkthrough. Read-only reconstruction shows player revision 30 at
commit 32; the one Talk command at commit 33 expected revision 29 and received RevisionConflict.
The fixture had released movement and pressed Talk in the same frame. It now observes stationary
arrival and matching displayed actor/target revisions before one press, and retains failure stage,
focus/pause/reading flags and the offer at press. No production action handling or deadline changed.

The corrected native farm journey passed movement, held dialogue, the six-wood handover, exact
paused-view restart and recorded-history inspection. Only this probe changed after the broad tests;
all five distinct non-client/test assemblies remained byte-identical after rebuilding. The 870 tests
were therefore not rerun. Both builds had zero warnings/errors. The failed integrated report and save
remain alongside the corrected native result. This is bounded combined qualification, not a full
uninterrupted verifier pass. Older fixtures, other native journeys, rendering and editor import were
not rerun for this isolated candidate API. The recurring editor-startup issue remains unresolved.

## Deliberate remaining work

- A world-level authority must select, persist and admit mutually compatible basin/province
  candidates before dependent places publish. Independent overlapping candidates are not automatically
  compatible. Cross-basin watershed divides, plate boundaries, downstream contracts and finite
  dependency composition remain required; only windows inside one prepared basin are qualified here.
- Macro grid addresses need their admitted mapping to local terrain. The existing farm still uses
  its original stratified terrain profile. This result is not yet consumed by mutable voxel/cell
  realization, and no new river or mountain is claimed in the playable client.
- The initial elevation sampler is a bounded seeded calibration, not accepted continental relief,
  stratigraphic geology or tectonic history. Later constrained profiles must retain existing versions
  and shared macro facts rather than replacing a seed's published geography.
- External terminal IDs do not supply physical water, receiving capacity, discharge rights, heat,
  sediment or ore. Conservative reservoirs/exchanges, coarse/detail custody, reactive fluids and
  resource admission must precede those claims. Contribution counts are topology, not rainfall.
- Lived mutations, process intervals, historical instants, moving substrate, hazards, Wait admission,
  cancellation during active preparation, fuzzing, regional workloads and cross-platform execution
  require their planned evidence. Current cancellation execution covers a pre-cancelled request.

All 181 baseline requirements, 14 full UI cases and full phase/production gates remain unchanged.
Native art, animation, audio, co-op and human experience acceptance are not part of this packet.
