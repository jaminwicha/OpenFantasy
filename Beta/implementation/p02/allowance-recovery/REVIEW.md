# Independent review and integration record

The native read-only critic reviewed storage atomicity, activation provenance, replay, fencing,
fixed-base timing, shutdown and event/row transitions. No external provider request occurred;
native workers and the critic consume Astra capacity. The prior provider spending limit remains terminal.

## Finding and disposition

P2: schema-ten live commits refused expiry evidence greater than 30,000 ms, but a self-consistent
post-activation journal could replay 30,001 ms. The Core rule legitimately retains its broader legacy
interpretation; operational recovery must enforce its selected finite policy at replay too.

`ControllerRecoveryReplay.ValidateEvent` checks every post-activation controller expiry against the
30,000 ms constant. Earlier schema-nine history retains its original interpretation. Three new
constructed-history cases cover fresh and upgraded rejection and pre-activation compatibility.
The critic inspected the repair and found the issue resolved in source, with no residual defect.
The critic did not execute tests; executed evidence belongs in `FINAL_CHECKS.json`.

The review also confirmed validation before unchanged-progress no-ops, immutable activation compared
at its original replay boundary, current rows compared at the final head, one fixed elapsed subtotal,
and one stop-request timestamp. Crash-tail duration is unknowable and is not bounded by a nominal
50 ms host cutoff; repeated crashes can extend effective grace. That limitation is documented.

## Integration failures

The initial build failed xUnit's cancellation-token analyzer in the new process-proof cleanup.
The test now carries the suite cancellation token through the bounded process join. Original build
logs remain in `qualification/`; no rule, assertion, threshold or old test changed.

The first five host journeys produced two passes and three fixture failures. Two called an unchanged
legacy comparator that requires profile 36's 27 fields, against profiles 37/38 with 28/29 fields.
The new journey now compares every canonical field under its explicit profile layout, retaining the
same controller/revision exceptions and lifecycle bookkeeping checks. The old comparator is untouched.
The third kept a reopened exclusive authority alive while opening the same database for journal
inspection; the scopes are now sequential. Original timing and replay assertions remain required.

Final source-pinned qualification passes all 44 new cases and affected regressions; see
`FINAL_CHECKS.json` for the 933 distinct selected cases, eight actual Host kills and rendered proof.
The lead directly inspected all three native captures: Pending blocks input, Restored offers Resume,
and Available reports resumed control. The player and control text/buttons are visible. The pending
header's height unit wraps within its allotted space; this prototype presentation has no new human
or production-art acceptance. No new client/art source changes were part of this increment.

The operator's source inventory also observed an empty WAL and SQLite shared-memory sidecar. These
transient bytes are preserved as `source-wal-observation.bin` / `source-shm-observation.bin`; they are
diagnostic observations, not permanent source-file dependencies. Final checks retain their original
hashes while separately checking the immutable source files and all seven unchanged canonical tables.
