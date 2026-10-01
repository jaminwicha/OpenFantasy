# Input critique: native trading foundation (`35b0797`, foundation `ff120fb`)

Independent review by `claude-opus-5-5[1m]` before any runtime edit. Paths are under `Beta/Runtime/`.
Priorities: **P1** blocks a required outcome or misstates evidence; **P2** player-facing or architectural
defect; **P3** economy/clarity. "Counterexample" records what I tried; "not found" is stated where an attack failed.

## Evidence claims versus exercised behavior

1. **P1 — every activation manufactures focus.** `MarketTradingPlayProbe.Activate` calls `button.GrabFocus()`
   before each Enter / controller A / click (`client/scripts/MarketTradingPlayProbe.cs:312-329`), `Attempt` does
   the same (`:332-347`) and `Choose` grabs the dropdown (`:294-309`). No step proves that the panel's own focus
   chain (`FarmMarketPanel.WireFocus`, `client/scripts/FarmMarketPanel.cs:333-353`) reaches any control, so the
   "arrows / D-pad / Tab move" hint in `scenes/FarmMarketPanel.tscn:316` is unexercised. Pointer runs also
   pre-focus a button before clicking it, hiding whether a click alone focuses and activates.
2. **P1 — dropdown rows are keyboard in every mode.** `Choose` taps `Key.Enter`/`Key.Down` regardless of route
   (`MarketTradingPlayProbe.cs:297-306`); `check_native_trading.py:35-41` excludes those rows from coverage and
   `:143` labels it. Controller (D-pad/A in the popup) and pointer (hover/click on a popup row) row selection are
   therefore unproved. The `GetFocusedItem` loop reads the popup correctly; it is not itself a manufactured input.
3. **P1 — no native refusal contrasts.** Expiry, insufficient funds and a moved issuer are covered only by
   managed cases (`tests/.../Content/Trading/NativeMarketSessionTests.cs:51-150`) and blockers in
   `MarketActionOffers.ConfirmBlocker` (`client/scripts/MarketActionOffers.cs:129-150`). Wren never moves after
   review in any run. The managed expiry test proves the blocker and no requote, not the native disabled-button path.
4. **P2 — cancel/repeat coverage is partial.** The only repeat is one route event on the filled offer's
   disabled Confirm (`:142`, `:332-347`). Cancelling an open dropdown (Esc / controller B / click outside) and
   moving focus *across* an enabled Confirm without activating it are never exercised. Counterexample to check
   natively: if a cancel press reached `FarmGame._UnhandledInput` while a popup is open it would close the whole
   stall (`FarmInputRouter.Bind`, `client/scripts/FarmInputRouter.cs:424`) rather than the popup; if controller
   B is not a `ui_cancel` event the popup could not be dismissed without choosing a row. Neither is evidenced.
5. **P2 — focus loss is a direct handler, not a window event.** With `BindWindowFocus=false` the fixture hook
   calls `OnFocusExited` directly (`client/scripts/FarmGame.cs:71-78`). That is honest as an injected boundary
   but must keep being labelled as such; it is not OS focus.
6. **P3 — evidence duplication.** `Observe` serializes the complete `LocalWorldView` (terrain and all actors)
   into every observation (`MarketTradingPlayProbe.cs:359-360`); no verifier reads those copies
   (`NativeTradingResultTests` reads hashes/ids only). Hashes plus the relevant actor/quote/lot/input facts
   suffice; the retained canonical save/journal stays the identity proof.

## Player-facing copy and clarity

7. **P2 — implementation language in the ordinary flow.** The status line prints `step N` and pause help says
   "It is not a shared multiplayer Wait" (`FarmMarketPanel.cs:148-159`); the hint repeats it (`.tscn:316`);
   history says "this save segment" and "closed at world revision N" (`FarmMarketPanel.cs:134`, `:253`,
   `.tscn:291`); terms use "more world steps", "Offered N steps ago" and raw enum `Status: Open`
   (`FarmMarketTerms.cs:52-56`, `:74-79`); request status counts "steps" (`:104-107`); the showing line names
   "until step N" (`FarmMarketPanel.cs:207`). Price, total, purse-after and the pause consequence are good and
   must stay; the step count should become remaining time in player terms, with the step basis in docs/tooltip
   diagnostics only.
8. **P2 — singular goods.** `"{units} {goods}"` everywhere (`FarmMarketTerms.cs:34-35`, `:50-51`, `:72-73`,
   `:91-94`; `FarmMarketPanel.cs:174`, `:186`; `MarketActionOffers.cs:96-121`) yields "1 berries".
9. **P3 — affordability is not in the offer card before review.** The purse appears only inside the long
   `Describe` text; a short shortfall line belongs next to the total so an unaffordable offer is visible before
   review (the blocker already exists for Confirm).

## Quote/commodity matching and future multi-stall support

10. **P2 — merchant identity is treated as stall identity.** `MarketActionOffers.OpenOffer` (`:73-75`), the
    history rows (`FarmMarketPanel.cs:245-246`) and `FarmMarketDraft.CaptureReview` (`FarmMarketDraft.cs:103-106`)
    accept any open quote whose issuer is the stall's merchant. With a second stall or commodity for the same
    merchant, a berry stall would list, review and label (`FarmMarketTerms.Outcome` uses the stall's goods name)
    another commodity's quote. `PaidQuoteView` carries `Definition` but no market identity, so commodity matching
    is a safe local guard; two stalls of one merchant and one commodity remain indistinguishable in this view.
    `ContextStall` also picks "first available, else first" (`:27-28`), which is only correct for one stall.

## Exact intent, timing and privacy (attacks that did not find a defect)

11. **Exact intent — not found.** Confirm restates the reviewed quote hash, units and observed issuer revision
    (`MarketActionOffers.cs:104-150`; `PaidQuoteView.PrepareAcceptance`); any choice change, Back, focus loss or
    disconnect discards the review (`FarmMarketDraft.cs:53-115`, `FarmGame.cs:532-570`). A fresh command id per
    presented frame is harmless: one pending action blocks a second submission (`FarmGame.cs:470-483`). I found
    no path where refresh swaps the reviewed quote; `Present` restates `current` but blocks on any hash change.
12. **Timing — not found, with one note.** The panel never changes readiness; pause is explicit; a request
    asked while paused waits (`FarmGame.Market.cs:67-88`). Note: the confirm card shows remaining time only as
    steps; it should state the real-time consequence (pausing stops the window) in player words.
13. **History privacy — not found.** Rows come from participant-projected `PaidQuotes` only, saved-record
    reads use the participant-scoped background lookup (`LocalWorldSession.Trading.cs:62-71`), and no stock,
    funds or credentials are shown. Archived-only discovery is still not surfaced (documented).

## Disposition plan (priority order)

P1: replace probe focus grabs with real navigation (keyboard arrows, controller D-pad, pointer clicks), route
dropdown rows per mode (keyboard Enter/Down, controller A/D-pad, pointer hover/click), add dropdown cancel and
focus-crossing checks; add three native boundary journeys (near-expiry, unfunded, moved issuer) on closed
real-authority fixtures with independent journal authentication. P2: player-language copy, singular goods,
commodity guard. P3: compact observations. Physical devices, OS focus, multiplayer Wait and human acceptance
remain open whatever the outcome.
