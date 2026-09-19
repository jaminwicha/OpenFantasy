# Ordinary stairs and multi-level routes

This increment proves ordinary walking and bounded NPC routing across actual completed construction,
including an occupied upper floor with a usable underpass. It advances the multi-level construction
dependency in the [full plan](../../../spec-v1_1/IMPLEMENTATION_PLAN.md); it does not close P01/P02.

## Retained behavior

Fresh dry profile twelve explicitly selects bounded mixed-height contact. Ordinary XY movement derives
its feet elevation from retained terrain and completed members, with full-body sweeps and whole-footprint
support. A step is at most 250 mm and at most one quarter of body height. It requires ground locomotion,
without hands or climb effort. Larger assisted climbs keep their separate action. Older profiles retain
their original rules and bytes; there is no played-save migration.

Routes search the actual 32-subunit ordinary movement graph, retaining XYZ. Deterministic forward/reverse
frontiers share 2,048 default expansions, with 4,096 maximum and a separate transition-work cap of sixteen
times the chosen node budget. Reverse predecessors must replay exactly through the forward movement
kernel. First verified meeting does not promise a shortest path. Missing dependencies, incompatible XY
residues and exhausted budgets retain explicit refusal outcomes. Construction access selects the matching
profile budget; earlier profiles retain 256. The NPC controller uses these routes and recognizes a different
home elevation even at the same XY.

The [architecture reference](../../../Runtime/docs/architecture/stair-navigation.md) describes contact,
directed routes, persistence and separate interaction restrictions. The direct 17-field root restores
completed construction before admitting actors. Spatial version three and world/step profile twelve
select this interpretation; the existing 16 MiB writer limit remains unchanged.

## Executed fixture

The retained fixture authors a fresh nine-tread assembly and upper deck, then earns its completion through
the existing six-wood quest, actual NPC movement/work, custody reservation and material production. It
does not inject installed geometry, completed work or final feet. The recipe's synthetic mass/volume
calibration isolates funded navigation from unimplemented structural-strength/material-balance design;
the one-unit work target is fixture calibration, not production construction balance.

Each tread rises 250 mm and runs 875 mm. The deck is 2.25 m above the floor, with 2 m of clear space beneath it
for the actual 1.8125 m body. All 12 member bearing faces derive from the completed definition. Both actors
visit every standing height from 256 to 832 subunits in both directions through ordinary samples.

The 585-commit journey retains these exact states:

| Revision | Evidence |
| --- | --- |
| 0 | Unfunded fresh genesis; upper deck is unreachable |
| 63 | Quest-funded assembly installed by the NPC |
| 134/135 | Closed/reopened midway through the stairs; ordinary rise 512→576 at actual XY |
| 177/249 | Player reaches the upper deck and returns downstairs |
| 425 | Player below and NPC above at identical XY, with exclusive clear body volumes |
| 585 | After the player clears home, the NPC controller routes downstairs to the same XY at ground height |

Every movement asserts one actor revision, one advancing/calendar step, effort recovery only, unchanged
construction/inventory, and the actual movement kernel's endpoint. Reverse historical reads reconstruct
all eight retained boundaries without changing the closed database. Automatic return uses the ordinary
controller; no scripted waypoint was added to avoid the route failure described below.

## Qualification

[Machine-readable summary](qualification/verification-summary.json) links the retained reports:

- Full solution build: zero warnings/errors. Full managed regression: **1,189 passed, zero failed/skipped**
  (763 Core + 426 Hosting; 16 new cases).
- New profile: 20 actual Host calls and two external process kills at the before/after-commit boundaries;
  exact retry/history behavior and nine independent byte witnesses for feet, effort, material state and
  unchanged receipt ledger. Ordinary movement changes no action receipt.
- Previous profile-eleven fixture: 26 actual Host calls and two process kills using the extended checker,
  preserving the old climb and suppressed-movement checks.
- All seven closed fixture files reproduce byte-for-byte in the full regression, including the final
  journey database and the mid-stair transition.
- Actual Godot headless foundation and native farm create/reopen/history pass. These exercise the shared
  adapter and prior playable farm; they are not rendered stair, physical-device or art acceptance.
- 255 architecture links resolve; 22 frozen specification hashes and the exact 19-file predecessor index
  are unchanged. Runtime naming and Python AST checks pass. The all-systems verifier was not run; its new
  fixture/checker hook was wired, and the new checker ran directly.

## Failures and independent review

All failed attempts remain in `qualification/`:

1. `test-first.log` records the expected missing-type test-first compile failure.
2. `core-focused.log`/TRX records an incorrect new assertion expecting a 36 m legacy breadth-first route to
   fit 256 expansions. The existing limit was preserved; the corrected regression proves its explicit
   `AccessLimit`, a successful nearby legacy route, and profile-twelve default/explicit budget behavior.
3. `hosting-stairs-01.log`/TRX records the NPC's direct work-to-upper-deck route exhausting 2,048 nodes.
   The entrance lies away from the destination's XY. Alternating directed frontiers resolved it with the
   same budget and unchanged fixture. `hosting-stairs-02` and the full regression pass all original gates.

A bounded native source critic found the construction-access budget mismatch and reviewed the route
adaptation. It identified that a blocked unshifted predecessor could conceal unloaded support at a lower
height. Support-plane enumeration now propagates missing cells directly. The regression includes a low
beam, clear body layer and omitted support column, proving `Unavailable` rather than false `Blocked`.
Additional cases cover unexpanded-frontier meetings, missing corridors, closed walls, exact waypoint
replay, numeric limits, unsupported hover, narrow/high stairs, quantized tops and head-clearance sweeps.
No acceptance threshold/deadline was relaxed and no external Claude/Fable call was used.

## Remaining full-design work

This is authored broad-tread geometry and a finite local route owner, not general generated buildings or
production navigation throughput. Load strength, damage/removal, stacked construction and deterministic
collapse remain open. The occupied-floor tunnel/removal scenario still needs its complete structural
consequences. Elevated work sites/resource anchors, cross-floor interaction, ramps/ladders/flight,
ability-specific routes, actor crowding and streamed route dependencies need their own contracts.

The default client remains the existing farm. Real multi-floor presentation, action offers, controls,
choreography, art and human acceptance are still required. Dry excavation/construction and wet reservoir
owners remain exclusive until sealing, openings, displaced volume and conserved spill composition are
qualified. All 181 requirements, remaining UI cases and full campaign through P14 remain in scope.
