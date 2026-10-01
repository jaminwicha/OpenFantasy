# World markets and learned prices

World profile 33 is an explicit complete-world interpretation. It adds local markets, actor-owned
price knowledge and sellers' explicit lot disclosures to the [paid world](world-paid-exchange.md)
(profile 32). A market quote is an ordinary paid quote. Its terms are derived from a versioned integer
rule and the merchant's actual usable stock. Settlement, supersession, withdrawal, expiry, currency
causality, custody evidence, cuts and Host publication all stay with the paid owner. Earlier profiles
keep their bytes, codecs and refusals.

## Selection and ownership

[`WorldMarketAdmission.CreateFresh`](../../src/OpenFantasy.Core/Worlds/WorldMarketAdmission.cs) composes
an unplayed profile-32 genesis with authored [`MarketBinding`](../../src/OpenFantasy.Core/Markets/MarketBinding.cs)
values. "Unplayed" means revision and step zero, an empty paid book and empty proposal and consent
histories. It recomputes the admission version and resource-history basis, and changes no inventory or
progression. It never upgrades a lived world. No transition into profile 33 exists.

| Fact | Owner |
|---|---|
| Market bindings (merchant, storage, account, commodity pin, home-place identity, rule) | `WorldMarketBook.Markets`, immutable for the world |
| Market basis of each market quote (market, rule version, stock count) | `WorldMarketBook.Quotes` (`MarketQuoteRecord`) |
| Actors' dated price memories | `WorldMarketBook.Knowledge` (`MarketPriceObservation`) |
| Sellers' explicit showings of one lot to one market | `WorldMarketBook.Disclosures` ([`MarketLotDisclosure`](../../src/OpenFantasy.Core/Markets/MarketLotDisclosure.cs)) |
| Quote terms, allowance, lifecycle, issuer fill revisions | `WorldPaidExchangeBook`, unchanged |
| Raw/produced stock, reservations, protection, capacity | Inventory and production, unchanged |
| Balances and finite supply | `ProgressionSnapshot.Currency`, unchanged |

The market owner holds no stock, money or quote terms. `MarketBinding.CountUsableStock` counts the
commodity's unreserved, unprotected raw and produced lots in the merchant's storage. This count is a
checked derived witness over the existing owners, never a second quantity ledger. It saturates one unit
above `MarketPricePolicy.MaximumStock`, where the rule posts no terms.

## Mobile actor-markets and locality

A market is a mobile actor-market: it trades wherever its merchant actually stands.
`MarketBinding.HomePlaceId` is the merchant's admitted home-place identity, an origin pin that
admission checks against the merchant's presence. Presence keeps that identity when the merchant walks,
so it proves neither the merchant's current location nor containment in a stall. Trading-distance checks
use actual geometry. [`WorldMarketLocality`](../../src/OpenFantasy.Core/Worlds/WorldMarketLocality.cs) is the
in-person gate. A market is available to an actor only when the merchant is a different actor with
live presence, both can actually reach each other where they stand, and the merchant is alive and able
to act. A fixed venue, a market tax area or a two-place circuit would need its own admitted geometry.

## Price rule

Each custodian and commodity pair has at most one market binding. Admission and decoding reject
competing rules over the same stock pool, which would otherwise allow a trader to buy under one
target and sell under another while restoring that pool. Distinct stores and commodities retain
independent rules; trading between them changes the actual distribution of stock.

[`MarketPricePolicy`](../../src/OpenFantasy.Core/Markets/MarketPricePolicy.cs) cites one immutable
`MarketSourcePrice`: the source item key, its source price and the source file digest. All other
parameters are authored separately: the stocking target `T`, the holding limit `H ≥ T`, the softening
`K`, price bounds, ask markup, bid markdown, an optional per-unit market due with its payee, the
per-quote lot limit and the maximum quote lifetime. The rule's content hash is its `Version`.

    mid(s)       = clamp(floor(source × (T + K) ÷ (s + K)), minimum, maximum)
    ask(s,q)     = mid(s − q) + askMarkup              for 1 ≤ q ≤ min(lot limit, s), s ≤ MaximumStock
    allIn(s,q)   = mid(s + q) − bidMarkdown            for 1 ≤ q ≤ lot limit, s + q ≤ H, result > 0
    bid(s,q)     = allIn(s,q) − unitFee                when that is positive

The buyer of every fill pays the market due to the authored payee. A buyer pays `ask + due` when it
buys from the merchant. When the merchant buys, it pays the seller `bid` and the payee the due, so its
all-in cost is `allIn`. The posted bid is the seller's price; the due never comes out of the ask.

The rule obeys these properties:

- `mid(T)` equals the source price, and `mid` never rises as stock grows.
- `UInt128` intermediates and the checked build make overflow impossible within the admitted bounds
  (1,000,000 price and stock, 1,000 lot). Stock above the bound posts no terms, so no quote record can
  receive an unrepresentable basis.
- A quote is priced at the state its whole allowance would produce.

**Supported guarantee.** Consider any closed sequence of fills against one market whose other state is
unchanged, in any split, order or interleaving, by any number of traders. The merchant receives at
least `askMarkup + bidMarkdown` per unit it sells. Every unit leaving the merchant at stock level `j`
pays at least `mid(j − 1) + askMarkup`. The matching arrival at that level costs at most
`mid(j) − bidMarkdown ≤ mid(j − 1) − bidMarkdown`. Currency is conserved, so every coalition that
excludes the merchant, including cycling traders together with the due's payee, loses at least that
spread. This covers the 1–4 player setting with cooperating NPCs. A due cannot be routed back to a
trader to subsidize a cycle.

**Limits.** The guarantee is about one market at unchanged other state. Profit remains available from
real differences: time, place, risk, two markets' different stock, and other actors' arrivals and
departures. A coalition that includes the merchant's controller only moves money among its members.
A due as large as `allIn` leaves no positive seller price, and the market then posts no bid. Fees remain
valid; an intentional subsidy would need its own explicit, finite owner.

Demand is the authored stocking target and holding limit. This is a declared initial policy, not a
simulation of population consumption. Real orders, needs or contracts are a later extension: each must
enter as an explicit typed cause with its own owner, and none may edit a rule in place.

## Commands

Action codec 18 defines four intents. Only profile-33 advancing steps accept them.
[`WorldMarketResolver`](../../src/OpenFantasy.Core/Worlds/WorldMarketResolver.cs) applies the shared
command contract first: current control, the caller's own presence revision, and an advancing unheld
step with a ready actor.

**`IssueMarketQuoteIntent(marketId, SpotQuote)`** is checked in this order:

1. The caller is the market's merchant and the quote's issuer. Otherwise, including for a missing
   market, the result is `NotAuthorized`.
2. The merchant's end of the quote uses the bound storage and account and the commodity pin. The window
   and allowance are within the rule. Otherwise the result is `Unavailable`. These checks read only the
   quote's own fields.
3. A buy quote pins the counterparty's private lot. Before anything about that lot is read, the
   counterparty must hold a live disclosure of exactly that lot to this market. An unknown, foreign,
   unshown, withdrawn or expired lot refuses alike with `NotAuthorized`.
4. A sell quote may name only one of the merchant's own usable lots of the commodity. Any other lot
   gives `Unavailable`.
5. The unit price equals `ask(s, allowance)` or `bid(s, allowance)` for the actual usable stock `s`.
   The fees are exactly the rule's due, and the payee is not the counterparty. A mismatch gives
   `RevisionConflict`. If the rule posts no such quote (no stock, demand exhausted, or stock beyond the
   rule's domain), the result is `Unavailable`.
6. Every remaining paid issuance rule runs under the same command through `WorldPaidExchangeResolver`.

Issuance records `MarketQuoteRecord(quote, market, rule version, s)`. It reserves nothing, moves
nothing and spends no work.

**`ObserveMarketIntent(marketId, expectedMerchantRevision)`** is the observer's in-person reading:

1. The market must be available under the in-person gate. A missing market and a distant one both
   refuse with `OutOfReach`, so a guessed identity discloses nothing.
2. The merchant's presence revision must match (`RevisionConflict`).
3. The merchant must be alive and able to act (`Unavailable`).

The world derives the single-unit posted terms and records a `MarketPriceObservation`. It holds the
observer, market, merchant source, commodity and rule pins, ask, bid and due, the publishing world
revision and advancing step, and the causing command and batch index. The stock count is never
recorded in the observation, although the posted prices can permit an inference about it.
Observation is a perceptual act: it moves nothing and spends no work. Player and NPC
controllers submit the same command under the same legality, so a merchant AI gains no omniscience.

**`DiscloseMarketLotIntent(marketId, kind, lot, definition, revision, units, account, expiry)`** is
the seller's own in-person showing of one lot to one market:

1. The market must be available to the seller under the in-person gate (`OutOfReach`, as for
   observation), and its merchant must be trading (`Unavailable`).
2. The lot must lie in a storage the seller owns, and the named account must be the seller's own.
   Another actor's lot or account, or a missing lot, refuses alike with `NotAuthorized`.
3. The lot must be usable, of the named definition and of the market's commodity. The shown units must
   not exceed it, and the window must end after the publishing step and within the market's quote
   lifetime (`Unavailable`). The pinned revision must match (`RevisionConflict`).

The world records a `MarketLotDisclosure`. It pins the seller, the market and its merchant, the exact
lot kind, identity, definition and revision, the units shown, the seller's storage, the account the
seller chose to be paid into, the window, and the causing command and batch index. A later showing by
the same seller to the same market replaces the earlier one. Showing sells, reserves and moves
nothing, spends no work and consents to no quote. It is distinct from a gift permission, a quote
acceptance and stock ownership. Only the seller's later acceptance of a buy quote sells the goods.

**`WithdrawMarketLotDisclosureIntent(marketId)`** is the seller's withdrawal of its current showing
to that market. Without one, the result is `Unavailable`. Quotes already issued keep their fixed terms,
and the seller can simply decline them.

A disclosure authorizes a buy quote whose counterparty is the seller and whose issuer is that
merchant, with the exact shown lot, revision, storage and account. Its allowance must not exceed the
shown units, and the quote must close no later than the showing. A disclosure is usable while the
world has advanced fewer steps than its `ExpiresAtStep`. After its lot changes, the shown pins no
longer match the lot. Every failed paid lot check for a disclosed buy quote gives `Unavailable`,
whether the revision, quantity, definition or storage changed, or the lot disappeared. Comparing
allowances cannot reveal its remaining quantity or new location through different refusal reasons.
The merchant learns that the shown pins no longer authorize issuance; preparation still uses only
the recorded showing.

**Bilateral quotes in profile 33.** `IssueSpotQuoteIntent` keeps its profile-32 meaning: an issuer may
sell its own lot at any agreed price. An issuer-buys quote, however, pins the counterparty's private
lot. In profile 33 the world first requires a live disclosure by that counterparty of exactly that lot
to a market of the issuer. Otherwise it refuses with `NotAuthorized` before the paid resolver reads
the lot, so the ordinary bilateral path is no bypass of the privacy boundary. An actor without a market
can still buy bilaterally from a seller that issues a sell quote of its own lot. Profile 32 keeps its
explicitly limited interpretation, in which the issuance receipt reveals whether a pinned
counterparty lot matches.

**Acceptance.** `AcceptSpotQuoteIntent` keeps every paid check, including mutual reach, both bodies and
the issuer's presence revision. For a quote with a market record, the world then requires the market
state that priced it, evaluated before settlement. That state is the same usable stock count, the same
rule version and the merchant's live presence. A stale fixed offer refuses `RevisionConflict` with
every owner unchanged. The receipt and passive time still publish, as for any refusal. Terms are never
rewritten.

Usable stock is a settlement condition, not a revision. An open quote that was refused while the count
differed becomes settleable again, at its exact terms, when non-trade movements such as gifts restore
the same count. A closed quote never reopens: a fill, withdrawal, supersession or expiry is final, and
every fill advances the issuer's fill revision exactly once. A stale quote is closed by its issuer's
withdrawal, by its policy-bounded window, or by a supersession when the same issuer fills another quote.

## Repricing and stock changes

Prices change only when usable stock changes through real operations, each counted once by the owner
it affects:

- arrival: a purchase fill, a saved-proposal gift settlement, gathering or crafting output;
- removal: a sale fill, a gift away, consumption or use.

Reads, UI polling, dictionary order and wall time cannot change a price, because the rule is a pure
function of the rule and the stock count. Rules are immutable per world. A future rule change must be
an explicit event with a typed cause that also stales the affected open quotes.

`SpotQuote.MarketRevision` keeps its paid meaning, the issuer's fill revision, and is not a price
counter.

## Knowledge

A world holds at most one observation per actor and market: four markets times the 64-actor batch
bound. That bound always fits the admitted roster, so knowledge is never evicted. A later in-person
observation replaces the same actor's earlier memory of that market. The replaced fact remains in the
journal and in archived roots, and a refused observation changes nothing.

`SqliteWorldAuthority.ReadMarketKnowledge(actor, delivery)` authorizes the delivery against the
actor's current control before reading anything. It returns only that actor's observations.
`MarketTradingService.ReadKnowledge` projects them as bounded `MarketKnowledgeEntry` values, aged
against the current step. Reading never refreshes an observation or reveals a remote change.

Disclosures follow the same bound: at most one per seller and market. Expired showings stay inert until
their seller replaces or withdraws them, so the bound is never met by eviction.

## Cuts and durability

The coordinated anchor captures a `WorldMarketContinuationContext`. It contains the exact bindings, the
records of quotes still open, all knowledge and all disclosures, each with its original cause. Its hash
is appended to the anchor only when markets are present, so every older anchor keeps its bytes. A
successor structurally must:

- keep the bindings exact and every carried record exact;
- keep each carried observation, or replace it only with a later observation;
- keep each remaining carried disclosure exact, or replace it only with a later showing;
- date every other observation and disclosure after the boundary.

Structural continuation permits a disclosure's absence because its seller can withdraw it. Durable
journal replay authenticates that absence through an actual seller withdrawal; a structurally valid
successor alone does not prove the cause of removal.

Records of quotes closed in a segment are archived in that segment's closing root. The world outcome
bound already accounts for one record per open or closed quote, so market quote records add no cut
reservation.

Snapshot codec V33 appends the market frame (`world.market-book.v1`: bindings, records, knowledge and
disclosures) to the paid coordination frame. Step codec V33 uses the shared frame. Schema eight,
journal accounting, recovery and material-care archives name profile 33 explicitly.
`SqliteWorldAuthority.SubmitMarketCommand` is the trusted single-command submission, with an exact
retry. Paid and proposal submissions accept profile 33.

## Structural admission

`WorldMarketAdmission.Validate` checks the market owner's internal consistency, not delivered history.
It checks the following:

- Every binding names an admitted merchant with presence and the same home-place identity, its own
  storage and account, and an existing fee payee.
- Every quote record names a quote of its own segment whose fixed terms are exactly the rule's. That
  means the unit price at the recorded basis and allowance, the window, and the due and its payee, which
  is not the counterparty.
- Every observation names a resident and lies within the world's time.
- Every disclosure names a resident seller's own storage and account, lies within the world's time and
  keeps a window within its market's quote lifetime.

Only the durable journal replay authenticates that the commands were delivered.

## Application service and content

[`MarketTradingService`](../../src/OpenFantasy.Hosting/Simulation/MarketTradingService.cs) prepares
commands and projections. Preparation returns null for unauthorized or undisclosed subjects.
Observation and showing additionally require the in-person gate:

- `PrepareOffer`: the merchant's rule-priced offer, for its controller only. A sell offer pins one of
  the merchant's own usable lots. A buy offer pins only what the counterparty showed to this market,
  within the shown units and window. Its prepared expiry is capped by the showing's expiry; this
  does not rewrite an issued quote or accept different terms on the seller's behalf. If the rule's
  fee payee is the counterparty's quoted account, preparation returns null for either trade direction.
- `PrepareObservation`: an observer's reading of a market that is available under the in-person gate.
  It pins the merchant presence revision the observer can see and supplies no price. A distant or
  guessed market reveals no revision.
- `PrepareDisclosure` and `PrepareDisclosureWithdrawal`: the seller's own showing of one of its own
  usable lots, paid into its own actor account, and its withdrawal.
- `ReadKnowledge`: the holder's own aged memories.
- `ReadDisclosures`: the live showings addressed to a merchant the caller controls.
- `ReadQuotes` and `FindRecordedQuote`: the caller's own quotes as participant-safe views, including an
  archived outcome through authenticated replay (see [scheduled trading](scheduled-trading.md#participant-quote-view)).

The world recomputes and authorizes everything the service prepares. The service never selects
another actor's lot or delivery and never lists another actor's inventory. Offer and showing preparation
also accept an already selected root; the [scheduled merchant and shopper controllers](scheduled-trading.md)
use that same path at a session cutoff, so pricing, lot pins and showing limits have one implementation.

[`MarketCircuitPolicies`](../../src/OpenFantasy.Hosting/Content/MarketCircuitPolicies.cs) cites the
retained legacy item prices (berries 5, rations 25) by key and file digest and authors two rules.
`BindResident` binds a rule to an admitted resident's own storage, account and home-place identity.
Binding admits no stock or money. The Marla and Kegga source records remain candidates for the later
content binding. Their identities, shop lists and prices are unchanged.

## Boundaries

- [Scheduled trading](scheduled-trading.md) binds transient inquiries, configured host merchants and
  host shoppers with authored stock targets to session cutoffs. A host shopper consults only the in-person
  gate above, never merchant stock or funds, and reconsiders an unchanged desire at a bounded interval.
  Saved gifts and ordinary care bind in the same profile-33 session. No native market interface, demand
  simulation, caravan route, two-town circuit, fixed-venue geometry or migration exists.
- Participants look up their own live and archived quote outcomes through participant views; the market
  record's stock basis is never part of such a view.
- A sell quote's posted price reveals what the merchant chooses to post and can permit stock inference.
  Actor projections expose no stock-count field, balances, other actors' memories or unshown inventory.
  A showing reveals its own pins; a failed issuance under those pins gives no distinct quantity or
  location failure reason.
