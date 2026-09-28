# Retained failures and corrections

## Initial integration build

[build-01.log](qualification/build-01.log) failed with 38 errors and no warnings: 36 missing current
test cancellation tokens (`xUnit1051`), one inaccessible internal V1 request-encoder call (`CS0117`),
and one collection assertion requiring `DoesNotContain` (`xUnit2029`). The test worker corrected
call sites; analyzers remain enabled. The public prepared V1 encoder already exercises its nested
request refusal. No runtime test outcome is claimed for that build.

[build-02.log](qualification/build-02.log) passes with zero warnings and errors after those call-site
fixes and the fixture correction below. The original build failure and its source pins remain retained.

## Independent fixture review

Before execution, the critic found that the new dry construction-proof request reused construction
project B's reservation, which still funded six raw wood units. Exclusive runoff funding correctly
requires a budget to describe all of its held stock; a dry request cannot claim that reservation.
The fixture receives a distinct unused runoff reservation in its optional original genesis admission.
Construction funding and all original fixture defaults remain unchanged. This is a source-review
correction, not an executed test failure.

## Earlier refusal boundary in the existing regression

The first full Core run ([core-final.log](qualification/core-final.log),
[TRX](qualification/core-final.trx)) reports 1,513 passed, one failed, zero skipped. Its retained name
predates the failure; it is not the final successful qualification. All 23 new cases had already passed
in [focused-02.log](qualification/focused-02.log), and all 23 legacy binary fixtures reproduced exactly.

`ProductionContinuationBoundaryTests.Runoff_and_nested_archive_do_not_silently_drop_the_imported_context`
constructed a generic continued request outside its assertion, expecting the later planner to refuse.
The explicit qualified-owner contract now refuses that request at construction. The updated test
asserts this earlier rejection, derives a separate valid cut from an actual capacity owner, verifies
that the V1 encoder still refuses continued state, and proves the explicit V3 roundtrip. Original
legacy bytes remain unchanged. The generic admission guard is stronger; no rule or test threshold
is relaxed.

The independently reviewed correction passes the final [build-03.log](qualification/build-03.log)
with zero warnings/errors, all **1,514 Core cases** in [core-final-03.trx](qualification/core-final-03.trx),
and **238 affected Hosting cases** in [hosting-final.trx](qualification/hosting-final.trx). All 23
pre-change binary fixtures remain exact in [legacy-comparison-final.json](qualification/legacy-comparison-final.json).
No runtime source changed after build 02; build 03 incorporates only the corrected existing test.

The earlier integration correction concerning generic-context identity/revision information loss is
recorded in [REVIEW.md](REVIEW.md). No acceptance threshold or game rule is relaxed by these fixes.
