# Ration production: source semantics and proposed physical admission

Use **cold berry portions packed in split-wood containers** for the first real ration transform. Preserve the original `2 berries + 1 wood → 2 rations` counts. Explicitly retain the wood offcuts as a third, authored waste output. This gives the existing P00 mass calibration a concrete interpretation without inventing combustion, atmosphere, drying or a fire station. It is a proposed target rule for the next native increment, not a claim that the original game defined these physical meanings or that production already executes.

## Exact source behavior

The frozen [recipes.json](../../../Runtime/content/legacy/aldermere-first-loop/source-support/data/recipes.json:15), JSON pointer `/recipes/1`, contains:

```json
{"id":"craft_rations","output":"rations","outputCount":2,"inputs":[{"item":"berries","count":2},{"item":"wood","count":1}]}
```

The canonical source payload hash is `c384027c76855ade482105233cadd07ea930a265f73e12beab852de1cc756491`. The associated [item records](../../../Runtime/content/legacy/aldermere-first-loop/source-support/data/items.json:96) are:

| Source pointer | Preserved fields | Source payload SHA-256 |
| --- | --- | --- |
| `/items/12` | Wood; Material; price 6 | `613f7643e208f8da2a49c4fac27cc3671a6b6fbc1b1a0e39a7e3f0f29af0505d` |
| `/items/15` | Berries; Material; price 5 | `a7b53e8648a12bb156269fe955ea868aeab9881ba75fb4b79bba057358a93214` |
| `/items/16` | Rations; Consumable; price 25; healHp 30; healMp 5 | `c40722d6d24262136de44bf2eaa2c27d67510ca1455ab3fccc4b9fa2c2e05030` |

[RecipeDef](../../../Runtime/content/legacy/aldermere-first-loop/source-support/src/Sim/GameData.cs.txt:124) defaults an omitted station to `None`. [CraftItem](../../../Runtime/content/legacy/aldermere-first-loop/source-support/src/Sim/World.Gathering.cs.txt:173) checks recipe existence, a forge only for forge recipes, and current ingredient counts. It removes the inputs, attempts to add the output, emits a loot event and returns `Crafted 2× Rations.` There is **no ration station, owned-tool, ATB, duration, work, knowledge, class, skill, quality or explicit living-body requirement** in that method. It is an immediate inventory operation. The current predecessor server's `CraftOp` accepts the session's entity and recipe ID; it adds no such craft prerequisites. Those server/UI files are supplemental working-tree witnesses, not additions to the frozen source closure.

The original inventory code has a concrete failure to correct: [AddItem](../../../Runtime/content/legacy/aldermere-first-loop/source-support/src/Sim/World.Systems.cs.txt:88) can partially add before returning false, contrary to its comment. With all 24 slots occupied, a ration stack at 98, and berry/wood stacks that remain nonempty after ingredient removal, crafting adds one ration to reach the 99 stack limit, then fails to place the second. The refund loop restores the ingredients but leaves that added ration. The target must preflight the complete post-consumption placement and commit everything atomically; this is an explicit correction, not a behavior to preserve. `RemoveItem` walks stacks in reverse list order; the target should choose eligible exact lot intervals deterministically instead of importing that incidental list order.

For the first executable command, retain immediate source behavior and require current control, exact recipe/input revisions, actual manipulation and eligible owned ingredients through the existing authority. Body/control/protection checks are deliberate target requirements. Do not silently turn the P00 illustrative **120 work units** into 120 ticks, milliseconds, gathering effort or a six-second job. The coordinator-approved upcoming atomic wrapper requires a new physical recipe with `RequiredWork == 1`, meaning **one complete operation**, not one time unit. A source-faithful atomic command has no added work/ATB delay; its enclosing selected advancing step contributes the normal calendar interval once. Held/offline state does not execute it. The P00 120-work definition remains unchanged as an accounting witness; a later timed-production profile needs its own explicit work rule, interruption and completion state.

## Concrete proposed calibration

The [P00 calibration](../../p00/production-contracts/craft-rations-calibration.json) is retained unchanged. The following interpretation uses its numeric masses, occupied volumes and hardness ratings, while making the composition explicit:

| Role | Unit meaning | Count | Mass/unit (mg) | Occupied volume/unit (µL) | Hardness |
| --- | --- | ---: | ---: | ---: | ---: |
| Input | One berry portion | 2 | 20,000 | 30,000 | 1 |
| Input | One prepared, splittable wood portion | 1 | 50,000 | 100,000 | 8 |
| Output | One packed ration: 20,000 mg berries and 20,000 mg wood container | 2 | 40,000 | 45,000 | 2 |
| Waste | Recoverable wood offcuts; no berry matter | 1 | 10,000 | 25,000 | 0 |

```text
Input:    2 × 20,000 + 50,000 = 90,000 mg
Rations:  2 × (20,000 berry + 20,000 wood) = 80,000 mg
Offcuts:  10,000 mg wood
Output + waste = 90,000 mg
```

No wood becomes edible matter and no matter disappears. Input occupied volume totals 160,000 µL; packed output plus loose offcuts totals 115,000 µL. These are authored occupied-volume/packing calibrations, not measured material densities or a claim that constituent solid volumes vanish. The operation rearranges packing space; all volume products, totals and destination capacities still need checked validation. Count capacity 32 is **not** a mass or volume limit. The values describe small qualification portions, not daily nutrition, a full meal or the entire mass of an oak sprite. Hardness is the existing game ordinal, not a physical cutting-energy measurement.

Fuel would require additional explicit atmospheric reactants and gas/ash outputs; the existing 90,000 mg recipe has none. Therefore the first operation does not burn the wood or invoke heat/fluid simulation. Changing to cooked rations later requires a newly pinned recipe and material accounting rather than silently changing this batch's interpretation.

## Preserve existing item pins; add physical bindings

The current native admission deliberately gave wood and berries abstract counted pins. Keep both identities **and versions** exactly:

| Current item | Definition ID | Existing version |
| --- | --- | --- |
| Wood | `f20a24bbcf04b36ebcb90a1d04ca58c0` | `c603d104fd21c40819d8dc0f6a4f5f3bc976936f7ac2c2cfe6ffefd62820614d` |
| Berries | `2038079b78c92d2f77fb1443eed73661` | `2a558e18e3a30b09446b6f0b25da2c2bcbca7ef8676459eab071ff27c8a286c6` |

These pairs were independently reproduced from the existing [base admission](../../../Runtime/content/native-farm/admission-v1.json:273), [gathering admission](../../../Runtime/content/adventure-farm/admission-v1.json) and their canonical ID/JSON-hash rules. They are distinct from source payload hashes.

`MaterialDefinition.Pin()` currently hashes the physical definition, and `MaterialCatalog.Resolve` demands that exact hash. Consequently, constructing a physical `MaterialDefinition` with an existing wood ID does **not** preserve its current version. Do not replace the lot pin, rewrite an admitted source, alias the old P00 numeric material IDs to existing items, or pretend the catalog already resolves these abstract pins.

Use the approved narrow `ItemPhysicalBinding(CountedItemDefinition item, MaterialDefinition physical)`: exact existing item pin, exact physical material ID/version and **one item unit to one calibrated unit**. Admission separately validates the source payload lineage and new packing interpretation. `CraftRecipeDefinition(definitionId, physicalRecipe, bindings, allocations)` pins those joins and exact per-output input allocations; its identity is distinct from the enclosed physical recipe identity. The newly authored physical recipe uses the calibration's numeric values and exact source recipe-payload hash, with the explicit one-operation work rule above. The upcoming world's production profile must admit the bindings, not accept arbitrary client-selected mass. Its actual inputs remain the original counted item pins. The old admission records remain immutable historical scope; a new admitted production component adds the physical interpretation explicitly. Existing saves must not acquire it through a silent decoder default.

Rations have a preserved source record but no currently admitted gameplay output pin in the gathering profile. Introduce a stable output item definition once, retaining the ration source payload lineage and the new packaged meaning. Wood offcuts are a newly authored item with their own identity/version. Neither output is a fresh `AdmittedCountedSource`: both are transformations of the actual consumed stock.

## Exact transformed provenance and placement

Consume precisely two berry units and one wood unit from currently owned, unreserved eligible intervals. The existing `CountedSourceLot` represents only an **untransformed integer source interval**; it cannot faithfully hold these composite outputs.

| Retained product unit | Berry allocation | Wood allocation |
| --- | ---: | ---: |
| Ration output ordinal 0 | First selected berry unit: 20,000 mg | 20,000 mg of the selected wood unit |
| Ration output ordinal 1 | Second selected berry unit: 20,000 mg | 20,000 mg of the selected wood unit |
| Offcut output ordinal 2 | 0 | Remaining 10,000 mg of that wood unit |

These are individual product units. In the approved recipe API, output **term index 0** is the two-unit ration term and **term index 1** is the one-unit waste term. Therefore the three `OutputMaterialAllocation` entries are berry 20,000 mg per unit → term 0; wood 20,000 mg per unit → term 0; wood 10,000 mg per unit → term 1. This distinction avoids confusing the second ration's unit index with the waste term's index.

The original wood interval is consumed **once**. Preserve a permanent transformed batch naming exact input claims, their calibration/recipe pins and these nonoverlapping allocations, either explicitly or through a pinned deterministic recipe/ordinal allocation. Giving every output the full original wood interval would multiply its rights. Source cursors and consumed-claim history must still prevent the original interval from being issued or consumed again. Recycling the offcuts cannot reconstruct a fresh full wood source unit without a separate valid transform. Unsupported fractional or composite provenance must not masquerade as an untransformed cross-branch export.

For this bounded first batch, at most three selected source intervals are needed: two independently selected berry units and one wood unit. Reserve/preflight all three resultant product units, including the offcut, before modifying inputs. A full destination cannot trigger an automatic waste drop or deletion. Persist batch, products/waste, remaining source lots, revisions, historical identities and terminal receipt through the same world authority. Pause, retry, crash recovery and rejection must never duplicate output or partially consume input. The next runtime design must settle transformed storage totals and placement, rather than treating the existing count-capacity check as physical admission.

## Eventual use and presentation

[UseItem](../../../Runtime/content/legacy/aldermere-first-loop/source-support/src/Sim/World.Systems.cs.txt:172) requires Consumable, living actor and one owned item, removes one item, then restores HP by `min(30, HP deficit)` and MP by `min(5, MP deficit)` for Rations. Only positive HP gain emits a heal event. Because the full-health rejection applies only when `HealMp == 0`, Rations can be consumed at full HP **and** full MP for zero effect. The source provides no hunger, nutrition, container return, metabolism, spoilage or eating animation. Its generic strings even say “drink.” Keep the exact 30/5 source effects as pending semantics, not an immediate energy/rest bonus.

Under the new packaging interpretation, eventual eating must preserve the 20,000 mg wood container and account for the 20,000 mg berry matter entering the actor/consumption process; deleting a whole 40,000 mg ration would lose both. The first native craft can create and display actual rations without claiming that actor metabolism or HP/MP use is implemented. If target use later refuses zero-benefit consumption, label that as a deliberate usability correction.

No dedicated ration sprite or item-icon mapping was found in the inspected original asset filenames, frozen item definition or current predecessor UI. `GameWindows.BuildCraft` and inventory rows render item names/counts; a craft loot event becomes a text chat line in `GameController`. The node-berry atlas is a world resource plant, not a ration asset. Use truthful text/count feedback for the first craft; any packed-ration/offcut art and hand-work choreography belong to the separate artist pipeline and require style acceptance. Asset inspection tools are available; none were needed to invent or transform art for this source task.

## Evidence and scope

This task read source and existing contracts and used Python only to reproduce item pins, source hashes and the integer mass/volume totals. It performed no runtime edits, source-archive changes, .NET/Godot build or craft execution. Selected witness SHA-256 values are retained here so current working-tree UI evidence cannot be mistaken for a frozen source record:

| Witness | SHA-256 |
| --- | --- |
| Frozen `data/recipes.json` | `0c81bcf33088668a7837e77cf01640cbfd2070e2a8caf9fa13ebdeb1e032a6b1` |
| Frozen `data/items.json` | `ac0944b98fbfc10474ae1d1309bd7ad6b74c511fb2da9c314ab41f6c8a655609` |
| Frozen `World.Gathering.cs.txt` | `dd5b97af267bfd08847481253534ab3156434a55cd504c186f30c33de07e2715` |
| Frozen `World.Systems.cs.txt` | `c9a9d49ca4021cded3f06c4568075cdf4956149b4736779d4ab4b7711325886e` |
| Frozen `GameData.cs.txt` | `b5c47c56d41d0c17984df96a20a7a336b450ae45c1a52a610b7d460ad0873ab8` |
| Frozen `SimConstants.cs.txt` | `7bdcec4fa6f7017bab1cb9c7ff028ee7e28b15ae37bc4503174f91b89e431742` |
| P00 `craft-rations-calibration.json` | `2781a9fa555eeabd0a97a4d9d482b25b75632f1b89757963a000a0f4f295ebe5` |
| Current `src/Client/GameWindows.cs` | `7e1b67bdd4432fb646111458ee5f6b8a009ed35f39edb1e5134209954ba2d525` |
| Current `src/Client/GameController.cs` | `f2c5b2070fd77cbaa7350f2e1e689907a955511a77208f170f37fc56824fc483` |
| Current `src/Net/ServerNode.cs` | `72119d8781707526b051447cb761dfa6cc88c4723a2b19bb8abc6dd356510074` |
