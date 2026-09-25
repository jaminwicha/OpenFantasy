# Encounter action review

Independent native critic Godel reviewed action/state ownership, source composition, admission,
canonical restore, authentication, targeting, timing, HP/incapacity, random replay and byte headroom.
This was a bounded source review, not a playtest or runtime qualification. The critic also checked
the final timeless encounter-action description against the source. No Fable invocation was made.

Two consequential findings were corrected before qualification:

1. Profile23 decoded its action archive but omitted it from the reconstructed world constructor.
   This selected profile22 and made complete recovery fail. Restore now passes the decoded owner;
   durable queued/first-strike/lethal recovery exercises the complete codec.
2. Admitted `int.MaxValue` attack and power1000 overflowed the damage helper's `uint` conversion before
   the actual HP clamp. Damage stays wide until safe saturation. Actual HP is bounded by signed int,
   so subsequent HP clamping retains the exact mathematical loss. Independent BigInteger extreme
   cases and an explicitly alternate authored world exercise this boundary.

The critic confirmed both corrections. An additional suggested anatomy-field enlargement was
investigated and retracted: complete body admission forbids mixed corporeal/spectral functions. The
largest valid corporeal function frame has nine functions and fits the existing160-byte bound;
the existing maximum-valid-body roundtrip automatically includes the new function. No byte limit
was enlarged or gate weakened for this suggestion.

Actual Host creation uncovered two other composition defects, with original logs retained:

- The added wolf used its source provenance hash as its comprehension rules version. It now uses
  the already-admitted shared comprehension rules pin, retaining an empty language profile.
- The anatomy decoder's hardcoded known-function count excluded the new `NaturalWeapon` value11.
  The known range now includes it while preserving all existing enum values and old canonical bytes.
  Old independent anatomy fixtures explicitly assert the new function is absent; they are not
  rewritten to manufacture anatomy or change their expected historical hashes.

The source review found no further consequential defect in the bounded pending-choice, side/body/range,
queue-versus-resolution, incapacitated eligibility, RNG, schema5 or serialization reserve contracts.
This does not qualify maximum-root exhaustion, production latency/scale, full encounters, native battle
presentation, independent co-op, death/rewards or any complete campaign phase. Those remain open.

Runtime and external evidence, including any additional failures and corrections, is indexed in
[the work record](README.md). Construction/water ownership uses the actual project owner instead of
assuming exactly one host-controlled resident; adding a real wolf does not transfer those rights.

The first focused run retained two fixture failures, subsequently corrected with the same assertions:
the old unknown-function negative case used numeric11, which now has a declared meaning; numeric12
still must refuse. The duel attempted a raw file comparison while the real authority held its
exclusive file lease. The fixture now closes that authority for byte comparison and reopens it before
continuing. It preserves both exact recovered state and unchanged closed-save bytes. Runtime locking,
world admission, assertions and deadlines were not relaxed. The corrected focused run passes all65
selected cases; the original log/TRX and partial export remain retained.

The foundation wrapper initially compared the `Runtime` diagnostic along with world values. Actual
Host.NET10 and native.NET8 processes both succeeded with empty stderr and agreed on every deterministic
field. The initial comparison failure is retained; the corrected check compares contract, identity,
sample and calendar using those same outputs. No new execution or altered runtime result is claimed.
