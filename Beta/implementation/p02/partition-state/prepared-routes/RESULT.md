# Prepared access routes over exact bounded geometry

Construction's actual planner checks site clearance and required access routes on every paid step.
A time-only offscreen continuation would bypass those dependencies. This increment makes the existing
route algorithms available over bounded prepared terrain/occupancy reads without constructing a complete
local scene; it does not introduce a second pathfinder or an offscreen work interpreter.

`SpatialRouteReadSet` joins exact scope/bounds and selected geometric detail. Ground and stair searches
use the same `SpatialMovementQueries` as ordinary scenes. The search algorithms are unchanged except
for their dependency type; occupancy/segment methods move into that shared owner with their bodies
preserved. [The extraction check](EXTRACTION_CHECK.json) records that source comparison.

Prepared terrain lookup is restricted to the declared query boundary. Extra cells in a loaded archive
cannot supply a route through unchecked occupancy. Missing endpoints and attempted detours report
unavailability; they do not prove a larger qualified region has no route. Actual body, swept volume,
footing and stair probes retain their normal checks. Complete-scene consumers retain their original
terrain availability. Read freshness still requires exact occupancy pages, terrain pins and support
interpretation; control, work eligibility, clocks and publication remain separate requirements.

## Concrete evidence

Seven new Core cases cover positive/negative flat routes, replay of every returned segment, loaded-but-
unrequested terrain, an apparent detour outside the read boundary, exact stair waypoints/search counts,
finite budgets, grid alignment and stale arrivals/terrain/support. Prepared queries agree with ordinary
routes when they have the same relevant geometry; a narrower boundary correctly refuses unchecked paths.

The funded-construction case uses the real quest/material fixture and ordinary work processor for all
200 paid steps. It compares required access before work, after the first paid step and after actual
installation. Partial work retains original reserved stock; completion retires it and installs the
structure through the unchanged material transaction. This is **complete-world work qualification**,
not proof that work executes with its terrain absent.

The file-backed 200-presence Hosting fixture additionally queries a route from its already selected
actor/terrain detail. It requires no complete scene and no additional archive read. The existing index,
movement/cargo, and before/after-commit recovery cases remain part of the affected verification.

## Verification

The complete Core suite passes **1,964 cases**, zero failures/skips, in 175.461 seconds within its
unchanged 180-second command limit. The breadth is warranted by the shared queries used by ordinary
movement, construction and other geometry consumers. The new funded-work case takes 1.532 seconds;
the longest existing material-history case takes 141.571 seconds. This single run does not diagnose
a slowdown or establish runtime performance. One one-second native process sample was taken during
the observed wait (2.926 seconds including symbol processing); its output is retained. The deadline was
not extended and the suite was not restarted.

The selected Hosting run passes **18 cases** in 16.451 seconds within its unchanged 180-second bound,
including actual construction work/recovery. There are **1,982 distinct passing cases** across the
complete Core suite and this Hosting selection, with no failures/skips or build warnings/errors.
The [verification record](VERIFICATION.json) contains final Hosting outcomes, exact commands, source/
binary pins, documentation checks and protected-input checks. No existing test, simulation law,
canonical format, profile, database schema or deadline changes. No current native, human, platform,
independent or full phase acceptance is claimed. Ordinary rendered journeys retain their original
binary/scope evidence; this shared Core change is not a new rendered qualification.

## Scope and next integrated proof

The complete construction planner still reads its world, funding, actor/control, tool, stock, placement,
access, exposure and material/identity capacity. The route read set does not remove those requirements
or cache a permanent permission to build. Cross-owner actor/cargo publication, absent-detail funded
work, historical recovery and measurements remain required by PLAN-STATE. Additional prepared queries
cannot close that integrated gate. Independent architectural review remains unrun while orchestration
is paused; source-based lead review is not an independent critic.

[Checkpoint](CHECKPOINT.json) preserves 8c7f6f23 under
`codex/checkpoint-before-prepared-routes-20261017`. The accepted marker is
`codex/milestone-prepared-access-routes-20261017`; work stays on `codex/runtime-foundation`.
Unrelated predecessor staging, frozen specifications, the October5 advisory and older packets are excluded.
Concurrent language-assessment edits to separate roadmap/handoff sections, implementation instructions
and two new files are preserved in the worktree and excluded from this commit. Recorded roadmap/handoff
hashes pin only this increment’s accepted route additions; the private commit index excludes the other
author’s changes without rewriting the working files.

## Documentation review

- **Plan/spec:** Roadmap, comparison and handoff record the access-query dependency and retain the integrated actor/cargo/work/publication/history proof. No standalone route result is counted as offscreen simulation or phase closure.
- **Reference:** World/geography explains shared search, declared availability, overfetch refusal and freshness; materials/activities explains why paid work must preserve clearance/access and its nonspatial dependencies.
- **Accuracy/completeness:** Lead traced ConstructionPlanner, ConstructionSnapshot, ConstructionSourceFrame, LocalConstructionController, route searches, terrain lookup and footing. Exact source comparison verifies the search/segment extraction; the new tests distinguish bounded geometric agreement from complete-world construction admission and canonical time/material publication.
- **Checks:** VERIFICATION.json records terminal tests, authored whitespace, documentation links/fields and source/binary/protected-input hashes. Mechanical checks supplement factual review. Unrun independent, native and full-scope gates remain explicit.
