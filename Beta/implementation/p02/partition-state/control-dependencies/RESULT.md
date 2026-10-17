# Prepared current-control dependencies

`WorldActorControlDependencies` selects exact current-control rows for one actor, host-established
delivery, optional delegated witness and required capabilities. It retains the permanent binding,
effective slot and, where delegated, the grantee binding, both lifecycle assignments and named
permission. World/branch/profile context and absent rows participate in revalidation. It does not
retain a complete world, inventory or controller roster.

Ordinary `WorldActorCommandAuthority.IsCurrentWitness` uses the same selection and rules. Its immediate
path uses a private value-type selection without allocating a retained dependency object. The existing
permanent-binding dictionary supplies exact actor lookups. Historical evidence and assignment
eligibility retain their distinct semantics and share their extracted row checks. No rule, encoded
byte contract, profile, schema, launch default or permission scope changes.

## Verification

The [focused Hosting run](check-01/hosting.json) passes 15 cases, including eight new dependency cases,
in 26.539 seconds under an unchanged 180-second bound, with no build warnings/errors or test skips.
New tests use the actual grant, acceptance, disconnect allowance, acquisition and SQLite authority:

- A new effective assignment invalidates an earlier original-owner read; unrelated grants and another
  actor's control changes do not invalidate the grantee's independent home-character read.
- Revocation, original return, grantee loss and release each invalidate current control while prepared
  geometry remains valid. Reopening reproduces that difference from original genesis. A captured
  authorization stays a historical result; it cannot grant fresh input after the current check fails.
- Real delegated movement advances position, step count and experienced time while unchanged control
  dependencies remain valid, including exact encode/decode and original-history reopening.
- Reacquiring the same accepted grant produces a different witness and refuses the old read.
- Insufficient/empty delegated scope, wrong principal and wrong session are refused; successful
  freshness validation of those denied selections does not turn them into authorization.

The [full Core run](core-full/process.json) passes all 1,938 cases without failures/skips in 91.819
seconds under the unchanged 180-second bound. Builds are warning-free. The
[retained-action run](retained-actions/process.json) passes six further cases in 6.100 seconds under
180 seconds: paid attack and retreat resolution after revocation, a retained care acknowledgment and
actual helper dose, all twelve permission pairs for four real home actors, absent-party permission
limits and impersonation/delegation refusal. There are **1,959 distinct passing cases**, with eight
new cases and no failures/skips in these runs. No unchanged aggregate was rerun to obtain a green label.

## Scope and next work

This extracts the control part of complete action dependency closure; it does not provide a partition
authority or an atomic read/write transaction. Capture/revalidation still receive an admitted complete
world. The publishing owner must serialize freshness checks with publication, authenticate transport
independently, and enforce health/debt, selected opportunities, holds, carried loads, geometry and time.
Do not apply current-control validation to already accepted work: its owning obligation retains
the accepted command and follows the existing paid resolution/interruption rules.

PLAN-STATE still requires actual actor/cargo ownership transfer, bounded authoritative directory,
absent-detail funded work, atomic publication, original-history replay and meaningful cost measurements.
No scale, partial-world, native, independent or human acceptance follows from this extraction.
The roadmap and timeless effective-control reference preserve that distinction.
Earlier rendered journeys retain their original source/binary evidence; this extraction does not
claim a new native qualification.

[Checkpoint](CHECKPOINT.json) preserves the prior spatial-read commit before these edits. Unrelated
predecessor staging, frozen specifications and the October5 advisory are outside this increment.
The accepted marker is `codex/milestone-control-dependencies-20261017`.

## Documentation review

- **Plan/spec:** Roadmap, handoff and partition comparison record selected control dependencies as
  preparation for the integrated proof, retaining material/time/readiness, directory and publication
  obligations. No phase exit or broader permission requirement is removed.
- **Reference:** Effective-control explains exact selected rows, absent-row checks, snapshot authorization
  versus current validation, historical accepted authority, ordinary-runtime reuse and privacy boundaries.
  It remains a timeless contract rather than a qualification log.
- **Accuracy/completeness:** Lead compared original/current witness checks, assignment eligibility,
  permanent-binding lookup and recorded terms reconstruction. Immutable record equality covers every
  selected row field; the prepared read retains no full-world owner. Actual durable closure tests
  distinguish unchanged geometry from changed authority and unchanged authority from changed time.
- **Checks:** Focused, full-Core and retained-action evidence is retained above. `VERIFICATION.json`
  records exact outcomes, source/binary identity, documentation links/fields, authored whitespace and
  protected inputs. Link checks supplement the factual review. Independent and human gates remain
  unrun under the current direct-work instruction.
