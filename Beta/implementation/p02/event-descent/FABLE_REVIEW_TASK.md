# Focused event-order collision review

Standing owner approval recommends Fable for bounded critics. Use exactly `fable[1m]`, read-only
Read/Glob/Grep, one call, 300 seconds, $2 client usage cap. No edits, commands, further agents,
automatic paid retry or model substitution. The integration lead owns the result.

Read `Beta/AGENTS.md` and the current handoff routing, then these primary files only:

- `Beta/Runtime/src/OpenFantasy.Core/Spatial/VerticalMotionResolution.cs`
- `Beta/Runtime/src/OpenFantasy.Core/Spatial/VerticalMotionScene.cs`
- `Beta/Runtime/src/OpenFantasy.Core/Spatial/VerticalDescentQuery.cs`

Your concrete review question: can an initially clear admitted supplied scene produce a successful
event-descent result that skips an earlier collision, erases a stopped obstacle, stops on an actor,
or exceeds the proved `(2*N+1)*Q` actual-query bound? All active bodies start at rest under one gravity;
they share cumulative displacement until stopped. Relative active geometry stays fixed. Completed
stops reenter the stationary scene. Probes may encounter distant unknown space, so the selected shorter
event prefix is checked separately. Same-event support validates all stopping bodies together.

The scene is a read-only geometric calculation. It grants no damage, live-world publication, load,
terrain-removal or material rights. Moving assemblies are rigid. Upper conservative footprints can
expand dependency closure and refuse a fringe case; do not call this an unsafe physical contact.
Stationary roles and complete affected geometry are caller obligations. No different release instants,
rotation, impulses or fracture are claimed. Do not broaden into a save-profile or full-game audit.

Only follow a supporting type if a specific counterexample depends on it. The new
`tests/OpenFantasy.Core.Tests/Spatial/VerticalMotionTests.cs` under `Beta/Runtime/` records concrete
unequal landings, later catches, actor/ledge refusals and per-prefix resume; inspect a relevant case
if useful. Avoid reloading research/specification archives or doing unrelated review.

Return at most 400 words: files actually inspected; up to two substantiated defects with exact
path/line and a reproducible geometric example; otherwise say no substantiated defect in this scope.
Clearly separate unverified hypotheses and coverage limits. Stop once the bounded question is answered.
