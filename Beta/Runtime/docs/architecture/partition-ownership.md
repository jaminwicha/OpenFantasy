# Typed ownership and admission cohorts

## Design contract and runtime boundary

A canonical owner is responsible for a fact's identity, revision and durable representation. An
**admission cohort** is the complete set of owners and physical evidence needed to validate a specific
action. A cohort is a read dependency set, not another persistent copy of those facts. Residency is
whether those owners' detail is loaded; it neither confers control nor awards time.

These contracts describe the partition extension to the portable C# authority. They do not imply an
implemented typed-owner database. The concrete `WorldSnapshot` remains a complete admitted locality.
The internal profile-five terrain-separated binding retains a single nonterrain locality image,
complete occupancy metadata and complete original-history replay. See
[construction locality](construction-locality.md#explicit-terrain-separated-storage-binding) and
[persistence and history](persistence-and-history.md#design-contract-partitioned-persistence-and-version-ownership)
for its narrower implemented boundary.

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
