# Coordinated encounter lifecycle implementation contract

The new permanent profile24 represents exploration and complete coordinated encounters inside one
world interpretation. Profiles22/23 and their saved meanings remain unchanged. Core/application stay C#.
This record specifies work in progress; qualification belongs in its eventual execution record.

## Owned records

- `EncounterScenarioDefinition`: immutable instance-bound encounter opportunity, with definition ID,
  key/display name, initiating actor, target actor, entry reach, and complete `EncounterCombatant`
  assignments. These are encounter-specific sides/grants, not permanent species hostility. The catalog
  is bounded to eight scenarios; each has the exact local resident set and uses admitted abilities.
- `EncounterReadinessDebt`: actor ID and remaining tactical units. The complete permanent ledger
  retains incapacitated and bystander rows. Its units do not reset at entry/release.
- `EncounterLifecycleSnapshot`: world/branch/seed, revision, total exploration steps, total tactical
  ticks, entry tactical ticks, encounter count, phase, ability catalog, scenario catalog, debt ledger,
  optional selected scenario ID and optional current/last `EncounterActionSnapshot`.
- Phases: `Exploration=1`, `Confrontation=2`, `Staging=3`, `Active=4`, `Outcome=5`.
  `HasEncounter` means phase is not Exploration. Exploration may retain the last selected scenario and
  action archive; those historical records do not route ordinary processing into combat.

The lifecycle revision follows complete world revision. Encounter count increments only on successful
entry. Entry captures count and both actor presence revisions, allowing ordinary unrelated clock ticks
without a stale-command loop while refusing prior encounter episodes. Release captures encounter ID and
exact action/outcome hash; current supported outside conditions are rechecked before atomic publication.

Component methods: `Begin(encounterId, scenarioId)`, `AdvanceStaging()` (Confrontation→Staging→Active),
`ObserveActive(timing, actions, tacticalTick, isOutcome)`, `Release()`, `AdvanceExploration()` and
`RetainBoundary()`. Each increments revision once. Begin copies actual debt into the separately owned
timing clock; ObserveActive copies its resulting debt back. Release preserves the last action archive
and all debt. Exploration pays at most180 tactical units per actor per earned1,800ms step. Held or
administrative boundaries pay none. A recovering actor cannot simultaneously perform ordinary voluntary
work or movement at the start of that step; entry itself preserves rather than spends its debt.

## Clock and authority invariants

`world.AdvancingSteps = explorationSteps + tacticalTicks` and total exposure is exactly
`explorationSteps * 1800ms + tacticalTicks * 50ms`. An active encounter's elapsed units equal
`(tacticalTicks - entryTacticalTicks) * 5`. Entry exposure is the retained exploration exposure plus
prior tactical exposure. No whole-process-boundary entry assumption is valid after a previous fight.
Pacing preserves exact process carry on release. Ordinary action preflights and real fluid advancement
use the earned process boundary, not the fractional calendar. No passive construction/mining labor.

One new lifecycle batch kind8 owns zero-duration authenticated entry/release or actionless staging.
Fresh schema6 admits profile24 with that kind; old schema5 is preserved. One existing SQLite authority
commits the full state/receipts/identity journal. Entry reserves a fresh historically unique encounter
ID derived from world/branch/seed, the exact scenario pin and the next committed encounter ordinal.
A supplied ID must equal this derivation; command IDs and unrelated clock revisions cannot select
a more favorable damage stream. Each scenario reserves an entire64-bit identity family: the first
ordinal's hash supplies a fixed high word and low-word base, then `baseLow XOR priorEncounterCount`
selects the exact next ID. Generic commands/causes/results/spawns cannot consume present or future
episode names. Only explicit lifecycle entry may claim its exact expected ID after actual-entity and
historical-ledger collision checks. Staging retains legal current positions as the declared in-place fallback, never teleporting actors.
The entire supported local world is coordinated, including source custody, dry terrain/support and
liquid processes. Unsupported dependency families are not claimed as solved.

Entry requires an actual live authorized initiator, live opposing participants, admitted scenario/grants,
current target revision, legal reach/sightline and complete current spatial/support/hazard admission.
Release requires the actual outcome, a living authorized initiator, pinned final action archive and
complete current dry-world admission. Disengagement/flee is not invented to shortcut the outcome gate.
HP0 actors retain identity/body/custody but cannot act, move, give consent, speak, trade, deliver quests
or complete work; existing dialogue and counterpart checks must respect that eligibility. Prior receipt
lookup remains available independent of later incapacity.

## Meaningful proof

Use a fresh source-backed world with two actual Bramble Wolves and two explicit scenarios. Explore/work,
enter the first confrontation, complete it with real HP0/debt/carry, release, and enter the second valid
encounter before recovery can reset. Prove the same debt persists and no instant extra attack is allowed.
Separate continuations resume ordinary movement and Wren's actual work after paid recovery, retaining
fractional carry and earned fluid/recovery intervals. Keep queued/outcome history and process-kill
recovery across entry/release. No source stat editing, free healing or synthetic victory is allowed in
the source-backed journey. Alternate authored negative controls are labeled separately.

Native battle controls/choreography, broader effect/death/reward systems, joining/merging, independent
co-op, wet/structural hazards, crowd continuation and all full campaign/platform gates remain open.
