# Durable gated reservoir transport

This checkpoint moves actual conserved water through explicit adjacent gravity gates inside the
existing world authority. It builds on [funded runoff controls](../runoff-commands/README.md) and
[physical reservoirs](../reservoir-admission/README.md). Its scope is finite, discrete, indivisible
delivery stages; it does not establish a continuous fluid solver or complete the hydraulic campaign.

## Implemented contract

Fresh world profile nine owns the complete physical reservoir state, an immutable gate catalog and
a bounded transport journal. A schedule pins actor/control and the complete transport owner, names
an existing funded reservation, and selects an explicit arrival boundary after its forcing interval.
Each wet basin routes its exact source interval to its own terminal receiver. Multi-destination
stages publish all source debits, destination credits and capacity retirement together, or none.

Gates have adjacent physical ports, a sill and one shared maximum delivery per exploration step.
The complete post-transfer source head must remain above the receiving surface and sill. Receiver
surface bounds include residual microlitres; ignoring even one can permit uphill delivery. Source
fragments retain provenance and use deterministic, globally checked identities. Material, head or
throughput blockage preserves both actual stock and incoming obligations for later reconsideration.

Actor actions resolve before due deliveries. A reading hold opened in the selected step prevents
delivery; held interactions do not advance time, and closing the hold provides no catch-up credit.
The retained processed boundary admits only the next 1,800-calendar-millisecond exploration step;
repeating a boundary is idempotent. Cancellation retains source stock/holds and records a distinct
outcome from completion. Profile eight retains its existing no-automatic-settlement behavior.

World snapshots/steps use version nine, scheduling actions version seven, and transport archives
their own version-one frame. Existing command encodings, schema-three transactions, replay, history
and global world byte limits remain unchanged. Fresh admission rejects an already-lived transport
journal even when bypassing the convenience factory. No automatic save migration is introduced.

The actual host can create and inspect this profile. Diagnostics report individual vessel contents,
incoming obligations, stage outcomes and the processed boundary. The all-systems verifier includes
the new fixture and process-kill harness. The default farm, art and player action offers are unchanged.

The timeless reference is [reservoir transport](../../../Runtime/docs/architecture/reservoir-transport.md),
with corresponding [persistence](../../../Runtime/docs/architecture/persistence-and-history.md),
[authority](../../../Runtime/docs/architecture/runtime-and-authority.md) and
[geography](../../../Runtime/docs/architecture/world-and-geography.md) updates.

## Qualification

[Machine-readable results](qualification/verification-summary.json) identify the exact runs and limits.
The full solution builds with zero warnings/errors. Full regression passes **703 Core +375 Hosting
cases**, followed by **three additional Hosting tamper cases**: **1,081 distinct passing cases, 22 new,
zero skips**. The final follow-up only adds tests; production sources do not change after the full build.
This combines full regression and focused follow-up, not one uninterrupted all-systems verifier run.

The [retained lifecycle](qualification/fixture/expected.json) schedules 600 litres, opens a reading hold
at arrival, rejects a held action, closes the hold, delivers on the next advancing step, and refuses
cancel/reschedule attempts on the completed reservation. Actual source stock changes **1,300 →700
litres** and receiver stock **50 →650 litres**; total actual water remains **1,350 litres** throughout.

The real operator harness independently checks admission/action/gate bytes, these known quantities,
and the autonomous delivery step. It kills actual processes before and after committing that delivery,
then verifies exact recovery and all nine historical boundaries in nonchronological order without
altering the database. Separate runs reuse prior reservoir, network and runoff-control fixtures,
including their existing kill boundaries. No timeout or assertion was weakened.

Tests also cover mixed-gate rollback without leaked fragments, exact source fan-out, historical ID
collisions, shared throughput, subunit head differences, ordinary material actions changing head,
same-step cancellation, unauthorized/stale schedules, old-profile rejection, malformed codecs,
fixed-owner rebinding and fresh-root enforcement. Three independently valid gate substitutions update
both bytes and stored hash in genesis, checkpoint and head; actual journal replay rejects all three.

Actual Godot headless foundation and farm create/reopen/history checks pass. The 199 architecture
links resolve; all 22 frozen specification hashes and the exact unrelated 19-file predecessor index
remain intact. These checks do not qualify rendering, physical devices, exports or performance.

## Retained failures and critique

- `test-first.log`: expected missing-API compilation before implementation.
- `domain-first.log`: a fixture exposed the invalid assumption that a fresh world's clock starts at
  zero. Creation now requires an explicit boundary; the fixture supplies its actual calendar.
- `world-cases.log`: an empty held batch violated the existing requirement for a discrete action.
  The fixture now submits a real rejected schedule while held; the runtime rule stays unchanged.
- `domain-and-codec.log`: the test analyzer required cancellation tokens for codec reads. The tests
  use the runner's cancellation token; no production behavior or gate was relaxed.

[Bounded native critique](qualification/critic-review.md) found two consequential ownership gaps:
bare physical-state rebinding could replace the fixed domain/history, and factory-only fresh-root
checks could be bypassed through direct world construction. Exact pinned plans, monotone internal
rebinding and world admission checks close those gaps. The final factual review found no remaining
consequential mismatch; critics did not run qualification or claim production acceptance. No Claude
or other external paid-model call was used.

## Remaining dependencies

The vessels and seals are authored assumptions over fixed geometry/topology. Terrain-supported walls,
construction/occupancy coupling, breaching, actor exposure, overflow and flooding remain open. Delivery
is an indivisible separately funded batch, not partial flow, a pressure-dependent rate or accumulated
elapsed-time discharge. Partial transport needs original/delivered/remaining source intervals and
remaining incoming obligations; reducing the original rainfall depth would corrupt its history.

There are at most 256 gates and 4,096 retained stages; exhaustion rejects new work. Lifetime compaction,
long-running/maximum mixed workloads, streaming, encounter-specific hydraulic Wait, phase changes,
lava/reactions, natural water generation, production performance and complete macro history remain
unqualified. New funding allocation, canceled-source release and player-facing transport controls
need explicit bindings. Full campaign, UI, art/audio, multiplayer, device and human acceptance gates
remain open. This checkpoint does not reduce the full specification.
