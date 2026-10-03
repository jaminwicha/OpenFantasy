# Independent capture and analysis review

A reused native investigator performed a bounded four-minute read-only review of the new listener,
analysis script and all three raw/derived captures. No build, edit or external provider request ran.
There is no claim of external-credit savings. The review verified input hashes, zero dropped/error
counters and reported GC-pause deltas; it found no defect undermining the limited conclusion.

Integrated findings:

- The below45ms statement applies to **first** movement sampling. Later expired checks following
  successful selection reach415.3512ms,269.8792ms and211.8233ms in the three captures. They are not
  unselected failures. The result already qualifies the first sample and retains selection counts.
- Event timestamps precede counter reads. In the second aggregate, sequence1139 has a34.794667ms
  event-to-sampling delay, excluded from the counter-call duration. Analysis now reports separate
  counter start-to-start intervals and both endpoint lags. No wall/CPU/pause tuple is claimed atomic
  or exclusively attributable to an encoder/thread.
- Accepted movement with no later age check must stay visible as unsampled. Analysis now retains
  that explicit state. All four roster movements in each run have age checks. The more complete
  analysis additionally exposes one unsampled input in each aggregate: the separate client input
  coalescing case sends movement followed immediately by stop. Exact port events show the zero
  replacement before any host sample. Both stay recorded with their replacing input; they are not
  silently dropped or confused with the four-player expired-lease failure.

The first finalization check incorrectly expected only four total nonzero inputs per aggregate and
refused these newly visible entries. Lead traced both additional ports and corrected that diagnostic
expectation: four sampled roster inputs plus one explicit immediately superseded input. No runtime,
test assertion, deadline or acceptance gate changed.

Lead reran only the analysis against the same raw captures after these script corrections, requiring
nonoverlapping counter reads and monotonic values. No further aggregate or isolated test repeat was
needed. Significant process GC pauses are supported; exclusive causation, workload-matched comparison
and a scheduler repair remain unproven. The original failed timing gate stays open.
