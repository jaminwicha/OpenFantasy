# Complete-world binding review

This source review prepares the binding after the production implementation. It does not qualify
an executed world action, upgrade, Host route or UI. The retained personal-dose objective remains
the end-to-end acceptance target, including the original save and failed acquisition evidence.

## Existing boundaries that must be extended together

| Current source | Consequence for the gift binding |
| --- | --- |
| [WorldActionResolver](../../../Runtime/src/OpenFantasy.Core/Worlds/WorldActionResolver.cs) | Ordinary giving checks source-owner transfer policy, current actor control, target/actor revisions, body manipulation and actual spatial reach. It moves only raw stock. Bilateral consent must compose these physical checks, not replace them. |
| [WorldStepBatch](../../../Runtime/src/OpenFantasy.Core/Worlds/WorldStepBatch.cs) | Actions are owned by actors and admitted under explicit profiles and timing boundaries. A paired transaction must account for both actors' opportunity/readiness and any competing action, rather than counting only its initiating actor. |
| [LocalWorldSession](../../../Runtime/src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs) | The player session checks the actual selected actor and delivery before fresh submission. An embedded recipient principal in a payload is not a second authenticated submission. NPC proposals need their real admitted controller path as well. |
| [WorldCommitPreparation](../../../Runtime/src/OpenFantasy.Hosting/Worlds/WorldCommitPreparation.cs) | Every terminal receipt must match an actual accepted command by principal, scope, commit and content hash. Adding an extra recipient receipt without representing that participant's actual submission would violate this invariant. |
| [SqliteWorldAuthority](../../../Runtime/src/OpenFantasy.Hosting/Worlds/SqliteWorldAuthority.cs) | Commit preparation, historical occupancy, identity writes and publication already execute under one lock and SQLite transaction. Use this boundary for the bilateral join. `FindReceipt` checks current control and exact command bytes before exposing a prior result. |
| [WorldCoordinatedAdmission](../../../Runtime/src/OpenFantasy.Core/Worlds/WorldCoordinatedAdmission.cs), [WorldSnapshotCodecV27](../../../Runtime/src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV27.cs) | The coordinated world selects its exact production context and V9 interpretation after a cut. A new material codec alone neither changes this selection nor admits a gifted transformed lot. An explicit new world interpretation and matching admission are required. |
| [WorldProfileMigrationReplay](../../../Runtime/src/OpenFantasy.Hosting/Worlds/WorldProfileMigrationReplay.cs) | Only the existing production, quest and construction migration tags are dispatched. There is no generic latest-profile upgrade. A consented-transfer upgrade needs its own exact transition and replay path. |

The current `Commit(WorldStepBatch)` API is a privileged host-selected input, not a public wire
authentication endpoint. Its reconstruction of command delivery claims must not be copied into a
network acceptance path as though that authenticated a counterparty.

## Recommended asynchronous transaction shape

Use separate proposal and acceptance commands with an explicit persisted offer owner. A proposal's
terminal outcome records that the offer was created, or rejected; it is never rewritten to claim
the items were later transferred. Acceptance references the exact admitted proposal and its terms.
Its terminal outcome records the actual atomic movement or refusal. A transfer event links the two
submissions and can be projected to both authorized participants without inventing another action
or publishing the same command twice.

This preserves the existing distinction between command outcomes and later world consequences.
The sender can inspect its offer's current state while retaining an immutable original command
receipt. Exact retries return the prior outcome rather than reserving or moving stock again.

An unauthenticated acceptance cannot consume the claimed sender's identity, expose the sender's
receipt, or replace an existing offer. Authenticated failed attempts may receive a terminal result
under their own command identity. Reusing that identity with changed content remains a conflict.
The durable schema must retain actual authenticated provenance and typed participant roles; a
decoded receipt or a hash match is not sufficient evidence.

Current Core bilateral contracts require both participants' current control. A stored delivery
record proves historical submission, not current authentication. The full binding must explicitly
revalidate the actual offer owner's current control and the accepting delivery at the commit
boundary. Its policy must say whether disconnect/control change cancels an offer or permits an
explicit reauthorization. Do not silently reinterpret an old session as a current one. The initial
recommendation is invalidation on loss/change of the offer's admitted control, with a new explicit
proposal after reconnect; this remains a binding decision to qualify, not implemented behavior.

## Preserved-world transition and acceptance sequence

1. Create a new-file backup of the original retained world through the existing exclusive writer
   boundary. Verify its complete journal, archive lineage and exact head before any transition.
2. Admit an explicit profile/rule transition from that exact head. Preserve original genesis,
   material rights, stocks, policies, care/resource history, calendar and prior receipt bytes.
   Adding the new transaction interpretation must not mint a dose or repair its missing source.
3. Replay the migration deterministically through a closed version/tag dispatch. Reopening the
   original remains the original; no fallback world or rewritten genesis qualifies migration.
4. Exercise sender proposal, recipient acceptance/refusal, stale stock/control/reach, cancellation,
   expiry, competing use, duplicate/reordered delivery and both participants' authorized views.
   Keep timing/opportunity effects and failure classes explicit.
5. Prove normal commit plus BeforeCommit/AfterCommit crash recovery, including actual pair identity
   reservations, exact terminal outcomes and material conservation across a further history cut.
6. In the original world's admitted continuation, acquire an existing legal dose through the
   bilateral route and use it through ordinary care. Native input, reopened state, archives and
   complete material/resource/care/time evidence must agree. If no actor actually owns an eligible
   dose, that is a content/reachability problem to solve through admitted play, not an inventory edit.

The production assignment is a dependency of these steps. A fresh-world fixture can prove new
mechanics while the retained-world migration remains open; it cannot replace that proof. This
review does not change existing timing, compatibility, source-preservation or human acceptance gates.
