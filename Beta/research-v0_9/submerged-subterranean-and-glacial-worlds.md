# Submerged, subterranean and glacial worlds

Research date: 8 September 2026. The recommendation is to extend the existing world, material, actor and history contracts with bounded environmental processes. Underwater ruins, inhabited caverns and changing ice should create different routes, work and stories without becoming separate minigames or a scientific Earth simulator. Plate tectonics, volcanism and reference-game generation internals belong to the companion geology study; this paper concentrates on water, cavities, ice and playable continuity.

Scientific sources establish mechanisms, not suitable game coefficients. Historical developer posts establish particular design decisions, not current complete behavior or proprietary internals. No Beta experiment, native scene or performance test was executed.

## Cavities, groundwater and an inhabited deep world

USGS describes karst as dissolution of soluble rock, especially limestone and dolomite, producing interconnected fractures, conduits, caves, springs and sinkholes. The surrounding rock can have low permeability while connected openings carry water efficiently. Not every underground water store is an open cave: saturated pores and narrow fractures must remain distinct from navigable rooms. USGS also distinguishes dissolution cavities from lava tubes, whose origin is different.[^1]

The USGS groundwater/surface-water synthesis describes both slow movement through pores and fine fractures and rapid storm recharge through enlarged conduits. A spring can combine these sources. This supports a useful gameplay distinction between a steadily supplied well and a passage that floods after remote rain, even when the local sky is clear.[^2] Represent aquifer stores and conduit connections separately from visible cell water. A declared exchange transfers accounted water between them; finding wet rock does not reveal an entire underground river or an omniscient route map. Recharge uses finite contributing regions and committed boundary fluxes, not fresh unlimited water generated independently by each chunk.

In Beta, cave galleries, shafts, flooded sumps, dry upper passages, excavated tunnels and subterranean cities occupy genuine stacked space. Rock, doors, clearance, light, supports and water connect them to the surface. A shadow plane is a different realm with explicit correspondence/portals. Calling a location an underdark does not relocate its physics into another dimension. Original deep cultures can have distinctive architecture, agriculture, worship and transport while using the same people, custody, institutions and terrain authority.

NPS cave biology explains why underground ecology needs an energy account. Beyond entrance light, ordinary green plants cannot sustain the food web. Water-borne material and visiting animals supply nutrients; some microbial systems instead obtain energy from minerals or dissolved chemicals. Cave specialists and temporary visitors therefore have different dependencies.[^3] Beta can support fungal cultivation, cave fish, bats, mineral-dependent organisms and magical ecosystems, but a glowing mushroom is not automatically a limitless food source. Assign actual substrate, waste, chemical or supernatural inputs. Diverting a stream or excluding a migratory animal can alter food supply, trade and care obligations without instantly exterminating every resident.

## Quakes and floods change routes through causes

USGS documents groundwater oscillation, step changes and recovery ranging from rapid return to persistent change after earthquakes. Depending on local conditions, fractures can widen, become unclogged or seal; water levels may rise or fall. Not every well responds alike.[^4] This argues against a generic “quake rerolls the dungeon” operation. Receive a bounded disturbance from the geology owner, then evaluate affected supports, conduit conductance and stored water under pinned rules. A quake might block one crossing, drain a sump, expose a spring or merely disturb sediment.

NPS describes river water back-flooding low passages at Mammoth Cave, delivering nutrients as well as changing access.[^5] Floods can carry sediment and debris through declared transport rules: a visible waterline alone must not imply that a door was destroyed or an ore vein replenished. Preserve old passages and their changed state. Historical maps can remain useful yet outdated; a surveyor can discover which part changed. Evacuation, pumping, shoring, a higher route or an aquatic ally offer meaningful responses. Outcomes may also include loss and a truthful changed objective; automatic safety tunnels would erase the environmental consequence.

## Underwater places need capabilities and readable habitation

NOAA distinguishes underwater light environments by depth, while emphasizing that penetration depends on conditions. Its explanatory zones are useful ecological inspiration, not mandatory game thresholds.[^6] Model a small number of tuned depth/light/temperature/current bands, together with local turbidity and shelter. Swimming, breathing medium, carried air, pressure tolerance, buoyancy/load and equipment eligibility should be explicit actor or assembly capabilities. Water-breathing need not confer pressure immunity, and a bird mount need not become an aquatic mount merely because it can fly.

Unknown Worlds' March 2015 Subnautica development post ties modular seabases to resources, stacked compartments, depth-sensitive hull integrity, breaches and repair. The April 2016 update adds power-dependent oxygen. These are historical early-access records, but they demonstrate a productive relationship between safe habitation and expedition reach.[^7][^8] Beta should make doors, pumps, breathable compartments, wards, docks and repair jobs real. Use bounded gas/pressure behavior where supported; do not require computational fluid dynamics or pretend that a cosmetic bubble creates breathable air.

An underwater settlement uses the same residents, workplaces, stock and authority as a surface settlement. Aquatic inhabitants, air-breathing visitors and mixed households have different infrastructure needs. Silt can obscure a workshop, salt exposure can damage a fitting, and a broken barrier can interrupt a market. Rescue, care, trade and adaptation remain ordinary actions. Historical travel revisits the same structures and applicable surviving individuals at their actual dates; descendants, migrants, remains and ownership changes follow recorded life events. Flooding a city must not replace it with an unrelated underwater prefab or resurrect its former population.

## Glaciers and ice ages leave irreversible history

NPS distinguishes accumulation from ice loss and explains that glacial ice can continue moving down-valley while its front retreats. Retreat is a changing extent, not ice flowing backward.[^9] Glaciers abrade and remove rock, transport debris, deposit moraines and produce outwash. Those deposits can themselves impound lakes.[^10] Beta therefore needs separate ice mass/extent, underlying terrain and transported sediment state. Removing an ice overlay cannot restore the pre-glacial surface automatically.

A historical NPS account of Bear Glacier's August 2014 outburst documents a released lake, breached moraine, changed outlet, silty plume and altered lagoon level. This is a specific observed event, not a universal flood formula.[^11] It suggests an excellent multi-system incident: changing ice or a barrier releases stored water; a bridge fails; a road and fishery change; residents receive evidence and reorganize work. A lake must lose the volume that is released, and downstream effects must respect actual connections.

NOAA attributes global sea-level change to factors including added land-ice meltwater and thermal expansion; local relative level also depends on land motion. Its glacial-adjustment explanation shows that land can continue rising or sinking after ice retreats.[^12][^13] Keep ocean boundary level, local ground elevation and floating ice distinct. Freeze/melt of already floating ice cannot be treated as newly added land runoff. Coarse climatic histories can expand ice, expose shelves, isolate populations, inundate coasts or uncover abandoned sites, but their rates and water budgets must be declared. Do not squeeze an ice age into several ordinary days simply to guarantee a dramatic event during every campaign.

Past/future views should preserve geological scars, later occupation and inherited consequences. A buried road may emerge broken; a port may become inland; a drowned shrine may acquire a different community. These are transformations of persistent places. Generated ancient transitions need order-independent refinement; lived floods, construction and excavations require preserved committed history.

## Reference lessons and restrained presentation

Bay 12's public feature description connects multiple Z levels, water diversion, underground travel, resident institutions and a persistent world. It supports treating deep space as part of ordinary life, although that legacy overview is not evidence of Beta-scale co-op or arbitrary-time reconstruction.[^14] The tModLoader project's documented `ModWaterStyle` hooks cover water appearance, droplets and waterfalls. This is public extension-interface evidence: presentation variations are useful, but a water-color hook does not establish salinity, buoyancy or groundwater simulation.[^15]

Below Zero's April 2020 Frostbite post connects glacial terrain, cold, weather and creature animation. Its August 2025 patch separately fixes streaming/save failures, inappropriate distant damage and frustrating vehicle interactions.[^16][^17] Atmosphere requires both expressive performance and correct spatial consequences. Neither dramatic ice art nor a hazard name justifies damage from an unloaded or unrelated place.

A retrieved Reddit discussion reports surprise at repeated plant-caused base flooding; replies disagree about countermeasures, and reactions range from amusement to frustration.[^18] This is an attention/feedback hypothesis, not verified engine documentation: Beta should make perceived breach causes inspectable instead of repeatedly punishing players with unexplained repairs.

Native production needs swimming/hovering, climbing, wading, slipping, hauling, bracing, sealing a leak and equipment-compatible work. Aquatic people require appropriate locomotion and gestures; buoyant props and hanging tools need restrained secondary motion. Show depth changes through silhouette, occlusion, contact and selected lighting, rather than washing every sprite under an opaque blue filter. Offer floor/depth selection, readable exits, non-color hazard cues and accessible camera controls. Test the Sunken Crypt in dry, flooded and ice-affected epochs: recognizable masonry, repairs and ownership marks should survive while atmosphere changes. These are proposed native scenes, not new accepted art.

## Proposed modular event contract

Each environmental operation records cause/event ID, world/branch/realm, dated interval, rule versions, input revisions, affected region, material/water/energy sources, dependencies and a resumable work cursor. Suggested operations are `ExchangeAquiferWater`, `ChangeConduit`, `InundateCompartment`, `AdvanceIceExtent`, `DepositSediment` and `ReleaseImpoundedWater`. They prepare bounded mutations; the existing authority owns commit and durable receipts.

Commit accounted quantity changes, terrain/condition revisions and required support/fluid/nav/visibility follow-ups together. Large events progress through persisted deterministic work without publishing an inconsistent partial dependency. Unloaded neighbors are not empty reservoirs. Rendering and rumor propagation follow committed state; learned observations can remain uncertain. Region budgets change processing cadence, not which known town survives.

Breathing, bodily exposure, vehicle motion, leaking compartments and coupled falling ice join the existing Wait dependency contract. A held actor cannot drown on an unrelated wall clock, gain free oxygen, or pump water outside using frozen machinery. Geological events may continue elsewhere; influence is admitted before consequences, then revalidated. Historical visits and retries must preserve exact water/material ownership and never reroll a collapsed gallery.

## Retained validation cases — all unrun

| ID | Required proof |
|---|---|
| SUB-01 — One stacked world | Walk from an inhabited surface site into upper dry caves, a lower flooded passage and a deep settlement; cross a separate shadow portal. Verify distinct Z/realm ownership, clearance, resident identity and persistence. |
| SUB-02 — Changed groundwater | Apply one disturbance to declared supports/conduits while remote rain adds accounted recharge. Repeat across chunk order, restart and concurrent excavation; preserve water/debris, valid routes and unchanged unaffected passages. |
| SUB-03 — Flooded habitation and Wait | Breach an occupied compartment with different breathing capabilities, cargo and a powered pump while another player holds combat. Resolve admission, exposure, evacuation, repair and recovery without free air, duplicated goods or hidden damage. |
| SUB-04 — Advancing and retreating ice | Accumulate/ablate ice over recorded terrain, move sediment, impound and release a lake, then visit previously unprepared intermediate instants. Preserve irreversible erosion/deposition, reservoirs and exact-time identity across compaction/restart. |
| SUB-05 — A coast across epochs | Revisit one coastal settlement before and after water-level/land-elevation changes. Preserve structures, legitimate population/lifecycle transitions, cargo and ownership; validate changed routes and possible recovery without substituting an era prefab. |
| SUB-06 — Ecology and native readability | Interrupt a cave nutrient input and compare aquatic/cave inhabitants' feasible responses. Humans inspect actual dry/wet/icy native scenes, repeated motions, depth clarity and meaningful travel/care choices; agents retain observed inputs/state, not imagined playtests. |

These mechanisms should enrich a finite, original capability library. The sources do not prove that one reduced model handles every aquifer, glacier, underwater society or future event. Science-derived coefficients, host capacity, long-term ecology, native animation cost and enjoyable hazard frequency remain qualification work. The paper deliberately adds no second terrain authority, unrestricted environmental solver or untested implementation claim.

## Sources

[^1]: USGS, [Karst aquifers](https://www.usgs.gov/mission-areas/water-resources/science/karst-aquifers), 20 July 2021.
[^2]: USGS Circular 1139, [Groundwater/surface-water interactions, karst section](https://pubs.usgs.gov/circ/circ1139/htdocs/natural_processes_of_ground.htm).
[^3]: NPS, [Great Basin cave life](https://www.nps.gov/grba/learn/nature/cave-life.htm). Includes ordinary nutrient inputs and qualified discussion of chemically supported microbial ecosystems.
[^4]: USGS, [Groundwater-level response to earthquakes](https://www.usgs.gov/mission-areas/water-resources/science/groundwater-level-response-earthquakes), 20 July 2021.
[^5]: NPS, [Mammoth Cave caves](https://home.nps.gov/maca/learn/nature/caves.htm), back-flooding and food-web discussion.
[^6]: NOAA, [Light travel in the ocean](https://oceanservice.noaa.gov/facts/light_travel.html), updated 16 June 2024.
[^7]: Unknown Worlds, [Seabases released](https://unknownworlds.com/en/news/subnautica-seabases-released), 13 March 2015; explicitly early access.
[^8]: Unknown Worlds, [Oculus update](https://unknownworlds.com/en/news/oculus-update), 2016; historical oxygen/power behavior.
[^9]: NPS, [Anatomy of a glacier](https://www.nps.gov/glba/learn/nature/anatomy-of-a-glacier.htm).
[^10]: NPS, [How glaciers change the landscape](https://www.nps.gov/articles/howglacierchangethelandscape.htm), updated 9 February 2018.
[^11]: NPS, [Bear Glacier's August 2014 outburst](https://home.nps.gov/kefj/learn/news/glacier-lake-outburst-flood.htm), 4 September 2014; archived historical report.
[^12]: NOAA, [Sea-level change](https://oceanservice.noaa.gov/facts/sealevel.html), updated 16 June 2024.
[^13]: NOAA, [Glacial isostatic adjustment](https://oceanservice.noaa.gov/facts/glacial-adjustment.html), updated 16 June 2024; mechanism used, simplified historical dates not adopted as Beta chronology.
[^14]: Bay 12 Games, [Dwarf Fortress feature overview](https://bay12games.com/dwarves/features.html); legacy developer overview, not a version-complete implementation audit.
[^15]: tModLoader maintainers, [ModWaterStyle API](https://docs.tmodloader.net/docs/1.4-stable/class_terraria_1_1_mod_loader_1_1_mod_water_style.html), archived 1.4-stable documentation labelled v2022.09; a modding API, not Terraria's full source.
[^16]: Unknown Worlds, [Below Zero Frostbite update](https://unknownworlds.com/en/news/subnautica-below-zero-frostbite-update), 30 April 2020; early-access design evidence.
[^17]: Unknown Worlds, [Below Zero 2025 patch](https://www.unknownworlds.com/en/news/subnautica-below-zero-2025-patch), 12 August 2025; historical fix/experience evidence, not current defect claims.
[^18]: r/subnautica, [Repeated base flooding discussion](https://www.reddit.com/r/subnautica/comments/1sk5486/i_kept_wondering_why_was_my_base_flooded_all_the/), accessed 8 September 2026; self-selected observations and conflicting advice, unspecified builds.
