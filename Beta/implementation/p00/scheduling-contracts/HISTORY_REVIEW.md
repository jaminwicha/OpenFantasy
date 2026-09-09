# Independent history contract review

Reviewer: the scheduling worker, reviewing the separately implemented history contract. The reviewer did not implement History and does not count this as independent review of Scheduling.

**Disposition: one candidate-window defect identified with the coordinator and corrected by the history owner; no other blocking defect found in this bounded contract scope.**

## Corrected finding

The first candidate selector validated world, branch label, pinned head revision and duplicate sequences but did not reject transaction timestamps outside the declared available chronology. A stamp before `HistoricalOrigin` could even be selected; a stamp after `KnownHead` could be silently filtered out. This was inconsistent with the request's inclusive available origin/head window.

The owner corrected selection to reject either boundary violation **before filtering**, including when cursor zero would otherwise exclude every candidate. `HistoryBoundary.Includes` now rejects timestamps before origin. Valid candidate stamps above the requested instant but at or below the known head still filter out normally. The contract explicitly distinguishes available-chronology origin from a checkpoint bound. The reviewer inspected both code changes, the cursor-zero/before-origin/after-head regression assertions, and the updated contract. Wire bytes and existing golden request hashes remain unchanged. Finding closed. The coordinator subsequently reported the full pinned Debug integration passing **149 tests with no skips**, including all **13 History tests** after the correction; this runtime result was executed by the coordinator, not by this reviewer.

## Other reviewed properties

- Exact world/branch/realm addressing retains signed 64-bit spatial subunits and signed-day/millisecond dates. Realm identity is independent of height. No float conversion or coarse keyframe rounding occurs.
- Boundary validation admits exact endpoints and rejects an impossible window, outside request or cursor beyond the pinned head. Absent cursor, explicit zero cursor and earlier positive cursor remain different immutable request values and canonical identities.
- Positive committed transaction sequences come from the retained `CommandCommitIdentity` contract. Commit metadata is sorted by unique sequence, not timestamp or input order; valid non-monotonic timestamps do not choose replay order.
- The supplied candidate batch must match branch label and pinned head revision. Foreign-world commits, unknown later revisions, invalid chronology, duplicate sequences and null entries reject before filtering. A caller's label is not evidence of correct ancestry; that responsibility remains explicitly assigned to the future storage resolver.
- Filtering does not use realm or X/Y/Z, so a local request cannot silently erase remote causal events. Inherited branch history is not rejected based on a stamp's original branch because branch ancestry is supplied outside these metadata stamps.
- Input enumeration is bounded and fails on the 4,097th attempted candidate. The count limits one operation and does not silently truncate full chronology. Read-only copied collections and immutable records retain the accepted input values after caller arrays change.
- Canonical request parsing has a fixed 194/202-byte shape and exact field count. Unknown framing/tag, unsafe lengths, bad widths, non-normalized times, malformed optional cursors, trailing data and incomplete fields reject. Reader bounds do not allocate according to malicious lengths.
- Scope remains honest: these types neither authenticate a historical destination nor reconstruct one. They do not prove the resolver's complete inherited prefix, generated prehistoric fidelity, checkpoint sufficiency or event-level reconstruction within a multi-event transaction.

## Executed reviewer check

The reviewer ran `generate_history_vectors.py` and compared its complete stdout against frozen `history-v1.json`: **exact match, 3,360 fixture bytes**. The generator uses Python `struct`/`hashlib`, imports no production application code and is read-only with respect to frozen expectations. All three absent/earlier/zero-cursor cases were included.

The reviewer intentionally ran no competing .NET build while the coordinator was integrating all work. The retained C# tests were read, including the corrected temporal-boundary assertions; the coordinator subsequently reported all 13 History tests passing in the 149-test full pinned Debug run, whose retained execution results belong to the central increment evidence. A reproduced oracle is not an executed history reconstruction or gameplay test.

## Reviewed input snapshot

Hashes bind the files actually reviewed after the correction. Later semantic changes require follow-up review.

| File | SHA-256 |
|---|---|
| [HistoryAddress.cs](../../../Runtime/src/OpenFantasy.Core/History/HistoryAddress.cs) | `f85eb1c573e245a7f7741bc3998a84ba5479a55f545a5a19579d77360e54c56f` |
| [HistoryBoundary.cs](../../../Runtime/src/OpenFantasy.Core/History/HistoryBoundary.cs) | `1b33b48cb93f15e25047b7b41491cfb0a8205bd955f3a9ed7a6213d9fb690985` |
| [HistoryCommitSelection.cs](../../../Runtime/src/OpenFantasy.Core/History/HistoryCommitSelection.cs) | `d1e1713a862a0fe73ea881d6b79861aeda3810e4da469309ebf5ddf2c7ef233a` |
| [HistoryCommitStamp.cs](../../../Runtime/src/OpenFantasy.Core/History/HistoryCommitStamp.cs) | `8a92e23d255333a5fe9f9ffc9125b602ef0624bdac0c2ce8f4316cfb5f85b605` |
| [HistoryRequest.cs](../../../Runtime/src/OpenFantasy.Core/History/HistoryRequest.cs) | `6313d8fec84fc4f9c6022fdacbc6ace3a185a0d8d4367a696a71dacf12d26df8` |
| [HistoryRequestCodecV1.cs](../../../Runtime/src/OpenFantasy.Core/History/HistoryRequestCodecV1.cs) | `b2c5cc9d6a5941b63d654b945f370c3aeec41707dec158c98d506b09fa3978e2` |
| [HistoryRequestReader.cs](../../../Runtime/src/OpenFantasy.Core/History/HistoryRequestReader.cs) | `0976b67f8dc8f6927412ac367e7bc4167a5bbf18030ca51a3e897389b19254de` |
| [HistoryBoundaryTests.cs](../../../Runtime/tests/OpenFantasy.Core.Tests/History/HistoryBoundaryTests.cs) | `8c65b288324de79db7fee8954b0a5b946442ae0e123efd2265e2eda7165fa793` |
| [HistoryCommitSelectionTests.cs](../../../Runtime/tests/OpenFantasy.Core.Tests/History/HistoryCommitSelectionTests.cs) | `d26ccd8dce9f457f068135404d4273789bf4c3a064de0e28300d7f8e76d762c7` |
| [HistoryRequestCodecTests.cs](../../../Runtime/tests/OpenFantasy.Core.Tests/History/HistoryRequestCodecTests.cs) | `75e59bc771b13fc9399efeecbfb7c757f9d104cf94424f0dc26950c2b4a81799` |
| [generate_history_vectors.py](../../../Runtime/tests/OpenFantasy.Core.Tests/Fixtures/generate_history_vectors.py) | `841141b9c533e8332ecbe5ba8ab5a8e787dc0d6c506fe3b6475d4d7b00d959c2` |
| [history-v1.json](../../../Runtime/tests/OpenFantasy.Core.Tests/Fixtures/history-v1.json) | `0d2713f510f40368a62ec4fec99459ed826056b00cdec9e3f3ed010e4d647a49` |
| [CONTRACT.md](../history-contracts/CONTRACT.md) | `adc8e92eeedf69c3e51004fbfc325fb0c5593e79b05a70f2463f39f6f4a20e42` |
