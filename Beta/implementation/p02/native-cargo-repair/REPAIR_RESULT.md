# Native cargo repair result

Status: **complete for review**. All eight granted checks pass on one final build. Model: claude-opus-5-5[1m],
requested Max. No commit, Git command, nested agent, fallback or retry. Only the eight granted commands were run;
there were no permission denials. Astra owns review, integration and commits.

Paths below are relative to `Beta/implementation/p02/native-cargo-repair/qualification/` (Q).

## Final accepted checks (build `a13cd62b…`, all `accepted: true`, all preservation intact)

| Check | Report | Result |
| --- | --- | --- |
| build | `Q/build-20261001T121634Z-336ba/report.json` | solution + client + Godot import, 10.4s |
| unit | `Q/unit-20261001T122504Z-2db38/report.json` | 22 passed |
| regression | `Q/regression-20261001T122516Z-f1f31/report.json` | 502 passed, 177s |
| fixtures | `Q/fixtures-20261001T121647Z-aed32/report.json` | cargo, trading and gifts exports, 3 passed |
| native | `Q/native-20261001T121703Z-94270/report.json` (+ `cargo.json`) | passed, 433.0s of 900s |
| native-auth | `Q/native-auth-20261001T122419Z-d2b72/report.json` | `NativeCargoResultTests` passed |
| legacy-native | `Q/legacy-native-20261001T122817Z-825a8/report.json` | three-mode trading and gift proofs passed, 81.7s |
| legacy-auth | `Q/legacy-auth-20261001T122941Z-56958/report.json` | 2 passed |

Preservation held in every run: 561 old tests, 23 frozen specs, Core, protected application files, copied art,
source database, predecessor staging and checkpoint. The only new compile links are the original 4. Core was
not rerun; it is unchanged and pinned.

Native timings (work dir `Q/native-20261001T121703Z-94270/native-cargo-g7o_q616/`):
- Active keyboard route: 385.9s process (750s limit), probe 381.9s (720s limit).
- Paused reopen: 12.7s process (90s limit); the opening finished inside the unchanged 30s deadline.
- Onboarding via production `--cargo`: 32.3s process (new limits 330/300s).

The 900/750/720/90/30s deadlines and the old trading/gift deadlines and assertions are unchanged.

### Retained failures (all kept, none converted)
- `Q/build-20261001T120349Z-2a28e`: compile errors in my first draft (int/ulong conversions, a stale `Regions`
  reference, an internal Core helper, xUnit analyzers).
- `Q/native-20261001T120547Z-d268f`: the lead's HUD-aware camera assertion failed at `farm-start`. The probe sampled
  the frame after a HUD relayout and before the game re-framed the camera. Correction: `Agree` waits at most six
  frames for the next presentation, then runs the unchanged exact assertion.
- `Q/native-20261001T120721Z-0f1e5`: my own history-read assertion was too strict. One cutoff that was already
  committing published before the read took the slot. It now allows at most that one in-flight step.
- `Q/native-20261001T120846Z-4b62c`: one long onboarding leg hit the route search's ExpansionLimit. It now walks
  ≤8-cell authored lane legs, and the walk to Marla retraces proven paths instead of crossing the opened divider.

## Findings disposition

**F1 — fixed.**
- The render path no longer touches the authority lock.
- `LocalRegionObserver` holds no authority. The regional `LocalWorldSession` constructor projects it in
  `ProjectView` at each publication, so `LocalWorldView.Regional` comes from the same root as the view's actors and
  offers. It is `[JsonIgnore]`, so public digests are unchanged.
- The region cache is keyed by bounds, revision, floor and stacked-region revisions. There is no database read per
  frame.

Evidence:
- Managed: `RegionalFramePublicationTests.Frame_reads_stay_responsive_while_a_real_recorded_inspection_holds_the_writer_gate`.
  - A real `InspectPaidQuoteAsync` is held inside the writer gate through the internal seam
    `SqliteWorldAuthority.RecordedQuoteReadHoldsGate` (granted PaidExchange file; null in hosts).
  - Counterexample: `ReadStateForHost`, which the old frame path called, stays blocked for 400 ms.
  - Meanwhile 240 frame reads (`ReadView`, `Regional`, run state, inquiry) finish in under 250 ms and return the
    same immutable objects.
- Native: at Brasshollow, `VerifyRecordedHistoryAsync` ran a complete verified replay to commit 240 in the
  background history slot.
  - It took 0.441s; 55 frames rendered, the longest 14.2 ms.
  - The world held step 237 throughout, and the replay reproduced the head (`HistoryRead` in `keyboard.active.json`).
  - Limit: this journal is short. A tens-of-seconds archived replay was not exercised natively; the managed seam
    covers an arbitrarily long hold.

**F2 — fixed.**
- What the observation now shows:
  - Ground kinds `Surface`, `Soil` and `Stone`; `Raised` when the cell above the floor is solid (a blocker, not
    walkable ground); `Open` with known depth read through stacked regions.
  - Per-column water: volume and exact fill.
  - The player's own work targets, carried on regional frames. The excavation cue sits on the worked column.
- Actions and authority are unchanged, and the fixed farm/worksite guards are untouched.
- Unsupported views are recoverable: the last frame is kept, the HUD posts a notice, nothing latches `Fail`, and
  the next supported view recovers. Actors and observation share one root, so the own-actor crash path is gone.
- The observer no longer demands solid terrain under the feet, so admitted structure support stays presentable.

Tests:
- `The_cargo_genesis_observation_presents_the_breach_channel_water_divider_and_work_target`:
  - Upper channel x110–115 is open at depth 1 and full, with the surface at the floor.
  - The divider is dry standing ground and is the work target.
  - The receiver (6 columns) is open at depth 2 and dry.
  - The banks are ground.
  - Every observed column's class is checked independently against the terrain; observed water equals total water.
- `Legal_floors_stay_presentable_and_unsupported_stances_are_an_explicit_absence`:
  - The receiver's lower floor −256 is observed alone.
  - This terrain-only world's spatial admission refuses stances over openings or between floors.
  - An absent player gives null.
- `Ordinary_earned_mining_and_released_water_reach_the_published_regional_view`:
  - Ordinary movement, three earned works, then water.
  - Every published view contains the player's own feet.
  - The divider is open at the authority's revision, and receiver water is below the floor.
- Native onboarding:
  - Q cued the divider's own column, three wheel mining actions opened it, and the drawn terrain changed.
  - A water pixel was sampled.
  - Images: `onboarding.bank.png`, `onboarding.water.png`, `onboarding.water-sample.png`.
- `NativeCargoResultTests` authenticates from the onboarding journal: the divider removed, receiver water and
  conserved total water.

Gaps:
- Unsupported regional views are unreachable in this world, so the recovery path is exercised by neither test
  nor native run. The `RegionalFallbacks` counters exist but are unobserved.
- No `Raised` column was specifically asserted to exist in the cargo world.

**F3 — fixed.**
- `FarmMarketDraft` gained `Look`, `ReleaseLook`, `IsExplicit` and `ChosenMarketId`. `PinsOnOpening` pins a stall
  only when it is chosen, in reach, or the world's only stall.
- Out-of-reach blockers name a stall that is in reach and the Offers → Stall route, without retargeting. The
  selector stays on its prompt while a stall is only being looked at.
- Tests: `CargoStallChoiceTests` (farm look/release, explicit never downgraded, blocker text; the single-stall
  world unchanged).
- Native:
  - `farm-offers`: nothing pinned, the selector is at the prompt, Ask is disabled, and closing forgets the stall.
  - At Brasshollow the wheel addresses Marla and opening pins her.
  - At Gearford with Marla pinned, the blocker reads "Marla is not within reach. Kegga Toolwright's stall is here:
    open Offers and choose it under Stall." (`StallChoice`). Images: `keyboard.farm-offers.png`,
    `keyboard.gearford-stall-choice.png`.
- Review, cancellation and exact parties are unchanged; they are authenticated by the result test.

**F4 — fixed.**
- `CargoProductionSelectionTests` (through the real `OpenCargoAsync`):
  - Fresh create, then same-world reopen.
  - A foreign seed (different world and branch identities) is refused with the save bytes unchanged.
  - Another world's save (`--markets`) is refused with its bytes unchanged.
  - Gap: a same-world, different-branch save was not separately constructed.
- Native:
  - `--cargo-onboarding-probe SAVE REPORT` builds the same `FarmGame { CargoProfile }` as `--cargo SAVE` on a fresh
    disposable path.
  - E/G/G/E talked to Wren and accepted and delivered her quest; the earned reward was 200.
  - The player mined, then walked to Marla, whose stall is available in person.
  - `NativeCargoResultTests.VerifyOnboarding` re-derives the production genesis and authenticates Speak, two quest
    intents, three Excavates, no trade intents, cardinal steps, reward, divider, water and the Marla locality.
- README: Marla is down-screen (increasing Y), Gearford a long way up-screen (decreasing Y). The controls are
  documented.

**F5 — fixed.**
- The lead's framing checks are retained. Mid-walk samples additionally require the actual node transform to
  trail its projection by at most one interpolation.
- `CargoRenderedEvidence` adds checks at settled points (farm start, three arrivals, reopen, five onboarding
  points):
  - The settled transform equals the projection exactly.
  - `CargoTerrainView.LastDrawn` (shape, revisions and per-column choice digest) equals the presented observation.
  - Framebuffer pixels: the player's column is not the clear colour; the sprite differs from ground and clear;
    visible padding equals the clear colour; water is bluer than ground.
- `NativeCargoResultTests` asserts every recorded player sample in both journals is cardinal with exactly 32
  subunits.
- The literal `FocusRequests = 0` is removed. Reports record `Focus` (Unfocusable, NativeFocused,
  ObservedNativeFocus, ObservedFocusTransitions = 0 over 45,569 sampled frames). The tool's
  `focusSourceAudit` is labelled a source audit.
- Pixel checks are heuristics, not visual acceptance.

**F6 — fixed.** `PickCargoStall` first requires a live, non-closing game, the router's World layer and
`Permits(NextTarget)`: focused, ready, no dialogue or reading hold, no encounter, no pause, no modal. Selection
stays non-committing. It has no native pointer route (keyboard qualification only).

## Changed responsibilities (Beta-relative)

- **Host:**
  - `Simulation/CargoPresentation/LocalRegionObserver.cs` and `LocalRegionObservation.cs`.
  - `Simulation/LocalWorldView.cs` (`Regional`, `IsRegional`).
  - `Simulation/LocalWorldSession.cs` (regional constructor, publication).
  - `Simulation/LocalWorldSession.ProposalInspection.cs` (`VerifyRecordedHistoryAsync`, `LocalHistoryVerification`).
  - `Worlds/SqliteWorldAuthority.PaidExchange.cs` (test seam only).
- **Client:**
  - `FarmGame.cs`, `FarmGame.Cargo.cs`, `FarmGame.Market.cs`, `FarmClientSession.cs`, `FarmCanvas.cs`.
  - `Presentation/FarmCanvasFrame.cs`, `FarmMarketDraft.cs`, `FarmMarketPanel.cs`, `MarketActionOffers.cs`,
    `NativeCargoEntry.cs`.
  - `Cargo/CargoTerrainView.cs` and `Cargo/CargoRoutePlayProbe.cs`.
  - New: `Cargo/CargoRenderedEvidence.cs` and `Cargo/CargoOnboardingPlayProbe.cs`.
- **Tests:**
  - New `Simulation/CargoPresentation/RegionalFramePublicationTests.cs`, `Client/Cargo/CargoStallChoiceTests.cs`
    and `Client/Cargo/CargoProductionSelectionTests.cs`.
  - Updated `CargoViewProjectionTests`, `CargoStallIdentityTests` (API call only) and `NativeCargoResultTests`.
- **Tool:** `tools/check_native_cargo.py` (onboarding phase, focus state and source audit, rendered and history
  checks, manifest `onboarding`).
- **Docs:** Runtime README; architecture `cargo-presentation.md`, `native-trading.md`,
  `presentation-and-interaction.md`, `runtime-and-authority.md`; packet `DESIGN.md`.
  `world-markets.md`, `cargo-circuit.md`, `scheduled-trading.md` and `content-and-composition.md` need no change.

## Remaining limits and gates
- `LocalRouteAdvice` is used by probes only. It still reads under the gate on the main thread and is never called
  during the history-read measurement.
- Publication still builds the inherited sight-complete Worksite and Liquids views over all regions; that cost
  existed before and is not per frame.
- Still open: controller and pointer cargo routes, physical devices and OS focus, human art and comprehension, a
  long archived-replay native sample, multi-floor cutaway, streaming and frontier, threat/escort, NPC travel,
  multiplayer and P02.
