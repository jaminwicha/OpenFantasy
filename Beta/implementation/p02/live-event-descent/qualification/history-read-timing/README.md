# Closed-save historical-read measurement

Implementation `0783db8` removes a duplicate full recovery in the three closed-save historical operators.
The new `RecordedWorldReader` holds the existing exclusive database lease, runs the unchanged complete
journal verifier once with a selected revision, and disposes the lease before returning an immutable
result pinned to the verified source head. Live authority reads and their writer gate/head comparison
are unchanged. A later file change cannot update the returned historical snapshot or its preparation pin.

## Evidence

The retained console harness runs against the actual funded profile18 fixture at revision 156, with head 166.
`split` measures authority opening and its subsequent historical read separately; `closed` measures the
one-pass public reader. Both use the same .NET 10 target and Debug Core/Hosting implementations. All
compared Core source files are unchanged. The baseline was captured before the added reader; reflection
in the second mode lets the identical harness build against that baseline. Reflection lookup is outside
the timed call, invocation inside. State/head hashes are collected after the timed work in both modes.

[Baseline](baseline.json), [one-pass result](closed.json) and [summary](summary.json) retain all three
observations per mode. Cumulative managed allocations fall from approximately 6.34 GB to 3.17 GB per requested
read, a reduction of 49.94–50.00%. Those are allocated bytes over the operation, **not peak/resident memory**.
The exact historical and source-head hashes agree in every observation; fixture and copied database bytes
remain unchanged. CPU/wall times and collection counts are retained, not substituted with a best sample.

This is a sequential Debug diagnostic on a shared machine. The one-pass wall times were 3.97–4.92 seconds;
the measured two-pass totals varied 14.96–29.44 seconds. That variability does not establish the cause of
the original 30-second timeout or a production speedup. Removing one demonstrated redundant replay explains
the allocation reduction. The remaining 3.17 GB per replay is an unresolved allocation/scale concern. No
C++ comparison, Native AOT qualification, production latency or full-city performance is claimed.

## Verification and review scope

The complete pre-fix solution regression passed 1,450 cases. The fix changes three Host call sites and adds
one small Hosting service; every existing Core and Hosting implementation remains byte-identical. A final
solution build succeeds, and 33 targeted cases pass on the rebuilt binaries, including nine new public-reader
cases plus existing history, legacy-save and database tests. These cover exact historical bytes/head pins,
corruption after the requested revision, missing/future queries, exclusive access, lease release on all
paths, unchanged saved bytes and prepared snapshot stability after another real commit. No timing threshold
is embedded in correctness tests. Actual Host deadline/crash qualification is recorded separately.

A bounded native source review identified the duplicated recovery and checked preservation of the complete
trace, final head and ledger validation under the same lease. The earlier Fable publication review remains
applicable to the unchanged event-authority implementation; it did not inspect this new diagnostic reader.
No additional paid review was dispatched for the thin reader. Hash caching and other optimizations were
deferred because this measurement establishes only the duplicate replay cost.

## Reproduction

Build `TimingProbe.csproj` with the retained .NET 10 SDK. Its project reference resolves to the repository's
Hosting project. Run its compiled executable with `split` or `closed`, a **closed copy** of the retained
`../fixture/after.sqlite`, and revision `156`. Keep builds and heavy regressions outside the measured run.
The harness repeats each mode three times, printing measurements and canonical pins as JSON. Inspect the
source before changing the repetition count or workload; it does not create or advance a world.
