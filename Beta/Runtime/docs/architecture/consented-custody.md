# Consented custody

## Domain contract

A consented transfer moves an exact amount of one raw counted lot from a storage its sender owns
into a storage its recipient owns, only when both current participants approve the same complete
transaction. It is the bilateral counterpart of the ordinary unilateral transfer, not a relaxation
of it. [CountedTransferAuthority](../../src/OpenFantasy.Core/Authority/CountedTransferAuthority.cs)
keeps its rules: a sender alone cannot deposit into another actor's `OwnerOnly` storage.
[ConsentedTransferAuthority](../../src/OpenFantasy.Core/Authority/ConsentedCustody/ConsentedTransferAuthority.cs)
is the single owner of the bilateral decision. It reads and returns the same
[CountedAuthorityState](../../src/OpenFantasy.Core/Authority/CountedAuthorityState.cs); it owns no
second inventory, and access policies pass through every candidate unchanged.

The recipient's approval is participation in one transaction. It is not a deposit rule, a reusable
permission, a boolean flag or a [care permission](ordinary-care.md). Treatment consent authorizes
no gift, and a gift acceptance authorizes no treatment.

## Approvals and binding

| Value | Meaning |
| --- | --- |
| [ConsentedTransferTerms](../../src/OpenFantasy.Core/Authority/ConsentedCustody/ConsentedTransferTerms.cs) | World, branch, sender and recipient actors, lot, source and destination storages, amount, and the expected branch, lot, source and destination revisions |
| [ConsentedTransferCommand](../../src/OpenFantasy.Core/Authority/ConsentedCustody/ConsentedTransferCommand.cs) | The sender's command identity, claimed principal and origin session, and the terms |
| [ConsentedTransferAcceptance](../../src/OpenFantasy.Core/Authority/ConsentedCustody/ConsentedTransferAcceptance.cs) | The recipient's own identity and claims, the sender command identity and content hash, and the restated terms |
| [DeliveredTransferCommand](../../src/OpenFantasy.Core/Authority/ConsentedCustody/DeliveredTransferCommand.cs), [DeliveredTransferAcceptance](../../src/OpenFantasy.Core/Authority/ConsentedCustody/DeliveredTransferAcceptance.cs) | Each submitted value paired with the [TrustedCommandDelivery](../../src/OpenFantasy.Core/Authority/TrustedCommandDelivery.cs) the host verified for it |

Terms name exactly four revision owners, so an approval cannot probe unrelated objects. They require
a positive amount, two different participants, and three different lot/storage identities.

An acceptance binds a command when the command identity, the hash of the complete canonical command
and every restated term are equal. A matching hash with different terms, or matching terms with a
different hash, approves nothing. Changing the amount, destination, any revision, the command
identity or the sender's claims produces a different transaction that needs its own acceptance.

Submitted identifiers are claims. Each side's claimed principal and session must equal its own
host-verified delivery, and that delivery must currently control the named actor. The authority
never derives a delivery from submitted data, and one participant's delivery never stands in for
the other's. Players and NPCs use this same control contract; a controller kind earns no exception.
One controller that currently controls both actors still supplies both exact approvals.

## Decision

`Decide` evaluates one state, both delivered approvals, the next commit identity and a
complete-history occupancy callback, in this order:

1. Dispatch preconditions: the command and commit select this world; otherwise the call is invalid.
2. Authorization (`NotAuthorized`): branch, binding, both participants' current control, the
   sender's ownership of the source storage and the recipient's ownership of the destination.
3. Availability (`ResourceUnavailable`): identity freshness, revisions, stock, protection,
   reservation, capacity, lot-count bound and arithmetic overflow.

Movement reuses [CustodyTransferPlanner](../../src/OpenFantasy.Core/Custody/CustodyTransferPlanner.cs).
A partial amount takes the prefix of the lot's source interval into a child whose identity derives
from the sender command; the remainder keeps the original identity with an advanced revision. A
whole lot keeps its identity and interval. Both storages and the branch advance one revision.
Availability failures share one public reason and reveal no private detail.
The shared manual movement rules block equipped lots and actual reservations (`ReservedBy`).
Other flags (`UserReserved`, `Heirloom`, `Favorite`) remain on the moved stock and do not themselves
block an explicitly approved transfer. Automatic proposals must respect the owner's preferences
before creating an approval; this authority does not infer willingness from those flags.

A [ConsentedTransferDecision](../../src/OpenFantasy.Core/Authority/ConsentedCustody/ConsentedTransferDecision.cs)
is all-or-nothing: a complete candidate, or the untouched original instance. `ApplyTo` returns the
candidate only for the exact decided state hash, which covers custody, controls and policies.
[ConsentedTransferReceipt](../../src/OpenFantasy.Core/Authority/ConsentedCustody/ConsentedTransferReceipt.cs)
ties the outcome to both approvals by identity and content hash, and names the moved lot when
applied. A decision is private authority output; constructing or decoding a receipt commits nothing.
`Records` checks the receipt's world, branch and both principal identities as well as the command
and acceptance identities/hashes. Applied claims also require a binding acceptance and either the
original lot identity or the command's derived split identity. A rejected pair need not bind.
Matching hashes alone cannot validate substituted receipt metadata. The predicate cannot prove the
actual outcome, commit sequence or whole-versus-split result without the authoritative decision or
authenticated journal. It grants neither receipt visibility nor durable authenticity.

## Identity freshness

The command identity, the acceptance identity and any spawned child must be unoccupied in the
current state and in complete history, and must not alias each other. Current state cannot answer
for retired lots or earlier commands, so the occupancy callback is mandatory. The caller owns that
history and must record the command, acceptance and any newly spawned child when it commits an
applied decision. An existing whole-lot identity is not a new reservation. `ApplyTo` pins the complete
counted state, not the external history callback's state. A durable caller must serialize decision,
identity reservation and publication, or revalidate complete historical occupancy at commit.

## Canonical encoding

[ConsentedTransferCodecV1](../../src/OpenFantasy.Core/Authority/ConsentedCustody/ConsentedTransferCodecV1.cs)
uses the existing canonical frame with fixed layouts: terms 262 bytes, command 390, acceptance 449,
receipt 296 rejected or 312 applied. Decoding enforces byte and field bounds, tags, versions, exact
field widths and local invariants, and fails with
[AuthorityFormatException](../../src/OpenFantasy.Core/Authority/AuthorityFormatException.cs). The
authority state encoding and the ordinary command and receipt encodings are unchanged.
This receipt contract admits only `NotAuthorized` and `ResourceUnavailable` rejection reasons;
other values from the shared rejection enum do not become valid merely by belonging to that enum.

## Boundaries

This is a standalone Core authority. It has no runtime binding: no world profile, step processor,
Host storage, journal, recovery or client interface refers to it.

- **Scope is raw counted stock.** Produced output has a different current owner and provenance
  contract; an unknown or produced lot is unavailable here.
- **Capacity is counted storage only.** Combined raw and produced capacity belongs to the complete
  production join.
- **Control, ownership, reach and time are separate layers.** This authority decides control and
  ownership. It proves no spatial reach and spends no actor time or readiness.
- **Both deliveries arrive together.** Constructing a command or acceptance creates no pending
  offer, reservation or expiry.

## Design obligations for a complete binding

1. A persisted offer lifecycle: pending, withdrawn, expired and superseded offers, with stock
   reservation rules and visibility limited to the named participants.
2. Complete-world admission of the candidate with production, material history, care and resource
   owners, under an explicit world profile and action version.
3. Durable publication: journal and receipt rows, exact retry lookup for both participants,
   recovery replay, and historical identity occupancy across continuation cuts.
4. Reach, readiness and exploration-time rules for both participants, including encounter holds.
5. Produced and crafted goods, multi-item exchange and payment as one atomic transaction.
6. Player and NPC offers, native controls and receipt visibility for each participant.

Rejected values retain submitted claims, which can name an unverified counterparty. A Host binding
must retain actual delivery provenance and must not use claimed principals or branch fields alone
for visibility, retry lookup or counterparty identity reservation. It must define when independently
authenticated submissions receive terminal outcomes and consume identities, without letting forged
claims occupy another participant's identities. No durable rejected-pair policy is supplied here.

Approvals bind origin sessions and require current control. Persisted offers therefore need explicit
reconnection, withdrawal and expiry rules; replaying a stored delivery as current authentication is
invalid. The shared command/child identity namespace spans unilateral and bilateral transfers.
Produced-goods terms need their actual lot kind and definition pins under an explicit new contract;
raw-counted terms cannot silently acquire that interpretation.
