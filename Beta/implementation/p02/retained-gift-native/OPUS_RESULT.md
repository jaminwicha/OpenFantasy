# Retained-world gift and own-use native journey — worker result

Status: managed implementation, fixture export and tests complete and passing. Native Godot execution
and native-journal authentication have **not** been run by the worker; both are root-owned.

## Changed / created (owned paths only)

- `Beta/Runtime/client/scripts/FarmClientSession.cs`: `OpenRetainedGiftsAsync(save, originalGenesis, target)`
  (existing save only; exact fresh profile27 genesis bytes; target must be 30; `OpenExisting(path, genesis)`
  full recovery, then same world/branch/admission and a saved-proposal profile30 head; dispose on refusal;
  `Attach(..., CareCompanion, gifts: true)`). `GiftPolicy(...)` extracted from `Attach`, same values.
- `FarmGame.cs`: `RetainedGiftGenesisPath` (no default save). `Bootstrap.cs`: `--retained-gifts SAVE GENESIS`
  (normal focus) and disposable `--retained-gift-probe SAVE REPORT GENESIS METADATA keyboard|reopen --background-render`.
- New `RetainedGiftNativeDriver.cs` (reusable bounded input/wait/capture driver; waits checked on success)
  and `RetainedGiftPlayProbe.cs`.
- `tools/play.py` (`--retained-gifts SAVE GENESIS`), new `tools/check_retained_gifts.py` (not executed).
- Tests `tests/OpenFantasy.Hosting.Tests/RetainedGifts/`: `RetainedGiftPreparation`, `RetainedGiftSelectionTests`
  (9 cases), `RetainedGiftFixtureTests`, `RetainedGiftResultTests` (skips without manifest; not run).
- Docs: `Runtime/README.md`, `material-proposals.md` (new retained selection section),
  `coordinated-world-continuation.md` (copy/open separation). No csproj, Core/Hosting, existing test or probe edits.

## Journey design (actual data)

Real source facts found: helper care permission was **active** (so preparation revokes it), player HP
95,463/100,000, remedy +40, self-use 1,800 ms, player owns raw lot `c482176b…` (8 units).
A cut can only precede a batch that creates an offer, so a helper gift can ride a cut only if a second
offer is created while it is open. Design: labeled managed preparation (1 patient revocation, 62 helper
offer/withdraw pairs → 62 outcomes, 34 empty steps to the helper's idle amble phase; revision 1739).
Native: helper policy offers 1 dose (62+1+1 fits) → player's native pack/Gifts offer of 1 raw unit to the
helper triggers the automatic cut (ordinal 4) carrying the open gift → helper policy declines → player
accepts via Gifts wheel → Care self-use → use 521. The managed scheduled-session continuation proves
exactly this sequence, including anchor carry and exact HP/MP/time/stock.

## Checks run (all terminal, accepted)

- build: `qualification/build-20260930T021455Z-ac743/report.json` (earlier failed compile retained:
  `build-20260930T020639Z-7e549`).
- focused 10/10 passed, 98.5s: `qualification/focused-20260930T021511Z-ad721/report.json`
  (earlier 9/10 run retained: `focused-20260930T020943Z-2271e`, a wrong expected exception type; fixed in test).
- fixture 1/1 passed, 129.5s: `qualification/fixture-20260930T021706Z-76a6b/report.json`;
  native inputs at `…/fixture-20260930T021706Z-76a6b/fixtures/` (save, genesis, transition, before, metadata, manifest).

## For Astra (unrun)

`check_retained_gifts.py --dotnet … --godot … --fixtures <fixture dir> --output <new json> --rendered`, then
`run_checks.py native-auth` with `OPENFANTASY_RETAINED_GIFT_RESULTS=<native-manifest.json>`.

## Unproved boundaries / risks

- No native process, screenshot, focus observation or `RetainedGiftResultTests` execution by the worker.
- Probe keyboard only; controller/pointer and existing fresh routes are not exercised here.
- Acceptance pins the helper's presence revision; idle-phase preparation reduces but does not eliminate
  amble risk if native frames are slow (>48 unpaused ticks). A failure must not be retried/relaxed.
- After own use the helper may natively offer again before pause; result tests accept exactly one such
  later helper offer and verify it structurally only.
- Native opening of the ~1,740-commit world within 30s is unmeasured in Godot.
- No process-kill/transition-interruption, physical device, OS focus, art or human acceptance claim.
