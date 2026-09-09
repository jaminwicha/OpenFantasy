# Command/package contracts and first-loop source preservation

8 September 2026. This is the next retained P00 implementation increment after the [application-style pass](../style-review/README.md). The current core stays portable C#; the Godot adapter remains the presentation boundary. Runtime identifiers use OpenFantasy. Small immutable domain values own invariants and copied collections; the host composes diagnostic responsibilities. Supporting verification and migration scripts remain straightforward procedural tools.

## Implemented scope

- **Commands and receipts:** immutable identities, original submission session, actor/world/branch scope, sorted revision expectations and a typed positive-unit lot-transfer intent. Bounded canonical decoders reject malformed, ambiguous or unsupported frames. Receipt comparison distinguishes an exact retry from changed content and another command. Current delivery authentication and permission remain the future host's responsibility.
- **Packages:** immutable namespace/payload/dependency metadata; a version hashes all three. Strict bounded JSON, independent canonical byte fixtures and deterministic dependency-first resolution reject missing, duplicate, cyclic or mismatched dependencies. The host can inspect real command files and manifest/payload files with bounded allocation and controlled failure exits.
- **Source preservation:** 31 selected roots now resolve to 49 structured records, two code-defined node archetypes and 171 typed edges. The packet keeps 29 original assets and 48 exact source/support files, with stable keys, original payloads and file hashes. Its wrapper binds the closure manifest as an opaque preservation payload. Every one of the complete inventory's 532 structured records, 10 indexed code-defined entries, 201 files and 187 prose headings remains accountable to the full migration.

These contracts do not implement inventory movement, world simulation, authenticated sessions or durable storage. The source packet has no executable gameplay conversion, accepted native art kit or audio. Keeping exact source fields makes those later transformations reviewable; copied source code is `.cs.txt` context rather than another compiled runtime.

## Executed validation

| Check | Result and evidence |
|---|---|
| Independent Python numeric, command/receipt and package reference generation | All three agree with frozen fixtures; the verifier compares output without rewriting expectations |
| Pinned Debug build and tests | **92/92 passed**, zero skipped; [verification](debug-verification.json), [TRX](core-debug.trx) |
| Release/ExportRelease solution build | Zero warnings/errors; [raw build result](release-build.json) |
| Release core tests | **92/92 passed**, zero skipped; [TRX](core-release.trx) |
| Actual host diagnostics | **39 cases passed**: independent positive projections, all 23 malformed command vectors, byte limits, empty/missing inputs, invalid JSON/schema/namespace, hash mismatch, argument handling and real source packet; [results](host-inspections.json) |
| Retained source packet integrity | Exact 52-file set, safe relative paths, lengths and hashes; 29 binary assets explicitly hashed |
| Source closure rebuild and fault tests | **15/15 passed**, byte-identical output; [validation](../migration-closure/VALIDATION.json) |
| Pinned Godot import and C# binding smoke | Passed, no engine error output; host/client foundation values agree |
| Runtime naming and stable inputs | Text contents and all runtime-relative input paths pass the requested scan; all input hashes unchanged during verification; no unexpected authored files appeared |
| Original staged Alpha work | Exact preexisting staged diff SHA-256 retained; [preservation and final input check](preservation.json) |

The integrated run used SDK **10.0.401**, Godot **4.6.stable.mono.official.89cea1439**, and observed **.NET 10.0.12** inside the host and Godot client. The core test target is net8.0; SDK, TFM and process runtime remain distinct. This is the existing macOS/Apple M2 machine, with the existing package cache. Neither Windows/Linux execution, desktop exports, mobile readiness, a performance floor nor gameplay quality is established by this run. The previous fresh-directory foundation evidence remains scoped to its earlier 46-test source tree; this increment does not relabel it as a fresh-directory test of these additions.

## Independent review and resolved findings

The source-closure author separately reviewed the other worker's command code and the coordinator's host. The command author separately reviewed the coordinator's package code and the other worker's closure correction. The coordinator reviewed the source mapping, host integration, tooling and combined evidence. Review scopes, original hashes and limits remain in the [command/host review](../contract-review/README.md) and [package/closure review](../command-contracts/CONTENT_REVIEW.md).

The command/host critic independently executed **18,312 bounded mutation/random frame probes**: 3,073 accepted frames re-encoded byte-identically, 15,239 raised typed format rejections and none raised an unexpected exception. That isolated probe used the reviewer's **SDK 8.0.413**, explicitly separate from the final pinned build. It supplies additional parser evidence, not a supported-platform or authority certification.

Resolved findings were a receipt branch/hash inconsistency, a host file-size observation race, explicit handling of `InvalidDataException`, and a possible late-discovered code-dependent source record escaping special reference expansion. Each correction is documented with its scope. The late source case now fails before publishing a closure claim; current source packet bytes remain unchanged. Package-test qualification also corrected an assertion to accept the documented `JsonException` family rather than requiring that exact base type when the parser throws its derived reader exception.

During integration, the verifier gained binary-asset hashing while retaining strict UTF-8 checks for required text sources. The command oracle changed from overwriting the expected JSON file to printing independent JSON for comparison. The latter and the updated command documentation are the only post-review deltas in the command critic's input snapshot; [review-deltas.json](review-deltas.json) retains old/new hashes and the coordinator's bounded rationale. Application/codecs and golden fixture bytes match the critic's snapshot. Original review reports are preserved rather than rewritten to imply a later independent review.

## Commits and next work

- `d310930`: portable command/receipt and pinned package contracts, independent fixtures and core tests.
- `97c8763`: exact typed source closure, package wrapper, source preservation checks and provenance.
- The following integration commit contains host inspection, verification, these evidence copies, independent reviews and the updated current progress record.

P00 still needs representative actor/body/form and material/process definitions, scheduler/history and finite custody contracts, source-unit/behavior transformations, concrete catalog/workload budgets and separately owned native art/audio packets. P01 must implement actual authenticated, atomic durable command/state/receipt handling with crash/retry/replay tests. P02 then proves the connected native loop and P03 the coupled history/Wait behavior. The [full implementation plan](../../../spec-v1_1/IMPLEMENTATION_PLAN.md) and all 181 requirements remain in force. No phase or complete-game gate is closed by this increment alone.
