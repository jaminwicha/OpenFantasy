# Client settings: mixer levels and captions

Lead implementation on `codex/runtime-foundation` from checkpoint
`codex/checkpoint-before-client-settings-20261020` (`54539d56`). It delivers part of the P02 UI row's essential
settings and the audio direction's required player controls: independent Master, Music, Ambience, Effects and
Voice levels, and captions for notable sounds. Keyboard and controller remapping, device and scaling settings
remain open. No world, save, codec or gameplay rule changes.

## Player outcome

O, a controller's share button or the HUD's Settings control opens a small panel at any time: during play,
encounters, pause, conversation or while a world opens. Opening it never pauses the world. Sliders set each
bus and a toggle enables captions. Closing with O, Escape, Start or Cancel saves the choices in
`user://settings.cfg`, separate from every world save, and the next launch restores them before any input.
With captions on, notable sounds such as an axe striking an oak show a short line near the top of the screen;
routine footsteps do not. Because no accepted audio assets exist yet, captions are the only visible effect of
the sound binding today.

## Source changes

| Path | Change |
|---|---|
| `client/scripts/Settings/FarmSettings.cs` | Pure settings model; per-entry fallback, clamping, string entries |
| `client/scripts/Settings/FarmSettingsStore.cs` | One user file through Godot `ConfigFile`; defaults on a missing or unreadable file |
| `client/scripts/Presentation/Audio/CaptionFeed.cs` | Notable-only, refreshed, bounded, fading captions; never for withheld sources |
| `client/scripts/Presentation/Audio/AcousticIntentCatalog.cs` | `IsNotable`; footsteps are routine |
| `client/scripts/Presentation/Audio/FarmSoundscape.cs`, `FarmCaptions.cs` | Caption feed, bus levels with mute at zero, caption layer |
| `client/scenes/FarmSettingsPanel.tscn`, `client/scripts/FarmSettingsPanel.cs` | Scene-owned panel raising preference events only |
| `client/scenes/FarmCaptions.tscn`, `client/scenes/FarmHud.tscn`, `client/scripts/FarmHud.cs` | Caption layer and HUD Settings control |
| `client/scripts/FarmInputRouter.cs`, `FarmInputMap.cs` | Settings action and layer, permissions, accept handling, O and share button |
| `client/scripts/FarmGame.cs`, `FarmGame.Settings.cs`, `FarmGame.ControlReturn.cs` | Load, apply, open, close, save; fixture isolation; control-return close |
| `client/scripts/FarmSettingsProbe.cs`, `FarmGatheringProbe.cs`, `Bootstrap.cs` | Native settings probe; captions requirements in the gathering probe |
| `tools/check_native_settings.py`, `tools/check_native_gathering.py` | New two-process settings check; gathering fixture enables captions in its own file |
| Hosting tests and project | Two router cases, three settings and caption cases, two linked pure files |

## Evidence

| Check | Result |
|---|---|
| Client, Hosting and Server test builds | Warning-free |
| [Focused families](managed/focused.trx): router, settings, captions, soundscape, catalog | 43 passed |
| [Hosting `Client` family](managed/client-family.trx) | 292 passed, 4 opt-in skipped, 2 pre-existing manifest-dependent verifiers failed, unchanged from the previous packet |
| [Native settings](native-settings-01/report.json), two processes | Passed first run: key opens and focuses, walking suppressed, world unpaused, Music 35% and Effects muted on the real buses, captions on, Cancel saves, second process restores all before input |
| [Native gathering](native-gathering-01/report.json), two processes | Passed with captions enabled by its fixture file: the real oak strike showed "Chopping wood"; the reopened regrowth showed no replayed caption; all earlier assertions held |
| Native [farm](native-farm-01/report.json), [encounter](native-encounter-01/report.json), [action offers](native-offers-01/report.json), [wheel](native-wheel-01/report.json) | All passed with the new HUD control and caption layer, including the encounter probe's footer-occlusion check |
| [Four-player selected control](native-selected-control-01/selected-control.trx) | Passed in 49 s; raw files hashed in its [evidence manifest](native-selected-control-01/EVIDENCE_MANIFEST.json) and kept on the lead's disk |

No run in this packet failed for a new reason. The two `Client` family failures are `NativeRetreatResultTests`
and `NativeRoadThreatResultTests`, which throw without their manifest variables; see the
[semantic sound record](../semantic-audio/RESULT.md#failures-and-classification).

## Limits

- No keyboard or controller remapping, device selection, text or UI scaling, or reduced-motion option yet.
- The share button is not present on every controller; such players use the HUD control by pointer.
- Caption placement, size and wording have no human readability review; HUD crowding remains an open P02 item.
- No volume change is audible until creator assets exist. Bus levels are verified on the mixer, not by listening.
- Settings are per user and per machine; no cloud or per-world preference exists.

## Documentation review

- **Plan/spec:** the roadmap's UI and AUDIO rows record the bound settings and captions; the handoff names this
  increment and the remaining remapping work. No requirement, phase exit or gate changes.
- **Reference:** `presentation-and-interaction.md` gains "Runtime binding: client settings and captions",
  covering storage outside saves, fallback, fixture isolation, the input layer and its non-pausing rule, and
  caption eligibility. `Runtime/README.md` lists the controls and file location.
- **Accuracy/completeness:** the lead checked the reference against the settings model, store, panel, router
  rules and caption feed, and every native claim against the retained reports. Omissions are under Limits.
- **Checks:** `python3 Beta/tools/check_documentation.py --work-record Beta/implementation/p02/client-settings/RESULT.md --files`
  with the affected live pages, and `git diff --check`; results in [CHECKPOINT.json](CHECKPOINT.json).
