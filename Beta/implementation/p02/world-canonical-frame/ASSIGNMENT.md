Implement and test one bounded serialization optimization in OpenFantasy. You are not alone in the shared checkout: preserve others' edits, never stage/reset/commit and stay strictly inside the owned paths below. Astra owns integration, architectural docs, all other files and broader qualification. No subdelegation. Use fable[1m], Max effort, with this single run's 1200-second/$12 limits. Stop and report any permission denial; no alternate tool/path workaround or paid retry.

Read AGENTS.md, Beta/AGENTS.md and the Active assignment in Beta/implementation/AGENT_HANDOFF.md. Read Beta/Runtime/docs/architecture/persistence-and-history.md and coordinated-world-continuation.md, and Beta/implementation/p02/coordinated-world-continuation/RECOVERY_FOLLOWUP.md. Do not reload the research archive. Pure portable modular object-oriented C#, stable OpenFantasy runtime names. No planning codename in application identifiers/comments/strings. No gate/budget/deadline/canonical version/rule change.

Evidence: a real 1569-commit profile27 replay costs29.645s; WorldSnapshotCodec.Encode is called3659 times, taking11.325s/10.9GB cumulative allocation. Core often encodes a final candidate for the envelope check and Hosting encodes the identical immutable root again. The unchanged30s external recovery failed twice. We want to eliminate repeated canonical serialization without caching/skipping admission or trusting checkpoint/hash-only history.

Deliver a narrowly owned immutable WorldCanonicalFrame bound to its exact WorldSnapshot, carried by WorldStepDecision and reused by WorldCommitPreparation. Capture bytes through the existing canonical encoder and keep the existing envelope checks at their original rejection boundaries. No public arbitrary bytes+world constructor; no mutable backing exposure, global cache, predecessor chain or broadly serialized cache flag. A public byte export must return a defensive copy; Span/read-only views must not expose mutable array ownership. Preserve lazy/fallback behavior where older paths did not eagerly encode, and preserve every old refusal/exception boundary. Do not move a failing per-action envelope check outside its rollback path.

Plumb already captured frames through final normal/movement/encounter/lifecycle/care decisions and archive wrappers. A movement preflight may reuse its captured frame only when its exact admitted root remains the final candidate; stale/preflight frames must never bind another root. Reject any candidate/frame mismatch internally. Complete WorldSnapshot admission, actual archive decoding/retrieval, canonical codecs and whole-journal verification remain unchanged. Work toward a single prepared frame for the final candidate, not a profile-specific workaround or a change of represented game state.

Owned paths ONLY:
- Beta/Runtime/src/OpenFantasy.Core/Worlds/WorldCanonicalFrame.cs
- Beta/Runtime/src/OpenFantasy.Core/Worlds/WorldStepDecision.cs
- Beta/Runtime/src/OpenFantasy.Core/Worlds/WorldStepProcessor.cs
- Beta/Runtime/src/OpenFantasy.Core/Worlds/WorldEncounterProcessor.cs
- Beta/Runtime/src/OpenFantasy.Core/Worlds/WorldEncounterActionProcessor.cs
- Beta/Runtime/src/OpenFantasy.Core/Worlds/WorldEncounterLifecycleProcessor.cs
- Beta/Runtime/src/OpenFantasy.Core/Worlds/WorldCareProcessor.cs
- Beta/Runtime/src/OpenFantasy.Core/Worlds/WorldConsequenceProcessor.cs
- Beta/Runtime/src/OpenFantasy.Hosting/Worlds/WorldCommitPreparation.cs
- Beta/Runtime/tests/OpenFantasy.Hosting.Tests/WorldStorage/WorldCanonicalFrameTests.cs
- Beta/implementation/p02/world-canonical-frame/RESULT.md

Add meaningful regressions in the owned WorldCanonicalFrameTests.cs: exact bytes against the unchanged encoder; mutation of exported arrays cannot affect later exports/hash/persistence; exact candidate/frame binding; retained frame across automatic resource/joint archive wrapping; movement and care rollback/final candidate boundaries where feasible. Use existing actual fixtures/helpers. Do not build synthetic earned history to bypass requirements. Public APIs can be tested directly; internal-only contracts can use the existing friend assembly conventions if already supported. If a required change lies outside ownership, report it for Astra instead of editing it.

Shell commands are narrowly prefix-allowed. Copy these EXACT executable and RELATIVE project spellings, with cwd /Users/alucinor/Workspace/Projects/OpenFantasy. Do not add shell pipes, redirections or substitute absolute project paths:
/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet build Beta/Runtime/OpenFantasy.sln --no-restore -m:1 -p:UseSharedCompilation=false --verbosity quiet
/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet test Beta/Runtime/tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~WorldCanonicalFrameTests --logger 'trx;LogFileName=frame-worker.trx' --results-directory Beta/implementation/p02/world-canonical-frame/qualification

You may run only these relevant builds/focused tests, serially. Do not run full regressions or external crash probes. Permission grammar mismatch was the previous assignment's failure; a denial means stop and report, not an approval wait. Direct Read/Glob/Grep are available for inspection, and Edit permission authorizes Write on the exact owned files. First create the owned RESULT.md with a brief assignment status to confirm write access, then implement. Report concise final implementation, tests actually executed, unresolved concerns and ownership changes in RESULT.md. Report model/effort/context from client evidence only, not guessed. This is a new authorized measured performance assignment, not a retry of the previous partial continuation task.
