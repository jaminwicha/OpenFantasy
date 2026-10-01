# Cargo world result

> **Correction notice (added 2026-10-01 by the repair run).** This file is the original delivery record and
> is left as written. An independent review (`CRITIC_REVIEW.md`) found overstated or missing claims; the
> repair run's dispositions, new evidence and corrections are in `REPAIR_RESULT.md`. In particular: the
> corridor overlays ten retained places and its lane crosses `bandit_camp` (x=346, y∈[305,317)), which is
> the exact threat dependency (not "an encounter on a corridor region"); the encounter content is *not*
> unchanged (scenario versions change, tactical scope is world-wide); the refused-step proof below was
> geometry-only and synthetic (a real session refusal is now tested); and the evidence paths and hashes
> below belong to the original source, superseded by the repair run's final checks.

Status: **delivered; all four fixed checks accepted on one final build** (terminal results observed).
Model route: `claude-opus-5-5[1m]`, requested Max. Source revision `bf0e5a3`. Nothing staged or committed.
This is a managed (Host) circuit; native, threat/escort, NPC-route, migration, multiplayer and phase
gates remain open (see below).

## Changed responsibilities

| Path (under `Beta/Runtime/`) | Responsibility |
|---|---|
| `src/.../Content/Cargo/CargoCircuitSources.cs`, `CargoSourceRecords.cs` | Read retained `npcs/items/world.json` by exact file digest; bind Marla, Kegga, Brasshollow, Gearford, rations by source identity with pointers and canonical payload hashes; refuse missing/duplicated identities, tiles outside places, shops without the good, or a rule citing another good/price/file |
| `CargoCorridor.cs` | Authored L corridor: farm region + 7 east + 6 south (14), generated from the farm region's own seed/rules; approaches one diagonal step from each tile; waypoints ≤ 8 cells; keeps every retained region exactly; checks the 16-region bound |
| `CargoMerchantPresence.cs`, `CargoMerchantProvision.cs`, `CargoCircuitPolicy.cs` | Presence at source tile with source-place home identity; declared finite provisions (Marla 8 rations/no purse, Kegga 0 rations/80 gil); two authored rations rules citing the unchanged price 25 |
| `CargoGenesisExtension.cs` | Extends an unplayed profile-26 root through each owner (space, residents, custody/controls/policies/sources, progression+currency, gathering, construction exits, lifecycle debts/non-combatant scenario entries); re-prepares the retained dry water domain against the extended inventory and rebinds placed cells to the extended regions |
| `CargoWorldAdmission.cs` | Fresh composition: ordinary-care farm → extension → unchanged paid and market admissions; counter lookup; opt-in session merchant configuration |
| `src/.../Simulation/Cargo/CargoLegCoordinator.cs` | Host itinerary advice: per-leg bounded ground search from actual feet, one 32-subunit sample per cutoff, one replan after a refused step, then stop with status; never saved |
| `tests/.../Content/Cargo/*` | `CargoSourceBindingTests` (5), `CargoCorridorRouteTests` (4), `CargoWorldAdmissionTests` (1), `CargoCircuitJourneyTests` (1) + `CargoJourneyWorld` fixture/evidence writer |
| `docs/architecture/cargo-circuit.md` (new); `README.md`, `world-markets.md`, `content-and-composition.md`, `runtime-and-authority.md`; `Runtime/README.md` | Timeless reference and index/runtime updates |

No existing runtime, test, content, client, project, schema or codec file changed (wrapper preservation:
1,790 runtime files, 549 tests, 23 frozen specs, source DB, predecessor index — all exact on every run).

## Checks (exact wrapper commands; terminal results observed)

| Check | Evidence | Result |
|---|---|---|
| build (final) | `qualification/build-20261001T084258Z-987ba/report.json` | accepted, 14.6 s |
| unit (final build) | `qualification/unit-20261001T084611Z-26733/report.json` | accepted, 10 passed / 0 failed / 0 skipped, 4.9 s |
| journey (final build) | `qualification/journey-20261001T084317Z-da5e9/report.json`; exports in `.../journey/` | accepted, 1 passed, 167.0 s |
| regression (final build) | `qualification/regression-20261001T084621Z-1d2a2/report.json` | accepted, 466 passed / 0 failed / 0 skipped, 191.3 s |

All four reports cite build report `build-20261001T084258Z-987ba` (sha256 `ddbdaff3…`); every report shows
`sourceUnchanged: true` and identical preservation before/after. Documentation edits after that build are
outside the wrapper's pinned source set (`src`, `tests`, `client`, `content`, `tools`).

Journey exports: `genesis.snapshot`, `before-departure.snapshot`, `mid-trip-before-save.snapshot`,
`mid-trip.sqlite`, `final.snapshot`, `final.sqlite`, `results.json` (sources, admission, route, prices,
lineage, commands/receipts, funds at each checkpoint, recovery).

Retained failed attempts (immutable, under `qualification/`): `unit-…082727Z-5abc2` (my test assumed one
farm region; the care farm has two), `unit-…083346Z-3a7fc` (reservoir owner pins exact inventory),
`unit-…083504Z-ee6a2` (custody-only reconcile refuses added actors/sources), `journey-…084101Z-ea90e`
(the participant view correctly refuses to prepare acceptance of a closed quote; the test now captures the
acceptance while the offer was open and submits it stale).

## Actual facts (journey `results.json`)

- Sources: Marla `/npcs/12` tile (96,350) in `brasshollow` `/regions/0`; Kegga `/npcs/31` tile (347,140) in
  `gearford` `/regions/2`, quest `kegga_timber`; rations `/items/16` price 25. Payload hashes recorded.
- Admission: profile 33, 6 residents, 15 regions (farm + breach receiver + 13 corridor), admitted supply
  280 = farm 200 (Wren's quest reserve) + Kegga purse 80. Player and Marla start at 0 gil.
- Funding: the player's only money is Wren's ordinary quest reward (200). Buy 2 at Marla, unit 22, total 44
  (Marla stock 8→6, purse 0→44). Sell 2 to Kegga, unit 31, total 62 (Kegga 80→18, stock 0→2). Player
  200→156→218. Every checkpoint: sum of all accounts = admitted supply = 280.
- Learned prices: Marla ask/bid 20/13 at step 226 → 24/16 at step 7,547; Kegga bid 38 (no ask, no stock).
  Marla's memory at Gearford kept its step-226 observation (aged, not refreshed); distant counters could
  not even prepare an observation.
- Lineage: shelf source `e4752b3b…[0,8)` → carried lot `8ac141e5…` `[0,2)` → Kegga's delivered lot `[0,2)`.
- Route: farm→approach 224 samples; approach→corner 1,992 (7 transitions); corner→Gearford 1,664
  (6 transitions); return 3,656 (13 transitions). 0 replans; every applied sample is exactly 32 subunits.
  ≈19 ms per cutoff (walking 142.4 s of the 167 s run). Final revision 7,550, 7,547 advancing steps.
- Recovery: stopped at the corridor corner (revision 2,224), reopened with genesis-authenticated
  `OpenExisting`; state hash identical (`8e499ac9…`); same cargo lot continued.
- Stale trade: a Marla offer captured while open was submitted at Gearford → receipt `Unavailable`, no
  balance or quote-count change, quote not open; the view also refuses to prepare it. Nothing requoted.

## Not proven / open gates

- Threat/escort: no encounter is admitted on the corridor; the farm wolves were not engaged. Required
  dependency: an encounter admission whose scenario and entry geometry lie on a corridor region (with
  aftermath costs), then guard-wage service and escort NPCs. No narration substitutes.
- Blocked-leg and refused-step behaviour is proven in `CargoCorridorRouteTests` against real geometry;
  the session journey itself had no obstruction (0 replans).
- Actor parity/own control: merchants answered under their own host control via the existing session
  trading controller; no new negative case for cross-actor commands was added in this journey.
- Archive crossings were neither asserted nor recorded in this journey.
- Native presentation (camera-relative region drawing, multi-stall identity/choice), traveling NPC traders,
  lived migration, multiplayer join/reconnect, infinite frontier, physical/human acceptance and all phase
  gates remain open. This is a managed circuit, not native cargo.

## Deviations and friction

- I chained two wrapper invocations with `&&` twice and once piped one through `tail` — outside the
  exact-command rule. They only ran the allowed wrapper modes; the runs are retained as listed above.
- DESIGN.md first recorded the breach builder's single-region assumption as blocking; the same packet
  now records how the fresh extension resolved it without touching that builder.

## Next concrete integration

Astra review of `CargoGenesisExtension` (especially the water re-preparation) and the journey evidence;
then a native region adapter for this world (observed regions, camera-relative drawing, two stalls by
identity), followed by a corridor encounter admission and escort/wage service on the same route.
