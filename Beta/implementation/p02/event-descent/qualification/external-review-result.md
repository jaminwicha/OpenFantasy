**No substantiated defect in this scope.** I traced every path and did not find a successful result that skips an earlier collision, erases a stopped obstacle, stops on an actor, or exceeds `(2N+1)·Q`.

## Files inspected
- `Beta/AGENTS.md`
- `Spatial/VerticalMotionResolution.cs`, `VerticalMotionScene.cs` and `VerticalDescentQuery.cs`, in full
- `Spatial/VerticalMotionBody.cs`, to check that `Bases` is a subset of `Boxes` and that `StructureAt` translates the bearing surfaces
- `FootingQuery.cs`, by grep for its return statuses only

I did not open the handoff routing or `VerticalMotionTests.cs`, because the cost cap was nearly reached.

## Conclusions
- **Earlier collision:**
  - An event cannot skip an earlier contact.
    - The event distance is the smallest complete contact (`VerticalMotionResolution.cs:234`).
    - Every active collider is then re-swept over exactly that distance (`:171`, `:253-260`).
    - If a probe came back `Unavailable` but its real contact is nearer than that distance, the sweep returns a shorter drop and the run fails.
  - Active bodies keep fixed offsets from each other. Leaving them out of `_query` hides no collision.
- **Stopped obstacles:** `PrepareRound` (`:148-152`) rebuilds the query from the stationary scene plus every stopped body at its frozen drop, so none is lost. The scene constructor's `complete` query already checks that the catalog can hold them all.
- **Actors:**
  - An actor contact at the chosen distance fails the run (`:236`).
  - A stopped actor re-enters the query as an actor.
  - A structure resting on a moving actor is not a dependent. The next round gives it a zero-drop `ActorContact`, which fails.
- **Support:** Support is always grounded and cannot be circular.
  - A dependent's base sits on a surface of the lower body, so its ground plane is strictly higher.
  - Non-stopping active bodies never enter `_supports` (`:271-273`).
- **Work bound:** The bound holds.
  - There are at most N rounds, each with at most Q probes and Q sweeps, and each body's bases are checked once across the whole run.
  - Zero-distance events skip the sweeps, so they cost less.

## Unverified hypotheses
1. **Uncaught crash on empty bases (`VerticalMotionResolution.cs:162`):** This applies only if `SpatialStructure` allows a `GroundPlaneZ` with no box at that height.
   - `Bases` (`VerticalMotionBody.cs:39-40`) would then be empty.
   - `PrepareSupport` would leave `_work` empty.
   - The next unit would index into it and throw `IndexOutOfRangeException`.
   - The catches at `:67-73` do not cover that exception. The result is a crash, not a false success.
   - I did not check `SpatialStructure`'s invariants.
2. **Order-sensitive actor touch:** Take a moving stepped structure whose higher box starts touching a moving actor's head. Its base lands fully on a pillar in the same event as the actor lands. The run succeeds, with the touch preserved and the structure fully supported by the pillar. If the actor lands one subunit earlier, the next round gives `ActorContact` and the run fails. I judge this inconsistent but not an unsafe "stop on an actor"; the integration lead may decide otherwise.

## Coverage limits
I did not inspect the terrain unknown-cell handling in `TerrainVolumeQueries.Query`, the internals of `FootingQuery`, the codecs or encoding, the host operator, or the tests. I ran nothing.
