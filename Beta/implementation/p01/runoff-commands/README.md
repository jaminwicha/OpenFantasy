# Durable funded runoff controls

This checkpoint adds admission and cancellation of already-funded runoff commitments to the
existing profile-eight world authority. It extends the
[physical reservoir binding](../reservoir-admission/README.md); it does not move liquid, generate
rain, prove terrain walls or complete the P01 hydraulic stages.

## Implemented contract

`AdmitRunoffIntent` and `CancelRunoffIntent` pin the complete reservoir hash, capacity revision and
an existing reservation identity, alongside the ordinary actor/control pins. The resolver checks
authorization before private funding, re-prepares admission against the current fixed domain and
publishes through the existing capacity and physical wrapper plans. Start time cannot precede the
selected advancing step's publication calendar. Existing reading holds, including a dialogue opened
earlier in the same batch, block both operations.

Cancellation closes the reservation permanently and releases only its incoming capacity claim.
Source lots, source holds, actual volume, geometry and Production remain exact. A prior cancellation
can make room for a following material action; an earlier accepted material action invalidates an
old complete-owner control pin. Both canonical actor orders are tested. Existing command/result
identity, rejection, transaction and exact history rules apply to capacity-only changes as well.

The bounded action-version-six encoding adds no world snapshot format or database schema. Older
profiles reject these controls; prior command encodings and global byte limits remain unchanged.
The existing world-step operator executes them. Player-facing offers and automatic scheduling need
separate bindings. The default farm and its presentation are unchanged.

The review found a real composition defect: independently valid runoff and quest/construction
domains could share a reservation ID. World admission now rejects that overlap for pending and
closed runoff IDs against every quest/project declaration, regardless of current activity. The
command path checks the same purpose rule. This strengthens admission rather than assigning two
meanings to one material hold.

Timeless contracts are maintained in
[world and geography](../../../Runtime/docs/architecture/world-and-geography.md#runtime-binding-funded-runoff-commitment-controls),
[persistence and history](../../../Runtime/docs/architecture/persistence-and-history.md) and
[runtime authority](../../../Runtime/docs/architecture/runtime-and-authority.md).

## Qualification and retained failures

[Machine-readable results](qualification/verification-summary.json) record the exact completed
checks and their limits. The full solution builds without warnings or errors. The managed regression
passes **1,059 cases: 692 Core and 367 Hosting, zero skips**. The 23 added cases exercise bounded and
malformed codecs, old-profile exclusion, exact funding, stale owner/actor pins, publication-time
boundaries, authorization, holds, both action orders, competing purposes and durable replay.

[The new fixture](qualification/fixture/expected.json) records admission, cancellation, stale
cancellation, second cancellation, closed-ID readmission refusal and repeated cancellation refusal.
The real host harness independently reads all six action frames, kills actual processes before and
after committing the first capacity-only operation, reopens exact states and reads all seven history
boundaries out of order without altering the database. Prior reservoir and network fixtures are
reused read-only in separate regression runs, including their own two kill boundaries. These checks
do not stand in for hydraulic timing, representative performance or player acceptance.

Actual Godot headless foundation and farm create/reopen checks pass. No rendered, foreground,
physical-device or export acceptance is claimed. The full all-systems verifier was not run; its
new hook is covered by invoking the same targeted harness directly. All 182 architecture links,
22 frozen specification hashes and the exact unrelated 19-file predecessor index are preserved.

Retained failures are part of the evidence:

- `test-first.log`: the new cancellation API did not yet exist; compilation failed as expected.
- `reservation-regression-before.log`: both active and closed reservation collision tests failed
  against the old admission rule because no exception was thrown. They pass after the actual fix.

[Bounded native critique](qualification/critic-review.md) covered the resolver, common action gate,
codec, profile/session guards, tests and affected reference. It found no remaining actionable defect;
the critic did not execute tests or qualify performance. No threshold, timeout or acceptance gate was
relaxed. No Claude call or additional external delegation was used.

## Remaining obligations

Source funding must already exist; this increment neither allocates new reservation IDs nor releases
canceled source holds. The fixed finite domain and authored watertight boundaries remain assumptions.
Terrain support, inlet/outlet topology, arrival/settlement, backpressure, live flow, phase changes and
encounter-scoped hydraulic timing still need explicit implementations and proofs. Maximum combined
workload, streaming, full geography/history, migration, production UI/art/audio, co-op, devices and all
full campaign gates remain open. Continue from the full plan and current handoff without treating this
bounded checkpoint as a completed fluid simulation.
