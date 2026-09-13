# Shared finite material ownership

Qualification date: 2026-09-13. This increment composes the previously qualified funded slabs under
one bounded allocation owner. It prevents valid individual slabs from claiming the same physical
support or source units. It is a standalone core registry, not live-world admission or playable mining.

Qualified code/evidence: `b47fec733fe0c85e77ce0e2b792e9b90189729bb`, preserved on
`codex/milestone-material-registry`. Development remains on `codex/runtime-foundation`.

## Delivered behavior

- One exact prepared terrain basis and one counted inventory back 1–64 allocations. Initial creation
  requires all allocated units actually in coarse custody. Allocation views reference the same inventory;
  canonical storage includes that inventory and the terrain once.
- Spatial/source overlaps, duplicate identities, conflicting item/physical definitions and opposite
  coarse/fine custodian roles reject. Existing per-owner capacities, source limits and 4,096-lot bound
  remain; the 4,096-active-cell limit covers the complete registry, including empty active bindings.
- Existing refinement/demotion planning creates one candidate inventory. Every allocation revalidates
  against it. Registry-wide identities protect accepted causes and spawned lots, including definitions
  belonging to a different allocation. Complete registry hashes/revisions pin plans, rather than only
  the selected slab. External custody reconciliation preserves admission and all active bindings.
- Actual shared-source transfers preserve interior holes and the neighbouring allocation's detailed
  lots. Stock placed in another allocation's custodian retains its original source attribution; it does
  not become material for the receiver's cells. Unrelated protected/reserved stock remains unchanged.
- A normalized 6,250,000-byte archive and read-only host operator validate shared ownership. Strict
  allocation, binding-length, identity-order and cumulative-cell bounds reject malformed data before
  candidate publication. The existing single-deposit archive remains exact and supported.
- Immutable networks share a lazy content hash and macro-role lookup. Deposits no longer copy the
  network's complete macro identity set. No mutable authority state, RNG or commit decision lives in that cache.

The timeless [registry reference](../../../Runtime/docs/architecture/world-and-geography.md#domain-contract-shared-material-allocation-registry)
and [activity reference](../../../Runtime/docs/architecture/materials-and-activities.md) explain ownership,
format and extension contracts. Runtime instructions include the operator and full-verifier fixture hook.

## Evidence

[Final qualification](qualification/verification-summary.json) records a clean managed solution build,
**971 passing tests (642 Core + 329 Hosting, zero skips)**, 14 added tests, **26 registry host inspections**
and **24 prior-material host inspections** on the rebuilt binaries. Five registry archives receive an
independent Python interval/spatial-overlap check; one shared-coarse archive is independently encoded
around exact retained terrain. This does not claim an independent watershed reimplementation.

The combined maximum fixture contains 65,536 macro columns and 64 allocations in **3,367,561 bytes**.
After the first binding initializes shared macro data, the next 63 bindings allocate **127,656 bytes**
on the measured thread. A 4,000,000-byte local test bound detects accidental per-binding copies of the
maximum network/role set. This is a retained allocation-shape check, not production latency, total
working set or a comparison against a measured old implementation. The full LANG workload gates remain.

The active-cap archive deliberately has 4,096 empty active cells over actually transferred source stock.
It is an archive-boundary fixture, not a claimed 4,096-action gameplay journey. Refining another allocation
rejects even with only a few live lots, proving the shared cell budget separately from the lot cap.
Malformed cases test count 65/UInt32 maximum, oversized binding, duplicate/reversed IDs, global cell
overflow across two records, source/spatial conflicts, wrong scope, truncation and unknown versions.

Failures remain retained: `test-first.log` records missing implementation APIs; `boundaries-first.log`
records use of an inaccessible private encoding helper in a test; `boundaries-corrected.log` and its TRX
record the missing copied compatibility fixture. The test gained its own framing helper and explicit
fixture output mapping. These failures were fixed, not waived. The final managed test logs also retain
macOS `MallocStackLogging` diagnostics; all reported assertions pass. No cause is inferred for those
allocator messages and no failed assertion was reclassified.

The [native critic record](critic-review.md) covers source ownership/conservation, global role checks,
normalized parsing and cache safety. It prompted the maximum-allocation check and explicit parser
adversaries. Final bounded review found no remaining consequential finding. Human game acceptance,
native rendered journeys and the complete verifier are not claimed by this campaign. No game/phase
gate, threshold or deadline was weakened.

## Remaining integration

The integrating world must own the complete allocation scope. Two separate valid registries do not
prove cross-registry exclusivity. This increment does not admit/remove allocations dynamically, change
existing composed-world save profiles, authenticate commands or implement durable retries. The caller
still owns accepted causes, complete historical identity checks and atomic durable publication.

Retirement/transformation needs composition with Production; current funded intervals must remain
fully covered in raw custody. Live mining additionally needs geometry/support, tools/labor, rights,
hazards and history. Complete physical macro profiles still need crust/history, climate/forcing,
coast/reservoir/external budgets and retained local realization. These dependencies and all full
requirements remain in the campaign; this registry does not close P01 or authorize broad expansion.

## Preservation

[Preflight](preflight.json) pins source `9181177`, the pre-edit branch and unrelated predecessor index.
Document checks retain all frozen spec hashes and source/reference links. Commits include only owned
successor code, fixtures and records; the 19 staged predecessor files remain intact. The integration
lead records the qualified commit and milestone without switching the development checkout.
