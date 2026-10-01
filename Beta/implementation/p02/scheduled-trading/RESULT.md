# Scheduled trading — result

Status: implemented and verified within the assigned scope; awaiting Astra review and integration.
Model route as dispatched: `claude-opus-5-5[1m]`, Max effort. No nested agents, retries, installs,
staging or commits. No Core, client, content, spec, predecessor or existing-test file was edited.

## Implementation map

Host source (`Beta/Runtime/src/OpenFantasy.Hosting/`):

| File | Change |
|---|---|
| `Simulation/PaidQuoteView.cs` (new) | `PaidQuoteStatus`, participant-safe `PaidQuoteView` with `PrepareAcceptance`, internal `PaidQuoteProjection` (live and archived; issuer revision only when mutually reachable) |
| `Simulation/LocalTradeInquiry.cs` (new) | `LocalTradeDirection`, `LocalTradeInquiry` (market, commodity, direction, units, own shown lot), `LocalTradeInquiryState`, `LocalTradeInquiryView` |
| `Simulation/LocalTradingPreferences.cs` (new) | Opt-in `LocalMerchantPreference`, `LocalShopperPreference` (authored stock target), `LocalTradingConfiguration` |
| `Simulation/LocalTradingController.cs` (new) | Transient FIFO inquiry queue, in-person gate first, merchant answers under own control, host shopper show/ask/accept, acceptance reservation, receipt-driven `Offered`/`Declined` |
| `Simulation/LocalTradingIdentity.cs` (new) | Stable semantic identities (one answer per inquiry, one acceptance per quote, one showing per lot state) |
| `Simulation/LocalInspectionSlot.cs` (new) | Small shared one-outstanding-lookup helper, now used by proposal and quote inspection |
| `Simulation/LocalWorldSession.Trading.cs` (new) | `SubmitTradeInquiryAsync`, `CancelTradeInquiryAsync`, `ReadTradeInquiry`, `InspectPaidQuoteAsync`, paid-intent profile validation, valid-acceptance reservation |
| `Simulation/LocalWorldSession.cs` | Trading-configured constructor overload; trading joins `SelectBatch` after gifts and before ordinary work; valid acceptances strip both participants' movement; commits feed receipts back; stop finishes both inspections |
| `Simulation/LocalWorldSession.ProposalInspection.cs` | Same semantics, now through `LocalInspectionSlot` and shared `ReadHistoryAsync` |
| `Simulation/LocalWorldView.cs` | `PaidQuotes` participant list |
| `Simulation/MarketTradingService.cs` | Snapshot-based `PrepareOffer`/`PrepareDisclosure` overloads (one pricing path), `ReadQuotes`, `FindRecordedQuote`; `OwnLot` internal |
| `Worlds/SqliteWorldAuthority.PaidExchange.cs` | `ReadPaidQuotesForController`, `ReadRecordedPaidQuoteForController` (authorize first, pinned-head replay) |
| `Worlds/WorldJournalRecovery.cs`, `Worlds/WorldMaterialCareArchiveReplay.cs` | Existing archive replay also selects one paid outcome from proven closing roots; no second archive owner |

Tests (`Beta/Runtime/tests/OpenFantasy.Hosting.Tests/WorldExchange/Scheduled/`, all new):
`ScheduledTradingWorld.cs` (composes the accepted `LocalMarketWorldFixture`: real reward, gifts, paid trade,
controller-route walking, backup-copy batch reads, real archive pressure), `ScheduledTradingSessionTests.cs`
(2 cases), `ScheduledNpcTradingTests.cs` (2 cases).

Architecture: new `Runtime/docs/architecture/scheduled-trading.md`; updated `world-markets.md`,
`world-paid-exchange.md`, `runtime-and-authority.md`, `presentation-and-interaction.md`, `README.md`.

## What the four new cases prove (actual session cutoffs, SQLite authority, manual time)

1. Working merchant (Wren at her funded worksite), player in person: work before inquiry; inquiry answered
   with a rule-priced sell quote while her work yields; participant view exact and credential-free, issuer
   revision observed; three idle cutoffs neither accept nor move money and Wren works again; separate
   confirmation fills, recorded batch holds only the acceptance and no participant movement, supply
   unchanged; unshown sell lot declined, shown lot bid with earned funds and settled; reopen reproduces the
   head, exact retry returns the original receipt, restart has no inquiry, live inspection reads `Filled`.
2. Stationary stall: paused session neither answers nor advances; duplicate/different inquiry handling;
   cancellation is final; distant real market and guessed market give the same `Unavailable`; unknown and
   foreign quotes are both null; foreign delivery refused; foreign/unshown lots and an unfunded stall get no
   bid; in-person sale funds the stall; a real gift makes the bid stale and acceptance is refused with owners
   unchanged; no requote and one standing offer; after expiry a new inquiry prices the changed stock and settles.
3. Host shopper (third human, target 0) and host merchant (Wren, working): shows its gifted lot (merchant
   keeps working), asks and is answered from the showing and Wren's earned funds, accepts under its own
   control; fill batch holds only that acceptance, no participant movement; balances/supply exact; shopper
   then idle and merchant works again; player cannot see the NPC quote. After a real automatic archive cut
   and reopening: player's archived fill via authenticated replay, NPC quote and unknown both null, merchant
   reads its archived NPC trade, foreign delivery refused.
4. Contention: the player's sell inquiry (admitted first) is answered before the host shopper's; while the
   player's valid acceptance reserves the merchant, the shopper's inquiry keeps its place; the next free
   cutoff answers it and the shopper settles; both bids come from the merchant's finite earned funds.

## Verification (exact wrapper commands, serial, terminal results observed)

Evidence root: `Beta/implementation/p02/scheduled-trading/qualification/`. Final, same build for both selections:

- `build-20261001T042054Z-b8b06`: accepted, 8.382s.
- `new-host-20261001T042105Z-3eed0`: accepted, **50 passed / 0 failed / 0 skipped** (46 prior exchange cases
  + 4 new), 16.495s.
- `session-host-20261001T042124Z-43834`: accepted, **127 passed / 0 failed / 0 skipped** (all `Simulation`
  cases, including the refactored proposal-inspection cases, + 4 new), 43.213s.

Earlier accepted runs on a three-case build are superseded but retained: `build-20261001T040907Z-43034`
(source only), `build-20261001T041654Z-1a0d4`, `new-host-20261001T041706Z-0c494` (49 passed),
`session-host-20261001T041756Z-3a05b` (126 passed). No run failed. Every report shows 532 existing tests,
23 frozen specs, the source database and the predecessor index unchanged, and sources unchanged during the run.
No test or assertion threshold was weakened. No indispensable Core change was found.

Process notes: before I noticed that the three wrapper commands are the only Bash grants, I made two
read-only `ls` listings (task packet, private task directory, runtime tree). All later discovery used
Read/Glob/Grep. Two wrapper runs used the shell's background mode; each was observed to its terminal
report before the next command started. No tool was denied.

## Policies made explicit

FIFO answers by admission order, one answer per merchant per cutoff, skipped inquiries keep their place;
canonical actor order only decides in-batch settlement. Inquiries: one waiting per shopper, 32-step fixed-world
lifetime, cancellable, no progress or expiry while paused, discarded on restart. One open offer per
merchant-shopper pair. Details in `scheduled-trading.md`.

## Genuine remaining gaps

- Inquiry lifetime expiry, a reading hold, a world-refused issuance and a refused host-shopper acceptance are
  implemented but not exercised by a case. A fill superseding another shopper's open offer from the same
  merchant follows the existing world rule; its participant view is untested here.
- The host shopper's buy direction is implemented but only its sell direction is exercised.
- Documentation link checks and an independent critique were not run in this task.
- Host shoppers and merchants do not walk to each other; no route, wage, cargo/escort, demand simulation,
  offscreen commerce or native trading menu exists.
- No independent review, process-kill or native evidence; full market, cargo/escort, multiplayer and
  P02/P00–P14 gates remain open.
