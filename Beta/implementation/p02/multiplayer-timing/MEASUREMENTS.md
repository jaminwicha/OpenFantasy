# Timing observations

The first traced retained Server aggregate (`qualification/server-managed-20261002T180152364140Z`)
passed 77 cases with one expected Windows-only skip in 42.090 seconds. This is the prior 77-case
selection (including the Windows-only case) plus the new diagnostic payload test. Existing test files/limits remained byte-exact.
The actual test-application capture contains 5,570 records, no dropped/incomplete/invalid records;
two discovery/wrapper captures contain only process counters and are not application timings.

Selected wall-time spans (including scheduling/GC suspension, not CPU attribution):

| Stage | Count | Median ms | 95th percentile ms | Maximum ms |
| --- | ---: | ---: | ---: | ---: |
| Host projection | 183 | 29.375 | 113.293 | 367.970 |
| Root hash at publication | 551 | 0.001 | 0.266 | 24.724 |
| Host commit | 29 | 11.313 | 80.732 | 130.612 |
| Observation capture, including unchanged views | 289 | 0.014 | 213.667 | 618.778 |
| Client replica application | 98 | 144.109 | 335.503 | 573.623 |
| Client startup | 36 | 338.719 | 758.460 | 823.329 |

The test application's cumulative managed allocation reached 15,926,787,424 bytes; this includes
all selected tests, fixture construction, diagnostic overhead and application work. It is not a
per-player or peak-memory figure. Its final working set was 241,369,088 bytes. Scope and workload
must be held constant before making an improvement claim.

All four roster movement samples were actually selected: observed ages at selection were about
45.5, 90.7, 16.2 and 122.1 ms, within the unchanged 150 ms lease. This run did not reproduce the
prior first-sample loss; later stale samples in this capture do not explain the original failure.

Source review identified repeated occupancy construction and a generic observation validator that
boxes and visits every boolean occupancy cell. The next experiment isolates allocation in that
validator while retaining exact depth, collection and aggregate-value bounds. Root hash work was
measured but is not the first optimization target from this capture. No aggregate or production
failure cause has been established; previous reports remain authoritative evidence of those failures.

## Isolated validator contrast

The baseline `occupancy-validation-20261002T181251367898Z` run passed 18 correctness cases and
failed the intended allocation claim: both owned occupancy and the generic boolean-array control
allocated 3,145,984 bytes for eight validations of 16,384 cells. After the narrow fast path,
`occupancy-validation-20261002T181515838102Z` passed all 21 cases and recorded zero bytes for
owned occupancy against the same 3,145,984-byte control. The warmed measurement is synchronous
and uses current-thread allocated-byte counters; it excludes input construction and any wall-time
threshold. The extra cases preserve first-failure precedence under competing budgets.

## Subsequent aggregate observations

With tracing disabled, `server-managed-20261002T181629253074Z` reproduced the five-second
four-player movement-observation failure (76 pass, one fail, one Windows-only skip). The next
same-selection traced run, `server-managed-20261002T181731479589Z`, passed 77 with one skip.
Its actual test process retained 4,265 events without drops or invalid records. Replica median
wall time was 25.803 ms, startup median 158.613 ms, and cumulative allocation 13,560,252,240 bytes.

The two traced runs executed different numbers of projections, observations and commits as the
real-time hosts ran. They are not a controlled whole-game allocation or latency comparison.
Only the fixed validator contrast above supports the precise allocation-reduction claim.
Traced success again failed to capture the normal aggregate's failure. Its cause remains open.
