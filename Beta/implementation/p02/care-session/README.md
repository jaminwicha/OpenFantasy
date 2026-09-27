# Care session and combined archive publication

Baseline: `daca1b425b6ee190997ea4ea7f375a08cf012299`.
Pre-change checkpoint: `codex/checkpoint-before-care-session-20260927T212325Z` at that baseline.
The successor was clean; the unrelated predecessor index was excluded. The
[preservation manifest](before-implementation.json) pins 23 frozen specifications and 56 prior milestones.

## Changed scope

The local session admits patient acknowledgment as an isolated Care batch, returns its receipt, and
selects a real Host helper's finite treatment at a later ready cutoff. Acknowledgment cannot impersonate
the helper. Pending recovery waits for readiness. A stable acknowledgment-scoped helper command identity
and the complete durable occupied-identity ledger prevent silent automatic retries after refusal or collision.
The public projection reports exact choices and pending/attention states without exposing helper custody,
provenance or credentials. Profile 25 retains its existing excavation capability in the session allow-list.

The combined durable proof fills the active segment through 256 actual one-HP attacks, acknowledges the
defeat without cutting history, then seals that pending root in the same transaction as raw/crafted treatment.
This is explicit fresh test-stat calibration. Its finite 1,600-combat-boundary budget covers the larger
workload; the ordinary fixture's 1,000 boundary limit and all timing, conservation and authority rules remain.

Architecture updates are part of this increment: [aftermath care](../../../Runtime/docs/architecture/aftermath-care.md),
[encounter lifecycle](../../../Runtime/docs/architecture/encounter-lifecycle.md),
[time](../../../Runtime/docs/architecture/time-and-encounters.md) and
[persistence](../../../Runtime/docs/architecture/persistence-and-history.md) describe actual contracts in
timeless terms. Qualification and pending work remain here, not in the reference or frozen specification.

## Qualification

Clean build03 and all seven corrected session cases pass. The combined raw/crafted archive cases
also pass. Four external SIGKILL cases and 20 Host operations verify complete before/after tables,
retry idempotency and recorded history with unchanged 30-second operation/signal and five-second
kill limits. The final full Hosting/client regression passes 707 cases with zero failures/skips.
Core sources/tests are unchanged from the retained 1,355-case baseline; that suite was not rerun.
[Verification](qualification/verification.json) pins source and evidence; no native care controls or
human acceptance are claimed. [Independent review](REVIEW.md) records the source and assertion review.

`qualification/crash-evidence.tar.gz` retains the complete fixtures and process evidence, including
SQLite files excluded from loose-file Git tracking. Every member is checked against
`qualification/crash-evidence-manifest.json`. Restore with
`tar -xzf crash-evidence.tar.gz` from a separate copy of the qualification directory; run
`verify_crash_recovery.py` with the explicit toolchain/Host paths, extracted fixtures and a new output
directory. Expected gameplay roots originate in managed fixtures; the separate Python parser checks
storage, archive/reference, treatment/consumption and receipt structure, not independent damage arithmetic.

Retained development failures:

- Build01 referenced an internal Core resolution encoder. The test now compares every public resolution
  member strictly, preserving the complete historical witness without widening runtime APIs.
- Focused run: five passed/four failed. Three immediate caller-task assertions raced the async continuation
  after an already committed host tick; bounded existing-deadline waits now observe the caller result
  without advancing another tick. The excavation test asked the local bounded pathfinder for a distant
  route; it now follows the retained dry-bank waypoints using actual locally validated movement and
  unchanged route/total-movement bounds. The seven-case follow-up passes. No runtime rule was relaxed.

## Remaining dependencies

- Explicit fresh care-enabled content admission and Godot/client offers, wheel, icons, status and control routing.
- Native pointer/keyboard/controller, save/reopen and visible care feedback under shared-desktop rules.
- Pending-care/currency corruption traces and treatment → later funded reward/refill → attack combinations.
- Broader medical policy, other owner continuation, full journal lifetime, production scale and all full-scope gates.

No paid Claude request was retried. Native workers supplied disjoint projection/test work and a bounded
independent source critic; Astra integrated the session and reviewed the result. No measured credit-saving
claim is made. The prior Claude failures remain recorded in the shared handoff.
