# Finite watershed networks

Code and evidence: `23851fa`, preserved on `codex/milestone-watershed-networks`.
Development remains on `codex/runtime-foundation`; no push, merge or checkout switch occurred.

12 September 2026. This P01 dependency increment composes immutable basins before local realization.
It supports the finite preparation/shared-boundary obligations in
[geological design](../../../spec-v1_1/GEOLOGICAL_WORLD_DESIGN.md), without closing RQ-151, RQ-159,
GEOR-02 or any phase exit. The enduring contract is in the
[architecture reference](../../../Runtime/docs/architecture/world-and-geography.md#domain-contract-finite-watershed-networks).

## Delivered scope

The portable planner composes 1–16 basins with equal scope/seed, explicit province ancestry and one
closure per outlet. Links enter a cardinally adjacent boundary column with a compatible spill route;
terminals have finite contributing-column capacity. Scope/role aliases, overlaps, missing/duplicate
closures, unknown destinations, uphill links, cycles and insufficient terminal coverage reject the
complete candidate. Source basin bytes remain immutable.

Incoming contributions enter the exact receiving column and propagate to its downstream route.
Composed counts are separate from each basin's native counts. Every origin reaches one terminal;
terminal totals equal network columns. Physical cross-basin faces derive from canonical world/realm/
province addresses, independent of member enumeration and branch. Only nominated links drain.
Existing outlet labels retain their role and cannot double as face identities.

The envelope is 65,536 columns, 2,048 possible shared faces and a four-million-byte archive. Decode
rederives all members/composition and exact-compares canonical bytes. The host's new
`--inspect-watershed-network` operator reads and reports the archive without modifying it or a world.
The full verifier includes independent vector regeneration, all new tests and the operator journey.

This is a standalone preparation contract. No new snapshot profile, saved-world admission, migration,
local terrain consumption, climate boundary, water volume/storage, fluid flux or production streaming
is claimed. Finite contributor coverage is not a substitute for those physical budgets. The default
farm and profiles one through six retain their existing behavior and bytes.

## Validation

[Qualification summary](qualification/verification-summary.json) records:

- Full managed solution build, including Godot C# client: zero warnings/errors.
- 581 Core plus 323 Hosting tests, all 904 executed/passed, zero skips. The 21 new network cases cover
  fan-in, exact receiving paths, nonflat spill barriers, closure/identity failures, branch/order
  invariance, cancellation before work, concurrent read/prepare configurations, signed extrema,
  canonical corruption and the maximum network.
- Five independent Python archives, including branch, relief, extrema and disconnected terminals.
  Network contributions use per-origin route enumeration instead of basin topological accumulation.
  Tests compare complete canonical bytes, counts and face identities. Existing basin golden bytes
  remain unchanged after adding optional generator input parameters with preserved defaults.
- 23 real host inspections, including all independent archives, invalid fields, truncation, unknown
  frame version, unused closure fields, oversize input and the maximum 65,536-column fixture.
- A bounded [independent critic review](REVIEW.md), source/document reconciliation, link and syntax
  checks, preserved frozen specifications and unchanged unrelated staged predecessor work.

The maximum fixture is a sixteen-basin chain with 960 actual cross-basin faces; it exercises the
maximum column/member envelope, not a claim to saturate the 2,048-face upper bound. These checks are
not latency/memory qualification, exhaustive fuzzing or a cancellation-during-work experiment.
No Godot import/render/gameplay/device journey or complete integrated verifier was run for this
additive pure-domain/read-only-host change. Saved-world crash/recovery gates remain separately owned.

## Failures and corrections retained

Initial test compilation reported xUnit1051 for missing test cancellation forwarding. The first
correction also passed an extra token to a two-argument test helper; its CS1501 diagnostic remains
in `qualification/before-scope-fix.log`. Both were fixed in the tests without suppressing analyzers.

The critic found scope-root IDs were initialized through a set literal, silently collapsing aliases.
Four targeted regressions then failed with no exception, including an archive whose nested basin was
valid and whose derived bytes agreed. `network-scope-red.trx` and `scope-red.log` preserve those failures.
Registering each root independently fixes the defect; all four pass in the final complete suite.

The first broad-build command used an incorrect `.slnx` suffix and executed no build; its diagnostic
is retained in `build-wrong-solution-name.log`. The corrected command uses the actual `OpenFantasy.sln`.
No failure was reclassified as an environmental interruption or an acceptance pass.

## Reproduction and continuation

From the runtime directory, build `OpenFantasy.sln` with the pinned SDK. Run the Core tests with
`OPENFANTASY_WATERSHED_NETWORK_EVIDENCE` set to a chosen output directory to retain `maximum.bin` and
its expected report. Run Hosting tests, then `tools/check_watershed_network.py --dotnet <pinned-sdk>
--maximum-fixture <that-directory> --output <report.json>`. The integrated `tools/verify.py` wires these
inputs automatically along with its broader existing gates; wiring is not a claim that it ran here.

Next: admit this complete network through the composed-world identity, codec and causal-history
boundary, then establish macro-to-local realization and actual resource/fluid custody. A network
must never be silently inserted into a lived profile-six world. Province/climate/coast/reservoir
contracts and visible bounded streaming remain separate obligations of the full plan.
