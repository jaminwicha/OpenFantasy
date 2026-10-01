# Encounter retreat

A committed retreat is a lawful way to leave an active encounter by walking away over real ground. It is a
typed encounter decision with its own readiness cost, published one ordinary movement sample at a time, and it ends
the episode only when contact is actually broken. It is not a Wait, an outcome release, a victory, a relocation, an
HP or debt reset or a deletion of anyone involved.

## Interpretation and selection

[WorldEncounterRetreatAdmission](../../src/OpenFantasy.Core/Worlds/WorldEncounterRetreatAdmission.cs) admits
profile 34: an unplayed local-market world (profile 33) plus one root owner,
[EncounterRetreatState](../../src/OpenFantasy.Core/Encounters/EncounterRetreatState.cs). The admission frame
`world.retreat-admission.v1` pins the basis hash and the retreat law
([EncounterRetreatRules](../../src/OpenFantasy.Core/Encounters/EncounterRetreatRules.cs), text `encounter.retreat.v2`;
only the pinned `v2` law is admitted; a `v1` rules pin is never silently reinterpreted).
Every older profile keeps its codecs, bytes and meaning: a retreat request in any other profile is refused when a
batch is built or decoded, and no lived save acquires the owner. Moving a lived profile-33 world into this
interpretation would be a separate, explicit, replayed transition.

[CargoRetreatWorldAdmission](../../src/OpenFantasy.Hosting/Content/Retreat/CargoRetreatWorldAdmission.cs) is the
host composition: the two-market cargo world, unchanged, followed by the profile-34 admission.

## The retreat law

[QueueEncounterRetreatIntent](../../src/OpenFantasy.Core/Encounters/QueueEncounterRetreatIntent.cs) captures the
episode, the expected timing revision, a destination and a route of 1–32 cardinal samples of exactly 32 subunits
(north decreases Y). The command's expected actor revision pins the origin. Two requests are the same choice only
when every field and every sample agree.

| Boundary | Rule |
| --- | --- |
| Queue (encounter decision) | Authenticated control and actor revision; living positive-side participant; due; no queued choice; no retreat in flight anywhere in the world (one may be queued again after an interruption); the supported scope; ground locomotion; origin + route equals the destination; every prefix `Prepared` by `LocalSpatialSnapshot.PrepareMove` and then admitted by the complete root (`WorldEncounterRetreats.AdmitsPlacement`, the same admission as an ordinary movement step); the destination clear of contact |
| Supported scope | Exactly two positive sides; the actor is the only combatant of its side, living or downed (no ally is left behind, no third-party fight is ended); otherwise `Unavailable` / `AllyRemains` |
| Clear of contact | For every living opponent, the horizontal distance between the two actual body centres (the attack geometry's centres) exceeds its longest granted range + 256; the 256-subunit margin is one full cell, the maximum single displacement. Sightline, missing cells and floor never shorten it. Advice ranking and authority use the same `ContactExcess` |
| Each of the actor's due boundaries | One sample through `PrepareMove`, then complete-world admission of exactly the root it would publish (reservoirs, seals, liquid envelopes, custody, construction); readiness `ceil(10000·100 / (800·normalizedSpeed))`, i.e. one eighth of a standard action rounded up, so a cell never costs less than one standard action |
| Refused sample | No movement; that sample's readiness is still charged; the route ends `Interrupted` with its reason; the actor decides again |
| Last sample | Clear of contact: the episode ends `Disengaged` in the same transaction; otherwise `Interrupted` (`ContactRemains`) |
| Incapacity or outcome | The route ends `Cancelled`; ordinary outcome rules apply |

A committed retreat is the actor's standing decision at each of its own boundaries
([WorldEncounterActions.HasRequiredDecisions](../../src/OpenFantasy.Core/Worlds/WorldEncounterActions.cs)), so an
undecided required human still holds the world (full Wait) while a decided retreat proceeds; nothing times out.
While it holds, the actor can queue nothing else (the public view shows a `Retreating` blocker for Wait and abilities
and never reads as an awaited choice), and no exploration movement exists during an encounter.
Opponents act at their own real boundaries between samples; nothing accumulates or replays. Only the root admission
of the sample's placement is a gameplay refusal (`Unadmitted`); any other failure propagates as a programming error.

The walk's tactical time is `(samples − 1) × ceil(charge / 5) × 5` units (each later sample waits for its charge in
whole 5-unit ticks); the last sample's charge stays as exit recovery, paid by exploration time after disengaging.
Offers state the steps, that walk time and the exit recovery, with the per-step and total readiness charge.

[WorldEncounterRetreatResolver](../../src/OpenFantasy.Core/Worlds/WorldEncounterRetreatResolver.cs) runs a sample at
a zero-duration resolution boundary: tactical ticks between samples pay the debt, so time, exposure, calendar and
passive processes follow the [pacing](world-pacing.md) rules unchanged. On disengagement every actor's actual debt,
including the last sample's, becomes the permanent ledger row
([EncounterLifecycleSnapshot.Disengage](../../src/OpenFantasy.Core/Encounters/EncounterLifecycleSnapshot.cs)); the
pacing binding ends with exposure and fractional carry intact; other actors' queued choices end with their
identities retained in the terminal record. HP, MP, positions, custody and rewards are never fabricated, and the
completed-combat [release](encounter-lifecycle.md#entry-staging-and-release) keeps its meaning.

## Ownership and canonical bytes

`EncounterRetreatState` owns the pinned law, the accepted-retreat count, at most one committed continuation (the
exact accepted command, origin, samples walked and readiness charged) and the latest terminal record (actor,
command, episode, origin, final feet, samples, readiness, status, reason, world sequence and cancelled choices).
Before its first sample a continuation is exactly one Action timing choice pinned to the command hash; afterwards no
timing choice remains for that actor, and the actor's feet always equal the walked prefix's endpoint. Complete-world
admission enforces both, so recovery cannot skip distance, cost or an intermediate position.

| Format | Content |
| --- | --- |
| [Action v19](../../src/OpenFantasy.Core/Worlds/WorldActionCodecV19.cs) | Ordinary command envelope + `world.action.queue-retreat` (encounter, timing revision, position frame, one byte per sample) |
| [EncounterRetreatCodecV1](../../src/OpenFantasy.Core/Encounters/EncounterRetreatCodecV1.cs) | `encounter.retreat.v1`: version, rules pin, count, optional continuation, optional record; 4 KiB bound; re-encoded on decode |
| [Root v34](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV34.cs) | The profile-33 frame and order, then the retreat owner as field 25 |
| [Step v34](../../src/OpenFantasy.Core/Worlds/WorldStepCodecV34.cs) | The shared step frame under profile 34 |

Profile 34 uses the coordinated schema-eight family through the ordinary SQLite authority, full verified journal
recovery and coordinated archive continuation; the retreat owner carries across a cut unchanged.

## Host advice, NPC policy and session

[WorldEncounterRetreatAdvisor](../../src/OpenFantasy.Core/Worlds/WorldEncounterRetreatAdvice.cs) is a pure bounded
query: breadth-first over 32-subunit samples with the authority's swept-body ground clearance, preferring the
fewest samples, then the largest centre-distance margin, then search order, and verifying each candidate with the
queue's own route admission (every prefix through the movement owner and the complete root, memoised per placement
within one search, then contact). It never encodes a root per expansion. Advice is never authority.
[LocalRetreatView](../../src/OpenFantasy.Hosting/Simulation/Retreat/LocalRetreatView.cs) projects the observer's
offer (route, destination, per-sample and total readiness, walk time, exit recovery, opponents in reach, blocked
reason) only at a boundary where the observer can choose, plus the observer's own progress and last record. Another
actor's retreat is visible only as an anonymous `OtherWithdrawing` flag, never with its route, destination,
readiness or command identity. `LocalWorldView.Retreat` is omitted from the public encoding when null.

[LocalRetreatConfiguration](../../src/OpenFantasy.Hosting/Simulation/Retreat/LocalRetreatConfiguration.cs) is opt-in
NPC advice (an HP permille threshold per actor), resupplied on every opening and never saved.
[LocalEncounterController](../../src/OpenFantasy.Hosting/Simulation/LocalEncounterController.cs) then submits the
same intent through the same admission, body and readiness rules; without a preference an NPC fights as before.
[LocalWorldSession](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs) admits the request only in
profile 34 and selects decisions, resolutions and ticks exactly as for other encounter choices.

## Native binding

The explicit `NativeRetreat.tscn` entry ([NativeRetreatEntry](../../client/scripts/Retreat/NativeRetreatEntry.cs),
`--cargo-retreat [SAVE]`) opens only the retreat-capable cargo world through
`FarmClientSession.OpenCargoRetreatAsync`; it never upgrades another save. During an active encounter the
production wheel's upper sector is Retreat (existing `traverse` art), built by
[RetreatOffers](../../client/scripts/Retreat/RetreatOffers.cs) from one captured offer. Hover, focus and the confirm
tooltip state the destination cell and direction, the step count, the walk time, the exit recovery, the readiness
charge and any blocker; selecting never acts, cancelling sends nothing and confirming submits that captured route
once. The wheel captures the offers of the view read when it opens
(`FarmClientSession.CaptureWheelView`). In the retreat world,
[LocalEncounterChoiceBoundary](../../src/OpenFantasy.Hosting/Simulation/Retreat/LocalEncounterChoiceBoundary.cs)
withholds executable Retreat, Wait and ability offers until due automatic choices have been published at the
current tactical instant. Those publications change the shared timing revision even though time does not advance.
The projection communicates only that choices are being prepared, never another actor's selected command or route.
Once these choices settle, Full Wait holds for the undecided human, providing a stable inspection interval. This
presentation policy changes neither queue admission nor host scheduling; inspection does not pause exploration. A
capture that genuinely changes while open is not retargeted: the wheel asks the player to close it and choose again.
While the route is walked the HUD reads "Retreating · step x of y"; the end of the observer's retreat is announced
once. Actors keep the classic four-direction walk.

## Supported scope and boundaries

One coordinated complete-resident episode with exactly two positive sides. The retreating actor must be the only
combatant of its side, living or downed; a party retreat or rescue is refused rather than leaving allies behind.
Because an episode's combatants are its authority-derived scenario's, an ally or third side can only come from an
authored scenario. Bystanders keep side zero, bodies and identities. Opponents have no pursuit movement in this interpretation. Terrain ground only; stepped or
construction-supported ground, independent pockets, joining/merging, outside hazards admitted during a retreat,
multiplayer and a corridor threat are not part of this binding.
