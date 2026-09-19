# Actual reservoir boundary assessment

This checkpoint connects nominal reservoir geometry to retained local terrain and completed
construction. It follows [discrete transport](../reservoir-transport/README.md) and supplies a real
read-only assessment of containment under an explicit sealing policy. It does not silently convert
existing reservoir profiles into terrain-aware fluid authorities.

## Implemented contract

The evaluator checks every nominal interior cell, floor and exposed cardinal wall, including risers
beside higher floors and dependencies across region seams. Original substrate identity does not turn
an excavated hole into a wall. Solid occupancy and hardness do not imply impermeability: a bounded
canonical policy names exact material versions and minimum continuous thickness for one liquid.

Completed projects contribute their actual installed output material and exact millimetre members,
backed by the existing Production/work/stock admission. Unfinished projects, input wood and rounded
collision envelopes provide no seal. Exact integer rectangle unions count overlaps once and reject
any uncovered area. Per-member normal contact and continuous thickness are required before tangential
union; air gaps between layers cannot be summed away. Interior material or full-body actor obstruction
also prevents success.

Reports pin complete world, reservoir and policy hashes and distinguish failures from incomplete
budget/coordinate work. Missing terrain and authored zero-thickness transport gates explicitly fail.
The host assesses exact committed history through
`--inspect-reservoir-boundaries <world.sqlite> <commit-revision> <seal-policy.bin>` without publishing
a command, changing history, or installing the external policy as a saved simulation rule.

The [timeless reference](../../../Runtime/docs/architecture/reservoir-boundaries.md) records exact
coordinate scales, policy bytes, dependency bounds, construction provenance and remaining boundaries.
The verifier retains and runs the actual-world fixture/operator via
`OPENFANTASY_RESERVOIR_BOUNDARY_EVIDENCE` and `tools/check_reservoir_boundaries.py`.

## Evidence

[Verification summary](qualification/verification-summary.json): the full solution builds with zero
warnings/errors. **1,118 managed cases pass (726 Core +392 Hosting), 37 new, zero skips**. All production
and test changes precede the full build/regression. The complete all-systems verifier was not run;
its targeted checks were invoked directly. No timeout, assertion or phase gate was weakened.

The [operator report](qualification/boundary-operator.json) retains **21 actual host calls** and proves
five database files are byte-identical before/after inspection. Each basic vessel has two one-metre
interior cells and nine boundary faces; the independently asserted full tank has one terrain floor and
eight constructed wall faces. The defective tank variants each lose two west wall faces:

- A one-millimetre slit remains unsealed although outward collision rounding can hide it.
- A one-millimetre displaced wall does not contact the required boundary.
- A wall intruding into the liquid fails both coverage and interior clearance.

The synthetic tanks are built by real NPC routing/work, spending funded quest stock through the
existing construction/Production pipeline. Their one-unit work requirement and calibrated sheet
material are explicitly test content, not shipped balance, pressure strength or an art claim. A
partial project contributes zero constructed faces. Correct material with insufficient minimum
thickness fails; declaring the input stock waterproof cannot qualify an undeclared output item.
The ordinary terrain fixture starts from deliberately authored excavations; it does not claim a
live mining command or ore allocation. Results before work, after work, after restart and in reversed
history order are evaluated against actual retained states.

Full-suite fixture reproduction matches all 15 original fixture artifacts byte-for-byte, separately
from the independent operator's coverage assertions. Other tests cover stepped-floor risers, region
seams, floor/wall holes, solid interior, missing dependencies, material-version mismatch, whole-body
occupancy, gate non-proof, cancellation, signed-coordinate limits, finite budgets, malformed policy
archives, exact overlap/sliver arithmetic and canonical ordering.

Actual Godot headless foundation passes. Gameplay adapters were unchanged, so farm, rendered and
physical-device acceptance were not repeated or newly claimed. All 211 architecture links resolve;
22 frozen specifications and the exact unrelated 19-file predecessor index remain preserved.
`test-first.log` retains the expected missing-API compiler failure. Focused/full runs then pass without
weakening a behavior assertion.

[Bounded native review](qualification/critic-review.md) covered consequential false-pass risks and the
reference. No remaining consequential defect/mismatch was found. The critic did not execute tests or
qualify production performance. No Claude/Fable or other external paid-model call was used.

## Remaining integration

This assessment neither blocks world actions nor changes a saved profile. Live terrain editing must
compose custody, actor support, construction, containment and hazard consequences at the same durable
publication boundary. It must not turn every unsupported edit into an unexplained action refusal or
allow a breach to erase stored water. That integration remains open.

Physical gate shutters/openings need finite material and displaced-volume geometry before transport
can claim terrain-backed containment. Nominal sealing does not establish structural load paths,
pressure strength, damage, maintenance, spill routing, immersion, phase changes, continuous flow or
breach dynamics. The local stratified terrain and dry macro interpreter remain separate. Production
workloads, full geography/history, UI/art/audio, co-op, exports/devices and every full campaign gate
remain open. This checkpoint preserves those obligations rather than narrowing the design.
