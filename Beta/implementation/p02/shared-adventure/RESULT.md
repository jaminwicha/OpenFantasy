# Shared adventure opening

Status: the bounded two-player opening is qualified; the cooperative expedition and full P02 remain open.
The parent accepted revision is
`ad448bb45bfbc37495bf28ae08436cd9b1209e0d`. Work proceeds directly without orchestration.

## Qualified player outcome

Two independently authenticated people enter one fresh ordinary adventure. One forages, the other
gathers wood; the supplier offers an exact earned unit through Pack and the recipient accepts it.
The recipient crafts supplies and delivers Wren's existing quest requirements. Both observe her
autonomous completed work. Controls, finite starting inventory and original world identities remain
intact. No authority command, teleport, grant or resource preparation substitutes for native input.

This is a shared opening, not a full cooperative expedition or a claim of four-player, human,
physical-device, art or production acceptance. The lead client renders without acquiring desktop
focus; the second uses the native scene headlessly. Existing 240-second total, request, input and
mailbox limits remain intact. Every action must retain its original authenticated command and exact
Applied receipt, without automatic command retry; recovery must replay original-genesis history.

## Evidence and limits

[Native attempt 06](evidence/attempt-06/journey.json) passes in 84.795 seconds under its unchanged
240-second total bound. It retains ten unique authenticated actions, their exact saved Applied receipts,
original principal/session attribution and no retries or control grants. Both clients close normally;
recovery from the original genesis reproduces revision 915 and final hash
`f0781754a2a2dea1304ab1ea594ba53e7d6291820f29d61154a13508b08f33e5`.
The [native TRX](checks/native-06.trx) records one pass, zero failures/skips. The four existing
[adventure-entry regressions](checks/adventure-entry-regression.trx), including actual four-human TLS
movement/reconnection, also pass with no skips. These are separate claims, not a four-player journey.
Server and client builds pass with zero warnings/errors. All 1,410 source/binary execution dependencies
in [execution-06](checks/execution-06.json) remain identical after validation;
[final comparison](checks/final-dependencies.json). Only explicit probe verbs and one new test change;
ordinary gameplay, Core/host rules, saves, admission profiles and schemas do not change.

The lead forages twice; the helper gathers one wood, proposes that exact lot and the lead independently
accepts it. The captured inventory sequence additionally shows that very contributed lot consumed by
crafting, producing two rations and one offcut while the lead's original eight wood remain. The quest
then consumes six original wood. Wren completes her work; the delivering character receives 200 gil
and the supplier receives no duplicated reward. See the [contribution review](checks/contribution-review.json).
This does not claim general shared quest-credit or contribution accounting.

Both native clients retain the no-focus startup guard and never acquire native desktop focus.
The lead's Pack capture visibly identifies “From Mira · 1 Wood”; the final frame shows Wren's
completed platform, retained rations and reward. Both frames were visually inspected. General HUD
layout, character visibility, art, animation, audio and human usability remain provisional; the
large HUD obscures part of Mira in the final frame. This is no visual-quality or physical-input pass.
No independent critic ran under the owner's direct-work direction.

## Findings and retained failures

- Attempt 01: the coordinator chased a small route connector using held keys; remote feedback
  allowed it to overshoot and oscillate until the unchanged 96-input bound refused the route.
- Attempt 02: target cycling was requested while paused. The explicit fixture now resumes before
  target selection; production input permissions stay unchanged.
- Attempts 03–04: short 55-ms presses did not always produce a published movement sample. Waiting
  for presented readiness did not resolve this. No claim is made that focus or readiness caused it.
  The host stores the latest leased direction, rather than queuing every press as a movement command.
  The short-pulse fixture is removed. Held input remains the required movement evidence.
- Attempt 05 reaches the resource, then the second Gather receives `RevisionConflict`. The home
  probe had waited for a receipt but not its resulting observation. It now requires the action's
  node/craft/quest/dialogue effect in both current and presented views before returning. No rejected
  command is retried; authority conflict checks are unchanged.
- A subsequent planner build exposed an inaccessible internal geometry helper; use the public
  `PrepareMove` query for detached candidate analysis. This was a test compilation error. A client build command also named a nonexistent project file;
  it was corrected to the discovered `OpenFantasy.Client.csproj`. Both failures are retained.

The revised coordinator ranks actual reachable progress over a short movement envelope instead of
requiring an exact tiny connector. It cannot install its diagnostic placements into the host. Each
press still awaits an actual observed step, with the original deadline. The passing run qualifies
its own route only; it does not explain all earlier input/render timing failures or prove reliability.

All six raw runs are retained under `attempt-01` through `attempt-06`, with hashes and a complete
local archive/restore location in
[RAW_EVIDENCE.json](RAW_EVIDENCE.json). Compact reports and reviewed frames are committed under
`evidence/`; raw mailbox repetition is excluded from normal Git staging. [WIP_BACKUP.json](WIP_BACKUP.json) records a complete local
archive and restore instructions. Checkpoint `codex/checkpoint-shared-adventure-wip-20261003-04`
preserves source, checks and manifest at `e126d9a76689d378392ad09dfb300c697296a34e` before the steering
correction; it is not an acceptance claim.

## Documentation review

- **Plan/spec:** [CURRENT_ROADMAP](../../CURRENT_ROADMAP.md) retains P02 shared participation and
  the full expedition exit. It explicitly adds the party-retreat contract dependency: shared roster
  scenarios bind all humans to one side, incompatible with the existing lone-combatant retreat law.
  No baseline requirement, full scope, original validation gate or archived spec is removed.
- **Reference:** [player rosters](../../../Runtime/docs/architecture/player-rosters.md) explains
  independent contribution, material consent, quest reward ownership and autonomous work.
  [Encounter retreat](../../../Runtime/docs/architecture/encounter-retreat.md) corrects the stale
  profile-34-only claim, identifies inheritance through profile40 and explains the party restriction.
  [Road threat](../../../Runtime/docs/architecture/road-threat.md) also names the inherited capability
  accurately. [Player client](../../../Runtime/docs/architecture/player-client.md) distinguishes
  committed receipts from the issuing player's current and rendered observations. Timeless contracts stay in the reference; this packet and roadmap retain status and failures.
- **Accuracy/completeness:** lead source review compares `WorldStepBatch`, `LocalProfileBindings`,
  `WorldEncounterRetreats`, roster admission, existing Pack consent and native action routing.
  Profile capability inheritance does not expand retreat law v2. Shared input does not grant shared
  rewards or control. No new gameplay law/schema is introduced. Independent review is unrun under
  the owner's no-orchestration direction; no critic or human acceptance is invented.
- **Checks:** the documentation checker passes all seven scoped files with no errors
  ([report](checks/documentation-final.json)); source review above is separate from that mechanical check.
  Native and regression/build results are listed above. Independent/human/full-P02 gates remain open.

The maintained verification skill additionally states that architecture and living plan/spec upkeep
remain priority deliverables under efficiency tuning. Ecosystem commit `70401b8` is synchronized to
all three installed skill copies with exact hash verification and guarded backups recorded at
`/private/tmp/documentation-priority-20261003.backups/receipt.json`. Frontmatter is byte-identical;
`quick_validate.py` could not run because its Python environments lack PyYAML. The four-line prose
change received direct review and a whitespace check; no validator pass is fabricated.
