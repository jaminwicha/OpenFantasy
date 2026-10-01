# Native trading

The native market binding connects [scheduled trading](scheduled-trading.md) and the
[local-market world](world-markets.md) to the Godot client. A player opens a merchant's stall from the
contextual wheel, asks for terms, reads the merchant's answer, reviews one exact offer and separately
confirms it. Every world effect is an ordinary command through the existing session; the client adds
no authority, price arithmetic or second inventory.

## Runtime binding: production world and selection

[`NativeTradingWorld`](../../src/OpenFantasy.Hosting/Content/NativeTradingWorld.cs) composes a fresh
market world from the unplayed ordinary-care journey: `RestorativeJourneyAdmission` →
`WorldPaidExchangeAdmission.CreateFresh` → one stall bound to Wren's own storage, account and home place
→ `WorldMarketAdmission.CreateFresh`. It adds no person and mints no stock or money. Wren trades the six
source-counted berries the care farm already gives her, the same finite supply her potion recipe uses; the
player's purchases are funded only by the ordinary quest reward, and Wren's purchases by her own proceeds.
The stall identity derives from the world and branch, so a same-seed world reproduces it exactly.
`FindStall` recognizes only that identity, merchant and rule version.

[`NativeTradingPolicy`](../../src/OpenFantasy.Hosting/Content/NativeTradingPolicy.cs) authors the stall
rule and its windows as new content. They count fixed-world advancing steps, never wall time: an answered
offer is acceptable for 1,200 steps, a showing lasts 2,400 steps (also the rule's longest quote) and a
request waits at most 600 steps. At the local session's nominal 50 ms cutoff these are one minute, two
minutes and thirty seconds of running time. The earlier fixture rules and the session's 32-step defaults
are unchanged. `Configuration` supplies Wren's opt-in merchant preference as Host advice on every opening;
it is never saved.

[`FarmClientSession.OpenMarketsAsync`](../../client/scripts/FarmClientSession.cs) is the explicit
selection. It creates the world in a new save or opens an existing save only when that save is the same
selected world, binding Wren's gift preference and the trading configuration together. It never upgrades,
resets or relabels another save, and other selections refuse a market save. The
[entry scene](../../client/scripts/NativeTradingEntry.cs) is launched by name; the default bootstrap and
earlier selections keep their behavior.

## Runtime binding: participant projection

[`LocalMarketView`](../../src/OpenFantasy.Hosting/Simulation/LocalMarketView.cs) extends the player's
`LocalWorldView` in market worlds. It carries the advancing step that dates offers, showings and requests,
and for each stall: identity, merchant, commodity and its public source name, the per-offer unit limit,
the longest window, whether the merchant is available in person where the player stands, the player's own
usable lots of the commodity and the player's own recorded showing. `PrepareShowing` captures one exact
own lot, units, the player's own account and an absolute expiry once; preparing submits nothing. The expiry is the
observed advancing step plus one scheduled publication step plus the chosen window. Cutoffs that elapse before
submission shorten the remaining window. Publication preserves the captured expiry; it does not renew consent or
silently retime the request. A showing whose remaining window has expired is refused by authority.

The projection never carries the merchant's stock count, funds, rule parameters, other actors' showings,
quotes or memories, or any control credential. Offers themselves remain participant-safe
[`PaidQuoteView`](scheduled-trading.md#participant-quote-view) values.

## Runtime binding: client models and panel

Engine-free models hold the player's choices and are linked into the managed tests:

| Type | Responsibility |
| --- | --- |
| [`FarmMarketDraft`](../../client/scripts/FarmMarketDraft.cs) | Stall, buy or sell, units and the exact own lot; one reviewed offer (`FarmMarketReview`) captured only by an explicit review |
| [`MarketActionOffers`](../../client/scripts/MarketActionOffers.cs) | Wheel stall choices, the show and confirm offers, request preparation and their blockers |
| [`FarmMarketTerms`](../../client/scripts/FarmMarketTerms.cs) | Readable price, units with agreeing goods names, dues, totals, purse and shortfall, remaining running time and outcome text from participant views; the step basis only as an optional diagnostic |

`MarketActionOffers.BelongsTo` defines which quotes are this stall's: the participant view's `MarketId`, read
from the canonical market quote record, names **this stall's market**, and the quote's merchant and goods
agree with it. The open offer, the outcome list, saved-record text, review capture and confirmation all use
it, so two same-goods stalls of one merchant are told apart, a merchant's quote for other goods is never
listed as this stall's, and a bilateral paid quote (no market record) belongs to no stall. Archived records
carry the market of their own recorded root (see [participant quote view](scheduled-trading.md#participant-quote-view)).

`ContextStall(view, chosen)` addresses the player's explicit choice by stable market identity: that stall or
nothing, even when it is out of reach or another stall is nearer, so no choice is retargeted. Without a
choice it offers the stall available in person, then the first stall in identity order; that fallback is
addressed only for display. `FarmMarketDraft.ChosenMarketId` is the explicit choice: the selector, a field
pick, or opening a stall's panel while it is in reach (or a world's only stall) records it. With several stalls
and none in reach, opening Offers only looks at one (`Look`): the selector stays on its prompt, nothing can be
asked there, and closing the panel forgets it. An explicit choice is never downgraded. When it is out of reach,
the blocker names a stall that is in reach and the Offers → Stall route instead of retargeting. In a world with
several stalls the panel's stall selector (hidden with one stall) lists every stall by identity with its
merchant, place and availability. Choosing another stall starts a fresh visit and clears earlier units, lot and
review; it asks, shows and spends nothing. The cargo world also lets a pointer pick a merchant's stall through
the field projection ([cargo presentation](cargo-presentation.md)).

Any change of direction, units or lot discards a review, and so do Back, focus loss and controller
disconnection. A review requires an open offer belonging to the stall on the chosen side, covering the
chosen units, for the chosen lot when selling, with an observable issuer. Confirmation restates that
review: it is blocked when the live quote is absent or closed, its hash or stall differs, the issuer is
no longer observable or its observed revision changed, the purse cannot pay, or the shown lot changed. The
confirm offer carries the acceptance's own command identity, because a paid acceptance restates it;
[`FarmActionOffer`](../../client/scripts/FarmActionOffer.cs) refuses any other identity for it.

The wheel's Market page offers Buy, Sell and Offers. These are panel choices: they carry no intent, the
session refuses to submit them, and confirming one only opens the stall, also while the world is paused.
The scene-owned [stall panel](../../client/scripts/FarmMarketPanel.cs) records choices in the draft and
requests separate commands: ask, cancel, show, confirm, inspect a saved record, pause and close. Its
inspector shows the exact terms without hover; tooltips and focus repeat them. Player text names price,
dues, total, purse, any shortfall and the time left while the world runs; steps, revisions, save segments
and implementation caveats stay out of the ordinary card (hovering the status line shows the step basis).

The visible controls form one cyclic focus chain rebuilt whenever the visible set changes, so arrows,
D-pad and Tab never leave the panel. The lot and outcome dropdowns open with Enter / A or a click; their
list owns input until a row is chosen (Enter / A / click) or cancelled. The engine's list cancel is
Escape only, so the panel also closes an open list on the router's controller Cancel (B) through the
popup's own window input; cancelling chooses nothing and keeps the stall open.

## Input, readiness and consent

[`FarmInputRouter`](../../client/scripts/FarmInputRouter.cs) gives the open panel its own `Market` layer.
It suppresses walking and world verbs and applies the existing held-direction rule after closing. Pause,
Cancel and the wheel key close the panel; a focused panel control owns a native accept, so controller A
completes activation on its release. Opening the panel does not change the host readiness request.

Inquiry, readiness, movement suppression, reading and consent are separate:

- A request is nonbinding session input. The merchant answers at an advancing cutoff, so the open panel
  keeps time running and never deadlocks the answer.
- **Pause to read** is the player's explicit local pause. It stops this single-player world, including
  every window, and Wren cannot answer meanwhile. It is not a shared multiplayer Wait.
- Showing a lot and confirming an offer are world commands that need running time and no pending action.
- A merchant keeps one standing offer per shopper; asking again waits until the player accepts it or its
  window closes. Nothing re-asks, renews or confirms for the player.

Saved-record inspection uses the session's background `InspectPaidQuoteAsync` and never reads history in
a frame. The panel lists outcomes of the current save segment; archived-only discovery is not surfaced.

## Native qualification binding

Disposable probes drive the stall only through ordinary engine input on a named injected route.
[`MarketProbeDriver`](../../client/scripts/MarketProbeDriver.cs) moves focus with arrows or the D-pad
along the panel's own neighbours, activates with Enter or A, or hovers, scrolls and clicks with the
pointer; dropdown rows are reached and chosen inside the open list by the same route. It never grabs a
destination's focus, selects a row, emits a signal or calls a semantic handler, and it traces every event,
focus move and row. [`MarketProbeEvidence`](../../client/scripts/MarketProbeEvidence.cs) records compact
observations: the participant-view hash the managed verifiers recompute, plus step, purse, actor
positions and presence revisions, quotes, lots, showing, request, review and input ownership.
[`MarketBoundaryPlayProbe`](../../client/scripts/MarketBoundaryPlayProbe.cs) runs refusal journeys over
closed real-authority saves: a refused Confirm is attempted with time running and every other condition
met, then recovered lawfully by a new request or an explicit re-review. A refused confirmation is never
submitted, so its reason is established from the recorded state at the attempt rather than a receipt.

## Boundaries

- This `--markets` world has one stall and one commodity; the two-stall cargo world is bound natively in
  [cargo presentation](cargo-presentation.md). Lived-save migration, demand simulation and merchant routes
  remain separate obligations.
- The windows are authored values for human reading in a single-player local session. Multiplayer timing,
  shared Wait and network delivery need their own admitted contracts.
- Injected native routes and background rendering do not qualify physical devices, OS focus, art, audio or
  human comprehension.
