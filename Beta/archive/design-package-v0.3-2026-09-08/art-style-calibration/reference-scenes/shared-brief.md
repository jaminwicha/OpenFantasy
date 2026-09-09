# OpenFantasy reference scenes — first set

The owner endorsed the restrained v06 direction with “good” and requested around five more reference scenes drawing on OpenFantasy's own content/assets. Use v06 as the approved visual reference for this exploration. Approval of a direction does not validate native sprites, animation, or an engine implementation.

## Shared visual direction

Reference: `/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/art-style-calibration/restrained-history-proposal-v06.png`.

Create five separate full-scene landscape images, approximately 1536×1024, using the same elevated three-quarter game view and deliberate pixel-art language. No right-hand asset panels, collages, titles, captions, HUD or text in the images. Keep people at useful gameplay scale and environments spatially legible. The scenes should feel like different places in the same game.

Maintain rich craftsmanship with selective detail and calm surfaces. People, routes and one or two story details should lead the eye. Use original game assets as identity/material/silhouette references, interpreted at the new quality level rather than pasted as tiny placeholders. Inspect every local input before generation. Keep original source files and all previous calibration images unchanged.

Age appears in repairs, worn thresholds, reused construction and maintained objects. Avoid covering everything in marks, flowers, moss or glitter. Magic is specific to a few meaningful objects or phenomena. Weather and lighting should communicate different conditions without hiding the accepted pixel work. Avoid blur, photorealism, cinematic depth of field, excessive bloom and 3D-rendered surfaces.

These are reference illustrations for future gameplay. Spatial staging, exact buildings, weather and visible repair actions are proposed interpretations. Distinguish them from existing authored names, quests, assets and dialogue. Do not claim a still image proves dynamic seasons, weather, aging, maintenance, animation or runtime integration.

## Scene set and original content anchors

| ID / output | Scene | Source anchors | Proposed condition and focal action |
|---|---|---|---|
| 01 `01-wrens-saplings-autumn.png` | Wren's Saplings, Verdance near Brasshollow | `farmer_wren`, `wren_orchard`, `farm_verdance_small`, wood/tool/appearance assets | Late autumn wind; Wren and a traveler repair the sapling windbreak with a few new boards among old timber |
| 02 `02-gearford-winter-forge.png` | Gearford's Grand Forge, Kharzum | `thane_bruni`, `kings_writ`, `dwarven_tank`, forge tile, metal recipes | First snow and overcast daylight; warm forge, a maintained tank, a courier and dwarven craftspeople |
| 03 `03-willowick-oasis-wind.png` | Willowick, Miradel | `matriarch_poppy`, `bartender_juno`, `scorpion_season`, town/port geography | Clear desert sunlight and gusts; an oasis/cellar courtyard with cared-for shade cloth and ordinary gnomish daily life |
| 04 `04-sunken-crypt-rain.png` | The Sunken Seal, Sylvas | `sunken_seal`, `sunken_crypt`, `crypt_wight`, `marsh_wisp`, Squire/Mage assets | Rain at blue dusk/night; a readable wet approach to the old crypt, adventurers and a restrained distant threat |
| 05 `05-brasswing-stormbreak.png` | Brasswing near Storm Spire, Miradel | `airship_brasswing`, `warden_of_storms`, `storm_spire`, `spire_top`, airship asset | Storm breaks toward sunset; a grounded or docked airship, small travelers and the weathered tower, with clear scale and travel invitation |

Original JSON sources are under `data/`; map/settlement construction and identity context are in `tools/gen_world.py`, `tools/gen_npcs.py`, `docs/MILESTONE_4.md`, `docs/MILESTONE_5.md` and current `src/Sim` sources. Later source/data takes precedence over older milestone counts. For the fifth image, the airship's staging near the tower is a concept composition; the quest establishes its reward and the breaking storm, not a preexisting landing pad there.

Wren's existing appearance uses skin index 5, blue hair-color index 13, and red outfit-color index 10. Thane Bruni is a dwarf with reddish hair/beard and dark outfit with warm trim. Matriarch Poppy is a gnome with violet hair and magenta/gold clothing. Use source appearance data for recognizable identities while adapting shapes to the accepted style. Do not substitute the calibration-sheet pair for every named person.

## Delivery and ownership

Each artist owns only assigned PNG files and matching per-scene `.json` records. Each record contains the exact generation prompt, built-in tool mode, source content/asset references, input image paths, returned generation source path, workspace output path, SHA-256, dimensions, source-versus-interpretation notes and a short visual assessment. Inspect each final image and identify material deviations honestly. No native-grid measurements are necessary unless an exact grid claim is being made; these are concept scenes.

The parent owns the gallery README, scene index/manifest, acceptance record and shared brief. No other project/source or ecosystem changes belong to these artist tasks. Do not modify an image with Python or other substitutes for the prescribed image-generation tool. No new paid service or MCP installation is needed.

## Subsequent owner review

All five scenes are positively received. Refine conspicuous repetition within and across scenes using coherent silhouette, pose, construction and placement variations; recoloring alone is insufficient. Keep the restrained detail budget. Future production follows the expressive animation/choreography contract for living personalities, emotions, idles, interactions and enduring narrative relationships. These later requirements do not retroactively claim the stills demonstrate animation.
