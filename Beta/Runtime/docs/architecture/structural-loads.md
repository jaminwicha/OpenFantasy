# Structural loads and failure planning

## Domain and world binding

[ConstructionBearingModel](../../src/OpenFantasy.Core/Structures/ConstructionBearingModel.cs) derives a
read-only bearing graph from a complete world snapshot, exact construction templates, declared terrain
foundations, body/item mass calibration and optional member condition. The model retains the world hash,
member-to-project/structure/installed-lot references and actor references. Its dependency pin covers the
world hash, canonical graph, template versions and foundation source/material addresses. It neither owns
world state nor installs external ratings as simulation policy.

Completed construction supplies actual member geometry and one retained installed material lot.
[StructuralMassCatalog](../../src/OpenFantasy.Core/Structures/StructuralMassCatalog.cs) partitions that
lot's physical mass by exact member volume. Integer floors and canonical member-ordinal remainder shares
conserve the installed mass exactly. It does not count the consumed input ingredients again.

Actors contribute explicitly calibrated body mass and live goods in their primary inventory custodian.
Raw and transformed lots count once, including reservations and equipped tools. Exact Production bindings
supply known item masses; supplemental pins must explicitly rate other items. Unknown calibration or
conflicting exact item definitions refuses binding. The installed construction lot belongs to its project
custodian and is not also carried by its builder. A primary-inventory binding does not establish spatial
load ownership for arbitrary storage, vehicles, attachments or every other custodian.

[ConstructionBearingTemplate](../../src/OpenFantasy.Core/Structures/ConstructionBearingTemplate.cs)
provides complete member ordinals, capacity, maximum horizontal span and minimum surviving support count
for one exact construction definition. A vertical bearing requires a positive overlapping horizontal
face at matching heights. A declared braced joint requires a positive shared vertical face. Contact alone
does not invent bracing, and material hardness is not load capacity. Definition geometry must fit its
explicit rating; ground members cannot acquire an authored joint that bypasses their terrain footing.
Member condition scales capacity by integer parts out of 10,000; zero condition fails explicitly.
A world with a [physical construction owner](construction-physical.md) supplies retained standing-member
condition; external condition overrides are rejected. Detached members require a new contact model and
are refused by this static binder.

[StableTerrainBearing](../../src/OpenFantasy.Core/Structures/StableTerrainBearing.cs) declares a finite
boundary at a retained solid cell with its exact source/material and positive capacity. Solidity alone
does not establish stable bedrock or geological self-support. These declarations define the graph's
external foundation boundary; their author must justify the omitted surrounding strata.

The [policy and motion contracts](structural-policy-and-motion.md) provide a canonical complete policy
asset and first-contact swept-descent queries. These preserve policy dependencies and actual collision
material; they do not install live collapse law or replace complete world admission.

## Contact, topology and accounting

[StructuralContactWeights](../../src/OpenFantasy.Core/Structures/StructuralContactWeights.cs) partitions
an exact footprint onto the highest eligible physical surface at each point. Clipped rectangles, exact
integer coordinates and an ordered sweep avoid double-counting overlapping lower surfaces. Every patch
of the footprint must have a qualified contact. Base members use their full bottom face. Actor contacts
use the world's ordinary support-height interpretation, including the bounded stair band when enabled.
Contact area determines relative load shares; joint face area determines authored bearing shares.

[StructuralLoadGraph](../../src/OpenFantasy.Core/Structures/StructuralLoadGraph.cs) retains at most 4,096
elements and 32 outgoing bearings per element. Element and bearing identities are unique, endpoints
exist, ordered pairs do not repeat, foundations have no outgoing edge, and cycles are rejected. A stable
identity-ordered topological traversal sends loads from supported elements toward their supports.

Each element retains unsigned 64-bit own/applied mass and capacity in milligrams. Accumulated loads and
accounting use unsigned 128-bit integers. Wider transient multiplication preserves exact weighted
fractions; no floating point, random choice or iteration arrival order chooses a share. Each division
rounds down and distributes the remainder in canonical bearing-identity order.

## Bounded cascading resolution

[StructuralLoadResolution](../../src/OpenFantasy.Core/Structures/StructuralLoadResolution.cs) starts with
explicit removed identities and resolves immutable work slices. Each pass freezes its excluded set.
An element fails when its condition is zero, it has too few surviving declared supports, or its load
exceeds effective capacity. A newly failed element does not transfer load. Failures remain monotonic,
retaining the first reason, load and pass. At a pass boundary with new failures, loads reset to the own
and applied mass of surviving elements and the next pass redistributes through the surviving graph.
A failure-free pass establishes the fixed point.

An `Advance` call evaluates 64 nodes by default, accepts one through 256, and stops at a pass boundary.
Array copies, failed-node skips and a possible reset each still cover up to the graph's 4,096 elements;
the evaluation budget is not a constant-time or frame-time guarantee. At most `N * (N + 1)` evaluations
suffice for a finite graph. Incomplete work exposes provisional loads, not final accounting.

On completion, accepted foundation load plus the original input mass of all failed elements equals
total graph input mass. Failure loads may include mass arriving from other nodes; those diagnostic loads
must not be summed to create debris or inventory quantities. The ledger accounts each original mass
once. A failure plan does not retire its source lots, produce loot or move geometry.

## Canonical continuation archives

[StructuralLoadCodec](../../src/OpenFantasy.Core/Structures/StructuralLoadCodec.cs) retains a bounded OFC
`structure.resolution.v1` frame containing the dependency pin, canonical graph, initial removals, pass,
cursor, evaluation count, completion bit, failures and provisional loads. Element/bearing rows and loads
follow canonical identity order. Accumulated loads use 16-byte unsigned big-endian encoding. Archives are
limited to 16 MiB; graph limits also bound each collection.

Decode requires a trusted expected graph and dependency pin, or a freshly bound construction model.
Comparing copied pin bytes is insufficient: the embedded graph must exactly equal the expected canonical
graph. Replay from initial removals checks every saved continuation field, followed by exact re-encoding.
Malformed, truncated, noncanonical, substituted or forged state is refused. This is dependency/integrity
validation, not authentication of caller-provided policy.

Restore can replay up to `N * (N + 1)` evaluations, independently of the per-advance work budget.
Cancellation is checked between replay slices. Archive size and finite work do not establish production
latency; an authority integrating continuation must account for validation work and host scheduling.

The read-only [Host structural inspector](../../src/OpenFantasy.Host/StructuralLoadOperator.cs) accepts a checkpoint, separately supplied expected graph and
pin, then resolves and reports the result. It cannot certify that those caller-supplied dependencies
belong to a world, and it does not alter a world database.

## Design contract: physical consequences and complete authority

Static load survival is not sufficient evidence of safe footing. An actor graph node requires one
surviving support and may redistribute its weight after losing part of its contact footprint. The spatial
rules require complete footprint coverage. Hidden lower contacts are absent from the original graph.
Physical consequence handling must rederive contact from surviving geometry before deciding whether an
actor remains supported, falls, or encounters another support. It must also check body clearance and
landing or retain an explicit falling state; a successful load calculation cannot waive those checks.

A complete collapse transaction couples construction condition/placement, terrain, actors, material
custody, effects and receipts. Completed work and production history remain historical facts when the
physical result is damaged or displaced. Natural collateral failure cannot masquerade as player mining
work or duplicate issued source intervals. One installed quantity cannot be copied into multiple debris
lots; conserved wreck geometry or a proper material transformation must retain exact ownership.

Live integration must cover the entire affected dependency closure, including loaded goods, moving
occupants, neighboring support and terrain. Missing neighbors or oversized work require explicit held
continuations and controlled admission; they must not silently drop dependencies. Revisions and trusted
policy pins invalidate stale plans. Intermediate work must not expose a partly collapsed authoritative
world. The [excavation transaction](excavation.md), [constructed footing](constructed-footing.md),
[stair navigation](stair-navigation.md) and [material ledger](materials-and-activities.md) keep their own
invariants while participating in the same final publication. Wet composition also preserves containment,
displaced volume and conserved spills; dry load planning supplies no fluid-sealing law.
