# Typed world composition and healing decision

Decision date: 2026-09-25. Owner requests a more fundamental model for healing/care, permits retaining
high-level concepts, and asks that the discipline apply coherently across the whole world.

## Decision

Use **domain actions composed from typed state, relations and atomic operations**. Keep Heal,
AdministerRemedy, Rest, Repair, Trade and Build meaningful. Extract shared ownership and interaction
mechanics beneath them. Apply the [world composition design contract](../Runtime/docs/architecture/world-composition.md)
across all systems without a universal ECS/property bag, new process, language rewrite or wholesale
refactor. Pure C#, existing planners and one complete-world authority remain the foundation.

This records the chosen design direction, not a claim that a generic interaction/effect framework is
implemented. The finite-care work in progress is preserved before adapting it. Existing saves and
qualified consumption evidence remain unchanged. Frozen specifications retain their historical bytes.

## What the care exercise exposed

The draft has strong conservation, identity and ordering checks, but its shape is too tied to one
story: a particular defeated player, a neutral helper, an acknowledgment and resumption of one episode.
That is a useful acceptance scenario, not the foundation for all healing. Self-use, conscious ally
aid, NPC-to-NPC aid, passive recovery, poison, camp treatment and magic should not each require a new
exception to the last combat record's HP check.

There are four separate concepts hiding under “acknowledgment”:

| Concept | Correct home | Consequence |
| --- | --- | --- |
| “Continue with this recovery option” | Player/session choice, pinned to the exact available outcome/offer | May request care or release the decision hold; is not the unconscious body's physical action |
| Permission to treat | Scoped assistance policy or agreement between actual participants | Specifies permitted treatment/recipient/helper/resource scope; relationship strength is not permission |
| Helper undertakes treatment | Ordinary authenticated helper intent or accepted activity commitment | Revalidates ability, reach, dose, timing and obligations; a pending commitment needs real reservations/interruption/restart rules |
| Request delivered/committed | Authority receipt and exact retry handling | Prevents duplicate effects; transport acknowledgment is not fictional consent |

Keep a specific prior care agreement for the authored defeat scenario. Represent its general meaning
as a scoped permission, while preserving the helper's independent authority over their stock. Do not
make every heal require a new player confirmation. An authorized NPC may initiate aid under its own
goals and established policy; a player sees meaningful choices where a choice is actually needed.
Hostile effects have their own targeting rules, not an accidental medical-consent requirement.

An unaccepted aid offer is not a reservation. If a persisted helper activity is accepted, explicitly
define resource/participant holds, revocation, cancellation and recoverable failure. Losing a helper
or its permission must not strand the world in an endless mandatory-treatment retry loop. A completed
player choice cannot authorize a replacement helper or replacement dose without a new legal selection.

## Concrete treatment composition

Illustrative responsibility names below are proposals, not existing APIs:

1. `TreatmentAction` names the helper, patient, remedy and exact item selection. Its domain definition
   distinguishes drinking, external aid, bandaging, magic and anatomical requirements. An incapacitated
   person must not be assumed able to swallow merely because HP is zero; the current source Potion's
   external aid remains an explicit fantasy adaptation with its delivery mode defined by content.
2. `InteractionAuthorization` checks authenticated actor control, scoped permission and stock rights.
   `CapabilityQuery` checks living form, manipulation, target compatibility, reach and readiness.
3. The material owner prepares one exact dose retirement. The resource owner prepares a capped vitality
   restoration. The timing owner prepares the actual action cost. These are private candidate changes.
4. A common `ResourceChange` records target/resource, cause, definition, previous/next resource revision,
   previous/current value and actual delta. Damage and treatment use the same ordered mutation contract.
   There is one current HP value, initially the existing progression resource owner; a new vitality
   facade must not duplicate HP in a second component. A later physical/stat ownership split needs an
   explicit migration rather than an incidental rewrite during this feature. Existing level-up
   restoration also participates; extracting only combat/healing would leave an untracked writer.
5. The world composes those changes, validates material/geometry and lets the encounter coordinator
   reconcile eligibility and phase. It commits once. Only then do permitted observations drive the
   helper's use gesture, the patient's reaction, sound and UI.

The historical lethal attack remains true at its own sequence. It is not rewritten to say the patient
had 40 HP then, and it is not treated as the final owner of the patient's HP now. Persisted ordered
effect evidence plus actual journal replay establishes later changes. A generic arithmetic mutation
record alone still cannot authorize healing. Retain exact command, policy and resource provenance.

A persistent effect is distinct from an instantaneous effect: regeneration/bleeding retains its
source, target, stacking/expiry rules, exposure clock and fractional carry. A longer treatment is an
activity with actual elapsed stages; it is not a zero-duration Potion use stretched by an animation.
Healing HP, stopping bleeding, treating infection, regrowing a part and resurrection remain separate
typed operations. Shared machinery should expose those distinctions rather than erase them.

## What to reuse and what to revise

| Existing work | Disposition |
| --- | --- |
| `ItemConsumptionPlanner`, V2 exact raw/produced claims and partition evidence | Retain; independent of how a restorative action is named |
| Geography/material retirement composition and explicit codecs | Retain after their tests pass; every consumption action needs these joins |
| Authenticated intents, revision pins, historical IDs, atomic candidate/receipt publication | Reuse existing authority infrastructure |
| Care-specific last-attack HP supersession | Replace with the bounded shared resource-effect ordering contract before extending healing families |
| Prior agreement and real neutral helper | Retain as a concrete scenario/permission binding, not a universal world entity taxonomy |
| Care acknowledgment as mandatory world behavior | Separate the optional defeat continuation choice from permission, helper activity and receipt |
| Fixed same-episode recovery behavior | Keep as the exact first scenario proof; broader eligibility reconciliation belongs to encounter rules |
| Every healed event kept forever in an entity array | Avoid; preserve reconstructable causal history through qualified journal/checkpoint ownership |

The preservation checkpoint is
`codex/checkpoint-care-wip-before-motion-research-20260926T000024Z` (`edc0d55`). It includes unqualified
runtime/test work and failed build logs; it is not a passing milestone. The [manifest](checkpoints/care-wip-before-motion-research-20260926T000024Z.json)
records scope and restoration. The last verified consumption milestone remains `04e6dba`.

## Increment and evidence

First extract the smallest useful resource-effect/permission composition and prove it through both
self-use and external aid. The same primitive must support an ordinary living recipient, not only a
defeated player. Preserve the actual raw and crafted Potion fixtures and exact material histories.
Then prove source defeat → explicit recovery choice when required → actual helper action → renewed
choice and another attack, with no time/RNG/debt reset. Add passive recovery only with its actual
exposure/carry contract and a pause/restart test. Do not implement the entire facet catalogue first.

Required adversarial evidence includes denied/revoked permission, unwilling or incapable helper,
wrong delivery mode, non-applicable body, stale target/dose, competing users of one dose, cost failure,
full health, death versus incapacity, poison/healing order, complete atomic rejection and duplicate
retry. Define no-effect cost policy explicitly (the initial ordinary full-health Potion offer rejects
before spending); do not let a generic operation invent it. Durable acceptance still needs exclusive
schema, capacity reserved for accepted obligations, restart before readiness and actual process-kill
tests. Native acceptance still needs observed restrained gestures and real gameplay continuation.

The broader cross-system matrix and encapsulation rules live in the reference, avoiding a second
competing specification. Each future feature checks its owners, dependencies and semantics there.

## Comparative evidence and limits

Cataclysm's published wound interface distinguishes body-part wounds and treatment definitions with
skill/time requirements. Its effect interface separately describes removal, prevention and immunity.
Those are useful precedents for preserving semantics within reusable mechanisms; they do not prove
our determinism, storage or co-op design. [Wounds](https://docs.cataclysmdda.org/JSON/WOUNDS.html),
[effects](https://docs.cataclysmdda.org/JSON/EFFECTS_JSON.html).

The project's existing body functions, immutable component snapshots, source-backed material plans
and common player/NPC command boundary already support this direction. This decision refines their
composition; it does not import another game's implementation or claim the whole world model is done.
