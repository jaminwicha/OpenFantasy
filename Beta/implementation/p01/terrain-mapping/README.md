# Macro-to-local dry terrain mapping

12 September 2026. This P01 increment adds an executable, retained mapping from exact finite watershed
networks to bounded local geometry and ancestral addresses. It advances the macro-to-local dependency
without claiming live terrain publication or closing the full geological preparation gate.

## Implemented boundary

`WatershedTerrainMapping` pins positive integer horizontal/vertical cell scales from 1 through 1,024,
horizontal zero anchoring and a signed elevation datum. `WatershedTerrainMapper` validates every supplied
network footprint and natural elevation with checked wide arithmetic before preparing a local window.
Negative coordinates use floor partitioning. Every requested XY column must have an exact macro owner;
gaps and exterior requests reject the entire candidate. Height overflow, unsupported scale and incomplete
coverage do not wrap, clamp or fall back to unrelated terrain generation.

The candidate takes each macro column's natural surface, piecewise constant over its local footprint.
It does not fill depressions to spill height or reinterpret contributor counts as water. Vertical windows
sample the same absolute surface. Their size is at most 32 × 32 × 16 cells; queries outside the retained
window reject. The surface cell belongs to candidate substrate, with void above it. These are dry geometry
witnesses, not live body collision/support or scientific volume accounting.

Column records refer to existing basin/index, formation and substrate parcel, with canonical local
offsets. Each candidate substrate cell has a typed address containing parent formation/parcel, mapping
pin and absolute local cell coordinate. That address excludes branch and request-window identity. It
creates no independently owned entity, source quantity interval, extraction permission or item. Void
cells have no substrate address. Material palettes, soil depth and ore allocation are deliberately not
invented from a drainage-only input. Later physical source partitioning must use actual retained budgets.

The immutable `PreparedWatershedTerrain` has its own exact archive and host inspector. It cannot be
substituted into `TerrainRegionSnapshot`: the old terrain codec regenerates its original stratified
baseline. The [architecture reference](../../../Runtime/docs/architecture/world-and-geography.md#domain-contract-dry-macro-to-local-geometry)
explains that compatibility boundary, format, transform, ancestry and extension obligations. No old
terrain/world codec, save profile, default farm selection or clock changes in this increment.

`WatershedTerrainCodecV1` nests the exact network, mapping and bounds plus rederived Y/X column rows in
a 4,100,000-byte envelope. It rejects malformed, noncanonical or inconsistent witnesses. Preparation and
decode expose cancellation and never return a partial candidate. The privileged read-only
`--inspect-watershed-terrain` operator uses bounded file input and reports exact hashes, transform,
bounds and occupancy counts. The full verifier includes its independent vectors and real inspections.

## Qualification and failures

[Qualification summary](qualification/verification-summary.json) retains exact runtime input/binary
hashes, commands and logs. The complete managed solution build passed with zero warnings/errors and
**607 Core + 329 Hosting tests (936 total, zero skips)**. All **27 real host inspections** passed.
No complete verifier, Godot gameplay/rendering, device/export, performance or full-phase run is inferred.
The client and ordinary world behavior are unchanged; the earlier native evidence remains at its own
preserved checkpoint.

The **17 new tests** cover:

- Six independently encoded Python archives. The oracle locates ownership by explicit inclusive
  footprint search and enumerates Z occupancy; production uses inverse floor lookup and interval counts.
  Exact archive bytes and mapping/content hashes agree across negative seams, branches, vertical slices,
  signed coordinate extremes and nonflat natural relief.
- Matching cells and ancestral addresses across X/Y/Z partitions, reversed requests and 1/2/4 concurrent
  workers. Branch scope changes archive hashes while common-ancestor addresses remain identical.
- Exact cross-basin face references; depressions remain at natural height rather than becoming ground
  or water at the routing spill height. Changed mapping pins change candidate ancestry.
- Missing coverage, holes, invalid scale, complete-transform overflow and signed-minimum elevations;
  out-of-window rejection; input immutability; pre-cancelled generation and decode.
- Maximum 16,384-cell windows and a **65,536-column source network** combined with that window. The
  retained maximum archive is **3,340,075 bytes**, below the unchanged 4,100,000-byte candidate bound;
  its 9,216 candidate substrate cells reproduce after decode. This is finite capacity evidence only.

The real [host inspections](qualification/terrain-inspections.json) additionally reject altered ancestry,
indices, surfaces, offsets, stale mapping witnesses, missing macro coverage, malformed/trailing frames
and oversized input. Every input archive remains unchanged after inspection. The maximum archive and
all invalid packets are retained alongside the reports.

The test-first build records missing new APIs. Two subsequent compiler runs caught a total of four
omitted runner cancellation tokens in test calls (three initial calls, then a newly added maximum-case
call). Those calls now forward `TestContext.Current.CancellationToken`; the complete focused run passed
all 17 cases before the broad regression. Original failed logs remain retained. No assertion, timeout,
archive limit, runtime guard or acceptance gate was weakened. Cancellation timing during computation
was source-reviewed but is not a separately measured latency or mid-computation cancellation test.

The bounded [critic review](REVIEW.md) found no consequential defect. It is independent source review,
not human gameplay acceptance. [Document/preservation checks](qualification/document-preservation-checks.json)
resolve 132 architecture links, verify 22 frozen specification hashes, check runtime naming and confirm
the unchanged unrelated predecessor staged index. This increment introduced no reusable process change
requiring an additional ecosystem update; its architectural facts belong in the project reference.

## Next dependencies

The saved network still lacks a complete physical macro dependency profile: crust/history constraints,
climate and water forcing, coast/reservoir/external-boundary budgets and source material allocation.
Complete those declared bounded profiles before publishing dependent physical places. Then bind an
explicit retained terrain interpreter and source partitions into world admission, with construction,
occupancy, support and existing material/history preserved. A geometry witness or hash is not that proof.

Interpolation, strata/ore/caves, river geometry, conservative fluids, dynamic frontier admission,
played-save migration, process history and arbitrary-time reconstruction remain open. Refinement must
preserve established geography or occur as an explicit versioned causal change; it cannot reroll a
known place or issue fresh material rights. The default farm and visual presentation remain unchanged.
All 181 requirements, 14 unrun full UI cases, full P00/P01/P02 exits and the campaign through P14 remain
in scope. This checkpoint does not close GEOR-02 or any full phase gate.
