# Encounter timing and exposure checkpoint

2026-09-25. The portable C# timing component implements the exact Wait scheduling prerequisite for
the live encounter work described in [DESIGN](DESIGN.md). It does not create a second duel game or
change any existing world profile. The owner's pure-C# decision remains fixed.

## Implemented scope

- Scoped immutable readiness, required-human decision barrier before automatic resolution,
  deterministic actor order, positive speed-derived action/Wait debt and exact five-unit ticks.
- Canonical bounded archives preserve pending choices and nonzero debt through restart; strict
  expected scope, identity, width, ordering and clock-shape checks reject malformed data.
- Existing `ExposureProgress` is retained through a scoped binding, including preexisting fractional
  carry. Zero-duration choices/holds earn no progress. Tactical observations cannot infer wall time.
- Exposure decoding requires the exact current clock, subject and rate. Arithmetic consistency is
  explicitly separate from authenticated accepted history and effect publication.
- One bounded read-only Host command reports alternative tick/confirmation proposals and exact bytes.
  It does not open SQLite or publish world effects. No shared live simulation law changes.
- [Architecture reference](../../../Runtime/docs/architecture/encounter-timing.md) and the time reference
  describe the executable contracts and remaining application obligations separately.

## Verification

The final solution [build](qualification/build-final.log) passes with zero warnings/errors. The
[complete Core suite](qualification/core-tests.log) passes **1,064 tests**, zero failed/skipped:
1,020 prior cases plus 44 new timing/exposure cases. This is not a claim that the 585 Hosting/client
tests were rerun; their source/behavior is unchanged and their previous qualified result remains in
the [native breach packet](../native-terrain-breach/README.md). No native rendering/input code changes.

The new cases cover all-required-human barriers, automatic-choice ordering, actual costed Wait,
integer cost extrema, missed-poll stasis, exact queue/confirmation/tick restoration, preserved debt,
stale/duplicate/unknown choices, complete actor bounds, capacity exhaustion and malformed archives.
Exposure cases prove preexisting carry, no retroactive join time, duplicate observation, exact pinned
restore, zero-duration decisions, 36 accepted ticks per 1800 ms quantum and atomic overflow refusal.
Four independently generated exposure byte/hash vectors include a tactical duration wider than 64 bits
in milliseconds. A counterfeit-history case deliberately demonstrates the boundary: shape-valid
arithmetic cannot authenticate accepted world history or grant effects.

The independent [Python oracle](../../../Runtime/tools/check_encounter_timing.py) passes
**35 actual Host processes** with unchanged 10-second bounds: eight independently authored sample clocks,
six sequential separate-process recoveries and 21 malformed/foreign-scope refusals. Every initial and
proposed state, canonical archive and hash matches independent integer/byte calculations. The complete
128-actor/128-choice clock is 14,556 bytes, within 16 KiB. Input files remain unchanged.
[Summary](qualification/host-oracle/summary.json) and full per-process output/inputs are retained.
The existing [Host foundation control](qualification/foundation-control.json) also passes.

[Independent review](REVIEW.md) checks timing, exposure, canonical recovery and the Host authority
boundary. Initial analyzer and oracle-script errors remain recorded in
[failures](qualification/INITIAL_FAILURES.md); neither required a game-rule or gate change.

## Next integration and open gates

This qualifies component arithmetic/archive recovery, not live SQLite encounter admission, combat,
branch calendar composition, independent co-op, NPC damage/lifecycle, animation, audio or playability.
No P00/P01/P02/P03 or production gate closes. The 128-actor bound is a fail-closed component limit, not
the full shared-pause/64-evaluation continuation. No crowd truncation or fallback claim is accepted.

The next coherent live work must version selected tactical time and process allowances in the existing
inhabited world, then bind admitted timing/subject ancestry, sole command receipts and complete journal
recovery. Current exploration steps charge 1800 ms while tactical ticks charge 50 ms. Gathering and liquid
processes cannot receive the old per-step allowance for each tactical tick or gain catch-up allowances
on release. Retained carries need real interval/effect ownership in the complete world.

Executable HP/MP already exist in progression, but damage and consistent incapacity/action gates are
required before admitting a source-backed monster/ability. The first encounter must connect ordinary
field positions, controlled actions, conservative NPC decisions, costed outcomes and safe release.
Source speed/action semantics need explicit versioned interpretation. Environmental wet occupancy,
reactive liquids, dependency closure, controller handoff, joins/merges, hazards, broader history and
scale remain their original required proofs. The [design](DESIGN.md) keeps that dependency sequence.

The reviewed 22 frozen spec files, unrelated 19-file predecessor index and earlier milestone markers
are preserved; exact checks and source fingerprints belong to `qualification/preservation.json`.
