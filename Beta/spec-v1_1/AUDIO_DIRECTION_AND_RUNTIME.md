# Music, environmental sound and atmospheric transitions

## Direction and evidence

Beta's sound should make a place feel inhabited and a moment feel appropriate to its scale. An ordinary harbor morning, a quiet workshop, a threatening dungeon and a major battle need distinct musical and acoustic character. Breath of the Wild and Final Fantasy XV are references for integration and restraint; Chrono Trigger's seaside atmosphere is a reference for memorable local detail. Create original music and recordings/assets, with supported provenance. The [research dossier](../research-v0_9/adaptive-music-and-world-sound.md) distinguishes documented developer practice from inferred implementation.

The default musical direction serves cozy making, ordinary life and quiet exploration. Favor space, modest instrumentation, varied motifs, gentle fades and stretches of ambience or intentional silence. Bombastic battle music is rare and earned by the event; entering an ordinary encounter does not automatically select it. Test track recurrence, phrase fatigue and repeated activity jingles across long crafting/exploration sessions. Track/stem cooldowns and compatible variation can reduce repetition without replacing musical judgment with random shuffling.

Audio has three cooperating layers: actual or contextual environmental sound, responsive action/character effects, and a non-diegetic musical interpretation. They need not all be loud or continuous. Silence, sparse instrumentation and ordinary activity can make an extraordinary event meaningful. A switch from town to dungeon should feel coherent with travel and acoustics, not like two unrelated playlists colliding.

## Soundscape coverage

| Situation | Acoustic identity and musical intent | Required responsiveness |
|---|---|---|
| Seaside town and harbor | Seabirds overhead, water against shore/pilings, ropes/wood/work, wind and localized conversation; a recognizable restrained town identity | Species/activity and season fit the place; storm, empty docks or submerged streets change the mix without losing site identity |
| Farm, road and ordinary work | Crops/foliage, insects or birds where present, tools, footsteps, livestock, cart components and nearby labor | Weather, surface, equipment, action and actual inhabitants affect sounds; ordinary work does not require a constant epic score |
| Forest, desert and changing ecology | Wind filtered by cover, plant movement, distinct fauna, calls or warning/growl events; quiet or uncertain music where appropriate | Forest loss, migration, drought, magical taint and recovery change relevant layers gradually through actual ecological state |
| Cave, crypt and underdark settlement | Water drips, flow, distant permitted activity, equipment/footsteps and enclosure-dependent reflections; dungeon or inhabited-space identity | Flooding, new entrances, depth, room connections and material change filtering/reverberation; no omniscient creature tracking through rock |
| Underwater and ice | Filtered above-water events, current/flow, bubbles where produced, supported creature sounds, ice movement/cracks | Listener medium, enclosure/seal, breathing equipment, moving ice and hazards determine cues; vital warnings remain understandable |
| Day, night and weather | Wind, rain on different surfaces, canopy/shelter contrast and appropriate daily/seasonal activity | Blend from authoritative weather/time/ecology projections; entering shelter changes acoustic exposure, not the weather itself |
| Conversation, camp and discovery | Space for character performance, contextual ambience, restrained motif or harmonic change | Duck competing layers for dialogue, preserve ordinary environmental presence, and handle skip/interruption/late join |
| Battle and other encounter shifts | Clear initiating action, transition to the appropriate tension/encounter score, readable impacts and warnings | Fade/layer into the current encounter; distinguish threat, resolution, escape and changed aftermath; no routine replacement scene |
| Major world event | Selective escalation, meaningful silence or changed motif, with actual quake/water/fire/people sounds | Reactions follow perceived local consequences and accepted event state, rather than a global fanfare revealing a secret distant crisis |
| Historical/planar return | Familiar place motif with an appropriate revised texture and environmental population | A before/after drowned city or healed forest has continuity and difference; time travel is not an excuse to restart every cue blindly |

## Authority and listener knowledge

The core owns facts and sound-producing actions. It emits semantic events and permission-scoped context, such as a committed tool strike, a perceivable bird call, local weather, enclosure state or a known encounter phase. Presentation chooses samples, pitch variation, mix and musical arrangement. Audio callbacks cannot commit an attack, advance time, create a creature or award a discovery.

Distinguish an identifiable actor's acoustic evidence from a permitted contextual soundscape. An actual predator growl follows its legal vocalization/activity and audibility rules; it may reveal justified presence, never an undisclosed name or private intention. A contextual seabird bed can represent established ambient ecology without asserting the precise location of a named creature. If listening can create a target, clue or tracked actor, authority must support that observation and bind the relevant source. No layer invents huntable animals or allows an ambient loop to refill populations.

Musical context uses deliberately allowed presentation knowledge. A hidden villain's arrival or an unseen attack cannot automatically change the score unless that cue is an intentional permitted discovery. Mood is an artistic interpretation, not a readout of secret moral alignment, NPC utility scores or future random outcomes. Different co-op listeners can hear different mixes from their own place, knowledge and current encounter; they do not need identical audio waveforms.

## Adaptive score and transition policy

Each music definition declares its place/culture/mood role, compatible stems, segment/phrase boundaries, tempo/harmonic compatibility where used, transition options, intensity range, looping and silence policy. A finite authored set of compatible layers and segments covers contexts; every weather × biome × encounter permutation does not require a separate track. Composer approval remains necessary for a supported combination.

The score director consumes a context snapshot and ordered cues. A versioned priority policy resolves overlapping needs: critical readable action/dialogue, committed scene or encounter phase, place identity, then environmental color. Use dwell times and hysteresis to prevent rapid location-border or threat-state oscillation. Ordinary changes can align with authored phrase/bar boundaries; urgent moments can use an immediate cue followed by a short authored fade or bridge. Two incompatible tracks should not be stacked merely because both predicates became true.

Crossfade exploration into battle or another encounter without delaying legal action until music finishes. On exit, choose the appropriate resulting context—quiet aftermath, continuing danger, shelter or ordinary work—rather than always restoring the exact pre-fight track. Escape, interruption, death, a second encounter and dialogue during a fade each require deterministic cue priority at the presentation level. Fade duration, envelope, silence and transitions are musical tuning values validated by listening, not simulation-time contracts.

Use horizontal transitions between authored sections and vertical layering within compatible material where they improve the score. Layers may express weather, cultural identity, tension or magical influence, but the mix must remain restrained. Sparse place motifs can recur across eras with revised arrangement to preserve memory of the same location. Original compositions and sound choices should evoke the requested atmosphere without copying reference-game melodies or recordings.

## Spatial rendering, timing and persistence

Presentation applies distance, permitted occlusion and enclosure/medium profiles to received sound sources. Horizontal position, elevation, portals/entrances and underwater or sheltered listening matter even in a 2D-rendered game. Shared room/connection projections support believable cave reflections and outdoor-to-interior transitions. This is a bounded game acoustic model, not a global wave-propagation solver.

A cue includes stable event ID, source/context, current revision, relevance window and policy for repetition or recovery. Suppress duplicate delivery of one-shots within the same listener, branch incarnation and presentation-visit occurrence. A legitimate later visit to the historical event can sound again; stable event identity is not a permanent mute key. Reconnect resumes the active occurrence and rejects archival cues outside its relevance window. On late join or restart, reconstruct the current sustained ambience/music context; do not replay every old crash, roar and impact from the event archive. Choreography resumes its actual phase and the audio director reconciles accordingly. Cosmetic music phase need not be canonical save state unless a specific interactive mechanic explicitly requires it.

Wait permits appropriate background music, ambience and cosmetic expression. Consequential action sounds follow committed actions; held actors do not hear repeated damage ticks from a separate wall-time simulation. Environmental sound can continue where cosmetic, while boundary hazards obey fluid/terrain admission and physical backpressure. An interactive rhythm/puzzle, if introduced, requires its own explicit timing authority rather than reusing a presentation callback.

Preload/stream according to measured target-device latency. Keep sound and score resources outside the core; the Godot C# audio adapter receives semantic projections and owns actual playback/mixing. Logical responsibilities can remain a few focused modules, without an external middleware or extra runtime unless useful evidence justifies it. Bound active voices, use perceptual priorities and virtualize quiet sources without altering authoritative action/observation state. Desktop/mobile performance and codec/loop/seek behavior need actual qualification.

## First-class asset and action sound contracts

Every applicable shipping asset, material, creature, biome/environment and action declares its acoustic intent alongside visual/performance metadata. Record semantic triggers, material/contact tags, environment context, source/loop choices, listener/occlusion policy, variation/repetition limits, mix priority and required captions or equivalent essential feedback. An intentional quiet/silent case is explicit; a missing mapping is not silently treated as completed sound design. This does not require every tree or person to emit a continuous loop.

Footsteps, equipped gear, tool impacts, doors, craft stations, vehicle parts, water/ice, plant contact, vocalizations and NPC/player work must react to their supported state. Share tested material/action families where valid, then author necessary distinctions. Sound creators and engine builders review the same native action fixture, including silence, duplicate suppression and overlapping activity. Generation/import validators check the acoustic contract as well as textures and animation anchors.

## Creation, accessibility and acceptance

Dedicated composers/sound creators own editable projects or stems, original motifs, sound libraries, loop/transition metadata and mix references. Engine builders own cue wiring, listener/acoustic projection, playback and diagnostics. The central task coordinates their interface and independent reviews, with Codex/Claude interchangeable ownership where tool capabilities support the work. Tool availability and music quality are evaluated on delivered assets; no particular generation service is presumed installed.

Provide separate music, ambience, effects and voice control, master level, captions for meaningful sounds, visual alternatives to essential audio-only hazards, and suitable dynamic-range options. Ducking preserves speech and important cues; frequent warnings, repetitive calls and harsh overlapping loops require long-session review. Captions expose only the information a legitimate audible cue conveys. Reduced or muted music must not change gameplay outcomes.

P02 retains native town→wilderness→encounter→aftermath transitions plus a cave or sheltered comparison, actual actions, wind/rain and a contextual seaside-bird cue where suitable. P03 tests retries, late join, Wait, changing enclosure and competing transition cues. P04–P10 expand weather/ecology, cities, underwater/underdark and historical identities with the corresponding content. P11 completes the audio catalog; P12 requires human mix/music acceptance and long-session playback; P13 validates desktop release; P14 qualifies mobile interruption, memory and output behavior.

Retain AUD-01–04 from the research dossier, plus the revision's native integrated scene. No music, recordings, adaptive playback or listening acceptance was produced by writing this contract. Actual exported playback is required to establish timing, immersion and quality.
