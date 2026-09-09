# Independent portable core review

8 September 2026. Reviewer: the P00 contract/inventory agent, who did not author or edit the reviewed core or its tests. **No unresolved blocking defect was found in the implemented primitive scope after the correction below.** This is a bounded P00 review, not approval of the entire authority, all P00 exit criteria or production readiness. The reviewer's own planning/catalog packet is not independently approved by this document.

## Scope and disposition

Reviewed the six concrete primitive implementations, their versioned byte/numeric contract and corresponding tests against the selected technical design: canonical framing and UTF-8; 128-bit identity bytes/comparison; checked basis-point and duration arithmetic; signed-day normalization/differences; RNG bounds, domain keys, counter exhaustion, restore and failure atomicity. Read the separate Python fixture generator and compared it with the written v1 frame contract.

**Closed finding — calendar JSON restoration bypassed the validating constructor.** The newly added round-trip test exposed a real defect: a nonzero negative-day instant deserialized to the default zero instant. The core author and coordinator observed the failing test; this reviewer independently identified the same missing constructor selection while inspecting the code. The owner added `[JsonConstructor]` to the existing validating constructor and clarified that this is not yet a versioned save envelope. The final independent run passes the negative eon-scale round trip and rejects out-of-range stored milliseconds. Missing-field/version/ownership validation remains future save-envelope work; this fix does not claim a complete persistence layer.

Other inspected contracts are internally consistent within their stated boundary:

- Frame lengths and version/tag fields prevent concatenation ambiguity. Strict UTF-8 rejects invalid surrogates and explicitly performs no implicit normalization. IDs compare unsigned big-endian high/low words rather than Guid or runtime hash ordering. Detected-collision rejection remains an authority registry obligation.
- The signed 128-bit product covers every pair of Int64 multiplication inputs; narrowing rejects an unrepresentable result. Division rounds toward zero for signed modifiers and upward for nonnegative durations without an overflowing pre-add.
- WorldInstant uses floor normalization for negative totals and checked narrowing for day overflow. Int128 differences cover the full signed-day range. The type deliberately does not advance biological, tactical or wall time.
- Rejection sampling excludes the incomplete low residue cycle; rejected candidates consume counters on accepted calls. The last UInt64 counter is usable once, persisted exhaustion prevents replay, and a bounded request unable to accept before exhaustion leaves public counter state unchanged. Restore validates version/key/exhaustion before mutation. Explicit rewind is a caller-controlled operation, not authority permission.
- Tests exercise independent fixed digests, shifted text boundaries, Unicode/culture, zero/all-one identities, full-range time arithmetic against BigInteger, signed rounding, invalid bounds, real rejected candidates, final-counter rejection/rollback and every RNG key component. No statistical randomness test substitutes for the exact contract checks.

## Executed verification

Environment: macOS 15.6.1, Darwin arm64; the executable reported SDK **10.0.401**. The core targets net8.0. No installation or runtime code change was performed by this reviewer.

1. Ran the Python standard-library fixture generator and compared stdout **byte-for-byte** with `reference-v1.json`: identical. The separate generator was supplied by the core author; this reviewer inspected and reran it. Its independence is from production C# code, not independent authorship of the proposed framing convention.
2. After the owner confirmed the Release build was stable, independently ran `dotnet test tests/OpenFantasy.Core.Tests/OpenFantasy.Core.Tests.csproj --no-build --no-restore -c Release --results-directory <review-output> --logger trx` using the pinned executable and DOTNET_ROOT. **46 passed, 0 failed, 0 skipped**, exit 0. The reviewer reused the owner's compiled Release artifacts to avoid concurrent build writes; this is an independent test execution, not a separate clean build.
3. Retained the actual [TRX result](core-review-results/independent-core-release.trx). Root integration/build evidence remains separately owned and required.

## Limits and next gates

This implementation does not yet supply typed physical quantity wrappers, complete character/world state, definition registry/collision handling, canonical full-save encoding, command receipt/durable transaction, scheduler/Wait policy, exact history, import, network permission or package validation. Those are explicitly absent and must pass their P01–P03 proofs. The review found no reason to replace this bounded C# primitive foundation or introduce speculative Rust/IPC. It establishes neither full-world performance nor LANG qualification.

Windows/Linux execution, other architectures, exported Godot behavior, native graphics/audio, human acceptance and hostile save-envelope handling were not tested here. The independent release run does not close these gaps. Before P01 persistence accepts external bytes, reject missing/unknown schema fields and verify owner/version/key requirements rather than relying on permissive default JSON construction.

## Reviewed source snapshot

Hashes identify the exact source/test inputs read for this verdict. Later semantic changes require affected review. Supporting build/dependency and source-preservation checks remain coordinator responsibilities.

| Input | SHA-256 |
|---|---|
| [CONTRACTS.md](../../Runtime/src/OpenFantasy.Core/CONTRACTS.md) | `7e50a1675909fa47fea974bc0d7a3b97876d4f5892c3d5e354c306d76fa7175c` |
| [CanonicalEncodingV1.cs](../../Runtime/src/OpenFantasy.Core/CanonicalEncodingV1.cs) | `8dd410c6f587149dac09b6b069fb23f11da81568d941983aefcf08cfb80d3dea` |
| [CounterRandomV1.cs](../../Runtime/src/OpenFantasy.Core/CounterRandomV1.cs) | `cb244ec45118eae559a0d9d08c5203d27d844e0ac4ea07f16efd7cd20d98d22c` |
| [EntityId.cs](../../Runtime/src/OpenFantasy.Core/EntityId.cs) | `650b8d71d0124beb99acc1b845dd3224c6cca7a39d6f410a155c5d41d0998c03` |
| [OpenFantasy.Core.csproj](../../Runtime/src/OpenFantasy.Core/OpenFantasy.Core.csproj) | `470b8bbe4ee9f686e1144d857fa279e6409a3312b4f4fd6b522fc9dd0aa7e96a` |
| [RandomCounterState.cs](../../Runtime/src/OpenFantasy.Core/RandomCounterState.cs) | `e3c82c03fcf0ef5cf821e17e54eab63f5fbf75c0dd408aac4438efbb198c4875` |
| [RuleMath.cs](../../Runtime/src/OpenFantasy.Core/RuleMath.cs) | `b03c50c681b7ea71d73cca86438c2054f77f041d43b1e4cd53cf1a4b028a6e51` |
| [WorldInstant.cs](../../Runtime/src/OpenFantasy.Core/WorldInstant.cs) | `0a7fe27f512a1b1c1e0ae8ade41d9078b4f71cd7dc6883c6d6f3e75a2c0e7156` |
| [CounterRandomTests.cs](../../Runtime/tests/OpenFantasy.Core.Tests/CounterRandomTests.cs) | `725d08ed7e51b170f16a4088a1c9b98e96b04779b54cbbf2d390f186de7f351c` |
| [EntityIdTests.cs](../../Runtime/tests/OpenFantasy.Core.Tests/EntityIdTests.cs) | `861adc475e6041ee8579ef4e3756313328ce34da429b269315127bac17aa9f65` |
| [generate_reference_vectors.py](../../Runtime/tests/OpenFantasy.Core.Tests/Fixtures/generate_reference_vectors.py) | `4d775a6ef61fb80b714380babd8df9a15c15c0edcf2ab1e9b17b3971da789dd1` |
| [reference-v1.json](../../Runtime/tests/OpenFantasy.Core.Tests/Fixtures/reference-v1.json) | `b431439ed30debe761eaf91150496330f0fcd3de979a9f1b149405791ce11649` |
| [OpenFantasy.Core.Tests.csproj](../../Runtime/tests/OpenFantasy.Core.Tests/OpenFantasy.Core.Tests.csproj) | `d70e52006296932dd2a5933f72d4782af1f86ace65fcf0461a2f7828aecce310` |
| [ReferenceVectors.cs](../../Runtime/tests/OpenFantasy.Core.Tests/ReferenceVectors.cs) | `1e1f16d845ed0ee00d7d66faec31b361f13ed63adcd3e94b6a5c99b21422db63` |
| [RuleMathTests.cs](../../Runtime/tests/OpenFantasy.Core.Tests/RuleMathTests.cs) | `1ae00f87d9a9cafccd5d20ef0ba90397bf8f44098e0ff6eb62f4c6923e60a525` |
| [WorldInstantTests.cs](../../Runtime/tests/OpenFantasy.Core.Tests/WorldInstantTests.cs) | `5dcce2e8dc2754f5acbedacfd9ae7a91881baeb928de7bf8a6e9caee1a9ffaf3` |

## Executed artifact snapshot

| Artifact | SHA-256 |
|---|---|
| [OpenFantasy.Core.Tests.dll](../../Runtime/tests/OpenFantasy.Core.Tests/bin/Release/net8.0/OpenFantasy.Core.Tests.dll) | `099dc8f2b85bfe77580be98d90b9848deee222ecd0bbff2a4864ce168feaa7e2` |
| [OpenFantasy.Core.dll](../../Runtime/tests/OpenFantasy.Core.Tests/bin/Release/net8.0/OpenFantasy.Core.dll) | `c136e2380d8b7bc9df69473dbba8dc6bee3609a2a53c1b6d7e8202bd34bbd211` |
| [independent-core-release.trx](core-review-results/independent-core-release.trx) | `5f9edc4d167c71e35c8fa69cee2c7b2b56e7255a7ef3351152be6a6d2a1b3518` |
