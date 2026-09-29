# Consented format qualification assignment

Implement the remaining format qualification promised by the Core-world assignment, against the
CURRENT working tree at 1b922ba286766ee060b3c02990b755a2373f2da1. This is a new deliverable after the completed boundary proof unit,
not a retry. Read AGENTS.md, Beta/AGENTS.md, current AGENT_HANDOFF.md, and the previous packet's
NEXT_QUALIFICATION.md, CODEC_CRITIC_REVIEW.md and CODEC_CRITIC_DISPOSITION.md. Use current architecture
consented-world.md / consented-production.md / continuation references. Avoid reloading the research archive.

Outcome: independently reproducible canonical vectors and meaningful missing decoder/archive tests,
with an honest coverage matrix mapping existing proofs as well as new ones. Existing 24 world cases,
1,664 Core and 196 managed Hosting cases pass. No runtime behavior/schema changes are assigned.

1. Write a standalone standard-library Python encoder/fixture generator that never invokes/imports
   runtime codecs or harvests its expected bytes from their output. Build expected fields from explicit
   semantic fixture facts. Pin exact hex/length/SHA-256, and support --write (only the NEW owned vector
   file) and --check (compare without modification). Existing generate_consented_material_vectors.py
   and consented-material-v1.json are READ ONLY references; reuse their fixed semantic values where
   useful, not their generated runtime output. Include new action V15 offer/acceptance, step V28,
   world consent evidence/history and the ordered V10 consent operation. Cover the new V28 snapshot /
   Production V11 context binding and archive envelope with explicitly documented compositional
   vectors: previously qualified opaque owner bytes may be imported only with pinned origin/hash and
   precise limits. Do not label a C# encode/decode self-roundtrip as an independent vector or claim
   unimplemented families covered. If a complete root requires full legacy owner fixtures, reuse the
   actual existing fixture graph and separate opaque old payloads from independently encoded NEW
   framing/contracts. New C# tests must compare actual codec bytes/hash/fields against these fixtures.
2. Review the unchanged MaterialOperationCodec/MaterialOperationHistory V10 consent addition (the
   explicit prior independent-review exclusion). Add substantive hostile/cross-context tests at that
   boundary, and report concrete findings with source locations. This runtime review is independent
   of the original implementation; your new test code still needs Astra integration review.
3. Map the critic's remaining negative matrix to actual existing tests FIRST, then implement missing
   material cases: V28/V27 and V11/V9/V10 capability mixes; actual first/second archive byte decode
   with wrong ordinal/null or wrong prior anchor/flipped bytes; evidence sequence outside segment;
   identity role collision; exact count/frame/action bounds. Oversize tests should assert the actual
   intended format refusal rather than any exception. 256 actual transfers, 4096 combined lots,
   construction, all encounter lifecycle phases, two valid simultaneous pairs, balanced custody
   tampering, definition/producer/prefix changes, and actor-order/issuance mutations already execute;
   reuse those proofs instead of duplicating entire journeys. Keep structural admission separate from
   historical journal authentication: document deliberate acceptance of re-framed pre-cut bytes if
   relevant, not a fake migration or unsupported expectation. Full durable identity reconstruction
   remains Host scope. No 65-actor/live-stock workload reduction or relaxed codec size thresholds.
4. Use existing Core fixture helpers and the Hosting.Tests/Care ConsentedWorldPublicationTests partial
   helpers. Add Hosting cases in that same partial class so journey selects them. Core classes must
   have Consented in their names so focused selects them. Do not change old tests, goldens or fixtures.
   Add only the needed new vector fixture copy/link to the two owned csproj files; preserve every
   existing item/reference/setting. Runtime sources remain READ ONLY. If an actual defect is exposed,
   retain its failing proof and report the minimal correction; Astra owns runtime integration.

## Ownership and safety

You are not alone in this checkout. Preserve others' changes; no git/index/branch mutation, model
switch, nested agents, network access or scope expansion. Astra owns commits, handoff and architecture.
Only these paths may be edited/created:
- Beta/Runtime/tests/OpenFantasy.Core.Tests/Worlds/ConsentedWorldFormatTests.cs
- Beta/Runtime/tests/OpenFantasy.Core.Tests/Production/ConsentedMaterialFormatTests.cs
- Beta/Runtime/tests/OpenFantasy.Core.Tests/Fixtures/consented-world-formats-v1.json
- Beta/Runtime/tests/OpenFantasy.Core.Tests/OpenFantasy.Core.Tests.csproj
- Beta/Runtime/tests/OpenFantasy.Hosting.Tests/Care/ConsentedWorldCodecQualificationTests.cs
- Beta/Runtime/tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj
- Beta/implementation/p02/consented-format-qualification/format_vectors.py
- Beta/implementation/p02/consented-format-qualification/WORKER_RESULT.md
- Beta/implementation/p02/consented-format-qualification/COVERAGE.md

Qualification runners are READ ONLY. Runtime src/, existing fixtures, all prior evidence, frozen specs,
predecessor and saves are READ ONLY. Stable OpenFantasy names in C#; no planning codename in source.
Make an actual minimal owned test/generator edit first to verify access. If a required operation is
denied, stop and return the exact denied call; no shell workaround or permission widening.

## Finite execution and evidence

Exactly fable[1m], one-million context, Max effort. Limit 1,800 seconds and $20 client estimate.
Budget for real tests. No automatic paid retry or substitution. Fixing compile/test issues inside
this one assignment is permitted. Retain every failed log and do not turn missing proof into a pass.
Use only these exact Bash invocations from repository root (no cd/pipes/redirection):

python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/consented-format-qualification/format_vectors.py --write
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/consented-format-qualification/format_vectors.py --check
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/consented-format-qualification/qualification/run_checks.py build
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/consented-format-qualification/qualification/run_checks.py focused ABSOLUTE_SUCCESSFUL_BUILD_JSON
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/consented-format-qualification/qualification/run_checks.py journey ABSOLUTE_SUCCESSFUL_BUILD_JSON

Finish ALL code/project/vector edits before each build/test. The runner pins source and assembly
hashes and prints the successful JSON path. Build deadline240s/test600s unchanged. No broad suite,
native window, shared toolchain upgrade or extra packages. Read failures with Read. Write a compact
WORKER_RESULT.md with exact files/coverage, executed paths/counts, defects and remaining gaps; put
mapping/provenance details in COVERAGE.md. Neither a codec pass nor this assignment closes Host,
native, original retained-save, all game phases or production readiness.
