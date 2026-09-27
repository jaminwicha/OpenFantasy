Review complete. Verdict first: I found no correctness defect in the six runtime files. The arithmetic, revision continuity, ordering, envelope and exact-basis decoding all hold under the counterexamples I tried. The findings below are test gaps, one doc-precision issue, and seam caveats the integration must honor.

## Findings

**P2, test gap, `ResourceHistoryBoundaryTests.cs:66-75`.** The evidence-aliases-a-rule path is never exercised. For identity 300 the second assertion builds a cause with evidence 300 and definition 300, which throws inside the record constructor at `ResourceChangeCause.cs:11`, so the check at `ActorResourceHistory.cs:99` is untested. Counterexample: change A cites (evidence 301, rule 300), change B cites (evidence 300, rule 302). Line 99 should refuse it. Fix: add a two-cause case where the evidence equals another change's rule id.

**P2, test gap, level refill and zero XP.** Only the chain test builds a level-up, and nothing proves the constructor at `ActorResourceChange.cs:15-16` refuses a stale refill. Counterexample: before level 1 with XP 25, reward 75 yields level 2, XP 0, HP 69, MP 12. A fabricated after with level 2, XP 0, HP 13, MP 9 and revision plus one must throw. Similarly, the doc claims zero-XP rewards are recorded, but no test constructs a reward of 0 and appends it. Fix: add both cases, plus a tamper variant on the after-level field.

**P3, exception types for direct callers.** `ActorResourceChange` and `ActorResourceHistory` propagate `InvalidOperationException` when a MagicExpenditure record's before actor cannot pay (`ActorProgressionState.cs:61`) and `OverflowException` for amounts above the 32-bit range or XP overflow. Every other refusal is `ArgumentException`. The codec maps all three at `ActorResourceHistoryCodecV1.cs:56`, but an integration calling `Append` directly must catch all three. Only the revision-overflow case is tested.

**P3, doc precision, `actor-resources.md:92-93`.** "Repeated passive effects need distinct process-occurrence identities" reads as enforced. The component enforces only command-id uniqueness at `ActorResourceHistory.cs:95` and never checks evidence uniqueness. The multi-command test reuses evidence 301 across two commands and it is accepted. Reword to state evidence uniqueness is a world obligation.

**P3, undocumented convention, `ActorResourceHistory.cs:92`.** A batch at the basis world sequence is refused, so a segment boundary can only be taken at the end of a step, and rollover mid-step is impossible. Untested and not stated in the doc. Add a refusal test for sequence equal to basis and one sentence in the reference.

## Seam validation against real writers

- **Attack resolution.** `WorldEncounterActionProcessor.cs:137-163` bumps progression once for target damage plus caster MP, matching the one-revision-per-batch rule. `EncounterActionResolution` has no identity of its own, so the decision command id is the only stable command identity. Using it as the batch command with the resolution sequence satisfies strict ordering. The component permits evidence id equal to command id.
- **Multiple rewards per step.** `WorldStepProcessor.cs:286-291` threads progression through each action, so revisions accumulate one per rewarding command. Ordinal gaps from rejected commands are legal.
- **Per-command rollback.** `WorldStepProcessor.cs:330-351` reverts progression for a command whose later reconciliation fails and still issues a rejected receipt. The history must be threaded and reverted identically or the segment breaks at validation. An `Append` refusal from envelope exhaustion must not be routed through that same branch, or "history full" silently becomes an ordinary rejection for every later resource command.
- **Continuation identities.** `PendingExcavation` reuses one command id across steps. Any future resource effect on such a continuation needs a process-occurrence identity, since a second batch under one command id is refused.
- **Codec change.** I could not diff `ProgressionCodecV1.cs` against HEAD without running git. From reading, only the actor encode/decode visibility appears relevant and the actor byte layout is unchanged. The lead's golden regression should confirm bytes.

## No-finding areas, justified

Frame bounds are consistent: a change encodes to about 706 bytes under the 1,536 limit, a full batch to about 91 KiB under 256 KiB, and a maximal segment to about 0.75 MiB under 2 MiB. Reader field counts of 7, 6, 8 and 5 match the encoders. Alias checks are order-independent because rule pins are collected before the batch loop. Per-actor change count equals the actor revision delta, so omission or reordering cannot survive `ValidateCurrent`.

## Files actually read

Candidate: all eight listed files. Context: shared and Beta AGENTS.md, AGENT_HANDOFF.md, actor-resources.md, the skill. Support: ActorProgressionState, ActorResourceTransition, ActorRewardPlanner, ProgressionSnapshot, ProgressionFrameReader, CanonicalEncodingV1, EntityId, ContentHash, LevelProgressionDefinition, ActorStatProfileDefinition, CurrencyLedger, ProgressionValidation, ProgressionFixture. Seam: WorldEncounterActionProcessor, DeliveryQuestPlanner, RestorativeItemPlanner, WorldStepProcessor lines 196 to 385, plus grep excerpts of EncounterActionResolution, WorldStepBatch and WorldActionReceipt. No commands were run and no tests were executed by me.
