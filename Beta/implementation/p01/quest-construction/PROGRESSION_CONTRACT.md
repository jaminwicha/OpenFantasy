# Actor progression and finite quest rewards

The portable `OpenFantasy.Core.Progression` component holds actual actor statistics, current HP/MP,
level, residual XP and conserved currency balances. It has no engine dependency, controller-specific
branch, clock advancement, IO or persistence acknowledgement. The world owner composes its private
reward candidate with quest completion and ingredient delivery in one durable transaction.

## Source behavior and explicit admission choices

The retained `World.cs.txt` source calculates each next-level threshold as
`floor(sqrt(level * level * level * 10000))` using integer square root. Its maximum level is ten.
The admitted curve stores the exact nine transition thresholds:

`100, 282, 519, 800, 1118, 1469, 1852, 2262, 2700`.

The source quest reward adds the full XP amount, repeatedly subtracts each threshold and advances
the level. Every level gain recomputes statistics and refills HP/MP. At the last level, residual XP
continues to accumulate. No reward below the next threshold heals an actor. This implementation
performs checked integer arithmetic, retains cap residuals, and reports every crossed level.
Its final recomputation is equivalent to the source's per-level recomputation for these pinned
linear profiles; there are no intermediate equipment, effect or ability callbacks.

The new admission explicitly chooses an unequipped **Human Squire** for the existing player actor.
The prior sprite and body did not establish a species or class. The profile key and display name
name this composition; class base and growth plus separately stored zero Human modifiers are hashed
into its pin. At level one its statistics are `60/10/12/4/8/8` in the order max HP, max MP, attack,
magic, defense and speed. Growth per level is `9/2/3/1/2/1`. The 420 XP reward produces level three,
38 residual XP, `78/14/18/6/12/10`, and current HP 78 / MP 14.

Wren retains a separately classified **Unassigned** profile: `1/0/0/0/0/1`, with zero growth and zero
flat modifiers. It does not acquire a Farmer or Squire class. Actor rewards use the same schema and
algorithm for either actor: a deliberately awarded unassigned NPC can gain levels while its fixed
statistics remain unchanged. That is a target composition seam, not a claim that the original NPC
spawn code supported quest rewards or selected a class for Wren.

Class calculation preserves source order: base plus linear growth, then admitted heritage modifiers,
then minima of HP 1, MP 0, attack 1, magic 0, defense 0 and speed 1. Unassigned values bypass those
class minima so the actual zero attack remains representable. All effective values fit signed
32-bit integers; negative intermediate class values clamp only after composition. Signed modifiers
use signed 64-bit wire fields with checked restoration into their 32-bit domain.

No equipment is currently equipped or granted by these profiles. Source ability lists, movement
speed, skill unlock execution, status effects, equipment ownership, species simulation and combat
are not imported by this statistic component. It does not infer a species from a sprite. Existing
anatomy, injury, form and body functions remain independent; HP refill does not repair lost parts.
The admitted gathering policy remains gain 10 / cap 1000 / cost 400 even when a stat level increases.
A future stat-derived work policy needs separate qualification and versioning.

The original source creates 200 gil during quest reward. The target instead admits **exactly 200 gil
in a separate quest reserve**, with player and Wren balances initially zero. The reserve is explicitly
authored admission, not Wren's source inventory or an inferred source treasury. A reserve has no
actor owner and does not occupy backpack item capacity. Reward payment debits the reserve and credits
the beneficiary's actor account; the total admitted supply cannot change in the planner.

Source witnesses (unchanged frozen files):

| Witness | SHA-256 |
| --- | --- |
| `source-support/src/Sim/World.cs.txt` | `fc55db3ec2c8a9602e0432a7359e460b8b6a6e92a4123620df4df5190eaea4e2` |
| `source-support/src/Sim/World.Systems.cs.txt` | `c9a9d49ca4021cded3f06c4568075cdf4956149b4736779d4ab4b7711325886e` |
| `source-support/src/Sim/SimConstants.cs.txt` | `7bdcec4fa6f7017bab1cb9c7ff028ee7e28b15ae37bc4503174f91b89e431742` |
| `source-support/data/classes.json` | `27c5956e9b269acdc36ecba339522c426380ea2f35bc33fead49ae778add6234` |

The witness root is `Runtime/content/legacy/aldermere-first-loop/`. Source methods are
`RecomputeStats` (World lines 163–194), `XpToNextLevel` (585–596), `GrantQuestXp`
(World.Systems 301–312), and `SpawnNpcs` (World.Systems 12–35).

## Concrete API and transaction responsibilities

- `ActorStatProfileDefinition` owns immutable base values, growth, flat modifiers and Class/Unassigned
  kind. `Version` hashes the complete canonical definition. `Calculate(level)` computes effective
  unequipped values without mutating current resources.
- `LevelProgressionDefinition` owns a bounded, ordered sequence of positive XP thresholds. The last
  level is one greater than the number of thresholds. An empty sequence is a legitimate one-level
  capped curve. Thresholds are values at particular levels; they are not sorted or deduplicated.
- `ActorProgressionState` binds actor identity and revision to exact profile and curve definitions.
  It stores level, residual experience and current HP/MP. Effective statistics are computed from
  the definitions, never accepted as a second independently editable cache.
- `CurrencyDefinition` pins a denomination and whole-unit name. `CurrencyAccount` identifies either
  exactly one actor owner or a non-actor reserve. `CurrencyLedger` requires unique account/actor
  identities and checked equality of all balances to its admitted supply.
- `ProgressionSnapshot` binds the component to world/branch, includes complete definition tables,
  and requires exactly one account for every admitted actor and no other actor accounts. Its
  `ValidateActorSet` proves equality with a caller-supplied local actor set. The world still checks
  cross-component identity roles and admission history.
- `ActorRewardDefinition` pins XP, exact currency definition, reserve identity and currency amount.
  `ActorRewardPlanner.Plan(state, actorId, expectedActorRevision, reward)` accepts this definition
  only after the caller has selected and authorized it. The API is not a client-authorized grant.
- `ActorRewardPlan` returns success or a typed rejection, the immutable candidate, and crossed levels.
  Unavailable funds, unknown actor/reserve, wrong account role or currency pin leave the original
  state intact. Stale actor revision and numeric/revision exhaustion also leave it intact. No broad
  exception handler converts IO or unexpected failures into a gameplay outcome.

A successful award increases actor progression revision and component revision once. A positive
currency payment also increments the ledger, reserve and beneficiary account revisions once;
other actor and account instances remain unchanged. An XP-only award leaves the currency ledger
unchanged. Even a zero-valued authorized reward advances the actor/component revision as an applied
operation; one-time quest state remains the caller's responsibility.

This planner intentionally permits another award with fresh revisions and sufficient reserve funds.
It does not mistake an arithmetic helper for an exactly-once ledger. Quest completion, current control,
body/range prerequisites, source delivery, history collisions, replay protection and durable receipts
belong to the containing world authority. A preview must not be published as a committed reward.

## Admission and restoration limits

The current component qualifies at most 128 local actors, 32 statistic profiles, 32 curves, 256 currency
accounts and 256 levels per curve. These are explicit bounded component limits, not a maximum global
population or the final game's level policy. XP, currency and revisions use checked unsigned 64-bit
values; current/effective statistics use validated signed 32-bit values. No overflow wraps or silently
discards a reward. A pinned statistic profile that cannot calculate a later level fails that reward
with `CapacityExceeded`; it does not consume funds.

The source save loader can accept residual XP above its next threshold without normalization. New
canonical actor states reject that condition below the cap. Importing such a source save would need
an explicit normalization/migration decision; the decoder never silently repairs it. Cap residual XP
has no next-threshold restriction. HP zero is representable, but these contracts alone implement
neither combat death nor resurrection, and do not modify body/form state.

`ProgressionCodecV1` encodes and strictly decodes snapshots, profiles, curves, currencies, ledgers and
reward definitions. Frames retain the shared OFC framing version, their own payload version one and
typed tags. Restored actors join definition ID **and** content hash. Collections with identity keys
must be in canonical identity order; duplicate identities, missing joins, wrong roles, false balances,
unknown tags/versions/enums, invalid UTF-8, noncanonical order, truncated/oversized fields and trailing
bytes reject with `ProgressionFormatException`. Constructor inputs are defensively copied into
read-only collections. No opaque serialized state bypasses the domain constructors.

| Public decoder | Maximum bytes |
| --- | ---: |
| Snapshot | 524,288 |
| Currency ledger | 131,072 |
| Statistic profile | 2,048 |
| Level curve | 4,096 |
| Currency definition | 1,024 |
| Reward definition | 2,048 |

The encoding is new to the progression component. Existing world profile 1–3 bytes and previously
frozen body, gathering and production encodings are unchanged. Root-owned profile 4 and migration
code provide the world-level compatibility transition; this component does not rewrite old saves.

## Retained verification

The new test sources define 20 facts covering actual source reward arithmetic, exact thresholds,
cap residuals, no-level resource retention, unassigned NPC behavior, reserve conservation, stale
revisions, independent actor/account retention, exact pins, identity joins, bounded collections,
defensive copying and all-or-nothing XP/stat/account/revision overflow handling.

The independent Python stdout oracle is at
`Runtime/tests/OpenFantasy.Core.Tests/Progression/generate_progression_vectors.py`. It constructs
frames and computes source thresholds with Python integer square root, without reading production
encoder code or using production output as expected bytes. The frozen `progression-v1.json` contains
10 positive frames, 29 explicit malformed frames and source thresholds. The tests also exercise all
10,401 truncated prefixes and all six public byte bounds.

The oracle was reproduced byte-for-byte during implementation: 115,897 bytes, SHA-256
`7b863c14e4df6a115117cb8fc7b52f2e459903674011afc1094b3980b8f319d3`.
Regenerate into stdout or a separate temporary file and compare; do not overwrite the expected
fixture as part of verification. The central coordinator owns .NET compilation and execution;
writing these tests and reproducing Python bytes is not a claim that the .NET suite has passed.
