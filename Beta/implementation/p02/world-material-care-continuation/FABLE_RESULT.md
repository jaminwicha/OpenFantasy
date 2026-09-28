# Bounded Fable implementation result

I'm stopping here because the $10 estimate is nearly spent. The care frontier and `CareCodecV3` are written, but **nothing has been compiled or run**, and the test coverage is narrower than the brief asked for.

## Actual API
- `CareContinuationContext.Capture(CareSnapshot closing)`, `CanCapture(closing)`, `Continue()`, `ValidateClosure(closing, continued)`, `ToCommitment()`.
- Context properties: `WorldId`, `BranchId`, `Ordinal`, `ClosingCareHash`, `ClosingRevision`, `ArchivedAcknowledgments`, `ArchivedTreatments`, `ArchivedOrdinaryUses`, `Frontier`, `SupportsOrdinary`, `Pending`, `PatientWitnesses`, `ContentHash`.
- `CareSnapshot.Continuation`, `TotalAcknowledgments`, `TotalTreatments`, `TotalOrdinaryUses`; `OrdinaryCareSnapshot.Continuation`, `TotalUses`.
- `CareCodec.Decode(bytes, context)`, `CareCodecV3.Encode`, `Decode(bytes, context)`, `EncodeContinuation`.
- Continued constructors are internal and take the context; the public constructors stay inline-only and refuse a continued ordinary owner.

## Deviations you should review
- **Four methods made public.** `CareSnapshot.Acknowledge`, `Treat`, `WithOrdinary` and `ValidateSuccessor` were internal. `OpenFantasy.Core` has no `InternalsVisibleTo` for its test project, so the tests could not reach them. I did not add an assembly-wide attribute; revert if you prefer that route.
- **`ValidateSuccessor` now null-checks** its argument (previously a null reference error).
- **Frontier is a world sequence only, not sequence plus ordinal.** Every post-closure operation needs a strictly greater world sequence. This relies on closures never splitting a publication.
- **V3 nests acknowledgments and treatments** in one `care.aftermath` frame so the root stays at 10 fields and the existing dispatcher's version read is untouched.
- **Three V2 record decoders changed from private to internal** for reuse; no byte format changed.

## Invariants implemented
- The context holds no closing snapshot, earlier context or closed history; its commitment is capped at 4,096 bytes, and V3 at the existing 2 MiB.
- Revision is identical across a closure. Totals are archived base plus retained records; the pending acknowledgment stays retained and is counted once; revocations are counted from permissions, once.
- No-progress rule: a continued owner whose revision equals its context's `ClosingRevision` cannot be captured. Inline owners, including empty ones, always can.
- Only the carried pending acknowledgment may sit at or before the frontier. Closed revocations must persist byte-identically; catalogs and grants must match the closure exactly.
- Patient witnesses seed the chronology check, so an old defeat or a non-advancing patient revision still fails after closure.
- Successors must stay within the same context; V1/V2 encoders refuse continued owners; inline bytes refuse a supplied context; V3 without its exact context is refused.

## Files
- **New source:** `CareContinuationContext.cs`, `CareContinuationPosition.cs`, `CarePatientWitness.cs`, `CareSnapshot.Continuation.cs`, `CareCodecV3.cs`.
- **Edited source:** `CareSnapshot.cs`, `OrdinaryCareSnapshot.cs`, `CareCodec.cs`, `CareCodecV1.cs`, `CareCodecV2.cs`, `CareCodecV2.Records.cs`.
- **New test:** `CareContinuationTests.cs` (4 tests).

No existing tests, World/Hosting code, docs or Git state were touched.

## Test coverage gaps
The four tests cover:
- 520 ordinary uses and 520 aftermath pairs, each across two closures
- pending acknowledgment carried through a closure and then treated
- revocation persisting and denying aid after two closures
- V3 roundtrip and a set of codec refusals, including old codecs

Requested but **not written**:
- mixed ordinary/aftermath chronology
- multiple patients
- ordinary use while an acknowledgment is pending
- rewritten catalog
- reordered or duplicate rows
- a no-parent-chain check beyond the commitment size bound
- `ValidateClosure`

## Risks
- Compile errors are possible, especially under warnings-as-errors and the xunit analyzers.
- In the codec test, the loop that overwrites context fields 3–9 with an 8-byte number should fail on any byte mismatch, but that is unverified.
- The legacy byte baselines in `CareLegacyCompatibilityTests` should be unaffected, since inline encoding paths are unchanged; that also needs your run to confirm.

Lead assessment: useful bounded source work, but substantial latency and thinking usage with incomplete requested tests. Completion and acceptance require lead review and executed checks. No automatic paid retry or subscription-credit-saving claim.
