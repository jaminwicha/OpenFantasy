# Current shared agent handoff

Updated: 2026-10-01. This is the current-state index; evidence and failure dispositions remain in
linked packets. The preserved specification is authoritative. Read only task-relevant contracts.

## Start here

1. [Implementation instructions](../AGENTS.md): ownership, stable `OpenFantasy` runtime names,
   modular object-oriented C#, preservation, verification and shared-desktop discipline.
2. [Runtime setup and controls](../Runtime/README.md) for builds/runs.
3. [Full plan](../spec-v1_1/IMPLEMENTATION_PLAN.md), [requirements](../spec-v1_1/REQUIREMENTS.json),
   [technical design](../spec-v1_1/TECHNICAL_DESIGN.md) and
   [baseline handoff](../spec-v1_1/IMPLEMENTATION_HANDOFF.md) for the next dependency.
4. [Living UI supplement](PLAYER_INTERFACE_AND_NAVIGATION.md) and
   [partial coverage](ui-management/COVERAGE.json) for interface work. These refine the baseline,
   without reprioritizing phases. Later owner naming instructions override source naming only.

5. [Architecture reference](../Runtime/docs/architecture/README.md): timeless ownership, source
   bindings and domain/design contracts. Read affected subjects, not the entire encyclopedia, for
   an implementation task. The [maintenance hook](../AGENTS.md#architecture-reference-and-maintenance-hook)
   requires same-change updates for architectural changes and a bounded source review at phase handoff.

## Core language assessment

The owner-requested [C#/C++ decision specification](CORE_LANGUAGE_CPP_ASSESSMENT.md) and
[source inventory](CORE_LANGUAGE_CPP_EVIDENCE.json) assess execution, in-process native versus IPC,
performance hypotheses, compatibility, effort, projected source size and conditional qualification.
Owner decision, 2026-09-20: retain C# for the core and application for simplicity. Native alternatives
remain research; no migration, custom native kernel or IPC worker is in the active plan. Preserve core
decoupling and address measured costs in C#. No comparative benchmark or new language gate is accepted;
the complete existing LANG gates remain in force. A language change requires a new owner decision.

## Architecture documentation

The [backfill record](architecture-reference/README.md) reconciles the reference with the retained
runtime. It separates executable domain contracts, actual application bindings and full design
obligations, especially history, Wait, local visibility and macro geography. Progress/evidence remain
here and in packets; frozen specs remain preserved. Documentation coverage does not close game gates.
Commit `2f91983`, preserved on `codex/milestone-architecture-reference`, records the reference and
project hook. Ecosystem commit `4e83086` records the portable hook and verified five-entrypoint sync.
All 123 reference links resolve; the bounded factual review corrections are retained in the packet.

## Current work and routing

The [native trading foundation](p02/native-trading/INTEGRATION_REVIEW.md) is accepted at
`codex/milestone-native-trading-foundation-20261001`, with its full native UI gate still open.
Production `--markets` entry, source-backed stock/earned money, explicit inquiry/show/review/confirm,
pause-to-read, native buy/sell and saved outcome/reopen use the existing authority.
[Final foundation evidence](p02/native-trading/FOUNDATION_CHECKS.json) pins 1,601 source files and 14
binaries, with **454 distinct managed passes** and three-mode native trading/gift runs authenticated
against real journals. All old source/test/save/index pins remain exact; Core was not rerun.

Opus 5.5 / 1M with requested Max delivered the implementation in 3,335.008s / $25.0703686 client estimate.
Its context checks passed and no permission denial occurred. Astra reviewed the source/UI/journals and
fixed the lead wrapper's one-mode versus three-mode verification mismatch without changing assertions.
Two worker read-only shell listings exceeded the exact command examples. Raw sessions remain private;
[foundation dispatch](p02/native-trading/DISPATCH.json) is terminal.
The [WIP checkpoint](p02/native-trading/DELIVERY_CHECKPOINT.json) preserves the delivery before integration.

The [independent native review/refinement](p02/native-trading-refinement/INTEGRATION_REVIEW.md) is accepted
at `codex/milestone-native-trading-refinement-20261001`. It fixes controller B dropdown cancellation,
proves real focus navigation and per-route dropdown selection, adds authenticated native expiry,
unfunded and moved-issuer refusals/recoveries, guards commodity identity and improves player copy.
[Final checks](p02/native-trading-refinement/FINAL_CHECKS.json) match 1,608 current source files and 14
binaries: build, **459 distinct managed cases**, three-mode native trading/gift and three keyboard
boundary journeys pass. Core, Host, 546 old tests, frozen specs, original save and predecessor index
remain exact. Injected background input does not qualify physical devices or human comprehension.

The [dispatch](p02/native-trading-refinement/DISPATCH.json) is terminal: verified Opus 5.5 / 1M,
requested Max, synchronized context, 2,511.901s / $14.2986962 client estimate within the assigned
3,600s/$25 limits. No permission denial, substitution or model retry occurred. No external job is
active. The packet retains failures, remaining UI limits and before/dispatch/delivery checkpoints.
[Upstream guidance](p02/native-trading-refinement/UPSTREAM.json) records ecosystem commit `a557436`
and verified three-entrypoint synchronization for aggregate-verifier input contracts.

Next: join actual source merchants/stock/funds, bounded legal travel and reusable region presentation
toward the two-town cargo circuit. [Cargo dependency notes](p02/spot-exchange/CARGO_DEPENDENCIES.md)
ground that work without claiming an executed route. Multi-stall quote identity/selection is also
required. Keep physical-device, human art/comprehension, multiplayer, lived migration, cargo and
production gates open; the native refinement is not full UI or P02 completion.

The [scheduled-trading integration](p02/scheduled-trading/INTEGRATION_REVIEW.md) is accepted at
`codex/milestone-scheduled-trading-20261001`. It binds participant-safe quote/history views,
authenticated transient inquiries, own-control merchant answers and opt-in NPC buying/selling to actual
session cutoffs. Exact player confirmation, bounded NPC reconsideration, work/movement and gift arbitration,
ordinary market-world excavation, archive/reopen and changed-root presentation use the existing authority.
Core, profile, codec and schema remain unchanged. There is no native trading interface in this milestone.

[Final checks](p02/scheduled-trading/FINAL_CHECKS.json) pin one final build, 1,466 source files and 11
binaries: **291 distinct managed cases pass**, including 17 scheduled cases. All 707 Core files, 532 prior
test files, 23 frozen specs, original source-save bytes and the unrelated 19-file staged predecessor patch
remain exact. Core's prior evidence is retained; Core was not rerun for this Host-only change. The packet
preserves initial failures, independent findings, lead counterexamples and honest remaining proof gaps.
No gate was weakened. All native, scale, multiplayer, production and P00–P14 exits remain open.

Both completed requests returned verified Opus 5.5 / 1M with requested Max and synchronized context. The
initial implementation took 1,965.071s / $13.1508518 client estimate; the independent input critique plus
repairs took 2,549.543s / $13.931321. Astra reviewed that repair and added four meaningful cases, fixing the
attempt-wide showing bound, external-hold projection and excavation binding. These estimates are not
billing or evidence of net credit savings. The first worker reports two read-only shell listings outside
its task examples; no out-of-scope mutation or permission denial is evidenced. Those two requests are terminal;
no automatic retry or model substitution occurred. The native assignment has its own dispatch status.

[Recoverable checkpoints](p02/scheduled-trading/LEAD_REPAIR_CHECKPOINT.json) preserve dirty/new delivery
and lead regressions before changes: `d375253` and `bf946bf` are explicitly WIP snapshots; the milestone
contains the later accepted source. [Upstream](p02/scheduled-trading/UPSTREAM.json) records ecosystem commit
`e876ab4`, bounded-goal/composed-action/view-refresh guidance, guarded installed-copy backups and all 71
verified alignment entries. Codex and Claude skill copies match. Continue bounded substantive work through
exact `claude-opus-5-5[1m]` with requested Max, finite limits, synchronized context and Astra integration.

The native foundation above implements the initial [market entry and interaction dependency](p02/scheduled-trading/NATIVE_FOLLOWUP.md)
with the recorded native follow-through still required. Keep the [full cargo integration path](p02/spot-exchange/INTEGRATION_PLAN.md),
legacy migration and complete campaign intact. Market-world injured self-use/aid, some scheduler exclusions
and other residual proofs remain explicitly recorded; binding types or a passing revocation test do not
prove all of those behaviors.

The preceding [local-market binding](p02/local-markets/INTEGRATION_REVIEW.md), preserved at
`codex/milestone-local-markets-20261001`, provides actual-stock integer prices, private dated knowledge,
explicit lot disclosure and durable history. Its 1,915-pass evidence includes the unchanged 1,753 Core cases.
That packet retains its independent price/privacy critique, failures, source preservation and delegation
friction; the scheduled packet supplies the later application binding. Full demand, native trading,
two-town cargo, lived migration and production remain separate obligations.

The owner-requested [paid-world binding](p02/paid-world/INTEGRATION_REVIEW.md) implements explicit
fresh profile32, atomic goods/payment, exact actor/currency causality and durable quote lifecycle.
The independent Opus critic's shared-capacity and canonicality defects are repaired; issuer withdrawal
and same-fill supersession have full-book, archive-cut, reopen and retry evidence. Earlier failed
runs, reviewer dispositions and exact source preservation remain in that packet.

The [latest Opus5.5 fixture increment](p02/paid-world/STOCK_GENESIS_INTEGRATION.md) closes the remaining
eligible-buyer and stock-constraint proofs. Seven new cases admit a third capable human with an empty
wallet, fund it through real earned payment/fees, and prove both buyers independently eligible before
competition. Canonical order decides one fill in all four order combinations. Real raw-plus-crafted
capacity, protected stock and reserved stock have lawful positive contrasts and exact reopening.
The [late-liquid proof](p02/paid-world/LIQUID_ROLLBACK_PROOF.md), committed as 9c192bd, separately proves
all-owner rollback after local payment and reservoir capacity succeed but placed-water validation fails.
The [original boundary gaps](p02/paid-world/INTEGRATION_CHECKLIST.md#required-boundary-proof-dispositions)
are now closed; no test or acceptance threshold was weakened.

Build and all 32 paid Host cases pass, including an independent lead rerun on the same final source
and binaries. Runtime/src, client, all prior tests, 23 frozen specs, the immutable source database and
unrelated predecessor staging remain exact. Previous composite 1,877-case regression and four actual
process kills/six SQL corruptions remain at the [boundary milestone](p02/paid-world/BOUNDARY_INTEGRATION_REVIEW.md).
They were not rerun on these test-only additions or their rebuilt binaries; no broader new claim is made.
The latest verified increment is preserved at `codex/milestone-paid-world-constraints-20261001`.
The clean pre-change checkpoint is `codex/checkpoint-before-paid-stock-genesis-20261001` at 9c192bd;
exact scope and verification are in the [dispatch record](p02/paid-world/STOCK_GENESIS_DISPATCH.json).

Opus returned verified 5.5/1M metadata, requested Max, verified pre/post context and no tool denials.
The latest run took 1,334.069s under 1,800s and had a client estimate of $8.3776246 under $20; the first
build/test passed. Astra reviewed the complete fixture/tests and reran the focused suite. No paid retry,
model substitution or claim of net credit savings occurred. The external job is terminal; nothing
awaits approval. Earlier WIP checkpoints and failure evidence remain linked in the integration packets.

Continue [market rules/local knowledge and scheduled people](p02/spot-exchange/INTEGRATION_PLAN.md)
toward the actual two-market cargo/escort circuit. Do not substitute repeated settlement reviews for
these missing gameplay bindings. Participant archived-quote queries, native
pacing and repeated-opportunity fairness need actual application policy. Canonical actor-ID execution
is deterministic, not an equitable-service guarantee. Dynamic roster/control transitions and scale
must preserve historical delivery and issuer revisions; no revision pruning or silent profile migration.
Dead-specific quote expiry remains additional unrun evidence. Native paid UI and retained-save migration
are not implemented. All remaining campaign, art/audio, multiplayer, device and human gates stay open.

The [atomic spot exchange component](p02/spot-exchange/INTEGRATION_REVIEW.md) is accepted at
`codex/milestone-atomic-spot-exchange-20260930`. It joins existing goods, provenance and finite
currency under exact quoted terms. Astra completed the initial Opus delivery's missing continued
history through explicit Production V12, corrected issuance freshness/stale-plan and seller-fee
handling, and added adversarial tests. Full solution build, 1,727 Core cases and one actual hosted
admission contrast pass: 1,728 distinct passes, zero skips, 30 new cases. All 507 original tests,
23 frozen specs, source database and unrelated predecessor staging remain exact. Architecture is
updated; [evidence](p02/spot-exchange/qualification/INTEGRATION_CHECKS.json) pins the accepted source.

Opus returned verified 5.5/1M metadata with requested Max on implementation, but the adapter reported
an optional shell-read denial and the scope was incomplete. Its separate read-only critic timed out
at 900s without a report or usage metadata; no paid retry occurred. The explicitly announced native
[independent critic](p02/spot-exchange/INDEPENDENT_REVIEW.md) found no actionable correctness defects.
No external job is active. WIP delivery and tested snapshots are retained in the packet. This outcome
does not establish net credit savings.

The [complete paid-world sequence](p02/spot-exchange/INTEGRATION_PLAN.md) retains the current
binding and its qualified boundaries above. Local price knowledge and source-backed stock rules
now have the bounded profile-33 binding. Full demand, scheduled trading, two-market cargo/escort
and all remaining P02/P00–P14 gates remain open. Unsupported older worlds still reject unrecorded paid candidates.

Two bounded Opus 5.5 / 1M / requested Max assignments completed successfully: the
[retained native gift route](p02/retained-gift-native/INTEGRATION_REVIEW.md) and
[resource encoding optimization](p02/retained-resource-encoding/INTEGRATION_REVIEW.md).
Both returned verified model/context, verified pre/post synchronization and no denials. No external
job is active. Their client estimates total about $21.81; no billing or net credit-saving claim is made.
Astra reviewed and integrated both at `codex/milestone-retained-native-gifts-20260930`. Normal
background-rendered active/reopen input, saved-journal authentication and 237 session regressions pass.
An independent SQL audit preserves every original historical row. Three unchanged-deadline startup
failures remain recorded; the later native pass does not establish reliable production latency.
Full retained replay allocates about 11.2% less while reproducing the exact head, without a consistent
total-speed result. Pure C#, canonical formats, full recovery and all gates remain unchanged.

The accepted new-test integration repair scopes authenticated archive recovery and independent SQL
inspection over byte-identical copies. It changes no runtime source and preserves all 501 files that
predate these assignments, 23 frozen specs, the original source and unrelated predecessor staging.
The 505-file resource-delivery pin remains historical: only this newly delivered result test differs.
Relevant WIP checkpoints and failures remain in the two packets. Build and 1,985 distinct managed cases
pass with no skips; [final evidence](p02/retained-gift-native/qualification/FINAL_CHECKS.json) deduplicates
the 1,698 Core, 104 resource/selector, 1 native-result and 237 session cases. The original 520-use world
now completes native offer/cut/accept/own-use 521 and separate paused reopen with exact history and 5
archives. The fresh keyboard route/reopen also passes; older controller/pointer journals are
reauthenticated, not newly rendered. Its history button reads a still-live terminal outcome; an
archived-only native lookup, transition-boundary interruption, physical input/OS focus, reliable
startup latency, peak memory and all full phase/campaign gates remain separate.

The owner-requested Opus5.5/1M/requested-Max implementation of the
[retained proposal transition](p02/retained-proposal-transition/INTEGRATION_REVIEW.md) is accepted at
`codex/milestone-retained-proposal-transition-20260930`. It adds an explicit
profile27-to-profile30 copy-and-publish operation and a managed continuation of the original 520-use
world through a helper offer, actual ordinary archive cut, player acceptance and later own use 521.
The following native increment binds that transitioned world; keep the original source unchanged.

Clean build and 2,051 distinct managed cases pass (Core 1,692; Host 359), including 22 focused cases,
authenticated recorded reads and reauthentication of retained native gift journals. An independent
SQL audit preserves every original non-head row, all 1,579 prior commits and three original archives.
The final journey has 521 uses/five archives. All 496 existing tests/fixtures, 23 frozen specs, the
original database and unrelated predecessor staging remain exact. [Final checks](p02/retained-proposal-transition/qualification/FINAL_CHECKS.json)
retain source/build pins; the packet preserves the incomplete/failed attempts and diagnostic correction.
The earlier Host run was interrupted with a missing native manifest; its idle wrapper sample did not
establish a worker hang. The corrected-input same-selection run passed within its unchanged 600s bound.
That earlier milestone did not qualify native transitioned-save selection/use; see the new packet.
Actual transition-boundary process interruption remains open.

[Actual dispatch](p02/retained-proposal-transition/DISPATCH.json) verifies `claude-opus-5-5`/1M metadata,
requested Max, synchronized context and finite limits. The run took 1,576.339s with a $21.3186486 client
estimate, not billing or demonstrated net savings. It delivered useful code/tests/docs, but a Glob
outside its task directory produced a terminal permission-denied adapter result and the worker returned
with broad verification unfinished. No external job is active; no model retry or permission expansion
occurred. Astra reviewed the implementation and added meaningful missing contrasts. The earlier Opus
native-controller repair and Fable spending-limit failure remain historical, terminal runs in
[compatibility evidence](CLAUDE_MODEL_COMPATIBILITY.md).

Reusable completion/artifact-scope discipline is upstream in ecosystem commit `59813730` with all 71
skill-alignment entries verified, and in project instructions. Ecosystem`ad0c2e2` adds heavy-check
serialization and content/build pinning, synchronized to all three installed reference copies.
Required checks must finish before a
worker returns; concrete task-scoped output paths replace parent temporary-directory discovery.

The preceding [native gift interface](p02/native-material-gifts/INTEGRATION_REVIEW.md) is
preserved by `codex/milestone-native-material-gifts-20260930`. A separate `--gifts` save exposes an
explicit stack/recipient/quantity form, participant offer/history inspection and a four-action Gifts
wheel. Captured choices never silently change or renew; the native advisory lifetime is 1,200 advancing
steps. Accepting items is separate from healing and treatment permission. An explicit NPC policy is
resupplied on reopening. Timeless architecture and runtime launch/control docs describe the binding.

Build and 259 distinct affected managed cases pass, including 19 focused cases and authenticated native
save effects. Three rendered journeys cover all four gift actions and three separate paused reopens;
the unchanged native care controller route/reopen also passes. Review fixed same-row reselection,
clipped layout and controller-release delivery. Real fixture injury/crafting/revocation precede the
native run; this does not claim native accepted-dose use, archive traversal, retained-world migration,
physical devices, OS focus, human art acceptance or full phase completion. All 490 old pinned test/fixture
files remain byte-exact; the test project differs only by two proved additive source links. All 23
frozen specs and the unrelated 19-file staged predecessor work remain exact. Failures stay in the packet.

The prior [proposal interaction boundary](p02/proposal-interaction-boundaries/INTEGRATION_REVIEW.md)
retains its 165-case qualification and `codex/milestone-proposal-interaction-boundaries-20260929`.
It proves explicit archived participant lookup, bounded concurrency/stop/failure behavior and ordinary
profile30 excavation. Deferred structural excavation remains a separate admitted profile. The
[next native slice](p02/proposal-interaction-boundaries/NEXT_NATIVE_SLICE.md) is now implemented by
the gift interface packet above; its Host-only report remains historical evidence.

The preceding [explicit NPC gift decisions](p02/npc-material-decisions/INTEGRATION_REVIEW.md),
preserved by `codex/milestone-npc-material-decisions-20260929`, retain their 154-case qualification.
Actual NPC-selected crafted medicine, later player acceptance/use, wanted/unwanted replies and no-repeat
decisions through real archive/reopen are proved. Configuration is explicit immutable Host advice,
supplied on reopening; it grants no inventory consent or treatment permission.
The preceding [player-session binding](p02/scheduled-material-proposals/INTEGRATION_REVIEW.md) is
preserved by `codex/milestone-scheduled-player-proposals-20260929` with its 142-case qualification.

It builds on [saved material proposals](p02/material-proposals/INTEGRATION_REVIEW.md),
preserved by `codex/milestone-saved-material-proposals-20260929`. Fresh profile30/action16 worlds
have independently authenticated local creation, acceptance, decline and withdrawal, deterministic
expiry and immutable command receipts. Offers reserve nothing; settlement rechecks actual control,
stock, body/reach/readiness, both opportunities and coupled physical/liquid/material effects.
Open grants survive an actual automatic archive cut; both participants can retrieve archived terminal
outcomes through a verified replay. The timeless [reference](../Runtime/docs/architecture/material-proposals.md)
records exact ownership, time, disclosure and continuation rules.

[Final checks](p02/material-proposals/qualification/FINAL_CHECKS.json) pin 1,941 distinct managed
passes (Core1,684; Host200 + proposal30 + legacy36 with nine overlaps) and four actual SIGKILLs
before/after creation and acceptance. Recovery checks every authoritative table, receipts, identities,
exact retry and recorded history. These process fixtures cross no archive cut; the managed cut proof
is separate. All 477 old tests/fixtures, 23 frozen specifications and unrelated staging remain exact.
The independent critic found real carry, archived-outcome, revision and admission gaps; the bounded
repair and Astra's contrasting tests address them. Original failures and precise exclusions remain in
the packet. Three verified Fable5.1/1M/requested-Max jobs produced the implementation, critic and repair;
client estimates and friction are recorded there, without claiming billing or net credit savings.

Profile30 is a named sibling of paired profile28; those routes are not combined. The explicit
retained27-to30 transition above does not admit profile28 migration. The nominal29 gap preserves old assertions. Independently delivered service submissions select one
advancing step; the player-session binding below instead uses the existing scheduled cutoff.
Admitted multiplayer control/session transition and pair/proposal composition remain required before
multiplayer integration. Historical participant lookup performs a full replay off
frame callbacks; production indexing, response equalization and live hardware/I/O fault injection
remain unqualified. General NPC knowledge/motivation, full native UI/device acceptance and network authentication remain open.

Earlier accepted layers remain linked rather than redispatched:
- [Durable paired Host](p02/durable-consented-world/INTEGRATION_REVIEW.md), milestone
  `codex/milestone-durable-consented-world-20260929`: explicit profile28/schema8, 244 distinct managed
  cases, two actual kills and two real automatic archive cuts. It is privileged paired publication.
- [Raw custody](p02/consented-custody/README.md), [world boundaries](p02/world-consented-material/BOUNDARY_PROOF_INTEGRATION.md)
  and [formats](p02/consented-format-qualification/INTEGRATION_REVIEW.md) retain their evidence and
  residuals. Do not redispatch their completed proof packets or claim format freeze.

Continue the [full custody integration path](p02/consented-custody/INTEGRATION_PLAN.md): bind actual
Host scheduling/control, player/NPC decisions and native presentation using the same authority.
The player-session and ordinary restorative bindings now expose profile30; profile28 remains separate.
Care permission or friendship never implies inventory consent.

The original post-cut 520-dose objective now has an explicit legal transition and a managed earned
acquisition/use path in the new packet. Its unchanged OwnerOnly custody still forbids unilateral gifts;
the saved bilateral proposal supplies consent. The exact potion ingredient still has no gathering source.
Do not fabricate stock, relabel definitions, replace genesis or silently migrate history. The
[joined native slice](p02/retained-proposal-transition/NEXT_NATIVE_SLICE.md) has its bounded
qualification in the retained-native packet above. Actual transition-boundary process interruption
and archived-only native history lookup remain unproved.

Recoverable starting and WIP references are recorded in the proposal packet's preservation/checkpoint
JSON files. They exclude unrelated predecessor staging; WIP snapshots are not acceptance claims.
Continue remaining P01/P02 dependencies against the full plan, including geography/history/streaming,
wet construction/occupancy/custody joins, NPC society, content reachability and the two-market
cargo/escort circuit. This navigation list neither replaces the scope nor waives a gate.

## Earlier verified runtime checkpoints

`codex/milestone-npc-material-decisions-20260929` preserves copied opt-in NPC preferences, own-control
offers/acceptances/declines, stable semantic decisions and supplier-only aid deferral while waiting
for a gift answer. Build and 154 affected managed cases pass, including 12 new NPC cases and eleven
preserved player cases. One real automatic cut and actual reopening prove stable occupied decisions.
The independent critic found four useful defects; fixes, test/fixture failures and precise proof limits
are retained in the [packet](p02/npc-material-decisions/INTEGRATION_REVIEW.md). No new external model
request, native/device, process-kill or production acceptance is claimed.

`codex/milestone-scheduled-player-proposals-20260929` preserves all four player proposal selections
through actual session cutoffs, bounded participant views, sender-work arbitration and later accepted
dose self-use. The build and 142 affected managed cases pass, including 11 new cases; source critique
and exact preservation checks are retained. This is a player-session checkpoint, not completion of
the larger NPC assignment, pending-excavation join, native UI, control transition or any full phase.

`codex/milestone-saved-material-proposals-20260929` preserves the fresh profile30 saved-offer
lifecycle, bounded carry, participant history, 1,941 distinct managed passes, four real process
kills/recovery and current architecture. It is a local Host progress marker, not native gameplay,
network authentication, retained-save migration, format freeze or full assignment/phase closure.

`codex/milestone-consented-custody` preserves the bilateral counted-source authority, 147 passing
cases, independent critique and corrections, eight retained pre-fix counterexamples and current
architecture. The [packet](p02/consented-custody/README.md) defines this standalone scope; no complete
gifting, retained-world acquisition, phase or production gate closes here. Reusable handoff and
receipt-checking lessons are upstream at ecosystem commit `8201a6d` with six verified installed targets.

[Immutable resource-batch encoding](p02/resource-batch-encoding/README.md) reuses private bytes on
exact immutable batch owners without changing codecs, schemas, replay or admission semantics.
Clean build; 253 distinct managed cases including two actual-native authentication cases, the original
three resource-history byte fixtures, and all six native routes/twelve processes pass. Independent Fable critique
improved ordinary golden-byte coverage and copied-input tests; architecture and project doctrine are
updated with the code. Complete same-save allocation falls 13.43%; no causal wall-time or production
latency claim. Native controls and integration preservation are recorded in its packet. Milestone:
`codex/milestone-resource-batch-encoding`. No full phase or production gate is closed by this increment.


The full same-save replay still allocates 9.760 GB. A later successful native opening does not
explain historical failures or establish production latency/scale. The prior diagnostic's empty
milestone query is explicitly corrected in its record; all earlier evidence remains preserved.

## Playable scope and controls

The authored farm connects movement, Wren's dialogue, finite gathering, ration crafting, a funded
six-wood quest, reward and NPC construction of a physical guard. Worksite/watercourse, encounter,
care and coordinated-history fixtures exercise additional explicit saved profiles. They do not
constitute the full inhabited-world game. Default startup and fixture entrypoints differ; consult
[Runtime README](../Runtime/README.md) and the relevant qualification packet before launching.

Input submits captured targets/costs/revisions and never silently retargets or retries. The pack,
four-action wheel, Care/Gifts pages, daylight and encounter presentation remain bounded implementations;
icons, capability-relevant screens, rich tooltips, full maps/calendar and accepted expressive pixel
art retain their complete design obligations. Background injected input does not prove physical
controls, OS focus, art quality or human enjoyment.

## Scope and acceptance

All **181 baseline requirements**, **14 unrun full UI cases**, full P00/P01/P02 exits and the campaign
through P14 remain open. Desktop production requires Windows/macOS/Linux and 1–4 hosted players;
mobile follows through the same core. Replay/recovery, temporal/co-op behavior, representative workloads,
exports/devices, content migration, art/audio, independent reviews and human acceptance need real proofs.

Keep one deterministic portable C# Core, an in-process durable host and Godot presentation. No new IPC,
parallel authority, Rust rewrite or framework without qualification. Preserve source/custody/command/save
contracts. Public projections are the UI boundary; the farm's sight-complete view is not full visibility.

The owner's concern about basic gameplay and poor graphics remains open. Production needs the accepted
restrained historical pixel art, varied sprites, expressive animation and state-driven weather/wear/audio.
Artists own asset production separately; [art calibration](../art-style-calibration/README.md) and
[presentation contracts](../spec-v1_1/PRESENTATION_CONTRACTS.md) require human style acceptance.
UI stays icon-led with rich inspection, curated wheels and capability-relevant screens. Current textual
pack/four-action wheel do not complete that design. The full spec retains the expansive world/history/
society/ecology/magic/construction/narrative scope; this index does not replace it.

## Evidence navigation and preservation

- [Architecture index](../Runtime/docs/architecture/README.md): current timeless ownership and bindings.
- [Milestone ledger](GIT_MILESTONES.md): preserved commits, qualified scopes and limitations.
- [Earlier handoff snapshot](AGENT_HANDOFF_2026-09-29_ARCHIVE.md): preserved historical progress,
  review/failure dispositions and links; it is not the current assignment or current routing policy.
- [Full baseline and handoff](../spec-v1_1/IMPLEMENTATION_HANDOFF.md): all original production obligations.
- [Proposal WIP handoff archive](AGENT_HANDOFF_2026-09-29_PROPOSAL_WIP_ARCHIVE.md): preserves the
  superseded integration state; it is not the current assignment.
- [Current delegation recipe](DELEGATION_CONTEXT.json): shared instructions/spec/skill synchronization.

Work on `codex/runtime-foundation`. Preserve the unrelated 19-file staged predecessor work, original
saves, source archives and frozen specs. The lead owns focused commits and immutable milestone refs;
checkpoint relevant dirty/new work before every major change. Keep architecture references current
with code. Put detailed test history in its packet, then link it here rather than growing this index
into another progress archive. Shared-desktop and risk-based verification rules remain in
[implementation instructions](../AGENTS.md); no timing or acceptance threshold is waived.


## Scheduled proposal session assignment

The [player-session increment](p02/scheduled-material-proposals/INTEGRATION_REVIEW.md) binds all four
proposal intents to actual authenticated cutoffs, participant-only live views and later use of an
accepted owned medicine. A valid acceptance reserves the sender's work opportunity; a stale or
out-of-reach request leaves funded NPC work eligible. There is no extra service transaction per tick.
The [final checks](p02/scheduled-material-proposals/qualification/FINAL_CHECKS.json) pin the build and
142 distinct affected managed passes, including eleven focused cases. [Native Astra source critique](p02/scheduled-material-proposals/CRITIC_REVIEW.md)
produced stronger work/context contrasts and a corrected test expectation. Architecture is updated.
No Core/schema, client/asset, old test or frozen-spec change is made; predecessor staging stays exact.

The subsequent [NPC increment](p02/npc-material-decisions/INTEGRATION_REVIEW.md) implements explicit
giving/accepting/declining preferences and stable decisions through refusal/expiry, a real cut and
reopen. Its new crafted-dose test uses a policy-chosen offer, later player acceptance and later own use;
the preceding player-only test retains its narrower service-created setup. The remaining session history and profile-join portions of that
[assignment](p02/scheduled-material-proposals/ASSIGNMENT.md) are covered by the
[interaction-boundary increment](p02/proposal-interaction-boundaries/INTEGRATION_REVIEW.md): actual archived
inspection, profile30 ordinary excavation and genuine pending16 refusal. Complete admission excludes
combining deferred structural consequences with profile30; such a new interpretation is not fabricated.
The native gift controls are qualified in the current packet; production policy authoring, general
NPC psychology/knowledge and recurring need episodes remain open. Preserve the full custody
integration path and original 520-dose objective above; no fresh world is a retained-save migration.

Fable's one run is terminal: monthly spend limit after 113.221 seconds, verified input synchronization,
notes only, and two recorded tool permission denials. [Failure evidence](p02/scheduled-material-proposals/DELEGATION_FAILURE.md)
includes actual Fable5.1/1M metadata and the client cost estimate; no savings are claimed. No approval
is waiting and no retry or model switch occurred. Astra completed the player and NPC slices directly.
Do not poll/restart handle12326. Fable5.1 remains approved with Max effort; more Opus use is optional.
