# Cargo-loop implementation dependency notes

Static source inspection at 214da29; not a route, performance or native acceptance result.
This complements the retained spot-exchange integration plan without changing its scope.

The next two-town binding needs three boundaries together: actual admitted merchants and goods,
legal spatial travel retaining the same custody/history, and a presentation adapter that can display
those places. The current farm presentation cannot simply display a more expansive WorldSnapshot.

- LocalSpatialSnapshot admits up to 16 nonoverlapping regions and 64 actors in one scope.
  TerrainRegionBounds admits 32 by 32 by 16 cells per region. Movement remains bounded by
  256 subunits per action, with full body occupancy and actual support. These are current
  local admission bounds, not an infinite-world or streaming implementation.
- FarmCanvasCoordinates hardcodes minimum cell (96,320), dimensions 32 by 32 and feet Z=256.
  Validate rejects all other positions. A cargo presentation needs a camera-relative observed
  region/floor adapter, preserving old farm interpretation instead of loosening its explicit guard.
  Terrain/actor drawing, camera bounds and inspection must consume the same permitted geography.
- GroundRouteSearch is a bounded local search (256 expansions); the stepped route has separate
  2048 default / 4096 maximum bounds. A long-trip failure is not evidence that no route exists.
  Use an explicit itinerary with bounded legal legs and revalidation, not a larger arbitrary
  search limit or directly relocating actors. Sparse waypoints alone prove no navigation.
- Retained source candidates Marla (96,350) and Kegga (347,140) are 461 cardinal cells apart.
  Their original locations and shop payloads are source facts. A continuous finite multi-region
  route may fit the local envelope, but geometry, generated support, world role admission and
  complete travel have not been executed. Do not relocate the merchants to the farm merely
  to reuse its renderer, or treat a hypothetical corridor as a procedural-frontier qualification.
- WorldMarketAdmission composes fresh unplayed paid genesis and creates no stock or money.
  Merchant home place, owned custodian and owned currency account are validated. The future
  source import must admit both residents, item definitions and lawful finite funding/supply;
  wiring MarketCircuitPolicies.RationCounter to names is not that import. Already earned cargo
  remains in the existing inventory/currency owners through travel, threats, sale and reopening.

A useful next coherent proof keeps the actual legacy coordinates and source records, presents both
places through a reusable local-region adapter, walks the retained cargo across a real boundary,
changes quoted availability through genuine stock changes, and authenticates the same lot lineage
and funds after saving/reopening. It must also retain a real threat and return consequences, followed
by the plan's NPC route, escort payment, migration and multiplayer proofs. This note does not close
those requirements or permit shortening the complete cargo acceptance gate.

The almost-infinite frontier still requires its separate residency, coarse/detail authority,
streaming, preparation/publication and history integration. Rendering two connected markets is
valuable local evidence, not evidence of that larger world lifetime contract.
