# OpenFantasy architecture

OpenFantasy is a persistent, seeded world simulation presented through a Godot C# game client.
A portable C# domain core owns game rules. An in-process host serializes and durably commits
decisions. Presentation translates permitted world observations into controls, images and sound.
Players and NPCs participate through the same rules; changing a controller does not create a
different kind of person.

## Reading the reference

| Subject | Questions answered |
| --- | --- |
| [Runtime and authority](runtime-and-authority.md) | Which module owns state? How does an action become durable? Where do adapters belong? |
| [World composition](world-composition.md) | How do meaningful domain actions compose typed facets, relations, active/passive processes and atomic cross-system changes? |
| [Identity and determinism](identity-and-determinism.md) | Which bytes, numbers, random streams and orderings define the same world? |
| [Persistence and history](persistence-and-history.md) | What is stored, replayed, migrated and addressed through history? |
| [Hosted player connections](hosted-player-connections.md) | How do up to four authenticated player ports share one clock, input cutoff, authority and NPC scheduler while retaining separate readiness, views and queries? |
| [Host timing diagnostics](host-timing-diagnostics.md) | How are startup, input sampling, commit, observation and cleanup delays measured without changing authority or deadlines? |
| [Durable character control](controller-lifecycle.md) | How do recorded disconnect, allowance expiry and safe return of control preserve character ownership, gameplay obligations and exact replay? |
| [Controller allowance recovery](controller-recovery.md) | How do explicit recovery admission, durable measured lower bounds and fenced host segments preserve allowance across restart without offline time? |
| [Co-player permission](control-permissions.md) | How do named offers, exact acceptance and revocation preserve ownership while separating consent from effective control? |
| [Effective co-player control](effective-control.md) | How do temporary assignments authorize real commands while preserving true submitter identity, accepted work and historical outcomes? |
| [Connected character control](selected-player-protocol.md) | How do consent, selection, scoped observations and deliberate Resume reach authenticated clients and native controls? |
| [Fresh player rosters](player-rosters.md) | How does fresh cargo admission create one to four real human characters with owned facets, finite starter tools and a complete cooperative encounter catalog? |
| [Player message transport](player-message-transport.md) | How do bounded local and WebSocket envelopes preserve ordering, ownership and complete-message boundaries? |
| [Player connection admission](player-admission.md) | How do scoped host-issued credentials, compatibility pins and bounded handshakes select one actual player port? |
| [Player gameplay routing](player-command-routing.md) | How do readiness, leased movement, canonical actions and durable receipts cross authenticated channels without blocking the host? |
| [Player HTTP/TLS hosting](player-http-host.md) | How does a bounded native-client TLS listener join existing saved player control, protect invitations and shut down without abandoning world work? |
| [Player protocol client](player-client.md) | How does a participant validate its connection, own bounded input and pending requests, and recover a complete visible replica? |
| [Player queries](player-queries.md) | How do nonbinding merchant inquiries and participant-only historical inspections cross authenticated channels with bounded work and complete terms? |
| [Player observation values](player-observation-views.md) | How do typed immutable views preserve geometry, participant terms and prepared actions across a bounded canonical encoding? |
| [Player observation streams](player-observation-streams.md) | How do visible revisions, bounded delta publication and atomic full recovery synchronize an authenticated player's view? |
| [Time and encounters](time-and-encounters.md) | How do calendar, exposure, Wait, rest and host readiness interact? |
| [Encounter timing](encounter-timing.md) | How do portable readiness, required choices, retained debt and exposure carry preserve exact tactical time? |
| [World pacing](world-pacing.md) | How do authenticated Wait choices, tactical time and earned process intervals commit with the complete world? |
| [Pending-care decisions](care-dispositions.md) | How do cancellation, helper decline, terminal chronology, safe control return and exact scoped choices preserve care history? |
| [Encounter actions](encounter-actions.md) | How do source-backed abilities, real HP/MP, deterministic damage and incapacity share timing and durable authority? |
| [Encounter lifecycle](encounter-lifecycle.md) | How do exploration, authenticated entry, outcomes and release preserve permanent recovery debt and fractional process carry? |
| [Native encounters](native-encounters.md) | How do public combat choices, contextual controls, hit feedback and exact reopening connect to the lifecycle? |
| [Encounter retreat](encounter-retreat.md) | How does a committed retreat walk real ground sample by sample, charge readiness, break contact and end an episode without a forged outcome, and how are players, NPCs and the native wheel bound to it? |
| [Encounter outcome](encounter-outcome.md) | How does profile 35 separate resolution, each required human's acceptance and the world's zero-duration completion, keep outcome facts apart from bounded present condition, close aftermath care on acceptance, and refuse foreign pins and impossible owners at decode? |
| [Road threat](road-threat.md) | How does the retained camp's Bandit Chief threaten carried cargo on the lane: source mapping, fresh admission, host-initiated entry under its own control, warning, escape, detour and onward trade? |
| [World and geography](world-and-geography.md) | How do terrain, watersheds, streaming, planes, geological history and ecology fit together? |
| [Local worksite](local-worksite.md) | How do funded building, multilevel public geometry, contextual mining and a separate save entry connect? |
| [Local watercourse](local-watercourse.md) | How do finite flowing water, an earned divider breach, public cell geometry and the inhabited farm connect? |
| [Local excavation](excavation.md) | How does mining couple labor, source custody and terrain removal in one durable action? |
| [Excavation material continuation](excavation-material-continuation.md) | How do partial mining, shared gathering effort, earned source rights and changed terrain survive material history cuts? |
| [Constructed footing](constructed-footing.md) | How do completed floors support actors and preserve safe assisted traversal through saves? |
| [Stair navigation](stair-navigation.md) | How do ordinary steps and routes preserve real elevations and actors above/below each other? |
| [Structural loads](structural-loads.md) | How do funded members, carried goods, rated bearings and bounded failure plans conserve mass? |
| [Coupled contact assessment](structural-contact-assessment.md) | How do candidate assemblies share exact support, preserve mass and identify load-dependent neighbors? |
| [Structural policy and motion](structural-policy-and-motion.md) | How does saved load policy govern actions, carried materials and movement, and how are descent paths checked? |
| [Intact settlement](structural-settlement.md) | How does excavation atomically lower intact material and riders onto known rated ground? |
| [Coupled intact settlement](coupled-settlement.md) | How do separately backed assemblies share support and publish a bounded common descent with their riders? |
| [Event-ordered descent](event-descent.md) | How do unequal landing events retain stopped obstacles, actual mass and explicit impact-response boundaries? |
| [Consequence continuation](consequence-continuation.md) | How do accepted physical changes resume after restart and publish once without spending extra time? |
| [Physical construction](construction-physical.md) | How do member condition and placement retain completed work, material ownership and atomic persistence? |
| [Reservoir transport](reservoir-transport.md) | How do scheduled gates move actual water, preserve provenance and share time/capacity? |
| [Reservoir boundaries](reservoir-boundaries.md) | What proves floors and walls against actual terrain and completed material, including narrow gaps? |
| [Reservoir containment](reservoir-containment.md) | How do retained seals govern complete world admission, real actions and exact recovery? |
| [Terrain liquid breach](terrain-liquid-breach.md) | How does earned excavation open conserved flow while preserving dry occupants, source custody and exact history? |
| [Detailed liquid flow](liquid-cell-flow.md) | How does source-backed liquid move between cells without relaying or losing material? |
| [Liquid material continuation](liquid-material-continuation.md) | How do runoff obligations, physical capacity and detailed placement survive material history cuts without losing provenance or admitting stale plans? |
| [Transport material continuation](transport-material-continuation.md) | How do reserved deliveries preserve exact custody, stages, clocks and physical acceptance across material history cuts? |
| [Terrain material continuation](terrain-material-continuation.md) | How do allocated source rights, exact retirement and coarse/detail representation survive history cuts without regenerating mined or consumed material? |
| [Actors and society](actors-and-society.md) | How do bodies, knowledge, relationships, cultures, institutions and narratives interact? |
| [Actor resources](actor-resources.md) | How do named damage, restoration, magic costs and rewards compose with exact item consumption while preserving one HP/MP owner? |
| [World resource history](world-resource-history.md) | How do actual attacks, quest rewards and treatments prove ordered resource continuity and roll back with complete world transactions? |
| [Resource archive continuation](resource-archive-continuation.md) | How does a complete world cross resource-history segment bounds while retaining retrievable evidence and exact boundaries? |
| [Ordinary restorative use](ordinary-care.md) | How do actual self/companion doses, patient-owned permission, normal exploration time and causal history compose? |
| [General assisted recovery](general-recovery.md) | How does explicit recovery permission restore an incapacitated actor after combat without depending on the latest encounter or duplicating health, injury or custody? |
| [Care continuation](care-continuation.md) | How do cumulative care, pending aid, revoked permissions and patient chronology survive bounded history cuts? |
| [Coordinated world continuation](coordinated-world-continuation.md) | How do full-world material, care and resource windows close together while preserving exact issuance, live work and durable evidence? |
| [Aftermath care](aftermath-care.md) | How are prior permission, recovery acknowledgment, helper commitment and finite treatment composed without equating them? |
| [Materials and activities](materials-and-activities.md) | How do gathering, crafting, quests, construction, trade and travel conserve resources? |
| [Atomic spot exchange](spot-exchange.md) | How do quoted goods, explicit payment, finite balances and continued provenance settle as one candidate? |
| [Paid world exchange](world-paid-exchange.md) | How do actual people settle quoted goods and money through world time, spatial eligibility, causal history and durable cuts? |
| [World markets and learned prices](world-markets.md) | How do stock-based rules, local price memories and explicit selected-lot disclosure compose with paid exchange? |
| [Scheduled trading](scheduled-trading.md) | How do transient shopper inquiries, host merchants' own answers, participant-safe quote views, bounded host reconsideration and session arbitration with gifts and care bind markets to actual cutoffs? |
| [Native trading](native-trading.md) | How do the fresh market world, the stall panel and wheel, authored offer windows and separate inquiry, reading, review and confirmation reach the session; which quotes belong to a stall; and how do native probes drive real focus, dropdown and refusal journeys? |
| [Cargo presentation](cargo-presentation.md) | How does the native cargo world open; how do one bounded terrain observation and one projection place ground, actors, cues, camera and pointer picks; how are retained merchants composed from their source appearances; and how is a stall chosen by market identity and qualified natively? |
| [Cargo circuit](cargo-circuit.md) | How are retained merchants, places and goods bound and freshly admitted with declared finite stock and funds; which retained places does the authored corridor overlay and where is the travel threat dependency; how do inherited encounters and water owners carry over; and how does host itinerary advice walk real legs, latch a stop and resume? |
| [Consented custody](consented-custody.md) | How do two independently authenticated participants approve one exact counted-source transfer without changing inventory access policy? |
| [Consented production](consented-production.md) | How do exact material approvals and ordered custody evidence preserve raw and produced stock, independent of complete-world publication? |
| [Consented world material](consented-world.md) | How are paired material decisions, physical eligibility and custody evidence composed, and what must complete-world publication enforce? |
| [Saved material proposals](material-proposals.md) | How do separate offers, decisions and immutable receipts preserve exact material consent across time, persistence and history? |
| [Item consumption](item-consumption.md) | How do candidate material retirement and explicit production archives retain consumed rights without granting treatment or world publication? |
| [Material continuation basis](material-continuation-basis.md) | How are exact surviving material rights and direct retirement captured without granting issuance, continued spending or archive authenticity? |
| [Live material transaction evidence](live-material-evidence.md) | How are actual accepted material/work transitions observed and reconstructed from a verified journal without changing authority or save semantics? |
| [Production continuation](production-continuation.md) | How do explicit continued material windows conserve imported rights and historical identities while remaining separate from complete-world archive publication? |
| [Construction continuation](construction-continuation.md) | How do earned work, reserved installation and historical construction backing survive material boundaries without repeated work or recreated buildings? |
| [Presentation and interaction](presentation-and-interaction.md) | How do actions, menus, maps, animation, art and audio express the simulation? |
| [Content and composition](content-and-composition.md) | How are source content, procedural opportunities, progression and cross-system changes admitted? |

**Runtime bindings** identify concrete classes and their composition in the application.
**Domain contracts** identify executable value types, codecs or pure operations; they need not be
connected to a complete gameplay system. **Design contracts** state required behavior and boundaries
without asserting the existence of a corresponding API. An operation named in the specification
is not automatically a method in the runtime. Qualification belongs to the
[implementation evidence index](../../../implementation/AGENT_HANDOFF.md), not to these descriptions.

## Architectural rules

- Consequential state has one owner and one committed identity. A UI, scene director, generator,
  worker or NPC planner proposes actions; it does not independently mutate world truth.
- Definitions are immutable, versioned content. Instances retain actual possessions, damage,
  learning, obligations and history. Derived display and search caches are rebuildable.
- Seed, rules, accepted inputs and canonical ordering determine results. Loading order, frame
  rate, locale and worker completion time cannot choose authoritative outcomes.
- Calendar date, personal exposure, tactical readiness and visual animation are distinct measures.
  Spatial elevation, realm identity and historical branch are distinct address dimensions.
- Preservation includes meaning and playable reachability. A copied asset, source row or save file
  alone does not establish semantic migration.
- Extensive mechanics enter play through understandable affordances and gradual discovery.
  Presentation simplifies access without concealing essential costs or weakening simulation rules.

## Source and document authority

The [preserved technical design](../../../spec-v1_1/TECHNICAL_DESIGN.md) and its linked domain
specifications define the game design. The [interface supplement](../../../implementation/PLAYER_INTERFACE_AND_NAVIGATION.md)
refines player interaction. This reference explains their architectural contracts alongside actual
source bindings. It does not silently amend the design to match a narrower implementation.

Each subject names its source owners. Architectural changes update the affected descriptions and
links in the same coherent change as the code. Stable rationale belongs here; dated decisions,
unresolved disagreements, migration execution records, measurements and milestone history belong in
the [implementation records](../../../implementation/AGENT_HANDOFF.md). Frozen specifications remain
preserved. A discrepancy is reconciled explicitly rather than hidden by rewriting either account.
