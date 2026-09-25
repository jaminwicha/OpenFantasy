# Water surface calibration

`water-surface-calibration-v1.aseprite` is the editable owner: 32×32 pixels,
eight frames, three named raster layers, and two four-frame tags. Each frame is
240 ms. The PNG export is 128×64: columns are frames and rows are variants.
The pivot is the top-left corner, at 32 pixels per metre.

This is original native pixel art calibrated toward the accepted V06 direction.
The five used colours form a quiet slate/teal ramp beside the farm's greens and
earth tones. The two arrangements are individually placed, with no mirrored
variant. Matching border colours avoid hard colour seams. There are no bank,
foam, falling-water, refill, directional flow, or depth claims in the artwork.

Edit the saved master in Aseprite. `author-calibration.lua` records the initial
native pixel/cel operations and refuses to replace an existing master; it is
not a regeneration step after human edits. `make-preview.lua` reads that master
without changing it and exports a flattened 2×2 preview with staggered phases.

`export-contract.json` is consumed by the maintained native-pixel-art skill's
`native_asset.py export` command with `--root Beta/Runtime`. Run its dry-run
before using `--publish`; the ownership ledger guards runtime PNG/metadata
against outside edits. Set `ASEPRITE_MCP` and `ASEPRITE_BIN` to the existing
verified local installations. No downloaded assets or editor binaries belong
in this kit.

The engine owns admitted wet geometry, clipping, actual volume/fill, falls and
cosmetic timing. Use nearest sampling and no mipmaps. Choose a stable variant
per presentation cell. The intended client derives animation from committed
calendar time, so a reading hold or pause freezes it. A ripple is ambience,
never evidence of material movement or replenishment.

This asset is a calibration, not human-accepted production art. Native export
checks and previews are retained under
`Beta/implementation/p02/native-watercourse/art/`. Contextual colour, repetition,
occlusion and animation need review in the actual client scene.
