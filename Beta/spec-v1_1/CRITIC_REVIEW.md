# Independent systemic and implementation-readiness critique — revision 1.1

Substantive review completed 2026-09-08; the final input snapshot below was captured subsequently by the coordinator. This review owns only this document; specification authors supplied corrections. The delivered [1.0 critique](../spec-v1_0/CRITIC_REVIEW.md) and its inputs remain unchanged.

## Verdict and review boundary

**Suitable to begin retained P00 work and advance through P01–P03 under the specified exit gates.** The inspected revision retains all 181 requirements, complete P00–P14 delivery, full Alpha OpenFantasy assimilation and the required later mobile phase. No open P0, P1 or P2 finding remains in this review's recorded inputs. Six concrete findings are closed at the document level, including an execution-dependency defect discovered in the final integrated pass. This is not runtime verification, gameplay certification, native-art approval, production authorization or an assurance of affordable implementation.

The recommendation remains a new portable C# authority with Godot C# adapters, selective algorithm/test reuse and complete content incorporation. Copying another project's clocks, weak object identities, save regeneration or GPU fluid state would defeat the stronger contracts. Rust remains a serious conditional in-process alternative under Q-07; workload size alone does not establish a language choice.

All nine Q gates remain unclosed by executable or human evidence. Broad expansion depends on the early authority, language, environmental/history and native-production proofs. Gate failures require correction and affected requalification, not deleting NPC depth, historical access, required bodies or source content. The largest risks remain cumulative complexity, archival cost, scheduling, content throughput and actual player experience.

This review inspected the consolidated GDD/TDD, all seven focused contracts, decisions, full plan/coverage, requirements, validation, pain-point review, presentation, synergies, Astra handoff, overview and research index/coverage. It compared changed GDD/TDD/presentation/synergy/plan text with the previously reviewed preserved baseline, inspected the current additions directly, and checked complete current registers rather than sampling only new IDs. It did not reopen every previously assessed reference-game source.

The reviewer authored the local terrain/roguelike paper and earlier systemic/society research. Those papers are **excluded from independent content approval here**. Their hashes and case links were checked only as provenance. The parent's local-reuse decisions were reviewed independently for authority/dependency consequences; another critic is responsible for independent assessment of this reviewer's authored research. The reported LifeFormzFable source test is author-retained evidence, not a test run by this critic or a Beta result.

## Findings and dispositions

### V11-C01 — P1: finite basins did not guarantee finite macro preparation

**Affected:** GEOLOGICAL_WORLD_DESIGN, “Macro geography without false planetary guarantees”; TDD §7; P01/P03.

**Counterexample:** each catchment promises a finite downstream contract, but requests continue forever, or A requires B while B requires A. Locally finite objects still produce nonterminating frontier preparation.

**Correction verified:** preparation now requires a finite outlet/boundary witness and acyclic dependency ordering, or an explicitly finite strongly connected reservoir component solved together. Named budgeted boundary exchanges can close the model. Actual tidal/groundwater cycles are distinguished from generator recursion. REV-04 and GEN-01/04 retain the counterexample.

**Disposition:** Closed in specification. A terminating generator still needs implementation and measurement.

### V11-C02 — P1: an accepted hazard plan could ignore live intervention

**Affected:** GEOLOGICAL_WORLD_DESIGN, “Sudden events, actors and Wait”; ECOLOGY_AND_FLUIDS; TDD §7; P03.

**Counterexample:** early flood stages commit, then an NPC breaches a dam or a player closes a sluice. An immutable propagation plan forces predicted inundation through changed geometry.

**Correction verified:** immutable cause/exogenous forcing is separate from revisable uncommitted propagation. Each stage revalidates topology, availability, reservations, occupancy and revisions; stale fronts recompute or cancel, releasing/transferring reservations once. Worker completion speed cannot choose a different causal outcome. REV-02/08 and SYN-20 cover live and child-branch intervention.

**Disposition:** Closed in specification. Crash-safe replan/cancellation and resource transfer remain unrun.

### V11-C03 — P1: pending flow could become infinite invisible storage

**Affected:** geological Wait section, fluid/ecology clock section, GDD §12 and P03.

**Counterexample:** a held cellar cannot admit incoming water, while upstream rain continues and the pending queue grows without physical occupancy. Reservations falsely relieve upstream hazards or store unlimited volume.

**Correction verified:** pending liquid remains in an identified finite source/reservoir/buffer; reservation is not storage. Exhausted capacity produces legal backpressure/overflow or widens the hold/shared pause. Source quantity and receiver capacity cannot be spent twice. Outsider action intents cannot reserve arbitrary unrelated resources through this rule. REV-03/SYN-20 make the held-inflow case explicit.

**Disposition:** Closed in specification. Long-hold behavior, safe widening and native clarity remain unrun.

### V11-C04 — P1: phase completion could precede retained introduction

**Affected:** IMPLEMENTATION_PLAN dependency graph/prose; COVERAGE phase dependencies; RQ-025/051.

**Counterexample:** RQ-025 introduces discovery/creation primitives in P04 and completes in P05, but P05 depended only on P03. RQ-051 introduces institutions/claims in P07 and completes in P08, but P08 depended only on P04. An orchestrator following the DAG could accept completion before the introductory proof.

**Correction verified:** JSON, Mermaid and prose now include P04→P05 and P07→P08 acceptance dependencies. Preparatory work can remain parallel; acceptance waits for its prerequisites. Original requirement texts and phase mappings remain unchanged. Independent transitive checks pass for all 181 requirement introduction/completion pairs and all 116 mapped case introduction/integration pairs; the graph is acyclic.

**Disposition:** Closed in specification. This corrects planning; no implementation phase is thereby completed.

### V11-C05 — P2: local reuse named a nonexistent source file

**Affected:** LOCAL_REUSE_DECISIONS, “Conditional specialist candidates.”

**Counterexample:** the MinesweeperAdventure candidate named `logic.js`, although its actual inventory and linked audit locate the relevant code in `js/engine.js`. A worker could chase a nonexistent extraction target or infer uninspected algorithms.

**Correction verified:** the candidate now names `js/engine.js`; `logic.js` is absent. Seeded topology/solvability remains required and the bounded puzzle candidate cannot replace world geometry.

**Disposition:** Closed reference defect. This is not independent approval of the reviewer's source-audit paper or the candidate's behavior.

### V11-C06 — P2: the research ledger's source-register hash drifted

**Affected:** research-v0_9/RESEARCH_COVERAGE, `source_requirements_sha256`.

**Counterexample:** updating requirement-register metadata left its research-ledger whole-file hash stale, although all individual requirement texts/hashes still matched. The handoff could misidentify the reviewed revision.

**Correction verified:** the whole-file hash now matches current REQUIREMENTS bytes; individual hashes and bidirectional topic membership also match. Subsequent register edits, including metadata-only edits, require refreshing this binding.

**Disposition:** Closed. Mechanical integrity remains distinct from semantic implementation coverage.

## Integrated counterexamples and required actual evidence

These are **specification analyses only**: concrete unrun scenarios bound to existing families, not imagined successful playtests.

| Counterexample | Contract judgment and required proof |
|---|---|
| Mine a vein, bury it under lava, erode/redeposit material, then visit intermediate instants and export from two branches | Ordered geological/lived changes, stable source intervals and origin custody align. Execute REV-01/05/08 and temporal cases; measure provenance fragmentation and archive growth. A new heightmap plus old edit overlay cannot pass. |
| A visible harbor depends on an unvisited upstream basin; later requests arrive in reverse order | Shared boundaries and constrained ancient refinement preserve established facts. Execute GEOR/GEN with far/negative addresses, historical queries and package changes. Failed preparation must hold honestly, not silently reroll the seed. |
| Four players hold encounters while a quake changes ship supports and water enters a cave | Finite causal fronts, physical storage and complete admission are consistent with disclosed local stasis. Execute REV-02/03/09, SUB and retained VT/TIME cases, including oversized closures and crash/reconnect. No hidden oxygen loss, displacement, duplicate cargo or routine ocean-wide freeze may substitute. |
| Hunters, livestock care and a caravan compete for food during dormancy and taint | One ledger, actual carriers and identity/cohort promotion govern effects. Execute ECOG/LOCAL across detailed/coarse work, Wait, save and branches. Biome labels, population timers and camera arrival cannot mint food, organisms or mature trees. |
| A river shifts a national boundary and floods an inherited home | Feature-following borders, surveyed claims, possession and recognition remain distinct. Execute REV-09/SYN-17 with evacuation, interrupted trade and delayed news. NPC actions/dialogue must follow evidence, not an omniscient disaster flag. |
| Death occurs during a dream manifestation, institutional job and competing return rituals | Living-body control is revoked; eligible `TransitionForm` grants scoped control on the same person. Execute CULT-04–06 through split remains, inheritance, retry and branches. Spirits, echoes, animated remains and returned persons cannot clone knowledge/property or automatically reclaim an office. |
| A young traveler visits a millennium-later date, then a party consents to long advancement | Arrival dates do not become biological exposure. Execute CULT-03 and REST/TIME with consent, interruption and held dependants. Existing elders/near-due families show life stages without forcing a normal 90–120-day campaign into generations. |
| One co-op actor understands a cant and hears a ghost; another has only beginner knowledge | Localization, comprehension, testimony, truth and permission remain separate. Execute CULT-01/02 and mystery cases with false beliefs and translation. Fluency, death, captions and expert tooltips cannot reveal unknown crimes, secret membership or future outcomes. |
| An ignored lesson binds the village blacksmith, then the workshop floods | Unaccepted guidance reserves nothing; accepted tasks use ordinary expiry/cancel. Execute LEARN/REV-13/GEN with skip, alternate solutions, lost supplies and late join. Actual learning needs human observation even when a teaching witness is valid. |
| Generation overlaps structures, gives one actor incompatible roles and places a required clue in an optional ruin | Typed conflicts, finite repair/pruning and honest rejection apply before commitment. Execute GEN/RED with required Alpha obligations and remotely realized facts. Measure useful seed yield, retries and native diversity, not only rejection success. |
| A giant uses small stairs, a serpentine traveler boards a craft, or a quickling enters Wait | Actual dimensions, fit, motion and timing override sprite scaling. Execute BODY-X01 and transport/terrain tests for every required family. P00 inventory/P11 completion expose production cost; two humanoid kits cannot close the eleven-family catalog. |
| A migrant teaches warded-castle methods while local fuel and operators are scarce | Teaching/design lineage differs from object provenance and practical capacity. Execute CULT-X01/02 and CULT-07/08 with production, defense, upkeep and counterplay. Allegiance cannot transfer all technology, repaint belongings or confer universal superiority. |
| An impact arrives twice, a client reconnects, then revisits that historical event | Listener/branch/visit occurrence distinguishes retry from legitimate repeat experience. Execute AUD/REV-10/11 with mute, enclosure changes and interruption. Audio cannot commit actions; historical event IDs cannot become permanent mute keys. |
| A polished scene hides weak saves or an unqualified mobile export | P01 portable/desktop and P03 combined proofs precede breadth; P11 catalog/P12 quality precede release. Execute LANG and device matrices with real state/assets. SDK, target framework, bundled runtime and templates are distinct; installing SDK 10 is not proof of a net10 Godot client. |
| Player chat discloses a secret while a quartermaster reallocates reserved supplies | Chat remains authenticated, recipient-scoped player coordination rather than automatic NPC knowledge. Management policies create actual jobs within rights, capacity and commitments. Execute ARC-01/02 through retries, changed channels, shift changes, shortages and absent delegates; notices cannot reveal hidden events and stock targets cannot duplicate goods or labor. |

These scenarios preserve mandatory breadth without demanding a scientific global Earth solver, unrestricted genome invention, every NPC replanning every frame or independent art for every Cartesian permutation. Bounded approximations are acceptable only when their declared identities, quantities, consequences and readable behavior survive.

## Delivery and handoff assessment

The early plan provides real portable authority, a connected original-content loop, local/offscreen NPC decisions, two-market trade, stacked excavation, on-field Wait, source claims, exact history, packages and provider-free replay. Geology/ecology, biological/form identity and generation repair enter early schemas and P01–P03 fixtures. Later phases expand these retained modules.

P04–P10 cover domestic production, character/magic, named transport and companions, settlements/economy, society, narratives and temporal/planar/creator breadth. Art, audio, writing, UI, localization and accessibility start early. P11 reconciles complete Alpha semantic/playable migration and all shipping families, including cultural body/equipment/tile/action/acoustic coverage. The eleven newly named species families are additive to earlier dragons, medusas, minotaurs, giants, orcs and the full Alpha roster. Explicit zone/system chat and stockpile/workforce/district controls restore concrete earlier design families rather than relying on generic “communication” or “delegation” labels. P12 requires sustained campaigns, ordinary careers, concurrent players, archival/queue measurements and human quality. P13 supplies operational desktop delivery/support; P14 retains actual mobile interoperability. Required desktop domains cannot become post-release polish.

The Astra handoff is consumable without implying completion: current reading order, isolation from Alpha compile/import scope, preserved staged work, a first real core/host/client/test skeleton, finite catalogs, fixture preparation and durable completion records. It did not launch implementation or change a model. Codex/Claude can lead from the same files; bounded ownership, independent critics and separately owned artists prevent sole-author acceptance of consequential work or inferred art quality.

The ecosystem remains conditional on callable tools, useful output and measured friction. Installation and more agents do not establish model-performance benefit. Sea of Stars finish and Stardew-like responsive interaction remain separate bars; editable native production and user art/fun acceptance remain necessary. Scope is planned, while credible dates and staffing await throughput evidence.

## Checks actually performed

- Verified 181 unique requirement IDs, exact requirement/coverage texts and SHA-256 and no missing/extra rows. The original 150 requirement texts and phase mappings remain unchanged; 149 original coverage records are identical, while RQ-123 deliberately changes only its concrete-validation denominator from 150 to 181. Its archived record remains unchanged.
- Verified 15 acyclic acceptance phases and transitive introduction-before-completion for all requirements and 116 unique mapped case pairs after V11-C04's correction.
- Counted 63 distinct decisions, nine gates and 22 synergy chains in their actual registers.
- Verified 181 research texts/hashes, 35 bidirectional topics, nine dossier hashes/word counts and 48 unique research cases. Each new research case has its exact proof text in the production map. The refreshed whole REQUIREMENTS hash matches.
- Verified all 32 payloads in the new specification-1.0 archive manifest and all 14 source files within its frozen `spec-v1_0` set against recorded hashes. This is not a new audit of every earlier archive or original asset.
- Read the parent's [mechanical report](SPEC_VALIDATION.json) and limits. Broader original-source/ZIP, earlier archive, ecosystem-target and local-source-fingerprint counts belong to that separately executed report, not independently rerun checks here.

The 48 new research cases comprise 38 web-informed and ten local-adaptation assertions, all unrun in Beta. GEN, REV, cultural/body design extensions, SYN and preserved 1.0 VT cases are additional specification assertions, not hidden additions to the research count. Earlier 53 research cases remain retained and unrun. One source-only LifeFormzFable continuation test passed in its recorded environment; 88 other tests were filtered out. This critic ran no game, core test, export, benchmark, native animation/audio preview or human playtest.

## Final input snapshot captured by coordinator

The reviewer delivered the substantive findings and verified their corrections before an agent usage limit interrupted final hash packaging. The coordinator captured the following delivered-file hashes and reran mechanical integrity checks. This final capture is not an additional independent review, a reviewer signature over later bytes, or approval of future edits. The review's inspection and authorship exclusions above still apply. Later semantic changes require affected review.

The completed [archive continuity audit](ARCHIVE_CONTINUITY_AUDIT.md) was attached by the coordinator after the review. Its chat, management and additive-species contract corrections had already been inspected by this reviewer, as recorded above; final audit packaging is not claimed as an additional independent critique. The generated mechanical report records the audit and both critics without circular hash dependencies. Research coverage is provenance-only where authorship overlaps.

| Delivered input | SHA-256 captured by coordinator |
|---|---|
| [README.md](README.md) | `55373ffe81fbb4aa942557ed2bd0cfd4579592e998497d39f36495b775266c1f` |
| [GDD.md](GDD.md) | `5d8158218fe38b0f081a31b219e150a37c484e1e0655a327567966b152a1c96d` |
| [TECHNICAL_DESIGN.md](TECHNICAL_DESIGN.md) | `4cbd6b0b085843b2b5325f28da607112a19ae5a585c79bb9501e842f1b85dc2a` |
| [DECISIONS.md](DECISIONS.md) | `d18d383f37a855a0ccff835acd985b6312bc42b0867abd67aea0db0b0da1964c` |
| [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) | `e26485cb3cd3bd551149d685698cb575242a60d4e1fdacb501bb70cda156f12a` |
| [IMPLEMENTATION_HANDOFF.md](IMPLEMENTATION_HANDOFF.md) | `7c4e0a7aefb5a7419cb2557bafd20f7ba18c87e44c8b7d5d4fb9fc83e7636d45` |
| [REQUIREMENTS.json](REQUIREMENTS.json) | `275f27d2a73ccad63a614435bb77bdd9954d4d10c35f193a5567caa28fe35aba` |
| [COVERAGE.json](COVERAGE.json) | `302b72f0990edd558440775d77fd48e6ecd16f2e3f0f845d499e1cd77e4d1864` |
| [VALIDATION.md](VALIDATION.md) | `03ea3cfe012e814ede28bc56b31313a138bf14f8b31a64cc82d40c7a70ce5974` |
| [GEOLOGICAL_WORLD_DESIGN.md](GEOLOGICAL_WORLD_DESIGN.md) | `944b3b01640d74c32475568b820e47ce6f30b519989a47da213648ddcc1501f4` |
| [ECOLOGY_AND_FLUIDS.md](ECOLOGY_AND_FLUIDS.md) | `3579f83d6c025a131108da7121200b3a1d687401e853f548585f69f6bd080dab` |
| [AUDIO_DIRECTION_AND_RUNTIME.md](AUDIO_DIRECTION_AND_RUNTIME.md) | `610b5879b03794fe6f2e1354a5cb5e38eb5bdb6a8863397ad33bf494d70aa261` |
| [PLAYER_EXPERIENCE_AND_ONBOARDING.md](PLAYER_EXPERIENCE_AND_ONBOARDING.md) | `46786e4bddbee393297afcb3d594fbf0ea940ace6697389b1bafecb0c3e1bd14` |
| [LANGUAGE_AND_AFTERLIFE.md](LANGUAGE_AND_AFTERLIFE.md) | `643f57051fadf69a18841d3e027600c418dc420ba6281f7f2d42ad15b4134a86` |
| [GENERATION_VALIDATION.md](GENERATION_VALIDATION.md) | `3669cf511b97d8c020ffdd1a5bf086678d2e241bf79958f9e5358edcc17a41df` |
| [PRESENTATION_CONTRACTS.md](PRESENTATION_CONTRACTS.md) | `57d10a651d3df975a0ae0a58bf12dd4a59add62817ac038ac0979b221f937547` |
| [CROSS_SYSTEM_SYNERGIES.md](CROSS_SYSTEM_SYNERGIES.md) | `1adf733c7ed9870ae2bb4e881efe82583afbc6195bcf45954df75a0cb1a27540` |
| [PAIN_POINTS_AND_RESOLUTIONS.md](PAIN_POINTS_AND_RESOLUTIONS.md) | `5cc7e2a7a415512e0434d4b3c2c0de654866de545c4dd205b7d200335005e97b` |
| [LOCAL_REUSE_DECISIONS.md](LOCAL_REUSE_DECISIONS.md) | `634dc338637ff19af1ee7ccbf00e676a7ca6858f992755fbbd467d0c91f19587` |
| [../research-v0_9/README.md](../research-v0_9/README.md) | `d4056660fbb0854eab0cd3526d0c6fe5b093420d8d3cdafe0798384d130cd14d` |
| [../research-v0_9/RESEARCH_COVERAGE.json](../research-v0_9/RESEARCH_COVERAGE.json) | `1c7e8597cbe72a9051f564ce0655ef1211781bc81e6fed70e4dee58666e21a32` |
