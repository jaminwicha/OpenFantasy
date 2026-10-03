# Co-player permission

Character ownership, consent and possession of control are separate facts. Permanent actor bindings
remain in the inventory authority. The [controller lifecycle](controller-lifecycle.md) records human,
disconnect-allowance and autonomous assignments. The permission facet records which named co-player
has accepted which scope of possible assistance. Permission alone changes neither owner nor assignment.

## Consent state and operations

[WorldControlPermissionState](../../src/OpenFantasy.Core/Worlds/ControllerPermissions/WorldControlPermissionState.cs)
owns one permission slot for each represented ordered pair of human home actors. The four-player roster permits at
most twelve pairs. A slot retains its monotonic revision even after revocation; a replacement offer
cannot reuse the previous acceptance. A current offer binds the world and branch, both home actors
and their permanent principal/session entitlements, the issuing command and capability terms.
Acceptance identifies that exact offer, revision and terms. Revoked offers leave the live slot;
their commands and occupied identities remain in authoritative history.

The original owner offers or revokes. The named grantee accepts using the grantee's own actor and
delivery. Offer and acceptance require human assignment; revocation authenticates the permanent
owner independently of temporary assignment. No grantee can delegate onward. Wire claims do not
establish a delivery's authenticity: the host authenticates before selecting a Core command.

Capabilities distinguish combat, traversal, recovery, inventory, trade, production, conversation and
quests, care disposition, and private history. Every offer carries explicit scopes. The simple
CombatAndSurvival preset contains combat, traversal and recovery. It does not include transferring
goods, commerce, cancelling or declining accepted care, or inspecting private history. Recording a
scope supplies no operation or exemption from ordinary skills, costs, custody, reach or targeting.

[WorldControlPermissionProcessor](../../src/OpenFantasy.Core/Worlds/WorldControlPermissionProcessor.cs)
applies an isolated, receipt-bearing, nonadvancing world boundary. These decisions do not release
reading or encounter holds, move actors, alter readiness, heal bodies, consume supplies or interrupt
accepted work. Canonical bookkeeping revisions distinguish the decisions. Stale revision, wrong
offer/terms, outsider identity and unsupported interpretation cannot create consent.

## Persistence and interpretation

[WorldControlPermissionAdmission](../../src/OpenFantasy.Core/Worlds/WorldControlPermissionAdmission.cs)
explicitly admits an empty profile39 permission facet over a fresh, unplayed profile-38
world. Opening a lived world does not add the facet. New permission commands have their own codec;
older commands retain their original bytes. [WorldActionCodecV22](../../src/OpenFantasy.Core/Worlds/WorldActionCodecV22.cs)
encodes permission operations, while [WorldSnapshotCodecV39](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV39.cs)
selects the complete root. Ordinary world copies, controller transitions and
coordinated archive cuts preserve the facet. Complete replay checks every command against its
original predecessor state; decoding a plausible slot alone does not prove lawful consent.

[Storage schema 11](../../src/OpenFantasy.Hosting/Worlds/WorldDatabaseSchemaEleven.cs)
admits the dedicated permission batch through the existing journal transaction,
receipt table, identity occupancy and exclusive writer. It also retains
[measured controller allowance recovery](controller-recovery.md). Creating a fresh permission world
selects that schema explicitly; earlier profiles retain their schema selection. No automatic lived
save migration, second writer or separate consent database exists. Its recovery activation must name
the fresh genesis: fabricated upgrade provenance cannot create an unchecked pre-activation history.

## Effective control and player integration

The permission owner is not an alternate `CanControl` path. Active delegated gameplay additionally
requires the expired original-owner disconnect episode, an explicit assignment at a safe boundary,
the actual connected grantee and an authenticated actor selection. Commands must retain the real
submitting principal/session and operated actor. Replacing inventory ownership or manufacturing an
owner delivery is invalid.

New decisions require current permission. Already accepted work needs immutable acceptance-time
authority and must finish once under its ordinary interruption policy. Revocation or owner return
blocks new delegated input without invalidating earlier costs or history. Closed material/paid outcomes
retain their historical authority; outstanding offers must revalidate before execution. Exact receipt
access by an original submitting principal is distinct from new action or private-history access.

Selection changes must clear held input and readiness, publish the selected actor's authorized
observation, and require deliberate Resume bound to the current selection generation. The grantee's
own character keeps its ordinary human choices and holds. Saved permission cannot manufacture an
active player, earn offline time, recreate a socket or restart the original owner's allowance.

The [effective-control interpretation](effective-control.md) binds active assignments, typed gameplay
authority, accepted-work witnesses and historical attribution through profile40/schema12. The consent
API itself records agreement; it does not provide actor switching, private views or a player-facing control UI.
The [player session](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs) explicitly refuses
fresh permission commands before queueing, because its scheduler does not select the dedicated consent
boundary. Exact previously committed receipts still return through authenticated retry before this
guard, including while paused. Recognizing a command's codec is not admission to a player action route.
