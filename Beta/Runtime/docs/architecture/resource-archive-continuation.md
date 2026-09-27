# Resource archive continuation

## Closing a complete boundary

[WorldResourceContinuation](../../src/OpenFantasy.Core/Worlds/WorldResourceContinuation.cs) reserves
capacity for the complete next world batch before its resource writers run. A physical resolution
reserves one batch and both target/caster effects; a treatment reserves one batch/effect; quest
rewards reserve their actual potential command count. A cut cannot split a command, split the
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
completed quest rewards, care acknowledgments/treatments, the current episode's RNG position and
original attack witnesses still referenced by care or the lifecycle. Old completed records remain
exact; accepted quests may complete later. Catalog pins, scope, admission and monotonic clocks
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

The durable host excludes care-enabled worlds. Before admitting them, an explicit storage schema
must write the sealed bytes, accepted batch, outcomes, identities, successor root and scheduled
checkpoint inside the same transaction, then publish memory only after commit. Preview prepares
without writing. A crash yields both archive and successor or neither; an uncertain outcome
requires recovery. Historical reads retain their source-head pin and retrieve the same evidence.

The resource segment bounds are not gameplay limits. This continuation mechanism does not remove
other retained-care/material collection limits, the host journal's development envelope, or roster
and rule-migration requirements. Those owners need their own admitted continuation contracts.
Archive bytes need explicit storage accounting and retrieval policy; a bounded current root does
not imply unlimited disk capacity or qualified production throughput.
