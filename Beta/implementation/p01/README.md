# P01 durable authority and terrain increment

9 September 2026. This increment begins P01 execution while the remaining P00 preparation exits stay open. It retains the entire reviewed design and all 181 requirements. It does not declare P01, P02 or the first playable prototype complete.

The subsequent [native local-world increment](native-loop/WORLD_CONTRACT.md) now connects grounded player/NPC movement, Wren's original utterance, shared reading holds and finite wood handover in Godot. Presence, terrain, custody, individual exposure, language and dialogue persist in composed schema2. The original counted schema1 described below remains supported as its separate profile, with no silent migration. The native walkthrough and exact paused-view process reopen are retained in that increment; production art, full quest completion and multiplayer are still open.

The next [gathering increment](gather-craft/README.md) connects finite tool-owned extraction, banked work and persistent depletion/regrowth with versioned profile dispatch. Both prior save profiles remain supported. The subsequent [production connection](production/README.md) adds actual raw/derived ingredient consumption, rations and offcuts, strict profile3 replay and a preserving schema3 upgrade of existing gathered progress. Full Wren quest/rewards and physical construction are the next required connections.

## Implemented behavior

The portable counted authority now admits explicit finite sources, actor controls and owner/deposit rules before evaluating transfers. Players and host-controlled NPCs use the same operation. Unauthorized access cannot probe arbitrary expected revisions; finite source intervals, capacity, historical identity claims and exact retry content constrain accepted actions. A split at the 4,096-lot ceiling rejects cleanly while a whole-lot transfer remains possible.

`OpenFantasy.Hosting` is a shared net8.0 library used by the net10.0 dedicated executable and net8.0 Godot client. It owns a local SQLite world with one lifetime connection, exclusive writer ownership, WAL/FULL durability and foreign keys. A transaction commits the command, receipt, complete counted state and newly reserved identities together. Rejection receipts advance the command sequence without changing the custody revision. Exact retries return the original receipt after current delivery/control validation. No receipt is fabricated after uncertain IO or commit failure.

Opening a world checks its exact versioned schema under exclusive ownership, validates stored checksums, replays every journal decision and verifies the resulting head and historical identity registry. Creation never overwrites or silently repairs an existing/corrupt file. SQLite integers do not truncate unsigned state counters: commit sequences use canonical 8-byte big-endian blobs; domain values remain in strict canonical snapshots. These protections address accidental corruption and authority consistency, not cryptographic authenticity against an operator who can rewrite every file.

The separate portable terrain module generates a seeded, globally addressed 3D substrate with bounded cells, negative-coordinate floor arithmetic and pinned materials. Adjacent/overlapping regions agree independently of generation order. Excavation retains the original formation/source identities, survives strict snapshot encoding/decoding, and changes standing/support queries without minting inventory. This is a small retained terrain substrate, not continental history, construction, fluid dynamics or a finished mining engine.

## Evidence

The [durable authority record](durable-authority/README.md) records full local verification, real external process kills, native SQLite loading in Godot and release checks. The independent [storage critic](durable-authority-review/CODE_REVIEW.md), [authority review](authority-review/REVIEW.md) and [terrain review](terrain-review/REVIEW.md) retain bounded claims and exact reviewed inputs. Original preparation, source archives and earlier evidence remain intact.

## Next connection to gameplay

1. Build on the now-composed actor/body/location/terrain/time transaction. Couple excavation/work to finite source admission and actual inventory yield; do not implement a second Godot-owned inventory or editable world.
2. Build on the now-connected gathering and original ration recipe with full quest adjudication/rewards and visible construction effects. Preserve all source fields already mapped and carry exact source provenance through consumed inputs and outputs. Movement, gathering, crafting and finite handover now work; quest credit/rewards and construction are still needed to complete the loop.
3. Add live NPC commitments, Wait encounter scheduling, durable event/outbox delivery, session grants/rebinding and reconnect. Prove the connected native loop and 1–4-player behavior against the full P01/P02 exit criteria.
4. Keep native pixel-art/animation/audio production with separate artists, then run virtual and human playtests. A rendered original-asset farm and native input walkthrough now run, but neither establishes the approved production-art target, animation quality, sound or enjoyment acceptance.

Remaining P00 workload/catalog budgets, missing representative domains and source-to-playable calibration still matter. Full platform exports, power-loss/filesystem qualification, storage/recovery budgets, branch/time-travel migration and production save compatibility remain explicit obligations. None is closed by a passing local test or by adding this library.
