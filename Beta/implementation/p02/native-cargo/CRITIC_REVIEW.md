# Native cargo review (snapshot 6061a440)

Status: **complete**. This is a source review of `snapshot/` and `delivery.diff`. Nothing was run. Paths are relative to `snapshot/Beta/Runtime/`. I left out the issues you already know about: the 30→85 s reopen wait, the HUD covering the edge, the incomplete proofs, and the three route attempts.

## Findings, in priority order

### F1 (High). Every cargo frame now takes the authority writer lock, so long host reads freeze the whole client
- **Evidence:**
  - Each frame, `FarmGame.Cargo.cs:72` calls `LocalRegionObserver.Observe()`.
  - That call (`src/OpenFantasy.Hosting/Simulation/CargoPresentation/LocalRegionObserver.cs:33`) runs `ReadStateForHost()`, which takes `lock (_gate)` (`src/OpenFantasy.Hosting/Worlds/SqliteWorldAuthority.cs:205-211`).
  - Before this change the frame path was lock-free: `LocalWorldSession.cs:95` reads with `Volatile.Read`, and `FarmGame.cs:378` says so.
  - The same lock is held for every commit (`SqliteWorldAuthority.cs:224-265`, including checkpoint writes).
  - Archived quote inspection also holds it. `SqliteWorldAuthority.PaidExchange.cs:82-113` now runs **two** complete verified replays inside the lock. The second, `ReadRecordedState`, is new in this delivery.
  - Coordinated cuts happen automatically once bounds are reached (`src/OpenFantasy.Core/Worlds/WorldMaterialCareContinuation.cs:20-28,65-103`), so archived inspection is part of ordinary long play.
  - RESULT.md reports that one full recovery of the ~7k-commit cargo journal took more than 30 s.
- **Consequence:**
  - While walking, each frame waits for SQLite commits, which can cause stutter.
  - During an archived record read, rendering and input stop for about two replay lengths, and the "Reading the saved record…" text never appears.
  - The architecture promise that a recorded read "is never part of a frame" (runtime-and-authority.md) is broken indirectly.
  - Not established: whether the panel can reach the archived path other than through a row whose quote was cut while selected.
- **Fix:** take the observation from a snapshot that the session publishes lock-free (the same root as `_view`), or project it on the session thread. Never call `ReadStateForHost` from `_Process`.
- **Proof needed:**
  - A managed test that holds `_gate`, for example during a stub replay, and shows `Observe` does not block.
  - A native frame-time sample taken while a recorded read runs.

### F2 (High). Fail-closed is a permanent session kill, and the liquids and excavation actually in this world are silently ignored
- **Evidence for the permanent kill:**
  - When the observation is null, `FarmGame.Cargo.cs:72-75` calls `Fail(...)`.
  - `Fail` (`FarmGame.cs:603-614`) sets `_failed`, marks input unavailable and stops readiness. After that, `_Process` returns early on every frame (`FarmGame.cs:262`), so the session is unusable until the game restarts.
  - `LocalRegionObserver.cs:50-63` returns null whenever any of these hold:
    - the player's Z is not aligned to a cell;
    - the cell under the feet is not solid terrain (for example, structure support);
    - no single region holds both the ground cell and the cell above it.
  - No test shows which states that the world allows reach these conditions in this farm.
- **Evidence for ignored liquids and excavation:**
  - The cargo farm is the breach farm. `EncounterFarmAdmission.cs:33` uses `BreachFarmBuilder`, which builds an excavation site in `RaisedBankTop` mode (`BreachFarmBuilder.cs:20,69-71`). The breach receiver region lies below the farm (`tests/.../Content/Cargo/CargoCorridorRouteTests.cs:27,192`).
  - The cargo genesis keeps `liquidCells`, the reservoirs and `excavation` (`src/OpenFantasy.Hosting/Content/Cargo/CargoGenesisExtension.cs:90-94`). As a result, `LocalWorldView.Liquids` and `Worksite` are non-null (`LocalWorldView.cs:115-116`).
  - `FarmGame.Cargo.cs` never reads `Liquids` or `Worksite`. The new doc nonetheless says "liquids and excavation presentation in this world are refused rather than approximated" (cargo-presentation.md, Boundaries).
  - Excavation targets ("Bank face", "Opening") can still be selected with Q (`FarmTargetSelection.cs:36-39`), and their actions are still offered. The player can change farm terrain, but the cargo view shows only dark `Open` cells. Any water would not be drawn at all.
  - The ground sample reads one layer only (`LocalRegionObservation.cs:35-42`), so a column that is solid above the floor would be drawn as walkable ground.
- **Secondary crash path:**
  - Actors come from `ReadView()`, but the observation comes from the authority head.
  - On any Z change between those two reads, the player is filtered out of the frame. `FarmCanvas.cs:118` and `FarmCanvas.cs:33` then throw on `.Single(IsOwnActor)` as an unhandled exception in `_Process`.
- **Fix:**
  - Either present liquids and excavation through the regional projection, or explicitly withhold the excavation offers and targets and refuse a frame that has wet cells, with a message.
  - Make an unsupported observation a recoverable state: keep the last valid frame, show a notice, and do not set `_failed`.
  - Build actors and terrain from the same root.
- **Tests needed:**
  - A managed test showing the cargo-genesis view has `Liquids` and `Worksite`, and asserting the chosen policy.
  - Observation tests at the mining stance, at the north and receiver banks (`BreachFarmLayout.cs:22-24`), and next to the excavated divider and the dry channel.
  - A test with the player at a lower Z and on structure support, asserting the behaviour is not fatal.

### F3 (Medium). A fallback stall becomes a sticky "explicit" choice, and the UI never points to the Stall selector
- **Evidence:**
  - The wheel's Offers entry is always executable; its blocker is null (`MarketActionOffers.cs:51-53`).
  - With no choice yet, `OpenMarket` (`FarmGame.Market.cs:43-51`) falls back through `ContextStall` (`MarketActionOffers.cs:27-28,35-36`) to "first stall in identity order". `MarketId` is random, so that can be Kegga. `Begin` then records it as the choice.
- **Trigger:** on a fresh game, press V, Q and open Offers at the farm, close the panel, then walk to Marla.
- **Consequence:**
  - Buy and Sell on the wheel stay addressed to the out-of-reach stall ("must be within reach…", `MarketActionOffers.cs:182`) while Marla stands next to the player.
  - At Gearford after buying from Marla, the same thing happens with Kegga.
  - Neither the blocker nor the HUD text (`FarmGame.cs:246`) mentions Offers → Stall.
  - The probe never covers this, because its first panel opening is at Brasshollow (`client/scripts/Cargo/CargoRoutePlayProbe.cs:144`).
- **Fix:**
  - Pin a stall only from the selector, a pointer pick, or a stall that is in reach when the panel opens.
  - When the pinned stall is out of reach and another stall is in reach, name that stall and the selector path in the blocker, still without retargeting.
- **Tests needed:** opening Offers at the farm and then arriving at Brasshollow; the wheel blocker text at Gearford.

### F4 (Medium). The ordinary `--cargo` entry and its save contract are unproven; success so far is fixture-only
- **Evidence:**
  - Nothing in the snapshot calls `FarmClientSession.OpenCargoAsync` (`FarmClientSession.cs:142-152`), and the production seed `"cargo-circuit-world"` (`NativeCargoWorld.cs:35`, used at `FarmGame.Cargo.cs:52`) is never composed in any test or tool.
  - There is no selection test for creating a fresh save, reopening the same world, or refusing another world, seed or branch without changing it. Other profiles have such tests: `RetainedGiftSelectionTests`, `CoordinatedNativeSelectionTests`.
  - The native run uses only `--cargo-probe` with `OpenCargoFixtureAsync`.
  - The fixture's quest is earned through managed `CargoJourneyWorld.SubmitOffer`, without targeting, the wheel or the HUD. So native onboarding in this world (Wren's quest, wood, the first walk) is untested.
- **README directions:**
  - README.md:~690 says "walk south to Marla … east … and south to Kegga".
  - The lane goes to +Y for Marla (y=349), then −Y down to y=141 for Kegga (cargo-circuit.md:101).
  - With the Y-down projection (`client/scripts/Cargo/CargoViewProjection.cs:6`), the two "south" legs point in opposite screen directions; the second one goes up the screen.
- **Fix and proof:**
  - Managed `OpenCargoAsync` tests: create, reopen, and refuse with the save bytes unchanged.
  - A short native `--cargo` smoke run on a fresh disposable save: opening, HUD, the quest through the wheel, and reaching Marla.
  - Correct the directions.

### F5 (Medium). The agreement evidence is mostly true by construction, and some assertions are empty
- **Evidence:**
  - "Drawn exactly at its projection" compares `Project(feet)` with `FarmActorView.ProjectedTarget`. That value is `_target` from the same projection (`FarmActorView.cs:~27`, `CargoRoutePlayProbe.cs:368-370`). It never samples the interpolated `Position` or any pixels.
  - The terrain check is a visibility flag (`FarmCanvas.cs:49`) plus a read of the observation itself (`CargoRoutePlayProbe.cs:371-373`). It never checks what `CargoTerrainView` last drew (`CargoTerrainView.cs:27-36`).
  - The camera "frames" check allows a quarter of the viewport (`CargoRoutePlayProbe.cs:378-379`).
  - `FocusRequests = 0` is a hard-coded literal (`CargoRoutePlayProbe.cs:485`). It is then "verified" by `tools/check_native_cargo.py:49-52` and `NativeCargoResultTests.cs:200`. Other probes inherit the same pattern.
  - The comment "one ordinary cardinal step" in the result test is backed only by a check of the delivery principal and session (`NativeCargoResultTests.cs:124-128`).
- **Consequence:** a stale terrain redraw, the wrong tile class, or broken actor interpolation at a region boundary would still pass.
- **Fix:**
  - Expose the shape and revisions `CargoTerrainView` last drew and compare them with `frame.Regional.Observation`.
  - Sample the settled actor `Position` and framebuffer pixels in the player's column.
  - Assert that each movement sample is a cardinal step of 32 subunits.
  - Measure focus requests or drop the claim.

### F6 (Low). Pointer pick bypasses the input router's checks
`_UnhandledInput` runs `PickCargoStall` before `_input.Route` (`FarmGame.cs:366-372`). The pick checks only whether the market, wheel or inventory is open (`FarmGame.Cargo.cs:115-116`). It ignores `_failed`, dialogue or reading hold, encounters and focus. It resets the draft and calls `Present` again, which can call `Fail` again after a failure. No command is sent, and the pointer route is documented as not natively qualified. Route the pick through `Permits`, or check `_failed` and `Situation()`.

## Verified as consistent
- **Archived market identity:**
  - Cuts close before a batch is interpreted (`WorldMaterialCareContinuation.cs:20-28`).
  - Records of closed quotes stay in the closing root (`WorldMarketBook.cs:207-217`).
  - So projecting a quote against its publishing root (`SqliteWorldAuthority.PaidExchange.cs:106-112`) gives the right `MarketId`. F1 is about the cost of doing this, not its correctness.
- **Stall separation:**
  - `BelongsTo` requires a matching `MarketId`, and a bilateral quote belongs to no stall.
  - Inquiry status is scoped to the stall it was asked at.
  - Cancel at another stall is explicit.
  - Opening focus lands on a choice, not a commitment (`FarmMarketPanel.cs:141-151`).

## Follow-up tests needed (summary)
- F1: the frame path does not block while `_gate` is held.
- F2: the liquids/worksite policy and the observation at the breach stance and the farm's lower and upper levels; unsupported observation is non-fatal.
- F3: an implicit fallback is not pinned, and the Gearford blocker names the selector path.
- F4: `OpenCargoAsync` create, reopen and refuse, plus a fresh native smoke run.
- F5: checks of what is actually drawn and rendered, and the cardinal-step assertion.

## Bounded verdict
**Not acceptable** as the completed native cargo join. The snapshot shows a plausible keyboard fixture circuit and sound market identity. It does not support the claims about fail-closed presentation, a usable fresh `--cargo` entry, or a non-blocking frame path. Fix F1 and F2 before acceptance. F3 and F4 are needed before claiming the game is playable from a fresh entry. F5 is needed before the agreement samples count as evidence.
