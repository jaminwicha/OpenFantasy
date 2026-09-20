# Authorized bounded contact-model critic

Standing owner approval, 2026-09-19: use exactly `fable[1m]` for review/critic work.
This is one separate read-only assignment, maximum 300 seconds and $2 client usage limit.
No edits, shell, tests, nested agents, paid retry or model substitution. Astra integrates.
The owner supports a larger fleet for earlier phases only if initial reviews demonstrate value.
The separate prior-settlement review exhausted its budget; do not repeat that scope here.

Review this narrow candidate-contact change for a concrete correctness defect. Read the four
production files below and the architecture page, then make at most five targeted supporting
lookups. Reserve budget for a final answer of at most 600 words. Do not read the full GDD,
research archive or historical qualification packets. Applicable project instructions are
AGENTS.md and Beta/AGENTS.md; current review steering is in this packet and the Execution
discipline section of Beta/implementation/AGENT_HANDOFF.md.

Beta/Runtime/src/OpenFantasy.Core/Structures/:
- ConstructionContactScene.cs
- ConstructionBearingModel.cs
- ConstructionLoadPolicy.cs
- ConstructionLoadNeighborhood.cs

Contract: Beta/Runtime/docs/architecture/structural-contact-assessment.md.
Supporting exact contact algorithm: StructuralContactWeights.cs, same source folder.
New tests are ConstructionContactAssessmentTests.cs, ConstructionLoadNeighborhoodTests.cs and
ConstructionContactAssessmentWorldTests.cs under Runtime/tests. Focused 12 Core +2 Hosting
cases pass; full regression is running. Existing tests are evidence, not a requested verdict.

Look for omitted/duplicated mass, incomplete scene dependencies, false exact support, or legacy
Bind semantics changing. Actor hover was already corrected by a complete footing check.
Derive a minimal triggering input before reporting a defect; distinguish speculative concerns
and coverage limits from actual findings. Do not suggest suppressing invariant exceptions.

This is a READ-ONLY candidate assessment. It does not publish a world, prove a fall trajectory,
issue excavated loot, change carried inventory, or relax the current one-assembly settlement.
Shared exact construction/materials and retained actor loads are deliberately fixed. A load
neighborhood is conservative connected bearing closure including rigid membership, not swept
collision closure. Those explicit boundaries are not missing features in this assignment.

Return severity, exact file/line, minimal counterexample and impact for each actual finding,
or say none found. Report what you could not inspect within the budget. Raw session/account
metadata remains local; reviewed findings and aggregate usage may enter project evidence.
