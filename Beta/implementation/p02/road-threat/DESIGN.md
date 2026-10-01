# Road threat design (Bandit Chief at the cargo-lane camp)

Status: design written before implementation (kept as the plan of record). Starting revision 223160e on
`codex/runtime-foundation`. Delivered outcome, evidence, failures and limits: [RESULT.md](RESULT.md). One deviation
from this plan: `LocalWorldView.RoadThreats` is null and omitted from the public encoding when a world has no threat.
Worker: Opus 5.5 / 1M / requested Max. Integration and acceptance belong to Astra.

## Smallest boundaries

Reuse every existing owner; add no Core rule, codec, schema, root field or profile.

| Concern | Owner (existing unless marked new) |
| --- | --- |
| Source binding | **new** `Content/RoadThreat/RoadThreatSources`: digest-pinned `monsters.json`/`world.json`, record pointers, raw-field checks, canonical payload hashes |
| Fresh genesis | **new** `RoadThreatGenesisExtension` over the cargo extension's unplayed output, then the unchanged paid/market/retreat admissions (`RoadThreatWorldAdmission`) |
| Actor facts | Space (presence/body), counted custody (empty store, OwnerOnly, Host control), progression (source stats, level), currency (zero wallet), gathering slot, construction exit, lifecycle debt, resource history |
| Danger rule | Lifecycle scenario catalog: one scenario whose **initiator is the chief** and target the player; entry range = the chief's own attack reach |
| Hostile decision | **new** `Simulation/RoadThreat/LocalRoadThreatController` (host advice for the chief's control only), selected by `LocalWorldSession` at an exploration cutoff as an ordinary lifecycle batch |
| Combat | Existing physical actions; `LocalEncounterController` chooses the chief's legal attack or Wait |
| Escape | Existing profile-34 retreat law; no change |
| Public warning | **new** `LocalRoadThreatView` on `LocalWorldView`: observed hostile opportunities against the observer, derived only from the published root |
| Native | **new** `--cargo-threat` entry over the existing cargo presentation and wheel |

The selection is fresh (`road-threat-world` seed): cargo world + chief + retreat profile 34. It never opens,
upgrades or relabels another save; a played root is refused by the extension (revision/profile/coordination checks).

## Source-to-runtime mapping

`monsters.json` `/monsters/33` (file digest pinned with the wolf's existing witness):

| Field | Runtime | Status |
| --- | --- | --- |
| id/name | stat-profile key `bandit_chief`, resident name `Bandit Chief` | executed |
| level 7 | progression level | executed |
| stats hp800 mp0 atk27 mag0 def15 spd10 | `ActorStatProfileDefinition` base stats, zero growth/heritage; HP 800/MP 0 | executed |
| attack power15 / range50 / atbCost480 | ability `bandit_chief_attack`: power 15, range 50×8 = 400 subunits (legacy `UnitsPerTile=32`, Beta 256 subunits/cell; same scale as the accepted Bite), readiness 480/300 | executed |
| aggroRange 140 (1120 sub) | retained; no exploration pursuit exists, so notice/approach is **not executed**; danger starts at actual reach | retained only |
| leashRange 400, moveSpeed 4 | retained; no pursuit/leash/return-heal | retained only |
| xpReward 1400, gil 260..480, three drops | retained; no victory reward, drop or gil is fabricated | retained only |

`world.json` `/regions/16` `bandit_camp` (345,305 15×12) and `/spawners/33` (id34, `bandit_chief`, same rect,
count1, respawnTicks3600) are bound with pointers and payload hashes. The legacy spawner picked a random walkable
tile centre in the rectangle; Beta authors one deterministic tile, (347,311), inside both the rectangle and the
admitted corridor, one cell east of the lane crossing x=346, y∈[305,317). Count 1 = exactly one stable actor.
Respawn is retained, not executed. The chief uses the world's admitted humanoid anatomy (explicit transform: the
record has no body) and an empty store/zero wallet.

## Behavior

* Initiation: at an unheld exploration cutoff, when the chief is alive with zero readiness debt, the player is alive
  and `QueryInteraction(chief, player, 400)` is Reachable (same floor, centre range, terrain sightline), the host
  submits `EnterEncounterIntent` under the chief's own control binding and revision. The authority re-validates.
  Pause (not ready), reading hold, pending excavation/care, encounter phases and near misses select nothing.
* Combat: confrontation → staging → active at actual positions; Full Wait holds the human. The chief attacks only
  when legal. Squire (HP60, def8) takes 32–39 per hit; two hits incapacitate. Level-7/800 HP is not beatable early.
* Escape: the unchanged retreat (destination centre distance > 400 + 256). Because entry needs ≤ 400, a completed
  retreat cannot be re-entered automatically; the chief stays where it stood (no teleport/erase). Returning within
  reach re-initiates from recorded state. Bypass: walking at x ≤ 345 stays ≥ 512 subunits away (paid by steps).
* Nonhostile residents (Wren, wolves, merchants) are side 0 in the threat scenario; the chief is side 0 in both wolf
  scenarios (their versions differ from cargo's, as the lifecycle owner requires every resident).

## Verification plan

Host tests (Content/Simulation/Client RoadThreat): source fields/pointers/hashes; genesis determinism, placement,
scenario sides, profile; initiation contrasts (reach, near miss, pause, reading hold, debt, refused retry); journey
buy → lane → chief entry → damage → retreat → bypass → sale → reopen with identities, lots, funds, HP, debt, time;
incapacity contrast. Native: `--cargo-threat`, warning HUD, wheel retreat; fixture exporter, probe, tool and result
tests as time permits. Limits and unfinished items will be recorded in RESULT.md.
