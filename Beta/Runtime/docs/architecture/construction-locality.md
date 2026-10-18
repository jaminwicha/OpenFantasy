# Construction locality continuation

## Domain binding and supported interpretation

[ConstructionLocalityState](../../src/OpenFantasy.Core/Construction/ConstructionLocalityState.cs) retains
all nonterrain owners of a complete admitted profile-five world during one project's selected work
interval. Capture requires inline production, terrain-only actor/structure support, an existing project
and actual quest/progression owners. It pins the original canonical world hash, revision and admission.
Other source profiles refuse; the type cannot import a later world by dropping its additional owners.
It selects no new saved profile, serializer, database schema or migration.

The state retains actual inventory/production/construction, progression, quests, optional gathering,
resident metadata with an exact earned-exposure basis, dialogues, actor bodies/presence, calendar and
advancing/commit counters. Reading `Residents` materializes the actual experienced durations; inventory
custodian lookup uses unchanged resident metadata without constructing exposure-updated copies. It copies complete
terrain references and identity reservations and creates a complete geometric occupancy directory.
Its site evidence records the original ordinary reach, placement and complete-population access result.
There is no retained `WorldSnapshot`, `LocalSpatialSnapshot`, terrain detail, encoded terrain archive,
terrain-retaining identity index or callback over the source world. Actor and custodian identities
remain stable; material and actor collections are still complete locality aggregates, not separately
paged canonical owners.

The caller provides the metadata-page budget at capture. Exceeding a preparation budget is a refusal,
not permission to omit people or truncate access obligations. All supplied geometry metadata remains
resident. Identity reservations also retain this interval's proposed command/outcome identities; their
size grows with selected steps. Neither collection is a bounded global frontier representation.

## Selected work and shared rules

`PrepareStep` accepts a `WorldStepBatch` for the exact world/branch, profile, revision and advancing-step
count. The operation contains exactly one work command for the captured project, no movement, and the
advancing or held interaction kind consistent with retained dialogue state. Other actions and timing
owners require their own execution path. A batch or encoded delivery is not transport authentication
or proof that a host selected its cutoff.

Current owner-established occupancy and terrain references are explicit inputs. The complete actor
and structure population, terrain pins and support interpretation must remain exact. An arriving actor,
changed body/position, edited region or installed neighboring structure cannot disappear from the access
obligations. Changed dependencies require actual preparation from current admitted owners; passing an
old directory again cannot establish that it is still authoritative. Source freshness and publication
serialize at the owner.

[ConstructionWorkQualification](../../src/OpenFantasy.Core/Construction/ConstructionWorkQualification.cs)
checks current control, ownership, HP/capability, quest funding, exact tool/storage/project revisions,
reservation-wide stock, actual required-access actors and placement before proposing earned work.
Its typed result names the project, actor, tool, storage, reserved stock, access requirements, recipe,
proposed progress and structure/placement. The result is ephemeral evaluation data, not a durable
admission witness, reusable authorization, clock advancement or published structure.

[ConstructionWorkKernel](../../src/OpenFantasy.Core/Construction/ConstructionWorkKernel.cs) calls this
same qualification for live, detached and locality paths. It retains partial work or performs the
existing final identity/capacity/material mutation and installed-backing checks. The caller still owns
earned time, complete-world admission and publication. Qualification keeps ordinary rejection order;
material completion uses the exact structure identity derived by qualification. Ordinary publication
still reads complete inventory/production/construction inputs. Qualification also accepts an internal
`IConstructionWorkOwners` boundary: the complete adapter reads those original owners, while the
stored-cohort adapter supplies exact selected holders/project under a captured admitted source.
That read-only binding is separate from work publication and due-process advancement.
The legacy control adapter uses actual counted authority and cannot interpret delegated control or
infer eligibility from an unloaded region.

[WorldStepIdentityReservations](../../src/OpenFantasy.Core/Worlds/WorldStepIdentityReservations.cs)
is shared with ordinary world execution. Cause and command identities are reserved before action
execution; the exact outcome and any spawned identities follow. Local reservations and the mandatory
external historical lookup both participate. Captured local identities do not cover all prior commands
or absent world history. Retrying a preparation on the same immutable source produces the same
candidate. Submitting its old batch to the successor cannot earn another step.

[WorldExplorationBoundary](../../src/OpenFantasy.Core/Worlds/WorldExplorationBoundary.cs) is also shared
with ordinary execution. After work, an advancing selection contributes one existing exploration
increment; due gathering/regrowth runs and every resident earns its existing experienced-time
increment. [WorldResidentExposureBasis](../../src/OpenFantasy.Core/Worlds/WorldResidentExposureBasis.cs)
retains the admitted membership and personal ages at the segment basis plus the exactly earned common
exploration exposure. Each successor requires the next selected ordinal and validates the oldest
resident's representable duration before returning. Overflow therefore refuses at selection, not at a
later read. Materialization uses the same `WorldResident.Experience` operation as ordinary execution.
It preserves every other resident field and does not derive exposure from calendar position. A refused action still consumes its already-selected advancing boundary. A held work
selection refuses without advancing calendar, gathering or resident exposure. Construction is not
completed by subtracting wall-clock timestamps or multiplying work by an absence duration.

Completion retains coupled input retirement, installed reserved output, offcuts, producer ancestry,
project progress and the new structure reference. The occupancy successor and material/project/time
successors belong to the same private candidate. A metadata-capacity failure publishes none of them.
The completed project cannot manufacture another output on a subsequent selection.

## Reconstruction and publication boundary

[ConstructionLocalityStep](../../src/OpenFantasy.Core/Construction/ConstructionLocalityStep.cs) contains
the exact immutable source, candidate, proposed receipt and identity claims. They are unpublished
transition data. A preview does not create a durable outcome, earn authoritative world time or make
that receipt eligible for delivery. The state has no storage writer or independent simulation loop. `ConstructionLocalityStep.Rehydrate`
performs candidate reconstruction and packages these same proposed outcomes as a `WorldStepDecision`;
that conversion does not establish source freshness or authorize delivery.

`Rehydrate` requires every exact pinned terrain region, reconstructs derived installed geometry and
runs ordinary complete `WorldSnapshot` admission with all retained owners. Missing, edited, duplicate
or unrelated regions refuse. The complete canonical storage envelope remains mandatory before persistence. The explicit
terrain-separated binding below can enforce that envelope from its admitted byte-length difference;
ordinary publication reconstructs and encodes the complete root. Reconstruction neither authenticates the candidate's ancestry nor writes a save.

The domain binding does not handle movement, gifts, conversations, control changes, care, encounters,
structural settlement, liquid processes or their later-profile histories. Those facts cannot be dropped
to enter this binding. Complete state must be admitted again when the supported interval changes;
interventions must preserve actual work and material, not restart the project. The single serialized
publisher must eventually bind exact owner dependencies, due state, receipts and original history in
one durable transaction. This responsibility is described in
[persistence and history](persistence-and-history.md#design-contract-partitioned-persistence-and-version-ownership).

See [construction execution](construction-execution.md) for ordinary runtime scheduling, cached geometry
and detached one-action evaluation. Keeping these boundaries distinct prevents a geometry result or
an uncommitted continuation from becoming an alternative world authority.

## Serialized publication binding

The internal `SqliteWorldAuthority.CommitConstructionLocality` entry point accepts a host-selected batch,
project and metadata budget, never a caller-supplied speculative decision. Under the ordinary authority
lock and SQLite write transaction, it validates current dispatch and uses actual genesis plus durable
identity history. A single retained locality is reusable only for its exact committed world hash,
project and metadata budget. Any ordinary commit clears it; a changed project or budget recaptures from
the actual complete world. Reopening rebuilds from verified ordinary history, with no persisted cache.

The shared construction kernel evaluates each selected command. The result reloads the authority's exact
terrain into complete admission and enters the same canonical row preparation, journal policy, identity
reservations, command receipts, head and checkpoint transaction as ordinary work. No client-facing
submission or default scheduler selects this internal binding. Source profiles outside its admitted
legacy law refuse; they cannot fall back to a reduced set of owners.

Only successful database publication installs the candidate and retained locality in memory. A fault
before or after the database commit requires reopening, using the existing recovery fence. The cache
cannot turn a rollback into earned work, bypass stale-batch checks or release a speculative receipt.
Ordinary journal replay remains the interpreter, so saved commands, hashes, outcomes and identities
must agree with ordinary execution. This complete-root entry point introduces no schema, codec or historical interpretation.

This binding retains a complete authoritative world and encodes it at each publication. Nonterrain
rule evaluation is separable, but durable publication is not terrain-free. It does not provide a
canonical partition directory, independently resident actor/material owners, partial recovery or
migration of the richer expedition interpretations. Those remain distinct persistence obligations.

## Resident exposure segment boundary

The exposure basis is transient and internal to the supported locality interpretation. It is not a
saved facet, shared global clock, permission to skip due processes or a model for different personal
rates. A hold retains the same basis without advancing it. Repeated reads and repeated preparation
from the same immutable source do not award additional time. Each immutable successor shares the
unchanged basis metadata rather than retaining a chain of intermediate resident copies.

An ordinary intervening action clears the publication cache. Recapture uses the current admitted
residents with all earned exposure already materialized and pins the current advancing ordinal; it
cannot charge the previous segment again. Changes to membership, personal modifiers or timing domain
must close and rebase their segment through admitted owners. The locality cannot interpret those
additional laws by treating them as its existing common exploration rate.

Complete-world publication materializes and serializes every resident, as required by its unchanged
format. Deferring resident copies reduces per-step owner rewrites only while consumers can retain the
basis without requesting complete resident snapshots on every step. It does not reduce the current
canonical journal's write set or prove a production memory/throughput benefit.

## Explicit terrain-separated storage binding

`SqliteWorldAuthority.CreateWithSeparatedTerrain` is an internal, fresh-only profile-five binding to
storage schema13. It is distinct from gameplay profile13. Default creation and existing saves retain
their named schemas; neither an implicit upgrade nor a reduced interpretation of profiles40/41 is
permitted. The ordinary profile-five interpreter remains authoritative for every saved command.

`ConstructionLocalityEncoding` encodes all supported nonterrain owners in canonical order: inventory,
actor bodies/presences, resident metadata/exposure, admission, calendar, revision, advancing ordinal,
dialogues, gathering, production, progression, quests and construction. Installed geometry is derived
from the exact construction definitions/output bindings, as in the complete profile-five codec. Each
terrain reference retains scope, bounds, revision and canonical content hash. The encoding supplies
no independent decoder or admission of untrusted owner fragments.

The original full genesis remains unchanged. `world_terrain` stores its immutable canonical regions
by hash. Head and checkpoints store the locality image; journal before/after hashes digest that image,
not the full `WorldSnapshot` encoding. Recovery verifies the terrain directory against genesis and
replays the complete original history, comparing every image, outcome and identity before readiness.
Historical world reads still return complete admitted roots. This is complete replay with a different
storage image, not partial authenticated recovery or a per-actor persistence directory.

`EvictConstructionTerrain` captures the exact committed locality and releases the authoritative full
world, scene, terrain and terrain-backed identity index. Genesis entity reservations are copied as
identities. The admitted difference between full-world and locality-image lengths is retained: both
encodings share exact component bytes and fixed-width framing, and selected work cannot mutate terrain.
Each candidate checks the resulting full-envelope length against the unchanged16MiB bound. Ordinals,
resident exposure and due gathering remain part of each selected advancing boundary.

`CommitWithoutTerrain` accepts one current selected work batch under the same authority lock. It
checks actual controller authorization and durable identity history, uses the shared construction
kernel, validates its receipt and enters the existing journal policy and SQLite write transaction.
Partial work reads no terrain archives. Final installation loads exact geometry, performs complete
admission and verifies the full length against the retained envelope before publication. The new
head, command outcome, identities and any due checkpoint commit atomically. Only then is the in-memory
locality replaced. Publication faults require reopening; terrain restoration/final-admission failures
also fence the authority without releasing a speculative receipt.

An ordinary action or host read restores the full current world through exact terrain references.
Ordinary commits invalidate the locality interval; explicit recapture is required before further
unloaded work. No default scheduler or public API enables this experiment. It retains a whole locality
of nonterrain owners, complete occupancy metadata and growing identity reservations; it does not claim
independent actor/custodian residency, reduced resident serialization, scalable frontier scheduling,
current-profile migration, a cold-load budget or production checkpoint qualification.

## Typed-owner storage binding

`CreateWithTypedOwners` selects fresh profile-five schema14, retaining the same domain continuation,
terrain eviction, controller authorization, historical identity checks and shared SQLite publisher.
`WorldStateImage` encodes a complete typed owner directory rather than the schema13 flat nonterrain
image. The current directory and all new immutable owner payloads publish with receipts and the head.
An ordinary intervening action still invalidates the interval; final installation requires exact
terrain and complete physical/material admission.

Custodian images contain both counted and transformed holdings. Project progress, construction
catalog revision, clocks and actor resident exposure retain their actual owners. Work that only advances
progress can preserve custody hashes; completion changes actual material/provenance owners. Common
exposure is still materialized into actor images on each advancing boundary. This does not imply
independently loaded actors or a persisted general exposure-basis law.

Complete replay verifies every historical payload and exact archive membership before readiness.
Recovery then reconstructs a complete admitted world from the stored head's actual owner payloads and
terrain, compares its canonical bytes with replay and returns that reconstructed head. Reopening a
partial project can recapture a construction interval and continue the same earned work. This loads
all required owners and terrain; it does not preserve detached residency across restart or skip history.
The internal `ReadOwnersForHost` diagnostic checks current owner bytes against the committed directory
hash. A complete nonterrain locality can supply these bytes without terrain; a selectively resident
interval restores the complete cohort for this diagnostic. It is not a player knowledge projection.
See [typed ownership](partition-ownership.md#runtime-binding-legacy-typed-owner-storage) for precise
payload responsibilities, storage bounds, historical verification and unsupported scope.

## Selected stored-owner qualification

[ConstructionWorkCohort](../../src/OpenFantasy.Core/Construction/Ownership/ConstructionWorkCohort.cs)
captures a profile-five work command from a complete admitted world. It retains the exact world and
directory hashes, source revision, required owner descriptors, complete reservation lot membership,
necessary source revisions and the existing site evidence. It retains no inventory, production,
resident collection, complete world/scene, terrain detail or owner payload byte arrays. Site evidence
still contains complete actor/structure geometry; this is not independent actor-detail residency.

Required stored records are the command actor/control, project/definition, project and actor inventory
holders plus every holder containing that reservation, and shared progression/quest/dialogue owners.
The source establishes reservation completeness across all holders; selected data cannot assert
absence on its own. Every payload read must match its captured hash, key, role, world/branch and field
shape. Existing custody/construction/progression/quest/dialogue codecs decode the work facts. The
selected reservation identities must exactly match the captured complete-source membership.

`CaptureStoredWorkCohort` runs under the Host lock and checks the captured directory against its current
committed head. `AssessStoredWorkCohort` serializes head comparison and selected reads with every writer.
Any changed directory, including an unrelated commit, invalidates the capture before payload reads.
Storage/format failures fence the Host for recovery. The Core reader by itself authenticates no journal;
its caller owns the trust in the admitted source and serialization of freshness with reads.

Assessment calls the same qualification guards and returns proposed progress/rejection only. It does
not advance calendar/gathering/resident exposure, reserve command identities, mutate materials, issue
a receipt or publish geometry. `RequiresCompletion` requires the larger final material/identity/geometry
cohort and may still fail its later checks. Ordinary work publication retains its complete pipeline. The separate stored interval below supplies
partial publication and due-process closure; this read-only capture itself expires on the very next
committed change.

## Selected stored-owner publication

[StoredConstructionInterval](../../src/OpenFantasy.Core/Construction/Ownership/StoredConstructionInterval.cs)
captures the exact admitted profile-five directory, owner hashes/lengths, terrain references and the
captured actor/project cohorts' complete reservation/geometry witnesses. It retains no inventory, production, terrain or
complete resident collection. Complete actor/structure geometry, directory metadata and copied source
identity reservations still remain resident. The interval is private execution preparation, not a
new persistent gameplay format or an independently authenticated historical snapshot.

Each selected boundary loads the union of work qualification dependencies plus timing, construction catalog,
gathering and **every actor image**. Their exact hashes, lengths, scope, roles and field counts bind
them to the admitted directory. Shared qualification and the project's progress operation handle the
work; the existing exploration boundary, gathering process and resident exposure law supply all due
effects in ordinary order. Rejected advancing work still advances these effects; held work preserves
them. Unrelated custodian contents and production history remain in storage. This is not a claim that
unselected residents can skip time, that all actor detail is unloaded, or that general scheduling is solved.

`BeginPreparation` returns a [stored preparation](../../src/OpenFantasy.Core/Construction/Ownership/StoredConstructionPreparation.cs)
that reads at most the requested owner count per slice. The exact head is required again at every
slice and completion; failed or invalidated preparations cannot resume. Until all dependencies are
present there is no candidate, receipt or earned interval. Completion processes the entire selected
batch synchronously, in ordinary actor order, before applying passive effects once. Empty selections
retain the same passive rules. The captured source and any published head remain unchanged by loading.

Selected project decoding uses the project's canonical payload and exact definition pin. It does not
construct a miniature catalog with the full catalog revision: that revision is the sum of all projects'
work and is not generally equal to the selected project's revision. Complete source admission and
the interval's own transitions retain the aggregate invariant. Partial qualification grants no authority
to admit an arbitrary independently assembled directory.

The interval produces changed payloads and a complete new directory, preserving unchanged owner
references. Only its own accepted steps renew the metadata and project/catalog revisions. Changed
field sizes update the retained full-world byte envelope: these fields occur once in the profile-five
frame and revision/framing widths stay fixed. Individual payload, aggregate payload, directory and
complete-world limits remain enforced. It neither changes a historical interpreter nor adds a new
saved exposure basis.

`EvictForStoredConstruction` captures under the authority lock, checks the actual committed directory
and releases the complete resident world and locality. `CommitStoredConstructionBatch` checks current
head/dispatch/controller/hold and original identity history, then publishes changed payloads, the
complete directory, input, all command receipts, reserved identities and any due checkpoint through `PublishWrites`
in one SQLite transaction. Only after commit does the admitted interval advance. Format/storage and
publication failures fence the authority; an unsupported/stale dispatch cannot consume a command.
Archive accounting still scans retained lengths; the complete directory is still encoded each step.
`CommitStoredConstruction` remains the one-action/one-receipt wrapper. The batch entrypoint completes
preparation synchronously under the existing lock; no persistent cursor or asynchronous world writer
is added. Per-slice loading does not imply a bound on final rule-evaluation latency.

A qualifying final work step, or a batch crossing the legacy aggregate construction revision bound,
returns the whole batch to ordinary full-world execution before publication. Earlier prepared actions
in that batch are not independently committed or converted into invented refusals.
The Host reconstructs all current owners and exact terrain, then the normal kernel applies combined
capacity, material provenance, spawned identity and physical installation admission. Full host reads
also reconstruct the current admitted world; ordinary commits invalidate the stored interval. Switching
back to complete nonterrain eviction discards its metadata. Intervening movement, gifts or other
operations require explicit fresh capture before selective work resumes. No receipt from speculative
preparation escapes, and repeated input still obeys the original identity rules.

Reopening verifies original complete history and every retained owner before reconstructing the full
head. Selective residency is not persisted across restart. Current-profile migration, arbitrary due-work
selection/fairness, paged directories, cold loading and production-scale performance remain separate
contracts. The internal binding does not change the default game or its scheduler.
