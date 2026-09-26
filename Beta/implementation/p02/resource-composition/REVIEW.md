# Resource composition review and failures

A bounded independent native source critic mapped all three HP/MP writers before implementation and
reviewed the completed primitive/composition diff afterward. No actionable correctness finding was
returned in the scoped review. It checked exact old revision behavior, level-up's single revision,
unsigned saturation, exact MP payment, original-owner retention on failure and the distinction between
transient candidates and durable authority. The critic did not run builds/tests. External Fable
history implementation and broader model analysis/review are separate assignments, not claimed here.

Preserved failures:

- `test-first-build.log`: missing new resource APIs before implementation; expected compile-red proof.
- `build-01.log`: existing unfinished care fixture used an assertion rejected by xUnit2029. The WIP
  fixture now uses `Assert.DoesNotContain`; no analyzer suppression or acceptance weakening.
- Earlier care build had used inaccessible `CountedAuthorityState.WithCustody` from a test assembly.
  The WIP test now constructs the same public immutable owner, retaining all authority fields.
- `core-focused.log` / `core-focused.trx`: sandbox blocked the test runner's local TCP socket; run aborted.
  Focused tests were rerun with the required local permission and a new result filename.
- `hosting-focused-02.log` / `.trx`: five new source-backed cases failed on a nonexistent `effects`
  wrapper. Inspection of the pinned Potion payload confirmed top-level `healHp: 40`, matching the prior
  supply tests. The test and separate WIP fixture were corrected, with source content unchanged.

- `isolated-restore.log`: macOS `/var` and `/private/var` aliases produced duplicate NuGet project nodes.
  The isolated path was canonicalized and restore serialized; the original setup failure is retained.

No timeout, rule, threshold, historical hash or native gate was relaxed. Component tests cannot prove
complete-world medical authorization, durable history, process-kill recovery or native presentation.
