# Home consent boundary

Source implementation is prepared; the integration lead owns build and execution evidence. No checks
were run by this worker. Earlier tests, fixtures, Core, storage, wire formats and canonical bytes are
unchanged. The checkpoint is `codex/checkpoint-before-connected-control-20261003T052146Z` at `f71e1b78`.

## Forwarding contract

The lead can add these methods to `LocalPlayerConnection`:

```csharp
public Task<LocalControlPermissionBoard> ReadControlPermissionsAsync() =>
    _host.ReadControlPermissionsAsync(this);
public Task<WorldActionReceipt> SubmitControlPermissionAsync(WorldActionCommand command) =>
    _host.SubmitControlPermissionAsync(this, command);
```

The corresponding host methods are internal and serialize through the same host gate as acquisition,
selection and gameplay. They require the current fully established transport and permanent home
binding. They deliberately do not require readiness, selected-home identity or `ControlAvailable`.
The command must retain its original home actor, principal/session and bytes, carry a permission intent
and carry no delegated witness. Exact committed receipts return before a new permission batch is
selected. Fresh stale revisions receive Core's ordinary recorded refusal. Offer and acceptance keep
Core's Human requirement; the returning original owner can revoke while accepted work defers Human
restoration. This does not enable any new gameplay input while pending.

One real `ControlPermission` boundary publishes each fresh consent request. It advances bookkeeping
only. The path neither queues nor replaces `PendingLocalAction`, changes readiness, samples movement,
releases holds, nor resolves accepted gameplay. Normal publication reconciles any assignment actually
invalidated by Core revocation, including its existing paused-home selection rule. Storage failures
fail the host; an ordinary dispatch refusal does not.

## Public presentation values

`LocalControlPermissionBoard` has `WorldId`, `BranchId`, `WorldRevision`, `HomeActorId`,
`HomeActorRevision`, `PermissionRevision`, `HumanActorIds`, `Slots`, and `HeldWitness`. Human actor IDs
are the bounded original roster, including currently absent humans. Slots contain only pairs where
this home is owner or named grantee. The board exposes no principal, session, delivery credential or
`WorldControlPermissionTerms` object. The terms hash and exact offered scopes are sufficient to prepare
an acceptance. World/branch/home identity and home presence revision supply the remaining command
pins; the client already owns its own authenticated delivery and must allocate a fresh command ID.

`LocalControlPermissionSlot` has `OwnerActorId`, `GranteeActorId`, `Revision`, `Status`,
`ChangedSequence`, `OfferedSequence`, `AcceptedSequence`, nullable `GrantCommandId`,
`AcceptanceCommandId`, `Capabilities`, `TermsHash`, and `Acquisition`. Revoked rows retain their slot
revision with no effective terms or command pins. The public constructors validate bounded strict
ordering, relevant pairs and slot structure. They copy list inputs into read-only collections. List
constructor parameters and properties both use `IReadOnlyList<T>` for explicit JSON constructor
binding. Transport codecs still own strict syntax, canonical encoding and envelope validation.

`HeldWitness` belongs only to this exact port, is reconciled against the actual assignment, and remains
visible when selection returns home. Constructor validation binds it to the same home's exact accepted
incoming slot. A held witness excludes an acquisition opportunity. `LocalControlAcquisition` becomes
public with unchanged fields and behavior; it captures terms, not authenticated possession.

Incoming accepted slots show acquisition only when the original owner has no established port, its
saved allowance really expired, the home grantee is Human and available, no assignment is held, and
the original owner's accepted-work boundary is safe. Acquisition independently rechecks the captured
terms, live presence and Core law. Reads never acquire, resume or advance the world.

## Prepared tests and limits

Seven new cases under `Simulation/ControlPermissions/LocalControlPermissionTests.cs` use the existing
real four-human and paid-combat fixtures. They cover offer/accept/revoke and exact original receipts;
stale receipt versus foreign/altered command refusal; provisional and replaced ports; actual loss and
30-second expiry before acquisition; held custody at home selection; consent while another actor is
selected; bounded immutable privacy-filtered DTOs; and actual home-owner revocation while restoration
is pending and an accepted paid attack remains retained. Snapshot field comparisons protect all
non-consent gameplay, lifecycle values, controller facts and accepted obligations.

These are host-boundary tests, not TLS/Godot reachability, rendered usability, crash or full phase
acceptance. The pending-return test preserves the accepted attack and original receipt; existing
effective-control tests separately prove its ordinary later paid resolution. Tests are unrun until
the lead's serialized qualification.
