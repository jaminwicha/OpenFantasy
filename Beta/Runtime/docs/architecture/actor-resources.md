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

## Storage and history boundary

Actor bytes remain owned by [ProgressionCodecV1](../../src/OpenFantasy.Core/Progression/ProgressionCodecV1.cs).
The transition result adds no actor field and does not alter the existing progression or encounter
formats. Material consumption uses its explicit V2 production contract. Restoring these independent
components proves their structural consistency; it does not authenticate a treatment event.

Generic persistent resource evidence requires an explicit complete-world contract: command/cause,
definition pins, world ordering, actor revisions and actual deltas must join the durable journal.
Every writer, including unchanged-resource XP rewards, must participate. A previous damage record
remains true at its own boundary; later authorized changes must be proven rather than dropping the
comparison against current resources. Existing profiles retain their exact admission rules.

Passive recovery additionally requires an owned exposure clock, rates, carry, ordering and interruption
rules. Calling the restoration primitive from a timer or presentation event does not provide those
contracts. See [world composition](world-composition.md) and [time and encounters](time-and-encounters.md).
