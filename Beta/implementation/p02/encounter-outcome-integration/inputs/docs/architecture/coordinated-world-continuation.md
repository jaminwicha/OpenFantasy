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
to one profile. The layout never selects an interpretation. Fresh creation and recovery explicitly
name profiles 27, 28, 30 and 32. Every replayed
step must carry the profile of the root it extends. A profile-28 world retains its genesis-selected
interpretation; the closed [proposal transition](#explicit-proposal-transition) is the only admitted
change, from profile 27 to profile 30. Archive publication checks the admitted genesis scope, and
journal accounting admits joint archives and refuses the separate resource archive family under the
four explicitly named profiles. No greater or intermediate profile is admitted by numeric range.
Opening never converts interpretations, and a step encoded under another profile fails replay.

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
publication, journal accounting and recovery name profiles 27, 28, 30 and 32; every other number,
including 29 and 31, is refused. A world keeps the interpretation of its stored genesis, except through the
one explicit transition below.

## Paid interpretation

[Profile 32](world-paid-exchange.md) adds a paid quote owner beside the saved proposals. The anchor
carries its exact open quotes and issuer market revisions. The successor preserves each carried
quote, either open or terminal, and dates other quotes after the boundary. A fill must use the
issuer revision at its own causal position; the issuer's final revision equals the carried basis
plus successor fills. A fill closes its issuer's stale siblings as superseded in the same transaction;
an issuer withdrawal records its own authenticated command without payment. Both terminal kinds
remain exact across cuts, with their causes and identities retained. Closed outcomes remain in the
actual closing root. The cut reservation counts
every open and newly issued quote's potential outcome as well as the existing owners' headroom.

The paid context hash joins the anchor only when that capability exists. Earlier anchor bytes are
unchanged. Progression at the boundary already includes previous payments, so successor replay adds
only its own fills. Receipts and quote/command/split identities remain durable across cuts. Profile 32
has fresh admission and ordinary continuation; the explicit transition below does not target it.

## Market continuation

The [market world](world-markets.md) adds `WorldMarketContinuationContext` to the same coordinated
anchor. It carries immutable market bindings, the pricing records of still-open quotes, all actor
price memories and selected-lot disclosures with their original causes. Inventory and progression
remain the only stock and money owners; continuation does not recalculate prices or refresh knowledge.

Carried price memories remain exact or are replaced by later recorded observations. A seller can
withdraw or replace its own lot disclosure through an explicit command. Quote records follow the
paid owner's lifecycle, so a carried live quote may settle after a cut while retaining its original
creation. Closed records remain in their segment's archived root. Structural checks constrain the
carried state; authenticated replay establishes the actual commands and their order.

The market context hash is appended only for this capability. Earlier anchors retain their exact
bytes, and reopening another profile cannot silently enable markets or disclosures.

## Explicit proposal transition

A lived profile-27 world can continue under profile 30 only through an explicit, privileged upgrade
into a new save. Opening, reading, fresh admission and ordinary cuts never select it.

| Part | Owner |
|---|---|
| Exact request and closed wire contract | [WorldProposalTransition](../../src/OpenFantasy.Core/Worlds/WorldProposalTransition.cs), [WorldProposalTransitionCodecV1](../../src/OpenFantasy.Core/Worlds/WorldProposalTransitionCodecV1.cs) |
| Interpretation of the source head | [WorldProposalTransitionProcessor](../../src/OpenFantasy.Core/Worlds/WorldProposalTransitionProcessor.cs) |
| Transition cut and bounded context | `WorldMaterialCareArchiveSegment.SealTransition` / `DecodeTransition` and the anchor's `TransitionHash` |
| Copy, append, replay and publication | [WorldProposalTransitionUpgrade](../../src/OpenFantasy.Hosting/Worlds/WorldProposalTransitionUpgrade.cs) |
| Recovery dispatch | [WorldProposalTransitionReplay](../../src/OpenFantasy.Hosting/Worlds/WorldProposalTransitionReplay.cs) |

The request frame is tagged `world.proposal-transition`, version 1, with thirteen fields: tag,
version, source profile `27`, target profile `30`, cause, world, branch, source revision, source
advancing steps, source head hash, source admission version, the head's latest joint anchor hash
(empty when the world never cut) and the ordinal of the closing archive. Any other profile pair,
version, field count, digest length or non-canonical byte fails. The profile fields are exact names,
not lower bounds; no other profile number is reserved or admitted by this frame.

The processor admits only a profile-27 head with no consent or proposal owner and no pending interval,
and only when revision, steps, head hash, admission version, anchor hash, ordinal and scope all equal
the request. A cause that the root, the genesis or the durable identity table already holds is
refused. It then seals the exact source head as an ordinary joint archive and rebases it once:

- Production adopts the consented complete-world context from its exact prior context. This is the
  sole path across the consent guard of `ProductionContinuationContext`; ordinary cuts still refuse to
  gain or drop consent.
- The anchor selects consent and an empty saved-proposal roster, and appends the request digest to its
  hash fields. Anchors without a transition keep their exact bytes and rules.
- The successor owns empty consent evidence and an empty proposal book, so no grant, offer or stock is
  created. Inventory, controls, bodies, resources, care, permissions, quests, calendar, readiness and
  advancing steps stay exact. The world commit revision advances once; the encounter lifecycle's
  mirror revision advances through `RetainBoundary()` without advancing its phase, clocks or debts.

The source admission version remains the pin for unchanged admitted content and dependent owners.
The exact target profile and canonical transition request select the new rule interpretation; a
replaced admission hash is neither necessary nor an alternative migration credential.

The transition context is the request digest in the anchor. It retains no source root, ancestor anchor
or completed-use list. Every later root binds that anchor by hash, so a successor cannot be decoded
against an ordinary anchor or a forged request. The next ordinary cut derives an ordinary profile-30
anchor with no transition field.

Archive retrieval supplies the request from the replayed journal commit, never from the archive row.
`DecodeTransition` requires the archived root to be the named profile-27 head under its preceding
anchor, with matching admission, steps, anchor and ordinal, and must reproduce the replayed anchor.
Recovery admits a head whose interpretation differs from its genesis only when the genesis is profile
27, the head is profile 30 and exactly one transition commit was replayed.

The durable commit is kind 3 in the existing schema-eight layout: one transaction appends the commit,
the archive closing the stored prior head, the cause identity, the new head and any scheduled
checkpoint. No table, index or physical schema changes. Journal accounting counts the archive under
the source profile; later commits use profile 30.

Copying and opening stay separate. The privileged upgrade writes only its unused destination; the
[retained native selection](material-proposals.md#retained-world-native-selection) later opens that
destination with the original profile-27 genesis bytes and the named profile-30 target, relying on
the recovery rule above. Neither the client nor ordinary opening ever performs, repeats or infers a
transition, and the original genesis and every lived row remain the history of the continued world.
After the transition, ordinary cuts close segments exactly as before, including an offer still open
at the closing boundary.
