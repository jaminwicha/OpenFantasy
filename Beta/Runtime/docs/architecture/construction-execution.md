# Construction execution and retained site evidence

## Runtime binding: shared decision rules

[ConstructionWorkKernel](../../src/OpenFantasy.Core/Construction/ConstructionWorkKernel.cs) owns the
construction decision sequence. Both ordinary work and detached evaluation use it. The rules retain
current command authority, project ownership, holds, body functions/HP, completed quest funding,
work position/reach, exact tool and storage revisions, reserved source stock, proposed structure
placement/access, work exposure, completion capacity, identity exclusions and the material transform.

[ConstructionPlanner](../../src/OpenFantasy.Core/Construction/ConstructionPlanner.cs) supplies live
world and geometry adapters. Its public signature and `ConstructionPlan` result remain the ordinary
boundary used by `WorldStepProcessor`. A completed live result must contain the fully admitted local
spatial candidate. The complete world then applies its own time/order and physical admission, and
Hosting publishes the durable transaction. The standalone material-continuation planner uses this
same kernel through the continued adapter and retains its original completion-evidence encoding.

The adapters expose meaningful read responsibilities rather than another world model.
[ConstructionDecisionSource](../../src/OpenFantasy.Core/Construction/ConstructionDecisionSource.cs)
provides the exact nonspatial facts the rule consumes;
[IConstructionGeometry](../../src/OpenFantasy.Core/Construction/IConstructionGeometry.cs) supplies
actual actors, point reach and proposed placement. A live adapter retains its world/scene only for
ordinary evaluation. A detached source copies decision facts and identity reservations instead.

## Runtime binding: reuse inside selected world execution

`WorldStepProcessor` owns one bounded [ConstructionSiteCache](../../src/OpenFantasy.Core/Construction/ConstructionSiteCache.cs)
through its construction planner. The cache retains one immutable site qualification. At the placement
operation, it checks the actual running scene and project, including changes made by earlier actions
in the selected batch. Changed terrain, occupancy, body, structure, support law or project access
replaces the qualification using the ordinary complete geometry operation. Both success and refusal
may be reused only while those dependencies remain exact. Every command-derived structure identity
still passes the spatial catalog check, and a successful placement still constructs and admits its
complete spatial candidate. Point interaction remains a live query.

This cache supplies geometry, not an alternative work executor. The processor still selects the batch
against the current world revision, reading hold and interpretation, routes care and encounter work,
checks readiness, and evaluates commands in their original order. Live inventory, project revisions,
tools, funding, identity reservations and complete physical reconciliation remain mandatory. Accepted
work suppresses that actor's subsequent movement. An already-selected advancing step contributes time
once even if an earlier action opens a reading hold; a held closing interaction contributes no work or
time. Reused geometry cannot change those rules.

The ordinary Hosting preparation and SQLite publisher use this processor. A preview can warm geometry
without earning work, spending a command or publishing a receipt. Commit re-evaluates against current
state under the serialized authority; heads, material changes, work and outcomes remain one transaction.
The cache is neither persisted nor trusted during reopening. Historical replay reconstructs it from
actual admitted scenes, and discarding it changes no canonical bytes or outcomes. The standalone
`ConstructionPlanner` also retains its uncached complete-geometry path.

Only detached scene metadata is retained in the cache. It keeps no world, terrain cells, identity index,
complete spatial candidate or material owner. Lookup/replacement is synchronized; evidence is immutable,
so concurrent independent decisions cannot turn another source's qualification into permission. Complete
world residency and encoding remain requirements of this runtime binding. See
[persistence and history](persistence-and-history.md) for the durable authority contract.

## Domain binding: detached work preparation

[ConstructionWorkPreparation](../../src/OpenFantasy.Core/Construction/ConstructionWorkPreparation.cs)
is created from an admitted world, an actual construction command and qualified site evidence. It
captures the exact source hash, command, inventory/production/project owners, selected advancing/hold
flags, command-specific authorization and the relevant actor, quest and identity facts. It retains
neither the source world nor its identity index. Identity reservations copy actual entity keys and
prospective encounter-family prefixes; terrain objects are not copied into that lookup.

`Evaluate` runs the shared kernel and returns a
[ConstructionWorkResult](../../src/OpenFantasy.Core/Construction/ConstructionWorkResult.cs): coupled
material/construction candidates and, on completion, the proposed structure and created identities.
It does not return an admitted partial world, physical member/load decision, earned clock or receipt.
Repeated evaluation of the same preparation produces the same candidate rather than accumulating work.
Historical identity occupancy remains a separate authority input, as in the ordinary planner.

`ValidateCurrent` conservatively compares the complete current root hash with the captured source.
Source capture and this check require the complete world; they are not a partially resident publisher.
The owner must serialize freshness, selected action ordering, readiness, other accepted effects,
earned exposure, physical/capacity admission and publication. Flags supplied for evaluation are not
permission to bypass that owner. A stale preparation may describe a historical candidate but cannot
authorize new input.

Material continuations must have qualified complete-world witnesses. A continuation retaining an
excavation region set or terrain-representation context is explicitly refused by detached capture:
keeping such an owner would indirectly retain terrain detail. Ordinary continuation admission remains
unchanged. Supporting those detached contexts requires preserving their actual witnesses through a
separately qualified boundary, not dropping the fields or treating them as empty.

## Domain binding: reusable site evidence

[ConstructionSiteEvidence](../../src/OpenFantasy.Core/Construction/ConstructionSiteEvidence.cs) captures
point reach and prospective placement from the actual complete scene of an admitted project. The
ordinary placement operation qualifies both structure geometry and required access, including current
actor positions. The resulting evidence retains actor/structure metadata, terrain references, support
interpretations, project geometry/definition pins and the exact evaluated access set. It releases the
scene, terrain cells and prospective complete scene after capture.

The evidence applies only to the same project geometry, required accesses and complete captured
occupancy population. Geometry can remain unchanged while project work progresses. A fresh work command
still derives its own result identities: known spatial identity exclusions and catalog bounds are
checked again, and completion still checks world/material/historical identity reservations. Shape,
bearing-surface or access changes cannot reuse a previous result.

Revalidation accepts either a complete spatial snapshot or an owner-established complete occupancy
directory plus exact terrain references and support modes. It compares the entire captured population,
not only colliders near the construction site, so an added actor cannot disappear from the legacy
same-floor access obligation. Changed body state, movement, structures, terrain or support interpretation
invalidates the result. Directory completeness remains the caller authority's obligation; the directory
is a geometric catalog, not a global ownership proof. Exact captured geometry is immutable, but its
continued applicability must be checked before publication.

This conservative all-population check preserves the existing access law. It is not a scalable
protected-access-domain law or an automatic re-preparation service. See
[materials and activities](materials-and-activities.md) for the witness-count constraint and
[partitioned persistence](persistence-and-history.md#design-contract-partitioned-persistence-and-version-ownership)
for canonical ownership, clocks and publication obligations.

## Coupled completion and extension boundary

Completion uses `MaterialTransformCommitter`: reserved input lots retire, the production batch and
output lots retain their ancestry, the installed output stays reserved, and the project records the
exact installed identities. These are candidate changes until the actual authority admits and commits
the complete transaction. Cached reachability cannot substitute for structural strength or the
consequences of inventory changes on supported actors and assemblies.

A detached candidate can execute without its source terrain objects. That fact alone does not supply
an offscreen scheduler, actor/cargo owners, atomic directory updates, crash recovery or historical
partition reconstruction. These remain responsibilities of the integrated authority. In particular,
loading a full world to refresh every prepared input must not be described as bounded world residency.
