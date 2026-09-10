# Finite farm gathering admission

`GatheringFarmAdmission.CreateEmbedded(seedText, playerName)` creates a candidate profile-2 world with the unchanged farm body, actors, source Wren text and terrain, plus a finite supplied tool kit and two representative resource anchors. The filesystem overload is internal for bounded source validation tests. Creation neither writes a store nor upgrades any profile-1 save.

Exact input bounds remain 256 UTF-8 seed bytes and 128 UTF-8 name bytes, rejecting whitespace-only/control/invalid UTF-16 input without normalization. A canonical OFC version-1 frame contains two length-prefixed fields: `farm-work.seed.v1` and the exact seed bytes. SHA-256 lowerhex becomes the unchanged base-farm builder's seed. For user seed `river` the result is `b7079362de39c95808fed1ec4e86edb85fd7c257a4e5c36bbd66ff0eb31d5461`. Thus original and gathering profiles do not become competing heads of the same world identity. Additional definition and instance IDs use `adventure-farm.definitions.v1` / `adventure-farm.instances.v1` domains and are collision-checked against the complete base identity index.

The authored [admission-v1.json](../../../Runtime/content/adventure-farm/admission-v1.json) has raw SHA-256 `f90505e21a578b6e8dd0df78ea195e1c2ee9e5451fee22779480b76ab04239cd`. It pins the unchanged base admission, original closure, eleven complete selected source records and nine supplementary frozen source files. `FarmWorldAdmission` first validates its complete original closure; the new adapter then validates the additional records against original JSON pointers and verifies source archetype/count/tool rules. No source archive or prior field mapping was rewritten.

| Admission | Initial state and meaning |
| --- | --- |
| Player storage | 32 counted units, containing the original eight wood plus one Copper Axe and one Sickle. This is an authored supplied kit, not original automatic equipment, a shop purchase or physical volume. |
| Wren storage | Unchanged eight-unit capacity, initially empty, with existing explicit access/control policies. |
| Oak | Authored center tile `(101,327)`, feet `(25984,83840,256)`. Five swings per cycle; 900 advancing steps to restore the next finite cycle; 160 total wood rights. |
| Berry | Authored center tile `(105,332)`, feet `(27008,85120,256)`. Four swings per cycle; 700 advancing steps to restore the next finite cycle; 128 total berry rights. |
| Work | Zero initial effort for both individually admitted actors. The core's versioned rule gives ten effort per advancing step, cap 1,000, cost 400 per swing. Source Squire calibration and Wren's authored applicability are distinguished in the source contract. |
| Finite supply | Five sources: eight initial wood, one axe, one sickle, 160 oak yield, 128 berry yield = **298 admitted counted units**. Node source intervals begin unissued; no harvested lots exist at genesis. |
| State | Day zero 09:00, revisions/advancing steps/experienced time zero, no dialogue. Body, control and comprehension contracts remain complete and unchanged. |

Source rectangles and current terrain are checked before node admission. Both chosen positions are original walkable, non-safe grass inside their source spawning rectangle and the current bounded region. A standing actor can occupy those anchors with its full actual body clearance. The two source sprites are nonblocking anchors; they do not supply terrain obstacles or relaxed actor collision volumes. One node of each kind does not claim the original count of twelve/eight nodes or the original RNG placement.

Node reserve rights are finite across all cycles; the source cursor never resets. These rights are not biological renewal, geological yield, or an import of physical mass/volume. The retained source ration counts, tools' market prices, quest rewards and windbreak narrative remain preserved source content, not executable production, currency, progression, quest completion or construction in this admission. [SOURCE_CONTRACT.md](SOURCE_CONTRACT.md) sets out the exact continuation requirements.

## Presentation

`FarmGatherNodeView` consumes only the root's public `FarmGatherNodeFrame` (node ID, display name, position, required tool kind, visible state). It accepts no complete world or private inventory. `FarmVisualAssets` lazily loads [nodes.tres](../../../Runtime/client/assets/adventure-farm/nodes.tres), whose two original PNG references export through Godot's native resource graph. [PROVENANCE.json](../../../Runtime/client/assets/adventure-farm/PROVENANCE.json) records byte-identical copies and their source paths/hashes.

The sheets preserve original 16×24 frames, three columns/four rows, nearest filtering and display scale two. The source idle sequence is `0,1,2,1` at 2.5 phases/second. The brief depleted interval dims the same original sheet; no original stump sheet exists. The root removes absent anchors based on committed public state. These animations never refill resources or determine harvesting. The source-based plant art is provisional; no new art was generated, and no final user acceptance or expressive work-animation coverage is claimed.

## Verification boundary

`GatheringFarmAdmissionTests` contains nine facts and six tamper cases: domain-separated identities/base preservation; exact node positions/capacities/counts; finite owned tools; complete initial work; deterministic instances and stable definitions; embedded/filesystem equality and full profile round trip; exact Unicode; invalid text before content reads; changed closure/source/asset/profile files; forged local pins, oversized and missing profile files. The seed expectations were independently computed with Python's SHA-256 and explicit big-endian framing, without calling the C# implementation.

The coordinator owns compilation, integrated tests and native execution. This author did not run a concurrent .NET/Godot build. Native gather/deplete/reopen/regrow behavior and final build results must be recorded from those executed runs before being claimed as passed; this admission note alone is not execution evidence.

The retained [Hosting Debug result](hosting-validation/test-results/gathering-hosting-debug-initial.trx) was inspected after execution: all 15 `GatheringFarmAdmissionTests` cases passed within the 108-case Hosting run. [hosting-validation/RESULTS.md](hosting-validation/RESULTS.md) records the broader run and its limits. This is attributed coordinator execution, not a separate build by the content author. [VISUAL_REVIEW.md](VISUAL_REVIEW.md) separately records actual native layout observations and remaining presentation limits.
