# Native cargo repair — design record

Decisions for the review-driven correction. Timeless contracts are in
[cargo presentation](../../../Runtime/docs/architecture/cargo-presentation.md); evidence is in REPAIR_RESULT.md.

- **One root per frame (F1).** `LocalRegionObserver` no longer holds the authority. The regional session
  constructor gives it to `LocalWorldSession`, whose `ProjectView` already runs at each publication on the
  serialized host owner. `LocalWorldView.Regional` is therefore projected from the same immutable root as the
  view's actors and offers. It is `[JsonIgnore]` so existing public-state digests are unchanged. The frame path
  reads `ReadView()` only. The region cache key adds floor and stacked-region revisions.
- **Gate proof seam.** `SqliteWorldAuthority.RecordedQuoteReadHoldsGate` (granted PaidExchange partial, internal,
  null in hosts) lets a managed test pause a real recorded inspection inside the writer gate. Natively,
  `VerifyRecordedHistoryAsync` uses the same history slot for one real complete verified replay. It returns only
  revision and match.
- **Faithful inherited world (F2).** Ground kinds gained `Raised` (solid cell above the floor). Openings carry a
  known depth through stacked regions. Water is per observed column (volume, exact top-cell fill), bounded to
  eight cells below the floor up to the cell above it. Regional frames carry the player's own work targets, so
  the excavation cue lands on the worked column and the terrain view outlines it. Authority, offers and fixed
  farm/worksite guards are unchanged.
- **Recoverable absence.** The observer refuses only a missing player, unaligned feet or no region holding the
  floor cell. It no longer requires solid terrain under the feet, so admitted construction support stays
  presentable. The client keeps the last coherent frame, posts a HUD notice and recovers. The cargo world admits
  terrain support only, so off-floor stances are refused by spatial admission itself; the tests document this.
- **Explicit stall choice (F3).** `FarmMarketDraft.Look/ReleaseLook/IsExplicit/ChosenMarketId`. A stall is pinned by
  `PinsOnOpening` only when it is chosen, in reach, or the world's only stall. One-stall `--markets` behaviour and
  its blocker text are unchanged.
- **Production entry (F4).** `--cargo-onboarding-probe SAVE REPORT` constructs the same `FarmGame { CargoProfile }`
  that `--cargo SAVE` does (`OpenCargoAsync`, production seed). Only the probe node and paused start differ.
- **Rendered measurement (F5).** `CargoRenderedEvidence` checks the settled node transform, `CargoTerrainView.LastDrawn`
  (shape, revisions, choice digest) and framebuffer pixels. Focus is observed state; the tool's source audit is
  reported separately. The lead's camera assertion is unchanged; samples wait at most six frames for the
  presentation that follows a HUD relayout.
- **Pointer routing (F6).** A pick requires the router's World layer plus `Permits(NextTarget)` and a live,
  non-closing game.
