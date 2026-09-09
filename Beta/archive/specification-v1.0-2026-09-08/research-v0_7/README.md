# Final architecture research pass

This research supports the [consolidated specification](../spec-v1_0/README.md). It combines primary developer statements, public technical material and clearly labelled community reconstruction. Each paper provides source notes, concrete design consequences and proposed validation. No proprietary original source availability, unviewed talk content or unexecuted gameplay result is implied.

| Research | Main evidence and consequence |
|---|---|
| [Classic architecture](classic-architecture.md) | FFIV/FFVI reconstructed routines, Chrono Trigger tooling and official developer testimony support declarative event/effect authoring and coordinated on-field staging. Hardware layouts, hidden restoration and presentation-blocking routines are historical constraints to avoid carrying into the new authority. |
| [Systemic worlds and narratives](systemic-worlds.md) | Dwarf Fortress continuity failures and ongoing simulation refinements, Qud's accessible actor abilities and storylet/deck design support persistent individual state, common action access, contextual selection and bounded work. System breadth requires authoring and presentation budgets as well as algorithms. |
| [Modern JRPG production](modern-jrpg-production.md) | Sea of Stars, Chained Echoes, Fields of Mistria, The Adventures of Elliot, Octopath Traveler 0 and Baldur's Gate 3 supply lessons in staging, readable encounters, co-op dependencies, animation, settlements and production scope. Observed features do not establish undocumented internals. |
| [Frontier streaming](frontier-streaming.md) | No Man's Sky's official update disclosures separate presentation improvements from preserved world identity. Grid streaming needs versioned generation, durable mutations, continuous shared edges, independent cameras and mode-specific look-ahead; travel scenery does not hide authority or save problems. |
| [Camps and daily rest](camp-and-rest.md) | D&D's rest structure, Sabotage's camp/cooking design and Larian's camp behavior fixes inform meaningful preparation and character interaction, partial interrupted recovery and lifecycle continuity. A 40-minute day is a Beta tuning hypothesis requiring real pacing evidence. |

The papers contain over 10,000 words of combined research and design analysis. They distinguish what a source actually establishes from the proposed Beta response. Primary pages are cited beside their claims or in source notes. The earlier [reference research](../reference-architecture-research-2026-09-08.md) and [code review evidence](../review-evidence/README.md) remain useful historical inputs.

## Decisions the research changes

Preserve the original game's content and deterministic ambition while rebuilding authoritative ownership in Beta. Treat procedural generation, active simulation and rendering as separate responsibilities with stable identities between them. Retain real offscreen people and obligations; constrain scheduling cost through bounded work and cadence rather than erasing known individuals.

Author adventurelets and encounter decks against world predicates, suitable roles and resource availability. Commit consequences through ordinary simulation commands. Multiple larger arcs can compete, reinforce each other, dissipate or be resolved by NPCs; the player is not required to become the designated world savior. Dwarf Fortress is the guide for consequential combinations, not a claim that every actor must run at full fidelity each frame.

Use common process/effect mechanisms for crafting, researched magic, vehicle maintenance and persistent environmental changes. Preserve all requested vehicle and companion families while measuring the cost of their shared verbs, traversal and art matrices early. Keep artists and engine builders separate, then integrate at native scale with state, motion and interaction contracts.

## Evidence boundaries

The No Man's Sky continuous-generation talk's listing and description were accessible; its complete presentation was not reviewed. The streaming paper therefore relies on official update disclosures for concrete implementation details and identifies Beta's own designs as proposals. Community disassemblies and event editors are reconstruction evidence, not official source releases or material to incorporate into this game.

Modern public documentation rarely exposes full proprietary engine architecture. The research does not fill those gaps with invented implementation facts. Historical interviews, announcements and shipped updates are dated and distinguished. Research-backed choices still require deterministic replay, save interruption, multi-client encounter/time, streaming pressure, migration and native-art proofs specified in the [validation plan](../spec-v1_0/VALIDATION.md).
