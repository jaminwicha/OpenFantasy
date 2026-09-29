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

The Host must retain permanent identity occupancy across cuts and supply that lookup for continued
decisions. Clearing segment evidence never makes old command or child identities available again.

## Application boundaries

The Core transaction does not provide persisted offers, expiration, withdrawal, reconnect, transport
authentication or UI selection. These require explicit Host/application bindings. An asynchronous
offer's creation receipt and the later transfer's outcome are different events; accepting later must
not rewrite an earlier receipt. Native controls, durable Host recovery and migration of older worlds
require their own qualifications. The standalone ordered production profile is not interchangeable
with this coordinated-world interpretation.
