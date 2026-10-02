# Independent routing critic

A native independent critic reviewed the routing implementation, host completion join, tests and
scope read-only with a six-minute limit. No external provider call, source edit or build was assigned.
It found no substantiated runtime defect in the code it reviewed and requested concrete coverage for
(1) closure with a paused pending action and (2) response-queue exhaustion and no-response input floods.

The added tests exercise Goodbye and actual host shutdown with a queued paused command, prove no
receipt or item loss, join routing completion, and prove an unaffected peer can still move after
Goodbye. Separate tests block the sender then exhaust eight response slots, and exceed 128 readiness
inputs without generating output. These test the declared bounds independently of the send timer.
An actual loopback WebSocket action also returns the SQLite authority's exact saved receipt.

The expanded focused run routing-20261002T081144584794Z had 20 passes and one failure: the input-rate
assertion passed, but fixture disposal re-awaited that fault and did not recognize InvalidDataException
as an expected protocol termination. It is not an IOException subtype. Cleanup now handles that exact
exception alongside the other expected terminal errors; the body still requires the original rate
exception and unchanged world revision. Preserve the failure and all original assertions.

Lead source review additionally identified an ordering race: the host cancels pending action waiters
before its completion task is necessarily observed. Action cancellation from an unavailable host is
ordinary teardown, while a real host failure remains on HostCompletion. Both paused-action shutdown
routes are retained; the timing race itself was not reproduced as a failing run. No gate, command
identity, receipt codec, gameplay law, queue bound or deadline changed.
