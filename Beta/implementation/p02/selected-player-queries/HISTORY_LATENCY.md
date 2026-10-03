# Retained history-query timing failures

The final-source `query-compatibility` selection reports 125 passes and two failures out of 127.
Its [original report](qualification/query-compatibility-20261003T050201519078Z/report.json) and log
remain unchanged. The failures are not waived by this checkpoint.

- `PlayerTradeQueryTests.Archived_paid_quote_is_exact_for_participant_and_absent_for_authenticated_outsider_over_sockets`
  times out on the first archived participant query at line 124. `QuerySessionConnection.Receive`
  retains its five-second bound.
- `PlayerProposalQueryTests.Archived_proposal_preserves_terms_and_filters_foreign_subjects_without_changing_history`
  reports its five-second connection-disposal timeout. That teardown exception can mask an earlier
  failure; the retained log does not identify the original operation that was still outstanding.
- The same aggregate's existing cost probe measures two complete paid-quote replays at 10,418.006ms,
  cumulatively allocating 536,291,224 bytes. This is a synchronous replay measurement within a parallel
  test process, not the duration of either failed socket query or proof of its cause.

Source inspection found that legacy inspection retains the same authorization, serialized background
read, complete replay, pinned-head check and shutdown join. The extracted private storage method bodies
preserve their former operations. The changed inspection slot uses a typed key and typed task matching;
legacy requests still share only their identical pending subject and retain the original capacity.
This review does not establish timing equivalence or rule out an integration defect.

## One bounded diagnostic

The diagnostic budget was one selection of the two unchanged failed cases and the unchanged cost probe,
using the same current-source build and ordinary test concurrency. All three pass in
21.370432s under the existing 600s process limit. See the
[diagnostic report](qualification/history-diagnostic-20261003T051023413629Z/report.json).
The probe reports 2,036.016ms and 536,188,888 cumulative allocated bytes for two replays. No deadline,
assertion, input lease, test fixture or production rule changed; no automatic retry occurred.

The reduced selection changes process workload. Its success establishes only that this execution of
those original tests completed within their bounds; it neither explains the aggregate failures nor
qualifies production latency. No new archive/socket-stage trace was captured. Do not infer that shared
desktop contention, GC, networking or the new code is the exclusive cause from these observations.
Do not repeat this unchanged aggregate merely to seek a passing result. Further latency repair needs
measurements on the actual failed service path, with full history authentication and original limits
preserved. The historical query and movement latency gates remain open.
