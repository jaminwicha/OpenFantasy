# Keyboard remapping

Lead implementation on `codex/runtime-foundation` from checkpoint
`codex/checkpoint-before-keyboard-remapping-20261020` (`f2ff1061`). It delivers the first item of the P02 UI row,
remapping, for the keyboard: the player can rebind walking and every action except pause from the Settings panel,
and on-screen hints follow the chosen keys. Controller remapping, device selection and scaling remain open. No
world, save, codec or gameplay rule changes.

## Player outcome

The Settings panel gains a Keyboard controls list. Choosing a row and pressing a key rebinds that action at once;
Escape keeps the current key. Choosing a key that another action already uses swaps the two, so nothing is left
unbound. Escape, arrows, Enter, Space and Tab cannot be taken, so the player can always pause, close panels, walk
with the arrows and navigate the interface. Reset keys restores the defaults. Bindings are saved with the other
settings and restored before any input on the next launch. Button labels, footer hints, wheel page hints and
opening messages show the current keys rather than fixed letters.

## Source changes

| Path | Change |
|---|---|
| `client/scripts/Settings/KeyBindings.cs` | Single table of remappable actions and default keys; reserved keys; swap on conflict; parse with whole-set fallback |
| `client/scripts/Settings/FarmSettings.cs` | Settings carry key bindings; entries round-trip them |
| `client/scripts/FarmInputMap.cs` | Defaults drawn from the binding table; `ApplyBindings` replaces primary keys, keeping arrows and controller events |
| `client/scenes/FarmSettingsPanel.tscn`, `client/scripts/FarmSettingsPanel.cs` | Scrollable controls list, capture mode, reset, notice line |
| `client/scripts/FarmGame.Settings.cs` | Applies bindings and rebuilds hints on every settings change; refuses reserved keys |
| `client/scripts/FarmKeyHints.cs`, `FarmKeyLabels.cs` | Presentation-only current key names read from the input map |
| `client/scripts/FarmHud.cs`, `FarmQuickWheel.cs`, `FarmGame.cs`, `FarmGame.Cargo.cs`, `EncounterOutcome/EncounterOutcomeText.cs` | Every fixed key letter in hints replaced by the current key |
| `client/scripts/FarmSettingsProbe.cs` | Rebinds Talk and Gather through capture mode; checks the swap, leakage, persistence and hints |
| Hosting tests and project | Three binding cases; linked binding and hint files |

## Evidence

| Check | Result |
|---|---|
| Client, Hosting and Server test builds | Warning-free |
| [Focused router and settings cases](managed/focused.trx) | 28 passed |
| [Hosting `Client` family](managed/client-family.trx) | 295 passed, 4 opt-in skipped, the same 2 manifest-dependent verifiers failed |
| Encounter outcome correction tests that assert default hint strings | 4 passed |
| [Native settings](native-settings-02/report.json), two processes | Passed: Talk rebound to T, Gather moved onto G with Give swapped to F, neither press leaked into gameplay, hints show T, G and F, and the second process restored all of it before input. The [first run](native-settings-01/report.json) also passed, before the hint assertion was added |
| Native [farm](native-farm-01/report.json), [encounter](native-encounter-01/report.json), [action offers](native-offers-01/report.json), [wheel](native-wheel-01/report.json), [gathering](native-gathering-01/report.json), [care](native-care-01/report.json) | All passed with rewritten hints and default keys |
| [Four-player selected control](native-selected-control-01/selected-control.trx) | Passed in 47 s; raw files hashed in its [evidence manifest](native-selected-control-01/EVIDENCE_MANIFEST.json) |

The native encounter-outcome journey needs prepared fixtures and was not run; its text paths are covered by the
managed outcome tests at default keys. No run failed for a new reason.

## Limits

- Controller buttons cannot be remapped; device selection, text and UI scaling, and reduced motion remain open.
- Only one primary keyboard key per action; arrows remain the fixed secondary walking keys.
- Key names come from the engine's key labels and are not localized.
- No human review of the controls list, wording or layout.

## Documentation review

- **Plan/spec:** the roadmap's UI row and the handoff record keyboard remapping and the remaining controller,
  device and scaling work. No requirement, phase exit or gate changes.
- **Reference:** `presentation-and-interaction.md` extends "Runtime binding: client settings and captions" with the
  binding table, reserved keys, swap and fallback rules, capture isolation and hint behavior. `Runtime/README.md`
  describes rebinding and reserved keys.
- **Accuracy/completeness:** checked against the binding model, input-map application, panel capture code, hint
  sites found by a full search of non-probe client scripts, and the retained native reports. Probes and tests that
  compare literal hints run at default keys because fixtures never load player settings.
- **Checks:** `python3 Beta/tools/check_documentation.py --work-record Beta/implementation/p02/keyboard-remapping/RESULT.md --files`
  with the affected live pages, and `git diff --check`; results in [CHECKPOINT.json](CHECKPOINT.json).
