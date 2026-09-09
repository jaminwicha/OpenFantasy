# Independent decoder and exposure review plan

9 September 2026. Reviewer owns this review directory only; runtime implementation belongs to the coordinator. Actors and production definitions are being reviewed by other owners. This record supplies bounded pre-implementation recommendations, not executed acceptance evidence.

## Custody decoder

Preserve every accepted v1 encoder byte. The exact largest current snapshot is **942,753 bytes**: each storage frame is 78 bytes; each source-lot frame with a reservation ID is 221 bytes; 256 storages produce a 21,027-byte collection; 4,096 reserved lots produce a 921,631-byte collection. Including the snapshot frame and fields yields the stated maximum. Check the total bound before nested allocation; check collection counts before loops or allocation. Do not infer that these finite working-group bounds limit shipping-world inventories.

Parse fixed schema depth, explicit magic/version/tag and exact field counts. Identity fields require 16 bytes, definition hashes 32, integers 8, and optional reservation identities either zero or 16 bytes. An all-zero present EntityId remains distinct from absent reservation. Reject unknown protection bits and preserve all recognized consequential fields. Reject trailing fields/bytes at every nested frame, not only the root. Storage and lot collections must be strictly ordered by canonical IDs; snapshot constructor sorting must not silently canonicalize an invalid input stream.

Only construct and return a complete immutable snapshot after every structural and semantic invariant passes. Duplicate identities, absent custodians, over-capacity contents, overlapping source claims, inconsistent source definitions and empty/reversed intervals reject. Constructor validation failures should become a consistent bounded decode diagnostic. A malformed decode must not mutate any existing snapshot or partially expose restored state. Decoding is not content/source admission, global uniqueness, ownership, authentication, durable world loading or schema migration.

Required hostile cases:

- Every truncated prefix of the independent golden snapshot; invalid magic, encoding version, tag or fixed field count.
- UInt32.MaxValue field lengths/counts, oversize root/nested payloads and excessive collection counts, with rejection before allocation based on attacker lengths.
- Wrong integer/identity/hash/reservation widths, unknown flags, and extra/trailing root or nested fields.
- Unsorted or duplicate IDs; source-overlap, wrong unit definition, absent custodian and impossible receiver occupancy.
- Absent reservation versus present zero identity; exact maximum-count/maximum-byte success and one-over rejection.
- For deterministic mutated samples: reject cleanly or return a valid snapshot whose re-encoded bytes are exactly the input. Do not incorrectly assume every bit mutation must be invalid.

## Exposure accounting

Keep nonnegative accepted local exposure duration a distinct type from world calendar destinations, commit sequence, tactical readiness coordinates and wall-clock durations. A primitive should not accept a branch-calendar subtraction to manufacture actor exposure. The current profile advances one accepted exploration step by 1,800 local/world milliseconds; a tactical progress unit represents 10 local exposure milliseconds, with five units per unheld step. Held time advances zero. Helpers do not themselves authorize advancing time or implement encounter dependency admission.

A rate-bound immutable rational accumulator preserves an explicit remainder. For rate numerator `n`, positive denominator `d`, accepted exposure `t` and prior remainder `r`, calculate checked wide `t*n+r`, divide by `d`, and retain the remainder in `[0,d)`. Checked accumulation rejects an unrepresentable output without changing the old value. Rate identity, units and denominator must remain associated with the saved remainder; a new rate cannot reinterpret an old remainder silently. A finite exposure increment/accounting value is not a substitute for the eon-scale `WorldInstant` calendar.

Tests should prove interval partition equivalence at fractional rates such as 1/3; zero/held exposure identity; exact 40-minute exploration-day conversion; tactical versus exploration rates; nonzero-remainder continuation after value restoration; invalid denominator/remainder rejection; maximum-width products and checked accumulation overflow; and independence from elapsed wall time or changed world destinations. These are arithmetic/representation tests. Full Wait, consent, interruption, biological lifecycle, source-history reconstruction and offline-host policies remain separate unimplemented responsibilities.
