# Liquid owners across material history boundaries

## Ownership and explicit cuts

[Runoff capacity](../../src/OpenFantasy.Core/Geography/WatershedRunoffCapacityState.cs) owns one
inventory/Production pair, the fixed drainage domain, pending incoming obligations and closed
reservation identities. A material history cut changes the representation of Production. It does
not deliver water, cancel an obligation, create storage room or advance a process clock.

`PlanContinue` derives the next Production segment from the owner's actual closing inventory and
Production. `PlanContinueMaterial` explicitly selects ordered material operations;
`PlanContinueConstruction` additionally requires the opaque construction state captured from the
actual admitted world. Subsequent cuts preserve the selected capabilities. An absent Production
owner cannot be invented by continuation.

The cut also carries bounded opaque geographic qualification: the fixed domain hash and exact new
Production context hash. It is minted from the validated closing capacity owner, not from an arbitrary
generic Production context. Reservoir qualification additionally pins the complete existing geometry
catalog. Neither qualification retains a parent owner or an expanding history chain. Constructing a
new reservoir geometry catalog over continued material is not an implicit geographic admission.

Every pending request is prepared again using the identical reservation, source intervals, forcing
window, network, mapping, calibration, owner and terminal bindings. The capacity revision, closed
identities, receiver quantities and physical capacities remain unchanged. The new request and owner
hashes identify the new representation. Prepared requests and plans from before the cut are stale,
even when every aggregate quantity and the logical revision remain equal.

[Reservoir plans](../../src/OpenFantasy.Core/Geography/LiquidReservoirPlan.cs) apply the complete
capacity candidate with the same geometry. Actual and projected fill remain derived from the exact
raw/produced holdings and incoming obligations. Publication installs the whole candidate atomically.
Applying a plan to an immutable old copy does not publish it to a game world.

## Ordinary actions and material authority

`PlanReconcile` retains its ordinary successor rules: advancing custody, fixed admission, unchanged
Production capabilities and continuation context, and complete retained operation prefixes. It cannot
smuggle a history cut into an ordinary action. Crafting and consumption use their qualified planners
and permanent historical identity lookup; the capacity wrapper adds source-hold and aggregate
headroom checks before publication.

The [runoff domain](../../src/OpenFantasy.Core/Geography/WatershedRunoffDomain.cs) validates continued
Production against the actual inventory. Imported rights and direct raw retirement are validated by
the material basis and replay contracts. Historical source ranges are not fabricated into complete
retired ingredients with invented lot identities or revisions. Current segment retirement records
retain the original revision checks. Qualification preserves the already checked historical identity
roles and revision constraints that generic material compaction alone cannot reconstruct. Public
requests containing arbitrary continued Production refuse; the actual capacity owner's `CreateRequest`
prepares additional obligations in its fixed, qualified domain.

Canceled commitments release only incoming capacity. Their source reservations and closed identities
remain retained. Crafting or consuming held source stock still refuses. A valid material action can
also refuse physical publication if its combined occupancy would consume promised receiving room.

## Nested component archives

The explicit V3 runoff, runoff-capacity and liquid-reservoir codecs retain their existing finite field
and total byte bounds. Each level selects the continued interpretation, including the nested request
and Production frame. Legacy V1/V2 geography codecs retain their exact inline-history interpretation
and reject continued owners.

[GeographyProductionDependencies](../../src/OpenFantasy.Core/Geography/GeographyProductionDependencies.cs)
captures qualification from an actual continued capacity or reservoir owner and supplies its exact
continuation context and bounded opaque gathering/construction evidence to nested decoders. Reservoir
decoding requires reservoir qualification and the identical complete geometry catalog; capacity-only
dependencies cannot grant it. Production retains its own V3, V4 or V5 interpretation; geography V3 is not a downgrade to
Production V3. A context mismatch or missing required evidence refuses decoding. Decoders revalidate
funding, reroute pending runoff, reconstruct projected fills and require identical canonical bytes.

Supplied component dependencies are not durable archive authentication. Authentic retrieval of
closing owners, accepted source evidence and permanent command identities belongs to the integrating
authority and history store.

## Detailed placement and flow

[Liquid cells](liquid-cell-flow.md) retain exact raw source intervals and produced-lot intervals.
`Rebind` can attach the qualified continued reservoir when its domain, geometry and live holdings
match the retained portions. It does not move water, change its identity, advance the boundary or
increment placement revision. A cut changes the reservoir hash, making an earlier flow plan stale.

Normal downward and lateral flow then acts on the rebound state under the retained one-interval
rules. Production ancestry may move into a bounded material basis while its surviving water portions
continue to name the same live origins. Detailed placement remains incompatible with pending coarse
runoff. A material action that changes allocated holdings requires an explicit spatial disposition;
generic rebinding cannot relabel consumed or newly crafted water.

## Composition boundaries

These component contracts do not independently enable complete-world publication. Profiles1–26
reject continued material owners. The [coordinated profile27](coordinated-world-continuation.md)
qualifies its own Production V9 context through the actual jointly rebased reservoir and complete
world archive. Its internal geography dependency factory is distinct from standalone construction.
[Reserved gate transport](transport-material-continuation.md) selects its own V7 typed custody
transition through a qualified complete transport-owner cut and explicit nested V4 geography frames.
Independent capacity cuts cannot replace that transport boundary. The separate
[terrain continuation](terrain-material-continuation.md) contract records coarse/detail partitioning;
joining that interpretation with liquid ownership and excavation source/effort remains a complete
integration responsibility. Liquid V3 dependencies retain their selected Production V3/V4/V5 profiles;
they do not silently accept V6 terrain or V7 transport operations. Component continuation does not grant those
permissions or alter legacy world profiles.

A coordinated cut rebinds detailed cells without running another flow interval. Its complete-world
codec supplies the exact joint geography dependency; direct standalone dependency construction
refuses this capability. Subsequent world actions still reconcile current holdings and terrain and
apply the ordinary single selected time boundary.
