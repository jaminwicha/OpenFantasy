# Codec/archive critic review — consented world (profile 28 / Production V11)

**Verdict: no blocker found in the reviewed scope.** This is a read-only structural reading; nothing was built or executed. It is not integration, Host, native, production or game acceptance.

Run facts: model Fable 5.1 (`claude-fable-5-1`). I cannot verify context size, effort setting or elapsed seconds from inside the session. Client estimate was about $6.64 before this message. No edits, execution, agents or git mutation.

## Findings, by severity

### M1 — Impossible action positions are admitted as evidence
- **What is checked:** `WorldConsentedTransferEvidence.cs:27-37` requires only distinct indices below 64. `WorldConsentedMaterialHistory.cs:27-37` requires only order and unique positions. `WorldConsentedMaterialAdmission.cs:42-58` and `WorldAdmissionValidator.cs:129-150` add nothing on positions.
- **What real batches guarantee:** `WorldStepBatch.cs:65,127,170` sorts actions strictly by actor, one action per actor.
- **Counterexamples that decode canonically and pass complete admission:**
  - sequence 5, offer index 3, acceptance index 1, with sender actor id < recipient actor id;
  - two records at one sequence with the same sender at positions 0 and 2;
  - index 63 in a two-resident world.
- **Effect:** no stock effect, because the produced partition and inventory are validated independently. These records refute themselves without a journal, so the structural layer can refuse them. The fix is added validation, not a relaxed invariant.
- **Assumption:** positions index `batch.Actions`, as the contract states; I did not read `WorldMaterialPairing`.

### M2 — Raw evidence is bounded by capacity, not issuance, and its lot is unbound
- `WorldConsentedMaterialAdmission.cs:49-57` checks only that the source exists, the definition matches and `MovedEnd <= UnitCapacity`.
- Retired raw rights elsewhere are bounded by the issuance cursor (`WorldAdmissionValidator.cs:556-560`, `577-579`).
- **Counterexample:** raw evidence whose interval lies wholly beyond `NextUnissuedUnit`, or whose `terms.LotId` never existed, is admitted.
- This sits inside recorded boundary B3. However, "bounded source rights" in `consented-world.md:90` can be read as issued rights. Bound it by the cursor where a node exists, or reword.

### L1 — Pre-cut interpretation is not evident from bytes
- Before the first cut, V28 uses the same V2 production and care frames as profile 27.
- The only differences are the profile number, the coordination tag and an opaque admission hash the decoder cannot recompute (`WorldConsentedMaterialAdmission.cs:13-14`).
- A lived pre-cut profile-27 root re-framed as 28 with empty history would pass structural admission.
- "Fresh admission cannot upgrade a lived world" (`consented-world.md:15`) is true of the API, not of bytes. Record it as a Host genesis-pinning obligation.
- After a cut this is stronger: context profile 9 is hashed into the anchor.

### L2 — Reopen work grows with the number of cuts
- The anchor has no decoder. Anchor *k* exists only by decoding closing root *k* with anchor *k−1* (`WorldMaterialCareArchiveSegment.cs:50-76`).
- Each decode runs full admission, partition replay and two encodes. Each frame is bounded; the chain is not.
- No measured bound is recorded. Only two cuts are claimed as tested.

### L3 — Core enforces that a permanent identity lookup exists, not that it is complete
- `WorldStepProcessor.cs:69-77` throws only on a null lookup after a cut.
- Evidence identities leave the successor root at the cut (`WorldCoordinatedContinuation.cs:33-34`, `WorldSnapshot.cs:417-424`).
- Before a cut a null lookup is allowed. Refused command identities reserved at `WorldStepProcessor.cs:152` do not appear in any snapshot owner I read.
- The "permanent identity refusal" proof is therefore a property of the supplied lookup.

### L4 — Standalone V11 decode returns unvalidated custody
- `ProductionContinuationValidation.cs:32-37` returns early for the consented-world context.
- Only world admission replays custody (`WorldConsentedMaterialAdmission.cs:59`).
- This is documented, but `ProductionCodecV11.Decode` plus `ValidateForAuthority` alone is not sufficient for any future consumer.

## CareWorldSession: test helper, not durable authority

- **In-memory path (`CareWorldSession.cs:16-31`):**
  - calls the processor directly and publishes the candidate unconditionally;
  - uses a `HashSet` as the identity lookup, seeded by the test;
  - keeps batches as objects and never passes them through `WorldStepCodecV28`;
  - retains only `SealedResourceSegment` (line 26) and drops `SealedMaterialCareSegment`.
- For a coordinated world `Archives` is therefore empty. This is inferred from `WorldCoordinatedAdmission.cs:44`.
- The assertion that every emitted identity is new is a useful check.
- **Durable Host, from the lines I read:** no profile-28 binding.
  - Schema Eight is tied to profile 27 exactly (`WorldJournalRecovery.cs:85`, `191`).
  - Schema Three rejects profile 28 at `WorldJournalRecovery.cs:171`.
  - Archive storage rejects any world that is not profile 27 (`WorldDatabase.MaterialCareArchives.cs:62`).
- Whole-world profile-28 evidence is therefore in-memory Core execution. It shows no durable commit, journal replay or recovery.

## Checked, no defect found

| Area | Result |
| --- | --- |
| V28 snapshot frame | 24 top-level and 5 coordination fields match reader limits; binding is empty exactly when the anchor is null; canonical re-encode is enforced. |
| Byte bounds | V2, V3 and V11 maxima are equal, so no post-cut state encodes but fails to decode. History bound (1,049,728 B) is included in the coordination maximum. |
| Evidence frame | 19 fields, at most 4,096 B each, at most 257 fields; overflow is mapped; both format exception types derive from `FormatException`. |
| V15 and step 28 | Bounded and canonical; material intents are refused under every other profile and every non-advancing kind (`WorldStepBatch.cs:71-75`). |
| V9 / V11 separation | Separated by version field, hashed context profile (7 vs 9) and symmetric encoder/decoder gates. V3 decode with a complete-world context fails at re-encode. |
| Early return in validation | Partition replay covers the skipped checks, except raw-lot identity collision with local outputs, which is unreachable without a derivation collision. |
| Capability retention | Enforced at the context constructor (46-50), coordination constructor (20-23), `ValidateSuccessor` (77) and `Rebase` (40-41). |
| Split child | Transfer step (`ConsentedMaterialTransferStep.cs:116-126`) and partition (`ProducedCustodyPartition.cs:98-109`) agree on protection, reservation and revisions. |
| V10 adapter | Exclusive with complete world (context 27-30); refused by `WorldSnapshot.cs:64` and `ContinueWorld` (238-239); codec gating is symmetric. |

## Required proof gaps

I have not read the four executable groups being built, or any current test.

1. **Archive by bytes.** Reopen after the first and second cut only through `segment.Encode()` and `WorldMaterialCareArchiveSegment.Decode`, chaining anchors. Refuse a wrong ordinal, a wrong or null previous anchor, and a flipped byte. Include a mutated closing root with a recomputed reference, to record what structure does and does not catch.
2. **Evidence mutations through V28 decode.** Cover M1 positions, M2 intervals, sequence at or below the boundary and above the revision, cross-role identity reuse, evidence without movement and movement without evidence. Add a codec-level regression for the earlier definition and producer finding.
3. **Capability mixes.** V28 with a V9 production field; V27 bytes with a consented anchor; V28 with a non-consented anchor; pre-cut 28 with a non-empty binding; the L1 re-frame recorded as structurally admitted.
4. **Exact bounds.** 256 versus 257 transfers, 4,097-byte evidence, a 65th action, a V15 command over 2,048 bytes.
5. **Protected stock.** Split-gift a manually flagged lot across a cut, then attempt retirement. Confirm a refusal rather than an admission throw.
6. **Identity lookup.** Rebuild it from archive bytes and journal identities rather than the session set, including refused command and result identities.
7. **V10 cross-offers.** Offer V10 bytes to the V9 and V11 decoders, and V11 bytes to V10, with each context.

## Scope

**Fully read (assigned):** `WorldSnapshotCodecV28`, `WorldStepCodecV28`, `WorldActionCodecV15`, `WorldConsentedMaterialCodec`, `WorldConsentedMaterialAdmission`, `WorldConsentedMaterialHistory`, `WorldConsentedTransferEvidence`, `WorldMaterialCareContinuation`, `WorldMaterialCareArchiveSegment`, `WorldMaterialCareArchiveAnchor`, `WorldCoordinatedContinuation`, `ProductionCodecV9`, `ProductionCodecV10`, `ProductionCodecV11`, `ProductionContinuationContext`, `ProductionContinuationValidation`, `MaterialContinuationReplay`, `CareWorldSession`.

**Fully read (dependencies):** `ProductionCodecV3`, `WorldSnapshotCodec`, `WorldSnapshotCodecV27`, `WorldStepFrameCodec`, `WorldStepCodec`, `WorldActionCodec`, `WorldFrameReader`, `WorldMaterialCareArchiveReference`, `WorldStepBatch`, `WorldSnapshot`, `WorldAdmissionValidator`, `WorldCoordinatedAdmission`, `ProducedCustodyPartition`, `ProducedCustodyMovement`, `ProductionContinuationBasis`.

**Partially read or grep only:** `ProductionSnapshot`, `ProductionMaterialCodec`, `WorldStepProcessor`, `WorldJournalRecovery`, `ConsentedMaterialCodecV1`, `ConsentedMaterialTransferStep`, `WorldStepDecision`.

**Not reviewed:**
- `MaterialOperationCodec` and `MaterialOperationHistory`, so the V10 consented operation frame itself is unexamined.
- `ProductionClaimValidation`, the identity registry and retirement history.
- `IsApprovedByBothParticipants`, `WorldMaterialPairing` and the planner.
- `WorldSourceIssuanceBaseline`, the coordinated resource decode, `CareCodecV3`, `LiquidReservoirCodecV3`, the care and geography contexts, `CanonicalEncodingV1`, `WorldCodecVersion`.
- All tests and fixture helpers.
- The Host beyond the cited lines.
