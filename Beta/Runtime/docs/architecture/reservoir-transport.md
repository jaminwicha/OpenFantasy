# Reservoir transport

## Runtime binding: exclusive staged transport

[ReservoirTransportState](../../src/OpenFantasy.Core/Geography/ReservoirTransportState.cs) owns one
complete physical reservoir wrapper, an immutable catalog of gravity gates and a finite journal of
scheduled arrivals. The physical wrapper owns its capacity ledger, calibrated material and watershed
network. Inventory and Production remain the same world authority; neither scheduling nor a renderer
creates a second material ledger.

[Profile nine admission](../../src/OpenFantasy.Core/Worlds/WorldReservoirTransportAdmission.cs) accepts
an unplayed construction world and a fresh transport owner at the same calendar boundary. The world
rejects independent reservoir/network arguments when transport owns them. Revision-zero world roots
require an empty, revision-zero transport journal even when constructed without the convenience factory.
Ordinary successors preserve transport ownership and exact shared inventory/Production. Profile eight
retains its separate rule that time passing cannot settle runoff.

## Domain contract: normally closed gravity gates

A [ReservoirConnection](../../src/OpenFantasy.Core/Geography/ReservoirConnection.cs) names distinct
source and receiving vessels, two cardinally adjacent columns, one sill elevation and a positive
maximum number of calibrated material units per selected exploration step. Both ports lie within their
vessel columns, at or above their bottoms and strictly below both spill planes. One shared XY face
has at most one gate; duplicate, opposing and vertically multiplexed aliases reject in this profile.
The catalog is bounded to 256 connections. Its identities cannot alias geography, storage, material,
actors, stages or other admitted roles.

Gates remain closed except for admitted indivisible transfer stages. Independent water levels between
stages therefore do not imply permanently open hydraulic equilibrium. The author supplies the finite
vessels and gated face; these contracts do not prove that local terrain, masonry or constructed seals
actually support those surfaces. Local wall/support admission is a separate dependency.

Each stage must fit the gate's whole-step allowance. Every gate shares that allowance across all due
stages; scheduling several budgets cannot multiply its discharge. The fixed bound describes discrete
batches over an authoritative step, not a continuous pressure solver or a fractional-flow accumulator.
No allowance accumulates during a pause, blocked stage or skipped processing interval.

## Domain contract: funded stages and provenance

[RunoffTransportStage](../../src/OpenFantasy.Core/Geography/RunoffTransportStage.cs) retains its own
identity, one existing funding reservation, an explicit arrival instant, a fingerprint of the original
forcing interval/source inputs, exact total units, basin-to-gate routes and outcome. Arrival is distinct
from a runoff forcing interval: it cannot precede that interval's end or the next selected step boundary.
A forcing interval never supplies an implicit arrival time.

Each independently funded commitment is indivisible. It may span several raw lots and several gates,
but all its transfers succeed together or none publish. Partial delivery requires an explicit progress
contract retaining original funding, delivered/remaining source intervals and residual receiver holds;
it cannot be represented by shrinking the original rainfall depth. Dry commitments have no gate routes
or material movement, but still produce an explicit terminal outcome.

[RunoffTransportRouting](../../src/OpenFantasy.Core/Geography/RunoffTransportRouting.cs) follows each
wet input basin through its unique finite drainage chain to its terminal. Its chosen gate must connect
the exact funded source store to that terminal's receiver. Every wet input has one route; dry inputs have
none. Aggregate terminal totals do not permit arbitrary redistribution of source lineage.

[RunoffTransportCustody](../../src/OpenFantasy.Core/Geography/RunoffTransportCustody.cs) partitions a
held raw lot at the actual source-claim boundaries. Each destination receives those exact intervals,
with the same material/source identity. The first interval retains the parent lot ID with advancing
revision; additional fragments derive stable IDs from the stage and canonical fragment ordinal.
Existing and historically occupied identities reject the stage. Unrelated lots and Production history
remain exact. Touched stores and complete custody advance once per accepted stage; no transferred unit
remains available at its source.

The planner retires the completed capacity commitment in the same transition that debits sources and
credits destinations. Remaining budgets are re-prepared against the new custody. This avoids both an
intermediate double-counted receiving obligation and a moment when capacity is free but water has not
arrived. Physical actual/projected fills and combined raw/Production limits validate the whole result.

## Domain contract: pressure, holds and outcomes

[ReservoirTransportPlanner](../../src/OpenFantasy.Core/Geography/ReservoirTransportPlanner.cs) evaluates
actual final fills after the proposed whole batch. For every used gate, the source's lower surface
bound must remain at or above the sill and the receiver's upper surface bound. The upper bound includes
one billionth of a metre when floor-band allocation has residual microlitres; ignoring that residual can
admit a subtly uphill transfer. Signed 128-bit head arithmetic avoids coordinate overflow. Future incoming
capacity reservations are never treated as actual pressure.

A failed gate invalidates the entire multi-destination material candidate, including lot revisions,
fragments and capacity changes. `BlockedByHead`, `BlockedByThroughput` and `BlockedByMaterial` retain
the original source water and receiving obligations. They can be reconsidered at later accepted steps;
there is no invisible queue storage or automatic release of held stock. Within the fixed finite vessels,
this is conservative backpressure, not an overflow, breach or flooding law.

The journal distinguishes `Scheduled`, blocked states, `Completed` and `Canceled`. Completion releases
the transferred source hold because the water has actually reached its destination. Cancellation releases
only incoming headroom and leaves source holds intact. Both terminal outcomes retain the reservation ID
in capacity's non-reusable `Closed` set; that set alone does not classify the outcome. A reservation has
at most one transport stage. The journal and terminal set each have a 4,096-entry bound; exhaustion
rejects new work rather than erasing history.

`PlanApply` accepts a plan pinned to the complete physical owner, then validates all scheduled funding.
The integrating world also checks fixed domain/geometry and monotone retained commitment history when
rebinding already validated material actions. A transport plan's `ApplyTo` pins the entire owner hash,
including its gate catalog, journal and processed calendar boundary.

## Runtime binding: action, clock and commit order

[ScheduleRunoffTransportIntent](../../src/OpenFantasy.Core/Geography/ScheduleRunoffTransportIntent.cs)
pins the complete transport hash and logical revision. The enclosing command pins actor revision and
trusted control delivery. [WorldTransportResolver](../../src/OpenFantasy.Core/Worlds/WorldTransportResolver.cs)
checks current owner control before private state pins, requires an advancing unheld batch and derives
a fresh stage ID from the command. Unknown, duplicate, unfunded or too-large schedules reject normally.
Player action offers and presentation use their own permitted-observation binding.

The world resolves canonical actor actions first. A cancellation at the due boundary can therefore
cancel before delivery; a material action can change actual head before a stage is considered. Every
accepted material candidate passes both physical and scheduled-funding reconciliation; rejection rolls
back all of that action's effects. Later actions retain earlier accepted results.

At the selected step's publication boundary, the transport planner processes due work by arrival instant
then stage ID. It accepts exactly the next 1,800-calendar-millisecond exploration boundary. A repeated
boundary is idempotent; past, partial or skipped boundaries reject instead of granting extra throughput.
A reading hold opened earlier in that batch suppresses all transfers while retaining the selected step's
boundary. Held-interaction batches do not advance the clock at all. Closing a hold never catches up water
from paused steps. Movement follows this due-work stage under the same held-state policy.

The complete journal, actual material, physical fills, new fragment identities and world head publish in
one existing schema-three transaction. Preview and replay run the same pure planner. Recovery reproduces
both blocked and completed stages; historical inspection cannot execute an arrival in the live head.
The locality-wide reading pause is conservative. Encounter-specific dependency closure, held bodies,
terrain hazards, support motion and region streaming retain their separate design/qualification contracts.

[Physical boundary assessment](reservoir-boundaries.md) reads actual terrain and completed construction
but explicitly reports these gates as unproved. A gate's authored shared edge supplies neither a
material shutter nor displaced-volume geometry; nominal wall coverage cannot establish gate operation.
