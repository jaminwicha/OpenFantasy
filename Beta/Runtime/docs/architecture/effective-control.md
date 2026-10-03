# Effective co-player control

Effective control lets an accepted co-player operate another character while preserving both people's
identities. It composes [consent](control-permissions.md), the original
[controller lifecycle](controller-lifecycle.md), and ordinary gameplay authority. Inventory bindings
continue to identify permanent character entitlements; temporary control never changes those bindings.

## Assignment and current authority

[WorldEffectiveControlState](../../src/OpenFantasy.Core/Worlds/EffectiveControl/WorldEffectiveControlState.cs)
owns bounded assignment slots with monotonic revisions. A released slot keeps its revision. An
acquisition records the named owner and grantee, exact offered and accepted grant, capability terms,
assignment cause and revision, acquisition sequence, and original expired disconnect episode.
[WorldControlAssignmentProcessor](../../src/OpenFantasy.Core/Worlds/WorldControlAssignmentProcessor.cs)
requires accepted permission, the owner's autonomous assignment after expiry, a human grantee, and a
safe boundary with no conflicting accepted actor choice. One grantee cannot hold two assignments.
Acquisition and release are nonadvancing host boundaries; they spend no gameplay time or resources.

The host authenticates the connection and establishes actual grantee presence before selecting an
acquisition. A saved human assignment or decoded witness cannot establish that a socket is connected.
Opening a save neither acquires control nor restarts the owner's disconnect allowance. This is the
same trust boundary by which the host establishes actual disconnection before submitting a lifecycle
event; Core validates the selected event's durable prerequisites.

[WorldActorCommandAuthority](../../src/OpenFantasy.Core/Worlds/EffectiveControl/WorldActorCommandAuthority.cs)
checks current commands and movement against the exact active witness. Scope is derived from the
typed intent. Combat, traversal, recovery, inventory, trade, production, conversation/quests and care
disposition remain distinct capabilities. Unsupported interpretations have no delegated scope.
Ordinary reach, bodies, readiness, costs, custody and complete-world admission remain mandatory.
The command retains the grantee's principal/session and the owner's operated actor. A typed
[WorldActorAuthorization](../../src/OpenFantasy.Core/Worlds/EffectiveControl/WorldActorAuthorization.cs)
passes that decision into applicable domain planners; it does not manufacture an owner delivery or
change the standalone planners' default ownership checks.

While a delegate holds an assignment, the original owner's ordinary command stream and autonomous
controller cannot compete for fresh actor decisions. Original-owner permission administration remains
separate. Revocation, replacement, owner return and grantee loss invalidate the assignment for new
input. Regranting or reacquiring creates a different revision and cannot restore an old witness.
The grantee's own character retains its original human choices and holds.

## Accepted work and historical evidence

An accepted combat choice or retreat owns the exact command it accepted. Resolution derives retained
authority from that actual obligation, including its command hash; callers cannot supply an
"already accepted" flag. Revocation stops new input while ordinary paid resolution continues. Retreat
still checks every movement sample and charges its normal readiness. Body, geometry and encounter
interruptions retain their ordinary meaning. Owner return waits for the established safe boundary.

Care acknowledgment creates a patient request, not a treatment. Revoking delegated control does not
withdraw that request or consume a dose. Treatment is a separate helper action requiring its own
authority and finite medicine. Cancellation/decline requires the separate care-disposition capability.

Outstanding material proposals and paid quotes require their captured assignment to remain current
at settlement. Closed transfers, fills and withdrawals retain original attribution. A returning
original owner may withdraw an offer created for that actor by a delegate: the original grant and
later terminal actor delivery remain separate evidence. An old offer cannot become executable merely
because a similar grant is accepted again. See [material proposals](material-proposals.md) and
[paid exchange](world-paid-exchange.md).

Recorded evidence validates world/branch, permanent home entitlements, capability terms and chronology.
Structural root validation establishes internal consistency. Complete journal replay against each
original predecessor establishes that acquisition and command acceptance were lawful at that time.
A plausible historical witness by itself never authorizes new execution.

## Canonical state and storage

[WorldDelegatedControlAdmission](../../src/OpenFantasy.Core/Worlds/WorldDelegatedControlAdmission.cs)
explicitly composes fresh profile40 over an unplayed profile39 world. It admits an empty assignment
facet; lived saves never gain it on opening. The profile carries the existing coordinated material,
care and resource archive owners without introducing a second journal or writer.

[Action V23](../../src/OpenFantasy.Core/Worlds/WorldActionCodecV23.cs) wraps the unchanged ordinary
command with its compact control witness; nested wrappers are refused. Profile40 step bytes carry
movement attribution and the dedicated assignment event. Earlier actions, roots, steps and bounded
retained readers keep their bytes and limits. Witnesses do not enable exclusive older terrain or
deferred-consequence interpretations.

[Schema twelve](../../src/OpenFantasy.Hosting/Worlds/WorldDatabaseSchemaTwelve.cs) admits the assignment
commit kind and retains measured controller recovery. Profile40 and schema12 must agree at genesis
and replayed head. Fresh genesis provenance is mandatory; fabricated recovery activation cannot hide
earlier history. Assignment, gameplay results, exact command receipts, occupied identities and any
archive publish through the sole transaction. Reopening authenticates the original genesis and full
history, including closed outcomes whose permission has since been revoked.

A grantee can retrieve its exact old command receipt after revocation by authenticating its own
permanent home entitlement. The supplied command must retain its original principal and exact bytes.
A fresh command still needs current authority. Receipt lookup grants no private inventory, proposal,
quote or market-history access; those readers retain their independent authorization.

## Live host assignment custody

[LocalWorldSession.EffectiveControl](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.EffectiveControl.cs)
binds acquisition and release to the current authenticated port under the sole host serialization gate.
An immutable [LocalControlAcquisition](../../src/OpenFantasy.Hosting/Simulation/EffectiveControl/LocalControlAcquisition.cs)
captures the accepted grant, capability terms, assignment revision and original disconnect episode.
The host derives the grantee from the port's permanent home binding and creates the new assignment
cause and sequence. Core still adjudicates the exact recorded prerequisites. A stale request is a
refusal, not a new grant or an opportunity to recapture different terms.

Only a current port whose final network admission completed can acquire. Saved Human state,
possession of a credential and provisional attachment do not establish live presence. An established
owner port prevents acquisition even while accepted work delays safe restoration. Acquisition clears
the grantee's readiness, held movement and unaccepted queued action; its home character keeps its
required human decisions. Neither permanent actor identity nor its observation/private readers changes.

The acquiring port retains the exact witness as transient custody. Explicit release must match that
port and assignment; an earlier witness cannot release a later acquisition. Core invalidation clears
transient custody as the host publishes the resulting state. Successful owner admission releases the
assignment before attempting safe restoration, so new delegated input stops while accepted work keeps
its ordinary costs and resolution. Grantee transport loss records only that grantee's controller event;
ordinary port disposal releases custody without fabricating any disconnect event or renewed allowance.

A live host attachment releases saved active assignments through ordinary nonadvancing journal
boundaries before starting its scheduler. Graceful shutdown relinquishes live custody. Inspection
through `SqliteWorldAuthority.OpenExisting` alone preserves the recorded head; a saved assignment
cannot establish a connection for a new host. Cleanup retains permission, original controller episodes,
accepted commands and resources, charging no offline gameplay time. A failed host does not pretend its
cleanup committed; the next attachment reconciles the persisted assignment through the same law.

## Player selection boundary

The internal live-host acquisition API implements assignment custody. Player-facing possession requires
a separately admitted adapter binding: authenticated live presence, explicit actor selection,
selection generations, freshly authorized observations, cleared queued input/readiness, and deliberate
Resume. Stale selection or readiness cannot become valid after an A-to-B-to-A switch. Saved consent
alone supplies none of these facts. The existing local/TLS/Godot player route remains bound to its
home actor; the trusted-host API is not a rendered actor-switching interface.
