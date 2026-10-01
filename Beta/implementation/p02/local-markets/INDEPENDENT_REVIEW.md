# Independent market boundary review — terminal

Reviewer: `claude-opus-5-5[1m]`, requested Max. This was a read-only critic: no shell, no runtime edits,
no subagents and no paid retry. Scope: fresh complete-world profile 33 local markets and selected-lot
disclosure at `cf55565` plus the integrated working tree. This review is not an acceptance. Astra
integrates and owns commits.

## Verdict

I found **two actionable defects. Both are low severity and neither blocks the bounded increment.** I
found no executable contradiction in the following areas:

- actor authorization, through both the service and raw market or bilateral commands;
- the integer price guarantee;
- atomic rollback;
- cut continuation, codec gating and finite bounds.

Four documentation statements overclaim and are listed under design limits.

## Evidence basis

**Executed by others; I read the reports and logs but ran nothing.**

- `new-host-20261001T030552Z-0972a`, filter `FullyQualifiedName~WorldExchange`: 40 passed. `0.log` names
  all eight market Host cases: six `LocalMarketWorldTests`, one `LocalMarketDisclosureTests` and one
  `MarketStockPoolTests`.
- `new-core-20261001T030638Z-c0412`: 26 passed, including 16 `MarketPricePolicyTests` cases.
- Build: `build-20261001T030512Z-ae505`.
- I did not re-verify report hashes or the preserved-test pins.

**Source inspection.** Every conclusion below comes from reading these files, unless a test name is cited.

- Core `Markets/`: `MarketPricePolicy`, `MarketBinding`, `MarketLotDisclosure`, `SpotQuote`,
  `SpotMaterialTransferStep`.
- Core `Worlds/`: `WorldMarketResolver`, `WorldMarketLocality`, `WorldMarketBook` (with its continuation
  context), `WorldMarketAdmission`, `WorldMarketCodec`, `WorldMarketIntents`, `WorldActionCodecV18`,
  `WorldSnapshotCodecV33`, `WorldStepFrameCodec`, `WorldPaidExchangeResolver`, `WorldCoordinatedContinuation`.
- Core: the `WorldStepProcessor` market, paid and rollback blocks, and the market parts of
  `WorldAdmissionValidator`, `WorldStepBatch` and `WorldMaterialCareArchiveAnchor`.
- Hosting: `MarketTradingService`, `SqliteWorldAuthority.Markets` and `FindReceipt`, `MarketCircuitPolicies`.
- Tests: the four Markets test files.
- Contracts: `world-markets.md` and `world-paid-exchange.md`.

## Findings

### F1 (low to medium, privacy contract): a stale showing still reveals the lot's remaining quantity

`world-markets.md:159-162` and `:288-289` say a merchant holding a stale showing "may learn only that
the lot it was shown has changed". The code reveals more than that.

**Why.** `MarketLotDisclosure.Authorizes` (`MarketLotDisclosure.cs:94-103`) admits any allowance up to the
shown units. After that check, issuance reaches the paid lot check, which answers in two ways
(`WorldPaidExchangeResolver.CheckPinnedLot`, `:243-249`):

- `Unavailable` when the lot is missing, has moved, or `raw.Quantity < quote.QuantityAllowance`;
- `RevisionConflict` when the lot is still in the shown storage with at least that many units.

Both issuance paths reach this check:

- the market path, `WorldMarketResolver.cs:105-108` then `:130-133`;
- the bilateral path, `WorldStepProcessor.cs:297-302` then `WorldPaidExchangeResolver.cs:62-64`, `:119`.

**Concrete trigger.**

1. The player shows lot L (3 berries, revision r), all 3 units, to Wren's market.
2. The player makes a real partial gift of 2 units from L. L stays in the player's storage with 1 unit
   at revision r+1.
3. Wren's controller submits raw `IssueSpotQuoteIntent` issuer-buys quotes pinning (L, r), at any price,
   with allowance 1, 2 and 3. The market path behaves the same within the rule's demand.
4. Allowance 1 gives `RevisionConflict`; allowances 2 and 3 give `Unavailable`.
5. Wren now knows L still sits in the player's storage holding exactly 1 unit.

If L had moved entirely, every allowance would give `Unavailable`.

**Bound.** The leak is limited to the shown lot, the shown units and the showing's window. It needs one
probing command per allowance.

**Smallest correction.** Collapse the lot-check result to a single refusal whenever an issuer-buys quote
was authorized by a showing (for example, map any non-`None` result to `Unavailable`). The correction
needs three parts:

- Pass the same signal from the market path, which currently calls the paid resolver without the
  predicate (`WorldMarketResolver.cs:132-133`).
- Update the documentation sentence at `world-markets.md:160-162`.
- Add a regression: after a real partial gift and after a full move, allowances at and above the remaining
  quantity all refuse identically, through both paths, with owners unchanged.

This is sufficient because the quote's pins must equal the showing's. An unchanged revision therefore
already implies that the shown units are present.

### F2 (low, service contract): `PrepareOffer` throws for the fee payee's own account holder

When a market's due is paid to a resident's actor account, preparing an offer with that resident as
counterparty misbehaves:

- **Sell offer.** It puts the payee account in as buyer account (`MarketTradingService.cs:77-79`, fees at
  `:194`). The `SpotQuote` constructor then throws `ArgumentException` (`SpotQuote.cs:95-99`). The
  service's documented contract is to return `null` when "the rule posts no such quote".
- **Buy offer.** If that resident showed a lot paid into the payee account, preparation returns a command
  the world always refuses (`WorldMarketResolver.FeeMatches`, `:142-146`).

**Trigger.** In the `ConstantWithDue` fixture (payee = the third human), call
`PrepareOffer(wren, WrenMarket, Trader, IssuerSells, OrdinaryLot, 1, 32, id)`.

**Impact.** This is latent today, because there is no merchant loop. A future NPC merchant iterating
nearby residents would crash on it.

**Fix.** Return `null` when the policy payee equals the counterparty account the offer would use, mirroring
`FeeMatches`. Optionally also refuse such showings in `Disclose`. Add one Host case for both sides.

## Checked with no actionable defect

**Authorization (source inspection).**

- `FindReceipt` binds the supplied delivery to the command's principal, control and current session.
- Market issuance refuses a missing market and a non-merchant issuer identically (`NotAuthorized`). The
  disclosure gate runs before any counterparty lot read.
- Sell issuance inspects only the merchant's own usable commodity lot.
- On the bilateral path, a forged-issuer probe of another merchant's showing gives the same
  `NotAuthorized` both ways.
- `Disclose` refuses another actor's lot, a missing lot and a foreign account alike.
- Observe, Disclose and `FindAvailable` share the mutual-reach gate. Preparation never reads another
  actor's lot or delivery.
- Knowledge reads authorize first. No Hosting API exposes quote records, stock bases or other actors'
  memories.
- A showing is not stock: `CountUsableStock` reads only the bound storage, and only the seller's
  acceptance moves goods.

**Pricing (source inspection, plus executed Core theory and the Host funded cycle).**

- Fills settle only at the issuance stock count (`IsCurrent`, evaluated before settlement), and every fill
  moves usable units one for one.
- In any closed walk of stock levels, each departure from level j is matched by an arrival at j. The
  departure earns at least `mid(j−1) + markup`; the arrival costs at most `mid(j) − markdown`. The
  merchant therefore gains at least the spread per unit. Partial fills only favour it.
- Reviving an open quote is harmless because each fill is priced at its actual count.
- Gifts and open-deposit storages (`CustodyDepositRule.AnyControlledActor`) can only raise stock at the
  giver's expense, and withdrawal is owner-only.
- The one-binding-per-pool guard closes the cross-rule loop.
- I found no deterministic profitable same-state cycle within the supported operations.

**Atomicity and durability (source inspection).**

- A rejected candidate restores `previousCoordination`, which covers both the paid book and the market book.
- `MarketIntent` is admitted only by profile 33 advancing batches (`WorldStepBatch.cs:89-90`).
- Codecs re-encode and compare. List bounds fit the 256 disclosures and observations.
- Records follow open quotes across cuts. The anchor hash is appended only with markets present.
- Expiry semantics match between quotes and showings (`IsDue` and `IsLiveAt`).
- Journal replay rejects a forged head or batch; this is executed in test 5.

## Design limits and documentation precision (no code change required)

- **Stock is derivable from posted terms.** With a strictly monotone curve such as `Steep`, an
  observation's posted terms or a quote price reveal the exact stock count. The documentation should
  read "never recorded; derivable from posted terms", not "never disclosed" (`:130`, `:287-288`).
- **Dropping a carried showing is enforced only by replay.** `ValidateSuccessor`
  (`WorldMarketBook.cs:289-297`) accepts any omission. The rule "drop only through withdrawal" (`:225-226`)
  holds only under replay, and the documentation should say so.
- **Bilateral buys under a showing are deliberately loose.** They are unpriced and have no stock
  condition; this is documented and the seller still consents.
- **Observation and showing causes are not registered as root identity roles.** Durable command
  reservation covers live operation, so this is optional strictness.

## Not run and remaining gaps

- No Host case for a dead or unable-to-act merchant.
- No Host case for a stale showing that keeps its storage (`RevisionConflict`).
- No regressions yet for F1 or F2.
- The Host fee-coalition cycle uses only a constant rule. Curved rules with dues are covered by the pure
  Core theory.
- Full regression, profile 33 crash or kill recovery, native UI, merchant scheduling and migration were
  outside these selections. Profile 32 kill evidence does not transfer.
