# Playable local worksite qualification

This increment connects the existing profile18 authority to a separate fresh content entry, public
multilevel geometry and the ordinary Godot controls. It advances the planned excavation/building
integration; it does not close P01, P02, production scale, art, devices, multiplayer or the full campaign.
The owner’s pure-C# decision remains fixed. Initial content/client implementation changes no Core
source or simulation law. A subsequent measured recovery fix memoizes the immutable world's canonical
hash; it adds no native bridge, new authority, changed bytes or skipped verification.

## Implemented scope

- Two unstarted projects use distinct funded supply commitments and ordinary Wren construction.
  The player retains eight original wood and can gather the four additional units from the finite oak.
  Each six-unit fabrication conserves installed mass and byproducts; 200 work units are required per job.
- A finite search selects a natural contour under one retained stratified generator rule. New content
  admits the exact selected terrain, clear authored corridors and rated support. Existing saves are
  neither rerolled nor implicitly upgraded. Search exhaustion is a refusal, not an admission bypass.
- Actual built stairs lead onto a fixed and removable deck. Three earned excavation units remove
  the adjacent support through the existing durable consequence owner. The authored overhang allows
  an ordinary assisted climb out after settlement.
- `LocalWorksiteView` exposes committed occupancy/member geometry, own mining targets/tools/effort
  and pending status. No raw authority, source interval, NPC bag or calculation cursor enters the UI.
- Explicit site selection enables contextual Mine and Climb wheel sectors. Offers capture exact
  revisions, tools and traversal displacement. Open slots cannot silently switch ledge exits or quests.
- A finite blockout renders elevation, actual member dimensions, terrain cuts and raised contours.
  Member nodes are reused; unchanged terrain does not rebuild. The original flat renderer stays separate.
- `--worksite` / `python3 tools/play.py --worksite` uses `wrens-worksite.sqlite`. Original farm entries,
  save upgrade chains and source content remain preserved.

The [timeless reference](../../../Runtime/docs/architecture/local-worksite.md) describes ownership,
contracts and boundaries. These geometric volumes, farm wood masses, tool roles and structural ratings
are explicitly demonstration calibration. They do not establish realistic timber, mining balance,
encumbrance, production art, animation or comprehensive room/underground rendering.

## Qualification record

Initial managed qualification passed: **1,479 cases**, comprising 951 Core and 528 Hosting/client tests,
with no failures or skips. The full hosting suite took 10 minutes 21 seconds on this shared host; the
new complete funded/recovery journey took about 4 minutes 57 seconds within that run. This is correctness
evidence, not a latency/throughput pass. The client then rebuilt with the additional native-probe assertions
and a pending-work status label. The later hash-cache correction adds two hash/concurrency regression cases
and requires the new full pass; the initial pass alone does not qualify that correction.

Implementation commit `e3e9040` preserves that managed checkpoint. The first movement-corrected rendered
journey passed in four separate Godot processes, with seven captures inspected by the integration lead.
Stricter independent review exposed additional probe assertion gaps, subsequently corrected. Its later
final reopen missed the unchanged 30-second limit, so the initial native pass does not qualify the final
source. The measured C# hash-cache correction has its own regression and native qualification below.

The [post-cache full regression](qualification/full-regression-hash-cache.log) passed **1,481 cases**:
953 Core and 528 Hosting/client, zero failed/skipped. Hosting took 8 minutes 45 seconds; this remains
correctness evidence. The cache and final probe sources now await the final rendered journey under the
unchanged deadlines. No worksite milestone is qualified until that result passes.

- Initial projection checks: eight passed.
- Initial relevant client/legacy checks: 46 passed, including exact offers and input/wheel behavior.
- Legacy rendered wheel and separate-process reopen passed under the existing 45-second bounds,
  with controlled background focus and zero focus requests. Physical/OS acceptance remains separate.
- [Full managed regression](qualification/full-regression.log) and retained TRX files record all 1,479 cases.
- [Initial rendered result](qualification/native-worksite-settling.json): fresh 24.284 s, build 96.110 s,
  mine 40.348 s and reopen 30.380 s, measured inside each probe. All processes exited zero without engine
  errors. The final paused reopen preserved database bytes; each recovery matched the prior public hash.
- The journey exercised two actual funded quests, partial construction recovery, 14 installed members,
  walking up/across/down built stairs, wrong-floor mining refusal, contextual keyboard/controller choices,
  cancellation on injected focus loss, saved partial mining, one-metre movement of only the removable
  assembly's two members and the real post-settlement climb out. This native scene has no rider on the
  falling deck; the managed journey separately proves rider motion.
- The [seven rendered captures](qualification/native-worksite-a58kcelb/) show the upper deck, lower stance,
  settlement and paused recovery. Geometry and actor height are represented, but faint cutaways,
  placeholder tiles and a large text-heavy HUD still need the planned presentation/clarity work.
  The reopened introductory hint is generic rather than progression-aware. This is no art or UX approval.

The native runner is [check_native_worksite.py](../../../Runtime/tools/check_native_worksite.py).
It uses a new disposable save and four stages: fund/start construction, recover/build/walk/start mining,
recover/finish mining/climb out, then exact paused reopen. It uses actual keyboard/controller events,
the live session and durable saves; it does not inject completed projects, earned resources or actor
endpoints. Its new scenario has a 180-second in-game overall bound per stage and a 210-second external
process deadline. These do not change any prior gate or imply production latency acceptance.

## Retained findings and corrections

1. Initial content tried to combine a depressed region with a different generation-rule pin. The
   engine rejected it. The corrected content uses one rule/seed and a naturally generated contour;
   no terrain codec, admission rule or excavation entitlement was weakened.
2. The two projects omitted the player's required access witness. Admission rejected them. Each now
   retains an actual home-to-parking witness alongside the builder's work-to-home witness.
3. Independent source critique found that the first overhang trapped the player after settlement:
   enough clearance to stand did not provide the required rise-before-traverse clearance. The authored
   deck now ends before the lower work stance; the real journey must prove the post-settlement exit.
4. The partial-mining test assumed an immediate receipt. The larger scene legitimately performs
   pending preflight even for partial work. The test now drains the existing bounded continuation and
   checks unchanged pre-publication state plus one terminal receipt; the work policy was not increased.
5. Client review found same-label refresh could select another ledge exit or another quest from the
   same person. Exact displacement/actor revision and quest definition/version now remain pinned.
6. Initial test compilation used nonexistent body-hash/quest-display members. Tests were corrected to
   actual canonical body bytes and the quest title; the failing build logs remain.
7. The [first native run](qualification/native-worksite-initial.json) stopped during initial gathering:
   a queued movement sample completed after input release, moving the player one 32-subunit step beyond
   the observed endpoint. The probe now releases, observes settling and corrects through ordinary input
   within the original 20-second walk deadline. Exact XY/Z assertions remain; no tolerance, teleport,
   speed, authority or deadline changed. The original failure and subsequent passing run are retained.
8. Final independent evidence critique found two native assertion gaps: comparing only zipped heights
   could miss wrong member identities, and temporary exhaustion could mask the completed-face blocker.
   The probe now pins the complete member identity set and checks exact XYZ, geometry and condition;
   completion refusal is checked after effort/readiness recover, with the completed-face reason required.
9. The same critique found that a successful predicate on a late frame could bypass a deadline check.
   All await-condition successes now check the original local/global limits, walking rechecks its limit
   after settling, frame waits check the global limit and final success rechecks it. Bounds are unchanged.
   The external 210-second process timeout remains independent. `FocusRequests: 0` is a declared fixture
   property, not an instrumented OS counter; background injection never establishes OS-focus acceptance.
10. The [stricter follow-up](qualification/native-worksite-final.json) failed the completed-face message
    check after the live effort predicate passed. `CurrentView` reads the live published world, while
    `CurrentOffers` is the frame's presented cache; that test did not wait for both observations. The
    corrected predicate requires recovered effort/readiness **and** the specific completed-face UI
    blocker within the same 15 seconds. A missing completed-site blocker still fails. The failed report
    lacks the old blocker text, so the exact old text is not claimed; the replacement records it.
11. The [next run](qualification/native-worksite-presentation.json) stopped before that check because
    its single Talk input did not open a conversation. The report does not identify the receipt reason.
    The approach now uses the existing quest/input-probe arrival contract: release movement, observe
    150 ms of stable player/target revisions, require the presented Talk revisions to match, then press
    E once. The same 20-second approach bound and range apply; rejected Talk is never automatically retried.
12. [Strict native run](qualification/native-worksite-qualified.json) passed fresh/build/mine, including
    the strengthened member/blocker checks, but final reopen failed at 30.095 seconds. This is an actual
    missed opening bound, not a pass or an assumed focus/contention interruption. Separate measured
    recovery of that 1,334-commit save took 26.517 seconds and cumulatively allocated 26.373 GB; fresh
    admission took 0.416 seconds and attach/view 0.011 seconds. Native and console timings differ, and
    the console result is not a substitute for the native gate. The OS sample could not symbolize managed
    frames; it is not a managed-hotspot proof. Direct phase measurements and source review identified
    repeated canonical serialization inside deterministic root validation.
13. `WorldSnapshot.ContentHash` now caches only the immutable digest on the same root, using atomic
    publication. New roots own independent caches; mutable encoded bytes are never cached. The mandatory
    validations, interpreter, canonical encoding and complete replay comparisons remain. Independent
    bounded source critique checked constructor initialization, recursion, reviewed immutable components
    and concurrency, finding no blocker. Concurrent cold computations may duplicate work safely. This
is no exhaustive transitive-alias audit or production history-scale acceptance.

One critic's initial face-bounds finding was withdrawn after rereading the changed layout. Native
review produced actionable content and choice-pinning corrections; no external provider call or
credit-saving claim is associated with this increment. Source workers did not build or commit;
the integration lead serializes builds, heavy regression and native deadline probes on the shared host.
The final independent probe reviewer rechecked the three corrected assertion/deadline issues and found
no remaining consequential source finding in that bounded scope. This is source/evidence critique,
not physical input, human playtesting or a review of the complete game.

Ecosystem commit `ffc0ad6` records the reusable asynchronous-probe guidance in its verification skill
and project doctrine; three installed entrypoints were hash-verified after guarded synchronization.
The local implementation instructions link that maintained guidance. The skill validator passed using
an isolated PyYAML dependency after the default and bundled Python lacked it; no global Python packages
or unrelated ecosystem files were changed.

The probe corrections change no application contract. The hash-cache contract is recorded in
[identity and determinism](../../../Runtime/docs/architecture/identity-and-determinism.md). Source pins
distinguish each retained source/build boundary. Disposable databases stay local; hashes remain in reports.

## Recovery measurement and byte preservation

The [small profiling source](qualification/open-profile-source/Program.cs) uses the existing public
admission, authority and paused session. Its optional `phases` mode reads a closed save, interprets each
retained batch and independently compares the complete candidate SHA-256 to each stored after-hash.
That diagnostic is not a substitute for the authority's full receipt/checkpoint/identity validation.
Run its project with the .NET 10 SDK and a closed worksite database; append `phases` for the second mode.
The actual runs used the same source from `/private/tmp`; the initial `/tmp` symlink build failure and
corrected canonical-path build are retained.

| Same 1,334-commit saved workload | Before cache | After cache |
| --- | ---: | ---: |
| Complete authority recovery, elapsed | 26.517 s | 23.899 s |
| Complete authority recovery, cumulative allocation | 26.373 GB | 22.104 GB |
| Phase diagnostic candidate hashes equal to stored hashes | 1,334/1,334 | 1,334/1,334 |

[Before](qualification/open-profile-before.json), [after](qualification/open-profile-after.json) and
[exact-byte follow-up](qualification/open-phase-profile-after.json) retain the raw measurements.
Allocation decreased about 16.2%; the elapsed comparison is one local observation on a shared host,
not a controlled latency benchmark. The native sample ran during the first authority measurement;
separate phase timing also varied, so no CPU-speedup percentage is asserted. Remaining roughly 22 GB of
cumulative replay allocation and full-journal scaling are unresolved production costs. No workload,
deadline, history depth, comparison or validation was removed to obtain the reduction.

## Preserved limits

The original farm art and worksite blockout do not answer the owner's production-art concern. Native
captures do not grant human clarity/style approval. Controlled background focus and injected events
do not qualify physical devices, desktop exports or operating-system focus. This bounded authored
locality is not infinite frontier generation. Production performance, history scaling, wet composition,
harmful impacts, broader fractures, general interactions and the full specification remain open.
