# Archived paid-quote replay checkpoint

The authority now obtains an archived quote and its exact publishing root in one complete verified
replay. Previously a participant query ran the complete replay twice: first to authenticate the
archived terminal outcome, then to reconstruct its publishing root for the original market record.
`WorldPaidQuoteReplaySelection` retains only the root whose revision actually publishes the requested
outcome. The retrieved archive is still independently authenticated; its complete terminal bytes must
match that root. Nothing is returned before all journal, checkpoint, archive, head, command-table and
occupied-identity checks finish. Current-control authorization, participant disclosure, pinned-head
checks and recovery-required poisoning retain their boundaries. There is no result cache, early
return, new authority, canonical encoding change or relaxed deadline.

## Measured scope

The same unchanged cost test opens a real 132-revision journal with one ordinary archive cut, warms
the actual authority query, then measures two participant queries. It checks the exact original market,
quote identity/hash, filled status, publication sequence, private issuer revision and unchanged head.

| Observation | Before | After |
|---|---:|---:|
| Cumulative allocated bytes, two queries | 1,071,986,376 | 536,331,088 |
| Elapsed milliseconds, same workload | 880.624 | 698.087 |

Allocation falls about 49.97%. These are cumulative allocations, not peak memory. Wall times are
shared-desktop observations, not a production latency guarantee or controlled speed-ratio claim.
One full replay remains linear in retained history. The earlier five-second archived-query failures
are preserved and unexplained; removing duplicate work does not establish their cause.

## Verification

[Final checks](FINAL_CHECKS.json) authenticate the actual final source and binaries against each run:

- Warning-free solution build; all selected aggregates pass.
- 20 new history-query cases: exact filled/withdrawn/expired/superseded publication, original market,
  live/open/unknown contrasts, current-control and participant privacy, seven later/archive storage
  corruptions and exact terminal substitution/original-root selection.
- 95 history/player-query/trading regressions, 265 networking cases and 77 managed Server cases pass.
  Their overlap yields **416 distinct passing cases**, including all 20 new cases.
- The one Windows ACL test is explicitly skipped on this macOS host.
- All 703 older test/fixture files, 23 frozen specs and unrelated predecessor staging remain exact.
  The before/after cost-probe source hash is identical; 1,289 architecture links resolve.

A native worker supplied the bounded archive-query test suite. The lead reviewed, built and ran it.
The [independent design/source review](REVIEW.md) found no actionable defect after its design challenges
were incorporated. No provider request or Claude credit offload occurred; the earlier account-limit
failure remains terminal. New failures did not occur in this packet; prior failure reports remain in
their original packets. The architecture reference describes publication capture and full validation.

## Preservation and remaining work

- Before work: `codex/checkpoint-before-quote-replay-20261002T194030Z` preserves source
  `7f9ffb435a8e694be169a7f6f76373dbe51d1464`; the relevant successor tree was clean.
- Before runtime optimization: `codex/checkpoint-before-single-quote-replay-20261002T194303Z`
  preserves WIP snapshot `61b19d277eb93c4e6c262c473c0aaa061034190a`, including the cost fixture and
  baseline evidence, excluding unrelated predecessor changes. Its build and baseline case passed;
  it is not acceptance of the later implementation.
- Verified milestone: `codex/milestone-archived-quote-replay-20261002`, resolved by the preserved branch.

No new native, multi-cut open-carry quote-query, physical platform, full Core, authenticated lived-save
migration, care capacity/crash, production scale or full campaign acceptance is claimed. Continue the
native profile 38 and complete pending/cut/disposition/re-cut qualification, then retained survival,
restart and co-player dependencies. Production history latency and prior unexplained query failures
remain explicit alongside those gates. All old assertions, leases, timeouts and concurrency policy
are unchanged. The full campaign scope remains authoritative.
