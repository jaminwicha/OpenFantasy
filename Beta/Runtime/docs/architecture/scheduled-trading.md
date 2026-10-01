# Scheduled trading

Scheduled trading binds the [local-market world](world-markets.md) (profile 33) to the
[local session](runtime-and-authority.md#runtime-bindings-action-lifecycle). A shopper asks a merchant for
terms; the merchant's own controller answers with an ordinary market quote at a session cutoff; the shopper
separately inspects and accepts or ignores it. Players and configured host actors use the same quote,
showing and acceptance commands, and the world recomputes and authorizes every one of them. The binding adds
no world profile, codec, durable request table or Core rule. An unconfigured session selects no autonomous
trading action. In paid profiles 32 and 33, a valid pending paid acceptance reserves both participants'
opportunities and excludes their movement (see Acceptances).

[`LocalProfileBindings`](../../src/OpenFantasy.Hosting/Simulation/LocalProfileBindings.cs) also binds
[saved proposals](material-proposals.md#local-session-selection), [ordinary care](ordinary-care.md) and
[excavation](excavation.md) in these interpretations. Each action retains its own admission, consent and
timing rules. Paid and market commands require an unheld exploration boundary; the session also rechecks
queued actions against the boundary selected at the cutoff.

## State ownership

| Fact | Owner | Lifetime |
|---|---|---|
| Inquiries (market, commodity, direction, units, own shown lot) | `LocalTradingController` inside one `LocalWorldSession` | Session memory; bounded, cancellable, lost on restart |
| Merchant and shopper preferences | `LocalTradingConfiguration`, copied by the session | Immutable Host advice, resupplied on reopening |
| Quotes, fills, supersession, withdrawal, expiry | `WorldPaidExchangeBook` | Durable world history, unchanged |
| Market basis, price memories, showings | `WorldMarketBook` | Durable world history, unchanged |
| Goods, balances | Inventory, production, `ProgressionSnapshot.Currency` | Unchanged |

An inquiry is application input, not a world fact. It reserves, promises and publishes nothing and binds no
merchant. Only the merchant's issued quote, the shopper's showing and the acceptance are world commands, each
with its own identity and receipt. A durable request would need its own gameplay contract (restart, retained
promise, expiry semantics) and an explicit profile; none is implied here.

## Application interfaces

[`LocalWorldSession`](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.Trading.cs) exposes:

- `SubmitTradeInquiryAsync(LocalTradeInquiry)`: the player's own inquiry. The session's actor is always the
  shopper; no other actor's inquiry can be submitted through it. A
  [`LocalTradeInquiry`](../../src/OpenFantasy.Hosting/Simulation/LocalTradeInquiry.cs) names the market, its
  commodity, `Buy` or `Sell`, the units and, only to sell, the shopper's own lot that it has already shown to
  that market. It never names a merchant lot, delivery or price. One inquiry may wait: identical terms share
  it, different terms are refused. Admission checks only the world interpretation (a market world), never the
  named market, so admission reveals nothing.
- `CancelTradeInquiryAsync()`: serialized with cutoffs, so a cancelled inquiry is never answered afterwards.
  A quote already issued keeps its world lifecycle; the player may still ignore it.
- `ReadTradeInquiry()`: the current or last inquiry as a `LocalTradeInquiryView`, a pure in-memory read.
- `ReadView().PaidQuotes`: the player's open quotes and current-segment outcomes as
  [`PaidQuoteView`](../../src/OpenFantasy.Hosting/Simulation/PaidQuoteView.cs) values.
- `InspectPaidQuoteAsync(quoteId)`: one quote by identity, including an archived outcome (see below).

Constructing a session with a [`LocalTradingConfiguration`](../../src/OpenFantasy.Hosting/Simulation/LocalTradingPreferences.cs)
opts host actors in; a separate public constructor accepts explicit NPC gift preferences and trading together.
A `LocalMerchantPreference` lets a host merchant answer inquiries at the markets it runs.
A `LocalShopperPreference` gives a host actor a bounded authored stock target for one market's commodity and
its reconsideration interval. The configuration also bounds every inquiry's lifetime (default
`LocalWorldSession.TradeInquiryLifetimeSteps`, 32 advancing steps). Admission refuses preferences for
non-host, uncontrolled or non-merchant actors, duplicates, a shopper's own market or a world without markets.

[`MarketTradingService`](../../src/OpenFantasy.Hosting/Simulation/MarketTradingService.cs) adds `ReadQuotes`
and `FindRecordedQuote` for any controller with its own delivery. Its offer and showing preparation also
accept an already selected root, which the scheduled controller uses, so cutoff decisions and direct calls
share one pricing and pinning path.

## Participant quote view

A `PaidQuoteView` is the participant-safe projection of a quote. It is produced only for the issuer or the
counterparty after current control of that actor is proven; unknown and foreign quotes both give nothing. It
carries the quote identity, parties and side, the exact fixed goods, storage, account, currency, price, fee
and window terms, the quote hash an acceptance must restate, the lifecycle status and, after a fill, the
agreed quantity and totals. It never carries:

- the issuer's principal or session (a `SpotQuote` embeds them, so it is not itself a participant DTO);
- an acceptance's or withdrawal's delivery, or the fill that superseded the quote;
- the market's stock basis or the issuer's fill counter;
- another inventory, balance or the world root.

The issuer's presence revision is present only for the counterparty of an open quote that can actually reach
the issuer where both stand. A distant issuer's current revision stays unknown. `PrepareAcceptance` builds
the counterparty's exact `AcceptSpotQuoteIntent` from the inspected terms and that observed revision.
Preparing submits nothing: confirmation is the separate submission of that intent under the counterparty's
own command identity, distinct from issuance and from settlement. A stale or changed quote is refused by
the world; nothing silently requotes or retargets.

## Cutoff selection and arbitration

At each ready, unheld advancing exploration cutoff the session selects, in order:

1. the player's pending action; when it is a valid paid acceptance, its issuer is reserved;
2. saved-proposal reservations and NPC gift decisions, live in the market world because profiles 30, 32 and
   33 bind them alike;
3. trading: expiry of waiting inquiries, configured shoppers' acceptances, showings and new inquiries, then
   configured merchants' answers;
4. ordinary NPC work (construction), then helper aid.

A reserved or acting actor is excluded from every later step. Trading precedes ordinary work, so a merchant
answering or settling at this cutoff does not work in it and returns to its work at a later cutoff. A player's
valid acceptance of a merchant's saved gift reserves that merchant as sender, and a merchant's own gift choice
spends its opportunity; in both cases its answer waits and the inquiry keeps its place.

At ordinary, care, lifecycle and encounter cutoffs, the session refuses a queued player command that the batch kind cannot
carry (`RefuseUnrepresentable`): the request receives an ordinary refusal, nothing is published for it and the
session keeps running. Paid and market commands are already refused at submission outside an advancing,
unheld exploration boundary (reading hold, retained consequence, care obligation, encounter or non-exploration
lifecycle phase), so a held batch is never asked to carry one.

**Acceptances.** A paid acceptance settles only when its issuer submits nothing in that batch and neither
party has already worked. Complete authority preview of the acceptance alone is advice, not a grant: a valid
acceptance reserves the issuer, and both participants receive no work, answer or movement sample in the
settling step. An invalid, stale or foreign acceptance reserves nobody, so a conflicting NPC choice never
rejects an otherwise valid player action and an invalid request never suppresses useful work. The final
batch still decides the only outcome.

**Merchant answers.** For each waiting inquiry the controller first applies the in-person gate
(`WorldMarketLocality.FindAvailable`): a missing, distant, absent, unable or unconfigured market gives one
uniform `Unavailable`, and nothing else is read. A locally available merchant then offers nothing
(`Declined`) when the commodity differs, the units exceed the rule's lot, it already has an open quote to
this shopper, the rule posts no such terms, or:

- to sell, none of its own usable lots covers the units (it chooses the lowest-identity raw, then produced, lot);
- to buy, the shopper has no live showing of exactly the named lot to this market within the shown units and
  window, or its own market account cannot pay the bid plus the market due.

The merchant reads only its own stock, funds and open quotes and the showings addressed to it. A shopper's
private price memory is never evidence of an inquiry, and no bag is scanned. Otherwise it issues an
`IssueMarketQuoteIntent` under its own host control. The inquiry becomes `Offered` with the quote identity
only when the committed receipt accepts the issuance; a refused issuance is a decline.

**Host shoppers.** A configured shopper acts only in person and under its own control. With an open quote
from its market's merchant it accepts the whole allowance once when the trade moves its usable holding toward
its target and, to buy, it can pay; otherwise it ignores the quote until it closes. Below its target it asks
to buy; above it, it first shows one of its own usable lots (a world command that spends no merchant
opportunity), then asks to sell the shown units. It never selects another actor's lot or delivery.

**Reconsideration.** A shopper decides only while `WorldMarketLocality.FindAvailable` says its market is
available to it where it stands: the merchant is present, mutually reachable, alive and able to act. That is
what a person standing there perceives; the shopper never reads the merchant's stock or funds, and a distant
price memory is never refreshed. A remote shopper therefore asks nothing and spends nothing. Decisions are
grouped into attempts. An attempt shows at most once and asks at most once. A new attempt starts when the
shopper's own holding or balance changed, or when `LocalShopperPreference.ReconsiderAfterSteps` advancing
steps passed since the last attempt began, and never while its inquiry waits or an offer to it is open. A
shopper silenced by a remote, declined, expired, refused or unaccepted outcome thus resumes after a real
arrival, a merchant's legitimate stock or funding change, or a closed quote. For unchanged own circumstances,
renewed attempts wait for that interval.

**Decisions, retries and confirmations.** Identities
([`LocalTradingIdentity`](../../src/OpenFantasy.Hosting/Simulation/LocalTradingIdentity.cs)) separate three
things. An exact retry resubmits the same command identity and bytes and only returns the original receipt.
A decision is selected once: one answer per inquiry, one acceptance per quote, one showing and one inquiry
per attempt, so a refused or colliding decision is never retried under a new identity. A new NPC decision is
a new attempt, whose identities derive from the shopper's circumstances and the attempt's starting step; it
concerns new terms (a new quote), never the refused one. A player's confirmation is always the player's own
separate submission; nothing confirms, renews or requotes for the player. A merchant keeps at most one open
quote per shopper, so the shared quote book cannot be flooded by repeated inquiries. Busy and refused
processing is ordinary state, never a session fault.

The showing identity belongs to the attempt. If separately consented transfers replace one owned lot with
another while the shopper's total holding and balance stay unchanged, the replacement waits for the next
attempt before being shown. Selecting different goods cannot increase the number of showings in that attempt.

## Service and fairness policy

Waiting inquiries are answered first in, first out by admission sequence. A merchant answers at most one
inquiry per cutoff and none while its opportunity is already taken; a skipped inquiry keeps its place, so no
later inquiry to the same merchant overtakes it. A missing, distant or unconfigured market closes the
inquiry at once as `Unavailable`, and a declined inquiry spends no merchant opportunity, so the next inquiry
to that merchant may be answered at the same cutoff.

A waiting inquiry to an available merchant is answered or declined at the first trading cutoff at which the
merchant is free. The merchant is not free at a cutoff when it:

- answered an earlier waiting inquiry with an offer at that cutoff;
- is the issuer reserved by the player's valid pending paid acceptance, or by a host shopper's valid acceptance;
- is the sender reserved by the player's valid acceptance of its saved gift, or selected its own gift choice;
- acted as a configured host shopper itself (a showing or an acceptance at another market).

An inquiry is also not answered when the batch with NPC actions fails its preview and the fallback batch omits
every NPC action. Every such skipped advancing cutoff consumes inquiry lifetime; held, care, encounter,
lifecycle and paused cutoffs publish no advancing step and consume none. The inquiry expires instead once the
world's advancing steps reach its `ExpiresAtStep`. Host shoppers decide in actor-identity order; that only
orders requests created at the same cutoff.

Inside a committed batch the world still executes actions in canonical actor-identity order. That order is a
determinism contract for settlement, not a fairness guarantee. The host never submits two acceptances against
one issuer in one batch, because the first valid acceptance reserves the issuer. This is a local single-host
policy over transient input; it is not network fairness, priority, wages or queue persistence.

## Pause, hold, time and restart

Inquiry lifetime is fixed-world time in advancing steps, bounded by the configuration. A paused session
selects no cutoff, so a waiting inquiry is neither answered nor expired. Reading holds, care, encounters and
pending consequences publish no trading answer; inquiries keep their place and expire against the world's
steps when trading resumes. A nonbinding inquiry may be submitted, wait and be cancelled during a hold; this
differs from actor commands, which are refused there. Cancellation is serialized with cutoffs, so a cancelled
inquiry is never answered afterwards. The session discards inquiries and host shopper attempts on stop. A
reopened session starts with none, so the first attempt after reopening is a new decision with new identities,
while issued quotes, showings and fills keep their durable world lifecycle and can still be accepted, ignored
or inspected; an acceptance refused before the restart stays decided.

## Historical lookup

`InspectPaidQuoteAsync` runs in the session's bounded background history slot, shared in mechanism with
saved-proposal inspection through [`LocalInspectionSlot`](../../src/OpenFantasy.Hosting/Simulation/LocalInspectionSlot.cs):
one lookup per subject kind may be outstanding, an identical request shares it and a different one is
refused, nothing is cached or retried, and stopping finishes or cancels the read before storage is released.
It never runs on a client frame or a tick.

`SqliteWorldAuthority.ReadRecordedPaidQuoteForController` authorizes the delivery against current control
before reading anything. A quote still in the live book is projected directly. Otherwise, when an archive
exists, one complete verified journal replay inspects every retrieved closing root through the existing
material-and-care archive replay, which now also selects one paid outcome. There is no second archive owner
or index. The result must match the pinned live head, and only an outcome the actor took part in reaches the
projection. Damaged storage refuses and requires reopening.

## Extension boundaries

- No native trading menu exists; the views and session calls are its intended boundary.
- Demand is the authored stock target; there is no consumption simulation, cognition, route, wage or escort
  contract, and no offscreen commerce. Host shoppers do not walk to markets.
- Fairness beyond this local first-in, first-out policy, multiplayer delivery and network authentication
  need their own admitted contracts.
- A durable inquiry or standing order would need explicit world state, a profile and history semantics.
