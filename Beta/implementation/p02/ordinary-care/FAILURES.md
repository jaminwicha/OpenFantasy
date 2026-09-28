# Retained failures and corrections

- `qualification/build-01.log`: four new-test compile diagnostics used unavailable APIs or a nullable
  care reference. Corrected to strict currency equivalence, `OpenExisting`, and the asserted nonnull
  owner. No production rule changed.
- `qualification/focused-01.log`: crafted aid reached durable commit, then the test compared receipt
  object identity instead of canonical bytes. The raw self fixture also tried an unauthorized helper
  deposit into the player's owner-only inventory; authority correctly refused it. Receipt comparison
  now uses canonical encoding. Explicit fresh ordinary content partitions the same two source doses
  between player and Wren, preserving the original scenario and deposit policy.
- `qualification/focused-02.log`: three ordinary cases failed fresh admission because the new split
  treated the third `SourceInterval` parameter as a quantity. It is an exclusive end: the player's
  interval is `[1,2)`, not `[1,1)`. The nine older care-admission tests passed. Corrected source
  distribution passes all 12 cases in focused-03.
- `qualification/build-05.log`: xUnit's analyzer refused `Assert.Single(collection.Where(...))`.
  Used its predicate overload with the same assertion; build-06 passes with zero warnings/errors.
- `qualification/preservation-before.json` has an empty milestone map because its ref prefix was
  too narrow. Preserve this failed inventory. `preservation-recorded.json` uses the correct wildcard,
  checks all 58 refs in the prior packet baseline plus that packet's subsequent milestone, and records
  all 59 refs before any branch write in this increment.

No runtime deadline, assertion, acceptance threshold, source combat statistic, recipe, total initial
Potion supply, currency rule or replay validation was relaxed. Archive stress calibration is explicitly
separate from unchanged source-stat gameplay tests. No automatic paid delegation retry ran.
