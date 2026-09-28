# Explicit production continuation

This increment implements the material-owner portion of the
[coordinated material/care proposal](../restorative-recovery/MATERIAL_CARE_CONTINUATION.md), building
on the [exact material basis](../material-care-continuation/README.md). Complete-world, care,
geography and Host publication remain separate required integration work.

## Implemented scope

- `ProductionSnapshot.Continue(inventory)` derives an immutable closing context from actual supplied
  owners and opens bounded active batch/consumption collections. Live stock, absolute revisions,
  recipe admission and cumulative operation totals remain exact. It performs no gameplay action.
- Context-bound Production V3 validates imported output partitions, exact per-lot prefix spending,
  raw retirement once, storage revisions and immutable admission pins. Contexts retain bounded
  live rights and immediate producer metadata, not owner/ancestor chains.
- Actual crafting and consumption support the continued owner with a mandatory external historical
  identity lookup. Old disposed commands and generated identities do not become reusable merely
  because their records leave the active window.
- V1/V2 keep their original interpretation and reject continued owners. Existing world, care,
  construction, excavation, source delivery and terrain/runoff entrypoints explicitly refuse this
  unintegrated representation. Nested legacy geography encoding cannot erase its context.

The standalone continued owner freezes non-custody admission and imports existing raw rights only.
New issuance, transfers, reservation changes and other custody operations need typed continuation
evidence within this same canonical owner. This is an integration boundary, not a reduction of the
game's gathering, mining, building, trade or acquisition requirements. The reference explains the
[timeless contract](../../../Runtime/docs/architecture/production-continuation.md).

## Evidence and limits

The new material journeys perform **530 actual raw consumptions** and, separately, **530 actual
recrafts after one initial craft**, each crossing two real material cuts. They preserve finite stock,
absolute counters, a surviving original byproduct witness, exact raw retirement and historical
identity rejection. Closed component bytes are retained and decoded with their actual prior contexts;
reconstructed contexts and canonical bytes must match. No health or world-history record is fabricated
to fill a window. Fresh stress supply is labeled separately from game balance.

These are material domain journeys, **not** 530 administered healing doses, persisted world archives,
process recovery or a production performance result. The fixture's closed-byte collection stands in
for externally retained history; it does not implement authenticated storage. Full-world >256-dose,
dependent-owner and native proofs in the proposal remain mandatory.

The focused selection has **52 passing cases**: 41 new continuation/boundary cases and 11 retained
material-basis cases. It includes changed-context/counter/scope bytes, missing/duplicate/spent output
rights, raw/transformed lot restrictions, storage revision/capacity changes, legacy refusal and
unsupported world/geography entrypoints. The final solution build passes with zero warnings/errors.
[Full regression](qualification/verification.json) passes **1,446 Core + 783 Hosting = 2,229 cases**,
zero failures/skips; the focused cases are included in the Core total. Hosting reports 15m05s, without
qualifying production performance. All 1,176 source/configuration and 15 execution pins remain exact. The original failed build remains in
[corrections](FAILURES.md); the substantive independent storage finding and its disposition are in
[review](REVIEW.md). No acceptance threshold is relaxed.

## Preservation and reproduction

Base: `d23d94df8962df6e1590e55e7eb000fcb3503581` on `codex/runtime-foundation`.
Pre-change checkpoint: `codex/checkpoint-before-production-continuation-20260928T041433Z`.
Verified component milestone: `codex/milestone-production-continuation`.
[The inventory](qualification/preservation-before.json) pins 23 frozen specs, 63 prior milestones
and unrelated 19-file predecessor staging. Two native workers own disjoint runtime/test scopes;
the integration lead owns dependent boundary guards, architecture, execution and Git. One independent
native critic reviews the consequential invariants. No Claude retry or credit-saving claim is made.

From the repository root, use the local .NET 10.0.401 executable to build
`Beta/Runtime/OpenFantasy.sln --no-restore -m:1 -p:UseSharedCompilation=false`.
Run Core and Hosting test projects separately with `--no-build --no-restore`; do not overlap heavy
runs. The focused Core filter is
`FullyQualifiedName~ProductionContinuation|FullyQualifiedName~Terrain_admission_refuses_a_standalone_material_cut`.
Quote the filter, use a fresh results directory and retain failed reports. Test runner sockets require
local permission. These tests need no foreground Godot session.

## Next complete integration

Retain one canonical owner while adding typed post-cut source issuance and custody-operation
evidence. Join terrain/excavation coverage and actual installed construction provenance; carry the
same context through nested runoff/capacity/reservoir/transport state and codecs. Then close care
causes, consumed material and resource effects together under an explicit new world interpretation.
Keep profile25/26 successes and refusals unchanged.

Preemptive whole-batch headroom must prevent impossible-offer maintenance deadlocks and stranded
acknowledgments. Material-only cuts need their own complete publication semantics. The Host must
atomically store actual closing bytes, commands, receipts, permanent identities and the new head,
then verify real retrieved archives during replay. Only that integrated protocol can discharge the
actual-dose, long-ancestry, dependency, rollback, external-process-kill and native post-cut/reopen gates.
Full journal lifetime, source-content migration, the 181 requirements, production platforms/scale,
multiplayer, art/audio and human acceptance remain open.
