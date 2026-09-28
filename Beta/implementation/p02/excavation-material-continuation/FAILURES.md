# Retained integration failures

## Build-01

The first integration build compiled the runtime but failed five test-analyzer checks in the new
behavior tests: two `Assert.Single` predicate-overload requirements, one collection absence assertion,
and two omitted test cancellation tokens. The original command, source pins and compiler output remain
in `qualification/build-01-command.json`, `source-pins-build-01.json` and `build-01.log`.
These are test-source corrections; no runtime rule, test expectation or acceptance threshold changes.

## Build-03

The lead's additional construction-positive test referenced the Core-internal lot encoder from the
Hosting test assembly. The compiler correctly refused that access. The test now compares every public
lot field and separately retains the complete component byte round trip. Build-03 output is retained;
no production API visibility was widened for the test. Build-02 had passed before this added test.

## Independent source review

The critic identified an unjoined current-world identity role: a new material batch or subsequently
retired raw lot could alias an admitted excavation site while no current live lot exposed the conflict.
The complete component must validate every newly introduced material identity against its captured
source roles, independently of the caller's permanent-history predicate. Dedicated actual-operation
regressions now pass in the focused run. This finding was discovered before runtime qualification.

The runtime worker also identified the existing installed-construction invariant that no new raw stock
may be delivered to an already installed project's reservation. Static preservation must retain that
invariant as well as the unchanged installed guard output. The focused positive/negative controls pass.

The lead then caught a positive-path issue in that stricter identity join before execution: first-cut
Production legitimately adds existing gathering node/definition roles and installed structure roles.
The bounded exception admits exactly those actual source identities. Site aliases remain forbidden;
real funded/completed construction survives two cuts and exact archive reopening in the focused run.

Build-04 is clean. All 38 focused tests pass without a runtime test failure. Broader regression,
the actual 512-operation boundary and legacy byte comparisons pass in the final qualification record.
