# Lead review notes

These are observations during the bounded implementation, not conclusions about its final delivery.

## Reserved world profile 31

`Runtime/tests/OpenFantasy.Core.Tests/Worlds/MaterialProposalCodecTests.cs:92` explicitly requires a
profile31 proposal batch to be refused. The513 preserved prior test/fixture files include this test.
The paid world must select an unused explicit profile that preserves this refusal (for example32),
rather than widening admission to31 or changing the assertion. The existing29 gap has the same
compatibility rationale. Confirm the final chosen profile and all explicit dispatch/storage sets.

This was discovered from the retained test itself. The implementation was still in progress when
the observation was recorded; no worker-owned source or test was changed by the lead.

## Complete identity admission

The in-progress delivery adds paid quotes and settlement evidence, but the observed
`WorldAdmissionValidator.Validate` still stops after `ReadMaterialProposals`, and
`WorldIdentityRole` has no paid quote role. Check the terminal delivery for complete registration
of quote IDs, creation/acceptance commands, historical lot identities, actual rights-owner roles
and exact item/currency pins. The durable identity table is not a replacement for the in-memory
root's own role/occupancy index. An unarchived root must retain its known paid identities without
needing an external history callback; malformed paid records must not alias actors, definitions,
gift proposals or other causes. Prove this with actual issued/settled roots and adversarial copies.

## Distinct refusal fixtures

The first in-progress hosted test run expects `OutOfReach` from an unfunded initial world but
actually receives `Unavailable`. The terminal tests must establish the desired precondition
explicitly, with a genuine distant-body contrast and a separate reachable-but-unfunded contrast.
Simply changing the expected refusal would not prove the reach requirement. Preserve the original
failed report and verify whether the fixture or runtime rule caused the discrepancy.


## Integration dispositions

Astra corrected all three delivery observations without changing any preserved test or gate:
profile32 is explicit, quote/create/acceptance/rights identities join complete root admission, and
actual funded walk-away/return is separate from the explicitly reachable but unfunded contrast.
Per-fill issuer revision now uses the historical prefix, and the existing gift Host ingress admits
its capability in profile32. A hostile second-fill quote with a plausible final count but an earlier
market revision is refused by complete admission. Wrong current deliveries and changed-content retry
are refused without advancing any world state.

The crafted journey initially earned the quest reward after combat; its level-up legitimately
refilled HP and defeated the test's injury precondition. The corrected journey earns funds first,
then takes actual combat injury and recovery before trading/recrafting/healing. No HP or balance is
injected. Failed reports remain unchanged. A missing Care import, signed-HP type and nullable test
access were compile-only repairs in new tests. The empty build across the environment refresh stays
interrupted/unverified; a new complete build supersedes it without relabeling it.

The external process runner initially received a nonexistent net8.0 Host path and failed before
starting a Host. Correcting the path to the project's actual net10.0 output enabled both before/after
commit kills for produced buyback and continued raw purchase. Every authoritative SQL table matches;
one raw fixture already contains one real archive. No kill coincides with creating a new archive,
and these results do not establish power-loss, network, native-input, migration or scale acceptance.
The build harness now pins every emitted target-framework OpenFantasy assembly, including net10.0
Host, as well as sources; proof executables match those exact current binaries.

The new independent critic is a distinct read-only assignment for the integrated world binding.
A private output location under home failed the adapter's outside-Git check before any model call;
preparation moved to the normal temporary directory. Preview succeeded, then explicit execution
started. This is not an automatic paid retry. Its terminal finding/evidence disposition remains due.


## Independent review dispositions before lifecycle repair

The independent Opus review completed in564.942s, with verified5.5/1M/requested-Max, synchronized
context and no denials. It found useful actionable issues; its original text is preserved in
INDEPENDENT_REVIEW.md. The bounded lifecycle repair is a separately authorized implementation unit.

1. Shared quote capacity: accepted. Supersede an issuer's now-stale sibling quotes with the same fill,
   and provide separately authenticated issuer withdrawal for stale-stock or unwanted open terms.
   Preserve exact causes, identities, continuation and complete rollback. Acceptance awaits code/tests.
2. The129th lifetime issuer: not reachable in the current complete world. WorldSnapshot rejects
   more than WorldStepBatch.MaximumActorsPerBatch=64 residents and requires the inventory actor roster
   to match those residents. Root paid admission requires every issuer to have an existing Actor role.
   Distinct issuer entries therefore cannot exceed64 while that roster is immutable. Do not prune or
   reset revisions: doing so could revive stale terms. Roster expansion/turnover must redesign the
   issuer index/continuation contract under its own scale and history gates. Document this dependency.
3. Issuer-buys information: a valid future visibility boundary, not proof of implemented secret
   inventory disclosure controls. The local trusted Host accepts exact lot claims; no player-facing
   market inspection/session/network route is bound. Add knowledge/disclosure policy before that
   route, and test that stale/distant observations cannot probe current private stock. Record the
   limitation now; do not silently present the local method as multiplayer authentication.
4. Historical controls: accepted architectural dependency. Current controls/storage/account owners
   are immutable. A future control, session, roster or ownership transition must capture/rebase
   historical authority explicitly; validating old fills against changed current bindings would fail.
5. Same-sequence byte permutations: accepted. Require one explicit canonical terminal outcome order
   without reordering real acceptance ordinals or normalizing malformed input at decode.

Process-kill and paid SQL tamper proofs are completed by the lead after the repair on fresh fixtures.
Seller-fee arithmetic is already independently covered in the accepted shared Spot transfer contract;
third-party fees have a real hosted journey. Further simultaneous multi-party, delayed body/readiness,
late reconciliation and participant archived-query cases are reviewed against their actual applicable
boundaries; they cannot be claimed from a component test or a list of planned scenarios.


## Final-run corruption fixture failure

The lifecycle source passed all1,868 selected managed cases. Its fresh produced fixture passed both
real process-kill/recovery boundaries. The first changed-price corruption probe then failed with
SQLite Error5 (database locked), not the intended replay rejection. The Python mutation helper used
a transaction context but did not close its connection before starting the Host. The repaired helper
explicitly closes that owned connection; source/Host rules and all deadlines are unchanged. The
original final-run and probe reports remain failed. Only the affected process/storage proofs are
rerun against the same pinned binaries; the managed suite does not need a ceremonial repeat.


## Boundary-proof integration

The Opus follow-up delivered eight passing new Host cases after retaining four invalid same-actor
batch fixture failures. Runtime and old tests stayed exact. Its report's "complete for priorities1/2"
is narrower than full required evidence: two eligible human counterparties were not exercised, and
capacity/protection/late rollback remain unproved. A fixture's two-human roster is not proof that a
larger legitimate fresh genesis is forbidden; that work was expressly permitted in the assignment.
The new cases are useful, but those original requirements remain open.

Astra pinned the gift conflict's exact Unavailable reason. Its first additional recovering-caller
check then attempted an issuer-readiness check one step later; that step had legitimately retired
the final recovery delay. The retained new-host-20261001T005145Z-c6dfb failure exposes a test
sequencing assumption, not a runtime defect. Two separate real journeys now test the caller and
issuer at the same pre-step boundary, preserving both requirements and ordinary time advancement.
