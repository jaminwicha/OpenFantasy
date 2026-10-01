# Cargo integration repair result

Status: **repair delivered; final build/unit/journey/regression accepted on one build** (build report sha256
`cfc29ca6…`; terminal reports observed). One required item remains a precise limitation: no inherited water
*operation* was run in cargo authority (finding 4). Astra's saved audit has not been rerun on the new exports.

Model route: `claude-opus-5-5[1m]` (Max requested), one request. Source head `bf0e5a3`; pre-repair
checkpoint `REVIEW_CHECKPOINT.json` (snapshot `b0fe54a`). Nothing staged or committed; no Core, schema,
codec, client, wrapper, dispatch, handoff, critic-report or lead-audit edits. The lead's
`Integration/CargoCircuitEvidenceTests.cs` is untouched and was **not** run by me for the revised source.

## Changed files (all within ownership)

| Path (under `Beta/Runtime/`) | Change |
|---|---|
| `src/.../Content/Cargo/CargoCorridorOverlays.cs` (new) | `CargoPlaceOverlay`, declared v1 overlay set, canonical overlay version, `Compute` (inherited/added tiles, per-segment lane crossings), exact `Check` (undeclared / changed / vanished refused), `BanditCampCrossing` |
| `src/.../Content/Cargo/CargoCanonicalFrame.cs` (new) | `OFC` v1 frame mirror of the core canonical encoder with explicit big-endian signed/unsigned 64-bit fields |
| `src/.../Content/Cargo/CargoSourceRecords.cs` | `CargoTileRect`, `CargoSourcePlace.Tiles` |
| `src/.../Content/Cargo/CargoCircuitSources.cs` | binds all 25 retained `world.json` places (pointer, payload hash, unique ids; malformed/duplicate refused) |
| `src/.../Content/Cargo/CargoCorridor.cs` | computes and checks overlays on creation; `Overlays`, `LaneCorners`; internal declared-set overload |
| `src/.../Content/Cargo/CargoGenesisExtension.cs` | water budget read from retained `Capacity.Closed`, fail-closed hash proof of fresh dry genesis; canonical `genesis-extension.v2` admission identity incl. overlay version; explicit-provision constructor |
| `src/.../Content/Cargo/CargoMerchantProvision.cs` | canonical `provision.v2` version |
| `src/.../Content/Cargo/CargoWorldAdmission.cs` | corrected encounter claim; `CargoOpeningStock` + `OpeningStock(...)` typed Host binding |
| `src/.../Simulation/Cargo/CargoLegCoordinator.cs` | latched stop against a value situation; `Advise(WorldSnapshot)`; `RouteQueries`, `IsLatched` |
| `tests/.../Content/Cargo/CargoCircuitJourneyTests.cs` | wolf fight→outcome→release at start with merchant invariants; exact buyer/seller/stock/lot deltas; overlays + threat dependency + journal counts in evidence |
| `tests/.../Content/Cargo/CargoJourneyWorld.cs` | encounter fight/release/debt helpers; world-aware walk |
| `tests/.../Content/Cargo/CargoCorridorRouteTests.cs` | refusal test replaced by latch semantics (geometry-only, labelled as such) |
| `tests/.../Content/Cargo/Refinement/*` (new) | `CargoOverlayRefinementTests` (2), `CargoAdmissionRefinementTests` (6), `CargoSessionRefinementTests` (2), `CargoSourceDocuments` helper |
| `docs/architecture/cargo-circuit.md`, `README.md`, `world-markets.md`, `content-and-composition.md`, `runtime-and-authority.md`; `Runtime/README.md` | timeless corrections |
| packet `DESIGN.md`, `RESULT.md` | appended correction section / notice; original text kept |

## Checks (exact wrapper commands; terminal reports observed)

| Check | Evidence (`qualification/…`) | Result |
|---|---|---|
| build | `build-20261001T092558Z-fa324` | accepted (source-only build, before tests were written) |
| build | `build-20261001T093340Z-c5d33` | **failed** — CS0029 in my test helper (`LocalWorldView.Encounter` is the summary view; the action view is `EncounterActions`); retained |
| build | `build-20261001T093359Z-12d37` | accepted |
| unit | `unit-20261001T093411Z-4a461` | **failed** 19/20 — water test compared `ReservoirStorageProjection` records whose `ReservoirFill` members lack value equality (values printed identical); retained |
| build (final) | `build-20261001T093455Z-fb1b3` (report sha256 `cfc29ca6…`) | accepted, 11.7 s |
| unit (final) | `unit-20261001T093510Z-53d91` | accepted, **20 passed** / 0 failed / 0 skipped, 38.1 s |
| journey (final) | `journey-20261001T093557Z-8278d`; exports in `…/journey/` | accepted, **1 passed**, 223.5 s |
| regression (final) | `regression-20261001T093950Z-6aaa8` | accepted, **476 passed** / 0 failed / 0 skipped, 174.6 s |

All accepted reports cite the same final build report and show `sourceUnchanged: true` with identical
preservation before/after (1,790 runtime files, 549 tests, 23 frozen specs, source DB, predecessor index).
One premature `regression` invocation while the journey held the shared lock exited at the non-blocking
lock (`BlockingIOError`) before creating any evidence directory or touching sources; it is not a test result.

## Actual journey facts (`journey-20261001T093557Z-8278d/journey/results.json`)

- Admission: profile 33, admission `b8bfd982…`, supply 280; overlay version `7db0dcfb…`; threat dependency
  `bandit_camp` crossing `[346,347)x[305,317)`; ten overlays exactly as declared.
- Inherited wolf (`bramble_wolf_first`, episode `0182e1d4…`): 7 ordinary Strikes over 177 cutoffs to
  outcome; player HP 4 (of 60), wolf HP 0; released; one exploration cutoff paid the player's debt. Both
  merchants unchanged (HP 1, feet, presence revision 0, purse 0/80, stock 8/0, debt 0).
- Funds (player, Marla, Kegga; Marla/Kegga stock): genesis 0/0/80 (8/0) → wolf released 0/0/80 (8/0) →
  reward 200/0/80 → bought 156/44/80 (6/0) → sold 218/44/18 (6/2) → returned 218/44/18 (6/2); total 280 at
  every checkpoint. Buy 2 × 22 = 44, sell 2 × 31 = 62, stale acceptance `Unavailable`.
- Lineage: shelf `e4752b3b…[0,8)` → carried `[0,2)` → delivered `[0,2)`; Marla keeps `[2,8)` (asserted).
- Route: 224 / 1,992 (7 transitions) / 1,664 (6) / 3,656 (13) cutoffs, 0 replans.
- Recovery: saved at the corner, revision 2,404 (2,372 steps), reopened hash identical `6b33550c…`.
- Journal at the final head: 7,730 commits, 75 command outcomes, last checkpoint at sequence 6,032 (6,000
  steps), 0 resource archives, 0 material-care archives.

## Per-finding disposition (CRITIC_REVIEW.md)

1. **Undeclared overlays / bandit camp — corrected.** `CargoCircuitSources.Places` binds all 25 retained
   places; `CargoCorridorOverlays.Compute` enumerates intersections with the inherited farm region, the 13
   added regions and each lane segment; `CargoCorridor.CreateCircuit` refuses unless they equal the declared
   v1 set exactly. Actual set: inherited `brasshollow` 403 (lane y=349, x∈[97,127)), `tavern_brasshollow` 63,
   `farm_verdance_small` 63 (full); added `gearford` 208 (lane x=346, y∈[141,154)), `tavern_gearford` 49,
   `castle_aldermere` 406, `standing_stones` 12, `bandit_camp` 84 (**lane x=346, y∈[305,317)**),
   `farm_sylvas_medium` 108 (full; lane x=346, y∈[330,339)), `port_gearford` 140 (full). Tests:
   `CargoOverlayRefinementTests` (computed = declared; bandit crossing exact; undeclared moved place, omitted
   declaration, shifted crossing and vanished place all refused; version changes with the declaration). The
   journey records `route.overlays`, `overlayVersion`, `threatDependency` and the walked farm approach's
   overlays, asserting each approach crossing lies on the inherited farm region. Source bytes untouched
   (digest-checked reads). Docs name `bandit_camp` as the exact threat dependency; the wolves are
   player-initiated farm encounters and are not claimed absent.
2. **Recovery proof breadth — (a) corrected, (b)(c) covered by the lead audit on the original delivery,
   (d) recorded.** (a) `Fresh_genesis_is_byte_deterministic_for_one_seed_and_differs_for_another`. (b)(c) the
   lead's `CargoCircuitEvidenceTests` passed on the original exports (not duplicated; **not run by me on the
   revised source** — Astra reruns it on the new journey exports). (d) the new journey writes
   `results.json.journal` (commit/outcome counts, last checkpoint sequence/steps, resource and material-care
   archive counts) — see the journey row below for the observed values.
3. **Encounter semantics — corrected and proven.** Claims fixed in `CargoWorldAdmission` docs and
   `cargo-circuit.md`: scenario versions change (asserted for both scenarios against the farm's, with
   participants/target/range unchanged and +2 bystanders) and tactical timing is world-wide (asserted:
   every resident, including Kegga, has a timing row at outcome). The inherited first wolf is entered from the
   player's start and fought with ordinary Strike choices to outcome and released, in both
   `CargoSessionRefinementTests` and the main journey; Marla's and Kegga's HP, feet, presence revision, purse,
   stock and readiness debt are asserted unchanged, both can act, Marla answers in person afterwards (unit), and
   the journey's later purchase and sale show both counters answer.
4. **Water equivalence — corrected; real water operation NOT done (retained limitation).** The closed budget
   is read from the retained `Capacity.Closed` (exactly one required, no pending runoff); the extension
   re-prepares against the retained pins first and refuses unless the retained capacity and reservoir hashes
   are reproduced exactly (fresh dry genesis), so lived water state is refused, not reset.
   `The_retained_water_owners_are_equivalent_except_for_their_inventory_pins` asserts domain hash, closed
   budgets, pending, receivers, capacity revision, geometries, projected units, seals (same policy), cell
   quantities/portions/domain cells/units/volume/boundary and topology nominal/open cells/open capacity equal,
   and the inventory pin changed. **Retained limitations:** (i) no inherited water *operation* ran in cargo
   authority. The concrete one is the breach excavation: walk to `BreachFarmLayout.MiningStance`, select the
   excavation target, accrue 400 effort, submit the `Excavate` offer with the admitted tool and let the
   pending-excavation water consequence resolve (`BreachActionOfferTests` shows the farm path). It needs
   session target selection and consequence handling I judged too risky to add after the final source was
   qualified; equivalence of the rebound owners is proven, their live operation is not. (ii) The fail-closed
   branch has no negative test (no lived-water root was constructed). (iii) Farm composition-time validators
   (`ConstructionFarmProfileValidation`, `QuestFarmWorldValidation`) are still not re-run on the extended root.
5. **Distance refusal — corrected.** `An_open_quote_out_of_reach_and_distant_or_foreign_commands_are_refused_without_effects`:
   after a real in-person purchase, a second Marla offer is captured open, the player walks four cells away
   (32 samples, far inside the 1,200-step window; quote still open in the book), the captured acceptance is
   refused `OutOfReach` with balances, both stocks, open-quote count and lots unchanged and the quote still
   `Open`; hand-built `ObserveMarketIntent` and `DiscloseMarketLotIntent` to both counters are refused
   `OutOfReach`, no disclosure exists and the player's knowledge stays empty.
6. **Leg coordinator cycle — corrected.** Stops latch to a value situation (walker feet/revision/locomotion,
   bodies within 16 cells of walker or waypoint, region revisions, structure ids, movement admission:
   encounter phase, `CanAct`, pending consequences); unchanged situations return the stop with no query.
   Session proof: at the wolf outcome the session takes no movement; the coordinator replans once, stops,
   and four further ticks with two `Advise` calls each return the identical stop with zero new route queries
   and unchanged feet; release (phase change) revalidates, retained readiness debt refuses samples and every
   stop observed is asserted to be the debt (`CanAct` false), then the walk resumes and arrives at Marla's
   approach. Geometry test: equal fresh snapshot and a far body move keep the latch; the walker's own move
   releases it. The original synthetic test was replaced and labelled geometry-only. Ordinary movement still
   owns legality.
7. **Exact parties — corrected.** The journey asserts, at the purchase, player −`TotalFor(2)`, Marla
   +`TotalFor(2)`, Kegga unchanged, Marla stock −2, the carried lot `[0,2)` and Marla's remaining `[2,8)`; at
   the sale, player +`UnitPrice×2`, Kegga −`UnitPrice×2`, Marla unchanged, Kegga stock +2, delivered lot =
   carried source interval and the player no longer holds it.
8. **Opening-stock provenance — addressed without a new persisted schema.** `CargoWorldAdmission.OpeningStock`
   returns the typed `CargoOpeningStock` (merchant, role `marla.opening-stock-source`, provision version,
   admitted source, `craft_rations` recipe id, output definition) after checking the admitted source's
   capacity (8) and definition against the recipe output; a mismatched provision is refused; Kegga has none.
   Test asserts the role, capacity, recipe/output binding, the raw interval `[0,8)` in custody and no
   transformed (produced) rations lot. **Not a durable typed fact:** the save holds only the admitted source
   (id, definition, capacity) and the admission hash that covers the provision version; the binding is
   recomputed by Host authoring. No production lineage is claimed or fabricated.
9. **Non-canonical admission text — corrected.** `CargoCanonicalFrame` (core `OFC` v1 layout) frames the
   admission identity (`genesis-extension.v2`, explicit signed 64-bit region fields) and provision versions
   (`provision.v2`). Test: identical hashes under invariant and U+2212-minus cultures for negative
   coordinates; exact frame bytes for one signed field; a one-cell shift changes the hash.
10. **Untested refusals — corrected except two.** Tested: duplicated npc identity, duplicated place
    identity, shop without the shared good, provision naming another merchant, another-profile root
    (encounter farm) and an already-admitted cargo root, corridor overlap with retained terrain and the
    16-region bound, undeclared/changed/vanished overlays, and control: the player's delivery cannot prepare
    Marla's observation or offer, a session submission as Marla is refused at dispatch
    (`AuthorityDispatchException`), and Marla's prepared offer rewrapped under the player's binding is refused
    (dispatch exception or `NotAuthorized`, asserted either way) with no state change. Not separately tested:
    a *played* ordinary-care root (same guard as revision/steps) and an identity alias in `Register`.
11. **Headroom — documented, unchanged.** The 16-region bound stays fixed with 15 used. Native presentation,
    corridor threat (needs an admitted encounter whose entry geometry and aftermath lie on the bandit-camp
    crossing cells), escort/guard wages, traveling NPCs, migration, multiplayer and the infinite-frontier gate
    remain open: a corridor encounter footprint or escort staging needs reshaped or streamed regions, not an
    added region. Timing figures remain Debug-harness measurements, not native budgets.

## Source preservation

Every wrapper run (accepted or failed) recorded identical preservation before/after: 1,790 protected runtime
files, 549 existing tests, 23 frozen specs, the original source DB and the predecessor index unchanged. The
retained `content/` bytes are read only (digest-checked); no new content file was added. Others' unrelated
working-tree edits were not touched, reverted or staged.

## Remaining limits

- Inherited water operation unexercised in cargo authority; fail-closed water branch without a negative test.
- Played-root and identity-alias refusals not separately tested.
- The wolf victory is deterministic but narrow for the journey seed (player HP 4 of 60); a different seed may
  lose, in which case the tests fail rather than skip.
- The coordinator's 16-cell relevance horizon is a declared bound: a body change farther away does not release
  a stop (a new coordinator always revalidates).
- The lead saved audit has not been run on these new exports by me; the new journey keeps its asserted values
  (3 player acceptances, one `Unavailable`; balances 156/44/80 mid and 218/44/18 final; shelf partition).
- Not a native, threat, escort, NPC-route, migration, multiplayer, frontier or phase acceptance.

## Friction

- My first test build failed on a view-type mix-up and my first unit run failed on record reference
  equality; both are retained above. One premature `regression` invocation hit the non-blocking shared lock
  while the journey ran and exited before creating evidence. All other commands were the exact permitted
  wrapper commands, one at a time; no chaining, pipes or redirects.
