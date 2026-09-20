# Event-descent review dispositions

One standing-approved bounded `fable[1m]` critic inspected the event ordering and collision work bound.
The [raw response](external-review-result.md) and [measured dispatch](external-review.json) retain its
actual coverage. Source/context preflight and postflight verified; no tools were denied. The response
reported no substantiated defect. It did not inspect tests, cursor codecs, the Host operator or complete
footing internals, and skipped the requested handoff read. Its 517-word response exceeded the requested
400-word limit. No wider independent acceptance is inferred.

## Hypotheses resolved locally

1. **Empty base list / possible indexing crash.** Impossible for the admitted body representation:
   `SpatialStructure` requires at least one box and computes `GroundPlaneZ` as the minimum of those boxes;
   at least one box therefore appears in `VerticalMotionBody.Bases`. An actor has exactly one body box.
   No constructor permits the separately supplied ground plane assumed by the hypothesis. No runtime
   change or synthetic impossible-state test is warranted.
2. **Head contact with a simultaneous or earlier actor stop.** Added a two-case geometric regression.
   A post-supported overhead structure and an actor whose head touches its underside may descend together
   and stop on their respective floors at the same event; neither acquires relative motion into the other.
   Raise only the actor's floor by one subunit and the actor stops first. The continuing structure then
   meets a stationary actor, so the resolver refuses without producing a candidate. Reversed body order
   and different query budgets must preserve both outcomes. These are different physical constraints,
   not an identity-order ambiguity. The test result is retained with the final Core report.

The reviewer’s statement that support is always grounded is limited by the supplied-scene contract:
the generic domain does not authenticate stationary objects' world/material/support ownership. The
construction adapter supplies the complete retained world and additionally validates final components
and the complete owned load graph. The review is not evidence that arbitrary declared stationary
geometry is a stable world.

## Value and economics

The call took 154.773 seconds, seven turns and $1.1310065 in provider-reported API-equivalent usage
under the $2 / 300-second limits. The requested model was exactly `fable[1m]`; the provider reported
`claude-fable-5-1`, plus a native auxiliary Haiku entry costing $0.001578. The lead requested no model
substitution, retry or relay. The response's budget language is not a recorded budget failure: provider
status was success below the cap.

The review confirmed a consequential path and motivated a useful paired regression, but found no verified
runtime defect. This supports occasional narrowly targeted reviews; it still does not justify a larger
retrospective fleet. API-equivalent cost is not a measured subscription-credit deduction, and no Astra
credit savings or full review coverage are claimed. No new ecosystem rule is needed: existing proportional
review, integer ordering, retained evidence and diagnostic/authority doctrines cover these lessons.
