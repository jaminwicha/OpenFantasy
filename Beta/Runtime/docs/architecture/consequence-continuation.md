# Durable consequence continuation

## Command, world and time ownership

[WorldConsequenceAdmission](../../src/OpenFantasy.Core/Worlds/WorldConsequenceAdmission.cs) admits profile
sixteen from an unplayed settlement world. The immutable
[ConsequenceExecutionPolicy](../../src/OpenFantasy.Core/Worlds/ConsequenceExecutionPolicy.cs) selects the
number of geometric work units per continuation. It remains present when no operation is pending, so
completion cannot silently change the saved world's interpretation. The dry settlement, load and
fabrication owners remain prerequisites; this profile does not admit general physical destruction.

[WorldConsequenceProcessor](../../src/OpenFantasy.Core/Worlds/WorldConsequenceProcessor.cs) accepts an eligible
excavation only as a singleton advancing batch without movement. Selection isolates it before asking
NPCs for sibling actions. The trigger reserves the command and its original advancing interval, increments
commit revision and retains [PendingExcavation](../../src/OpenFantasy.Core/Worlds/PendingExcavation.cs).
It does not advance calendar, resident exposure, gathering recovery, labor, terrain, custody or poses.
There is no terminal receipt yet. Ineligible commands retain ordinary immediate rejection semantics.

The private calculation may contain unsupported proposed geometry. Published world state remains fully
stable and admitted. While a job exists, only an explicit empty-action, empty-movement continuation is
accepted. It receives a fresh cause and increments revision. Completion publishes the original command's
single terminal receipt, the final work debit and consequences, and exactly one advancing interval.
Refusal also charges that interval and capped effort recovery, but publishes no work debit, source or
physical change. Effort is spent before recovery on success, preserving behavior at the cap. No wall-clock
catch-up or second player request grants more time.

Before accepting durable work, the owner validates and encodes the time-only refusal successor. Final
success admission also checks complete encoded root size. This prevents accepting an interval whose
clock/exposure arithmetic or terminal fallback cannot be represented. A successful geometric calculation
can still refuse at final capacity, material, resource-anchor or complete-load admission. Partial excavation
labor uses the same complete-hole preflight, but discards projected movement and issuance until final work.

## Actual incremental geometry

[ConstructionSettlementResolution](../../src/OpenFantasy.Core/Structures/ConstructionSettlementResolution.cs)
is immutable. `Advance` executes actual base-contact, member-sweep, rider-sweep or actor-footing/landing
queries; it preserves computed progress between calls. Uniform intact descent, safe distances, outside
collision groups and known rated terrain obey the [settlement contract](structural-settlement.md).
The synchronous resolver exhausts the same calculation. Profile sixteen's finite bound is 192 geometric work units: at most
32 base contacts, 32 member sweeps, 64 rider sweeps and 64 actor units. An actor unit checks footing
and may then query descent, so a work unit is not a single primitive query. Zero-work phase transitions do
not manufacture progress. Terminal advancement is idempotent, and a failed result cannot expose placements.

[The resolution codec](../../src/OpenFantasy.Core/Structures/ConstructionSettlementResolutionCodecV1.cs)
retains the complete input pin, completed query count and terminal status in a frame bounded to 512 bytes.
Restoration reconstructs exactly that prefix against supplied original spatial and physical state,
candidate terrain and immutable policy, then checks canonical status and bytes. It never trusts detached
cached positions. Forward work does not replay the prefix; restoration does. Initial topology/rider scans,
dependency hashes, array copying, complete-scene validation and complete-world load admission are not sliced.
A query-count budget is consequently not a wall-clock or production throughput guarantee.

## Stable dependency and identity reservations

The pending basis is the complete job-free root at the captured original revision. Profile sixteen uses
its fixed single-assembly interpretation; [profile seventeen](coupled-settlement.md) retains its own coupled
policy, versioned cursor and assembly-dependent work bound.
Every semantic component and policy is pinned. Current revision and cursor are outside that basis;
no duplicate world snapshot or inverse effort/respawn operation is needed. Saved pending revision must
be `basis revision + 1 + continuation count`. Each nonterminal continuation accounts for its full authored
query budget; a terminal calculation is published immediately. Archive admission validates command eligibility, canonical work and the pinned world dependencies.
A standalone supplied root does not authenticate an accepted trigger: a different eligible command can
form a different valid supplied root. Exact command/trigger bytes and the actual sequence of continuation
inputs are bound to their accepted transitions by complete journal replay.

The pending identity index reserves the future source lot and both possible terminal result IDs at every
possible completion sequence within the finite continuation bound. IDs are derived from the original
command; results retain the existing receipt identity convention. The trigger refuses aliases within this
set, the trigger/command, existing world identities or recorded history. Continuation causes cannot occupy
those future IDs. Only the chosen terminal result and an actually issued lot become durable issued-identity
rows. Unused possibilities retire with the pending job. Completion removes its own temporary reservations
before applying the exact original action, while historical/genesis occupancy remains enforced.

## Storage, recovery and host scheduling

[WorldSnapshotCodecV16](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV16.cs) has a direct twenty-two-field
root: profile fifteen's component positions, the execution policy and an optional pending frame.
[PendingExcavationCodecV1](../../src/OpenFantasy.Core/Worlds/PendingExcavationCodecV1.cs) retains exact command,
trigger cause, original revision, basis hash, continuation count and geometric archive. Encoding is pure;
decoding validates canonical bytes and the complete stable world. The 16 MiB root bound remains in force.

[Schema four](../../src/OpenFantasy.Hosting/Worlds/WorldDatabaseSchemaFour.cs) admits commit kind 4 for
continuation; kind 3 remains reserved for migration. Profiles sixteen and seventeen require schema four, while earlier
profiles retain their exact schemas. No separate pending table becomes a second authority. Commit, head,
checkpoint, terminal outcomes and issued identities are written in the existing single transaction.
[WorldCommitPreparation](../../src/OpenFantasy.Hosting/Worlds/WorldCommitPreparation.cs) accounts exhaustively:
an ordinary input receives its terminal outcome, an exact trigger becomes retained pending work, and a
terminal continuation owes only its predecessor's exact command. No general omitted-outcome exception exists.

[The journal policy](../../src/OpenFantasy.Hosting/Worlds/WorldJournalPolicy.cs) checks worst-case remaining
continuation commits, encoded inputs/outcomes and the original terminal result before accepting a trigger.
The fixed development envelopes remain 250,000 commits, 65,536 terminal outcomes and 128 MiB of accepted
journal. Full replay repeats the same admission and validates complete ledgers and checkpoints.

[LocalWorldSession](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs) resumes durable work before
client readiness and ordinary player/NPC selection. Resolving is distinct from reading hold and pause.
An exact authenticated retry attaches to the accepted command, including its original session bytes;
current controller authorization still applies. Conflicting requests refuse. A waiter completes only with
the terminal receipt. Stopping cancels in-memory waiting, leaving committed work available after restart.
Read-only opening and recorded-history reconstruction never advance the cursor or write consequences.

## Extension boundaries

The durable owner covers one isolated excavation and bounded intact settlement. Profile seventeen adds
[explicit coupled group ownership](coupled-settlement.md), including cross-assembly contact, one uniform
trajectory and dynamic reservations. Concurrent jobs, unequal interacting trajectories, fracture/debris
conservation, harmful impacts, surviving/deforming joints, long flight, wet displacement and world-wide
asynchronous simulation require separate domain owners and qualification. Durable continuation preserves those obligations; it does not substitute for them.
