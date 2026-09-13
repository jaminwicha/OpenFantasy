# Bounded consequential review

Native critic `geography_admission_critic` reviewed the design boundary and final C# mapper, immutable
candidate, archive, host reader and tests without editing files or running builds/tests/benchmarks.

The critic identified the essential compatibility boundary: `TerrainRegionCodecV1` reconstructs its
baseline through `StratifiedTerrainGenerator`. Placing network-derived cells into that existing snapshot
would misrepresent its retained generation contract. The separate candidate/archive avoids that failure.
The complete climate/coast/reservoir/material dependency profile remains an explicit prerequisite to
live terrain admission; a geometric sample cannot pass that gate.

Final review found no consequential numeric, ancestry or codec defect. Signed floor division handles
negative extremes; full mapped footprints and natural elevations use checked wide intermediates;
vertical occupancy remains within the window; ancestry omits branch and request-window pins; canonical
decode rederives exact witnesses; and bounded host reading does not write an existing world.

The design review suggested distinct candidate source IDs when physical material interpretation exists.
This narrower increment deliberately emits typed geometric addresses instead: parent formation/parcel,
mapping hash and absolute cell coordinate. It adds no material palette, soil profile, quantity interval
or independent identity ownership. Repeated/branched sampling cannot create rights through this API.
A later physical source-allocation contract must resolve custody rather than treating these addresses
as funded reservoirs. The final critic accepted that boundary.

The review noted optional cancellation-token calls in small test helpers as a possible analyzer issue.
The compiled suite verifies the final call sites; four flagged test calls were fixed to forward the
runner token, with failed compiler logs retained. Cancellation tests establish pre-cancellation only;
no deterministic mid-computation cancellation timing or latency is claimed.

This review is distinct from the executed qualification. It does not prove full geological feasibility,
terrain publication, water/material conservation, production performance, arbitrary history, rendering,
physical devices or full phase acceptance. See [the increment record](README.md).
