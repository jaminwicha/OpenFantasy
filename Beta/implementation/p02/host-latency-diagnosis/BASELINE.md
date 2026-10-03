# Reservation baseline

The new fixed profile17 continuation probe built without warnings and completed every canonical,
mutation-refusal, occupied-identity, commit and reopen check before the intended final reference-
reuse assertion failed. Its 16 measured decisions allocated 226,050,336 bytes (14,128,146 per decision).
Elapsed 222.195 ms is observational; the byte count is the fixed comparison. The measured input
hash is `876d6620c27eba81f5f50c7daf36879a00168770b23661f89b3aea87915f3461` and candidate hash
is `d4ef0be77be607b31f1e737801d81e52f82fa712d62536bb77933a55d68ab3f3`; both encode to 245,730 bytes.

[Baseline report](qualification/reservation-20261003T015338498682Z/report.json) retains the failure,
full log and TRX. This is an expected-red diagnostic checkpoint, not accepted runtime work. Existing
runtime source is unchanged from `6a723bed`; the sole new test and supporting evidence are retained
before the runtime optimization under `codex/checkpoint-before-reservation-reuse-20261003T0155Z` (resolve its exact commit through Git).
The original server failures remain unexplained; the later instrumented pass and allocation observations
are recorded separately in [the trace analysis](BASELINE_TIMING.json).
