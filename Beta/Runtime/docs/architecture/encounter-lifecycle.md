# Encounter lifecycle and return to exploration

## Permanent ownership

Profile twenty-four coordinates exploration and encounter episodes in one complete terrain/liquid
world. [WorldEncounterLifecycleAdmission](../../src/OpenFantasy.Core/Worlds/WorldEncounterLifecycleAdmission.cs)
admits a fresh profile-twenty-two exploration basis with no active encounter. It pins that basis and
the complete lifecycle archive. It does not migrate a lived save. Profiles twenty-two and twenty-three
retain their fixed [pacing](world-pacing.md) and [physical-action](encounter-actions.md) interpretations.

[EncounterLifecycleSnapshot](../../src/OpenFantasy.Core/Encounters/EncounterLifecycleSnapshot.cs) owns
the immutable ability and scenario catalogs, complete actor recovery-debt ledger, phase, encounter
count and cumulative exploration/tactical counters. It also retains the selected scenario and current
or last action archive. Its revision follows the complete world revision. Exploration can therefore
retain an earlier outcome without routing ordinary work back into that encounter.

An [EncounterScenarioDefinition](../../src/OpenFantasy.Core/Encounters/EncounterScenarioDefinition.cs)
names the initiating actor, target, entry range and every resident's encounter-specific side and
ability grants. Bounds are eight scenarios, thirty-two abilities and sixty-four actors. Positive
opposing sides are participants; side zero is a coupled bystander with no ability grants. These are
explicit opportunities, not species-wide hostility or permission to omit other residents from time,
space or material ownership.

Every resident has actual progression and a positive speed whose multiplication by ten fits the
unsigned tactical speed representation. This is checked during exploration admission, including for
bystanders and actors outside the selected scenario's positive sides.

## Entry, staging and release

[WorldEncounterLifecycleProcessor](../../src/OpenFantasy.Core/Worlds/WorldEncounterLifecycleProcessor.cs)
publishes lifecycle changes through the same authenticated world authority as other actions.

| Current phase | Accepted transition | Result |
| --- | --- | --- |
| Exploration | Authenticated entry into an admitted scenario | Confrontation; encounter count increases and timing starts with retained debt |
| Confrontation | Actionless lifecycle boundary | Staging at the actual current positions |
| Staging | Actionless lifecycle boundary | Active at those positions |
| Active | Physical action resolution leaves at most one living positive side | Outcome with remaining choices cancelled |
| Outcome | Authenticated release by the living initiating actor | Exploration with the last action archive, debt and exposure retained |

Entry, staging and release earn no time, recovery, movement or material. Staging is an explicit
in-place fallback: it does not find a formation, execute approach routes or teleport an actor. The
complete root is reconstructed at each transition, preserving its current clearance, terrain support,
construction access and dry-occupant liquid-envelope requirements.

[EnterEncounterIntent](../../src/OpenFantasy.Core/Encounters/EnterEncounterIntent.cs) captures the
scenario, episode identity, expected encounter count and target presence revision. Its ordinary
command envelope captures the initiating actor's presence revision and authenticated control. Entry
requires a living authorized initiator, living opposing participants, and actual reach and sightline.
Unrelated clock commits do not change the encounter count or stationary actors' presence revisions.
An actor may enter while recovering; its debt is preserved rather than waived.

The episode identity is derived by
[EncounterEpisodeIdentity](../../src/OpenFantasy.Core/Encounters/EncounterEpisodeIdentity.cs). A hash of
world, branch, common seed, scenario identity/version and first ordinal under the
`encounter.episode-identity.v1` domain defines each scenario's identity family. Every episode retains
that hash's high 64 bits; its low 64 bits are the hash's low bits XOR the prior encounter count. This
gives a stable distinct identity for each supported ordinal without letting a command or cause choose
the damage stream.

[WorldIdentityIndex](../../src/OpenFantasy.Core/Worlds/WorldIdentityIndex.cs) reserves the entire family
against generic commands, causes, results and spawned objects. `Contains` includes prospective family
identities; `ContainsEntity` reports only actual admitted entities and references. Only accepted entry
may claim its exact next episode identity, after actual entity and historical collision checks. A
different supplied episode receives a refusal; attempting to occupy a family identity in a generic
role refuses the commit. Neither path can advance the encounter ordinal or poison a later episode. Admission also requires
family prefixes to be disjoint from dynamic, terrain and geography identity roles. Static layers
lazily cache their high-word sets, so unchanged roots do not rescan terrain cells for this check.

[ReleaseEncounterIntent](../../src/OpenFantasy.Core/Encounters/ReleaseEncounterIntent.cs) captures the
episode identity and exact final action hash. Release requires the actual Outcome phase, that matching
archive and its living initiating actor, followed by complete current world admission. It is not flee,
disengagement, healing, reward issuance, corpse creation or respawn. An incapacitated initiator has no
automatic release or rescue transition in this interpretation.

## Debt, exposure and process boundaries

The permanent ledger retains readiness debt for every resident, including bystanders and incapacitated
actors. Entry copies it into the new timing owner; accepted active boundaries copy resulting debt back.
While an episode exists, complete-world admission requires every timing row to equal its permanent
ledger row, including the Outcome phase. These reconciled witnesses cannot drift independently.
Release changes neither that debt nor earned exposure. Each ordinary exploration step pays at most
180 tactical units per actor, clamped at zero. Held interactions and lifecycle boundaries pay none.

[WorldActorAvailability](../../src/OpenFantasy.Core/Worlds/WorldActorAvailability.cs) requires positive
HP and zero starting debt for ordinary voluntary actions and movement. A step that pays the remaining
debt cannot also use the newly recovered readiness for its starting action or movement sample. NPC
work and movement use the same eligibility. Speaking, giving and quest delivery also require a living
counterpart. HP0 preserves the actor's body, occupancy, possessions and identity; this is incapacity,
not the broader death lifecycle. Authenticated retrieval of an earlier exact receipt remains available.

The [aftermath-care composition](aftermath-care.md) adds a separately interpreted recovery boundary:
an exact outcome acknowledgment and a capable neutral helper's finite treatment can resume the same
episode, preserving historical damage and charging helper debt. This is distinct from voluntary
outcome release; ordinary movement and action availability retain their positive-HP requirement.

For cumulative exploration steps `E` and tactical ticks `T`:

```
world.AdvancingSteps = E + T
accepted exposure = E * 1800ms + T * 50ms
process intervals = floor(accepted exposure / 1800ms)
process carry = accepted exposure % 1800ms
```

An episode captures `EntryTacticalTicks`; its elapsed tactical units are exactly
`(T - EntryTacticalTicks) * 5`. [WorldPacingState](../../src/OpenFantasy.Core/Worlds/WorldPacingState.cs)
anchors encounter exposure at entry and preserves its complete progress when ending the timing binding.
Calendar and every resident's experienced duration continue from that same exposure.

Fractional carry survives release, exploration, reentry and restoration. For example, releasing with
1,750 ms of carry and selecting one exploration step earns one process interval while retaining that
1,750 ms remainder. Liquid preflights and actual flow use `ProcessBoundary`, the last earned whole
boundary, rather than the fractional calendar. Gathering recovery and regrowth use earned process
intervals. Construction and excavation still require actual labor commands. Waiting for input, held
time and host stalls do not produce catch-up intervals. Ordinary NPC amble cadence also uses process
intervals in this interpretation.

## Content and public session boundary

[EncounterJourneyAdmission](../../src/OpenFantasy.Hosting/Content/EncounterJourneyAdmission.cs) provides
explicit embedded and filesystem admissions with caller-supplied seed and player name. It retains the
player, Wren, original delivery quests and complete breach-world owners, and adds two actual Bramble
Wolves before reservoir/custody binding. Their fresh stances are one cell east and west of the player
on the same elevation; spatial admission checks clear grounded bodies, melee sightlines and construction
exit witnesses.

[EncounterFarmSources](../../src/OpenFantasy.Hosting/Content/EncounterFarmSources.cs) checks retained
source files and selected payload hashes. Both wolves share the same source-backed statistics and
authored quadruped anatomy definitions while retaining distinct actor, body, form, control, empty
inventory and zero-balance wallet identities. Each starts with 62 HP, no MP, no earned work and no
language grant. The two complete four-resident scenarios assign the player and chosen wolf to opposing
sides; Wren and the other wolf are bystanders. Strike and Bite retain the physical-action adaptations
described in [encounter actions](encounter-actions.md#content-hosting-and-presentation). No automatic
legacy rewards, drops or respawn are imported.

[LocalWorldSession](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs) selects entry and
release requests, actionless staging, active decisions/resolution/ticks, or ordinary exploration
according to the retained phase. It clears sampled movement at lifecycle and encounter boundaries;
old movement cannot spill into resumed exploration. A ready session resumes the retained phase after
recovery. Outcome holds until an eligible explicit release. Existing exact receipt lookup precedes
new-action eligibility; attaching to the same queued bytes does not execute another command.

[LocalEncounterLifecycleView](../../src/OpenFantasy.Hosting/Simulation/LocalEncounterLifecycleView.cs),
available through `LocalWorldView.EncounterLifecycle`, exposes phase, encounter count, player debt and
action eligibility. Entry rows contain scenario/name, target and captured target revision, derived
episode ID and a current eligibility hint. An eligible initiating player at outcome receives the opaque
release action hash. These observations neither expose the journal nor authenticate a command; the
host rechecks actual authority and legality. The projection uses the existing complete local observation
scope, not a general perception or remote knowledge model.

## Canonical persistence and scope

[EncounterLifecycleCodecV1](../../src/OpenFantasy.Core/Encounters/EncounterLifecycleCodecV1.cs) retains
the complete bounded catalog, counters, phase, debts and optional current/last action archive in a
canonical `encounter.lifecycle.v1` frame, at most 512 KiB.
[WorldSnapshotCodecV24](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV24.cs) uses a direct
22-field root with permanent pacing at index twenty and lifecycle at index twenty-one. The action
archive is owned inside the lifecycle rather than duplicated as a separately writable component.
The existing 16 MiB world bound reserves maximum liquid, pacing and lifecycle serialization together.

[Action protocol version twelve](../../src/OpenFantasy.Core/Worlds/WorldActionCodecV12.cs) encodes entry
and release in the ordinary command envelope, bounded to 512 bytes. Roots and batches use profile
twenty-four. Lifecycle batch kind eight has no movement and contains one entry/release command or an
actionless staging transition. Explicit requests receive the normal durable terminal receipt; system
staging does not invent an actor receipt.

[Schema six](../../src/OpenFantasy.Hosting/Worlds/WorldDatabaseSchemaSix.cs) adds kind eight to the
existing paced commit kinds and is exclusive to profile twenty-four. The sole SQLite writer commits
the complete root, accepted batch, outcomes and identities atomically. Full-journal recovery validates
exact replay, ledgers, checkpoints and head before readiness. Fresh database admission rejects elapsed
intervals, encounter history and preloaded readiness debt. Canonical archives alone do not prove that
their history was authorized. Schema five and profiles twenty-two/twenty-three remain unchanged.

The privileged `--create-encounter-journey <database> <seed> <player-name>` operation creates this
separate fresh world. It does not alter the ordinary native launch. The supported composition retains
dry actors outside nominal wet volumes; it does not integrate occupied wet passages, structural
settlement, general staging movement, encounter joining/merging, independent co-op, broader effects or
death/reward systems. Native battle controls, choreography, animation and presentation have separate
contracts. A successful in-place transition makes no claim about those wider mechanisms.
