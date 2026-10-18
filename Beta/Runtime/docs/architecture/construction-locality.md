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

[ConstructionWorkKernel](../../src/OpenFantasy.Core/Construction/ConstructionWorkKernel.cs) evaluates
current retained control, HP, funding, tool/stock/revisions, geometry, work and material completion.
The legacy control adapter uses the actual counted authority; it does not infer eligibility from an
unloaded region. It cannot interpret delegated control. The kernel preserves its ordinary rejection
precedence and exact output identities.

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
or unrelated regions refuse. Canonical encoding and its complete storage envelope remain mandatory
before persistence. Reconstruction neither authenticates the candidate's ancestry nor writes a save.

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
must agree with ordinary execution. There is no new schema, codec or historical interpretation.

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
