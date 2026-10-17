# Physical encounter actions

## Ownership and admission

[WorldEncounterActionAdmission](../../src/OpenFantasy.Core/Worlds/WorldEncounterActionAdmission.cs)
admits a fresh action-capable encounter over the complete [paced world](world-pacing.md). Profile
twenty-three retains every terrain, liquid, custody, activity, resident and timing owner. Admission
requires no elapsed time, queued choice, random draw or previous resolution. It pins the complete
basis and action archive, including the common terrain seed. Ordinary successors cannot add or
remove this owner or change an older world's interpretation.

[Profile twenty-four's permanent lifecycle](encounter-lifecycle.md) owns the same action component
inside its current/last episode archive. It adds explicit entry, staging and outcome release while
retaining the source-backed action law. Those transitions do not change profile twenty-three's fixed
encounter semantics.

[EncounterActionSnapshot](../../src/OpenFantasy.Core/Encounters/EncounterActionSnapshot.cs) owns
immutable ability definitions, explicit actor grants and encounter sides, retained ability requests,
the next deterministic damage draw and the last resolution. [Progression](../../src/OpenFantasy.Core/Progression)
continues to own HP, MP and derived stats. The action record describes an actual change; it does not
create another health ledger. Timing owns readiness and queued decision pins. Complete admission
requires exact actor coverage and a one-to-one match between timing action choices and retained
requests.

The care-enabled composition additionally retains [ordered resource history](world-resource-history.md).
It checks the original successful attack against its historical stats, draw and paired HP/MP effects.
Later treatment or earned level refills explain current resources without rewriting that attack.

An encounter side is a local participation assignment. Side zero means a coupled bystander; positive
different sides permit opposing targets. Sides are not species hostility, alignment, faction membership
or general relationship policy. Bystanders retain their bodies, inventory, quests and applicable
exposure while taking no combat decisions.

## Selection, legality and consequences

[QueueEncounterAbilityIntent](../../src/OpenFantasy.Core/Encounters/QueueEncounterAbilityIntent.cs)
uses the ordinary authenticated actor-command envelope. It names the encounter, expected timing
revision, owned ability and target. [WorldEncounterActions](../../src/OpenFantasy.Core/Worlds/WorldEncounterActions.cs)
checks living participation, explicit grant, opposing target, level, available MP, functional anatomy
and actual spatial reach. Physical reach requires equal feet elevation, bounded XY center distance and
a clear center sightline through retained terrain and constructed obstacles. This is a declared melee
geometry contract, not projectile travel or arbitrary-height attack simulation.

[WorldEncounterActionProcessor](../../src/OpenFantasy.Core/Worlds/WorldEncounterActionProcessor.cs)
also authenticates control, actor revision, timing revision and due readiness. A batch has at most one
choice per actor and compares commands against its starting timing revision. Every submitted choice
receives its ordinary terminal queue receipt. Applied means accepted into the queue; it does not mean
that damage has happened. The complete request and command hash survive save and recovery. One exception
exists only in the explicit retreat world: a queued retreat is an Action decision pinned to its command hash whose
pending request is retained by the retreat owner rather than the action queue, and after its first sample the
committed retreat holds the actor without any decision; while it holds, Wait and abilities are refused and offered
as blocked (`Retreating`) ([encounter-retreat](encounter-retreat.md)).

In profile41 Core, [collective withdrawal](party-withdrawal.md) applies that standing decision to
each uncompleted route independently. Arrival restores ordinary due decisions while retaining the
party commitment. Wait keeps that commitment and pays its usual cost; an accepted ability cancels
it. Explicit route cancellation is a nonadvancing decision boundary, including between movement
samples, and preserves previously paid debt. It never queues a free replacement action.

Resolution is a separate system boundary. All required human choices must be present before the first
eligible due actor resolves in canonical order. Legality is checked again against the actual world.
A legal physical action spends its MP, updates the actual target's HP, advances its damage stream and
charges positive readiness debt in one publication. An invalidated action charges readiness but spends
no MP and draws no damage randomness. Wait uses the actor's ordinary positive delay without damage.
These zero-duration boundaries do not advance calendar, liquid flow or passive work.

For nonnegative integer stats, the physical formula is:

```
base = max(1, floor(attack * powerTenths / 10) - floor(defense / 2))
variation = 900 + uniform_integer(0, 200)
damage = min(currentTargetHp, max(1, floor(base * variation / 1000)))
delay = ceil(10000 * costNumerator / (costDenominator * normalizedSpeed))
```

Wide integer arithmetic prevents intermediate overflow. The standalone damage helper saturates to
`uint.MaxValue` before the processor clamps to signed-int HP; this preserves exact actual loss even
for the largest admitted attack and power. [CounterRandomV1](../../src/OpenFantasy.Core/CounterRandomV1.cs)
uses the terrain seed, action rules hash, `encounter.physical-damage.v1` domain and encounter identity.
Unbiased bounded sampling, fingerprint, next counter and exhaustion state are retained. Draws are
independent of rendering, wall-clock time and unrelated random domains.

## Incapacity and completion

Zero HP makes an actor ineligible for decisions and targeting. Its identity, anatomy, possessions,
controller and timing row remain. Outstanding choices for newly incapacitated actors are cancelled
with their exact IDs retained in the resolution. This is combat incapacity, distinct from the full
death, succession, remains, afterlife and resurrection design.

An encounter has an outcome when at most one living positive side remains. Remaining choices are
cancelled, and the session holds the resulting complete state. Readiness debt and fractional process
carry are retained. Outcome alone neither releases exploration nor grants rewards, drops or respawn.
Profile twenty-three has no release transition. Profile twenty-four's
[lifecycle contract](encounter-lifecycle.md#entry-staging-and-release) requires an authenticated release
by the living initiator, pinned to the exact outcome and current complete world admission. Joining,
healing, death and broader effects have separate contracts.

Eligible due actors are derived from timing plus authoritative HP and sides. This preserves the
portable timing component's complete actor rows and the original profile-twenty-two Wait semantics.
Tactical advance is allowed only with no eligible due actor and reduces retained debt by five units.
It earns 50 ms of exposure; real liquid flow and gathering recovery still require a completed
1,800 ms process interval. Construction and excavation earn no free labor.

## Content, hosting and presentation

[EncounterFarmAdmission](../../src/OpenFantasy.Hosting/Content/EncounterFarmAdmission.cs) creates a
separate fresh Squire-versus-Bramble-Wolf confrontation using retained source records and checked
payload hashes. It adds the wolf before complete reservoir and custody binding. The wolf has real
source stats, an empty inventory and wallet, no granted language or labor, and an authored quadruped
body with a functional natural weapon. Wren remains the same quest-bearing bystander. The actual
project owner supplies the watercourse owner; adding another host-controlled resident cannot select
a different owner by accident.

[EncounterJourneyAdmission](../../src/OpenFantasy.Hosting/Content/EncounterJourneyAdmission.cs)
creates a separate four-resident exploration world with two such wolves and two explicitly authored
scenarios. Its shared source definitions do not duplicate actor identities, possessions or earned work.
The [lifecycle reference](encounter-lifecycle.md) describes its complete ownership and public entry view.

Strike retains power 10, MP cost 0 and level requirement 1. Its source range 48 plus the original
player grace 8 maps to 448 spatial subunits. The wolf's power 10 and range 44 map to 352 subunits.
Source ATB costs 300 and 400 become the explicit readiness ratios 300/300 and 400/300; legacy ATB
values are not reinterpreted as tactical units. The new grounded/sightline rule and authored body
dimensions are deliberate adaptations. Other retained abilities and legacy death rewards do not
become executable merely because their records exist.

[LocalEncounterController](../../src/OpenFantasy.Hosting/Simulation/LocalEncounterController.cs)
proposes actual legal attacks using declared grants and opponents, with at most 32 candidate
evaluations per actor before choosing Wait. It uses the same legality as player actions. The complete
local world view is this controller's bounded context; broader NPC perception requires its own
knowledge boundary. Readiness, no-catch-up scheduling and authenticated publication remain in the
[local session](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs).

[LocalWorldView](../../src/OpenFantasy.Hosting/Simulation/LocalWorldView.cs) projects current and
maximum HP, side, incapacity and encounter outcome. These are observation contracts; battle controls,
animation, choreography, audio and player acceptance are separate presentation responsibilities.

## Canonical storage

[EncounterActionCodecV1](../../src/OpenFantasy.Core/Encounters/EncounterActionCodecV1.cs) encodes a
13-field frame: tag, version, rules hash, world, branch, encounter, seed, revision, abilities,
combatants, pending requests, random state and optional last resolution. Bounds are 32 definitions,
64 combatants, 64 pending requests of at most 1,024 bytes each, and a 128 KiB action archive.

[WorldSnapshotCodecV23](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV23.cs) appends that archive
to the direct paced root as field index 21, giving 22 fields. It restores all components before
complete admission and requires byte-canonical re-encoding. The 16 MiB world allowance reserves the
maximum liquid, pacing and action archives together. Independent journal and identity limits apply.
Ability commands use action version eleven; batches and roots use profile twenty-three.

Schema five explicitly admits profiles twenty-two and twenty-three. The existing writer atomically
commits state, accepted batch, receipts and historical identities. Recovery replays each accepted
boundary and verifies exact hashes, draws, resources, outcomes and checkpoints before readiness.
Canonical shape alone does not authenticate a command. The operator
`--create-encounter-world <database> <seed> <player-name>` admits the source-backed fresh world without
migrating lived saves or selecting a new ordinary native launch.

Profile twenty-four instead retains the action archive inside the permanent lifecycle and uses
[schema six](encounter-lifecycle.md#canonical-persistence-and-scope). Its derived episode identity pins
the damage stream independently of caller-chosen command and cause identities.
