# Composed local-world authority and fixed-step input

9 September 2026. Independent design review by `p00_core`; no runtime changes or builds. This recommendation extends the existing durable counted authority into the first walk/talk/hand-over loop. It retains one portable Core and one in-process Hosting owner, shared by CLI and Godot.

Historical recommendation retained for review. The implemented [world contract](../WORLD_CONTRACT.md) and [increment record](../README.md) supersede proposals below where they differ: movement remains leased pre-admission input, action order is bounded and canonical by actor, missed host deadlines are discarded, and giving items grants no quest credit in this increment. No recommendation here establishes an implemented or verified behavior.

## Recommended retained boundary

Introduce a specifically named **local-world profile v2**, with a strict canonical state codec and a concrete local-world coordinator. Compose the existing counted authority state with admitted terrain, actor form/condition and presence, the branch clock/step state, bounded movement control state, and the actual interaction progress required by this loop. The coordinator prepares one complete immutable candidate; the shared SQLite owner commits it. It must not open a nested `SqliteCountedAuthority` beside another world service.

Keep the v1 counted APIs, encoding bytes, exact schema admission and tests operational for v1 files. A profile-aware opener selects one compatible service for an opened world. The exclusive ownership boundary prevents either profile or another process from simultaneously owning that file. Share concrete connection/transaction mechanics where they are the same; do not duplicate a second persistence engine or introduce a generic event bus accepting arbitrary payload objects.

The v2 state must validate joins, not merely decode each component separately:

- Counted owners/control actors resolve to the composed world's admitted actors. Each required physical actor has a compatible form binding, definition pin, condition and authoritative presence; it is not enough to serialize parallel arrays with matching counts.
- World/branch/realm identities and terrain scope agree. Form/physical-body/presence references resolve to the correct actor, and identities remain disjoint across incompatible roles. Existing body and terrain admission do not independently prove these world-wide relationships.
- Presence uses canonical integer subcell coordinates and the admitted terrain/body dimensions. Realm is separate from height. All movement candidates use the same clearance/support/occupancy rules for player and NPC, with no authoritative Godot float positions.
- Actor, presence, body, custody and terrain revisions have explicit owners. A global step changing the clock must not make every unchanged inventory revision stale. The old counted `CustodySnapshot.Revision` is not automatically the composed world's commit sequence or step ordinal.
- Store actual interaction completion/commitment facts needed by the loop. A transient UI flag saying the NPC thanked the player cannot prevent repeated hand-over credit after restart.

Immutable terrain/body definitions should be pinned and retained once. Canonical mutable state can refer to those accepted bytes by hash. No admitted definition may disappear merely because the visible scene unloads. Existing frozen v1 encodings can be nested or referenced through a typed versioned component, without changing their bytes.

## Explicit profile and schema evolution

Do not raise `user_version` while silently changing the meaning of old journal blobs. Store and verify the selected profile, schema version and required codec/rules/package pins under exclusive ownership. Unknown combinations fail closed. The v1 opener must reject v2 files; the v2 opener must not treat v1 counted state as a world with invented bodies, a guessed date or a default terrain.

The smallest honest initial implementation is `CreateNewLocalWorld` from explicit, validated admission plus unchanged v1 operation. If a v1-to-v2 upgrade is supplied, make it an explicit migration operation:

1. Acquire the source's exclusive ownership and validate/replay the complete v1 world. Produce a consistent SQLite backup/copy; do not copy an active main file independently of its WAL.
2. The migration input supplies the new body/presence/terrain bindings, rules pins and calendar anchor. A counted commit number does not imply a historical date. Preserve the same world/branch and source/actor/item identities unless an explicit recorded mapping is necessary.
3. Preserve the v1 genesis, exact command/receipt bytes, commit prefix and historical occupied identities. Record the original head hash and migration version/input as provenance. Build v2 admission from the actual recovered counted head, not the original sample genesis.
4. Start the new world commit sequence after the preserved prefix; record a typed migration commit. Never recalculate old receipt IDs under a new codec or allow their command IDs to be reused by v2.
5. Validate/replay the copy, including legacy exact retries and the new composed head. Select it atomically only after successful migration. A failed migration leaves the old world usable and intact. Retain the versioned legacy interpreter for its prefix.

Until that operation is real, report upgrade as unsupported. This preserves working v1 code/data without pretending that a fresh v2 sample is a migration.

## Typed commands, receipts and one commit boundary

Use a closed set of concrete v2 intent kinds, initially movement control, talk to a known actor/topic, and handing over an actual counted lot. Do not serialize arbitrary method names, callbacks, `object`, dictionaries of ad hoc fields or a second request language. Shared envelope identity fields may be factored internally only while the existing v1 golden bytes remain identical.

Keep retry uniqueness **world + principal + command ID**, across protocol versions and branches. Neither version, session, intent kind nor actor is an extra key that permits reuse. Exact original canonical bytes distinguish an identical retry from conflicting content. A current delivery context is separate from immutable origin-session bytes. Authorize receipt access before revealing an existing outcome. Do not rerun a completed hand-over because its old expected revision is now stale.

A world step can finalize **zero to many** commands. It therefore needs a typed step/administrative commit record plus per-command outcomes, rather than forcing all work into the v1 one-command/one-receipt row:

| Record | Responsibility |
|---|---|
| World genesis/head and accepted definition blobs | Current composed continuation and pinned immutable content |
| Typed world commit journal | Stable commit sequence, cause kind, step ordinal where applicable, exact admitted inputs/external decisions, replay/result witnesses, before/after state hashes |
| Per-command outcomes | Exact original bytes and typed applied/rejected receipt keyed by principal/command ID; references one actual world commit |
| Occupied identities | Command, result, actor/form/body, source, item, interaction and internal causal identities retained across history |
| Optional durable pending-input table | Only required when the host promises durable admission before the execution step |

An internal step cause has its own typed identity and ordinal. It is not a client-authored `AdvanceTime` command or an invented player principal. The receipt can identify the one world commit and its own command result; no public outcome should attach an unrestricted world snapshot/hash or hidden rejection details.

For a hand-over, validate control, known target/reach/realm, relevant current body/presence/terrain eligibility, actual source access/receiving policy and counted revisions. Atomically commit movement/clock continuation, the actual lot transfer, the specific interaction fact/credit and its receipt. A conversation can inspect that committed fact; it must not infer completion from a client animation or award credit each time the same dialogue opens. Rejections do not consume an extra step, create goods or advance an independent interaction clock.

## Minimal safe ingress: stage first, acknowledge at commit

For the first embedded loop, prefer a bounded in-memory **pre-admission** input queue. Submission returns a task awaiting a durable outcome. It must not return a durable Accepted receipt while the input exists only in memory. At the host cutoff, assign and persist the accepted ingress order inside the same transaction that commits the step and all its outcomes. This avoids a second transaction for every input while meeting the durable-order requirement.

Exact duplicates already committed can immediately return their receipt without advancing time. Exact duplicates staged for the same command ID share the pending outcome; different bytes conflict. If the host crashes before admission, no receipt promised acceptance and the caller may resubmit the same bytes/ID. Once accepted, a command must either complete or remain durably pending; silently dropping it is not allowed.

The pre-admission queue has explicit per-controller/world count and byte limits. A full queue rejects or applies backpressure before admission. Do not return a fake committed gameplay result for queue saturation. If later transport requires an acknowledged queued state, add the real durable inbox and a separate admission ticket; that ticket is not an Applied command receipt.

## Host-owned 20 Hz progression

One host-owned loop determines eligible 50 ms steps. Rendering callbacks and command submission never supply authoritative elapsed time, step counts, movement distance or world dates. A headless test driver can supply explicit steps through a trusted driver boundary; that capability is not a player command.

At each step:

1. Take a fixed bounded cutoff of staged inputs under the host owner. Use persisted ingress order, with command ID as the declared tie-breaker. Later arrivals belong to a later cutoff.
2. Process due transitions/expiry, accepted commands, then observations/planning/follow-ups in the existing scheduling priority contract. Define movement's exact place within this sequence so talk/reach validation observes a consistent position, and retain that choice in replay.
3. Apply at most **one movement integration per actor for this step**, using its valid quantized movement control. Multiple control changes can alter the selected direction according to the declared input policy, but cannot cause multiple displacements. Define finite interaction capacity/cost separately; repeated button events cannot perform unbounded talks or hand-overs in one tick.
4. Increment the authoritative step ordinal once and advance the exploration calendar by the existing **1,800 world milliseconds** for one advancing exploration step. Retain exact movement/exposure remainders where the speed/rate requires them. Journal the whole candidate and outcomes atomically, then publish.

The simplest native control samples a quantized held direction and emits changes, including neutral on release; it does not emit a movement action on every rendered frame. Staging may coalesce raw, unacknowledged controller samples. It may not erase already accepted commands without an explicit recorded outcome. Diagonal motion must obey the same speed budget; accepting two axes cannot provide two full cardinal moves.

Count commit sequence, step ordinal, calendar and local exposure separately. Command-only admission/control/rejection commits need not advance a simulation step. An active empty step still advances appropriate world time; conversely 1,000 rejected input attempts between two steps must not advance it 1,000 times.

The host uses an elapsed-time accumulator or monotonic deadlines to schedule work, not to decide gameplay outcomes. A slow renderer can interpolate less often without changing the accepted step trace. A slow host retains or explicitly pauses unprocessed work; it does not merge many missed steps into a large collision-skipping delta or silently grant/drop simulated time. Bound catch-up work per host turn and expose backlog. On pause, no-ready-player state or restart, offline wall time is not added to the simulation backlog.

Persist movement continuation, but require fresh controller readiness/input before resuming a restarted world. A saved held direction must not walk a character away before control is re-established. Neutralization/rebinding that changes authoritative continuation needs an explicit host transition or admitted input, not a hidden scene-load edit. Do not reconstruct movement or exposure by subtracting the old save timestamp from the current wall clock.

## Storage growth needs a change before sustained walking

At 20 Hz there are 72,000 steps/hour. A full 16 KiB after-snapshot in every journal row retains about **1.18 GB/hour**, before database overhead; a 1.5 MB snapshot retains **108 GB/hour**. Those are arithmetic illustrations, not measured sizes for the proposed state. Repeating immutable terrain and body definitions makes this worse. The counted prototype's full-after-state-per-command strategy should not be carried unchanged into continuous walking.

Retain compact typed step inputs, external decisions and deterministic result/delta witnesses, with current mutable head/components stored once and periodic canonical checkpoints. Keep every consequential historical input; do not compact by deleting old lived intervals. The TDD's initial checkpoint policy is five simulated minutes or 10,000 commits, whichever occurs first. Name the exact clock used for that simulated-minute threshold; it is not automatically five wall-clock minutes at the accelerated calendar rate.

Measure actual bytes per step/command/checkpoint, WAL/checkpoint behavior, retained bytes per active/idle hour, fsync/step p50/p95/p99, allocation/GC, reopen/replay time and queue age. Rewriting the full current head each tick can still impose substantial write traffic even if the archive is compact; pin immutable blobs and update only the necessary current components when evidence warrants it. A replay test over a handful of steps does not qualify long-world storage or performance.

## Must-test boundaries for this increment

| Test | Required observation |
|---|---|
| Same accepted input trace under 30/60/144 Hz presentation | Same fixed-step positions, clock, custody and interaction outcomes |
| Duplicate/spammed move/talk/hand-over inputs | No extra movement integration, time, goods or repeated completion credit; exact retries return original outcomes |
| Opposite/diagonal controls within one cutoff | Declared deterministic selection and speed; no frame-rate bonus |
| Empty active steps versus paused/unready/offline intervals | Only eligible steps advance calendar/exposure; no restart catch-up |
| Restart with a saved held direction | No uncontrolled movement before fresh readiness/input; deterministic continuation afterwards |
| Move near/away from a target at the interaction boundary | Reach/realm/body/occupancy validation uses the declared authoritative position and order |
| One step containing multiple actions and a transfer | Crash yields all committed clock/presence/custody/interaction/results together, or none |
| Lost acknowledgement and exact retry after restart | Same receipt and one completion/transfer; no replayed reward |
| Corrupt/cross-profile state or incomplete component joins | Open/admission rejects before publishing or enabling gameplay |
| Explicit migration, if implemented | Old counted bytes/receipts/IDs remain exact, current counted head is retained, failure leaves source usable |
| Long retained active and idle traces | Bounded per-step work, measured archive/write growth and deterministic checkpoint replay |
| Native Godot input and visuals | Submitted intents and committed projections drive actual walking/talking/hand-over; renderer state is never authoritative |

A polished native loop is the next gameplay proof. It does not by itself close full NPC depth, transport/reconnect, arbitrary history, terrain mutation/fluids, full combat Wait, original content realization or all production export targets. Its modules and stored facts must remain valid foundations for those later features.
