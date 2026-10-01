# Local markets and learned prices — result

Status: **implementation, tests and architecture written; verification blocked. No build or test has
run.** Nothing here is a qualified pass.

## Blocking verification failure (tool denial, not worked around)

Every granted wrapper mode first creates `implementation/p02/local-markets/qualification/<label>` with a
non-recursive `mkdir()`. The packet had no `qualification/` directory, so both `build` runs failed before
dotnet was invoked:

    FileNotFoundError: ... local-markets/qualification/build-20261001T020427Z-9453c
    FileNotFoundError: ... local-markets/qualification/build-20261001T020529Z-db9c7

Creating the directory (via a placeholder `qualification/README.md`) lies outside the owned paths, and
that Write was denied. As the assignment requires, that operation ended there. No direct dotnet, mkdir or
other workaround was used. **No new or existing test has run, and no source has been compiled.** The code
was reviewed by hand against `LangVersion 12`, `Nullable`, `TreatWarningsAsErrors` and
`CheckForOverflowUnderflow`, and against the xUnit analyzers. Compile errors or failing assertions may
still remain.

Lead action required: create `Beta/implementation/p02/local-markets/qualification/`, then run each of the
following and observe its terminal report: `build`, `new-core`, `new-host`, `focused-core`, `focused-host`.
Repair any failures before integration. The report paths will be
`qualification/<kind>-<UTC>-<id>/report.json`. Expected new cases: 6 Core methods (7 cases including the
theory rows) and 5 Host cases.

## Implementation map

Core (`Runtime/src/OpenFantasy.Core`):

| File | Responsibility |
|---|---|
| `Markets/MarketPricePolicy.cs` | `MarketSourcePrice` (immutable cited item key, price, source digest); `MarketPricePolicy` (versioned integer rule, `MidPrice`, `Ask`, `Bid`) |
| `Markets/MarketBinding.cs` | market → actual merchant, owned storage/account, commodity pin, home place, rule; `CountUsableStock` witness; `MarketPostedTerms` |
| `Observations/MarketPriceObservation.cs` | actor-owned dated memory: source merchant, pins, ask/bid/due, world revision, step, causing command/index; no stock |
| `Worlds/WorldMarketBook.cs` | profile-33 owner (`Markets`, `MarketQuoteRecord`s, knowledge one per actor×market); `WorldMarketContinuationContext` |
| `Worlds/WorldMarketCodec.cs` | canonical frames; re-encode check refuses any other form |
| `Worlds/WorldMarketIntents.cs`, `Worlds/WorldActionCodecV18.cs` | `IssueMarketQuoteIntent`, `ObserveMarketIntent`; action codec 18 |
| `Worlds/WorldMarketResolver.cs` | rule-priced issuance (then every paid rule via `WorldPaidExchangeResolver`); in-person observation; `IsCurrent` stale-basis check |
| `Worlds/WorldMarketAdmission.cs` | profile 33 `CreateFresh` (unplayed profile-32 genesis only) and structural `Validate` |
| `Worlds/WorldSnapshotCodecV33.cs`, `Worlds/WorldStepCodecV33.cs` | profile-33 root and step frames |

Joins to existing Core files:

- `WorldCoordinatedContinuation` gains `Markets`, `WithMarkets` and cut carry.
- `WorldMaterialCareArchiveAnchor` gains the market context. Its hash is appended only when markets are
  present, so older anchors keep their bytes.
- `WorldSnapshot.ProfileVersion` returns 33, and the snapshot, step and action codecs dispatch it.
- `WorldStepBatch` and `WorldStepFrameCodec` add 33 to their profile lists. Market intents are accepted
  only in 33; paid intents in 32 or 33.
- `WorldStepProcessor` gains the market branch and refuses stale market-quote acceptances.
- `WorldAdmissionValidator.ReadMarkets` registers market identities, with a new `WorldIdentityRole.Market`.
- `WorldConsentedMaterialAdmission` runs market validation.
- `WorldRestorativeResolver`, `WorldRestorativeActions`, `WorldRestorativeAdmission`, `WorldCareAdmission`
  and `WorldResourceAdmission` add 33 to their profile lists.

Hosting (`Runtime/src/OpenFantasy.Hosting`):

- `Worlds/SqliteWorldAuthority.Markets.cs` adds `SubmitMarketCommand`: one trusted command per advancing
  step, authorization before disclosure, and exact retry. It also adds `ReadMarketKnowledge`, which reads
  only for the holder's current controller and authorizes the delivery first.
- Profile 33 is added to schema selection, `WorldJournalPolicy`, `WorldJournalRecovery` (two checks), the
  material-care archive reader, and the proposal and paid submission profile checks.
- `Content/MarketCircuitPolicies.cs`: cited legacy prices (berries 5, rations 25; items.json digest
  `ac0944b9…655609`), authored `BerryStall` and `RationCounter` rules, and `BindResident`.
- `Simulation/MarketTradingService.cs`: prepares a merchant's offer for its own controller and an
  observer's reading with no price supplied, and projects the bounded `MarketKnowledgeEntry`.

Architecture: `Runtime/docs/architecture/world-markets.md` (new). The lead reconciles README,
world-paid-exchange, spot-exchange and coordinated-continuation links.

## Tests written (all unrun)

`tests/OpenFantasy.Core.Tests/WorldExchange/Markets/MarketPricePolicyTests.cs`:

- source anchoring and mid prices that never rise with stock, including `ulong.MaxValue` stock;
- ask/bid zero-stock, lot-limit, holding-limit and spread edges;
- **closed-loop exhaustive theory**: every split pair up to 4 units, stocks 0–16, with and without a due,
  loses at least the spread per unit;
- construction refusals, and the largest admitted rule's checked arithmetic;
- purity under reordered evaluation, and version sensitivity;
- canonical owner bytes, refusal of trailing data and permutation, and knowledge pin and invariant
  refusals.

`tests/OpenFantasy.Hosting.Tests/WorldExchange/Markets/` uses `LocalMarketWorldFixture`: a fresh profile-33
world built from the existing read-only `PaidTradeWorldFixture`, with Wren's stall and the third human's
empty stall. It has five cases in `LocalMarketWorldTests`:

1. **Rule-priced buy and resale.** The earned quest reward funds a buy at `ask(s,1)`. The merchant's stock
   falls once; it buys the unit back at `bid(s−1,1) < ask`. Goods, provenance and supply are checked.
   Exact retries, reopening and receipt bytes are unchanged.
2. **Stale basis after a real arrival.** An empty stall cannot quote. A real saved gift arrives and the
   stall issues a quote. Repeated polling changes nothing. A second real gift arrives: acceptance is
   refused `RevisionConflict` with every owner unchanged, while ordinary time advances, and the exact
   retry is identical. The issuer withdraws, requotes at a lower ask and the sale settles.
3. **Local, dated, private knowledge.** A guessed market id creates no knowledge. The player and the NPC
   trader each observe in person. A foreign controller's read is refused. The player actually walks out
   of range, and Wren lawfully gives a unit away. The old memory is unchanged and only ages. A distant
   observation refuses and does not advance knowledge. The player walks back, and a new observation shows
   the changed ask with a later revision. The NPC's memory is unchanged.
4. **Automatic cut.** An observation and a market quote are carried across an automatically selected
   coordinated cut, driven by real bilateral expiries. Settlement happens live after reopening, and the
   exact receipt retry is checked after a second reopening. Two tampering cases are refused:
   - a genuine earlier root with a recomputed hash written as the head;
   - a well-formed journal batch whose observation names another market under the same identities.
5. **Exact scope.** Merchant preparation refuses a non-controller, and submission refuses a foreign
   delivery. A mispriced quote gives `RevisionConflict` with no owner change. Another actor issuing
   under Wren's market gets `NotAuthorized`. A bilateral profile-32 quote stays valid in 33 and records no
   market basis. Market intents are refused in profile 32. Command and world bytes are canonical, and
   re-admitting a profile-33 or lived root is refused.

Real operations covering arrival and loss: saved-proposal gift settlements (arrival at the receiver,
removal at the giver) and paid fills (purchase and sale). Gathering, crafting and consumption change the
same counted owners, but no hosted case here exercises them. No quantity is injected.

Hosted risks I could not check: whether the farm offers a walkable goal 4–6 tiles away (test 3 searches
six directions), whether the quest reward covers the prices used (asserted with messages), and whether
Wren's genesis berry stock is at least 2 (the steep rule needs it).

## Consequential design decisions

- `SpotQuote.MarketRevision` keeps its paid-fill meaning. Pricing state lives separately: a
  `MarketQuoteRecord` pins the market, the rule version and the usable stock count that priced the quote.
- **What invalidates a fixed offer.** It settles only if, before settlement, the merchant's usable stock
  equals the record's basis, the rule version is unchanged and the merchant is at its home place. A stale
  offer is refused `RevisionConflict`. Terms are never rewritten, revived or partially consumed. The
  issuer withdraws it, or its rule-bounded window (64 steps in the authored rules) expires it. A fill by
  the same issuer still supersedes its siblings under the old rule. The bounded book always stays
  closable.
- **Price.** `mid(s)=clamp(floor(src·(T+K)/(s+K)),min,max)`, `ask=mid(s−q)+markup`, `bid=mid(s+q)−markdown`
  while `s+q ≤ H`. With a spread of at least 1, the cost telescopes, so no same-state cycle profits in any
  split. The due goes to an authored payee that may be neither participant. Repricing happens only
  through real stock changes. Reads, polling and wall time never reprice.
- **Demand.** The authored stocking target and holding limit are an initial policy. Real orders and needs
  are a documented seam that needs typed causes.
- **Knowledge.** One latest observation per actor and market, bounded by roster × 4 markets, never evicted.
  Observation requires current control, readiness, an advancing unheld step and mutual reach to a living
  merchant at its place. Missing and distant markets refuse alike. Observation spends no work and records
  no stock count. Player and NPC controllers follow the same rules.
- **Profile 33 is a fresh composition** of an unplayed profile-32 genesis: it mints no stock or money,
  and no lived world migrates. Profile-32 bytes, codecs and refusals are unchanged, and bilateral
  quotes keep their old interpretation.

## Remaining gaps (not claimed)

- All verification (see the blocker above). The architecture page is new and its cross-links are not
  reconciled.
- Rule versions are immutable per world; a typed policy-change event is a seam. There is no NPC trading
  loop, native UI, two-town route, archived market-quote participant lookup or retained-save migration.
