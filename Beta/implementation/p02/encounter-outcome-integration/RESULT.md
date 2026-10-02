# Outcome input and presentation integration — result

Status: **implementation complete for findings 1–5; verification incomplete (native care routes failed/unfinished);
item 6 docs not updated.** Stopped early because the $25 client budget was nearly exhausted (one late tool call
cost ~$3). Exact `claude-opus-5-5[1m]`; no Git, packages, recursive agents or Core/Hosting/Host source edits.
Design: [DESIGN.md](DESIGN.md). Astra's LEAD_FEEDBACK findings (seam passivity, immutable bytes, V hint, exact
pre-input selection bytes with nested-fact counterexample) were read after the first native failure and applied.

## Terminal evidence (all under `qualification/`)

| Check | Report | Result |
| --- | --- | --- |
| build | `build-20261002T023748Z-6c41a` | failed: CS0108 (probe `Ready` hid `Node.Ready`), fixed |
| build | `build-20261002T023810Z-43aad` | failed: Godot `Scan thread aborted` (editor init), C# built |
| build | `build-20261002T023835Z-a6a50` | accepted (identical retry) |
| outcome-unit | `outcome-unit-20261002T023848Z-df2cb` | accepted, 33 passed |
| outcome-fixtures | `outcome-fixtures-20261002T024017Z-ec9b5` | accepted (fresh; generator text changed) |
| outcome-native | `outcome-native-20261002T024111Z-8e8eb` | failed: victory sale `ShowTradeLot` readiness snapshot taken before keyboard navigation; panel re-derives expiry each frame. Fixed by proving readiness at the activating Enter (strike/Continue rows showed `SameOfferObject: true`) |
| build | `build-20261002T024326Z-71cc7` | accepted (superseded) |
| build | `build-20261002T024609Z-a5e38` | failed: Godot `EditorSettings … android_sdk_path` (same environmental error as predecessor) |
| build | `build-20261002T024632Z-c38e0` | **accepted — final build** |
| outcome-unit | `outcome-unit-20261002T024644Z-2f9df` | **accepted, 33 passed (final build)** |
| outcome-fixtures | `outcome-fixtures-20261002T024809Z-a4f42` | **accepted (final)** |
| outcome-native | `outcome-native-20261002T024903Z-fc95d` | **failed**: victory and defeat probes completed (images, `victory.json`, `defeat.json`); the `care` process wrote no report and exceeded its unchanged 330 s process bound (save left with WAL). Cause not diagnosed. `care-wheel`, reopen and outcome-auth never ran. |

Not run (no accepted native evidence to authenticate, and budget): outcome-auth, fixtures, road-native/auth,
native/native-auth, client-regression. Core, retained-auth and six-kill durability are not re-claimed; Core/Hosting
production source is unchanged (PROTECTED_APPLICATION preserved in every report), but no dependency comparison was
recorded here.

## Changes (Runtime client/tests/tool only)

- **HUD (F1).** Cause established: the non-wrapping `Movement` hint in the `Actions` row plus four exploration buttons
  exceeded the footer width (grow-both footer → clipped both edges). Compact `TerminalHint` (`Incapacitated`/`Esc: pause`),
  `TerminalStatus`, one-line `InlinePreview` for outcome/care choices; full text in status/button tooltips and the card
  (`EncounterOutcomeText.Card`). Probe captures each state first, then records post-draw rects of header, footer, target
  bar, every visible footer/header label/button (lines, widest unwrapped line) and sprite clearance; checker re-verifies.
  Victory-completed/terminal HUD checks passed natively in `…024903Z` victory/defeat; images retained there.
- **Choices (F2).** Reading beside care: HUD button text is its label (no `[E]`), tooltip names pointer/focus/wheel;
  wheel puts the reading in the unused right slot (care stays left); sector label from the offer (`SectorLabel`).
  Hint `E / A: care · V: choices`. New `care-wheel` route (V, Right, Enter) — **not yet natively proven**.
- **Seam (F3).** `FarmClientSession.Submitted` passive event + new linked `FarmSubmission` (exact offer, command,
  copied canonical bytes, receipt task, retry flag); observer exceptions recorded in `ObservationFailure`, never reject
  a queued command; `FarmGame.ObserveSubmissions` (disposable save only). Probe proves readiness, requires one
  submission per input whose pre-input offer re-encoded with the generated ID equals the submitted bytes, and reports
  rows. `NativeEncounterOutcomeResultTests` authenticates rows/selections against `world_command_outcomes` and journal
  commits, requires the player-principal bijection, and asserts failures for altered intent/command/receipt/identity,
  omission and a nested sale-payment/archive-hash alteration — **written but never executed** (outcome-auth not run).
  Managed seam test (`FarmSubmissionSeamTests`) passed: exact offer, retry identity, stale `Unavailable` rejection,
  journal byte equality, throwing observer, copy immutability.
- **Care text (F4).** Null view → "No care is offered to you"; pending → "Care requested from Wren · …" (no helper
  acceptance claimed); every acceptance statement carries "every required player has read it and no care is pending".
- **Proofs (F5).** Human first wolf's reading decodes vs host first wolf's refused (control kind isolated); care hold
  asserts exact `InvalidOperationException`/`AuthorityDispatchException` messages; victory derivation asserts HP 1;
  fixture header/manifest/metadata and checker/probe text state the care fixture's managed approach/entry.
- Test expectation changes (strengthened, not removed): text-test strings updated to the corrected wording;
  journey/correction tests' `No helper has agreed…` and `declines care` hint assertions replaced by the honest forms.

## Remaining obligations

Native `care`/`care-wheel` routes (hang cause unknown), outcome-auth execution of the correlation proof, all shared
regressions (road/native/client), item 6 architecture/README updates, native accepted care, native care-specific
approach/entry timing, no-survivor resolver route, real two-session readiness, lived profile-34 migration. No phase,
production, balance, device, human UI/art or credit-saving claim.

## Friction

Two environmental Godot editor failures needed identical retries; one probe readiness defect cost a native cycle;
the care hang left no probe report. Budget ran out before diagnosis.
