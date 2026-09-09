# Independent architecture and production review

## Review status

**Final expanded specification review, 2026-09-08: suitable to begin retained P00–P03 foundational implementation and its evidence gates.** The actual integrated 150-requirement baseline, seven expanded research dossiers, presentation/synergy contracts and final pain-point resolutions have been inspected. All **14 recorded findings are closed as verified document corrections**; no open P0, P1 or P2 contract finding remains in this inspected revision. The earlier 127-requirement review is superseded by this disposition, with its findings retained below.

This does not certify a playable game, production capacity, native motion, successful migration, or enjoyment. All seven Q-01–Q-07 qualification gates still require their stated execution/acceptance evidence. Broad P04 production remains conditional on the foundational/language proofs, and bulk art requires native human acceptance. Later expanded workloads must requalify earlier capacity evidence.

The recommended architecture preserves OpenFantasy's deterministic simulation and complete content lineage while building the portable C# authority and Godot presentation in Beta, subject to the explicit early language qualification gate. A whole Rust authority through a coarse in-process ABI remains a justified alternative if measured pervasive costs or a concrete embedding need meet that gate; it does not require IPC or two state owners. Extending the existing scene/runtime architecture wholesale would retain couplings that this specification deliberately replaces. The existing game remains the source of content, behaviors and migration fixtures. The union of compatible concepts belongs in one coherent world model; it does not require retaining two competing engines.

This is specification analysis. No playable Beta, execution trace, benchmark, animation, human playtest, or production import was run for this review. Virtual tabletop walkthroughs describe adversarial interactions and expected evidence, not passing gameplay tests.

Severity: **P0** means an immediate authority, durability, or scope failure that makes the proposed implementation unsafe to follow; **P1** means a contract or dependency blocker before the affected phase can proceed; **P2** means an important refinement or tuning question with a bounded implementation path. No open question is permission to discard required scope.

## Findings and verified dispositions

| ID | Severity | Source and problem | Required correction | Disposition |
|---|---|---|---|---|
| CR-01 | P1 | v0.6 §12 and v0.5 final paragraph left calendar exposure, deadlines, encounter merges, hazards, vehicle motion, and disconnect behavior unresolved. | Define clock owners, entry/exit accounting, coupled effects and long holds. Distinguish rules, tuning and unproven evidence. | **Closed as specification:** TDD §§5–6 declares shared pause, independent local stasis without catch-up, admission/revalidation, complete vehicle coupling, fixed versus anchored deadlines, release and recorded disconnect handoff. P03 supplies executable proof. |
| CR-02 | P1 | Practically unbounded frontier plus persistent player-equivalent individuals had no finite realization/scheduling envelope. | Commit identities and remote facts when established; preserve them while bounding active scheduling and latent generation. | **Closed as specification:** GDD §3, TDD §7 and frontier research distinguish finite realized history from potential regions; camera/order cannot recreate established people. Workload/storage qualification and safe refusal remain required. |
| CR-03 | P1 | Universal runtime reachability of every old quest would conflict with autonomous NPCs completing or destroying opportunities. | Separate valid initial fidelity fixtures from truthful continuations in a changed world. | **Closed as specification:** GDD §§3, 19, TDD §9 and plan P09/P11 require both source-route proof and systemic adaptations. Preservation alone in an archive does not satisfy incorporation. |
| CR-04 | P1 | Body/equipment/gesture/mount/transformation breadth could make still-image acceptance masquerade as animation coverage. | Enumerate shipping compatibility and native proof before multiplying assets. | **Closed as specification:** GDD §17, plan art lane/P02/P05/P11 and VALIDATION §6 require a versioned intersection matrix, native motion, editable assets and human acceptance. No permanent unaccepted legacy fallback satisfies release. |
| CR-05 | P1 | Temporal/history and package proof could be deferred until after schemas and bulk content hardened. | Test branch custody, clocks, identity/versioning and upgrades/removals early in retained modules. | **Closed as specification:** plan P01 introduces authority/interfaces and P03 proves real transfers, recovery, versions and optional-provider-independent replay before broad production. P02 already implements real bounded NPC cognition; P08 expands its institutional scope. |
| CR-06 | P1 | Shared pause for an oversized dependency union initially lacked a progress mechanism. | Process the complete set in bounded deterministic slices; never truncate participants or hazards to fit a target count. | **Closed after author correction:** TDD §6 now specifies sequential slices, durable continuation, no partial logical publication and no calendar advance before complete commit. Unsupported dependency rules fail diagnostically instead of pretending pause solves them. |
| CR-07 | P1 | Item-only provenance could permit two branches to mine the same copied unextracted deposit and export newly minted IDs. | Bind claims before extraction; preserve quantity intervals through splitting, merging, processing and renewable cycles. | **Closed after author correction:** TDD §9 covers source reservoirs, production-cycle identity, transformed claims, return custody and fork-before-extraction/crash fixtures. No new export allowance appears on return. |
| CR-08 | P2 | S0/S1–S2 gates conflicted with plan P00–P14 naming. | Use the plan identifiers consistently. | **Closed after author correction:** TDD/DECISIONS now name P00–P03. |
| CR-09 | P2 | New TDD §5/D-27 initially place isolated-rest UX qualification at P02/P04 while plan P03 first implements actual rest. | Align qualification with implemented work and distinguish the initial day/night introduction from full rest. | **Closed after author correction:** TDD §5/D-27 now require P03/P04 rest qualification, and plan P02 explicitly introduces native day/night calibration. RQ-126 introduction at P02 is consistent with this narrower initial proof. |
| CR-10 | P1 | Expanded TDD §8 initially failed to distinguish powers held by a deceased actor from previously issued institutional delegations. A ruler's distant death could ambiguously cancel every standing tax, care or supply order. | Type issuer principal and survival/revocation policy; preserve authorized third-party work according to its recorded contract and maintain scoped notification. | **Closed after author correction:** TDD §8 distinguishes actor, office and institution issuers, vacancy policy, surviving worker jobs and interval revalidation. Personal action/office powers end; legal rejection cannot reveal a secret death. |
| CR-11 | P2 | Expanded TDD §7 initially lacked allocation order for competing fluid inflows computed from previous state. | Use stable joint/source-edge allocation with source and receiver reservations before commit. | **Closed after author correction:** TDD §7 reserves source volume and destination capacity in phase/source/destination order against the previous committed step, preventing overfill and duplicate source spend. |
| CR-12 | P1 | Arbitrary historical preparation in TDD §9 initially left multiple same-time commits and the moving source-head boundary implicit. | Pin a source revision and define inclusive timestamp/commit ordering for reconstruction and writable-child creation. | **Closed after author correction:** TDD §9 pins the accepted head revision, includes qualifying same-time commits in sequence order and permits an explicit earlier cursor. Prepared references and child prefixes remain immutable across retries and later live commits. This preserves exact-time access; P03 still must execute the proof. |
| CR-13 | P2 | GDD §15 initially permitted paid conversion of a dream object while TDD §9 prohibited symbolic ore/equipment becoming physical cargo. | Distinguish a separately defined paid physical creation from exporting a dream duplicate; align the baseline wording. | **Closed after parent correction:** GDD §15 now forbids exporting symbolic cargo and permits only separately validated waking creation/transformation with real costs and provenance. TDD's inventory/tether rule remains intact. |
| CR-14 | P2 | The final ecosystem dossier's ECO-01–04 cases were retained in validation and substantively covered by phase work, but lacked the explicit plan/coverage maps given to the other new dossiers. | Map workflow comparison, second-kit proof, package/history compatibility and provider-independent replay to retained introduction and full-integration gates. | **Closed after plan-author correction:** IMPLEMENTATION_PLAN's ECO row and COVERAGE's ecosystem case map name P00→P02, P02→P04 and P03→P10 duties, with later regression. All remain unrun; complete native catalog acceptance still belongs to P11/P12. |

The independent integration review also found shared-appointment ambiguity and incomplete temporal-transfer membership. Both corrections were read in the integrated TDD: §5 assigns one visible anchor to a negotiated shared-duration obligation and defaults shared appointments to fixed world dates; §9 atomically includes the consented vehicle/crew/passenger/mount/cargo/effect/job dependency group or rejects before departure. See [the independently owned integration review](INTEGRATION_CRITIC.md) for its own disposition.

## Adversarial virtual tabletop walkthroughs

Each walkthrough is a design exercise. Its runtime evidence remains to be produced in the retained implementation.

### VT-01 — The held battle and the working household

A player enters Wait combat near a settlement while another player travels independently. A child has an appointment, an NPC has promised to deliver medicine, a pet needs feeding, and a rainstorm threatens a workshop. The first player spends an arbitrarily long real interval choosing.

The contract must determine exactly which world intervals, actor needs, appointments, weather changes, and tactical effects advance. It must say what happens when the courier or fire becomes encounter-relevant and whether a player can gain work, healing, study, or hazard immunity by entering combat. The response may disclose temporal abstraction, but cannot use an undefined “catch up later” operation to apply retroactive damage or duplicate labor. Repeat with shared pause, independently exploring co-op, disconnect, and reconnect.

### VT-02 — One reagent, three promises

An NPC alchemist reserves the last herb lot for medicine. Another NPC promises that lot to a trade guild. A player offers the same lot during spell research and burns the workbench before the first job completes.

Only one compatible custody/reservation history may commit. Promises can be made without guaranteed fulfillment, but jobs cannot consume goods twice. Interruption must preserve actual work, waste, catalyst condition, knowledge acquired, and released reservations. Belief that an herb remains available can be stale without making inventory ownership contradictory.

Repeat automatic material selection with equipped, favorited, protected heirloom and reserved items that technically match the recipe. Default selection must honor durable visible protection and custody; an intentional permitted-owner override previews the actual lots. It cannot protect an item by leaking an undiscovered quest significance or silently consume prized equipment to make the concise interface appear successful.

### VT-03 — The hungry familiar on a departing ship

A bonded bird carries a message and a valuable charm. Its keeper boards an airship, dismisses the familiar, saves, then recalls it after the ship crosses unloaded territory. A delegated caretaker had promised to feed it.

Dismissal must resolve the same individual's body, custody, needs, bond, and pending delivery under a declared lifecycle. Care consumes a real supply and labor interval. Recalling cannot create a second bird, erase hunger or injury, teleport cargo for free, or reveal news before the message's supported transmission occurred. Repeat with an ordinary mount, injured animal, NPC owner, and body transformation.

### VT-04 — Two grand narratives compete for one person

A priest is eligible for a succession-crisis adventurelet while a secret faction seeks the same priest to awaken a potential dark lord. A separate ecological crisis threatens food production. An encounter deck selects a roadside meeting with the priest just after another NPC arrests them.

Revalidate the binding; the deck cannot conjure a duplicate priest or undo arrest. Cancel, transform, or defer the opportunity using authored rules and explainable selection state. These arcs should interact through loyalties, resources, information, and actions. A single global progress flag must not silently force every arc into the same ending. Test no dark lord, successful opposing coalition, transformed crisis, and continuing domestic careers.

### VT-05 — Someone else completes the old quest

Start from an original OpenFantasy quest's valid initial conditions and complete its preserved objective/reward path. In another run, let an autonomous NPC achieve the objective before the player accepts it.

Both runs must preserve the original content lineage. The second needs truthful continuation, credit, dialogue, and dependent-quest handling; it cannot re-create a slain target or claim the player did the deed. The migration report must distinguish preserved-route proof, live-world adaptation, and complete source accounting. Merely retaining prose in an archive is insufficient incorporation.

### VT-06 — A vehicle crosses a temporal boundary

A customized carriage carries two people, a mount attachment, borrowed cargo, an enchanted axle, and an unfinished repair. It attempts a temporal/planar transfer while an earlier transaction is retried and one passenger refuses or disconnects.

The transfer contract needs an explicit atomic group or a rejected partial transfer, origin/destination provenance, ownership of divergent counterparts, clock-domain conversion, and durable retry receipts. The outcome must not leave passengers attached across worlds, duplicate a loaned artifact, or reset wear. This needs an early retained fixture before the full transportation catalog is built.

### VT-07 — News outruns frontier realization

A caravan from an unvisited region has already delivered an artifact, treaty, and named refugee. Later, four players approach that region from different directions while generation workers finish in different orders.

Committed references constrain the region's eventual realization. The seed cannot reroll the refugee's hometown, parents, source material, or political history because the loading order changed. Generator versions and macro facts must reconcile across chunk edges. Yet the engine need not enumerate every possible future citizen in the unbounded frontier. Document the boundary between latent possible content and instantiated world truth.

### VT-08 — An unusual body changes its profession

A birdlike NPC becomes a skilled smith and illusion specialist, wears inherited armor, loses use of a limb, changes shape temporarily, and joins a paired repair action on a sandship.

Capabilities, proficiency, anatomy, equipment fit, attachment points, action prerequisites, and presentation must agree. If a particular action is mechanically legal, there must be an approved readable performance or functional fallback. Unsupported sprite combinations must not silently delete class powers or change stats. Asset families must preserve identity and variation without requiring an independently hand-painted animation for every Cartesian product.

### VT-09 — A blocked route becomes a planning storm

A bridge collapses during harvest. Thousands of persistent NPCs reconsider jobs, meals, shipment routes, family obligations, and shelter. Two factions then issue competing orders.

Prioritize by declared deterministic work policy, cap search expansion, schedule retries, and preserve urgent needs without looping on impossible paths. No hardware-dependent dropping of decisions may change history silently. Measure backlog, starvation, failed-path reasons, memory growth, and restoration time on the desktop target matrix; target counts remain hypotheses until measured.

### VT-10 — A package changes an ancient promise

A package update changes a spell primitive and a deity definition. A centuries-old pact still grants an effect, a dead NPC's heir owns its bound object, and a temporal quest references the old rule version.

A declared migration must preserve, translate, or explicitly reject compatibility before play. Removal cannot turn unknown IDs into empty defaults or regenerate an affinity from the current seed. Backups must be recoverable, and package order must be pinned in replay evidence. Unsupported configurations can be labeled without removing required supported-profile coverage.

### VT-11 — The world's explanation leaks a secret

A merchant correctly raises prices after an embargo. The player's character knows only about shortages, while an administrator knows the covert pact that caused them. A companion's familiar sees one messenger.

Public dialogue, UI explanations, deck eligibility feedback, network snapshots, and animation cues must respect each observer's knowledge. A debug inspector can reveal full causes under administrative access. Introspection progression can reveal more analysis, but essential accessibility feedback must remain available from the start.

### VT-12 — Parallel agents finish incompatible work

An engine agent changes the action schema while a content agent authors spells and an artist exports animations against the previous schema. A virtual playtester reports success using privileged commands, and the builder attempts to approve its own result.

The central task needs versioned contracts, explicit file ownership, integration responsibility, and an independent critic disposition. Playtest harness privilege must be declared; functional traversal should use ordinary supported commands. Agent evaluations cannot supply owner art approval or replace human fun/accessibility judgment. Preserve original source and archived specification versions across the integration.

### VT-13 — Copied ore before the first pickaxe strike

Fork a checkpoint before mining a rare deposit and harvesting a renewable grove. Extract in both branches under different command IDs; split, blend, smelt, craft, salvage and trade the products. Attempt to export both, return an approved shipment, export again and crash during transfer acknowledgement.

The same source quantity cannot obtain multiple export authorities. Local branch use may diverge under the declared policy, but transformation and byproducts cannot launder claims. Return moves custody. Unsupported provenance needs a recoverable refusal rather than deletion of an unrelated destination object. The proof belongs with early transfer primitives and later full-process regressions.

### VT-14 — Bedtime with a separated, awake friend

One player sleeps in a ship berth, an NPC keeps watch, another cooks, and a separated player refuses time acceleration. Repeat with no available controllable companion, an injured familiar, an approaching storm and a connected controller who becomes unavailable.

No actor receives simultaneous full sleep and work credit. Refusal prevents global time skipping; restart and cancelled rest preserve earned partial recovery without granting reset bonuses. A watch detects only what it can perceive. An isolated sleeping player may have little to do, so measure actual idle minutes, recovery choices, camp frequency and interruption burden. The approximate 40-minute full day/night cycle is a starting hypothesis, not evidence that daily rest is enjoyable. Normal-time rest, shorter respite, deferral, delegated activity and tuning must be evaluated without inventing a private instant-healing clock.

### VT-15 — The last pillar spans two loaded regions

Two players excavate opposite faces of one load-bearing deposit beneath an inhabited fortress. A tunneling ship arrives from a third region; the ceiling, occupied upper floor and part of a stairwell are outside the currently rendered layer. One miner repeats a command after disconnect.

Extraction must arbitrate one material balance and terrain revision, produce conserved spoil/ore, consume appropriate tool/work costs and invalidate dependent support, clearance, access and hazards. A supported collapse cannot be suppressed by a chunk boundary or camera cutaway. Half-committed geometry must not permit passage before the resulting support consequences become authoritative. Repeat with a held encounter on the supported floor and an interruption during durable support-work continuation. Degraded throughput is acceptable under its declared fallback; deleting occupants or restoring the deposit is not.

### VT-16 — A spring floods a shaft nobody is viewing

Mining joins a hot-spring reservoir to a shaft, a lower mine and an unloaded fortress cellar. A door closes while another player drains a connected channel. The affected water also carries declared heat or contamination; a region unloads before propagation completes.

Declare where fluid volume, flow, barriers and thermal effects are owned. An unloaded boundary is neither an infinite sink nor an automatic wall. Propagation must preserve deterministic order, boundary state and ongoing work across restart, with supported sources/sinks accounted explicitly. Prevent traversal or hydraulic influence based on a stale door/support revision. Actor danger must obey encounter admission, and visible cutaways must not reveal secret rooms through unrestricted state projections.

### VT-17 — Same coordinates, different floor and plane

A player stands above a hidden cellar, an ally traverses the stairwell and an NPC crosses a portal to a differently connected interior. A thrown object, a hovering familiar and a multi-level vehicle overlap their screen coordinates. The lower floor is then rebuilt while the camera switches visible layers.

Spatial identity must distinguish volume, height, traversal connection, world/branch/plane and attachment frame. Rendering overlap cannot become collision, target selection or shared knowledge. Stairs, slopes, shafts and portals need legal dimensions, access, support and transitions; an ordinary floor transition must not accidentally fork history. Save/reload and negative/far coordinates must preserve the same connectivity. Test normal mouse/controller targeting and navigation as well as privileged topology diagnostics.

### VT-18 — Death during a wedding and a royal delivery

A married monarch owns a workshop, holds a faction office, owes a debt, has reserved materials for a repair and is carrying borrowed regalia. The monarch dies offscreen as a wedding, succession adventurelet and trade handover are eligible. Two heirs and a pretender claim power; distant relatives have not received the news.

Commit death exactly once, revoke live action/controller authority and reconcile jobs and custody without erasing the person's identity, relationships or deeds. A corpse's physical inventory, legal estate, debt claims and office vacancy are different state. Heirs inherit only what the world's declared rules transfer; skills, secret knowledge, personal trust and borrowed goods do not copy automatically. Claimants may dispute power while actual office permissions remain well-defined. News, grief and political response follow scoped evidence. Decks cannot keep the dead person acting because a scene needs them.

### VT-19 — A child is born while history changes resolution

A household expects a child while one parent is held in a local encounter and another travels. A marriage changes, a caretaker dies, an institution recognizes the child's status, and the region switches between detailed and coarse simulation around the due interval. Save immediately before and after the birth and visit an earlier branch later.

The child receives one durable identity and recorded lineage/variation; loading and branch traversal must not reroll ancestry or instantiate an extra child. Gestation, maturation, aging and care must use declared clocks rather than an ambiguous combination of world date and held exposure. Distinguish parentage, legal/household relationship and culture; these can change through different causes. Guardianship, provisioning, affinity and future learning are actual commitments, not inherited full adult capability. A death/birth crossing within one batched interval must be processed in causal order.

### VT-20 — The restored ruler and the weathered enchanted gate

An heir inherits a mine and repairs an old enchanted floodgate using its alloy stock. A storm and magical blast damage that gate; weather exposure also accumulates across the same interval. The former ruler is later restored through a supported resurrection effect while the heir has already sold part of the estate. Players use tea and a hot spring during recovery.

Resurrection preserves the correct individual under explicit identity rules without reversing recorded transactions, duplicating property or automatically removing a valid successor. Legal restoration or conflict needs actual actions. Condition tracks distinguish age, prior repairs, accrued exposure and discrete damage events; coarse catch-up must not apply a storm event twice or erase patina with healing. Restorative sources use typed effects, duration, eligibility and costs: entering a spring, sipping tea or reloading cannot repeatedly grant a full reset or stack an unbounded enchantment. Crafting and salvage must preserve transformed-material provenance.

### VT-21 — Expertise without an omniscient spreadsheet

A novice repairs the same damaged gate that an experienced smith inspects. The alloy contains an undiscovered magical flaw; a covert faction has restricted replacement materials. Later, the novice learns inspection and the expert chooses the concise interface during a storm.

Both players need understandable legal actions, known costs, observed danger and accessible controls. Expertise can reveal acquired composition, causal rules, useful estimates and learned options, but exact odds or computed tooltips must not incorporate secret authoritative inputs that the character has not discovered. Mark unknown inputs and uncertainty rather than rendering misleading certainty. The expert must retain a usable simple view; increasing skill cannot force manual operation of every background subsystem. Test ordinary players performing a coherent task without administrative telemetry, then verify deeper inspection teaches the actual supported game rules.

### VT-22 — The dreamer remembers the wrong culprit

A grieving NPC dreams that a rival killed their parent, although the belief is false. A skilled player enters that dream while the sleeper's real body rests aboard a moving ship. The dreamer desires reconciliation, fears abandonment and owns an heirloom that appears in the dream. An outside attacker then injures the body as another player holds a decision inside the dream; disconnect and retry occur during the return.

Dream imagery must retain its subjective provenance. An invented culprit, imagined location or dream copy of the heirloom cannot become a historical fact, discovered real secret or exportable physical item without a separately legal effect. The projection/tether must identify one actor/body authority and preserve physical care, custody and commitments. Specify which body/avatar can act, how clocks couple, and what injury, awakening, death and interrupted return do; two manifestations cannot produce duplicate labor or rewards. NPC motives must affect actual feasible decisions and relationship responses after waking, within acquired evidence, rather than merely changing a generated description.

### VT-23 — Two merchants accept the last profitable quote

Two players sell to a shop using the same displayed quote and remaining purse. Meanwhile an NPC caravan buys its stock, a bridge closes and an escort contract is interrupted by a guard's death. A player returns to the previously visited town and expects yesterday's favorable price.

Revalidate a quote's quantity, revision, funds, expiry and permissions at the atomic exchange; explain changed terms rather than silently executing a worse price. Costs and receipts must prevent simultaneous overspend or duplication. Distant price knowledge needs an observation time and uncertainty. The caravan owns real goods, provisions, mounts, guards and funded rewards, which survive route changes and restart. An early player must be able to understand and complete a modest trade/escort loop without owning a city or manually calculating the whole economy. Measure net returns after travel, care and losses; a valid merchant career should not require a known market-reset exploit.

### VT-24 — A fortress proclaims itself a kingdom

A player builds a castle, crypt, mine, workshop and town beneath and around land already claimed by another polity. Residents have divided loyalties. The founder declares independence, issues a tax order and seeks recognition while an existing garrison disputes jurisdiction.

Changing a building's label cannot automatically grant sovereignty, confiscate inhabitants or erase old claims. Institutions, jurisdiction, possession, membership, consent/coercive means, tax obligations and recognition need explicit world records. Established powers may recognize, negotiate, ignore or oppose the claim through actual actors/resources. Strategic views and local encounters must describe the same occupied structures, roads and residents, with no separate nation-mode inventory or manufactured army.

### VT-25 — A lawful dissenter, a popular villain and a divine pact

An orderly official refuses an unjust ruler, a generous merchant is falsely blamed for theft, and a popular warlord secretly harms a neighboring village. One belongs to a species associated historically with a particular cosmic affinity; another breaks a divine oath.

Personal values, moral/order orientation, social reputation, legal allegiance and metaphysical bindings must be distinct. A faction's approval does not mechanically prove goodness; rejecting one sovereign does not automatically mean chaos; ancestry does not erase individual agency. Supported divine consequences follow the pact/rule and acquired evidence as appropriate. NPC decisions must remain traceable to motives and legal capabilities, including conflicted or exceptional individuals, instead of reducing behavior to one alignment switch.

### VT-26 — One historical instant, several committed actions

While the world calendar is held, two players transfer an artifact, issue an institutional order and learn a clue in separately committed transactions. Request the same historical millisecond twice while another such transaction commits; crash during preparation, resume, and create a writable child. Repeat at a partial exposure interval and at an initially unrealized ancient address, querying neighboring dates in opposite orders. Change a structure A→B→A between checkpoints and visit the intermediate state; a net-change archive cannot erase B simply because the latest structure again equals A.

The preparation handle must identify a stable source branch/revision and an explicit ordering boundary for all events at the requested instant. Recovery cannot silently move that boundary or create a child from a later live head. Include exactly the intended same-time transactions and preserve fractional exposure, original identities and constrained ancient facts. A different branch/revision may have a different legitimate state at the same displayed date; the interface and archive must not pretend dates alone identify all state. Storage/preparation measurements remain required, and generated ancient detail must retain its declared fidelity rather than claim nonexistent millisecond simulation.

After intervention, the child's factual prefix remains fixed but its later outcomes can diverge. Facts from the parent's post-fork future must not force the child back onto that future or instantiate a child-only ancestor to reconcile the contradiction.

### VT-27 — A destroyed clue beside a moving floodgate

A witness wrongly identifies a murderer. Rain damages a written clue; the suspect moves the actual weapon through a flooded, multi-level passage. Players use acting, writing, tracking, magic and a timed cooperative floodgate mechanism to investigate. One enters Wait at the mechanism while another visits a subjective dream and a divergent historical branch. An autonomous NPC later resolves part of the incident first.

Puzzle motion, water, bodily hazards and interaction costs must share the authority and encounter coupling rules. Every clue has an incident-derived cause and knowledge/provenance; skill inspection must not reveal the hidden solution or label false testimony with administrator truth. Alternative routes must be real and capability-feasible where the case remains solvable. Destroying all justified evidence may produce an honest unresolved case or changed continuation; a director cannot fabricate a replacement proof to preserve an authored ending. Testimony from another branch or dream is evidence of that experience, not automatic proof of the present branch's crime. A correct private deduction, public proof, lawful accusation and faction acceptance are distinct supported outcomes.

### VT-28 — Every doorway is easy, but the journey is impossible

A novice reaches a large town with resident households, genuine visitors, traveling merchants, sewers and a shadow-plane chapel. Each advertised obstacle has a modest individual difficulty, but the only teaching NPC leaves before the novice can earn the required fare; the alternative route requires two simultaneous operators, and its key material is reserved by a caravan. A storm changes the timetable and a polity disputes access.

Progression validation must evaluate complete stateful routes, including learned knowledge, time windows, consumable stock, party capability, care/travel costs and competing reservations. Averaging difficulty values or counting connected rooms cannot prove a viable route. A supported starting state needs an understandable advancement path and recovery/continuation after changes, while optional hard routes and deliberate dead ends remain allowed. Population density must represent persistent residents and funded travel without requiring every person to plan each frame. Native navigation, meaningful choices and causal explanations must be evaluated on the realized settlement, including its vertical and planar connections.

### VT-29 — A lasting injury changes the route home

A novice loses use of a hand while holding a borrowed tool; a mount suffers a locomotion injury and an NPC craftsperson loses a sense. Equipment, passengers and unfinished jobs must be reconciled. One actor then enters a dream, rests, changes realm and receives a supported restoration spell while the borrowed tool has already been returned to its owner.

Persist injury identity, function, treatment and causal history; recalculate actual capabilities and lawful attachments without duplicating or deleting equipment. Ordinary rest cannot silently regrow missing anatomy, and a valid restoration must deliver its declared recovery. A harmless cosmetic scar must not hide a functional penalty. Test whether an early player can understand adaptation and reach recovery without already possessing the lost capability. Measure serious-loss frequency across whole campaigns and large NPC populations: a small per-hit probability does not alone prove that lasting losses feel rare. Native motion and tool handling need human review, including restrained readable scar presentation.

## Integrated scope and evidence inspection

Inspected the actual [GDD](GDD.md), [technical design](TECHNICAL_DESIGN.md), [decision register](DECISIONS.md), [implementation plan](IMPLEMENTATION_PLAN.md), [requirements](REQUIREMENTS.json), [coverage](COVERAGE.json), [validation protocol](VALIDATION.md), [presentation contracts](PRESENTATION_CONTRACTS.md), [synergy atlas](CROSS_SYSTEM_SYNERGIES.md), [pain-point resolutions](PAIN_POINTS_AND_RESOLUTIONS.md), baseline README and the independently authored integration findings. Context included v0.6 architecture/requirements, the v0.5 adopted combat-transition contract, content-migration and expressive-animation plans, preceding frontier/camp research, all seven expanded dossiers and their [research coverage](../research-v0_8/RESEARCH_COVERAGE.json). This reviewer authored the two systemic/society research papers and does not count self-review of those artifacts as independent research validation; the parent and other reviewers inspect those inputs.

The final independent mechanical check found **150 unique requirements and 150 unique coverage rows**, identical ID sets, exact current requirement-text agreement, and matching SHA-256 hashes of each requirement text. Research coverage also contains 150 matching hashes and 25 valid topic groups with consistent membership and existing artifact links. The decision register has 45 decisions and seven qualification gates; the plan has 15 phases. Every requirement coverage status remains planned and every runtime evidence-path list is empty. RQ-079 reflects selected desktop platforms/player count while preserving actual toolchain/hardware qualification. The later rows carry rest, mutable terrain, lifecycle institutions, dreams, trade, nation-building, knowledge-scoped presentation, arbitrary history, nonlinear places, action/deduction puzzles, professions, cities, composite/time-based progression, shared-system combinations and lasting injury. RQ-054 preserves arbitrary historical access; RQ-145 explicitly permits truthful cold cases after evidence becomes unavailable. Presence in a register is scope evidence, not implemented completeness.

The expanded research retains 53 named research cases; the integration atlas adds 16 SYN situations, and this review retains 29 VT walkthroughs. These are distinct assertions that should share real fixtures where appropriate. Neither their count nor the document hash checks is an executed gameplay result. Original OpenFantasy/RogueFiction check results remain separately attributed to the earlier review and its retained diagnostic sources; this critic did not rerun them or treat them as Beta verification. Source preservation and installed-skill integrity are mechanical provenance duties, distinct from playable incorporation or measured ecosystem benefit.

The central [mechanical integrity record](SPEC_VALIDATION.json) reports seven source-register hashes, 162 preserved exact source records, 60 archived files, 201 original source files including ZIP payloads, and 90 current ecosystem targets. This critic inspected the report's scope and limitations; those preservation counts are attributed to its checks, not a claim of independently rerunning every archive/installation comparison. Its external-link inventory also is not a fresh content verification of every website. The report must be refreshed after both critics finish editing so its document hashes describe the delivered revision.

The production plan provides P00–P14, closes required non-mobile domains before desktop release, and retains Android/iOS as a required later production phase using the same core. P11 reconciles all finite source and generated-family migration denominators; P12 requires actual representative long campaigns and ordinary careers rather than extrapolating a short deterministic loop. Art, audio, interface, localization and accessibility are continuous lanes. Real NPC cognition begins in P02 and expands through household/institutional/world work; it is not a late decorative feature.

The full body of scope remains recognizable: inherited content and classes, deep magic/effect research, production graphs, weather and material age, species/culture/individual divergence, deities, social and institutional relationships, alliances and secrecy, careers and construction, all named transport modes, cared-for mounts/pets/familiars, strategic crises and alternate narratives, temporal/planar play, supported creation/packages and optional AI. The expanded contracts add actual volumetric terrain and conserved mining, support/fluid seams, family/care/estate/office transitions, subjective dream tethers, funded trade, contested sovereignty, skills with concrete actions, actual incident-derived mysteries, temporal/capability progression and persistent anatomy. The design avoids using declarations of endless possibility as permission to omit concrete verbs, costs and outcomes. New powers require supported rules and production coverage.

The most valuable Dwarf Fortress lesson is causal combination across persistent people, resources, institutions and events. Beta applies that through shared legal capabilities, knowledge, commitments and event-linked consequences. It does not assume that depth requires every NPC to deliberate every frame. Caves of Qud and storylet lessons support recombination with authored structure; here encounter decks and adventurelets select and interpret opportunities while world authority supplies actual actors and resources. Grand narratives can intersect or fail through world action instead of controlling history from an omnipotent director.

VALIDATION correctly separates specification walkthroughs, executable core/integration checks, real agent-operated builds and human acceptance. The central task owns versioned contracts, integration and durable evidence; bounded subagents own distinct work. Creators do not independently approve their own changes. Codex and Claude remain interchangeable task owners, with Antigravity reserved by default for maintenance. Dedicated artists have native preview and editable-asset obligations. Tool count and nominal MCP coverage are not treated as evidence of usefulness. No agent can accept the owner's art or substitute its judgment for human fun evidence.

## Remaining evidence gates

There is no honest document-only route to eliminating all doubt. The following are **open evidence gates**, not unresolved ownership rules or waived requirements:

| Gate | Owner and stopping point | Required evidence |
|---|---|---|
| Core language, exact Godot/.NET, desktop and later mobile compatibility | Engine/export/performance owners; P00 candidate pins, P01 desktop execution, P01–P03 language qualification before broad P04, P14 mobile | Retained LANG-01–04 workload/allocation/durability/replay evidence and actual exports; a measured native alternative must pass ABI, ownership, recovery and platform parity before selection |
| Determinism, Wait and temporal durability | Core/host owners plus independent integrity critic; P01/P03 before format/bulk hardening | Replays, crashes/retries, whole dependency transfers, resource claims, long holds/admissions, shared appointments and clock transitions |
| Native art and production combinatorics | Dedicated artists/content owners plus human acceptance; P02 before bulk, P11 completeness | Editable/importable family kits and real motion/attachments; complete supported intersections and legacy representation |
| Population, frontier and history capacity | Simulation/performance owners; baseline P00–P03, travel P06, expanded city P07/P08, chronology P10, endurance P12 | Cold four-player workloads, real resident work and visitors, backlog age, sparse relationships, archive growth and recoverable overload; no dropped identities, erased dates or machine-dependent rules. Early subset capacity does not prove the full production envelope |
| Rest, living NPCs, meaningful frontier and ordinary careers | Design/content owners and real virtual/human playtesters; early slices through P12 | Observed causal recognition, pacing, idle/chore burden, multi-seed variation, independent outcomes and sustained campaign quality |
| Complete source incorporation and operating release | Migration/content/release owners plus separate critics; P11–P14 | Source accounting, valid-route and changed-world semantics, shipping catalogs, updates/recovery, accepted assets and platform-specific operation |

The completed pain-point review states actual defaults and their costs: local stasis in independent Wait, constructive supported starts with truthful later failure, declared generated-history fidelity, visible preparation/storage costs, rare functional injuries with reliable defined restoration, and selective ecosystem use. Its alternatives are not competing hidden authorities. The 40-minute cycle, campaign duration, city profile and art ambitions remain qualification targets with concrete ways to revise failures.

Proceed with the retained foundational phases of the selected Beta architecture. A “final spec” label cannot bypass the named experiments: failure must revise the specific rule, workload or presentation contract with save/content consequences recorded. Required breadth remains the destination, and expansion stops at a failed dependency gate until that failure is resolved. This verdict approves the coherence and traceability of the inspected specification, not zero future redesign or production readiness.

## Inspected revision hashes

SHA-256 identifies the actual design and research inputs inspected for this final disposition. Later semantic edits require review of affected contracts; this verdict does not automatically approve future revisions. Research hashes preserve provenance, including the reviewer-authored society paper; they do not represent independent approval of that paper. The central integrity report and the other critic are intentionally excluded because their final records are written after this review.

| Artifact | SHA-256 |
|---|---|
| [README.md](README.md) | `112defd2f0621c45596b1fef0251190c9d5d098b3651c381c56b5f99f3a387a0` |
| [GDD.md](GDD.md) | `90bbcb852f2583f0618d713cb4acc6ddf19031222b4c29b5d976e1cde3afa114` |
| [TECHNICAL_DESIGN.md](TECHNICAL_DESIGN.md) | `58aa82f9925055c48c58b50a8f89a7207d47af2cf116b283d93211b985eb4315` |
| [DECISIONS.md](DECISIONS.md) | `0998292cd8417593fbe8ff78d396abf7279e7b3d5e49040239ec5c38640d47aa` |
| [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) | `3b5943a9329073220cc02aa1ec318097687988a10e76ccd321f6e908ab95cb16` |
| [REQUIREMENTS.json](REQUIREMENTS.json) | `946dd5e5f6f07933d743450ad3b7f7f464298349e258185d889a5c8021d63b36` |
| [COVERAGE.json](COVERAGE.json) | `c0aa7dde5c3dd979e7f9f460fc03e82a13be4f72faa53195c116cdaa19d1f151` |
| [VALIDATION.md](VALIDATION.md) | `4afd678d462e4da3f6386c93654482550bf9194c4fa50dc725526d9435ed5bb2` |
| [PRESENTATION_CONTRACTS.md](PRESENTATION_CONTRACTS.md) | `acaca28f5e578882cd4e7895a6ce84d9a013f243239b890f0472db598bc4e38d` |
| [CROSS_SYSTEM_SYNERGIES.md](CROSS_SYSTEM_SYNERGIES.md) | `26205d7577429cb866cd8b9456a93d60a16344ae36e7de9fc6f4e28dbf251b42` |
| [PAIN_POINTS_AND_RESOLUTIONS.md](PAIN_POINTS_AND_RESOLUTIONS.md) | `b7e27388d1ff1ada16a4bc399eeba8ec2446562ea7931172e61d0cac7ea374f1` |
| [README.md](../research-v0_8/README.md) | `94aaf6804d2bf81b353ffe8337453acba2f463e335c26a5dafa0394356abd0b1` |
| [RESEARCH_COVERAGE.json](../research-v0_8/RESEARCH_COVERAGE.json) | `5ce5ce7804543e89e8bfae6b9ced94254bde9b8c7c135824f146423885314e31` |
| [terrain-runtime-and-worlds.md](../research-v0_8/terrain-runtime-and-worlds.md) | `c9ccac09066db28752bf0d5aadf83987cfaa75a552ebd005df34ae6d53be43ff` |
| [society-lifecycles-and-emergent-stories.md](../research-v0_8/society-lifecycles-and-emergent-stories.md) | `8155452ba7c1742e4d9e0497ad25e96064ad051146bf820c3cd808b54f020322` |
| [presentation-production-and-player-experience.md](../research-v0_8/presentation-production-and-player-experience.md) | `389f8a3e07c829ed5168e851ea59e061345bf8f7f36d3ef1ec3e2b4725c4aa11` |
| [skills-crafting-magic-and-dreams.md](../research-v0_8/skills-crafting-magic-and-dreams.md) | `8a0b09a1a603395298ff672e38aad575ffea407b6365f60a42de9949fb3537c6` |
| [places-puzzles-and-progression.md](../research-v0_8/places-puzzles-and-progression.md) | `4d70b923886c9e6fbabbaeca86fc5afb499e1bd03cbb1a79af11648ff3d5d83d` |
| [csharp-rust-core-reassessment.md](../research-v0_8/csharp-rust-core-reassessment.md) | `046240f8370ad83830975d3c2b4195855af39bf6df38263f9e8e95e13379d00b` |
| [ecosystem-packages-and-production.md](../research-v0_8/ecosystem-packages-and-production.md) | `6f3b609bb30f5565662f9fd4dc1c192249241c4f3eb394122acaeffaa0d78173` |
