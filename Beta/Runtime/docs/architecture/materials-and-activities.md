# Materials and activities

## Domain contract: custody and provenance

[Custody](../../src/OpenFantasy.Core/Custody) represents counted sources, source intervals, lots,
custodians, storage and transfer plans. A lot is a claim on actual units, not just an item label and
quantity. Splitting or moving stock preserves source coverage and exclusive custody. Reservations
restrict use without creating another copy. Definition/version pins determine which material an
interval represents.

[Production](../../src/OpenFantasy.Core/Production) distinguishes live transformed output from retired
ingredients and preserves input rights through transformations. Recipes declare typed mass/volume,
output allocation, waste and byproducts. A crafted output cannot still leave its consumed ingredients
spendable, and byproducts cannot create additional ancestral rights. Physical binding connects an
item definition to material measures; counted units, milligrams and volume are not substituted by
unchecked integer arithmetic.

The standalone [consented custody authority](consented-custody.md) composes the existing counted
movement planner with both owners' exact, independently authenticated approvals. Its candidate
preserves source intervals and access policies. Produced goods, complete-world capacity, material
history and durable publication require their own joined binding; this authority does not provide
an asynchronous gift or a market by itself.

The standalone [item-consumption owner](item-consumption.md) adds explicit retained dispositions for
actual raw or produced goods. Its planner validates owned stock and prepares coupled inventory and
production candidates; complete output partitions prevent reintroducing consumed material. Raw source
bounds and disjointness still require a complete authority's issuance/history join. Production V2
retains these claims. Profiles through 24 reject the enabled owner; the explicit
[aftermath-care composition](aftermath-care.md) joins it to treatment and resource evidence in
in-memory complete-world transactions. The component grants no health effect, recipient permission,
time or durable publication.

[Ordered material continuation](production-continuation.md) composes actual gathering issuance, raw
transfers and reservation-bearing source delivery with crafting and consumption under one bounded
history. Its source bridge requires the actual sequential gathering owner and preserves exact rights,
operation order and revisions across material cuts. These standalone adapters do not replace complete
world, terrain, care or durable archive admission.

[Funded terrain material](world-and-geography.md#domain-contract-funded-terrain-material) reuses this
counted authority for an explicitly finite homogeneous slab. Detail changes partition existing source
intervals between coarse and cell-bound custodians; quantities remain derived from actual lots. A
transfer to another custodian remains owned there when detail is demoted. The standalone material
state requires continuous coverage of its original interval. The explicit
[production composition](world-and-geography.md#domain-contract-terrain-and-production-composition)
covers consumed intervals with retained direct raw retirement, counting them once through further
transformations. Raw-only archives retain their stricter live-coverage contract. This representation
planner grants neither mining labor nor extraction permission.

The [shared allocation registry](world-and-geography.md#domain-contract-shared-material-allocation-registry)
checks spatial and source exclusivity across multiple slabs while retaining one actual counted
inventory. A transfer remains attributed to its original allocation's source interval even when the
receiving storage is another allocation's coarse custodian. Shared detail and storage limits apply to
all allocations together; successful validation of separate slab states alone cannot establish that.
Attached production participates in combined storage/lot limits and identity/calibration checks.
Crafting and item consumption retire stock outside representation custody and retain their history
alongside the allocation views. [Composed material evidence](item-consumption.md#composed-material-evidence)
counts direct raw retirements once and preserves exact prior batches, consumption claims and recipe
admission. Reconciliation cannot restore consumed source rights by replacing the proof.

[Funded runoff budgets](world-and-geography.md#domain-contract-funded-runoff-budgets) use existing
reserved raw source intervals and exact volume calibration to prepare finite downstream obligations.
The complete source reservation is accounted for, and all terminal arrivals sharing one custodian
compete for its counted and physical headroom, including existing crafted contents. The budget is a
read-only projection; live flow still needs owned capacity reservations, source/buffer storage and
staged transfers. Neither a contributing-column count nor a graphical ocean plane creates water.

## Runtime binding: one connected activity chain

| Operation | Concrete owner | Important checks and effects |
| --- | --- | --- |
| Excavate | [ExcavationPlanner](../../src/OpenFantasy.Core/Terrain/ExcavationPlanner.cs) | Owned cell permission, exact tool/work/reach, direct support and atomic whole-cell issuance/removal; see [contract](excavation.md) |
| Gather | [GatherPlanner](../../src/OpenFantasy.Core/Gathering/GatherPlanner.cs) | Owned suitable tool, body/reach, node revision, earned effort, finite source and issued yield |
| Craft | [InstantCraftPlanner](../../src/OpenFantasy.Core/Production/InstantCraftPlanner.cs) | Actual eligible input lots, protection/reservation, exact recipe, output allocation and retained consumption |
| Accept/deliver | [DeliveryQuestPlanner](../../src/OpenFantasy.Core/Quests/DeliveryQuestPlanner.cs) | Addressed agreement, owned stock, real funding, durable participation, once-only delivery/reward |
| Prepare reward | [ActorRewardPlanner](../../src/OpenFantasy.Core/Progression/ActorRewardPlanner.cs) | Validate admitted progression/currency definitions and funding; calculate candidate state |
| Build | [ConstructionPlanner](../../src/OpenFantasy.Core/Construction/ConstructionPlanner.cs) | Builder/tool/work eligibility, reserved material, physical access and atomic installed geometry |
| Climb | [WorldClimbResolver](../../src/OpenFantasy.Core/Worlds/WorldClimbResolver.cs) | Actual movement/manipulation capability, swept clearance, continuous projected support and shared earned effort |
| Walk stairs | [Ordinary walking](stair-navigation.md) | Actual tread contact and full-body clearance; one existing movement sample, no climb effort or hand requirement |

The farm admission connects finite oak/berry gathering, ration crafting and Wren's six-wood agreement.
Its funded reward is 420 XP and 200 gil. Delivered wood remains reserved for Wren's seedling guard;
200 accepted work actions complete an installed guard and three offcuts. These constants belong to
the admitted content profile, not universal quest or construction rules.

The installed guard is a reserved live transformed lot bound to a physical structure. Completion
retires the consumed material and installs derived collision in one world commit; it does not create
a display prop plus a separately spendable item. Blocked routes, missing tools or occupied geometry
defer work. Dialogue holds and unreadiness grant no work. The construction recipe is rejected by the
ordinary instant-craft path, preventing it from bypassing required labor.

Construction placement qualifies required escape connections, including connections from current actor
positions, on every paid step. Exact unchanged geometry may reuse its admitted result; changed dependencies
require a fresh ordinary qualification. A worker's absence from rendering does not waive this geometry
or make a time-only completion lawful. The [prepared route boundary](world-and-geography.md#routes-over-bounded-prepared-reads)
provides the shared search over exact selected geometry with an explicit availability boundary.
[Shared construction execution](construction-execution.md) runs those work and material rules through
live adapters or an exact detached preparation. Retained site evidence can avoid repeating unchanged
geometry. The ordinary world processor revalidates that evidence against its running action state and
still owns earned time, physical admission and publication. Detached evidence alone is not an admitted
world, earned clock, physical-load result or durable work receipt.

[Prepared placement](world-and-geography.md#prepared-construction-geometry) combines prospective
structure clearance/support with those required routes, using the same ordinary placement queries.
It returns geometric status only. Neither boundary replaces `ConstructionPlanner`, complete candidate
admission, funding, tools, actual exposure or identity/capacity checks. Offscreen execution must join those dependencies and revalidate affected
access after interventions; a retained reachable route is not perpetual permission to build.

`ConstructionSnapshot.ValidateComponents` requires every same-floor actor in its complete supplied
locality to appear in the project's access obligations. `ConstructionProject` bounds the stored
requirements plus distinct actor/destination pairs to 32; one witness per distinct actor therefore
allows at most 16 such actors. This is an admission limit of that interpretation, not a population
limit for the game's design. A selected occupancy read cannot silently shrink the required population.
A different protected-access-domain law requires an explicit interpretation while historical admission
retains its original meaning.

Funded work needs earned simulation exposure, actual stock and tool rights, valid actor/control state
and complete relevant physical consequences. Retained spatial evidence may avoid repeating unchanged
geometry only when its exact dependencies are revalidated. It cannot award elapsed wall-clock work,
ignore a reading hold or install an object without the same material, identity and load admission.

The explicit [constructed-footing composition](constructed-footing.md) derives actor-bearing surfaces
from those same completed physical members. Actor support is exact in XY; it does not establish load
strength or allow the construction geometry to exist before its work and material publication.

[Coupled intact settlement](coupled-settlement.md) lets explicitly admitted assemblies bear on one
another while retaining separate project, installed-lot and member identities. The complete owned load
policy partitions contact without adding a second aggregate assembly mass. Builder-carried raw stock
and byproducts remain actor loads; installed backing contributes through the exact member allocation
once. Changing support cannot turn that backing into spendable stock or silently hide carried mass.

An eligible excavating action retains its earned work, source interval, physical members and occupants
under one [reserved consequence owner](consequence-continuation.md). The bounded group moves together;
final material issuance and placement share one publication. A failed geometric or load result cannot
leave partially moved members or an independently issued mining reward. Unequal motion, fracture,
debris recovery, harmful impacts and general repair require their own conserved transitions.

Exact action retries return their durable outcomes. Repeated delivery or completion cannot pay again.
The quest participation checks and authority transaction/receipts enforce that once-only boundary;
the pure reward planner does not persist acknowledgement or prevent repeated use on its own.
Inventory projection and recipe inspection confer no permission to consume, equip or transfer stock.

[Reservoir boundary assessment](reservoir-boundaries.md) reads completed projects' exact installed
output material and millimetre members. It tests continuous sealing thickness and face coverage under
an explicit policy; incomplete projects, input stock and rounded collision boxes cannot supply a seal.
This derived inspection does not introduce a separate wall inventory or assert pressure strength.

## Design contract: process graphs and learned powers

Crafting, smithing, cooking, herbalism, alchemy, knitting, research and vehicle building share a typed
process vocabulary: ingredient predicates, tools/catalysts, station, knowledge, worker roles, time,
environment, outputs, quality, waste and interruption. Jobs reserve real stock, capacity and labor.
Substitutions explain their properties; accepted discoveries become immutable versioned definitions.
Graph depth, emitted work and affected targets are bounded; large effects use staged region work.

Magic defines school, elemental facets, delivery, target rules, costs, concentration, duration,
stacking and resistance. Illusion affects eligible perception; invocation produces declared effects.
Spellbinding, weapon enhancement, lingering areas and researched combinations use owned effect
instances. Removing an effect removes its own contribution, not unrelated later changes. Spell
research can reuse process validation without pretending magical effects are material quantities.

Fishing, hunting, writing, acting, acrobatics and other professions have concrete affordances and
results. Skill use can satisfy puzzle or narrative predicates through ordinary actions. A skill name
alone is insufficient integration; its resources, legality, learning and performance cues must agree.

The standalone [structural load model](structural-loads.md) binds exact completed members and actor
loads to explicitly rated supports. Its bounded failure plan conserves mass and can resume canonically;
it does not change physical placement, retire material or publish collapse. Load survival and complete
spatial footing are separate obligations.

[Physical construction](construction-physical.md) separately retains each completed member's condition
and placement, preserving original work and material provenance. Its owner joins the ordinary durable
completion transaction; supplied physical changes are not automatic collapse or actor damage actions.

## Design contract: construction, condition and management

Rooms, floors, subfloors, tunnels, sewers, castles, crypts and towns occupy real terrain and structures.
Mining respects hardness, tools, labor, support and conserved material. Building or excavation can
change access, fluids, hazards, ownership and encounters. Fortress management is a way to delegate
ordinary jobs within that same world, not a separate simulation mode.

Condition responds to elapsed exposure and discrete events. Wetness, corrosion, temperature, fatigue
and damage have material-specific ownership and fractional continuation. A strike and subsequent
burning may both contribute only under separately defined causes. Repair consumes work and material
and restores declared function while retaining provenance and age. Appearance reflects these facts.

Management policies define stock filters, minima/maxima, protected goods, capacity, priorities,
shifts, roles and delegates. They generate feasible jobs under real mandates and commitments.
Changed stock or labor causes revalidation, not duplicated orders or invented workers.

## Design contract: markets, transport and care

Markets price actual stock against local demand and supply evidence using versioned bounded rules.
Quotes bind issuer, quantity, revision, fees and expiry; requesting a quote reserves nothing.
Acceptance atomically validates allowance, goods, money and rights. Price updates follow economic
events or scheduled intervals, never UI polling. Known prices and forecasts are scoped observations,
not free global market truth.

Caravans carry real cargo and travelers with routes, wages, guard obligations and risk. Buying in one
town and selling in another is legitimate when transport and total costs permit a margin. Delays,
theft, damage and losses affect actual contracts, relationships and prices.

Vehicles are assemblies with sockets, parts, condition, propulsion, steering, capacity and crew roles.
Boats, canoes, ships, carriages, chariots, tanks, sandships, tunneling craft, balloons and airships use
appropriate profiles. Tunneling spends resources and excavates terrain; it cannot ignore solid space.
Boarding, mounting and towing preserve one attachment relation, cargo owner and motion frame.

Mounts, pets and familiars are actors with diet, fatigue, care, training, bond, injury and agency.
Feeding consumes actual food; delegated care reserves an available caregiver and supplies. Dismissal
and recall use routes, habitats or supported magical transit. Death, transformation and resummoning
preserve identity and possessions rather than spawning a convenient replacement.

See [time](time-and-encounters.md) for work/rest exposure,
[world geometry](world-and-geography.md) for terrain intervention, and the
[technical design](../../../spec-v1_1/TECHNICAL_DESIGN.md) for graph, economic and assembly rules.

Receiving obligations also constrain custody publication through the
[runoff capacity owner](world-and-geography.md#domain-contract-shared-runoff-capacity-ownership).
A transfer or craft candidate may satisfy ordinary storage rules yet exceed physical headroom already
held for inflows. Reconciliation counts raw and derived occupancy with all pending obligations before
publishing the whole composed state. Canceling a capacity hold leaves actual source reservation metadata
unchanged; releasing stock is a separate custody operation.

[Physical reservoir composition](world-and-geography.md#domain-contract-finite-physical-reservoirs)
adds geometric volume limits to mapped sources, buffers and receivers. It counts all raw/derived goods
once under one exact incompressible-material calibration. Equilibrium column fills are projections of
that custody; they do not issue per-cell source claims or supply an unlimited waiting-flow buffer.

## Applicable process intervals

The [coordinated world pacing owner](world-pacing.md) distinguishes advancing batches from earned
1,800 ms passive-process intervals. Gathering validation, recovery and public regrowth visibility use
`WorldSnapshot.ProcessIntervals`; legacy worlds retain their advancing-step interpretation. Tactical
ticks accumulate fractional exposure rather than granting repeated exploration allowances. Construction
and excavation still require their actual actions and earn no labor from that passive carry.

Both complete-world admission and `GatherPlanner` validate gathering's snapshot boundary against
that process clock. Advancing-batch count alone cannot validate a gathering action after an encounter,
because tactical elapsed time can complete additional process intervals. Returning to exploration
preserves earned effort and finite source progress without resetting either clock or granting extra
recovery. Worlds without pacing obtain their process count from the advancing-step fallback.

`GatheringSnapshot.AdvanceTo` computes the exact result of idle process intervals without iterating
every interval. It saturates effort and applies eligible regrowth without resetting issuance cursors.
This is an idle transition, not a substitute for recorded harvesting, climbing or excavation effort.
The material continuation bridge compares this reconstruction with the actual supplied gathering
owner and refuses unrepresented changes. Advancing to a time cannot overwrite actual spending.

## Quoted goods and payment

[Atomic spot exchange](spot-exchange.md) is a standalone domain joining the existing inventory,
production and progression owners with a quote book. Both participants approve exact goods,
price and fees; settlement preserves finite money and material provenance as one candidate.
Explicit V12 ordered material history records genuine paid movement evidence for the standalone
contract. The [paid world binding](world-paid-exchange.md) shares terms and transfer arithmetic while
owning its own quotes and typed payment causes. It couples actual control, reach, readiness, both
participants' work opportunity, progression and material history, joint cuts and SQL publication.
Its production continuation uses V11. Neither binding supplies a stock/demand price model, NPC
quoting policy or native trade interface.
