# World composition refinement: three-pass analysis

Date: 2026-09-25. Author: Claude `fable[1m]`, Max effort, delegated read-only analysis. Reviewer and
integrator: Astra. A separate independent Fable critic reviews this document afterwards. This is a
design deliverable, not an implementation record: no source was changed, no build or test was run,
no Git operation was performed. Companion machine-readable index: [FINDINGS.json](FINDINGS.json).

## 0. Scope, inputs, method and status legend

**Inputs read.** `AGENTS.md`, `Beta/AGENTS.md`, `Beta/implementation/AGENT_HANDOFF.md` (lines 1–115),
`Beta/implementation/WORLD_COMPOSITION_DECISION.md`, all 36 pages under
`Beta/Runtime/docs/architecture/`, the frozen `Beta/spec-v1_1/TECHNICAL_DESIGN.md`,
`CROSS_SYSTEM_SYNERGIES.md`, `ECOLOGY_AND_FLUIDS.md`, `GEOLOGICAL_WORLD_DESIGN.md`,
`LANGUAGE_AND_AFTERLIFE.md`, the retained scenario
`Beta/implementation/p02/encounter-choice-validation/RECOVERY_DESIGN.md`, and source evidence:
`Beta/Runtime/src/OpenFantasy.Core/Progression/ActorProgressionState.cs`,
`Care/RestorativeItemPlanner.cs`, `Worlds/WorldResident.cs`, `Spatial/ActorPresence.cs`, plus a
directory listing of Core modules.

**Deliberately not inspected.** `Progression/ResourceHistory*` and its tests (another worker owns
them in flight; findings that touch ordered resource evidence are stated as integration obligations,
not as claims about that code). The Care WIP types (`CareSnapshot`, `CareAgreement`,
`CareAcknowledgment`, `AdministerRemedyIntent`, `CareCodecV1`, `RestorativeRemedyDefinition`) were
only listed; the handoff records them as unbuilt/unqualified. `REQUIREMENTS.json` (7,089 lines),
`COVERAGE.json`, `IMPLEMENTATION_PLAN.md`, the test projects and client scripts were not read;
phase names below come from the technical design, synergies atlas and handoff.

**Status legend used throughout.**

| Tag | Meaning |
| --- | --- |
| IMPL | Runtime binding present in this WIP tree according to the reference and the source read |
| CAND | Executable standalone domain candidate; no complete world admits it |
| WIP | Present in the tree but recorded as unbuilt/unqualified by the handoff |
| DESIGN | Obligation stated by frozen spec or reference without a corresponding API |
| GAP | Neither an owner nor an explicit obligation is stated; a decision is needed |

**Caveats.** The code is a WIP snapshot; the handoff says the last care build failed in tests. Nothing
here asserts that any test passes. Doc line numbers refer to the current working tree, which has
uncommitted edits to `AGENTS.md`, `Beta/AGENTS.md` and the handoff.

**Method.** Pass 1 inventories owners, definitions, relations, permissions, capabilities,
actions/processes, dependencies, history and projections, then lists inconsistencies. Pass 2 walks
concrete cross-system scenarios, including coupled and adversarial cases, against the inventory.
Pass 3 extracts only abstractions justified by at least two real contrasting uses, explains clocks,
scopes and atomicity, and produces the plan in §5. Findings are numbered `WC-nn`; open decisions
`D-nn`.

## 1. Pass 1: inventory of the model

### 1.1 Canonical owners composed in the world root

`WorldSnapshot` (`Worlds/WorldSnapshot.cs`) is the immutable root. Profiles 1–24 add owners
monotonically along two lineages (see §1.10, I-8). Each row names the fact the owner holds.

| Facet / fact | Owner (IMPL unless noted) | Revision | Notes |
| --- | --- | --- | --- |
| Counted material custody: sources, lots, custodians, storage, reservations | `CountedAuthorityState` (`Custody/`) | custody root + per lot | Custodian's owner is an actor; title is not separated from possession |
| Transformation history, live derived lots, retired ingredients, consumed claims | `ProductionSnapshot` V1/V2 (`Production/`) | production | V2 consumption owner rejected by every complete world (item-consumption.md:12–16) |
| Terrain cells, generation pin, excavation edits | `TerrainRegionSnapshot` inside `LocalSpatialSnapshot` | region | Not a geological event log |
| Actor pose, form binding, body condition | `ActorPresence` (`Spatial/ActorPresence.cs:7–35`) | presence | Body condition is a spatial-owned field; `At()` bumps the same revision on movement (lines 37–38) |
| Controller kind, home, comprehension, per-resident experienced time, inventory reference | `WorldResident` (`Worlds/WorldResident.cs:35–75`) | none | `ExperienceStep()` adds 1,800 ms per exploration step (line 71) |
| Level, XP, stat profile pin, current HP/MP, currency ledger | `ActorProgressionState` / progression snapshot | actor | Sole HP/MP owner; level gain refills both to maxima (`ActorProgressionState.cs:84–86`) |
| Calendar day/ms, world revision, advancing steps | root fields | root | Distinct counters |
| Dialogue holds | dialogues component | — | Local hold semantics only |
| Gathering nodes, per-actor effort/work | `GatheringSnapshot` | — | Process-interval regrowth |
| Delivery quests, participation, funding | `QuestSnapshot` | — | Once-only delivery/reward |
| Projects, earned work, installed output, derived colliders | `ConstructionSnapshot` | work count | Installed guard is a reserved live lot bound to geometry |
| Member condition, disposition, displacement | `ConstructionPhysicalSnapshot` (profile 13+) | physical + per member | Only condition facet that exists anywhere |
| Load/settlement/execution/coupled policies, descent mode | immutable policy assets in root (14–18) | pinned | No command edits them |
| Pending excavation consequence (basis pin, cursor, reserved IDs) | `PendingExcavation` (16–18) | basis+1+count | Prototype of durable staged work |
| Excavation sites: owner, tool, work, source entitlement, reach mode | `ExcavationSnapshot` (10+, 21) | site = work | Fixed authored work per site (excavation.md:13–17) |
| Reservoir capacity holds, closed reservation set, geometry, fills | `LiquidReservoirState` over `WatershedRunoffCapacityState` (8+) | owner logical | Closed set ≤4,096, never reused |
| Gate catalog, arrival stages, processed boundary | `ReservoirTransportState` (9) | transport | Journal ≤4,096 stages |
| Sealing policy | `ReservoirSealPolicy` in root (19+) | pinned | Enforced on publication |
| Exact liquid cell portions, flow profile, boundary | `LiquidCellState` V1/V2 (20+, 21+) | liquid | Portions ≤16,384; excludes runoff commitments |
| World exposure origin/progress, optional encounter timing + entry exposure | `WorldPacingState` (22+) | root | "Every resident … has exactly that experienced duration" (world-pacing.md:8–9) |
| Ability definitions, grants, sides, pending requests, damage draw, last resolution | `EncounterActionSnapshot` (23; nested in 24) | timing-linked | Last resolution retains post-damage HP used as an admission check (RECOVERY_DESIGN.md:29–35) |
| Scenarios, phase, encounter count, permanent readiness-debt ledger, cumulative E/T counters | `EncounterLifecycleSnapshot` (24) | = world | Debt copied into/out of timing rows (encounter-lifecycle.md:78–79) |
| Actor readiness, remaining delay, pending decisions | `EncounterTimingSnapshot` (inside pacing) | timing | Second copy of debt during Active |
| Care agreement, acknowledgment, treatment claims | `CareSnapshot` (WIP) | — | Not admitted by any profile; not inspected |

**Durable history owners (Hosting):** `SqliteWorldAuthority` journal (`world_commits`,
`world_command_outcomes`, `world_occupied_identity`, `world_checkpoints`, `world_head`), schema
three/four/five/six; recovery replays from genesis and verifies the hash chain
(persistence-and-history.md:10–28, 256–266). `RecordedWorldState` reconstructs exact commits for a
privileged operator; `History/` types (`HistoryAddress`, `HistoryRequest`, …) are CAND and reconstruct
nothing.

### 1.2 Immutable definitions

Stat profiles and level curves; counted item definitions with physical calibration; recipes;
gathering node/tool definitions; construction definitions with millimetre members; body definitions,
parts, functions and locomotion requirements (`Actors/`); ability and scenario definitions;
language channels and comprehension requirements (`Language/`); policy assets (load, settlement,
execution, coupled, seal, flow profile, pacing rules); `RestorativeRemedyDefinition` (WIP). All are
version-pinned by content hash; admission rejects unknown pins. No definition carries instance
state. This matches the composition table's Definition row (world-composition.md:26).

### 1.3 Typed relations: present versus required

| Relation | Endpoints and semantics | Status |
| --- | --- | --- |
| Custody | custodian → lot interval; custodian → owning actor | IMPL; conflates possession and title |
| Reservation | lot → reservation ID (quest, project, runoff, tool) | IMPL; purpose is a world-composition rule, not a catalog field |
| Control | principal/session → actor (trusted delivery) | IMPL local; network authentication DESIGN |
| Form binding | actor → form instance → physical body, tether anchor | IMPL (`ActorFormBinding`); tether unused |
| Home | presence → home place | IMPL |
| Agreement (delivery quest) | giver ↔ deliverer, funded reward | IMPL |
| Builder commitment | builder ↔ project (controller-level home/commitment) | IMPL in Hosting controller only |
| Encounter side, ability grant | scenario-local | IMPL |
| Site ownership | site → permitted worker | IMPL; "permissions do not imply shared-party extraction rights" (excavation.md:30) |
| Installed lot ↔ structure ↔ members | material backing of geometry | IMPL |
| Care agreement | patient ↔ helper, permitted treatment | WIP |
| Comprehension | actor → channel band | IMPL |
| Kinship, household, marriage, guardianship | directed with legal recognition separate | DESIGN (TD §8:139) |
| Membership, office, mandate, delegation | typed authority graph; survival policy | DESIGN (TD §8:143) |
| Scoped permission (treat, extract, care for, act for) | grantor → grantee, scope, resources, revocation | GAP: only the care draft; see WC-13 |
| Attachment / support / boarding / towing | one relation, one cargo owner, one motion frame | DESIGN (TD §8:167–169) |
| Evidence / belief | world truth → scoped evidence → belief | DESIGN; `Observations/` covers resources only |
| Title / estate / claim | ownership distinct from custody | DESIGN (TD §8:141,145) |

### 1.4 Permissions in force

| Action | Who may | Evidence |
| --- | --- | --- |
| Consume/administer a dose | custodian's owner, authenticated; recipient named only | item-consumption.md:25–29 |
| Excavate a site | site owner with tool, effort, reach, unheld advancing step | excavation.md:27–36 |
| Build | eligible builder with reserved material and access | materials-and-activities.md:61 |
| Accept/deliver quest | addressed participant with stock and living counterpart | encounter-lifecycle.md:86 |
| Enter/release encounter | living initiator; release pinned to exact outcome | encounter-lifecycle.md:46–74 |
| Runoff/transport controls | owner control, correct profile | world-and-geography.md:434–466 |
| Treat another actor | none admitted; care agreement WIP | RECOVERY_DESIGN.md:46–49 |

### 1.5 Capabilities

Body functions (manipulation, locomotion, natural weapon) derived from parts and injuries; ability
level/MP requirements; melee reach and sightline; readiness (zero debt) and positive HP for
voluntary action (`WorldActorAvailability`); tool suitability; comprehension. There is no single
capability evaluator: "living" is checked independently by availability, encounter actions, lifecycle
entry/release and dialogue/give (encounter-lifecycle.md:83–88, encounter-actions.md:69–72). Cached
capability results with dependency versions (world-composition.md:30) are DESIGN.

### 1.6 Actions and processes with their clock measure

| Action version | Intent | Profiles | Time charged |
| --- | --- | --- | --- |
| 6 | runoff admit/cancel | 8, 9, 19 | ordinary step |
| 7 | schedule transport | 9 | ordinary step |
| 8 | excavate | 10–18, 21 | ordinary step; consequence profiles reserve it |
| 9 | climb | 11, 12 | ordinary step + 400 effort |
| 10 | Wait | 22–24 | zero; positive readiness debt |
| 11 | ability | 23, 24 | zero; MP, damage, debt at resolution |
| 12 | enter/release | 24 | zero |

Passive processes and their order inside one advancing step: actions → calendar increment → due
transport stages (arrival, then stage ID) → detailed liquid flow at the earned 1,800 ms boundary →
movement (runtime-and-authority.md:101–103; reservoir-transport.md:111–116; liquid-cell-flow.md:73–75).
Gathering regrowth and NPC amble use process intervals; tactical ticks add 50 ms and retain carry.
Passive recovery, bleeding, hunger, wear, growth and weather have no owner (DESIGN).

### 1.7 Dependency joins at publication

`WorldStepProcessor` reconciles, per action candidate: material custody ↔ production ↔ reservoir
wrapper ↔ liquid portions ↔ excavation coverage ↔ construction physical members ↔ structural loads ↔
containment seals ↔ encounter lifecycle/timing. Each page describes its own reconcile; no single
ordered checklist exists in the reference (see WC-14). Identity roles are joined by
`WorldIdentityIndex` and `WorldAdmissionValidator`.

### 1.8 Embedded bounded ledgers

| Ledger | Bound | Exhaustion behaviour | Rollover contract |
| --- | --- | --- | --- |
| Consumed item claims (V2) | 256 | refuse | none |
| Runoff closed reservation set | 4,096 | refuse | "explicit owner contract" (never written) |
| Transport stage journal / terminal set | 4,096 each | refuse | none |
| Pending encounter requests / definitions / combatants | 64 / 32 / 64 | refuse | not needed (per episode) |
| Excavation sites | 32 | refuse | none |
| Production batches, live lots | 16 MiB archive; 4,096 lots | refuse | none |
| Journal envelope | 250,000 commits, 65,536 outcomes, 128 MiB | host stops visibly | checkpoints do not compact |

The reference states these are envelopes, not rules (world-composition.md:153–157), and forbids
deleting provenance to fit a bound. No provenance-preserving compaction exists anywhere (WC-09).

### 1.9 Observer projections

`LocalWorldView` (sight-complete locality; controlled actor's stock; applicable progression, quests,
construction), `LocalLiquidView`, `LocalWorksiteView`, `LocalEncounterActionView`,
`LocalEncounterLifecycleView`. `Observations/` provides scoped resource observations as a CAND.
NPC controllers consume the same local view as their bounded context (encounter-actions.md:115–116),
which is the right precedent for evidence-scoped planning. General perception, remote knowledge,
belief and per-principal co-op filtering are DESIGN.

### 1.10 Inventory findings: inconsistencies, duplicates, ambiguity, missing links

- **I-1 Past result owns present state.** Profile 23/24 admission requires the retained last attack's
  target HP to equal the actor's current HP (RECOVERY_DESIGN.md:29–35). The decision document names
  this seam; it makes any later authorized HP change (aid, level gain, regeneration) fail admission
  unless the record is rewritten. → WC-01.
- **I-2 Two exposure facts.** `WorldResident.ExperiencedTime` is per resident; `WorldPacingState`
  declares that every resident has exactly the world's accepted exposure. The pair is reconciled by
  admission, so the fact is duplicated with a copy discipline. The design requires divergence
  (held pockets, uncoupled dependants, independent co-op; time-and-encounters.md:62–66, TD §5:74–79).
  → WC-02.
- **I-3 Two readiness-debt facts.** Timing rows during Active; lifecycle ledger otherwise; copied at
  entry and after each active boundary (encounter-lifecycle.md:78–79). → WC-03.
- **I-4 Body condition inside spatial presence.** Injuries are a field of `ActorPresence`, whose
  revision also advances on every move. Anatomy has no owner separate from pose. → WC-04.
- **I-5 "Living" rule duplicated.** HP > 0 is re-derived in at least four places; consciousness,
  eligibility and life state are not facets. Incapacitated actors cannot be targeted, cannot act, and
  have no death or bleed-out path: they are immortal and stuck unless aided. → WC-05.
- **I-6 Remedy policy in the planner, not the definition.** Full-health refusal and quantity-one are
  hard-coded (`RestorativeItemPlanner.cs:36–40`), while the reference says the action definition
  names no-effect cost policy (world-composition.md:86–88). Delivery mode and anatomy are caller
  obligations with no typed carrier. → WC-06.
- **I-7 Untracked HP writer.** `GainExperience` refills HP/MP to new maxima on level gain
  (`ActorProgressionState.cs:84–86`); the decision already flags it as a required caller of any
  resource-evidence contract. → WC-07.
- **I-8 Two profile lineages.** Dry structural profiles 13–18 (physical members, load policy,
  settlement, consequence continuation) and wet profiles 19–24 (containment, flow, breach, pacing,
  encounters) both derive from profile 5 and are mutually exclusive by explicit guards
  (liquid-cell-flow.md:61–66; terrain-liquid-breach.md:29–32; reservoir-containment.md:22–25). Care is
  being added on the wet lineage; no join plan exists. → WC-08.
- **I-9 Bounded ledgers without rollover.** §1.8. → WC-09.
- **I-10 Fungible custody only.** Counted intervals cannot carry per-object condition, maker, repairs
  or heirloom protection, which the spec requires (LANGUAGE_AND_AFTERLIFE.md:41; TD §8:161,165).
  Member condition exists only for construction. → WC-10.
- **I-11 Currency lives in Progression; owner equals custodian.** actors-and-society.md:15–17. Markets,
  estates, institutions and theft need wallets on any custodian and title separate from possession.
  → WC-11.
- **I-12 No persistent effect/condition owner and no declared process phase order** for damage-class
  versus recovery-class processes. → WC-12.
- **I-13 Composition seams can miss a new owner.** RECOVERY_DESIGN.md:102–107 lists five scans that
  must include or reject consumed dispositions; nothing enforces that a new component is joined by
  every reconcile. → WC-14.
- **I-14 Missing causal links (DESIGN only):** evidence→belief, death→estate/office, event→price,
  injury→function→equipment eligibility, exposure→condition, weather→exposure, generation
  witness→feasibility revalidation. Each is a named obligation in the spec; none has an owner.

## 2. Pass 2: scenario challenges

Each scenario states what the WIP does, what the design requires, the coupled/adversarial variants,
and the domain law that must be retained rather than flattened into one scalar system.

### 2.1 Self, NPC and ally treatment; injuries; death

*Self-use at full HP.* IMPL: refused before any expenditure (planner line 36–40). Correct for this
remedy; wrong as a universal rule (a bandage on a bleeding actor at full HP is not a no-op). The
policy belongs to the remedy definition (WC-06).

*Ally at 1 HP, NPC helper with its own Potion, standing party policy.* Required composition: scoped
permission (treat), helper's own stock right (helper authenticates), capability query (manipulation,
reach, delivery mode compatible with patient consciousness), one transaction publishing dose
retirement, HP change, helper readiness debt, encounter eligibility and receipt. IMPL supplies the
dose+HP candidate and authenticates the supplier first; permission, delivery mode, reach, cost and
encounter reconciliation are caller obligations (actor-resources.md:56–61). The care WIP binds a
specific agreement; the general permission relation does not exist (WC-13).

*Incapacitated patient, drinkable remedy.* Design: an HP-zero body is not assumed able to swallow;
delivery mode is content-defined (decision doc, item 1). IMPL: no carrier for delivery mode; the
planner would accept it. Test obligation in WC-06.

*Two helpers, one dose, same batch.* Conflicting selected inputs must resolve in declared order or
reject; IMPL custody revision pins give the second command `RevisionConflict`; canonical actor order
decides. Test: both submitted at the same starting revision; exactly one claim; second receipt is a
refusal; exact retry of either returns its receipt.

*Poison tick and regeneration in one boundary at 3 HP.* No owner exists. Two legal orders give
different worlds (0 then 5 versus 8 then 3). The reference forbids net-arithmetic rearrangement
(world-composition.md:104–105). A versioned phase order is required: damage-class effects before
recovery-class within a boundary, each producing its own ordered resource change, thresholds
evaluated after each change (WC-12). Open decision D-03: does natural regeneration apply at HP 0 at
all, or does incapacity require stabilisation (aid, rest) before passive recovery resumes? Without
hysteresis an actor could oscillate between conscious and incapacitated every interval.

*Death.* Not implemented; HP 0 is incapacity only and targeting excludes HP 0
(encounter-actions.md:71–72). Consequences today: a defeated solo player with no helper is stuck at
Outcome (RECOVERY_DESIGN.md:17–22); no wolf can finish a downed opponent; no bleed-out exists. The
design's `ResolveDeath` (TD §8:141) is a single typed transition with remains custody, revocation,
continuations. Finding WC-05 asks for an explicit life-state facet and a capability evaluator before
any second consumer of "living" is written, and D-02 asks whether attacks on incapacitated actors
are legal (coup de grâce) or whether death arrives only through declared processes.

*NPC heals NPC out of the player's sight.* Same command path with Host controller; the player's view
is unchanged; the journal proves it. Test in WC-15.

*Domain law retained:* vitality restoration is capped additive; it never regrows anatomy, cures a
condition or resurrects (world-composition.md:41–45). Anatomy restoration is a separate typed
operation with its own permission and cost.

### 2.2 Crafting; magic research; effects

*Three berries → Potion → consumed.* IMPL as CAND with exact partition proof (item-consumption.md).
*Researched spell "Regrow Arm".* DESIGN: an accepted discovery becomes an immutable versioned
definition composed of reviewed operations from a finite catalog (TD §8:159). No DSL is needed:
compose typed operations (restore vitality, remove condition, regrow part, transfer custody) with
declared costs; a validator bounds graph size (64 nodes, depth 16). *Enchant a sword twice, remove
the first.* Requires an effect-instance owner whose contributions are individually attributable so
removal subtracts only its own contribution (TD §8:153). No such owner exists; it shares shape with
bleeding/regeneration (source, target, definition, stacking, expiry on the subject's exposure,
fractional carry). This is the second real use that justifies one `EffectInstance` owner (WC-12).

*Adversarial:* a recipe whose output is an ingredient of itself is already rejected by acyclic
lineage (item-consumption.md:63–64). A researched effect that emits a craft that grants XP that
levels up that refills HP is bounded only if level-gain refill is a tracked resource change
(WC-07) and follow-ups are counted against the 256-emission budget.

*Domain law retained:* material intervals are conserved; effects are not material; MP is a pool with
all-or-nothing payment (`SpendMagic`).

### 2.3 Wear, repair, mining and multilevel construction

*Wear.* No item condition exists; tools are fungible counted lots. The spec requires per-object
condition, provenance and repair history (I-10). Proposal WC-10: an individual item is a
never-split quantity-one lot whose identity survives whole transfers (custody already retains the
parent lot ID on whole moves, reservoir-transport.md:59–61) plus an `ItemInstance` facet keyed by that
lot ID. *Repair a member.* `ConstructionMemberState.WithState` can revise condition
(construction-physical.md:33–36) but no operation consumes material into a member. Proposal: a
production operation kind "Maintain" that retires ingredients with a disposition targeting the member
or item instance and revises condition, without retiring the target. This is a natural extension of
the retired-disposition taxonomy (§3.4).

*Mining.* IMPL: owned sites with authored fixed work, whole-cell issuance, direct-support refusal,
harmless settlement in profiles 15–18. DESIGN: hardness, tool power, tool wear, partial spoil
(TD §7:103). The fixed-work adaptation is legitimate but should be recorded as such (WC-19), and tool
wear depends on WC-10.

*Second floor on a first floor with an actor standing during Wait.* Structural support is checked at
every action; collapse is refused, never published (structural-policy-and-motion.md:85–88). During an
encounter, excavation is unavailable (Wait-only interpretation), so the coupling never arises in the
current lineage. The consequence continuation reserves a 1,800 ms interval and assumes flight fits it
(structural-settlement.md:18–21); that assumption has no meaning under 50 ms tactical ticks. This is
the concrete invariant conflict behind WC-08.

*Domain law retained:* member condition is parts per 10,000 scaling rated capacity; mass shares are
integer with canonical remainders; harmless descent ≤ 1 m; none of these are "HP".

### 2.4 Fluids, lava, ice; weather, climate, geological time; ecology

*Water.* IMPL: finite conserved cells, downward before lateral, per-face rates, no relay, terrain
enclosure only, held boundaries earn nothing. *Rain.* IMPL as a funded runoff budget drawn from an
admitted raw source lot and delivered to a receiver with real capacity
(world-and-geography.md:185–227). This is the right shape for weather: an atmospheric custodian with
finite stock issuing scheduled forcing, never minted by a renderer. *Lava meets water.* DESIGN:
reaction stage after transfer stage, reserved reactants, declared outputs (ECOLOGY_AND_FLUIDS.md:43).
*Ice expansion.* DESIGN: reserve displacement before committing the phase change (line 22). Both
require heterogeneous-material composition that the fixed single-material domain explicitly excludes
(world-and-geography.md:314–315); that exclusion is honest and should stay until a second material
exists.

*Geological time.* DESIGN. Geological stages are due work on the branch calendar with their own
rates and immutable causes; they are not a second authority clock. Their only coupling to a held
pocket is admission before moving its floor (GEOLOGICAL_WORLD_DESIGN.md:91). §3.1 explains why this
is a legitimate separate clock without a separate authority.

*Ecology.* IMPL: gathering nodes regrow per process interval from a finite source. DESIGN: cohorts,
food webs, dispersal with source shares and destination capacity. The cohort→individual promotion
rule (reserve and remove the exact share, ECOLOGY_AND_FLUIDS.md:57) is the same partition law as coarse
→ detailed terrain material (world-and-geography.md:505–512). That reuse is a synergy (§3.4).

*Adversarial:* excavating the last sealing cell of a pool: refused before partial work
(terrain-liquid-breach.md:46–48). Two runoff budgets promising the same receiver space: counted once
against all pending (world-and-geography.md:262–266). Weather exposure on actors: no owner (WC-12).

### 2.5 Mounts, pets, vehicles and custody

Nothing is implemented. The model already contains the right primitives: a mount is a `WorldResident`
with its own inventory custodian, body and progression; a vehicle is an assembly of members with
condition and displacement (the physical member owner is the prototype, but its geometry is anchored
to a project origin and the sealing evaluator ignores displacement). Missing: one attachment relation
(rider→mount, cargo→vehicle, tow) whose edge names the coupling law (support and time coupling,
world-composition.md:113–115), a motion frame for parent-local coordinates (TD §7:101), and title
separate from custody (a mule carrying the player's goods is custodian, not owner; WC-11).
*Adversarial:* mule dies while carrying reserved quest wood: remains custody must keep the reservation;
the quest revalidates rather than manufacturing wood. *Streaming:* unloading a boarded ship must
preserve the whole coupled set (TD §7 and time-and-encounters.md:71–73).

### 2.6 Trade, caravans, economies

IMPL: one funded delivery agreement with atomic once-only reward. DESIGN: quotes bind issuer,
quantity, revision, expiry; acceptance atomically revalidates and transfers goods and money
(materials-and-activities.md:147–151). *Two players accept one quote:* allowance consumption inside
the same transaction; second receives a refusal; exact retry returns the receipt. *Caravan lost:*
cargo custody transitions (theft, wreck), contract obligations revalidate, and prices change only
through evidence reaching a market. Prerequisites: wallets on any custodian (WC-11), an activity
commitment for a route with crew roles (WC-13's second family), and evidence records (WC-15).
Currency as counted lots from a mint is a viable alternative that unifies money with material
conservation, at the cost of lot fragmentation; D-05 records the choice.

### 2.7 Relationships, birth, marriage, succession, institutions, war

All DESIGN. The scenario "ruler dies" (content-and-composition.md:72) needs: one `ResolveDeath`
transaction, office mandate survival by policy, estate custody, and belief propagation via
evidence. The adversarial requirement is that no subscriber edits every citizen's mood; the
architecture already prohibits mutating subscribers (world-composition.md:92–93). The concrete
mechanism is missing: an evidence record kind with commit stamp, observer scope and subject, consumed
by planners exactly as `LocalEncounterController` consumes the local view today. War: aggregate
resolvers are recorded before outcome (TD §7:121); the recording is an accepted external decision in
the journal, so replay does not reroll it.

### 2.8 Knowledge, language, culture

IMPL: comprehension bands per actor and channel; utterances in explicit channels; resource
observation scopes. *Co-op relay:* player B's fluent character can relay in fiction; the UI must show
per-principal paraphrases and must not transfer knowledge (LANGUAGE_AND_AFTERLIFE.md:19). The local
view is per session and per controlled actor, which is the right seam; a test must prove two
principals in one world receive different comprehension projections of one utterance. Culture:
DESIGN, mostly content; its mechanical hooks (craft process families, construction grammars) attach
to recipes and construction definitions rather than to a culture facet on actors.

### 2.9 Dreams, planes, time travel

Hooks exist: `TerrainScope` carries realm; `ActorFormBinding` carries a tether anchor;
`History/` request types encode addresses. *Dream entry:* a session realm seeded from session
identity; the avatar is the same actor identity in another realm with a tether; the body stays in the
world on its own exposure; hazards to the body are dependencies of the session's Wait admission
(TD §9:193–195). *Time travel:* a child branch materialises on first local interaction with an exact
prefix; cargo export requires ancestral source intervals whose authority is assigned once by a transit
ledger (TD §9:197–199). *Adversarial fixture (required by the spec):* fork before harvest, harvest
both copies, craft, export both → the ledger refuses the second export; return and retry after crash
returns the same receipt. §3.2 explains the identity scopes this relies on; no implementation is
claimed.

### 2.10 Procedural generation, rejection, progression-path and DC balancing

IMPL: bounded deterministic seed search with refusal on exhaustion (local-worksite.md:19–25);
witnesses for walking corridors only. DESIGN: `GenerationWitness` chains and vector-valued
`ChallengeAssessment` (TD §9:177–179). *Pruning removes a witness's NPC:* revalidate witnesses before
pruning (skill guidance and content-and-composition.md:41–43). *Lived contradiction:* no reroll; report
changed feasibility. Findings: witness retention must be a durable, privileged record that never
enters forecasts; feasibility revalidation should be an ordinary scheduled process keyed to the
witness's dependency revisions. Balance must stay a vector: a rating conditional on capabilities is
not a scalar DC (WC-17 lower-priority).

### 2.11 Active and passive time, Wait, co-op, streaming, distant simulation

IMPL: one coordinated branch clock, dialogue holds, tactical ticks with carry, permanent debt,
no-catch-up host policy. DESIGN: independent co-op pockets, deadlines with one anchor, shared-pause
fallback, merges, release revalidation. *Player A in an encounter pocket while player B's caravan
arrives at A's location:* the caravan submits an admission intent before influence; queued admission
reserves nothing; revalidation at A's next boundary. This needs per-subject exposure anchors (WC-02)
and the coupling law on relation edges. *Distant simulation:* the same actors, batched until the next
interrupt, with an interval owner and last-advanced marker; splitting an interval must give the same
result. That property already holds for the exposure binding (encounter-timing.md:72–73) and must be a
stated per-process property (WC-12 test list). *Streaming:* the current root is a single locality (≤16
regions, ≤64 actors); partitioning it later must keep one home partition per fact and one writer
transaction; §3.3.

### 2.12 UI, animation, audio, perception

IMPL: captured offers with revisions, refusal on stale offers, HP-decrease feedback from observed
projections, no replay of transient UI as commands. DESIGN: performance requests derived from accepted
actions, audio as permitted projection, deduplication on rejoin. *Heal animation plays but the commit
is refused:* the view's HP baseline follows committed projections, so nothing changes; the cue must be
derived from the receipt/observation, not from submission (WC-16 lower-priority). Perception: sight-
complete locality is not a perception model; sound as evidence needs the evidence record (WC-15).

## 3. Pass 3: synergies, complementary structures and explanations

### 3.1 Legitimate separate clocks

| Clock | Owner | Why it is separate | Coupling rule |
| --- | --- | --- | --- |
| Commit sequence | authority | orders changes that advance no time | none |
| Branch calendar | root | historical date, fixed appointments | advances only by accepted exposure |
| Subject exposure | today: pacing (world-wide); design: per subject anchor | needs, recovery, growth, aging apply to the subject that experienced time | edges (attachment, support, active treatment, coupled process) declare when two subjects share exposure |
| Tactical readiness | per encounter timing owner | scheduling debt, not a duration | 5 units per tick contributes 50 ms of exposure to coupled subjects |
| Process boundaries | each process (gathering, liquid, transport, future effects) | processes integrate at their own resolution; some are per-step, not integrable | processes read `ProcessBoundary`/intervals, never the fractional calendar |
| Geological/ecological stages | future stage owners on the calendar | rates differ by orders of magnitude; causes immutable, propagation revisable | admission before affecting held subjects |
| Host monotonic | session | freshness, deadlines, disconnect decisions recorded as external inputs | never enters the core |
| Cosmetic | client | animation | never enters the core |

None of these is a second authority. They differ in *measure* and *subject*, and every one is
published by the same transaction. The only structural change the design demands is moving exposure
from "one world duration for all residents" to "one anchor per subject with declared coupling edges"
(WC-02), because held pockets and uncoupled dependants must diverge.

### 3.2 Identity, knowledge and history scopes

- **Identity scope** is `(world, branch, realm)` plus the distinction between ancestral identity
  (baseline material, generated features, shared across forks) and branch incarnation (a live person
  in one branch). Dream sessions are realms with session identity; a tethered avatar is the same
  actor identity, not a clone; a branch child copies incarnations and retains ancestral references.
  Cross-branch cargo needs the ancestral source interval plus a transit-ledger assignment, which is why
  provenance must stay interval-based rather than quantity-based.
- **Knowledge scope** is per observer principal/actor: projections filter by what the observer can
  perceive or has learned. Today the seam is the per-session local view; the design generalises it to
  evidence records consumed by both UI and NPC planners. Knowledge transfers as learned facts; it
  never rides on custody.
- **History scope** is the commit sequence of one branch with checkpoints as accelerators. Embedded
  component ledgers (last resolution, consumed claims, stage journals) are working sets, not the
  history; the journal is. WC-09 keeps that distinction honest by giving working sets a compaction
  path that references the journal.

### 3.3 Cross-owner atomicity and staged work

One writer, one SQLite transaction, one candidate root composed of private component candidates.
Cross-owner effects are atomic because they are one root publication, not because components call
each other. When closure exceeds the envelope, the existing `PendingExcavation` shows the pattern:
pin the basis, reserve every possible terminal identity, accept zero-time continuation commits,
charge the original interval once at terminal publication, refuse rather than publish a partial
world. That pattern is the correct home for structural collapse, staged floods and the shared-pause
fallback. Recommendation: do not extract a generic `PendingWork` envelope until a second concrete
kind exists (collapse or flood staging); when it does, extract the envelope (basis pin, cursor codec,
identity reservation, journal headroom) and keep the payload typed.

### 3.4 Abstractions justified by two real contrasting uses

| Candidate abstraction | Use 1 (exists) | Use 2 (exists or next) | Verdict |
| --- | --- | --- | --- |
| Ordered resource change (HP/MP/stamina pools) | damage, restoration, MP spend | level-gain refill; passive effects next | Extract (in flight elsewhere); scope to pools only |
| Retired disposition taxonomy | recipe retirement | actor consumption; installed backing; next: repair-into-member, fed-to-mount, burned fuel | Extract: one enumerated disposition kind set with one partition validator |
| Scoped permission relation | care agreement (WIP) | site extraction rights; delegated care; party sharing | Extract when second use lands; keep semantics (treat ≠ extract ≠ act-for) |
| Capability evaluator | availability HP>0, zero debt | encounter legality, delivery-mode compatibility, dialogue counterpart | Extract now: four callers exist |
| Effect instance with attributable contribution | bleeding/regeneration (resource process) | enchantment/modifier (capability) | Extract with passive recovery; contributions are typed per target facet |
| Share partition (coarse→detail) | terrain material refinement | cohort→individual promotion; reservoir→cells | Reuse the interval-partition builder; do not write a second |
| Durable staged work envelope | excavation consequence | collapse or flood staging | Wait for the second use |
| Individual item instance | none yet | wear/repair, heirloom, vehicle part, particular sword | Introduce with wear; as a never-split lot plus facet |
| Evidence record | resource observations (CAND) | political/death news, NPC aid witness, sound | Generalise `Observations/` shape; do not add a message bus |

Rejected: a universal facet registry or property bag; an event bus that mutates authority; per-cell
actors for fluids; event-sourcing every component; a single "Resource" abstraction covering material,
condition, readiness and vitality (their laws differ: intervals conserve, condition scales capacity,
debt schedules, vitality caps).

### 3.5 Domain laws retained

Vitality: capped additive pool changes with ordered evidence. Material: exact interval custody and
partition. Condition: parts per 10,000 scaling declared function; repair consumes material into the
target. Readiness: integer debt paid by ticks; never a duration. Exposure: milliseconds with exact
carry per subject. Liquid: per-face, per-step transfers with no relay; not integrable across steps and
must say so. Structural: integer mass shares, acyclic rated bearings, refusal without a consequence
owner. Permission: scoped relation with revocation; affection is not consent. Knowledge: comprehension
bands and scoped evidence. Life: explicit typed transitions (incapacity, death, form change), never a
threshold alone.

### 3.6 Explainability, testability, authoring gains

Ordered resource evidence makes every HP value explainable by cause (`Explain` in TD §2). A single
capability evaluator gives one place for the UI's "why not" blockers and NPC deferral reasons. A
disposition taxonomy turns conservation into one test family run over every sink. A versioned
process-phase table makes split-interval and pause tests mechanical. Remedy definitions carrying
policy let content authors define bandages, potions and spells without planner edits.

## 4. Findings

Severity: High (blocks a required contract or admits an invalid world), Medium (design debt that
will force a migration if deferred), Low (quality/explainability). Priority reflects ordering in §5.

### WC-01 Last-resolution HP equality makes a past result the owner of current HP

- **Scenario.** Wolf reduces the player to 0 HP; a helper restores 40 HP; the next complete
  admission compares the retained lethal resolution's post-damage HP with current HP and refuses,
  even though the phase was reconciled. Level-gain refill after damage triggers the same refusal.
- **Evidence.** RECOVERY_DESIGN.md:29–35; encounter-actions.md:17–24 (action record "describes an
  actual change; it does not create another health ledger"); WORLD_COMPOSITION_DECISION.md:57–59,
  84–87; `ActorProgressionState.cs:84–86` (level-gain refill).
- **Status.** IMPL seam in profiles 23/24. Ordered resource evidence is WIP by another worker (not
  inspected).
- **Resolution.** Keep the resolution record as history (it is true at its sequence). Replace the
  equality check with: the actor's current HP equals the value recorded by its latest ordered resource
  change, whatever the cause, and every writer (damage, restoration, MP spend, level gain, later
  passive effects) appends one. The resolution record references the change revision it produced.
  Version the admission interpretation (new profile) rather than removing the check from old profiles.
- **Alternatives / objections.** (a) Keep the equality check and add an explicit "supersession
  witness" per treatment: rejected by the decision as one-off. (b) Drop the check entirely: rejected;
  replay must still prove the sequence. (c) Concern: the evidence log becomes a second HP ledger.
  Mitigation: it stores deltas and revisions, never a current value that could disagree with the
  progression owner; a test must fail if a change record can exist without the matching progression
  revision.
- **Tests.** (1) Damage → aid → admission passes, replay reproduces. (2) Damage → tampered current HP
  without a change record → admission and replay refuse. (3) Damage → XP reward with level gain →
  one change record with cause LevelGain; without level gain → no record. (4) Exact retry of the aid
  command returns the original receipt with no second change. (5) Profile-24 bytes remain admitted
  under their old interpretation.
- **Affected.** `Worlds/WorldEncounterActionAdmission.cs`, `Worlds/WorldEncounterActionProcessor.cs`,
  `Progression/ActorProgressionState.cs`, `Progression/ActorRewardPlanner.cs`,
  `Encounters/EncounterActionSnapshot.cs`, new world profile/admission; docs `actor-resources.md`,
  `encounter-actions.md`.
- **Phase.** Current care increment. **Severity/priority.** High / 1. **Open decisions.** D-01.

### WC-02 Exposure is stored twice and the design needs per-subject divergence

- **Scenario.** Independent co-op: player A held in a pocket, B farming; A's mule is uncoupled and
  must hunger; A must not. A rider on a ship shares the ship's exposure. Today no resident can have a
  different experienced duration from the world.
- **Evidence.** world-pacing.md:5–9; `WorldResident.cs:71–74`; persistence-and-history.md:137–139
  ("distinct but exactly reconciled"); TD §5:74–81; time-and-encounters.md:62–73.
- **Status.** IMPL duplicate with admission equality; divergence is DESIGN.
- **Resolution.** Now: state in `world-pacing.md` and in an admission test that the pacing exposure is
  canonical and resident exposure is a derived copy that must equal it. Next: introduce
  `ExposureAnchor` per subject (actor, coupled process, group) referenced by residents and effects,
  with coupling edges on attachment/support/treatment relations. Ship it as an explicit profile; a
  lived coordinated save keeps its interpretation.
- **Alternatives / objections.** Keep one world clock until multiplayer: cheaper, but passive recovery
  and dependants then get built on the wrong subject and need rewriting. Alternative: keep per-resident
  duration as canonical and derive world exposure as the maximum: wrong for holds.
- **Tests.** (1) Existing: admission rejects a resident whose duration differs from pacing (verify
  present). (2) Future: held subject earns no exposure while the branch advances; release adds none
  retroactively. (3) Split-interval equivalence for every process bound to an anchor. (4) Coupled
  rider/ship anchors advance together; detaching stops coupling at the boundary.
- **Affected.** `Worlds/WorldPacingState.cs`, `Worlds/WorldResident.cs`, `Exposure/*`,
  `Encounters/EncounterExposureBinding.cs`; docs `time-and-encounters.md`, `world-pacing.md`.
- **Phase.** After care; before passive recovery, rest and independent co-op. **Severity/priority.**
  Medium now, High later / 4. **Open decisions.** D-04.

### WC-03 Readiness debt is duplicated between timing rows and the lifecycle ledger

- **Scenario.** Restart during Active: which copy is truth if they disagree? A forged lifecycle ledger
  with lower debt but intact timing rows.
- **Evidence.** encounter-lifecycle.md:78–79; encounter-timing.md:12–15.
- **Status.** IMPL copy-in/copy-out. Whether admission checks equality during Active is uncertain
  (not verified in source).
- **Resolution.** Declare the timing owner canonical while an episode is Active and the ledger
  canonical otherwise; add (or confirm) an admission invariant that during Active the ledger equals
  the timing rows for every actor, and that release copies exactly once. Document in the reference.
- **Tests.** Round-trip entry/active/release preserves per-actor debt; tampered ledger during Active
  refuses; bystander debt survives.
- **Affected.** `Encounters/EncounterLifecycleSnapshot.cs`, `Worlds/WorldEncounterLifecycleAdmission.cs`.
- **Phase.** Care increment (cheap). **Severity/priority.** Low–Medium / 6.

### WC-04 Body condition is owned by spatial presence

- **Scenario.** A serious injury (lost grip) must change equipment eligibility and actions, survive
  branch travel and be treated by a restoration spell. Today the injury lives in `ActorPresence`, whose
  revision also changes on every move, so an injury command would pin a revision that movement
  invalidates, and movement would invalidate a pending treatment.
- **Evidence.** `ActorPresence.cs:7–38`; actors-and-society.md:12–15; TD §8:131.
- **Status.** IMPL placement; injuries as gameplay are DESIGN.
- **Resolution.** Introduce an `ActorBodyState` facet (form binding, injuries, functions) with its own
  revision; `ActorPresence` keeps pose and pins the body revision. New spatial codec version; earlier
  profiles keep bytes.
- **Objection.** Premature until injuries exist. Counter: the care increment already reasons about
  "functional manipulation" and delivery mode; doing it on presence revision now bakes the coupling in.
  Compromise: do not move the field now; add the split to the injury increment's entry criteria.
- **Tests.** Injury change does not alter feet/presence revision semantics; movement does not alter body
  revision; treatment pins body revision only.
- **Affected.** `Spatial/ActorPresence.cs`, `Spatial/LocalSpatialSnapshot.cs`, `Actors/*`, spatial codec.
- **Phase.** Injury/restoration increment (SYN-08). **Severity/priority.** Medium / 8.

### WC-05 No life-state or capability facet; "living" is re-derived in four places; incapacitated actors are immortal and stuck

- **Scenario.** Solo player at 0 HP with no helper: Outcome holds forever (no death, no bleed-out, no
  targeting). A wolf cannot kill; an ally cannot decide to end suffering; poison cannot progress. When
  death and unconsciousness arrive, four callers must change consistently.
- **Evidence.** RECOVERY_DESIGN.md:17–22; encounter-actions.md:69–78; encounter-lifecycle.md:83–88;
  world-composition.md:41–45; TD §8:141.
- **Status.** IMPL threshold rule; life state DESIGN.
- **Resolution.** (1) Now: one `ActorCapabilityQuery` (living, conscious, can act, can move, can
  manipulate, can swallow, reach) used by availability, encounter legality, lifecycle, dialogue and
  treatment, returning typed reasons. (2) Next: explicit `LifeState` facet (Alive, Incapacitated,
  Dead, Transitioned) changed only by typed transitions; `ResolveDeath` as one transaction with remains
  custody. (3) Decide D-02 (attacks on incapacitated) and D-03 (recovery threshold/hysteresis).
- **Alternatives.** Keep deriving from HP with a shared helper only: acceptable for the care increment;
  insufficient once poison or bleed-out exists.
- **Tests.** Parity test enumerating every caller against the evaluator; HP0 + regeneration
  oscillation test under the chosen D-03 rule; death transition once-only with exact retry; targeting
  an incapacitated actor per D-02.
- **Affected.** `Worlds/WorldActorAvailability.cs`, `Worlds/WorldEncounterActions.cs`,
  `Worlds/WorldEncounterLifecycleProcessor.cs`, dialogue/give planners, care WIP.
- **Phase.** Evaluator in care increment; life state with passive effects. **Severity/priority.**
  High (design) / 3.

### WC-06 Remedy policy hard-coded in the planner; no typed delivery mode

- **Scenario.** Bandage at full HP (no-op should still be refusable by definition), Potion poured into
  an unconscious mouth (refuse), salve applied externally (allow), spell (no manipulation).
- **Evidence.** `RestorativeItemPlanner.cs:36–40`; world-composition.md:86–88;
  WORLD_COMPOSITION_DECISION.md:49–53; actor-resources.md:59–61.
- **Status.** IMPL policy in planner; `RestorativeRemedyDefinition` WIP not inspected.
- **Resolution.** The remedy definition carries `NoEffectPolicy` (RefuseBeforeCost | SpendAttempt),
  `DeliveryMode` (Ingest | External | Magical), anatomical requirement, and the restorative amount pin;
  the planner reads them and the capability query checks compatibility with the patient's state.
- **Tests.** Definition variants; unconscious + Ingest refuses with typed reason; External passes;
  amount bound to definition version (stale version refuses).
- **Affected.** `Care/RestorativeItemPlanner.cs`, `Care/RestorativeRemedyDefinition.cs`.
- **Phase.** Care increment. **Severity/priority.** Medium / 2.

### WC-07 Level-gain refill is an untracked resource writer

- **Evidence.** `ActorProgressionState.cs:73–87`; WORLD_COMPOSITION_DECISION.md:61–63.
- **Status.** IMPL. **Resolution.** Route the refill through the ordered resource change with cause
  LevelGain, governed by a profile flag `RefillOnLevelGain`; keep the single actor revision.
  **Tests.** See WC-01 test 3; refill respects any future effective-maximum modifier (WC-12).
  **Affected.** `Progression/ActorProgressionState.cs`, `Progression/ActorRewardPlanner.cs`.
  **Phase.** Care increment. **Severity/priority.** Medium / 2.

### WC-08 Two mutually exclusive profile lineages with no join plan

- **Scenario.** A "complete world" with mining beneath an occupied floor during a paused encounter,
  water in a breached trench, and care. No profile admits physical members and pacing together.
- **Evidence.** persistence-and-history.md:30–60 (profile table); liquid-cell-flow.md:61–66;
  terrain-liquid-breach.md:29–32; reservoir-containment.md:22–25; structural-settlement.md:18–21
  (flight must fit 1,800 ms); consequence-continuation.md:12–23 (reserved interval, zero-time
  continuations); world-pacing.md:71–74 (Wait excludes labour).
- **Status.** IMPL guards; join is GAP.
- **Resolution.** Publish a join plan before extending either lineage further: (1) profile "24 +
  physical members + structural policy", collapse still refused, no continuation; (2) then the
  consequence continuation under pacing with an explicit rule: pending work during an episode is an
  admission dependency of the encounter (it cannot run on exploration intervals while ticks advance);
  (3) then detailed flow with construction sealing faces. Each step lists the invariants it relaxes.
- **Objections.** Joining now delays care. Agreed; the ask is a written plan and a genesis-join test
  scaffold, not the implementation.
- **Tests.** Join genesis admission from an unplayed 24 basis; every existing exclusion guard has a
  named test that must be retired deliberately; excavation with a rider during Wait refuses with a
  typed reason.
- **Affected.** `Worlds/WorldSnapshot.cs` constructor guards, new admission/codec, `WorldStepProcessor`.
- **Phase.** After care; before any "complete world" claim. **Severity/priority.** High / 5.

### WC-09 Bounded embedded ledgers have no provenance-preserving rollover

- **Scenario.** A long campaign consumes the 257th Potion; the world refuses. A transport network runs
  its 4,097th stage.
- **Evidence.** item-consumption.md:99; world-and-geography.md:281–283; reservoir-transport.md:87–90;
  world-composition.md:153–157.
- **Status.** IMPL bounds; rollover GAP.
- **Resolution.** A versioned compaction operation per ledger: seal the oldest claims into a
  "coverage witness" (exact interval union per source/output, count, hash of the sealed archive) held in
  the root, archive the sealed claims in a checkpoint-linked archive, and validate partitions against
  witness + live claims. The journal remains the history; compaction is an ordinary commit kind.
- **Alternatives.** Raise bounds: postpones, breaks the 16 MiB reserve. Move claims wholesale to the
  journal: loses cheap partition validation.
- **Tests.** 300 consumptions across a rollover with exact partition; forged witness refuses; replay
  through the compaction commit; re-consumption of a sealed interval refuses.
- **Affected.** `Production/ProductionClaimValidation.cs`, `ProductionCodecV2.cs`, capacity/transport
  codecs, `WorldDatabaseSchema*`, `WorldJournalPolicy.cs`.
- **Phase.** Before endurance qualification (P04). **Severity/priority.** Medium now, High later / 9.

### WC-10 Individual items need identity, condition and provenance beyond counted lots

- **Evidence.** LANGUAGE_AND_AFTERLIFE.md:41; TD §8:161,165; construction-physical.md:25–36 (only
  condition facet); reservoir-transport.md:59–61 (whole moves keep lot ID).
- **Status.** GAP. **Resolution.** `ItemInstance` facet keyed by a never-split quantity-one lot ID:
  condition (parts per 10,000), maker, repairs, protection; production operation "Maintain" retires
  materials with disposition target = instance/member and revises condition. Tool wear debits condition
  per work action under the tool definition. **Tests.** Whole transfer keeps ID and facet; split
  refuses; repair retires material once and does not retire the instance; wear survives save.
  **Affected.** `Custody/CountedSourceLot.cs`, `Custody/CustodyTransferPlanner.cs`, `Production/*`,
  `Construction/ConstructionMemberState.cs`. **Phase.** Wear/repair increment. **Severity/priority.**
  Medium / 10.

### WC-11 Currency inside Progression; custody conflates owner and custodian

- **Evidence.** actors-and-society.md:15–17; encounter-lifecycle.md:122–124 (wallet identities per
  actor); materials-and-activities.md:147–151; TD §8:141,145,149.
- **Status.** IMPL placement; markets DESIGN. **Resolution.** Before the two-market proof: a wallet
  ledger keyed by any custodian identity (actors, shops, institutions, estates), transferred in the
  same transaction as goods; later an ownership-claim relation distinct from custody for estates,
  theft and lending. **Alternatives.** D-05: coin as counted lots from a mint source.
  **Tests.** Institution wallet; atomic goods+money exchange with refusal on either side; remains
  custody keeps coins on death. **Phase.** Trade increment. **Severity/priority.** Medium / 11.

### WC-12 No persistent effect owner and no declared process phase order

- **Scenario.** §2.1 poison/regeneration; enchant twice/remove one; weather exposure; held pocket must
  not expire effects.
- **Evidence.** world-composition.md:96–105; WORLD_COMPOSITION_DECISION.md:73–77; TD §8:153,165;
  materials-and-activities.md:110–114; runtime-and-authority.md:101–103 (current order).
- **Status.** DESIGN. **Resolution.** `EffectInstance` owner: source, target facet, definition pin,
  stacking rule, exposure anchor, expiry, fractional carry, attributable contributions. A versioned
  process-phase table per profile: actions → calendar → due stages → damage-class effects → recovery-
  class effects → liquid → ecology → movement, with per-effect order (phase, definition ID, instance
  ID). Each effect emits its own ordered resource change; thresholds evaluate after each. Declare per
  process whether it is integrable (split-interval equal) or per-step.
- **Tests.** Poison+regen ordering fixture; two enchantments, remove first; expiry frozen during hold;
  split-interval equivalence for integrable processes; per-step declaration test for liquid.
- **Affected.** new `Effects/` owner; `WorldStepProcessor`; `WorldPacingState`; docs
  `time-and-encounters.md`, `world-composition.md`. **Phase.** Passive recovery increment.
  **Severity/priority.** High / 7. **Open decisions.** D-03.

### WC-13 Scoped permission relation

- **Evidence.** WORLD_COMPOSITION_DECISION.md:26–44; excavation.md:30; world-composition.md:47–51;
  RECOVERY_DESIGN.md:46–49.
- **Status.** WIP one-off. **Resolution.** `InteractionPermission` (grantor, grantee, kind, subject,
  resource scope, revision, expiry/revocation) in a relations owner; first use the care agreement,
  extract when the second (site rights, delegated caregiver or party sharing) lands. Permission never
  spends the grantee's or grantor's stock by itself; a standing policy lets an NPC initiate.
- **Tests.** Revoked before commit refuses; permission plus no stock refuses; NPC initiates under
  policy without player confirmation; exact retry; permission does not grant control or custody.
- **Phase.** Care (first use); extraction later. **Severity/priority.** Medium / 3.

### WC-14 Publication checklist and admission coverage

- **Evidence.** RECOVERY_DESIGN.md:102–107; §1.7.
- **Status.** IMPL scattered. **Resolution.** One ordered, versioned reconciliation list per profile in
  code, and a coverage test that fails when a root component has no reconcile participant or when a new
  disposition kind is not seen by every scan (excavation coverage, runoff funding, registry, admission).
  Document the order once in `runtime-and-authority.md`.
- **Tests.** Add a fake component/disposition in a test build → checklist rejects. **Phase.** Care
  increment (V2 admission). **Severity/priority.** Medium / 2.

### Lower-priority opportunities

- **WC-15 Evidence records.** Generalise `Observations/` into typed evidence with commit stamp,
  subject and observer scope; NPC planners and UI consume only evidence. Test: NPC aids NPC unseen;
  the player's view is unchanged; the journal proves it. Phase: society/trade.
- **WC-16 Presentation cue deduplication.** Derive performance requests from receipts/observations
  keyed by (command ID, phase); rejoin suppresses duplicates. Phase: native care acceptance.
- **WC-17 Balance as vector.** Keep `ChallengeAssessment` vector-valued; store witnesses as privileged
  durable records revalidated by dependency revision. Phase: generation.
- **WC-18 Vehicles as mobile assemblies.** Reuse physical member owner with a motion frame and
  displacement-aware geometry; attachment relation with coupling law. Phase: travel.
- **WC-19 Record the fixed-work mining adaptation** against TD §7 hardness/tool wear; define the
  migration trigger (tool condition exists). Phase: wear.
- **WC-20 RNG stream registry.** Each domain owner embeds its own counter; document the rule "one
  stream per (domain, responsible identity), persisted with its outcome" and test that no two owners
  share a domain tag. Phase: any new randomness consumer (loot, research).

## 5. Prioritized integration plan

| Step | Findings | Depends on | Test obligations | Migration / compatibility risk |
| --- | --- | --- | --- | --- |
| A. Care increment (profile 24 lineage, new profile) | WC-01, WC-06, WC-07, WC-14, WC-03 check, WC-13 first use, WC-05 evaluator | ordered resource evidence (in flight) | WC-01 tests 1–5; remedy policy variants; parity evaluator test; checklist coverage; conservation across all scans | New profile only; old profile-24 bytes keep the equality interpretation; 16 MiB reserve must include the new owner |
| B. Passive effects | WC-12, WC-02 anchor, WC-05 life state, D-03 | A | phase-order fixture; hold freezes expiry; split-interval; oscillation | Exposure anchor is a root change → new profile; lived coordinated saves unchanged |
| C. Structural join | WC-08 | A (not B) | join genesis; guard-retirement tests; Wait+excavation refusal | Codec field positions; schema kinds; consequence reservation semantics under ticks |
| D. Materials and economy | WC-09, WC-10, WC-11, WC-19 | A | rollover partition; instance identity; wallet atomicity | Production V3 codec; journal commit kind for compaction |
| E. Later | WC-04, WC-15–18, WC-20 | B–D as noted | as listed | Spatial codec version; relations owner |

Every step follows existing doctrine: fresh explicit profile, no lived-save migration, exact
re-encoding, reserved headroom, exclusive writer, replay before readiness.

## 6. Open decisions

| ID | Question | Recommendation | Revisit trigger |
| --- | --- | --- | --- |
| D-01 | Does the resolution record keep post-damage HP as a field? | Yes as history; admission uses the change revision | If replay cost of change records exceeds envelope |
| D-02 | Are attacks on incapacitated actors legal? | Illegal by default; a declared "finish" action with its own targeting rule and consequences | When death is implemented |
| D-03 | Does passive recovery apply at HP 0? | No; stabilisation (aid or completed rest stage) precedes regeneration; hysteresis avoids oscillation | Playtest of defeat pacing |
| D-04 | Per-subject exposure anchors now or with co-op? | With passive effects (step B), because effects need the right subject | If passive effects are deferred past co-op |
| D-05 | Coin as counted lots or wallet ledger? | Wallet ledger first | If mint budgets, counterfeit or coin provenance become content |
| D-06 | Level-gain refill retained? | Yes as a profile rule routed through ordered evidence | If effective-maximum modifiers arrive |
| D-07 | Extract a generic staged-work envelope now? | No; wait for collapse or flood staging | Second concrete pending kind |
| D-08 | Split body state from presence before injuries? | No; make it the injury increment's entry criterion | First injury feature |

## 7. Objections and alternate resolutions

- **Too much abstraction for one Potion.** Only WC-01, WC-06, WC-07, WC-14 and the evaluator are asked
  of the care increment; the rest are plans with entry criteria.
- **Ordered evidence for every writer is event sourcing by another name.** It is not: it records
  deltas for pools only, the owner keeps the value, and the journal remains the history. The guard
  test (a change record without a matching owner revision must be impossible) keeps it from becoming
  a parallel ledger.
- **Process phase table is a global transaction for all society.** It orders processes inside one
  publication of one branch step; it does not couple distant subjects, whose anchors and edges decide
  coupling.
- **Per-subject anchors are premature.** Possible; the cost of building passive effects on a world-wide
  duration and then re-subjecting them is the counter-argument.
- **Coverage-witness compaction weakens provenance.** The sealed archive remains in checkpoints and the
  journal; only the live root loses per-claim detail. If the owner rejects any loss of live detail,
  the alternative is raising bounds with an explicit endurance test that shows the 16 MiB reserve
  still holds.
- **Profile join is not a composition problem but a scheduling one.** Both: the excluded-owner guards
  are composition facts, and the reserved-interval semantics under ticks is a scheduling rule; the
  plan names both.

## 8. Coverage matrix

| Domain | IMPL | CAND/WIP | DESIGN | Findings |
| --- | --- | --- | --- | --- |
| Self/NPC/ally treatment | dose+HP candidate; supplier auth | care owner (WIP) | permission, delivery, activity | WC-01, 05, 06, 13 |
| Injuries, death | body functions, incapacity | — | injury records, ResolveDeath, afterlife | WC-04, 05 |
| Crafting | instant craft, V2 consumption | — | process graph, research | WC-09, 14 |
| Magic research/effects | MP spend, abilities | — | effect instances, research | WC-12 |
| Wear, repair | member condition | — | item condition, Maintain | WC-10, 19 |
| Mining | owned sites, settlement | — | hardness, wear, collapse | WC-08, 19 |
| Multilevel construction | footing, stairs, loads | — | upper-floor work, collapse | WC-08 |
| Fluids | water cells, gates, breach | runoff budgets | lava, ice, reactions | (2.4) |
| Weather/climate | funded runoff | — | forcing, exposure | WC-12 |
| Geological time | — | terrain candidates | stages, history | (3.1) |
| Ecology | node regrowth | — | cohorts, dispersal | (3.4 partition reuse) |
| Mounts/pets/vehicles | — | — | attachment, assemblies | WC-18, 11 |
| Custody | counted intervals | — | title vs custody | WC-10, 11 |
| Trade/caravans/economy | delivery quest | — | markets, routes | WC-11, 13, 15 |
| Relationships/lifecycle | — | — | edges, birth, marriage, succession | WC-05, 15 |
| Institutions/war | — | — | mandates, resolvers | WC-15 |
| Knowledge/language/culture | comprehension, utterances | resource observations | evidence, culture | WC-15 |
| Dreams/planes | realm scope, tether field | history types | sessions, transit | (3.2) |
| Time travel | recorded commit reads | history types | branches, export ledger | (3.2), WC-20 |
| Generation/rejection | bounded seed search | — | witnesses, pruning | WC-17 |
| Progression/DC | stat profiles, XP | — | vector assessment | WC-07, 17 |
| Active/passive time | pacing, ticks, carry | — | anchors, phase order | WC-02, 12 |
| Wait/co-op | coordinated Wait | — | pockets, merges, deadlines | WC-02, 03 |
| Streaming/distant | single locality | — | residency, batching | (2.11, 3.3) |
| UI/animation/audio/perception | offers, views, feedback | — | cues, audio, perception | WC-16, 15 |

## 9. Uncertainties and non-claims

- Whether admission already checks lifecycle-ledger versus timing-row debt equality (WC-03) was not
  verified in source.
- The care WIP types may already carry delivery mode or policy fields (WC-06); they were not read.
- Test project layout beyond `tests/OpenFantasy.Hosting.Tests/Production/RestorativeSupplyTests.cs`
  was not inspected; test placements in FINDINGS.json are proposals.
- No claim is made that any system beyond the tagged IMPL bindings exists, that any test passes, or
  that the ordered resource evidence in flight satisfies WC-01; those remain Astra's integration
  checks and the independent critic's review.
