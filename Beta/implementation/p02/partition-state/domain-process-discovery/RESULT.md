# Integrated regrowth and reserved-transport discovery

Implemented immutable typed discovery consumed by the existing gathering and reservoir-transport
executors. **96 distinct affected cases pass, including11 new**, with no behavior-test failure. No
schema, historical clock, default game selection or ownership law changes. This is PLAN-STATE
preparation; it closes neither the general discovery/index contract nor P02.

## Actual behavior and boundaries

`GatheringSnapshot.DiscoverRegrowth` derives due node occurrences from its complete immutable source,
ordered by process interval and node identity. Each occurrence carries the real node revision, due
interval, unissued stock and the complete-cycle requirement at the strongest admitted tool power.
A due node can remain ineligible because finite stock is insufficient. Exact-step and idle-elapsed
recovery both consume the discovery through the existing Regrow operation, in original owner order;
actor effort still recovers even when no node is due. Inspection earns no time or new material.

`ReservoirTransportState.DiscoverDue` lists pending stages, including blocked stages, by due calendar
instant then stage identity at the current or next exact boundary. The existing planner consumes that
same selection; its continuation/history checks, hold handling, shared throughput, pressure, physical
custody, spawned identities and blocked outcomes are unchanged. Current-boundary discovery cannot
bypass the planner's idempotent no-advance rule. No fresh identity replaces a blocked obligation.

Both queries retain their complete source owner. IsFor uses reference identity, not hash equality,
historical authority or permission. Changed/canceled/decoded owners require fresh discovery. They
are not saved queues, independently paged owners or complete discovery membership for an unloaded
world. Their actual invocation through ordinary domain methods uses post-action owners.

New tests exercise an actual GatherPlanner harvest and cursor, shortage under stronger tools, empty
sets with effort recovery, enumeration order and elapsed-versus-step recovery, scheduling/cancellation,
reading holds, shared throughput with later completion, blocked-head reservation retention and exact
clock refusal. The existing profile9 fixture lawfully contains gathering and transport. Its new world
witness cancels a stage at the due boundary before transport while gathering and every retained resident
still earn exactly one interval. It does not pretend two separately authored domain examples are a
single saved journey with simultaneous harvested-node regrowth and water delivery.

[Source review](SOURCE_REVIEW.json) compares the transport body mechanically (only the selector changes)
and reviews the gathering predicate/order, clock guards and effort arithmetic. The only removed work
is computing a tool maximum for nodes that are not due; admitted owners require a matching tool, and
the bounded swing count/uint power product is representable. No invalid owner is newly admitted.
This is lead review, not independent review; orchestration remains paused.

## Verification and failure retention

| Invocation | Passing cases | Wall seconds | Scope |
| --- | ---: | ---: | --- |
| geometry-01 | 1 | 3.161 | Existing profile9 admits both process owners, exact clocks and full actor membership |
| core-01 | 84 | 4.228 | Gathering/discovery, transport/discovery/codec, pacing, golden bytes, stored construction and selected material-continuation dependencies |
| hosting-01 | 12 | 7.703 | New post-action world contrast, existing saved transport/reading hold/history/receipts and historical tamper rejection |

The geometry case overlaps Hosting; the distinct total is96. [VERIFICATION.json](VERIFICATION.json)
contains exact names and unrounded durations. Each managed process retains the180-second group bound;
no native or longer journey qualification was run and no deadline was extended. Final
[filters](preflight-04/filters.json) have nonzero discovery and [pins](preflight-04/pins.json) match
actual source/content/project/binaries. The retained admission preflight precedes behavior runs;
[EVIDENCE_DEPENDENCIES.json](EVIDENCE_DEPENDENCIES.json) explains the later test-only retention change,
and the full Hosting selection repeats that admission case on final binaries.

[FAILURES.json](FAILURES.json) retains two fixture compilation failures before behavior execution:
missing Custody/Worlds imports in a new Core test, then a Host test calling a Core-internal exposure
helper. Both failed sources and build logs remain. Corrections add imports and assert the public
exposure value plus the same1800ms interval for the full actor set. No production rule, expectation,
limit or assertion was weakened. Corrected preflights were deliberate; no automatic retry occurred.

The existing StorageFiles test fixture unconditionally deleted databases even after assertion failure.
The narrow optional `OPENFANTASY_STORAGE_ARTIFACTS` hook retains all case directories when supplied;
default temporary cleanup is unchanged. [RETENTION.md](RETENTION.md) documents the run/restore contract.
The qualification explicitly supplies a fresh root. [ARTIFACTS.json](ARTIFACTS.json) pins every retained
SQLite and source/history evidence file, including deliberately tampered databases. Preserve originals;
copy the complete case directory and sidecars before inspection/recovery. A deliberately tampered
case is expected to refuse, not an alternative playable save. The existing saved-transport witness
also emits original admission, exact batches and expected historical hashes.

No full-Core, general current40/41 partial-residency, new fault-injection matrix, simultaneous new
regrowth/delivery lineage, sustained scheduling/latency/scale, platform, independent or human pass
is claimed. Existing failure records and all required broader gates remain in force. The query
objects add allocations and retain complete owners; no performance benefit or residency reduction
is inferred from this behavioral qualification.

## Preservation, cadence and next work

[CHECKPOINT.json](CHECKPOINT.json) records `codex/checkpoint-before-domain-process-discovery-20261018`
at9abc2451ae6ab05242d380bdd9fe30f814d09ecc from74ae01c47610840377be7bbb9b694f30cea381d9. This is WIP
preservation, not acceptance. The accepted scoped commit creates
`codex/milestone-domain-process-discovery-20261018` without switching checkout. Concurrent steering,
all19 staged predecessor files and the full existing roadmap/closure table remain protected.

The [living process contract](../../../PLAN_STATE_PROCESS_DISCOVERY.md) now distinguishes this executed
binding from missing joined regrowth/delivery history, authenticated membership and partial-loading,
fairness, current-profile migration, projection/scale and independent review. The conditional storage
recommendation and all P02/NPC/story/ecology/art/audio dependencies remain unchanged; all181 frozen
requirements and production phases remain. Queries are consumed by actual operations, but are not
an excuse to declare generic scheduling complete.

Packet-wide API call count and average input context are unavailable from exposed tools. No explicit
fresh-session/compaction control is available for this automatic continuation; cached entry contracts
were retained. Shell wildcard/path misses and some oversized read outputs caused avoidable retrieval
friction; the two compilation failures also cost build work. Their measured records remain. The optional
retention hook required a final rebuild/discovery before storage execution. Heavy checks ran serially;
other applications/focus were untouched. No credit saving or efficiency percentage is claimed.

## Documentation review

- **Plan/spec:** updated the process-discovery contract, due-work map, conditional decision, handoff,
  appended roadmap and milestone entries. Preserve phase order, full P02 closure table and181 requirements;
  explicit remaining witnesses prevent this domain binding from becoming an unsupported phase claim.
- **Reference:** materials/activities, reservoir transport and scheduling references now describe the
  actual query types, consumed execution paths, immutable reference affinity, clock/stock/blocking
  semantics and complete-owner limitation. No progress dates or acceptance counts enter those pages.
- **Accuracy/completeness:** reviewed actual predicates/operation order, post-action world inputs,
  node cursor and effort, transport reservations/throughput, exact time guards and cleanup behavior.
  Tests prove named behavior and old saved-history rejection, not an authenticated persisted index or
  the complete game. Independent architectural/phase review remains unrun during owner pause.
- **Checks:** maintained documentation checker with this work record and [owned pages](OWNED_DOCS.json),
  scoped diff and source/binary-pin checks; [DOCUMENTATION_CHECKS.json](DOCUMENTATION_CHECKS.json)
  records terminal results. Code and documentation are accepted together only after those checks.
