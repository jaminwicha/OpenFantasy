# Independent custody-decoder and exposure review

Reviewer: portable-core/custody critic. 9 September 2026. Runtime implementation belongs to the coordinator; this reviewer changes only this review directory. Actor and production definition work is outside this review's independent semantic scope.

## Initial code inspection

The custody decoder retains the established v1 encoder bytes, rejects input exceeding 942,753 bytes before nested allocation, and bounds each nested record/collection. Its fixed-depth reader checks magic, encoding version, tags, exact primitive widths, collection field counts and exhaustion of every frame. Strict ID ordering prevents constructor normalization from accepting a noncanonical stream. Reservation absence remains distinct from a present zero identity. Constructed source-lot/snapshot invariant failures produce typed parsing diagnostics. No live state is mutated or partially published.

The exposure design separates unsigned normalized experienced days/milliseconds from signed calendar destinations. Profile conversions expose only accepted exploration/tactical progress. The rate-bound accumulator's quotient-first UInt128 calculation is mathematically equivalent to multiplying the full exposure then dividing, but supports low rates over eon-scale durations without an unnecessarily oversized intermediate. Overflow remains a rejection; the immutable original survives.

Two restoration boundaries were called out before final tests:

| ID | Priority | Boundary | Status |
|---|---|---|---|
| DEF-01 | P2 | Restored elapsed/rate must agree exactly with completed output and remainder. The only supported start is zero, with no rate switch/reset, so arbitrary completed credit must not be accepted. | Closed: coordinator added exact recomputation; reviewer verified code and passing invalid-state/rate tests |
| DEF-02 | P2 if JSON roundtrip is supported | The getter-only LocalExposureDuration struct needs an explicit JSON constructor or a clearly excluded generic JSON path; implicit struct deserialization otherwise resets elapsed time. | Closed: diagnostic JSON route uses JsonConstructor; nonzero and invalid-normalization tests independently pass |

## Final reviewed result

**No remaining concrete P0/P1/P2 defect was identified within the reviewed custody-decoder and exposure arithmetic scope.** Both restoration boundaries were resolved in coordinator-owned code and independently checked. Restore now recomputes cumulative output and carry against the pinned rate; the duration's explicit JSON constructor preserves nonzero experienced time and validates normalization. No runtime file was edited by this reviewer.

The independent stable-build run used pinned SDK 10.0.401/runtime 8.0.31, Debug net8.0, with `--no-build --no-restore` and a custody/exposure namespace filter: **38 passed, 0 failed, 0 skipped** (30 custody, eight exposure). The runner reported 578 ms; this is correctness evidence, not a performance benchmark. The [TRX result](test-results/decoder-exposure-independent.trx) records actual outcomes.

The reviewer separately executed all three Python oracle scripts: frozen valid custody, 27 malformed custody frames, and four exact exposure vectors. Each regenerated fixture matched byte-for-byte. The original custody fixture also matched its hash recorded by the prior independent custody review, preserving the established v1 bytes. The 534-byte original snapshot supplies all 534 incomplete-prefix probes and 4,272 single-bit mutation probes; another 1,000 fixed-seed random frames must either reject with typed diagnostics or re-encode exactly. The maximum valid 942,753-byte snapshot test includes present all-zero reservation identities, every recognized protection flag and maximum revision values. These bounded probes do not claim all malformed inputs were enumerated.

The final [custody decoder contract](../custody-decoding/CONTRACT.md) and [exposure contract](../exposure-contracts/CONTRACT.md) correctly preserve future obligations and distinguish arithmetic, structural decoding and state consistency from authoritative gameplay. Static review of the host inspector confirms an explicit pre-allocation file bound and `snapshot-invariants` output scope. The reviewer did not execute host CLI tests and does not report the coordinator's full 194-test run as independent execution.

The [validation manifest](REVIEW_VALIDATION.json) binds exact reviewed source, tests, support contracts, oracles, toolchain inputs, tested assemblies and TRX. [Oracle validation](ORACLE_VALIDATION.json) records independent byte comparison results. Assembly hashes bind execution artifacts without claiming semantic review of unrelated actor/production/history/scheduling code.

Still unproved here: actual Wait/consent/interrupt selection, interval ownership and deduplication, resource/effect application, full biological/physical processes, source/content admission, legal custody, whole-world uniqueness, cross-branch allowances, durable multi-domain commits, complete saves/migrations/restart and binary exposure decoding. No performance, cross-platform export or user-experience qualification is claimed.
