# Actual crafting and preserved save continuity

9 September 2026 local time. This increment connects gathering to actual instantaneous crafting in the portable C# world, the existing in-process host and the native Godot client. It preserves all reviewed requirements and earlier evidence. It does not close P00/P01/P02 or the first-feedback-prototype goal.

## Implemented connection

The original `craft_rations` recipe consumes two berries and one wood and creates two rations. The explicit physical interpretation packs each berry portion in wooden material and retains one recoverable offcut: 90 grams of input become two 40-gram rations and 10 grams of offcuts. Source counts and source item pins remain unchanged; physical calibration, packaging and offcuts are separately pinned authored content. The operation remains instantaneous, without converting the old illustrative 120-work value into elapsed ticks.

Raw source intervals retire exactly once. Produced lots claim disjoint intervals in a real transformation batch rather than appearing as newly admitted raw sources. Complete output accounting, per-output composition and an acyclic dependency graph support subsequent recipes consuming transformed ingredients. Partial remainders preserve their identity and revision history. Failed selections, protected inputs, stale revisions, insufficient capacity or historical identity collisions leave the material state unchanged. Gathering and handover count derived contents against the same pack capacity; consumed gathering rights still constrain each source's monotonically increasing cursor.

The actual client exposes C/controller B to pack rations using owned, publicly projected lots. The HUD shows wood, berries, rations and offcuts. Godot supplies input and presentation; the same host commits ingredients, products, waste, lineage, receipt and identities atomically. Pauses and dialogue holds do not purchase work or simulation time.

## Existing worlds continue

World profile3 and exact SQLite schema3 add a typed migration event and permanent definition-identity reservations. A migration preserves the original profile2 genesis, prior journal rows, command/receipt keys, source budgets, consumed cursors, actor/control IDs, inventories, terrain, dialogue, calendar and experienced time. It adds one commit revision and zero advancing steps.

Startup selects `wrens-farm-production.sqlite`. If that destination is absent and the prior `wrens-farm-work.sqlite` exists, it requests an explicit upgrade of that gathered world. SQLite creates a consistent private copy; the host compares retained rows, migrates the copy atomically, replays its complete history, closes/checkpoints it and publishes only to an absent destination. The original remains intact. Creation, backup and final publication reject an occupied main/WAL/shared-memory/rollback-journal file set and leave any existing files untouched. The final check also catches sidecars introduced just before publication. Unsupported/corrupt worlds fail opening, with no reset or fallback to an older history. A brand-new world admits production directly. Legacy exploration/gathering formats and their native verification paths remain supported.

## Evidence and review

Final Debug and Release each passed **451 core + 157 hosting = 608 tests**, with zero skips. The complete verifier passed **517 current runtime inputs**, all **19 independent fixture generators**, **68 host inspections**, earlier native journeys and the new three-process production journey. The actual final rendered journey also passed after the publication hardening.

[VALIDATION.json](VALIDATION.json) retains final executed tests, exact current inputs, independent oracle results, native reports and source-file preservation. The integrated verifier runs all earlier golden fixtures, host checks and native farm/lifecycle/gathering journeys as well as the new production journey. Debug and Release counts are recorded separately. An actual rendered macOS run complements the headless automation; neither is an exported-platform acceptance claim.

The new native journey uses three processes:

1. Open the retained gathering profile, walk to berries and gather twice: eight wood, two berries and two finite tools.
2. Upgrade that actual paused save on a copy, preserve the recorded public fields exactly, resume and craft: seven wood, two rations, one offcut and two tools.
3. Reopen the production world with an exact paused public projection, resume, gather twice more and craft again: six wood, four rations, two offcuts and two tools.

The original gathered database remains byte-identical through both later stages. The native projection proof is complemented by Hosting tests comparing complete canonical state and historical rows, old/new retries, held/depleted progress, eight tampering cases and six interruption boundaries. These fault injections do not establish hardware power-loss or every filesystem behavior.

The first integrated run correctly failed on an editor error despite exit0. Its command combined `--import` with `--quit`. The installed Godot help says `--import` owns the wait-for-import/exit lifecycle while `--quit` requests exit after one iteration. Verification now uses only the documented import lifecycle and retains strict error detection. Two focused imports and the subsequent complete run are retained separately. A separate independent persistence review reproduced stale orphan-WAL replacement in SQLite; a shared file-set guard and pinned native regression now cover that publication/creation edge. Removing competing shutdown requests is the justified import correction; no claim is made that an engine-internal race was conclusively diagnosed or that all editor teardown failures are impossible.

| Record | Responsibility and claim |
| --- | --- |
| [Source contract](SOURCE_CONTRACT.md) | Exact original recipe/items, source partial-add bug, packaging interpretation and later consumption obligations |
| [Production contract](CORE_CONTRACT.md) | Typed retirement, calibrated allocation, chaining, bounded canonical ledger and authority requirements |
| [Admission](ADMISSION.md) | Preserved source closure, exact item/physical pins and new authored manifest |
| [Core critic](CORE_REVIEW.md) / [content critic](CONTENT_REVIEW.md) | Independent domain/source review and reproduced oracle evidence |
| [Migration contract](PERSISTENCE_REVIEW.md) | Author's explicit copy-and-publish design and remaining qualification obligations |
| [Hosting tests](hosting-validation/RESULTS.md) / [independent hosting critic](HOSTING_REVIEW.md) | Actual native SQLite cases and separate persistence inspection |
| [Visual review](VISUAL_REVIEW.md) | Readable actual 960×540 controls/counts; provisional art limitations |

## Next connection and limits

Wren's original six-wood quest still needs actual acceptance, completion, exact XP/stat/level and 200-gil reward semantics. A prior ordinary gift is not retrospectively quest credit. The original quest completion does not construct a windbreak. Construction needs its own authored blueprint, site, finite materials, custody, work, support, clearance and accessible approach. Six current 50-gram wood units are only 300 grams: a lightweight seedling guard or a contribution to a larger funded construction bill is physically distinct from six complete structural cells. Resolve that specific blueprint in the next work packet while preserving the original quest count and preventing double payment.

Eating rations is also not yet implemented: original HP/MP effects and the wooden packaging's subsequent disposition must both be accounted for. The current production ledger's 32 recipes, 256 retained operations and 4,096 combined live lots are development limits, not a production economy capacity promise. The profile3 global production revision also means that independent actors crafting from one shared snapshot can conflict; later co-op/NPC scheduling must qualify fair batch-start or per-actor admission without changing old replay. Count-based storage is enforced; positive per-term volume calibration does not establish full physical volume-capacity admission. Long-lived ledger pagination, broader migrations, cross-branch/export rights, timed jobs, combat/Wait, co-op, frontier/history simulation and the remaining complete-game systems stay required.

The current sprites and grass are a provisional source baseline. The reviewed production-art target, native animation/puppetry, audio, input comfort and enjoyment require separately owned artist work and human playtests. Local macOS Debug/Release builds do not prove Windows/Linux exports, four-player networking, performance targets or production readiness. No earlier GDD/spec version or source content was discarded.
