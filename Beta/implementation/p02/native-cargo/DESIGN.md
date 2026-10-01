# Native cargo join — design record

Implementation decisions for the bounded native cargo assignment. The timeless contract lives in
[cargo presentation](../../../Runtime/docs/architecture/cargo-presentation.md); this file records why.

## Canonical stall identity (contract 4)

- `PaidQuoteView.MarketId` is read from `WorldMarketBook.FindQuote(quoteId).MarketId` of the **same root**
  that holds the quote or outcome. Live views use the current book.
- Archived outcomes: the live book drops closed records at a cut, and the archive replay
  (`WorldJournalRecovery`, `WorldMaterialCareArchiveReplay`, both immutable) returns only the outcome. The
  editable `SqliteWorldAuthority.ReadRecordedPaidQuoteForController` therefore reconstructs the recorded
  root at `outcome.WorldSequence` (a second verified replay pinned to the same live head) and projects
  against it; that root's book still holds the record. Cost: one extra full replay per explicit archived
  inspection (background slot only, never per frame). The immutable session lambda is unchanged.
- `MarketActionOffers.BelongsTo` = `quote.MarketId == stall.MarketId` plus issuer/commodity agreement.
  Bilateral quotes (`MarketId == null`) belong to no stall; their view is otherwise unchanged.
- Explicit choice is the existing draft's `MarketId`; `ContextStall(view, chosen)` never falls back from a
  chosen identity. The panel selector is hidden for one-stall worlds, so the `--markets` focus chain and
  probes are unchanged. Place labels come from `NativeCargoWorld.Stalls` and are labels only.

## One projection (contract 2)

- Host `LocalRegionObserver` (new, `Simulation/CargoPresentation`) projects regions within 24 cells of the
  player's column at the player's single floor; ground kinds only; cached by bounds+revision; returns null
  for unsupported situations. Reads the live in-memory head through the Host-internal accessor.
- Client `CargoViewProjection` (engine-free, linked into tests) is the fixed farm-origin affine map with
  admission against the observation; `FarmCanvasFrame`/`FarmActorFrame` gained an internal regional mode so
  every presented position is validated by the same projection; the fixed farm and worksite constructors and
  guards are untouched.
- Terrain: observed ground kinds through the retained atlas; the farm's own source floor only for its own
  cells. No repetition of the 32×32 farm array.

## Appearance (contract 3)

- Fields from the digest-pinned `npcs.json` appearance records, checked against the source binding's
  appearance hash; layers from a new `merchant_layers.tres` over the exact copied PNGs; ramps from the copied
  `appearance.json`. Layer order follows `src/Client/SpriteComposer.cs` (dwarf race layer in hair ramp).
  `FarmPaperdollComposer` gained a general `Compose`; Wren's output path is the same layers and ramps.

## Native proof (contracts 6–8)

- Fixture: fresh cargo genesis, Wren's quest via ordinary offers, exported closed save before any travel.
- Probe: held arrow keys; Host `LocalRouteAdvice` (read-only ground route) only chooses the key; legs are
  the authored lane (≤ 8 cells), replanned from actual feet after a 2 s stall (bounded).
- Agreement samples compare the presented frame's own feet (not the live view, which can be one step ahead)
  with drawn positions, observed ground, camera limits and framing.
- Results authenticate a disposable byte-identical copy of the native save.
