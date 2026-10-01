# Lead market review notes

These are independent review targets, not game acceptance evidence. The implementation request is
running; its source and final tests remain unreviewed.

## Quantity-sensitive round trips

An independent arithmetic check found a concrete counterexample to a plausible but unsafe rule.
Let marginal reference price be max(1,20-stock), current ask reference+1 and bid max(1,reference-1).
With stock2, a two-unit purchase costs38 and its immediate resale returns38 (no profit); with stock3,
a three-unit purchase costs54 and resale returns57, gaining3 while restoring the same merchant stock.
The finite ledger still conserves currency: this is a policy exploit that drains the merchant, not
minting. In the checked stock2..40, quantity1..min(stock,10) grid, ordinary pre-trade flat pricing
produces profitable self-cycles. A spread alone is insufficient when price impact exceeds it.

The same grid has no profitable round trip when each purchase is bounded by its highest post-trade
marginal price and each sale by its lowest post-trade marginal price. That is one conservative
comparison, not a prescribed final pricing algorithm or a proof for the implemented game. Review the
actual formula, allowed partial quantities, rounding, fees and stale quotes. Translate the counterexample
into a real complete-world test if its input semantics apply; do not accept a detached arithmetic pass.

## Other discriminating checks

- An old quote must not become valid again when stock quantity returns to an earlier value. Scoped
  revisions/causal evidence must distinguish repeated economic states; a content-derived current price
  alone cannot authenticate the earlier offer's still-valid allowance.
- Unrelated remote inventory should not accidentally become a public price signal through a global
  fingerprint. Name exactly which stock/demand/condition events affect the market and its offers.
- Current price state and dated actor memory have different owners. A read of memory after leaving
  must neither update it nor consult hidden live values to mark its truth/confidence. Show its age.
- Quotes are policy-governed commitments with explicit sides and fees; participant projections should
  not return merchant control credentials, private balances or unrelated inventory just because the
  underlying authority knows them.
- Source-backed item prices/identities do not alone prove a merchant, city or caravan has been imported.
  Keep the remaining Marla/Kegga content and actual spatial cargo route in the full integration plan.

## Initial source inspection (not final acceptance)

The first MarketPricePolicy uses a monotone stock curve and a conservative post-allowance endpoint
for fixed unit quotes. That addresses the flat-pre-trade counterexample in principle; actual partial
quantity, order splitting, stale offers and complete-world cycles still need tests.

MarketBinding currently describes PlaceId as the location where the merchant trades but binds it to
ActorPresence.HomePlaceId. ActorPresence.At preserves HomePlaceId when feet change. Review the final
locality guard: home/origin identity is not a geometric current-place or fixed-stall containment proof.
A mobile merchant may legitimately trade where it actually stands, provided actual scope/reach and
policy are explicit. A fixed market needs a real admitted location boundary. Avoid claiming two-place
travel or fixed-market taxes from a home identity that never changes on movement.

The initial resolver uses a stock-count precondition plus the existing paid issuer revision, not a new
price epoch. An open conditional quote could become eligible again if non-trade stock changes restore
that same count, while a filled/closed quote must never reopen. The earlier blanket target against any
return to eligibility was stronger than the assigned contract. Review the declared semantics and real
exploit/replay cases before requiring an extra counter; do not impose a schema change merely to satisfy
a reviewer preference. Preserve exact lot, issuer, policy, presence, allowance, expiry and closed-identity
checks. The final architecture must explain this distinction without calling stock quantity a revision.

## Preliminary continuation concern

The first WorldMarketAdmission.Validate draft rejects a quote record when its quote's CreatedSequence
is at/before the archive boundary and the quote is no longer open. A quote carried open across a cut
must legitimately be able to settle afterward, retaining its original creation sequence and current
closing outcome. Check the final code/test for this exact case: the closing sequence, not simply the
creation sequence, determines whether a carried quote's new outcome belongs in this segment. This is
a provisional source finding while implementation is active, not a final defect claim.

Also reconcile the structural-admission description with actual fee validation: the first draft checks
market price and lifetime but does not compare a quote's fee list with its pinned policy. Durable replay
remains the authentication boundary; identify separately whether structural admission promises the
complete market-terms invariant or only a subset, and cover the actual promised invariant.

## Preliminary application disclosure concerns

The first MarketTradingService draft has two concrete projection hazards to test on final source:

1. PrepareObservation authorizes the observer but reads a guessed market's merchant presence revision
   before checking locality. Its returned ObserveMarketIntent exposes ExpectedMerchantRevision even
   when the later submission is out of reach. Repeated preparation could reveal remote movement; a
   missing-market fallback to the observer's revision may distinguish absence. The refusal in the later
   world action does not undo information disclosed by preparation. Require a genuinely local visible
   preparation or a caller-known pin, with indistinguishable unavailable/unknown projections.
2. PrepareOffer authorizes the merchant, then FindLot searches every private raw/produced lot by guessed
   identity and returns definition/revision before validating the lot's actual custodian. A forged quote
   may fail later but still leaks private lot data through the prepared command. Validate the allowed
   source and commodity before disclosure. Buying somebody else's private goods also needs an explicit
   authorized selected-lot disclosure path; proximity or knowing an ID is not a general inventory grant.

These are source findings in an active draft, to recheck after the worker's terminal result. Do not edit
worker-owned files concurrently. Test both preparation and submission boundaries; a rejection-only
world test would miss these leaks.

[Prepared privacy tests](MarketPrivacyReviewTests.cs.txt) are an uncompiled integration-review draft
outside the Runtime build. They preserve the candidate API-level contrasts without changing the
worker's owned source or overlapping its build. Adapt and run only after the worker is terminal; the
actual movement route and any API changes must be verified then. No test pass/failure is claimed yet.

The buy-side concern also needs a command-level check: after bypassing PrepareOffer and constructing
IssueMarketQuoteIntent directly, does successful issuance versus rejection become an oracle for a
counterparty's undisclosed private lot? Profile32 explicitly left general private-stock disclosure as a
later binding; the new local-market API must state and prove its boundary rather than imply that the
older limitation disappeared. Fixing only a projection filter is insufficient if an equivalent write
request reveals the same fact. Prefer an actual authorized selected-lot disclosure, declared public
sale offer, or another explicit proven permission contract. Do not synthesize a second actor's consent
from its account/control identifiers, reuse care/gift permission as sale disclosure, or silently drop
required buy-side functionality. Assess this on the final implementation before choosing a repair.

## Fee-recipient coalition counterexample to assess

The current constructor permits a constant mid-price5 (source/min/max5), markup1, markdown1 and a
unit fee3 paid to a third actor. A buy/resell of one unit has ask6 and bid4. The cycling actor loses5,
the fee recipient gains6 and the merchant loses1, restoring stock and conserving total currency.
A coalition of the cycling actor and fee recipient gains1 risk-free, as long as the merchant has real
funds. No minting is required. The current pure Cycle test considers only the cycling actor and would
miss this aggregate gain. This is arithmetic derived from the draft policy, not an executed game proof.

Assess the intended policy guarantee for1–4 players and cooperative NPCs: compare the merchant's
all-in acquisition cost (bid plus its fee) against its sale proceeds, or explicitly model an intentional
subsidy with its own finite purpose/limits. Do not silently claim the test proves fee-linked coalition
safety. Reproduce against the final policy and real funded Host before deciding a correction; third-party
fees themselves remain valid and must not be removed just to make an anti-exploit test pass.
