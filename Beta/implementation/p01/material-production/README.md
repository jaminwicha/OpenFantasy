# Terrain material and Production composition

This increment connects the finite terrain registry to the existing Production ledger. It does not
add a parallel material balance, grant extraction permission or change a lived world/save profile.
The implementation lead owns source, tests, integration and commits; the bounded native critic
reviewed independently. No external model calls or paid delegation were used.

## Behavior and architecture

- Explicit production attachment and atomic reconciliation retain one inventory, allocation set and
  immutable consumption proof. Source coverage includes live raw intervals and direct raw retirement.
  Recrafting derived outputs never counts the original units twice.
- Actual authorized instant crafting follows transfer out of representation custody. The wrapper,
  reconciliation and archive admission reject direct consumption from registered coarse/fine terrain.
  This is a necessary composition rule; tool/labor/access and durable extraction authorization remain open.
- Demotion/refinement retain consumed holes, production roles, combined live-lot/storage limits and
  complete state pins. Consumed lot IDs stay occupied through the retained production history.
- Changed proof requires advancing production and custody revisions, unchanged admission/recipes and
  exact prior batches. Independently balanced replacement history cannot erase old consumption.
- Explicit normalized registry v2 and single-state v2 archives carry exact ProductionCodecV1 bytes.
  Raw-only v1 encoders reject production evidence, retain their formats and pass old byte fixtures.
  A read-only host operator inspects the new registry profile.

Timeless [geography](../../../Runtime/docs/architecture/world-and-geography.md) and
[material](../../../Runtime/docs/architecture/materials-and-activities.md) references describe ownership,
interfaces, quantities, formats and integrating boundaries. Frozen specs are unchanged.

## Qualification and retained failures

[Summary](qualification/verification-summary.json): clean full managed build, **983 passing tests
(654 Core + 329 Hosting), zero skips**; twelve new production-composition cases. The retained tests
exercise actual craft, craft-again, partial prefix consumption, empty-cell demotion, remaining-stock
refinement, history rewriting/removal/revival, stale/authenticated requests, identities, calibration,
combined capacity and a 4,096-live-lot boundary. The latter adds a valid split of crafted output to
reach the exact combined cap; it is a boundary fixture, not thousands of gameplay actions.

Twenty real host inspections include four valid independently counted archives and sixteen malformed
or contradictory inputs. The independent Python oracle computes direct raw retirement and spatial/source
coverage; temporary analysis-only retirement rows reuse the older geometric checker and are never
submitted as game state. Recipe mass allocation and the derived-output DAG retain their managed proofs;
we do not claim an independent reimplementation of those algorithms. Twenty-six prior registry and
24 prior material host inspections also pass on the same final binaries. Existing source/custody and
Production golden tests remain part of the full managed suite.

Failures are retained rather than waived:

1. test-first.log records missing APIs before implementation.
2. focused.log records a canonical reader field-count int/uint compiler mismatch.
3. focused-2.log records test use of the internal WithCustody helper and cascading assertion diagnostics;
   fixtures now construct public inventory snapshots through the existing test helper.
4. focused-3.log records seven failing tests from the fixture's incorrect mass-per-output-unit allocation.
   The fixture used batch mass where the contract requires unit mass. It was corrected; no conservation
   check, budget or threshold changed. The calibration test also runs against a constructible recipe.
5. focused-4.log records the corrected 46-case material run, before the final additional cases.

[Critic coverage](critic-review.md) and [preflight](preflight.json) retain scope and preservation context.
The full verifier, native rendering and durable process-kill campaign were not rerun for this additive
standalone domain change. tools/verify.py includes the new fixture/inspection path for future campaigns.

## Remaining integration

Full physical macro profiles, local terrain realization, complete world allocation ownership,
construction/occupancy/support, mining labor and rights, exact history publication and long-lived
provenance retention must compose before playable terrain admission. Existing finite recipe/batch
bounds remain unchanged; this does not qualify an indefinite economy. Production performance,
platform/device acceptance, complete UI/art and all full phase/feasibility/game gates remain open.
