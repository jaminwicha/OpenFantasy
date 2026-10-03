# Shared adventure opening

Status: work in progress; no shared-journey acceptance. The parent accepted revision is
`ad448bb45bfbc37495bf28ae08436cd9b1209e0d`. Work proceeds directly without orchestration.

## Intended player outcome

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

## Findings and retained failures

- Attempt 01: the coordinator chased a small route connector using held keys; remote feedback
  allowed it to overshoot and oscillate until the unchanged 96-input bound refused the route.
- Attempt 02: target cycling was requested while paused. The explicit fixture now resumes before
  target selection; production input permissions stay unchanged.
- Attempts 03–04: short 55-ms presses did not always produce a published movement sample. Waiting
  for presented readiness did not resolve this. No claim is made that focus or readiness caused it.
  The host stores the latest leased direction, rather than queuing every press as a movement command.
  The short-pulse fixture is removed. Held input remains the required movement evidence.
- A subsequent planner build exposed an inaccessible internal geometry helper; use the public
  `PrepareMove` query for detached candidate analysis. This was a test compilation error.

The revised coordinator ranks actual reachable progress over a short movement envelope instead of
requiring an exact tiny connector. It cannot install its diagnostic placements into the host. Each
press still awaits an actual observed step, with the original deadline. A later passing run would
qualify its own route only, not explain all earlier input/render timing failures or prove reliability.

Raw runs are retained under `attempt-01` through `attempt-04`, with hashes in
[RAW_EVIDENCE.json](RAW_EVIDENCE.json). [WIP_BACKUP.json](WIP_BACKUP.json) records a complete local
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
  Timeless contracts stay in the reference; this packet and roadmap retain status and failures.
- **Accuracy/completeness:** lead source review compares `WorldStepBatch`, `LocalProfileBindings`,
  `WorldEncounterRetreats`, roster admission, existing Pack consent and native action routing.
  Profile capability inheritance does not expand retreat law v2. Shared input does not grant shared
  rewards or control. No new gameplay law/schema is introduced. Independent review is unrun under
  the owner's no-orchestration direction; no critic or human acceptance is invented.
- **Checks:** pending the revised build/native validation and documentation link/field check.
