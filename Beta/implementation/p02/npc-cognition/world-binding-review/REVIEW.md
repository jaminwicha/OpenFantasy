# Critic report: NPC-01 profile-42 world binding

This was a read-only review on Opus 5.5 (1M context): no commands run, no edits, no other model or subagent. It cost about $6–7 of the $20 limit, read from the session's budget meter, so it's an estimate, not an invoice. I made about 55 tool calls. The host shows neither a clock nor per-call context, so I can't report elapsed time.

## Verdict: accept with required changes

The foundations hold:
- **Atomicity:** evidence is derived only after the whole candidate is admitted, rolls back with its action and is published in the same root.
- **Overflow:** a capacity overflow can't turn into a refused action. `Acquire` and `Adopt` run outside every broad `catch`. Both Hosting commit paths, and `Preview`, stop the authority.
- **Replay:** nothing in Core references `CognitionPolicy`. Only `CognitionPolicy.cs` itself and Hosting's `LocalCognitionController.cs` do, so replay never runs the planner.
- **Strict successor:** profile 42 is 41's fields plus one final cognition field. Every profile list I checked that includes 41 also includes 42.

**Before acceptance:** findings 2, 3 and 4 (code and tests), plus the record changes in 1 and 6. **Before the freeze point:** findings 5 and 7.

## Findings, most severe first

**1. Medium-high: ordinary play hits the evidence bound, and the record doesn't say so.**
- **Where:** `WorldCognitionLaw.cs:62-71`, `ActorCognition.cs:74-75`, `SqliteWorldAuthority.cs:302-307`, and the record's "Known gaps".
- **Problem:** every actor, players included, gains evidence and nothing ever replaces it. A gather adds two entries; a look, a received gift or a delivery adds one.
- **Consequence:** after about 32 gathers by one actor, a profile-42 world stops. Batches are deterministic, so that action stops the world again on every retry until the code changes. This blocks proof 6 (the baseline) on profile 42 and a sustained proof 7 (player-absent continuation).
- **Required:**
  - State the limit in Known gaps and in the actors reference.
  - Make one of these a precondition for proofs 6 and 7: decision 7's lossless replacement (a newer observation of a source replaces an older one that nothing cites, with the old value kept in history), or bounds re-sized from measuring those workloads.

**2. Medium: a recorded decision can fabricate state its evidence doesn't support.**
- **Where:** `WorldCognitionLaw.cs:139-163`, `ActorCognition.cs:107-131`, `CognitionIntention.cs:44-45`, `WorldAdmissionValidator.cs:162-164`, `WorldCognitionAdmission.cs:66`.
- **Problem:** admission only checks that the cited evidence is held, that the subject's role exists, and that a `WorkFundedProject` intention cites something. All of these are admitted:
  - `WorkFundedProject` citing a source observation, or naming a project the actor doesn't build.
  - `GatherPersonalStock` citing a gift.
  - An `AdoptedAt` anywhere in the past, or up to 50 ms in the future.
  - `CompletedCurrent = true` when nothing completed, which writes a "Completed" memory.
  - A `NextWake` anywhere from the next step (thrashing) to `ulong.MaxValue`, which disables decision 6's declared fallback.
- **Consequence:** memory is what future dialogue explains from, so it would "remember" false causes and completions.
- **Required:** add these law checks inside `ResolveDecision`, each with a refusal test:
  - (a) a new intention's `AdoptedAt` equals `state.Calendar`;
  - (b) `WorkFundedProject` requires the actor to be the project's builder and a cited `QuestDeliveryReceived` for that project's quest;
  - (c) `GatherPersonalStock` requires a cited `SourceObserved` of its subject;
  - (d) `AwaitSupply` names the actor's own project;
  - (e) accept `CompletedCurrent` only when the project really is complete, or derive "Completed" in law and drop the field;
  - (f) bound `NextWake` to at most the declared cadence after the current step, and add that cadence to `RulesPin`.
- Also state that deferral reasons remain unvalidated planner claims.

**3. Medium: a decision is serviced under a hold opened earlier in the same batch.**
- **Where:** `WorldStepProcessor.cs:530`, compared with `:541` and `:549`.
- **Problem:** inspection and gathering check the batch's running hold; `ResolveDecision` doesn't. Actions run in actor-ID order (`WorldStepBatch.cs:240`). So if a lower-ID actor's speech opens a hold, a later due decision in the same advancing step is admitted while held.
- **Consequence:** this contradicts decision 6 and the record's own "a hold already suppresses service".
- **Required:** pass the running hold into `ResolveDecision`, refuse with `Unavailable`, and add a same-batch test. I couldn't confirm whether a care obligation can open partway through a batch.

**4. Medium-low: inspection refusals leak remote state.**
- **Where:** `WorldCognitionLaw.cs:126-131`.
- **Problem:** a missing node returns `Unavailable` and a stale revision returns `RevisionConflict`, both before the sightline test. Receipts go back to whoever submitted.
- **Consequence:** a free look at any node ID reveals whether the node exists and whether someone has harvested it since a known revision. That leaks other actors' activity, which NPC_01 forbids. The ordering was copied from `GatherPlanner.cs:56-70`, which is a separate, pre-existing issue.
- **Required:** test sightline before the node's revision, return the same `OutOfReach` for absent and out-of-sight nodes, and test both cases.

**5. Medium-low (open question 1): a deferral with an unchanged intention leaves no record.**
- **Where:** `ActorCognition.cs:119-120`.
- **Problem:** the journal keeps the command bytes, but decision 4 makes deferral records with typed reasons part of the facet.
- **Consequence:** the actor's own state can't explain the change from "missing tool" to "insufficient effort", or even its current reason.
- **Required (it can land with the host-policy packet):**
  - Keep the current deferral (reason, policy version, since when) beside the intention.
  - When the reason changes, turn the old one into one memory entry. Memory then grows with reason changes, not with every wake.

**6. Low: the acceptance table claims more than the tests show.**
- **"Consented transfers":** unreachable on profile 42, because paired material only exists in profile 28 (`WorldStepBatch.cs:83-87`).
- **Proposal settlement:** reachable, and the recipient is correct (`WorldMaterialProposalResolver.cs:131`), but untested.
- **Row 1:** the undisclosed change tested is a harvest, not the remote delivery the decision names.
- **Row 3:** the "changed planner" is only a renamed version string. Replay independence is guaranteed by the code's structure, and the record should say so.
- **Row 5:** encounter release is untested. The hold and encounter tests show the fixture declining to propose (`CognitionWorldTests.cs:206,240`), not admission refusing.
- **Untested:** memory overflow, rollback after an action resolved, the occupied-identity refusal, and refusal of a delegated decision. `CognitionCommandCodecTests.cs:40` is named for the delegated envelope but passes no delegation witness.
- **Unstated gap:** `OwnResourceChanged` is never scheduled (`WorldCognitionLaw.cs:104` only schedules `ActionFinished`), so recovered effort waits for the 100-step fallback.
- **Required:** narrow the wording, or add tests for encounter release, proposal settlement, finding 3 and the refusals in findings 2 and 4.

**7. Low, before freeze: profile 42's rules pin is never stored or checked.**
- **Where:** `WorldCognitionAdmission.cs:22-37`; compare `WorldSnapshotCodecV35.cs:150-158`.
- **Problem:** `RulesPin` only feeds the genesis admission hash. Unlike the recovery and care-disposition pins, it isn't a root field checked on decode.
- **Consequence:** a law change without a profile bump is only caught by comparing against a supplied genesis.
- **Required:** store the pin as an exact root field and check it on decode. Codec changes are free before freeze.

**8. Note on the shared checkout.**
- HEAD `c9846097` already contains this binding.
- The checkout also has uncommitted `Hosting/Simulation/LocalCognitionController.cs`, which references `WorldCognitionPerception` (line 107). No such type exists in the sources I read, so the checkout may not currently build.
- I didn't review that file. The record's "no host policy" holds only for what's committed.

## The six questions

1. **Atomicity and authority.** I found no path that gives evidence for a refused action, skips it for a committed one, or turns an overflow into a refusal. The care, encounter, consequence and controller processors admit no evidence-producing actions.
   - The one latent risk: `CognitionCapacityException` is a subclass of `InvalidOperationException`, which `WorldStepProcessor.cs:256` and `WorldCareProcessor.cs:66` catch. It's safe today only because of where `Acquire` and `Adopt` sit.
2. **Knowledge and privacy.** Gifts and deliveries are lawfully in person: gifts require reach (`WorldActionResolver.cs:54`), deliveries reach plus an open dialogue (`DeliveryQuestPlanner.cs:56-63`).
   - The four-cell rule is a defensible, conservative first rule. It applies only on the same floor, so any height difference blocks sight.
   - Finding 4 is the only leak I found.
3. **Recorded decisions and replay.** Replay is planner-independent. Admission refuses stale, undue, delegated and no-op decisions, but see finding 2 on fabrication.
4. **Wakes and suppression.**
   - The post-pass reads the boundary's end state, which is correct for every gameplay batch kind, including a hold opened and closed inside one boundary.
   - Skipping it on controller-only boundaries is correct, because those successors keep dialogues, care, pending work and pacing. I'm assuming `EncounterLifecycle.RetainBoundary` keeps the phase; I didn't check.
   - Dropping the candidate frame is correct and changes no sizes.
   - Treating own-project work as a self-owned fact satisfies decision 6: only the builder can work (`ConstructionWorkQualification.cs:43`), so completion is both a self-owned fact and the builder's own outcome.
5. **Versioning and storage.**
   - Strict succession holds: 32 fields against 41's 31, and decoders up to 41 refuse cognition bytes (version check, field count and canonical re-encoding).
   - Schema twelve is wired at `SqliteWorldAuthority.cs:70` and `WorldJournalRecovery.cs:127,260`.
   - Reserving the cognition section at its full maximum is correct, and it's what keeps cognition growth from ever refusing an action through the size check. I'm assuming frame length prefixes are fixed-width; I didn't check.
6. **Decision 8's trigger.** It fired, so record the reconsideration as its own reviewed decision before profile 43.
   - I'd use a per-profile capability table (one switch listing every known profile, throwing for unknown ones) rather than numeric ranges. Ranges are unsafe here: profiles 6–9 are siblings, and 29 and 31 don't exist.
   - Gate it on byte-identical round trips of every retained fixture.
   - Run it as its own mechanical packet, separate from the host policy.

## Suggestions (not required)

- Stop `CognitionCapacityException` deriving from `InvalidOperationException`, or exclude it from Core's broad catches.
- Coarsen `SourceObserved` to depleted versus available. Beliefs only test for zero (`CognitionBeliefs.cs:47`).
- Move the subject and role checks into `ResolveDecision`. Today they depend on `FitsCompleteCandidate`, whose full check is gated by `ReservoirSeals` (`WorldStepProcessor.cs:217`); that always applies on profile 42, but only by accident.
- Add two tests: a durable reopen after the first gather that commits the rest and compares hashes, and a replay of a lawful decision v1 would never make.
- Decide how player-controlled actors' wakes are serviced. Nothing services them today, so they stay due forever and will skew the baseline's due-age figures. Also consider a fallback wake at genesis.
- In the baseline, measure re-encoding the whole root, including a cognition section of up to about 3 MiB, for every action and movement candidate.
- Before freeze, retire the never-produced `ProjectWorked` kind. Also note that any change to the resident set must update cognition records in the same transaction (`WorldCognitionAdmission.cs:58`).

**Not verified:** the 45-file count (no git), all test results and the 16 failure classifications (no commands run), the `RetainBoundary` phase, and fixed-width length prefixes.
