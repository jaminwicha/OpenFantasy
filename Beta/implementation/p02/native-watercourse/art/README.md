# Native water-surface calibration evidence

One original surface kit is authored in the native Aseprite editor through Lua
pixel/cel operations. Its saved layered master is the editable owner. This is
calibration toward `restrained-history-proposal-v06`, not human art acceptance
or production validation.

- `native-tool-probe.log`: real local stdio MCP layered edit/save/reopen,
  two-frame export and first-frame PNG regression passed.
- `native-authoring.log`: new 32×32, eight-frame, three-layer master saved.
- `export-dry-run.log`, `export-publish.log`, `export-roundtrip.log`: guarded
  MCP native inspection/export and byte-identical re-export.
- `water-sheet-4x.png`: all eight exported frames at integer zoom.
- `water-tiled-native.png` / `.gif`: native 64×64 preview, two-by-two tiles.
- `water-tiled-4x.png` / `.gif`: native-editor integer-zoom preview exports.
- `validation.json`: exact dimensions, alpha, palette, timings, source hashes,
  unique frames, non-mirrored variants, seams and loop pixel-change counts.

Existing Aseprite 1.3.18.5-dev and the locally patched pinned aseprite-mcp
revision `8b206267ba2e91de1e4b0de00b71d1fd569b0868` were used. The existing
first-frame PNG patch was present; no installation, credential or editor
configuration changes were made. MCP SDK 1.30.0, Python 3.12.13 and Pillow
12.3.0 supplied transport and read-only artifact checks. No direct session MCP
namespace is claimed; the maintained skill connects over actual local stdio.

The author inspected native-size and 4× stills and checked all four loop edges
numerically. The GIF is exported with four 240 ms frames. These facts do not
establish in-engine playback, moving-scene legibility, human animation review,
or complete V06 fidelity. The five-colour surface is deliberately quiet;
the bank/floor cutaway and actual wet geometry remain separate engine work.
