# Progress and efficiency audit

The project has made substantial technical progress and recently started converting more of that
work into connected player capabilities. It still has a large gap between its qualified development
scenarios and the complete playable experience required by the plan. The present workflow is good at
preserving and proving narrow increments, but too willing to accumulate separate proofs, profiles
and reports before testing the usefulness of the combined experience. More code or more passing
cases is not the main thing needed next: integration, reliability and actual player evaluation are.

This audit is pinned to **`ffa1f971ee2e5541ea7608b6d1da78b8b0598d4d`**, the accepted shared-History
checkpoint committed at **2026-10-03 14:30:02 UTC**. It reviews repository evidence, not a new build
or playtest. The concurrently edited delegated-recovery work is excluded from completion claims.
Its two retained missing-import build failures are mentioned only as additional process evidence.
No source, test, frozen specification or acceptance threshold was changed for this audit.

## What has actually advanced

The useful distinction is between a retained foundation, an integrated developer journey, and an
accepted game experience. This repository has all three kinds of obligations, but mostly evidence
for the first two. The following is a delivery sequence, not a feature-count score:

| Recorded period | Concrete advancement | What that does not establish |
|---|---|---|
| September 8–10 | Portable deterministic primitives, durable authoritative farm, actual gather/craft/quest progression and Wren's routed construction, with native restart journeys. | A complete P01 or P02 exit; broad inhabitants, campaign generation, production platforms or human acceptance. |
| September 11–29 | Inventory/targeting/wheel/daylight; much deeper finite terrain, water, structure, care, material provenance and continuation machinery; consented transfers and saved gifts. | That all separately admitted physical interpretations compose in one ordinary player save; large-world throughput or satisfactory play. |
| September 30–October 1 | Retained-world gift continuation, real paid trading, the two-market cargo route, actual road threat and retreat. | Full merchant/escort economy, frontier streaming, full combat/party behavior or migration between all scenarios. |
| October 2 | Authenticated transport, native four-player connection and reconnect, durable controller lifecycle, recovery and explicit co-player permission/control foundations. | A general four-player product or explanation of the retained timing failures. |
| October 3 through the pinned checkpoint | Actual selected-character control and shared gifts, production, recipes, construction, trade, care and independently scoped History. Genuine input/permission/return journeys exercise the same UI and authority. | Full selected-domain breadth, full profile 40 recovery, historical-name catalogs, native History paging/paid quotes, physical-device comfort or phase acceptance. |

The recent integration is real. [Cargo composition](../p02/cargo-world/INTEGRATION_REVIEW.md),
[road threat](../p02/road-threat-corrections/lead-integration/INTEGRATION_REVIEW.md),
[selected production](../p02/selected-production/RESULT.md) and
[shared History](../p02/shared-history/RESULT.md) join actual costs, custody, time, permissions,
saved history and native interaction. Calling the work only isolated demos would miss that progress.
Calling it a complete game, or treating the sum of narrow milestones as a completed phase, would
also be wrong. The [P02 plan](../../spec-v1_1/IMPLEMENTATION_PLAN.md#p02--one-inhabited-legacy-place-one-complete-playable-loop)
still requires the inhabited loop, causal NPC behavior, investigation, coordinated field puzzle,
ecology/liquid introductions, audio, cultural/content obligations and actual human recognition of
the outcome. The companion requirements audit should own the full coverage reconciliation.

## Pace: what the numbers support

[metrics.json](metrics.json) records the exact snapshot, commands, filters and raw values. Git
committer times are used consistently and normalized to UTC. The Beta path history contains 274
commits from September 8 00:00 UTC through the checkpoint; 103 fall in the September 27–October 3
window, and 20 fall on October 3 UTC through 14:30. These are activity and checkpoint counts,
including documentation, diagnostics and fixes. They are **not** 274 delivered features or a rate
per working hour. The record provides no reliable total of active human/agent hours, interruptions,
concurrent work or total model usage. No full-game completion percentage or credible shipping date
can be derived from those counts.

The sequence supports a narrower judgment: integrated shared-player capability expanded quickly
over the last few recorded days, after several weeks of deep authority/continuation work. The
distance from a native farm in early September to authenticated four-player domain screens in
early October is meaningful. The distance from those screens to the full 181-requirement campaign
is still large, and many of its hardest dimensions have no comparable measured delivery rate.
Extrapolating the latest screen-per-checkpoint cadence to ecology, generation, narrative, art,
audio, scale or human acceptance would be unjustified.

The lead should report pace as completed **user journeys and dependencies removed** over a named
interval, together with new defects and still-open gates. Report new cases separately from reused
regression coverage. A total such as 2,360 passing cases is valuable compatibility evidence, but a
poor headline for how much additional game the player received.

## The world is increasingly composed, but still fragmented for the player

There are **38 supported world-snapshot profile codecs**, numbered 1–28, 30 and 32–40 in
[WorldSnapshotCodec](../../Runtime/src/OpenFantasy.Core/Worlds/WorldSnapshotCodec.cs). This is a
compatibility and interpretation surface, not 37 inhabited worlds or 40 separate engines. The
snapshot has shared typed owners, and later cargo/control admissions compose many earlier systems
through them. Profiles preserve old byte/rule meanings rather than silently upgrading lived saves.
Those are legitimate reasons for explicit profiles; indiscriminately collapsing them would damage
the preservation contract.

However, the ordinary local launch still selects the construction farm, profile 5. The
[bootstrap](../../Runtime/client/scripts/Bootstrap.cs) and
[session startup](../../Runtime/client/scripts/FarmClientSession.cs) retain separate selections for
worksite, watercourse, breach, encounters, care, restorative care and gifts, with further cargo,
network and retained-save routes. Newer capabilities do not automatically become part of an older
played farm. The narrow 27→30 transition and earlier preserved farm upgrades prove specific paths,
not a general current campaign migration. A newcomer following the default launcher will therefore
see less than the collection of recent milestone descriptions suggests.

The actual scale is finite and modest. The original native farm admits one **32×32×16** region,
with a source place rectangle of **9×7** cells
([admission](../../Runtime/content/native-farm/admission-v1.json)). The cargo circuit extends it to
**14 connected full-size route regions** plus the separate retained breach receiver (**15 regions in the complete root**), inside the **16-region** local
bound ([CargoCorridor](../../Runtime/src/OpenFantasy.Hosting/Content/Cargo/CargoCorridor.cs)).
That is 14,336 horizontal region cells; it is not 14,336 authored encounters or proof of procedural
frontier streaming. The native roster is 1–4 humans; the current composed root admits at most 64
residents through the same actor bound. Those are bounded proof interpretations, not delivery of
the plan's 10,000-actor workload, 20,000-resident city probe or four active branches. No new scale
measurement was run here.

The integration priority should be a single declared current development journey with coherent
entry, discovery, work, travel, danger, recovery and continuation. Keep historical profiles and
proof fixtures available, but stop presenting their count as progress toward a unified world.
Any new profile should say which incompatible persisted interpretation requires it, which player
journey consumes it, and how its eventual migration/composition obligation is tracked. This is a
discipline for new work, not permission to remove compatibility or silently migrate saves.

## Where the workflow is spending effort poorly

### Repeated verification has a measurable, bounded cost

Six consecutive packets—selected gifts, selected production, recipes, human construction, selected
trading and restorative choices—each ran all **1,885 Core cases**. Their accepted report durations
sum to **684.114 seconds**, or about **11.4 minutes**, for **11,310 case executions**. Core source,
Core tests, Directory.Build.props and global.json have an empty Git diff between `8741766b` and
`d3f8ebc2`. The [production result](../p02/selected-production/RESULT.md) explicitly records why an
old pass was not reused: stamped dependency binaries differed despite unchanged Core source.

Those reruns were consistent with the actual binary-pinning rule; it would be wrong to relabel them
unnecessary under the existing evidence contract. It is nevertheless wasteful to keep recreating
this situation. Qualify a dependency-aware build/evidence reuse mechanism once: retain exact
previous component artifacts when all their source, toolchain, generator, build-option and discovery
dependencies match. If binary metadata alone differs, establish that fact and an approved claim
boundary explicitly; never infer binary equivalence from source similarity alone. The current
[instructions](../../AGENTS.md) already support dependency-aware reuse and require affected/full
gates when warranted. Shared History demonstrates more proportionate scoped qualification without
a fresh full-Core claim.

Eleven minutes is real but is not evidence that Core reruns dominate development time. The audit
does not sum overlapping/inherited report durations into total work time. Broad storage runs,
native setup, review and integration have different costs. Prioritize reliability and correct
fixture preparation before seeking smaller gains from shaving fast tests.

### Packet growth is now a maintenance burden

At the pinned Git tree:

| Scope | Tracked paths | Logical uncompressed bytes |
|---|---:|---:|
| Runtime src | 1,122 | 4,803,100 |
| Runtime client, including assets/probes | 322 | 1,626,059 |
| Runtime tests, including fixtures | 836 | 10,426,470 |
| Runtime tools | 60 | 821,284 |
| Runtime architecture reference | 83 | 1,015,880 |
| Implementation records, all | 53,449 | 5,916,856,842 |
| Implementation paths containing `/qualification/` | 51,201 | 5,844,481,719 |

The implementation tree references 31,457 unique blobs totaling 4,840,166,758 uncompressed bytes,
with 21,992 repeated blob references. These are not compressed `.git` size, model token consumption,
or proof that every duplicate is avoidable. Retained saves, failure artifacts and exact reproduction
inputs have real value. Nonetheless, 132 P02 packet directories, 34 P01 directories and 67 packet
scripts named `verify.py`, `finalize.py` or `check.py` expose a substantial parallel tooling surface.
JSON, SQLite, TRX and images dominate bytes; prose alone is not the storage problem.

Preserve old evidence. For new runs, use one maintained runner/finalizer with declarative workload
manifests, one exact source/binary dependency manifest per build, and content-addressed artifact
references. Keep each run's commands, environment, failures and assertions reconstructible. Do not
create another custom verifier simply to produce a new packet header. A consolidation must first
prove equivalent rejection/acceptance behavior against retained positive and negative reports;
it must not weaken the oracle or delete failures to make storage look smaller.

### Documentation repeats status and still misses stale claims

The current handoff is 428 lines/3,998 whitespace-delimited words, the milestone index 483 lines/
11,775 words, and the runtime README 909 lines/10,466 words. These exact counts are only maintenance
indicators. More concretely, the README describes actual TLS serving near its beginning but its
project-layout section still says network authentication, session grants and gameplay projections
remain pending. Its old farm summary also speaks of food use remaining pending, while later
restorative behavior exists in separate selections. Some scope qualifiers are legitimate; the
mixed present-tense summary is still misleading to a reader trying to find the current product.

A single machine-readable current status view should connect requirement/gate IDs to the current
journey, exact source dependencies, evidence, defects, owner and next prerequisite. Render the
short handoff and milestone summaries from it, while keeping timeless architecture and frozen
requirements separate. Status entries must distinguish unavailable, implemented-but-unqualified,
qualified-bounded, and accepted-full-scope. Earlier packets retain historical truth. This is a
proposal for maintaining one current index, not a new competing specification or permission to
rewrite frozen records. The present handoff should select the next work; it should not repeat the
last dozen results in enough detail to become another archive.

### Avoidable lead and worker mistakes are part of the cost

The [History result](../p02/shared-history/RESULT.md) retains three failures: a representation-sensitive
record comparison, a wrongly chosen pre-preparation conservation baseline, and actual detail
clipping. The first two were verifier mistakes; the third was a production UI defect. Keeping all
three as undifferentiated "failed attempts" hides the actionable causes. The serialization boundary
was available, but the lead's original baseline did not use it. The UI had already repeated a
read-only explanation, and the layout overflow was found only at the full native gate. These are
our integration mistakes, not unavoidable costs imposed by strict correctness requirements.

Other recent packets retain compile/analyzer errors, unprepared fixture effort, blocked paths,
unsettled input/layout and a cross-reopen reference-equality mistake. The uncommitted recovery packet
already contains two consecutive builds failing to resolve `AuthorityDispatchException`; that work
is not counted as qualified here. A shared source freeze should include a cheap focused compile and
API/namespace check under the lead's serialized build slot before broader verification. Workers can
perform read-only import/signature review while waiting. Repeating a full build after guessing an
import is weaker than locating the symbol once and checking every affected use.

Introduce small reusable fixture preconditions: settled actual view/input; lawful readiness/effort;
real admission release after disconnect; a serialized conservation baseline; complete stable value
identities. Use them in the existing actual-UI journeys rather than fabricating success or bypassing
native input. Keep rejection evidence and deadlines unchanged. Classify failures by production bug,
fixture/checker error, infrastructure/tooling, and unresolved reliability; each should have a named
disposition. The retained [scheduling experiment](../p02/runtime-scheduling-diagnosis/RESULT.md)
already sets a good finite limit on nonreproducing repeats. Preserve that discipline.

## Delegation: useful delivery, substantial friction, no demonstrated billing saving

The [compatibility record](../CLAUDE_MODEL_COMPATIBILITY.md) documents real useful Opus/Fable work
and actual failures. Examples include a world-resource implementation that consumed 1,323.143 s yet
made no runtime edits because permitted writes were denied; a 900 s review with no report; and a
successful narrow native-controller repair taking 868.279 s. Substantial retained-native-gift and
resource-encoding deliveries took 1,943.293 s and 1,419.851 s respectively and still needed integration.
The paid-world critic found real capacity and canonical-order defects. That was useful independent
review rather than ceremony.

These are selected documented runs, not a complete provider success-rate sample. Provider list-price
estimates are not subscription billing, and there is **no evidence for subscription-credit savings**.
Recent native subagents consume Astra capacity. The terminal spending-limit failure is not a reason
to retry or silently switch models. This audit executed no external request.

The routing policy should optimize accepted useful scope per bounded assignment, not number of
delegations. Keep explicit ownership, exact model/context verification and one finite independent
critic at a coherent risk boundary. Avoid paying setup/review overhead for a tiny fix unless its
risk warrants that review. Validate an owned write early when a future authorized implementation
dispatch needs it. Reserve enough of the assignment for terminal verification; an unfinished
background handle is not a deliverable. The lead remains responsible for integrating omitted
acceptance criteria rather than turning a worker's smaller result into the new scope.

## Recommended changes in execution order

1. **Finish and assess the current recovery increment, without expanding its claim.** Preserve failed
   timing/gift-opening gates and the exact crash/archive/native obligations. Do not use a later green
   diagnostic to erase an unexplained original failure. Concurrent recovery work needs its own final
   qualification before it changes this audit's accepted boundary.
2. **Choose one integrated development journey for the next useful milestone.** Connect the already
   implemented farm/work/cargo/danger/care/shared-control/history capabilities in an explicitly admitted
   current save and entry path, with truthful unsupported states. Measure the player's full progression,
   interruptions and recovery. Do not require a giant campaign rewrite or silently migrate old saves.
   Keep full P02 obligations visible, including those outside recent networking/UI work.
3. **Get bounded human feedback on that actual journey before another long sequence of screen
   expansions.** The 200 separate construction confirmations and incomplete object/HUD legibility are
   concrete reasons to test comfort and comprehension early. Injected controller evidence cannot
   answer whether a person enjoys this interaction. Coordinate the desktop/device interval; unavailable
   human feedback remains an open gate, not a simulated pass.
4. **Consolidate evidence planning and status maintenance.** One executable dependency manifest and
   runner should select inherited, affected and required-full checks; one current status index should
   generate concise human summaries. First port a small existing packet and prove its positive and
   negative report behavior. Preserve original reports, checkpoint doctrine, strict deadlines and
   full acceptance gates. Limit this tooling unit so it does not become another month of foundations.
5. **Tie later work to complete journeys and the full roadmap.** Scope each unit around a player result,
   its true dependencies and the evidence that can close it. Prefer shared implementations to a new
   actor-specific panel or profile. Still schedule the missing art/audio/content/ecology/generation
   lanes; a cleaned-up workflow does not reduce their required scope. Revisit rate estimates only after
   comparable integrated milestones and actual workload/cost records exist.

The existing preservation and authority discipline should remain. The change needed is stronger
selection of what earns a milestone, less duplicated verification infrastructure, and earlier
feedback on the combined game. The current evidence supports saying the foundations and shared
interaction layer are advancing; it does not support saying the project is close to the complete
campaign or that the current process is cost-efficient merely because many tests pass.
