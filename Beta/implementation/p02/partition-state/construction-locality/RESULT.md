# Sequential construction continuation with terrain detail absent

`ConstructionLocalityState` now retains the complete nonterrain owners of an admitted profile 5 locality
for one project's work interval. It accepts exact selected work batches, not caller-supplied advancing
flags, and prepares successive inventory/production/project, clock, gathering, resident and occupancy
candidates without reloading the source world each step. Its actual `ConstructionWorkKernel`, identity
reservation rules and exploration boundary are shared with ordinary execution. It has no independent
storage writer, host loop, new saved profile or implicit migration.

This directly advances the earlier one-candidate proof: **all 200 steps and final material completion
run after the source world, scene, identity index and terrain objects are collected**. The test creates
its complete-world oracle only after that interval finishes. Every recorded per-step nonterrain-owner
hash, receipt and identity set matches ordinary execution. Rehydrating exact original terrain then
produces the exact ordinary complete world bytes, including the installed reserved output and offcuts.

## Ownership and correctness boundaries

Capture retains actual inventory/production/construction, progression, quest, optional gathering,
residents, dialogues, actors and time, plus complete detached geometric/terrain/identity metadata.
It preserves source root hash, admission and revision. Profile5 has no care, delegated control,
encounter, liquid, structural-settlement or coordinated-history owners to omit. Any other profile
refuses explicitly; two new Hosting tests demonstrate refusal of actual profile 40 and 41 adventures
while their original bytes and additional owners remain intact.

One selected batch contains one work command for the captured project and no movement. Unsupported
actions require their proper execution path and new preparation; they are not silently ignored.
Preparation validates scope/profile/revision/step/hold, exact current geometry metadata, real control,
HP, funding/tool/stock revisions, source and historical identities. Work precedes the shared exploration
boundary, due gathering and every resident's exposure. A held selection earns no time/work; a refused
action in an already-selected advancing batch still contributes that one ordinary boundary.

Metadata changes require real re-preparation from current admitted owners. Missing or changed terrain
cannot be treated as empty; arrivals cannot vanish from complete required access. Completed material
and its occupancy reference change together in the private candidate. Repeated preparation on the
same immutable source is repeatable; stale batches and reused command identities cannot award another
step. A final historical identity collision preserves stock/work, and a new valid selection completes
once. Proposed receipts remain ineligible for delivery until the actual authority publishes them.

`Rehydrate` validates every exact terrain reference and uses normal complete `WorldSnapshot` admission.
Encoding/storage bounds and durable source ancestry remain separate publication requirements. The
continuation is not a new admitted persisted world, and its source hash is not a replacement for a
canonical owner directory or commit history. Current metadata and historical lookup must come from
and serialize with the real owner; passing stale metadata again cannot establish freshness.

## Verification

The final affected Core run passes **767** cases in **29.494 s** including build: Construction, Worlds,
Gathering, Encounters, Exposure, Spatial and Structures. The Hosting selection passes **26** cases in
**37.837 s** including build: source-profile refusal, preview and final construction recovery, actual
carriage, complete-root/current-adventure measurements and collective withdrawal world behavior.
There are **793 distinct passes, 11 new cases**, with no skips. The earlier eight-case focused pass
in 12.811 s overlaps and is not added again. All commands retain the 180-second bound, owned process
groups and observed terminal outcomes. Builds report no warnings/errors. No failed managed run occurred
in this increment; earlier full-Core timeouts and native reliability failures remain open.

New tests additionally cover current control/tool refusal, held and unfunded work, exact reconstruction
refusals, stale batches, arrival/terrain invalidation and actual re-preparation. A genuine harvesting
sequence depletes a resource through ordinary commands; idle selected steps reach just before its due
regrowth, and the continuation crosses that boundary while construction proceeds. Every compared
complete state preserves the source cursor and exact regrowth step. No fixture grants regrowth or
replaces earned time with a counter adjustment.

The ordinary processor change is limited to extracting its existing reservation book and exploration
boundary. [EXTRACTION_CHECK.json](EXTRACTION_CHECK.json) records an exact transformed-source comparison;
shared helper bodies were also reviewed for ordering, exception/overflow and object-lifetime semantics.
Legacy golden/ordinary admission tests remain included. No independent critic ran under the owner's
orchestration pause; these are lead review and executable evidence, not independent acceptance.

## Measured diagnostic and remaining work

The terrain-free interval retains two actors, one terrain reference and four occupancy pages. Complete
identity metadata grows from 313 to 917 entries, accounting for 600 step cause/command/outcome identities
and four final material/structure identities. The weak-reference assertions run before and after all
200 steps. No original terrain/scene/world/index reference survives capture, and no terrain reader or
callback is retained by the continuation.

On the Apple M2/macOS arm64/.NET 8 host, the one sequential sample allocates 8,652,856 managed bytes in
66.841 ms for continuation preparation versus 17,291,288 bytes in 96.997 ms for ordinary decisions.
Continuation runs first; measurements exclude capture, assertion hashing and final reconstruction.
These numbers are diagnostic only: they omit publication, I/O, peak residency and concurrency and are
not a qualified performance improvement. Complete metadata and all nonterrain owners remain resident;
per-resident exposure still changes explicitly. The timing-domain exposure-basis comparison is open.

This supplies a substantive legacy whole-locality execution experiment, not the entire PLAN-STATE
acceptance episode. **Still required:** stable per-actor/custodian owner boundaries or an evidence-backed
whole-locality decision; actual movement with cargo and cross-owner transfer; later-profile coverage
without facet loss; canonical dependency/owner publication in the single SQLite authority; interrupted
partial publication, original-history recovery and integrated cost measurements. No numeric save profile,
schema, codec or production migration is selected. Permanent NPC persistence, current expedition
partial residency, independent review and full P00–P14/platform/human/scale gates remain open.

## Preservation

Checkpoint `codex/checkpoint-before-construction-locality-20261017` resolves to
`8c0710dd9b66111840e3466c80a4bd4046cbdb7d`, parent `3064d3d6`, preserving concurrent language documents
as WIP recovery only. Accepted marker: `codex/milestone-construction-locality-20261017`. Working branch
remains `codex/runtime-foundation`. No unrelated predecessor staging, solution edit, language assessment,
original research input, frozen specification or prior evidence is included or altered.

## Documentation review

- **Plan/spec:** Roadmap, handoff, comparison and prototype sequence now record sequential terrain-free profile 5 work without upgrading it to current-profile or durable partition acceptance. Joined movement/gift/publication, timing-basis measurement and original campaign gates remain explicit.
- **Reference:** New construction-locality reference documents complete retained owners, supported inputs, shared work/identity/clock rules, due processes, metadata freshness, reconstruction and unpublished outcomes. Architecture index, construction execution, pacing and persistence cross-reference the boundary with timeless contracts.
- **Accuracy/completeness:** Lead traced every profile 5 source owner and unsupported-profile guard, shared ordinary reservation/time extraction, closure/object retention, reconstruction and complete-world checks. Actual missing/stale dependencies and higher-profile refusal prevent silent omission. Full-world encoding, source authority, authentication, historical lookup and durable publication are not supplied by the domain state.
- **Checks:** VERIFICATION.json pins current source/binaries, command/TRX results, extraction comparison, protected inputs, documentation links/fields and whitespace. Mechanical checks supplement source-based review and do not constitute independent, rendered or human acceptance.
