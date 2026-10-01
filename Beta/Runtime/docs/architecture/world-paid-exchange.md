# Paid world exchange

World profile 32 is an explicit complete-world interpretation. It adds world-owned paid spot quotes to
the [saved-proposal world](material-proposals.md) (profile 30). The
[atomic spot exchange](spot-exchange.md) supplies the terms and the shared goods and payment
arithmetic. The world supplies authentication and control, time, bodies, reach, work opportunity,
causal history and durable publication. Earlier profiles keep their own bytes and rules.

## Selection and ownership

[`WorldPaidExchangeAdmission.CreateFresh`](../../src/OpenFantasy.Core/Worlds/WorldPaidExchangeAdmission.cs)
selects profile 32 from a fresh ordinary-care genesis through the complete coordinated admission. It
attaches empty consent evidence, an empty proposal book and an empty
[`WorldPaidExchangeBook`](../../src/OpenFantasy.Core/Worlds/WorldPaidExchangeBook.cs). Fresh admission
never upgrades a lived world. No transition into profile 32 exists.

| Fact | Owner |
|---|---|
| Open quotes, segment outcomes and their terminal causes, issuer market revisions | `WorldPaidExchangeBook` in `WorldCoordinatedContinuation.PaidExchange` |
| Raw stock, produced stock, reservations, capacity | Inventory and production, unchanged |
| Balances and finite supply | `ProgressionSnapshot.Currency` |
| Ordering of currency-only causes | The book's fills, read by resource admission |
| Receipts, permanent identities, closed history | Durable command/identity tables, joint archives |

The book holds no balance and no stock.

## Commands

Action codec 17 defines three intents, and only profile-32 advancing steps accept them:
`IssueSpotQuoteIntent(SpotQuote)`, `AcceptSpotQuoteIntent(SpotAcceptance, expectedIssuerRevision)` and
`WithdrawSpotQuoteIntent(quoteId)`. Each is a separate command with its own identity and receipt.
[`WorldPaidExchangeResolver`](../../src/OpenFantasy.Core/Worlds/WorldPaidExchangeResolver.cs) checks the
same things in the same order as the proposal resolver: current control, the caller's own presence
revision, an advancing unheld step with a ready actor, and only then the quote.

**Issuance** checks the following before recording the quote:

- The quote identity derives from the command identity.
- The issuer's principal and session equal the command's.
- Each party owns its own storage and account.
- `IssuedAtStep` is the count this step publishes.
- The expiry is later than that count and at most 65,536 steps after it.
- The market revision equals the issuer's current revision.
- The currency pin and every fee payee exist.
- The lot pin names actual current stock that covers the whole allowance.

Issuance reserves nothing, moves no money and uses no work opportunity. It does not check lot
protection or reservation; such a quote cannot settle and the issuer may withdraw it.

**Acceptance** uses the command identity as the acceptance identity. It checks the following in order:

1. The quote is open and not due, names this counterparty, and the outcome window holds the fill
   together with every supersession it causes.
2. `Binds` holds: exact quote hash, quantity within the allowance, exact payment and fee totals.
3. The issuer's recorded delivery is still its current control.
4. The issuer submitted no action in this batch, and neither party has already worked.
5. The issuer is ready, and its presence revision matches the one the counterparty observed.
6. Both bodies support manipulation and both parties are alive.
7. Each party can reach the other.
8. Each party still owns its storage and account.
9. The market revision and currency pin still match.
10. The buyer has the funds.

Settlement then runs the shared `SpotMaterialTransferStep`: exact lot and revision, unreserved and
unprotected stock, combined capacity, and prefix split with the acceptance's child identity. It pays
through `CurrencyLedger.Transfer` with `SpotQuote.PaymentsFor`. The production revision is unchanged,
because a custody movement is neither a craft nor a use. The standalone `SupportsSpotExchange`/V12
capability is never selected. Settlement uses both participants' work opportunity.

**Withdrawal** is the issuer's own command for one of its open quotes, for example when the pinned stock
became stale or protected or the offer is no longer wanted. After the shared command checks it requires
an open, not yet due quote whose issuer is the submitting actor. A missing, closed, due or foreign quote
gives the same `Unavailable` refusal. Withdrawal pays nothing, moves no goods, advances no market
revision and uses no work opportunity; it only releases the quote's open slot.

A refusal changes no transaction owner: inventory, production, progression and the book stay exact.
The step still publishes its receipt and elapsed time, and passive processes of that step (time, due
expiry, flow and other actors' actions) still advance normally. A failure in later complete validation
rolls back every owner the command touched.

## Lifecycle

Every quote ends in exactly one terminal outcome, which keeps the exact quote and a typed cause:

| Outcome | Cause recorded | Effect |
|---|---|---|
| `Filled` | Settlement: acceptance, its batch position, moved rights | Goods and payment move; issuer revision advances once |
| `Superseded` | The superseding fill's acceptance and quote | None |
| `Withdrawn` | Withdrawal command identity, its delivery and batch position | None |
| `Expired` | None (the fixed-world time boundary) | None |

A fill advances the issuer's market revision, which makes every other open quote by that issuer
unfillable. In the same complete transaction the book therefore closes each such sibling as
`Superseded`, naming the fill as its cause. Supersession moves no money or goods, adds no progression or
currency revision and no second issuer revision. Consequently every open quote always names its issuer's
current market revision, and the book refuses a root that leaves a stale quote open. Quotes expire at
the advancing boundary, under the same rule as proposals. A closed quote is never reopened; its
identity, creation command and terminal command remain occupied permanently.

### Canonical outcome order

Outcomes of one segment follow one strict order, which the runtime produces and the book requires:

1. world sequence;
2. within one sequence, every command-caused outcome before the time boundary's expirations;
3. command-caused outcomes by batch position (the fill's or withdrawal's command ordinal);
4. at one position, the command's own fill before the supersessions it caused, which follow in quote
   identity order;
5. expirations in quote identity order.

Two primary outcomes cannot share one command position. Decoding refuses any other permutation of
supplied bytes rather than normalizing it. Fills keep their actual acceptance order, so currency replay
order is unchanged.

## Limits

| Bound | Value | Meaning |
|---|---|---|
| `WorldPaidExchangeBook.MaximumOpen` | 16 | Open quotes shared by the whole world. A full book refuses the next issuance; it never evicts. |
| `WorldPaidExchangeBook.MaximumOutcomes` | 64 | Outcomes of one segment, until the next coordinated cut archives them. |
| `WorldPaidExchangeBook.MaximumIssuers` | 128 | Never-pruned issuer revisions. |
| `WorldPaidQuote.MaximumLifetimeSteps` | 65,536 | Advancing steps a quote may stay open. |

The cut reservation counts one outcome for every open or newly issued quote, plus one reserved
operation. Since a fill closes at most every open quote, the window always holds a fill and all of its
supersessions; after every published step, open quotes plus outcomes stay below 64.

Issuer revisions are never reset or pruned, because a reset could make old quoted terms valid again. An
issuer must be an actor with admitted presence, and a complete world admits at most 64 immutable
residents and 64 present actors, so the issuer bound cannot be reached today. A future roster
transition that admits more actors must revisit this bound explicitly.

## Currency-only causality

A paid fill adds no actor resource batch. For profile 32,
[`WorldResourceAdmission`](../../src/OpenFantasy.Core/Worlds/WorldResourceAdmission.cs) requires:

    progression revision = basis revision + actor batches + fills

It replays one merged sequence of actor batches and fills, ordered by world revision and then command
ordinal. It refuses a tie. Rewards pay through `ActorRewardPlanner`, and fills pay through the shared
ledger transfer. The resulting ledger must equal the live ledger byte for byte, including account
revisions. Supersessions, withdrawals and expirations are not currency causes. Every other profile has
no fills, so its equation is unchanged.

## Custody evidence

Produced-lot fills are merged with proposal settlements in world order and replayed by
`ProducedCustodyPartition`. A raw fill must name its admitted source and stay within actual issuance.

## Root admission

Structural admission of a root proves a self-consistent segment, not delivered history:

- identities: each quote, creation command, acceptance, withdrawal command and split child is unique;
  a supersession resolves to its fill's acceptance and quote;
- causality: each issuer's revision equals its boundary revision plus its fills; each quote names its
  issuer's revision at issuance; each fill supersedes exactly the issuer's quotes still open at it; an
  issuer submits at most one command per step and none in a step that settles its quote;
- control: fills and withdrawals compare their recorded deliveries with the admitted control bindings.

That last check assumes control bindings and storage/account ownership are immutable, as they are
until an explicit session or roster transition exists. Such a transition must check historical
deliveries against the bindings in force when they acted, or a legitimate segment would become
inadmissible. Only the durable journal replay authenticates that the commands were actually delivered.

## Cuts and durability

The joint anchor carries a `WorldPaidExchangeContinuationContext` with the open quotes and issuer
revisions. Its hash is appended to the anchor only when this capability is present, so older anchors
keep their bytes. A successor must:

- keep every carried quote exact, either open or in exactly one outcome after the boundary;
- date every other quote after the boundary;
- hold issuer revisions equal to the carried revisions plus that segment's fills;
- satisfy the same lifecycle replay from the carried revisions, so a carried quote can only be closed by
  a cause inside the successor segment.

Closed outcomes, including supersessions and withdrawals, remain in the closing root of the archive.
Schema eight keeps its physical layout. Creation, recovery, journal accounting and archive publication
name profile 32 explicitly.

## Host authority

`SqliteWorldAuthority.SubmitPaidExchangeCommand` is the local trusted-delivery submission. Under one lock
it authorizes the independently supplied delivery against the current world before disclosing anything.
An exact retry returns the original receipt without a second effect; otherwise it publishes the
single-command step in one SQL transaction.

`SqliteWorldAuthority.Commit(batch)` is a privileged host path. It takes each command's claimed delivery
at face value, as it does for proposals, and is not a player-facing authorization boundary.

## Boundaries

- Profile 32 has no local or native session binding: `LocalWorldSession` and its controllers select
  profile 30, not 32. There is no native quote interface.
- No lived-world migration into profile 32 exists, and it does not combine with profile 28 pairs.
- Participants cannot yet look up an archived quote outcome by identity, as they can for proposals;
  after a cut, receipts and the archive are the only history.
- An issuer-buys quote pins a lot in the counterparty's storage, and its issuance receipt reveals whether
  that lot still matches. Local visibility rules must validate that disclosure before this is exposed.
- Local pricing, discovery, NPC trading policy and live control transitions are not part of this binding.
