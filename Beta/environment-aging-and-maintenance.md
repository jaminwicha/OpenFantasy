# Changing time, weather, aging and maintenance

8 September 2026. Owner direction: art must respond to day/night, sun, overcast, overhead clouds, wind, storms and changing seasons. Accumulated days, seasons and years must wear into objects, with material-appropriate maintenance and repair. This document proposes the simulation/art boundary and first verification steps; none of these Beta systems is implemented yet.

**Core gameplay pillar: the world changes, the art communicates that change, and players act on it.** This is shared work for simulation, game design and artists. The condition of an object, the consequences of exposure and the effect of maintaining it must have coherent rules and readable feedback. A beautiful aging shader alone cannot satisfy this requirement.

## One world state, several timescales

| Timescale | Authoritative state when consequential | Presentation and asset needs |
|---|---|---|
| Within a day | Calendar position, relevant illumination/visibility rules, work/activity scheduling | Sun direction and color, ambient light, readable night palettes, interior/exterior lighting and local lamps |
| Minutes to days | Weather region, precipitation, wind, temperature and consequential exposure | Moving cloud cover, rain/snow, gusts, foliage/water response, overcast treatment, storm effects and sheltered spaces |
| Seasonal | Climate/season phase, vegetation/crop states and relevant water/snow conditions | Growth/flowering, autumn foliage, bare branches, snow coverage, seasonal ground/water and household adaptations |
| Years and events | Object age, material condition, use/exposure totals, damage and repairs, ownership and modifications | Wear patterns, patina, repairs, replacement pieces, changed structures and inspectable historical marks |

The world's calendar and regional climate define seasons; do not assume every generated biome has the same four-season cycle or that one hemisphere's winter applies everywhere. Solstice dates, climate bands and exceptional magical environments belong to world rules/content. The player's need is legibility: they should understand the local season, weather and likely consequences.

Day/night and weather are not exclusively color filters. Some effects change navigation, crops, work, senses or physical risk, and therefore belong to simulation. Others are cosmetic: the exact flight of a leaf or arrangement of harmless cloud pixels need not become durable history. If a cloud's shadow affects an energy collector, or a lightning strike starts a fire, record the consequential input/effect; its renderer cannot independently invent it.

## Pure simulation responsibilities

Keep calendars, regional weather transitions, exposure, material condition and work orders in the engine-independent deterministic core. Use versioned rules and separate keyed random domains. Generate weather consistently across space/time; exploring a new chunk cannot reroll a storm or erase the winter that aged its buildings.

Represent meaningful exposure with a bounded set of quantities, such as wet-duration, freeze/thaw cycles, solar exposure, abrasion/use, salt/contact exposure and discrete damage events. Each material/product archetype declares which matter. These quantities are not all mandatory on every object. A sheltered ceramic pot, a working steel blade, a bridge rope, a roof beam and a river stone need different rules and update frequencies.

Prefer periodic or event-based updates over touching every object every tick. Record a last-processed simulation time and versioned environmental summaries for inactive regions. On activation, apply the same declared aggregate process that would apply at the supported coarse resolution. Test conservation and equivalence at that contract; do not claim a coarse solver reproduces every unmodeled local raindrop. A critical named artifact or active fire can demand a finer update policy.

Environmental histories must retain enough information for the model actually used. If wear depends on ordering—wetness followed by freezing, for example—an average temperature and rainfall total are insufficient. Store the relevant threshold/cycle summaries or ordered exceptional events rather than years of per-frame weather. Bound retained data and define what is approximate.

World time advances according to an explicit host policy. A local saved game should not silently experience ten years of decay because its owner stopped playing. A continuously running shared server may continue to age; make that rule visible and provide legitimate household/workshop upkeep arrangements. Calendar jumps, time travel and branches must advance or isolate the correct condition state; wall-clock time is not a substitute for the world calendar.

## Materials, maintenance and history

| Example | Aging or damage | Meaningful maintenance | Historical trace after repair |
|---|---|---|---|
| Timber roof / bridge | Water ingress, rot under sustained conditions, loose fittings, load damage | Replace affected boards, reseal joints, improve drainage/support | Newer planks and patches; sound structure can remain visibly old |
| Ferrous tool / mechanism | Corrosion, edge wear, fatigue or impact damage | Clean/protect, sharpen, replace a component, reforge when appropriate | Changed handle, grind marks, replacement gear and recorded workmanship |
| Stonework | Weathering, cracks, frost damage, settlement, plant intrusion | Repoint, brace, replace blocks, redirect water | Different mortar/stone and layered construction rather than complete visual reset |
| Cloth / leather | Abrasion, tears, moisture damage, color change | Clean/dry, stitch, patch, replace a strap or panel | Repairs that retain the item's recognizable silhouette and identity |
| Ceramic / glass | Chips, cracks, catastrophic breakage | Mend where rules allow, replace a piece, salvage material | Visible mending; a reconstructed heirloom is distinct from a new identical object |
| Field / orchard / garden | Seasonal dormancy, weather damage, soil and water conditions | Tend, replant, prune, irrigate, repair shelter | Changed growth and recovered productivity, with surviving older plants where plausible |

These are starting design cases, not a validated engineering materials model. Define readable condition states and relevant consequences before tuning rates. Chronological age, functional condition, appearance and historical significance are related but distinct. Excellent upkeep can preserve a very old building; a new badly built bridge can fail early.

Repair is a typed work order: target/component, desired outcome, requirements, allowed methods, material reservations, worker skill/tool access, progress, costs and failure/cancellation rules. Commit resource consumption and condition changes together. Two workers may contribute under declared rules; two clients cannot both spend the same board. Cleaning should not automatically restore missing material, and sharpening should not repair a broken handle.

The original recipes, tools, resource nodes and NPC workshops provide required content for this system. Preserve their identities and original capabilities as they are adapted. Households and hired craftspeople can perform routine upkeep; player construction can reduce future exposure. Let the player choose a rustic patched building or restored finish without forcing all age into one universal “damaged” bar.

Maintenance should support attachment, planning and making. It should not become compulsory busywork for every owned object every few minutes. Start with conservative rates, visible warning stages and meaningful interventions; tune from playtests. Avoid catastrophic offscreen loss without the world's declared time/risk rules and reasonable ways to prepare. A deliberate survival mode can have harsher rates without changing the data architecture.

## Artist and renderer responsibilities

Artist agents author a coherent base set, seasonal variants where silhouettes actually change, material-aware condition/repair variants, palettes and controlled effect layers. Engine builders implement the runtime selection, masking, lighting and animation. Neither role should solve the other's mistakes by silently changing accepted proportions or simulation rules.

Avoid drawing the full Cartesian product of every asset × hour × season × weather × wear level. Separate features where that remains visually convincing: neutral base art, selected lighting palettes or restrained light response, canopy/cloud occlusion, wet/snow masks, wind anchors, seasonal foliage replacements, wear/repair components and local emissive details. Some hero props or highly changed silhouettes deserve dedicated art; compositing is not a mandate to accept poor results.

The package needs semantic material/surface tags, region/biome compatibility, seasonal substitutions, sheltered/exposed coverage, condition/repair variant IDs, attachment points and effect limits as appropriate. The core emits semantic state; it does not store shader objects or engine texture handles. Rendering the same aged object in another engine can look different while preserving its identity and functional history.

Daylight, overcast and night must preserve navigational/character readability. Moving cloud shadows should not shimmer the pixel grid. Wind should move anchored foliage and cloth rather than wobble entire buildings. Rain/snow must respect roofs and indoors. Snow accumulation, thaw and wetness need sensible ordering. Lightning and magic should support contrast without washing out the accepted pixel treatment. Accessibility options may reduce flashes, shake, particles or decorative motion without erasing consequential information.

## Calibration and first proof

Keep the current style candidates as reference directions; none demonstrates a functioning weather or aging renderer. After selecting the visual direction, use the same small town corner and representative people, creature, roof, bridge, metal fitting, tool and ceramic object for the following matrix:

- Clear day, overcast with moving clouds, twilight, readable night with local lights.
- Rain and wind, a storm, snowfall and sheltered versus exposed surfaces.
- Spring growth, summer, autumn and winter where the local climate supports them.
- New, used, weathered, neglected and repaired examples, including an old well-maintained object.
- A short transition in motion to check palette shifts, pixel stability, effects, seasonal substitution and identity.

Do not require the artist to generate all combinations before learning from this sample. The exact native grid, palette strategy and layer contract should be chosen from accepted in-engine evidence.

| Proposed test | Required result |
|---|---|
| ENV-01 Calendar/weather replay | Same checkpoint, versions and ordered inputs yield the same consequential weather/exposure state |
| ENV-02 Local/coarse transitions | Repeated region activation does not reset seasons, double-apply wear or lose important damage/repair history |
| ENV-03 Shelter and materials | Comparable exposure affects different materials according to declared rules; roofs/interiors and maintenance matter |
| ENV-04 Repair transaction | Cancellation, restart and concurrent clients preserve resources and progress; rewards/repairs do not duplicate |
| ENV-05 Temporal travel | A branch or calendar advance ages the intended objects once, preserves lineage and leaves other branches unchanged |
| ENV-06 Art and accessibility | Accepted objects remain recognizable and readable across the calibration matrix, in motion and with reduced effects |
| ENV-07 Playability | Players can distinguish harmless age from actionable damage, anticipate upkeep and delegate routine work |

The first proof should include one outdoor structure and one owned tool, one consequential weather transition, one coarse-time advance and one repair that leaves a visible trace. Expand only after confirming that the aging is perceptible, coherent, enjoyable and affordable to produce.
