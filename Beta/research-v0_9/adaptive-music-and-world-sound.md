# Adaptive music and world sound

Research checked 8 September 2026 for **Beta**, the successor to **Alpha OpenFantasy**. The recommendation is a composed, adaptive score supported by expressive world sound, with deliberate space for ordinary life. This paper proposes contracts; it does not claim a working audio implementation, completed listening tests or access to proprietary engines. Reference recordings and melodies are inspirations, not production assets.

## 1. Developer evidence and its limits

Nintendo's Breath of the Wild developer blog describes recording many source sounds, combining materials for footsteps/equipment and selecting/mixing them according to Link's actions and equipment. Manaka Kataoka separately describes expressing nature, history and characters through musical choices. This directly supports sound as authored material and narrative expression, rather than a generic effect attached to an animation name.[^1]

The composers' soundtrack-booklet interview, available here through an attributed English translation, explains why constant dramatic music could impose the wrong feeling on mundane actions. They discuss preserving environmental sounds, different day/night moods, town music and extensive walking through the actual world to tune transitions. This is first-person design evidence through a translated source; it is not an engine specification.[^2]

Sho Iwamoto's GDC 2017 presentation provides actual Final Fantasy XV architecture evidence: MAGI supports tempo/meter changes and authored synchronization points. The slides distinguish **vertical remixing**, changing simultaneous layers for reversible modes such as riding/fishing/outposts, from **horizontal resequencing**, progressing between sections for events such as bosses and summons. They also document asymmetric transition timing: responsiveness and avoiding musical clutter can require different entry/exit rules. This demonstrates a production solution, not a mandate to recreate MAGI.[^3]

In a direct 2018 interview, FFXV audio lead Atsushi Suganuma describes animation-driven sounds, weather/day-driven ambience, creature palettes associated with AI states, and ambient layers responding to nearby tree/crowd density. He also discusses CPU/memory constraints shaping Windows Edition spatial audio. The practical lesson is contextual selection and measured mixing cost. Neither “ecosystem” terminology nor layered crowd sound establishes complete individual acoustic or ecological simulation.[^4]

A later Nintendo developer account for **Tears of the Kingdom**, clearly a different game, describes increasing musical tension along an ascent and deliberate silence at a revealing view. It emphasizes avoiding spectral conflict with informative effects. Silence and contrast are authored tools, not missing content.[^5]

## 2. Proposed musical contract for Beta

Give regions, settlements, cultures, relationships and major narrative possibilities original thematic identities. Towns can feel inhabited and welcoming; dungeons can feel enclosed, uncertain or reverent. Those are contextual choices, not mandatory cheerful/ominous labels. Quiet, cozy crafting and exploration are the default emphasis. Varied sparse phrases, gentle fades and stretches without score prevent an overbearing soundtrack. Bombastic battle ballads are very rare, reserved for warranted dramatic stakes; entering ordinary combat does not automatically authorize them. Weather or nighttime can alter instrumentation and texture without restarting a track on every cloud change.

The presentation layer receives a **listener-scoped musical context**: known location/era, permitted narrative state, encounter phase, perceived threat, activity, weather and current dramatic focus. It selects a cue family and priority with hysteresis/minimum residence periods. Secret villains, undiscovered ambushes and another player's private revelations must not choose revealing themes. Unease can reflect audible evidence or an explicitly authored, permitted dramatic cue; it must not expose hidden simulation truth accidentally.

Each music asset defines intro, sustainable sections, exit points, transition bridges, compatible stems and authored fade curves. Use horizontal transitions between exploration, encounter, combat, aftermath and changed location; use vertical layers for compatible changes within a composition. Harmony, meter and phrase compatibility need composer decisions: crossfading arbitrary tracks is not automatically musical. Preserve urgent action/SFX immediately while scheduling a musical change at the next suitable point; never delay a combat command to reach a bar line.

Long Wait decisions need a sustainable, non-escalating musical treatment. Brief encounters should not repeatedly restart a grand introduction or victory fanfare. Dialogue and intimate social moments may duck or replace layers; interrupted narration cancels its pending accent. A return from combat resolves toward the **current** environment, which may have become stormy, burned or evacuated.

## 3. World sound must correspond to a world

Separate persistent environmental beds, sparse ambient life, identifiable source events, character/object Foley and interface cues. Rain on a roof, wind through foliage, a cave drip's echo, birds overhead, distant growls, a forge and a ship's rigging should differ because their materials, movement and surroundings differ. The owner's valued Chrono Trigger seaside-seagull memory is an aesthetic target for an original coastal soundscape, not proof of that game's internal implementation.

Ambient birds or insects may represent an authorized local population cohort; every chirp need not instantiate an NPC. An identifiable quest creature's call, a conversation, an enemy footstep or a hunted animal's movement must come from the appropriate actual source and perception rules. Captions must preserve that same uncertainty: “distant growl” cannot become the hidden creature's name. Reducing audible voices under load must not erase simulation sound emissions used by NPC hearing, alarms or stealth.

Use the existing stacked-space topology for a bounded acoustic projection: listener/source positions, open passages, barriers, materials, enclosure and medium determine attenuation, filtering and reverb profiles. A sealed room, breached wall and flooded crypt differ. Underwater perception deserves an authored profile rather than a universal muffling filter on every sound, including UI. A 2D screen view does not imply all stacked floors share one acoustic plane. This is a proposed approximation, not physically exact wave propagation.

Native motion supplies controlled contact/action markers for footsteps, gear rub, wingbeats, breathing and object handling. Variation pools and cooldowns prevent machine-gun repetition; they do not detach meaningful sounds from actual actions. Fewer well-placed birds and drips can convey more space than a continuous wall of effects.

## 4. Audio time, Wait and shared worlds

Keep audio transport and crossfades independent of authoritative simulation time. Music can continue smoothly during Wait without advancing hunger, hazards or combat. Held actors cannot generate fresh footsteps, attacks or narrated events merely because their local audio loop continues. Sustainable ambience is presentation; new authoritative thunder, collapse or creature calls require their actual event/exposure rules.

Four separated players can legitimately hear different music. Replicate permitted semantic context and authoritative sound events, not sample-by-sample background music. Shared diegetic performances need a shared event start/phase reference and bounded local correction. Music playback never owns world chronology. On reconnect, reconstruct current beds and cue phase, suppress expired one-shots and deduplicate event IDs; do not replay every battle accent missed during disconnection. Historical travel clears obsolete branch context and selects only the destination's permitted state.

Godot's documented `AudioStreamInteractive` supports clip transitions and timing/fade rules; `AudioStreamSynchronized` supports simultaneous streams. These are credible native starting points.[^6] Its synchronization guidance explicitly discusses audio-thread buffering, output latency and clock drift.[^7] Retain a native proof on the selected Godot version before introducing middleware. An audio-resource API alone does not prove seamless stems, variable-meter authoring, device recovery or sufficient streaming performance.

## 5. Composer, sound-creator and integration workflow

Separate composer/sound-design ownership from engine integration, coordinated through the central task. Deliver editable sessions, stems, masters, loop/exit markers, tempo/meter metadata, variants, source/license provenance and an auditionable transition map. Creator agents can prepare alternatives and metadata; the audio director and owner evaluate identity, emotional fit and fatigue. Automated waveform checks cannot approve composition or atmosphere.

Build an audio coverage matrix alongside native animation: terrain/material contacts, bodies/equipment, creatures/actions, vehicles/load/condition, weather/enclosures, settlements/cultures, narrative states and accessibility. Acoustic intent is mandatory asset metadata for environments, biomes and NPC/player/creature activities: event or bed, source/medium, variation, priority and contextual voice limit. Intentional silence is valid; a missing mapping is not approval. Reuse coherent sound families without making every creature identical. Schedule listening walks through real retained scenes, including mundane maintenance and prolonged exploration. Inspect transitions in context, not only an exported soundtrack album.

Provide separate music, ambience, effects, dialogue and UI controls; useful captions, mono compatibility and restrained dynamic-range options. Critical information needs equivalent accessible presentation. Measure simultaneous voices, decompression, streaming memory, underruns, peak levels and long-session drift on desktop targets and later mobile. Do not choose middleware from reputation alone: require a demonstrated native limitation and measured authoring/export benefit, including licensing and maintenance cost.

## 6. Retained validation cases

All four cases are **unrun**. Introduce real native playback during P02 and interruption/replication proofs in P03; expand domestic/travel/world coverage through P04–P10, complete assets in P11, and qualify listening/performance in P12–P14.

| Case | Required retained evidence |
|---|---|
| AUD-01 — musical transitions | Traverse town, wilderness and dungeon through day/night/weather changes, brief battles, long Wait and dialogue interruptions. Record the cue/transition trace and native output. Check phrase continuity, no clicks/storms of restarts, intelligible effects and human acceptance of both epic and mundane pacing. |
| AUD-02 — acoustic world truth | Follow original coastal birds, a specific creature, roof rain and cave drips across stacked rooms, a breached wall and submersion. Verify source/medium/topology changes, local population allowance, captions and secrecy. Muted audio must preserve authoritative NPC hearing consequences. |
| AUD-03 — clocks and recovery | Run four separated players, one held encounter, a shared performance, reconnect, scene streaming and historical travel. Verify no gameplay advancement from audio transport, no expired one-shot replay or private-story leakage, correct context recovery and bounded playback drift. |
| AUD-04 — production endurance | Play long native sessions with dense weather/crowds/vehicles, cozy crafting/exploration, routine combat and a rare warranted climax on representative desktop and later mobile hardware. Audit acoustic-intent mappings and contextual voice limits; capture memory/streaming/underrun evidence. Compare headphones, speakers, mono and reduced dynamics. Humans assess fatigue, repeated songs/phrases, gentle fades, restraint, atmosphere and accessibility. |

## Sources

[^1]: [Nintendo, Breath of the Wild developer blog](https://www.nintendo.com/jp/character/zelda/blog/index-04.html), June 2017 music/Foley entries; Japanese text inspected and paraphrased.
[^2]: [Composers' soundtrack-booklet interview, English translation](https://nintendoeverything.com/breath-of-the-wild-composers-on-changing-up-zeldas-music-formula-and-more/), published 5 January 2019; translation provenance distinguished from Nintendo-hosted text.
[^3]: [GDC 2017 session record](https://www.gdcvault.com/play/1023971/Epic-AND-Interactive-Music-in) and [Sho Iwamoto's Square Enix presentation, archived mirror](https://archive.thedatadungeon.com/final_fantasy_xv_2016/documents/technical/iwamoto_sho_epic_and_interactive.pdf), especially slides 7–19. Slides/abstract inspected; presentation audio was not auditioned.
[^4]: [Atsushi Suganuma, direct interview](https://designingsound.org/2018/05/30/interview-atsushi-suganuma-of-square-enix-japan/), 30 May 2018; historical FFXV production/Windows Edition account.
[^5]: [Nintendo, Music that supports the game experience](https://www.nintendo.co.jp/jobs/keyword/132.html), Tears of the Kingdom developer account; Japanese text inspected.
[^6]: Godot official [interactive streams](https://docs.godotengine.org/en/stable/classes/class_audiostreaminteractive.html) and [synchronized streams](https://docs.godotengine.org/en/stable/classes/class_audiostreamsynchronized.html); pin the implementation version during qualification.
[^7]: [Godot 4.6, Synchronizing gameplay with audio](https://docs.godotengine.org/en/4.6/tutorials/audio/sync_with_audio.html); timing guidance, not proof of Beta's behavior.
