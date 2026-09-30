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
independently delivered local application boundary. Every submission and query brings its own `TrustedCommandDelivery`.
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

## Local session selection

[`LocalWorldSession`](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs) accepts the four
proposal intents through its existing controlled-player queue. Current delivery authorization and
exact completed-receipt lookup precede new selection. A fresh command requires profile30, ready
unheld exploration, no pending excavation and no pending care obligation. Context refusal occurs
before queueing; stale admissible commands still receive their ordinary durable gameplay outcome.
Identical pending bytes share one result, and stopping cancels input that has not been selected.

The session selects the command at its normal cutoff, alongside movement and optional NPC work,
then publishes one complete batch. It does not call the independently advancing proposal service
inside its tick. Paused receipt lookup, inspection and cancelled input create no advancing step.
An admitted advancing batch has its ordinary elapsed interval even when an action is refused.

[`LocalMaterialProposalSelection`](../../src/OpenFantasy.Hosting/Simulation/LocalMaterialProposalSelection.cs)
protects a valid bilateral acceptance from the sender's optional construction or ordinary aid. It
evaluates that captured acceptance through the authority's pure complete-world preview before
asking for competing NPC work. Only a successful preview excludes the sender from those choices.
This excludes a work opportunity, not stock: offers still reserve nothing. A stale, foreign,
unreachable or otherwise invalid acceptance cannot suppress the sender's independent work. The
complete selected batch revalidates the request; the preview grants no authority or durable outcome.
Successful settlement itself spends both participants' movement and action opportunities in Core.

[`LocalMaterialProposalView`](../../src/OpenFantasy.Hosting/Simulation/LocalMaterialProposalView.cs),
exposed as `LocalWorldView.MaterialProposals`, reuses the service's participant filter against the
live owner. It carries the controlled player's open and current-segment terminal proposals, including
captured grant, exact terms and observed sender revision. Projection performs no submission or
historical replay. A captured acceptance is never refreshed or retargeted while waiting in the queue.
Archived inspection uses the explicit background session query described below, or the independently
delivered service outside frame and simulation callbacks.

Profile30 also exposes [ordinary restorative controls](ordinary-care.md) through the same session.
An accepted dose enters the recipient's actual owned inventory; spending it requires a separate
selected self-use command and a later cutoff. Acceptance itself restores no HP and grants no care
permission. Ordinary helper aid retains its own explicit permission and selection policy.

## Captured creation choices

`LocalMaterialProposalView.Preparation` carries a
[`LocalMaterialProposalPreparation`](../../src/OpenFantasy.Hosting/Simulation/LocalMaterialProposalPreparation.cs)
only at an admitted unheld exploration boundary with room for another offer. It lists exact available
raw and produced lots from the player's own primary storage, excluding protected and reserved stock,
and named resident recipients with owned primary storages. It retains immutable item definition pins
and the addressing needed to form terms, but no world root, other inventory or controller credentials.
Separate lot and recipient lists avoid materializing their cross-product.

`Prepare` requires one explicit lot kind/identity, recipient and positive quantity no greater than
that captured lot. A missing or incompatible selection yields nothing; no first-row, nearest-actor or
same-item substitute is selected. The result pins scope, both parties and storages, full item version,
lot revision and quantity. Its expiry is the captured advancing count plus the creation step plus
1,200 steps, approximately one nominal minute at the native 20 cutoffs per second. Holds spend no
advancing lifetime. Refreshing the public view does not change an earlier preparation or intent. A client retains
that captured intent through confirmation; explicit preparation is required to change terms or expiry.
`CanStillPropose` compares complete current terms and the original expiry without renewing it.
It is bounded presentation advice; it neither publishes nor bypasses Core admission.

Preparation neither reserves stock nor checks every eventual settlement condition. It is readable
while the session is paused, but submitting a fresh action still requires readiness and normal queue
admission. Actual Core creation rechecks the current pin and expiry; a later settlement also rechecks
both bodies, opportunities, reach and capacity. Acceptance remains separate from use or treatment.

## Explicit session history inspection

[`InspectMaterialProposalAsync`](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.ProposalInspection.cs)
accepts one exact identity and uses only the session's fixed player and trusted delivery. It always
acquires the session serialization boundary asynchronously, then runs the durable query on the
background task scheduler, even when slot acquisition completed synchronously. The query uses the
same current-control and participant filter as the service. It is available
during paused inspection and reading holds, creates no receipt or advancing step, and never runs from
`TickAsync`, `ProjectView` or a render callback.

One inspection may be outstanding. A request for that same identity shares the pending task; a
different identity is refused until it finishes. Completed results are not cached and failures are not
automatically retried. Missing and foreign proposals both return nothing. A storage or replay failure
faults the inspection and session, clears input and requires reopening; a control refusal affects only
the query. Stopping cancels queued work, waits for a selected read, and then disposes the durable owner.

Background execution protects the caller's frame, not simulation throughput: a verified archive replay
holds the serialized authority and delays subsequent ticks or submissions. Missed cutoffs are discarded
under the ordinary session clock policy; no catch-up steps are synthesized. Inspection is an explicit
user operation, never an automatic per-frame refresh. A responsive production history index requires
its own authority, invalidation and latency contract.

## Excavation composition

The session admits profile30's ordinary `ExcavateIntent` through its existing cutoff and complete
terrain/liquid/material publication. Offers and proposal occupancy stay in the same root. This is
synchronous terrain-bound excavation, not a deferred structural-consequence interpretation.

The rooted coordinated graph requires base profile21 with fixed reservoir seals. Complete admission
excludes structural consequence policy and pending excavation from that graph. Actual deferred
excavation belongs to profiles16–18, whose pending cursor also checks that exact family; those sessions
refuse saved-proposal input without disturbing the retained cursor. Neither capability is inferred
from a numerically greater profile. Combining them requires an explicit complete-world composition.

## Explicit NPC gift preferences

[`LocalMaterialGiftPolicy`](../../src/OpenFantasy.Hosting/Simulation/LocalMaterialGiftPolicy.cs)
configures one Host actor, one named partner and an exact self-use definition/version. The session
copies policies into a bounded immutable actor-ordered collection and rejects duplicate actors,
missing participants, player-controlled givers and mismatched definitions. Constructors without a
policy collection retain their behavior. Configuration is Host advice, not saved world state: the
host supplies it again when opening a session. Submitted decisions and outcomes live in ordinary
world history; there is no private persistent decision ledger or inferred policy from friendship.

[`LocalMaterialGiftController`](../../src/OpenFantasy.Hosting/Simulation/LocalMaterialGiftController.cs)
uses the configured NPC's current control binding. Its preference order is:

1. When answering is enabled, choose the first non-due incoming offer from that partner in proposal
   identity order. Accept only the exact remedy item for a supported body and within the configured
   desired stock count; otherwise decline. Acceptance still undergoes complete authority validation.
2. When giving is enabled and no outgoing offer to that partner remains open, consider one owned
   dose for a living partner whose missing HP can absorb the whole remedy. Both bodies must support
   the interaction, actual reach must hold in both directions, and available stock must exceed the
   configured reserve. The defaults retain one dose, seek at most three when receiving, and give
   offers 64 advancing steps of life. Raw stock precedes produced stock, then lot identity; both
   queries exclude reserved and protected lots. The offer pins exactly one unit of the chosen lot.

An offers-only policy does not answer or wait on an incoming offer. An answering policy prioritizes
its preferred incoming decision, including an occupied/refused decision; it does not skip to another
offer, lot or partner as a hidden retry. An open gift reserves no stock. The configured giver defers
automatic ordinary assistance while awaiting that partner's answer, so an unselected gift does not
immediately become direct treatment. This is a scheduling preference, not revoked care permission;
construction, movement and other authorized uses of the item remain independent. Ordinary assistance
can be selected again after closure or expiry under its unchanged permission and eligibility checks.
Deferral is checked only against the supplying helper. It does not prevent the giver from receiving
someone else's permitted aid, and is separate from whole-actor action-opportunity exclusions.

Gift decisions are considered before optional construction and ordinary aid, after the player's
selection and any valid bilateral reservation. A proposal or decline consumes only its own actor's
action; another actor's queued request cannot suppress that declaration. Acceptance also requires
the sender's opportunity. Only a successful complete preview reserves that sender from later work
or gift choices. The final selected batch remains the sole authority for all effects.

[`LocalMaterialGiftIdentity`](../../src/OpenFantasy.Hosting/Simulation/LocalMaterialGiftIdentity.cs)
derives two stable semantic keys. An outgoing decision includes world/branch, both actors, the pinned
self-use definition, preferred lot identity/kind and the partner's current/maximum HP. An answer
includes world/branch, both actors and the proposal identity. Neither key includes elapsed time,
movement, global resource/storage revision, absolute expiry, or a refreshed lot/presence revision.
The command still captures the exact current revisions required by authority.

Permanent identity occupancy suppresses an unchanged accepted, declined, expired or refused choice
after segment rotation and reopening. It also suppresses an answer whose current preference would
switch between acceptance and decline. A changed HP pair, pinned use definition or preferred item
identity can select a distinct outgoing decision. Returning to an identical earlier key does not
authorize a retry; repeated help for a new episode requires its own explicit policy contract.
Updating an observation or reopening the same save cannot manufacture a new decision identity.

## Boundaries

An independently supplied local delivery is not a network handshake. The world admits no change of
an actor's control binding, so a saved grant is qualified across disposal and reopening with
unchanged control. The explicit local medicine policy is not a general NPC motivation or knowledge
system. The native binding below exposes this bounded profile; reserved offers, reciprocal exchange
and migration of older worlds require their own bindings. Production scale is a separate gate.

## Native selection and commitment

[`FarmClientSession`](../../client/scripts/FarmClientSession.cs) selects a separate saved proposal
world with `--gifts`, using a fresh admitted ordinary-care genesis when that dedicated save is absent.
Opening an existing selected save authenticates its own genesis; no earlier profile is silently
converted. The binding explicitly configures Wren's medicine preference for the controlled player,
including 1,200 advancing steps of offer life, while the generic policy default remains 64.

[`FarmGiftSelection`](../../client/scripts/FarmGiftSelection.cs) owns only transient explicit choices:
physical stack kind/identity, recipient, positive integer quantity, prepared intent and selected
participant offer. The pack uses public projections; it neither enumerates private inventories nor
submits commands. Missing or spent choices remain missing, without same-item fallback. A draft
remains fixed across frame updates until the player deliberately prepares again.

[`MaterialProposalActionOffers`](../../client/scripts/MaterialProposalActionOffers.cs) maps those
choices into four independent semantic actions. The Gifts wheel follows Actions and any available
Care page, keeping create/accept/decline/withdraw in stable cardinal positions. Opening or paging
selects nothing. A selected wheel pins the complete intent and acting actor revision; acceptance
also pins the sender observation. Sender movement alone does not invalidate decline or withdrawal.
A changed or terminal offer is inspectable with a blocker, never automatically recaptured.

[`FarmGiftPanel`](../../client/scripts/FarmGiftPanel.cs) keeps dropdown row identities stable while
a popup is visible, then validates the selected identity against the latest public view. Explicit
reselection of the same offer refreshes its observed terms. Only separate wheel confirmation can
submit through the ordinary authenticated session cutoff. Pause, dialogue, pending action and focus
ownership retain their usual rules. The pack's explicit history button starts the bounded background
query; frames only observe completion, and results apply only to the still-selected identity.
