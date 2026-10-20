# STORY-01: The unmarked ore

Content and technical design for CURRENT_ROADMAP's STORY-01, the missing-cargo investigation and field
puzzle, written at source `4c88c15b`. It consumes NPC-01's evidence and dialogue contracts and TRADE's
traveling merchant. It claims no running behavior, allocates no profile, codec or save field, and does not
reduce investigation, recruitment, relationship, ecology or P02 scope. Every person, place, good and line
below is checked against the retained packet in
[`content/legacy/aldermere-first-loop/source-support/data`](../Runtime/content/legacy/aldermere-first-loop/source-support/data).
What the incident adds is labeled **new**.

## Player experience

Kegga Toolwright ships ore from Gearford to Marla's Brasshollow General with a traveling carter. Marla
receives five chunks of the eight that left Gearford. The carter says hill brutes cracked his sledge on the
hill road and three chunks rolled away. The tavern already repeats that brutes crack sledges, so the story
sounds true. It is not: the carter buried three chunks at the edge of the Sylvas fields to sell later to
the Hooded Figure, who pays far above market for unmarked ore.

The player can notice the shortfall, count what left and what arrived, ask who saw what, walk the lane,
find disturbed ground, dig out the cache and decide what to do with it. They can also ignore it, blame the
bandits, or sell the ore to the Hooded Figure themselves. The world records what actually happened in every
case. No branch requires combat, and none requires the player to be the one who solves it.

## Source binding and new content

| Element | Retained source fact | New in this incident |
|---|---|---|
| Kegga Toolwright | `npcs/kegga`, tile (347,140) in `gearford`; sells iron tools; already bound by the cargo circuit | Ships an eight-chunk ore consignment to Marla |
| Marla | `npcs/marla`, tile (96,350) in `brasshollow`; Brasshollow General; already bound | Ordered the ore; receives and counts the delivery |
| Bardin, the carter | `npcs/visitor_brasshollow_1`, tile (114,351): "Just passing through.", "Buying trophies, selling stories.", "I've walked three continents." | Is TRADE's traveling carter; diverts and buries three chunks; tells the sledge story. Motive follows his retained line: he buys trophies and needs coin |
| Hooded Figure | `npcs/hooded_figure`, tile (102,345) in `tavern_brasshollow`: "I need ore. Unmarked. Don't ask what for."; quest `quiet_delivery`: three `ore_chunk`, 450 gil, 400 XP | The buyer Bardin intends to supply. Their own purpose stays unexplained here |
| Toma | `npcs/bartender_toma`, Rusted Tankard: "better rumors than the crier", "The hooded one in the corner pays in real gil." | Can testify that Bardin drank with the Hooded Figure before the run |
| Tavern regulars | Milo, Osric, Hark: "Don't play cards with the hooded one." | Background only; no new testimony |
| Hilda Kegkeeper | `npcs/bartender_hilda`: "Brutes cracked another sledge on the hill road. Thane's fuming." | Her line is an older rumor. She has no knowledge of this run unless someone tells her |
| Thane Bruni | `npcs/thane_bruni`, quest `brute_iron`: "Hill brutes smash our ore sledges flat." | Mentioned authority only; not required in reachable geometry |
| Oldbark, Deryn, Miren | `farm_sylvas_medium` residents on the lane; Oldbark: "Forest soil grows strange beets." | May see the carter stop and dig at dusk, only if actually present and perceiving |
| Sable | `npcs/sable`, Tankard party leader: "We take contracts, split fair" | Optional paid help for the dig, through recruitment |
| Quinn | `npcs/guard_brasshollow_0`, gate guard: "Gate's open dawn to dawn." | Can state when the carter entered town, if on duty and observing |
| Bandit Chief, camp | `monsters/33`, `regions/16`, already bound on the lane at x=346, y∈[305,317) | A red herring: the chief takes no part and holds no ore |
| Ore chunk | `items/ore_chunk`, Material, price 35 | Eight admitted chunks with canonical provenance as the consignment |
| Copper pickaxe | `items/copper_pickaxe`, Tool, price 90; sold by Marla and Kegga | Required tool for the dig |
| The lane | Cargo corridor y=349 from x=97 to x=346, then x=346 north to y=141; overlays `farm_sylvas_medium` fully at y∈[330,339) | The cache cells lie at the Sylvas field edge beside the lane |

The quiet-delivery quest's existing terms are not changed. Its retained turn-in line "The coin is real. The
rest never happened." remains the figure's response to any qualifying delivery. New utterances for
Bardin, Toma, Marla, Kegga and the Sylvas residents are new content and are versioned separately from source
dialogue. Rolf, Gadget and Briar share Bardin's retained lines; the binding uses Bardin's identity, never a
line match.

## Ground truth as committed events

The incident is a sequence of ordinary committed actions, not story flags. Each step is a real command by a
real actor under existing custody, movement, excavation and time rules.

1. Before the run, Bardin talks with the Hooded Figure in the Tankard. Toma, if present and perceiving,
   observes the meeting; its content stays private between them.
2. Kegga transfers custody of eight `ore_chunk` lots, owned by Marla, to Bardin as carrier. Ownership stays
   with Marla; custody is Bardin's under the delivery commitment. Each lot keeps its source interval.
3. Bardin walks south along x=346. At dusk at the Sylvas field edge he excavates a small pit, places three
   chunks in a buried cache and refills it. Each step spends his real effort and time. The terrain records
   the excavation and refill. Anyone present and perceiving can observe the stop, digging and refill.
4. Bardin continues to Brasshollow, enters through the gate and delivers five chunks to Marla. He tells
   Marla, and anyone else he speaks with, that brutes cracked his sledge on the hill road.
5. Bardin holds a commitment to retrieve the cache on a declared later dusk and deliver it to the Hooded
   Figure. If nobody intervenes he does so, and the figure pays him from their own real balance.

There is no sledge object in P02. "Sledge" exists only in Bardin's claim, which matches the retained rumor.
No hill brute is imported onto the corridor, so no brute leaves evidence on the lane either way.

## Evidence and knowledge

| Evidence | How a participant acquires it | What it supports | What it does not establish |
|---|---|---|---|
| Shipped count: eight | Ask Kegga, who dispatched them | Eight left Gearford | Where they went |
| Received count: five | Ask Marla, or inspect her disclosed lots | Three are unaccounted for | Who took them |
| Missing lot identities | Compare the source intervals Kegga and Marla disclose | Exactly which three lots are missing, by identity | Their current location |
| Bardin's claim | Hear it from Bardin or second-hand from Marla | A stated cause and place: the hill road | That the claim is true |
| Empty claimed site | Walk and inspect the hill road segment Bardin names | No ore lies where he says it fell | That no brute ever cracked a sledge elsewhere |
| Sylvas witness | Ask Oldbark, Deryn or Miren, if one actually observed the dusk stop | Someone with a pack stopped and dug by the east rows | Identity beyond appearance and time, unless the witness knows Bardin |
| Disturbed ground | Inspect terrain at the field edge | Recent excavation and refill at specific cells | What is buried, or by whom |
| Tavern meeting | Ask Toma, after buying from him or otherwise in conversation | Bardin and the Hooded Figure met before the run | What they agreed |
| Gate time | Ask Quinn | When Bardin entered Brasshollow | Anything about the cache |
| The cache | Dig the cells | Three chunks with the missing identities | Who buried them, without other evidence |

Each witness states only what they perceived or were told, distinguishing seeing from hearing. Marla's
belief in the sledge story is second-hand and can be stale. Bardin's private intent is never shown as a
fact; it can only be inferred or confessed. The Hooded Figure never confirms anything. Failed searches
reveal only what the searcher observed. A hypothesis is the player's; the world never marks one correct.

## Routes to an accountable answer

Any two independent pieces establish the shortfall and contradict the sledge story. Locating the cache has
two independent routes:

1. **Testimony:** a Sylvas resident who observed the dusk stop directs the player to the east rows.
2. **Field observation:** walking the lane and inspecting terrain finds the disturbed cells without any
   testimony.

Identifying Bardin also has two routes: the missing lot identities found in the cache or later in his or the
figure's custody, together with his own claim; or Toma's tavern testimony plus the gate time and the Sylvas
description. Confronting Bardin with at least two independent pieces lets him confess; with fewer he
repeats his story. A wrong accusation of the bandits or brutes finds nothing and changes nothing about the
truth.

## Field puzzle: recovering the cache

The cache is two refilled excavation cells at the Sylvas field edge. Recovering it is a physical task:

- **Tool and cost:** a pickaxe, sold by Marla and Kegga for 90 gil, which the player can afford after Wren's
  200-gil reward. Wren's joinery kit is not a pickaxe.
- **Work:** the existing excavation rule's required work per cell, paid from actual effort and time.
- **Load:** three ore chunks' calibrated mass against carry capacity. One participant may need two trips;
  two participants can split the load.
- **Timing:** Bardin's retrieval commitment falls on a declared later dusk. Delay lets him retrieve the cache
  first, which moves the evidence to his custody and then the figure's rather than erasing it.
- **Team path:** one participant digs while another watches the lane or carries; both act through ordinary
  commands, and the second participant is never required.
- **Noncombat:** no step requires a fight. Wolves near the Sylvas farm are an optional ordinary danger.
- **Accessible alternatives:** hiring Sable's party to dig through recruitment, at a real gil cost; or
  telling Marla or Kegga where the cache is, after which their people recover it later at a time cost and
  without a finder's share.

## Resolutions and consequences

| Choice | Committed result |
|---|---|
| Return the chunks to Marla | Custody returns to the owner. Marla can pay a finder's share from her own balance; no reward is invented |
| Confront Bardin with two or more pieces | He confesses and offers repayment from his real balance; if short, he offers what he actually owns. TRADE assigns later consignments to another carrier |
| Report to Marla or Kegga without the cache | They act on the testimony they received and recover the cache later, at their pace |
| Deliver three chunks to the Hooded Figure | The retained quest's terms apply: 450 gil, 400 XP. Marla stays short, and the ore stays in the figure's custody |
| Do nothing | Bardin retrieves the cache on schedule and sells it to the figure. Marla's count stays short; later conversation reflects what each person knows |
| Accuse the bandits or attack the camp | The chief holds no ore. Combat follows ordinary encounter rules; the incident's truth is unchanged |

Returning to Brasshollow later shows the result through actual state: Marla's stock, Bardin's route and
standing, the figure's custody and what residents can say.

## Design witnesses

| Witness | Requirement |
|---|---|
| Timing | Trigger on TRADE's first consignment run after the player completes Wren's opening; retrieval on a declared later dusk; all times are world calendar time |
| Costs and knowledge | Pickaxe, effort, time and carry capacity are real; no clue requires knowledge the participant has not acquired |
| Challenge rating | Level 1–3 with Wren's reward; no mandatory combat; the bandit chief's level-7 danger is avoidable |
| NPC-first resolution | Without the player, Bardin's retrieval and sale complete and the world records them. Marla and Kegga act on any testimony they receive |
| Unavailable witnesses | If no Sylvas resident observed the dig, or a witness is absent or dead, field observation still finds the cache. If Toma is unavailable, lot identities and the cache still identify Bardin |
| Dead culprit | If Bardin dies, his custody and the buried cache persist under ordinary custody and lifecycle rules; nothing is looted or deleted by script |
| Failure and recovery | A failed dig, a lost race with Bardin or a wrong accusation leaves recoverable evidence in actual custody; no state becomes unsolvable by script |
| Accessible alternatives | Paid help, reporting instead of digging, and a split team load; no rapid input is required |

## Dependencies and gaps

| Need | Present binding | Required before implementation |
|---|---|---|
| Residents and places | Cargo circuit binds Marla and Kegga; road threat binds the chief and camp | Bind Bardin, the Hooded Figure, Toma, Quinn, Sable and the Sylvas residents by identity; verify every tile lies in admitted geometry or declare a corridor extension |
| Ore consignment | Counted custody, provenance intervals, transport material continuation | Admit `ore_chunk` with calibrated mass; carrier custody separate from ownership under a delivery commitment |
| Traveling carter | Not implemented | TRADE's traveling merchant with real routes and commitments |
| Evidence, beliefs and testimony | Fixed opening utterances only | NPC-01 evidence acquisition, beliefs, memory and evidence-dependent dialogue |
| Buried cache | Excavation exists in the worksite profile; no item lot can sit in terrain | A placed storage owner or buried-lot rule, with refill, discovery by excavation and conserved custody |
| Terrain inspection | Canonical excavation history | A presentation affordance that shows recently disturbed cells only to an observer in view |
| Lot comparison | Selected-lot disclosure in markets | A participant-facing way to compare disclosed lot identities |
| Paid help | Not implemented | Recruitment and companion contribution from the P02 NPC row |
| Pickaxe purchase | Runtime counters stock only rations; other retained shop goods are facts only | Admit `copper_pickaxe` stock at Marla's or Kegga's counter with finite provisions |

## Implementation sequence and acceptance

1. After NPC-01 and TRADE land, admit the consignment and bind the residents with geometry witnesses.
2. Add the buried-lot rule and the carter's commitments: dig, place, refill, deliver, retrieve.
3. Add evidence-dependent testimony for the listed witnesses, versioned apart from source dialogue.
4. Add terrain inspection and lot comparison affordances.
5. Qualify every resolution in one saved journey with restart, absence and return, including the do-nothing
   and dead-witness contrasts.

| Contrast | Required evidence |
|---|---|
| Real incident | The diversion, burial and false claim are committed actions with receipts; no story flag sets them |
| Two routes | The cache is found once with testimony only and once with field observation only |
| Knowledge boundaries | No witness states what they did not perceive; Bardin's intent appears only by inference or confession |
| Conservation | Eight chunks remain accounted for across every branch, through custody and provenance |
| NPC-first | The do-nothing branch completes Bardin's retrieval and sale without the player |
| Recovery | Restart at each stage reproduces exact state and pending commitments |
| Player understanding | A human playtester can explain what happened and why, without a debug view |

## Documentation review

- **Plan/spec:** supplies STORY-01's first deliverable in CURRENT_ROADMAP and the P02 STORY row's design
  witnesses. No requirement, gate or phase changes. Implementation remains blocked on NPC-01 and TRADE, and
  through NPC-01 on the PLAN-STATE decision and review.
- **Reference:** no impact. [Content and composition](../Runtime/docs/architecture/content-and-composition.md)
  already states that mysteries distinguish truth, discoverable clues, observations and inferred beliefs and
  that alternative solutions need real consequences. Nothing here is implemented, so no runtime binding changes.
- **Accuracy/completeness:** every retained name, tile, line, quest term, item price and place footprint was
  read from the packet's `npcs.json`, `quests.json`, `items.json` and `world.json` and the cargo-circuit and
  road-threat references. Geometry containment is verified only for the already-bound residents and the
  Sylvas farm; the other tiles are listed as a binding requirement. The incident's culprit, motive, events and
  new utterances are new content.
- **Checks:** `python3 Beta/tools/check_documentation.py --work-record Beta/implementation/STORY_01_UNMARKED_ORE.md --files`
  with the affected pages, and `git diff --check`.
