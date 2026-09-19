# Funded structural loads and resumable failure plans

This increment supplies the load-planning dependency for the occupied-floor/support-removal scenario in
[the full implementation plan](../../../spec-v1_1/IMPLEMENTATION_PLAN.md). It binds actual funded
construction and actors to rated load paths, resolves cascading failures in bounded slices, and restores
canonical continuation archives. It does **not** publish collapse, debris, falling occupants or a new
world profile. P01/P02 and the full production campaign remain open.

## Implemented and exercised

The portable Core retains a finite directed bearing graph, explicit capacity/condition/span/joint
ratings, source-pinned terrain foundations and exact mass calibration. Member shares conserve the actual
installed output lot's mass. Actor loads count the pinned body and live primary-inventory goods, including
reserved/equipped items, without counting retired recipe inputs or project custody a second time.

Each immutable resolution evaluates at most the selected 1–256 nodes per call (64 by default), with bounded
whole-graph copying/reset work. Cascades proceed to a failure-free pass in stable identity order. Weighted
integer sharing uses exact wide arithmetic and canonical remainder allocation. Final foundation load plus
failed original input mass equals total input mass. A diagnostic failure's accumulated load is not a new
spendable material quantity.

Archives retain the complete graph and continuation, but decoding also requires independently expected
graph bytes and a dependency pin. Deterministic prefix replay checks every saved field. Caller-supplied
policy is not authenticated world policy. Decode can replay more work than one ordinary slice; production
latency and host scheduling remain unqualified. The [architecture reference](../../../Runtime/docs/architecture/structural-loads.md)
records these contracts and the separate spatial-contact obligation.

## Actual funded fixture

The fixture reuses the real quest-funded stair/deck assembly and actual ordinary traversal from the
[stair checkpoint](../../p01/stair-navigation/README.md). Wren completes construction at revision 63; the
player walks upstairs through the actual authority. No test injects installed geometry or final feet.
The closed SQLite world, expected world/model hashes, graph, pending checkpoint and complete checkpoint
are retained in `qualification/fixture/`.

The read-only binding contains 1,045 elements and 21 bearings. It explicitly rates existing terrain
sources as external foundations, each body at 70 kg, and fixture tool definitions at 1 kg. Completed
member mass is 270,000 mg, inherited from the earlier synthetic recipe. These values qualify accounting
and dependency propagation, **not production density, engineering strength or gameplay balance**.

The intact occupied deck is safe under the declared load rules. Losing one of its two leg foundations
redistributes weight. With the other leg rated at 50 kg, the plan records:

1. Explicit foundation removal at pass zero.
2. Its dependent leg unsupported at pass one.
3. The remaining leg overloaded after redistribution at pass two.
4. The deck unsupported at pass three.
5. The upstairs actor's load node unsupported at pass four; pass five reaches the fixed point.

Total input is 143,370,000 mg. The result accounts 71,225,172 mg at surviving foundations and 72,144,828 mg
as failed original input. The downstairs builder remains supported. A checkpoint at pass two, cursor
zero, after 1,044 evaluations resumes to the identical complete archive. The final plan contains 5,210
evaluations. The source world remains intact, with unchanged construction, inventory and actors.

Two separate regressions pin the live-integration boundary. Actual model-derived half-footprint contacts
can redistribute all actor mass to one surviving cell while the unchanged actor pose is spatially
unsupported. A removed upper contact can expose a lower landing that the old load graph does not contain.
Consequences must rederive geometry and contact, not infer safety or falling from graph membership alone.

## Qualification and retained failures

See [the machine-readable summary](qualification/verification-summary.json) for exact retained reports.
The full solution builds with zero warnings/errors. Full regression passes **1,241 managed cases**:
810 Core and 431 Hosting, with 52 new cases, zero failures/skips. The actual Godot headless foundation
passes; no rendered collapse, physical-device, art or human acceptance is claimed. All five closed fixture
files reproduce byte-for-byte in the full regression. Architecture links, frozen specification hashes and
the exact unrelated predecessor index are checked separately.

The independent Python checker reconstructs the final graph solution and canonical result bytes using
integer arithmetic, then compares actual fresh Host process output and corruption refusals. Its detailed
run record is linked from the summary. The all-systems verifier has a fixture/checker hook; this increment
runs the checker directly rather than claiming another complete all-systems qualification.

Retained failures and their dispositions:

- `core-structural-01.log`: new-test compilation exposed ambiguous `Version` references and the required
  xUnit cancellation-token rule. Explicit names/tokens fixed these without changing assertions.
- `hosting-structural-01.log`: the new fixture accidentally selected the eighth stair tread as a deck leg
  because both ended at 2,000 mm. The real face check rejected it. The fixture now selects its two actual
  rear legs; geometry validation was not relaxed.
- `core-structural-03.log`: a real 128-bit pending load exposed checked narrowing in the low-word encoder.
  The encoder and independent test mutator now explicitly mask the low 64 bits. The same wide-load and
  forged-value cases pass; no numeric bound was reduced.

A native independent source critic found that comparing a copied dependency pin alone allowed graph
substitution. Decode now compares the trusted graph itself, with altered-mass and altered-capacity
regressions. Its narrow recheck confirms the fix. The critic also identified the distinction between actor
load survival and physical footing; explicit documentation and two geometry regressions retain it.
No gate/deadline was relaxed and no external Claude/Fable call was used.

## Required next integration

The next work must connect this domain to live authority before claiming the structural scenario:

- Separate completed construction work/provenance from physical condition, placement and surviving
  geometry. `ConstructionProject` completion and the installed lot's original production history remain
  facts after damage; do not decrement completed work to represent collapse.
- Give debris conserved ownership. One installed quantity cannot become several copied `[0,1)` lots.
  Retain a wreck bundle with exact geometry or use a proper ledger transformation; collateral terrain
  failure must not claim player mining work or duplicate extraction entitlement.
- Prepare the entire affected support closure before final spatial admission. Existing excavation
  reconstructs strict occupancy immediately after private removal and therefore refuses missing support.
  A coupled resolver must prepare physical consequences before that final invariant check.
- Recompute contact from surviving geometry, then prove clear landing/fall consequences or retain an
  explicit falling state. Other occupants, gathering anchors, moving actors, carried goods and neighboring
  structural loads participate. Actor motion and custody changes can alter load even without excavation.
- Own ratings, condition, revision pins and pending work durably. Missing neighbors and oversized closures
  need held staged work, not dropped dependencies. Publish terrain, construction, custody, occupancy,
  effects and receipts atomically; qualify restart/retry and exact history at real commit boundaries.
- Keep old profiles/bytes unchanged, preserve dry/wet exclusivity until actual seals/openings/displaced
  volume/spills compose correctly, and qualify ordinary interaction and multi-floor presentation.

Natural-strata stability, arbitrary storage/vehicle load ownership, generated extraction rights,
hardness/tool scaling and production throughput remain required. All 181 requirements, the living UI
supplement, complete migration and the campaign through P14 remain in scope.
