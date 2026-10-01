# Complete paid world binding — Opus implementation

The owner explicitly requests Opus 5.5. Use exactly `claude-opus-5-5[1m] --effort max`, one request bounded to 3,000 seconds and $35 client estimate. No substitution, recursive agent, paid retry or tool installation. Astra owns integration, independent review and Git. You are not alone in the checkout: preserve others' changes, edit only owned paths and do not stage, commit, reset or change branches. Create `OPUS_RESULT.md` first to verify owned write access. Keep it updated with concrete completed work and gaps before expensive verification.

## Deliverable

Implement paid quote issuance and acceptance as actual complete-world actions, joining the existing atomic spot-exchange domain to the coordinated world, resource causality and durable history. Aim for one coherent admitted world interpretation with a real funded buy/craft/sell journey, successful save/reopen and at least one actual coordinated archive cut. The intended next destination is the two-market cargo/escort loop in `../spot-exchange/INTEGRATION_PLAN.md`; native UI, local supply/demand rules and NPC trading policy remain subsequent work. Do not build a detached toy market or silently discard continuity to fit the assignment.

Read the shared instructions and current handoff, then the retained world-composition, spot-exchange, world-resource-history, material-proposals, coordinated-world-continuation and persistence references. Immutable input copies are in `/var/folders/np/sh0qs32n2xlf150ph1vlcvrc0000gn/T/openfantasy-opus-paid-world-8fp7o5wy/input`; actual architecture pages are editable deliverables. Inspect actual code and choose a cohesive, modular C# design. Do not reload the full research archive. All 181 requirements and existing gates remain intact.

## Architectural obligations

- One canonical owner per fact. Goods remain in CountedAuthorityState/ProductionSnapshot, balances in ProgressionSnapshot.Currency; quotes/receipts/causes own no duplicate stock or money. Existing high-level spot terms and atomic transfer arithmetic are reusable. A genuine paid world path needs its own evidence and admission, not forged gift approval or unrecorded currency replacement.
- Explicit new profile/codec capability; all old profiles, decoders, archives and canonical bytes retain their rules. Profile30's saved gifts must coexist with paid trades in the selected new interpretation. Do not silently enable it on old saves. Fresh admission and an actual lived-world migration are distinct; no retained migration is assigned. Profile28 pair composition is not required by this slice and must not appear accepted accidentally.
- Issuance and acceptance pass normal world authentication/control, expected actor revision, advancing-step/Wait/readiness, body/reach and work-opportunity rules. The payer and recipient are explicit people with actual storages/accounts. Both parties' current control and spatial capability must be valid at settlement. Conflicting same-batch work by the issuer and acceptance must have deterministic refusal/reservation semantics, as saved gifts do. Expiry uses the actual advancing world step; no wall-clock or polling progression. Fixed original terms, explicit total/fees/quantity, no hidden requote.
- Publish inventory, production, finite payment, quote state, causal evidence, receipts and identities as one candidate/SQL transaction. Every refusal/overflow/stale plan preserves all owners except the normal recorded world-step/time effects. Keep full/partial lot lineage, exact source intervals, reservations, protection and combined capacity. No minting, stock reset or account chosen merely to make tests pass. Fees must preserve finite supply and advance each touched account once.
- Currency-only trade must NOT invent zero damage, XP, actor revision or an empty legacy ResourceChangeBatch. Today WorldResourceAdmission requires progression revision = basis + actor batches and replays quest currency. Add an explicit coherent currency-only causal interpretation that orders paid settlements together with real actor-resource causes and reconstructs the exact ledger/account/progression revisions. Preserve legacy batches/old validation. Merely accepting a final ledger or hash is insufficient; test interleaving earned quest rewards, paid trades and actual healing/crafting. Avoid a second action journal or balance authority.
- Existing coordinated Production is not standalone V12. Do not turn on the standalone SupportsSpotExchange context inside a complete world or bypass UnsupportedHistory. Use an explicit complete-world interpretation/replay path and shared arithmetic where semantics match. Complete-world production revisions retain their meaning (craft/consumption, not arbitrary transfers).
- Bounded quote/settlement/resource state must continue through real coordinated cuts without losing open quotes, issuer revisions, funds, original offer/acceptance provenance or occupied identities. Closed history goes to authenticated archives; permanent identity lookup still prevents replay. Old anchors keep exact bytes. New state/step/anchor decoding must be canonical, bounded and profile-specific. A copied hash is never historical authentication.
- Host publication/recovery must replay actual saved commands, verify archive bytes and preserve receipts/identities and exact retry/lost-ack behavior. Existing schema8 may retain its physical layout if explicit interpretation checks can soundly support the new profile; never widen numeric ranges or accept arbitrary future versions. Journal/history reads must not skip authentication. If a required cross-cut or storage dependency cannot be completed within limits, fail closed, return the precise boundary and mark the overall delivery incomplete.

## Starting seams (navigation, not a substitute for inspection)

`WorldMaterialProposalResolver` is the closest spatial/work-opportunity template. `WorldStepProcessor` must roll every new owner back after later validation rejection and reserve issuer work only for success. `WorldResourceHistory`, `WorldResourceAdmission`, `WorldResourceHistoryCodec`, `ActorResourceHistory` and `WorldMaterialCareContinuation` currently assume actor batches; preserve their old meanings. `WorldCoordinatedContinuation`, `WorldMaterialCareArchiveAnchor`/Segment and profile30 codecs show open-offer carry. Hosting `WorldDatabase`, `SqliteWorldAuthority`, `WorldMaterialCareArchiveReplay`, `WorldJournalPolicy` and replay/codec dispatch contain explicit profile sets. Inspect all relevant callers instead of fixing only the obvious constructor.

The accepted component is at HEAD9867ecb; source/test evidence is in `../spot-exchange/INTEGRATION_REVIEW.md`. Its hosted boundary test uses `CareWorldFixture.CompleteQuest` to earn finite money and proves old worlds reject unrecorded payment. The original profile27 source database is immutable: do not open it with SQLite or mutate it. Use fresh admitted world fixtures for this assignment; they do not prove migration.

## Ownership

Edit only necessary integration files within:

- `Beta/Runtime/src/OpenFantasy.Core/Worlds/**`
- `Beta/Runtime/src/OpenFantasy.Core/Markets/**`
- `Beta/Runtime/src/OpenFantasy.Core/Production/**`
- `Beta/Runtime/src/OpenFantasy.Core/Progression/**`
- `Beta/Runtime/src/OpenFantasy.Hosting/Worlds/**`
- `Beta/Runtime/tests/OpenFantasy.Core.Tests/WorldExchange/**` (new tests only)
- `Beta/Runtime/tests/OpenFantasy.Hosting.Tests/WorldExchange/**` (new tests only)
- `Beta/Runtime/docs/architecture/world-paid-exchange.md` (new reference) and existing `spot-exchange.md`, `world-resource-history.md`, `coordinated-world-continuation.md`, `persistence-and-history.md`, `material-proposals.md`, `production-continuation.md`, `README.md` within that architecture directory
- `Beta/implementation/p02/paid-world/OPUS_RESULT.md`

No other source/client/assets/content, old test/fixture, project/build configuration, instructions, handoff, frozen spec, original save or predecessor edit is owned. If a necessary narrow source join lies outside this set, describe the exact file and required change in your result; do not use a reflection/test bypass. No unrelated style refactor. Supporting scripts are lead-owned. Core/application changes remain readable, object-oriented pure C#.

## Required evidence

Use new tests/fixtures that exercise complete WorldStepProcessor and real SqliteWorldAuthority, not only direct component planners. Cover raw and crafted payment/provenance, both quote sides, single-fill partial quantity, exact fees/conservation, actual earlier earned funding, issuer/counterparty control, not-ready/out-of-reach/dead/held actors, same-step work conflict, expiry, stale terms/lot, insufficient money/capacity, protected/reserved goods, atomic rollback, duplicate/conflicting identity, malformed canonical data and old-profile refusal. Also prove interleaved actor and currency-only history, a real automatic cut carrying an open quote, settlement after cut, closed identity non-reuse and save/reopen exact replay. At minimum, durable retry must return the original receipt without another transfer. Report live process-kill/power-loss evidence as unrun unless actually exercised; Astra can add integration qualification. Do not equate a passing codec roundtrip with authenticated history.

Keep all513 existing tests/fixtures,23 frozen specs, source database and unrelated staged index exact. Add meaningful new adversarial tests, never weaken old tests or increase existing deadlines/caps. Build/check commands below use the shared heavy-check lock and pin actual sources/assemblies; no overlapping build/test invocation. Observe terminal completion, not a background start banner. Read a concrete emitted log/report with Read if the native client backgrounds a call; do not start another check while it runs.

Allowed shell forms (Bash timeout600000, no additional shell commands):

```
python3 /var/folders/np/sh0qs32n2xlf150ph1vlcvrc0000gn/T/openfantasy-opus-paid-world-8fp7o5wy/worker_checks.py build
python3 /var/folders/np/sh0qs32n2xlf150ph1vlcvrc0000gn/T/openfantasy-opus-paid-world-8fp7o5wy/worker_checks.py focused-core
python3 /var/folders/np/sh0qs32n2xlf150ph1vlcvrc0000gn/T/openfantasy-opus-paid-world-8fp7o5wy/worker_checks.py focused-host
```

Use Read/Glob/Grep for exploration, not Bash cat/rg/pipelines. The previous implementation's optional shell-read denial and critic timeout are recorded; avoid repeating that friction. A required-operation denial ends that operation; no workaround escalation/retry.

Return a compact OPUS_RESULT with the actual design, exact changed-file groups, terminal check/report paths, known defects and missing proof, and recommendations for lead review. Update timeless architecture with code, keeping dates/progress in this result. A useful incomplete delivery is preferable to a false completion claim, but prioritize completing a coherent end-to-end world binding over scaffolding many disconnected layers.
