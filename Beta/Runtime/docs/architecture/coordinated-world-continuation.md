# Coordinated world continuation

## Scope and owners

[WorldCoordinatedAdmission](../../src/OpenFantasy.Core/Worlds/WorldCoordinatedAdmission.cs) selects an
explicit complete-world interpretation at fresh ordinary-care genesis. Profile27 preserves gathering,
shared effort, crafting, custody, construction and installed backing, delivery quests, progression,
wet excavation, reservoirs and detailed liquid flow, pacing, encounters and both forms of care.
A lived profile26 save does not acquire this interpretation on opening. Earlier profile and component
frames retain their own rules, including capacity refusals.

The existing world journal is the single ordered action history. Production revisions count crafts
and consumption. Raw transfers, gathering, delivery and partial work advance their actual owners;
they do not invent Production operations or duplicate the journal in a second material log.
One world commit can apply one action per admitted actor. Cumulative Production, quest, progression
and aggregate construction revisions count domain operations, so they can exceed the number of world commits. Profile27's
structural upper bound uses the existing per-batch actor limit with overflow-safe arithmetic; exact
operation counts and order still come from owner validation and journal replay. Earlier profiles
retain their original admission rules.
Each builder's work still fits that actor's actual advancing steps and experienced time.
[WorldCoordinatedContinuation](../../src/OpenFantasy.Core/Worlds/WorldCoordinatedContinuation.cs) owns
the initial issuance baseline and the latest joint archive anchor. Each canonical fact continues to
belong to its domain owner.

## Exact material rights

[WorldSourceIssuanceBaseline](../../src/OpenFantasy.Core/Worlds/WorldSourceIssuanceBaseline.cs) captures
actual initial raw intervals, gathering issue cursors and initially extracted sites. Later issued rights
come from the current gathering and excavation owners. Source capacity is an upper bound, never an
allocation. Archived direct raw retirement, active direct raw retirement and live raw intervals must
be disjoint and together equal all admitted issued rights. Missing units are as invalid as overlaps.

Production V9 uses the [continued material frontier](production-continuation.md) inside this complete
world. Original producer witnesses and surviving output partitions preserve lineage, spent prefixes
and holes. A recrafted input contributes its actual output interval; ancestry does not consume the
same raw units a second time. The complete world validates issuance; deterministic Host replay
validates the actions that moved, crafted, consumed or earned those rights. Structural conservation,
monotonic counters and matching hashes do not establish that an action was authorized.

World crafting, care and source delivery supply permanent historical identity lookup after a cut.
The world reserves each current command before invoking a domain planner; its adapter excludes only
that current command from the planner's duplicate check. Other prior commands and created identities
remain occupied. Source delivery admits this complete-world contract explicitly, while unsupported
standalone continued profiles retain their refusal. Imported producer witnesses and original installed
construction ingredient identities participate in the complete world's role index.

## One complete closing boundary

[WorldMaterialCareContinuation](../../src/OpenFantasy.Core/Worlds/WorldMaterialCareContinuation.cs)
reserves capacity for the complete next batch plus future selectable work before interpreting any
command. A boundary does not split a batch, earn time or clear pending aid, partial excavation or
unfinished construction. Material-only closure can have an empty resource segment; resource-only
closure can carry care with no new operation. The capacity rules preserve a further actionable offer
so a full active window cannot hide the command needed to advance it.

[WorldMaterialCareArchiveSegment](../../src/OpenFantasy.Core/Worlds/WorldMaterialCareArchiveSegment.cs)
retains the exact complete closing bytes. Its reference names world, branch, ordinal, closing world
revision and byte hash. The joint anchor retains qualified bounded material and care frontiers,
resource basis, source identity and dependent-owner context. It retains neither a parent root nor an
ancestor-anchor chain. Full historical evidence lives in stored archives and the journal.

The closing world revision and latest care-operation sequence are distinct. New causal effects belong
after the complete closing world boundary even when no care occurred near that boundary. Continuing
care preserves pending acknowledgment, permanent revocations and patient treatment witnesses.
Material, resource and care windows close together; completed causes cannot be replayed for another
restoration or disposition. Nested reservoirs and liquid owners bind the same actual Production and
inventory, with current topology and volumes preserved.

## Canonical decoding and authenticated retrieval

[WorldSnapshotCodecV27](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV27.cs) uses inline owners
before the first joint cut and explicit continued frames afterwards. A post-cut root requires its exact
joint anchor. An encoded binding or supplied hash alone is insufficient. Archive decoding checks
bounded stored bytes, scope, ordinal, hash and canonical resealing using the preceding verified anchor.
An iterative reader needs only the current anchor, rather than an in-memory history tree.

Structural decoding does not authenticate genesis or accepted history. The Host derives each expected
cut from replay of its actual journal and compares the actual retrieved archive bytes to the complete
prior replay root. Missing, substituted, unscheduled or trailing archive rows invalidate recovery,
including a request for a state or archive earlier than the damaged row.

An optional [canonical candidate frame](persistence-and-history.md#canonical-candidate-ownership)
carries already encoded bytes from preparation into durable publication. The archive wrapper retains
the frame of the exact continued candidate, never substitutes the closing root's frame, and does not
change archive decoding or authentication. The frame is transient serialization ownership; it is not
a trusted checkpoint or a second source of world state.

## Durable transaction and budget

[Schema eight](../../src/OpenFantasy.Hosting/Worlds/WorldDatabaseSchemaEight.cs) binds profile27 and
the explicit [consented interpretation](#consented-material-interpretation) to
`world_material_care_segments`. The separate resource archive table belongs to schema7/profile25–26.
Each joint row records scope, ordinal, boundary revision/hash, publication revision and exact closing
bytes. Revision N closes at publication N+1. Both scope/ordinal and publication are unique.

[SqliteWorldAuthority](../../src/OpenFantasy.Hosting/Worlds/SqliteWorldAuthority.cs) publishes the
commit row, archive, receipts, permanent identities, head and any scheduled checkpoint in one SQL
transaction. Storage matches the archive to the actual stored prior head, admitted genesis and
publishing commit. Preview writes none of them. An uncertain result invalidates the open authority;
reopening determines whether the whole transaction committed.

[WorldMaterialCareArchiveReplay](../../src/OpenFantasy.Hosting/Worlds/WorldMaterialCareArchiveReplay.cs)
checks every stored joint archive alongside full command replay. Historical reads and privileged
`ReadMaterialCareArchiveForHost` verify through the current head before returning detached evidence
with source-head pins. These synchronous operator reads do not belong on a presentation frame.

[WorldJournalPolicy](../../src/OpenFantasy.Hosting/Worlds/WorldJournalPolicy.cs) counts joint archive
bytes separately from accepted journal bytes. Its joint envelope is 1,024 archives/256 MiB; complete
states retain the 16 MiB bound and the existing command/journal limits remain unchanged. The two archive
families cannot be combined to increase a world's budget. Consistent backups preserve both the
required archive family and every other authoritative table.

## Extension boundaries

This interpretation composes the ordinary-care graph. It does not add separate structural-physics,
transport, settlement consequences or macro terrain-registry owners, change source-world roles, or
qualify unlimited storage and production throughput. Extending that graph requires complete admission,
matching nested codecs and evidence that every live writer preserves closure. The detached
[live material observations](live-material-evidence.md) are a separate diagnostic contract and do not
mint material rights or replace archive verification.


## Consented material interpretation

[World profile 28](consented-world.md) explicitly selects a coordinated owner with bilateral transfer
evidence at fresh admission. It uses the same closing-root and derived-anchor mechanism, retains
transfer evidence in the closing archive, and starts an empty segment list over actual imported
custodians. Transfer headroom joins the existing material/care/resource budget. Production selects
V11/context profile 9 and complete admission replays its produced-custody evidence; receipts and
permanent identities remain separate facts. Snapshot/step decoding explicitly selects this profile.

Schema eight stores both interpretations in the same physical layout: its joint archive table holds
bounded opaque closing roots with scope, ordinal, boundary and publication, none of which is specific
to one profile. The layout never selects an interpretation. Fresh creation names profiles 27 and 28
explicitly, and recovery requires a profile-27 or profile-28 genesis for this schema. Every replayed
step must carry the profile of the root it extends, this schema admits no profile migration, and the
recovered head must keep the interpretation of its stored genesis. Archive publication checks the accepted
genesis scope under either named profile, and journal accounting admits joint archives, and refuses
the separate resource archive family, for those two profiles only. No other or greater profile is
admitted, and opening never converts one interpretation into the other. A profile-27 save remains a
profile-27 save; a step encoded under the other profile fails dispatch during replay.

The durable binding is the privileged host-selected batch. It publishes both participants' receipts,
every permanent identity, the archive, head and scheduled checkpoint in the one transaction described
above. It is not authenticated dual-party ingress: see the
[consented world](consented-world.md#durable-host-binding) boundary.

## Saved proposal interpretation

[World profile 30](material-proposals.md) explicitly selects a coordinated owner that also holds
saved material proposals. It uses the same closing-root and derived-anchor mechanism. The closing
archive retains every open offer and every outcome of the segment. The successor carries every open
offer across the cut and starts with no outcomes, so a pending offer never disappears at a boundary.
Closed proposal, command and split-child identities stay occupied through the stored identity table.

The joint anchor of this interpretation also holds the bounded
[proposal continuation context](material-proposals.md#carry-across-a-cut): the exact offers open in
the closing owner. The anchor hash includes that context only when the closing world has the
capability, so anchors of profiles 27 and 28 keep their exact bytes. A successor must keep the
capability of its anchor, keep every carried offer exact as an open offer or a terminal outcome, and
date every other offer after the closing world sequence. The check runs when a continuation is
constructed and when a root is decoded against its actual anchor.

The cut reservation adds one proposal outcome for every open and newly created offer to the existing
material, care, resource and transfer headroom. It changes no earlier window, budget or schedule.

Schema eight stores this interpretation in the same physical layout. Fresh creation, archive
publication, journal accounting and recovery name profiles 27, 28 and 30; every other number,
including 29, is refused. A world keeps the interpretation of its stored genesis.
