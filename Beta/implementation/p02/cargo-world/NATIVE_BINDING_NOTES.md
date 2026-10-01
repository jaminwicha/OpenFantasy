# Native cargo binding dependencies

Lead source inspection at `bf0e5a3`, while the separate cargo-world implementation runs. These are
integration requirements, not executed native evidence or permission to change the pinned runtime
during that assignment. Reconcile them against its actual returned world before assigning client work.

## One consistent geographic projection

Changing only the camera or removing one coordinate guard cannot make the current farm adapter
display other places correctly. The fixed grid is consumed by:

- [FarmCanvasCoordinates](../../../Runtime/client/scripts/Presentation/FarmCanvasCoordinates.cs):
  origin (96,320), 32×32 cells, floor Z=256 and pixel/subunit conversion.
- [FarmCanvasFrame](../../../Runtime/client/scripts/Presentation/FarmCanvasFrame.cs): actor, gathering
  and structure admission; multilevel mode has its own equally bounded worksite validator.
- [FarmCanvas](../../../Runtime/client/scripts/FarmCanvas.cs): actor focus, targets, terrain layers,
  gathering and structure placement.
- [FarmGroundView](../../../Runtime/client/scripts/Presentation/FarmGroundView.cs) and
  [FarmVisualAssets](../../../Runtime/client/scripts/Presentation/FarmVisualAssets.cs): fixed source
  tile-array indexing. Repeating that farm's ground array at every region would invent geography.
- Actor/gathering/structure views, WorksiteCoordinates/WorksiteTerrainView and WatercourseView:
  several direct conversions and fixed traversal bounds.
- [FarmGame](../../../Runtime/client/scripts/FarmGame.cs): camera bounds initialized from the canvas,
  then focus follows the projected own actor.

The native binding needs one explicit permitted-region/floor projection consumed by terrain, actors,
objects, target cues, camera framing and pointer-to-world selection. Keep authority coordinates and
identities unchanged when the viewport moves or regions enter/leave presentation. Preserve existing
farm and multilevel interpretation instead of weakening their guards. Reject absent/unsupported
observations; do not render all authoritative regions merely because the Host stores them.
The actual source world must determine visible terrain rather than a decorative continuous backdrop.

## Identity, appearance and stalls

FarmSpriteKind admits only Wren, Squire and Wolf. Source imports of Marla/Kegga need explicit public
appearance bindings and asset mappings. Artist-authored source/style acceptance remains separate;
repeated Wren sprites would not prove migrated cultural appearance or meet the owner's variety goal.
A temporary diagnostic appearance must be labelled as such and cannot close art acceptance.

PaidQuoteView currently projects participant terms and observed issuer revision, without MarketId.
MarketActionOffers uses issuer plus commodity and selects the first reachable stall. The broader
binding needs explicit stall choice and authenticated quote-to-market context, including historical
records when surfaced. Resolve that using the existing market quote owner/history rather than a
client dictionary pretending to own market identity. Preserve privacy and old non-market quote
interpretation. Regenerate and authenticate affected native evidence if public serialization changes;
old recorded public hashes cannot silently be relabelled as new-view results.

## Required joined evidence

Use the actually accepted cargo-world admission and earned cargo/funds. The native journey must
cross a real region boundary and reach both merchants through the same commands as managed travel,
select the correct stall/lot/terms, sell and reopen without resetting coordinates, history or stock.
Trace presented observations and settled input together. Camera/target/terrain agreement, navigation,
cancel/focus behavior and precise saved effects need real rendered and journal evidence. Keep prior
farm/care/gift/market adapters covered where shared code changes. Background injected input is not
physical-device or human acceptance.

Complete the full cargo plan's real threat and aftermath, NPC travel, escort wages, return
consequences, migration and multiplayer joins; do not substitute an empty corridor for those gates.
Finite multi-region presentation is not streaming, realistic settlement generation, procedural
frontier scale, atlas discovery or production load acceptance. Those retain their full obligations.
