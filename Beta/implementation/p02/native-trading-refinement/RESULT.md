# Native trading refinement — RESULT

Status: **delivered for lead review.** All nine assigned checks are accepted on one final source/binary set.
Ran as `claude-opus-5-5[1m]` (effort is not observable from inside the run), about 07:14Z–07:56Z on
2026-10-01. Nothing is left running. Only the exact `run_checks.py` commands were used in Bash.

## Critique disposition

[INPUT_CRITIQUE.md](INPUT_CRITIQUE.md) was written before any runtime edit.

| # | Finding | Disposition |
| --- | --- | --- |
| 1 | Probe grabbed focus before every activation | Repaired. `MarketProbeDriver` reaches controls only with arrows or the D-pad along the panel's own neighbours, or by pointer clicks. Every move is traced From→To. |
| 2 | Dropdown rows used the keyboard in every mode | Repaired. Rows are reached, cancelled and chosen per route: Enter/Down/Esc, A/D-pad/B, or hover/click/click outside. The verifier rejects any row from another route. |
| 3 | No native refusal contrasts | Added three closed real-authority boundary journeys with independent journal authentication. |
| 4 | Cancel and focus-crossing unexercised; controller B could not dismiss a list | Repaired. The engine `ui_cancel` is Escape only, so the panel now closes an open list on the router's Cancel (B) through the popup's `WindowInput`. The stall stays open. Focus or pointer now crosses an enabled Confirm without submitting. |
| 5 | Focus loss is injected | Kept and labelled as injected. |
| 6 | Full views copied into every observation | Repaired. Observations are a compact hash plus actor, quote, lot, request, review and focus facts. Saves and journals are retained. |
| 7–9 | Steps, segments, revisions and multiplayer text in the ordinary card; "1 berries"; no shortfall shown | Repaired with player wording. The step basis is only a hover diagnostic and in the docs. |
| 10 | The stall's merchant was taken as proof of the stall | Commodity guard `MarketActionOffers.BelongsTo`. The remaining limit is recorded below. |
| 11–13 | Exact intent, timing, privacy | No defect found. |
| new (screenshots) | After the player's own fill, the card said "Cannot confirm: already filled". The post-ask notice said "world step". A 2,398-step showing read "about 120 s". | Repaired. The card now says "Done: …", the notice uses player wording, and durations switch to minutes from 90 s. |

## Changed responsibilities

- `MarketActionOffers.BelongsTo`: a quote belongs to the stall only if it is from the stall's merchant **and** for its commodity. Used by:
  - the open offer
  - the outcome list
  - saved-record text
  - `FarmMarketDraft.CaptureReview`
  - confirmation
- `FarmMarketTerms`:
  - player-language window (`Window`)
  - singular goods (`Count`/`Singular`)
  - purse and shortfall
  - `StatusWord`
  - hover-only `Diagnostics`
- `FarmMarketPanel` and its `.tscn`:
  - player wording throughout
  - diagnostics tooltip
  - controller-B list cancel
  - "Done" card after a fill
- `FarmGame.Market`: the post-ask notice wording.
- Probes:
  - new `MarketProbeDriver`, `MarketProbeEvidence` and `MarketBoundaryPlayProbe`
  - `MarketTradingPlayProbe` rewritten on the driver
  - `NativeTradingEntry` accepts `--trading-boundary-probe SAVE REPORT GENESIS expired|unfunded|moved keyboard`
- Tools:
  - `check_native_trading.py` now checks that each run used only its own route, requires the per-route lot cancel and choice, the outcome-row choices and navigation moves
  - new `check_native_trading_boundaries.py`
- New tests in `Client/Trading/Refinement/`:
  - `MarketWordingAndMatchingTests` (3 cases)
  - `NativeTradingBoundaryFixtureTests`
  - `NativeTradingBoundaryResultTests`
  - The test csproj is unchanged (0 links), and so are the existing tests and helpers.
- Docs:
  - `Runtime/README.md` Markets section
  - architecture `native-trading.md` (new "Native qualification binding" section and multi-stall limits), `presentation-and-interaction.md`, `runtime-and-authority.md`, `README.md`

## Final evidence (`qualification/`, all accepted, same source and binaries)

| Check | Report | Outcome |
| --- | --- | --- |
| build | `build-20261001T074610Z-5eb21/report.json` | accepted |
| unit | `unit-20261001T074941Z-65030/report.json` | 18 passed (15 existing + 3 new) |
| fixtures | `fixtures-20261001T074618Z-bf51e/report.json` (`trading/`, `gifts/`, `boundaries/`) | 3 passed |
| native | `native-20261001T074642Z-32cbe/native.json`; manifest `native-trading-bgycfq7r/native-manifest.json` | passed, 6 processes |
| native-auth | `native-auth-20261001T074824Z-4fd87/report.json` | 1 passed (unchanged five-command verifier) |
| boundary-native | `boundary-native-20261001T074732Z-9f7cf/native.json`; manifest `native-trading-boundaries-zc2gd7z6/native-manifest.json` | passed, 3 processes |
| boundary-auth | `boundary-auth-20261001T074902Z-91322/report.json` | 1 passed |
| regression | `regression-20261001T074951Z-64e5e/report.json` | 456 passed, 0 skipped |
| legacy-native | `legacy-native-20261001T075327Z-5aef1/report.json` | native passed for all three modes; 1 auth passed |

Every report keeps the original preservation: 546 existing tests, 23 frozen specs, protected bytes, the source DB, the predecessor index and the checkpoint.

**What the normal journey exercises.** In each of keyboard, controller and pointer:
- The wheel opens, pages, selects and confirms on the named route.
- Keyboard and controller runs make 88 traced focus moves each, starting only from focus the game set itself.
- The lot list is opened, reaches the bought lot's row, is **cancelled** (nothing chosen, stall open, no command), then the same row is **chosen** on the route.
- The outcome list is chosen on the route in the active run and again in the paused reopen.
- Focus or the pointer crosses an enabled Confirm and leaves without submitting.
- A repeat on the filled offer's disabled Confirm sends nothing.
- The unchanged `NativeTradingResultTests` still authenticates exactly five command rows.

**Boundary journeys** (keyboard route). Each starts from a closed save prepared through ordinary session cutoffs, with the authored 1,200-step window:

| Journey | Refusal and attempt | Recovery | Authenticated |
| --- | --- | --- | --- |
| expired | Earned purse, Wren in reach, 40 steps left. Reviewed while paused, resumed; it expired at step 1,498. The attempted Confirm sent nothing. | A new request; Wren's *new* quote was reviewed and confirmed. The old quote stays Expired. | Recorded state at the attempt shows it due, funded and reachable; Wren's reissue command; one acceptance. |
| unfunded | Purse at its source zero; the card reads "You need 16 gil; you have 0". The attempted Confirm sent nothing. | Native talk, accept, deliver and continue earned 200 gil; Wren went to work and returned; the *same* offer was re-reviewed and confirmed. | Player commands are exactly [speak, quest, quest, close, accept]. Recorded state at the attempt shows the purse short of the total, quote open, Wren in reach and stock present. |
| moved | Funded, offer open. Reviewed beside Wren; on resume she walked to work; the card reads "Wren moved since you reviewed this offer". The attempted Confirm sent nothing. | After she returned, an explicit re-review captured her new presence revision and the same offer was confirmed. | Recorded Wren revision at the attempt differs from the reviewed one. The only player command is that acceptance, restating the new revision. |

**Images to inspect:**
- `native-20261001T074642Z-32cbe/native-trading-bgycfq7r/{keyboard,controller,pointer}.{buy-offer,buy-review,buy-outcome,sell-review,sell-outcome,history,final,reopen,reopen-final}.png`
- `boundary-native-20261001T074732Z-9f7cf/native-trading-boundaries-zc2gd7z6/{expired,unfunded,moved}.{review,refused,recovered}.png`

I inspected screenshots myself, with no clipping found at 960×540. This is not human aesthetic acceptance.

## Failures and friction (all retained)

- `build-…073517Z-d0804`, `…073731Z-e3350` and `…074551Z-6cd58` failed on a Godot import message: `EditorSettings not instantiated yet … android_sdk_path`. Each came on the first import after a client assembly change; the identical rerun passed each time. No code change was involved, and I did not investigate the editor cause.
- `native-…073619Z-affa7`, pointer route: the popup ignored the injected hover. The trace showed zero-velocity motion, which a moving device never produces. The driver now sends the real relative motion and velocity.
- `native-…073815Z-f7d17`, pointer route: my list-cancel check compared a whole-view hash while time was running, and the slower hover sweep let a step advance. I replaced it with the facts a command would change: quotes, lots, showing, pending action and selection.
- `regression-…074256Z-72529`: 456/456 passed, but it is not accepted because I edited copy while it ran. The final regression above is the accepted one.

## Honest limitations

- These are injected events in unfocused background windows. No physical devices, OS focus, multiplayer Wait, art, audio, human comprehension or production gate is claimed.
- Focus loss is injected through the fixture hook.
- All three boundary journeys share the keyboard route.
- A refused confirmation is never submitted, so there is no world-refusal receipt. The managed tests establish the reason from the recorded state at the attempt, located by its advancing step.
- In the expired and unfunded journeys the attempt happened within the same or the next step as the refusal appeared. That is a valid moment, but not a long dwell.
- Multi-stall support:
  - `PaidQuoteView` carries no market identity, so two stalls of one merchant selling the same commodity are still indistinguishable.
  - `ContextStall` addresses the first stall available in person.
  - Both are one-stall bindings, documented in `native-trading.md`. A real fix needs a stall identity on the Host quote view, which is outside this edit scope.
- The singular rule covers English "-ies"/"-s" plurals only; localization is out of scope.
- After a fill, the request line still restates the last request ("Wren offered terms"). The new "Done" card and the notice carry the outcome.
- History details can need scrolling inside the reply column.
- Archived-only outcomes are still not listed. Lived-save migration, the cargo circuit, demand and all phase gates remain open.
