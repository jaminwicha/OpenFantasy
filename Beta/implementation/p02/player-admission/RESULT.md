# Authenticated player admission checkpoint

The host now issues bounded expiring credentials for actual existing human control bindings and admits
compatible local/WebSocket handshakes into independent player ports. Clients cannot select another
actor by sending identity claims. Exact world/branch/profile/admission/package pins, duplicate-owner
exclusion, initial unready state, bounded admission and safe cleanup are enforced. This is a real join
to the shared SQLite-backed host, without another writer, clock or saved control assignment.

The [final checks](FINAL_CHECKS.json) record a clean full solution build and **48 distinct focused
passes**: 18 protected transport cases, 21 new admission cases and all 9 prior shared-player cases.
The 39-case network selection includes actual loopback TCP/WebSocket admission; HTTP Upgrade and TLS
are not exercised. Build, network and shared checks took 4.175, 8.569 and 40.707 seconds respectively,
inside unchanged 240/120/600-second limits. Exact current sources and binaries were checked again after
completion. The old transport and shared-player case sets are preserved, not merely equal in count.

[Independent critique](CRITIC_REVIEW.md) identified a transport-close failure that stranded an actor
port and host shutdown during backpressured Welcome. Both had retained failing counterexamples before
correction, and both pass unchanged afterward. Existing tests/specs and the exact unrelated 19-file
predecessor staging are unchanged. Two earlier compilation failures remain recorded; none was bypassed.

The [architecture reference](../../../Runtime/docs/architecture/player-admission.md) defines trust,
wire formats, credential lifetime, host serialization and cleanup. All 120 local links across five
affected architecture pages resolve. The pre-change transport commit is 6f3a891; its verified backup
ref is in CHECKPOINT.json. No provider request, fallback or usage-savings claim was made.

## Remaining implementation and gates

The next coherent increment routes readiness, leased movement and canonical commands through these
channels, retaining exact committed receipts and a responsive bounded receive loop. The listener needs
HTTP Upgrade, validated TLS for remote clients and explicit trusted provisioning. Public observation
encoding must include actual regional data, participant visibility and recovery/deltas without private
root counters. Godot clients, solo envelope parity, native 1–4-client proof and the recorded disconnect /
30-second reconnect / controller-handoff policy remain required. The complete campaign, earlier unclosed
phase gates, art/device/human acceptance and production scale remain unchanged. This checkpoint does
not claim the rebuilt game has passed the earlier whole-game qualification; its claim is the 48 affected
cases above. Preserve prior reports and rerun affected families when binding the next gameplay slice.
