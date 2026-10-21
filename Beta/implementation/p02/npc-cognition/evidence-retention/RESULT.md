# NPC-01 evidence retention and remembered deferrals

**Status:** implemented and tested on internal profile 42, and independently reviewed: accepted with the review's
required changes applied ([disposition](../evidence-retention-review/DISPOSITION.md)). This removes the evidence-bound precondition named by the
[binding review](../world-binding-review/DISPOSITION.md) (finding 1) and lands finding 5 in one cognition codec
revision with its Python oracle. Profile 42 stays internal and non-default, and its saves are development fixtures
with no compatibility promise. NPC-01, P02 and every gate stay open.

## Player outcome

Before this, Wren and every player kept every observation and every gathering outcome they ever acquired. About 32
gathers by one actor reached the 64-entry evidence bound and stopped the world, and the same deterministic batch
stopped it again after reopening. Now looking at or harvesting a known source keeps only the actor's latest view of
it, so ordinary play no longer approaches that bound. Wren's own state also says why she is waiting: after the gift
her reason changes from "missing tool" to "insufficient effort", and that change is one remembered entry.

## Decisions

The owner's 2026-10-20 steering asks the lead to take the most logical decision, have it reviewed and record it.

1. **Bind decision 7's replacement rule, not re-sized bounds.** Re-sizing bounds from a workload only moves the
   point where ordinary play stops the world, because looks and gathers grow without limit. The
   [exit decision](../../../PLAN_STATE_EXIT_DECISION.md) already names the lossless path: beliefs are replaced like
   market knowledge, with old values in history. The bounds stay at 64 evidence entries and 64 memories.
2. **What replaces what.** A newer `SourceObserved` replaces the actor's older observation of the same source, and a
   newer `GatherOutcome` replaces its older outcome there. Replacement happens only when no intention or memory
   cites the older entry, so every cited basis, and with it every adoption's provenance, is kept. Received items and
   deliveries are never replaced: each names a distinct counterpart act. The replaced value stays in the journal and
   archived roots, as replaced market knowledge does. A gathering completion is admitted while a yield since adoption
   is held, but nothing cites that yield, so the next gather may replace it: what justified a completion is then
   recoverable only from the journal. Dynamic dialogue that needs lifetime totals must replay history.
3. **Gathering outcomes are replaced too.** Otherwise each gather still adds one entry, and the bound would be reached
   after about 60 gathers instead of 32. The cost is that the cumulative `GatheredQuantity` belief no longer exists.
   Each known source now reports its `LatestYield`. The policy never read the total: personal stock is a self-owned
   inventory fact. An actor's lifetime gathering total is history, not a held belief. A real gather always yields
   something, so the latest outcome is positive and the completion check is unaffected.
4. **Canonical form.** A root holding two uncited entries of one replaceable kind and subject does not decode, so the
   rule is a decoding invariant as well as a transition. The evidence bound is checked after replacement.
5. **The current deferral beside the intention** (finding 5). Each actor holds `CurrentDeferral`: the reason, the
   policy version that first gave it, and since when. A decision that keeps the intention with a new reason replaces
   it and adds one `Reconsidered` memory carrying that reason. The same reason again keeps its original policy and
   instant and adds nothing. Memory therefore grows with reason changes, not with wakes, as the review required.
   Reconsidered memories follow the existing memory rule that a memory's reason explains its own transition.
6. **Without an intention nothing is deferred.** A decision with no intention must defer `None`, and a held intention
   always has a deferral no earlier than its adoption. The policy never produced anything else; this makes it a law.
7. **The law changed, so the pin changed.** `RulesPin` is now `world.cognition-laws.v2`, naming retention and
   deferral memory. The cognition section is `cognition.snapshot.v2`. Recorded decision frames are unchanged, so
   journal command bytes decode to the same decisions; what those decisions do is governed by the root's v2 pin. Under decision 8 this is pre-freeze iteration: no permanent
   interpreter is minted, and earlier profile-42 development saves no longer decode.
8. **Spent effort keeps a gathering plan's reason** (from the review). Policy `cognition.delayed-supply.v3` keeps
   `AwaitingSupply` while a gathering plan's next swing waits for effort. Effort delays the swing; it does not change
   why the funded work is deferred. Under v2 each two swings added two remembered reason changes, so one long
   gathering intention filled memory after about 60 swings. Recorded v2 decisions replay unchanged.

## What changed

| Area | Change |
|---|---|
| [LocalCognitionController](../../../../Runtime/src/OpenFantasy.Hosting/Simulation/LocalCognitionController.cs) | A decision whose preflight overflows a bound is still proposed, so the authority stops by its overflow route instead of the session failing during batch selection |
| [CognitionPolicy](../../../../Runtime/src/OpenFantasy.Core/Cognition/CognitionPolicy.cs) | v3: a gathering plan waiting for effort keeps its own reason |
| [ActorCognition](../../../../Runtime/src/OpenFantasy.Core/Cognition/ActorCognition.cs) | `Acquire` replaces uncited same-kind, same-subject observations and outcomes and refuses another actor's evidence; `Deferral`; `Adopt` records reason changes; invariants for canonical replacement and deferral |
| [CognitionIntention.cs](../../../../Runtime/src/OpenFantasy.Core/Cognition/CognitionIntention.cs) | `CurrentDeferral`; `MemoryOutcome.Reconsidered`, never with a reading hold |
| [CognitionBeliefs](../../../../Runtime/src/OpenFantasy.Core/Cognition/CognitionBeliefs.cs) | `KnownSource.LatestYield` replaces the cumulative total |
| [CognitionCodecV2](../../../../Runtime/src/OpenFantasy.Core/Cognition/CognitionCodecV2.cs) | Renamed from V1; snapshot and actor frames are version 2 and refuse version 1; actor frame carries the deferral |
| [WorldCognitionAdmission](../../../../Runtime/src/OpenFantasy.Core/Worlds/WorldCognitionAdmission.cs) | Rules pin v2; a deferral cannot postdate the world's instant |
| [Python oracle](../../../../Runtime/tests/OpenFantasy.Core.Tests/Cognition/generate_cognition_vectors.py) | Contract `cognition-v2`: 4 valid and 44 invalid vectors, including uncited duplicates, retired versions and every deferral refusal |

## Verification

| Check | Result |
|---|---|
| Python oracle | Regenerated; a second run reproduces `cognition-v2.json` byte for byte |
| [Core cognition tests](managed/core-cognition.trx) | 35 of 35 pass, including 4 new: replacement keeps cited evidence across 192 swings; evidence cited only by a memory outlives its intention; the bound is checked after replacement; reason changes are remembered once and fill the memory bound |
| [Profile-42 world, authority and session tests](managed/hosting-cognition.trx) | 20 of 20 pass, including the hosted session journey and the host's overflow route |
| Full Core suite | 2,075 of 2,075 pass in 1 m 20 s; after the review fixes, 2,076 of 2,076 in 1 m 19 s |
| Full Hosting suite, with `OPENFANTASY_DOTNET` set | 2,239 passed, 25 opt-in skipped and 25 failed in 19 m 26 s; every failure is classified [below](#full-regression) |
| Server tests and Godot client builds | No warnings or errors, before and after the review fixes |

The Hosting changes worth naming:

- **Repeated looks** (new): 129 inspections of the oak keep one observation, the first one's command is still in the
  recorded batches, and the state round-trips.
- **The episode** now records the gift's reason change as `Reconsidered / InsufficientEffort`, the finding-5 case.
- **Evidence overflow** still proves a player's gift that would exceed Wren's bound stops the boundary rather than
  being refused. Only eight gifts to Wren are lawful at genesis, and nobody can give to a player, so the at-bound root
  is admissible, not reachable: the real head with 64 received items in its final cognition field, decoded under
  complete admission. Admission checks identities, roles and counterparts, but not that each evidence identity
  derives from a real command, and 64 receipts from one giver at one instant cannot happen. The conclusion depends
  only on the entry count.
- **Durable overflow** reaches the memory bound with admissible planner claims: Wren records alternating reasons
  until 64 memories, and the next decision stops the authority, which refuses again after reopening. The law does not
  check reasons, so most of those claims are false; the host policy never records them.
- **Host overflow route** (new, review finding 1): from the same filled memory, a real session's host policy proposes
  the 65th memory. The session ends with the overflow, the authority refuses commits until reopened, and the reopened
  head is unchanged. With the fix removed the authority is not stopped and this test fails.
- **Deferral instant** (new): a root whose current deferral postdates the world's instant does not decode.

### Full regression

The 25 Hosting failures were classified before any rerun:

- **15 runner configuration, a lead omission.** Every `ProposalMigration` and `RetainedGifts` failure throws in its
  fixture constructor because `OPENFANTASY_RETAINED_PROPOSAL_SOURCE` was not set; the Runtime README names it and the
  previous checkpoint set it. Rerunning both namespaces with it set gave 24 passed, 1 opt-in skipped and none failed.
- **6 manifest-dependent result verifiers** and **1 fixture exporter**, the classes recorded at the binding and the
  host policy.
- **3 `ControllerPermissionAdapterTests` cases**, failing as at unchanged HEAD `69646306`: pre-existing and still
  undiagnosed.

So the classes and counts match the host-policy checkpoint's 10 failures. The full run is from `398e8ecc`; the review fixes change only
profile-42 paths, whose test families were rerun, so it is not repeated. Raw reports stay local and are hashed in the
[evidence manifest](EVIDENCE_MANIFEST.json). The host exposes no API call count or per-call context.

## Remaining limits

- **Decision 7's second half is not bound.** Closed episodic memory does not yet move to immutable actor-owned
  segments. Memory reaches 64 entries after one adoption and 63 reason changes, so a planner that alternates
  reasons stops the world. Under policy v3 the host's reason changes come only from tool, supply, source and stock
  events; Wren's whole episode uses eight memories. The decision-3 baseline must report memory growth per actor and
  per hour, including an effort-limited gathering workload, or record why the authored content cannot show it.
- Evidence still grows with items received, deliveries (one per quest), each source first seen and every citation.
- **Before freeze** (review findings 3 and 5): decoding must require the held intention to equal its latest adoption
  memory and the current deferral to equal the latest memory's reason, policy and instant, with oracle vectors; the
  law must check the reasons the actor's own state decides. The memory-segment design must decide whether citations
  from segments still keep evidence in the record. Optionally, a gathering completion memory cites its outcome.
- Remembered reasons are planner claims; the law does not validate them yet.
- Replacement is per actor, kind and subject. It does not consolidate across sources or summarize history.
- A replaced evidence identity leaves the root's identity index but stays reserved in the database's identity table,
  so a later command cannot reuse it.
- Unchanged from the binding: `OwnResourceChanged` is never scheduled, paid-exchange receipts are not evidence,
  proposal settlement and encounter release are untested, and there is no dialogue, presentation or baseline.

## Review

One independent critic reviewed `398e8ecc` under the owner's 2026-10-20 steering and returned "accept with required
changes". The [disposition](../evidence-retention-review/DISPOSITION.md) applies findings 1, 2, 4, 5 and 6 and
schedules finding 3 and the reason check for the freeze.

## Documentation review

- **Plan/spec:** completes the evidence-bound precondition and finding 5 named by the roadmap and handoff. The next
  work is the decision-3 baseline and the player-absent continuation. No requirement, phase exit or gate changes.
- **Reference:** `actors-and-society.md` describes the record's contents, the retention law, deferral memory and the
  remaining bounds, replacing the "evidence only accumulates" limit. `persistence-and-history.md` names the v2
  section and the unchanged decision frames.
- **Accuracy/completeness:** checked against `ActorCognition`, `CognitionBeliefs`, `CognitionCodecV2`, the rules pin,
  the oracle and the tests named above.
- **Checks:** `python3 Beta/tools/check_documentation.py --work-record Beta/implementation/p02/npc-cognition/evidence-retention/RESULT.md --files`
  with the affected pages.
