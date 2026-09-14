# Funded watershed runoff budgets

This increment makes the next physical macro dependency executable: a finite, source-funded
**effective-runoff budget** for one exact calendar interval over the retained watershed network.
It is an immutable projection of downstream obligations, not a hydraulic flow step or lived-water state.

## Implemented contract

- Every basin has uniform effective-runoff depth or an explicit dry input. Metre-cell area and depth
  convert exactly to calibrated counted units; fractional units and checked arithmetic overflow reject.
- Wet inputs are backed by disjoint live raw source intervals, held by the admitted reservation and
  owner. Fragmented stock is supported. Missing, retired, protected, incorrectly pinned or extra
  reserved stock cannot be hidden in the budget. No new material is minted or transferred.
- Weighted contributions follow the network's existing drainage and exact receiving columns. Dry
  basins may carry upstream water obligations. Terminal totals—not the sum of path rows—equal input.
- All terminal deliveries sharing a receiver compete together with its actual raw/derived occupancy.
  Both counted capacity and an explicit physical volume witness must fit; conflicting witnesses and
  unknown-volume mixed cargo reject. Source and receiver storage roles are distinct.
- The request pins network/mapping, authority/Production, owner/reservation, half-open WorldInstant
  interval, calibration, source allocations and receiver definitions. EnsureCurrent detects complete
  basis changes. The v1 archive stores exact inputs and verifies routing witnesses by rederivation.
- A real host operator and maintained verifier hook provide read-only inspection. The old terrain
  mapping decoder is reused by changing helper access from private to internal; its logic and bytes
  remain unchanged. Existing game/save profiles are unchanged.

The timeless [geography](../../../Runtime/docs/architecture/world-and-geography.md#domain-contract-funded-runoff-budgets)
and [material](../../../Runtime/docs/architecture/materials-and-activities.md) references describe
ownership, formulas, canonical layout and the integrating obligations without progress language.

## Evidence

[Qualification summary](qualification/verification-summary.json): clean full managed build,
**995 passing tests (666 Core + 329 Hosting), zero skips**, including twelve new runoff tests.
Cases cover unequal and dry contributions, shared receivers, fragmented funding, source reuse,
protected/wrong/missing funding, exact conversion/overflow, negative calendar time, complete basis
changes, actual crafted receiver contents and the complete 65,536-column network. Preparation is
compared across 1/2/4 worker counts and reversed input/terminal order on the small fixture.

Twenty-five real host inspections include five valid archives checked by an independent Python oracle
and twenty invalid inputs. The oracle follows each original contribution to its terminal, rather than
repeating the managed topological accumulation; it compares every stored route quantity and checks
source funding and calibrated receiver headroom. It does not independently derive the terrain,
reimplement the Production DAG or prove unique scheduling across independently submitted candidates.
The largest archive and exact checksums are retained in the summary. Maximum profile coverage is a
bounded correctness/archive check, not a production latency, working-set or long-running climate test.

The test-first missing-API compilation failure remains in qualification/test-first.log. The two-case
first implementation and twelve-case focused suite pass; no acceptance assertion, budget or threshold
was relaxed. A final style pass expands application guard blocks before the final build/tests. Native
critic design/source coverage is recorded in [critic-review.md](critic-review.md). The complete native
verifier/rendered journeys were not rerun for this additive standalone core/inspection change.

## Remaining dependencies

A source reservation is real held stock. Several inspection candidates can still reference it; a world
owner must establish unique forcing admission. Projected receiving headroom is **not** an owned capacity
reservation. Live stages need real source/buffer storage, receiving reservations, geometry, arrival
rules, backpressure/overflow, topology revalidation and encounter Wait admission before any transfer.
The aggregate forcing interval does not define interior-time evolution or a partial-event query.

This supplies one physical budget witness, not the full macro profile. Crust/boundary history, climate
and water partitioning, coast/reservoir geometry, stratified local realization, terrain/material/world
ownership and exact staged history still need integration before live terrain admission. Atmospheric
forcing, infiltration, evaporation, aquifers, ice and liquid reactions retain their full design scope.
All full P00/P01/P02, later production phases, device/art/human and feasibility gates remain open.
