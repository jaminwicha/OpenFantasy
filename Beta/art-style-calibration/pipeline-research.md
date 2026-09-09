# Pixel-art production tooling research

Research date: 8 September 2026. Recommendation status: **candidate workflow, pending an actual tool comparison and user style acceptance**.

The strongest practical direction is a dedicated artist workflow combining a pixel-focused generator with an exact pixel editor. PixelLab is the first generator to trial because its documented MCP surface covers the asset families this game needs. Aseprite is the first editor to trial because its official scripting and export interfaces can preserve editable, native pixel assets. This is a reasoned shortlist, not a benchmark finding that either produces the best Chrono Trigger match.

## Available now versus available in documentation

| Tool | Current evidence | Role in this project | Limitation |
|---|---|---|---|
| Built-in image generation | Callable here; generated and edited the two included PNGs | Art direction, composition, visual alternatives, reference candidates | Both included outputs fail the strict requested 4× native-grid check. Animation, tile continuity, sprite identity across frames, and editable layers were not established. |
| PixelLab MCP | Official documentation and tool descriptions inspected; no callable PixelLab tools discovered in this session | First candidate for native-scale characters, variants, animation, objects, and connected terrain | Not connected or tested here. No third-party generations or subscription purchases made. Documentation is capability evidence, not style-quality evidence. |
| Aseprite | Official Lua API and CLI inspected; no executable found on PATH or the usual system, user, or Steam application paths checked | Exact editing, palette work, animation timelines, layered source, reliable exports | No local rendering or integration test performed. An installation elsewhere has not been ruled out. |
| Aseprite MCP implementations | Public maintainer repositories inspected; none callable here | Possible agent interface to the editor | Third-party adapters have different tradeoffs; an adapter should pass real editor/export tests before adoption. |
| Blender MCP | Tools discoverable in this session | Potential later helper for perspective/blockout or reference, when useful | Its presence does not make it the best tool for native 2D pixel art. No Blender work is needed for this calibration. |

## PixelLab: broad asset coverage with meaningful mode differences

PixelLab documents a remote MCP service for characters, animation, terrain tilesets, isometric tiles, map objects, and raw image operations. Its public overview includes connected terrain generation and map-object style matching. This breadth maps well to the proposed artist remit, and is more useful here than a generic image endpoint alone. Its access guide lists both Codex and Claude clients and requires an active subscription; therefore the art role can remain independent of the engine-builder model. [PixelLab MCP overview](https://www.pixellab.ai/mcp), [ways to use PixelLab](https://www.pixellab.ai/docs/ways-to-use-pixellab).

The detailed MCP guide matters more than the marketing examples. Character generation has standard, pro, and v3 modes with different parameter behavior. Pro/v3 force eight directions and ignore several controls; for example, pro ignores the listed proportion, shading, and outline controls. V3 can rotate an existing reference sprite, while pro can take an existing multi-direction character as a style anchor. Size and style hints are not equivalent to guaranteed output constraints. The guide also exposes animation and state operations. **Recommendation:** choose a mode by the capability actually needed, retain accepted references, inspect every generated direction, and measure canvas, baseline, identity, and palette stability. Do not assume a more expensive mode obeys more controls. [PixelLab MCP tool guide](https://api.pixellab.ai/mcp/docs).

PixelLab's separate style-reference documentation describes reference-driven generation for characters, items, and objects, including transparent output. This supports the proposed calibrated-category workflow, but that page describes editor/Creator interfaces rather than proving identical parameters on every MCP operation. The directional-sprite documentation similarly describes concept and style references, view selection, and body types. Treat interface parity as something to verify through the actual connected tool schema. [Style-reference generation](https://www.pixellab.ai/docs/tools/consistent-style), [eight-direction sprite generation](https://www.pixellab.ai/docs/tools/create-8-rotations-pro).

The useful experiment is whether this tooling can preserve the accepted worker, adventurer, and creature across directions and actions at the chosen native size. A sprite that looks good in a large browser preview but changes its face, accessories, silhouette, or apparent resolution between frames fails that experiment. A tileset must also survive adjacency combinations and the actual camera view. These are proposed project criteria; they have not been demonstrated by a vendor demo.

## Aseprite: exact art production and inspection

Aseprite's official Lua API exposes sprites, images, cels, frames, layers, palettes, tags, slices, tiles, and tilesets. Its CLI can run without the UI, execute scripts, and export sprite-sheet images with JSON metadata. That provides the native editor and export surface needed to repair a generated candidate, preserve layered work, and produce an engine-consumable artifact. An MCP wrapper is optional transport around these capabilities. [Aseprite API](https://www.aseprite.org/api/), [Aseprite CLI](https://www.aseprite.org/docs/cli/).

The editor does not supply aesthetic judgment by itself. An artist agent must choose the cluster shapes, simplify noise, preserve character identity, and inspect the result. Code can enforce native pixel positions and metadata; it cannot turn a weak silhouette into a strong one merely by passing validation. The calibration's measured grid failures make this exact editing and inspection stage a concrete need, not an assumed preference for a particular editor.

Two MCP implementations illustrate different useful approaches:

| Candidate | Documented approach | Why trial it | What to establish locally |
|---|---|---|---|
| [mattt/aseprite-mcp](https://github.com/mattt/aseprite-mcp) | Two tools: run Lua, inspect exported PNG | Broad access to the editor with little tool-schema overhead; a capable artist agent can express a whole edit or animation operation coherently | Run against a real Aseprite version, verify exports and errors, and confirm the chosen execution boundary. The maintainer states that path checks do not sandbox arbitrary Lua filesystem access. |
| [jcscocca/aseprite-mcp](https://github.com/jcscocca/aseprite-mcp) | Structured operations for pixels, palettes, layers, frames, tags, previews, and exports; exact pixel readback | Explicit operations and measured pixels make small corrective edits easy to audit; documented integration tests use the real editor | Run its real integration suite, verify native JSON conventions, and test the operations required by this game's asset package. The maintainer distinguishes mocked/unit checks from editor-backed checks. |

Tool count is a poor proxy for coverage: one well-supported scripting operation can reach more of an editor than a long list of shallow operations. Conversely, structured pixel reads and validated edits may reduce mistakes on a small sprite. The choice should follow the actual specimen trial. Neither adapter was installed or benchmarked here, and neither should be treated as an official Aseprite MCP product.

## Selection trial after the required access is available

Use the same accepted reference and the same three production briefs in each candidate workflow: one worker with four directions and a short walk, one original nonhuman creature with an idle/action, and one ground transition plus prop cluster. Keep the intended native dimensions and display scale fixed. Let a dedicated artist agent finish the assets, including cleanup; count the cleanup work instead of reporting only successful first-generation examples.

Compare the results on:

- User judgment of reference fidelity, personality, and category consistency.
- Correct native dimensions, deliberate clusters, intended alpha, stable foot baseline, and direction/frame identity.
- In-motion readability, contact/foot planting, outfit and prop stability, and usable timing.
- Tile seams, repetition, occlusion, and readability in a small actual-engine scene.
- Editable source quality, export metadata, rerun reproducibility, and cost/time per accepted asset including rejected attempts.

Choose the generator/editor pair on accepted output and total production effort. If a specialist generator fails to match the accepted baseline, use direct pixel editing or another qualified generator; the ecosystem should not force a failing tool to remain in the path. Do not produce the full art inventory before that comparison establishes a viable workflow.

## Production handoff

The artist agent should deliver art source and reviewed visual evidence; the engine builder should consume a documented artifact contract. A compact per-asset record can include asset ID, category, accepted-style version, native canvas, pivot and attachment points, direction order, named animations and timings, export files, source file, generating/editing tool versions, and review status. The engine determines runtime layout, collision, simulation meaning, and asset loading; these should not depend on the image generator's job IDs.

Once a style and specimen are accepted, maintain a small golden reference set across people, creatures, props, and environments. Expand it when a new category exposes a real gap. Reuse the user's existing acceptance within its scope. Reopen the style decision when a meaningful drift or new visual requirement appears, not every time an agent produces another conforming pot or tree.

## Source notes

All linked pages were accessed on 8 September 2026. PixelLab sources are vendor documentation; they establish offered interfaces and stated behavior, not independently verified quality. Aseprite sources are official editor documentation. Adapter sources are the implementations' own maintainers describing their projects, including their stated verification scope. The local image inspection is original evidence from this calibration and is recorded in [inspection.json](./inspection.json). No source-code, asset, engine-performance, or native sprite-generation benchmark of the proposed third-party toolchain was performed.
