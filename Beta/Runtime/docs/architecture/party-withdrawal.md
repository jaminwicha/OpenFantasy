# Party withdrawal

## Component boundary

[EncounterWithdrawalParty](../../src/OpenFantasy.Core/Encounters/Withdrawal/EncounterWithdrawalParty.cs)
is a deterministic component for one declared encounter side. It retains the complete required actor
set and independent route commitments. The component itself admits no commands, moves no bodies and
ends no encounter. The complete-world binding below interprets it under explicit profile41. Legacy
profiles retain their [single-combatant retreat law](encounter-retreat.md).

Required membership and committed choices are different facts. A required actor without a choice
still belongs to the side. Ending an interrupted or cancelled choice removes only that choice.
`AllArrived` means every declared member has a retained choice whose complete route is recorded;
it does not prove that the roster is the world's actual side, that anyone can act, that contact is
broken, or that the episode may end. Downed members must not disappear from the required set.

## Ownership and operations

| Fact | Owner |
| --- | --- |
| World, branch and episode identities; declared side and complete required actor set | `EncounterWithdrawalParty` |
| Exact command, original principal/session/control witness, captured route/origin/speed and walked sample count | [EncounterWithdrawalMember](../../src/OpenFantasy.Core/Encounters/Withdrawal/EncounterWithdrawalMember.cs) |
| Actual feet, body condition, occupancy and ground clearance | The world's spatial/body owners |
| Actual readiness debt, required decisions and tactical time | The encounter timing owner |
| Command authentication, side membership, terminal records, receipts and coupled publication | The complete-world authority |

`RecordAccepted` retains one unwalked choice for a declared actor. It cannot replace an existing
choice or choose for another actor. `RecordMoved` requires the exact party revision, actor, command,
current prefix endpoint and next 32-subunit cardinal endpoint. It records one sample. `RecordEnded`
requires the same revision/command binding and preserves required membership. Each operation returns
a new immutable component; caller collections cannot mutate its membership or choices.

The component's revision counts its own changes, not calendar time or global commits. Restoration
requires enough revisions for all retained acceptances and samples. This is a structural lower bound,
not proof of historical publication. A cancelled choice's identity, reason and any charged failed
sample belong in the world's terminal record and journal; removing it from this component is not
permission to omit that evidence, refund debt or reuse the command identity.

## Spatial and timing contracts

Each choice retains an immutable origin and route of 1–32 samples using
`QueueEncounterRetreatIntent`. Expected feet are derived from the walked prefix; the spatial owner
remains the sole authority for actual position. The retained normalized speed fixes the sample cost
under the existing retreat-v2 tariff: `ceil(10000 × 100 / (800 × speed))`. Walked readiness is derived
from the sample count and that cost; it is evidence about this choice, not a second readiness ledger.

The world must publish a walked sample and its timing charge atomically. It must reject a mismatch
between the retained speed and the admitted timing interpretation rather than silently recomputing
already recorded cost. A refused attempted sample must still charge its due boundary according to
the movement law; the component alone cannot perform that transaction. Finishing a route does not
remove debt, change exposure carry or grant exploration movement during the encounter.

The component makes no scheduling choice for a member who has arrived. It neither supplies free
Wait actions nor bypasses Full Wait. An integration must specify how arrived members make subsequent
choices, how a changed choice cancels withdrawal, and how current controller authority is respected.

## Canonical representation

[EncounterWithdrawalCodecV1](../../src/OpenFantasy.Core/Encounters/Withdrawal/EncounterWithdrawalCodecV1.cs)
encodes `encounter.withdrawal-party.v1`: format version, explicit component rules pin, world/branch/
episode, side, component revision, required identities and member frames. Member frames retain the
exact canonical command, origin, normalized speed and sample count. Collections encode in actor
identity order. Decode re-encodes and compares the complete bytes, rejecting alternate order,
unknown rules, malformed counts, inconsistent origins/progress, mixed scopes and trailing data.

The actor bound is the timing component's 128 actors; routes retain the existing 32-sample bound.
The component frame is bounded at 200 KiB. These are representation limits, not proof of a supported
party size, complete-world envelope capacity, frame time or production workload. Multiple side
components must share the world's complete actor bound and its unchanged total byte budget.

The existing `encounter.retreat.v1` codec and this component codec reject each other's frames. The
component has no implicit save upgrade or player-protocol binding. Its decodability cannot be used
as evidence that an old save accepts a new retreat rule.

The [saved-interpretation contract](persistence-and-history.md#interpretation-and-admission-identity)
separates original admission lineage from rule selection. Resource history and archive anchors keep
their original identities across a lawful transition. A complete binding must version the new law,
preserve old bytes and authenticate its explicit transition; creating fresh resource history over an
earned world is not continuity. Component size must also fit the root's reserved capacity.

## Complete-world authority

[WorldPartyWithdrawalAdmission](../../src/OpenFantasy.Core/Worlds/WorldPartyWithdrawalAdmission.cs)
binds profile41 to the full effective-control world. Fresh selection requires an unplayed profile40
genesis. It grants no material, health or control. This factory is not a lived-world transition.
Normal state updates cannot add or remove the collective interpretation. The legacy active route
cannot coexist with the collective owner; legacy counters and terminal evidence retain their meaning.

[EncounterWithdrawalState](../../src/OpenFantasy.Core/Encounters/Withdrawal/EncounterWithdrawalState.cs)
holds at most two side books, sharing the timing owner's128-actor limit. Each book's required roster
must equal its actual complete positive side, including incapacitated people. A held member must
agree with actual feet, captured timing speed, accepted command scope and the queued or walked
stage. The active episode must have exactly two positive sides. A third-party encounter requires
another participant-exit law; withdrawal cannot end an unrelated fight.

[WorldEncounterActionProcessor](../../src/OpenFantasy.Core/Worlds/WorldEncounterActionProcessor.cs)
checks each submitter's current authority, actor revision, episode, timing revision and due boundary.
A new retreat captures that actor's route and origin under its own command. Other members are neither
moved nor committed by this choice. The initial timing decision pins the exact command; later walked
samples use the retained standing choice. Every due undecided human still holds Full Wait. Opposing
actors resolve their ordinary abilities and costs between samples.

[WorldPartyWithdrawalResolver](../../src/OpenFantasy.Core/Worlds/WorldPartyWithdrawalResolver.cs)
resolves only the first capable due actor. It revalidates accepted command attribution, locomotion
and the next ordinary32-subunit movement against current occupancy. The proposed body, route progress
and readiness charge must pass complete-root admission together. A blocked or unadmitted attempt
moves nobody, spends one sample's readiness, ends only that choice and retains required membership.
Routes reserve no future ground and cannot swap bodies or teleport through an earlier arrival.

An arrived actor remains in the episode and no longer holds a standing movement decision. When due,
that actor must supply an ordinary choice. Wait retains the arrival commitment and incurs its normal
cost on resolution; an accepted ability ends the commitment. A replacement route requires explicit
cancellation first. There is no automatic guard, free Wait or arrival-based readiness refund.

[CancelEncounterWithdrawalIntent](../../src/OpenFantasy.Core/Encounters/Withdrawal/CancelEncounterWithdrawalIntent.cs)
names the exact retained command and current timing revision. Its authenticated decision boundary
may end walking between due samples without changing position, calendar or paid debt. It consumes
no new tactical action and grants none: the actor remains unable to act until its debt expires.
Cancellation removes the initial queued route choice when present. It cannot cancel an arrived
actor's independently accepted Wait. Multiple cancellations in one boundary retain all ended commands.

[WorldPartyWithdrawalChoices](../../src/OpenFantasy.Core/Worlds/WorldPartyWithdrawalChoices.cs)
also ends routes interrupted by actual incapacity or an actual encounter outcome. Incapacity removes
the choice, never the person from the required roster. A genuine outcome clears incompatible books
and retains terminal evidence; it is not a successful escape. Rescue, carrying and surrender need
their own admitted actions and cannot be inferred from a missing withdrawal choice.

Departure occurs only when every required member of one side has completed a chosen route, remains
capable and is actually clear of every living opponent. The single terminal transaction ends the
episode, retains all actors' charged readiness debts and exposure carry, and preserves position,
HP/MP, custody and resource history. The other side's retained routes end explicitly; any other
cancelled timing decisions remain identifiable. Departure creates no victory or reward.

The original-owner return boundary consults only an uncompleted walking obligation. Arrival
bookkeeping does not keep delegated control forever. Retained commands and terminal evidence keep
the original principal/session/control witness; decoding that witness does not prove its historical
authorization. Historical replay and authenticated host selection remain distinct obligations.

## Complete-root representation and adapter boundary

[EncounterRetreatCodecV2](../../src/OpenFantasy.Core/Encounters/Withdrawal/EncounterRetreatCodecV2.cs)
encodes `encounter.retreat.v2`, with the collective rules pin, exact legacy evidence as a nested V1
frame, state revision, ordered side books and the latest terminal boundary. Terminal rows retain
the exact ended member and any charged failed sample; earlier rows remain in world history. The
owner is bounded at512KiB and participates in the unchanged16MiB complete-root reservation. The
V1 codec refuses collective state rather than silently discarding it.

[WorldSnapshotCodecV41](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV41.cs) selects the new
retreat frame inside the complete root; profiles35–40 keep the old field and4KiB bound. The new
[step codec](../../src/OpenFantasy.Core/Worlds/WorldStepCodecV41.cs) retains inherited action and
control boundaries. [Action codec24](../../src/OpenFantasy.Core/Worlds/WorldActionCodecV24.cs) encodes
cancellation; the existing delegated envelope can retain it, while earlier world profiles refuse
that intent. Standalone command decoding never expands an old world's action permissions.

These are portable Core transactions. SQLite recovery, ordinary adventure admission and connected
client selectors do not select profile41. A lived profile40 save requires the explicit preserved-source
transition described by the persistence contract; a fresh profile41 root cannot replace its history.
Native preparation and observations must expose each actor's own choice, blockers and actual costs
without revealing another participant's private command. Those adapter contracts must be implemented
and qualified before this interpretation becomes an ordinary playable entry.

## Cross-system integration requirements

A world binding must establish all of the following together:

- The scope names its active episode, and required identities exactly equal the full authoritative
  positive-side membership, including incapacitated allies. Bystanders retain their own identities.
- Each command was authenticated for its own actor and accepted once; current and historically
  accepted delegated authority retain their distinct meanings. A restored structural frame is not
  proof of consent. Reusing a previously consumed command remains forbidden by the global ledger.
- At each due boundary, complete-world placement, body, occupancy, speed, membership and contact
  are revalidated. Another route's movement can obstruct this route; advisory geometry cannot
  authorize a teleport, a swap through an occupied body or an omitted sample.
- Every undecided required human still holds Full Wait. Opponents resolve their normal choices and
  costs between movement samples. Cancellation and interruption retain already incurred debt.
- Arrival remains a distinct state from departure. Ending the episode requires the complete side's
  explicit commitments, actual clear contact, accounted-for incapacitated members and a coherent
  terminal transaction. HP, MP, position, material custody, carry and all remaining readiness debts
  survive. Other queued choices retain their identities when ended by departure.
- The save interpretation, recorded transition, network compatibility and own-character UI agree
  on the rule. Existing worlds keep their pinned law unless an explicit verified transition occurs.

The standalone component cannot prove these obligations by itself. The complete-world binding must
be preserved by every adapter and later state writer, including future movement/displacement and
speed-changing effects that can invalidate a route. Legacy profiles continue to refuse a party
with `AllyRemains`; new Core encoding alone is not a migration, connected-play or usability proof.
