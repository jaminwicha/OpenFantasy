# Local watercourse

## Content and authority

[WatercourseFarmAdmission](../../src/OpenFantasy.Hosting/Content/WatercourseFarmAdmission.cs) composes
an explicit fresh world from the ordinary farm's gathering, production, quest and construction owners,
then admits fixed containment and detailed cell placement. [The builder](../../src/OpenFantasy.Hosting/Content/WatercourseFarmBuilder.cs)
pins the complete initial terrain and source candidate. Existing worlds do not participate in this
selection. The application argument `--watercourse` opens its own save; world, branch, profile and
admission identity must match when reopening. No implicit farm/worksite migration occurs.

[The layout](../../src/OpenFantasy.Hosting/Content/WatercourseFarmLayout.cs) defines a stepped channel
and lower pool east of the existing activity. Known substrate under the original region uses the same
seed, scope and terrain rules. Initial holes are authored geography, not an executed excavation. All
nominal sealing faces are actual terrain. Actors and construction remain outside the complete liquid
envelope, including cells that currently contain no water.

[WatercourseFarmWater](../../src/OpenFantasy.Hosting/Content/WatercourseFarmWater.cs) admits one finite
6,000-litre source entitlement and matching actual custody lot. Six upper cells start with 1,000 litres
each. A dry canceled runoff commitment provides the existing capacity domain without any live incoming
water promise. Downward transfers are capped at 25 litres per face and lateral transfers at four litres
per face per selected step. The detailed planner owns shared starting budgets and exact source intervals.
No camera change, save reopen or visual animation refills the water. The finite stock drains and settles.

Ordinary movement, tools, crafting, conversation and funded NPC construction retain their existing
owners. A reading hold freezes detailed flow; local pause stops session readiness. Resume earns only
ordinary selected steps. This content grants no swimming, water-use action, gate operation or excavation
inside the vessel. Construction also retains its bounded access proof from every current actor position
to the authored exits. A distant bank can exceed that search budget and withhold work even when a route
exists. Returning through ordinary walking to a provable approach permits the same funded work to resume;
no stock or work is consumed by a withheld proposal. The route limit is not evidence of a physical blockage.
[Detailed flow](liquid-cell-flow.md) defines the conserved authority and its exclusions.

## Public observation

[LocalLiquidView](../../src/OpenFantasy.Hosting/Simulation/LocalLiquidView.cs), reached through
[LocalWorldView](../../src/OpenFantasy.Hosting/Simulation/LocalWorldView.cs), exposes committed occupied
cell coordinates, aggregate geometric microlitres and known terrain occupancy in the admitted
sight-complete locality. A whole cell has one billion microlitres. Source intervals, lot identities,
reservoir/custodian identities, private inventories and authority hashes are excluded. Host diagnostics
are not a presentation interface. Missing terrain outside supplied bounds is unknown.

Each occupied cell retains its own Z and fractional volume. The view does not derive a single column
surface from total stock: transient upper and lower portions can be separated. The complete-world
material partition remains the sole liquid inventory; the public geometry is an immutable projection.

## Excavation composition

The [terrain-bound breach](terrain-liquid-breach.md) shares this liquid projection while also exposing
owned work targets through `LocalWorksiteView`. The separate `--breach` entry opens
`user://worlds/wrens-breach.sqlite` using the same strict world, branch, admission and profile selection.
It neither upgrades another farm save nor introduces another simulation owner. Public work, terrain and
liquid observations come from the same committed world.

Partial work retains the solid divider and dry receiver. On earned completion the next projection shows
the removed cell, the issued substrate in the player's pack and actual subsequent liquid transfers.
Selecting the divider captures its exact work/tool/revision inputs through the ordinary wheel. This
selection admits Mine but no assisted Climb. The completed target retains its inspectable completion
blocker; neither inspection nor cosmetic water animation submits a command.

## Native presentation

[WatercourseView](../../client/scripts/Presentation/WatercourseView.cs) draws an open-channel oblique
cutaway using the same 32-pixel/metre projection as actors, targets and the camera. This selection uses
integer one-times zoom and a southward camera offset (72 pixels for the watercourse,
96 for the deeper breach receiver) to keep the channel between the existing HUD panels. Known floor and rear
bank geometry come from occupancy. The front earth is omitted to reveal lower layers. A proved dry
support tile is restored under an actor where its ground plane overlaps the cutaway, avoiding a false
immersion cue. This is a bounded open-channel view, not a general cave, overhang or underwater renderer.

Horizontal surfaces use [a native editable asset](../../art-source/watercourse/water-surface-calibration-v1.aseprite)
and its [exported sheet](../../client/assets/watercourse/water-surface.png). Two variants each have four
frames. Placement and vertical fill derive from committed quantities; cosmetic frames derive from the
public calendar and stop under its holds. Ripple animation conveys neither replenishment nor a measured
velocity. Nearest filtering preserves pixels. Render nodes cannot change world state.

When liquid and worksite observations coexist, the liquid renderer owns terrain and the worksite's
duplicate terrain layer is hidden. Physical members keep their independent renderer. Ordinary completed
guard sprites remain visible whenever there is no physical-construction owner. A raised-bank target's
unfilled boundary and selected marker identify the solid cell top, then move to the removed cell's
base when actual occupancy is empty; its authored dry stance remains the action anchor. The boundary
draws no lid, speculative fracture or water. The opened target has an explicit completion label. HUD
hints follow the traversal/physical-construction capabilities instead of treating all excavation as a
stair worksite. These cues are interface annotations, not physical material.

The native kit is calibration material. File integrity, runtime projection, visual review and owner art
acceptance are distinct claims. Terrain cutaways and inherited farm assets retain their own presentation
limitations; the full style, animation, sound and device contracts apply independently.
