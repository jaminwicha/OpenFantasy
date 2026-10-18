# Commit and milestone doctrine

Owner direction, 2026-09-10: commit implementation progress along the way and create milestone
branches to record major progress. This is shared project discipline for all authorized agents.

- Commit coherent completed increments with the relevant checks and a useful description.
  Do not wait until a large campaign is finished to preserve working progress.
- At a reviewed vertical slice, major integration or other meaningful accepted checkpoint,
  create a `codex/milestone-<descriptive-name>` branch at the exact qualified commit. Record
  the commit, scope, evidence and remaining limits here. This names an existing commit; it
  does not switch the working checkout or include uncommitted work.
- Keep milestone branches as historical markers. Do not advance or force-move an existing
  milestone to a later state; create a new descriptive milestone when progress warrants it.
- Continue daily work on the active development branch. Milestone creation does not require
  pushing, merging or publishing. Those actions follow their own authorization and scope.
- The integration lead owns commits and milestone creation when workers share a checkout.
  Workers return bounded edits/evidence; preserve unrelated staged files and never stage all.
- Passing a bounded checkpoint does not close a phase, production gate, art acceptance or
  human playtest requirement beyond the evidence recorded for that checkpoint.
- Before an accepted commit/milestone, complete the [documentation review hook](DOCUMENTATION_MAINTENANCE.md)
  in its existing work record. Reconcile affected live plan/spec/reference claims and run the document
  check. WIP preservation may retain open items with explicit limits; it is not acceptance.

## Recorded milestones

| Branch | Commit | Qualified scope and limits |
|---|---|---|
| `codex/milestone-exit-reconciliation-20261018` | resolved by preserved branch | [PLAN-STATE exit reconciliation](p02/partition-state/exit-reconciliation/RESULT.md): original obligations mapped to scoped evidence, stale next-step wording corrected, membership/due-service preparation and undispatched review scope explicit. Documentation only; no gate closed or runtime change. |
| `codex/milestone-resident-exposure-basis-20261018` | resolved by preserved branch | [Exact exploration basis](p02/partition-state/resident-exposure-basis/RESULT.md) in legacy locality work: personal age/metadata, ordinal/overflow, due and ordinary publication equivalence. Explicit versus deferred/every-step materialization diagnostic; no saved timing format, partial persistence, current-profile or phase acceptance. |
| `codex/milestone-construction-publication-20261018` | resolved by preserved branch | [Locality publication baseline](p02/partition-state/construction-publication/RESULT.md): sole-writer selected work, same-save carried gift/intervention/partial return/completion, ordinary replay equivalence and first/final commit recovery. Full-root residency/encoding remains; no partition format, current-profile offscreen acceptance or phase closure. |
| `codex/milestone-construction-locality-20261017` | resolved by preserved branch | Complete supported legacy nonterrain owners execute 200 selected work candidates with source terrain collected; shared time/identity rules, due regrowth, exact ordinary outputs and reconstruction. [Result](p02/partition-state/construction-locality/RESULT.md) records 793 affected passes, 11 new and explicit40/41 refusal. No durable partition publication, current-profile partial residency or phase acceptance. |
| `codex/milestone-construction-authority-20261017` | resolved by preserved branch | Revalidated site evidence in the ordinary selected-batch executor and SQLite publisher; full-world/receipt/identity equivalence, hold/time ordering and preview/interruption recovery. [Result](p02/partition-state/construction-authority/RESULT.md) records 480 affected passes, nine new and measured bounded allocation reduction. Complete residency remains; no partition/offscreen or phase acceptance. |
| `codex/milestone-construction-execution-20261017` | resolved by preserved branch | Shared ordinary/detached construction kernel, exact reusable site evidence and real coupled material completion. [Result](p02/partition-state/construction-execution/RESULT.md): 411 affected passes, 12 new; source world/scene/index/terrain collection verified. Whole-source capture/freshness, terrain-retaining material contexts, physical/time admission and joined durable partition publication remain open; initial test compilation failure retained. |
| `codex/milestone-partition-contract-20261017` | resolved by preserved branch | [Integrated prototype target](PARTITION_PROTOTYPE_CONTRACT.md): stable ownership versus location, actual custody-transfer proof, full access-population/earned-work dependencies and atomic episode matrix. [Source audit](p02/partition-state/prototype-contract/RESULT.md) identifies the access ceiling and retained terrain references. Design/documentation checkpoint only; executable integration, measurements and independent acceptance remain open. |
| `codex/milestone-research-disposition-20261017` | resolved by preserved branch | Source-checked disposition of four October17 research leads, mapped to existing geography/NPC/audio phases and timeless design contracts. [Review](research-review-20261017/LEAD_DISPOSITION.md) preserves the original, rejects unsupported speed/API/algorithm claims, and retains PLAN-STATE priority and every runtime/phase gate. Documentation only. |
| `codex/milestone-prepared-placement-20261017` | resolved by preserved branch | Shared prospective structure clearance/support and required-access assessment over exact bounded reads; ordinary full candidate/material/work admission remains. [Result](p02/partition-state/prepared-placement/RESULT.md) retains 373 within-budget affected passes, both aggregate timeouts, a late out-of-budget TRX and corrected fixture. No offscreen work, partition publication or phase acceptance. |
| `codex/milestone-prepared-access-routes-20261017` | resolved by preserved branch | Ordinary flat/stair search shared with exact prepared reads; extra decoded terrain cannot grant unchecked detours. Full Core 1,964 passes, including seven new cases and actual 200-step funded-construction access agreement. [Result](p02/partition-state/prepared-routes/RESULT.md) records Hosting evidence. This does not execute offscreen work or publish partition owners. |
| `codex/milestone-occupancy-publication-20261017` | resolved by preserved branch | Complete immutable occupancy catalogs and atomic metadata updates, exact saved-world reconciliation and historical index reconstruction. [Result](p02/partition-state/occupancy-publication/RESULT.md) retains affected tests and both fixture-error classes. This is derived-index consistency, not canonical partition ownership, offscreen simulation or cross-partition durability. |
| `codex/milestone-paged-spatial-reads-20261017` | resolved by preserved branch | Bounded occupancy metadata and exact selected geometric archives prepare movement without a complete local scene. 180 affected cases pass, 15 new; two fixture compile failures retained. The 200-presence fixture measures selected I/O only. [Result](p02/partition-state/spatial-directory/RESULT.md). Directory authority, owner transfer, absent-detail work, atomic history and broader gates remain open. |
| `codex/milestone-joined-movement-prerequisites-20261017` | resolved by preserved branch | Joined spatial/control/exact-carriage/readiness/time reads, actual file-backed region crossing with ordinary durable cargo/history continuity, and same-step dialogue suppression. 273 selected cases pass, six new; two fixture-assumption failures retained. [Result](p02/partition-state/movement-prerequisites/RESULT.md). Full-world step ordering/admission stays mandatory; partition ownership, partial simulation, atomic owner publication and broader gates remain open. |
| `codex/milestone-control-dependencies-20261017` | resolved by preserved branch | Exact selected current-control rows share ordinary command authority; revoke/return/loss/release and reacquisition invalidate prepared reads independently of geometry. Full Core 1,938 and selected Hosting 21 pass, including eight new cases and retained paid work. [Result](p02/partition-state/control-dependencies/RESULT.md). No complete action transaction, partition publication, offscreen or native/phase acceptance. |
| `codex/milestone-prepared-spatial-reads-20261017` | resolved by preserved branch | Selected actor/structure dependencies, arrival/departure/state revalidation and a shared ordinary movement kernel. Full Core 1,938 and focused Hosting three pass; seven new dependency cases. [Result](p02/partition-state/spatial-read/RESULT.md). Supplied-scene completeness is a prerequisite; global directory, action authority, atomic partition publication and offscreen work remain unproved. |
| `codex/milestone-movement-terrain-footprint-20261017` | resolved by preserved branch | Actual actor-derived cardinal/stair terrain footprint feeds pinned loading; ordinary movement shares the step-height rule. 160 affected cases pass, including ten new footprint comparisons. [Result](p02/partition-state/movement-footprint/RESULT.md). One test compile failure retained; other actor/structure/time dependencies and partition simulation remain open. |
| `codex/milestone-terrain-read-dependencies-20261017` | resolved by preserved branch | Exact scoped terrain plans, immutable read sets and bounded archive loading with pre-realization header checks. File-backed two-of-three-region read reuses actual geometry; 58 affected tests pass. [Result](p02/partition-state/terrain-reads/RESULT.md). No full action closure, directory authority, partition publication/offscreen or production-scale acceptance. |
| `codex/milestone-carriage-boundary-20261017` | resolved by preserved branch | Shared immutable physical-carriage projection used by structural loading; actual movement/gift continuity and funded installation/reopening, 100 distinct affected tests pass. [Result](p02/partition-state/carriage/RESULT.md). Legal custody ownership remains distinct from physical carriage; no partition owner, partial-residency or offscreen qualification. |
| `codex/milestone-state-layout-baseline-20261017` | resolved by preserved branch | Actual current-format movement and legacy/current funded construction measured through real Core/SQLite owners, exact reopening, 15 initial tests and four final diagnostic cases. [Result](p02/partition-state/RESULT.md). Read-only canonical layout accounting; design comparison maps clock/access/transaction dependencies. No partitioned residency, format decision, offscreen/scale or full phase acceptance. |
| `codex/milestone-shared-expedition-20261017` | resolved by preserved branch | Complete original-earned-save two-player expedition: copied40→41 publication, purchase, travel, real combat, independent withdrawals, one potion, exact sale, return and replay; ten receipts/15,614 movement samples. [Result](p02/party-withdrawal/expedition-completion/CONTINUATION.md). Nine geometry cases and final build pass; three failed full attempts retained. PLAN-STATE comparison prepared, prototype/decision and broader phase/human gates remain open. |
| `codex/milestone-expedition-sale-20261017` | resolved by preserved branch | Exact retained second-town sale, owned cargo/currency and original-history replay; six travel geometry cases and warning-free builds. [Result](p02/party-withdrawal/expedition-completion/RESULT.md). Both complete native runs fail and remain retained; post-care travel diagnosis and full return are open. |
| `codex/milestone-native-withdrawal-continuation-20261017` | resolved by preserved branch | Exact earned-save Active/Staging native continuations: real damage, independent paid withdrawal, one conserved potion/no-dose guard, exact receipts/replay; older four-player wait/reconnect retained. Five travel and three market cases; warning-free client build. [Result](p02/party-withdrawal/native-journey/RESULT.md). Complete commerce/return expedition, autonomous changed-place episode and full P02 remain open; failed attempts are preserved. |
| `codex/milestone-withdrawal-operator-20261017` | resolved by preserved branch | Explicit exact-head request and verified copied publication, original-genesis migrated solo selection;11 focused managed cases and warning-free Godot C# build. [Result](p02/party-withdrawal/saved-expedition/RESULT.md). Native cooperative expedition, autonomous change during absence and all wider gates remain open. |
| `codex/milestone-party-withdrawal-core-20261017` | resolved by preserved branch | Profile41 portable Core: independent paid routes, Full Wait, complete-side exit, cancellation/collision/incapacity, original command attribution and safe owner return, canonical restore/replay. [Result](p02/party-withdrawal/world-binding/RESULT.md). SQLite migration, connected/native UI, independent review and full P02 remain open. |
| `codex/milestone-adventure-continuation-20261003` | resolved by preserved branch | Same earned solo save: native buy/haul/danger/retreat/care/sale/return, original supplies/building retained, six exact command/receipt pairs, original-history replay and exact paused rows except the authenticated single recovery fence.20 checker and9 managed cases pass. [Result](p02/adventure-journey/RESULT.md). Original timeout remains unexplained; shared journey, human/art/platform/scale and full P02 remain open. |
| `codex/milestone-audit-corrections-20261003` | resolved by preserved branch | Iterative roadmap/documentation corrections: restored merchant/escort and prerequisite routing, strengthened work-record link/template checks, reusable audit skill, independent pass3 reviews with zero new findings within declared scope. [Ledger](roadmap-audit-20261003/CONVERGENCE.md). Separate orchestration report analysis remains pending its location; no phase or general orchestration acceptance. |
| `codex/milestone-roadmap-audit-20261003` | resolved by preserved branch | Complete 181 requirement trace, full phase/P02 gap audit, progress metrics, two native critics and reviewed external critique. Living roadmap, compact preserved handoff and plan/reference documentation hooks; 91 live pages mechanically checked, six checker counterexamples/positive cases. [Audit](roadmap-audit-20261003/RESULT.md). No phase, human, platform or full-game acceptance; frozen scope unchanged. |
| `codex/milestone-delegated-recovery-20261003` | `cbcd7565` | Existing profile40/schema12 archive/revocation/accepted-retreat recovery: 95 final-source cases, ten new, three native, seven actual storage-operator kills and three inspected return captures. [Result](p02/delegated-control-recovery/RESULT.md). Three failed attempts retained, 919 prior inputs/23 specs and unrelated index exact. No production contract change; populated allowance, grantee reacquisition, listener death, latency/scale and full phase/human gates remain open. |
| `codex/milestone-shared-history-20261003` | resolved by preserved branch | Independent shared History UI, exact opening/page/row captures, owned late responses and task drainage. [Evidence](p02/shared-history/RESULT.md): warning-free builds, 208 dependency-verified passes (22 new, five native), actual four-peer gift history/allowance/return, delayed inbound TLS, four inspected images and independent critique. Three failed attempts retained; 912 prior inputs/23 specs and unrelated staging verified, with two additive model links. Before-change `bb18995e`. Native paging/paid quotes/catalog, production latency/crash/scale, physical/human/art and full campaign gates remain open. |
| `codex/milestone-private-history-directory-20261003` | resolved by preserved branch | Authenticated terminal history discovery with fixed 16-entry pages, stable cap/tie cursor, complete replay, independent permission and usable local/remote capture API. [Evidence](p02/private-history-directory/RESULT.md): warning-free build, 194 distinct final-source cases (33 new), actual archive/reopen and TLS discovery/allowance/revocation, independent critique. Three failures retained; 821 old inputs, 23 specs and predecessor index preserved. Before-change `d3f8ebc2`; WIP `3a9eb52a`. Native History UI/delayed inbound replies, production latency/crash/scale, physical/human/art and full campaign remain open. |
| `codex/milestone-restorative-choices-20261003` | resolved by preserved branch | Shared home/operated Care library, independently scoped recovery/withdrawal, exact Review/Confirm and compatible home7/selected4 known terms. [Evidence](p02/restorative-choices/RESULT.md): warning-free build, 2,360 distinct dependency-verified passes (33 new, Core1,885, five native), five original restorative input/reopen routes and ten inspected captures. Genuine injury/finite doses, real allowance, exact receipts, owner-return refusal and original replay qualify the bounded route. Independent critique fixes and four failed attempts retained; narrow scene reuse explicit. Before-change `0e8414a4`,811 prior inputs,23 specs and unrelated staging preserved; three additive links/exact original. Broader history/care/catalog, crash/reliability, scale, physical/human/art and campaign gates remain open. |
| `codex/milestone-selected-trading-20261003` | resolved by preserved branch | Shared home/operated Trade screen, exact lot showing/retraction, quote review/confirmation and guarded asynchronous lifetimes. [Evidence](p02/selected-trading/RESULT.md): warning-free build, 2,313 distinct dependency-verified passes (ten new, full Core 1,885, nine native), three home input/reopen and three refusal routes, nine inspected images and independent critique. Real allowance, exact lot/currency attribution, owner-return invalidation and original replay qualify the bounded route. Three failed attempts retained; footer-only evidence reuse is explicit. Before-change `02f295bf`, separate WIP `93e41f42`, 805 prior inputs, 23 specs and unrelated staging preserved; two additive links/exact original. History discovery, restorative screens, crash/reliability, scale, physical/human/art and campaign gates remain open. |
| `codex/milestone-human-construction-20261003` | resolved by preserved branch | Explicit home/delegated Build captures, fresh human-builder scenario retaining original funding/work, independently public geometry and compatible home6/selected3 observations. [Evidence](p02/human-construction/RESULT.md): warning-free build, 2,299 distinct dependency-verified passes (20 new, fullCore1,885, nine native), four inspected images and independent review. All200 native work steps,198 delegated, partial stop/reopen, owner return/revoke, conservation and original replay qualify the bounded route. Five failures and one interrupted fixture retained. All797 prior inputs,23 specs and unrelated staging preserved; one additive source link/exact original. Sustained activity, unobscured object/HUD legibility, history/crash/reliability, physical/human/art and full campaign gates remain open. |
| `codex/milestone-recipe-library-20261003` | resolved by preserved branch | Explicit admitted Recipes library, exact Prepare/Confirm captures, scoped public material names and versioned compatible observations. [Evidence](p02/recipe-library/RESULT.md): warning-free build, 2,280 distinct dependency-verified passes including 28 new cases, fullCore1,885/eight native cases, ten inspected images and independent review. Real harvest/regrowth, both recipes, owner-return invalidation, exact receipts and original replay qualify the bounded route. Two failed attempts retained; older gift/movement/history reliability remains unexplained. Before-change ref preserves `8c68ee04`;789 old inputs,23 specs and unrelated staging preserved, with two additive source links/exact original. Learning, construction/public geometry, crash/history, physical/human/art and full production/campaign gates remain open. |
| `codex/milestone-selected-production-20261003` | resolved by preserved branch | Shared permitted gathering/crafting/mining, exact scoped captures, regional projection and operated HUD. Two original four-human native worlds prove gathered inputs → split produced gift and three swings → bank opening/conserved water, with original replay and owner return. [Evidence](p02/selected-production/RESULT.md): warning-free build, 2,123 distinct dependency-verified passes, 11 inspected images and independent source review. Four failed attempts retained; the original raw-gift reopening failure remains unexplained despite a passing diagnostic, so full regression/reliability is not accepted. Before-change ref preserves `3467ee2d`;783 old inputs,23 specs and unrelated staging remain preserved, with two additive test-project links/exact original. Full recipe/construction/public geometry, history/crash, production and campaign/physical/human/art gates remain open. |
| `codex/milestone-selected-material-gifts-20261003` | resolved by preserved branch | Shared Pack/Gifts from exact operated Inventory, fixed preparation and separate history permission; actual four-human native source-lot create/accept/decline/withdraw, return/revoke refusal and original-genesis replay. [Evidence](p02/selected-material-gifts/RESULT.md): warning-free build, 2,111 distinct current-dependency passes, eight inspected images, independent UI review and current architecture. Three failed attempts retained; overflow repaired without changed limits. Before-change ref preserves `8741766b`;777 old inputs,23 specs and unrelated staging preserved, with one additive test-project link/exact archived original. Native produced/history, mouse dropdowns, public readable item catalog, profile40 crash, production timing and full campaign/physical/human/art gates remain open. |
| `codex/milestone-native-selected-control-20261003` | resolved by preserved branch | Actual Godot Characters consent, real allowance expiry, acquired/selected Resume, movement, paid combat, each home decision and owner return/revoke across four native TLS peers, with exact original-genesis replay. [Evidence](p02/native-selected-control/RESULT.md): warning-free build, 2,103 distinct passes on current dependencies, nine inspected images and bounded independent UI review. Panel lifetime/layout and selected resource visibility fixes retain earlier metadata failure and rejected clipped images. All775 prior tests/project/fixtures,23 specs and unrelated staging remain exact. Before-change ref preserves `bb3812ff`; full screens, profile40 process-kill, production timing and full campaign/physical/human/art gates remain open. |
| `codex/milestone-connected-player-control-20261003` | resolved by preserved branch | Home-authorized consent and genuine four-human TLS actor selection, scoped input/queries, delivered-frame Resume, home combat decisions, return/revoke and original replay; Godot adapter source plus existing home-player native compatibility. [Evidence](p02/connected-player-control/RESULT.md): warning-free build, 2,500 distinct passes on verified dependencies, Windows-only skip, independent review, repaired shutdown race and current architecture. Original764 tests/project inputs and23 specs preserved, with two client Compile links and archived original. Selected native UI/screens, profile40 process-kill, production history/movement timing and all full campaign gates remain open. Earlier failures and WIP checkpoint retained; before-change ref preserves `f71e1b78`. |
| `codex/milestone-selected-player-queries-20261003` | resolved by preserved branch | Exact local selection-bound inquiries and inspections; independent open-offer/history permissions, tactical due-state exclusion, actual archive/corruption/revoke/shutdown and merchant/home-confrontation proofs. [Evidence](p02/selected-player-queries/RESULT.md): warning-free build; new 16, storage 78, Core 1,885, server 76 pass with expected Windows-only skip. Query aggregate has 125 passes and two original socket-history timeouts; one three-case diagnostic passes but does not explain or waive those failures. Independent source review and current architecture; all 759 prior tests/fixtures, 23 specs and predecessor staging preserved. Full regression/latency, TLS/Godot selection, complete home combat, native/crash and production/campaign gates stay open. Before-change ref preserves `2a885656`. |
| `codex/milestone-local-actor-selection-20261003` | resolved by preserved branch | Internal generation-bound actor selection, scoped actionable views, immutable home attribution and correct submitting-port completion. [Evidence](p02/local-actor-selection/RESULT.md): warning-free build; 2,155 distinct passes (selection 11, Hosting 183, Core 1,885, server 76), expected Windows-only skip; independent critique, current architecture and all 756 old tests/fixtures/23 specs/staging preserved. Two failed builds and one corrected fixture assumption retained. TLS/Godot selection, selected queries/history, positive construction/helper/home-decision proofs, crash/native and all campaign/production gates remain open; earlier timing failure remains unexplained. Before-change ref preserves `885e3b07`. |
| `codex/milestone-runtime-scheduling-diagnosis-20261003` | resolved by preserved branch | Opt-in test-process CPU/GC/pool capture and exact event correlation; no production changes. [Evidence](p02/runtime-scheduling-diagnosis/RESULT.md): warning-free build; two original server selections pass76 plus expected Windows-only skip, isolated original four-player case passes (76 distinct cases). Failed movement did not reproduce; no causal attribution or repaired latency gate. Preserve all755 old tests/fixtures,23 specs and staging. Resume full actor selection; retain all latency/native/phase/production obligations. Before-change ref preserves `7ab81d44`. |
| `codex/milestone-movement-delivery-diagnosis-20261003` | resolved by preserved branch | Captured original four-player failure: accepted sample expires before first selection; scheduling gap precedes tick serialization. [Evidence](p02/movement-delivery-diagnosis/RESULT.md): warning-free build, six additive codec/scope/duplicate-name checks on verified .NET10.0.12, independent diagnostic critique and all754 prior tests/fixtures,23 specs/staging preserved. No production code changes; server timing gate remains failed. CPU/runtime pauses/executor cause, full selection and all native/phase/production gates remain open. Before-change ref preserves `b2bc3196`; working branch remains unchanged. |
| `codex/milestone-live-control-presence-20261003` | resolved by preserved branch | Internal authenticated-port assignment custody, exact request/witness binding, owner-return/disposal/startup/stop release, original loss episodes and accepted paid work retained. [Evidence](p02/live-control-presence/RESULT.md): warning-free build,154 fresh Hosting passes including8 new, fresh full Core1,885 evidence, independent critique/current architecture and original752 tests/fixtures/23 specs/staging preserved. **Uninstrumented server gate remains failed:**75 passes, four-player movement timeout, Windows-only skip; traced diagnostic76 passes does not explain it. Explicit selected views/input/generations, third-player custody contrast, TLS/Godot/native/crash and full phase/production gates remain open. |
| `codex/milestone-excavation-reservation-reuse-20261003` | resolved by preserved branch | Immutable excavation publication reservations shared only through private forward cursors; full interval/collision/decode/world validation and bytes retained. [Evidence](p02/host-latency-diagnosis/RESULT.md): fixed 16-decision allocation 226,050,336→201,974,072 bytes (10.6508% reduction), warning-free build, full Core 1,885, complete storage 489 under unchanged 600s, server 76/Windows-only skip and focused co-player regressions. Independent critique/current architecture; original 751 tests/fixtures, 23 specs and staging preserved. Earlier storage/server timing failures remain unexplained; no native, platform, phase or production-latency gate closes. Pre-edit expected-red checkpoint preserves `53cd1085`. |
| `codex/milestone-effective-control-managed-20261003` | resolved by preserved branch; implementation `d76f4681` | Fresh profile40/schema12 witnessed co-player command authority, actual delegated combat/retreat/care/trade, archive/reopen and managed publication faults. [Evidence](p02/effective-co-player-control/RESULT.md): clean build, 2,420 distinct passing cases from accepted groups including all 21 new and full Core 1,885; independent critique and current architecture. Broader qualification is incomplete: two unchanged 600s storage timeouts, repeated five-second server movement failure and one 15s startup failure remain. Separately passing storage cases do not replace the aggregate. All 741 older tests/fixtures, 23 specs and unrelated staging preserved. Live selection/presence, native/process-kill, Windows ACL, full phase and production gates remain open. |
| `codex/milestone-control-permissions-20261002` | resolved by preserved branch | Fresh profile39/schema11 durable named co-player offers, exact acceptance, replacement/revocation, bounded scopes and nonadvancing receipts through the sole authority. [Evidence](p02/co-player-control/RESULT.md): warning-free build, 2,818 distinct selected passes including 34 new/full Core1,881, real archive and exact replay, independent critique/current architecture. Storage488 repeat passes under unchanged 600s; earlier timeout remains unexplained. All 735 old tests/fixtures, 23 specs and predecessor staging preserved; previous handoff archived exactly. Effective takeover, UI/protocol, migration, Windows ACL, native and full campaign/production gates remain open. |
| `codex/milestone-allowance-recovery-20261002` | resolved by preserved branch | Explicit schema10 measured-lower-bound recovery, fenced host segments, original-history activation and atomic event/row persistence. [Evidence](p02/allowance-recovery/RESULT.md): warning-free build, 933 distinct selected passes including 44 new, eight actual Host kills, inherited/later archive publication and one rendered Godot/TLS handback with three inspected captures. All 726 older tests/fixtures, 23 specs and predecessor staging preserved; independent replay-parity critique fixed. Unknown crash/offline time is uncharged; Windows ACL, co-player/general survival, production migration/capacity/platform/human/campaign gates remain open. |
| `codex/milestone-control-return-20261002` | resolved by preserved branch | Observation-bound deliberate Resume, pending-control HUD/input, genuine paid hostile retreat and native TLS handback/replay. [Evidence](p02/control-return/RESULT.md): warning-free build, 644 distinct passes including 14 new, 12 Godot processes/eight rendered/21 captures; current architecture and independent critique. All 718 old tests/fixtures, 23 specs and predecessor staging preserved. Five failed attempts remain recorded; Windows ACL, restart/co-player, general survival, capacity, production and campaign gates remain open. |
| `codex/milestone-care-disposition-durability-20261002` | resolved by preserved branch | 128 real alternating care dispositions and finite recovery costs, coordinated cut, ten transaction faults/ten Host process kills, exact replay, native original/archived decisions and corrected HUD layout. [Evidence](p02/care-disposition-durability/RESULT.md): warning-free build, 232 distinct managed passes including five new, 12 rendered processes/30 captures; independent review/current architecture. Intermediate green renders rejected by image review remain retained. Maximum disposition-only/composite capacity, survival/restart/co-player, physical/platform/human and production/campaign gates remain open. |
| `codex/milestone-care-disposition-qualification-20261002` | resolved by preserved branch | Exact native cancellation/decline and separate reopens, actual pending-cut/disposition/re-cut with distinct treatment/closure frontiers, helper HUD wording and regional fixture framing. [Evidence](p02/care-disposition-qualification/RESULT.md): warning-free build,219distinct managed passes including10new,6rendered processes/15images and journal authentication; independent review/current architecture. Capacity/crash/native-after-cut, physical devices, survival, production and full campaign gates remain open. |
| `codex/milestone-archived-quote-replay-20261002` | resolved by preserved branch | One complete archived-quote replay with exact publishing-root capture, independent retrieved-archive authentication, full later-history validation and unchanged permission/byte/deadline contracts. [Evidence](p02/archived-quote-replay/RESULT.md): warning-free build, 416 distinct managed passes including 20 new; two-query allocation1,071,986,376→536,331,088 bytes. Independent review and current architecture; historical timeout causes, native, production history latency and full campaign gates remain open. |
| `codex/milestone-terrain-projection-reuse-20261002` | resolved by preserved branch | Captured stale movement before first selection; exact immutable terrain occupancy reuse across worksite/liquid/player projections, weak retention, unchanged state/bytes/leases/deadlines. [Evidence](p02/multiplayer-service-latency/RESULT.md): warning-free build, 567 distinct managed passes including 8 new; normal Server 77 pass/Windows skip. Fixed 32-view allocation16,003,392→1,217,344 bytes. Independent review and current docs; historical archive-query, native, production scale and full campaign gates remain open. |
| `codex/milestone-care-dispositions-20261002` | resolved by preserved branch | Explicit profile38/CareV5 pending-request cancellation/decline, real terminal history and archive/replay, safe unready control return, human-helper choices and immutable wheel offers. [Evidence](p02/pending-care-disposition/RESULT.md): 2,544 distinct cases have passing evidence including all69new/fullCore1,870; unchanged tests/specs/staging. Managed Server retains its five-second four-player movement failure; earlier query timeouts remain unexplained. Native, full world capacity/crash, migration, production and campaign gates remain open. |
| `codex/milestone-general-recovery-20261002` | resolved by preserved branch | Explicit profile37/CareV4 general assisted recovery through ordinary finite transactions; real later battle/archive/travel/crafted aid/reopen and transport completion proofs, preserved old formats, current-incapacity presentation, and exact remote-query task joining. [Evidence](p02/general-recovery/RESULT.md): 2,454 distinct cases have passing evidence, including 53 new and full Core1,831. Managed Server aggregate remains failed (75 pass, Windows skip, one movement deadline miss); isolated four-player journey and repeated controller73 pass without explaining timing failures. Production/native/full survival and campaign gates remain open. |
| `codex/milestone-autonomous-aftermath-20261002` | resolved by preserved branch | Existing-law autonomous care requests and living outcome acceptance; separate finite helper treatment, preserved queued human receipts, neutral controller wording and strict versioned observations. Warning-free build and 484 distinct cases pass, including 36 new; older tests/specs/staging remain exact. [Evidence](p02/autonomous-aftermath/RESULT.md). Incapacitated rescue, pending-helper failure, restart/co-player policy, full hostile retreat, native profile 36 and all production/full-campaign gates remain open; failed native-manifest selection and earlier TLS timeout remain retained. |
| `codex/milestone-live-controller-host-20261002` | resolved by preserved branch | Live established transport events, episode-bound 30-second host allowance, safe provisional rejoin and lone-player accepted-work continuation; 55 focused controller cases and affected network/shared/server evidence pass, with profile 35 native compatibility. [Evidence](p02/live-controller-host/RESULT.md). 341 distinct cases have passing evidence, but the concurrent native+managed aggregate retains a five-second TLS timeout despite isolated/managed passes. Its cause, Windows ACL, survival/restart/client control-return, profile 36 native production and full campaign gates remain open. |
| `codex/milestone-controller-journal-20261002` | resolved by preserved branch | Explicit profile 36/schema 9 durable controller events through one authority; bounded state/codec, safe restoration guards, real archive/reopen, queued paid combat and finite care preservation. Build and 2,476 distinct cases pass; original four-native-player profile 35 history replays to its exact revision 180 hash. [Evidence](p02/controller-lifecycle/RESULT.md). Old tests/specs/staging preserved; original failed proof and corrected lifecycle bookkeeping retained. Live transport/timer/survival/rejoin, restart/co-player policy, production selection/migration, native/platform/human and full campaign gates remain open. |
| `codex/milestone-native-player-roster-20261002` | resolved by preserved branch | Four actual Godot/TLS clients physically assemble, hold for a required human, close/rejoin the same actor, pay their own Wait costs, defeat the wolf, acknowledge all four outcomes and reopen exact history/ownership. Build and 440 distinct selected cases pass, seven native processes, plus real four-resident construction and automatic v2 archive/reopen. [Evidence](p02/native-player-roster/RESULT.md). Independent critique and retained failed run led to explicit observation barriers in the fixture; no production rule change. Journaled 30-second disconnect/handoff, Windows ACL, other platforms, physical devices, human art/UI and full campaign/production gates remain open. |
| `codex/milestone-player-roster-20261002` | resolved by preserved branch | Fresh one-to-four-human cargo roster, finite separately owned tools, preserved NPCs/solo bytes, physically assembled cooperative encounters and explicit bounded lifecycle catalog v2. Build and 2,768 distinct selected cases pass, including four actual TLS clients and two separate single-client Godot process cases; independent capacity/observation critique integrated. [Evidence](p02/player-roster/RESULT.md). Broad exploratory Hosting timeout remains unexplained; Windows ACL, four native clients, roster construction completion, v2 archive rollover and full phase/production/human gates remain open. |
| `codex/milestone-native-player-session-20261002` | resolved by preserved branch | Native Godot session over real TLS, source-bound presentation/status, bounded automatic inquiry refresh, separate local/remote ownership and correct initial focus. 446 distinct passing cases and five fresh Godot processes; exact saved network receipts plus fresh trade-boundary regressions. [Evidence](p02/native-player-session/RESULT.md). All prior tests/specs preserved; native 1–4-player, reconnect, other platforms and full production/human gates remain open. |
| `codex/milestone-player-client-20261002` | resolved by preserved branch | Validated authenticated client startup, bounded ordered input, exact pending requests, complete replica recovery and joined teardown; platform-trust TLS connector. Build and244 distinct affected passes include23 new and every prior server/network case. [Evidence](p02/player-client/RESULT.md). Windows ACL case unrun; Godot binding, native multiplayer, recorded disconnect and full gates remain open. |
| `codex/milestone-player-http-host-20261002` | resolved by preserved branch | Real TLS/WebSocket listener, existing saved-world dedicated composition, protected create-only invitations and joined request/process shutdown. Build and387 distinct affected checks pass on macOS, including exact prior network188, shared9 and separate original saved-journey authentication. [Evidence](p02/player-http-host/RESULT.md). Windows ACL case unrun; native clients/1–4-player proof, recorded disconnect, other platforms and full gates remain open. |
| `codex/milestone-player-queries-20261002` | resolved by preserved branch | Authenticated bounded trade inquiry and participant-only historical quote/proposal queries; independent query slot, responsive controls, exact archived terms and joined teardown. Build and 228 affected cases pass, including all prior 147 networking cases and 41 new query cases. [Evidence](p02/player-queries/RESULT.md). Independent critic found no actionable defect; initial fixture failures retained. HTTP/TLS, native clients/disconnect, history scale/latency and all full phase/production gates remain open. |
| `codex/milestone-player-observations-20261002` | resolved by preserved branch | Typed regional/player observations, captured action terms, bounded visible deltas/full recovery and immutable replica publication; real local/socket maximum-inventory delivery. Build and 237 affected cases pass, including all prior network/shared cases; independent capacity defect reproduced then repaired with bounded lossless encoding. [Evidence](p02/player-observations/RESULT.md). Wire900KiB; new expandedJSON16MiB budget. Composite maxima, production resources, HTTP/TLS, trade queries, native clients/disconnect and full gates remain open. |
| `codex/milestone-player-command-routing-20261002` | resolved by preserved branch | Authenticated readiness/movement/canonical actions, original durable receipts, bounded concurrent receive/output, exact retry after response loss and actual host reopening. Build and 69 affected cases pass, including real loopback WebSocket action and critic-requested shutdown/resource limits. [Evidence](p02/player-command-routing/RESULT.md). Observation/query codecs, HTTP/TLS, native clients, recorded disconnect and full phase/production gates remain open. |
| `codex/milestone-player-admission-20261002` | resolved by preserved branch | Actual host-issued actor/control credentials, exact compatibility handshake, bounded pending work/expiry, duplicate-owner exclusion and failed-admission cleanup over local and loopback WebSocket transports. Build and 48 focused cases pass; two independent critic defects reproduced then fixed. [Evidence](p02/player-admission/RESULT.md). HTTP/TLS, gameplay wire routing, observations, native multiple clients, durable disconnect and full phase/production gates remain open. |
| `codex/milestone-shared-player-sessions-20261002` | resolved by preserved branch | Up to four trusted in-process player ports, one host clock/writer/NPC scheduler, private input/views/inquiries, fair service, simultaneous combat, individual readings and exact care/close/reopen boundaries. Build, 2,363 distinct managed cases, fifteen fresh native processes, fresh/retained authentication and twelve actual process kills pass. [Evidence](p02/shared-player-sessions/RESULT.md). Network transport, native multiple clients, recorded disconnect handoff, independent encounter pockets, lived migration, full phases/production and art/device/human acceptance remain open. |
| `codex/milestone-native-accepted-care-20261002` | resolved by preserved branch | Full-health native walking/entry, real defeat, separate finite helper treatment, same-episode continuation and terminal outcome/reopen; truthful restoration HUD. Build, 2,354 distinct managed cases, fifteen fresh native processes, authenticated journals and twelve fresh actual process kills pass. [Evidence](p02/native-accepted-care/RESULT.md). No multi-session, lived migration, production-scale, physical/human/art or full phase/campaign acceptance; old timeout failures remain unexplained. |
| `codex/milestone-encounter-outcomes-20261002` | resolved by preserved branch | Explicit fresh profile35 reading/completion, care distinction, readable HUD/wheel, exact native selection-command-receipt-journal proof; build,2348 distinct managed cases and13 fresh native processes/authentication pass. Six genuine prior process kills retained by dependency review. [Evidence](p02/encounter-outcome-integration/INTEGRATION_REVIEW.md). Native accepted care/entry, multi-session readiness, lived migration, no-survivor resolver, unexplained historical timeouts and full phase/production/human acceptance remain open. |
| `codex/milestone-road-threat-20261001` | resolved by preserved branch | Source-bound camp chief, hostile cutoff ordering, bounded visible warning, native farm purchase/road combat/paid retreat/detour/sale/reopen, exact journal authentication and explicit terminal/hold/control contrasts. Build and2,312 distinct managed cases, fresh three-route road and ordinary-retreat rendering/auth, inherited identical-authority regressions/six-kill storage proofs, independent Opus critique and lead integration. [Evidence](p02/road-threat-corrections/lead-integration/INTEGRATION_REVIEW.md). Terminal Outcome continuation, pursuit/escort/balance, migration, multiplayer, frontier, physical/human/art and full phase/production gates remain open. |
| `codex/milestone-lawful-retreat-20261001` | resolved by preserved branch | Explicit solo retreat: complete-root terrain admission, distance-based disengagement, charged real samples, Full Wait, stable native wheel and exact durable recovery. Thirteen modes, 2,294 distinct managed cases, six actual process kills, a real checkpoint and archive cut, plus fresh/retained native journals pass. [Evidence](p02/retreat-integration/INTEGRATION_REVIEW.md). One documented old expiry-checker correction; original failures preserved. No party/pursuit/road-threat, lived migration, physical-device, human art/UI, frontier or full phase/production acceptance. |
| `codex/milestone-cargo-world-20261001` | resolved by preserved branch | Managed Marla/Kegga cargo circuit with real source lots, finite funds, continuous travel, preserved save prefix, inherited wolf/water joins, declared source overlays and blocked-route revalidation. Build and 481 distinct managed cases pass on exact final source/binaries; independent Opus critique and lead counterexamples integrated. [Evidence](p02/cargo-world/INTEGRATION_REVIEW.md). No native cargo, route threat/escort, traveling NPCs, lived migration, multiplayer, frontier, human or full phase/production acceptance. |
| `codex/milestone-native-trading-refinement-20261001` | resolved by preserved branch | Independent critique, controller dropdown cancellation, per-route navigation/row selection, native expiry/funding/moved-issuer recovery and readable trading cards. Build, 459 distinct managed cases, three-mode trading/gift and three keyboard boundary journeys with exact artifacts/pins. [Evidence](p02/native-trading-refinement/INTEGRATION_REVIEW.md). Injected input only; physical/human, multi-stall, archived-only history, migration, cargo, multiplayer and full phase/production gates remain open. |
| `codex/milestone-native-trading-foundation-20261001` | resolved by preserved branch | Production market entry, finite native windows, explicit buy/show/sell, pause-to-read and actual saved results. Build, 454 distinct managed cases and three-mode trading/gift native runs with journal authentication; protected pins exact. [Evidence](p02/native-trading/INTEGRATION_REVIEW.md). Dropdowns still use keyboard; native expiry/funding/moved-issuer contrasts and full native UI, physical/human, migration, cargo, multiplayer and production gates remain open. |
| `codex/milestone-scheduled-trading-20261001` | resolved by preserved branch | Participant-safe quotes/history, transient inquiries, scheduled merchant/NPC trades, bounded reconsideration, gift/work arbitration, market-world excavation and observed-root view refresh. Final build and 291 distinct managed cases pass; independent input critique and lead regressions repaired; all original pins preserved. [Evidence](p02/scheduled-trading/INTEGRATION_REVIEW.md). No native trading, lived market migration, full economy, new process-kill or phase/production acceptance. |
| `codex/milestone-local-markets-20261001` | resolved by preserved branch | Fresh profile 33 mobile markets, actual-stock integer pricing, private dated knowledge and explicit selected-lot showings over atomic paid history. Build and 1,915 distinct managed cases pass; independent critique and both findings repaired with failing-then-passing regressions; existing pins unchanged. [Evidence](p02/local-markets/INTEGRATION_REVIEW.md). No scheduled/native trading, lived migration, profile-33 process kills or full phase/production acceptance. |
| `codex/milestone-paid-world-constraints-20261001` | resolved by preserved branch | Legitimate third-human competition in both canonical/submission orders, actual raw-plus-produced capacity, protected/reserved stock and committed late-liquid rollback. Build and 32 focused paid Host passes, independent lead rerun, all original pins preserved; runtime unchanged. [Evidence](p02/paid-world/STOCK_GENESIS_INTEGRATION.md). Closes the three original boundary gaps; no new process/scale, native, multiplayer or phase acceptance. |
| `codex/milestone-paid-world-boundaries-20261001` | resolved by preserved branch | Nine new actual Host contrasts for combat/care/death/manipulation/readiness, competing gift/payment, reward funding and seller fees; runtime unchanged. Build,24 paid Host cases, retained source-equivalent broad regression totaling1,877 distinct managed cases; four process kills/six SQL corruptions on final Host. [Evidence](p02/paid-world/BOUNDARY_INTEGRATION_REVIEW.md) retains Opus/lead fixture failures. Eligible-counterparty race, capacity/protection and late-rollback proofs remain open; no full assignment, native, production or phase closure. |
| `codex/milestone-paid-world-lifecycle-20261001` | resolved by preserved branch | Explicit fresh profile32 atomic paid goods/currency, saved gifts, complete causal history and ordinary continuation; issuer withdrawal and exact same-fill supersession. Build,1,868 distinct managed cases, four actual process kills and six SQL corruption refusals; independent Opus critic/repair and current architecture. [Evidence](p02/paid-world/INTEGRATION_REVIEW.md) retains the proof-connection failure and incomplete original delivery. Original hosted boundary-evidence completion, pricing, scheduled/native trading, migration and all full phase/production gates remain open. |
| `codex/milestone-atomic-spot-exchange-20260930` | `9867ecb` | Conserved standalone raw/produced spot exchange with explicit continued provenance, exact fees/allowances and currency ownership. Build and1,728 distinct managed cases, independent critique and current architecture. [Evidence](p02/spot-exchange/INTEGRATION_REVIEW.md). This standalone milestone does not claim paid world admission, native markets or full phase acceptance. |
| `codex/milestone-retained-native-gifts-20260930` | resolved by preserved branch | Native continuation of the original520-use world through a carried gift, acceptance, own-use521 and separate reopen; exact historical prefix. Immutable resource encoding reduces complete-replay cumulative allocation about11.2%. Build and1,985 distinct managed cases, retained/fresh native keyboard journeys pass. [Evidence](p02/retained-gift-native/INTEGRATION_REVIEW.md). Startup consistency, physical devices, archived-only native lookup, new process-kill and full production/phase gates remain open. |
| `codex/milestone-retained-proposal-transition-20260930` | resolved by preserved branch | Explicit profile27-to30 copy-and-publish transition, exact prior history and managed original520-to521 gift/accept/own-use journey through a real automatic archive. Clean build and 2,051 distinct managed passes (Core 1,692; Host 359), independent SQL prefix audit and current architecture. [Evidence](p02/retained-proposal-transition/INTEGRATION_REVIEW.md) retains Opus delivery, lead critique, failures, diagnostic correction and all preservation pins. Native transitioned-save selection/journey, actual transition-boundary process kills and full phase/production gates remain open. |
| `codex/milestone-proposal-interaction-boundaries-20260929` | resolved by preserved branch | Exact explicit own-lot gift preparation, bounded background participant/archive inspection and ordinary profile30 excavation; genuine pending16 refusal preserves the separate interpretation. Clean build, 165 distinct affected managed passes including 34 focused, source critic/shutdown correction and current architecture. [Evidence](p02/proposal-interaction-boundaries/INTEGRATION_REVIEW.md) retains failures and exact 488 old files/23 frozen specs/unrelated staging. Native gift UI, control/network binding, production latency/indexing, retained520-dose and full phase gates remain open. |
| `codex/milestone-npc-material-decisions-20260929` | resolved by preserved branch | Opt-in immutable NPC medicine preferences issue actual own-control offers/replies at existing cutoffs; later player acceptance/use, supplier-only aid deferral and stable decisions survive refusal/expiry/real cut/reopen. Clean build, 154 distinct affected managed passes including 23 focused cases, native Astra source criticism/corrections and current architecture. [Evidence](p02/npc-material-decisions/INTEGRATION_REVIEW.md) preserves failures, 486 old files, 23 frozen specs and unrelated staging. Archived session inspection, pending-excavation joins, general NPC/policy authoring, recurring need episodes, native/control/network binding, retained520-dose and all full phase/production gates remain open. |
| `codex/milestone-scheduled-player-proposals-20260929` | resolved by preserved branch | All four player proposal intents use actual session cutoffs, participant views and valid-only sender work arbitration; an accepted owned medicine is used at a later cutoff. Clean build, 142 distinct affected managed passes including 11 focused cases, independent native Astra source critique and current architecture. [Evidence](p02/scheduled-material-proposals/INTEGRATION_REVIEW.md) retains the failed Fable run, original analyzer failure, test corrections and exact preservation. Explicit NPC gift/decision policy, pending-excavation/profile joins, archived session inspection, native/control/network binding, retained520-dose journey and full phase/production gates remain open. |
| `codex/milestone-consented-custody` | resolved by preserved branch | Standalone bilateral raw counted-source transfers preserve existing custody and OwnerOnly policies. Clean solution build, 147 affected cases, independent Fable critique, eight retained pre-fix counterexamples and same-change architecture; [evidence](p02/consented-custody/README.md). Produced goods, complete-world/history admission, durable dual-party receipts/offers, native gameplay and the original personal-dose route remain open. No phase or production gate closes. |
| `codex/milestone-resource-batch-encoding` | resolved by preserved branch | Exact immutable batch-owned V1 bytes preserve public buffer isolation, complete replay and all admission/codec/schema rules. Clean build; 253 distinct managed cases, three identical original fixture files, six rendered native routes/twelve processes and actual ledger/corruption authentication; independent Fable critique and same-change architecture. [Evidence](p02/resource-batch-encoding/README.md). Complete 1,581-entry replay allocation falls 13.43% with the same head and validation-call counts. Production latency/memory/lifetime, personal-dose acquisition and full-phase gates remain open. |
| `codex/milestone-native-startup-attribution` | resolved by preserved branch | [Disposable-copy native/Host timing](p02/native-startup-profile/README.md) isolates full replay/encoding from small client attachment, verifies actual .NET10 runtime and exact failed-save preservation. Original30s native failure remains open; no production source or architecture change, no gate relaxation. Fable timed out after completing diagnostic; returned model/cost unavailable. |
| `codex/milestone-coordinated-save-selection` | resolved by preserved branch | Exact-origin existing-save selection and explicit profile27 client attachment. Clean build; 129 substantive managed passes, five legacy native routes and full legacy authentication; current architecture and independent critique/fixes. [Evidence](p02/coordinated-native/README.md). Final large-world withdrawal passes, but separate native reopen exceeds the unchanged 30s opening gate; native timing, original personal-dose acquisition/use and production/full-phase gates remain open. |
| `codex/milestone-care-definition-digests` | resolved by preserved branch | Exact-owner lazy care/grant digests with unchanged codecs, admission and permission semantics. Clean build; 1,724 distinct cases (full Core and 141 affected Hosting), 53 identical outputs, independent source review and current architecture. [Evidence](p02/care-definition-digests/README.md). Actual normal/BeforeCommit/AfterCommit matrix passes ten commands/two SIGKILLs under unchanged bounds; cumulative replay allocation falls 10.3% with all admission calls retained. Native post-cut, production and full-phase gates remain open. |
| `codex/milestone-world-canonical-frame` | resolved by preserved branch | Exact immutable candidate-frame reuse at existing rejection boundaries, defensive byte ownership and legacy Host fallback. Clean build; 2,473 full Core/Hosting passes, 53 identical retained outputs, independent source review and current architecture. [Evidence](p02/world-canonical-frame/README.md). Same-ledger replay allocation fell 24.2%; unchanged external normal recovery timed out before planned kills. Timing, complete crash/native, production and full-phase gates remain open. |
| `codex/milestone-coordinated-world-managed` | resolved by preserved branch | Explicit fresh world27/schema8 joint material/care/resource continuation over the actual ordinary-care world. Clean build; 2,465 distinct managed passes, 49 exact legacy fixtures, real 520-dose/520-recraft journeys, wet mining and concurrent funded builders across cuts, independent source review and same-change architecture. [Evidence](p02/coordinated-world-continuation/README.md). Bounded managed scope only: external recovery exceeded its unchanged 30-second gate twice; AfterCommit kill and native post-cut qualification remain open. Instrumented recovery identifies repeated serialization as the next investigation, without changing validation or deadlines. |
| `codex/milestone-excavation-material-continuation` | resolved by preserved branch | Joined dry excavation/shared gathering work, exact source/terrain/effort/frontier preservation and Production V8/component archives. Clean build; 1,556 full Core +372 affected Hosting cases, 48 exact retained binary fixtures, actual 511/work-512 boundary, real construction preservation and independent identity-role critique/fix. [Evidence](p02/excavation-material-continuation/README.md) retains failures and same-change architecture. Standalone component only; complete-world care/material/resource, wet/physical joins, authenticated resume, native and full production gates remain open. |
| `codex/milestone-transport-material-continuation` | resolved by preserved branch | Complete-owner transport cuts, typed reserved delivery and explicit transport V3 / liquid V4 / Production V7 retain exact material, journal, identities and clocks. Clean build; 1,556 full Core +331 affected Hosting cases, 24 exact legacy fixtures, actual 511/512 boundary, mixed three-cut split delivery and independent critique. [Evidence](p02/transport-material-continuation/README.md) retains test-only integration failures and current architecture. Standalone component only; excavation, whole-world care/material/resource and liquid/terrain joins, durable authentication/recovery, native and full production gates remain open. |
| `codex/milestone-terrain-material-continuation` | resolved by preserved branch | Qualified actual-registry terrain cuts, typed refine/demote evidence and terrain V4 / Production V6 archives preserve exact source rights, active cells and construction capability. Clean build; composite 1,535 Core +331 affected Hosting final passes, 24 exact legacy fixtures, actual 512 boundary and three-cut mixed journey, independent critique/fix review and same-change architecture. [Evidence](p02/terrain-material-continuation/README.md) retains both diagnostic failures and replay-cost observations. Standalone components only; reserved transport/excavation, combined liquid/terrain and world care/material joins, durable authentication/recovery and full production gates remain open. |
| `codex/milestone-liquid-material-continuation` | resolved by preserved branch | Explicit actual-owner runoff/capacity cuts, exact geometry qualification and nested V3 archives retaining Production V3/V4/V5 capabilities. Genuine craft/recraft/consume and raw/produced flow across cuts, pending obligations and separate counted/physical headroom controls. Clean build, 1,514 Core +238 affected Hosting cases, 23 exact pre-change V1/V2 fixtures and independent critique/fix review; same-change architecture. [Evidence](p02/liquid-material-continuation/README.md). Standalone components only; terrain/transport/excavation joins, complete-world care/material closure, authenticated archives, native and full production gates remain open. |
| `codex/milestone-construction-continuation` | resolved by preserved branch | Explicit V5 reserved construction, bounded original source batches, immutable recipe classification and current-frame work qualification. Actual two-project completion, byproduct recrafting and two further cuts; 512-transfer capacity boundary. Clean build, 1,491 Core + 257 affected Hosting cases; independent critique/fix review and same-change architecture. [Evidence](p02/dependent-material-continuation/README.md). Standalone component only; full world/care/liquid/terrain joins, authenticated durable work resume, archive recovery, native and production gates remain required. |
| `codex/milestone-material-custody-continuation` | resolved by preserved branch | Explicit V4 ordered gathering, raw transfer/reserved delivery, craft and consumption; actual source/effort ownership, 13 mixed operations and 1,028 operations across two cuts. Clean build, 1,468 full Core + 89 affected Hosting cases; independent capacity critique and same-change architecture. [Evidence](p02/material-custody-continuation/README.md). Standalone material only; complete-world/care/dependent-owner admission, durable archive authentication, native use and production gates remain required. |
| `codex/milestone-production-continuation` | resolved by preserved branch | Explicit context-bound Production V3, exact imported rights and storage/admission pins, permanent identity lookup, 530 consumptions and 531 crafts across two cuts. Clean build, 2,229 full Core/Hosting cases and independent review with storage-lineage fix; same-change timeless architecture. [Evidence](p02/production-continuation/README.md). Standalone domain only; coordinated care/world/Host, new issuance/custody and dependent-owner integration remain required. |
| `codex/milestone-material-continuation-basis` | `d23d94d` | Immutable exact material basis, remaining output intervals, direct raw retirement and nonrecursive producer witnesses. Clean build, 235 affected Core cases and independent review; same-change timeless architecture. [Evidence](p02/material-care-continuation/README.md). Structural capture only; no continued owner/world/Host interpretation or history-capacity relief at this checkpoint. |
| `codex/milestone-restorative-recovery` | resolved by preserved branch | Profile26 raw/crafted self-use, helper assistance, stocked patient revocation and actual resource-cut/use atomic recovery. Six managed cases; ten SIGKILL probes, 61 Host calls and eleven corruption refusals, all fifteen databases checked for exact authorized receipts. Runtime unchanged; independent source review and same-change architecture/provenance proposal. [Evidence](p02/restorative-recovery/README.md) preserves fixes, source pins and full archived saves. Lossless material/care/crafting continuation, journal lifetime, combat items and full production/human gates remain open. |
| `codex/milestone-restorative-interface` | resolved by preserved branch | Public self-use/patient withdrawal, separate Care wheel page, helper-owned full-dose policy and stable no-retry choice, exploration health cue, and post-combat gathering clock correction. Clean build; 546 distinct affected managed cases with passing final outcomes; ten new rendered processes with five database authentications, plus two legacy native controls/four Host reads. Same-change architecture and independent source review. [Evidence](p02/restorative-interface/README.md) retains failures, exact checkpoints and remaining lifetime material/care, combat, crash, production and human gates. |
| `codex/milestone-care-session` | resolved by preserved branch | Patient acknowledgment and separately controlled Host-helper treatment at distinct ready cutoffs; public care projection, durable no-retry identity guard and preserved profile 25 excavation. Clean build, 707 Hosting/client cases, unchanged 1,355-case Core baseline, four combined archive/treatment SIGKILL probes and 20 Host operations. Same-change architecture and bounded independent review. [Evidence](p02/care-session/README.md); native/ordinary care, other owner lifetime, production scale and full phases remain open. |
| `codex/milestone-durable-resource-archives` | resolved by preserved branch | Explicit schema 7/profile 25 atomic resource archives and finite aftermath care; exact stored-byte replay/retrieval, separate storage accounting, two actual cuts and255/two-reward rollback. Clean build, 2,053 managed cases, six external kills/30 Host operations and native critique pass. [Evidence](p02/durable-resource-archives/README.md) retains original failures and source identity. Combined pending-care/archive treatment, session/native/ordinary care, other owner lifetimes, scale and full phases remain open. |
| `codex/milestone-resource-continuation` | resolved by preserved branch | Pure Core whole-batch archive cuts, exact retrieved closing roots and bounded anchors; actual520-resource-command replay across two256-batch cuts.2,043 managed cases plus final23-case follow-up, independent byte oracle and native critique pass. [Evidence](p02/resource-continuation/README.md) preserves source scope and original compile failure. SQLite publication/authentication, other owner lifetime limits, ordinary/native care and all full phases remain open. |
| `codex/milestone-world-resource-integration` | resolved by preserved branch | In-memory profile25 actual attack/MP, funded XP and finite aftermath care with complete causal resource history. Composite qualification covers2,039 distinct managed cases, final clean build,19 affected journeys and two fixed independent critic findings. [Evidence](p02/world-resource-integration/README.md) distinguishes earlier full Hosting bytes from final profile-only follow-up. Durable genesis remains disabled; authenticated continuation, ordinary/durable/native treatment, performance and all full phases stay open. |
| `codex/milestone-resource-history` | resolved by preserved branch | Ordered complete actor revisions with explicit basis-pinned archives; isolated clean build and 1,949 managed tests, independent six-effect oracle/five corruptions, and Fable Max critique. [Evidence](p02/resource-history/README.md) retains failures and five review tests. No live-world causal admission, archive rollover, care/profile25, durable/native treatment, performance or full-phase acceptance. |
| `codex/milestone-resource-composition` | `604b6d0` | Named damage, restoration, exact MP and reward transitions; source-backed one-dose self/external restorative component composition. Exact nine-file implementation/test scope builds cleanly over committed code and passes 1,222 Core + 665 Hosting/client = 1,887 tests. [Evidence](p02/resource-composition/README.md) preserves failures, 200 checked links and unchanged prior state. Generic persistent history, complete-world care/profile25, native recovery and all full phase gates remain open. |
| `codex/milestone-world-composition-design-20260925` | `24941f6` | Reviewed hybrid world-composition decision, healing/acknowledgment separation, restrained classic-motion research and project doctrine. 184 local links resolve; 22 frozen specs, 48 prior markers and unrelated index preserved. Documentation checkpoint only; care implementation remains separate WIP and native motion is not qualified. Ecosystem lessons committed as `88d41e3`. |
| `codex/milestone-item-consumption` | `04e6dba` | Pure-C# raw/produced consumption candidates, exact retained provenance and command identity, V2 archives, preserved subsequent crafting and V1 separation. Clean solution build; all1,849 managed cases, four independent byte/hash vectors and two malformed archives pass. [Evidence](p02/item-consumption/README.md) retains critic fix/build failures and raw issuance limits. Existing worlds reject the owner; no live healing, care profile, durable/native recovery or full-phase gate closes. |
| `codex/milestone-encounter-choice-validation` | `04c4ce1` | Seven native costed Waits across wheel/keyboard/controller/pointer, actual player HP0, usable inspection and exact paused reopen. Two rendered Godot processes, five Host reads and193 independently reconstructed encounter roots/15 receipts/225 identities pass; six images independently reviewed. [Evidence](p02/encounter-choice-validation/README.md) retains original failure and recovery design. Core/Hosting unchanged; aid remains designed, not implemented, and no full phase/art/device gate closes. |
| `codex/milestone-native-encounters` | `43b2ac1` | Pure-C# Godot encounter controls, exact public offers, contextual wheel/buttons, observed-HP feedback and persistent incapacity over unchanged profile24/schema6 authority. All645 Hosting/client cases, final three-process rendered journey, four exact-entry Host reads and original headless farm/wheel controls pass. [Evidence](p02/native-encounters/README.md) retains failures and independent source/image review. No new full Core regression; native confirmed Wait/player defeat, broader lifecycle/abilities, co-op, scale and all full art/device/phase gates remain open. |
| `codex/milestone-encounter-lifecycle` | `cc31f08` | Pure-C# profile24/schema6 exploration, authenticated entry, in-place staging, physical combat, outcome release and second encounter retain readiness debt and fractional time. Composite qualification covers 1,781 distinct managed cases (1,779 full regression plus final Core/encounter checks), all188 oracle roots/187 commits,17 Host calls/four external kills and old encounter/native farm controls. [Evidence](p02/encounter-lifecycle/README.md) retains source findings and exact coverage limits. Native battles, broader lifecycle/effects, co-op/scale and all full phases remain open. |
| `codex/milestone-encounter-actions` | `60a2463` | Fresh profile23/schema5 source-backed physical attacks, real HP/MP, saved damage draws, actual body/range legality and retained incapacity/outcome;1,719 managed cases, independent176-root/175-commit oracle,21 Host calls/four kills, old Wait and headless farm/foundation controls. [Evidence](p02/encounter-actions/README.md) retains findings/failures. Pure C#; entry/release, native battles, broader effects/lifecycle, co-op/scale and full phases remain open. |
| `codex/milestone-live-encounter-pacing` | `9770fa5` | Complete world/schema5/session Wait timing, exact 50 ms exposure and retained passive-process carry;1,676 managed cases, independent44-root/43-commit oracle,14 ordinary Host calls/two kills and headless farm/foundation controls. [Evidence](p02/live-encounter-pacing/README.md). Pure C#; fresh coordinated Wait-only admission, with attacks, entry/release, native combat, co-op closure and full phase/production gates open. |
| `codex/milestone-encounter-timing` | `0b01a2e` | Portable C# Wait readiness/required-choice barrier, exact scoped archives and exposure carry; 1,064 Core tests, independent four-vector exposure oracle and35 Host processes including six separate-process recoveries. [Evidence](p02/encounter-timing/README.md). Canonical component arithmetic is distinct from admitted history/effects; no live world profile, SQLite combat recovery, co-op fallback, native combat or full-phase acceptance. |
| `codex/milestone-native-terrain-breach` | `e45eec1` | Separate native breach save, owned excavation/public capabilities, captured Mine and conserved rendered terrain/water. 1,605 managed tests plus10 targeted label checks; final3-process partial/reopen/breach recovery, legacy farm/rendered watercourse and foundation controls pass. [Evidence](p02/native-terrain-breach/README.md) retains visual failures and independent corrections. Pure C#; no Core/storage law change or full phase, wet-occupancy, loads/gates, art/device/scale acceptance. |
| `codex/milestone-terrain-liquid-breach` | `6fce6ba` | Fresh profile21 terrain-derived capacity, earned raised-bank excavation and atomic conserved substrate/water publication. 1,595 managed tests, independent exact post-breach oracle, 21 Host operations/two external kills, native foundation/farm controls and unchanged 1,334-commit worksite recovery. [Evidence](p02/terrain-liquid-breach/README.md). Finite provenance certificate; near16MiB reserve threshold is source-reviewed only. Native breach interface, wet occupants/loads, scalable continuation and full phase/production gates remain open. |
| `codex/milestone-native-watercourse` | `3df0937` | Finite source-backed water beside the real farm, public cell geometry, native editable surface art, ordinary walking/dialogue/funded work and exact two-process rendered recovery. 1,543 managed tests, legacy native/foundation controls and independent source/image review. [Evidence](p02/native-watercourse/README.md) retains the inherited far-bank construction access limit, safe refusal/resumption, original visual failure and corrected framing. Provisional cutaway/art; wet interactions, scale and full phase/production gates remain open. |
| `codex/milestone-liquid-cell-flow` | `7ae5cad` | Fresh profile20 exact raw/produced cell placement, conservative starting-budget flow, complete action/hold/time bindings and schema3 recovery. 1,536 managed cases, independent13-state oracle, 37 Host calls/two external kills, actual durable fragment-overflow rollback and headless native/history controls. [Evidence](p02/liquid-cell-flow/README.md) preserves the incomplete interrupted run. Recoverable fluid continuation, gates/loads/breaches/immersion, rendered water, other materials and all full phase/production gates remain open. |
| `codex/milestone-reservoir-containment` | `5441293` | Fresh profile19 saved sealing policy, exact material pins and mandatory fixed nominal-envelope admission; real crafting/construction, containment-only completion rollback, exact history and schema3 atomicity. 1,499 distinct managed cases, 12 Host calls/two external kills, headless foundation/farm and unchanged 1,334-commit worksite recovery. [Evidence](p02/reservoir-containment/README.md). Physical gates, fluid loads, breach/spill consequences, immersion, playable water, production scale and full phases remain open. |
| `codex/milestone-playable-worksite` | `7da8fcc` | Two genuinely funded projects, stairs/decks, contextual mining/climbing, exact visible settlement and four-process native save/recovery with unchanged deadlines; 1,487 managed cases and final legacy wheel control pass. Pure-C# replay improvements preserve all 1,334 historical hashes. [Evidence](p02/playable-worksite/README.md) retains failures, source review and measured startup/allocation limits. Provisional art/UX, OS/devices, multiplayer, wet composition, production history/performance and full phases remain open. |
| `codex/milestone-live-event-descent` | `e668cd7` | Fresh profile18 live intact event settlement and exact durable continuation/publication; one-pass closed-save historical reader preserves complete verification. 1,459 distinct retained/new tests, 83 Host calls/six external kills plus two exact operator controls, legacy fixture/source/spec/index preservation. [Evidence](p02/live-event-descent/README.md) retains the original timeout and measured roughly 50% allocation reduction. Common 256-subunit live fall only; unequal release, fracture/injury, visual/device and production/full-phase gates remain open. |
| `codex/milestone-event-descent-assessment` | `25da978` | Event-ordered unequal intact geometric descents, stopped-body reinsertion and complete-world read-only assessment with exact impact demand. 1,435 distinct tests, eight actual Host inspections, unchanged save bytes and bounded partial Fable critique. [Evidence](p02/event-descent/README.md). No live profile change, fracture/debris/injury, production latency/rendering or full-phase closure. |
| `codex/milestone-coupled-settlement` | `463509d` | Fresh profile17 live cross-assembly support and one shared intact harmless trajectory, complete outside sweeps, rider/material conservation and dynamic durable/journal reservations. 1,409 tests, 59 ordinary Host calls/six kills, headless foundation/farm, 20 new/26 legacy exact files. [Evidence](p02/coupled-settlement/README.md) retains the reproduced admission defect and partial external review. Unequal trajectories, fracture/impact/injury, wet composition, presentation, production performance and full gates remain open. |
| `codex/milestone-coupled-contact-assessment` | `7e16d11` | Read-only exact foreign-assembly support, complete candidate/policy pins, shared mass accounting and conservative load neighborhoods; 1,385 tests, 26 byte-exact prior fixtures and preserved source/design/index contracts. [Evidence](p02/coupled-contact-assessment/README.md). Live coupled motion/publication and whole phase gates remain open. Three bounded external critics yielded a contract clarification, no verified runtime defect and one budget failure; fleet expansion remains evidence-dependent. |
| `codex/milestone-farm-input-foundation` | `b1482db` | Connected farm/gather/craft/quest/NPC construction and client input foundation; 825 tests and retained native journeys. [Evidence](p01/input-focus/README.md). Provisional art, general UI, physical device/export qualification and full phase exits remain open. |
| `codex/milestone-action-previews` | `b40f28c` | Captured farm action offers, visible costs and read-only inspection; code `3ae7855`, 832 tests, all retained native journeys and rendered review. [Evidence](p01/action-offers/README.md). Full wheels/management, physical devices, art acceptance and phase exits remain open. |
| `codex/milestone-explicit-targeting` | `747925f` | Explicit Wren/harvestable selection, stable target identity and incompatible-verb blocking; code `c7e29f5`, 838 tests, all retained native journeys and eight rendered states. [Evidence](p01/target-selection/README.md). General pointer targeting/confirmation, full UI, physical devices, art and phase exits remain open. |
| `codex/milestone-inventory-inspection` | `ea56b64` | Native own-pack inspection, host timing display and scoped input; code `3eed739`, 846 tests, all final native journeys and twelve rendered states. [Evidence](p02/inventory-inspection/README.md) retains an earlier travel timeout with unconfirmed cause. Equipment/use/transfer, icons, full UI, OS/physical-device acceptance and phase/feasibility gates remain open. |
| `codex/milestone-recorded-history` | `9e0018d` | Exact recorded-commit reconstruction and operator; code `913a5f2`, corrected stationary quest probe `1d26210`, 855 final tests and combined native evidence. [Evidence](p01/recorded-history/README.md) preserves failed runs, confirmed stale Talk revision and unconfirmed editor-startup error. Arbitrary interior time, prehistory, branch travel, production latency, independent review and full phase gates remain open. |
| `codex/milestone-daylight` | `80bdda0` | Calendar-driven outdoor light and bright target/UI cues; 855 tests, complete integrated regression plus probe-only night-interface followups, ten daylight and twelve daytime UI captures. [Evidence](p02/daylight/README.md) retains the corrected unmapped probe input and recurring, unconfirmed editor-settings error. Weather/seasons, rest/camp, production art/audio, independent/human review and all full gates remain open. |
| `codex/milestone-quick-wheel` | `c8989ae` | Native four-action wheel, pinned inspection/confirmation, controlled background rendering and removal of legacy fixture refocus loops; 861 tests, separate complete native qualification, four affected fixture followups and sixteen rendered frames. [Evidence](p02/shared-desktop-validation/README.md) retains the failed integrated editor startup and precise source/binary boundaries. This is combined qualification, not one uninterrupted verifier pass. Full UI, OS/devices, co-op, art, independent/human reviews and phase gates remain open. |
| `codex/milestone-watershed-persistence` | `bf96ed9` | Explicit genesis-admitted watershed in the composed-world authority; 883 tests, real before/after-commit process kills, exact history/restart, independent outer archive/pin checks and native foundation/farm regression. [Evidence](p01/watershed-admission/README.md). Default farm terrain is unchanged; macro-to-local realization, basin composition, fluid custody, migration, production performance and all full gates remain open. |
| `codex/milestone-architecture-reference` | `2f91983` | Timeless architecture reference, source-binding backfill and upkeep hook. [Review/checks](architecture-reference/README.md) cover 123 reference links, bounded factual critique, frozen-spec/index preservation and five synchronized skill entrypoints (ecosystem `4e83086`). Documentation checkpoint only; no gameplay or phase acceptance is inferred. |
| `codex/milestone-watershed-networks` | `23851fa` | Finite basin composition, canonical archive, exact contributor propagation and shared faces; 904 tests, five independent archives, 23 real host inspections and bounded critic review. [Evidence](p01/watershed-network/README.md) preserves the reproduced scope-alias defect and correction. No live-world/network admission, fluid custody, local realization, performance or full phase acceptance is inferred. |
| `codex/milestone-watershed-network-persistence` | `b2176e5` | Complete finite network retained in explicit fresh profile-seven world; 919 tests, 74 existing inspections, 27 real operator calls, both process-kill boundaries, exact history and headless foundation/farm regression. [Evidence](p01/network-admission/README.md) retains fixture failures and bounded critic coverage limits. Default farm, prior saves/encodings and all byte/recovery gates remain unchanged. No local realization, fluids, played-save migration, performance or full phase acceptance is inferred. |
| `codex/milestone-watershed-terrain-mapping` | `41865ed` | Retained dry macro-to-local mapping with exact integer scale, vertical windows and ancestral addresses; 936 tests, six independent archives, 27 real host inspections and bounded critic review. [Evidence](p01/terrain-mapping/README.md) retains compiler failures and scope limits. Separate candidate/archive only: no live terrain/material/fluid admission, visual change, performance or full phase acceptance. |
| `codex/milestone-terrain-material-allocation` | `163a7bb` | Finite funded slab and exclusive coarse/detailed source partitions; 957 tests, 24 final real host inspections, independent interval/outer-format checks and bounded native critique. [Evidence](p01/terrain-materials/README.md) includes partial/interior actual transfers, 4,096-lot/active-cell caps and retained analyzer failures. Standalone trusted composition only: no live mining, retirement, world allocation registry, performance or full phase acceptance. |
| `codex/milestone-material-registry` | `b47fec7` | One shared inventory for 1–64 disjoint terrain allocations, normalized archive and shared immutable macro data; 971 tests, 26 registry plus 24 prior-material host inspections, independent ownership checks and bounded native critique. [Evidence](p01/material-registry/README.md) retains maximum-network allocation measurements and corrected fixture failures. No durable world admission, live mining, retirement, production performance or full phase acceptance. |
| `codex/milestone-material-production` | `a1783a9` | Shared terrain/Production proof, direct raw retirement conservation, exact retained history and explicit v2 archives; 983 tests, 20 new plus 50 legacy real-host inspections, independent interval/spatial accounting and bounded native critique. [Evidence](p01/material-production/README.md) retains corrected fixture/compiler failures. No live mining, durable world admission, long-lived history compaction, production performance or full phase acceptance. |
| `codex/milestone-runoff-budgets` | `02f2819` | Funded effective-runoff projection over exact finite drainage, shared physical/counting receiver headroom and canonical archive; 995 tests, 25 real host inspections, five independent origin-path/source checks and bounded native critique. [Evidence](p01/runoff-budgets/README.md) includes the 65,536-column profile and retained test-first failure. No hydraulic flow, receiver-capacity reservations, complete macro profiles, durable world admission, performance or full phase acceptance. |
| `codex/milestone-runoff-capacity` | `83db425` | Shared ownership of pending runoff headroom, stale-safe cancellation and actual custody/Production reconciliation; 1,004 managed tests, nine focused followup cases and 46 host inspections with independent volume/source checks. [Evidence](p01/runoff-capacity/README.md) retains failures, old archive compatibility and execution boundaries. Standalone finite owner only; live flows, physical reservoirs, durable world admission, production performance and full gates remain open. |
| `codex/milestone-reservoir-geometry` | `f00e637` | Finite authored watertight vessels, exact actual/projected fills, source/buffer/receiver composition and pinned wrapper plans; 1,018 tests, 26 host inspections and an independent binary-search fill oracle. [Evidence](p01/reservoir-geometry/README.md) preserves test-first failure, old capacity bytes and 4,096-column bounds. No live flow, terrain-wall proof, durable world admission, production performance or full-gate closure. |
| `codex/milestone-reservoir-persistence` | `a3789c1` | Fresh profile-eight exclusive physical owner; atomic material actions, canonical recovery/history, full 1,035-case regression plus five stronger follow-ups (1,036 distinct), 55 real operator calls/four process kills and native headless foundation/farm. [Evidence](p01/reservoir-admission/README.md). Assumed watertight supports; no live flow, terrain support, migration, performance or full phase acceptance. |
| `codex/milestone-runoff-controls` | `204d262` | Durable funded admission/cancellation inside profile eight, exclusive reservation purposes and exact capacity-only recovery; 1,059 managed cases, 73 real host calls/six process kills, headless foundation/farm and bounded native critique. [Evidence](p01/runoff-commands/README.md). Existing source holds only; no terrain wall proof, live flow, player controls, performance or full phase acceptance. |
| `codex/milestone-reservoir-transport` | `6ef6e83` | Actual conserved discrete gravity-gated delivery in fresh profile nine, shared step throughput, reading holds and exact source/history; 1,081 distinct managed cases, 95 operator calls/eight process kills and headless foundation/farm. [Evidence](p01/reservoir-transport/README.md). Authored seals/fixed topology; no continuous fluids, terrain support, flooding, player controls, performance or full phase acceptance. |
| `codex/milestone-reservoir-boundaries` | `5dd8110` | Exact read-only nominal containment assessment against retained terrain and completed material, explicit sealing/thickness policy, gap/obstruction/gate failures and recorded-world operator; 1,118 tests, 21 actual inspections/five unchanged saves and headless foundation. [Evidence](p01/reservoir-boundaries/README.md). No changed world admission, terrain mutation, strength, breach dynamics, physical gate proof or full phase acceptance. |
| `codex/milestone-live-excavation` | `7df18df` | Durable selected-cell excavation in fresh dry profile ten: actual player/NPC effort, exact source entitlement, atomic whole-cell issuance/removal and direct support refusal; 1,141 tests,144 ordinary Host calls/two process kills, exact history and native headless foundation/farm. [Evidence](p01/live-excavation/README.md) retains corrected critic findings and an unconfirmed initial parallel-build child exit followed by a clean full serial build. No generated mining frontier, wet-world composition, collapse, player mining controls, production balance/performance or full-gate closure. |
| `codex/milestone-constructed-support` | `2dcdd79` | Fresh dry profile11 with completed-member actor footing, exact XY support and earned assisted vertical traversal;1,173 tests,26 real Host calls/two process kills, exact history and native headless foundation/farm. [Evidence](p01/constructed-footing/README.md) proves real quest-funded NPC construction then actual climb/reopen. No ordinary stair routing, load strength/collapse, wet-world composition, player climb controls, production performance or full-gate closure. |
| `codex/milestone-stair-navigation` | `d4f01b4` | Fresh dry profile12 ordinary stairs and bounded directed multi-level routes; 1,189 tests, 20 new-profile Host calls/two process kills, legacy26/two-kill regression and native headless foundation/farm. [Evidence](p01/stair-navigation/README.md) proves a real quest-funded nine-tread/deck assembly, 585-commit player/NPC journey, stacked occupancy and automatic downstairs return. Same route budget after a retained real failure; no load/collapse, wet composition, rendered stair, throughput or full-gate acceptance. |
| `codex/milestone-structural-load-planning` | `ca149cb` | Actual funded member/actor mass binding, explicitly rated DAG and immutable bounded cascading failure plans with trusted-graph canonical resume; 1,241 tests, 14 fresh Host inspections, independent integer/byte oracle and headless foundation. [Evidence](p02/structural-loads/README.md). Read-only diagnostic scope: no live collapse, debris, actor falling, world-profile publication, production strength/latency or full-gate closure. |
| `codex/milestone-construction-physical` | `159e431` | Fresh dry profile13 retains complete physical member lifecycle independently of fabrication, with conserved installed material and atomic completion/identity publication. 1,274 tests, 26 new-profile Host calls/two kills, 20 legacy calls/two kills, headless foundation/farm and seven identical recreated files. [Evidence](p02/construction-physical/README.md). Exact-component support and pre-work refusal are qualified; live collapse, debris, actor falling, universal load admission, production performance/art/devices and full gates remain open. |
| `codex/milestone-structural-policy-motion` | `ca724fb` | Complete canonical authored load policy, inactive-template topology validation and exact whole-body vertical first-contact queries. 1,321 managed tests, 47 new, six new/five prior fixture files recreated exactly, bounded native critique and 300 reference links. [Evidence](p02/collapse-motion/README.md). Read-only dependencies: no installed world policy, live collapse/impact/debris, pending continuation, new native/crash campaign, performance/art/devices or full-gate closure. |
| `codex/milestone-structural-policy-admission` | `c6b032c` | Fresh profile14 durable full policy, identity ownership and stable complete local action/movement gates; builder-carried stock/byproduct accounting conserves mass without double-counting installed backing. 1,334 tests, new17/two-kill and prior26/two-kill Host controls, headless foundation/farm and25 identical fixture files. [Evidence](p02/structural-policy-admission/README.md). Terminal refusals only: no pending/live collapse, impacts/debris, production performance/art/devices or full-gate closure. |
| `codex/milestone-structural-settlement` | `92dd90a` | Fresh profile15 actual atomic excavation with one intact assembly/rider descent, complete swept collision and retained backing/foundation identities. 1,350 tests, new17+legacy5 ordinary Host calls/two kills, prior17/two-kill control, headless foundation/farm and 14 exact old fixtures. [Evidence](p02/structural-settlement/README.md). Explicit harmless one-cell calibration; pending continuation, harmful impact/fracture, multiple-assembly closure, wet composition, production performance/art/devices and full gates remain open. |
| `codex/milestone-consequence-continuation` | `757bf18` | Fresh dry profile16/schema4 retained excavation command and real geometric cursor, reserved interval, exact retries/automatic resume and single terminal publication. 1,371 tests; 43 new ordinary Host calls/six kills, 22 older-profile calls/two kills, headless foundation/farm and 26 exact recreated fixture files. [Evidence](p02/consequence-continuation/README.md). The first 30-second historical-read timeout is retained with unconfirmed cause; exact recheck and full serial probe pass unchanged deadlines. Functional checkpoint only: production latency, broader collapse/damage, wet composition, art/devices and full gates remain open. |


The inventory-inspection branch records bounded code `3eed739` and retained evidence at
`ea56b64981f9f5409248dc9bb728af08439db386`. The development checkout continues on
`codex/runtime-foundation`; the earlier milestones were preserved and none was checked out.
No push or merge was performed.

The recorded-history branch preserves code and evidence at `9e0018d`. Qualification combines
successful integration checks with targeted native followups and final tests on the rebuilt
binaries; it is not a single uninterrupted full-verifier pass. The development checkout and all
earlier milestone branches remain in place. No push, merge or phase closure accompanies this marker.

The daylight branch preserves source and evidence at `80bdda07398a8657b1b8c7034c83242ef8dc641b`.
The final native probe extension was qualified separately after the integrated pass, with identical
production sources and tested non-client binaries. The development checkout remains unchanged;
earlier milestone branches and the unrelated staged predecessor work were preserved.

The quick-wheel branch preserves code and evidence at `c8989aed1178bfd13f68bcf38b64be82f540f2b6`.
The earlier wheel packet remains its original partial checkpoint; the shared-desktop packet
records subsequent completion. No checkout switch, force update, push or merge accompanied the
marker. The recurring editor-startup error remains unresolved and retained as a failure.

The watershed-persistence branch preserves code and qualification at `bf96ed9baa076f80f6b5266383612fa5a1f466ce`.
It records one explicit new-world macro admission, not a completed frontier or geological simulation.
The development checkout and previous milestone branches remain unchanged. No push or merge occurred.

The watershed-network-persistence branch preserves code, architecture updates and qualification at
`b2176e5ddb29bbbf355035b217d523941a4d4cc9`. It records durable ownership of finite prepared networks, not a completed frontier
or geological simulation. The development checkout, earlier milestones, frozen specifications and
unrelated staged predecessor work remain intact. No checkout switch, force update, push or merge occurred.

The watershed-terrain-mapping branch preserves code, reference updates and bounded qualification at
`41865edfa17a1d8476946261007abbe0ec53f334`. The development checkout and all earlier markers remain in
place. No checkout switch, force update, push or merge accompanied this marker; unrelated staged
predecessor changes and frozen specifications remain intact.

The terrain-material-allocation branch preserves code, reference and qualification at
`163a7bbc17bf07549ab2f6aa86050e7a1d9a8b9e`. It records conserved material representation over existing
custody, not playable mining or completed geological realization. The development checkout, prior
milestones, all 22 frozen spec hashes and exact unrelated predecessor index remain preserved.
No checkout switch, force update, push or merge accompanied this marker.

The material-registry branch preserves code, reference and qualification at
`b47fec733fe0c85e77ce0e2b792e9b90189729bb`. It records finite shared ownership and normalized material
archives, not durable integration or playable excavation. The development checkout, prior milestones,
all 22 frozen spec hashes and the exact unrelated predecessor index remain preserved. No checkout
switch, force update, push or merge accompanied this marker.


The material-production branch preserves code, reference updates and bounded qualification at
`a1783a984b4a120a799ac6cfa2b16570e20131d4`. It records standalone source retirement composition,
not playable mining or a complete geological simulation. The development checkout, prior milestones,
22 frozen spec hashes and exact unrelated predecessor index remain preserved. No checkout switch,
force update, push, merge or full-gate closure accompanies this marker.


The runoff-budgets branch preserves source, architecture reference and bounded qualification at
`02f28192dd380e1c04a3fac93b9eb327f1f0b878`. It records funded aggregate runoff and physical receiver
witnesses, not live flow or completed geological realization. The development checkout, prior markers,
22 frozen spec hashes and exact unrelated predecessor index remain preserved. No checkout switch,
force update, push, merge or full-gate closure accompanies this milestone.

The runoff-capacity branch preserves source, timeless reference updates and bounded evidence at
`83db4259717a3026f52f154d17a173069877591a`. It establishes capacity ownership inside the standalone
compositor, not globally admitted reservoirs or live water movement. The development checkout,
previous milestones, 22 frozen spec hashes and exact unrelated predecessor index remain preserved.
No branch switch, force move, push, merge or full-gate closure accompanies this marker.

The reservoir-geometry branch preserves source, timeless reference updates and bounded qualification
at `f00e637eb734573240f88f71cbe7e451636e24a2`. It records assumed watertight physical storage and exact
volume projections, not naturally realized lakes or hydraulic simulation. Development continues on
`codex/runtime-foundation`; previous milestones, frozen specs and the unrelated predecessor index
remain preserved. No checkout switch, force move, push, merge or full acceptance closure occurs.

The reservoir-persistence branch preserves source, timeless reference updates and bounded integration
qualification at `a3789c1892313beb11a93580809dc9846dd42fc4`. It records one complete durable physical
owner inside the existing world authority, not a hydraulic simulation or changed default farm.
Development remains on `codex/runtime-foundation`; prior milestone branches, frozen specs and the
exact unrelated predecessor index remain intact. No checkout switch, force move, push, merge or
full acceptance closure accompanies this marker.

The runoff-controls branch preserves source, timeless reference updates and bounded qualification at
`204d262dd83d663df2e150a323aa5a70e48ca13c`. It records durable control of existing funded receiving
commitments, not liquid movement or changed default gameplay. Development remains on
`codex/runtime-foundation`; prior markers, 22 frozen specifications and the exact unrelated predecessor
index remain intact. No checkout switch, force move, push, merge or full acceptance closure accompanies
this checkpoint.

The reservoir-transport branch preserves source, timeless references and bounded qualification at
`6ef6e83808c7711074a35190f99b5e2e27896b57`. It proves actual finite water transfer, including recovery
at its commit boundary, not a complete fluid simulation or changed default farm. Development remains
on `codex/runtime-foundation`; prior markers, 22 frozen specifications and the exact unrelated predecessor
index remain preserved. No checkout switch, force move, push, merge or full acceptance closure accompanies
this checkpoint.

The reservoir-boundaries branch preserves source, timeless references and bounded qualification at
`5dd8110fbb88a6993263f8aac9d1af90e9da66bb`. It qualifies an exact derived assessment over retained terrain
and actual completed construction; it does not convert saved reservoir profiles into coupled fluid/terrain
authorities. Development remains on `codex/runtime-foundation`; prior milestones, frozen specs and the
exact unrelated predecessor index remain intact. No checkout switch, force move, push, merge or full
game/phase acceptance accompanies this marker.

The live-excavation marker preserves source, timeless reference and retained qualification at
`7df18df50b6fd349473882594d98828b06d874bf`. Development remains on `codex/runtime-foundation`;
prior milestones, all frozen designs and unrelated staged predecessor changes remain intact.
No checkout switch, force update, push, merge or full-phase closure accompanied this checkpoint.

The constructed-support marker preserves source, timeless reference and qualification at
`2dcdd798bd569c503aac3b5ef387ebafe71711f4`. Development remains on `codex/runtime-foundation`;
all prior milestones, frozen specifications and the exact unrelated predecessor index remain intact.
No checkout switch, force update, push, merge or full-phase closure accompanies this checkpoint.

The stair-navigation branch preserves code, reference and bounded qualification at
`d4f01b4`. The development checkout, earlier markers, 22 frozen specifications and exact 19-file
predecessor index remain intact. No checkout switch, force update, push, merge or phase closure occurred.

The structural-load-planning marker preserves `ca149cb` and its exact funded-world graph/archive
qualification. The source world is unchanged. Actor load survival is explicitly distinct from spatial
footing; physical collapse still needs complete consequence publication, conserved debris and checked
falling/landing. Canonical restore binds an expected graph as well as a dependency pin. This checkpoint
closes no full phase and changes no predecessor history or frozen specification.

The construction-physical marker preserves `159e4316c6e2a5336e1eb68b6065615f228bf040` with source,
timeless references and bounded qualification. Completion-derived members share the original material
backing; supplied displaced poses do not claim an executed fall. Actual collapse and its complete world
publication remain the next dependency. Development stays on `codex/runtime-foundation`; all previous
markers, 22 frozen designs and the exact unrelated predecessor index are preserved. No switch, force move,
push, merge or full-phase closure accompanies this marker.

The structural-policy-motion marker preserves `ca724fbc3f76e5b392245a9bd67e073e7c66a0b0` with source,
timeless reference and bounded managed/funded-world qualification. Candidate contacts and load assessments
do not publish consequences. Live policy ownership and complete atomic collapse remain open. Development
stays on `codex/runtime-foundation`; previous markers, frozen specifications and the exact unrelated
predecessor index remain intact. No switch, force move, push, merge or phase closure accompanies this marker.

The structural-policy-admission marker preserves `c6b032cf5c63dd062a39e64612df0254c8b875df`, including
runtime policy authority, carried-project mass correction, timeless references and real recovery qualification.
A terminal overload refusal creates no consequence job; full coupled collapse remains open. Development
stays on `codex/runtime-foundation`; earlier markers, frozen specifications and the exact unrelated predecessor
index are preserved. No checkout switch, force move, push, merge or full-phase closure accompanies this marker.

The structural-settlement marker preserves `92dd90a342f7a1fb887540fab36ebfb53d49db89`, including actual
funded member/rider motion, conserved source/material ownership, current-pose load binding, timeless
reference and crash/retry/history qualification. The original failed compile/fixture/probe attempts remain
retained alongside corrected passing checks. This bounded atomic interpretation does not close durable
pending-work or broader collapse gates. Development remains on `codex/runtime-foundation`; previous
markers, all frozen designs and the exact unrelated staged predecessor work remain intact. No checkout
switch, force move, push, merge or full-phase closure accompanies this checkpoint.


The consequence-continuation marker preserves `757bf18d1fa7c7a8188a28c2806afb198a864a44` with the complete
command/basis/cursor owner, geometric query progression, exhaustive deferred receipt accounting, exact
publication reservations, journal completion capacity, source-aligned architecture and qualification.
Development remains on `codex/runtime-foundation`; prior markers, 22 frozen specs and the exact 19-file
predecessor index remain intact. Original failures remain retained. The functional checkpoint does not
close production latency or broader collapse gates. Ecosystem commit `e25ade3` preserves the portable
validation scheduling clarification. No checkout switch, force update, push or merge accompanied this marker.

The coupled-settlement marker preserves `463509d05e313578a479d13bffd423e1568e3432` with implementation `eee42ee`,
source-aligned references, independently checked fabrication/source/member/actor conservation and actual
commit-boundary crash recovery. The pre-change checkpoint is `codex/checkpoint-coupled-settlement-base`.
Development stays on `codex/runtime-foundation`; every previous marker, frozen specification and the exact
unrelated predecessor index remains intact. The original admission failure remains recorded. Ecosystem
`5aa4099` preserves the reusable diagnostic/authority boundary. No checkout switch, force update, push,
merge or full-phase/production acceptance accompanies this marker.

The event-descent-assessment marker preserves `25da97892d72629e7bdb425b4608aba658645e0f`, with implementation
`8279268` and pre-change checkpoint `codex/checkpoint-event-descent-base`. Its unequal trajectories are
qualified domain calculations; the retained live profile17 still publishes its original common descent.
Every original simulation source is unchanged. Development remains on `codex/runtime-foundation`, with
all earlier markers, frozen designs and the exact unrelated predecessor index preserved. No checkout
switch, force move, push, merge or full-phase/production acceptance accompanies this marker.

The playable-worksite marker preserves `7da8fcc50c31ec91fa474cde715fc243f39d8c6a`, including
complete managed qualification, the strict four-process rendered journey and the original farm's
native wheel control. Movement-root reuse is committed at `518e802`; initial content and the earlier
hash cache remain in its ancestry. All failed probes, bounds and open production obligations are
retained. Development continues on `codex/runtime-foundation`; previous markers, all 22 frozen
specification hashes and the exact unrelated predecessor index remain unchanged. No checkout switch,
force move, push, merge or full-phase closure accompanies this marker.

The reservoir-containment marker preserves `5441293b6458f2adab360b22049a61a099d58f86`, including
saved policy authority, real material reconciliation, independently reviewed construction refusal,
exact recovery, native controls and source-aligned architecture. The full regression passes 1,498 cases;
the final targeted run adds one new regression for 1,499 distinct cases. Development remains on
`codex/runtime-foundation`; prior markers, all 22 frozen specifications and the exact unrelated 19-file
predecessor index are preserved. No checkout switch, force move, push, merge or full-phase acceptance
accompanies this fixed-vessel checkpoint.


The `codex/milestone-world-composition-refinement` marker preserves the reviewed three-pass analysis,
independent Fable challenge, lead disposition, timeless-reference clarifications and retained failure
records. It qualifies the documentation/review scope only. The separately tested care/material WIP
is recoverable at `codex/checkpoint-care-composition-tested-wip-20260926T011925Z` (`6e14121`): clean build,
1,980 managed cases, no failures/skips, but no accepted live/durable/native care flow. The raw analysis
checkpoint is `codex/checkpoint-composition-review-20260926T010637Z` (`69ddf77`). Earlier references,
frozen specs and unrelated predecessor index remain preserved. The Max-effort history implementation
hit its finite limit; a smaller follow-up remains pending approval, with no automatic paid retry.


The `codex/milestone-material-consumption-composition` marker preserves the isolated 30-file
material-retirement/explicit-codec integration and reviewed Fable correction. Qualification includes
a clean build, 1,912 managed cases, 13 independently checked new archives, four oracle refusals and
37 byte-identical legacy fixtures. A separate 71-case care-WIP compatibility check is not live-care
acceptance. All 51 prior milestones, 23 tracked frozen specs and unrelated staged predecessor changes
remain intact. The before-integration WIP is preserved at
`codex/checkpoint-before-material-consumption-integration-20260926T013444Z` (`cc184f7`). No complete-world
consumption, durable treatment, native recovery or production-scale gate closes with this milestone.


The `codex/milestone-consented-world-boundaries-20260929` progress marker preserves produced and
raw bilateral World transactions, explicit profile 28 / Production V11 continuation, custody
partition validation, scoped independent critique, structural corrections and current architecture.
Clean build and 1,883 distinct managed cases pass: Core 1,664, managed Hosting 196 and 24 complete
world cases with one overlap. Actual 4,096-lot, construction, recraft/care/reload, two-pair, lifecycle
and rollback proofs retain their bounds. Pre-change recoveries are
`codex/checkpoint-before-consent-boundary-proof-20260929T080633Z` (`35f8a41`) and
`codex/checkpoint-consent-proofs-before-hardening-20260929T082152Z` (`048bdf1`).
Original failing checks, partial delegation results and review exclusions remain in the packet.
Formats remain unfrozen; byte-vector/remaining codec, durable Host/native/retained-save and full
assignment/phase/production gates stay open. The marker is a backup pointer; work stays on
`codex/runtime-foundation`. All 23 frozen specs, 51 old fixtures, prior refs and the unrelated
19-file predecessor index remain intact. No branch switch, forced update, push or merge occurs.

The `codex/milestone-consented-formats-20260929` marker preserves independently generated consent
vectors, additive codec/archive tests, exact authority-replay refusals, the 64/65 movement bound and
a corrected V10 API comment. A clean build and 117 distinct scoped cases pass (88 Core, 29 world);
the earlier full regression is retained evidence, not rerun or relabelled as current full coverage.
Runtime behavior and all limits are unchanged. The exact coverage, opaque-import limitations,
retained failures and remaining format/Host/native/retained-save gates are recorded in
[format integration](p02/consented-format-qualification/INTEGRATION_REVIEW.md).
Pre-change refs are `codex/checkpoint-before-consented-format-qualification-20260929T084626Z`
(`1b922ba`) and `codex/checkpoint-formats-before-integration-20260929T091403Z` (`5e808c1`).
All 23 frozen specs, 52 existing fixtures and unrelated 19-file predecessor staging are preserved.
This is a progress backup pointer on `codex/runtime-foundation`, not a format freeze or phase exit.

The `codex/milestone-durable-consented-world-20260929` progress marker preserves the explicit
profile-28 SQLite binding in schema eight, complete atomic paired publication, permanent identities,
two actual coordinated cuts/reopening and updated timeless references. Qualification passes the
integration build, 200 broad affected Host cases, 17 durable and 38 legacy cases, with 244 distinct
passes after overlaps and verified equal runtime source/library bytes across the two builds.
Two actual SIGKILLs at commit boundaries recover exact before/after ledgers; duplicate commands
publish no second transfer. The actual net10 Host and 15 archived fixture inputs are separately pinned.
[Integration review](p02/durable-consented-world/INTEGRATION_REVIEW.md) records failure dispositions,
review scope, source bindings, delegation cost limits and remaining gates. The original worker fixture
failure, overlapping checks and lead preflight-path failure remain preserved; no threshold was relaxed.
Pre-change refs are `codex/checkpoint-before-durable-consented-world-20260929T092257Z` (`d88e039`) and
`codex/checkpoint-durable-consent-before-integration-20260929T100036Z` (`55b6431`). All 23 frozen specs,
53 existing fixtures and the exact 19-file staged predecessor index remain intact. Public paired
ingress, asynchronous offers, native interaction, retained-save transition, remaining format proofs,
production scale and full phase/campaign gates remain open. Work stays on `codex/runtime-foundation`;
this is a backup pointer, with no branch switch, force update, push or merge.


The `codex/milestone-saved-material-proposals-20260929` marker preserves fresh profile30/action16
saved offers in the schema-eight sole authority: independent local submissions, immutable receipts,
actual expiry/readiness/opportunity/physical joins, exact open-grant carry and participant-visible
archived outcomes. The integration build and 1,941 distinct managed cases pass: Core1,684 plus
Host200, proposal Host30 and legacy36, with nine overlaps and exact runtime compatibility for the
retained legacy run. Four actual SIGKILLs cover before/after creation and acceptance; complete ledgers,
receipts, identities, retries and history recover exactly. Those process fixtures do not cross a cut;
the automatic archive/carry proof is separately managed. [Integration review](p02/material-proposals/INTEGRATION_REVIEW.md)
and [final checks](p02/material-proposals/qualification/FINAL_CHECKS.json) retain source/build pins,
independent criticism, repaired defects, failures, delegation limits and remaining obligations.
Pre-change `codex/checkpoint-before-material-proposals-20260929T102335Z` preserves `76209f2`;
worker/repair WIP snapshots `eeaacf9`, `b4e9cc2` and `05a0adf` remain recoverable under their unique
checkpoint refs recorded in the packet. All 477 old tests/fixtures, 23 frozen specs and the unrelated
19-file predecessor index remain intact. Profile30 is a sibling, not a migrated or combined world.
Actual session scheduling/control transition, native/NPC/network binding, production scale and the
retained520-dose journey stay open, as do full phase/campaign gates. Work remains on
`codex/runtime-foundation`; the milestone is a backup pointer, with no checkout switch, force update,
push or merge.


The `codex/milestone-native-material-gifts-20260930` marker preserves the explicit native gift
profile, physical stack/recipient/quantity preparation, four-action Gifts wheel, background participant
history and captured input terms. Build and 259 distinct affected managed cases pass (19 focused
included), plus six rendered gift processes and two unchanged care-controller processes with four
recorded-state inspections. All four gift actions have authenticated native journal effects and
separate-process paused reopening. Independent source critique and an owner-requested verified
Opus5.5/1M/requested-Max repair improved reselection and controller release ownership; Astra
reviewed, tightened the focus guard, fixed clipping and ran the checks. [Integration review](p02/native-material-gifts/INTEGRATION_REVIEW.md)
retains actual scope, failures and remaining gates. Pre-change HEAD 710c210 is preserved by
`codex/checkpoint-before-native-gifts-20260929T134834Z`; WIP 3c63e7f is preserved by
`codex/checkpoint-native-gifts-before-opus-20260930T000043Z`. All 490 old pinned tests/fixtures,
the exact earlier test project apart from two additive links, all 23 frozen specs and unrelated 19-file
predecessor staging remain intact. Work stays on `codex/runtime-foundation`; this backup pointer
closes no full game, phase, migration, device, art or human acceptance gate.

The retained-proposal-transition marker preserves one explicit exact-head rules transition of the
original 520-use ledger, its managed gift/accept/use521 continuation and bounded authenticated history.
The pre-change b3e2294 and WIP delivery 9bf0e83 remain under their unique checkpoint refs in the packet.
Opus5.5/1M/requested-Max delivered the implementation; Astra strengthened tests, independently reviewed
all changed authority/continuation paths and completed qualification. Build and 2,051 distinct cases
pass, including all new 22 and full Core; all 496 old tests/fixtures, 23 frozen specs and exact 19-file
predecessor staging remain unchanged. The same-selection Host repeat passes in 503.644s under 600s;
the interrupted first attempt and its corrected diagnostic interpretation stay retained. The original
source and every old authoritative row stay exact. Ecosystem commit 59813730 records the portable delegated
command-completion/artifact-scope lesson. Work remains on `codex/runtime-foundation`; no full phase,
native/device, new process-kill, production or human acceptance gate closes.


The `codex/milestone-retained-native-gifts-20260930` marker preserves two completed verified
Opus5.5/1M/requested-Max deliveries and Astra’s integration: explicit original-genesis selection,
native carried proposal acceptance and own-use521, plus object-owned structural resource bytes.
All1,985 distinct managed cases pass with no skips; native active/reopen and fresh keyboard/reopen
pass on exact final runtime binaries. The original1,579 commits and every non-head historical row
remain exact. Three native startup failures and three new-result-test failures remain recorded;
only the new test’s archive-context/connection lifetime handling was repaired, with no rule or
threshold weakening. The source,501 old tests/fixtures,23 frozen specs and unrelated19-file
predecessor staging remain intact. Pre-change and delivery checkpoints resolve via both packets;
`codex/checkpoint-before-retained-journal-test-repair-20260930T031248Z` preserves WIP`d01b218`.
The11.2% cumulative-allocation improvement is not a production latency/peak-memory claim.
Ecosystem`ad0c2e2` preserves shared verification serialization and measured-source pinning.
Work stays on`codex/runtime-foundation`; this backup pointer closes no full phase, campaign,
physical-device, art or human acceptance gate and entails no branch switch, push or merge.


The `codex/milestone-observation-validation-20261002` marker preserves opt-in host timing and
owned occupancy validation without per-cell boxing. The fixed validation workload allocates zero
bytes versus 3,145,984 before, preserving all count/depth/error-order and wire/diagnostic contracts.
Warning-free build and 584 distinct cases have passing evidence, including 22 new; the normal
Server aggregate still has one unchanged five-second four-player movement failure and a Windows
skip. Its traced repeat passes, without explaining the failure. This is bounded optimization
qualification, not all-green aggregate, production latency or phase acceptance. All 688 old tests/
fixtures, 23 frozen specs and unrelated staging remain exact. The pre-change ref preserves d9e6ec57;
the WIP checkpoint before optimization preserves 68eac65e. Exact refs, scope, failures, independent
critique and source/binary pins are in [the result](p02/multiplayer-timing/RESULT.md). Work stays on
`codex/runtime-foundation`; no branch switch, push or merge. The pending-care disposition proposal
is preserved separately and claims no implemented rule or reserved profile version.

## Terrain-separated locality storage preparation — 2026-10-18

- Before-change checkpoint `codex/checkpoint-before-separated-terrain-storage-20261018` resolves to
  `4fc4d2e1287de7840021589b6239123fd03c548d`, preserving relevant WIP from source
  `e1c26a48f886db50bae40de28e2f51aab3d66532`; concurrent/predecessor scope is recorded in
  [CHECKPOINT.json](p02/partition-state/separated-terrain-storage/CHECKPOINT.json).
- Verified increment pointer: `codex/milestone-separated-terrain-storage-20261018`, created at the
  owned commit without switching the working checkout. Includes the explicit internal schema13
  profile-five storage candidate, shared publisher, full replay/refusal tests, architecture/plan
  updates and retained evidence. Default creation/current gameplay profiles remain unchanged.
- Evidence:400 distinct current-source cases pass (303 Core,97 Hosting), all within180-second bounds.
  One production schema failure and one fixture pooling failure are retained; a runner path bug
  overwrote earlier successful preflight records and is disclosed, not retroactively certified.
  Full-Core prior timeouts, noninitial checkpoint qualification, richer-profile migration, typed
  owner comparison, scheduling and independent review remain open. No PLAN-STATE/P02 closure.
- [Result and documentation review](p02/partition-state/separated-terrain-storage/RESULT.md) distinguishes
  exact byte/residency evidence from throughput, platform, production or credit-saving claims.

## Matched partition comparison and lead recommendation — 2026-10-18

- Before-change WIP checkpoint `codex/checkpoint-before-partition-decision-20261018` resolves to
  `0bb1cc76e404b1dc0d4f7707d9444ffa288d5fa2`, from source5b21fbcb; concurrent scope is retained
  in the packet CHECKPOINT.json. No predecessor or concurrent-doc changes enter the owned commit.
- Verified increment pointer: `codex/milestone-partition-decision-20261018`, created at the owned
  commit without switching checkout. Includes two matched actual storage/publication comparisons,
  a lead typed-owner/admission-cohort recommendation and synchronized timeless/living documentation.
-19 focused cases pass in14.442 seconds; no test or production source gate was weakened. Prior400
  passes retain their original pins; rebuilt metadata is disclosed. No independent, human, scale,
  complete-Core or PLAN-STATE/P02 acceptance follows. Migration, scheduling, checkpoint and review
  gaps remain explicit in [the result](p02/partition-state/partition-decision/RESULT.md).

## Typed owner directory publication — 2026-10-18

- Before-change WIP checkpoint `codex/checkpoint-before-typed-owner-storage-20261018` resolves to
  `5ab02064c9e74ec61c7b0215e8c48aa48552d01a`, from3311564c. Concurrent/predecessor protections
  are retained in the packet CHECKPOINT.json.
- Verified increment pointer: `codex/milestone-typed-owner-storage-20261018`, created at the owned
  commit without switching checkout. Includes explicit internal legacy schema14, immutable scoped
  owner directory/payloads, shared atomic publisher and complete-history verification, with updated
  timeless/living documentation and compact handoff. Default/current game formats stay unchanged.
- 520 distinct affected cases pass (406 Core,114 Hosting),11 new; no failed test/build run or extended
  deadline. The earlier30-case pass overlaps and is not added again. Full-Core, independent/human,
  checkpoint/scale/fairness/migration and PLAN-STATE/P02 acceptance remain open.
- [Result](p02/partition-state/typed-owner-storage/RESULT.md), measured payload/archive facts and
  retained handoff source distinguish bounded publication from independent stored-owner loading.

## Complete stored-owner reconstruction — 2026-10-18

- Before-change WIP checkpoint `codex/checkpoint-before-owner-reconstruction-20261018` resolves to
  `0baffd22aa9d6caac24b145e9335f5797235a1ad`, from0cd76d51. Protected concurrent/predecessor scope
  is recorded in the packet CHECKPOINT.json.
- Verified increment pointer: `codex/milestone-owner-reconstruction-20261018`, created at the owned
  commit without switching. Complete schema14 owner/terrain decoding uses existing admission and
  exact full-replay comparison; returned recovered owners can continue the same partial project.
- [Result](p02/partition-state/stored-owner-reconstruction/RESULT.md) retains terminal checks and two
  compiler-preflight failures with their corrections. No changed deadline, acceptance shortcut or
  independent review. Selective loading, migration, fairness, checkpoint/scale and full gates remain.
- Timeless architecture, living plans/roadmap and compact handoff are updated in the same increment.

## Current-profile ownership and dependency preparation — 2026-10-18

- Before-change WIP checkpoint `codex/checkpoint-before-profile-dependencies-20261018` resolves to
  `f6236b4913ccebc1f36570d98c5a48dd9ad73680`, from6606db30. Concurrent/predecessor protections are in the packet CHECKPOINT.json.
- Verified document increment pointer: `codex/milestone-profile-dependencies-20261018`, created at
  the owned commit without switching. Includes current40/41 complete outer-frame mapping, nested
  ownership/witness dependencies and concrete selective-construction read/write/due closure.
- [Result](p02/partition-state/current-profile-dependencies/RESULT.md) distinguishes source review and
  document checks from runtime migration/loading or independent acceptance. Runtime bytes/tests are
  unchanged; no new test, throughput or feasibility pass is claimed. All PLAN-STATE/P02 gates remain.
- The timeless dependency reference and live plan/roadmap/handoff are synchronized together.

## Shared construction qualification — 2026-10-18

- Before-change WIP checkpoint `codex/checkpoint-before-work-qualification-20261018` resolves to
  `2029c3ebcd6193cccbc757532f611319c9f6d1cf`, fromf430d696. Concurrent/predecessor scope remains in CHECKPOINT.json.
- Verified increment pointer: `codex/milestone-work-qualification-20261018`, at the owned commit
  without switching. Existing work guards/progress are shared separately from final material mutation;
  ordinary, detached and locality paths retain the same rule body and completion algorithm.
- [Result](p02/partition-state/work-qualification/RESULT.md) records exact source extraction, terminal
  behavior checks and retained compiler/runner failures. Complete owner inputs remain; no selective
  loading, migration, independent review or PLAN-STATE/P02 acceptance follows.
- Timeless construction/dependency references and living decision/map/plan/roadmap/handoff are current.

## Selected stored construction assessment — 2026-10-18

- Before-change WIP checkpoint `codex/checkpoint-before-stored-work-cohort-20261018` resolves to
  `1743d69f2aba06cc3f34b48d7cd2e4292419d0e5`, from8fad3fc4; concurrent/predecessor protection is in CHECKPOINT.json.
- Verified increment pointer: `codex/milestone-stored-work-cohort-20261018`, created at the owned commit
  without switching. Includes source-bound selected payload qualification, shared owner-read boundary,
  exact-head serialized Host assessment and retained-detail/read measurements.
- [Result](p02/partition-state/stored-work-cohort/RESULT.md) records terminal tests and bounded claims.
  Assessment does not publish a step or run due processes, and the site retains complete actor/structure
  geometry. Partial publication, witness renewal, migration/fairness/scale and full gates remain open.
- Timeless ownership/dependency/construction references and living plans/roadmap/handoff stay current.

## Selected stored partial-work publication — 2026-10-18

Before-change WIP checkpoint: `codex/checkpoint-before-stored-work-publication-20261018`
(`be81c44893031448bfbb9b7752fd8bd24c6d2fdf`, parent768b8bfd). The verified increment is preserved by
`codex/milestone-stored-work-publication-20261018` without switching the checkout; see
[packet evidence](p02/partition-state/stored-work-publication/RESULT.md) for terminal validation.
Scope: selected stored work and all due exposure/gathering, exact directory renewal, sole-writer atomic
publication, full final/intervening admission and original history. Complete geometry/metadata and
full replay remain; no current40/41 migration, scheduling/scale or phase acceptance. Unrelated staged
predecessor work and concurrent steering remain excluded.

## Stored-work noninitial checkpoint qualification — 2026-10-18

Before-change WIP checkpoint: `codex/checkpoint-before-stored-checkpoint-20261018`
(`2dc524f82a682bc6d8403ef9608d93774c7f6b62`, parent9100315f).
Verified milestone: `codex/milestone-stored-checkpoint-20261018`, created at the owned commit without
switching. [Eight-case evidence](p02/partition-state/stored-checkpoint/RESULT.md) qualifies the actual
6000-step checkpoint during partial construction, original-history continuation, atomic faults and
historical tamper refusal. Runtime source/policy unchanged; test fixture, exact-case preflight support
and documentation only. Retained SQLite artifacts/restore instructions are linked from the result.
General scheduling/current-profile/performance and phase gates remain open; concurrent steering and
staged predecessor files remain excluded.

## Due-work discovery preparation review — 2026-10-18

Before-change WIP checkpoint: `codex/checkpoint-before-due-work-discovery-20261018`
(`8cbc814667f25956b1f77fac0c794c504518321b`, parent `9e567ab777b221f2d4898858064a172458feefa0`).
Verified documentation checkpoint: `codex/milestone-due-work-discovery-20261018`, created at the owned
commit without switching. [Result](p02/partition-state/due-work-discovery/RESULT.md) pins the actual
selection, boundary and standalone-queue sources, records documentation validation and retains all
runtime/fairness/phase obligations. No runtime edits or test execution are claimed. Concurrent
steering and unrelated staged predecessor work remain excluded.

## Multiple-NPC ordinary cutoff proof — 2026-10-18

Before-change WIP checkpoint: `codex/checkpoint-before-multiple-npc-cutoff-20261018`
(`30dc1f6dbd81651cdeec9022c820b7ebc7876885`, parent `010175aca7d611b53d8c926fd697d91170bf72ab`).
Verified milestone: `codex/milestone-multiple-npc-cutoff-20261018`, created at the accepted owned commit
without switching. [Result](p02/partition-state/multiple-npc-cutoff/RESULT.md): nine new plus thirteen
existing cases pass; two real funded host builders, one ordinary cutoff, holds/exclusions/refusals
and three retained SQLite publication/recovery cases. Two fixture compilation failures are retained.
Runtime source and legacy laws unchanged; partial-residency scheduling, full fairness and phase gates
remain open. Concurrent steering and staged predecessor changes remain excluded.

## Stored complete-batch preparation — 2026-10-18

Before-change WIP checkpoint: `codex/checkpoint-before-stored-batch-preparation-20261018`
(`22962129f6f865cca3c3568e54559bc9ef9e2932`, parent `873766d33c443af453c168745d6132e56ac6ce8c`).
Verified milestone: `codex/milestone-stored-batch-preparation-20261018`, created at the owned commit
without switching. [Evidence](p02/partition-state/stored-batch-preparation/RESULT.md) records611distinct
passes including13new, whole multi-actor stored publication, resumable exact-head owner reads and
fresh6000-step checkpoint recovery. The failed six-case production run, exact changed failure sources
and databases remain retained. Pure C#, same schema/profile laws, full history and all broader gates
remain; synchronous preparation is not a general scheduling or fairness exit. Protected concurrent
steering and all19staged predecessor files remain excluded.


## Combined stored due-boundary qualification — 2026-10-18

Before-change WIP checkpoint: `codex/checkpoint-before-combined-due-boundary-20261018`
(`1cab143436543da610f778b1175130d867a14c92`, parent `f54f16b66c430a51c3fb5621ecd2188afb77bcd1`).
Verified milestone: `codex/milestone-combined-due-boundary-20261018`, created at the owned commit
without switching. [Result](p02/partition-state/combined-due-boundary/RESULT.md):30 affected cases pass,
six new, with real regrowth/two-builder work/all-resident exposure, sliced preparation and retained
SQLite atomic fault/history proof. Test-only increment; the cheap population-preflight failure is
retained, all production laws and broader gates unchanged. Protected concurrent steering and all19
staged predecessor files remain excluded. Timeless architecture accuracy reviewed; live plans updated.


## Matched owner-cost qualification — 2026-10-18

Before-change WIP checkpoint: `codex/checkpoint-before-owner-cost-comparison-20261018`
(`d8579d1c5f2e14764c4b28249bf8480180495d0f`, parent `6d3b03ab827e25658aad427aa45a4026366c8ff0`).
Verified milestone: `codex/milestone-owner-cost-comparison-20261018`, at the owned commit without
switching. [Result](p02/partition-state/owner-cost-comparison/RESULT.md):8 affected cases pass,4 new,
with three rotating matched storage orders, actual full-history equivalence, retained measurements
and a candid coarse-versus-typed cost disposition. The112.302-second fixture diagnostic failure and
original databases/source are retained. No production law/default/gate changes. Timeless architecture
reviewed, living plans updated; protected concurrent/predecessor work remains excluded.


## Yielded authority publication — 2026-10-18

Before-change WIP checkpoint: `codex/checkpoint-before-yielded-publication-20261018`
(`4204c9dad1ee813ea6d38ed7e48625c55c50b26c`, parent `1c552c6755e378d466b92fdce63c59c5ddedf49a`).
Verified milestone: `codex/milestone-yielded-publication-20261018`, at the owned commit without switching.
[Result](p02/partition-state/yielded-publication/RESULT.md):35 affected passes,7 new; bounded gate-releasing
reads, source/authority affinity, cancellation/invalidation and full atomic publication/recovery.
The transaction body is unchanged; ordinary live-loop integration and all wider gates stay open.
Timeless architecture and living plans updated together. Concurrent steering and staged predecessor
work remain excluded; prior failures retained, no new failed run or extended deadline.


## Live stored-construction preparation — 2026-10-18

Before-change checkpoint: `codex/checkpoint-before-live-construction-slices-20261018`
(`1bed3a84546e40dad86f2eb0bb275c9c6a9e9455`, parent `6d6fd5b56189ae2c036d5eaaa79608ce869c402f`).
Verified milestone: `codex/milestone-live-construction-slices-20261018`, created at the owned commit
without switching. [Result](p02/partition-state/live-construction-slices/RESULT.md) records terminal
checks, retained failures and explicit opt-in legacy scope. The existing session loop now owns
sliced preparation, readiness cancellation, queued input and coherent publication observation.
Default/current profiles, all broader gates and the original P02 closure table remain unchanged.
Timeless and living docs are updated together; concurrent steering and staged predecessor work stay excluded.


## Process discovery contract — 2026-10-18

Before-change WIP checkpoint: `codex/checkpoint-before-process-discovery-contract-20261018`
(`339b32b99284c51d89f2d07b2265ee847834f8cd`, parent `29e89f6b68ec5644716996fcf8c383cbb5de1fdf`).
Documentation checkpoint: `codex/milestone-process-discovery-contract-20261018`, created at the scoped
commit without switching. [Result](p02/partition-state/process-discovery-contract/RESULT.md) pins reviewed
sources, post-action/clock/blocking contracts and the next executable contrast. Documentation checks
only; no runtime, independent, phase or performance acceptance. Concurrent/predecessor edits excluded.


## Typed domain-process discovery — 2026-10-18

Before-change WIP checkpoint: `codex/checkpoint-before-domain-process-discovery-20261018`
(`9abc2451ae6ab05242d380bdd9fe30f814d09ecc`, parent `74ae01c47610840377be7bbb9b694f30cea381d9`).
Verified increment: `codex/milestone-domain-process-discovery-20261018`, at the scoped accepted commit
without switching. [Result](p02/partition-state/domain-process-discovery/RESULT.md) retains exact checks,
fixture build failures, source review and remaining limits. Domain queries feed existing operations;
no persistent index/current migration or phase acceptance follows. Concurrent/predecessor edits excluded.


## Joined saved process boundary — 2026-10-18

Before-change WIP checkpoint: `codex/checkpoint-before-joined-process-boundary-20261018`
(`38919feb70bde7c6c936fc8805b763912e0518a8`, parent `d6d66fabe67503850dd869edc2d2a01b381c046c`).
Verified milestone: `codex/milestone-joined-process-boundary-20261018`, created at the scoped commit
without switching. [Result](p02/partition-state/joined-process-boundary/RESULT.md) retains preflight,
original saved-lineage/fault evidence and explicit full-owner/profile9 limits. Production unchanged;
no independent, partial-residency, phase or human acceptance. Concurrent/predecessor work excluded.
