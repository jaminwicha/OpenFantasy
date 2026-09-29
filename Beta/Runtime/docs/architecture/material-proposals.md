# Saved material proposals

A saved proposal lets a sender offer one exact lot interval now and lets the recipient decide later.
Creation, acceptance, decline and withdrawal are separate commands with separate receipts. The
proposal owner stores offers and their outcomes; inventory owns raw stock, production owns produced
stock, and care permission never implies inventory consent. See the [consented world](consented-world.md)
for the simultaneous pair and the shared material decision, and the
[coordinated continuation](coordinated-world-continuation.md) for cuts and the durable layout.

## Selection and ownership

[`WorldMaterialProposalAdmission.CreateFresh`](../../src/OpenFantasy.Core/Worlds/WorldMaterialProposalAdmission.cs)
selects the interpretation from an ordinary-care genesis through the complete coordinated admission.
It attaches an empty [`WorldMaterialProposalBook`](../../src/OpenFantasy.Core/Worlds/WorldMaterialProposalBook.cs)
and an empty consent evidence owner to
[`WorldCoordinatedContinuation`](../../src/OpenFantasy.Core/Worlds/WorldCoordinatedContinuation.cs).
Fresh admission never upgrades a lived world and no root is relabeled.

The interpretation selects world snapshot and step profile 30 and action codec 16. Earlier profiles
refuse the four intents and the profile bytes; their own bytes and rules are unchanged. Profile 30
does not interpret the simultaneous pair, so its consent evidence owner stays empty and exists only
to keep the consented production capability. Complete admission refuses pair evidence in this profile.

Profile numbers name interpretations and carry no order. No interpretation is numbered 29, and every
admission, codec and accounting join refuses that number as it refuses any other unnamed profile.

| Fact | Owner |
|---|---|
| Open offers and current-segment outcomes | `WorldMaterialProposalBook` in the complete world root |
| Raw and produced stock, reservations, capacity | Inventory and production, unchanged |
| Command outcomes | The durable command table, one immutable receipt per command |
| Permanent identity occupancy | The durable identity table, across cuts |
| Closed history | The journal and the joint archives |

## Identities and the saved grant

[`WorldMaterialProposal`](../../src/OpenFantasy.Core/Worlds/WorldMaterialProposal.cs) stores the sender's
submitted grant: the creation command identity, principal, origin session and exact terms. It also
stores the delivery recorded when creation was accepted, the creating world sequence and step, and the
absolute expiry step. The terms pin both participants, both storages, world and branch, the lot, its
kind and definition, the quantity and the approved lot revision.

Six identities stay distinct: the proposal, its creation command and that command's result, the
terminal command and its result, and a split child. The proposal identity and the split child are
both derived from the creation command under different ordinals, so a partial amount's child is pinned
at creation. Only a settlement creates and occupies the child.

The recorded delivery is evidence for one proposal. It is compared with current control when the
offer is settled. It never authenticates a caller, selects a command stream or controls an actor.
The shared [`ConsentedMaterialApproval`](../../src/OpenFantasy.Core/Authority/ConsentedMaterial/ConsentedMaterialApproval.cs)
therefore compares a recorded grant with the current binding; only the recipient's delivery belongs
to the settling step.

## Lifecycle

```text
create ──> Open ──accept (settled)──> Accepted
             ├──decline (recipient)──> Declined
             ├──withdraw (sender)────> Withdrawn
             └──step boundary────────> Expired
```

[`WorldMaterialProposalResolver`](../../src/OpenFantasy.Core/Worlds/WorldMaterialProposalResolver.cs)
decides each submission inside [`WorldStepProcessor`](../../src/OpenFantasy.Core/Worlds/WorldStepProcessor.cs).

All four submissions share one command contract, in this order: current control of the acting
actor; the actor's own `ExpectedActorRevision`; an advancing step without a hold and a ready actor;
and only then the proposal. A stale revision gives `RevisionConflict` for an existing and for a
missing proposal alike, as it does for ordinary actions and permission revocation. The conflict
concerns only the caller's own actor, changes no owner and is an ordinary elapsed step.

**Creation** requires the sender's own control, ownership of the source storage, a recipient who owns
the destination storage, a bounded expiry and actual current stock matching the pinned lot. It
reserves nothing: the lot stays free for any other use, and two offers may pin the same lot.

**Acceptance** rechecks, in order: the open offer and its named recipient; expiry; the exact restated
terms and grant hash; the sender's current control; both opportunities; the sender's observed
presence revision; both bodies; reach in both directions; and the shared material decision with current stock, reservation,
protection, capacity and identity occupancy. Unrelated activity in either bag does not invalidate
the offer. A changed lot revision refuses it, and nothing is retargeted or requoted.

The sender submits no command in the settling step. Its saved grant is valid only while the control
binding recorded at creation is still the source actor's current binding. The sender must have no
action of its own in that batch. A settlement consumes both participants' action and movement
opportunity; a custody movement leaves the production revision intact.

**A refused acceptance closes nothing.** The offer stays open until it is accepted, declined,
withdrawn or expired, and a later acceptance under a new command identity may settle it. Physical,
liquid and complete-root reconciliation failures roll back both material owners and the proposal
owner, and the receipt reports no success. Every refusal is an ordinary elapsed step.

**Decline** belongs to the recipient and **withdrawal** to the sender's creating principal. The first
authorized terminal outcome in authoritative order wins. Later, duplicate and reordered requests
cannot reopen an offer or move stock twice.

## Expiry

Expiry uses the world's advancing-step count and no wall clock. An offer created by the step that
publishes count `C` must name an expiry `E` with `C < E <= C + 65,536`.

Within one advancing step the order is fixed: every action in actor order, then the time boundary,
then expiry, then movement. An acceptance is admitted when the step starts at a count below `E`.
The step that publishes `E` still admits an acceptance among its actions; its boundary then closes
any offer still open as `Expired`, with no terminal command. A due offer is already unacceptable
wherever the count advanced, including tactical boundaries that do not interpret proposals; the next
exploration boundary records its outcome.

## Structural admission

[`WorldMaterialProposalAdmission`](../../src/OpenFantasy.Core/Worlds/WorldMaterialProposalAdmission.cs)
validates every root, including each candidate inside a step. It checks shape and time, and it
authenticates nothing.

| Record | Rule |
|---|---|
| Every proposal | Its creating step count does not exceed its creating world sequence, because one commit publishes at most one advancing step. |
| Open offer | It stays admitted when it is due or when its recorded grant is stale. |
| `Expired` outcome | The world's count has reached the absolute expiry step, and the outcome follows creation by at least one commit for every step of its lifetime. |
| `Accepted` outcome | The recorded grant and the recipient's delivery equal the admitted control bindings, as pair evidence requires. |
| Raw settlement | It names an admitted source and rights inside actual issuance. |

## Refusals and disclosure

A missing, closed, expired or foreign proposal gives the one refusal `Unavailable`. The Host boundary
refuses a delivery that does not control the acting actor before it selects anything. Inside a
privileged batch the same command receives `NotAuthorized`. The named recipient receives the
specific reason of a failed acceptance, as a participant of the pair does.

## Bounds

The owner holds at most 16 open offers and 64 outcomes per segment, inside a bounded frame. This is a
development envelope for a small hosted party. A full bound refuses the next creation or closure; it
never evicts. [`WorldMaterialCareContinuation`](../../src/OpenFantasy.Core/Worlds/WorldMaterialCareContinuation.cs)
reserves one outcome for every open and newly created offer plus its existing headroom, and closes
the complete root before a batch that would not fit. No earlier limit, budget or schedule changes.

A cut archives the closing root with every offer and outcome. The successor keeps every open offer
and starts with no outcomes. Closed identities stay occupied through the durable identity table.

## Carry across a cut

The coordinated anchor holds a
[`WorldMaterialProposalContinuationContext`](../../src/OpenFantasy.Core/Worlds/WorldMaterialProposalContinuationContext.cs):
the exact offers that were open in the actual closing owner, at most 16. Its presence is the
saved-proposal capability, and an empty roster still selects it. It holds no closing root, no
outcome and no earlier context. The anchor hash includes the roster hash only when the capability is
present, so an anchor of another interpretation keeps its exact bytes.

Every successor of that boundary obeys these rules, at construction and at decoding:

- The successor has the capability exactly when its anchor has it.
- Every carried offer appears once with its exact content, open or inside one terminal outcome.
- Any other offer was created after the closing world sequence.

A successor therefore cannot forget or change a carried grant, and cannot add a grant dated inside
the closed segment. An offer created after the boundary is admitted normally. The next cut replaces
the context with the roster of its own closing owner.

## Durable Host binding

[`SqliteWorldAuthority`](../../src/OpenFantasy.Hosting/Worlds/SqliteWorldAuthority.cs) names profile 30
in fresh creation, archive publication, journal accounting and recovery, within the shared
[schema eight](coordinated-world-continuation.md#durable-transaction-and-budget) layout. One SQL
transaction publishes the complete root with its proposal owner, the journal row, the command
receipt, every permanent identity, the closing archive when required, the head and any scheduled
checkpoint. Recovery replays every batch from the stored genesis and requires each stored row to
equal that replay; a root or receipt whose own hash is consistent is still refused when it differs.

[`SubmitMaterialProposalCommand`](../../src/OpenFantasy.Hosting/Worlds/SqliteWorldAuthority.MaterialProposals.cs)
holds one lock while it authorizes the supplied delivery, returns the original receipt of an exact
retry, selects the single-command advancing step and publishes it. A fresh command must originate in
the current session. A retry may keep its original origin after current authorization. Changed
content under a used identity is refused.

## Query boundary

[`MaterialProposalService`](../../src/OpenFantasy.Hosting/Simulation/MaterialProposalService.cs) is the
local application boundary. Every submission and query brings its own `TrustedCommandDelivery`.
A query first requires current control of the named actor, with the same refusal whether or not
anything exists. It then returns only proposals in which that actor takes part: identity, terms,
grant hash, expiry, state and the sender's observed presence revision. It returns no world root, no
other participant's command stream and no unrelated inventory.

| Query | Reads | Cost |
|---|---|---|
| `Find`, `List` | Open offers and outcomes of the current segment | The live root only |
| `FindRecorded` | One exact identity, including an outcome a cut archived | One complete verified replay when the live owner does not hold the identity |

`FindRecorded` gives both participants the terminal result after it leaves the live segment. The
journal and its archives stay the only authority: there is no proposal table, cache or second ledger.
Under the authority lock the lookup checks current control, consults the live owner, and otherwise
runs one complete verified replay that inspects every retrieved closing root for that identity. The
replay must end at the pinned live head. A missing identity and another pair's proposal both return
nothing. Damaged storage refuses the lookup, and the owner must be reopened.

The recorded lookup is synchronous non-frame work. It must not run on a client frame or a
latency-sensitive simulation callback. Its duration is not equalized between a live, an archived
and a missing identity. Archive retrieval by ordinal stays privileged operator work and is not a
participant query.

## Boundaries

An independently supplied local delivery is not a network handshake. The world admits no change of
an actor's control binding, so a saved grant is qualified across disposal and reopening with
unchanged control. Non-player decisions, native presentation, reserved offers, reciprocal exchange
and migration of older worlds require their own bindings. Production scale is a separate gate.
