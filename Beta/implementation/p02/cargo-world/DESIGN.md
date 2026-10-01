# Cargo world design (bounded)

Source revision `bf0e5a3`. Inspected APIs, not assumptions. This records owners, transforms, route
decisions and the concrete composition dependency found before implementation.

## Canonical owners (unchanged)

| Fact | Owner |
|---|---|
| Retained source bytes and payloads | `content/legacy/aldermere-first-loop/source-support/data/{npcs,items,world}.json`, read through `FarmContentFiles` with exact file digests |
| Merchant/good/place source binding (new, read-only) | `Content.Cargo.CargoCircuitSources` — typed records, payload hashes and JSON pointers; no copy, no edit |
| Corridor geometry (new authored) | `Content.Cargo.CargoCorridor` — region rectangles and leg waypoints; terrain cells come from the basis region's own `StratifiedTerrainGenerator` seed/rules |
| Market rules for the two counters (new authored) | `Content.Cargo.CargoCircuitPolicy` citing `MarketCircuitPolicies.Rations` (source price 25, items digest) |
| Stock, lots, provenance | Inventory/production (unchanged) |
| Money and finite supply | `ProgressionSnapshot.Currency` (unchanged) |
| Quotes, knowledge, disclosure | `WorldPaidExchangeBook` / `WorldMarketBook` (unchanged) |
| Leg progress during travel (new, transient Host advice) | `Simulation.Cargo.CargoLegCoordinator` — never persisted; recomputed from actual presence every cutoff |

## Source transforms (explicit)

- Marla `npcs.json` id `marla`, tile (96,350); Kegga id `kegga`, tile (347,140). Tile→cell 1:1 and
  tile centre→feet (`FarmAdmissionDefinition.Feet`, Z 256) are the farm's existing transforms.
- Place identities are the source regions containing those tiles: `brasshollow` (94,339 33×27) and
  `gearford` (344,127 33×27). Containment is checked, not assumed.
- Shared good: `items.json` id `rations`, price 25 (unchanged), checked against the market rule's
  cited `MarketSourcePrice` and digest. Appearance, dialogue, shop lists, quests (`kegga_timber`) and
  wander radius are retained as source facts with canonical payload hashes; none is relabelled.
- Not yet admitted as runtime meaning: dialogue utterance, Kegga's quest, the other shop goods.

## Route decision

The source tiles are 251 + 210 = 461 cardinal cells apart. The authored corridor is an L of 14
non-overlapping 32×32×16 regions (≤ the fixed 16): the existing farm region (96,320) plus seven
eastward along y 320–351 and six southward along x 320–351 to y 128. Every new region is generated
by the basis region's own seed/rules pin, so terrain is flat ordinary support. Legs join adjacent
authored waypoints at most 8 cells apart, so each leg is planned with the unchanged 256-expansion
ground search; the 16-region, search and per-step movement bounds are unchanged. Missing coverage
is reported as a status, never as proof of no route. Movement uses only the 32-subunit cardinal
sample of `WorldMovementInput`; nothing is relocated.

`CargoLegCoordinator` recomputes the current leg from the actor's actual feet each cutoff. Any
non-reachable status (blocked actor/terrain, unavailable cells, budget) stops the itinerary with that
status and issues no movement; a later call revalidates against the new state.

## Admission decision

First finding (kept for the record): `BreachFarmBuilder` (`BreachFarmBuilder.cs:42`) takes
`farm.Space.Regions.Single()`, so corridor regions cannot enter through the only pre-custody hook
(`EncounterJourneyAdmission.Create(..., composeFreshFarm)`). I first recorded this as blocking.

Resolution: `CargoGenesisExtension` extends the *finished* unplayed ordinary-care root (profile 26,
revision 0, no coordination) through the public `WorldSnapshot` constructor, then the unchanged
`WorldPaidExchangeAdmission.CreateFresh` and `WorldMarketAdmission.CreateFresh` run on it. Each owner
receives the merchants through its own record (space, residents, custody/controls/policies/sources,
progression + currency, gathering slots, construction exits, lifecycle debts and non-combatant
scenario entries). The resource history uses the public constructor equivalent of the fresh factory.
The water capacity pins the counted admission (custody-only `PlanReconcile` refuses added actors and
sources), so the retained dry domain is re-prepared unchanged against the extended inventory and the
placed cells are rebound (`RebindTerrain`) to the extended regions. All root validators run; no
validator is bypassed and no history exists to forge. The farm admission and every existing builder
stay byte-identical. Fresh selection only — no lived-save migration.

## Risks

- Throughput: a full round trip is ≥ 7,376 one-sample cutoffs (922 cells × 8). Commit cost with 15
  regions must be measured, not avoided by shortening (measured afterwards: ≈19 ms per managed cutoff,
  see RESULT.md; not a production latency claim).
- Liquid topology/excavation validators scan all regions; the water owners must be re-prepared and
  rebound rather than reconciled (custody-only reconciliation refuses added actors and sources).
- Ration provenance: farm rations are produced by `pack-rations`. Merchant opening stock must be a
  declared, typed authored source of the same definition, never relabelled berries or produced lots.
- Threat/escort: the admitted wolves stand on the farm; no encounter is admitted on the corridor.

## Repair corrections after independent review (added 2026-10-01; text above is the original record)

See `REPAIR_RESULT.md` for per-finding evidence. Design-level corrections:

- **Retained places.** The corridor overlays retained `world.json` places in the same tile plane. Owner:
  `Content.Cargo.CargoCorridorOverlays` — a declared, versioned overlay set (v1) checked exactly against the
  computed intersections of every retained place with the inherited farm region, added regions and lane
  segments. Three inherited (brasshollow, tavern_brasshollow, farm_verdance_small), seven added (gearford,
  tavern_gearford, castle_aldermere, standing_stones, bandit_camp, farm_sylvas_medium, port_gearford). The
  exact threat dependency is the lane crossing `bandit_camp` at x=346, y∈[305,317). The overlay version is
  part of the admission identity.
- **Encounter claim corrected.** "Encounter content unchanged" was wrong: side-zero merchants change both wolf
  scenario versions, and an entered encounter's tactical scope is world-wide. The wolves are player-initiated
  farm encounters, not threats to cargo in transit.
- **"No validator is bypassed" narrowed.** Root, coordinated, paid and market validators run on the extended
  root; farm composition-time checks (`ConstructionFarmProfileValidation`, `QuestFarmWorldValidation`) are
  not re-run on it.
- **Water.** The closed budget is read from the retained capacity (`Closed`), and re-preparation is accepted
  only when it reproduces the retained capacity and reservoir hashes (fresh dry genesis); otherwise refused.
- **Leg coordinator.** A stop is latched to a value situation (walker, bodies within 16 cells, terrain
  revisions, structures, movement admission) instead of being retried on the next call.
- **Admission identity.** Canonical `OFC` v1 frames with explicit signed 64-bit numbers replace the text
  hash (finding 9); provision versions are framed the same way (`provision.v2`).
- **Opening stock (finding 8).** No new persisted fact: `CargoWorldAdmission.OpeningStock` recomputes and
  checks the named role/source/capacity/recipe/provision binding over existing owners.
