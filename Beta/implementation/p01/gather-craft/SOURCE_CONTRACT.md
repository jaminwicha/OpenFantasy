# Gathering, rations and Wren: source contract

This read-only archaeology separates actual predecessor behavior from the authored next admission. It does not change the frozen [source packet](../../../Runtime/content/legacy/aldermere-first-loop/records.json), [field mapping](../../p00/first-loop-mapping/mapping-v1.json), or [native farm admission v1](../../../Runtime/content/native-farm/admission-v1.json). The connected goal is gather → make rations → deliver wood → construct a persistent windbreak. **The predecessor implements the first three operations, but never constructs the windbreak.** The next bounded gathering admission is one increment toward that goal, not completion of the whole loop.

## Exact retained records

Pointers below address original files in the frozen packet's [source-support/data](../../../Runtime/content/legacy/aldermere-first-loop/source-support/data/) directory. The packet preserves each complete payload and its SHA-256; prices and omitted fields are not silently reinterpreted as new physical parameters.

| Original file and JSON pointer | Retained meaning |
| --- | --- |
| `world.json#/nodes/0` | Spawner 101: oak, rectangle `(95,315,60,60)`, count 12, respawn 900 source ticks. |
| `world.json#/nodes/2` | Spawner 103: berry, rectangle `(100,330,50,50)`, count 8, respawn 700 ticks. |
| `items.json#/items/12` | Wood, Material, price 6 gil. |
| `items.json#/items/15` | Berries, Material, price 5 gil. |
| `items.json#/items/16` | Rations, Consumable, price 25 gil, heal 30 HP and 5 MP. |
| `items.json#/items/17` | Copper Axe, Tool/Axe, power 1, price 90 gil. |
| `items.json#/items/19` | Sickle, Tool/Sickle, power 1, price 60 gil. |
| `recipes.json#/recipes/1` | `craft_rations`: two berries plus one wood produce two rations. Omitted station defaults to `None`. |
| `npcs.json#/npcs/116` | `farmer_wren`, Wren, tile `(103,322)`, wander 2, exact retained appearance and dialogue; quest `wren_orchard`; omitted shop is empty. |
| `quests.json#/quests/44` | `wren_orchard`, “Wren's Saplings”, Fetch six wood, giver Wren, minimum level 1, 420 XP and 200 gil. Omitted prerequisite and item reward are empty. |
| `world.json#/regions/18` | `farm_verdance_small`, rectangle `(100,320,9,7)`. |

Wren's opening is “Just me, the rows, and the crows. Suits me.” The two extra flavor lines concern wolves taking a lamb and inconvenient rain/sun. The offer is “Six wood for a windbreak and my saplings might see spring.” The turn-in is “The wind can howl all it likes now. Thank you kindly.” Wolves, lambs, weather damage and spring survival are narrative references here, not implemented causal farm systems.

## Actual source behavior

[World.Gathering.cs.txt](../../../Runtime/content/legacy/aldermere-first-loop/source-support/src/Sim/World.Gathering.cs.txt), lines 9–19 and 109–159, defines oak as five depletion swings requiring an axe and yielding wood; berry as four swings requiring a sickle and yielding berries. Each accepted swing spends **400 of a 1,000-point ATB gauge**, decreases node HP by exactly one, and attempts to award the best held matching tool's `ToolPower` units. Power improves output per swing; it does not increase node damage, reduce time, define hardness, or derive physical mass. Copper Axe/Sickle yield one each. Tools need only be in the inventory, not equipped; there is no tool wear or repair in this method.

Gathering requires a live node on the actor's map, distance ≤56 original units, a living actor, no active vehicle and a matching tool. Insufficient ATB returns an empty response and changes nothing. It turns the actor toward the node and emits loot/damage; depletion emits death. The source has no node/hand line-of-sight test, body-function gate or ongoing work reservation. Those are deliberate target validity additions, not imported algorithms.

Spawner initialization attempts its count of nodes, each with at most 32 shared-RNG candidate placements, accepting walkable, non-safe tiles. Failed placement leaves a gap. Depletion schedules a new node after 900/700 ticks; the old dead node remains for 20 ticks. Respawn reselects a location, has a new entity ID, and has no conserved soil/biomass budget or ecology. Source entities are not terrain obstacles: ordinary movement checks map tiles rather than treating a node sprite as a collider. A visible source tree therefore does not authorize inventing blocking terrain in the target.

The comment calling the dead-node interval a “stump” does not correspond to a separate stump asset: [EntityView.cs.txt](../../../Runtime/content/legacy/aldermere-first-loop/source-support/src/Client/EntityView.cs.txt) dims the same sprite when dead. Nodes use the existing three-column/four-row, 16×24 frames with a `0,1,2,1` idle sequence at 2.5 phases/second. This visual time is not work/respawn authority.

`CraftItem`, lines 174–197, immediately removes inputs and adds outputs; it has no duration, work skill, recipe research, actor-life check or ATB cost. Forge recipes check nearby forge tiles; rations require none. The source never says whether the wood is fuel, packaging or edible ingredient, and supplies no mass, volume or residue. Preserve its counted recipe while separately authoring any physical transformation interpretation. Ration use in [World.Systems.cs.txt](../../../Runtime/content/legacy/aldermere-first-loop/source-support/src/Sim/World.Systems.cs.txt), lines 169–183, consumes one and heals capped HP/MP; it is not hunger, nutrition or cooking-time simulation.

Quest methods in that same file, lines 222–312, require interaction with the correct nearby giver, the offered quest, an unaccepted task and minimum level/prerequisite. Fetch readiness is current held quantity, not a cumulative gathering counter. Completion removes six wood, marks the player's quest completed once, grants gil/XP and emits quest progress. It does not deposit wood into Wren's inventory, reserve materials, spawn construction, alter tiles or protect crops. The 420 XP is not merely a notification: the source level-up loop subtracts thresholds, recomputes stats and restores HP/MP. From level 1 and zero XP it reaches level 3 with 38 XP (thresholds 100 and 282). A target reward receipt without those systems is a preserved reward obligation, not a full progression port.

Additional inspected predecessor code, [ServerNode.cs](../../../../src/Net/ServerNode.cs), lines 295–375, cycles flavor by `Tick / 200 % lineCount`; it shows the turn-in line when ready, before successful completion. A constructed-windbreak claim should instead follow the target's actual committed construction. This source file is supplemental current-checkout evidence, not part of the frozen closure.

Two source failure behaviors should be deliberately corrected: gathering still spends ATB and damages the node when inventory admission fails; `AddItem` can partially fill a stack before returning false despite its “nothing added” comment. Crafting's refund attempt is therefore not a reliable atomic transaction. The target should reject insufficient capacity without partial yield/depletion or consumed ingredients, and retain that explicit semantic correction.

## Explicit space and time conversion

One source tile maps to one admitted cell; 32 source units map to 256 subunits. Thus source positions multiply by eight, tile centers are `tile * 256 + 128`, and gather range is **448 subunits**, distinct from conversation's 768. Realm, branch, elevation, body support and occlusion remain target validation.

[SimConstants.cs.txt](../../../Runtime/content/legacy/aldermere-first-loop/source-support/src/Sim/SimConstants.cs.txt) fixes 20 source ticks/second. The new admitted exploration pacing is [ExposurePacingV1](../../../Runtime/src/OpenFantasy.Core/Exposure/ExposurePacingV1.cs): **1,800 calendar/experienced milliseconds per accepted advancing step**. Preserve original active-time rates by mapping one source tick to one advancing step, not one calendar millisecond:

| Preserved count | Active time at 20 accepted steps/second | New calendar/experienced time |
| --- | ---: | ---: |
| Oak renewal: 900 steps | 45 seconds | 27 minutes |
| Berry renewal: 700 steps | 35 seconds | 21 minutes |
| Depleted visual: 20 steps | 1 second | 36 seconds |
| 400 ATB from empty at gain 10: 40 steps | 2 seconds | 72 seconds |
| Three-line flavor selection period: 200 steps | 10 seconds | 6 minutes |

These are a declared mapping into the new 40-minute day profile. They do not preserve the predecessor's separate 10-minute presentation-day fraction. Pause, shared reading holds, rejected steps and offline wall time contribute zero work or renewal progress. The scheduler must record the due advancing step and persist/replay it; a Godot timer cannot refill a node.

Original ATB fill is `min(1000, ATB + 6 + Speed / 2)` each source step. A level-1 Human Squire has speed 8 and hence gain 10; source player ATB initially defaults to zero. The new farm player has an authored body and a temporary squire sprite, not a complete Squire-stat import. Initial zero/gain ten/cap 1,000/cost 400 must therefore be an explicit versioned work profile for this admission. Wren may use the same actor-owned work seam by authored admission; the source NPC placeholder has no such gather behavior or Squire speed. Preserve banked ATB if this profile is used: a fixed per-swing cooldown would change the source's ability to spend stored readiness.

## Smallest retained admission and connected continuation

The coordinator selected a new `GatheringFarmAdmission.CreateEmbedded(seedText, playerName)` that composes the unchanged farm v1 builder using a domain-separated seed. Validate the exact original user seed/name first; encode an OFC version-1 frame with tag `farm-work.seed.v1` and the exact seed UTF-8 bytes, then pass its lowercase SHA-256 hex as the base builder's seed. This gives a distinct world/actor lineage as well as content/version identity and save path. The new genesis retains the base profile's eight authored wood; no existing save is converted, replaced or stripped of stock.

1. Admit **one representative oak and one berry node**, not the full source count 12/8. Candidate authored centers `(101,327)` and `(105,332)` are inside the corresponding source rectangles and current loaded crop; both are original walkable grass outside safe zones, verified by decoding the source RLE map. Exact placements are new admission choices, not results reproduced from the original shared RNG. They need current actor-clearance/access checks and stable seeded instance IDs.
2. Give an explicitly supplied finite kit containing one source Copper Axe and one source Sickle, with exact item/power pins. The source never grants these automatically, and Wren has no shop. Preserve the eight prior authored wood. Expand the new profile's counted storage capacity explicitly; v1's capacity eight cannot hold that stock plus tools or new harvest. A 32-unit profile is sufficient for this demonstration, but is not a conversion of the source's 24 slots ×99 stack limit or a physical volume measure.
3. Admit **32 complete cycles per node**, including its initial cycle: 160 total oak wood rights and 128 berry rights at the admitted power one. Depletion and respawn may activate only the reserved next finite cycle. Retain the source identity, monotonic next-unit cursor and past issued rights; replacement cannot remint them. After the final cycle the node stays exhausted. Fixed-site respawn with stable node identity is an authored bounded policy, distinct from the original random relocation/new entity ID. This finite supply admission does not simulate renewable ecology.
4. Wire a real gather action using actor ownership, owned matching tool, body manipulation applicability, exact node/tool/source/storage/work revisions, range and accepted-step readiness. Material is granted per swing; no client counter supplies it. Persist node depletion, cycle transition, readiness and lots in the same world commit.
5. Next, execute the source ration counts atomically from actual held lots into two newly derived ration lots with ingredient lineage. A measured work duration or station can be added only as authored tuning; it is not necessary to preserve source crafting. Keep any initial stock and harvested stock distinct in provenance. Crafting's one wood and the quest's six wood cannot reuse the same consumed rights.
6. Add actor-owned quest acceptance, six-wood turn-in and exactly-once reward handling. Existing gifts to Wren are not retroactive completion of an unaccepted quest. Retaining source semantics means accepting any legally held six wood, not forcing them to be newly gathered. The test path should nevertheless exercise real gathering before crafting/delivery. If progression/currency authority is absent, retain the exact 420/200 obligation and label completion partial; never mint decorative reward counters and call the legacy quest fully imported.
7. Connect the delivery to an **authored construction job** with a funded six-wood reservation/transfer, explicit builder control and site permission, footprint/material definitions, work duration, revision/occupancy checks and permanent terrain/structure edits. A blueprint along the farm's north edge could reuse the source fence's visual motif while leaving approaches open, but neither its dimensions nor its bill of materials/time exists in source. Do not imply one abstract counted wood equals one solid cubic cell. Announce the windbreak only after the physical candidate commits; construction rejection must not lose the materials. Wren working with their own authorized material custody is a real NPC seam; a client command spending private NPC inventory without permission is not.

The next gathering increment needs concrete node definition/state, finite cycle supply, actor-owned work/readiness, owned tool binding and command/result fields. Crafting adds a real multi-input transformation; quest continuation adds acceptance/reward state; construction adds a site/blueprint/job and its conserved placement transaction. These should compose through the current world authority and typed journal rather than creating separate client authorities.

Minimum connected validation is gather from live nodes with real tools; reject range/capacity/stale-revision attempts without partial loss; observe depletion/renewal and final finite exhaustion; produce two rations from distinct input rights; deliver a different six wood once; build actual persistent occupied structure with no actor entombment or blocked sole route; reopen/replay exact state; and show dialogue/world visuals only from committed outcomes. Ration healing, full XP leveling, ecology, sapling weather protection and source population density remain separate pending features unless actually implemented.

## Retained presentation and evidence

Use the original `node_oak.png` and `node_berry.png` in [assets/units](../../../Runtime/content/legacy/aldermere-first-loop/assets/units/); both are 48×96 RGBA sheets. The existing [tile atlas](../../../Runtime/content/legacy/aldermere-first-loop/assets/tiles.png) contains the source fence at atlas cell `(5,5)`, a 16×16 rectangle beginning `(80,80)`, and farmland at `(6,5)`. These mappings come from supplemental [WorldRenderer.cs](../../../../src/Client/WorldRenderer.cs), line 45. There is no source completed-windbreak asset or harvested-stump sheet in the retained packet. Reuse source assets without generation, but identify any new adaptation and keep accepted final art separate.

This review read source and decoded JSON/RLE/PNG headers with Python. It did not build or run either game. Exact SHA-256 inputs:

| Input | SHA-256 |
| --- | --- |
| Frozen `records.json` | `9bfc3372954475402ea2fab30dab9de29b2a7c800f2fb61ab654d2efc81e973b` |
| Frozen `closure-manifest.json` | `d9630158849e2936d8b45d1f858347e62ac1af4e3a53d4837dac90bfb5338582` |
| Frozen `World.Gathering.cs.txt` | `dd5b97af267bfd08847481253534ab3156434a55cd504c186f30c33de07e2715` |
| Frozen `World.Systems.cs.txt` | `c9a9d49ca4021cded3f06c4568075cdf4956149b4736779d4ab4b7711325886e` |
| Frozen `World.cs.txt` | `fc55db3ec2c8a9602e0432a7359e460b8b6a6e92a4123620df4df5190eaea4e2` |
| Frozen `SimConstants.cs.txt` | `7bdcec4fa6f7017bab1cb9c7ff028ee7e28b15ae37bc4503174f91b89e431742` |
| Frozen `GameData.cs.txt` | `b5c47c56d41d0c17984df96a20a7a336b450ae45c1a52a610b7d460ad0873ab8` |
| Frozen `world.json` | `9f46be269ea80ddeef908d9a8305b70bf26a8ca11a544bed32c640fbfee7668d` |
| Frozen `classes.json` | `27c5956e9b269acdc36ecba339522c426380ea2f35bc33fead49ae778add6234` |
| Frozen `EntityView.cs.txt` | `505fc173ed6cf64ed69e82e43cca6d244502fe2e3d87b6199dfdfcce53c0785a` |
| Original `node_oak.png` | `3c95536649a836516f5bda0abc65dcd2d2b6a54611a8a0804c6ef133290188c8` |
| Original `node_berry.png` | `8928cce8c9bd2b1c197650cac6b467ccb9b35035531f64548ac7b76b39721457` |
| Original `tiles.png` | `401e2d79a74e8c82ae273a4c9a1e6bf1f2e400ea13f4252f622b559befcb1a0d` |
| Supplemental `src/Net/ServerNode.cs` | `72119d8781707526b051447cb761dfa6cc88c4723a2b19bb8abc6dd356510074` |
| Supplemental `src/Client/WorldRenderer.cs` | `acd3966b121e8a3faf37bf6a1739080bd633107d84711b3fa393632f412dfd95` |
| Current `ExposurePacingV1.cs` | `829e2e5ec602bed89dbb19dc58b235ff02920506b0a3b42fa97287eb9bb186b8` |

The complete frozen payload hashes remain attached to the exact record pointers in `records.json`; this note does not replace that manifest or reinterpret its source hashes as accepted runtime versions.
