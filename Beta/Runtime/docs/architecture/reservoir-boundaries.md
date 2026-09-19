# Reservoir boundaries

## Domain contract: assessing the retained physical envelope

[ReservoirBoundaryEvaluator](../../src/OpenFantasy.Core/Geography/ReservoirBoundaryEvaluator.cs) assesses
the complete nominal volume of the reservoirs already owned by a `WorldSnapshot`. It reads that world's
retained local terrain, corporeal bodies and completed construction. It neither allocates wall material
nor substitutes watershed geometry for local terrain. The stratified local interpreter and dry macro
mapping have separate occupancy and ancestry contracts; matching coordinates do not unify them.

The result pins the complete world hash, reservoir-owner hash and external sealing-policy hash.
[Assessment results](../../src/OpenFantasy.Core/Geography/ReservoirBoundaryAssessment.cs) distinguish
successful coverage, discovered failures and incomplete work. They are derived observations, not a new
authority, persistent admission right or permission to mutate terrain. Reusing a result for another world
or another policy is invalid. Admission of coupled terrain/fluid operations requires its own complete
publication contract; an inspection result cannot silently upgrade existing world profiles.

## Domain contract: explicit sealing materials

[ReservoirSealPolicy](../../src/OpenFantasy.Core/Geography/ReservoirSealPolicy.cs) names the exact counted
liquid content pin and one through 64 distinct solid content pins. Each solid rule declares impermeability
with a minimum continuous normal thickness of 1–1,000 millimetres. A different version of the same item is
not an admitted seal. Hardness and positive collision occupancy supply no implicit impermeability.

These are explicit material-profile assumptions, not measurements of real-world leakage or pressure
strength. One whole solid terrain cell can supply at most its one-metre thickness. A constructed member
must individually supply the declared thickness in the normal direction, with exact contact at the
liquid boundary. Separated sheets cannot add their thickness across an air gap. Tangential patches from
qualified members may combine to cover a face. Lamination, porous transport, pressure-dependent failure,
deterioration and reactions need their own material laws and retained consequences.

[The policy codec](../../src/OpenFantasy.Core/Geography/ReservoirSealPolicyCodecV1.cs) uses a version-one
canonical frame tagged `geography.reservoir-seal-policy.v1`, followed by the 48-byte liquid pin and ordered
56-byte rule rows (16-byte solid ID, 32-byte content hash, unsigned-64 millimetre thickness). Its total
bound is 4,000 bytes. Decode bounds counts and sizes, validates rules and requires exact re-encoding;
duplicate IDs, alternate ordering, invalid thickness, unknown tags and trailing data reject.

The policy is an explicit external input to inspection. It does not amend the saved world's material
rules, install a new capability or assert a supported production material catalog.

## Domain contract: interiors, floors and exposed walls

A reservoir column occupies `[BottomZ, SpillZ)` over one square metre. Every nominal interior cell must
be retained and free of solid substrate, exact installed material and full corporeal body clearance.
This is the complete capacity envelope, not only the presently wet fraction. An actor standing on a
boundary is assessed by its whole clearance body, not its centre or feet alone. Safe immersion and
buoyancy/exposure are separate contracts; this assessment does not infer them.

The floor is at the `BottomZ` plane and contacts cell `BottomZ - 1`. A terrain generator's `SurfaceZ`
is the highest occupied cell index, so its upper physical surface is `SurfaceZ + 1`. Those quantities
cannot be equated without an explicit conversion.

Each cardinal face exposed outside the same vessel needs complete coverage up to the spill plane.
An adjacent higher reservoir floor hides only the shared open portion; its lower riser still needs a
wall. Internal faces between connected liquid cells are not walls. Every required terrain dependency,
including outward cells across region seams, must be present. Excavated cells retain provenance but
provide no solid coverage. Missing regions and signed-coordinate overflow never imply containment.

The open spill plane is not a roof requirement. Floor/perimeter coverage does not establish structural
load paths, pressure strength, spill routing, groundwater behavior or stable support beneath a suspended
terrain shelf. Those proofs remain distinct from complete geometric sealing under a supplied policy.

## Domain contract: exact constructed material

The evaluator reads only projects with a retained `InstalledStructure`. The complete world admission
already binds those projects to their completed work, exact Production batch and live reserved output.
The installed output item's pin determines sealing, not its input stock, tool, display label or a freely
supplied collider. An unfinished project contributes no material faces.

[Exact boundary boxes](../../src/OpenFantasy.Core/Geography/ReservoirBoundaryBox.cs) combine project
origins and authored `MillimetreBox` members without rounding. Coordinates use 1/256 millimetre ticks:
one cell is 256,000 ticks, one spatial subunit is 1,000 ticks, and one authored millimetre is 256 ticks.
Signed 128-bit arithmetic handles large and negative locations. Outward-rounded `SpatialStructure`
collision envelopes never serve as sealing evidence; rounding can hide a narrow physical gap.

[Rectangle coverage](../../src/OpenFantasy.Core/Geography/BoundarySurfaceCoverage.cs) clips qualified
face patches to the one-square-metre target and computes an exact union area. Overlaps and duplicates
count once; edge/corner contact contributes no area. It uses integer interval sweeps and rejects more
than 256 input rectangles. Each covering primitive first proves continuous normal thickness; the
2-D union cannot turn displaced material into a contacting wall. Any positive uncovered area fails.
Material intruding into the reservoir independently fails the interior check even if its projection
covers a boundary.

## Domain contract: finite work and unresolved gates

One assessment admits at most 65,536 nominal interior cells and 65,536 exposed unit faces across the
complete owner. A conservative dependency-scan estimate is bounded to 4,194,304 checks, using regions,
installed members and actors; exact rectangle unions have their own 256-patch bound. Exhaustion returns
explicit `WorkLimit` failures with `Completed = false`. Coordinate limits are also incomplete. Reports
retain counts and at most 32 representative failure samples per vessel; sample truncation never erases
a failure total or converts an incomplete result into a pass. Cancellation throws without returning a
partially successful assessment. These finite bounds do not qualify production latency or workload.

[Transport gates](reservoir-transport.md) declare an operation across a shared edge but supply no
finite material shutter or wall opening. Every connected vessel therefore reports `UnprovedGate`,
even when unrelated solids cover that edge. A nominal seal cannot prove an operable hydraulic gate.
Combining gate operation with physical construction needs actual opening/closure geometry, displaced
volume, material custody and publication rules; it cannot grant a zero-thickness authored gate the
properties of a completed wall.

## Runtime binding: exact read-only inspection

[ReservoirBoundaryOperator](../../src/OpenFantasy.Host/ReservoirBoundaryOperator.cs) implements
`--inspect-reservoir-boundaries <world.sqlite> <commit-revision> <seal-policy.bin>`. The existing authority
recovers the save and reconstructs the requested exact committed state. The result includes source-head
and selected-state pins, policy hash, coverage totals and named failures. Unsealed or incomplete geometry
is a successful inspection with `IsContained = false`; malformed input or an unavailable commit fails
the command. Inspection publishes no command, new material, edited terrain or changed history.
