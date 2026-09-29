# Consented production transfer

How do two independently authenticated participants approve one exact gift of actual raw or
produced stock, and how does the production owner retain enough ordered evidence to validate the
gift, the recipient's later use and a further gift?

This page describes a standalone domain contract in the portable core. It is not a host binding, an
offer protocol, a user interface or a durable world transition. Related subjects:
[consented custody](consented-custody.md) for the raw-only contract and
[production continuation](production-continuation.md) for continued material windows.

## Responsibilities

| Type | Responsibility |
| --- | --- |
| [`ConsentedMaterialTerms`](../../src/OpenFantasy.Core/Authority/ConsentedMaterial/ConsentedMaterialTerms.cs) | The complete transaction both participants approve. |
| [`ConsentedMaterialCommand`](../../src/OpenFantasy.Core/Authority/ConsentedMaterial/ConsentedMaterialCommand.cs), [`ConsentedMaterialAcceptance`](../../src/OpenFantasy.Core/Authority/ConsentedMaterial/ConsentedMaterialAcceptance.cs) | The sender's and the recipient's immutable submitted claims. |
| [`ConsentedMaterialApproval`](../../src/OpenFantasy.Core/Authority/ConsentedMaterial/ConsentedMaterialApproval.cs) | Both claims, each beside the identity verified for its own delivery. |
| [`ConsentedMaterialCodecV1`](../../src/OpenFantasy.Core/Authority/ConsentedMaterial/ConsentedMaterialCodecV1.cs) | Canonical fixed-layout frames for the command and acceptance. |
| [`ConsentedMaterialTransferStep`](../../src/OpenFantasy.Core/Production/Continuation/ConsentedMaterialTransferStep.cs) | The single decision of one approval against actual owners. |
| [`ConsentedMaterialTransferPlanner`](../../src/OpenFantasy.Core/Production/Continuation/ConsentedMaterialTransferPlanner.cs), [`ConsentedMaterialTransferPlan`](../../src/OpenFantasy.Core/Production/Continuation/ConsentedMaterialTransferPlan.cs) | One complete candidate pair and its pinned application. |
| [`MaterialConsentedTransferOperation`](../../src/OpenFantasy.Core/Production/Continuation/MaterialOperation.cs) | The ordered evidence retained by the production owner. |
| [`ProductionCodecV10`](../../src/OpenFantasy.Core/Production/ProductionCodecV10.cs) | The explicit byte interpretation of consented ordered history. |

Inventory remains the only owner of raw lots and production the only owner of produced lots. No
type here holds stock of its own.

## Approval contract

Terms name the world and branch, both actors, the lot kind, the lot, the exact item definition
identity and version, both storages, the amount and the approved lot revision. The lot and both
storages are three different identities and the participants are two different actors.

Consent and execution freshness are separate. The approved lot revision is the only revision in the
terms: any spending, movement or split of that lot advances it, so consent to one interval never
spends a different surviving remainder. The production revision, the custody revision and both
storage revisions are execution state. The decision reads them from the actual current owners, so an
unrelated craft or an unrelated item entering or leaving either storage does not withdraw consent.
Current control, ownership, stock, protection, reservation and combined capacity still decide
whether the transaction is possible at that moment.

An approval is accepted only when all of the following hold for the current inventory:

- the acceptance pins the command identity, its complete content hash and restates equal terms;
- each participant's claimed principal and session equal the identity verified for that
  participant's own delivery, and that delivery currently controls the named actor;
- the sender owns the source storage and the recipient owns the destination storage.

Deliveries are never derived from claims and one participant's delivery never stands in for the
other's. Deposit policies are neither consulted nor changed: a recipient's participation in one
transfer is not a reusable deposit permission, and it is not consent to treatment or to any other
action. Each later transfer needs a new command and a new acceptance.

The raw consented custody contract is a separate contract with separate frame tags. Its values are
never converted into material approvals, and neither codec decodes the other's frames.

## Movement rules

The lot kind is an exact claim about existing stock. A produced approval addresses only a
`TransformedLot`; a counted-source approval addresses only a `CountedSourceLot`. A mismatched kind,
definition identity or definition version refuses the transfer.

Produced stock moves as a whole lot or as a prefix of its output interval:

- A whole transfer keeps the lot identity, output interval, definition and manual protection flags,
  changes the custodian and advances the lot revision.
- A partial transfer keeps the parent identity for the remaining suffix with an advanced revision
  and creates one child for the moved prefix. The child identity is derived from the command and the
  shared split ordinal, starts at revision zero and must not collide with the command, the
  acceptance or any occupied identity.
- The producing batch, output index and producer actor and storage never change, so later
  consumption or recrafting retires intervals of the original producer and the ancestry of a
  recrafted item reaches the gifted interval.
- Stock with a genuine reservation, including an installed construction output, and equipped stock
  is unavailable. Manual favorite, heirloom and user flags travel with the stock, as they do for raw
  transfer.

Raw stock moves through the shared `CustodyTransferPlanner`, so its established rules apply
unchanged and no source interval is issued or invented. Its private movement request carries the
approved lot revision and the custody and storage revisions read from the current owners. Valid
terms name three distinct revision owners in one branch, so the request is constructible for every
terms value and an absent or aliased lot is a refusal, never an exception.

Both kinds advance the custody revision and both storage revisions by one and occupy one ordered
operation. Storage capacity counts raw and produced contents together with wide arithmetic, and the
combined live-lot bound covers both owners.

Refusals distinguish missing authority, a stale lot revision and unavailable stock or identities.
Authority is decided first, before a full history window, stock or identity occupancy is reported.
A refusal returns the untouched original owners.

## Ordered evidence and replay

An applied transfer appends one `MaterialConsentedTransferOperation` holding the command, the
acceptance, both verified deliveries and the lot that actually changed custody. The operation's
sequence is the production revision it produced.

The command and the acceptance are separate permanent identities. Within a segment no command,
acceptance or created identity may equal another, and the production identity registry records the
acceptance role, both participants, both storages, the definition pin and the moved lot with the
role of its owner kind.

Validation against inventory replays the ordered operations from the captured frontier. Replay
calls the same decision step the planner used, with the inventory and produced lots reached at that
position. A recorded operation must be decided as applied and must reproduce the recorded moved lot;
the final replayed owners must equal the current ones. Recorded deliveries are historical claims of
a past submission. They are evidence for replay and never current authenticated ingress. A recorded
approval cannot be used again: its command and acceptance identities are permanently occupied and
its lot revision is already spent.

Decoding establishes structure only. Frames that decode cleanly are still refused by authority
validation when their approval does not bind, their delivery differs from their claims or their
recorded result differs from the decided one. Balanced custody edits are refused at that stage:
swapped custodians, a moved suffix instead of the prefix, and moved stock without its recorded
approval all conserve every unit, decode as structure and fail the ordered replay.

Existing provenance and partition rules are unchanged: output claims must partition each producing
budget exactly, and a custody change without its matching ordered operation fails replay.

## Custody partition evidence

[`ProducedCustodyPartition`](../../src/OpenFantasy.Core/Production/ProducedCustodyPartition.cs)
validates produced custody where no ordered material history exists. `ProductionSnapshot.ValidateCustodyEvidence`
takes ordered [`ProducedCustodyMovement`](../../src/OpenFantasy.Core/Production/ProducedCustodyMovement.cs)
values as explicit input and replays every imported or locally produced lot through its retirements
and movements in lot-revision order:

- a retirement spends the exact available prefix held by the custodian of that moment;
- a whole movement changes the custodian and advances the lot revision;
- a split creates one child for the moved prefix at revision zero and leaves the suffix with the
  parent, whose revision advances;
- reserved or equipped stock cannot move, and a movement must name the revision it actually spent;
- complete-world evidence must retain the actual item definition/version and exact producer/output prefix.

The live lots must equal the replayed partition exactly. Producer batch, output index and interval
never change, so recraft ancestry reaches the moved interval. Production neither stores nor hashes
the movements; their owner is the evidence owner described in [consented world](consented-world.md).

## Capability and versions

`ProductionContinuationContext.SupportsConsentedTransfer` is an explicit capability selected only by
`ProductionSnapshot.ContinueConsentedMaterial`. It requires ordered material history and excludes
the construction, terrain, reserved transport, excavation and complete-world interpretations. A
later ordinary cut retains it; no cut gains or drops it silently. The capability has its own context
profile number, so its context hash differs from every other interpretation, and its snapshot bytes
are `ProductionCodecV10`.

Every earlier context profile and snapshot codec keeps its exact bytes and rules. They refuse
consented history: their contexts reject the operation, their encoders reject the capability and
their decoders reject the version.

## Application

A decided candidate is execution state, not consent. `ConsentedMaterialTransferPlan.ApplyTo`
returns the candidate pair only when the supplied current inventory and production both equal the
owners that were decided. After any other change the same unchanged approval is decided again
against the actual current owners. The inventory hash covers
controls, access policies, capacities, reservations and lots; the production hash covers the
closing context with its recipes, the live lots and the ordered history. Any difference makes the
plan stale. A rejected plan has no candidate.

Changed control, policy, source or capacity admission is also refused when planning against a
continued segment, because the segment pins its admission.

## Boundaries and extension contracts

The core takes permanent identity occupancy as an input. A segment remembers only its own
identities and its imported frontier; identities closed by an earlier cut are known only to the
retained registry. A host binding owns that registry and its concurrency with a decision, the
visibility of rejected identities and receipts, offer lifetime and withdrawal, reconnect and
redelivery, spatial reach and durable commit.

The ordered profile on this page is a standalone interpretation. Its bytes and the material
approval frames are unpublished and may change. It is not the world binding.

The coordinated complete world is a separate interpretation. It keeps raw movement and work with
the world's owners and has no ordered production history. Its ordinary profile freezes produced
custody between retirements. Its explicit consented-world profile instead supplies mandatory
world-owned movement evidence, including the exact producer and moved prefix, to partition replay.
After a coordinated cut this selects `SupportsConsentedWorld`, context profile 9 and
`ProductionCodecV11`. The imported basis contains the actual surviving custodians; subsequent
movement is validated against the new segment's evidence.

Both coordinated interpretations require the production context hash to equal the world anchor.
A standalone consented ordered context is not complete-world and is refused by world admission.
[Consented world](consented-world.md) describes profile selection, paired publication, structural
admission, coordinated archives and the Host/application boundaries.
