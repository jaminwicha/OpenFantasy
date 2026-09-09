# Independent first-loop mapping review

Review date: 9 September 2026. Reviewer: the language/history-contract worker, independently reviewing the first-loop mapping worker's artifacts. Outcome: **no remaining blocking findings for this source-accountability artifact**. It is not an importer or playable content qualification.

## Reviewed source and destination claims

I read the builder, independent validator, adversarial tests, frozen mapping, README and retained validation evidence. I checked the selected record values and their original pointers, complete nested field coverage, source dependencies, actual target declaration markers, missing-field defaults and relevant retained source-code behavior.

The mapping covers six primary JSON records (Wren, Wren's Saplings, wood, berries, rations and the ration recipe), two resource-node context records and two exact code archetypes. Its 74 field rows account for 66 scalar leaves and eight containers, preserving array positions and the original payloads. The selected outgoing closure retains 28 typed edges and 14 supporting endpoints. This is a subset of the earlier 49-record/two-code-record preservation packet, not a replacement for it or evidence of complete migration.

Only seven fields claim existing target field representation: four local item/recipe keys and three recipe counts/roles. Every complete target record still has no accepted definition ID/version and remains `not_admitted`. The distinction is clear in both the mapping and README. Source payload hashes remain lineage; authored calibration and body-family examples are not silently imported as real species, statistics or physical values.

I examined original code around NPC tile-center placement/home leash, Fetch turn-in consumption/rewards, XP level-up restoration, resource placement attempts, per-swing gathering yield and costs, depletion/respawn, crafting's failed-output refund attempt and capped ration healing. The source appearance manifest identifies race index zero as Human. These checks substantiate the mapped meanings while retaining explicit future decisions about geography, actor depth, legal tools, resource renewal, actual construction, custody, prices, progression and process interpretation. Asset hashes were verified as preserved dependencies; I did not perform a native art or animation acceptance review.

## Review findings and resolutions

Two independent-validator concerns were raised before the owner froze the artifacts:

1. A self-declared target label could originally be replaced by a different existing Core file/marker with a matching file hash. The validator now independently pins all twelve target labels to exact paths and declaration markers. The negative test substitutes the real recipe-key declaration for material-key and requires `MappingError`.
2. Semantic evidence could originally be weakened or relabeled through self-declared markers. The validator now independently pins the complete seven-witness set, source paths and exact marker sets, plus the fixed missing-default witness. Negative mutations cover missing witnesses, unrelated real evidence, empty or absent markers and the wrong default witness.

Both concerns are closed in the exact hashes below. No mapping source or tests were edited by this reviewer. Natural-language explanation truth remains a reviewed claim; the structural validator does not prove arbitrary prose or future gameplay semantics by itself. The frozen builder comparison also guards the authored mapping rules and explanations against unintended drift.

## Independently executed checks

I ran these commands with `PYTHONDONTWRITEBYTECODE=1` from the repository root:

```text
python3 Beta/implementation/p00/first-loop-mapping/test_mapping.py
python3 Beta/implementation/p00/first-loop-mapping/validate_mapping.py
```

Both exited zero. The test runner independently passed **56 tests: five positive/reproducibility/immutability checks and 51 negative mutation cases**. The positive tests compare builder stdout with the frozen artifact, exercise read-only `--check`, confine explicit output to a temporary file and verify validation does not mutate the document. Negative cases mutate real valid documents and specifically require `MappingError`; they are not counted merely because an unrelated exception occurred.

Direct validation reported 74 field rows, 28 dependency edges, twelve target references, seven source-evidence records and four witnessed missing defaults. Dispositions are seven representable field mappings, 48 pending explicit transforms/gameplay and nineteen retained source-only rows. I also checked the author's retained evidence input hashes against current file bytes.

I did **not** execute a runtime importer, instantiate the mapped NPC/quest/material definitions, run gathering/crafting/consumption, authorize custody, test a seeded playable route, launch Godot or run a .NET build for this review. Those remain separate implementation/qualification work. The artifact does not claim that all original content has already been ported into playable authority state.

## Exact final artifact and witness hashes

Paths are repository-relative. This set includes the seven frozen mapping artifacts, four source-packet indexes/manifests, exact referenced Core declarations, retained semantic source files and selected asset/data endpoints. Referenced dependency hashes establish preservation scope, not visual or gameplay acceptance.

| File | SHA-256 |
| --- | --- |
| `Beta/Runtime/content/legacy/aldermere-first-loop/assets/layers/acc_03.png` | `0907b9dd49e4553eae718bb47f4f84e30c53637483570536016ed59055eb3048` |
| `Beta/Runtime/content/legacy/aldermere-first-loop/assets/layers/body.png` | `17a3af88e92baa615c9bb3f4d21e095767639458c655348a6b93080e82fe40e7` |
| `Beta/Runtime/content/legacy/aldermere-first-loop/assets/layers/hair_05.png` | `b636e8e6acd8c9282a73a09a5e3956baeeb5bf2ea6fcfc0528ad187db965dd5c` |
| `Beta/Runtime/content/legacy/aldermere-first-loop/assets/layers/outfit_08_a.png` | `bec72107f5dcbbacbc79b42266e2d603b6173d60f1d5c25f4f342f261796e52a` |
| `Beta/Runtime/content/legacy/aldermere-first-loop/assets/layers/outfit_08_b.png` | `90f50e5210177c981efdde1e816b867aa0be7c224192c9814338160ba4028817` |
| `Beta/Runtime/content/legacy/aldermere-first-loop/assets/units/node_berry.png` | `8928cce8c9bd2b1c197650cac6b467ccb9b35035531f64548ac7b76b39721457` |
| `Beta/Runtime/content/legacy/aldermere-first-loop/assets/units/node_oak.png` | `3c95536649a836516f5bda0abc65dcd2d2b6a54611a8a0804c6ef133290188c8` |
| `Beta/Runtime/content/legacy/aldermere-first-loop/closure-manifest.json` | `d9630158849e2936d8b45d1f858347e62ac1af4e3a53d4837dac90bfb5338582` |
| `Beta/Runtime/content/legacy/aldermere-first-loop/records.json` | `9bfc3372954475402ea2fab30dab9de29b2a7c800f2fb61ab654d2efc81e973b` |
| `Beta/Runtime/content/legacy/aldermere-first-loop/references.json` | `a00865fabaf0637b929d5b8ab3ff2f5a66c0f258e4068a4d7ef0dee7a8396076` |
| `Beta/Runtime/content/legacy/aldermere-first-loop/source-key-map.json` | `fa56980272b65895595b4989b5d55a5529441347f239d28ad3dc43919d8f854a` |
| `Beta/Runtime/content/legacy/aldermere-first-loop/source-support/data/appearance.json` | `3c9388eddd5a1343065f2e8d476f570a7cf0cf6c92998fedfdc0254df4761a9b` |
| `Beta/Runtime/content/legacy/aldermere-first-loop/source-support/data/items.json` | `ac0944b98fbfc10474ae1d1309bd7ad6b74c511fb2da9c314ab41f6c8a655609` |
| `Beta/Runtime/content/legacy/aldermere-first-loop/source-support/data/npcs.json` | `a75e0f3e565e21cd751dc3697f948b773439935478053c29d5467e868d439746` |
| `Beta/Runtime/content/legacy/aldermere-first-loop/source-support/data/quests.json` | `3ce9eabab223fc66131a9e048a43e47c8c745d2fe6e33542e762291a74148045` |
| `Beta/Runtime/content/legacy/aldermere-first-loop/source-support/data/recipes.json` | `0c81bcf33088668a7837e77cf01640cbfd2070e2a8caf9fa13ebdeb1e032a6b1` |
| `Beta/Runtime/content/legacy/aldermere-first-loop/source-support/data/world.json` | `9f46be269ea80ddeef908d9a8305b70bf26a8ca11a544bed32c640fbfee7668d` |
| `Beta/Runtime/content/legacy/aldermere-first-loop/source-support/src/Sim/GameData.cs.txt` | `b5c47c56d41d0c17984df96a20a7a336b450ae45c1a52a610b7d460ad0873ab8` |
| `Beta/Runtime/content/legacy/aldermere-first-loop/source-support/src/Sim/SimConstants.cs.txt` | `7bdcec4fa6f7017bab1cb9c7ff028ee7e28b15ae37bc4503174f91b89e431742` |
| `Beta/Runtime/content/legacy/aldermere-first-loop/source-support/src/Sim/World.Gathering.cs.txt` | `dd5b97af267bfd08847481253534ab3156434a55cd504c186f30c33de07e2715` |
| `Beta/Runtime/content/legacy/aldermere-first-loop/source-support/src/Sim/World.Systems.cs.txt` | `c9a9d49ca4021cded3f06c4568075cdf4956149b4736779d4ab4b7711325886e` |
| `Beta/Runtime/src/OpenFantasy.Core/Actors/ActorFormBinding.cs` | `f7c27d278f63a6bc40f92e1ec58e2bd4882cee1be3e118518a47191eb9544670` |
| `Beta/Runtime/src/OpenFantasy.Core/Actors/BodyDefinition.cs` | `878dc032f0ef6228a421a3a72290c4590466ee44fcde66fa0eec6199d0188423` |
| `Beta/Runtime/src/OpenFantasy.Core/Content/ContentHash.cs` | `279bcb92c2a19dffa74bcb9f1a7f13e709b2a2a7943ccc87e454f6e726d3bc45` |
| `Beta/Runtime/src/OpenFantasy.Core/Content/PackageId.cs` | `1292adfcc7493455a31b133b108f74d19389a577d0dd0b4df35f84738e406421` |
| `Beta/Runtime/src/OpenFantasy.Core/Custody/CountedItemDefinition.cs` | `45fdd14ec87f906ba5cb5905652c12ea2f2e43d85a668da79232fbd073e357f6` |
| `Beta/Runtime/src/OpenFantasy.Core/EntityId.cs` | `85211ece11a8715cd530ad5abf7c922935e7a4b0505ab3bffbaa369759c7190c` |
| `Beta/Runtime/src/OpenFantasy.Core/History/HistoryAddress.cs` | `f85eb1c573e245a7f7741bc3998a84ba5479a55f545a5a19579d77360e54c56f` |
| `Beta/Runtime/src/OpenFantasy.Core/Production/MaterialDefinition.cs` | `ae5e62794834506c243f5e11893d0f93347827381ae8ea4d33d9c038c56de705` |
| `Beta/Runtime/src/OpenFantasy.Core/Production/MaterialRecipeDefinition.cs` | `47665e2842ddd388365bba7ec33d48a48b98c81e0d7cd9a3c355657339dd5331` |
| `Beta/Runtime/src/OpenFantasy.Core/Production/RecipeTerm.cs` | `d3bf4717a3c639da0f726ed1017c402c4de86fb7c33b94a76f8120b286d286be` |
| `Beta/Runtime/src/OpenFantasy.Core/Scheduling/CalendarClockCoordinate.cs` | `e4a720723256cc911f4dcb26c57db4f4bd952bacd0c0280bfdfd3865d9c4eaae` |
| `Beta/implementation/p00/first-loop-mapping/.gitignore` | `32dae3052f331ee34d628ef535709b301259a45df7c7522c4d35dcf49873f00b` |
| `Beta/implementation/p00/first-loop-mapping/README.md` | `745494a5a2fc600692dd54a5d6ee78a84a831574ae477ae358becb6e2166a3e3` |
| `Beta/implementation/p00/first-loop-mapping/VALIDATION.json` | `528110aba4ef757548548a1b944343cb05a5e059dad8ff766c121370cbcee74f` |
| `Beta/implementation/p00/first-loop-mapping/build_mapping.py` | `5b87eaf6738f7d9c9bfee6e500bcfe626feed107ea0cb28db81ceb0b89451981` |
| `Beta/implementation/p00/first-loop-mapping/mapping-v1.json` | `3ea01d2caf7c9061de7c90e5ed4780a9de80681da48c9efb6042d3a43962505a` |
| `Beta/implementation/p00/first-loop-mapping/test_mapping.py` | `163df09a2c52b276389efc9972db4478dd0c82edf748a15136ab5be944e10f6a` |
| `Beta/implementation/p00/first-loop-mapping/validate_mapping.py` | `4bfab8bd2828a913185dc44ec0a408112c0701d02f4f32cf82dd2d92f5f906dd` |

Reviewed artifact/witness set: 39 distinct files. Aggregate SHA-256: `aa5d291fe64d9b5f2a31fd8af25f60e5248ebeae961d3df9486ba7cf21f83695`.

The aggregate hashes the UTF-8 concatenation of each sorted repository-relative path, a NUL byte, its lowercase hexadecimal SHA-256 and a newline. Later changed files require an appropriate review refresh.
