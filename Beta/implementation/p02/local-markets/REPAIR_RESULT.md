# Market privacy and policy repair — result

Status: **implemented and verified by all five granted checks on one final source/binary pin.** This is
a worker report, not acceptance; the lead reviews the diff and tests before committing. No nested
agents, installs, staging, commits or extra paid requests were used. No tool was denied.

## Evidence (all under `implementation/p02/local-markets/qualification/`)

| Check | Report | Result |
|---|---|---|
| build | `build-20261001T024528Z-d381a` | **failed** (CS0136, duplicate local `revision` in `WorldActionCodecV18`); retained, fixed by renaming |
| build | `build-20261001T024538Z-ee98c` | accepted (source only) |
| build | `build-20261001T025253Z-37543` | accepted; `new-core-20261001T025306Z-6477a` 26 passed, `new-host-20261001T025313Z-4278c` 38 passed (superseded) |
| **build (final)** | `build-20261001T025421Z-ed3af` | accepted, 14.5 s |
| **new-core** | `new-core-20261001T025439Z-67ae3` | **26 passed**, 0 failed/skipped |
| **new-host** | `new-host-20261001T025443Z-9d1b9` | **39 passed**, 0 failed/skipped |
| **focused-core** | `focused-core-20261001T025511Z-2da06` | **125 passed** |
| **focused-host** | `focused-host-20261001T025517Z-f0392` | **155 passed**, 268.2 s |

Every report shows 526 pinned existing tests and 23 frozen specs preserved before and after, along with the
source database, the predecessor index and an unchanged source. The final four test reports cite the
final build report (sha256 `100efcae…51b6`).

Counts: Core went from 17 to 26 market cases (+9). Host `WorldExchange` has 39 cases: the 32 paid cases
and 7 market cases. The market cases are the 5 delivered tests (4 strengthened) plus 2 new ones. The
previously failing `Learned_prices_are_local_dated_private_and_never_refresh_from_a_distance` now
passes, with a real route.

## Implementation map

Core (`Runtime/src/OpenFantasy.Core`):

| File | Change |
|---|---|
| `Markets/MarketLotDisclosure.cs` (new) | Typed seller→market showing of one lot: market and merchant, lot kind, id, definition and revision, units, seller storage and account, window, cause command and index. Has `IsLiveAt` and `Authorizes(SpotQuote)` and canonical `market.lot-disclosure.v1` bytes |
| `Markets/MarketPricePolicy.cs` | Adds `AllInBid = mid(s+q) − markdown`. `Bid = AllInBid − unitFee`. `Ask`/`Bid` post nothing above `MaximumStock`. Remarks state the guarantee |
| `Markets/MarketBinding.cs` | `PlaceId` renamed to `HomePlaceId`, documented as origin identity, not location. `CountUsableStock` saturates at `MaximumStock + 1` |
| `Worlds/WorldMarketLocality.cs` (new) | The in-person gate: presence, mutual reach, alive, able to act. Public `FindAvailable` for preparation |
| `Worlds/WorldMarketBook.cs`, `WorldMarketCodec.cs` | `Disclosures` list: one per seller × market, at most 256, never evicted; `Disclose`/`Retract`/`DisclosesTo`/`DisclosuresTo`. Carried across cuts; `ValidateSuccessor` rules; fourth canonical list |
| `Worlds/WorldMarketIntents.cs`, `WorldActionCodecV18.cs` | `DiscloseMarketLotIntent` and `WithdrawMarketLotDisclosureIntent`, frames `world.action.market-lot-disclose` and `world.action.market-lot-withdraw` |
| `Worlds/WorldMarketResolver.cs` | Buy issuance requires a live disclosure before any lot read (`NotAuthorized`). Sell issuance inspects only the merchant's own usable commodity lot. Disclose/withdraw handlers. Home-place comparisons removed. `FeeMatches` shared with admission |
| `Worlds/WorldPaidExchangeResolver.cs`, `WorldStepProcessor.cs` | Optional `counterpartyDisclosed` predicate. Profile 33 supplies it, so a bilateral issuer-buys quote needs the counterparty's showing to a market of the issuer. Profile 32 passes null and is unchanged |
| `Worlds/WorldMarketAdmission.cs`, `WorldAdmissionValidator.cs` | Validate checks fee and payee equality, the merchant presence's `HomePlaceId`, and disclosure ownership, time and window. Identity roles for disclosure seller, storage and account |

Hosting: `Simulation/MarketTradingService.cs`:

- `PrepareObservation` is gated by `FindAvailable` and returns null before any revision is read.
- `PrepareOffer` reads only the merchant's own lots for sells and only the counterparty's own showing for buys.
- New `PrepareDisclosure`, `PrepareDisclosureWithdrawal`, `ReadDisclosures` and `MarketDisclosureEntry`.

Tests:

- `Core.Tests/WorldExchange/Markets/MarketPricePolicyTests.cs`: 5 new methods, 9 cases.
- `Hosting.Tests/WorldExchange/Markets/LocalMarketWorldFixture.cs`: payee-aware rule overload, `ConstantWithDue`, `StepTraderAside` and a corrected `DistantGoal`.
- `LocalMarketWorldTests.cs`: tests 1, 3 and 4 strengthened, plus 1 new test.
- `LocalMarketDisclosureTests.cs` (new): 1 test.
- Docs: `Runtime/docs/architecture/world-markets.md`.

## Contrasts proved through the actual authority

1. **Observation locality.**
   - The route failure was real geometry, not a range problem. After Wren's approach the player is enclosed by four admitted bodies: the wolves at ±1 tile in X, Wren at −1 tile in Y and the third human at +1 tile in Y. Every gap is narrower than the player's body. The test now asserts that enclosure: the route is not `Reachable`.
   - The third human then walks one tile along the exit its admission already proves. The player walks +Y through real advancing steps until out of reach. Nothing is teleported, and reach is unchanged.
   - From a distance, `PrepareObservation` gives null for the real market and for a guessed id alike. The head hash is unchanged.
   - Raw commands, even one naming Wren's true presence revision, refuse `OutOfReach` exactly like the guess, and knowledge is unchanged.
   - The old-memory read, the revisit with new evidence, the NPC observer's unchanged memory and the foreign-reader refusal are all retained.
2. **Buy-side privacy** (`LocalMarketDisclosureTests`).
   - With no showing, preparation of a buy quote returns null for the player's real lot and for an unknown id. A sell offer naming the player's lot also returns null.
   - Raw market buy quotes and raw bilateral `IssueSpotQuoteIntent` issuer-buys quotes refuse `NotAuthorized` with all owners unchanged. This holds for the real lot at its true revision, at a wrong revision and for an unknown id, so the issuance oracle is closed.
   - Forged showings refuse: the trader's own command naming the player's lot gives `NotAuthorized`, a foreign delivery throws `AuthorityDispatchException`, and the trader cannot prepare a showing for the player.
   - Only Wren's controller sees the lawful showing.
   - Withdrawal, a one-step expiry and a stale lot (the shown lot moved; issuance refuses `Unavailable`) each stop authorization.
   - A lawful positive resale settles and survives reopening with an exact receipt retry.
   - Test 4 carries a live showing (Wren → the trader's market) across a real automatic cut with its exact hash and cause. After reopening, the trader issues a buy quote under it.
   - Test 1's resale now requires the player's own showing first.
3. **Fee coalition.**
   - The Core test reproduces the counterexample under the pre-repair formula: ask 6, bid 4; trader −5, payee +6, merchant −1; trader and payee together +1.
   - The repaired rule gives bid 1 (all-in 4): trader −8, payee +6, merchant +2, so trader and payee together lose −2, the full spread.
   - An exhaustive theory covers every closed sequence of up to 4 signed fills of 1–4 units, all splits and interleavings, stocks 0–16, dues 0, 2 and 3, and both curved and constant rules. The merchant always gains at least the spread per unit, and the three parties' changes sum to zero.
   - The funded Host round trip under this exact rule shows player −8, third human (payee) +6 and Wren +2, with supply unchanged and stock restored.
4. **Structural admission.**
   - A genuine profile-33 root decodes. The same root, with Wren's rule re-authored to due 2 (the ask is asserted unchanged) and the record re-pinned, is refused by `WorldSnapshotCodec.Decode`.
   - The pure tests show that stock above the domain posts no ask or bid, and that `MarketQuoteRecord` itself refuses such a basis. The resolver therefore refuses `Unavailable` and never throws.
5. **Home place and stock condition.**
   - New Host test: an open quote is refused `RevisionConflict` after an arrival. Once a gift back restores its count, it settles at its exact terms.
   - After the fill, the same count returning never revives it (`Unavailable`), and the issuer fill revision advanced exactly once.

## Consequential decisions

- **The disclosure is market-scoped, keyed by seller × market, and replaces any earlier showing.** It is not consumed by a quote. A buy quote must close no later than the showing and may name at most the shown units. The seller picks its own payment account; nothing else in its wallet or bag is read. No manufactured delivery, account consent, automatic sale or global bag view exists.
- **Profile-33 bilateral issuer-buys quotes require the same showing to a market of the issuer.** Otherwise they refuse before the lot is read. Bilateral selling of one's own lot is unchanged. Actors without a market buy bilaterally through a seller-issued sell quote. Profile 32 keeps its documented limited interpretation, because the predicate is null there.
- **Stale disclosures.** After the shown lot changes, a quote under the old pins refuses in the paid lot check. The merchant learns only that the lot it was shown changed, which is within the showing's scope.
- **A secondary oracle in the delivered draft is also closed.** A sell quote naming a foreign usable lot previously reached the price check: a wrong price gave `RevisionConflict`, while a missing lot gave `Unavailable`. Sell lots must now be the merchant's own commodity lots before any other check.
- **The fee guarantee holds for one market at unchanged other state.** For any split, order or trader set, every coalition excluding the merchant loses at least `markup + markdown` per unit. Time, place, risk and other actors' changes remain intended arbitrage. Positive nonzero-fee trading is preserved. A due at or above the all-in bid simply posts no bid.
- **The suspected carried-quote creation-sequence rejection was not reintroduced.** The record lifetime rule in `Validate` is unchanged, and test 4's live settlement of a carried quote after the cut still passes.

## Remaining gaps (not claimed)

- The unavailable-merchant (dead or unable to act) preparation contrast is implemented in the shared gate, but no Host case exercises it. Only unknown versus distant is tested.
- A stale disclosure whose lot keeps its custodian but changes revision (`RevisionConflict`) is not separately exercised. Only the moved-lot case is.
- The fee-forgery admission test discriminates by construction, because only the due differs. It was not run against the pre-repair source.
- The high-stock refusal is proven at the policy and record level. No Host world holds more than 1,000,000 units.
- Expired showings stay stored (bounded and inert) until replaced or withdrawn. There is no archived disclosure lookup.
- The lead still has to reconcile the dependent pages (`world-paid-exchange.md` boundaries, README and index links). Full-Core and legacy selections were not part of this grant and were not run.
- The lead's `MarketPrivacyReviewTests.cs.txt` sketch was not copied. Its contrasts are covered by test 3 and the disclosure test.
