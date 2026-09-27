# World resource integration implementation assignment

Astra delegates this substantial coding/testing unit to Claude `fable[1m]`, Max effort. Owner authorization
is in AGENTS.md and Beta/AGENTS.md; Opus is also approved but this dispatch deliberately uses the proven
Fable selector. Finite limit: 1,800 seconds and $15 list-price budget. No subagents, retries or model fallback.
You are not alone in the repository. Work ONLY in this isolated checkout and only the listed ownership.
Do not revert anyone's work, modify frozen specs, commit, change branch/index, or enable a disabled gate.

## Context and starting state

Read shared instructions, handoff, architecture/actor-resources.md, world-composition.md and the accepted
p02/resource-history/WORLD_INTEGRATION_CHECKLIST.md. The accepted history unit is commit 049408e.
This checkout adds preserved unqualified Care/profile25 work: it compiles but is not accepted live or
durable. Do not discard it. WorldGenesisAdmission deliberately refuses Care; leave that refusal intact.
The original timed-out standalone history assignment was completed directly. This is new world-writer
integration, not a retry. Read the installed game-technical-design skill; do not reload the whole archive.

## Required outcome

Integrate the accepted ordered resource-history component into the ACTUAL draft complete-world/profile25
transaction and canonical state, covering all present HP/MP/XP writers. The new world owner must retain
causal records sufficient to check each change against its actual named domain evidence, not merely
accept a self-consistent ResourceChangeCause label. Keep high-level attacks, rewards and treatment as
domain concepts. Do not invent a universal event language, ECS or second mutable HP owner.

1. Establish an exact fresh progression anchor with world/branch/boundary and admission binding, retain
   immutable historical evidence, and validate the complete replayed actor set. Decode must not claim
   authentication from a basis supplied solely by its own history. Distinguish canonical structural/
   causal checks from trusted genesis/journal authentication, which remains the host's responsibility.
2. Capture actual successful quest reward/XP effects (including zero/no-level XP), actual attack damage
   plus caster MP in one batch, and existing aftermath treatment. Recompute/validate amounts and pins
   against retained ability/damage-draw, quest/reward, remedy/treatment/material evidence. Reject a
   component-valid invented or wrong cause, mismatched amount, missing MP/resource effect, or forged
   refill at complete-world admission. Preserve all actors and coupled payment/publication semantics.
3. Multiple rewards can share one world sequence: use actual deterministically ordered action positions.
   Attack resolution has no batch.Actions: retain the earlier selected decision command, now resolved
   at the actual world sequence. Later material/geography rejection must roll back the history as well
   as progression. No-op actor transitions add no history, but XP revisions always do.
4. Remove the care-specific ExpectedHpAfterLastAttack supersession seam. Preserve the original attack
   fact and RNG evidence; explain later HP/MP through generic ordered history. Do not delete or weaken
   historical attack validation to permit healing. Keep original profiles through24 exact and unchanged.
5. Encode/decode the COMPLETE new owner in the draft profile25 only, with bounded parsing and no silent
   truncation. The standalone history archive bounds are storage envelopes, not acceptable permanent
   gameplay caps. If host-authenticated rollover cannot be provided in this assignment, keep durable
   admission explicitly disabled and document the exact continuation obligation; never turn capacity
   exhaustion into an ordinary rejected action that silently freezes all future resource progress.
6. Add actual in-memory WorldStepProcessor journey tests using source-backed care/encounter fixtures:
   real attack(s), aftermath treatment and another resource change; XP with and without refill where
   the world's actual action policy allows it; canonical restore at meaningful boundaries. Include
   forged cause, omitted/reordered effect, stale basis/current actor, failure rollback, and unchanged
   previous profile behavior. Tests must exercise the actual writer paths, not hand-built fake current
   HP. It is fine to add in-memory fixture support alongside existing Sqlite fixture methods while the
   durable genesis gate stays shut. Do not broaden medical permission or declare ordinary/live/native
   care complete here; their remaining integration is explicit in the existing checklist.

## Ownership

- `Beta/Runtime/src/OpenFantasy.Core/Worlds/WorldSnapshot.cs`
- `Beta/Runtime/src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV25.cs`
- `Beta/Runtime/src/OpenFantasy.Core/Worlds/WorldCareAdmission.cs`
- `Beta/Runtime/src/OpenFantasy.Core/Worlds/WorldCareProcessor.cs`
- `Beta/Runtime/src/OpenFantasy.Core/Worlds/WorldEncounterActionProcessor.cs`
- `Beta/Runtime/src/OpenFantasy.Core/Worlds/WorldEncounterActionAdmission.cs`
- `Beta/Runtime/src/OpenFantasy.Core/Worlds/WorldStepProcessor.cs`
- `Beta/Runtime/src/OpenFantasy.Core/Worlds/WorldIdentityIndex.cs`
- `Beta/Runtime/src/OpenFantasy.Core/Worlds/WorldAdmissionValidator.cs`
- `Beta/Runtime/src/OpenFantasy.Core/Worlds/WorldResource*.cs`
- `Beta/Runtime/tests/OpenFantasy.Core.Tests/WorldResources/**`
- `Beta/Runtime/tests/OpenFantasy.Core.Tests/Care/**`
- `Beta/Runtime/tests/OpenFantasy.Hosting.Tests/Care/**`
- `Beta/Runtime/docs/architecture/world-resource-history.md`
- `Beta/implementation/p02/world-resource-integration/worker/**`

The lead owns all Hosting production/storage code, existing architecture pages, integration and commits.
You can READ other source as needed. If a correctness fix necessarily needs a file outside ownership,
report the exact required patch as a deliverable; do not quietly expand scope. Update the new timeless
world-resource-history reference to state real bindings and limits; put progress/evidence in worker/.

## Tools and verification

Executable: /Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet.
Run direct dotnet commands; compound shell setup/redirect/env-assignment commands are not granted.
Use Write/Edit for files and precreated worker/ directory for evidence. The project path is Beta/Runtime.
Restore OpenFantasy.sln with --locked-mode --ignore-failed-sources --disable-parallel. Build --no-restore
-m:1 -p:UseSharedCompilation=false. Run focused affected tests with --logger trx and --results-directory
Beta/implementation/p02/world-resource-integration/worker (use the correct path relative to cwd).
Do not run full Hosting suite: the lead owns broad regression and independent review after integration.
Retain failed test outputs. No native Godot/focus work. You have Read/Glob/Grep/Edit/Write and only scoped
Bash(dotnet); no git shell is necessary. Read/Write access includes the copied skill under this task root.

Return compact REPORT.md under worker/ with files changed, real tests/results, counterexamples fixed,
remaining obligations, archive/authentication boundary and any ownership gaps. If time is short, finish
one coherent implementation and its tests; preserve honest partials rather than fabricate completion.
