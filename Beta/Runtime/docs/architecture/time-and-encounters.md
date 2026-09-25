# Time and encounters

## Distinct clocks

| Measure | Owner and meaning |
| --- | --- |
| Commit sequence | Authority ordering, including changes that advance no game time |
| Branch calendar | Historical date and fixed-world appointments |
| Local exposure | Applicable duration for needs, work, recovery, growth, condition and biological aging |
| Tactical readiness | Encounter action scheduling and remaining delays |
| Host monotonic time | Input freshness, scheduling and recorded external session decisions |
| Cosmetic time | Client animation, interpolation and atmospheric presentation |

The [WorldInstant](../../src/OpenFantasy.Core/WorldInstant.cs) representation and
[Exposure](../../src/OpenFantasy.Core/Exposure) arithmetic are portable domain contracts. The
[Scheduling](../../src/OpenFantasy.Core/Scheduling) types represent due work, scoped clock coordinates
and deterministic selection. Their presence does not bind every world process to a universal queue.

## Runtime binding: local session pacing

[LocalWorldSession](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs) owns one admitted
authority and a player control binding. Construction does not start time; readiness permits selection.
The session targets 50 ms monotonic deadlines. After a late step it discards missed deadlines and
waits at least one interval; there is no burst catch-up or offline progress.

Each ordinary advancing step contributes 1,800 calendar milliseconds through
[ExposurePacingV1](../../src/OpenFantasy.Core/Exposure/ExposurePacingV1.cs). At uninterrupted nominal
cadence, 48,000 steps produce a full day in 2,400 real seconds: approximately 40 minutes. Holds,
pauses and host stalls lengthen wall-clock days. Input never chooses elapsed simulation time.

Movement is an exact 32-subunit cardinal sample or zero, with a 150 ms freshness lease. Losing
readiness clears movement. A batch selected before that change may finish committing; later batches
remain paused. The client therefore cannot infer committed state solely from its latest focus flag.

An advancing batch resolves actions before movement. Opening a dialogue suppresses movement but
retains that already selected step's time increment. A held batch selects eligible actions only and
advances neither calendar nor exposure, including the close that releases the final hold. Gathering,
regrowth and construction earn no held or offline work. This local dialogue hold is a concrete
runtime behavior; it is not an implementation of encounter pockets or independent co-op timing.

[Detailed liquid flow](liquid-cell-flow.md) processes the selected calendar boundary once after actions
and before movement. Opening a reading hold advances its boundary without moving liquid. Closing in
a held batch advances neither boundary nor liquid; the next advancing step receives only its ordinary
per-face allowance. Flow preparation in an action preflight never creates a second simulation step.

## Design contract: Wait encounters

The [portable encounter timing component](encounter-timing.md) owns canonical readiness, required-choice
barriers and exposure carry. The [complete world pacing binding](world-pacing.md) adds authenticated Wait
choices, 50 ms tactical ticks, zero-duration queue/resolution commits and exact SQLite recovery for a
coordinated resident set. Permanent pacing separates selected advancing batches from earned 1,800 ms
process intervals. Passive gathering recovery and real liquid flow publish only at those boundaries.
Dialogue holds and exploration retain their own semantics. [Physical encounter actions](encounter-actions.md)
bind real resources, anatomy, targeting, damage and incapacity through a separate complete profile.
Safe encounter entry and release remain distinct admission requirements; an outcome retains both
readiness debt and process carry.

Encounter stages are exploration, confrontation, staging, ready/hold, resolving, outcome and release.
Admission claims participants and consequential dependencies before hostile effects apply. Staging
uses legal positions and routes. Camera movement, actor choreography and audio provide an in-place
transition; their duration does not charge or waive authoritative action costs.

Solo and coordinated co-op use branch-wide holds. Independent co-op permits an encounter pocket
to wait while uncoupled activities continue. This explicitly permits local stasis and missed outside
opportunities. Held participants accumulate neither free work/recovery nor retrospective hunger,
aging, weather damage or effect duration when released. Remaining durations follow local exposure.

The coupled set includes carried goods, attached effects, support and dependent processes. A ship
includes its crew, passengers, cargo, fitted parts, attachments and consequential motion/fire.
An independent remote workshop can continue; an output pipeline cannot run while its required
worker or power source is held. Outside attacks, healing, boarding, trade and hazards request
admission before influence, then revalidate source, stock, route, rights and costs at a boundary.
Queued admission is neither a free attack nor a reservation of future resources.

Connected encounters merge in canonical encounter order while preserving remaining readiness and
durations. A bounded shared-pause fallback retains the entire coupled set and completes deterministic
work slices without publishing a partial logical action. Unsupported dependencies cause a recoverable
diagnostic pause; truncating a crowd or ignoring a hazard is not a valid fallback.

Release revalidates terrain, support, ongoing hazards and outside conditions. It preserves outstanding
costs and ends suspension once. It cannot teleport participants to safety, replay stale queued actions
or apply a backlog of invisible attacks.

## Design contract: deadlines, rest and hosted readiness

Personal duration obligations name one exposure anchor; shared fixed appointments use a world instant.
Recurring obligations have occurrence identities and one suspension ledger. Multiple participants'
holds cannot be added together to extend one promise repeatedly. The policy is visible when accepted.

Rest is an ordinary activity with shelter, quality, fatigue, care and interruption. Completed exposure
intervals grant bounded recovery against real deficits; opening camp, reloading or ending sleep grants
no automatic reset. Watches, cooking, repair, research and conversations consume ordinary supplies and
labor. Care of uncoupled dependants requires an available caregiver, even while their owner waits.
Acceleration requires all affected connected controllers' consent and stops at meaningful interrupts.

The default hosted policy pauses when no players have acknowledged a playable ready state. A socket
connection is insufficient. An active connected player has no automatic decision timeout. Confirmed
disconnect and its host-policy handoff are recorded external decisions, replayed as records rather
than recalculated from wall time. Full policies and numeric encounter budgets are owned by the
[technical design](../../../spec-v1_1/TECHNICAL_DESIGN.md).

## Reserved physical consequences

The [durable consequence contract](consequence-continuation.md) retains eligible profile-sixteen excavation
work across explicit continuation commits. The authoritative scene and game clocks remain unchanged until
terminal publication charges the reserved advancing interval once, including on refusal. Active sessions
resume before client readiness; read-only opening and recorded history do not advance pending work.
