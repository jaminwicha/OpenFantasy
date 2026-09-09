# P00 first increment: implemented foundation

8 September 2026. Coordinator execution record. The design-readiness checklist allowed implementation to start; it did not close P00 or any of the nine complete-game qualification gates. This record supersedes only the pending build/primitive evidence in the inventory author's [initial exit checklist](P00_EXIT_CHECKLIST.md).

## Implemented and exercised

- The reviewed design/research baseline is committed separately at `4b33961`. Original staged milestone work remains byte-identical.
- Build/import isolation and the naming override are committed at `2bd7175`. The predecessor explicitly includes its own `src/**/*.cs`, preserving all 43 previous compile entries. Its build succeeds with zero warnings/errors. The nested planning directory is excluded from its Godot import.
- Portable `EntityId`, `WorldInstant`, `RuleMath` and `CounterRandomV1`, canonical framing, independent Python vectors, locked tests and byte contracts are committed at `2003f54`.
- The pinned solution, host entry point, independent Godot C# adapter and reproducible verifier are committed at `e3d7b2c`. The runtime source/project naming scan passes. The host is an executable entry point and diagnostic, not an implemented world server.
- All **46 tests execute and pass** in Debug. An independent critic reran all 46 against stable Release artifacts. No tests were skipped. Tests exposed a historical-date JSON reset; the validated constructor correction now passes.
- The integrated Debug build, locked restore, editor import and Godot headless binding checks pass. Host and client produce identical canonical identity, RNG and historical-midnight results. Both actually load **.NET 10.0.12** on this machine, despite the client targeting net8.0. SDK, target framework and observed runtime remain separate facts.
- The Release solution builds with zero warnings/errors; client output is in **ExportRelease**. This is a build, not a distributable platform export.
- A fresh directory containing only the committed runtime subtree, with no previous build artifacts or predecessor project, repeats locked restore, build, all 46 tests, import and host/client checks successfully. It uses the same installed tools/package cache and macOS machine; this is not an independent OS qualification.
- A native OpenGL/Metal development window rendered correctly at 960×540; the [retained frame](evidence/development-harness.png) was inspected for legibility. It is a development harness and supplies no game-art, animation, audio or enjoyment approval.

## Evidence and independent review

| Record | Claim supported |
|---|---|
| [Local verification](evidence/local-foundation-verification.json) / [Debug TRX](evidence/local-core-debug.trx) | Exact commands, versions, actual runtimes, input hashes, nonzero test counts and Godot binding results |
| [Fresh-directory verification](evidence/clean-foundation-verification.json) / [fresh Debug TRX](evidence/clean-core-debug.trx) | Committed runtime builds/runs without predecessor files or inherited build products |
| [Core review](CORE_REVIEW.md) / [independent Release TRX](core-review-results/independent-core-release.trx) | Independent primitive inspection and test rerun; fixed calendar defect |
| [Build review](BUILD_REVIEW.md) | Independent configuration/verifier review; corrected release mapping, empty-test and import-error false-pass risks |
| [Machine](evidence/machine.json) / [preservation](evidence/preservation.json) | Observed hardware, intact source archive/staged work, explicit predecessor configuration exception |
| [Planning packet validation](packet-validation.json) | Actual 31-record/22-file legacy selection, 12 catalog families and 23 contract families; no import claim |

The build critic's recorded working verification SHA-256 corresponds to the retained local-verification copy. Its original `artifacts/` path may be overwritten by future runs. Preserve these dated evidence copies. The full source ZIP still matches all 201 recorded payloads; 200 current source files remain unchanged and the sole intentional current-file change is the compile boundary in `OpenFantasy.csproj`. The original snapshot verifier intentionally fails that now-changed live-file assumption; do not alter the preserved snapshot or its historical report to conceal implementation work.

The actual development machine is an Apple M2 with 8 GiB RAM, macOS 15.6.1/24G90. It is not an accepted minimum. Godot 4.6 .NET and SDK 10.0.401 were installed separately from the predecessor's tools, with verified archive checksums. Runtime 8.0.31 was added without replacing the SDK10 launcher. Matching export-template metadata is recorded, but templates/platform exports and Windows/Linux execution remain unqualified. Candidate net8 targeting must be revisited alongside actual P01 export evidence and support lifetime.

## Still open in P00

The following are real preparation/implementation work, not reasons to reopen broad research or discard the core:

- Complete tagged command, receipt, revision, scheduler/history and custody formats with valid/invalid vectors and explicit ownership. The current primitives implement only their declared byte/numeric contracts.
- Define finite initial actor/body/form and material/process/package fields through representative data. Prove representability of later mechanics without generating empty implementations for every concept.
- Close first-loop package dependencies and field transforms. The selected farmer Wren and original six-wood objective remain exact source inputs; repair, recruitment, finite markets and ecological interactions require actual new implementation.
- Set initial production catalog target counts and coverage denominators, measurable workload/storage/queue budgets, and candidate desktop machine profiles. Inventories and workload distributions are currently proposals.
- Prepare separately owned native art/audio work packets with editable source formats and callable tool candidates. User acceptance of native motion, equipment/body coverage and sound remains necessary before bulk production.
- Review those concrete additions, then assess the full P00 exit. A passing scaffold must not close the world-history, import, co-op, performance or human-quality gates.

## Next retained work packet

Continue P00 with versioned command/receipt and package contracts, first-loop dependency closure, and bounded ownership/custody fixtures. Engineering owns the portable types and diagnostic host integration; a content worker owns source-to-target mapping and representative package data; an independent critic reviews the resulting artifacts. Artists receive separate native-kit and acoustic assignments. Each task states owned paths, requirement IDs, fixtures and evidence; existing modules and tests are retained.

P01 then implements actual durable acceptance/receipts and state continuation against these contracts, followed by the connected native loop in P02 and coupled history/Wait proofs in P03. Neither entering P00 nor completing this first increment authorizes skipping those exits or claiming the full game is implemented.
