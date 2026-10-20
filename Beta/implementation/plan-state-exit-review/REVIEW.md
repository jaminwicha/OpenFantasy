# PLAN-STATE exit decision: returned independent review

The reviewer's final result, verbatim, returned by the maintained delegation adapter. It reviewed
[the decision](../PLAN_STATE_EXIT_DECISION.md) as committed at 684dc873 under [the assignment](ASSIGNMENT.md).
Line references point to that revision. The raw session stays local. Each finding is dispositioned in
[the disposition](DISPOSITION.md).

---

## Verdict: accept with required changes

The direction is sound:
- keep the complete root as the default for the first cognition episode;
- use typed stable owners as the logical model;
- make cognition an actor-keyed facet under a new closed interpretation;
- reconsider on events.

Every required change below is to the decision's wording and scope; none needs runtime work before acceptance. As written, though, the decision has four problems:
- Decision 5 and the obligation table quietly move P01/P02 offscreen gates behind triggers.
- Decision 2 misstates its own evidence.
- Decisions 3, 4 and 6 are not yet measurable or complete enough to meet the ownership contract's extension boundary.
- The table drops several remaining items.

**Short answers to your five questions:**
1. **Deferring promotion:** sound for NPC-01. The later conversion can't be avoided at reasonable cost, but per-owner framing keeps it small. The triggers are not measurable as written and miss the limits that bind first.
2. **Extension boundary:** not met yet.
3. **New interpretation per facet:** yes, with constraints.
4. **Scheduling and bounds:** not coherent with NPC-01 as written.
5. **Obligation table:** mostly honest, but six dispositions need fixing.

## Findings, by severity (all required unless marked as a suggestion)

All `:N` line references are to `Beta/implementation/PLAN_STATE_EXIT_DECISION.md` unless another file is named.

**R1 — High [Q2, Q4, Q5]. Decision 5 (:37-41) plus "Partial residency is a scale need the current game does not have" (:24) and table row :53.**
- **Problem:** this redefines offscreen as permanently resident detailed execution. That conflicts with:
  - `NPC_01_CAUSAL_EPISODE.md:123-124` ("Do not claim an offscreen proof from executing a permanently full resident world…") and its acceptance row at :169;
  - `CURRENT_ROADMAP.md:274` (NPC-01: "Compare detailed/offscreen continuation"), `:116` (P02 NPC row: "bounded offscreen reconciliation") and `:92` (P01 exit: "actor cognition/distant scheduling… meaningful workload proof");
  - the frozen `IMPLEMENTATION_PLAN.md:134` and `:138`.
- **Consequence:** the detailed/offscreen contrast passes trivially. P01/P02 gates become contingent on triggers a small world may never trip, which `PLAN_STATE_EXIT_REVIEW.md:37-40` forbids.
- **Required:**
  - Scope total residency to NPC-01's first episode.
  - Label its evidence "player-absent detailed continuation" (presentation-independent, an actual changed-place return, restart during absence), explicitly not an absent-detail proof.
  - State that non-resident reconciliation stays a P01/P02 obligation before P02 closure, independent of decision 3.
  - Name the first deliverable that exercises it (traveling merchant/NPC travelers, or ECO-01).
  - Rewrite row :53 to match.

**R2 — High [Q1]. Decision 3's triggers (:25-29).**
- **Not measurable:**
  - "its share" has no number;
  - no hardware or build is named (the only data is from the M2 development host);
  - "supported workload" and the population the p95 is taken over are undefined.
- **Lagging:** the triggers are checked on already-supported workloads, but steps B–E are a multi-step program, so a trigger only fires once the limit is already exceeded.
- **Missing limits that bind earlier:**
  - *Full-history reopen.* Current profile 40 reopened revision 252 in 733.978 ms (`p02/partition-state/RESULT.md:59-60`), at most about 2.9 ms per commit. The session runs a nominal 20 advancing steps per second (`time-and-encounters.md:26-31`). If each step is a commit and cost is linear, an hour of play means minutes of reopen, and cognition raises the per-commit replay cost.
  - *Journal envelope.* 250,000 commits / 65,536 outcomes / 128 MiB, after which the host stops (`persistence-and-history.md:391-396`). That is about 3.5 hours of play at nominal cadence, and autonomous NPC commands consume outcomes.
  - *Whole-boundary cost* including NPC reconsideration, not just publication and admission.
  - *Region headroom.* The cargo corridor already uses 14 of 16 regions (`CURRENT_ROADMAP.md:38`).
- **Certain, not contingent:** the open P01 exit (`IMPLEMENTATION_PLAN.md:134`, decision D-25's 10,000 actors) cannot fit 64 actors, so B–D are needed before P02 closure regardless. Reopen growth and the journal envelope are not fixed by B–E at all; they need checkpoint/dependency authentication (decision 7 of `PLAN_STATE_DECISION.md`) and journal segmentation.
- **Required:**
  - numeric shares of the 50 ms window, named hardware and build;
  - named reference workloads: the longest P02 same-save journey, the four-player expedition, and NPC-01 with every NPC autonomous;
  - sample every boundary and report p95, max and late steps;
  - add triggers for reopen time, projected journal consumption and cognition bytes per actor;
  - map each trigger to the workstream that fixes it;
  - check the projections when designing each P02 deliverable (ECO-01, STORY-01, merchants);
  - state that the P01 exit requires B–D in a qualification representation;
  - start LANG-01/03/04 reporting now, as the frozen plan does (`IMPLEMENTATION_PLAN.md:132`).

**R3 — High [Q1]. Decision 2's basis (:22-24) misstates the comparison.**
- **Problem:** per `owner-cost-comparison/RESULT.md:20-28`, the complete root is the most expensive path for the 199 partial commits:

  | Path | Time | Managed allocation |
  | --- | ---: | ---: |
  | Complete root | 459.5–565.0 ms | 275 MB |
  | Typed owners | 238.4–385.6 ms | 103 MB |
  | Coarse terrain-separated | 96.5–121.2 ms | 62 MB |

  The complete root wins only on reopen, final-installation allocation and file size. The fixture is legacy profile 5 (24–25 KB root). Current profile-40 roots are 80–87 KB (`p02/partition-state/RESULT.md:36-42`) and their publication cost is unmeasured.
- **Consequence:** the stated basis contradicts the metric trigger 1 uses.
- **Required:**
  - Correct the sentence and state the real basis: no 40/41 conversion is needed; the coarse candidate exists only for profile 5; B–C is costly; the current scale fits.
  - Require a per-boundary cost baseline on the current profile before NPC-01 is accepted.

**R4 — High [Q1, Q2]. Decision 4 (:30-36): ownership and dependency coverage is incomplete.**
- (a) **Accepted commitments** already belong to quest participation, the project and custodian reservations (`actors-and-society.md:86-87`). Required: cognition owns unaccepted intentions and *references* accepted commitments; promises belong to the interaction owner.
- (b) **Existing actor knowledge** — profile-33 price knowledge and disclosures (`world-markets.md:204-209`), resource observations, dialogue sessions — stays canonical and is referenced, or is absorbed only by an explicit semantic transition. No copies.
- (c) **Missing NPC-01 concepts:** motives/preferences (authored definition versus mutable state), decision/deferral records with typed reasons (`NPC_01:67-72`, `actors-and-society.md:82`), and pending reconsideration (see R6).
- (d) **Per person, not per NPC.** Player characters can act autonomously (profile 36). State that every person-actor owns the facet and that control changes never create, reset or delete it (`partition-ownership.md:41-42`).
- (e) **Admission cohort and step position.** Name the cohort for cognition transitions and where they sit in the step order (`due-work-and-scheduling.md:18-24`), so evidence publishes with the outcome that produced it.
- (f) **Per-owner framing.** The root's cognition section must be the canonical sequence of per-actor records keyed (world, branch, cognition, actor). Without this, the "storage-only conversion" claim at :36 doesn't hold.

**R5 — High [Q2, Q3]. Which cognition changes are recorded decisions and which are replayed laws is undecided.**
- **Problem:** `NPC_01:78-79` and `actors-and-society.md:173` require versioned policy and replay of recorded decisions; decisions 4 and 5 don't say which is which.
- **Required:**
  - Evidence acquisition, belief revision/staleness and any consolidation are interpretation laws.
  - Intention and plan selection enter canonical state only through admitted, recorded commands carrying the policy version.
  - Replay never runs the planner.
- **Consequence otherwise:** every planner change would need a new interpretation and a permanent historical planner, which defeats "one interpretation per facet".

**R6 — Medium-high [Q4]. Decision 5's triggers.**
- (a) "A commitment becoming feasible or invalid", if judged against world truth, wakes Wren at the remote delivery — contrary to `NPC_01:98` and the "Delayed/unknown evidence" contrast. Required: triggers only on lawfully acquired evidence, self-owned facts and the actor's own action outcomes.
- (b) Persist pending reconsideration (earliest unserviced boundary and cause), so restart keeps the wake and due age survives reopen (membership contract :79-86).
- (c) Name the timing domain (exploration advancing steps) and the behaviour during holds, encounters and paid continuations: suppressed, with a recorded reason.
- (d) Classify reconsideration as an autonomous-proposal service (`due-work-and-scheduling.md:12-16`), keeping the `SelectAdmissibleBatch` fallback that drops NPC actions, and the absent starvation bound (:51-55), explicit.
- (e) A reconsideration that changes nothing writes no commit or outcome, or is counted against the journal envelope.

**R7 — Medium-high [Q4]. Decision 6 (:42-45): bounds.**
- **Problem 1:** "Refuses the change" contradicts the envelopes it says it follows. The journal limit "cannot… turn the failure into a rejected player action" (`persistence-and-history.md:395-396`); owner-archive overflow rolls back and stops the authority (`partition-ownership.md:216-219`). Read literally, a player's delivery could fail because Wren's memory is full — both a cap and a leak of hidden state.
- **Problem 2:** with no lossless overflow path, any finite live bound eventually ends a long campaign — a de facto depth cap.
- **Problem 3:** the retention list is narrower than `actors-and-society.md:90-91`, which also protects evidence needed for relationships, mysteries and history. STORY-01's testimony depends on that.
- **Required:**
  - Overflow rolls back the whole boundary and stops the authority; it never refuses a gameplay action.
  - Adopt the reference's full retention rule.
  - Name the lossless path for NPC-01 step 3: beliefs replaced like market knowledge (old values retained in history), and closed episodic memory moved to immutable, retrievable actor-owned segments.
  - Size the bounds from a measured workload with margin, and show the composed root fits 16 MiB beside the existing sub-limits (residents 4 MiB; 64 KiB per resident record). State where cognition lives.

**R8 — Medium [Q2]. "Follows the 40→41 pattern" (:35).**
- **Problem:** the profile-41 facet could lawfully start empty. A cognition facet that starts empty asserts "observed nothing" (membership absence rules :49-68), and memories derived from history would be unlawful acquisition.
- **Required:** a copied 41→42 transition records an explicit "unrecorded before transition" boundary, derives only self-owned facts and commitment references, cannot claim earlier memories in dialogue, and refuses while states are in flight (open hold, pending gift, action mid-way).

**R9 — Medium [Q3]. Versioning: right in principle, under-specified.**
- **Why it's right:** a new closed interpretation with a *required* facet honours the membership warning (:104-106) and the "profiles are saved compositions" rule (`persistence-and-history.md:60`).
- **Required:**
  - 42 is a strict successor of 41, never a sibling (as profiles 6–9 are) and never an optional section in the existing frame; decoders for profiles ≤41 refuse cognition bytes.
  - Codec-only revisions go through storage dispatch; law changes get a new interpretation.
  - A freeze point: internal and non-default until NPC-01 is accepted. Otherwise every development iteration mints a permanent interpreter.
  - A policy for default creation and existing saves, plus NPC-01's qualification origin: a fresh 42 from the unplayed adventure genesis with the original pins (six wood, 420 XP, 200 gil, 200 work).
- **Suggestions:** bundle the dynamic-dialogue and perception bindings into the same interpretation; replace "if the list becomes unmanageable" (:68-70) with a measurable criterion.

**S1 — Low, suggestion.** Decision 1 should say that for profiles ≤41 the nested owner map remains rollout design until step B.

**S2 — Low, suggestion.**
- Name the affected pages for the `--files` check: `partition-ownership.md`, `actors-and-society.md`, `due-work-and-scheduling.md`, `persistence-and-history.md`, plus the roadmap, handoff and exit review.
- Add to "Not decided here": default creation, the 41→42 transition, checkpoint authentication, the journal envelope and non-resident reconciliation.

## Obligations judged unmet or misdisposed

1. **Row :52 (stable identities)** drops "arbitrary realms/frontier populations remain unproved" (`EXIT_REVIEW:28`).
2. **Row :54 (atomic publication and history)** drops "current-profile checkpoint/migration and partial authenticated replay remain open" (`EXIT_REVIEW:30`).
3. **Row :55 (host selection and timing):** NPC fairness, starvation and a bounded final projection stay open (`EXIT_REVIEW:31`). Reporting due age is not a bound.
4. **Row :57 (bytes, load, replay, due age):** no trigger measures cold load, so "moved to triggers" deletes it, and `ROADMAP:273` makes this diagnostic separate from LANG.
   - The honest disposition: process-cold load (fresh process, OS cache warm) was measured.
   - Still unmet: OS-cache-cold load, retained/peak residency, boundary stalls and oldest due age. I found no single-domain figure either.
   - Either keep these as PLAN-STATE residuals with a named gate, or record an explicit gate clarification.
5. **Missing rows from the original roadmap row:**
   - global journal: retained as the single SQLite journal;
   - facet manifest / facet-owned validation: evaluated, closed per-interpretation compositions chosen, generic manifest not selected — but the risk text at :68-70 currently implies it was never evaluated;
   - branch/history reads: designed, not proved.
6. **Row :53 (funded offscreen work):** general residency is a P01/P02 gate, not trigger-contingent (R1).

The table rightly does not demand B–E or a whole shipping engine for this exit.

## What NPC-01 implementation must prove first

1. Evidence comes only from committed outcomes, in the same boundary. A submitted or rejected gift writes nothing. An undisclosed remote delivery changes neither a belief nor when reconsideration wakes.
2. Cognition holds references, not copies: the guard commitment, quest participation and price knowledge stay with their owners; recapture is exact; reload creates no duplicate acceptance.
3. Replay with the planner removed or changed reproduces identical history.
4. A restart with a pending wake, mid-gather, matches the uninterrupted run, and due age survives reopen.
5. Holds, encounters and paid continuations suppress reconsideration with a reason. A bound overflow stops the authority and never refuses a player action. Decoders for profiles ≤41 refuse cognition bytes.
6. A decision-3 baseline on profiles 40/41 and 42 with every NPC autonomous: per-boundary p95/max including reconsideration, reopen time against commit count, journal use per hour and cognition bytes per actor.
7. The offscreen result is recorded only as player-absent detailed continuation.

## Uncertainty and limits

- The reopen and envelope projections assume each nominal 50 ms step is one journal commit and that cost is linear. That fits the time contract and the revision counts, but I did not check it in `LocalWorldSession`.
- I did not confirm that the 14-region corridor is the same admitted world as Wren's farm.
- I did not read `PLAN_STATE_CURRENT_PROFILE_MAP.md`, `PARTITION_PROTOTYPE_CONTRACT.md` or the NPC requirement texts.

No tool call was denied. I edited, ran and committed nothing; spend was about $3.50 of the $20.
