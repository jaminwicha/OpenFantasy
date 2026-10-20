# PLAN-STATE exit decision

**Status:** accepted with the independent review's required changes applied, under the owner's 2026-10-20
delegation to decide, have the decision reviewed by another agent and record it. The reviewer's verdict was
"accept with required changes"; every required change is applied below and dispositioned in
[the review disposition](plan-state-exit-review/DISPOSITION.md). This closes the PLAN-STATE decision and review
exit only. It does not close P01, P02, P03, any LANG gate, platform, scale or human acceptance, and it removes no
measurement obligation: residual measurements keep named gates in the obligation table.

Inputs: the [conditional recommendation](PLAN_STATE_DECISION.md), [exit reconciliation](PLAN_STATE_EXIT_REVIEW.md),
[membership contract](PLAN_STATE_PROCESS_MEMBERSHIP.md), [current-profile rollout](PLAN_STATE_CURRENT_PROFILE_ROLLOUT.md),
[ownership contract](../Runtime/docs/architecture/partition-ownership.md), the
[matched owner-cost comparison](p02/partition-state/owner-cost-comparison/RESULT.md) and the
[NPC-01 causal design](NPC_01_CAUSAL_EPISODE.md).

## Decisions

1. **Ownership model.** Typed stable owners validated against complete operation admission cohorts is the
   canonical logical ownership model for every persistent fact, as the ownership contract states. Owner keys are
   world, branch, typed role and stable subject identity, independent of place. One authoritative fact, one
   publisher: the existing SQLite authority and atomic commit. For profiles up to 41 the nested owner map remains
   rollout design until rollout step B; it is not a claim about how those profiles are stored.

2. **Saved representation for NPC-01's first episode.** The complete canonical root under the existing authority
   remains the default saved representation for current profiles and for the new cognition interpretation. The
   typed-owner directory and the terrain-separated locality image remain internal, qualified candidates; neither
   is promoted.
   - **The comparison does not favor the complete root on commit cost.** Across 199 partial commits it took
     459.5–565.0 ms and 275 MB of managed allocation, against 238.4–385.6 ms and 103 MB for typed owners and
     96.5–121.2 ms and 62 MB for the coarse candidate. It wins only on original-history reopen, final-installation
     allocation and file size. That fixture is legacy profile 5, with 24–25 KB roots; current profile-40 roots are
     80–87 KB and their publication cost is unmeasured.
   - **The actual basis:** profiles 40 and 41 need no storage conversion to admit a new interpretation; the coarse
     candidate exists only for profile 5; rollout steps B and C are a costly multi-step program; and current
     supported worlds fit their bounds.
   - **Required before NPC-01 acceptance:** a per-boundary cost baseline on the current profiles and the cognition
     profile, as decision 3 specifies.

3. **Measured triggers and certain work.** The triggers are measured, not calendar dates.
   - **Reference host and build:** the Apple M2, 8 GiB development host and the Release build on the .NET 8.0.31
     test runtime used by the existing measurements, until LANG names a minimum desktop profile.
   - **Reference workloads:** the longest retained P02 same-save journey, the four-player expedition, and NPC-01's
     episode on the cognition profile with every resident NPC autonomous. Every committed boundary is sampled and
     reported as p95, maximum and late steps.
   - **Triggers and the workstream each makes mandatory:**

     | Trigger | Threshold | Mandatory workstream |
     |---|---|---|
     | Whole boundary, including NPC reconsideration, decision, admission and publication | p95 above 12.5 ms or maximum above 25 ms, a quarter and half of the 50 ms window, or any late step | Rollout steps B–E |
     | Publication and admission alone | p95 above 5 ms, a tenth of the window | Rollout steps B–E |
     | Canonical root | above 8 MiB, half the 16 MiB cap, or any section above half its own sub-limit | Rollout steps B–E |
     | Spatial bounds | a P02 deliverable's design projects more than 16 regions or 64 residents | Rollout steps B–E before that deliverable |
     | Full-history reopen | above 2 s for a reference workload, or projected above 10 s for one hour of play at the measured commit rate | Checkpoint and dependency authentication, decision 7 of the conditional recommendation |
     | Journal envelope | projected to reach 250,000 commits, 65,536 outcomes or 128 MiB within eight hours of play at the measured rate | Journal segmentation |
     | Cognition bytes | any actor above 16 KiB in a reference workload | The lossless retention path in decision 7 |

   - **Projection check:** each P02 deliverable's design, including ECO-01, STORY-01 and the traveling merchants,
     projects these quantities before implementation. The cargo corridor already uses 14 of 16 regions, so the
     next frontier extension is expected to meet the spatial trigger.
   - **A projection that already concerns:** if each nominal 50 ms advancing step is one journal commit, the
     250,000-commit envelope is reached in about 3.5 hours of continuous play. The local session commits an
     advancing step on every tick outside a reading hold, so this is plausible; the baseline measures it.
     Neither journal segmentation nor checkpoint authentication is fixed by steps B–E.
   - **Certain, not contingent:** the P01 exit requires durable resident and visitor records and causal
     scheduling at the city workload, with a 10,000-actor first fixture. That cannot fit 64 actors, so steps B–D
     in a qualification representation are required for the P01 exit regardless of any trigger. LANG-01, 03 and
     04 reporting starts now on these workloads, as the frozen plan directs.

4. **The cognition facet.** Permanent NPC-01 cognition is a typed actor-owned facet.
   - **Who owns it:** every person-actor, players included, since a player character can act autonomously.
     Control changes never create, reset or delete it.
   - **What it owns:** acquired evidence with source command, counterpart, subject and time; the current
     unaccepted intention and the evidence it rests on; decision and deferral records with typed reasons and the
     policy version; memory of outcomes with their evidence links; and pending reconsideration, meaning the
     earliest unserviced boundary and its cause.
   - **Beliefs are derived, not copied.** An interpretation law derives each belief from held evidence and cites
     its supporting evidence. A belief can be stale; it changes only when new evidence is acquired.
   - **References, never copies.** Accepted commitments already belong to quest participation, the project and
     custodian reservations, and promises belong to the interaction that made them; cognition references them.
     Existing actor knowledge, such as profile-33 price knowledge and disclosures, resource observations and
     dialogue sessions, stays canonical with its owner and is referenced, or is absorbed only by an explicit
     semantic transition.
   - **Motives and preferences** are authored content definitions for NPC-01, not mutable state. Mutable
     preference state needs its own reviewed addition.
   - **Cohort and step position:** evidence is derived inside the step processor from each admitted action's
     committed outcome, beside the existing material-evidence capture, and joins that action's admission cohort.
     It publishes in the same boundary as the outcome that produced it. A recorded decision is its own actor
     action in the ordinary canonical action order.
   - **Per-owner framing:** the root's cognition section is the canonical sequence of per-actor records keyed by
     world, branch, role `cognition` and actor. A later storage-only conversion can therefore move each record to
     a typed owner image without a semantic change.

5. **Laws and recorded decisions.** Evidence acquisition, belief derivation and staleness, and any consolidation
   are interpretation laws, replayed as part of the profile. Intention and plan selection enter canonical state
   only through admitted, recorded commands that carry the policy version. Replay applies the recorded decision
   and never runs the planner, so a planner change needs no new interpretation.

6. **Reconsideration and residency.**
   - **Wakes:** only lawfully acquired evidence, self-owned facts and the actor's own action outcomes wake
     reconsideration, plus a declared fallback cadence. World truth never wakes an actor, so an undisclosed remote
     delivery changes neither a belief nor a wake.
   - **Timing domain:** exploration advancing steps. Pending reconsideration persists with its cause, so a restart
     keeps the wake and due age survives reopen.
   - **Suppression:** reading holds, encounters and paid continuations suppress reconsideration. The pending wake
     keeps its cause and the suppression reason; no extra commit is written for it.
   - **Service class:** reconsideration is an autonomous-proposal service. The existing batch selection fallback
     that drops NPC actions stays, and the missing starvation bound remains an explicit open requirement;
     reporting due age is not a bound.
   - **No empty commits:** a reconsideration that changes nothing writes no commit or outcome.
   - **Residency:** every resident stays resident for NPC-01's first episode only. Its offscreen evidence is
     labelled *player-absent detailed continuation*: independent of presentation, with an actual changed-place
     return and a restart during absence. It is not an absent-detail proof. Non-resident reconciliation stays a
     P01 and P02 obligation before P02 closure, independent of decision 3, and is first exercised by the traveling
     merchant deliverable.

7. **Finite bounds and retention.**
   - **Overflow stops the authority.** Exceeding a cognition bound rolls back the whole boundary and stops the
     authority, as owner-archive overflow does. It never rejects a gameplay action and never reveals another
     actor's state.
   - **Retention** follows the actor reference rule in full: summaries and retention preserve the causal
     obligations and evidence needed for relationships, mysteries and historical reconstruction, unresolved
     commitments and promises, private provenance, and the difference between an attempt and its committed result.
   - **Lossless path, required by NPC-01 step 3:** beliefs are replaced like market knowledge, with old values
     retained in history, and closed episodic memory moves to immutable, retrievable actor-owned segments. Until
     then the development bounds of 64 evidence entries and 64 memories per actor hold, sized for the episode.
     They are re-sized from the measured workload with margin, and they are not limits on character depth.
   - **Composition fit:** an actor record is bounded at 48 KiB, below the 64 KiB per-resident record bound, and
     the facet's worst case is about 3.1 MiB for 64 actors. It is its own root section beside the 4 MiB residents
     sub-limit, and the baseline shows the composed root fits the 16 MiB cap.

8. **The cognition interpretation, profile 42.**
   - **Strict successor:** profile 42 succeeds 41; it is never a sibling, as profiles 6–9 are, and never an
     optional section in an existing frame. Decoders for profiles up to 41 refuse cognition bytes.
   - **Versioning:** a codec-only revision goes through storage dispatch; a law change needs a new interpretation.
   - **Freeze point:** profile 42 stays internal and non-default until NPC-01 is accepted. Saves made with it
     before then are development fixtures with no compatibility promise, so iteration does not mint permanent
     interpreters. It freezes at acceptance, together with the perception and dynamic-dialogue bindings it needs.
   - **Creation and existing saves:** content keeps creating profile 40 by default until acceptance, and existing
     saves stay on their profiles. NPC-01 qualifies from a fresh profile 42 created from the unplayed adventure
     genesis with the original pins: six wood, 420 XP, 200 gil and 200 work.
   - **A later copied transition from 41** records an explicit "unrecorded before transition" boundary, derives
     only self-owned facts and commitment references, cannot claim earlier memories in dialogue, and refuses while
     anything is in flight, such as an open hold, a pending gift or an action midway. An empty facet asserts that
     nothing was acquired since genesis, which is lawful only from an unplayed genesis.
   - **Interpreter growth:** the generic facet manifest was evaluated in PLAN-STATE and not selected. It is
     reconsidered if one new interpretation costs more file changes than profile 41 did, about 25 across Core and
     Hosting, or if more than three interpretations are admitted in one phase.

## What NPC-01 must prove first

1. Evidence comes only from committed outcomes, in the same boundary. A submitted or rejected gift writes nothing.
   An undisclosed remote delivery changes neither a belief nor a wake.
2. Cognition holds references, not copies: the guard commitment, quest participation and price knowledge stay with
   their owners, recapture is exact and reload creates no duplicate acceptance.
3. Replay with the planner removed or changed reproduces identical history.
4. A restart with a pending wake, midway through gathering, matches the uninterrupted run, and due age survives
   reopen.
5. Holds, encounters and paid continuations suppress reconsideration with a reason. A bound overflow stops the
   authority and never refuses a player action. Decoders for profiles up to 41 refuse cognition bytes.
6. The decision-3 baseline on profiles 40, 41 and 42 with every NPC autonomous: per-boundary p95 and maximum
   including reconsideration, reopen time against commit count, journal use per hour and cognition bytes per actor.
7. The offscreen result is recorded only as player-absent detailed continuation.

## Obligation disposition

| Original PLAN-STATE obligation | Disposition |
|---|---|
| Compare ownership alternatives | Done: three matched candidates with identical receipts and worlds; decision 2 states what the comparison actually shows |
| Stable identities across spatial crossing and real custody transfer | Done within the legacy episode; decision 1 makes the keys canonical. Arbitrary realms and frontier populations remain unproved |
| Funded offscreen work with absent detail, return and intervention | Done within its scope. Non-resident reconciliation is a P01 and P02 gate before P02 closure, not contingent on decision 3's triggers; decision 6 names its first deliverable |
| Atomic coupled publication and original history | Done for named profiles; decision 1 keeps the single-writer rule. Current-profile checkpoint and migration and partial authenticated replay remain open |
| Real host selection and earned timing | Done within scope; decision 6 sets NPC-01's wakes and timing domain. NPC fairness, the starvation bound and a bounded final projection remain open |
| Actual process discovery | Done within scope; membership and absence remain required before partial loading |
| Bytes, allocations, load, commit, replay, oldest due age | Warm measurements and a first authority open in a fresh test process are done; whole-process startup is not. OS-cache-cold load, retained and peak residency, boundary stalls and oldest due age remain PLAN-STATE residuals, gated before rollout step D and before P02 closure; decision 3 adds measured triggers but does not replace these |
| Legal composition, historical versions and preserved-source rollout | Designed; decision 8 adds cognition only as a strict successor interpretation, never by altering old ones |
| Global journal | Retained as the single SQLite journal; decision 3 adds the envelope projection and segmentation trigger |
| Facet manifest and facet-owned validation | Evaluated; closed per-interpretation compositions were chosen and the generic manifest was not selected; decision 8 states when to reconsider it |
| Branch and history reads | Designed, not proved |
| Independent architectural decision review | Done: [assignment](plan-state-exit-review/ASSIGNMENT.md), [returned review](plan-state-exit-review/REVIEW.md), [dispatch record](plan-state-exit-review/DISPATCH.json) and [disposition](plan-state-exit-review/DISPOSITION.md) |

## Risks accepted

- Deferring partial residency makes a later storage conversion larger. Mitigation: decision 4's per-owner framing
  keeps the cognition facet convertible without semantic change, decision 3 makes steps B–D certain for the P01
  exit, and its triggers make them mandatory earlier if a workload needs them.
- Complete-root publication grows with every facet. Mitigation: decision 3's root and boundary triggers are
  reported on named workloads, and no facet may raise the root cap to pass a test.
- The journal envelope may already be within a few hours of continuous play. Mitigation: the baseline measures it
  before NPC-01 acceptance and the segmentation trigger makes the fix mandatory.

## Not decided here

Partial loading; due-age fairness across domains and the starvation bound; cold, platform and scale qualification;
current-profile storage conversion and migration; typed-owner promotion; default creation of profile 42 after
acceptance; the implementation of a copied 41→42 transition; checkpoint and dependency authentication; journal
segmentation; non-resident reconciliation; and every P02 closure row. Each keeps its gate.

## Documentation review

- **Plan/spec:** records the PLAN-STATE exit decision the roadmap requires before permanent NPC persistence, with
  the independent review's required changes. No requirement, phase exit or gate is removed. Residual measurements
  keep named gates, and decision 3 states the P01 work that is certain.
- **Reference:** the ownership contract's extension boundary now cites decisions 4, 5, 7 and 8 for persistent
  character depth. The actor, scheduling and persistence references already state the rules these decisions apply.
- **Accuracy/completeness:** figures were re-read from the sources the review cited: the matched comparison's
  partial-commit rows, root sizes and the 733.978 ms reopen in the partition-state result, the journal envelope in
  the persistence reference, the 16 MiB, 4 MiB and 64 KiB bounds in the root and resident codecs, the 14-region
  corridor in the roadmap, and the P01 exit in the frozen plan. The tick commit path was read in the local session.
- **Checks:** `python3 Beta/tools/check_documentation.py --work-record Beta/implementation/PLAN_STATE_EXIT_DECISION.md --files`
  with the ownership, actor, scheduling and persistence references, the roadmap, handoff and exit review.
