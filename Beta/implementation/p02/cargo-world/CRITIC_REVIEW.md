# Independent cargo-world critique

Status: **final review, no verdict** (the assignment asks for none). Reviewer: `claude-opus-5-5[1m]`, using
Read/Glob/Grep only. **I ran no build, test or shell command.** Inputs are the pinned context manifest
(source head `bf0e5a3`, snapshot `bbed6e8`). Paths are relative to `Beta/Runtime/` unless they start with
`Beta/` or name a qualification report.

Labels: **Executed** = a result recorded in the implementer's qualification reports, which I read but did
not rerun. **Source-derived** = my reading of code or data, never executed. Each finding is classed as a
demonstrated defect, a missing proof, a design challenge, an honest deferral or a speculative concern.

## Executed evidence relied on (read, not rerun)

- Final build `build-20261001T084258Z-987ba` was accepted. The unit run (`unit-…084611Z-26733`) passed 10
  of 10. The journey run (`journey-…084317Z-da5e9`) passed 1 of 1 in 167.0 s and hashed 7 artifacts. The
  regression run (`regression-…084621Z-1d2a2`) passed 466 of 466. All four reports show
  `sourceUnchanged: true`, identical preservation and the same build report (`ddbdaff3…`).
- `journey/results.json` records:
  - per-account funds at each recorded checkpoint;
  - the lineage `e4752b3b…[0,8)` → carried `[0,2)` → delivered `[0,2)`;
  - an identical recovery hash `8e499ac9…` at revision 2,224;
  - `staleRejection: "Unavailable"`;
  - 0 replans, with 13 region transitions on the way out and 13 on the way back.
- Pass counts prove nothing about the claims below that no test exercises.

## Findings, ordered by severity

### 1. The corridor overlays retained source places it does not declare, and the walking lane crosses the source bandit camp — Medium-High · demonstrated scope/meaning mismatch (source-derived)

`world.json` region rectangles are in the same tile space as the cells. The farm frame is source tile
space: `FarmAdmissionDefinition.cs:75-77` requires the source player and Wren tiles to lie inside the
farm. So the corridor's flat ground sits exactly on these retained places:

| Source place `[x0,x1)×[y0,y1)` | How the corridor touches it |
|---|---|
| `bandit_camp` [345,360)×[305,317) | Partly covered by the south column; **the lane at x=346 crosses y 305–316** |
| `farm_sylvas_medium` [338,350)×[330,339) | Fully covered by the corner region; **the lane at x=346 crosses y 330–338** |
| `port_gearford` [330,340)×[220,234) | Fully covered |
| `castle_aldermere` [166,195)×[311,334) | Rows 320–333 covered |
| `standing_stones` [306,326)×[111,130), `tavern_gearford` [345,355)×[128,135) | Partly covered |
| `tavern_brasshollow` [95,105)×[340,347) | The test's farm→Brasshollow approach lane at x=101 crosses it. This is inside the retained farm region, so it is inherited, not caused by the corridor |

- `CargoCorridor.Validate` (`src/.../Content/Cargo/CargoCorridor.cs:127-153`) checks only the two merchant
  tiles, the waypoints and the two approach places. Nothing enumerates other retained places.
- `cargo-circuit.md:55-64` and `RESULT.md` call the corridor "authored qualification geometry" but never
  disclose that it covers a castle, a port, a farm and a hostile site with ordinary farm ground.
- The threat dependency in `RESULT.md` ("no encounter is admitted on the corridor … an encounter admission
  whose scenario … lie on a corridor region") is imprecise in two ways:
  - **Region 0 of the corridor is the farm region.** `AllRegions` prepends `Basis`. Both wolves are
    admitted there, one cell either side of the player's start (`EncounterFarmWolf.cs:34`, offset ±256
    subunits). However, they can only be engaged by the player (`EncounterJourneyAdmission.cs:62`, the
    player is the initiator; `WorldEncounterLifecycleProcessor.cs:106`), so they cannot threaten a lot in
    transit.
  - **The real dependency sits on the lane and is nameable from source.** `bandit_camp` is targeted by
    `quests.json:598` (`targetRegion: bandit_camp`) and `quests.json:612` (`targetMonster: bandit_chief`),
    and `bandit_chief` is defined at `monsters.json:1126`.
- **Repair:** make `CargoCorridor` enumerate every retained `world.json` region that intersects a corridor
  region or the lane. Refuse unless each one is in a declared, versioned overlay list. Write that list into
  `results.json`/`route` and `cargo-circuit.md`. Name `bandit_camp` at x=346, y∈[305,317) as the exact
  threat/escort dependency.
- **Test:** the computed intersection set equals the declared list, and an undeclared overlap is refused.

### 2. Recovery proof is narrower than claimed: no final reopen, no prefix comparison, unproven genesis determinism — Medium · missing proof

**What is strong (source-derived):** `SqliteWorldAuthority.OpenExisting(path, expectedGenesis)`
(`src/.../Worlds/SqliteWorldAuthority.cs:85-114`) performs a full `WorldJournalRecovery` replay
(`WorldJournalRecovery.cs:80-245`). That covers per-commit deterministic replay, the hash chain, the
command and identity tables, and head equality. So the reopen at revision 2,224 is genuinely
replay-authenticated (executed: the hashes are equal).

**What is missing:**

- **(a) The expected genesis is not independent.** It is the same in-memory object that created the save
  (`CargoJourneyWorld.cs:27`; `CargoCircuitJourneyTests.cs:28,125`).
  - No test composes `CargoWorldAdmission.CreateEmbedded(seed, name)` twice and compares the bytes.
    `CargoWorldAdmissionTests` uses a single `Lazy`.
  - Genesis determinism is required by any product path that opens a save against an expected genesis, and
    it is unproven.
- **(b) `final.sqlite` is exported but never reopened.**
  - `CheckpointStepInterval = 6_000` (`WorldJournalPolicy.cs:24`), so only the final save (7,547 steps)
    contains a checkpoint.
  - The reopen at 2,221 steps crosses no checkpoint, and no test replays the full 7,550 commits.
- **(c) The preserved prefix is never compared.**
  - Nothing checks that the final save's recorded state at revision 2,224 equals `8e499ac9…`.
  - Nothing checks that journal rows 1..2,224 are byte-identical between `mid-trip.sqlite` and
    `final.sqlite`.
  - The assignment asks to "compare the preserved prefix and complete final state".
- **(d) Archive crossings are undetermined.** `RESULT.md` says they were neither asserted nor recorded, but
  the test also never establishes whether any resource or material-care archive was sealed.

**Repair:**
- After the final stop, open `final.sqlite` with the bytes of a **fresh** `CreateEmbedded(SeedText,
  "Traveler")`. Assert that the head hash equals the `final.snapshot` hash.
- Read the recorded state at revision 2,224 and assert it equals the mid-trip hash.
- Compare the two saves' journal prefixes.
- Record checkpoint and archive ordinals in `results.json`.
- Add a unit test: two compositions are byte-equal, and different seeds differ.

### 3. Inherited encounter semantics are altered and coupled across the whole world — Medium · demonstrated identity change; operational effect unexercised

**Demonstrated (source-derived):**
- `ExtendLifecycle` (`CargoGenesisExtension.cs:203-213`) rebuilds both wolf scenarios with Marla and Kegga
  as side-0 bystanders.
- The owner forces this: `EncounterLifecycleSnapshot.cs:51` requires every scenario to name the full debt
  actor set. It also matches the existing bystander pattern (`EncounterJourneyAdmission.cs:63-66`, where
  Wren is side 0). It is therefore **not a bypass**.

**Consequences:**
- **(a) The scenarios get new content identities.** Scenario `Version` hashes the combatant list
  (`EncounterLifecycleCodecV1.cs:81-85`; `EncounterScenarioDefinition.cs:55`). So "encounter content
  unchanged" (`CargoWorldAdmission.cs:13-14`; `cargo-circuit.md:31`) is not literally true.
- **(b) A farm fight pulls in the whole world.**
  - Entering a wolf encounter builds tactical timing for **every** resident
    (`WorldEncounterLifecycleProcessor.cs:59-65`), and the lifecycle phase is world-wide.
  - A fight on the farm therefore puts Kegga, roughly 460 cells away, into tactical timing as an
    `Automatic` actor, and moves all 15 regions from exploration steps to tactical ticks.
  - *Speculative, not traced:* whether trading or walking elsewhere is suspended during that phase.

**Unexercised:** the journey never enters an encounter. The admission test checks only the quest, recipes
and remedy.

**Repair/test:**
- In the cargo world, enter `bramble_wolf_first` at genesis (the player starts one cell from that wolf)
  and run it to outcome and release.
- Assert that merchant debts, HP, feet and stock are unchanged, and that both counters answer afterwards.
- Document the world-wide tactical scope as a threat-gate limitation.

### 4. Water re-preparation is a reconstruction whose equivalence is unproven — Medium · missing proof

- `RebindWater` (`CargoGenesisExtension.cs:90-107`) reproduces `BreachFarmWater.cs:60-67`:
  - It re-derives the budget identity from hard-coded strings (`"breach-farm"`, `"water.closed-budget"`,
    line 95). That matches `BreachFarmWater.cs:27,80` today.
  - It keeps only `retained.Geometries` and rebuilds the reservoir state from new capacity.
  - It then rebinds the cells.
- No test compares the retained and rebuilt reservoirs or liquid cells (capacity, contents, seals,
  topology). `CargoWorldAdmissionTests.cs:15-52` checks no water owner, and the journey never touches
  water. `RESULT.md` itself names this as the first thing to review.
- *Speculative:* any retained reservoir state beyond geometry would be reset by `LiquidReservoirState.Create`.
- *Speculative:* `DESIGN.md` says "no validator is bypassed". That holds for root validators and the
  coordinated/paid/market admissions. Farm composition-time checks (`ConstructionFarmProfileValidation`,
  `QuestFarmWorldValidation`) are not re-run on the extended root. I did not verify that they would still
  accept it.
- **Repair:** read the budget identity from the retained capacity state, if it exposes one, rather than
  re-deriving it by profile name. Assert that the rebuilt reservoirs and cells equal the retained ones
  except for the inventory/production pin. Exercise one inherited water operation in the cargo world.

### 5. Distance refusal at the new scale is unexercised — Medium · missing proof

- **The stale refusal tests expiry, not distance.** The stale offer was refused `Unavailable` (executed)
  because its 1,200-step window (`CargoCircuitPolicy.cs:32`) had already closed during a 3,656-step walk.
  `WorldPaidExchangeResolver.cs:160-187` checks openness and control before it reaches the `OutOfReach`
  check.
- **No open quote is tried from a distance.** The journey therefore never shows that an *open* quote cannot
  be accepted from another region.
- **"Distant counters could not even prepare an observation" is a host-service result.** The test checks
  that `PrepareObservation` returns null (`CargoCircuitJourneyTests.cs:70-71,148`).
- **The authority does enforce reach (source-derived):**
  - `WorldMarketResolver.cs:154-159` for observe;
  - `WorldMarketResolver.cs:193-198` for disclose;
  - `WorldPaidExchangeResolver.cs:183-187` for accept.
- **Repair test:**
  - Capture an open Marla offer, walk four regions or fewer (under 1,200 steps), and submit it.
    - Expect `OutOfReach`.
    - Expect no change to balances, stock or quote count.
    - Expect the quote to remain open.
  - Submit a hand-built distant observation command and a distant disclosure command.
    - Expect `OutOfReach`.
    - Expect no knowledge entry.

### 6. Leg coordinator: no "wait until state changes" latch; refusal test is synthetic; no in-session obstruction — Medium-Low · design gap and missing proof

- **The stop does not persist.** After `Blocked`, the coordinator clears `_expected`
  (`CargoLegCoordinator.cs:75-77,86-89`). The next `Advise` then clears `_refusedAt` (61-65), replans and
  advises a step again, even if nothing changed.
  - Suppose an obstruction is accepted by the route search but refused by `PrepareMove`.
  - A host loop would then cycle Moving → Moving → Blocked indefinitely, two refused samples per cycle.
  - "Stop and revalidate when … state changes" (`ASSIGNMENT.md` outcome 3; `cargo-circuit.md:68-76`) is not
    implemented as a state-change check.
- **`RESULT.md` overstates the refusal proof.** It says refused-step behaviour is "proven … against real
  geometry", but `CargoCorridorRouteTests.cs:102-122` produces "refusal" by advising twice on an unchanged
  snapshot. The "occupied" case is an occupancy check made before planning. The session journey had 0
  replans.
- **Note, not a defect.** For straight legs of up to 8 cells, `GroundRouteSearch.Find` returns through the
  direct `Connect` path before any expansion (`GroundRouteSearch.cs:58-62`). On flat corridor ground the
  256-expansion budget described in `DESIGN.md` is therefore never consumed.
- **Repair:** latch `Blocked` together with the observed space or actor revision, and keep returning it
  until that revision changes.
- **Test:** an in-session obstruction built from admitted bodies, for example a lane through Marla's tile
  or a later actor move, asserting stop, status and resumption.

### 7. Exact parties and stock deltas are recorded as evidence but not asserted — Low-Medium · missing proof

- **Asserted:**
  - money conservation at each recorded checkpoint (`CargoCircuitJourneyTests.cs:55`);
  - the carried quantity and interval (104-106);
  - the delivered interval (169-171);
  - the player's sale credit (167).
- **Only recorded in `results.json`/`funds` (298-358):**
  - Marla +44 and Kegga −62;
  - the player's −44 at purchase;
  - stock 8→6 at Marla and 0→2 at Kegga. Marla's 6 is implied only indirectly by `Ask(6,1)` at line 179.
- Structural validation lowers the risk: quote records must name the merchant's own custodian and account
  (`WorldMarketAdmission.cs:78-98`). This is not a suspected defect. But the assignment's "independently
  check … all participants' money" is satisfied only by reading the evidence.
- **Repair:** assert the per-party deltas against `buyOffer.TotalFor(2)` and `sellOffer.UnitPrice*2`.
  Assert both counters' stock. Assert that the sale settlement names the disclosed lot.

### 8. Opening-stock provenance is typed only by a derived identity — Low-Medium · design challenge

- Marla's shelf is a generic `AdmittedCountedSource` of the farm's **produced** rations definition
  (`CargoGenesisExtension.cs:110-114,135-141`).
- The authority therefore validates it as raw counted-source rights
  (`WorldPaidExchangeAdmission.cs:215-227`), with no production lineage. Rations made on the farm are
  produced lots instead, so one definition now has two provenance kinds.
- The provision version (`CargoMerchantProvision.cs:23-24`) survives only inside the opaque admission hash
  (`CargoGenesisExtension.cs:215-230`).
- This is honest: no berries are relabelled. But it is weakly typed for future consumers.
- **Repair:** record a durable typed provision fact that links the source identity to the provision
  version. Test that the shelf source is exactly the `marla.opening-stock-source` role, with capacity 8,
  bound to the `craft_rations`→`rations` output (`ProductionFarmSourceValidation.cs:32-33`).

### 9. The admission text is not canonical — Low · latent / speculative

- `AdmissionVersion` builds a string with `StringBuilder` and appends `CellCoordinate` through the
  compiler-generated record `ToString()` (`CargoGenesisExtension.cs:217-221`; `CellCoordinate.cs:4`), using
  current-culture number formatting.
- Other admissions use `CanonicalEncodingV1.Frame` (`WorldPaidExchangeAdmission.cs:21-22`;
  `WorldMarketAdmission.cs:37-38`).
- All current coordinates are non-negative, so this makes no difference today. A basis below zero would
  make the hash depend on culture and compiler formatting.
- **Repair:** frame explicit integers with the canonical encoder.

### 10. Implemented refusals have no tests — Low · missing proof

- **Refused in code but never tested:**
  - a duplicated identity (`CargoCircuitSources.cs:129-132`);
  - a shop list without the shared good (107-110);
  - a provision naming another merchant (`CargoGenesisExtension.cs:119-122`);
  - a played or other-profile root (47-51);
  - an identity alias (234-240);
  - a corridor that overlaps retained terrain or exceeds 16 regions (`CargoCorridor.cs:96-103`).
- **What the tests cover:** a mismatched cited price, a missing merchant, a moved place and changed bytes
  (`CargoSourceBindingTests.cs:40-89`).
- **No negative actor-control case either.** Nothing tests the player commanding a merchant, or a merchant
  answering under the wrong delivery binding. `RESULT.md` admits this.

### 11. Little headroom for the next gates — Low · design challenge

- **Regions:** 15 of the fixed 16 are used (executed: `route.totalRegions`). A corridor encounter, escort
  or town footprint will need reshaping, not addition.
- **Timing:** about 19 ms per cutoff includes at least two harness state reads per cutoff
  (`CargoJourneyWorld.cs:112-121`) and Debug binaries. It is neither a commit-cost measurement nor a native
  real-time budget.

## Counterexamples tried that held up

- **Tile→cell (source-derived).** Marla's (96,350) inside the farm region is her real source tile, not a
  relocation. `farm_verdance_small` (100,320) lies in the same region.
- **Shared profile 33.** The native farm also derives profile 33 (`WorldSnapshot.cs:309`). Recognition goes
  by market identity and policy version:
  - `NativeTradingWorld.FindStall` (44-50) returns null here, so the native farm path will not misbind.
  - `CargoWorldAdmission.Find` fails closed if the policy changes. Any authored policy change makes existing
    cargo saves unrecognised, with no migration path.
- **Saves are closed when copied.** The session disposes its authority on stop
  (`LocalWorldSession.cs:742`).
- **Other checks held.**
  - The water budget derivation matches today.
  - Side-0 bystanders follow the existing pattern.
  - Market admission checks the merchant's home place, storage and account ownership
    (`WorldMarketAdmission.cs:68-77`).
- **Funding.** Kegga's 80 gil is declared new supply (`CargoGenesisExtension.cs:172-176`). The 200-gil
  reserve stays Wren's reward. Conservation is asserted at every recorded checkpoint (executed).
- **Honest deferrals as stated:**
  - not admitted at runtime: wander radius, dialogue, Kegga's quest and the other shop goods;
  - gates left open: native presentation, traveling NPCs, migration, multiplayer, frontier and all phase
    gates.

## Suggested order of repair (not a verdict)

1. Declare and validate the source places the corridor overlays, and rename the threat dependency to
   `bandit_camp` (finding 1).
2. Reopen the final save, compare the prefix, and test genesis determinism (finding 2).
3. Test water equivalence (finding 4) and run one real wolf encounter in the cargo world (finding 3).
4. Test `OutOfReach` for an open quote and for a forged distant observation (finding 5), and assert
   per-party deltas (finding 7).
5. Add the coordinator latch and an in-session obstruction test (finding 6). The remaining findings are
   lower priority.
