# Resource archive continuation

## Closing a complete boundary

[WorldResourceContinuation](../../src/OpenFantasy.Core/Worlds/WorldResourceContinuation.cs) reserves
capacity for the complete next world batch before its resource writers run. A physical resolution
reserves one batch and both target/caster effects; a treatment reserves one batch/effect; quest
rewards and ordinary restorative uses reserve their complete potential command count. A cut cannot split a command, split the
world transaction or relocate an effect to another world sequence.

When the active segment lacks capacity, [WorldResourceArchiveSegment](../../src/OpenFantasy.Core/Worlds/WorldResourceArchiveSegment.cs)
seals the exact canonical complete closing root. Its reference contains world, branch, ordinal,
closing world sequence and SHA-256 of those bytes. The first ordinal is zero; each successor derives
its ordinal from the previous anchor. Empty resource segments and pending excavation cannot be
sealed. An acknowledged care obligation is an already committed boundary and may be retained.

The candidate starts a fresh active segment at the closing progression and world sequence, then
executes the ordinary selected batch. Archiving earns no simulation time and creates no resource
effect. The original input root remains immutable. `WorldStepDecision.SealedResourceSegment`
returns the detached archive together with the complete candidate, receipts and identities.
A failed preparation publishes none of them. Rejected gameplay commands can still accompany a
valid deterministic storage cut; they create no resource effect.

## Bounded continuation context

[WorldResourceArchiveAnchor](../../src/OpenFantasy.Core/Worlds/WorldResourceArchiveAnchor.cs) retains
only bounded immutable progression, care, quest and lifecycle components from the closing root.
It does not retain the root, active history or a chain of ancestor anchors. Prior full evidence
remains in retrievable archive bytes, rather than accumulating in the current snapshot.

Resource admission starts from this exact progression and currency basis. It carries forward
completed quest rewards, care acknowledgments/treatments and ordinary uses, the current episode's RNG position and
original attack witnesses still referenced by care or the lifecycle. Old completed records remain
exact; accepted quests may complete later. Ordinary definitions and permission grants remain pinned;
revocations retain their exact order, and a successor cannot insert a newly claimed revocation before
the sealed boundary. Catalog pins, scope, admission and monotonic clocks
cannot change across the cut. New effects still replay through the named damage, MP, XP and
restoration laws; a prior reward or treatment cannot execute a second time.

Historical command uniqueness also spans archives. `WorldStepProcessor` requires the caller's
complete historical identity lookup before creating or using a continued segment. The host must
retain that index; the active segment is not a substitute for permanent command/identity history.

## Explicit bytes and actual retrieval

[WorldResourceHistoryCodec](../../src/OpenFantasy.Core/Worlds/WorldResourceHistoryCodec.cs) keeps
the original inline V1 bytes for uncontinued histories. Continued histories use an explicit V2
outer frame containing the archive reference and the bounded active V1 structural payload. The
active payload's basis must match the retrieved boundary's progression and sequence exactly.
The V1 encoder refuses to discard an anchor; legacy histories do not silently upgrade on restore.

A continued `WorldSnapshotCodec.Decode` requires its actual `WorldResourceArchiveAnchor`.
`WorldResourceArchiveSegment.Decode` bounds and hashes the retrieved closing bytes before decoding,
uses the predecessor anchor where required, and reseals to verify every reference field and exact
canonical bytes. Missing, substituted, wrong-branch, wrong-ordinal or truncated evidence fails.
A reference/hash supplied without the corresponding validated boundary cannot restore the root.
A reader can walk segments iteratively, retaining only the current anchor between steps.

Structural retrieval still does not authenticate a claimed genesis or command history. A host
must derive each seal from its actually replayed prior world and compare the stored bytes. It must
reject missing and extra archives, rather than regenerate missing storage and call recovery valid.

## Durable publication contract

[Schema seven](../../src/OpenFantasy.Hosting/Worlds/WorldDatabaseSchemaSeven.cs) exclusively binds
care-enabled profiles25 and26 to `world_resource_segments`. Each append-only row records world, branch,
ordinal, boundary sequence, publication sequence, boundary hash and complete closing bytes. The
boundary is revisionN; its publication is commitN+1. The publishing commit's before-hash equals
the boundary hash, and its sequence is a foreign key. Scope/ordinal and publication are unique.
Older schemas retain their exact definitions; opening a save does not migrate its interpretation.

[SqliteWorldAuthority](../../src/OpenFantasy.Hosting/Worlds/SqliteWorldAuthority.cs) writes the archive
after the commit row and before the head, in the same transaction as outcomes, identities and any
scheduled checkpoint. Storage checks the exact prior head bytes and admitted genesis scope.
Only transaction success publishes memory. Preview prepares without writing; uncertain storage
outcomes require reopening. The existing exclusive lease also owns consistent new-file backups.

[WorldResourceArchiveReplay](../../src/OpenFantasy.Hosting/Worlds/WorldResourceArchiveReplay.cs)
walks actual stored rows alongside deterministic journal replay. Each required row must exactly
match the closing replay root and expected seal, then decode with the preceding retrieved anchor.
Missing, altered, unscheduled and trailing rows fail. Complete head/checkpoint comparison and the
permanent identity ledger still apply. Recovery never replaces missing evidence with a regenerated
seal. Privileged `ReadResourceArchiveForHost` verifies the complete journal under the writer gate
and returns a detached immutable segment with publication sequence and the source-head revision/hash.
It neither exposes a live database resolver nor grants player knowledge. Historical state reads use
the same verification path; these synchronous operator operations do not belong on a client frame.

[WorldJournalPolicy](../../src/OpenFantasy.Hosting/Worlds/WorldJournalPolicy.cs) accounts separately
for archive count/bytes and accepted batch/outcome bytes. It checks the development envelope before
publication, reconstructs identical usage during recovery and throws a host stop when exhausted.
The archive envelope is1,024segments/256MiB; the unchanged accepted-journal limits still apply.

The resource segment bounds are not gameplay limits. This continuation mechanism does not remove
other retained-care/material collection limits, the host journal's development envelope, or roster
and rule-migration requirements. Those owners need their own admitted continuation contracts.
Archive bytes need explicit storage accounting and retrieval policy; a bounded current root does
not imply unlimited disk capacity or qualified production throughput.

## Coordinated material and care interpretation

Resource, care and material histories describe different facts. Resource batches prove effects;
ordinary uses and aftermath records prove their named causes; production claims account for the
actual disposed material. A coordinated continuation must seal a complete admitted boundary and
reserve the whole next batch before changing any active window. It must retain pending obligations,
permission revocations, absolute owner revisions and permanent command/result identities, while
preserving exactly one material disposition and one resource effect for each completed use.

The [material continuation contract](item-consumption.md#canonical-archive-ownership) also protects
issued source rights, crafted-output partitions and surviving lot lineage. A bounded anchor cannot
quietly accumulate all predecessor roots or replace that proof with a cumulative count. Full closed
records remain retrievable and their storage participates in the publication budget.

The standalone [material basis](material-continuation-basis.md) captures exact surviving material
rights and direct raw retirement from complete supplied owners. It is an immutable observation,
not a resource archive anchor or an independently authenticated grant. A coordinated boundary must
qualify those rights against actual issuance and the full world before using them in continued state.

The separate [continued production owner](production-continuation.md) gives material operations an
explicit active-window interpretation. It does not replace this resource anchor or let care effects
outlive their causes without a jointly admitted closure. Profiles1–26 refuse that owner.

The standalone [care frontier](care-continuation.md) retains exact pending aid, grants/revocations,
cumulative counts and latest patient treatment witnesses in Care V3. Profiles1–26 also
refuse that continued owner. Its care-operation watermark does not replace the closing world revision
or the causal resource anchor; those boundaries must agree through the coordinated interpretation.

Changing active-window semantics requires an explicitly admitted interpretation and matching nested
codecs. Existing profiles retain their capacity refusals as well as their successful outcomes; replay
cannot reinterpret a historically refused at-capacity command as a newly accepted use. Opening a
lived save is not authorization to migrate it. Resource-only continuation preserves the existing
care/material histories under their current interpretation.

The [live material evidence seam](live-material-evidence.md) observes accepted actions in their actual
complete-world transaction. It separates world publication order from per-action order and retains no
ancestor root. It does not replace closure, material conservation, retrieved archives or authenticated replay.

[Profile27's coordinated protocol](coordinated-world-continuation.md) implements the separate joint
closure and schema8 publication contract. It can close an empty resource window when material or
care requires the boundary. Resource-history V3 binds that exact joint anchor; V1 refuses to discard
either kind of anchor. The schema7 resource-only protocol and its historical command outcomes remain
unchanged. The complete world journal, rather than an additional material operation log, orders the
actual raw custody, gathering, crafting and work actions.
