# OPUS_RESULT — paid world binding

Status: INCOMPLETE. Build passes; focused-host 118/119 — one NEW test fails (below). Time bound
exceeded; no further runs. Nothing here closes a gate.

## Design (as implemented)

Explicit new complete-world interpretation **profile 31** = profile 30 (saved proposals, consent
production V11) plus a world-owned paid-quote owner. Selected only by
`WorldPaidExchangeAdmission.CreateFresh(ordinaryCareGenesis)` (admission label
`world.paid-exchange-admission.v1`). No lived-world migration; profile 27/28/30 bytes and rules unchanged.

- Owner: `WorldPaidExchangeBook` (in `WorldCoordinatedContinuation.PaidExchange`): open `WorldPaidQuote`s
  (≤16), segment outcomes `WorldPaidQuoteOutcome` Filled/Expired (≤64), per-issuer market revisions.
  Holds no balance/stock. Reuses `SpotQuote`/`SpotAcceptance` terms and `SpotExchangeCodecV1` frames.
- Actions (action codec 17, profile-31 advancing steps only): `IssueSpotQuoteIntent`, `AcceptSpotQuoteIntent`.
  `WorldPaidExchangeResolver` mirrors the proposal contract: control → own presence revision →
  advancing/unheld/ready → quote. Issue: derived quote id, issuer claims = command, ownership of both
  ends, issued step = count+1, bounded expiry, market revision, currency pin, exact current lot pin
  (allowance ≤ stock); reserves nothing, not work. Accept: acceptance id = command id; open, not due,
  exact `Binds`; issuer's recorded delivery = current control; issuer has no action in batch; neither
  party worked; issuer ready; presence revision; manipulation bodies and HP>0 for both; reach both ways;
  market revision; funds; then shared `SpotMaterialTransferStep` (goods, protection, reservation,
  combined capacity, lineage) and `CurrencyLedger.Transfer` via new shared `SpotQuote.PaymentsFor`.
  Production revision unchanged (no V12, no `SupportsSpotExchange`). Consumes both opportunities.
- Currency-only causality: no actor batch is invented. `WorldResourceAdmission` requires
  `progression.Revision = basis + actorBatches + paidFills` and replays one merged ordered sequence
  (world revision, command ordinal) of actor batches and paid fills (`WorldPaidCurrencyCause`),
  reconstructing the exact ledger/account revisions; ties refused. Old profiles have zero paid causes.
- Custody: produced paid moves merged with proposal settlements in world order for
  `ProducedCustodyPartition`; raw fills checked against admitted source rights/issuance.
- Expiry at the advancing boundary (same rule as proposals). Rollback via existing per-action restore.
- Cuts: anchor gains `WorldPaidExchangeContinuationContext` (open quotes + issuer revisions), hash
  appended only when present; successor must keep carried quotes exact and issuer revisions =
  carried + fills; cut reservation counts open + newly issued quotes.
- Codecs: `WorldSnapshotCodecV31`, `WorldStepCodecV31`, `WorldActionCodecV17`, `WorldPaidExchangeCodec`.
- Host: schema 8 physical layout reused; profile 31 named explicitly in creation, recovery (genesis
  and state), journal accounting and joint archive publication. `SubmitPaidExchangeCommand` (exact
  retry returns original receipt, no second transfer).

## Changed files

Core/Worlds new: WorldPaidQuote.cs, WorldPaidExchangeBook.cs, WorldPaidExchangeCodec.cs,
WorldPaidExchangeIntents.cs, WorldPaidExchangeResolver.cs, WorldPaidExchangeAdmission.cs,
WorldActionCodecV17.cs, WorldSnapshotCodecV31.cs, WorldStepCodecV31.cs.
Core/Worlds modified: WorldCoordinatedContinuation, WorldMaterialCareArchiveAnchor,
WorldMaterialCareContinuation, WorldConsentedMaterialAdmission, WorldResourceAdmission,
WorldStepProcessor, WorldSnapshot (ProfileVersion), WorldSnapshotCodec, WorldStepCodec, WorldActionCodec,
WorldStepBatch, WorldStepFrameCodec, WorldCareAdmission, WorldRestorative{Resolver,Actions,Admission}.
Core/Markets: SpotQuote (`PaymentsFor`), SpotExchangeAuthority (uses it; behavior unchanged).
Hosting/Worlds: SqliteWorldAuthority(.PaidExchange.cs new), WorldJournalRecovery, WorldJournalPolicy,
WorldDatabase.MaterialCareArchives.
Tests new: tests/OpenFantasy.Hosting.Tests/WorldExchange/PaidWorldExchangeTests.cs.

Docs: new `Runtime/docs/architecture/world-paid-exchange.md`. The existing reference pages
(spot-exchange, world-resource-history, coordinated-world-continuation, persistence-and-history,
material-proposals, README index) are NOT yet updated to cross-reference profile 31. Lead follow-up.

## Checks (terminal, observed)

- build: failed once on a missing test `using` (retained:
  `qualification/build-20260930T045347Z-50b2f`); then PASS at `qualification/build-20260930T045438Z-64340`
  (513 existing tests, 23 specs, source/index preserved: true).
- focused-host: `qualification/focused-host-20260930T045455Z-a418f` — 119 total, 118 passed, **1 failed**:
  `PaidWorldExchangeTests.Unfunded_unreachable_and_expired_quotes_change_no_owner` at line 124:
  expected `OutOfReach`, got `Unavailable`. This is the genesis-distance acceptance. An earlier
  resolver check refuses first (probably issuer readiness/`CanAct`, or presence/body at genesis), so
  the test's assumption about ordering is unproven. The original assertion is retained, not weakened.
  The rest of that test (expiry, unfunded) did not run. Both other new tests pass: the earned-funds
  partial raw buy with fee/conservation, stale terms, exact retry and reopen, and old-profile refusal
  with codec canonicality. Every existing test in the focused-host selection passed.
- focused-core: NOT RUN (time).

## Known defects / missing proof

- The failing test above needs diagnosis. Either the refusal order differs at genesis (the test
  expectation must then change, with a reason), or the resolver has a real defect.
- No test of an actual automatic coordinated cut carrying an open quote, settlement after the cut,
  or closed identity non-reuse across a cut. The code path exists: anchor context, successor
  validation, cut reservation. It is unproven.
- No tests for: produced/crafted-lot payment, IssuerBuys, protected, reserved or insufficient-capacity
  stock, the issuer busy in the same batch, dead/held actors, duplicate/conflicting identity, or
  malformed paid-book frames beyond one trailing byte.
- Interleaved quest reward and paid fill are proven once: the reward precedes the fill in the passing
  test, and every candidate runs the merged resource admission. No test interleaves a fill with
  healing or crafting.
- Recovery replays saved commands generically (`WorldJournalRecovery`). Only the reopen of an uncut
  world is proven. `WorldMaterialCareArchiveReplay` and the archived-outcome lookup do not interpret
  paid outcomes; there is no participant query for quotes.
- Genesis admission does not explicitly require an empty paid book. Structure makes quotes impossible
  at revision 0, but no explicit check exists.
- Process-kill/power-loss: UNRUN.
- Receipts reuse `WorldActionRejection`: funds, capacity and stock all report `Unavailable`. No
  specific reason is disclosed.

## Recommendations for lead review

1. Diagnose the genesis refusal: step through `WorldPaidExchangeResolver.Accept` with the fresh
   ordinary genesis.
2. Add a cut test using `CareWorldFixture(materialCareStress…)` or proposal-style owner-published cuts,
   carrying an open quote and settling it after the cut and after reopening.
3. Check the anchor-hash field order (proposals, then paid, then never transition) and that the
   `WorldPaidExchangeContinuationContext` hash encodes the carried book.
4. Update the cross-reference pages listed above.
