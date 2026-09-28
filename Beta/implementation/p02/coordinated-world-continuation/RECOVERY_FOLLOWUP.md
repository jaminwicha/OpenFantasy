# Recovery timing follow-up

The required external recovery operation exceeded 30 seconds in both the original run and one exact
same-command diagnostic. Neither result establishes a machine-contention cause. The earlier normal
commit, duplicate and historical operations took 23–26 seconds. Preserve their actual saves, commands,
execution pins and failed timing assertions; do not rerun repeatedly until one happens to pass.

## Known source facts

- `RecordedWorldReader.ReadForHost` already opens one exclusive lease and performs one full journal
  verification pass. Removing an imagined duplicate replay is not the next optimization.
- `WorldJournalRecovery` verifies every command, outcome, permanent identity, checkpoint, actual
  retrieved archive and final head, even for an earlier selected revision. Preserve those checks.
- `WorldSnapshot` invokes complete resource/care admission on constructed roots. Resource admission
  reconstructs the bounded active causal history from its basis; time/movement boundaries can retain
  the same causal owners while constructing new world roots. The previous admission index reuses
  exact immutable terrain/watershed layers, not resource validation.
- The actual chosen closing world is revision 1569 with two prior archives; the next accepted dose
  produces archive ordinal 2. Before/after canonical states and complete databases are retained.

## Hypotheses to measure, not conclusions

Measure exclusive replay, final report projection, cumulative allocation and representative call
costs separately on the same preserved before/after databases. No other task-owned heavy test or
build should overlap a deadline-sensitive run. Identify the actual execution process, not merely a
waiting VSTest parent. A distinct bounded profiling run may gather measurements; it is not a pass of
the 30-second acceptance command.

Repeated full validation of unchanged immutable causal owners may be redundant. If measurement
confirms it matters, consider a narrowly owned admission certificate keyed by every dependency the
validation actually reads. Only a previously fully admitted immutable state may supply reuse; changed
care, resources, quest causes, lifecycle episode/random/catalog facts, world scope or chronology must
invalidate it. Public constructors/decoders and untrusted checkpoints still perform complete checks.
Do not retain an unbounded ancestor chain, skip actual archive retrieval or substitute a counter/hash
for causal validation. This is a candidate for investigation, not an accepted cache design.

Any optimization needs a before-change checkpoint, independent invalidation critique, retained
adversarial cases, exact historical candidate/receipt/identity byte comparisons and unchanged external
BeforeCommit/AfterCommit deadlines. Complete native post-cut interaction and reopening afterwards.
Full production lifetime/scale and multi-platform/device acceptance remain distinct gates.

## Bounded independent source review

The read-only critic identified two candidates for measurement. Resource admission reconstructs the
active causes on each invocation; the retained arithmetic head does not replace permissions, care,
quest or seeded-attack checks. Its dependencies include ResourceHistory, Progression, Care, Production,
Quests, Inventory controls/storage revisions, actor-to-bag mappings, presence revision lower bounds,
lifecycle seed/catalogs/episode/actions, world scope, admission and revision. Whole lifecycle reference
identity changes even on ordinary time steps, so it cannot alone identify unchanged resource causes.
Public construction, decoding and joint cuts must still perform full admission.

The other candidate is repeated encoding: Core validates the candidate's canonical envelope and
Hosting subsequently encodes the same immutable root for persistence. A retained canonical frame may
avoid duplicate work, but would need a bounded memory policy and protection against callers mutating
returned arrays. Neither candidate is approved or implemented by this analysis.

`qualification/profile_recovery.py` instruments a disposable source copy at eleven named boundaries,
then reads a copy of the failed ledger. Inclusive elapsed and thread-allocation totals overlap; they
must not be summed as independent stages. Its finite 90-second diagnostic collection budget is not
the unchanged 30-second acceptance command. The script verifies original source/save preservation;
all acceptance must subsequently use ordinary uninstrumented binaries and the original thresholds.

## Executed diagnostic

`qualification/recovery-profile-01` completed one instrumented read of the failed revision-1569
ledger. Its command took 30.240 seconds; original source/save hashes were unchanged, and both the
reported selected and source-head hashes equal the retained pre-commit root. This remains diagnostic
evidence, not a pass of the unchanged 30-second external command.

| Instrumented boundary | Calls | Inclusive time | Inclusive thread allocation |
|---|---:|---:|---:|
| Complete replay | 1 | 29.645 s | 16,441,177,456 bytes |
| Commit preparation | 1,569 | 28.662 s | 16,295,783,920 bytes |
| World encoding | 3,659 | 11.325 s | 10,904,782,680 bytes |
| World-root construction | 2,092 | 9.926 s | 2,332,518,176 bytes |
| Resource cause validation | 2,092 | 5.946 s | 1,623,303,808 bytes |

These inclusive values overlap and must not be summed. Allocation is cumulative, not live memory.
The measurement supports investigating repeated canonical encoding first; it does not establish the
savings of an unimplemented cache. A narrowly owned immutable candidate frame passed from Core's
existing envelope check to Host persistence is a candidate, before broader world-validation reuse.
Preserve exact encoding, size checks, hash derivation, defensive byte ownership, actual archive
verification and full replay. Take a new checkpoint before that implementation and qualify its bytes,
caller-mutation resistance, memory retention and original crash deadlines independently.
