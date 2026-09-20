# Commit and milestone doctrine

Owner direction, 2026-09-10: commit implementation progress along the way and create milestone
branches to record major progress. This is shared project discipline for all authorized agents.

- Commit coherent completed increments with the relevant checks and a useful description.
  Do not wait until a large campaign is finished to preserve working progress.
- At a reviewed vertical slice, major integration or other meaningful accepted checkpoint,
  create a `codex/milestone-<descriptive-name>` branch at the exact qualified commit. Record
  the commit, scope, evidence and remaining limits here. This names an existing commit; it
  does not switch the working checkout or include uncommitted work.
- Keep milestone branches as historical markers. Do not advance or force-move an existing
  milestone to a later state; create a new descriptive milestone when progress warrants it.
- Continue daily work on the active development branch. Milestone creation does not require
  pushing, merging or publishing. Those actions follow their own authorization and scope.
- The integration lead owns commits and milestone creation when workers share a checkout.
  Workers return bounded edits/evidence; preserve unrelated staged files and never stage all.
- Passing a bounded checkpoint does not close a phase, production gate, art acceptance or
  human playtest requirement beyond the evidence recorded for that checkpoint.

## Recorded milestones

| Branch | Commit | Qualified scope and limits |
|---|---|---|
| `codex/milestone-playable-worksite` | `7da8fcc` | Two genuinely funded projects, stairs/decks, contextual mining/climbing, exact visible settlement and four-process native save/recovery with unchanged deadlines; 1,487 managed cases and final legacy wheel control pass. Pure-C# replay improvements preserve all 1,334 historical hashes. [Evidence](p02/playable-worksite/README.md) retains failures, source review and measured startup/allocation limits. Provisional art/UX, OS/devices, multiplayer, wet composition, production history/performance and full phases remain open. |
| `codex/milestone-live-event-descent` | `e668cd7` | Fresh profile18 live intact event settlement and exact durable continuation/publication; one-pass closed-save historical reader preserves complete verification. 1,459 distinct retained/new tests, 83 Host calls/six external kills plus two exact operator controls, legacy fixture/source/spec/index preservation. [Evidence](p02/live-event-descent/README.md) retains the original timeout and measured roughly 50% allocation reduction. Common 256-subunit live fall only; unequal release, fracture/injury, visual/device and production/full-phase gates remain open. |
| `codex/milestone-event-descent-assessment` | `25da978` | Event-ordered unequal intact geometric descents, stopped-body reinsertion and complete-world read-only assessment with exact impact demand. 1,435 distinct tests, eight actual Host inspections, unchanged save bytes and bounded partial Fable critique. [Evidence](p02/event-descent/README.md). No live profile change, fracture/debris/injury, production latency/rendering or full-phase closure. |
| `codex/milestone-coupled-settlement` | `463509d` | Fresh profile17 live cross-assembly support and one shared intact harmless trajectory, complete outside sweeps, rider/material conservation and dynamic durable/journal reservations. 1,409 tests, 59 ordinary Host calls/six kills, headless foundation/farm, 20 new/26 legacy exact files. [Evidence](p02/coupled-settlement/README.md) retains the reproduced admission defect and partial external review. Unequal trajectories, fracture/impact/injury, wet composition, presentation, production performance and full gates remain open. |
| `codex/milestone-coupled-contact-assessment` | `7e16d11` | Read-only exact foreign-assembly support, complete candidate/policy pins, shared mass accounting and conservative load neighborhoods; 1,385 tests, 26 byte-exact prior fixtures and preserved source/design/index contracts. [Evidence](p02/coupled-contact-assessment/README.md). Live coupled motion/publication and whole phase gates remain open. Three bounded external critics yielded a contract clarification, no verified runtime defect and one budget failure; fleet expansion remains evidence-dependent. |
| `codex/milestone-farm-input-foundation` | `b1482db` | Connected farm/gather/craft/quest/NPC construction and client input foundation; 825 tests and retained native journeys. [Evidence](p01/input-focus/README.md). Provisional art, general UI, physical device/export qualification and full phase exits remain open. |
| `codex/milestone-action-previews` | `b40f28c` | Captured farm action offers, visible costs and read-only inspection; code `3ae7855`, 832 tests, all retained native journeys and rendered review. [Evidence](p01/action-offers/README.md). Full wheels/management, physical devices, art acceptance and phase exits remain open. |
| `codex/milestone-explicit-targeting` | `747925f` | Explicit Wren/harvestable selection, stable target identity and incompatible-verb blocking; code `c7e29f5`, 838 tests, all retained native journeys and eight rendered states. [Evidence](p01/target-selection/README.md). General pointer targeting/confirmation, full UI, physical devices, art and phase exits remain open. |
| `codex/milestone-inventory-inspection` | `ea56b64` | Native own-pack inspection, host timing display and scoped input; code `3eed739`, 846 tests, all final native journeys and twelve rendered states. [Evidence](p02/inventory-inspection/README.md) retains an earlier travel timeout with unconfirmed cause. Equipment/use/transfer, icons, full UI, OS/physical-device acceptance and phase/feasibility gates remain open. |
| `codex/milestone-recorded-history` | `9e0018d` | Exact recorded-commit reconstruction and operator; code `913a5f2`, corrected stationary quest probe `1d26210`, 855 final tests and combined native evidence. [Evidence](p01/recorded-history/README.md) preserves failed runs, confirmed stale Talk revision and unconfirmed editor-startup error. Arbitrary interior time, prehistory, branch travel, production latency, independent review and full phase gates remain open. |
| `codex/milestone-daylight` | `80bdda0` | Calendar-driven outdoor light and bright target/UI cues; 855 tests, complete integrated regression plus probe-only night-interface followups, ten daylight and twelve daytime UI captures. [Evidence](p02/daylight/README.md) retains the corrected unmapped probe input and recurring, unconfirmed editor-settings error. Weather/seasons, rest/camp, production art/audio, independent/human review and all full gates remain open. |
| `codex/milestone-quick-wheel` | `c8989ae` | Native four-action wheel, pinned inspection/confirmation, controlled background rendering and removal of legacy fixture refocus loops; 861 tests, separate complete native qualification, four affected fixture followups and sixteen rendered frames. [Evidence](p02/shared-desktop-validation/README.md) retains the failed integrated editor startup and precise source/binary boundaries. This is combined qualification, not one uninterrupted verifier pass. Full UI, OS/devices, co-op, art, independent/human reviews and phase gates remain open. |
| `codex/milestone-watershed-persistence` | `bf96ed9` | Explicit genesis-admitted watershed in the composed-world authority; 883 tests, real before/after-commit process kills, exact history/restart, independent outer archive/pin checks and native foundation/farm regression. [Evidence](p01/watershed-admission/README.md). Default farm terrain is unchanged; macro-to-local realization, basin composition, fluid custody, migration, production performance and all full gates remain open. |
| `codex/milestone-architecture-reference` | `2f91983` | Timeless architecture reference, source-binding backfill and upkeep hook. [Review/checks](architecture-reference/README.md) cover 123 reference links, bounded factual critique, frozen-spec/index preservation and five synchronized skill entrypoints (ecosystem `4e83086`). Documentation checkpoint only; no gameplay or phase acceptance is inferred. |
| `codex/milestone-watershed-networks` | `23851fa` | Finite basin composition, canonical archive, exact contributor propagation and shared faces; 904 tests, five independent archives, 23 real host inspections and bounded critic review. [Evidence](p01/watershed-network/README.md) preserves the reproduced scope-alias defect and correction. No live-world/network admission, fluid custody, local realization, performance or full phase acceptance is inferred. |
| `codex/milestone-watershed-network-persistence` | `b2176e5` | Complete finite network retained in explicit fresh profile-seven world; 919 tests, 74 existing inspections, 27 real operator calls, both process-kill boundaries, exact history and headless foundation/farm regression. [Evidence](p01/network-admission/README.md) retains fixture failures and bounded critic coverage limits. Default farm, prior saves/encodings and all byte/recovery gates remain unchanged. No local realization, fluids, played-save migration, performance or full phase acceptance is inferred. |
| `codex/milestone-watershed-terrain-mapping` | `41865ed` | Retained dry macro-to-local mapping with exact integer scale, vertical windows and ancestral addresses; 936 tests, six independent archives, 27 real host inspections and bounded critic review. [Evidence](p01/terrain-mapping/README.md) retains compiler failures and scope limits. Separate candidate/archive only: no live terrain/material/fluid admission, visual change, performance or full phase acceptance. |
| `codex/milestone-terrain-material-allocation` | `163a7bb` | Finite funded slab and exclusive coarse/detailed source partitions; 957 tests, 24 final real host inspections, independent interval/outer-format checks and bounded native critique. [Evidence](p01/terrain-materials/README.md) includes partial/interior actual transfers, 4,096-lot/active-cell caps and retained analyzer failures. Standalone trusted composition only: no live mining, retirement, world allocation registry, performance or full phase acceptance. |
| `codex/milestone-material-registry` | `b47fec7` | One shared inventory for 1–64 disjoint terrain allocations, normalized archive and shared immutable macro data; 971 tests, 26 registry plus 24 prior-material host inspections, independent ownership checks and bounded native critique. [Evidence](p01/material-registry/README.md) retains maximum-network allocation measurements and corrected fixture failures. No durable world admission, live mining, retirement, production performance or full phase acceptance. |
| `codex/milestone-material-production` | `a1783a9` | Shared terrain/Production proof, direct raw retirement conservation, exact retained history and explicit v2 archives; 983 tests, 20 new plus 50 legacy real-host inspections, independent interval/spatial accounting and bounded native critique. [Evidence](p01/material-production/README.md) retains corrected fixture/compiler failures. No live mining, durable world admission, long-lived history compaction, production performance or full phase acceptance. |
| `codex/milestone-runoff-budgets` | `02f2819` | Funded effective-runoff projection over exact finite drainage, shared physical/counting receiver headroom and canonical archive; 995 tests, 25 real host inspections, five independent origin-path/source checks and bounded native critique. [Evidence](p01/runoff-budgets/README.md) includes the 65,536-column profile and retained test-first failure. No hydraulic flow, receiver-capacity reservations, complete macro profiles, durable world admission, performance or full phase acceptance. |
| `codex/milestone-runoff-capacity` | `83db425` | Shared ownership of pending runoff headroom, stale-safe cancellation and actual custody/Production reconciliation; 1,004 managed tests, nine focused followup cases and 46 host inspections with independent volume/source checks. [Evidence](p01/runoff-capacity/README.md) retains failures, old archive compatibility and execution boundaries. Standalone finite owner only; live flows, physical reservoirs, durable world admission, production performance and full gates remain open. |
| `codex/milestone-reservoir-geometry` | `f00e637` | Finite authored watertight vessels, exact actual/projected fills, source/buffer/receiver composition and pinned wrapper plans; 1,018 tests, 26 host inspections and an independent binary-search fill oracle. [Evidence](p01/reservoir-geometry/README.md) preserves test-first failure, old capacity bytes and 4,096-column bounds. No live flow, terrain-wall proof, durable world admission, production performance or full-gate closure. |
| `codex/milestone-reservoir-persistence` | `a3789c1` | Fresh profile-eight exclusive physical owner; atomic material actions, canonical recovery/history, full 1,035-case regression plus five stronger follow-ups (1,036 distinct), 55 real operator calls/four process kills and native headless foundation/farm. [Evidence](p01/reservoir-admission/README.md). Assumed watertight supports; no live flow, terrain support, migration, performance or full phase acceptance. |
| `codex/milestone-runoff-controls` | `204d262` | Durable funded admission/cancellation inside profile eight, exclusive reservation purposes and exact capacity-only recovery; 1,059 managed cases, 73 real host calls/six process kills, headless foundation/farm and bounded native critique. [Evidence](p01/runoff-commands/README.md). Existing source holds only; no terrain wall proof, live flow, player controls, performance or full phase acceptance. |
| `codex/milestone-reservoir-transport` | `6ef6e83` | Actual conserved discrete gravity-gated delivery in fresh profile nine, shared step throughput, reading holds and exact source/history; 1,081 distinct managed cases, 95 operator calls/eight process kills and headless foundation/farm. [Evidence](p01/reservoir-transport/README.md). Authored seals/fixed topology; no continuous fluids, terrain support, flooding, player controls, performance or full phase acceptance. |
| `codex/milestone-reservoir-boundaries` | `5dd8110` | Exact read-only nominal containment assessment against retained terrain and completed material, explicit sealing/thickness policy, gap/obstruction/gate failures and recorded-world operator; 1,118 tests, 21 actual inspections/five unchanged saves and headless foundation. [Evidence](p01/reservoir-boundaries/README.md). No changed world admission, terrain mutation, strength, breach dynamics, physical gate proof or full phase acceptance. |
| `codex/milestone-live-excavation` | `7df18df` | Durable selected-cell excavation in fresh dry profile ten: actual player/NPC effort, exact source entitlement, atomic whole-cell issuance/removal and direct support refusal; 1,141 tests,144 ordinary Host calls/two process kills, exact history and native headless foundation/farm. [Evidence](p01/live-excavation/README.md) retains corrected critic findings and an unconfirmed initial parallel-build child exit followed by a clean full serial build. No generated mining frontier, wet-world composition, collapse, player mining controls, production balance/performance or full-gate closure. |
| `codex/milestone-constructed-support` | `2dcdd79` | Fresh dry profile11 with completed-member actor footing, exact XY support and earned assisted vertical traversal;1,173 tests,26 real Host calls/two process kills, exact history and native headless foundation/farm. [Evidence](p01/constructed-footing/README.md) proves real quest-funded NPC construction then actual climb/reopen. No ordinary stair routing, load strength/collapse, wet-world composition, player climb controls, production performance or full-gate closure. |
| `codex/milestone-stair-navigation` | `d4f01b4` | Fresh dry profile12 ordinary stairs and bounded directed multi-level routes; 1,189 tests, 20 new-profile Host calls/two process kills, legacy26/two-kill regression and native headless foundation/farm. [Evidence](p01/stair-navigation/README.md) proves a real quest-funded nine-tread/deck assembly, 585-commit player/NPC journey, stacked occupancy and automatic downstairs return. Same route budget after a retained real failure; no load/collapse, wet composition, rendered stair, throughput or full-gate acceptance. |
| `codex/milestone-structural-load-planning` | `ca149cb` | Actual funded member/actor mass binding, explicitly rated DAG and immutable bounded cascading failure plans with trusted-graph canonical resume; 1,241 tests, 14 fresh Host inspections, independent integer/byte oracle and headless foundation. [Evidence](p02/structural-loads/README.md). Read-only diagnostic scope: no live collapse, debris, actor falling, world-profile publication, production strength/latency or full-gate closure. |
| `codex/milestone-construction-physical` | `159e431` | Fresh dry profile13 retains complete physical member lifecycle independently of fabrication, with conserved installed material and atomic completion/identity publication. 1,274 tests, 26 new-profile Host calls/two kills, 20 legacy calls/two kills, headless foundation/farm and seven identical recreated files. [Evidence](p02/construction-physical/README.md). Exact-component support and pre-work refusal are qualified; live collapse, debris, actor falling, universal load admission, production performance/art/devices and full gates remain open. |
| `codex/milestone-structural-policy-motion` | `ca724fb` | Complete canonical authored load policy, inactive-template topology validation and exact whole-body vertical first-contact queries. 1,321 managed tests, 47 new, six new/five prior fixture files recreated exactly, bounded native critique and 300 reference links. [Evidence](p02/collapse-motion/README.md). Read-only dependencies: no installed world policy, live collapse/impact/debris, pending continuation, new native/crash campaign, performance/art/devices or full-gate closure. |
| `codex/milestone-structural-policy-admission` | `c6b032c` | Fresh profile14 durable full policy, identity ownership and stable complete local action/movement gates; builder-carried stock/byproduct accounting conserves mass without double-counting installed backing. 1,334 tests, new17/two-kill and prior26/two-kill Host controls, headless foundation/farm and25 identical fixture files. [Evidence](p02/structural-policy-admission/README.md). Terminal refusals only: no pending/live collapse, impacts/debris, production performance/art/devices or full-gate closure. |
| `codex/milestone-structural-settlement` | `92dd90a` | Fresh profile15 actual atomic excavation with one intact assembly/rider descent, complete swept collision and retained backing/foundation identities. 1,350 tests, new17+legacy5 ordinary Host calls/two kills, prior17/two-kill control, headless foundation/farm and14 exact old fixtures. [Evidence](p02/structural-settlement/README.md). Explicit harmless one-cell calibration; pending continuation, harmful impact/fracture, multiple-assembly closure, wet composition, production performance/art/devices and full gates remain open. |
| `codex/milestone-consequence-continuation` | `757bf18` | Fresh dry profile16/schema4 retained excavation command and real geometric cursor, reserved interval, exact retries/automatic resume and single terminal publication. 1,371 tests; 43 new ordinary Host calls/six kills, 22 older-profile calls/two kills, headless foundation/farm and 26 exact recreated fixture files. [Evidence](p02/consequence-continuation/README.md). The first 30-second historical-read timeout is retained with unconfirmed cause; exact recheck and full serial probe pass unchanged deadlines. Functional checkpoint only: production latency, broader collapse/damage, wet composition, art/devices and full gates remain open. |


The inventory-inspection branch records bounded code `3eed739` and retained evidence at
`ea56b64981f9f5409248dc9bb728af08439db386`. The development checkout continues on
`codex/runtime-foundation`; the earlier milestones were preserved and none was checked out.
No push or merge was performed.

The recorded-history branch preserves code and evidence at `9e0018d`. Qualification combines
successful integration checks with targeted native followups and final tests on the rebuilt
binaries; it is not a single uninterrupted full-verifier pass. The development checkout and all
earlier milestone branches remain in place. No push, merge or phase closure accompanies this marker.

The daylight branch preserves source and evidence at `80bdda07398a8657b1b8c7034c83242ef8dc641b`.
The final native probe extension was qualified separately after the integrated pass, with identical
production sources and tested non-client binaries. The development checkout remains unchanged;
earlier milestone branches and the unrelated staged predecessor work were preserved.

The quick-wheel branch preserves code and evidence at `c8989aed1178bfd13f68bcf38b64be82f540f2b6`.
The earlier wheel packet remains its original partial checkpoint; the shared-desktop packet
records subsequent completion. No checkout switch, force update, push or merge accompanied the
marker. The recurring editor-startup error remains unresolved and retained as a failure.

The watershed-persistence branch preserves code and qualification at `bf96ed9baa076f80f6b5266383612fa5a1f466ce`.
It records one explicit new-world macro admission, not a completed frontier or geological simulation.
The development checkout and previous milestone branches remain unchanged. No push or merge occurred.

The watershed-network-persistence branch preserves code, architecture updates and qualification at
`b2176e5ddb29bbbf355035b217d523941a4d4cc9`. It records durable ownership of finite prepared networks, not a completed frontier
or geological simulation. The development checkout, earlier milestones, frozen specifications and
unrelated staged predecessor work remain intact. No checkout switch, force update, push or merge occurred.

The watershed-terrain-mapping branch preserves code, reference updates and bounded qualification at
`41865edfa17a1d8476946261007abbe0ec53f334`. The development checkout and all earlier markers remain in
place. No checkout switch, force update, push or merge accompanied this marker; unrelated staged
predecessor changes and frozen specifications remain intact.

The terrain-material-allocation branch preserves code, reference and qualification at
`163a7bbc17bf07549ab2f6aa86050e7a1d9a8b9e`. It records conserved material representation over existing
custody, not playable mining or completed geological realization. The development checkout, prior
milestones, all 22 frozen spec hashes and exact unrelated predecessor index remain preserved.
No checkout switch, force update, push or merge accompanied this marker.

The material-registry branch preserves code, reference and qualification at
`b47fec733fe0c85e77ce0e2b792e9b90189729bb`. It records finite shared ownership and normalized material
archives, not durable integration or playable excavation. The development checkout, prior milestones,
all 22 frozen spec hashes and the exact unrelated predecessor index remain preserved. No checkout
switch, force update, push or merge accompanied this marker.


The material-production branch preserves code, reference updates and bounded qualification at
`a1783a984b4a120a799ac6cfa2b16570e20131d4`. It records standalone source retirement composition,
not playable mining or a complete geological simulation. The development checkout, prior milestones,
22 frozen spec hashes and exact unrelated predecessor index remain preserved. No checkout switch,
force update, push, merge or full-gate closure accompanies this marker.


The runoff-budgets branch preserves source, architecture reference and bounded qualification at
`02f28192dd380e1c04a3fac93b9eb327f1f0b878`. It records funded aggregate runoff and physical receiver
witnesses, not live flow or completed geological realization. The development checkout, prior markers,
22 frozen spec hashes and exact unrelated predecessor index remain preserved. No checkout switch,
force update, push, merge or full-gate closure accompanies this milestone.

The runoff-capacity branch preserves source, timeless reference updates and bounded evidence at
`83db4259717a3026f52f154d17a173069877591a`. It establishes capacity ownership inside the standalone
compositor, not globally admitted reservoirs or live water movement. The development checkout,
previous milestones, 22 frozen spec hashes and exact unrelated predecessor index remain preserved.
No branch switch, force move, push, merge or full-gate closure accompanies this marker.

The reservoir-geometry branch preserves source, timeless reference updates and bounded qualification
at `f00e637eb734573240f88f71cbe7e451636e24a2`. It records assumed watertight physical storage and exact
volume projections, not naturally realized lakes or hydraulic simulation. Development continues on
`codex/runtime-foundation`; previous milestones, frozen specs and the unrelated predecessor index
remain preserved. No checkout switch, force move, push, merge or full acceptance closure occurs.

The reservoir-persistence branch preserves source, timeless reference updates and bounded integration
qualification at `a3789c1892313beb11a93580809dc9846dd42fc4`. It records one complete durable physical
owner inside the existing world authority, not a hydraulic simulation or changed default farm.
Development remains on `codex/runtime-foundation`; prior milestone branches, frozen specs and the
exact unrelated predecessor index remain intact. No checkout switch, force move, push, merge or
full acceptance closure accompanies this marker.

The runoff-controls branch preserves source, timeless reference updates and bounded qualification at
`204d262dd83d663df2e150a323aa5a70e48ca13c`. It records durable control of existing funded receiving
commitments, not liquid movement or changed default gameplay. Development remains on
`codex/runtime-foundation`; prior markers, 22 frozen specifications and the exact unrelated predecessor
index remain intact. No checkout switch, force move, push, merge or full acceptance closure accompanies
this checkpoint.

The reservoir-transport branch preserves source, timeless references and bounded qualification at
`6ef6e83808c7711074a35190f99b5e2e27896b57`. It proves actual finite water transfer, including recovery
at its commit boundary, not a complete fluid simulation or changed default farm. Development remains
on `codex/runtime-foundation`; prior markers, 22 frozen specifications and the exact unrelated predecessor
index remain preserved. No checkout switch, force move, push, merge or full acceptance closure accompanies
this checkpoint.

The reservoir-boundaries branch preserves source, timeless references and bounded qualification at
`5dd8110fbb88a6993263f8aac9d1af90e9da66bb`. It qualifies an exact derived assessment over retained terrain
and actual completed construction; it does not convert saved reservoir profiles into coupled fluid/terrain
authorities. Development remains on `codex/runtime-foundation`; prior milestones, frozen specs and the
exact unrelated predecessor index remain intact. No checkout switch, force move, push, merge or full
game/phase acceptance accompanies this marker.

The live-excavation marker preserves source, timeless reference and retained qualification at
`7df18df50b6fd349473882594d98828b06d874bf`. Development remains on `codex/runtime-foundation`;
prior milestones, all frozen designs and unrelated staged predecessor changes remain intact.
No checkout switch, force update, push, merge or full-phase closure accompanied this checkpoint.

The constructed-support marker preserves source, timeless reference and qualification at
`2dcdd798bd569c503aac3b5ef387ebafe71711f4`. Development remains on `codex/runtime-foundation`;
all prior milestones, frozen specifications and the exact unrelated predecessor index remain intact.
No checkout switch, force update, push, merge or full-phase closure accompanies this checkpoint.

The stair-navigation branch preserves code, reference and bounded qualification at
`d4f01b4`. The development checkout, earlier markers, 22 frozen specifications and exact 19-file
predecessor index remain intact. No checkout switch, force update, push, merge or phase closure occurred.

The structural-load-planning marker preserves `ca149cb` and its exact funded-world graph/archive
qualification. The source world is unchanged. Actor load survival is explicitly distinct from spatial
footing; physical collapse still needs complete consequence publication, conserved debris and checked
falling/landing. Canonical restore binds an expected graph as well as a dependency pin. This checkpoint
closes no full phase and changes no predecessor history or frozen specification.

The construction-physical marker preserves `159e4316c6e2a5336e1eb68b6065615f228bf040` with source,
timeless references and bounded qualification. Completion-derived members share the original material
backing; supplied displaced poses do not claim an executed fall. Actual collapse and its complete world
publication remain the next dependency. Development stays on `codex/runtime-foundation`; all previous
markers, 22 frozen designs and the exact unrelated predecessor index are preserved. No switch, force move,
push, merge or full-phase closure accompanies this marker.

The structural-policy-motion marker preserves `ca724fbc3f76e5b392245a9bd67e073e7c66a0b0` with source,
timeless reference and bounded managed/funded-world qualification. Candidate contacts and load assessments
do not publish consequences. Live policy ownership and complete atomic collapse remain open. Development
stays on `codex/runtime-foundation`; previous markers, frozen specifications and the exact unrelated
predecessor index remain intact. No switch, force move, push, merge or phase closure accompanies this marker.

The structural-policy-admission marker preserves `c6b032cf5c63dd062a39e64612df0254c8b875df`, including
runtime policy authority, carried-project mass correction, timeless references and real recovery qualification.
A terminal overload refusal creates no consequence job; full coupled collapse remains open. Development
stays on `codex/runtime-foundation`; earlier markers, frozen specifications and the exact unrelated predecessor
index are preserved. No checkout switch, force move, push, merge or full-phase closure accompanies this marker.

The structural-settlement marker preserves `92dd90a342f7a1fb887540fab36ebfb53d49db89`, including actual
funded member/rider motion, conserved source/material ownership, current-pose load binding, timeless
reference and crash/retry/history qualification. The original failed compile/fixture/probe attempts remain
retained alongside corrected passing checks. This bounded atomic interpretation does not close durable
pending-work or broader collapse gates. Development remains on `codex/runtime-foundation`; previous
markers, all frozen designs and the exact unrelated staged predecessor work remain intact. No checkout
switch, force move, push, merge or full-phase closure accompanies this checkpoint.


The consequence-continuation marker preserves `757bf18d1fa7c7a8188a28c2806afb198a864a44` with the complete
command/basis/cursor owner, geometric query progression, exhaustive deferred receipt accounting, exact
publication reservations, journal completion capacity, source-aligned architecture and qualification.
Development remains on `codex/runtime-foundation`; prior markers, 22 frozen specs and the exact 19-file
predecessor index remain intact. Original failures remain retained. The functional checkpoint does not
close production latency or broader collapse gates. Ecosystem commit `e25ade3` preserves the portable
validation scheduling clarification. No checkout switch, force update, push or merge accompanied this marker.

The coupled-settlement marker preserves `463509d05e313578a479d13bffd423e1568e3432` with implementation `eee42ee`,
source-aligned references, independently checked fabrication/source/member/actor conservation and actual
commit-boundary crash recovery. The pre-change checkpoint is `codex/checkpoint-coupled-settlement-base`.
Development stays on `codex/runtime-foundation`; every previous marker, frozen specification and the exact
unrelated predecessor index remains intact. The original admission failure remains recorded. Ecosystem
`5aa4099` preserves the reusable diagnostic/authority boundary. No checkout switch, force update, push,
merge or full-phase/production acceptance accompanies this marker.

The event-descent-assessment marker preserves `25da97892d72629e7bdb425b4608aba658645e0f`, with implementation
`8279268` and pre-change checkpoint `codex/checkpoint-event-descent-base`. Its unequal trajectories are
qualified domain calculations; the retained live profile17 still publishes its original common descent.
Every original simulation source is unchanged. Development remains on `codex/runtime-foundation`, with
all earlier markers, frozen designs and the exact unrelated predecessor index preserved. No checkout
switch, force move, push, merge or full-phase/production acceptance accompanies this marker.

The playable-worksite marker preserves `7da8fcc50c31ec91fa474cde715fc243f39d8c6a`, including
complete managed qualification, the strict four-process rendered journey and the original farm's
native wheel control. Movement-root reuse is committed at `518e802`; initial content and the earlier
hash cache remain in its ancestry. All failed probes, bounds and open production obligations are
retained. Development continues on `codex/runtime-foundation`; previous markers, all 22 frozen
specification hashes and the exact unrelated predecessor index remain unchanged. No checkout switch,
force move, push, merge or full-phase closure accompanies this marker.
