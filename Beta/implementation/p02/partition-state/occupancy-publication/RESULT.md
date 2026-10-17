# Complete occupancy-index publication and saved-root reconciliation

`SpatialOccupancyDirectory` creates a complete geometric metadata index with one exact identity entry
and identical references in every overlapping page. `Apply` validates a bounded batch's exact source
references and directory revision, then returns all changed pages/entries together as an immutable
successor. Failed capacity, stale-reference or duplicate-change validation leaves the source untouched.
Unchanged pages are shared. The index owns metadata consistency; actual actor, material and world owners
still establish legality and durable publication.

The new `SpatialOccupancyChange` permits insertion/removal and same-identity/kind/scope replacement.
All old copies are removed before new ones are inserted, making final page capacity independent of
input order. A 1,024-entry metadata-page exchange proves this without inventing a physically admitted
overlapping population. Removed pages retain empty revision records, so departure and identical return
cannot resurrect an earlier pin. Entity/page budgets are explicit; retained empty pages consume capacity.
No implicit reclamation or discarded collider makes a full directory fit.

`WorldSpatialOccupancyIndex` binds this index to an exact admitted world root/hash/revision/profile.
It reconciles one adjacent compatible successor, deriving every actor/structure metadata change.
Time-only commits share the geometric directory while advancing the world binding. A same-revision
forked source refuses. This adapter retains metadata and hashes rather than its source world, but it
still scans complete local roots and can require their full canonical encoding.

## Integrated evidence

Four actual ordinary SQLite movement steps move the same actor from X=0 to X=-128, crossing into
negative cells and leaving its old occupancy page. Each movement uses the directory's selected detail
with the shared geometric kernel and agrees with the actual committed pose. The original six counted
units and their exact carried-lot identity remain. Old immutable indexes retain their historical views.
Reopening from original genesis and reconciling each verified recorded root reproduces every queried
page hash/revision and the final exact source root.

Before/after-commit fault injection combines ordinary movement with a two-unit gift. Recovery produces
the complete old or new root. The before-commit case retries the original batch; the after-commit case
rejects that stale batch and retrieves its original receipt. Canonical receipt bytes remain exact on
repeated lookup. The mover retains four units and the recipient two. Index reconstruction follows only
verified durable roots; the pre-interruption index remains unchanged. These are thrown-I/O boundary
faults in the existing SQLite authority, not process kills or new cross-partition transactions.

The existing 200-presence file fixture now uses the complete directory implementation instead of its
hand-built page dictionary. Removing a distant metadata reference updates its pages without reading
that actor archive or invalidating the local query. The actual actor archives remain intact; this is
an index mutation exercise, not a gameplay death/deletion. Selected reads remain four metadata pages,
one 1,047-byte actor archive plus separate mover discovery, and one terrain archive. All metadata is
resident. No deep-NPC, memory high-water, city, performance or offscreen-simulation claim follows.

## Verification and retained failures

Final terminal outcomes and exact source/binary pins are recorded in [verification](VERIFICATION.json).
The final Core run passes 175 cases in 16.400 seconds; Hosting passes 15 in 33.984 seconds,
each within its unchanged 180-second bound. All **190 distinct cases** pass with no failures or skips,
including ten new cases and the strengthened 200-presence fixture. Builds report no warnings/errors.
The affected scope is the spatial suite plus occupancy/terrain preparation and joined movement Hosting
regressions. No existing simulation rule, codec, admission limit, database schema, authority commit path,
package reference or ordinary launch changed. This is additive Core/index behavior with retained actual
Hosting consumers in the qualification tests; it is not enabled automatically on the live game loop.

The first Core run retained 173 passes and two fixture failures. A small actor helper's hard-coded
home ID collided with instance IDs when generating the larger catalog, including an early exit from
the bounded-enumeration test. The fixture now uses a disjoint ID range. The corrected Core run passes
175 cases. Hosting then retained 13 passes and two failures because the fixture compared separately decoded receipt
objects by reference. `WorldActionReceipt` is a sealed class without value equality. The assertion now compares the complete canonical
receipt bytes; no receipt field, runtime rule or threshold was weakened. Only the affected Hosting
selection reruns after that test-only correction. Original failed results remain in check-01/check-02.

No full-suite, native, independent, human or full phase qualification is claimed. This additive scope
uses focused checks; earlier broader evidence retains its original source scope. Orchestration remains
paused and direct lead review does not replace the required independent architectural review.

## Roadmap and remaining obligations

This closes a concrete index-update boundary, not PLAN-STATE. Complete supplied-catalog consistency
is now enforced, but a catalog's external completeness and authoritative provenance still belong to
its owner. Every metadata page remains resident; empty records accumulate within the declared budget.
There is no disk-paged ownership directory, independently resident actor/cargo transaction, absent-detail
funded-work execution, new persistence format or atomic cross-partition recovery yet. The integrated
actor/cargo/work/history prototype and its measurements remain required before the NPC persistence decision.

[Checkpoint](CHECKPOINT.json) preserves f09bcc61 under
`codex/checkpoint-before-occupancy-publication-20261017`. The accepted marker is
`codex/milestone-occupancy-publication-20261017`; work stays on `codex/runtime-foundation`.
Unrelated predecessor staging, frozen specs, October5 advisory and earlier raw evidence remain excluded.

## Documentation review

- **Plan/spec:** CURRENT_ROADMAP, PLAN_STATE_COMPARISON and AGENT_HANDOFF distinguish atomic immutable index updates from canonical partition-owner publication. The full integrated crossing/work/recovery/history measurement and independent review remain open; no phase, manifest, shipping capacity or format decision is inferred.
- **Reference:** World/geography documents complete-catalog import, exact batch changes, final-capacity ordering, immutable sharing, retained empty-page revisions, budgets and the exact-world adapter. Persistence/history explains verified-root reconstruction, uncertain-commit recovery and fresh-capture versus replay lineage.
- **Accuracy/completeness:** Lead reviewed all new types, source-root/hash/revision checks, page-copy updates and actual world history/receipt APIs. Tests cover stale sources, density exchanges, rollback, departure/return, immutable history and real cargo/receipt continuity. Metadata consistency, geometric placement and mutable ownership remain separate contracts; the complete-root and metadata-residency costs are explicit.
- **Checks:** VERIFICATION.json retains terminal tests, documentation links/fields, authored whitespace and source/binary/protected-input hashes. Factual review supplements mechanical checking. The two fixture-error classes remain recorded, with no production repair or weakened gate represented as their solution.
