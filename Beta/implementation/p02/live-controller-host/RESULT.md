# Live controller host binding

Status: bounded implementation qualified; full disconnect/survival and production gates remain open.
Source: controller-journal milestone `7aef4971`; roadmap-only commit `adb0893b` adds the owner's equipment
upkeep and direct vehicle/mount attack clarification without reprioritizing implementation.
The milestone `codex/milestone-live-controller-host-20261002` records this increment.

## Implemented behavior

- Established peer closure, invalid client protocol and transport failure enter profile 36's existing
  controller journal through the sole serialized world authority. Local disposal, failed Welcome,
  backend failure and deliberate shutdown retain separate semantics; old profiles gain no events.
- A monotonic, episode-bound allowance expires at 30 seconds, including with zero ready ports.
  External events earn no simulation time. Connected humans retain unlimited Wait.
- Rejoin is provisionally unready and preserves accepted actions/costs. Control returns only at the
  existing safe boundary. A lone returning actor can finish accepted care, staging or combat without
  a ready second player; new human choices remain required. Early readiness is discarded.
- Stale callbacks cannot detach replacement ports or restart a later episode. Participant receipts
  remain retrievable while new input is blocked. Transport classification preserves public exception
  types and observes origin before cancellation wakes a sibling loop.
- Final credential/deadline validation occurs inside host serialization. Revocation and expiry while
  queued refuse admission without failing the host. Established request abortion differs from shutdown,
  including the narrow interval after successful Welcome and before admission returns.
- The affected timeless architecture references describe ownership, timing, input and policy boundaries.
  No Core/schema/rules or original tests/specs were edited. No production selector enables profile 36.

## Evidence and limits

[Final checks](FINAL_CHECKS.json) bind all results to the corrected source and build:

| Selection | Result |
|---|---|
| Solution build | Passed, 21.623 seconds |
| Controller journal/live host/transport | 55 passed, including 46 new cases |
| Networking | 219 passed |
| Shared sessions and related scheduling | 26 passed |
| Managed server selection | 72 passed; Windows ACL unrun on macOS |
| Exact concurrent-failure diagnostic | The four-player TLS case passes alone under its original deadline |
| Native process cases | Headless client, rendered client and four-player roster journey pass |

Selections overlap: 341 distinct passing cases, 376 executions in the selected final evidence.
Earlier runs and the failed aggregate are retained, not included as extra coverage. The native cases
pass individually in a server aggregate that failed another case; that aggregate is explicitly **not
accepted**. The managed selection uses the prior native-roster checker's established exclusion of
native workloads. No test assertions or deadlines changed.

The concurrent native+managed server run missed the four-player TLS observation's five-second deadline.
The isolated case and subsequent managed selection pass, but do not establish the cause or close
concurrent-load/production timing qualification. The original failure, the initial unconfigured-native
skip aggregate and critic findings remain in [review](REVIEW.md). Windows execution is still required.

Actual Godot evidence includes seven processes, physical four-player assembly, required-human Wait,
reconnection, paid combat choices, outcome readings and exact saved-history verification under profile
35. The native journey completes in 175.315 seconds under its unchanged 240-second bound. This proves
compatibility of the new host/transport code with the prior native route; it does not prove profile 36's
unfinished client control-return flow. Rendered screenshots were inspected: prototype HUD, sprites and
overlapping actor nameplates remain; no new art or human/device acceptance is claimed. Disposable
background fixtures set no-focus before window creation and did not claim desktop focus.

[Archive manifest](EVIDENCE_ARCHIVES.json) hashes a verified 6,331,379-byte archive containing original
native logs, requests/responses, images, genesis and database. Extract `native-evidence.tar.gz` into an
empty directory to recover its `roster/`, `rendered/` and `headless/` evidence; each file has its own hash.
The working originals remain ignored, while the archive and manifest are committed.

[Document/preservation checks](DOC_CHECKS.json): 1,252 architecture links resolve; all 667 older
tracked test/fixture files and 23 frozen specifications remain exact; predecessor staging hash remains
`356898fb582b34244aa92b10c322e076422877ccba97e3ccc68b4fd6aa253aa5`. Core source is unchanged;
its previous full qualification is retained at the controller-journal milestone, not claimed as rerun.
The [initial checkpoint](PRESERVATION.json) and [pre-correction WIP snapshot](CRITIC_CHECKPOINT.json)
retain before-change states and verification limits.

## Next dependencies

1. Qualify the conservative survival selector and explicit co-player policy through ordinary paid
   actions. Autonomous mode alone must never count as implemented survival behavior.
2. Define and prove persisted allowance/restart reconciliation. Monotonic process timestamps do not
   survive restart; no offline simulation or invented elapsed evidence is permitted.
3. Expose pending control and bind fresh readiness to the restored client observation. The current
   boolean wire acknowledgement and prototype UI do not complete that product contract.
4. Prove profile 36 native disconnect, before/after allowance, reconnect/care/retreat and save/reopen
   journeys before enabling a production selector. Reconcile the concurrent timing concern and retain
   Windows/macOS/Linux, physical device, human usability and complete campaign gates.

The full implementation objective remains active. The upkeep roadmap belongs in its existing P04–P07
sequence, P11 catalog reconciliation and P12–P14 qualification. No Claude request or Claude credit
offload occurred; the worker and independent critic were native Astra agents.
