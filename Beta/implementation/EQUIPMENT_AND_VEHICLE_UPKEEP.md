# Equipment and vehicle upkeep roadmap

Owner clarification, 2026-10-02: include vehicle wear, damage and repair, and item, weapon, armor
and clothing wear, damage, mending and repair, with connected crafting options. The owner also requires attacks to directly target and damage
vehicles and mounts. Schedule this within
its existing domain phases; this clarification does not increase its priority or interrupt current work.

The preserved baseline already requires material aging and repair (RQ-093–095), customizable vehicles
with wear/maintenance (RQ-107), crafting processes (RQ-032/RQ-111), damage from exposure and events
(RQ-131), and cultural equipment/tool lineages (RQ-179). This living supplement makes personal
clothing, mending and the complete cross-domain acceptance scope explicit without editing the
[frozen requirements](../spec-v1_1/REQUIREMENTS.json) or
[production plan](../spec-v1_1/IMPLEMENTATION_PLAN.md).

## Coverage and sequence

| Phase | Required integration |
|---|---|
| P04 — Domestic ecology and production | Shared material/item condition and process foundations: use/exposure wear, event damage, preventive maintenance, repair and textile mending. Real tools, knowledge, materials, work, quality and outputs; contrast at least a torn garment and worn tool. Craft new replacements and repair existing objects through the same resource/custody rules. |
| P05 — Character, equipment and magic | Weapons, armor and clothing use those owners. Condition affects the appropriate protective, practical or functional capability, with readable equipped-item feedback. Tailoring, leatherwork, smithing and supported magical repair are distinct methods with declared capabilities and costs; ordinary healing does not repair equipment. |
| P06 — Transport and companions | Vehicle assembly and part wear, impact/environmental damage, inspection, upkeep, field patching where supported, workshop repair, part replacement and enhancement. Attacks can directly target vehicles and mounts under ordinary reach, visibility and ability rules. Damaged propulsion/hull/running gear affects its actual function, passengers, cargo and travel constraints; boarding or streaming cannot reset it. Mount wounds use actor anatomy, injury and care, not equipment repair. |
| P07 — Settlement economy | NPC repair/mending services, workshops, contracts, supply and labor use the same processes. Ownership, consent, custody and payment remain explicit; care of another person's property is not free access to their inventory. |
| P11 — Complete content | Reconcile applicable item, clothing, armor, weapon and vehicle-part families, cultural/material methods, recipes, tools, animation, icons/tooltips, sounds and historical condition cues. Record exceptions explicitly; no unsupported family is silently omitted. |
| P12–P14 — Balance and production | Qualify enjoyable upkeep frequency, long sessions, multiplayer/reconnect, save/history continuity, desktop and later mobile behavior. Keep normal early play approachable; advanced inspection and bulk/delegated maintenance arrive through appropriate capabilities. |

These rows refine existing acceptance dependencies, not promises that the systems are implemented.
P01–P03 foundations are extended when their actual consumers need the contracts; no speculative
universal durability framework or new parallel authority is required now.

## Shared model and player experience

Wear from use or elapsed environmental exposure differs from discrete damage (a cut, collision,
fire or corrosive liquid). Typed material and component condition owns each fact once; item and
vehicle assemblies derive their functionality from that condition. Permanent injury remains a body
contract, not equipment durability. Do not flatten every object into an identical depletion meter.

Repair restores the stated function without erasing identity, provenance, cultural design, enchantment
ownership or meaningful history. Mending can leave a patch or seam; replacement consumes and transfers
real parts and accounts for salvage/waste. Crafting, maintenance, enhancement and enchantment must
state their inputs, eligibility, work, resulting quality and interruption policy. Supported field fixes
and full restoration may differ. Repeated repair is not a source of free materials, duplicated items
or unearned skill rewards.

Use restrained visual/material cues and icons, with hover/focus tooltips for condition, functional
consequences and repair needs. Prefer useful notices and inspectable detail over constant modal
interruptions or a chore after every encounter. Crafting/repair menus reveal relevant learned methods;
advanced diagnosis and crew/workshop delegation can deepen later play. Balance rates from playtests,
not an invented compulsory timer. NPCs, players and unattended vehicles obey the same admitted rules.

## Direct attacks on transport and mounts

Vehicles and mounts are first-class attack targets alongside riders and crew, when an action can
legally reach, perceive and affect them. Target selection must distinguish the mount, rider, vessel
and any supported exposed component; attacking one does not automatically damage all of them.
Area effects and coupled impacts apply only their explicitly admitted affected set. Material resistance,
armor/cover and anatomy determine consequences through their existing owners.

A damaged wheel, sail, engine or hull can affect movement, control, protection or buoyancy where that
part is modeled. A wounded, incapacitated or dead mount affects its rider through explicit attachment,
dismount/fall and recovery rules. Preserve occupants, cargo, possessions and aftermath; disabling or
destroying transport is not deletion. Resolve failures through the ordinary spatial/fluid/support and
actor lifecycle systems, including held combat and streaming boundaries. Introduce these rules with
P05 targeting/damage contracts and complete their vehicle/mount integration in P06.

## Required proofs

- Garment damage → inspect → mend using owned thread/tool and actual work → changed condition and
  visible repair history; armor/weapon repair contrasts material, method and affected function.
- Vehicle part wear and collision damage → functional travel consequence → lawful repair/replacement
  with retained passengers, cargo, customization, part identity and resource accounting.
- Rain, corrosion and other supported exposure obey applicable simulation clocks, holds, shelter and
  material rules; loading, fast travel or reconnect neither grants a free repair nor duplicates wear.
- Direct attacks distinguish rider/mount and crew/vehicle, enforce reach/cover/eligibility, and apply
  appropriate material damage or bodily injury. Disablement, wreck or mount incapacitation resolves
  occupancy, rider consequences, cargo and survival without duplication or silent disappearance.
- Interrupted work, duplicate requests, competing repairs, full inventory and missing inputs preserve
  atomic costs/custody and produce explicit outcomes. Recovery/replay reproduces exact condition.
- Actor parity, NPC services and delegated upkeep retain consent, payment, materials and time.
- Native art/animation/audio communicates actual condition and action without confusing cosmetic age
  with functional damage. Early-game and long-session playtests reject tedious upkeep or menu clutter.

No gate is closed by this roadmap. New domain implementations must bind these proofs to their retained
phase evidence alongside the baseline tests and content reconciliation.
