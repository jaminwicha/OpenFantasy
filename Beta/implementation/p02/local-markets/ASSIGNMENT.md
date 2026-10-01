# Local markets and learned prices — Opus 5.5 implementation

Implement a coherent retained market-rule and local-knowledge layer on the qualified paid-world
runtime, toward the full two-place cargo/escort circuit. This is substantive code and hosted tests,
not a survey or detached calculator. The owner requests Opus; use exactly
`claude-opus-5-5[1m] --effort max`, at most3,600s/$35 client estimate. Astra integrates and reviews.
No nested agents, automatic paid retry, silent substitution, install, staging, commit or branch change.
You are not alone: own only the paths listed below, preserve every other writer and all existing tests.
First create RESULT.md with an in-progress note to confirm access. A required tool denial ends that
operation; report it instead of working around the grant. Work toward a build early enough to finish.

## Context and intended result

Read root/Beta AGENTS, the current handoff, `../spot-exchange/INTEGRATION_PLAN.md`, the paid-world
architecture and world-composition contract. The immutable technical design's economy paragraphs
and P02/P03 define the larger scope. `../spot-exchange/LEGACY_MARKET_INPUTS.json` retains Marla,
Kegga and shared source goods/prices; those records are candidates, not already-admitted merchants.
Do not reread the entire research archive. The atomic paid-world layer is qualified:32 paid Host
cases, real continuation/recovery evidence and a newly admitted third-human fixture. Its existing
files/tests are not a reason to redo the completed proof exercise.

Deliver versioned market rules grounded in actual owned stock and explicitly modeled local demand,
integer bid/ask quotation, and actor-owned dated price knowledge. Real Host actions must use the
existing paid settlement, and all authoritative state must survive ordinary save/reopen and an actual
automatic coordinated cut. A caller can learn a local price, leave, and later read the old observation
without learning a new distant price. The architecture must remain useful for later merchant AI,
player shops, native menus, caravan arrivals and changing supply; no alternate economy runtime.

## Owned files

- Runtime/src/OpenFantasy.Core/Markets/** and Runtime/src/OpenFantasy.Core/Worlds/** (only relevant
  market/knowledge and necessary complete-world joins; preserve existing interpretations/bytes).
- New Runtime/src/OpenFantasy.Core/Observations/Market*.cs if needed; existing observations are read-only.
- Runtime/src/OpenFantasy.Hosting/Worlds/** and Runtime/src/OpenFantasy.Hosting/Content/** (necessary
  authority/replay/admission joins, not unrelated refactors).
- New Runtime/src/OpenFantasy.Hosting/Simulation/Market*.cs application services. Existing session,
  client, Host executable and unrelated projects remain read-only; report any additional needed seam.
- New Runtime/tests/OpenFantasy.Core.Tests/WorldExchange/Markets/** and
  Runtime/tests/OpenFantasy.Hosting.Tests/WorldExchange/Markets/**, namespace under WorldExchange.
  ALL existing test files, including PaidTradeWorldFixture and all paid cases, remain read-only.
- New Runtime/content/market-circuit/** for small authored policy/binding definitions, if useful;
  existing content and source-support archives remain immutable.
- New Runtime/docs/architecture/world-markets.md only. Lead reconciles existing dependent pages.
- implementation/p02/local-markets/RESULT.md only. Assignment, preservation, wrappers, shared
  instructions, handoff, existing specifications and all other reports are immutable inputs.

Paths above are relative to Beta. No new planning-codename identifiers/comments/runtime strings.
Keep cohesive, readable C# types, immutable values and named operations; avoid giant owners, property
bags, speculative interfaces, a universal economy DSL or a source rewrite.

## Required authority and policy contracts

1. Stock and money retain their existing owners. Market roles bind actual admitted actors, custodians,
   accounts, commodity definition/version, place/presence and rule version. A counted/produced stock
   observation is a checked derived witness, never another quantity ledger. Finite supply is conserved.
2. Demand must mean something explicit, e.g. an authored merchant stocking target/commitment against
   actual usable holdings. A declared target is an initial demand policy, not a claim of simulated
   population consumption. Define the hook for later real orders/needs without inventing them now.
   Include actual available raw and produced goods, reservations/protection and legitimate changes.
3. Use checked integer bid/ask arithmetic, declared spread, bounded rules and explicit fee semantics.
   Source item prices are immutable inputs; new policy parameters are authored separately. Prices
   cannot change because a UI polls, a dictionary iterates differently or the wall clock advances.
   Repricing has explicit economic-event or simulation-interval semantics. Arrival, purchase, sale
   and loss act through real inventory changes and are counted once; replay/retry must be exact.
4. Critically, SpotQuote.MarketRevision currently means the issuer's paid-fill revision and is
   authenticated by its fill history. It is NOT a general price-update counter. Keep those meanings
   separate. Explicitly decide what invalidates an already issued fixed offer and retain the exact
   quoted price/fees/window. Repricing cannot silently rewrite terms, revive a closed quote, consume
   an allowance twice or strand a bounded offer book with unclosable stale entries. If new causes or
   metadata are required, give them a typed, canonically ordered, replayed interpretation.
5. Issuance, observation, settlement and withdrawal are different acts. Actual trusted delivery,
   current control, rights, bodies, reach, readiness, Wait and both work opportunities still apply.
   No service chooses another actor's account merely to make a trade possible, issues under a forged
   merchant delivery, or publishes diagnostic candidates. Profile32's bilateral arbitrary agreed
   price remains valid under its old interpretation; do not silently make it a new market policy.
6. Local price knowledge belongs to an actor in world/branch scope. Inspect existing ObservationScope,
   resource observations and proposal participant services for reusable semantics, not fake reuse
   of a resource-quantity record as a price. Acquiring knowledge needs actual local admissible evidence
   and durable causal recording; a client-supplied price, mere query or guessed market ID is not proof.
   A learned observation records its actual time, source, commodity/policy pins and quoted terms.
   Reading it later does not refresh it, reveal remote changes or disclose private stock/balances.
   Player and NPC controllers use the same legality; no free omniscience for merchant AI.
7. Native/UI consumers receive bounded actor-authorized projections, not a WorldSnapshot or every
   private inventory. Exact command retry and missing/foreign lookup behavior must not leak history.
   Expensive archival inspection stays off frame callbacks. Do not claim network authentication.
8. Preserve old codecs/profiles and their refusals. Use an explicit new interpretation for new
   canonical state if needed (33 appears unused; verify), never widen a numeric range to admit unknown
   profiles or reinterpret a lived32 save. Pure structural consistency is not authenticated history.
   Complete snapshot/action/frame/identity/Host schema/journal/continuation paths must agree.
9. Couple market updates, knowledge, goods/payment, quote outcomes, receipts and identities in the
   existing complete transaction. Capacity, arithmetic, locality or late admission failure restores
   every affected owner while preserving normal passive time. Cuts carry all live market/knowledge
   state and retain typed historical causes without evicting known facts to hide capacity failure.
   Keep finite bounds and a usable continuation path; no unbounded per-frame full-journal replay.

## Concrete proofs and useful content

Use actual source-backed item definitions/price inputs where available; rations are shared by the
retained merchant candidates. Supply must be explicitly authored at a fresh admission or lawfully
acquired, with finite funding explained. The existing quest reserve is not an infinite merchant bank.
A reusable runtime admission/service should be usable by a future local session, not test-only state
injection. Hosted tests can author two real actor-owned local markets and change their accessible
stock; this does not claim the full two-town route or silently rename the farm Brasshollow/Gearford.
Preserve Marla/Kegga migration obligations and source identities for the later actual content binding.

Required checks, through the actual complete world/SQLite authority where applicable:
- One funded buy and sale using rule-derived integer terms, with actual goods/money/provenance.
- Actual stock arrival/removal and a purchase/sale change eligible local terms once; unchanged polling,
  repeated/reordered read calls and exact command retry change nothing. State which real operations
  cover loss/arrival; an injected quantity delta does not qualify as an in-world event.
- Closed-loop buy/resell, split-order/quantity edges, fees, zero stock, limits and arithmetic overflow:
  no money creation or deterministic risk-free policy exploit from one's own reversible trade cycle.
  Distinguish intended time/location/risk arbitrage from a same-state self-generated price loophole.
- Both quote sides, stale price/stock/policy/presence, unsupported older profile and unauthorized actor
  refuse with exact scope; expected ordinary advancing time is not misreported as transaction leakage.
- Observe locally, move actually out of range, change the market lawfully, then inspect the old price:
  no remote refresh. Revisit/observe again yields new evidence. Another actor cannot read that memory;
  a failed observation or query cannot create or advance knowledge. Include an actual NPC observer.
- Genuine quote/knowledge retention across an automatically selected coordinated cut, live settlement
  after it, later reopening and exact receipt retry. Actual journal tampering refuses despite a
  structurally rehashed current state. Preserve all prior tests and source DB bytes.

Prioritize a coherent integrated implementation and these proofs. If the time/usage bound prevents
completion, preserve meaningful partial work, report exact missing joins and unrun checks and do not
label scaffolds, a standalone calculator or mocked persistence as a finished market layer.

## Verification and report

Only these exact shell invocations are granted, one terminal result at a time (Bash timeout600000):
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/local-markets/run_integration_checks.py build
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/local-markets/run_integration_checks.py new-core
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/local-markets/run_integration_checks.py new-host
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/local-markets/run_integration_checks.py focused-core
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/local-markets/run_integration_checks.py focused-host

Wrappers retain failures, enforce240s build/600s tests, source/binary pins, unchanged old fixtures and
one shared heavy-check lock. Use Read/Glob/Grep for source; no extra shell discovery, parent-temp scan
or overlapping probes. Observe the completed reports, not a yielded start handle. Repair your own
code/test failures within this same request and unchanged bounds; no model retry is authorized.

Keep architecture encyclopedic: owners, APIs, invariants, canonical state, timing, migration boundaries
and extension seams. Keep progress/measurements in RESULT.md. Return a compact implementation map,
exact report paths/counts, observed failures, remaining gaps and consequential design decisions for
Astra's integration and the independent critic. No native/art/audio, physical devices, full caravan
AI, multiplayer, migration or production acceptance is assigned by this increment.
