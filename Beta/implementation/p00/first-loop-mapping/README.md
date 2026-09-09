# First-loop source-to-target semantic mapping

This P00 artifact makes one connected original-content spine accountable before its gameplay is implemented. It preserves exact source values and dependencies while identifying which **field shapes** existing contracts can express. No selected NPC, quest, material, resource node or recipe has been admitted as a playable runtime definition.

The source is the immutable [Aldermere first-loop packet](../../../Runtime/content/legacy/aldermere-first-loop/closure-manifest.json), with the earlier [migration closure evidence](../migration-closure/VALIDATION.json). That broader closure retained 49 structured records and two code definitions with 171 typed edges. The field mapping here deliberately covers a smaller connected subset; it does not replace that closure, assert complete migration, modify any source byte or promote an illustration into accepted tuning.

## Accounted scope

Every selected JSON record keeps its complete original payload and source identity, including original file, JSON pointer and payload SHA-256. Every non-root JSON node has an additional field row with its exact pointer/value/value hash, units, meaning, disposition and applicable dependency edges. Containers are retained as well as their children: **74 rows comprise 66 scalar leaves and eight containers**.

| Source key suffix in `openfantasy.aldermere` | Original file/pointer | Field rows | Role |
|---|---|---:|---|
| `npcs/farmer_wren` | `data/npcs.json#/npcs/116` | 21 | Primary NPC |
| `quests/wren_orchard` | `data/quests.json#/quests/44` | 11 | Primary Fetch quest |
| `items/wood` | `data/items.json#/items/12` | 4 | Quest and recipe ingredient |
| `items/berries` | `data/items.json#/items/15` | 4 | Recipe ingredient |
| `items/rations` | `data/items.json#/items/16` | 6 | Recipe output/consumable |
| `recipes/craft_rations` | `data/recipes.json#/recipes/1` | 10 | Primary production recipe |
| `nodes/101` | `data/world.json#/nodes/0` | 9 | Oak gathering context |
| `nodes/103` | `data/world.json#/nodes/2` | 9 | Berry gathering context |

The exact original `oak` and `berry` code-defined archetypes are also preserved as two code-context rows. These retain their original declarations and hashes rather than pretending that C# source text is an admitted content schema. The selected rows account for **28 original typed dependency edges** and **14 supporting source/asset endpoints**, including the appearance manifest and layers Wren uses. Code-defined tool requirements remain explicit `Axe`/`Sickle` source semantics. This does not claim that actual tool availability, craftable tools or a guaranteed route to the tools has been implemented.

The machine artifact is [mapping-v1.json](mapping-v1.json). Source strings are retained verbatim, including the empty NPC fallback sprite. Narrative text remains retained source content; flavor about wolves, lambs and weather is not proof that those events already exist in the new simulation. Asset preservation does not constitute acceptance of the new art style, animation coverage, native production files or engine presentation.

## What maps faithfully now

Exactly **seven fields** can map without inventing semantics: the three item keys, the recipe key, its two input counts and its output count. Existing `MaterialDefinition.Key`, `MaterialRecipeDefinition.Key` and `RecipeTerm.Count`/`RecipeMaterialRole` can express their original spelling and positive integer amounts. This is a field-shape mapping only. A complete definition still needs its actual accepted identity/version, physical parameters and related systems.

The recipe remains **two berries plus one wood producing two rations**. No waste, work, mass, volume, hardness or station requirement is inserted into that original payload. The separate `openfantasy.calibration` example and its authored residue/work values remain qualification examples as explained in the [production contract](../production-contracts/CONTRACT.md).

Four absent-field defaults have direct retained source-code witnesses: the recipe's omitted station is `None`; Wren's omitted shop is empty; the quest's omitted prerequisite and item reward are empty. These recover original meaning without authoring a new shop, quest-chain gate, item reward or forge requirement. They are the four defaults this artifact explicitly maps, not a claim to enumerate every property of the original C# classes.

The remaining field dispositions are **48 pending explicit transform/gameplay** and **19 retained source-only**. Pending fields are retained just as completely as mapped fields. A source-only narrative/appearance container is not discarded and can receive an explicit target binding when that system is implemented.

All target rows retain namespace `openfantasy.aldermere` and their exact source key. `definitionId` and `acceptedVersion` remain null; `runtimeAdmission` is `not_admitted`. A later compiler must allocate collision-checked identities under a reviewed source-alias policy and hash the canonical accepted definition after its explicit transforms. The original payload hash remains lineage metadata. It is neither an accepted runtime version nor a geological/renewable source claim. The mapping references 12 actual runtime declarations by exact file, declaration marker and SHA-256; their existence does not substitute for gameplay execution.

## Concrete remaining decisions from the source

| Source evidence and exact values | Required implementation decision or proof |
|---|---|
| Wren is Human appearance index `0`, with skin `5`, hair style `5`, hair color `13`, outfit `8`, colors `10/10`, accessory `3`; the original spawn uses HP `1`, level `1`, speed `1`, movement speed `2`. | Retain the appearance identity and layers while explicitly authoring new body dimensions, health, movement eligibility and actor depth. Source NPC placeholders cannot be promoted into combat statistics, personality or the authored humanoid candidate profile. Native sprites must preserve Wren's recognizability while receiving reviewed animations, clothing/body bindings and expression coverage. |
| Wren is at tile `(103,322)`, wander radius `2`. The source uses 32 units/tile and tile centers, giving `(3312,10320)` source units. Oak region is `(95,315,60,60)`; berry region `(100,330,50,50)`. | Define the source-to-world embedding: coordinate units, realm, branch, origin, elevation and retained geography. `HistoryAddress` alone does not settle tile scale, collision, navigation or reachable placement. A two-tile old leash cannot supply cognition, desires, schedules or a migration policy. |
| Oak requires an axe and has HP `5`; berries require a sickle and have HP `4`. Every successful gathering invocation spends `400` source ATB, decreases node HP by one, and attempts to add `tool.ToolPower` yield. Range is `56` source units. | Define legal tools, work/capability, exposure costs, reservoir quantities and actual source claims. Source HP is a number of depletion swings, not physical hardness. Yield occurs **per swing**, not only on depletion. Tool power must not silently become target work speed, hardness or mass. |
| The source attempts 12 oak and eight berry nodes; each placement tries at most 32 random walkable, non-safe candidates and may fail. Respawn delays are 900/700 ticks. Source tick rate is 20/s, so these are 45/35 source seconds after depletion. | Prove the new seed supplies a reachable onboarding resource route and required tools. Choose ecology/growth and renewal-cycle semantics, plus typed exposure/calendar scheduling. Repeated original respawn cannot mint independent source rights. No conversion to the new day length or tactical Wait follows from the source tick constant. |
| A full source inventory still spends gathering ATB and reduces node HP; failed respawn placement can leave the node absent. | Preserve these as original behavior evidence, then decide explicit target capacity/failure behavior. Transactional admission, item protection and consistent player feedback must be designed; silently retaining or silently changing this behavior would both be unreviewed. |
| Wren's Saplings is a `Fetch`: minimum level 1, six wood, 420 XP and 200 gil. The source consumes six wood on turn-in to the giver. | Implement actual eligibility, legal owned-lot selection, reservations, revision checks, funded rewards and atomic completion/receipts. Tune progression/DC/onboarding explicitly; old level/XP/gil values are retained evidence, not a new balance curve. The source's quest XP side effects must not become an implicit universal rest/healing rule. |
| The quest offer requests wood for a windbreak and the turn-in says the wind can howl. The original completion changes inventory/rewards/quest state; it does **not** build a windbreak or protect simulated saplings. | The faithful first quest can prove a six-wood delivery. If the new narrative claims a constructed windbreak, attach a real construction/job outcome and its world/plant effects. Also define continuation if Wren dies, leaves, changes allegiance or delegates receipt; neither the retained text nor a simple giver ID solves that simulation requirement. |
| Recipe inputs are `berries ×2`, `wood ×1`; outputs `rations ×2`, station defaults `None`. Source crafting is immediate inventory removal/addition with a failed-output refund attempt. | Decide whether wood is packaging, a material ingredient, fuel or another explicit process role. Author accepted material measures and process steps/costs accordingly. Combustion requires real source/sink operations; an untransformed counted lot cannot represent mixed transformed provenance. Implement interruption, capacity, claims and output/waste placement before claiming executable crafting. |
| Rations heal 30 source HP and five MP, each capped by deficit. Static prices are wood `6`, berries `5`, rations `25` gil. | Define target resource pools, legal consumption, effect transactions and economic units/quotes. These values cannot become work, mass, learning or a guaranteed merchant price through a rename. Wren has no shop in the source; a line about paying gil does not create one. |

The six wood delivered to Wren and the one wood used in each ration batch are separate consumption obligations. The same real lot quantity cannot satisfy both after it has been consumed. This small loop is therefore a useful future proof of shared custody across gathering, production and quests rather than three independent counters. NPC dialogue and visible windbreak/sapling changes should ultimately respond to the committed world effects; that integration remains to be built.

No new numerical tuning is accepted by this artifact. Straightforward key/count copies and witnessed defaults are resolved; physical calibration, currency/progression transforms, coordinate/time embedding, tool access, source renewal, quest-world consequences and complete actor behavior remain explicit decisions for the implementation increments that own those systems.

## Validation

From the repository root:

```sh
python3 Beta/implementation/p00/first-loop-mapping/validate_mapping.py
python3 Beta/implementation/p00/first-loop-mapping/build_mapping.py --check
python3 Beta/implementation/p00/first-loop-mapping/test_mapping.py
```

The builder prints the generated JSON to stdout by default. Updating the artifact requires an explicit `--output` path; validation and `--check` never overwrite the expected artifact. The validator is independent of the builder's functions and field enumeration. It resolves actual retained JSON pointers, compares every original payload and field, verifies hashes and exact code witnesses, checks exhaustive selected-field coverage and selected dependency edges/endpoints, and independently pins allowed target declarations and semantic-evidence identities. Unknown target labels, relabeled real classes, invented transforms, forged identity/version/admission claims and incomplete coverage reject.

The retained run passes **56 Python tests**: five positive/reproducibility/immutability checks and **51 negative mutations**. Negative checks include changed source values with forged replacement hashes, missing containers/leaves, duplicate rows, changed dependency endpoints, forged code text, actual-but-wrong target classes, wrong recipe roles/counts, unsupported gameplay conversions, calibration substitution, missing or irrelevant source witnesses and invented defaults. The exact commands, outputs, input hashes and counts are retained in [VALIDATION.json](VALIDATION.json).

These checks establish source accountability and a bounded review artifact. They do not execute an importer, materialize actor/material definitions, authenticate custody, run a quest, gather/craft/consume items, prove a playable route, test native art, qualify performance or assert a complete migration. No .NET build or Git mutation is performed by this artifact task.
