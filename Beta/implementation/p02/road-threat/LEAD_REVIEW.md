# Lead review of the road-threat delivery

The delivery at `48fb19e176255e5a3c20fd9d92900814df7e5be4` is not accepted yet. Its final wrapper
reports account for 2,302 distinct managed cases (1,764 Core, 538 Host); the fifteen modes returned
accepted results. This establishes the executed checks, not full assignment coverage.

## Demonstrated scheduling defect

`LocalWorldSession.TickAsync` selects hostile entry only when no player request is pending.
The retained [counterexample](integration/scheduling-before/scheduling.trx) walks the actual source
camp lane while submitting a fresh, well-formed gather request for an unreachable oak each cutoff.
The gather actions are refused, but their advancing batches keep moving the player through the
chief's real reachable interval. The expected encounter count of one remains zero. The new
`RoadThreatSchedulingRegressionTests` asserts the intended behavior and currently fails.

Correction must give eligible hostile entry its proper cutoff priority without weakening genuine
reading, care, pending-consequence, pause or encounter holds. A pending ordinary action must be
completed/refused coherently rather than hang or execute against an unintended new boundary. Preserve
command identity/retry law, ordinary movement before contact and all existing session cases.

## Required native join is missing

Every new device fixture starts at road cell (346,318) after managed quest reward, purchase and travel.
That boundary is honestly documented in RESULT, but does not prove assignment requirement 5's complete
native bought-cargo → road threat → paid escape/bypass → far sale → reopen journey. A keyboard route
may start after authentic earned funding, then buy through the actual UI and walk the outbound lane;
short controller/pointer fixtures may retain their declared road start. Never substitute managed
purchase/movement for this required native join or expand the 720/750/900-second bounds.

## Unsupported threat disclosure

`LocalRoadThreatView.Project` publishes every hostile scenario's precise HP/level/distance from the
world root, even at the distant farm. The retained [observation counterexample](integration/observation-before/observation.trx) exposes level7/HP800 at a centre distance of63,077subunits (246.39cells). `RoadThreatWarning.Urgent` displays it without an observation
filter. Publish only supported local observations (distance, same floor and sightline), so renderer
filtering cannot hide an overbroad public contract. Prove distant/occluded/different-floor contrasts
and an observed pre-contact warning. Preserve omission of the whole field in older worlds.
The tooltip's “attacks anyone” must agree with the implemented player-targeted scenario.

## Other acceptance work

- Complete missing reading-hold, readiness, sightline and control initiation contrasts proportionately;
  real geometry or a clearly declared fresh calibration is acceptable, invented played history is not.
- Remove the planning codename from the new source comment in RoadThreatSources and the corresponding
  architecture sentence; source folder history stays unchanged.
- Reconcile new architecture contracts and preserve the full unresolved game scope.
- RESULT's attribution of intermittent Android editor settings errors as environmental is unproven;
  report the observation and unexplained cause, not a diagnostic conclusion.
- Prototype presentation/physical devices/human art acceptance remain unqualified.

Independent review is running separately. Its findings and these lead findings must be dispositioned
before acceptance. New failures remain preserved; no old test assertion or gate is to be weakened.
