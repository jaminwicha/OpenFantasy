# Independent challenge review of the world-composition refinement

Date: 2026-09-25. Reviewer: Claude `fable[1m]`, Max effort, independent critic. Inputs: the three
refinement files, the decision record, `world-composition.md`, `actor-resources.md`, the current
handoff, `REQUIREMENTS.json` (all 181 rows), `IMPLEMENTATION_PLAN.md`, `TECHNICAL_DESIGN.md`,
`CROSS_SYSTEM_SYNERGIES.md`, and targeted source in `Worlds/`, `Care/`, `Progression/`, `Actors/`,
`Custody/`, `Production/`, `Exposure/`, `Observations/`. Not inspected: `Progression/ResourceHistory/`
and its tests (another worker), Hosting session/client code, per-test bodies. No build, test or Git
operation was run; nothing below is a pass claim. Machine-readable companion:
[REVISED_FINDINGS.json](REVISED_FINDINGS.json).

Classification used: **BUG** (implemented defect), **CONSTRAINT** (explicit documented profile rule),
**PLANNED** (obligation in spec/plan without an owner yet), **SPECULATION** (neither), **DOC**
(documentation drift only).

## 1. Corrections that change the plan

Five source facts contradict the analysis and must govern integration.

1. **The HP-equality seam is live only inside an episode.** `WorldSnapshot.EncounterActions` returns
   the lifecycle archive only while `HasEncounter` is true (`WorldSnapshot.cs:284-285`); after release it
   is null and `WorldEncounterActionAdmission.Validate` returns immediately (`:36`). Ordinary actions,
   including quest delivery and its XP reward, are unavailable during the Wait/ability interpretation
   (`world-pacing.md:71-74`). Therefore the analysis's "level gain after damage triggers the same refusal"
   is unreachable today. The seam blocks exactly one planned writer, aid during Outcome, and the care WIP
   already handles that through an exact-resolution witness (`WorldCareAdmission.ExpectedHpAfterLastAttack`,
   `:111-126`). WC-01 is a CONSTRAINT plus a design obligation, not a BUG, and the "supersession witness"
   the analyst lists as rejected is what the tree currently implements.
2. **Unchanged-resource XP rewards advance the actor revision.** `GainExperience` constructs a new
   candidate with `Revision + 1` unconditionally (`ActorProgressionState.cs:84-86`); only the three
   resource operations return the original actor on zero delta (`:68-69`). The analysis's test
   "without level gain, no record" would create a revision gap. The lead's objection 1 is verified.
3. **Both reconciled copies are enforced equal at admission.** Resident exposure must equal pacing
   progress (`WorldPacingAdmission.cs:40`); during any episode each timing row's `RemainingDelay` must
   equal the lifecycle ledger (`WorldEncounterLifecycleAdmission.cs:47-59`). Per-resident exposure is
   also a consumed witness: excavation and construction bound each owner's work by that resident's own
   experienced time (`ExcavationSnapshot.cs:87`, `ConstructionSnapshot.cs:72`). WC-02 and WC-03 describe
   witnesses, not duplicated authority. Lead objection 2 verified.
4. **Lineage acyclicity is a batch check, not a recipe-definition check.** `ValidateAcyclic` walks
   batch/lot dependencies (`ProductionClaimValidation.cs:64,158-167`). A recipe definition whose output
   item is also its ingredient is not rejected there; that is the P01 compiler obligation ("rejects illegal
   cycles", plan P01 exit; TD §8:159 "cycles require explicit bounded iteration"). Lead objection 4
   verified; the analysis's §2.2 adversarial claim is wrong.
5. **Item protection and identity partly exist.** `LotProtection` carries Equipped/Favorite/Heirloom/
   UserReserved and follows both halves of a split (`CustodyTransferPlanner.cs:65-71`); a whole move keeps
   the lot ID (`:51`); a quantity-one lot cannot be split. What is missing is per-object condition, maker
   and repair history, and identity continuity through transformation, which retires the input and mints
   an output. The analysis's "cannot carry heirloom protection" is wrong; its condition/provenance gap is
   right.

Two further corrections: the admission validators already run in one fixed order
(`WorldSnapshot.cs:232-248`), so WC-14's "no ordered checklist" is only true of the step reconcile; and
the fixed-work mining adaptation is already recorded in the reference (`excavation.md:16-17`), so WC-19
reduces to naming the migration trigger.

## 2. Verdicts on WC-01 to WC-20

| ID | Verdict | Class | Correction and required shape |
| --- | --- | --- | --- |
| WC-01 | Refine | CONSTRAINT + PLANNED | Keep the resolution's own `TargetHpAfter` equality for profiles 23/24 bytes. For the new profile, admission checks a gap-free per-actor change chain: every actor revision advance has exactly one entry, entries carry cause kind and a resolvable cause reference (decision command, consumption ID, reward definition), `before` of entry n equals `after` of entry n-1, and the current value equals the last `after`. This is structural consistency; only full replay authenticates. Do not compare "latest value" alone (`actor-resources.md:70-74`, `RECOVERY_DESIGN.md:34-35`). Zero-delta entries are legal and required. |
| WC-02 | Reject as defect; accept obligation | PLANNED | Keep `ExperiencedTime` and the equality invariant in the coordinated profile. Per-subject anchors are the P03 independent co-op obligation (RQ-105, RQ-121; TD §5 table; `time-and-encounters.md:63-66`). When anchors arrive, the invariant becomes "resident exposure equals its anchor's progress and never exceeds branch exposure"; the work-bound consumers already read the resident's own exposure. |
| WC-03 | Reject | DOC | Equality is enforced whenever an episode exists, not only during Active. Add one sentence to `encounter-lifecycle.md:78-79`. A tampered-ledger regression is worth keeping if absent; presence not verified. |
| WC-04 | Refine | PLANNED | Split body condition from presence only with the injury increment (RQ-150, P03 intro → P12). Treatment pins presence (reach), body (anatomy), progression, storage and production revisions, as `AdministerRemedyIntent` already does (`:9-20`); "pins body revision only" is unsafe. Lead objection 3 verified. Anatomy types exist (`BodyInjury` Scar/MissingPart/FunctionLoss with `CauseId`; `BodyConditionSnapshot` function derivation); the missing pieces are injure/heal transitions and a body revision. |
| WC-05 | Refine | CONSTRAINT + PLANNED | HP > 0 is derived in at least seven core sites (availability, encounter actions ×3, pacing admission, construction planner, care actions/admission) plus hosting views. Extract composable primitives with typed reasons: `IsIncapacitated` (resource rule of this profile), `SupportsFunction` (body), `IsReady` (debt), `IsHeld`, `FormKind`. Each action composes them; do not add a universal `Alive` enum or `CanBeTargeted`. Targeting HP0 is illegal for hostile abilities by profile rule (`WorldEncounterActions.cs:43`) and required for care; a spirit form (`ActorFormKind.IndependentSpirit`, `SpectralMotion`) is not "dead". "Immortal and stuck" is a documented CONSTRAINT (`encounter-actions.md:69-79`); death is P03 (plan: "Exercise death with real custody..."; RQ-132/174/175). Lead objection 5 verified. |
| WC-06 | Refine | PLANNED (one field now) | `RestorativeRemedyDefinition` carries only item, restored HP and range (`:9-25`); helper checks Manipulation only and the patient is HP0 by construction (`WorldCareActions.cs:79-85`). The decision explicitly requires a content-defined delivery mode (item 1), so add `DeliveryMode` and the swallow/anatomy compatibility check now. Keep no-effect refusal as this remedy's planner rule until a second remedy with a different policy exists; do not build a medical dispatcher. Lead objection 6 verified. |
| WC-07 | Accept | PLANNED | Level-gain refill and the no-level revision advance both route through the ordered change contract. Drop the proposed `RefillOnLevelGain` flag: it is a new policy with no requirement; the refill is the admitted profile rule. Revisit when effective-maximum modifiers arrive (RQ-117, P05). |
| WC-08 | Refine | PLANNED gap | Exclusions verified (`WorldSnapshot.cs:180-193, 213-218`): wet requires terrain-only ground support; physical members require stepped support; seals exclude every structural owner; care requires profile 24. P02 exit requires tunnel-under-occupied-floor, upper floor, flowing water and a reaction in one loop (plan P02), so the join is required work, not optional. Correct the ordering: first join constructed footing with the wet envelope (bodies and members stay outside wet volumes, `terrain-liquid-breach.md:30-31`), then physical members under pacing, then consequence continuation under Wait. The 1,800 ms flight versus 50 ms ticks is not a present contradiction: labour is unavailable during Wait, and outside consequences must request admission before influence (TD §6:89, `GEOLOGICAL_WORLD_DESIGN.md:91`). Lead objection 9 verified: a written plan is not a claim of ease. |
| WC-09 | Refine | PLANNED | Bounds verified (256 claims, 256 care ops, 256 injuries, 4,096 closed reservations/stages). TD §4:60 and RQ-141/054 require lossless compaction. Union-plus-hash is insufficient: sealed intervals keep per-interval identity and source lineage (cross-branch export assigns authority per interval, TD §9:199), stay addressable through `HistoryAddress`, and are authenticated by replay as a typed commit kind like the existing upgrade transitions. Partition validation runs over live plus sealed intervals. Lead objection 8 verified. |
| WC-10 | Refine | PLANNED | Identity continuity rules: whole transfer keeps ID (exists); fitting into an assembly is an attachment relation, never a transformation; repair retires consumables with disposition "into target" and revises the target's condition without retiring it; cross-branch travel carries the lot's source interval (exists). Condition, maker and repair history are new facets keyed by lot ID. RQ-131, P04. |
| WC-11 | Refine | PLANNED | The ledger already has non-actor accounts (`CurrencyAccountKind.Reserve`, `ActorRewardPlanner.cs:44-48`). Add account kinds for shops/institutions rather than moving the ledger. Title separate from custody arrives with death/estate (P03) and theft (P07). RQ-137 two-market circuit is P02. Defer D-05. |
| WC-12 | Refine | PLANNED | No effect owner exists; the step order is actions → time → gathering/transport/liquid → movement (`WorldStepProcessor.cs:391-444`, docs). The reference forbids net-arithmetic rearrangement; it does not choose damage-before-recovery. Ordering is an owner decision (D-03/D-09). Integrable processes must satisfy split-interval equality (already true for exposure bindings, `encounter-timing.md:72-73`); liquid flow is per-step by law and must be declared so. First passive resource writer is P03 rest recovery (RQ-126), before regeneration/bleeding (P04/P05, RQ-117/131). Lead objection 7 verified. |
| WC-13 | Accept | PLANNED | `CareAgreement` is a one-off with no scope, revocation or expiry (`:8-25`) and requires a side-0 helper. Extract a permission relation on the second real grant (delegated care or party sharing, P04 RQ-116/127). Site ownership is an entitlement, not a grant; it is not the second use. |
| WC-14 | Refine | DOC + test | Admission order exists (`WorldSnapshot.cs:232-248`). Add the coverage tests: a root component without a validator participant fails; a new retired-disposition kind unseen by any of the five scans fails. |
| WC-15 | Accept, rephase | PLANNED | Evidence records are P02, not late: the plan's P02 loop investigates missing cargo through evidence and testimony and implements perception → belief → commitment (RQ-046, RQ-115). `ResourceObservationProjector` enforces audience binding and is the right seed. Sound is evidence only through an authorized perception path (`presentation-and-interaction.md:107-109`). |
| WC-16 | Accept, rephase | PLANNED | P02 native audio fixture, AUD-01/02 (RQ-169). Cues derive from receipts/observations; rejoin suppresses duplicates (TD §9:183). |
| WC-17 | Accept, rephase | PLANNED | GEN-01..05 at P01/P02/P03 (plan line 103), RQ-147/176. |
| WC-18 | Accept, rephase | PLANNED | First rider-on-assembly is P03 ("moving platform dependency"), full profiles P06 (RQ-107/116). |
| WC-19 | Refine | DOC | Adaptation already recorded; add the trigger: hardness/tool suitability at P04 (RQ-129). |
| WC-20 | Reject as finding; keep test | DOC | Contract already documented (`identity-and-determinism.md:59-70`). A unique-domain-tag test is cheap; nothing else. |

Lead objection 10 (coverage breadth) is addressed in §4; objection 1 through 9 verdicts appear above.

## 3. The care increment, corrected

The smallest coherent unit that the evidence supports:

- **Ordered actor change chain** (owner: ResourceHistory worker, integration Astra). Entry = actor,
  revision before/after, resource kind, before/after value, maximum before/after, cause kind
  {Damage, Restoration, MagicExpenditure, ExperienceReward, LevelGainRefill, later Passive}, cause
  reference, world sequence. Invariants: one entry per actor revision advance including zero-delta
  rewards; chain contiguity; current value equals last `after`; a cause reference must resolve to a
  retained record in the same root; `WithResourceTransition` and `WithActorState` are the only writers.
  Adversarial: forged contiguous chain with a fabricated cause reference refuses; missing zero-delta reward
  entry refuses; two entries at one revision refuse; replay from genesis rejects a chain the journal did
  not produce.
- **Retire the care-specific supersession** (`ExpectedHpAfterLastAttack`) in the new profile only:
  the resolution keeps `TargetHpAfter` as history and references the change entry it produced; admission
  compares current HP to the chain, not to the resolution. Profile 23/24 bytes keep the equality.
- **Care as scoped permission plus ordinary helper intent**: keep the authored agreement as the first
  permission instance; the acknowledgment remains an optional continuation choice; `AdministerRemedyIntent`
  keeps its five pins; add `DeliveryMode` to the remedy definition and the compatibility check.
- **Capability primitives with reasons**, consumed by the seven call sites without changing any verdict.
- **Coverage tests** for WC-14.

Everything else in the analysis's step A (WC-03 check, evaluator as a facet, checklist rewrite) is
either already present or belongs later.

## 4. Extrapolation: one fact owner, typed boundaries, real synergies, real exceptions

Each row names the canonical owner, the boundary that stays typed, the synergy that actually shares
machinery, the exception that must not be flattened, one adverse scenario with its invariant, and the
requirement/phase from the real plan. Nothing here is implemented.

| Domain | Owner and typed boundary | Real synergy | Domain exception | Adverse scenario → invariant | RQ / phase |
| --- | --- | --- | --- | --- | --- |
| Permission vs commitment vs cost | Permission: relations owner (grantor, grantee, kind, subject, scope, expiry). Commitment: activity owner (participants, reservations). Cost: the paying owner's ordered change. | Same revision pins and receipt path as any action. | Permission never spends; a commitment reserves; a UI confirmation is neither (decision §"four concepts"). | Helper revokes consent between offer and commit → refuse, no dose spent. Player confirms twice → one receipt. | RQ-108, RQ-115; P02 care, P04 delegation |
| Active/passive/environmental | Active: authenticated intent. Passive: process with owned clock, rate, carry, interruption. Environmental: forcing with a funded source (runoff budgets show the shape). | One ordered change contract for pools; one interval engine (`ExposureProgress`). | A renderer or timer never calls `RestoreHealth`; environmental forcing needs a finite source and receiver capacity. | Weather exposure applied to a held actor → zero; released actor gets no retroactive damage. | RQ-016, RQ-094; P03 rest, P04 weather |
| Long activities, interruption, partial effects | Activity owner with staged progress; each stage is its own commit; no rollback of earned time (`world-composition.md:86-88`; `PendingExcavation` precedent). | Consequence continuation envelope reused only after a second staged kind exists (D-07). | An instantaneous Potion is not a stretched activity; a bandage is. | Crash mid-stage → resume exact cursor; retry returns the stage receipt; no half-published world. | RQ-111, RQ-126; P03/P04 |
| Species, culture, cosmic affinity, individual variation | Definitions: species profile (anatomy, senses, lifespan). Instance: birth record pins parentage and variation draw; culture is learned norms on the person, not a species flag. | Body contracts and comprehension bands already type anatomy and language. | Culture cannot rewrite personality or buildings (`LANGUAGE_AND_AFTERLIFE.md:31`); species does not fix ethics (RQ-110/139). | Migration changes affiliation → belongings and knowledge unchanged unless acquired. | RQ-109/110/177/180; P01 schema, P02 legacy place, P08 |
| Deities, classes, magic research | Deity: first-class actor with declared powers/costs (TD §8:137). Class: versioned progression definition. Research: accepted discovery becomes an immutable definition compiled through the same validator (TD §8:159). | Researched spell composes typed operations (restore, remove condition, regrow part) with declared costs; node/depth/emission bounds. | Regrowth and resurrection keep stronger contracts than HP restoration; divine action is not admin mutation. | Spell emitting craft → XP → level → refill chain must count against the 256-emission budget and appear as ordered entries. | RQ-028/117/118/119; P05, P08 |
| Settlements, politics, economy | Institutions own offices/mandates; markets own stock, quotes, expiry; custody owns goods; ledger owns funds. | Quote acceptance is one transaction moving goods and money (TD §8:149); mandates survive holders by policy. | Building a castle grants no nation; prices move on events, never UI polling. | Two players accept one quote → allowance consumed once; second refused; retry returns receipt. | RQ-137/138/133/051; P02 two markets, P07, P08 |
| Creatures, ecology, geology, seasons | Cohorts/patches for unindividuated life; individuals on promotion reserve and remove their exact share; geological stages are due work on the branch calendar with immutable cause and revisable propagation. | Share partition reuses the terrain material partition builder; stage journals reuse transport-stage shape. | No species-wide hostility; approaching cannot reroll a flood; a biome label derives from conditions. | Dam built in a child branch → downstream stages invalidated once, reservations released once. | RQ-017/151-167; P02 habitat, P03 staged flood, P04, P10 |
| Vehicles, mounts, pets, parts | Vehicle: assembly of condition-bearing members with a motion frame; mount/pet: actor with needs; attachment: one relation naming its coupling law. | Physical member owner is the part prototype; custody rules feed and load. | Cargo custodian is not owner; a rider keeps identity; tunneling cannot ignore rock. | Mule dies carrying reserved wood → remains custody keeps the reservation; quest revalidates, no wood minted. | RQ-107/116; P03 simple assembly, P06 |
| Puzzles, quests, logical advancement, progressive UI | Puzzle state in ordinary objects/topology; quests as participant-bound instances; witnesses as privileged durable records revalidated by dependency revision; UI reveals by learned inspection, never hidden truth. | Skill actions satisfy predicates through ordinary actions; encounter admission couples hazards before effects. | A rating is a vector conditional on capabilities, not a scalar; guidance never grants rewards or time. | Witness NPC pruned by generation → revalidate before prune; lived contradiction reports changed feasibility, no reroll. | RQ-143/147/148/134/171; P02 puzzle, P03 witness, P09 |
| Observers, knowledge, language, audio, art, animation | World truth → scoped evidence → belief per principal; comprehension per actor and channel; cues derived from receipts. | `ResourceObservationProjector` audience binding generalises to evidence kinds; NPC planners consume the same projection as the UI. | Localization grants no knowledge; sound is evidence only through an authorized perception path; animation never causes effects. | Two principals in one world receive different comprehension of one utterance; NPC aids NPC unseen → player view unchanged, journal proves it. | RQ-046/071/115/169/173; P02 |
| Planes, dreams, ghosts, time travel, branch identity | Identity scope (world, branch, realm); ancestral identity vs branch incarnation; dream session seeded from session identity with a tether; transit ledger assigns export authority once. | `TerrainScope` realm and `ActorFormBinding` tether anchor already exist; `History/` addresses exist as candidates. | Neither travel nor a dream grants healing or cloned goods; a ghost is a form transition on the same person, not a new inventory. | Fork before harvest, harvest both, craft, export both → second export refused; crash and retry returns the same receipt. | RQ-053/054/136/141/175; P03 tether and transfer, P10 |
| Streaming, level of detail, co-op, Wait | Chunk residency separate from simulation authority; one home partition per fact; batching integrates only to the next interrupt with one interval owner and `lastAdvanced`. | Split-interval equality is the same property the exposure binding already has. | Distant batching recovers each individual's actual state; it never invents a deeper person on activation. | Unload a boarded ship → whole coupled set preserved; caravan arriving at a held pocket submits admission before influence. | RQ-104/105/121/073; P03, P06 |

Real synergies worth naming once: the interval partition builder (terrain material, cohort promotion,
reservoir-to-cell allocation); the retired-disposition taxonomy (recipe retirement, consumption, installed
backing, repair-into-target, fed-to-mount, burned fuel) with one partition validator; the ordered change
contract for pools only; the staged-work envelope after a second kind; the audience-bound projection for
evidence. Rejected, agreeing with the analysis: universal facet registry, mutating event bus, per-cell
actors, event-sourcing every component, one "Resource" type spanning intervals, condition, debt and
vitality (their laws differ).

## 5. Prioritised integration and test plan

Compatibility rules for every step: fresh explicit profile, no lived-save migration, exact re-encoding,
reserved 16 MiB headroom, exclusive writer, replay before readiness, older profiles keep their bytes and
interpretation. Preserved gates: LANG-01–04, durable recovery with process-kill tests, native defeat →
aid → renewed choice acceptance, the 1,849-case managed regression, 22 frozen specifications, milestone
branches. "Coverage: missing" means no retained test is known to exist; none is marked accepted here.

| Step | Scope | Owner | Depends on | Requirements / phase | Adversarial tests (all coverage missing) |
| --- | --- | --- | --- | --- | --- |
| 1 | Ordered actor change chain with zero-delta continuity; remedy `DeliveryMode`; capability primitives with reasons; WC-14 coverage tests; new profile replacing the care-specific supersession | ResourceHistory worker (chain), Fable coding unit (care/capability), Astra (profile, integration) | none | RQ-088, RQ-099, RQ-114, RQ-105; P02 current increment | Damage → aid → admission and replay; XP without level between damage and aid; forged contiguous chain; duplicate revision entries; tampered HP without entry; unconscious + ingest refuses, external passes; exact retry of aid; profile-24 bytes unchanged; caller parity for the seven HP sites |
| 2 | Rest as the first passive writer: exposure-owned recovery stages, interruption, consent | Fable coding unit; Astra | 1 | RQ-126, RQ-034; P03 | Held subject earns nothing; repeated short rests grant no refill; interrupted stage keeps partial credit; split-interval equality; restart mid-rest |
| 3 | Lineage join plan and scaffold: constructed footing + wet envelope, then physical members under pacing, then continuation under Wait with admission-before-influence | Astra (plan), Fable coding unit (scaffold) | 1 | RQ-128/129/161, SYN-02/20; P02 exit, P03 | Genesis join from unplayed 24 basis; each exclusion guard has a named test retired deliberately; excavation request during Wait refuses with a typed reason; occupant on installed floor beside water stays outside the wet envelope |
| 4 | Injury transitions and body revision split; treatment pins presence and body | Fable coding unit | 1 | RQ-150, SYN-08; P03 → P05 | Injury does not change presence revision; movement does not change body revision; treatment with stale presence refuses (reach); restoration spell regrows only its declared loss |
| 5 | Death, estate custody, evidence records, life/form transitions | Fable coding unit; Astra | 1, 4 | RQ-132/174/175, RQ-046/115; P03 | Death commits once with exact retry; remains custody keeps reservations; NPC aids NPC unseen; secret death not revealed by a rejection reason |
| 6 | Item instance facet, repair-into-target disposition, tool wear; recipe-definition cycle rule in the compiler | Fable coding unit | 1 | RQ-131/111/129; P01 compiler rule, P04 | Whole transfer keeps ID and condition; repair retires material once and not the target; self-referential recipe definition rejected by the compiler; wear survives save |
| 7 | Provenance-preserving compaction as a typed commit kind; sealed intervals stay addressable | Astra with a Fable unit | 1 | RQ-141/054/075; P03 intro, P10 full | 300 consumptions across a rollover with exact partition; forged sealed archive refuses; replay through compaction; sealed interval re-consumption refuses; cross-branch export of a sealed interval assigned once |
| 8 | Effect instances with attributable contributions and a versioned per-profile process phase table | Fable coding unit | 2 | RQ-117/131; P04/P05 | Poison and regeneration at 3 HP under the declared order; remove first enchantment keeps second; hold freezes expiry; liquid declared per-step |
| 9 | Per-subject exposure anchors with coupling edges; permission relation extraction; wallet account kinds | Fable coding unit; Astra | 2 | RQ-105/121/108/137; P03 co-op, P04 delegation, P02 markets | Coupled rider/ship advance together and stop at detach; revoked permission before commit; two acceptances of one quote |

## 6. Unresolved decisions for the owner

- **D-01** Keep `TargetHpAfter` in the resolution as history (recommended yes); the change entry, not
  the resolution, is the admission comparator in the new profile.
- **D-02** Hostile targeting of incapacitated actors: illegal by profile today; a declared finish action
  is a gameplay choice for P03 death.
- **D-03** Passive recovery at HP 0 and hysteresis: owner decision before step 2.
- **D-09 (new)** Process phase order within a boundary: declared per definition class, then instance
  start sequence, is my recommendation; the reference does not decide it.
- **D-05/D-06/D-07/D-08** Defer as the analysis recommends; D-06 without a new flag.
- **D-10 (new)** Whether the lineage join or the care increment comes first for the P02 exit; both are
  required by the plan and the join is the larger risk.

## 7. Coverage disclosure

Requirement families touched: A1 depth targets (RQ-004/005), A2 geography/ecology (RQ-015–020,
151–167), A3 actors/progression (RQ-021–030, 114, 117, 119, 150), A4 creation/survival (RQ-031–039, 111,
126–131), A5 society/economy (RQ-040–052, 108, 112, 132–139, 146), A6 time/planes (RQ-053–058, 136, 141,
148), A7/A8 safeguards (RQ-070–079, 088, 099, 104, 105, 121), presentation/audio/onboarding (RQ-134, 140,
169–171), culture/lifecycle (RQ-173–181). No requirement is marked covered by this review. Not verified:
existence of any named regression test; the in-flight ResourceHistory shape; Hosting session behaviour;
client code; per-requirement rows of `COVERAGE.json` beyond RQ-150 (P03 → P12). Phase mappings for other
rows come from `IMPLEMENTATION_PLAN.md` text and may differ from the ledger; Astra should reconcile before
assignment.
