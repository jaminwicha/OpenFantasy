# Detailed liquid placement and flow

## Material ownership and actual placement

[LiquidCellState](../../src/OpenFantasy.Core/Geography/LiquidCellState.cs) partitions the live holdings
of one exact [reservoir owner](world-and-geography.md#domain-contract-finite-physical-reservoirs) across
whole cubic-metre cells. Custody and Production remain the sole material ledgers. A
[portion](../../src/OpenFantasy.Core/Geography/LiquidCellPortion.cs) names a vessel, XYZ cell, origin kind,
origin identity and positive half-open unit interval. Raw portions use absolute source intervals;
produced portions use offsets within an existing derived lot. Their disjoint union must exactly exhaust
the corresponding custodian's live holdings. Unknown, missing, duplicated or overfull claims reject.

The detailed domain contains every cell inside the fixed vessel geometries, including empty cells,
with at most 4,096 cells and 16,384 normalized portions. Adjacent intervals coalesce only in the same
cell and origin. Material volume per unit must divide 1,000,000,000 microlitres exactly; rates and amounts
use that indivisible unit. Cell quantity derives from its portions and cannot exceed one cubic metre.

Pending coarse runoff commitments cannot coexist with this detailed allocation. Closed runoff history
and existing source holds remain intact. The reservoir geometry and domain stay fixed during material
reconciliation. `Rebind` accepts an exact compatible material root only when all retained portions still
cover its live holdings. It cannot silently relabel raw stock as a newly crafted lot.

`ReservoirFill` describes the equilibrium distribution of aggregate stock. It is not the actual local
surface of a moving front. Detailed placement and volume come from the retained cell portions alone;
the Host's detailed-world diagnostics report these and suppress the equilibrium surface report.

## Conservative transfers

[LiquidCellPlanner](../../src/OpenFantasy.Core/Geography/LiquidCellPlanner.cs) prepares one selected
1,800-calendar-millisecond boundary. Downward proposals precede lateral proposals. Both spend the same
starting donor quantities and starting receiver free capacity. Incoming liquid cannot relay onward in
that step, and outgoing liquid cannot create extra receiving room in that step.

Downward flow is bounded by the retained per-face rate. Lateral flow uses half the starting quantity
difference under its per-face rate; an odd unit stays in place. Faces are cardinal, within one vessel
and canonically ordered by vessel and Z/Y/X address. Missing cells and adjacent other vessels are closed
boundaries. This is a finite cellular free-surface rule, not a pressure, pump or momentum solver.

Transfer allocation consumes canonical starting origin/range order. Incoming portions are retained
separately until all transfers have been selected, then normalized with the remaining portions. The
[plan](../../src/OpenFantasy.Core/Geography/LiquidCellPlan.cs) binds the complete starting component hash;
another placement, profile, material owner or clock makes it stale. A repeated boundary is idempotent;
past, partial or skipped boundaries reject. There is no accumulated flow allowance after a hold.

The final component must fit its fragment bound, and the complete world must fit its byte bound. A
failure throws before authority publication; it cannot truncate provenance, commit partial flow or
charge the selected time. The local session's failure path stops that session. Reopening preserves the
last committed state but does not cure an intrinsically excessive next step. Recoverable work slicing
requires an explicit continuation contract; neither silent stalling nor a larger arbitrary limit supplies it.

## Complete world, actions and clocks

[WorldLiquidCellAdmission](../../src/OpenFantasy.Core/Worlds/WorldLiquidCellAdmission.cs) creates a fresh
detailed world from an unplayed, unheld contained-reservoir world with the identical material owner and
calendar and liquid revision zero. [WorldLiquidCellBinding](../../src/OpenFantasy.Core/Worlds/WorldLiquidCellBinding.cs)
requires the component's boundary to equal the world calendar and its revision to equal advancing steps.
Ordinary successors retain this owner; opening an older save does not install it.

The retained sealing policy validates every complete root. Detailed flow additionally requires
terrain-supplied enclosure, with no construction supplying sealing faces. Nominal interiors stay clear
of bodies and installed solids. Excavation, scheduled reservoir transport, movable physical members and
structural owners are incompatible with this composition. These guards prevent unsupported pressure,
fluid loads, displacement and immersion from being admitted by an apparently valid capacity calculation.

[WorldStepProcessor](../../src/OpenFantasy.Core/Worlds/WorldStepProcessor.cs) reconciles each material
action through the reservoir and detailed partition before complete admission. A generic action that
changes allocated holdings refuses unless its spatial material effect is explicitly represented.
Refusal restores the attempted components and generated identities while preserving ordinary legal
movement and selected time. Unrelated material work can rebind without relocating the water.

Action preflight projects the eventual clock without flowing liquid. Actual flow runs once after actions
and the selected calendar increment, before movement. Opening a reading hold suppresses that interval's
flow while retaining its selected time. Held interaction and paused sessions advance neither boundary
nor placement. These local rules do not establish independent multiplayer encounter timing.

## Persistence and privileged inspection

[LiquidCellStateCodecV1](../../src/OpenFantasy.Core/Geography/LiquidCellStateCodecV1.cs) has a 1,400,000-byte
limit. Its seven-field frame contains tag, revision, calendar day, calendar milliseconds, reservoir hash,
flow profile and normalized rows. Each 80-byte row contains a 16-byte reservoir ID, signed-64 XYZ,
unsigned-64 origin kind, 16-byte origin ID and unsigned-64 start/end. Decode requires the exact reservoir
owner, complete invariant validation and identical canonical re-encoding.

[WorldSnapshotCodecV20](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV20.cs) extends the direct
contained-world root to nineteen fields, with liquid cells at index eighteen. The complete 16 MiB bound,
schema-three writer, journal identities and exact history checks remain mandatory. Its fresh admission
hash binds `world.liquid-cell-admission.v1`, the prior admission and complete cell hash.
[WorldStepCodecV20](../../src/OpenFantasy.Core/Worlds/WorldStepCodecV20.cs) retains the existing supported
action family while excluding runoff controls, scheduled gate controls, mining and assisted climbing.

The privileged Host operator is
`--create-liquid-cell-world <world.sqlite> <profile19-genesis.bin> <liquid-cells.bin>`.
Execution and recorded history use the existing authority. Reports expose the actual cell quantities,
volumes, intervals, calibration and boundary; they are diagnostics, not permitted player projections.

## Design obligations at adjoining boundaries

Mechanical ports and shutters require finite displaced volume, exact poses, swept clearance, effort and
actual transfer between custodians. Breaches and changing capacity require conserved spill outcomes;
fluid loads and occupied spaces require structural, displacement, buoyancy and exposure rules. Other
liquids, phase changes, reactions, ecology, coarse/detail exchange and temporal encounter coupling need
their own compatible state and publication contracts. Rendering and sound consume permitted committed
outcomes. A retained flow component alone does not provide those integrations.
