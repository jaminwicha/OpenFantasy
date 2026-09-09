# First retained legacy loop

RQ-085/087/088/098/111/119/137/171. Status: source-inspected plan; no playable import is claimed. The [selection manifest](legacy-first-loop-inputs.json) preserves **31 exact source records and fingerprints 22 source files** against the 201-file inventory. It is a selected starting set, not a dependency-closed package. Every original field remains intact, including references to unselected definitions.

## Actual source spine

Use **`farmer_wren`**, not another character merely named Wren. The source contains several different Wrens. This Wren lives at tile (103,322), within `farm_verdance_small` at (100,320), 9×7 tiles, north of Brasshollow. His `wren_orchard` quest, **Wren's Saplings**, requests six `wood`, pays 420 XP and 200 gil, and describes a windbreak. These are source facts; their future rule-unit transforms and reward tuning must be recorded.

The source gathering code defines `oak` → `wood` with an axe, `berry` → `berries` with a sickle, and `ore` → `iron_ore` with a pickaxe. The selected oak placement 101 and berry placement 103 overlap the Brasshollow area. `craft_rations` consumes two berries and one wood to produce two rations. Marla's actual shop lists the copper axe, sickle and rations. The retained Squire class references Strike, Cleave, Breaker and Rally; all four ability records are included so this class's direct ability references remain preserved. The selected Bramble Wolf retains its own statistics, attack and wolf-pelt/potion drop references.

A small playable sequence can therefore bind real source identities: meet Wren → obtain appropriate tools → gather from actual plants → choose how to allocate wood between food and the six-wood promise → deal with an encountered threat using the retained class/action → make rations → deliver the committed materials → return to an observable changed farm. This is a designed successor sequence, not a claim that all these links already form an authored source quest.

## Retained source versus authored expansion

| Element | Source evidence | Required new realization |
|---|---|---|
| Wren and quest | `data/npcs.json#/npcs/116`; `data/quests.json#/quests/44` | Same accountable actor with motives, knowledge, possessions, duties, relationships and changed-world continuation; quest turn-in cannot conjure inventory or duplicate reward |
| Windbreak | Quest prose requests wood and says it protects saplings | Author a versioned construction/repair process, real geometry/material custody, finite labor and exposed condition. A completed fetch is not already a built windbreak |
| Gather and make | `World.Gathering.cs` archetypes and `craft_rations` | Retain recipe identity/source quantities, explicitly translate tick/range units, replace free respawn with source-budgeted ecology, declare yield/fuel/byproducts and interrupted work |
| Recruit and cooperate | Full specification requires recruitment and synergies | Author a consenting helper's actual availability, obligation/cost, shared work/action and interruption rules; source Wren is not automatically an available party member |
| Threat | Actual Bramble Wolf data and source encounter distribution | Preserve recognizable identity and compatible retained encounter case; add hunger/fear/territory/knowledge so wolf identity does not imply universal hostility |
| Prices and markets | Toma in Brasshollow and Hilda in Gearford both list rations; source items have fixed prices | Separate real finite stocks, quote revisions, costs, cargo ownership, local supply/demand and merchant routes. Price difference must come from declared rules/starting state, not scripted profitable completion |
| Travel | Retained Brasshollow/Gearford ports, Wick, canoe recipe and canoe item | Validate a legal land/water itinerary and actual embark/cargo state. Two distant source towns do not prove path availability, travel time or viable early balance |
| Missing cargo / field puzzle | Required by the current retained P02 plan | Author an incident with actual culprit/motive/evidence and at least two valid clue/skill routes, plus a real action puzzle and accessible alternative; label these new content |
| Geology / build | Source region/node placement | Add actual strata, supported tunnel/upper floor/stairs, river outlet and dry/flooded cave. Preserve source place relationships while recording added geometry and site attachment |
| Teaching / performance | Wren's source dialogue, appearance indices and original visual files | Real observe/practice/use/combine assistance, optional expert detail, readable skill/tool/action motion and meaningful audio; no actor knowledge from UI tutorial state |

## Bounded P00 import assignments

1. **Content author:** assign the reviewed namespace/stable-key convention once; preserve original payload and inventory key as provenance. The provisional namespace is `openfantasy.aldermere`. Do not rename an older record to hide a conflict. Record split/consolidated aliases and every field transform.
2. **Content compiler owner:** compute complete transitive references for chosen roots before an import can pass. The manifest includes full NPC shops/quest lists and full class references; unselected references are deliberately visible. Either include their complete definitions or declare a supported package dependency. Never trim original fields, invent defaults or mark the 31-root selection imported while references dangle.
3. **Rules author:** specify source→new attribute, damage/range, clock, currency, stack/mass/volume, resource-respawn and vehicle mappings with named compatibility rationale. Source tile/unit settings are in `SimConstants.cs`; do not infer that old range 48 is 48 metres. Keep source and new rules versions independently recoverable.
4. **World/content author:** bind original places and people to a valid supported start, then author the actual windbreak, helper, two-market route, puzzle/clues and bounded geological/ecological additions. Produce geometry/availability/resource/knowledge witnesses before describing the route as playable.
5. **Migration critic:** compare payload hashes and references, then verify one clean start, one interrupted/restarted job, duplicate turn-in, NPC-first resolution and unavailable/dead Wren continuation. Only actual P02 execution can close playable incorporation.

Preserving all source metadata is necessary even when only a narrow action is executable. The first source packet is not permission to omit the remaining **532 structured records, 10 indexed code-defined entries, 187 prose headings or 89 original asset files**. These are different denominators. P11 closes the entire import and newer source deltas; each earlier phase retains and expands this same package.

## Native production assignments

Artist agents own editable original work and acceptance proposals. The source selection preserves Wren's body/hair/outfit/accessory files and palettes, a Squire, wolf, resource plants, canoe and terrain sheet as provenance/calibration inputs. A filename or matching hash does not establish usable action directions, tile seams or quality. Proposed first native kit: one person, one creature, one non-hostile animal and one interactive object, with distinctive idle, walk, talk/gesture, gather/work, combat, damage/condition and interruption where applicable. Avoid an unbounded Cartesian variant set; enumerate incompatible body/equipment/action combinations and deliberate reuse.

The audio owner prepares editable original ambience/action/music sources for farm/town, wilderness, field encounter/aftermath and cave/shelter. Include tool/material contact, footsteps, wood work, wind/rain, an ecologically valid bird cue, gentle transition and intentional quiet. The client consumes semantic events with listener permission and deduplication; artists/audio authors cannot award quest outcomes. Human acceptance of native motion and listening is still required before bulk production.
