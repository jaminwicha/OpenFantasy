# Road threat: the bandit camp on the cargo lane

The threatened cargo world is a fresh, explicitly selected world in which the [cargo circuit](cargo-circuit.md)'s lane
crosses the retained bandit camp and the camp's one Bandit Chief is a real resident. Carrying bought lots along the
lane brings the player within the chief's reach; the chief then begins an encounter itself, under its own control, and
the [retreat law](encounter-retreat.md) is the lawful escape. Nothing here adds a Core rule, codec, schema, root field
or profile: the binding is source content, one more resident and scenario in existing owners, and host advice.

## Source binding and mapping

[`RoadThreatSources`](../../src/OpenFantasy.Hosting/Content/RoadThreat/RoadThreatSources.cs) reads `monsters.json` and
`world.json` by exact file digest and binds `/monsters/33` (`bandit_chief`), `/regions/16` (`bandit_camp`) and
`/spawners/33` by source identity at those pointers. Each record keeps its canonical payload hash; every executed raw
field is checked against the reviewed values, so a moved record or edited field is refused rather than reinterpreted.
The source simulation measures distance in 1/32-tile units (`SimConstants.UnitsPerTile`, a hash-checked witness);
one Beta cell is 256 subunits, so one source unit is eight subunits, the same scale as the accepted wolf Bite.

| Source field | Runtime binding | Executed |
| --- | --- | --- |
| id, name, level 7 | stat-profile key and name, resident name, progression level | yes |
| stats HP800 MP0 ATK27 MAG0 DEF15 SPD10 | `Unassigned` profile base stats, zero growth/heritage; HP 800, MP 0 | yes |
| attack power15 range50 atbCost480 | ability `bandit_chief_attack`: power 15, reach 400 subunits, readiness 480/300 | yes |
| aggroRange 140, leashRange 400, moveSpeed 4 | retained only: no exploration pursuit, leash return or return-heal exists | no |
| xpReward 1400, gil 260–480, three drops | retained only: no victory reward, gil or drop is issued | no |
| camp rectangle (345,305 15×12) | home place identity; the stance must lie inside it | yes |
| spawner id34, count1, same rectangle | exactly one stable actor at an authored tile in the rectangle | yes |
| spawner respawnTicks 3600 | retained only: no respawn | no |

The legacy spawner chose a random walkable tile centre in the rectangle; this binding authors the deterministic tile
(347,311), inside the rectangle and the admitted corridor, one cell east of the lane crossing (x=346, y∈[305,317)).
The record has no anatomy, so the chief uses the world's admitted humanoid body (an explicit transform).

## Fresh admission

[`RoadThreatWorldAdmission`](../../src/OpenFantasy.Hosting/Content/RoadThreat/RoadThreatWorldAdmission.cs) composes the
unplayed ordinary-care farm, the unchanged cargo genesis extension, then
[`RoadThreatGenesisExtension`](../../src/OpenFantasy.Hosting/Content/RoadThreat/RoadThreatGenesisExtension.cs) on that
still-unplayed root, then the unchanged paid exchange, the two counters and the profile-34 retreat admission. The
extension gives the chief its own records in every coupled owner: presence and body in space, an empty owner-only store
and host control in counted custody, source statistics in progression, an empty wallet, a gathering work slot, a
construction exit and a lifecycle debt row; the water owners are re-prepared through the shared
[`CargoWaterRebinding`](../../src/OpenFantasy.Hosting/Content/Cargo/CargoWaterRebinding.cs) exactly as for the
merchants. The lifecycle gains the source attack and one scenario whose **initiator is the chief** and whose target is
the player, with the chief's own attack reach (400) as entry range; the player holds Strike on side 1, the chief its
attack on side 2 and every other resident is a side-zero bystander. The chief joins both wolf scenarios as a bystander,
so their versions differ from the cargo world's. The admission identity frames the cargo basis hash, the source binding
version, the stance, the body definition and the complete extended lifecycle. A played or other root is refused, and
`Find` recognizes the world only by its derived identities, scenario and ability version.

## Hostile entry

[`LocalRoadThreatController`](../../src/OpenFantasy.Hosting/Simulation/RoadThreat/LocalRoadThreatController.cs) is host
advice for admitted hostile opportunities: scenarios whose initiator is a host resident and whose target is the
session's player. [`LocalWorldSession`](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs) asks it at an
exploration cutoff with no pending player request, before any exploration step. It proposes the ordinary
`EnterEncounterIntent` under the initiator's own control binding and revisions only when the world is the retreat
profile, the cutoff is unheld (no reading hold, retained consequence, care obligation or encounter), the initiator is
alive with zero readiness debt, every positive participant can take part, and the player is within the entry range on
a clear centre sightline (`QueryInteraction`, the authority's own check). The authority re-validates; a proposal it
would refuse is not committed. A paused session selects nothing. The decision reads only the selected root, so a
reopened session behaves from recorded state; nothing is remembered in the host.

Because entry needs a centre distance of at most 400 and a completed retreat ends beyond 400 + 256, a retreat can never
be followed by an automatic re-entry; walking back within reach starts a new confrontation. The chief never moves: it
keeps its stance, HP and identity after disengagement. During the encounter `LocalEncounterController` chooses its
attack when legal, otherwise Wait. Against the farm's early Squire (level 1 HP 60, or level 3 HP 78 after Wren's reward)
each hit takes 30–39 HP; staying to fight is incapacity, which holds at Outcome: there is no automatic release, rescue,
robbery or reward. Other residents keep their own scenarios and policies; nothing becomes species-wide hostility.

## Public warning and native binding

[`LocalRoadThreatView`](../../src/OpenFantasy.Hosting/Simulation/RoadThreat/LocalRoadThreatView.cs)
(`LocalWorldView.RoadThreats`) projects each hostile opportunity from the published root: name, level, HP, reach,
feet distance and a state (`Watching`, `InReach`, `Recovering`, `Engaged`, `Incapacitated`) classified with the same
interaction query. It carries no command, episode token, random state or inventory and grants nothing.
[`RoadThreatWarning`](../../client/scripts/RoadThreat/RoadThreatWarning.cs) words it: in exploration the encounter status
line reads "Danger ahead · Bandit Chief guards the road · attacks within 1.6 cells" and its tooltip names level, HP,
reach, distance, that the chief does not chase and that a detour or retreat avoids it.

`NativeRoadThreat.tscn` ([`NativeRoadThreatEntry`](../../client/scripts/RoadThreat/NativeRoadThreatEntry.cs),
`--cargo-threat [SAVE]`) opens only this world through `FarmClientSession.OpenCargoThreatAsync`, with the regional
cargo presentation, the retreat-capable session and the ordinary wheel. The chief is presented with the existing
Squire prototype sprite and its name label; no new art is claimed.
[`RoadThreatPlayProbe`](../../client/scripts/RoadThreat/RoadThreatPlayProbe.cs) and
[`check_native_road_threat.py`](../../tools/check_native_road_threat.py) qualify keyboard, controller and pointer routes
from an exported fixture that starts on the actual road; `NativeRoadThreatResultTests` replays each saved journal.

## Boundaries and extension joins

- One fresh world and one chief. Pursuit, leash/return, approach movement, aggro radius, territory memory, rewards,
  drops, gil, respawn, NPC release after an incapacitating outcome, robbery and rescue are not executed.
- Escort or guard wages, traveling merchants and party retreat join here: an escort would be a side-1 combatant of an
  authored scenario (the retreat law then refuses a lone retreat while an ally remains); wages are paid exchange.
- The sixteen-region bound is unchanged: the chief stands inside the existing corridor; nothing was added to it.
