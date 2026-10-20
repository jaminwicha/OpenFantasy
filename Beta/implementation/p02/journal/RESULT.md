# Journal: character stats and known quests

Lead implementation on `codex/runtime-foundation` from checkpoint `codex/checkpoint-before-journal-20261020`
(`dc5cf00a`). It delivers the stats and journal items of the P02 UI row from data the player's permitted view
already carries. Equipment, known skills, the local explored map and the calendar remain open, because no
projection exists for them yet and the journal does not invent one. No world, save, codec or gameplay rule changes.

## Player outcome

J or the HUD's Journal control opens a read-only journal at any time during play, encounters, pause or a
conversation, without pausing the world. It shows the character's name, profile, level, experience, HP, MP and gil,
and each quest the player knows about: an offer while someone is offering it, then the accepted quest with how many
items are ready against the requirement and its reward, then the completed quest. It states plainly that equipment
and known skills are not shown yet. The journal key is remappable in Settings and the HUD label follows it.

## Source changes

| Path | Change |
|---|---|
| `client/scripts/FarmJournalModel.cs` | Pure journal projection from `LocalWorldView`; ordering and texts |
| `client/scenes/FarmJournalPanel.tscn`, `client/scripts/FarmJournalPanel.cs` | Scene-owned read-only panel |
| `client/scripts/FarmGame.Journal.cs`, `FarmGame.cs`, `FarmGame.ControlReturn.cs` | Open, present while open, close, control-return close |
| `client/scripts/FarmInputRouter.cs`, `FarmInputMap.cs` | Journal action and layer, permissions, J key; settings reachable from the journal |
| `client/scripts/Settings/KeyBindings.cs`, `FarmKeyHints.cs`, `FarmKeyLabels.cs` | Remappable journal key and hint |
| `client/scenes/FarmHud.tscn`, `client/scripts/FarmHud.cs` | HUD Journal control with key hint |
| `client/scripts/FarmQuestProbe.cs` | Native journal stages during the held conversation after delivery |
| Hosting tests and project | Router journal case; journal model cases against real quest worlds |

## Evidence

| Check | Result |
|---|---|
| Client, Hosting and Server test builds | Warning-free |
| [Focused families](managed/focused.trx) | 72 passed; 2 failed were manifest-dependent outcome verifiers swept in by my broad `EncounterOutcome` filter |
| [Hosting `Client` family](managed/client-family.trx) | 298 passed, 4 opt-in skipped, the same 2 manifest-dependent verifiers failed |
| [Native quests](native-quests-01/report.json) | Passed: after real delivery, J opened the journal in the held conversation showing Wren's Saplings completed, level 3 and 200 gil, with the held state unchanged; J closed it |
| Native [settings](native-settings-01/report.json), [farm](native-farm-01/report.json), [encounter](native-encounter-01/report.json), [action offers](native-offers-01/report.json), [wheel](native-wheel-01/report.json), [gathering](native-gathering-01/report.json), [care](native-care-01/report.json), [construction](native-construction-01/report.json) | All passed |
| [Four-player selected control](native-selected-control-01/selected-control.trx) | Passed in 50 s; raw files hashed in its [evidence manifest](native-selected-control-01/EVIDENCE_MANIFEST.json) |

No run failed for a new reason. The focused-run failures are `EncounterOutcomeDurabilityResultTests` and
`NativeEncounterOutcomeResultTests`, unconditional facts that throw without their manifest variables; my substring
filter selected them. They are the same pre-existing class as the retreat and road-threat verifiers.

## Limits

- No equipment, known skills, explored map or calendar views; these need projections first.
- Item names are not shown in quest progress; the counts are the view's eligible and required quantities.
- No controller button opens the journal directly; controllers use the HUD control.
- No human review of the journal's wording, density or placement.

## Documentation review

- **Plan/spec:** the roadmap's UI row and the handoff record the journal and the remaining equipment, skills, map
  and calendar items. No requirement, phase exit or gate changes.
- **Reference:** `presentation-and-interaction.md` gains "Runtime binding: journal", covering its projection,
  offer visibility, the not-shown statement, input layer and non-pausing rule. `Runtime/README.md` lists J.
- **Accuracy/completeness:** checked against the journal model, panel, router rules and the retained native and
  managed results; the real-world managed cases confirm offer visibility only while addressed, and the level-3,
  200-gil completion values come from the actual quest world.
- **Checks:** `python3 Beta/tools/check_documentation.py --work-record Beta/implementation/p02/journal/RESULT.md --files`
  with the affected live pages, and `git diff --check`; results in [CHECKPOINT.json](CHECKPOINT.json).
