# Lawful encounter retreat — result

Status: **delivered as a bounded capability, with named missing obligations (below).** All eleven granted wrapper
modes are accepted on the final source and binaries (build `build-20261001T135327Z-9061f`). Model route:
`claude-opus-5-5[1m]`, requested Max; one request, no agents, no Git mutation, no paid retry or fallback.
Decisions: [DESIGN.md](DESIGN.md). Timeless reference: [encounter-retreat.md](../../../Runtime/docs/architecture/encounter-retreat.md).

## What ships

A typed, versioned committed retreat for players and NPCs. The actor walks a captured route of ordinary 32-subunit
cardinal samples, one per own due boundary, each through `LocalSpatialSnapshot.PrepareMove` and complete-world
admission, charging `ceil(10000·100/(800·speed))` readiness per sample (one standard action per cell). Opponents act
between samples at their real boundaries. When the last sample leaves every living opponent's contact radius
(`min(768, longest granted range + 256)`) the episode ends `Disengaged` in that transaction: debts become permanent,
pacing ends with exposure/carry, other queued choices end with retained identities. A refused sample charges its
readiness without moving (`Interrupted`); incapacity/outcome cancels (`Cancelled`). Full Wait still holds every
undecided human; a committed retreat is that actor's standing decision. Release, Wait and all older profiles keep
their meaning.

## Semantics and version decisions

- **Profile 34** (`WorldEncounterRetreatAdmission`, frame `world.retreat-admission.v1`) = unplayed profile-33 world +
  root owner `EncounterRetreatState` pinned by rule text `encounter.retreat.v1`. Fresh opt-in only; lived saves are
  never upgraded (lived migration is a separate obligation, documented in the Runtime README).
- New contracts: action codec **v19** (`world.action.queue-retreat`), `encounter.retreat.v1` owner codec
  (continuation + terminal record incl. cancelled choice ids), root codec **V34** (V33 fields + field 25), step codec
  **V34**. The root dispatcher pre-screen rises from 24 to 25 fields; each older codec keeps its own exact bound.
- Retreat intent is refused for every other profile at batch construction/decode, at the session (`AdmitsRetreat`)
  and outside EncounterDecision boundaries. No golden bytes changed; 571 old test files stay byte-exact.
- Supported party interpretation: two positive sides; the retreating actor must be the sole living member of its side
  (`AllyRemains` refusal); bystanders keep side 0; opponents have no pursuit in this interpretation.

## Paths and responsibilities (all relative to `Beta/Runtime`)

- Core (new): `src/OpenFantasy.Core/Encounters/{QueueEncounterRetreatIntent,EncounterRetreatRules,EncounterRetreatState,EncounterRetreatCodecV1}.cs`;
  `src/OpenFantasy.Core/Worlds/{WorldEncounterRetreats,WorldEncounterRetreatAdmission,WorldEncounterRetreatResolver,WorldEncounterRetreatAdvice,WorldActionCodecV19,WorldSnapshotCodecV34,WorldStepCodecV34}.cs`.
- Core (modified): `WorldSnapshot` (owner, profile 34, successors), `WorldEncounterActionProcessor` (queue/resolve/cancel),
  `WorldEncounterActions.HasRequiredDecisions`, `WorldEncounterActionAdmission` (queued retreat choice),
  `EncounterLifecycleSnapshot.Disengage`, codec dispatchers, profile lists (`WorldStepBatch`, `WorldStepFrameCodec`,
  care/resource/restorative validators).
- Hosting: `Content/Retreat/CargoRetreatWorldAdmission.cs`; `Simulation/Retreat/{LocalRetreatView,LocalRetreatConfiguration}.cs`;
  `LocalEncounterController` (opt-in NPC retreat), `LocalNpcController`, `LocalWorldSession` (config + selection),
  `LocalWorldView.Retreat`, `LocalProfileBindings`; schema-eight/recovery/journal/archive/paid/market/proposal lists.
- Client: `scripts/Retreat/{RetreatOffers,NativeRetreatEntry,RetreatPlayProbe}.cs`, `scenes/NativeRetreat.tscn`;
  `EncounterActionOffers`, `FarmQuickWheelModel` (upper sector), `FarmWheelDisc` (existing `traverse` art + label),
  `FarmInputRouter` (enum), `FarmClientSession`/`FarmGame.Cargo` (explicit `--cargo-retreat` selection).
- Tools/tests: `tools/check_native_retreat.py`; `tests/OpenFantasy.Core.Tests/{Encounters,Worlds}/Retreat/*`;
  `tests/OpenFantasy.Hosting.Tests/{Simulation,Client}/Retreat/*`; one additive Compile link (`Client/Retreat/RetreatOffers.cs`).
- Docs: new `docs/architecture/encounter-retreat.md`, index row, cross-references in encounter-lifecycle, world-pacing,
  native-encounters, cargo-circuit, time-and-encounters; Runtime README section.

## Evidence (final source, `qualification/`)

| Mode | Report | Result |
| --- | --- | --- |
| build | `build-20261001T135327Z-9061f` | accepted, 6.5 s |
| unit | `unit-20261001T135339Z-1945b` | 7/7 retreat session tests |
| core | `core-20261001T135404Z-f9a54` | 1,763 passed (1,756 retained + 7 new) |
| regression | `regression-20261001T135545Z-ec7f8` | 510 passed |
| fixtures | `fixtures-20261001T140019Z-e16e6` | retreat + cargo + trading + gift exports |
| native | `native-20261001T140110Z-d28d9` | keyboard 13.8 s, controller 13.5 s, pointer 20.4 s, reopen 4.9 s; aggregate ~53 s; rendered, background |
| native-auth | `native-auth-20261001T140243Z-d6958` | journals of all three routes authenticated |
| cargo-native / cargo-auth | `cargo-native-20261001T140309Z-1aa28` / `cargo-auth-20261001T141023Z-4856b` | accepted (430.2 s / 39.1 s) |
| legacy-native / legacy-auth | `legacy-native-20261001T141105Z-15330` / `legacy-auth-20261001T141235Z-334cf` | accepted |

Managed proofs (`RetreatSessionTests`): earned reward → in-person purchase → carried lot → real wolf → advised
retreat committed → every published position one 32-subunit step → `Disengaged` at the destination with
wolf HP unchanged, player HP only reduced, last-sample debt retained → ordinary travel → second purchase → full
journal recovery with identical state hash, funds, HP, debt, feet, lot lineage (Marla's source interval) and record →
exact retry returns the original receipt. Also: opted-in NPC wolf retreat through the same intent/admission/costs;
stale timing/actor revision, destination mismatch, contact-ending route (`OutOfReach`), route through a body
(`Unavailable`), wrong controller (dispatch refusal), recovering actor; recovery mid-route (2 of N samples) resuming
without skipping samples + retry; counterfactual occupancy after selection → `Interrupted/Blocked`, no movement,
sample charged; ordinary victory + release and defeat still work; profile-33 world refuses retreat.

Native (per route): the wolf selected and engaged by the device, wheel opened, Retreat inspected (destination cell
and direction, steps, recovery, explanation, opponents in reach — see `controller.retreat-inspected.png`), cancelled
once with nothing sent, re-inspected and committed, 16 walked samples observed, disengaged, held-key travel back to
the origin, paused; keyboard save reopened paused with identical feet/record/balance and unchanged bytes.

## Failures retained

- First native run: probe asserted wheel labels too early; diagnostics then showed the first wheel opened right after
  becoming due captured the previous frame's blocked Retreat and correctly marked it "changed — close and choose again";
  the probe now closes and re-inspects (bounded, nothing sent). The capture staleness itself is unfixed wheel behaviour.
- Rendered capture showed the sector labelled "Action"; fixed to "Retreat" and the whole matrix rerun.
- Unit run 1: 5/7 (wolf ambled during long walk → added ordinary re-approach; wrong controller is a dispatch refusal,
  not a receipt). Three `build` attempts (`133255Z`, `133314Z`, `135312Z`) hit a transient Godot `EditorSettings not
  instantiated` import error; identical retries passed. One build failed on two compile errors (name clash, xUnit analyzer).

## Missing obligations (not claimed)

- **Actual process interruption** around a selected retreat publication (no new owned kill helper was written);
  recovery is proved by closing and fully replaying the journal, not by SIGKILL.
- **Checkpoint/archive interaction**: no test crosses a coordinated archive cut or checkpoint during a retreat
  (the owner is carried by `WithCoordinatedCut` in code only).
- **Native trade after retreat**: native continuation is held-key travel; the second purchase after retreat and the
  bought-lot reopen are managed only. Controller/pointer routes do not repeat travel-to-market.
- Untested refusal kinds: lost ground locomotion, `AllyRemains` party refusal, `ContactRemains` at route end,
  lost controller mid-route (code paths exist, no test). The first-open stale wheel capture is not fixed.
- Not claimed: party/joint retreat, pursuit, independent pockets, multiplayer/four-client, bandit-camp threat, guard
  wages, NPC itineraries, stepped/constructed ground, lived-save migration, physical devices, OS focus, human/art
  acceptance, full P02.

## Friction

Seven-thousand-second budget with ~17-minute full-matrix reruns forced freezing scope before the missing items above;
native fixtures re-export on every source change. No permission denials.
