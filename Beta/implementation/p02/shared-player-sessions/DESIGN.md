# Shared player sessions

The existing local host owns one world scheduler, but binds input and views to one human. Creating
another local host for each player would duplicate cutoffs, autonomous work and authority disposal.
Existing two-human outcome tests use direct extra writers and do not prove connected-session behavior.

Introduce up to four authenticated in-process player connections to that same host. Each connection
owns its input lease, one outstanding command, readiness, participant view and inspection slots.
The host retains the sole timer, durable authority, NPC controllers and mutation serialization.
The existing local API forwards to its original player connection, preserving solo behavior.

Sample all ready players' movement together. Serve waiting actions fairly in stable actor order with
a rotating cursor, at most one human action per selected cutoff (the existing host boundary policy).
Simultaneously queued combat choices share one captured decision boundary, so selecting one player does not stale another player's choice. Authority still decides legality and canonical world effects. Encounter choices and readings from
every admitted controller reach this queue; an inactive/absent required human is never auto-played.
Any ready connection permits the host clock; none pauses it, apart from already accepted physical
consequence completion. Menus or another player's focus loss do not impersonate a world reading.
Closing a port clears only its uncommitted input and transient inquiry, never disposes the authority
or cancels another player. Reopening an actor requires a fresh authenticated port and starts not ready.

These are in-process host connections, not sockets. Network authentication, recorded confirmed
disconnection/30-second handoff, independent encounter pockets and native multi-client transport
remain full-plan work. No controller reassignment, save/profile transition or simulation rule change
is part of this increment. Connection closure alone is not a recorded network disconnect decision.

Prove multiple actual connections driving encounter decisions and outcome readings without direct
extra commits, differing observer projections, forged-controller refusal, per-player input ownership,
zero-ready pause, fair service, independent close/reconnect and exact durable reopening. Preserve old
tests/specs and use focused session plus coherent Hosting regression. Critique the concurrency and
ownership boundary once; no external model request while the recorded spending-limit failure holds.

Preserve genuine stale outcome commands as durable rejections. After rejecting one, continue the
already-due host cutoff once from the new root, without automatic retry/reselection. This preserves
client observation compatibility without granting a world-pause entitlement to rejected traffic.
Only the legacy initiator's premature release for its current episode retains that Confrontation/Staging choice boundary.

At Outcome, already-queued ready patient care for the exact current episode precedes other traffic and
completion. Once care is accepted, its original patient may receive one separate stale-offer refusal
while the world revision still equals the retained acknowledgment sequence and the request matches
its agreement and episode. Only a published receipt can occupy that cutoff; failed preflight continues
the due helper action. The incremented durable revision closes the allowance across reconnect/reopen,
so later requests cannot starve treatment. Rejected traffic from another patient/episode grants no hold.
These are scheduling boundaries;
Core remains the sole owner of care and lifecycle legality, including stale/history adjudication.
