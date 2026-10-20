# AUDIO-01 lead binding: semantic sound from committed observations

Lead implementation on `codex/runtime-foundation` from checkpoint
`codex/checkpoint-before-semantic-audio-20261019` (`863d8448`). This delivers the lead-owned part of the
roadmap's AUDIO-01 unit: semantic event and listener bindings bound to actual actions. It ships **no audio
assets**; every intent awaits a designated creator, so play remains silent. It does not close the P02 AUDIO
row, any listening or human gate, or ART-01.

## Player outcome and contract

When accepted sound exists, a player hears their own and visible residents' footsteps, tool contacts at the
oak, berry bush, joinery or dig site, a quiet outdoor bed, encounter music that starts with a real entry, an
aftermath that holds while the outcome is read, and speech-ducked music during conversation. Reopening,
reconnecting or switching characters restores the current mood without replaying earlier actions.

The contract follows [AUDIO_DIRECTION_AND_RUNTIME](../../../spec-v1_1/AUDIO_DIRECTION_AND_RUNTIME.md): stable
cue identity with source, revision and ordinal; one delivery per listener, branch and visit; restoration of
sustained context on late join or restart; dwell and hysteresis that never delay a legal action; separate
Music, Ambience, Effects and Voice buses; captions limited to what an audible cue conveys; an explicit
distinction between a missing asset and a deliberate silence. The timeless description is in the
[presentation reference](../../../Runtime/docs/architecture/presentation-and-interaction.md#runtime-binding-semantic-sound).

## Source changes

| Path | Change |
|---|---|
| `client/scripts/Presentation/Audio/SoundscapeObservation.cs` | Permitted projection of one presented `LocalWorldView` |
| `client/scripts/Presentation/Audio/SoundCue.cs` | Cue kinds, contact materials and stable keys |
| `client/scripts/Presentation/Audio/SoundscapeCueDeriver.cs` | Transient cues from committed changes, baselines and per-visit delivery |
| `client/scripts/Presentation/Audio/SoundscapeContextDirector.cs` | Sustained score, ambience and ducking with dwell |
| `client/scripts/Presentation/Audio/AcousticIntentCatalog.cs` | Intent per cue and context, playback gate for spacing and voices |
| `client/scripts/Presentation/Audio/SoundscapeDiagnostics.cs` | Bounded probe diagnostics, per-source counts |
| `client/scripts/Presentation/Audio/FarmSoundscape.cs` | Godot adapter: buses, voices, fades, withholding |
| `client/default_bus_layout.tres` | Scene-owned Music, Ambience, Effects and Voice buses |
| `client/scripts/FarmGame.cs` | Reset on session open, advance each frame, present home/regional views, suspend for controlled views |
| `client/scripts/FarmWalkthroughProbe.cs`, `client/scripts/EncounterPlayProbe.cs` | Native soundscape requirements and report fields |
| `tests/OpenFantasy.Hosting.Tests/Client/SoundscapeTests.cs`, test project | 18 focused cases, six linked pure files |

No Core, Hosting, codec, profile, save or gameplay rule changes. Godot import also generated `.uid` files for
the new scripts; four `.uid` files it generated for earlier scripts belong to their own packets and are left
uncommitted.

## Evidence

| Check | Result |
|---|---|
| Client and Hosting test builds, pinned SDK 10.0.401 | Warning-free |
| Focused soundscape cases, including four against real saves | [18 passed](managed/focused.trx); [first run](managed/focused-first-run.failed.trx) 16 of 17 |
| Hosting `Client` family, 180 s bound | [287 passed, 4 opt-in skipped, 2 failed](managed/client-family.trx), classified below |
| [Native farm walkthrough](native-farm-02/report.json), two processes | Passed: player path 896 subunits, 4 player footsteps over 4 strides, 2 more from the visible walking resident, conversation ducked and released, buses 1–4 present, paused reopen restored context with zero cues |
| [Native encounter journey](native-encounter-01/report.json), three processes | Passed: real entry started the encounter context, mid-duel reopen restored it without transition, release reached the aftermath, released reopen restored exploration |
| [Native four-player selected control](native-selected-control-01/selected-control.trx) | Passed in 49 s with the soundscape suspended for controlled views; regression only, no sound assertions. Its 96 MB of raw files are hashed in an [evidence manifest](native-selected-control-01/EVIDENCE_MANIFEST.json) and kept on the lead's disk, not committed |


## Failures and classification

- **Native farm, first run** ([retained](native-farm-01/report.json)): *probe defect, mine.* It compared
  all-source footsteps with the straight-line walk. The approach walks one axis then the other, and the resident
  walks too. The probe now measures the player's committed path and counts per source. Production unchanged.
- **Focused director test, first run:** *test expectation defect, mine.* After a long outcome reading, leaving it
  returns to exploration at once with the four-second fade; the test expected a further wait. Production
  unchanged; a separate case now checks that a short outcome still receives the full aftermath.
- **Launcher import, `tools/play.py --prepare-only`:** *pre-existing tooling failure, unexplained.* The Godot
  editor plugin reports `.NET Sdk not found. The required version is '10.0.12'` and cannot load `Microsoft.Build`.
  It reproduces identically on a clean worktree of the checkpoint, inside and outside the sandbox. The
  [October 3 import](../adventure-entry/checks/native-01.json) had no error. The pinned SDK bundles runtime
  10.0.12, its direct SDK listing works, and the toolchain and global .NET install are unchanged since August.
  Effect: the ordinary launcher and `Play OpenFantasy.command` stop at preparation. Native probes that launch
  the game directly are unaffected. Diagnosis stopped at its bound; it is a separate open item.
- **Two `Client` family failures:** *pre-existing configuration-dependent verifiers.* `NativeRetreatResultTests`
  and `NativeRoadThreatResultTests` are unconditional facts that throw without their manifest variables, unlike
  their four skipping siblings; unchanged since `cdeb9190`. Not caused by this packet; semantics left unchanged.

## Limits

- No accepted recordings or music; every intent is `AwaitingCreatorAsset`. Listening, mix and human acceptance are unrun.
- Captions are declared but not displayed; there is no volume or caption setting UI yet.
- Footsteps are generic ground; surface, weather, equipment, enclosure, portals and daylight variation are unbound.
- Controlled-character views suspend cue derivation rather than deriving their own.
- Regional withholding runs in code but no native assertion targets it.
- Independent review is not run; orchestration remains paused.

## Packet telemetry

This packet ran in a continued session rather than a fresh one. From the goal start to this record: 277 API
calls, average context about 432,000 tokens, maximum 544,018, 399,053 output tokens. That is about 2.8 times
the lead session's measured average of 155,000 and the main efficiency cost of this packet; the efficiency
levers' fresh-session rule was not followed because the work continued inside an existing conversation.

## Documentation review

- **Plan/spec:** the roadmap's AUDIO-01 unit and P02 AUDIO row record the bound lead binding and its limits;
  the handoff names the launcher import failure. No requirement, phase exit or gate changes; the AUDIO row,
  listening and creator-asset obligations remain open.
- **Reference:** `presentation-and-interaction.md` gains "Runtime binding: semantic sound", describing ownership,
  derivation rules, baselines, context dwell, intent statuses, buses, withholding and the controlled-view limit.
  The audio design contract in the same page is unchanged and still accurate. `Runtime/README.md` notes that the
  client derives sound cues but ships no accepted assets.
- **Accuracy/completeness:** the lead checked the reference against the six source files and the adapter wiring
  in `FarmGame.cs`, the cue rules against the focused and real-session tests, and native claims against the three
  retained reports. Omissions are listed under Limits. The four uncommitted `.uid` files and the unexplained import
  failure are recorded rather than resolved.
- **Checks:** `python3 Beta/tools/check_documentation.py --work-record Beta/implementation/p02/semantic-audio/RESULT.md --files`
  with the affected live pages, and `git diff --check` on authored pages; results in [CHECKPOINT.json](CHECKPOINT.json).
