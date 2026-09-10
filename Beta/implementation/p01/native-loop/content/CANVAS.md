# Public native farm presentation

`OpenFantasy.Client.FarmCanvas : Node2D` consumes `Present(FarmCanvasFrame frame)` on the Godot main thread. It references the portable identity, instant and position values only. It does not accept `WorldSnapshot`, call host inspection, read private NPC inventory, authenticate a viewer, submit actions, advance simulation time or introduce gameplay colliders.

The immutable DTOs live in `OpenFantasy.Client.Presentation`:

```csharp
new FarmActorFrame(actorId, displayName, feet, isOwnActor, FarmSpriteKind.Wren);
new FarmActorFrame(playerId, playerName, playerFeet, true, FarmSpriteKind.Squire);
new FarmCanvasFrame(calendar, visibleActors);
```

`FarmCanvasFrame` copies at most 64 uniquely identified public actors into canonical identity order and requires exactly one own actor. Actor names use the 128-byte bound; sprite kind is explicit, not inferred from species or control ownership. This view accepts only the admitted `(96,320)` through `(127,351)` region and feet Z=256. A different realm/floor/region needs its own admitted projection and presentation support. The factory does not decide whether a viewer is allowed to see an actor: the host must do that before constructing these values.

`LocalBounds` is `(0,0,1024,1024)` display pixels. `FocusPosition` is the own actor's latest committed point relative to that rectangle. The conversion is exact within these bounded coordinates: **256 subunits = one cell = 32 display pixels**. Wren starts at `(240,80)`, player at `(176,176)`. Root UI may center a `Camera2D` on this value and handle framing, input, own-wood display and dialogue separately. Transparent space outside the rectangle does not imply more loaded terrain.

## Preserved assets and declared adaptation

Eight files are byte-identical copies from the frozen source packet: original tile atlas, squire sheet, body/hair/outfit A/outfit B/accessory layers and appearance manifest. `client/assets/native-farm/PROVENANCE.json` records original/copy paths, byte lengths and SHA-256 hashes, the exact original cropped tile grid, the displayed grid and the adaptation. Original source files and archived mappings are unchanged.

`farm_visual.tres` is an ordinary exportable Godot Resource with explicit external references to all seven PNGs. It embeds the exact source palette text, Wren appearance values and 1,024 display tile IDs. Runtime rendering therefore does not depend on exported JSON file filters or access to the source checkout. The copied JSON and provenance files remain readable evidence; the native Resource and PNG references supply the displayed content.

The original RLE world map crop contains **859 walkable floor cells and 165 blocking prop cells**. The source fence, wall, tree, counter and crystal tiles have no matching solid structures in the admitted flat native terrain. Their displayed replacement is grass. The original walkable floor arrangement—grass, road, cobble, flowers, planks, farmland and grate—remains exact. This is explicitly a ground-only adaptation, not a claim that the original buildings and map have been fully reconstructed. Reintroducing those obstacles requires their real terrain/structure state and suitable approach routes first. Original tile values are retained for that future work.

The floor atlas coordinates and wrapping visual variant selector follow the inspected predecessor `src/Client/WorldRenderer.cs`, whose exact hash is recorded as an additional source witness. No simulation random stream is consumed. Ground commands are cached by the `Node2D` custom drawing lifecycle rather than recreated for every actor movement.

Wren's compositor preserves the source algorithm and order: body using skin ramp 5; outfit 8 A and B using outfit-color ramp 10; hair style 5 using hair-color ramp 13; accessory 3 using outfit B's ramp. Race 0 adds no layer. Pixels with alpha below 0.5 are skipped; opaque pixels select the nearest original grayscale value `[40,110,175,235]`, preserving first-match behavior. Composition creates one cached 48×96 sheet for the view, not a new generated art style or a mutation of source assets.

The original sheets have **three 16×24 columns and four direction rows** (down, up, left, right). They are displayed at integer scale 2 with nearest filtering. The original standing column is 1 and walking sequence is 0,1,2,1. Names and a small own-actor ring identify the two actors without exposing private stats. The full simulation clearance remains **180×140×464 subunits**, independent of source sprite pixels; no sprite shape is used to invent or shrink a collision volume.

## Motion and scope

Only observed public position changes determine facing and walking. Display position interpolates toward the latest committed point over 50 milliseconds and snaps on corrections beyond 160 pixels. Rendering never extrapolates a future location. A short 120-millisecond presentation grace returns a stationary actor to the source standing frame when no movement arrives. Animation uses the source seven-frame-per-second phase cadence and cannot change calendar time, custody or accepted movement. Both player and Wren use the same view class; an own-actor flag changes the name/ring styling only.

All Godot texture, image, node and draw operations run on the main thread. The generated composite texture is owned by the view asset set and released on canvas deletion; original loaded textures remain engine resources. Actor nodes sort by displayed feet Y. Ground objects remain decorative source references until corresponding authority exists. Full NPC quirks, expressive choreography, injury animation, contextual gestures, time/weather rendering, sound and accepted final art remain separate work.

## Verification boundary

The implementation applied the `godot-csharp-standards`, `2d-essentials` and `assets-pipeline` skills, and consulted local Godot 4.6 API XML for the drawing, image and deletion methods used. The C# engine classes are partial; drawing calls stay within `_Draw`; nearest filtering is explicit; texture references are exported native resources. No shared project, scene or Bootstrap edit belongs to this view increment.

`verify_visuals.py` checks source-copy hashes and PNG dimensions, original RLE crop and filtered layout, preserved palette data, declared source appearance and native Resource references. It reports exact input hashes to stdout; `VISUAL_EVIDENCE.json` retains one executed report. These are file/data checks, not C# or Godot execution. The coordinator must still compile/import the resource, inspect an actual native frame, verify the two named actors and camera framing, observe both walking directions and stopping, and capture the resulting build/seed/input evidence. No native render, animation quality or human art acceptance is claimed by this document.
