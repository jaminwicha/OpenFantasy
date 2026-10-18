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

`LocalWorldSession` still selects and commits through the ordinary synchronous path; it does not consume
these handles between live ticks. The yielded authority boundary enables that integration but does not
supply its readiness/input-cutoff policy, a final-evaluation CPU budget or a fairness guarantee.

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
unprocessed mandatory effect. Publish the complete logical boundary through the sole writer. A separate
durable preparation continuation needs explicit admission, recovery and cancellation semantics; the
existence of a queue or a reserved excavation continuation does not provide those semantics generally.

Measure mandatory backlog in its own timing domain. Report eligible proposal deferrals and host
preparation stalls separately. Include missing/invalidated dependencies, lawful holds, repeated competing
work and restart in fairness qualification. Stable iteration order alone proves no starvation bound,
and a one-builder completion cannot establish fairness across actors, domains or places.
