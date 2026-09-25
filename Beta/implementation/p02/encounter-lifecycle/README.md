# Coordinated encounter lifecycle checkpoint

The core/application remain pure C#. Fresh profile 24/schema 6 connects ordinary exploration to
authenticated scenario entry, in-place confrontation/staging, real physical combat, outcome release
and another encounter. The complete terrain/liquid/farm owners persist throughout. Older 22/23 saves
retain their meanings; no migration or native-language layer is introduced.

The [contract](CONTRACT.md) defines this bounded implementation. The maintained
[architecture reference](../../../Runtime/docs/architecture/encounter-lifecycle.md) explains ownership,
canonical state, session observations, identity reservations and extension boundaries. The
[review](REVIEW.md) retains critic findings and development failures.

## Actual journey

The unchanged retained Squire and Bramble Wolf source records drive two explicit four-resident
opportunities. Wren and the unselected wolf are coupled bystanders. A real exploration step precedes
entry; staging preserves actual positions without teleportation or elapsed time. The first duel ends
with the chosen wolf at HP 0 and the player at HP 2. This is incapacity, not corpse creation or death.

Release retains the last action archive, player debt 125, defeated-wolf debt 59 and 300 ms process carry.
Immediate entry into the second scenario preserves them. A premature player Strike receives an
ordinary Unavailable receipt, with no time or random draw. An explicitly selected, costed second-wolf
Wait then permits one tactical tick: player debt becomes 120. This controlled Wait isolates recovery;
it does not claim that the separately tested automatic attack policy volunteers such a respite.

The complete main trace has 188 states, 187 commits, 18 command receipts, one exploration step and 151
tactical ticks. It ends at 9,350 ms exposure, five earned process intervals and 350 ms carry. All 6,000
liquid units, bodies, terrain, inventories, currency, excavation and construction ownership remain
preserved. Original source stats and costs are never edited to fabricate victory or recovery.

A separate continuation from the exact closed release save pays ordinary recovery. Incapacitated
actions, cooperation and movement refuse; the recovering player cannot move in the same step that
pays its debt. Actual later walking, conversation, quest funding and Wren's 200-work construction
complete and reopen exactly. This branch is managed-test evidence; the independent oracle does not
claim to reconstruct its movement or work algorithms.

## Verification

- The full regression passes **1,150 Core + 629 Hosting/client = 1,779** cases, zero failed/skipped.
  A subsequent narrow static-identity admission correction and two added cases pass a rebuilt
  **1,150 Core + 46 encounter-focused Hosting** cases. That is 1,781 distinct managed cases across the
  qualification, not a claim that the entire Hosting suite ran again after the final correction.
  Both solution builds have zero warnings/errors. [Logs and counts](qualification/test-summary.json).
- The [independent Python oracle](../../../Runtime/tools/check_encounter_lifecycle.py) reconstructs
  **all 188 complete roots**, deterministic damage, phase/debt/exposure/carry, receipts and every SQLite
  journal/identity row. All 17 ordinary Host operations pass, with a maximum 1.238 s against unchanged 30 s bounds; actual
  Host creation matches the source-backed genesis. External before/after
  commit kills exercise both entry and release, including exact reopen, uncommitted retry, duplicate
  refusal and changed-history rejection. [Execution record](qualification/external-oracle.json).
- Source critique caught caller-selected damage randomness, unsupported speed, generic command-ID
  poisoning and static-layer identity collisions. The final world reserves complete episode-ID
  families and validates them against all identity layers. Seven poison/collision regressions cover
  current/future names, command/cause aliases and valid alternate terrain bases. Earlier exact
  receipts remain recoverable. [Review and failure dispositions](REVIEW.md).
- Session tests cover unrelated clock commits, exact request retry, paused restart, staging without
  elapsed time, early-release refusal, legal automatic wolf selection and the required-human barrier.
  Old profile 23's tracked genesis is compared byte-for-byte, including its pinned SHA256.

The [prior physical-action oracle](qualification/prior-actions-control.json) still reconstructs all
176 old roots and 175 commits, passing 21 Host operations and four external kills without changing its
fixtures. The [headless native farm](qualification/native-farm.json) passes actual movement, conversation,
finite handover and exact reopen; [Host/Godot foundation](qualification/foundation-controls.json) agrees
on deterministic fields. [Preservation](qualification/preservation-and-links.json) confirms all 44 prior
milestones, 22 frozen specifications, 550 architecture links and the unrelated 19-file staged index. Verification is serialized against this task's heavy builds/tests; no foreground Godot
window is required or commandeered.

## Remaining scope

The ordinary native game launch is unchanged. The new operator is
`--create-encounter-journey <database> <seed> <player-name>`; native battle controls, target/action UI,
choreography, animation, audio and player acceptance remain the next presentation connection.

Release is supported only for an actual outcome and living authorized initiator in the complete dry
terrain/liquid interpretation. Flee/disengagement, rescue of an incapacitated initiator, healing,
broader effects, death/rewards/respawn, joining/merging, independent co-op, wet/structural hazard closure,
crowd continuation, long-lived history and production scale remain open. Maximum-root reserve is
enforced by admission; near 16 MiB experimental qualification is not inferred. No full phase, platform,
art, audio, device, migration or campaign gate closes at this checkpoint.
