# Finite counted custody and source-preserving transfer candidates

9 September 2026. P00 contract implementation for RQ-011/025–033/057/070/072/074/075/095/111/129–131 and the future material/temporal custody proofs. This contract deliberately names **untransformed counted source lots**. It is useful for a finite source-faithful wood delivery, while keeping crafted mixtures and cross-branch export as distinct obligations. It does not implement a playable quest, source extraction, content admission or durable authority.

## Ownership and finite inputs

`CountedItemDefinition` pins an EntityId and full immutable definition-version hash. The content adapter must validate that definition and its units. `SourceInterval` assigns a positive half-open unsigned interval `[Start, End)` to a source identity established before extraction or a renewable production cycle. Its end is at most UInt64.MaxValue. Count is exactly `End - Start`; distinct mass and volume measures cannot use this counted-unit contract implicitly.

`CountedSourceLot` owns an ID, custodian, pinned definition, source interval, revision, protection flags and optional job reservation. Definition and claim survive a custody transfer. `CustodianStorage` owns counted-unit capacity and revision; legal ownership, custody, physical capacity and consent are different concepts. This initial capacity is a literal integer rule budget, not a claim about mass/volume packing or maximum stacks.

`CustodySnapshot` supplies a world, branch, branch revision and **complete counted contents for every included custodian**. It defensively copies and sorts immutable storage/lot records. It rejects duplicate lot/storage identities, absent custodians, capacity overflow, overlapping intervals for a source, and inconsistent counted-unit definitions for one source. UInt128 accumulation prevents overflow from disguising excessive contents. Candidate input limits are 256 custodians and 4,096 lots; over-limit enumeration rejects at the next entry rather than truncating. These are bounded working-group limits, not shipping actor/world inventory limits.

The future trusted adapter must establish completeness, source existence, original reservoir/cycle quantity and global claim uniqueness. A constructor cannot prove that omitted real items do not exist or that a fabricated SourceId was admitted. Local duplicate checks preserve admitted claims within the supplied group; source creation, whole-world registries, domain partitions and cross-partition transactions remain host/world responsibilities. Refusing an over-budget group must preserve all queued work until a qualified transaction strategy exists.

## Transfer planning and source identity

`CustodyTransferPlanner.Plan(snapshot, CommandEnvelope)` consumes the already defined `TransferLotIntent`. It requires expected revisions for the branch, source lot and both custodian storage records. Every additional declared expected revision must also resolve inside this snapshot and match; an unsupported target fails closed. Scope mismatch, absent/wrong custodian, missing/stale revisions, active job reservations, equipped lots, insufficient quantity and inadequate receiver capacity reject without modifying input state.

This is an explicit transfer primitive. Favorite, heirloom and user-reserved designations persist on both partitions. It grants no protection override permission. Future **automatic ingredient selection must exclude these designations by default**, and must also honor equipment, other jobs, owner preferences and knowledge rules. Equipment detach and reservation-owner operations need their own coordinated actions; this planner refuses them.

A full transfer preserves LotId and source interval and increments the lot's revision. A partial transfer moves the deterministic prefix, retaining the original LotId for the remainder with revision + 1. The moved child uses existing `EntityId.DeriveSpawned(command.CommandId, 0)` and starts at revision zero. Ordinal zero is reserved for this transfer payload's sole split child; another spawning behavior requires a new contract. Claim endpoints partition exactly, with no overlap or gap. Both storage revisions and the branch snapshot revision increment once. Checked overflow rejects, including revision exhaustion.

The planner checks a split child against every locally known lot, custodian, world, branch, source, definition and reservation identity and against the command/principal/session/actor IDs. The inserting host must still check the whole-world identity registry. Source claims retain their source identity across branch copies; changing a branch or command does not create new export allowance. Neither this source interval nor the child ID authorizes transit.

`CustodyTransferPlan` contains one complete candidate, moved lot ID, original revision and original canonical SHA-256. `ApplyTo` verifies both revision and full base hash before returning the immutable candidate. An unchanged old snapshot can deterministically produce the same candidate again; this is not global exactly-once execution. Applying to an already advanced or same-revision-but-different snapshot rejects. The host must atomically commit all corresponding state, effects, scheduler/RNG changes, receipt and outbox, and answer accepted retries from durable receipts. No receipt is constructed here.

## Canonical encoding and validation

`CustodyEncodingV1` encodes/hash-binds snapshots with the existing core `OFC\0` version-1 framing and big-endian unsigned integers. Tags explicitly version each record:

- `custody.snapshot.v1`: world ID, branch ID, revision, nested storage collection, nested lot collection.
- `custody.storages.v1`: storage frames sorted by custodian's canonical ID bytes.
- `custody.storage.v1`: custodian ID, counted-unit capacity, revision.
- `custody.lots.v1`: lot frames sorted by lot ID.
- `custody.source-lot.v1`: lot ID, custodian ID, definition ID, raw 32-byte definition version, source ID, start, end, revision, protection flags, optional reservation ID. Absence is a zero-length final field; a present all-zero EntityId is 16 bytes and remains distinct.

The independent Python stdout oracle spells the existing frame and SHA derivation without importing application code. It pins original/candidate bytes and hashes for eight source wood units partitioned into six transferred plus two remaining. The quest source really requires six wood; the initial eight-unit stock and capacity values are test inputs, not a transformed source save or balance choice. Unit tests cover exact oracle agreement, full/split transfer, successive splits near the unsigned boundary, conservation, overlaps, capacity failure with existing receiver stock, stale competing candidates, all expected revisions, known child collisions, protection and reservations, revision overflow, bounded enumeration and immutable copies. Final executed results belong to the coordinator's dated evidence.

This increment has a canonical encoder/hash contract but **no custody decoder, durable snapshot format or migration interpreter**. A later persistence contract must supply a strict bounded decoder and restart/corruption/migration proofs before these values become stored authority. The encoded tags and golden bytes remain a compatibility obligation; diagnostic JSON is not a replacement for that work.

## Source lesson and later representability

Alpha's `AddItem` can partly fill a stack before returning false on exhausted capacity, contrary to its all-or-nothing comment; `CraftItem` later attempts refunds after calling it. This implementation does not reuse that mutation pattern. Its finite-capacity regression verifies both input inventories remain byte-identical on rejection. The [independent custody review](../custody-review/CODE_REVIEW.md) records the exact source and findings.

Alpha's `craft_rations` consumes **two berries plus one wood to produce two rations**. Output count is therefore not a substitute for the union of input claims. A transformed-lot/process contract must account for multiple heterogeneous sources, apportioned products, waste and conservation without multiplying export rights. Current `CountedSourceLot` cannot claim those mixtures are represented, and is not to be reused by silently assigning a fresh source ID to crafted output. Later immutable transformation provenance can compose these admitted counted inputs; existing branch-local transfers remain valid.

Still pending: source extraction/admission, actor legal ownership/control, complete inventory projection, equipment operations, reservation lifecycle, process graphs, heterogeneous material claims, mass/volume storage, source-cycle/reservoir accounting, cross-branch transit allowance and crash/retry durability. The full Wren content and all other original records/assets remain required migration scope.
