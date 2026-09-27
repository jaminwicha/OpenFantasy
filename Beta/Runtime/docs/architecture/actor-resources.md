# Actor resources and restorative composition

## State ownership and named transitions

[ActorProgressionState](../../src/OpenFantasy.Core/Progression/ActorProgressionState.cs) owns current
HP and MP alongside level, residual experience, pinned stat profile and progression curve. Effective
resource capacities come from those rules. Anatomy, consciousness, death, permission and combat
eligibility have distinct owners and policies; a vitality increase alone does not repair an injury.

The immutable actor prepares named operations rather than exposing an arbitrary resource setter:

| Operation | Resource law | Actor revision |
| --- | --- | --- |
| `TakeDamage` | Subtract the lesser of requested damage and current HP | Once for an actual change |
| `RestoreHealth` | Add the lesser of requested restoration and missing HP | Once for an actual change |
| `SpendMagic` | Pay the entire MP amount or reject insufficient resources | Once for an actual change |
| Experience reward | Normalize earned XP; refill HP and MP to new maxima only if a level is gained | Once for the complete progression change, including a reward without a level gain |

Zero resource deltas retain the original actor for the first three operations. Their caller still
decides whether a no-effect action is legal and what it costs. Experience rewards retain their
explicit revision semantics even when resource values are unchanged. Checked arithmetic refuses
revision/experience/stat overflow. Restoration uses the missing amount before adding, so an excessive
unsigned request cannot overflow signed current health.

[ActorResourceTransition](../../src/OpenFantasy.Core/Progression/ActorResourceTransition.cs) contains
the original actor, immutable candidate, named operation, requested amount and actual HP/MP changes,
including before/after capacities. The actor is the sole current-state owner. The transition is a
private calculation result, **not** authenticated history, a command receipt or permission to publish.
Its constructor is internal, and callers obtain it through named domain behavior.

## Runtime callers and composition

[WorldEncounterActionProcessor](../../src/OpenFantasy.Core/Worlds/WorldEncounterActionProcessor.cs)
calculates authorized physical damage using the admitted ability, stats and deterministic draw,
then uses `TakeDamage` and `SpendMagic`. Target and caster remain separate actual actors. A zero-MP
ability does not revise the caster. The complete attack advances progression once while publishing
resources, randomness, decisions and readiness together.

[ActorRewardPlanner](../../src/OpenFantasy.Core/Progression/ActorRewardPlanner.cs) returns its actor
transition with the reward candidate and gained levels. A level gain changes stats, XP and both
resources in one actor revision; it is not followed by a second healing revision. Currency and quest
completion remain coupled by the caller's complete transaction.

[RestorativeItemPlanner](../../src/OpenFantasy.Core/Care/RestorativeItemPlanner.cs) composes the
[material consumption contract](item-consumption.md) with `RestoreHealth` for either the supplier or
another admitted recipient. It accepts a caller-admitted restoration amount, one exact dose selection
and the expected recipient progression revision. The selection retains its exact definition/version;
the caller must bind the restorative amount to the approved content definition.

Supplier authentication and stock ownership precede patient-state inspection. Inventory, production
and progression must share world/branch scope and the complete actor set. A stale patient revision,
unavailable dose, quantity other than one, zero restoration or full health refuses the composition.
No-effect use spends nothing under this remedy policy. Any handled failure, including resource or
progression revision overflow, returns all original owners and no claim or transition.

The [plan](../../src/OpenFantasy.Core/Care/RestorativeItemPlan.cs) contains the coupled private
inventory, production and progression candidates, exact consumption claim and actor transition.
Unrelated actors, currency and MP retain their state. It grants no complete-world publication rights:
the caller owns medical permission, delivery mode, anatomy, reach, readiness/action costs, encounter
participation, geography/material joins and historical command identity. Recipient identity alone
does not establish permission; HP zero alone does not establish death or ability to drink.

The [aftermath-care caller](aftermath-care.md) supplies a separate prior agreement, exact defeat
acknowledgment, capable reachable neutral helper and pinned remedy. It couples the actual dose,
restoration, readiness debt, encounter resumption and resource history in one complete transaction.

## Storage and history boundary

Actor bytes remain owned by [ProgressionCodecV1](../../src/OpenFantasy.Core/Progression/ProgressionCodecV1.cs).
The transition result adds no actor field and does not alter the existing progression or encounter
formats. Material consumption uses its explicit V2 production contract. Restoring these independent
components proves their structural consistency; it does not authenticate a treatment event.

## Ordered continuity evidence

[ActorResourceHistory](../../src/OpenFantasy.Core/Progression/ResourceHistory/ActorResourceHistory.cs)
replays a bounded segment from a separately admitted progression basis to the complete current actor
set. It is standalone consistency evidence, not another current HP/MP owner or a world admission API.
`ActorResourceChange` re-executes a named law over the exact prior actor revision and checks the whole
result, including level, XP and resource capacities. No-op damage/restoration/MP transitions add no
record; every XP revision remains recorded, including zero XP. Reordering, omission and substituted
actor/profile/curve pins cannot be hidden by comparing only final HP.

`ResourceChangeBatch` contains the ordered actor changes from one committed command. World sequence
and command ordinal order batches; several commands can share a world sequence. Ordinal gaps are
legal for rejected or non-resource commands. Every batch is strictly later than the basis world
sequence, so segment boundaries represent completed steps; mid-step rollover is not admitted. Effects within a command have contiguous ordinals.
The progression revision advances at least once per retained batch, not once per actor effect.
The current actor roster and admitted profile/curve sets match the segment basis exactly; changes
to those sets require an explicitly admitted segment boundary. Currency retains its own authority.

The causal reference pins an evidence identity and rule definition/version. Local checks prevent
scope/actor/account/rule identity aliases and conflicting rule pins. They do not resolve the evidence,
authenticate a command, or prove its requested amount, permission, payment or randomness. An attack
may resolve an earlier selected command at a later world sequence. History must publish or roll back
with the complete action, including later geography/material reconciliation. Repeated passive effects
require a world-owned process-occurrence contract. This component only enforces unique batch command
identities; causal evidence identities may repeat across effects and batches. The world must bind each
occurrence to its continuing source and prevent duplicate application.

[ActorResourceHistoryCodecV1](../../src/OpenFantasy.Core/Progression/ResourceHistory/ActorResourceHistoryCodecV1.cs)
encodes scope, basis sequence/hash, command positions, causes, operations and before/after actor
readings. Decode requires the caller's exact separately admitted basis and boundary. It bounds frames,
counts and arithmetic, replays the history and requires exact canonical re-encoding. Direct domain
construction can refuse with `ArgumentException`, `InvalidOperationException` for unpaid magic, or
`OverflowException` for arithmetic limits. Decode maps these to `ProgressionFormatException`; complete
transaction callers must handle the domain refusals without publishing partial candidates. It neither embeds
a replacement basis nor changes old progression/encounter/world bytes. A segment holds at most 256
batches, 128 changes per batch and 1,024 total changes. These are bounded archive envelopes, not limits
on character lifetime. Continuous play requires authenticated archival continuation with accessible
causal evidence before the envelope fills; dropping records or retaining only an unreachable hash
cannot justify advancement. The component itself provides no rollover/publication mechanism.

Complete-world resource history requires an explicit contract: command/cause,
definition pins, world ordering, actor revisions and actual deltas must join the durable journal.
Every writer, including unchanged-resource XP rewards, must participate. A previous damage record
remains true at its own boundary; later authorized changes must be proven rather than dropping the
comparison against current resources. Existing profiles retain their exact admission rules.

The care-enabled profile implements the in-memory and canonical
[world resource binding](world-resource-history.md): retained domain witnesses, seeded attack
recomputation, funded quest reward replay and atomic history rollback. Its host authentication
and continuous archive contract remain distinct from the standalone component's consistency checks.

Passive recovery additionally requires an owned exposure clock, rates, carry, ordering and interruption
rules. Calling the restoration primitive from a timer or presentation event does not provide those
contracts. See [world composition](world-composition.md) and [time and encounters](time-and-encounters.md).
