# Retained failures and corrections

- The first solution build passes with zero warnings/errors (`qualification/build-01.log`).
- The initial focused run (`qualification/material-01.log` and `.trx`) reports **1 pass, 19 failures**.
  All 19 fail during shared fixture construction: the new fixture requested gathering tool power 4,
  outside the existing admitted 1–3 profile. These cases did not reach their intended behavior and
  establish no continuation result. Correct the fixture and genuine operation/count expectations;
  do not widen the runtime profile, fabricate stock or grant free effort. The later run is recorded
  separately and does not rewrite this failure.

The first compiled binary identities are retained in `qualification/initial-execution-pins.json`.
The test worker added review-driven capacity cases after that build; those new cases were not part of
this initial diagnostic. Runtime changes remain frozen while the test fixtures are corrected.

- The second clean build (`qualification/build-02.log`) passes. The corrected 22-case diagnostic
  (`qualification/material-02.log` and `.trx`) reports **21 pass, 1 failure**, including passing long
  continuation and combined-capacity cases. The remaining test fails while preparing its intended
  actual excavation effort spend: the fixture's excavation action is refused before the material
  bridge is exercised. Its source/geometry/eligibility setup must be corrected, with the exact
  rejection made visible; runtime excavation rules are not relaxed. The second source and execution
  inventories are retained as `source-pins-02.json` and `execution-pins-02.json`.

- Source inspection located the excavation fixture error: cell `(0,-1,0)` supports part of the
  actor's footprint. Even partial excavation checks the eventual opening, so removing it was
  legitimately refused. The corrected site `(0,1,0)` remains within reach and preserves actor
  support and live resource anchors. Explicit eventual-space and exact-rejection assertions now
  precede the intended effort-continuity check. Runtime rules are unchanged. The third build
  (`build-03.log`) is clean; the single-case `excavation-03.trx` passes.

Final regression results are recorded separately in `qualification/verification.json`; the failed
runs above remain retained and are not counted as successful behavioral evidence.
