# World pacing and coordinated encounters

## Permanent clock ownership

[WorldPacingState](../../src/OpenFantasy.Core/Worlds/WorldPacingState.cs) owns the accepted exposure of
one complete world and branch. It retains an origin calendar, exact integer exposure progress, and an
optional [encounter timing owner](encounter-timing.md) with its exposure at entry. Calendar equals origin
plus accepted exposure. Every resident in this coordinated interpretation has exactly that experienced
duration; no resident is silently outside the clock.

The fixed process rate produces one interval per 1,800 ms, retaining the remainder. `ProcessIntervals`
counts those earned intervals; `ProcessBoundary` is origin plus their whole duration. `AdvancingSteps`
counts selected advancing batches instead. These measures coincide for an exploration-only history;
a world retaining tactical history must keep them distinct even after returning to exploration.
A process must use its appropriate measure rather than assuming a commit or tactical tick grants
one exploration interval.

| Accepted boundary | Calendar and resident exposure | Process effects |
| --- | --- | --- |
| Ordinary exploration step | 1,800 ms | One ordinary interval, with existing dialogue-opening flow suppression |
| Held dialogue interaction | Zero | None |
| Queue Wait choice | Zero | Authenticated pending choice only |
| Resolve Wait choice | Zero | Positive readiness debt; no passive work |
| Tactical advance | 50 ms, five tactical units | Only a newly completed 1,800 ms interval earns passive effects |
| Profile-twenty-four entry, staging or outcome release | Zero | Retains debt, carry and actual positions |

Thirty-five tactical ticks retain 1,750 ms of carry. The next tick earns one interval; restart preserves
that carry and boundary. Choices, resolution, focus loss, waiting for a human and host stalls never
become retrospective elapsed time. `AdvanceExploration` rejects an active encounter.

## Complete admission and actions

[WorldPacingAdmission.CreateFresh](../../src/OpenFantasy.Core/Worlds/WorldPacingAdmission.cs) accepts an
unplayed, unheld profile-twenty-one terrain/liquid world with zero resident exposure. Its admission hash
pins the complete basis hash, process rules and pacing archive. Profile twenty-two retains all custody,
terrain, residents, gathering, production, progression, quests, construction, reservoirs, seals, liquid
placement and excavation owners.

An optional encounter includes every resident in canonical identity order. Actual progression supplies
normalized speed (`Stats.Speed * 10`); dead or nonpositive-speed actors refuse this interpretation.
Players require human decisions and NPCs use automatic decisions. Admission checks the exact resident
set, speed, controller policy, calendar, resident exposure, timing revision bound and advancing-batch
count. Profile-twenty-two encounters exclude dialogue holds and admit only Wait choices. The complete world retains
its 64-resident bound; the timing component's 128-actor capacity neither expands it nor supplies fallback.

Pacing cannot be added or removed through ordinary `WithState` calls. Genesis admission is distinct
from an encounter-entry gameplay action. Profile twenty-two has no release operation. Discarding
readiness debt or a fractional process remainder is not a legal return to exploration.
[Profile twenty-four's lifecycle](encounter-lifecycle.md) supplies explicit entry and outcome release
while keeping permanent pacing, debt and cumulative exploration/tactical accounting. Joining and
merging require separate admission contracts.

[QueueEncounterWaitIntent](../../src/OpenFantasy.Core/Encounters/QueueEncounterWaitIntent.cs) names the
encounter and expected timing revision. [Action version ten](../../src/OpenFantasy.Core/Worlds/WorldActionCodecV10.cs)
uses the ordinary principal, session, actor and expected actor-revision envelope. The command hash pins
the pending choice; the command ID is also the decision ID.

[WorldEncounterProcessor](../../src/OpenFantasy.Core/Worlds/WorldEncounterProcessor.cs) validates actual
control, actor revision, due readiness and absence of a previous choice. Commands selected together
compare against the same starting timing revision. Every submitted command receives an ordinary terminal
queue receipt, including rejection. Applied means queued, not resolved. Resolution is a separate system
boundary, with no second command receipt or duplicated resource grant. Historical cause, command and
result identities remain reserved by the durable ledger.

Profile-twenty-two decision batches are Wait-only and nonempty. Resolution and tactical batches are actionless; all three
exclude movement. The timing owner enforces required-human barriers and canonical confirmation order.
An unavailable system boundary refuses without committing. Only tactical advance increments advancing
steps or exposure. At a newly completed process interval, gathering advances once and the existing liquid
solver processes its next exact 1,800 ms boundary once. Fractional ticks do not multiply flow allowances.
Source-backed liquid, recovery, time and readiness publish in one complete transaction.

Construction and mining require actual labor actions, not passive elapsed-time rewards. Those actions,
movement and dialogue are unavailable during this Wait-only interpretation. Damage, healing, incapacity
and other encounter actions require their own complete legality and consequence contracts.
[Physical encounter actions](encounter-actions.md) define profile twenty-three with authenticated
ability requests, actual HP/MP consequences, explicit bystanders and derived incapacitated-actor
eligibility. It retains the complete timing rows, permanent carry and process rules described here.

Profile twenty-four retains all accepted tactical ticks across successive episodes. Release ends the
active timing binding without resetting exposure or remainder. Its ordinary preflights and fluid
advancement use `ProcessBoundary`, so a fractional calendar after release cannot skip or duplicate a
liquid interval. An exploration step pays at most 180 tactical units of permanent actor debt, but a
recovering actor cannot also act or move using the readiness earned by that same step.

## Host selection and observations

[LocalWorldSession](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs) selects encounter work
only while ready. It combines the pending player request with due NPC choices (Wait in profile
twenty-two, legal physical actions or Wait in profiles twenty-three/twenty-four), then holds for
required human choices, resolves the first due boundary, or selects one tactical tick. Input never
supplies elapsed time. Monotonic deadlines and the no-catch-up policy remain applicable.

A refused pending preview completes that request with an error and retains the encounter boundary;
it cannot fall back to exploration movement, work or time. A host-only failure follows the session's
failure policy. [LocalWorldView](../../src/OpenFantasy.Hosting/Simulation/LocalWorldView.cs) exposes
encounter identity/revision, elapsed units, player readiness, queued-choice status and required-decision
status. Gathering visibility uses earned process intervals. This observation contract is separate from
native battle controls, animation and choreography. Profile twenty-four also exposes its
[lifecycle observation](encounter-lifecycle.md#content-and-public-session-boundary), including permanent
player debt, captured entry targets and an eligible outcome-release token.

## Storage and replay

[WorldPacingCodecV1](../../src/OpenFantasy.Core/Worlds/WorldPacingCodecV1.cs) has seven fields: tag
`world.pacing.v1`, world16, branch16, origin day64/millisecond32, exposure-progress frame, optional
complete timing frame and optional initial encounter exposure day64/millisecond64. Numeric suffixes
mean bits for numbers and bytes for identities. The latter two fields are present together. Its maximum
archive is 18 KiB. Restore checks exact rate, subject, scope, carry and temporal relationships; wide
arithmetic validates both derived calendar instants.

[WorldSnapshotCodecV22](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV22.cs) retains a direct
21-field root: the terrain/liquid composition plus pacing at index twenty. It decodes all owners before
complete admission. The 16 MiB bound reserves maximum liquid and pacing archive sizes so mandatory
pending choices have serialization headroom. Independent identity and journal limits still apply.

[Schema five](../../src/OpenFantasy.Hosting/Worlds/WorldDatabaseSchemaFive.cs) exclusively stores this
paced family, profiles twenty-two and twenty-three, adding tactical-advance, encounter-decision and
encounter-resolution commit kinds. The same
SQLite writer atomically stores state, batch, outcomes and identities. Migration kind three remains a
table-level allowance inherited from schema three; recovery rejects those rows in schema five because
this interpretation defines no migration.

Profile twenty-four uses exclusive [schema six](encounter-lifecycle.md#canonical-persistence-and-scope)
with an additional zero-duration lifecycle commit kind. It preserves the pacing codec while replacing
fixed episode ownership with the permanent lifecycle archive; existing schema-five saves do not acquire
that interpretation on opening.

[WorldGenesisAdmission](../../src/OpenFantasy.Hosting/Worlds/WorldGenesisAdmission.cs) rejects fresh
databases with elapsed exposure, timing progress or already queued choices. Canonical bytes alone do
not authenticate a choice. Recovery replays every accepted batch and compares exact state, receipts,
identities and checkpoints before readiness. Old profiles and schemas retain their meaning. The
privileged operator `--create-paced-world <database> <profile21-genesis.bin> <encounter-id|none>` uses this
admission; it neither migrates a lived save nor changes the normal game launch.

The [full Wait design](time-and-encounters.md#design-contract-wait-encounters) also governs dependency
closure, independent co-op, outside hazards, controllers, bounded continuation and safe release. This
coordinated complete-resident clock is one explicit interpretation within that design.
