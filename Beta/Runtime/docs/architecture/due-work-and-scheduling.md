# Due work and scheduling

## Runtime binding: one complete selected boundary

[LocalWorldSession](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs) selects commands
under its serialization boundary and publishes through the existing authority. Preparation time is
host time; it does not earn simulation exposure. The [time contract](time-and-encounters.md) separates
commit sequence, advancing steps, process intervals, calendar, tactical time and personal exposure.

Three different kinds of work must remain distinct:

| Kind | Actual owner and meaning | Publication rule |
| --- | --- | --- |
| Mandatory boundary effects | The selected interpreter's exposure, regrowth, expiry and physical processes | Complete the applicable effects with the boundary, or publish none of that boundary |
| Autonomous proposals | NPC policy proposes an ordinary command from actual state, permissions and commitments | Selection is not guaranteed execution; admission, exclusion, conflicts and ordinary refusals still apply |
| Preparation work | Dependency reads, route searches, qualification and candidate construction | Does not advance time, consume a gameplay opportunity or acknowledge completed world work |

In the ordinary advancing path, [WorldStepProcessor](../../src/OpenFantasy.Core/Worlds/WorldStepProcessor.cs)
evaluates selected actions before the time boundary. It then advances exploration time, expires applicable
material proposals and paid quotes, prepares reservoir transport, advances gathering and every resident's
exposure, prepares detailed liquid flow and finally considers movement. Optional components and hold
conditions govern the effects actually due. A bounded liquid-plan failure aborts the selected step;
it does not publish time with missing flow. Encounter, care and reserved-consequence cutoffs have their
own interpreters and must not be routed through this ordinary sequence merely because they contain work.

An advancing action that opens a dialogue still earns its selected interval. The resulting hold
suppresses movement and affects applicable physical processes; it does not turn that advancing batch
into a held batch. A held interaction earns no advancing interval. An ordinarily refused action can
still belong to a lawful advancing batch with passive effects. A dispatch failure is a different boundary.

## Runtime binding: NPC proposals and service

[LocalNpcController](../../src/OpenFantasy.Hosting/Simulation/LocalNpcController.cs) examines host-controlled,
available residents, excluding actors already occupied by selected participation. It can propose work
for several builders at the same cutoff. [LocalConstructionController](../../src/OpenFantasy.Hosting/Simulation/LocalConstructionController.cs)
finds an incomplete assigned project backed by completed quest participation, requires the builder at
the work point with a real tool and preflights the ordinary construction planner. Selection invents
neither progress nor tools. It suppresses infeasible work instead of producing endless refused receipts.

The session arbitrates material gifts, trading, ordinary NPC work and restorative proposals in its
selection order. That order determines participation exclusions; it is not the final command execution
order. [WorldStepBatch](../../src/OpenFantasy.Core/Worlds/WorldStepBatch.cs) canonicalizes selected actions
by actor identity. The executor then rechecks their coupled effects in that canonical order.

[WorldSnapshot](../../src/OpenFantasy.Core/Worlds/WorldSnapshot.cs) distinguishes domain operation
revisions from world batch revisions. Legacy interpretations retain their aggregate revision bound;
coordinated interpretations admit the bounded per-actor action count. Multiple successful operations
do not imply multiple elapsed intervals. A storage or scheduling adaptation must preserve the selected
interpretation's bound, including its refusals, rather than enlarging it to accommodate a fixture.

If the proposed batch's preview throws `AuthorityDispatchException`, `SelectAdmissibleBatch` can rebuild
it without NPC actions to preserve a valid player request. This fallback omits autonomous proposals;
the ordinary advancing interpreter still owns mandatory passive effects. It establishes no finite
NPC starvation bound. The [player selector](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.Players.cs)
has a cyclic service cursor with care-priority rules; that is not evidence of fair NPC work scheduling.

## Runtime binding: stored construction

[StoredConstructionInterval](../../src/OpenFantasy.Core/Construction/Ownership/StoredConstructionInterval.cs)
accepts only the supported profile-five interval and no movement. Capture names a bounded set of
actor/project work cohorts; selected batches may use those actors or contain no actions. Their
dependency union includes all resident actor owners, timing and gathering. Canonically ordered work
produces one receipt per selected action, followed by one ordinary exposure/gathering boundary.
Missing required payloads refuse preparation. Final installation and a legacy aggregate-limit crossing
return the entire batch to complete admission before any publication. This preserves the old law;
it does not introduce the coordinated interpretation or a general multi-domain scheduler.

[StoredConstructionPreparation](../../src/OpenFantasy.Core/Construction/Ownership/StoredConstructionPreparation.cs)
can read the exact dependencies in owner-count-bounded slices. Every slice and completion require the
same admitted directory. Incomplete loading cannot complete a candidate; a read failure or stale
directory permanently invalidates that preparation. Completion evaluates all selected work and due
effects synchronously. No slice publishes time, receipts or partial work. The Host's stored-batch
entrypoint drains preparation under its existing outer lock. The authority also exposes an
[unpublished publication handle](../../src/OpenFantasy.Hosting/Worlds/StoredConstructionPublication.cs)
through `BeginStoredConstruction`, `ReadStoredConstruction` and `PublishStoredConstruction`. Each
bounded read call releases the authority gate before returning; no transaction spans calls. Completion
requires the originating authority, the same captured interval and committed directory, and every
required owner. Cancellation, consumption or a changed head makes that handle unusable. An incomplete
publication attempt refuses without consuming the handle, so loading can continue explicitly.

Storage/read failures fence the authority and retire the preparation; semantic dispatch failures cannot
publish it. A foreign authority cannot load, publish or cancel another authority's handle. Cancellation
earns no time and creates no receipt; it does not roll back an already published boundary. Publication
uses the same complete write set and transaction as synchronous work, including ordinary full-admission
fallback. Reopening requires fresh capture: handles are transient, not saved continuations.

The internal `LocalWorldSession` constructor accepts an optional positive
`storedConstructionOwnersPerSlice` budget only with explicit profile-five typed-owner storage.
The public/default path uses ordinary synchronous publication. The
[live preparation binding](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.StoredConstruction.cs)
qualifies only advancing, movement-free NPC construction batches with no awaiting player action.
Selection and ordinary admission preview happen before capture. One private handle owns the complete
selected command set across the existing serialized host ticks; each tick reads at most the configured
number of owners. Newly queued player actions wait for the next cutoff and receive no early receipt.
Movement samples retain their ordinary lease; preparation does not extend an input's lifetime.

Between slices, no ready player or a stop request cancels unpublished preparation. Resumption selects
a fresh batch; elapsed wall time does not earn catch-up time. Readiness is sampled at the start of an
opportunity, so an already admitted final opportunity can finish if pause races with it. A changed
source invalidates the selection, refreshes the committed view and ends that tick; a later tick may
select fresh commands. A semantic failure with the same source propagates instead of being hidden
as intervention. Storage errors retain recovery fencing and fail the session.

Read slices keep the previous immutable view. Successful publication reconstructs the complete world
and uses the same receipt/trading observation and view publication as ordinary commits. Selection,
input submission and final projection can still require full-world reads. Owner-count slicing supplies
no final-evaluation CPU budget, selective projection, durable cursor or fairness guarantee.

Splitting an ordinary multi-actor batch into one advancing commit per actor would change both exposure
and opportunities to work. Bounded preparation must instead preserve the complete selected boundary,
including the original command set/order, coupled stock/access decisions and mandatory processes.

## Standalone calendar queue contract

[DueWorkQueueSnapshot](../../src/OpenFantasy.Core/Scheduling/DueWorkQueueSnapshot.cs) and its codec are
standalone portable components. The world session and snapshot interpreters do not consume this queue.
It retains at most 1,024 entries and selects at most 256 per call; those are component limits, not
shipping population or world-work targets. Each selection is tied to the exact queue scope, revision
and hash, with an explicit phase and `HasMoreEligibleWork` indicator. `ApplySelection` makes a candidate
acknowledgment; it neither executes an owner operation nor commits its effects.

[ScheduledWorkEntry](../../src/OpenFantasy.Core/Scheduling/ScheduledWorkEntry.cs) orders by due calendar
instant, phase, work domain, actor and event identity. It points to an owner's work item and continuation
ordinal. Calendar coordinates cannot carry tactical ticks or exposure counts by numeric substitution.
The queue's transition-before-command convention is not permission to move existing ordinary
post-action expiry before commands. Any binding must preserve the historical interpreter's exact phase
semantics, or introduce an explicitly versioned semantic change with retained old interpretation.

## Design contract: discovery, continuation and fairness

Discover mandatory work from admitted typed owners and actual obligations, independently of loaded
presentation detail. A retained obligation identifies its owner, timing domain/units, due boundary,
occurrence or continuation and complete dependency closure. Persistent obligations belong to world
state; a derived discovery index must authenticate completeness and freshness against those facts.
An unloaded owner is not an empty owner, and discovering only the player's vicinity is insufficient.

A preparation budget can suspend candidate construction, but cannot advance shared time beyond an
unevaluated mandatory effect. A lawful blocked outcome is evaluated work: it retains the original
obligation and its resources under that interpreter's rules. It does not require successful completion
before the clock can advance. Publish the complete logical boundary through the sole writer. A separate
durable preparation continuation needs explicit admission, recovery and cancellation semantics; the
existence of a queue or a reserved excavation continuation does not provide those semantics generally.

Measure mandatory backlog in its own timing domain. Report eligible proposal deferrals and host
preparation stalls separately. Include missing/invalidated dependencies, lawful holds, repeated competing
work and restart in fairness qualification. Stable iteration order alone proves no starvation bound,
and a one-builder completion cannot establish fairness across actors, domains or places.


## Discovery depends on the selected interpreter and post-action state

An authenticated source directory establishes the starting owners, not the final process read set.
In `WorldStepProcessor.DecideImmediate`, selected actions can change gathering, coordination, inventory,
reservoirs, transport, liquid placement and dialogues before the advancing boundary. Process discovery
must see those admitted tentative replacements. A source-bound index can identify candidates, but
cannot authorize applying an old owner's result after an action replaces it. Earlier processes can
also change inputs of later ones: transport replaces reservoir-backed inventory before the complete
candidate is admitted. Missing dependencies abort preparation; they are never equivalent to an empty
owner or a lawful blocked stage.

| Boundary responsibility | Actual discovery and evaluation input | Timing and outcome contract |
| --- | --- | --- |
| Material proposals and paid quotes | Post-action coordination books, including settlements, closures and newly retained offers | `ExpireDue` uses the selected advancing-step count and records outcomes at the world sequence, after actions. A world sequence is not an expiry clock. |
| Reservoir transport | Post-action pending stages, reservations, connections, physical reservoirs, material history and occupied identities | `ReservoirTransportPlanner` selects pending stages by due calendar instant then stage identity, sharing connection throughput across that selected boundary. Held evaluation retains stages; blocked head/material/throughput retains the uncompleted obligation and conserved stock. |
| Gathering recovery | Post-action node depletion/cursor and admitted tool powers, plus every gathering actor's effort | `GatheringSnapshot.AdvanceStep` uses process intervals where pacing exists, otherwise advancing steps. Due regrowth also requires enough unissued capacity for a full cycle. A due timestamp alone grants no new stock; effort recovery still applies when no node regrows. |
| Personal exposure | Every retained resident, regardless of camera, work eligibility or controller | Ordinary exploration applies one `ExperienceStep`; tactical advancement applies its actual exposure duration. Non-time decisions do not earn another interval. |
| Detailed liquids | The reconciled post-action liquid owner, exact source portions/cells and effective reading hold | `LiquidCellPlanner` uses the next exact physical process boundary. A held plan advances its boundary without moving liquid; an invalid bounded plan aborts the step. Transfers use starting quantities, not repeated within-step propagation. |

Transport blocking is not NPC proposal exclusion or missing data. Its planner records
`BlockedByThroughput`, `BlockedByHead` or `BlockedByMaterial` while advancing its own boundary; due
pending stages remain candidates for a later lawful opportunity. Reading holds preserve the stages.
Gathering can likewise retain a depleted node whose finite source cannot fund another complete cycle.
Report eligible work, evaluated blocking, exhausted stock and absent dependencies separately. An age
metric that counts every old timestamp as skipped mandatory execution misrepresents these rules.

### Clock and interpreter boundaries

The ordinary processor's dispatch owns which law runs. An active encounter does not execute the
ordinary post-action process sequence. `WorldEncounterActionProcessor` and `WorldEncounterProcessor`
observe tactical timing; gathering and liquids advance only when the pacing accumulator earns its
next process interval. Personal exposure uses the actual tactical duration. Decision and resolution
boundaries must not be multiplied into exploration intervals. Withdrawal resolvers retain their own
paid-action contracts; a discovery adapter must not add ambient time for the same opportunity.

Encounter lifecycle transitions reconstruct the complete admitted world while retaining its calendar,
advancing count, gathering and resident exposure. Care updates its selected treatment/consent facts
and readiness through its own processor; it is not an ordinary advancing boundary. Controller,
permission and assignment boundaries also dispatch separately. A process owner present in the world
does not imply its ordinary advancing rule runs at every commit.

`WorldConsequenceProcessor` illustrates a distinct persisted continuation: accepted excavation retains
its command and reserved outcome across geometric work. Intermediate commits do not earn its reserved
interval; terminal publication returns the accepted command to `DecideImmediate` exactly once. An
unpublished construction read handle has no equivalent saved contract. Neither mechanism authorizes
reusing a calendar queue for unrelated continuations.

### Completeness and derived indices

A general discovery adapter must identify the interpreter, owner scope, source revision/hash, timing
domain and actual occurrence or continuation. Discovery coverage includes owners with no due item:
otherwise omitting an owner could look like an empty result. Any derived index needs an authenticated
membership claim plus invalidation when actions, earlier processes, control/phase changes or migration
change its dependencies. A partially loaded index cannot certify absence.

These are extension obligations, not a saved general discovery index. The construction binding knows
its explicitly captured work set and mandatory legacy owner union. It does not enumerate all current
profile obligations or supply timing-domain membership. Preserve existing semantic order and complete
original-history validation while adding typed domain adapters; use existing operation owners rather
than an alternative simulation of their effects.


## Runtime binding: typed process discovery

Gathering and reserved transport expose derived immutable discovery objects consumed by their existing
operations. [Regrowth discovery](materials-and-activities.md#source-bound-regrowth-discovery) reports
actual due node occurrences and finite-stock eligibility; both exact-step and idle gathering recovery
consume it while retaining complete actor effort. [Transport discovery](reservoir-transport.md#runtime-binding-action-clock-and-commit-order)
retains stage identity and due ordering across lawful blocking; the planner still owns pressure,
throughput, custody and created identities. These are separate timing-domain contracts, not one generic
event counter. Both retain complete source owners and use reference affinity. Neither authenticates
absent owners, persists a discovery index, substitutes for host selection or establishes fairness.
The ordinary world invokes them through the effective post-action gathering/transport owner; queries
against a source snapshot cannot be transferred silently to its successor.
