# Typed ownership and admission cohorts

## Design contract and runtime boundary

A canonical owner is responsible for a fact's identity, revision and durable representation. An
**admission cohort** is the complete set of owners and physical evidence needed to validate a specific
action. A cohort is a read dependency set, not another persistent copy of those facts. Residency is
whether those owners' detail is loaded; it neither confers control nor awards time.

These contracts describe the partition extension to the portable C# authority. The bounded typed-image
storage binding below implements durable owner separation for one legacy interpretation; general
owner loading and scheduling remain separate contracts. `WorldSnapshot` is still a complete admitted locality.
The internal profile-five schema13 binding retains a single nonterrain locality image,
complete occupancy metadata and complete original-history replay. See
[construction locality](construction-locality.md#explicit-terrain-separated-storage-binding) and
[persistence and history](persistence-and-history.md#design-contract-partitioned-persistence-and-version-ownership)
for its narrower implemented boundary.

The [dependency reference](partition-dependencies.md) distinguishes current facts, immutable definitions,
derived views and historical witnesses, including control, coordinated archives and due-process closure.

## Stable owners and spatial membership

Use typed aggregates that follow coupled gameplay facts, rather than one root per field or one root
for every person in a region. Owner keys contain the world, historical branch, owner kind and stable
subject identity. An actor's current realm, cell and storage residency are location/index facts;
walking, sailing or crossing a planar boundary cannot mint a new person or inventory identity. Terrain
addresses include their physical realm and bounded extent. Storage remapping has its own revision
and must not impersonate a gameplay ownership transfer.

| Aggregate responsibility | Canonical facts | Referenced facts |
| --- | --- | --- |
| Actor | Body/presence, personal condition, resident metadata, private knowledge, motives and commitments under their actual typed laws | Inventory custodian, control, participation in shared interactions, definitions, timing membership |
| Custodian/material store | Storage capacity/access rules, exact held lots and their revisions, custody and reservation relations | Carrier/institution, immutable origin/transformation witnesses, destination policies |
| Project/assembly | Funding/progress, construction definition and site, installed backing, physical members and shared work obligations | Builder, stock/tools, terrain/support, access population and producer history |
| Place/process | Terrain changes, liquid/physical/ecological state grouped by actual coupling, boundary obligations | Geography ancestry, adjacent processes, affected bodies and structures |
| Interaction/encounter | Genuinely shared conversation, encounter, care/exchange proposal or coordinated continuation | Participants, permissions, exact reserved resources and original commands |
| Timing domain | Earned ordinal/calendar interpretation, holds, membership epochs and ordered due obligations | Actors, encounters and processes; a clock does not own their bodies or cargo |
| Definitions/history | Immutable rule/content/provenance witnesses and original historical interpreters | Named owners, admissions, branches and archive lineage |

Player and NPC actors share these ownership and interaction laws; changing a controller does not
replace the person or erase their history.

The table describes responsibilities, not a universal optional-component container. A concrete
aggregate may keep closely coupled facets together. A later split requires evidence that validation,
updates and recovery remain complete. For example, a person's medical state and bodily actions are
not independent merely because both can be serialized separately. Trade moves real holdings between
custodians atomically; carried movement changes physical membership without manufacturing a custody
transaction. Installed output retains its material provenance and reservation link.

The canonical ownership directory locates an exact owner revision. Spatial pages index actual body
coverage and possible contacts; they cannot replace canonical custody or identity history. Directory
lookup must establish both membership and relevant absence at the selected head. Stable keys alone
do not prove directory completeness, authorization or absence of a retired historical identity.
Bounded directory pages and historical roots are required; loading a global manifest recreates the
complete-world bottleneck.

## Cohort admission and one execution kernel

A host-selected action first determines its dependency closure. Each read identifies its owner,
revision, interpreter and why it is needed. Physical queries additionally bind terrain references,
occupancy/page revisions, affected bodies, support and access-population completeness. Shared immutable
pins may be reused; owner facts may not be replaced by a caller's success/permission flags.

The admission boundary constructs a typed, immutable execution context only after those dependencies
are present and valid. Ordinary complete-world and partition adapters use the same domain operations.
A storage image, cached route or a speculative kernel result cannot independently authorize mutation.
If an existing rule inspects the complete locality, the cohort must supply that complete locality
until a separately qualified rule/dependency decomposition exists. Selecting convenient nearby actors,
ignoring crowded cells or raising an admission cap is not partitioning.

Changed body/control, arrivals, altered terrain/support, stock/tool revisions or expired interpreter
pins invalidate the relevant context. Missing detail requests bounded preparation or deferral before
work is earned. A failed preparation does not consume a command, release a receipt or silently retry
an actor action. The one serialized authority rechecks the exact read revisions and absence claims at
publication. Background readers and spatial projections do not become competing writers.

## Earned time and due obligations

Physical visibility, residency, AI decision cadence and timing-domain membership are distinct. An
unloaded farmer still has an actual commitment and due obligations; an on-screen actor can be held by
an encounter or reading interaction. Offscreen execution interprets the same accepted actions and
passive laws rather than awarding an estimated outcome.

Due discovery belongs to typed process/domain owners. Selection needs a deterministic order within
its timing domain, using due ordinal, explicit causal precedence and stable identity as appropriate
to that law. Simultaneous obligations whose effects interact require the same admitted boundary;
a priority queue cannot redefine their precedence. Queue entries are references, not copies of work
progress or body state. Consuming an obligation and scheduling its successor commit with the gameplay
changes that justify them.

Budgets limit preparation and execution, not elapsed gameplay consequences. A domain cannot advance
past mandatory due work while pretending every participant experienced a complete boundary. When
admission needs more detail, retain a truthful obligation and its age instead of discarding it.
Oldest due-work age is measured in that obligation's own domain units; subtracting unrelated encounter
and exploration ordinals is invalid. Scheduling fairness and bounded lateness require explicit policy
and workload qualification, not a wall-clock polling interval.

A common exposure basis can represent earned experience exactly only for members sharing that law
and uninterrupted interval. Joining/leaving, personal-rate changes and transitions between timing
domains close and rebase the corresponding segment. Stored representation changes require an explicit
format contract and exact reconstruction; a changed clock law also requires its historical semantic
interpreter. Neither may silently reinterpret saved resident exposure.

## Atomic publication and reconstruction

One SQLite transaction publishes the complete coupled write set: new owner revisions, directory and
spatial membership, due obligations, identity reservations, original accepted input, outcomes and head.
There is no second writer or IPC coordinator. Immutable archive payloads must be available and verified
before a committed reference can become visible. Publication faults recover the complete old or new
boundary; a speculative actor, inventory credit or installation never escapes by itself.

An owner revision, actor/body revision, spatial page revision, timing ordinal, global commit sequence,
format version and semantic interpreter version are distinct. They must not share a counter merely
to simplify a manifest. A single actor movement need not rewrite unrelated inventory or personal
knowledge, while its occupancy and physical presence must agree at the same committed boundary.

A storage envelope may name exact typed facet versions and hashes. Canonical ordering, required
presence, supported combinations, bounded sizes and cross-owner references are admission obligations.
Missing, duplicate, contradictory or unsupported facets refuse. Facet-local validation supplements
composition admission; it does not replace it or make arbitrary combinations legal.

Historical reads resolve the directory and owner revisions at the requested branch/commit using the
original interpreters and archive ancestry. Complete replay remains necessary until a separately
qualified checkpoint/dependency proof can authenticate a smaller read. A matching payload hash alone
does not authenticate a world history. Preserve original genesis, prior codecs, receipt identity,
pending commands and reserved future consequences across an explicit migration. No later-profile
facet can be omitted merely because an earlier fixture lacks it.

## Extension boundary for persistent character depth

Character motives, knowledge and commitments belong to real typed actor/interaction owners and retain
source, visibility, resources and chronology. They must not be keyed to the currently loaded tile or
stored in an unversioned AI sidecar. Temporary execution views may reference those facts without
owning another copy. A permanent persistence model requires complete ownership/dependency coverage,
original-history migration/recovery and a qualified offscreen scheduling policy. A generic dictionary
or a coarse terrain-separated image alone does not establish those contracts.

The [PLAN-STATE exit decision](../../../implementation/PLAN_STATE_EXIT_DECISION.md) binds the first such model:
- **Owner and key:** every person-actor owns one cognition record keyed by world, branch, role `cognition` and
  actor. Control changes never create, reset or delete it.
- **Contents:** acquired evidence, the current unaccepted intention, decision records with reasons and policy
  version, memory with evidence links, and a pending reconsideration wake. Beliefs are derived from evidence.
- **References:** accepted commitments, promises and existing knowledge such as price knowledge stay with their
  owners and are referenced, not copied.
- **Laws and decisions:** evidence and belief derivation are interpretation laws; plan selection enters state
  only through recorded commands, and replay never runs the planner.
- **Bounds:** overflow rolls back the boundary and stops the authority; it never rejects a gameplay action.
- **Versioning:** the facet enters only through profile 42, a strict successor of 41, as a canonical sequence of
  per-actor records so a later storage-only conversion keeps its meaning.

The profile-42 root binds these rules in the existing complete root under the single SQLite authority. Its laws are
described in the [actor reference](actors-and-society.md#runtime-binding-cognition-interpretation-profile-42). The
cognition section is still one root field. No typed-owner image, partial load or non-resident reconciliation of
cognition exists yet.

## Runtime binding: legacy typed-owner storage

`SqliteWorldAuthority.CreateWithTypedOwners` admits a fresh profile-five world into explicit storage
schema14. Schema14 is not gameplay profile14; normal game creation and existing saves keep their
named formats. The binding preserves full original genesis, immutable terrain and the ordinary
profile-five interpreter. It cannot import later profiles by dropping their owners.

[ConstructionOwnerDirectory](../../src/OpenFantasy.Core/Construction/Ownership/ConstructionOwnerDirectory.cs)
contains the complete bounded current directory. Its envelope binds world/branch/realm, global commit
revision and exact terrain references; entries map typed role plus stable subject identity to an
immutable payload hash. The same actor/custodian key survives movement and work. An owner hash is a
storage revision; the existing actor, storage, project and global commit counters retain their own laws.

| Typed image | Encoded facts |
| --- | --- |
| Inventory basis | Original admission pin, finite capacity, admitted actor/reservation identities and counted-source definitions/capacities |
| Inventory ledger | Exact global custody revision |
| Timing | Calendar and earned advancing ordinal |
| Actor | Physical realm, exact body/form/condition/presence/home binding and all resident metadata/exposure for that actor |
| Body definition | Shared immutable body definition |
| Control | Exact principal/session binding for its actor |
| Custodian | Storage/revision, owner/deposit policy, counted lots and transformed lots, including source/output/protection/reservation facts |
| Recipe / production batch | Exact immutable recipe or batch/provenance record |
| Production history | Global production revision and exact recipe/batch membership |
| Construction definition | Shared immutable blueprint, tool/access and material rules |
| Construction catalog | Global construction revision and definition membership |
| Project | Funding/builder/owner/quest/site/work/access/progress/stock-location and installed output binding |
| Gathering / progression / quests / dialogues | Complete existing typed facet encodings; these facets retain their shared aggregate scope |

World/branch identity scopes every payload. A realm is physical membership for an actor; it does not
become part of the stable actor key. Lot records belong to exactly one custodian image; production
history references their immutable producer ancestry rather than owning another mutable holding.
Derived installed geometry remains reconstructible from the admitted construction definition and
installed backing. Unsupported consumption, continuation or richer material-history interpretations
refuse instead of becoming empty facets.

The directory and payloads own immutable encoded bytes and export defensive copies. Capture accepts
admitted `WorldSnapshot` or `ConstructionLocalityState`. The separate
[ConstructionOwnerReconstruction](../../src/OpenFantasy.Core/Construction/Ownership/ConstructionOwnerReconstruction.cs)
decoder consumes stored directory bytes, a selected directory hash and owner/terrain readers. It
reconstructs the complete admission cohort; it does not authenticate history by itself. The authority can retain either a complete nonterrain locality or the separately admitted
[selected work interval](construction-locality.md#selected-stored-owner-publication). Shared progression/
quest/process facets are not independently resident actor roots merely because payloads have stable keys.

`WorldStateImage` selects complete-root, terrain-separated-image or typed-directory storage explicitly.
Head and checkpoints hold directory bytes for schema14; `world_owners` stores immutable payloads keyed
by hash. The journal's before/after digests bind directory images. Full-world content hashes retain their
existing meaning. The ordinary preparation path still checks the full16MiB canonical envelope; evicted
work retains the exact flat-image/full-world length relation and complete final physical admission.

`PublishWrites` writes the accepted journal record, owner payloads, identities, outcomes, head and any
due checkpoint in one SQLite transaction. Existing identical payloads are verified byte-for-byte and
reused; only new hashes add archive rows. Complete-world/locality preparation encodes and visits every current owner. The selected stored
interval instead loads its work and complete due cohort and supplies only changed payloads to the
same writer; it still rebuilds the complete directory. Archive accounting also scans retained row lengths
after publication preparation; its cost must be included in measurements. The complete directory advances atomically with a gift,
work or installation; a fault cannot leave only one custodian credited or release an uncommitted receipt.

`WorldOwnerArchiveReplay` verifies every unique historical payload against ordinary original-history
replay and then checks exact archive count/byte membership. Missing, changed and extra historical
payloads refuse readiness. After the stored head matches replay and the command/identity tables pass,
`WorldDatabase.ReconstructOwners` loads that head's actual payloads and exact terrain. Recovery compares
the resulting complete world bytes with ordinary replay and returns the reconstructed world. Original
full replay remains mandatory; no implicit migration, history truncation or alternative simulator exists.

The decoder bounds the directory before reading payloads and requires its exact selected hash. Entries
have supported roles and strictly increasing unique role/subject keys. Each payload must match its
content hash, world/branch, subject, role, version and exact field count within individual and aggregate
byte bounds. Actor realm and loaded terrain scope/bounds/revision/hash must match the directory.
Custodian holdings are combined in canonical lot order; existing custody/authority, production,
construction and spatial codecs validate their domain data. The ordinary world constructor admits
cross-owner relationships and derived installed structures, and the complete canonical encoding keeps
the existing world byte limit. Recapturing the admitted world must reproduce the exact input directory:
this also rejects omitted, extra, mislabeled or contradictory memberships and noncanonical payloads.
Structural validity or a caller-supplied self-consistent hash grants no command or historical authority.
The binding reads every required owner and terrain region, not an independently paged subset.

The experimental envelope permits at most1MiB of directory metadata,16MiB per payload and32MiB of
current payloads, while retaining the separate full-world16MiB admission check. Retained owner archives
are bounded by100,000 unique payloads and256MiB. Exceeding the write envelope rolls back publication
and stops the authority for recovery; exceeding it on read refuses readiness. These are explicit
finite development-storage limits, not shipping population/world-size targets. Noninitial checkpoints, general selective loading beyond the admitted construction interval,
current-profile migration, due-work fairness and frontier-scale history remain separate obligations. Complete-cohort decoding does not establish a
partial-admission proof or a cold-load/performance budget.

### Selected qualification reads

The [stored construction cohort](construction-locality.md#selected-stored-owner-qualification) binds
selected payload hashes to a complete admitted source and current directory. It can reconstruct work
qualification after unrelated custody/production and terrain detail is released, while retaining the
site's complete actor/structure geometry and source-established reservation membership. The read-only
Host binding serializes freshness and reads; no cached source survives a changed directory implicitly.
This does not change schema14 publication, full original-history recovery, actual due obligations or
final completion admission. Qualification's smaller read set is not the entire simulation step's
required residency. Capture cost, complete geometry witness and shared-facet read granularity remain
part of any future memory/I/O measurement.

### Stored interval and due owners

The selected construction publication binding extends the union of captured work cohorts with timing, gathering,
construction catalog and all actor payloads. The source-bound reservation and geometry witnesses
remain valid only across its own partial-work transitions. Changed project/catalog, timing,
gathering and resident images replace their references atomically; unrelated custody/production
payloads are not loaded or rewritten by that preparation. All resident exposure is still materialized.
Final completion and intervening actions restore complete admission. The complete directory and
identity metadata remain resident, and recovery retains full history authentication. No general
paging, absent-history trust or offscreen scheduling policy follows from this one interval.

Dependency reads may be sliced without publishing the boundary. All selected action receipts,
changed project/catalog owners and mandatory passive effects still publish together. The selected
project's revision remains distinct from the whole catalog's revision; canonical project decoding
does not fabricate a complete catalog from a subset. Missing or stale owner facts cannot be skipped
to finish a read budget, and a preparation cursor is not persisted timing-domain membership.


The Host can retain an authority-affine [publication handle](../../src/OpenFantasy.Hosting/Worlds/StoredConstructionPublication.cs)
between owner-read calls. A handle is private preparation metadata, not another canonical owner or a
saved obligation. It binds the exact selected batch to one captured interval and head; an intervening
commit or recapture invalidates it. Read slices release the gate without publishing any owner, time or
receipt. Final publication reacquires the same sole-writer gate and commits the complete coupled
boundary. Cancellation and storage-read failure retire that handle; a storage-read failure also fences
the authority. Foreign handles cannot be acted on. Restart retains only committed world history,
requiring fresh preparation; no persisted discovery frontier or timing membership is implied.

The optional internal profile-five [local-session consumer](due-work-and-scheduling.md#runtime-binding-stored-construction)
owns one unpublished handle across serialized ticks. It does not create another fact owner, writer or
clock. The previous view remains stable during dependency reads, and all committed owners are restored
for the next coherent view. New input remains outside that selected boundary. Cancellation between
slices or an intervening source change discards only preparation, not earned world state. This binding
retains complete source capture and projection; independently resident live views require their own
admission and knowledge contracts.
