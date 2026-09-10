# Independent client and session review

The final inspected client/session source has no remaining blocking defect identified in this bounded review. Three concrete lifecycle findings were reported to the coordinator and corrected. Runtime engine lifecycle/interaction probes remain separate from this static review and managed test execution.

## Scope and independence

Reviewed the coordinator's `FarmClientSession`, `FarmGame`, `FarmHud`, `FarmInputMap` and the input-installation call in `Bootstrap`. Reviewed the other worker's `LocalWorldSession`, `LocalWorldView`, `LocalNpcController`, `PendingLocalAction`, session contract and 12 session/controller tests. This critic did not edit those implementation files.

The critic authored the composed authority service and its 16 tests. Their supplemental execution in the same run is implementer verification, not independent review of that service. The source roles and exact hashes are recorded in `REVIEW_VALIDATION.json`.

## Closed findings

| Finding | Original failure | Final disposition |
| --- | --- | --- |
| CS-01: close while opening | `CloseAsync` could acquire and dispose the completed opening while `_Process` simultaneously adopted that result and called `SetReady` on a stopped session. | `_Process` now returns whenever closing is pending. The close operation exclusively owns opening-to-disposal during shutdown. Closed by static inspection; native timing probe owned by coordinator. |
| CS-02: leaving the scene | `_ExitTree` disconnected signals but did not release the background session/SQLite owner if no window-close signal had occurred. | Every tree exit starts or reuses `CloseAsync` and observes its exception without background Godot calls. Closed by static inspection; scene-removal probe owned by coordinator. |
| CS-03: readiness after host failure | Pause/focus/startup callbacks could call `SetReady` after the host failed but before the next `_Process` noticed `Completion`, allowing an uncaught lifecycle exception into the Godot callback. | One guarded `ChangeReadiness` helper routes failures through the existing error display and guarded readiness stop. Closed by static inspection. |

## Authority, pacing and failure behavior

The UI samples cardinal movement and requests semantic actions. It supplies no elapsed game time and performs no custody, dialogue or NPC-state mutation. Native diagnostics are not queried every host step. `LocalWorldSession` owns one admitted authority, serializes request lookup/selection/commit, and begins unready. Readiness changes clear movement. Its monotonic 50 ms selection schedule discards missed deadlines, and movement expires after 150 ms without renewal. A step already selected can finish committing after readiness changes; no subsequent step should run while unready. That boundary is explicitly documented rather than promising cancellation of an in-progress durable transaction.

Pending identical action bytes coalesce into one task. Different new requests are refused while one is pending. An exact prior receipt is checked through current actor/delivery authorization. The session publishes a new immutable view and completes the request only after the authority returns from Commit. A preview produces no early acknowledgement.

Dispatch conflicts can reject one pending request and leave ordinary NPC/clock selection able to proceed. A reading hold has no empty fallback commit. Distinct recovery-limit exceptions and IO/backend failures fault the session and pending request; they are not converted into actor gameplay receipts. Stop cancels uncommitted input and closes the owned database after the loop ends.

NPC home movement is versioned and deterministic from accepted world/actor/cycle values. The policy selects through explicit host actor bindings and the same body/terrain/movement rules as the player. Replaying accepted batches does not rerun live NPC selection. This is a narrow home-amble policy; it does not qualify full NPC cognition, narrative motivation or migrated predecessor behavior.

## Projection and local-trust limits

The immutable view contains the current player's inventory and addressed dialogue. It excludes NPC inventory/source claims, global commit revision, world hashes and native storage diagnostics. Input setup is actually called by `Bootstrap`; declaring `FarmInputMap` alone was not mistaken for installation.

The current farm declares a sight-complete local presentation profile. `LocalWorldView` enumerates all admitted actors and exposes the global reading-hold flag under that assumption. It does not compute generic line-of-sight, discovery, hidden-actor or multiplayer knowledge permissions. The flat, fully disclosed prototype can use this boundary, but it must not become the general observation pipeline for occluded/secret/other-player content without an explicit visibility contract. This scope limit was reported to the coordinator and is also recorded in the session README.

The embedded client is a trusted local operator adapter: it resolves the accepted local player binding from its own save. This is not remote authentication. The client rejects mismatched world/content admission rather than resetting a save, and retains an opening failure for display.

## Executed evidence

The critic independently executed the 12 worker session/controller tests and reran the 16 authored authority-service tests against stable Debug binaries with the pinned SDK root: **28 passed, zero failed**. The retained TRX is `test-results/client-session-review-debug.trx`.

The managed tests exercise controlled host deadlines/readiness, input freshness, no catch-up, pending request coalescence, exact receipt retry/restart, dialogue holds, private preview fallback, NPC parity, cancellation/disposal, backend failure propagation, read-only projections and invalid actor/session claims. The service cases additionally exercise actual SQLite commits/recovery and the 6,000-step checkpoint boundary. They do not render native scenes or inject native focus/close events. Windows/Linux exports, power loss, large-world performance and production save lifetimes are not qualified by this review.
