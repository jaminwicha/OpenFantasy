# Authenticated gameplay routing checkpoint

Authenticated local and WebSocket channels now carry readiness, leased movement and exact world
commands into the existing host. One outstanding action does not block readiness. The host alone
selects/commits work; responses carry its original canonical receipts. Preflight refusal and pending
Busy are explicitly separate transient messages. No command is recaptured, renumbered or auto-retried.

[Final checks](FINAL_CHECKS.json) record a clean solution build and **69 distinct affected cases**:
60 network cases (39 retained + 21 new routing) and all 9 prior shared-player cases. Exact old case
sets, current source and binaries are verified. Build/network/shared elapsed times are 14.036 / 10.668 /
42.466 seconds within unchanged 240/120/600-second limits. All original tests/specs and the exact unrelated
19-file predecessor staging remain unchanged. Earlier focused reports, including the failure, remain.

Evidence includes:

- Pause/resume through the receiver while a command waits; one original durable action receipt.
- Forged actor/principal/session/world/branch refusal, pending duplicate Busy, and actual committed
  revision-conflict rejection without confusing those outcomes.
- Exact committed retry while unready; changed bytes under the same command identity refused.
- Shared movement/time, independent peer continuation, and closed-port exclusion.
- Actual loopback TCP/WebSocket action and exact SQLite receipt, followed by clean Goodbye.
- Response loss after real commit, real database reopening, exact receipt retrieval, unchanged item
  spending and a reproduced recorded history. This is controlled response loss, not a process-kill test.
- Absolute send timeout, response-queue exhaustion, no-response input-rate exhaustion, idle host
  shutdown and Goodbye/host shutdown with a paused pending action and no invented commitment.

[Independent review](CRITIC_REVIEW.md) requested useful boundary tests; those are included. The retained
failure was fixture cleanup rethrowing an already-asserted InvalidDataException, now handled precisely.
Lead review also addressed host action-cancellation ordering without claiming the race was reproduced.
No gate, assertion, gameplay law or deadline was relaxed. The [timeless reference](../../../Runtime/docs/architecture/player-command-routing.md)
is current; 115 local links across four affected reference pages resolve.

The pre-change checkpoint preserves 3a94ee3 and the earlier admission milestone. This increment is
preserved at `codex/milestone-player-command-routing-20261002`. No Claude call or fallback was made.
This is an affected-component qualification, not a new full-game/native/platform/production acceptance.

## Next integration

Implement participant-safe regional observations/recovery and authenticated trade inquiries/quote and
proposal inspection, then bind the headless HTTP/TLS listener and actual Godot clients. Solo must use
these same semantic envelopes. Prove native 1–4-player operation and the full recorded disconnect,
30-second reconnect allowance, permitted controller handoff and safe return. The complete campaign,
content preservation, earlier open phase gates, art/device/human acceptance and scale remain required.
