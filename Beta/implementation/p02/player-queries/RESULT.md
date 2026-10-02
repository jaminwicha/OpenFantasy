# Authenticated trade and history queries

The authenticated player channel exposes the existing shopper's submit/cancel/read inquiry and
participant-only paid-quote/material-proposal inspections. Requests cannot choose a controller or
delivery. Typed results bind the request kind, subject and world/branch/actor scope. A pending query
has its own bounded slot, preserving readiness/movement/ping processing and independent action work.
Replies use the existing bounded output queue; closure joins admitted history reads safely.

Actual merchant selection still produces quotes; inquiry submission reserves nothing. Acceptance is
a separate canonical world command with its original durable receipt. Unknown and foreign historical
subjects both return absent values. No live/archived authority, pricing, consent, control or game rule
changes. No Core or frozen-spec changes. The [architecture reference](../../../Runtime/docs/architecture/player-queries.md)
documents exact wire forms, ownership, pressure and lifecycle behavior.

## Evidence

[Final checks](FINAL_CHECKS.json) pin the exact runtime/test source and built assemblies. The solution
build passes; **228 distinct affected cases pass**: 188 networking, 31 retained trading/history and
9 shared-player cases. All 147 preceding networking cases are retained. The **41 new cases** include:

- Complete bounded requests/results, canonical value preservation and prepared acceptance parity;
  hostile lengths, versions, fields, scope, participant, status, closure and settlement totals.
- Real local and loopback WebSocket inquiry → merchant-authored offer → separately accepted purchase
  → original saved receipt and filled-term inspection. A pending action does not block query replies.
- Real archived purchases and NPC-to-NPC trades, comparing the player's own exact history against
  foreign/unknown results. Archived material proposals preserve grant hashes, terms and closure.
- Identical inquiry submission, conflicting request refusal, cancellation and ephemeral-state loss
  on authenticated reconnection; no world advance or resource mutation caused by the inquiry itself.
- A pending read behind the actual authority lock: bounded second-query refusal, readiness/movement
  and ping responsiveness, then successful completion or joined connection shutdown.

The [independent critic](CRITIC_REVIEW.md) found no actionable defects in the bounded source review.
Failed build/focused-test reports remain retained: the lead corrected only new test fixture usage;
the real NPC-as-player refusal was preserved and the privacy test now uses a genuine player.
All 628 prior tracked test/fixture files and 23 frozen specs remain byte-identical. The unrelated
19-path predecessor staging remains exactly as found at this increment's start; its starting hash
differed from the previous packet, and no attempt was made to restore or commit those unrelated bytes.

## Scope and continuation

Before-change checkpoint: `codex/checkpoint-before-player-queries-20261002T092308Z` at `df2b1d7`.
Verified milestone: `codex/milestone-player-queries-20261002`. Work stays on `codex/runtime-foundation`.

This proves the bounded query boundary, not production networking or a phase exit. Historical replay
still holds the host serialization gate and can delay cutoffs; no production query latency/scale claim
is made. HTTP Upgrade/TLS and provisioning, actual Godot network clients and solo envelope parity,
native 1–4-player qualification, journaled disconnect/reconnect/controller handoff, lived migration,
full world/campaign/content scope and art/platform/device/human acceptance remain open.

No provider request was made after the terminal monthly spend-limit failure. The coding helper and
critic here were native Astra-side agents, not Claude work or evidence of Claude credit savings.
