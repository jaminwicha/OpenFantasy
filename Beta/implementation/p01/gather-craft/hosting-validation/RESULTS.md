# Shared Hosting verification

The pinned Debug run passed **108 of 108 Hosting cases**, with zero failures or skips. This includes 11 newly authored compatibility/gathering/session cases and the retained suite plus the other worker's content-admission cases. The full raw runner result is `test-results/gathering-hosting-debug-initial.trx`; `VALIDATION.json` records its exact counters, times, case names and source/test/binary hashes.

The worker implemented the generic Hosting codec routing, current profile batch selection and bounded player gathering projection. These test results are implementer verification of those changes. Its separate static review of the coordinator's core codec/processor/client changes is recorded in `../PERSISTENCE_REVIEW.md`; no independent review of the worker's own Hosting implementation is implied.

## Proven boundaries

- The actual pre-change native save, containing 32 commits and three exact terminal receipts, reopens unchanged under the new dispatcher. Its old state/receipt bytes and historical occupied IDs match, a legal old-profile step continues it, and another reopen matches. No gathering state or migration is invented. The retained fixture and original artifact have the same SHA-256.
- A gathering swing commits its exact v2 command, unchanged v1 receipt format, finite source interval, node state and effort together. Restart and exact retry do not issue the interval again. Before/after-commit lost replies recover either no result or the exact committed result.
- A full pack produces a durable rejection without damaging the node, issuing rights or charging swing effort. The already accepted ordinary step still contributes its usual effort increment.
- The actual depletion/regrowth trace preserves the issue cursor across restart and another generation. All six issued intervals are disjoint and retain their exact boundaries. No regrowth itself mints inventory.
- The controlled session stages duplicate requests without early publication, freezes pending work while unready, uses the persisted v2 batch profile, freezes work under a reading hold and keeps source-budget/cursor fields out of the player view. A legacy projection contains no invented gathering state.
- An incompatible batch cannot silently upgrade or downgrade a world. A forged legacy head cannot replace a committed v2 journal's replayed result.

## Narrow measurements

The synthetic two-actor gathering fixture ran **1,001 advancing steps**, including five oak swings, the actual 900-step source delay, and a sixth swing after regrowth. Its final state encoded to **7,920 bytes** and its closed database measured **561,152 bytes**. The run recorded 82.849 ms through first depletion, 600.282 ms for the intermediate replay/regrowth/second-harvest segment, and 445.790 ms for final full replay. Other tests ran concurrently, so these are observed qualification-run timings, not a controlled benchmark or a production performance budget.

The retained 6,000-step checkpoint fixture also passed. It remains a distinct small workload; neither trace qualifies long-lived production recovery, larger node/actor counts, other devices or desktop export packages.

## Native failure investigated separately

The coordinator's first rendered gathering run successfully harvested five wood and four berries, paused, and reopened. Its regrowth stage later timed out. Read-only inspection of that failed run is retained in `native-timeout-diagnosis.json`, with the exact failing process report.

At timeout the saved world had advanced from step 361 to **580**, only 219 accepted steps after reopening. Oak was due at step **1,100**, berries at **1,060**; their issue cursors, finite capacities, clock and journal were coherent. Thus neither plant was yet due to regrow. The save does not establish why active progression stopped or slowed: native focus/readiness pause and a stall remain possibilities. The reviewer did not change simulation rates, skip the source delay or claim native acceptance from the managed pass. Subsequent coordinator native evidence, if successful, must be retained separately from this failed run.

No pre-fix gameplay test failure is claimed for these 11 new cases. The first coordinator compilation caught an xUnit analyzer warning in one authored assertion; it was corrected before this executable test run. Native rendering, final art/animation, actual migration, remote authentication, full observation and production save lifetime remain outside this Hosting qualification.
