# Finite reservoir geometry and physical storage

This increment connects the runoff capacity owner to finite source, buffer and receiving geometry.
A capacity witness must now match the receiving vessel's actual dimensions; all mapped stock must fit
its physical container even after every forcing commitment has been canceled.

## Implemented scope

Reservoirs are authored watertight open vessels with one-square-metre columns, a common spill plane,
and connected rising floor contours. They have bounded depth/support and exact integer volume. This
profile assumes impermeable floors/perimeter walls and an explicitly incompressible material; it does
not infer natural terrain walls, liquid phase or a supported inlet from a material name or heightmap.

The equilibrium fill calculation distinguishes actual liquid from projected pending receipts. Sorted
floor bands and canonical one-microlitre residuals preserve exact volume, including signed coordinate
extremes and exact level transitions. Column quantities are projections of custody, not new material
claims or a coarse/detail partition. Disconnected/perched pockets reject under this supported profile.

One immutable wrapper retains geometry for every wet source and receiver, plus optional finite buffers.
It checks all raw/derived contents, exact calibration/ownership, geometric and counted capacity, identity
roles and nonoverlapping support. Stacked isolated vessels are supported. Cancellation retains physical
source constraints. A new wet source without geometry or a legal inner delivery that overfills a vessel
rejects at the complete-wrapper boundary. Plans pin geometry as well as the nested capacity state.

The normalized archive retains the exact existing capacity owner, geometry and rederived fill witnesses.
The read-only host operator and maintained verifier hook expose this standalone contract. Timeless
[architecture documentation](../../../Runtime/docs/architecture/world-and-geography.md#domain-contract-finite-physical-reservoirs)
records assumptions, ownership, formulas, byte layout and extension obligations.

## Qualification

[Summary](qualification/verification-summary.json): full solution build with **zero warnings/errors**;
**1,018 passing managed tests (689 Core + 329 Hosting), zero skips**, including fourteen new cases.
Tests cover terrain-independent geometry, disconnected/perched profiles, exact bands/residuals,
coordinate extremes, geometry limits, complete stock/derived accounting, real delivery/crafting,
retained canceled-source limits, new-source admission, geometric alias/overlap, stacked vessels,
stale complete-wrapper plans, archive corruption and cancellation.

**26 real host inspections pass:** five valid independently checked archives and twenty-one malformed
or incompatible cases. The Python oracle uses integer binary search for fill height rather than the
managed floor-band algorithm, then checks every column allocation and scalar fill witness. It checks
nested source funding and receiver obligations through the prior independent capacity oracle. Malformed
count, length, order, global-column, scope/role, support and witness inputs reject. This does not provide
independent terrain geometry generation, full Production DAG proof or hydraulic behavior.

The complete 4,096-column wrapper and the 4,096-column/1,024-depth geometry bound are exercised.
Combined maximum vessels/network/Production/pending workload performance remains unqualified.
All five retained capacity archives re-emit byte-for-byte identically. All 172 architecture links and
22 frozen spec hashes pass; the exact nineteen-file predecessor index is preserved.

The test-first missing-API compilation log is retained; implementation resolves it without suppressing
checks. The initial two geometry cases, expanded twelve-case pass and final fourteen-case full-suite
coverage remain recorded. [Native critique](critic-review.md) found no actionable source issues.
No gates, thresholds, deadlines or full acceptance criteria were relaxed.

## Remaining integration

This is a standalone physical storage compositor with assumed watertight boundaries. Exclusive durable
world ownership must publish it atomically and prevent inner-state bypass. Terrain-supported geometry,
inlet/outlet connection, complete crust/climate/coast admission, real arrivals/settlement/backpressure,
encounter Wait, topology mutation and exact history still require their own integration. Other liquid
phases, reactions, porous storage and cave/underdark flooding retain the full specification's scope.

The native/rendered campaign was not rerun; this increment changes no playable interaction or art.
All full P00/P01/P02 and later phase/production/performance/device/human acceptance gates remain open.
