# Critic report: NPC-01 evidence retention and remembered deferrals (profile 42)

This was a read-only review with Read, Glob and Grep: no commands, edits, other models or subagents, and no permission was denied.

## Verdict: accept with required changes

The core of the change holds:
- **Replacement is correct.** `Acquire` drops an uncited older entry of the same kind and subject. It checks the 64-entry bound after that replacement and before building the new state (`ActorCognition.cs:96-101`).
- **Lawful play never breaks the new decoding rule.** The set of cited evidence only grows: memory is append-only, and each new intention's basis is copied into its `Adopted` memory (`:163-165`). So the "at most one uncited entry" rule refuses no state that lawful play produces.
- **Replay is deterministic.** Replaceable evidence only comes from the actor's own action, one per batch (`WorldStepBatch.cs:187`), and each advancing step adds 50 ms (`WorldStepProcessor.cs:232`). Two such entries for one actor therefore never share an instant.
- **No catch turns an overflow into a refusal** (`WorldCognitionLaw.cs:156-162`, `WorldCommitPreparation.cs:31,56`).
- **The test counts are real.** 34/34 Core and 18/18 Hosting match the committed TRX counters.

**Before acceptance:** findings 1 and 2, plus the record changes in 4, 5 and 6. **Before freeze:** finding 3 and the law check in finding 5.

## Findings, most severe first

**1. Medium-low: under the host policy, a memory overflow never reaches the authority, and that path is untested.**
- **Where:**
  - `LocalCognitionController.cs:86` calls `cognition.Adopt(...)` as a preflight.
  - It runs inside `SelectBatch` (`LocalWorldSession.cs:843`, via `LocalNpcController.cs:111-112`), which `SelectAdmissibleBatch` calls outside its `try` (`:769`).
  - The run loop's `catch (Exception) { Fail(error); throw; }` (`:378`) catches it. Compare `SqliteWorldAuthority.cs:215-218` and `:302-306`.
- **Problem:** when a host decision would add the 65th memory, `Adopt` throws `CognitionCapacityException` during batch selection.
  - No batch reaches `Preview` or `Commit`, so the authority's `_requiresRecovery` flag is never set. The session fails through its generic error path instead.
  - The durable test (`CognitionAuthorityTests.cs:44-75`) submits fixture-built batches straight to the authority, so it never exercises this path.
- **Consequence:** three statements are true only for batches submitted directly:
  - the record's "the next decision stops the authority";
  - its "closes the review's untested world-level memory overflow";
  - the reference's "`SqliteWorldAuthority` marks itself as requiring reopening" (`actors-and-society.md:120-123`).

  The next packet runs the live host, so its first memory overflow will surface as a host-policy exception and is easy to misclassify. Nothing is refused or published, so decision 7 still holds in effect.
- **Required:** in `Decide`, catch `CognitionCapacityException` from the preflight and still return the decision. `Preview` then stops the authority by the documented route. Add a session-level test with Wren seeded at 63 memories, proving the session stops and nothing is published. Alternatively, document this path in the record and the reference.

**2. Medium-low: sustained gathering now hits the memory bound at roughly the point decision 3 cites for the evidence bound, and the planned baseline won't show it.**
- **Where:** `CognitionPolicy.cs:49-70`, `ActorCognition.cs:147-158`, `GatherRulesV1.cs:6-8`, and `WorldCognitionLaw.cs:104` (no `OwnResourceChanged` wake). Also the record's decision 3, player outcome and remaining limits.
- **Problem:** with gathering limited by effort, two swings take effort from 1000 to 600 to 200.
  - At the evidence wake the reason becomes `InsufficientEffort`. At the 100-step fallback it changes back to `AwaitingSupply`.
  - That is two `Reconsidered` memories per two swings. One gathering intention lasting about 60 swings (about 3,100 steps, roughly 2.6 minutes) fills memory. That matches decision 3's "about 60 gathers".
  - The authored stock target of 2 ends the intention after two swings, and a depleted source can end it sooner. Wren's episode uses only 8 memories (`CognitionWorldTests.cs:140-150`).
- **Consequence:** "ordinary play no longer approaches that bound" is true for evidence only. For a sustained autonomous gatherer the world-stop moves from about 32 swings to about 60; it isn't removed.
  - The decision-3 baseline on the authored content can't reveal this.
  - "A few times per gathering cycle" is vague. It is also wrong for the authored cycle, which has no reason change mid-cycle.
- **Required:** state the rate and the horizon in the record, the reference's "Remaining bounds" and the handoff's baseline note. Have the baseline also report memory growth under an effort-limited workload, or record why it can't.
- **Alternative (planner-only, new policy version, no law change):** keep the reason at `AwaitingSupply` while a gathering plan's next swing merely waits for effort. Spent effort doesn't defer the preferred work, and this removes the main source of reason changes.

**3. Low, before freeze: decoding admits states lawful play can't produce, and one of them can wedge an actor.**
- **Where:** `ActorCognition.cs:54-66`.
- **Problem:** the constructor accepts three such states:
  - (a) an intention whose basis no memory cites;
  - (b) a `CurrentDeferral` that disagrees with the latest memory's reason, policy and instant;
  - (c) an uncited entry older than a cited entry about the same subject.

  Case (a) is harmful. Suppose that intention is superseded by one that cites nothing. Its old basis then becomes uncited next to a newer uncited entry of the same kind and subject, which breaks the one-uncited rule. `Adopt` throws `ArgumentException`, so `ResolveDecision` refuses every such decision (`WorldCognitionLaw.cs:159-161`), and the host preflight throws.
- **Consequence:** the record's "a decoding invariant as well as a transition" holds only in the narrow sense, and spliced roots like the test fixture's are admitted. Decision 7's planned memory segments will also move citations out of the record. Once they do, lawful roots would break this rule.
- **Required:**
  - Add two decoding rules, each with an oracle vector:
    - the held intention equals its latest `Adopted` memory (kind, subject, `At` equal to `AdoptedAt`, and basis);
    - a non-null `CurrentDeferral` equals the latest memory's reason, policy and instant.
  - Record that the segment design must decide whether citations from segments still keep evidence in the record.

**4. Low: what justified a completion is not kept, though the record implies cited provenance is.**
- **Where:** `ActorCognition.cs:160-162`, `CognitionIntention.cs:108-109`, `WorldCognitionLaw.cs:198-200`, and the record's decision 2.
- **Problem:** the law admits a gathering completion only while a `GatherOutcome` acquired since adoption is held. Nothing cites that outcome, so the next gather replaces it. After that, canonical state can't show what justified the completion; only the journal can.
- **Consequence:** decision 7's lossless path allows this. But the record speaks only of adoptions, and decision 7 names private provenance.
- **Required:** say so in decision 2 and in the reference. Optionally, before freeze, let a gathering `Completed` memory cite the outcome the law checked.

**5. Low: remembered reasons are unvalidated, and the durable test records false ones.**
- **Where:** `WorldCognitionLaw.cs:133-134` and `:147-152`, `CognitionWorldFixture.cs:159-164`, `CognitionAuthorityTests.cs:50-58`.
- **Problem:** each reason change now becomes a persistent `Reconsidered` memory and `CurrentDeferral`, but the law doesn't check the reason.
  - The test alternates `MissingTool` and `InsufficientEffort` after about 100 steps of effort recovery.
  - Most of its roughly 32 `InsufficientEffort` entries are therefore probably false. I couldn't verify Wren's starting effort.
  - The record calls this "lawfully".
- **Consequence:** memory, which dialogue will explain from, can hold invented causes, and a planner whose reasons flip back and forth can stop the world.
- **Required:**
  - State in the record that remembered reasons are planner claims (the reference already says so at `:113`).
  - Reword "lawfully" as "admissible planner claims; the host policy never records these".
  - Before freeze, have the law check the reasons the actor's own state decides: `InsufficientEffort`, `MissingTool`, `NoKnownSource`, `SourceDepleted` and `AwaitingSupply`.

**6. Low: record wording and test gaps.**
- **The at-bound root.** The fixture says complete admission "checks every identity and counterpart" (`CognitionWorldFixture.cs:129-134`). `WorldAdmissionValidator.cs:143-166` does less:
  - it doesn't check that an evidence identity derives from its source command, or that the command ever existed;
  - the synthetic root holds 64 items received from one giver in one instant, which one action per actor per batch makes impossible.

  The overflow conclusion still holds, because it depends only on the entry count. Required: describe the root as "admissible, not reachable".
- **"Only eight gifts are lawful at genesis."** True for gifts to Wren. Two players passing one item back and forth seem to be lawful without limit (unverified beyond the reach check). About 127 gifts would put one player at the bound. That is both a cheap lawful fixture and a co-op hazard the remaining limits should name.
- **"Journal command bytes keep their meaning."** The bytes decode to the same decision, but what they do is set by the root's v2 law pin. Reword.
- **Untested refusal.** The new `Deferral.Since > world.Calendar` check (`WorldCognitionAdmission.cs:71`) is listed as a change but has no test. Add one.
- **Growth list.** Deliveries (one per quest) are missing from the remaining limits and from the reference at `:127-129`. Add them.

## The six questions

1. **Losslessness and provenance.**
   - **Kept:** adoption provenance, causal obligations and the difference between an attempt and its result. Received items and deliveries are never replaced.
   - **Completion check unaffected:** a zero yield can't happen (`GatherPlanner.cs:64,92`), so the latest outcome is always positive and the "yielded since adoption" check is preserved.
   - **Not kept:** completion provenance (finding 4).
   - **Honest equivalent:** "the journal and archived roots keep it" matches the market precedent (`world-markets.md:206-209`): old values come back by replaying history under a pinned law.
   - **Identities:** the database keeps each replaced evidence identity itself, not just its command (`WorldStepProcessor.cs:628-631`, `SqliteWorldAuthority.cs:298`), so the record's caveat understates the protection.
   - **Decision 3:** justified, since nothing reads the cumulative total. Record that dynamic dialogue would have to replay history to recover lifetime totals.
2. **Citation and canonical form.** "Nothing cites it" is the right test for this increment. Nothing can become uncited through lawful play today, though planned memory segments would change that; see finding 3.
3. **Deferral memory.** It satisfies review finding 5 without unbounded growth.
   - Recording the new reason at the instant of change matches how the other memory kinds record reasons, and the chain of memories reconstructs each period's reason, start and policy.
   - Keeping the original policy and instant when only the policy version changes is right: `Since` means when the reason began.
   - Invented reasons and back-and-forth reason changes are findings 5 and 2.
4. **Bounds and authority.** The bound is checked at the right point, and nothing converts an overflow into a refusal. The live path is finding 1, the memory risk finding 2, and the missing deliveries finding 6.
5. **Versioning.** A sound pre-freeze iteration under decision 8.
   - Nothing in src, tests, tools or content pins `cognition.snapshot.v1`, the v1 law pin or `CognitionCodecV1`.
   - Only two immutable manifests name the deleted `cognition-v1.json`: `core-contracts/EVIDENCE_MANIFEST.json:21` and `host-policy/EVIDENCE_MANIFEST.json:54`. No test reads them.
   - By inspection, decision frames are unchanged (`CognitionCodecV2.cs:196-232`), but no oracle vector pins their bytes.
6. **Tests and evidence.** The tests prove the stated Core and world claims. Gaps are in findings 1 and 6. One more: no test covers an observation cited only by a memory after its intention ends, so the citation check could quietly regress to "cited by the intention" with every test still passing.

## Suggestions (not required)

- Add a decision-frame vector to the Python oracle.
- Add tests for evidence cited only by memory, and for a cited observation when the bound is already reached.
- Build the at-bound fixture from a lawful two-player exchange.
- Correct the "empty-handed attempt" comment in `CognitionTests.cs:85-91`: a zero-yield gather can't happen, so the depletion branch it exercises (`CognitionBeliefs.cs:52-53`) is unreachable.
- These two predate this increment:
  - The host never decides again once a builder's project completes (`LocalCognitionController.cs:41,73-75`). So `WorkFundedProject` never gets a live `Completed` memory, and that `ActionFinished` wake is never serviced.
  - When the intention is unchanged, `Adopt` ignores the recorded `CompletedCurrent` flag and basis (`ActorCognition.cs:141,147`).

## Not verified

- The full-suite failure classification (the raw reports stay local).
- Whether players can repeatedly gift items to each other.
- Wren's starting effort.
- How a failed session completes a player's pending request.
- Whether profile 42 has archived roots beyond journal replay.

**Cost:** about $5–6 of the $15 limit, read from the session's budget meter, so it's an estimate, not an invoice. I made about 50 tool calls. The host shows no clock, so I can't report elapsed time.