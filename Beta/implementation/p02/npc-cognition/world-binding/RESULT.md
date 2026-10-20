# NPC-01 first world binding: profile 42

**Status:** accepted after one independent review, with every required change applied
([disposition](../world-binding-review/DISPOSITION.md)). Core laws and durable storage of the internal cognition
interpretation are implemented and tested on the adventure's original people, tools, quest and project. This is step 2 of the
[NPC-01 sequence](../../../NPC_01_CAUSAL_EPISODE.md#implementation-sequence-and-acceptance), bound by the
[PLAN-STATE exit decision](../../../PLAN_STATE_EXIT_DECISION.md). The host policy that proposes cognition actions
in a live session is the [next increment](../host-policy/RESULT.md). Profile 42 stays
internal and non-default. NPC-01, P02 and every gate stay open. One independent critic reviews this binding
before acceptance (see [Review](#review)).

## What exists

- **Profile 42.** `WorldCognitionAdmission.CreateFresh` accepts only an unplayed profile-41 world. The root is the
  complete profile-41 frame plus a final cognition section with one record per resident, players included. The
  section's full codec maximum is reserved inside the 16 MiB root budget. The root stores the exact `RulesPin` as a
  field before the section, and decoding refuses any other pin. A fresh adventure root is 80,540 bytes.
  The root uses schema twelve with no migration entry. Decoders for profiles up to 41 refuse a cognition root, and
  profile 41 cannot encode one.
- **Evidence from committed outcomes.** In `WorldStepProcessor.DecideImmediate`, after an action passes complete-
  candidate admission and beside the material-evidence capture, `WorldCognitionLaw` derives evidence. It covers:
  - direct gifts and settled proposals, which give the recipient `ItemReceived` (consented pairs exist only in
    profile 28, so that branch is unreachable here, and proposal settlement is untested);
  - completed deliveries, which give the quest giver `QuestDeliveryReceived`;
  - gathers, which give the gatherer its yield and a fresh look at the source;
  - inspections, which give `SourceObserved`.

  A refused or rolled-back action gives nobody anything. Evidence identities join the identity index and the
  batch's reserved identities.
- **Perception rule.** `InspectGatherSourceIntent` looks at one source within four cells, along the same anchored
  terrain and structure sightline that gathering reach uses. It needs no tool and reveals only visible remaining
  harvests. Sight is tested first, so an absent source and one out of sight are both `OutOfReach`.
- **Recorded decisions.** `RecordCognitionDecisionIntent` carries the complete decision. Admission requires:
  - the actor's own control, with no delegated witness, and no hold opened earlier in the batch;
  - the expected cognition revision;
  - a due, unsuppressed wake;
  - a fallback within the 100-step cadence;
  - a decision that changes something;
  - support from the actor's own evidence and project:
    - a new intention is adopted now;
    - waiting and working name the actor's own unfinished project;
    - working cites a received delivery for that project's quest;
    - gathering cites an observation of its source;
    - completion is recorded only for a completed project, or for gathering at its target that has yielded.

  Deferral reasons and the stock target remain planner claims.

  Replay applies the recorded bytes and never runs a policy.
- **Suppression.** Each committed gameplay boundary records its condition on pending wakes: a reading hold, an
  encounter or a paid continuation. Due step and cause are kept. Nothing commits for it alone.
- **Overflow stops the authority.** A capacity overflow propagates. `WorldCommitPreparation` no longer converts it
  into a dispatch refusal, and `SqliteWorldAuthority` marks itself as requiring reopening.

## Decisions taken in this binding

Each is the lead's choice, recorded here and put to the critic.

| Decision | Reason | Alternative not taken |
|---|---|---|
| A four-cell sightline observation range for `InspectGatherSourceIntent` | Wren never wanders, and the oak is three cells from her home, so touch reach would make the source unknowable. The existing sightline query already models terrain and structure occlusion. | Touch reach only (episode impossible); authored initial knowledge (an empty facet would then assert something false); any distance (omniscience). |
| Work on one's own project is a self-owned fact, not evidence; the completing step wakes the builder | The 200 authored work units would exceed the 64-entry evidence bound, and the actor already owns the project's progress. | One `ProjectWorked` per step, which overflows; aggregated work evidence, which adds a new law with no reader. |
| Dense records: every resident, at genesis, empty | Decision 4 gives every person-actor a record, and validation then checks the actor set exactly. | Sparse records, which leave "missing" versus "empty" ambiguous. |
| Suppression applied as a post-pass on every gameplay decision | Holds, encounters and care each open and close in different processors. One law covers all of them, and a replaced candidate drops its captured frame. | Per-processor edits, which are easy to miss. |
| A gather also yields a `SourceObserved` of the harvested node | The gatherer sees the source it just harvested. Without it, depletion could only be learned by a separate look. | Depletion learned only from a refused gather, which commits no evidence by design. |
| Decision service at advancing steps only, using the actor's action slot | It is an ordinary actor action in canonical order. A hold already suppresses service. | Riding held batches, which would contradict suppression. |

## Acceptance order: what this binding proves

| Exit decision proof | Status here |
|---|---|
| 1. Evidence only from committed outcomes, same boundary; refused gift writes nothing; undisclosed change wakes nobody | **Proved** for a harvest elsewhere; no remote delivery is possible because delivery is in person. Shown by `Evidence_comes_only_from_committed_outcomes_in_the_same_boundary` and `A_change_elsewhere_leaves_beliefs_stale_until_new_evidence_is_acquired` |
| 2. References not copies; exact recapture; no duplicate acceptance on reload | **Proved for this episode**: evidence names quest, lot and counterpart by identity, and participation stays with quests. A reloaded duplicate decision is refused as stale. Existing price knowledge is not yet referenced. |
| 3. Replay with the planner removed or changed reproduces history | **Proved structurally**: nothing in Core references the policy, and replay from genesis uses only recorded batch bytes and reproduces the root exactly. The "variant" test renames the version only |
| 4. Restart with a pending wake midway through gathering matches the uninterrupted run; due age survives | **Proved in Core**: an encode/decode restart after the first gather yields identical final bytes, and durable reopen keeps the wake. A hosted-session restart awaits the host policy. |
| 5. Holds, encounters and paid continuations suppress with a reason; overflow stops the authority; ≤41 decoders refuse | Holds, including one opened earlier in the same batch, and encounter entry **proved**. Encounter release and paid continuation are implemented but **not exercised**. Overflow **proved** in Core and through `SqliteWorldAuthority`. Refusal by ≤41 decoders **proved**. |
| 6. Decision-3 baseline on profiles 40, 41 and 42 | **Not started**; it needs the host policy driving every resident |
| 7. Offscreen labelled player-absent detailed continuation | **Not started** |

## Interpreter growth: decision 8's trigger fired

Profile 42 changed **45 production files**: 39 edited and 6 new across Core and Hosting. Profile 41 took about 25.
About 28 of the edits add 42 to hand-written profile lists, such as `is not … and not 41` and
`or WorldPartyWithdrawalAdmission.ProfileVersion`. Decision 8 therefore requires the generic facet manifest to be
reconsidered before another interpretation is admitted. The lead's recommendation is a named successor-family
predicate, for example "admits the collective-withdrawal laws", that replaces these lists. That is a mechanical
refactor and not a facet manifest. It is not done here, so the semantics of this binding stay reviewable.

## Known gaps

- **The evidence bound is reachable in ordinary play.** Every actor, players included, accumulates evidence and
  nothing replaces it. A gather adds two entries, so about 32 gathers by one actor stop a profile-42 world, and the
  same deterministic batch stops it again after reopening. Decision 7's lossless replacement, or bounds re-sized from
  measured workloads, is a precondition for proofs 6 and 7.
- `OwnResourceChanged` is never scheduled, so recovered effort waits for the 100-step fallback.
- World-level refusals for memory overflow, rollback after resolution, an occupied evidence identity and a
  delegated decision are enforced in code but untested. Proposal settlement and encounter release are untested.

- The deferral reason is not remembered when the intention is unchanged. Wren's change from "missing tool" to
  "insufficient effort" after the gift moves only her wake. Per finding 5, the current deferral and remembered
  reason changes are added before freeze.
- The paid-continuation suppression path has no test.
- Paid exchange receipts and market observations are not evidence yet.
- There is no dynamic dialogue binding, native presentation or baseline measurement.

## Verification

| Check | Result |
|---|---|
| [Profile-42 world and authority tests](managed/hosting-cognition.trx) | 12 of 12 pass |
| [Core cognition tests](managed/core-cognition.trx), including the 3 new decision/action codec cases | 31 of 31 pass |
| Full Core suite | 2,069 of 2,069 pass in 1 m 58 s. It was built before the 3 codec cases existed; no Core source changed after it. |
| Full Hosting suite | 2,242 passed, 25 opt-in skipped and 16 failed, in 20 m 56 s. Each failure is classified below. |
| The six real-process withdrawal tests, rerun with `OPENFANTASY_DOTNET` | 6 of 6 pass |
| Server tests and Godot client builds | No warnings or errors |

The 16 Hosting failures, classified before any rerun:

- **6 manifest-dependent result verifiers** (native retreat, road threat, retreat durability, outcome durability,
  native outcome, native accepted care). They throw without their recorded native manifests. This is the known
  runner-configuration class.
- **6 `PartyWithdrawalProcessTests` cases.** These are runner configuration: they need `OPENFANTASY_DOTNET` to
  start a real host. With it set they pass, as listed above.
- **1 `NativeAcceptedCareFixtureTests` export.** It is a fixture exporter that requires an explicit destination.
- **3 `ControllerPermissionAdapterTests` cases** with `permissionProfile: True`, failing at line 49 with zero
  advancing steps where one was expected. They fail identically on a clean worktree of unchanged HEAD
  `69646306`, so they are **pre-existing and undiagnosed here**. They are recorded for their own investigation.

Raw reports stay local and are hashed in the [evidence manifest](EVIDENCE_MANIFEST.json). The host does not expose
this session's API call count or per-call context, so neither is reported.

## Review

One read-only critic reviewed `c9846097` through the maintained adapter and returned "accept with required
changes". The [disposition](../world-binding-review/DISPOSITION.md) applies findings 1–4, 6 and 7, schedules 5 for
the freeze, and adopts the decision-8 recommendation for its own packet. The post-review changes are verified with
the host-policy increment's checks, recorded in [its result](../host-policy/RESULT.md).

## Documentation review

- **Plan/spec:** implements step 2 of NPC-01's sequence for Core and storage; the roadmap and handoff record that
  the host policy, baseline and offscreen proofs remain. No requirement, phase exit or gate changes.
- **Reference:** `actors-and-society.md` gains "Runtime binding: cognition interpretation (profile 42)";
  `persistence-and-history.md` adds profile 42 to the table and to the shared-codec reservation;
  `partition-ownership.md` states the facet is still one root field; `due-work-and-scheduling.md` records the
  wake as persistent unbounded-service due work.
- **Accuracy/completeness:** checked against the source and the twelve world/authority tests. Every
  limit above is stated where the reference describes the binding.
- **Checks:** `python3 Beta/tools/check_documentation.py --work-record Beta/implementation/p02/npc-cognition/world-binding/RESULT.md --files`
  with the four reference pages, the roadmap and the handoff.
