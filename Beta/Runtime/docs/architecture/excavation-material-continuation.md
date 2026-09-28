# Excavation across material history boundaries

## Joined ownership

[MaterialExcavationState](../../src/OpenFantasy.Core/Terrain/MaterialExcavationState.cs) joins the exact counted inventory and Production owner with excavation sites,
current spatial regions, shared Gathering effort and actor action frontiers. Initial capture requires an
actual admitted source world. A site's source entitlement remains distinct from its terrain ancestry:
neither capture nor a material cut grants extraction merely because counted capacity exists.

`PlanContinue` selects Production V8 and derives further material segments through the actual joined
owner. A cut preserves all quantities, site progress, source claims, terrain, effort, storage revisions
and action frontiers. It changes representation and invalidates plans pinned to the old complete state.
Generic Production, construction-only and geographic cuts cannot discard excavation qualification.

Ordinary crafting, recrafting and transfers reconcile through `PlanReconcile` with the same continuation
context and complete retained operation prefix. Exact excavation coverage is checked before publication
and before another cut. A newly captured old source world cannot accept the newer material owner as an
ordinary successor, even when partial work left inventory unchanged.

The original construction catalog, fabrication backing and geometric support remain retained facts.
Project-reserved raw and produced stock remain exact, including unfinished funding and installed
backing. Unrelated unreserved material can still move. Static backing does not authorize an independent
construction mutation outside the joined state.

Current source-world identities also remain occupied. Reconciliation checks introduced material
identities against those roles, including batch producers and already retired ingredient lots. A
permanent-history predicate supplements this current-owner check; it does not replace it.

## Work, shared effort and time

Each accepted mining swing is an ordered `MaterialExcavationWorkOperation`, including partial work.
Partial work spends effort and advances site progress and Production history without advancing custody,
storage or terrain revisions. Completion additionally removes the exact cell and issues its complete
counted-source interval in one lot; custody and the receiving storage advance once.

Mining and gathering share the same effort and action-opportunity owners. Joined gathering uses the
current component space and retains its genuine source-issued evidence inside the work operation.
Generic material gathering cannot act independently against an older source world's terrain or bypass
the shared opportunity check. Gathering source cursors never reset when mining begins or history is cut.

Fresh, actually committed source-world frames provide actor state and experienced time under the
captured admission. Their original region, fabrication and idle-source bindings must remain consistent;
unaccounted donor work, effort or source-cursor mutations refuse. The component advances its retained
Gathering state by exact idle recovery to the observed process interval, then spends the accepted work.
It does not borrow the donor's unspent effort over its own prior spending.

One shared frontier per actor prevents mining and gathering from consuming the same observed action
opportunity twice. Other actors may use the same global boundary, but no action may reverse the latest
observed world, advancing-step or process boundary. A rejected attempt consumes neither a frontier nor
effort. Representation cuts preserve these frontiers, including when no material units moved.

## Current terrain and exact source coverage

Current component regions survive source-world observations. Only compatible fresh actor observations
are combined with them; the donor's original solid cell cannot restore a completed hole. Actor contact,
tool reach, structure support and resource anchors are checked against that resulting space. An actor
pose valid over an old floor may be unsupported over the current hole and must refuse.

[MaterialExcavationPlanner](../../src/OpenFantasy.Core/Terrain/MaterialExcavationPlanner.cs) proves the
eventual complete opening before spending even partial labor, preserving the
[ordinary excavation contract](excavation.md). An incomplete site has a solid retained cell and no live
or directly retired claims. A complete site retains its excavated cell, with the exact partition:

`entitled source interval = live raw claims + imported direct retirement + current direct retirement`

Crafting extracted stock retires its original raw interval once. Recrafting a transformed output does
not retire those ancestral source units again. Completed holes and source ownership remain intact even
when no raw extracted stock survives. Material storage and lot limits apply to combined raw and produced
holdings; an unavailable final issue rolls back its work, effort and proposed opening together.

## Work evidence and component archives

Opaque [ExcavationWorkEvidence](../../src/OpenFantasy.Core/Terrain/ExcavationWorkEvidence.cs) binds actual command/frame and exact preceding material, work, terrain
and effort observations to the resulting transition. Ordered replay checks those dependencies and
reproduces partial work or exact source issuance. A genuine proof from another preceding Production
history cannot substitute merely because raw inventory quantities match.

The unchanged 512-operation active history bound applies to work as well as ordinary material actions.
A full window refuses before spending effort or changing progress. A qualified cut provides another
history window while preserving action timing; it cannot manufacture another action at the same boundary.
Permanent historical lookup protects both command and newly issued lot identities after old records leave
the active material window.

[ProductionCodecV8](../../src/OpenFantasy.Core/Production/ProductionCodecV8.cs) requires its actual
continuation context and matching opaque work evidence.
`MaterialExcavationCodecV1` additionally requires `ExcavationProductionDependencies` captured from the
actual joined owner. Complete-owner qualification binds nonmaterial facts as well as material replay.
Original excavation V1/V2, Production V1–V7 and complete-world archive interpretations retain their own
meanings. Unsupported interpretations refuse rather than drop the excavation contract.

These supplied component dependencies are not durable authentication. The host must retrieve and verify
actual closing worlds, work evidence and permanent identities before a complete-world continuation can
publish archive, receipt and head changes atomically.

## Complete-world boundaries

Pending consequence jobs, structural load/settlement policies, liquid ownership and encounter/care timing
have their own coupled publication rules. Capture refuses unjoined owners; it cannot remove their fields
to qualify a simpler world. Newly issued carried mass, changed hydraulic topology and a pending job's
original command require complete-world validation. A material cut cannot restart or discard that job.

Consumption capability retains its captured interpretation. The admitted dry excavation profiles carry
consumption-disabled Production; consumption-enabled care uses the coupled terrain/liquid and encounter
world. Extracted-material consumption across those histories therefore belongs to that combined protocol.
The standalone component does not silently enable consumption or provide a care-free source world.

Donor-only movement does not establish navigation into a newly excavated component cell while the donor
world still retains its original terrain. Live traversal, changed construction, wet excavation, structural
consequences and durable complete-world care/resource integration require the joined world binding.
