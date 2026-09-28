# Retained failures and limitations

- Initial local dispatch preview refused a missing private skill source path. The skill sync manifest
  resolves its relative source under the private task folder; the maintained skill and companions
  were copied there, then the same preview passed. No model request or paid retry occurred.

## Delegated implementation shell denial

Fable wrote partial Core sources and five unexecuted cases. Its two Bash builds were denied. The
first appended a pipe; both used an absolute project path while the exact configured allowlist
used `build Beta/Runtime/tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj:*`.
Thus the worker's description of its second attempt as "exact permitted" is inaccurate. No build
or test ran, and no gate passed. Context postflight verified. The run returned the requested Fable
family/1M context with requested Max,1,595.111s and$16.78311325 list-price estimate. No paid retry.

Astra retains the source and integrates directly. Future bounded assignments must reproduce the
allowlisted command's path spelling exactly; preflight prompt examples should match the generated
allowlist. A relative/absolute path mismatch is an adapter/assignment defect, not an account-access
test or a reason to broaden tools to unrestricted Bash. Five uncompiled tests remain in the build;
the worker's suggestion to exclude failing tests is rejected.

## First integration build

`qualification/integration-build-01.log` retains two CS0246 errors in Astra's new durable test:
the `TrustedCommandDelivery` namespace import was missing. Added `OpenFantasy.Core.Authority`;
no production rule, assertion, or test inclusion was changed. Core itself compiled in this attempt.

## First durable integration test

`qualification/durable-01.trx` retains eight failures from the shared actual journey setup: the first
post-cut restorative use was refused because the world had already reserved the current command ID
and passed it unchanged to the continued planner's historical lookup. The adapter now excludes only
that already-reserved current command, retaining all other permanent identity checks. The same
integration issue was found in aftermath care; crafting also lacked the required continued lookup.
The third integration build passes; later corrections still require a new build and execution.

## Recovery diagnostic

The durable-02 run's VSTest parent was idle while xUnit v3's separate executable remained active.
A parent-process stack sample alone did not demonstrate a hang. The actual child was identified by
parent PID and exact Hosting.Tests executable; it was consuming CPU in the same run. No process was
killed or deadline relaxed. Diagnostics and completion status are separate from test acceptance.

## Fourth integration build

`qualification/integration-build-04.log` retains two test-source issues: a new test attempted to use
Core's internal `WithCustody` helper from the Hosting assembly, and a new worker test used a collection
size assertion rejected by xUnit's analyzer. The test now reconstructs through the public authority
constructor; the worker changed the singleton assertion to `Assert.Single`. No application visibility,
assertion meaning, test inclusion or analyzer configuration was relaxed.

The durable-02 run subsequently passed all eight cases on build03. Its elapsed wall time and build pins
are retained; later integration fixes require their own execution. The parent diagnostic did not find
a runtime deadlock. A second diagnostic against the active child was stopped when it did not return;
the actual test process completed normally. No gameplay or test process was terminated.

## Focused integration run

`qualification/focused-02.trx` passed26/28 with two new test-assumption errors. The spent-source test
expected the complete-world partition check to be the first rejection, but existing Production
validation already refuses resurrected archived raw retirement. The test now asserts that earlier
precise refusal both at the domain boundary and when composing the world. The mining-to-gathering
journey attempted to walk directly through the authored worksite wall. It now returns via its actual
mine approach and then the existing farm berry connector. Movement collisions, route budgets and
assertions remain unchanged; no teleport or terrain edit was introduced.

## First external process-crash run

`qualification/external-crash-01/result.json` is failed. Normal publication, duplicate refusal and
historical inspection completed in25.890s,23.267s and25.942s. The process reached BeforeCommit in24.506s
and was killed externally with SIGKILL in0.011s. The next full recovery/historical inspection exceeded
the unchanged30-second bound. The after-commit kill was not reached. No later result erases this timing
failure or establishes its cause; recovery latency/scale remains open.

The same report also flagged fixture drift. Comparing every path found only newly created
`before.sqlite3-{shm,wal}` and `after.sqlite3-{shm,wal}` sidecars; all main databases, canonical frames and
metadata stayed byte-identical. SQLite read-only connections can create those sidecars. The harness now
reads only complete standalone backup inputs with immutable mode after proving their WAL is absent or
empty. Output/recovery ledgers continue reading actual WAL normally. It retains all original hashes and
failure evidence; it does not ignore changes to authoritative bytes or pending journal state.

The one same-command diagnostic also exceeded30seconds (`before-reopen-diagnostic.json`). This
confirms the deadline remains unresolved; it does not identify the cause. No repeated search for a
passing run, extended deadline, reduced journal, skipped archive verification or after-commit kill
claim follows. Full retained managed regressions proceed separately; external/native qualification
remains an explicit open gate for this implementation.

## Concurrent-builder test integration

`integration-build-07` failed in the two new test files: a missing Content namespace import and
an assumed OrdinaryCareSnapshot.ContentHash property that does not exist. The test now compares
actual definition/version, permission/revision and use collections, including after canonical
reopening. No application source or acceptance rule changed. Build and executed cases follow in
the retained qualification records.
