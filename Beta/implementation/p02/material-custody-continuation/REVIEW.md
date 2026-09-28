# Material custody continuation review

Status: independent source review complete; final execution results are recorded in the qualification packet.

## Integration findings

- The first source-proof design pinned one whole-world/spatial hash for the entire material segment.
  That would make ordinary movement and time changes incompatible with later gathering. The design
  instead pins current context per actual proof, retains a bounded source baseline, and requires the
  actual supplied gathering owner to match its exact history plus deterministic idle recovery.
- Initial delivery-helper extraction moved production validation ahead of legacy control/reservation
  rejection. The public legacy path must preserve its established ordering when sharing preparation.
- The first `MaterialOperationPlan.CandidateGathering` derived the source candidate from retained
  material history for every result. That evidence may predate actual idle recovery or an external
  effort spend. Publishing it on a rejected gather or unrelated transfer could restore obsolete state.
  The correction requires explicit optional source candidates: unchanged actual supplied source on
  gather refusal, genuine accepted source on success, and no source replacement for non-gather work.
  Regressions must verify full source hashes and clock/effort, not only unchanged material totals.

The integration lead raised these findings during implementation. The frozen source incorporates all
three corrections; the full 1,468-case Core regression passes, including current-world time advancement,
source-owner preservation after actual excavation effort spending and unchanged legacy rules.

A separate native critic reviewed ordered conservation, source/effort continuity, identities,
permissions, revisions, old-format boundaries, codec proofs and architecture. Review remains bounded
source evidence, distinct from actual execution, complete-world admission and human acceptance.

## Independent frozen-source checkpoint

The independent native critic found no additional blocking runtime defect. It confirmed the source
candidate correction and requested two follow-ups: a targeted V4 intermediate raw-plus-derived
capacity challenge, and explicit documentation that `Decode` must be followed by
`ValidateForAuthority(actualInventory)`. The latter is now stated in the reference. The resulting tests
exercise actual combined-capacity behavior; source review itself does not establish their pass.

The independent critic reviewed the added capacity cases and closed that test-design gap: the raw
transfer independently succeeds, corrected reorder tokens preserve final material, and the negative
requires the exact intermediate combined-capacity rejection after successful decoding. Both capacity
cases subsequently pass in the second diagnostic. Its other fixture failure remains separately
recorded and does not negate or generalize that bounded result.

The final Core regression passes all 22 new cases, including both capacity challenges and the corrected
actual partial-excavation journey. No blocking source finding remains from this bounded review.
Affected Hosting results and final file-identity checks are in
[verification](qualification/verification.json). This review does not close complete-world, archive
authenticity, native presentation, performance or human acceptance gates.
