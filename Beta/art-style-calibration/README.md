# Beta OpenFantasy art calibration

**Status: restrained v06 accepted as the visual reference for further scene exploration.** The owner responded “good” and requested five scenes drawing on original OpenFantasy content/assets. Use that direction without repeating its approval request. The earlier references remain preserved; v05's density prompted the successful v06 restraint pass. Native assets, animation and dynamic gameplay still require their own implementation and verification.

The target is authentic Chrono Trigger pixel-art technique as a close baseline, with the world detail and presentation care expected from Chrono Trigger, Sea of Stars, and Stardew Valley. These references describe quality and technique; they do not impose the original hardware limits on the game engine. Characters, creatures, objects, architecture, terrain, effects, portraits, and interfaces must belong to one user-accepted visual language.

## Five original-content reference scenes

The [scene gallery](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/art-style-calibration/reference-scenes/README.md) expands v06 across Wren's autumn orchard, Gearford in snow, sunny Willowick, the rainy Sunken Crypt and Brasswing after the storm. Each image has original-content references, an exact prompt and an inspection record.

## Latest production requirements

All five scenes are positively received; the owner identifies too much repeated sprite use. Preserve their style while varying incidental asset families and composition. The [living-performance contract](../expressive-animation-and-choreography.md) adds personality, emotions, idles, gestures, interactive people/animals/objects and choreography. Review these in motion before bulk production; existing stills are preserved.

## Preserved options and the new comparison

| Proposal | Use | Assessment |
|---|---|---|
| [Restrained history proposal v06](./restrained-history-proposal-v06.png) | Latest refinement: selective richness and quieter surfaces | Reduces scattered flowers, repeated markings and magical accents while retaining composition, craftsmanship and focal repairs. Flower reduction is noticeable; final density remains a user choice. |
| [History-rich magical proposal v05](./history-rich-magical-proposal-v05.png) | Latest refinement: decades and centuries visible in cared-for objects and places | Layered masonry, reused carved stones, replaced planks, patched roof, worn fittings and mended ceramic. Reduced general glitter. Some repair/spiral details are pronounced stylizations; simulated history is not demonstrated by the image. |
| [Magical detail proposal v04](./magical-detail-proposal-v04.png) | Richer craftsmanship and small magical details | Embroidery, carving, ornate metal, ceramics, botanical detail and luminous shell markings. Some motes retain soft halos; concept only. |
| [16-bit+ proposal v03](./sixteen-bit-plus-proposal-v03.png) | One noticeable rung richer, preserving the scene and identities for comparison | More articulated foliage, stone planes, timber, water and creature-shell detail. Some details are redrawn; it remains a concept, not a native-grid sprite export. |
| [Strict cluster proposal v02](./strict-cluster-proposal-v02.png) | Closer baseline for discussing pixel density and readability | Simplifies foliage and surfaces, increases clarity, and retains the original character and creature identities. It still has inconsistent microscopic pixel treatment and is not an exact native-grid export. |
| [Modern detail proposal v01](./modern-detail-proposal-v01.png) | A more detailed alternative for comparing atmosphere and density | Attractive inhabited scene, warm materials, coherent subjects. Too dense and painterly to serve as evidence of exact SNES-style production fidelity. |

All six contain original people, a reed-shell turtle, stone and timber village scenery, a chest, pot, lantern, and rock. They demonstrate how categories might coexist. The supplied images contain pose and direction studies; those studies have not been validated as a complete direction set or animation.

The new variant was produced with the built-in image generation tool. Its exact prompt and local output are recorded in [generation-prompts.json](./generation-prompts.json). These are image-generation concepts. The larger right-hand figures are visual studies, not verified integer enlargements of the in-scene figures. No sprite extraction, resampling, palette reduction, animation authoring, tile slicing, or engine integration has been performed.

## What was actually checked

All six images are 1536 × 1024 PNG files. V03 contains 548,455 distinct RGBA values and 8 of 98,304 uniform aligned 4 × 4 blocks; v04 contains 701,406 and 1 uniform block; v05 contains 730,618 and 3 uniform blocks. V06 contains 671,622 distinct RGBA values and no uniform aligned 4 × 4 blocks. They propose visual treatments without establishing a native production grid. The stricter v02 edit requested a 384 × 256 logical canvas enlarged exactly four times. A read-only inspection tested whether every aligned 4 × 4 block contains a single RGBA value. V02 passed only 8 of 98,304 blocks, and v01 passed none. Their distinct RGBA counts were 476,700 and 1,108,190 respectively. These are measured file properties, not an art-quality score.

The stricter image therefore **fails its requested exact 4× grid constraint**. The check does not establish whether some other pixel grid could be extracted, and does not prohibit high-color effects in the final game. It demonstrates why generated concept art cannot be labeled pixel-correct merely because it looks pixelated. The unmodified source files, hashes, and inspection method are recorded in [inspection.json](./inspection.json); exact prompts and generation mode are in [generation-prompts.json](./generation-prompts.json).

## Acceptance contract

The user chooses or revises the visual baseline. Agent preference, a passing build, or silence does not accept a style. Record acceptance in [acceptance.json](./acceptance.json), including the actual response, reference version, and scope. An accepted style can govern subsequent batches without repeated style approval. A materially different treatment must be identified and brought back for acceptance.

Style acceptance and production readiness are separate:

1. **Visual direction:** compare the proposals, select the desired density, proportions, palette, perspective, outlining, and lighting treatment. No proposal needs to be selected unchanged; the prior direction is already recorded as a favored option.
2. **Production specimen:** make a small native-resolution original character, creature, prop set, terrain transition, and scene from the accepted direction. Review at the intended gameplay zoom and enlarged with nearest-neighbor.
3. **Motion and integration:** review a short walk cycle, one expressive interaction or attack, a creature idle, and one environmental loop in the actual game renderer. Check both still and moving readability.
4. **Calibrated batches:** produce assets against the accepted specimens. Artist review establishes style consistency; engineering verifies import and behavior. The user retains the final visual acceptance standard.

This gate applies to final-art production. Engineering can continue with clearly labeled disposable placeholders while the style is unsettled.

The [history/materials brief](./history-and-materials.md) and [environment/gameplay design](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/environment-aging-and-maintenance.md) govern further work. Daily light, clouds, weather, seasons, accumulated wear and repairs must share a contract with gameplay. These six still images do not validate any of those changing systems.

## Proposed production specimen

The following values are experiment parameters, not claims about historical Chrono Trigger asset dimensions and not engine constraints:

| Property | Starting experiment | What determines the final choice |
|---|---|---|
| Terrain grid | 16 × 16 logical cells, with larger multi-cell structures | Traversal readability, architecture scale, and tile repetition |
| Human canvas | Compare 24 × 32 and 32 × 48; draw at native resolution | Preferred facial/body expressiveness and scene density |
| Body proportions | Approximately 2.5–3 heads tall | Accepted silhouette and personality, including varied bodies |
| Material ramps | Begin with 3–5 purposeful tones, colored shadows, selective outlines | Readability and consistency; no universal hard palette cap |
| Scene camera | Elevated three-quarter view with consistent feet and ground plane | Occlusion, stairs, roof lines, navigation, and combat readability |
| Display | Integer zoom specimens plus the intended actual game window | Pixel stability in camera movement and supported display sizes |
| Initial animation | A small four-direction walk set and one interaction | Temporal coherence, foot planting, equipment stability, action readability |

The specimen should include a plainly dressed worker as well as an adventurer, one small creature with a nonhuman silhouette, wood/stone/metal/ceramic props, a doorway, stairs, foliage, water, and an indoor/outdoor lighting comparison. Later calibration should cover skin tones, age, body type, large creatures, unusual materials, combat effects, portraits, and UI. “Same style” means a shared visual grammar, not one interchangeable body template.

The visible acceptance questions are whether people and creatures feel right, whether objects are readable and belong in the same world, and whether scene richness survives actual gameplay scale. Technical checks cover native dimensions, pixel-grid discipline, intended alpha, stable pivots, consistent direction order, frame timings, palette coherence, tiling seams, roof/elevation occlusion, and absence of camera shimmer. A beautiful contact sheet alone cannot establish these properties.

## Artist and engine responsibilities

Dedicated artist agents own art briefs, reference interpretation, asset generation, pixel cleanup, animation, visual consistency, and editable art sources. Engine builders own the import contract, display scaling, layering, lighting integration, runtime animation, and performance. Either Codex or Claude can fill either specialist role; the separation concerns responsibility and context, not model brand.

The handoff is an asset package: editable source where supported, native PNG/atlas, animation metadata, pivots and attachment points, semantic identifiers, palette/style version, preview, generator/editor provenance, and known limitations. Collision and gameplay rules remain semantic engine data; engine builders do not silently distort the accepted art to fit a prototype sprite template.

Tool research and a proposed selection trial are in [pipeline-research.md](./pipeline-research.md). A portable skill draft for ecosystem curation is in [skill-draft/game-art-direction/SKILL.md](./skill-draft/game-art-direction/SKILL.md).
