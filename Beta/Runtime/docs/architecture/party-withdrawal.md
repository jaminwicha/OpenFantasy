# Party withdrawal commitments

## Component boundary

[EncounterWithdrawalParty](../../src/OpenFantasy.Core/Encounters/Withdrawal/EncounterWithdrawalParty.cs)
is a standalone deterministic component for one declared encounter side. It retains the complete
required actor set and independent route commitments. It does not change the running world's
[retreat law](encounter-retreat.md), admit commands, move bodies, advance time or end encounters.
The complete world must compose and admit this component before it can become gameplay authority.

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

The existing `encounter.retreat.v1` codec and this codec reject each other's frames. This component
has no implicit root-codec dispatch, save upgrade or player-protocol binding. Its decodability cannot
be used as evidence that an old save accepts a new retreat rule.

## Complete-world integration requirements

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

These obligations are not supplied by the standalone component. The unchanged single-combatant
retreat binding continues to refuse a party with `AllyRemains`.
