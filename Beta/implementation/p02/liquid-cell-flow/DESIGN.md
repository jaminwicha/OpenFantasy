# Retained detailed liquid placement and flow

## Full-plan direction and dependency correction

The full P01/P02 fluid contract requires starting-state finite-volume neighbor transfers, a river/outlet
and receiving pool, then occupied flooded passages, another material and actual reactions/phase changes.
Investigation of finite throats showed that profile nine's indivisible direct source-to-terminal delivery
cannot supply this solver. It has no intermediate custody/progress, and equilibrium reservoirs cannot
represent a moving causal front. Mechanical shutter actuation remains required but follows the detailed
flow owner it will control. This refines the earlier containment packet's sequence; it removes no requirement.

This increment retains actual placement of existing liquid and advances that placement through real world
steps and exact history. It uses the same C# core, material authority and storage writer. It does not turn
an equilibrium witness into detailed head, create per-cell inventories or supply free material.

## Owned state and interfaces

`LiquidCellKey` identifies one reservoir and whole-cell XYZ address. `LiquidCellPortion` holds that key,
an origin kind (`RawSource` or `ProducedLot`), origin ID and positive half-open unit interval. Raw intervals
refer to the admitted source's absolute units; produced intervals refer to the existing derived lot's
unit offsets. Original custody/Production retains provenance and ownership. Cell portions partition it.

`LiquidFlowProfile` pins positive whole-unit downward/lateral per-face allowances for each selected
1,800-calendar-millisecond exploration step. Existing physical volume per unit must divide one cubic
metre exactly. This interpretation has integer flow quanta and rates; fractional-rate accumulation is not
silently rounded into it. Odd lateral differences leave one conserved unit in place.

`LiquidCellState` binds an exact `LiquidReservoirState` hash, flow profile, processed calendar boundary,
logical tick revision and normalized portions. It derives cell quantities from intervals. The domain is
the union of its fixed nominal vessel cells, at most 4,096; at most 16,384 normalized portions are retained.
Each cell is at most one cubic metre. Portions must be disjoint, lie within their actual vessel and
exactly exhaust that vessel custodian's live calibrated raw/produced holdings. Geometry, material/domain
and source roles are pinned. There is no simultaneous coarse allocation of these holdings and no pending
runoff budget; previous terminal history and source holds remain recorded rather than erased.

State APIs: `Create(reservoirs, profile, boundary, portions)` creates revision zero;
`Rebind(reservoirs)` preserves placements and time, validating unchanged domain/geometry plus exact live
coverage; internal `WithPlacement(boundary, revision, portions)` validates a complete result; internal
`AdvanceBoundary(nextBoundary)` advances one interval without changing placement for a held/preflight
boundary. `ReservoirHash`, `Profile`, `Boundary`, `Revision`, `Portions`, `DomainCells`, `Quantities`,
`UnitsPerCell`, `VolumePerUnit` and `ContentHash` are immutable or read-only.

`LiquidCellPlanner.PlanAdvance(state, nextBoundary, held)` returns a plan pinned to the complete state.
`LiquidCellPlan.ApplyTo(state)` refuses stale inputs. A repeated boundary is idempotent; past, fractional
or skipped boundaries reject. Canonical profile/component codecs preserve all intervals and revalidate
the expected reservoir owner on decode. Their byte limit fits the unchanged 16 MiB complete world limit.

## Conservative algorithm

Prepare transfers from starting cell quantities. Downward faces precede lateral faces; both share the
same source spending and destination starting-capacity budgets. Canonical reservoir/XYZ/face order
determines contention. The donor's promised inflow cannot move again in the same step. Its outgoing
amount does not create same-step receiving capacity. Lateral proposals use starting level differences
and whole-unit half differences under the finite per-face rate. No pressure/pump phase is implied.

Allocate exact portions from each donor in canonical origin/range order and retain incoming portions
separately until final publication. Normalize adjacent ranges only within the same cell/origin. No
interval may be dropped or duplicated. Flow remains within each real vessel; a receiving pool belongs
to the same finite connected terrain channel. Missing geometry is not an outlet. Unchanged regions need
no material mutation; sparse active donors avoid repeatedly processing empty cells.

A fragment/work bound refuses the complete proposed stage before publication. It cannot truncate
provenance, silently omit a selected transfer or consume time while hiding a partially completed flood.
Recoverable continuation and production-scale fronts remain later measured obligations.

Independent review identifies the operational limit precisely: this overflow propagates out of world
step preparation and faults the local session. No transaction, time or identities may publish. Reopening
restores the prior state, but retrying the same intrinsic fragment overflow does not make progress.
An actual-authority limit regression must establish durable immutability. This profile does not claim
recoverable fluid continuation or production readiness at that bound.

## Live world interpretation

Fresh profile twenty joins profile nineteen's complete fixed contained world with `LiquidCells`.
It retains the direct root and schema-three writer. Fresh admission requires an unplayed, unheld
profile-nineteen basis, zero liquid tick revision and the identical starting calendar/material owner.
Ordinary successors retain the new component. Decode joins construction, reservoirs and cell allocation
before complete root admission. Prior profiles and step bytes retain their original interpretation.

Complete admission requires retained seals and terrain-supplied floors/walls: the same exact boundary
assessment must be complete with no constructed sealing faces. Bodies and installed solids remain
outside the nominal envelope. This supplies a fixed terrain-contained free-surface case; it neither
models suspended construction carrying water nor grants safe immersion. Transport, runoff controls,
excavation, moving physical construction and structural owners remain incompatible with this profile.

Every accepted material action first reconciles the reservoir and then the exact cell partition.
Unrelated inventory changes can rebind; a generic action that changes allocated holdings must update
placement atomically through an explicit future spatial material action or refuse. In particular,
repacking raw water into a new produced lot cannot silently relabel existing spatial source claims.
Action refusal restores the complete attempted components and IDs. Preflight projects the selected
eventual time without running speculative flow; that root is discarded. Actual due flow runs once after
actions and the selected time increment, before movement. Opening a reading hold suppresses that flow;
held interaction/pause advances neither boundary nor liquid. No catch-up throughput accumulates.

Old `ReservoirFill` values remain equilibrium/aggregate storage witnesses. Profile twenty's actual
placement and local level come only from `LiquidCells`; reports must identify this explicitly and must
not present the equilibrium projection as its actual cell surface. The Host creates, executes and
inspects the owned state through the existing authority. Native presentation consumes permitted liquid
projections when connected; this authority increment alone is not a rendered-water acceptance claim.

## Required evidence and continuation

Prove source-backed falling, lateral spreading and pool arrival across several committed steps;
starting-state capacity, shared source budgets, no same-step relay, exact interval conservation,
empty/full recipients, material rebind/refusal, limits, missing terrain, actual holds/time and stale plans.
Exercise canonical malformed input, real restart/history/retry and external commit-boundary kills.
Keep old profile controls, source/spec/index preservation and bounded independent critique.

Follow with permitted native water projection, finite shutter/port displacement and actual actuation,
conserved excavation/breach/capacity consequences, represented fluid loads and occupied wet space.
Second liquids, reactions/phase changes, ecological effects, Wait boundaries, coarse/detail exchange,
history scale and full production gates remain required. The frozen full specification is unchanged.
