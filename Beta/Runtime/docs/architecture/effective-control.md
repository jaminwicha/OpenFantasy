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

[WorldActorControlDependencies](../../src/OpenFantasy.Core/Worlds/EffectiveControl/WorldActorControlDependencies.cs)
captures the current-control inputs for one actor, delivery, optional witness and required capability
set. Its immutable selection contains world/branch and interpretation, the actor's permanent binding
and effective slot, and, for a delegated witness, the grantee binding, both lifecycle assignments and
the named permission slot. Missing rows are dependencies too: a later assignment cannot silently turn
an earlier owner selection into current authority. The selection retains individual rows, not the
source world, inventory, complete control roster or unrelated actors' permissions.

Ordinary command and movement authority uses the same selection and eligibility rules through a
value-type read; it does not allocate a retained dependency object for every check. Recorded historical
evidence continues to check permanent bindings and captured terms separately from current eligibility.
The immutable `IsAuthorized` result describes the captured selection. `ValidateCurrent` compares its
exact selected rows against the supplied current admitted world; successful validation of a denied
selection does not grant authority. A release and reacquisition invalidates the old selection even
when the named grant and capabilities remain identical. Equivalent decoded rows compare by value.
Unrelated gameplay revisions, positions and elapsed time do not alone invalidate this control read.

Revalidation is neither a lock nor proof of live transport presence. The publishing authority must
serialize it with the other dependency checks and publication, and still enforce health, readiness,
geometry, carried loads, resource costs, holds and complete admission. Capturing from an admitted
world does not establish a paged ownership directory. Control dependencies are internal authority
inputs; they do not authorize exposing credentials, private permissions or rejection differences
through a player observation.

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

The profile41 [collective withdrawal Core](party-withdrawal.md) recognizes each exact retained member
command as accepted work. Walking holds return; arrival bookkeeping does not. Cancellation requires
current Combat and Traversal permission, names the exact route and cannot refund its paid readiness.
Terminal rows preserve the original witness even after assignment loss. These Core rules do not
expand the profiles accepted by SQLite recovery, host invitations or connected-client selection.

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

[LocalWorldSession.ActorSelection](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.ActorSelection.cs)
binds explicit local selection to a current established port and the held assignment. The
[selection value](../../src/OpenFantasy.Hosting/Simulation/EffectiveControl/LocalActorSelection.cs)
separates immutable home identity from operated actor, random port identity, monotonic generation and
exact witness. Connection dictionaries, admission, disconnects, custody and the home character's human
obligations continue to use the permanent home actor. Returning selection home does not release the
assignment; explicit custody release is a separate boundary.

A transition clears held movement, waiting trade inquiry, readiness and unaccepted queued action.
Accepted obligations retain their original command. The host publishes the home view and an authorized
selected view together before accepting Resume against that generation. The generation identifies an
input lifetime, not a continually advancing world revision. Switching A-to-home-to-A cannot revive
old readiness, movement, zero/stop input, pause or command selection. After the first transition,
unbound legacy input and inquiry mutation remain refused even when home is selected again. Existing
read-only history queries continue to address the permanent home actor; they never become delegated
readers implicitly.

New selected commands retain the authenticated port's real principal/session and exact witness;
Core derives their required capability and rechecks ordinary authority. Movement retains its original
150ms lease and current witness at the simulation cutoff. A queued result belongs to the actual
submitting port, not a dictionary lookup by the operated actor. Exact earlier committed receipts are
retrievable with original authenticated command bytes before fresh selection checks. Receipt lookup
is not permission to queue a new command. Revocation, replacement and owner return invalidate custody,
force a new paused home generation and clear the selected view; old input cannot resume it.

[LocalControlledActorView](../../src/OpenFantasy.Hosting/Simulation/EffectiveControl/LocalControlledActorView.cs)
projects current capability-specific choices without retaining a full actor view or world root.
Missing sections mean withheld information. Combat, recovery, care disposition, inventory, trade,
production and conversation/quests have separate purpose disclosures; no blanket progression or
completed private history is supplied. Purpose-scoped
[request preparation](../../src/OpenFantasy.Hosting/Simulation/EffectiveControl/LocalControlledRequests.cs)
captures quest concurrency tokens and matching delivery lots, recipe inputs and production/storage
revisions, and unfinished construction project/storage/tool choices. It keeps captured terms rather
than reading fresh authority while the player confirms them. Material selection takes larger stacks
first with stable identity ties to respect bounded request sizes. Home and selected observations come
from one published root.
[Selected asynchronous queries](player-queries.md#explicit-actor-selection-queries) preserve the exact
captured selection through admission, serialized reads and response validation. Trade and Inventory
expose only their open participant offers; PrivateHistory independently permits actual terminal quote
and proposal history through narrowly authorized storage readers. The
[history directory](private-history-directory.md) applies that same independent permission to bounded
discovery, including archived records, with exact selection and page-request correlation. Home history
remains permanently home-bound. Inquiry ownership includes the port and generation, so transition cleanup cannot cancel
another selection's request. The [connected character protocol](selected-player-protocol.md) provides
explicit consent and selected-input envelopes, bounded typed observations and a delivered Resume proof.
Its optional application interface carries the same captured context into the Godot presentation;
managed authority and serialization checks alone do not establish native usability.
