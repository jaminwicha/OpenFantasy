# Anatomy, production definitions, exposure and custody decoding

9 September 2026. This retained P00 increment follows the [149-test state-contract checkpoint](../state-contract-increment/README.md). It adds concrete, immutable definitions and strict restoration boundaries to the existing portable C# core. The full 181-requirement campaign remains in force; the [progress record](../PROGRESS.md) identifies the next implementation work.

## Delivered behavior

**Custody decoding.** Existing counted-custody v1 bytes now have a bounded strict decoder. It rejects corrupt framing, unsorted or duplicate records, invalid protection flags, overlapping source claims and over-capacity contents. A maximum legal snapshot is exactly 942,753 bytes. Decoding a retained snapshot preserves its hash and permits the same subsequent transfer candidate. The host exposes `--inspect-custody` as a local invariant diagnostic. See the [decoder contract](../custody-decoding/CONTRACT.md).

**Experienced time.** Duration values separate experienced time from calendar destinations. Exact rational progress preserves fractional carry across interval partitions and capture/restore, including eon-scale low-rate intervals. Restoration checks the entire pinned rate and recomputes credited totals and carry. Existing 40-minute exploration-day pacing has explicit accepted-step conversions. This arithmetic does not select accepted intervals, execute Wait or grant effects. See the [exposure contract](../exposure-contracts/CONTRACT.md).

**Anatomy and forms.** Versioned body trees, dimensions, functions and injury records derive anatomical eligibility for both players and NPCs. Missing ancestors suppress descendant functions; overlapping injuries retain separate causes; scars remain cosmetic. Five authored examples cover humanoid, quadruped, avian, legless reptilian and incorporeal bodies. Form references keep accountable actor identity separate from body, manifestation and tether identities. These references do not execute healing, death, possession or movement. See the [actor contract](../actor-contracts/CONTRACT.md).

**Material and recipe definitions.** Explicit mass, occupied volume, hardness and work units support versioned finite materials and one strict single-operation recipe format. Recipes resolve exact material versions and require input mass to equal output plus waste mass. The original `craft_rations` counts remain two berries plus one wood producing two rations, with the original payload hash retained. Separate illustrative physical values and a residue term demonstrate accounting; they are newly authored qualification data, not accepted balance. No output lots or new source claims are created. See the [production contract](../production-contracts/CONTRACT.md) and [calibration](../production-contracts/craft-rations-calibration.json).

Application types use immutable values, copied collections and cohesive behavior. Core code remains independent of Godot. No new assembly, IPC bridge or speculative framework was introduced. Runtime names and relative paths pass the owner's naming rule; the historical planning-folder name remains preserved outside that boundary.

## Executed evidence

| Evidence | Actual result |
|---|---|
| [Pinned Debug verification](debug-verification.json) / [Debug TRX](core-debug.trx) | **194/194 passed, zero skipped**: previous 149 + 6 custody decoding + 8 exposure + 15 Actors + 16 Production |
| [Release verification](release-verification.json) / [Release TRX](core-release.trx) | **194/194 passed, zero skipped**; full solution including Godot ExportRelease builds with zero warnings/errors |
| Ten independent Python stdout oracles | Previous six plus malformed custody, exposure, production and actors reproduce frozen fixtures without replacing expectations |
| Host diagnostics | **68 cases pass**: previous 39 plus one valid custody snapshot, 27 malformed snapshots and an oversized file |
| Source-packet and Godot checks | Original 52-file source packet set/lengths/hashes pass; editor import and host/client foundation projections pass |
| [Independent decoder/exposure review](../definition-review/CODE_REVIEW.md) / [validation](../definition-review/REVIEW_VALIDATION.json) | **38/38 tests independently executed** against stable Debug binaries; three relevant Python oracles match |
| [Actor review](../production-contracts/ACTOR_REVIEW.md) / [production review](../actor-contracts/PRODUCTION_REVIEW.md) | Separate workers cross-reviewed source, tests, definitions and exact oracle output; production review verified original recipe provenance |
| [Preservation](preservation.json) / [review input check](review-input-check.json) | Current runtime inputs match integrated evidence; reviewed file hashes match; the 19 unrelated staged Alpha paths retain their exact staged diff |

The coordinator owns full Debug/Release and host/Godot execution. Workers' cross-reviews distinguish static inspection and Python execution from managed test execution. The independent critic executed only the custody/exposure subset. Candidate tools remain SDK 10.0.401 and Godot 4.6.stable.mono.official.89cea1439 on the existing Apple M2/macOS machine. Core tests run on .NET 8.0.31; host and Godot binding smoke report .NET 10.0.12. These runs do not establish Windows/Linux exports, mobile support, production performance, native art/motion/audio acceptance or enjoyment. No new presentation behavior was added.

## Commits

- `4fb0717`: strict custody decoding, exact exposure progress, fixtures and tests.
- `003c600`: actor anatomy/form and material/recipe definitions, fixtures, tests and cross-reviews.
- The following integration commit retains host/verifier updates, executed evidence, the independent decoder/exposure review and current progress.

## Findings and remaining boundaries

Two restoration defects were closed and independently reviewed: captured exposure state now rejects totals/carry inconsistent with elapsed time and the pinned rate; diagnostic JSON now preserves nonzero immutable duration values through an explicit validated constructor. Reviews also clarified that anatomy functions are only one contributor to future capabilities, and that mass-balanced recipe definitions do not establish checked aggregate volume or actual receiver capacity. Physical process admission must validate those separately.

The custody v1 decoder task is complete within its stated schema. Actors and exposure currently supply canonical encoders and validated in-memory values; strict persistence decoding and migration remain required before loading authority state. Production supplies a strict definition codec but no crafting execution. Body dimensions/locomotion thresholds and material calibrations remain authored examples requiring later balance and presentation acceptance.

Next P00 work covers observation/language/cognition and representative ecology/fluid/teaching/acoustic fields; explicit source-to-playable mappings; measurable catalog/workload budgets; and separately owned native art/audio packets. Actual mixed-input processing must retain provenance through products and waste, validate volume/capacity, and commit inputs, effects, receipts and scheduler state atomically. Source admission, global identities, durable transactions, historical reconstruction and actual coupled Wait remain later implementation and qualification work. P00 stays open.
