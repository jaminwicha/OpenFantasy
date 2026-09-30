# Consented world material

A complete world can publish an exact raw or produced gift only when both participants submit
matching commands. Inventory owns raw stock, production owns produced stock, and the coordinated
world owns the evidence joining them. No consent object owns inventory or grants continuing access.
See [consented production](consented-production.md) for the shared movement and partition contracts.

## Selection and ownership

[`WorldConsentedMaterialAdmission.CreateFresh`](../../src/OpenFantasy.Core/Worlds/WorldConsentedMaterialAdmission.cs)
selects the interpretation from an ordinary-care genesis through the complete coordinated admission.
It hashes that admitted basis and attaches an empty
[`WorldConsentedMaterialHistory`](../../src/OpenFantasy.Core/Worlds/WorldConsentedMaterialHistory.cs)
to [`WorldCoordinatedContinuation`](../../src/OpenFantasy.Core/Worlds/WorldCoordinatedContinuation.cs).
The same owner accompanies every candidate root. The fresh-admission API cannot upgrade a lived
world. Structural decoding alone does not authenticate that choice: before a cut, older and newer
interpretations share several owner frames. The Host must pin the actual admitted genesis and
interpretation and replay its authorized transitions; relabeling a root is not a migration.

The interpretation selects world snapshot and step profile 28. The snapshot stores the evidence
beside the coordinated source baseline and archive anchor; the step permits the two intents in an
advancing batch. Earlier profiles refuse the intents and profile bytes. Action codec 15 provides
the distinct canonical encoding for these submissions.

## Two submissions and one transaction

The sender's `OfferMaterialIntent` contains the exact material terms and observed recipient presence
revision. The recipient's `AcceptMaterialIntent` pins the offer command identity and material-claim
hash, repeats the terms, and records the observed sender presence revision. The terms identify the
participants, their storages, world/branch, actual lot and item definition, amount and lot revision.
Unrelated activity in either bag does not invalidate consent; the exact approved lot must remain
current. Each actor has its own command and terminal receipt.

[`WorldMaterialPairing`](../../src/OpenFantasy.Core/Worlds/WorldMaterialPairing.cs) matches commands
actually present in the privileged Core batch. Naming a recipient in an offer does not submit an
acceptance. A matched acceptance defers to the offer's action position, even when it appears first.
The world resolves the pair once at that position. Receipts retain the existing canonical ordering
by command identity, independently of action order.

Core batches are privileged host-selected inputs, also used in trusted journal replay. They do not
authenticate transport. A Host or offer service must independently authenticate both submissions
before selecting them into a batch. The public standalone planner therefore requires explicit
`TrustedCommandDelivery` values for both parties; it never treats supplied claims as authentication.
Historical recorded deliveries describe evidence and cannot authenticate a current request.

## Decision and publication

[`WorldConsentedMaterialPlanner`](../../src/OpenFantasy.Core/Worlds/WorldConsentedMaterialPlanner.cs)
checks these constraints in order:

1. Both submitted claims match their respective verified delivery and current actor control.
2. World, branch, participants, storage ownership and the exact bilateral approval match.
3. The step advances and has no reading hold.
4. Both residents and all observed presence revisions remain current.
5. Both actors have an action opportunity and manipulation-capable bodies.
6. Interaction is reachable in both directions.
7. The shared material decision accepts current stock, reservation/protection, combined capacity,
   live-lot bounds and permanent identity availability.

Missing control gives that participant `NotAuthorized` and reveals only `Unavailable` to the other.
A paired refusal changes neither material owner and creates no transfer evidence.

[`WorldStepProcessor`](../../src/OpenFantasy.Core/Worlds/WorldStepProcessor.cs) reserves both command
identities before interpretation. The material occupancy check excludes only those two reservations;
it still sees every other current or historical identity, including a proposed split child.
The processor appends successful evidence to the candidate coordination owner, reconciles physical
and liquid owners, and validates the complete root. Any failed reconciliation or complete admission
rolls back both material owners, evidence and coupled effects, with neither receipt reporting success.
A successful gift consumes both participants' action and movement opportunity. Custody and both bag
revisions advance; a custody movement is not a craft/use and leaves the production revision intact.
The reservoir join has a separate internal custody-reconciliation path, selected only with evidence
whose before/after hashes match the actual material owners. That path retains the exact craft/use
revision, recipes, context and history. Public ordinary reconciliation still requires advancing
production. Both paths retain receiving-volume, occupancy and complete-world validation.

`WorldConsentedMaterialPlan.ApplyTo` pins the inventory and production owners used in a diagnostic
decision. It is not a world publication API: complete root admission and publication belong to the
processor, which also handles space, time, opportunities, identities and receipts.

## Evidence and validation

[`WorldConsentedTransferEvidence`](../../src/OpenFantasy.Core/Worlds/WorldConsentedTransferEvidence.cs)
records world sequence, both action positions, both approvals/deliveries, moved lot identity, rights
owner and exact moved interval, and before/after inventory and production hashes. The dependency is
acyclic: material owners feed evidence, evidence feeds the world root, and the root feeds the journal.
Evidence never includes the world root or publication hash that covers it.

History holds at most 256 transfers and has bounded record/frame sizes. Records follow world sequence
and offer position; both action positions are unique within a sequence. Participants ordered by those
positions must form a strictly increasing subsequence of the batch's unique actor order. Positions may
have gaps for other actions, including refused submissions; an actor cannot participate twice in one
sequence. Offer, acceptance and newly
split child identities cannot be reused. World admission adds these to the typed identity index,
including retired children, and checks scope, admitted participants and segment chronology.

Raw evidence must reference its admitted source and definition, remain within source capacity, and
stay below the retained issuance cursor when gathering owns that source. Exact historical raw-lot
identity and before/after owner hashes still require journal replay. Produced
evidence supplies the exact approved item definition/version and producer/output/prefix metadata to `ValidateCustodyEvidence`. The production
partition replays moves with retirements and requires the resulting live lots to match exactly.
Conserving total quantity alone does not permit moving a suffix, substituting a producer, changing a
custodian without evidence, or moving an installed/reserved output.

Structural snapshot admission is distinct from authenticated historical replay. Recorded owner
hashes and raw historical operations require the journal to reproduce actual commands and states;
a decoder does not establish that those commands were submitted or durably committed.

## Coordinated continuation

The new world interpretation uses `SupportsConsentedWorld`, production context profile 9 and
`ProductionCodecV11` after a coordinated cut. It keeps the complete-world owners and has no separate
ordered production history. This capability replaces fixed produced-custodian assumptions with the
world's mandatory custody-evidence validation. The older V9 production codec refuses it, and cuts
cannot gain or drop the capability.

[`WorldMaterialCareContinuation`](../../src/OpenFantasy.Core/Worlds/WorldMaterialCareContinuation.cs)
reserves all offers in the next batch plus its existing safety headroom against the transfer window.
It seals the complete closing root before interpretation when any coordinated window requires a
cut. That root retains its evidence. The successor imports actual surviving stock as its production
basis and starts an empty segment history. Decoding a successor requires the matching anchor derived
from decoded closing archive bytes. Repeating a cut preserves the capability and material ancestry.

The Host retains permanent identity occupancy across cuts in its stored identity table and supplies
that lookup for continued decisions and for replay. Clearing segment evidence never makes old command
or child identities available again.

## Durable Host binding

[`SqliteWorldAuthority`](../../src/OpenFantasy.Hosting/Worlds/SqliteWorldAuthority.cs) admits a fresh
profile-28 genesis by name and stores it in
[schema eight](coordinated-world-continuation.md#durable-transaction-and-budget), whose layout it
shares with profiles 27 and 30. A profile-28 world keeps the interpretation of its stored genesis;
it has no admitted migration. Recovery refuses an unnamed genesis profile, a replayed step whose
profile differs from its root and any unauthenticated interpretation change. The separate
[proposal transition](coordinated-world-continuation.md#explicit-proposal-transition) admits only
27→30 through a typed journal commit. It neither adds nor combines profile 28's simultaneous pairs.
Opening with an expected genesis compares exact canonical bytes after complete recovery. Opening
never migrates or falls back between interpretations, and numeric ordering admits no profile.

One SQL transaction publishes the complete candidate root, the journal row, both participants'
command receipts, every permanent identity, the closing archive when the batch required a cut, the
head and any scheduled checkpoint. Permanent identities are the commit cause, both command
identities, both receipt result identities and a split child. A refused pair still publishes its
cause, both commands and both terminal receipts; it publishes no child. Loss before the commit leaves
none of these rows; loss after it leaves all of them. An uncertain outcome invalidates the open
authority, and reopening decides by complete replay.

Recovery replays every accepted batch from the stored genesis and requires the journal, each archive,
the command table, the identity table, every checkpoint and the head to equal that replay exactly.
A stored archive whose bytes match its own recomputed hash is still refused unless it is the replayed
closing root at that boundary. Historical state and archive selection verify through the current head
first, so later damage also blocks reading an earlier archive. Identity occupancy is permanent across
cuts: a closed offer, acceptance, retired split child or refused command can never name a new command,
and a replayed batch is a stale dispatch that moves nothing. The journal, state and archive bounds of
the [coordinated world](coordinated-world-continuation.md#durable-transaction-and-budget) apply unchanged.

`Commit` is privileged. It receives batches the Host already selected and reconstructs each
submitted delivery claim from the command itself in order to check current control. That consistency
check is also what trusted replay performs; it does not authenticate either participant.
`FindReceipt` is the local authorization boundary. It compares an independently supplied current
delivery with the command's principal and the actor's current control before it reads any stored
outcome, so a wrong delivery receives the same refusal whether or not an outcome exists. The stream
key is principal plus command identity, with current control checked for the command's actor.
Control of one participant does not grant access to another participant's stream. A principal that
already controls both actors may access both authorized streams; their separate transfer approvals
are still required. Naming another principal's command identity does not change the selected stream.

## Saved offers are a separate interpretation

The paired intents are never persisted offers, and profile 28 stores none. An offer that is created
now and decided later belongs to the [saved material proposal](material-proposals.md) interpretation,
which selects its own profile, action codec, typed owner and receipts at fresh admission. That
interpretation reuses the shared material decision and the control, presence, body, reach and
reconciliation rules described here. It does not interpret the paired intents, and a profile-28
world never gains a proposal owner.

Both interpretations use `ConsentedMaterialApproval`. The pair supplies two deliveries verified for
the current step. A saved proposal supplies the sender's delivery recorded at creation, which is
compared with the current control binding and never treated as a newly authenticated caller. A
coordinated anchor without the saved-proposal capability refuses a successor that carries a
proposal owner, and an anchor with it refuses a successor without one.

## Application boundaries

The durable binding does not provide public paired ingress. A service that accepts submissions from
two parties must verify both actual deliveries before it selects a batch, reports availability or
discloses a prior outcome. Identifiers copied into a payload, or the recipient named by an offer,
supply neither delivery. Transport authentication and UI selection require explicit
Host/application bindings. An asynchronous offer's creation receipt and the later transfer's outcome
are different events; accepting later never rewrites an earlier receipt. Native controls, non-player decisions, production history scale and
migration of older worlds require their own qualifications. Fresh admission never upgrades or
replaces a saved world of another interpretation. The standalone ordered production profile is not
interchangeable with this coordinated-world interpretation.
