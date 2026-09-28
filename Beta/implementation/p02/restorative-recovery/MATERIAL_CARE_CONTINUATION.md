# Material and care continuation proposal

**Status: source-informed proposal; not implemented or accepted runtime behavior.**

Source baseline: `8a827638` on `codex/runtime-foundation`, preserved at
`codex/checkpoint-before-restorative-recovery-20260928T043500Z`. This proposal identifies the next
material/care dependency. It does not qualify continuation by documenting it, change any bound, or
replace the [world-composition decision](../../WORLD_COMPOSITION_DECISION.md). The integration lead owns
implementation, source preservation and acceptance. Names below describe proposed responsibilities,
not existing APIs or allocated codec/schema numbers.

## Recommendation

Implement a coordinated **material/care boundary with a bounded live-right frontier**, backed by actual
retrieved complete-world archives and journal replay. Rotate production batches, consumed claims and
completed care history together; flush/rebase causal resource history at the same boundary. Keep live
custody, reservations, item/recipe pins, permissions, current physiology, cumulative revisions and
historical identity occupation intact.

Do not ship consumed-list truncation, a higher count limit, a hash-only receipt summary, or the existing
resource-history cut as this feature. A safe material continuation is a substantial owner/versioning
increment. Completing profile26 external process-kill qualification is a coherent independent proof;
it does not require rushing a partial ledger change.

## Source constraints that determine the boundary

| Invariant | Exact owner and consequence |
| --- | --- |
| Production retains separate 256-batch and 256-consumption bounds, with a shared live-lot bound. | [ProductionSnapshot](../../../Runtime/src/OpenFantasy.Core/Production/ProductionSnapshot.cs), [InstantCraftPlanner](../../../Runtime/src/OpenFantasy.Core/Production/InstantCraftPlanner.cs). Clearing consumption alone leaves crafting blocked after 256 batches. |
| Every produced output is partitioned exactly among live lots, later recipe inputs and consumed claims. | [ProductionClaimValidation](../../../Runtime/src/OpenFantasy.Core/Production/ProductionClaimValidation.cs): `Validate`, `AddOutputClaim`, `ValidateAcyclic`. Removing claims creates missing output; removing batches makes surviving output references unbacked. Recipe chains can remain live across arbitrarily many operations. |
| Raw source capacity is a ceiling, not evidence of issuance. | `ValidateSourceClaims` in the same file checks bounds/disjointness; [WorldAdmissionValidator.ReadProduction](../../../Runtime/src/OpenFantasy.Core/Worlds/WorldAdmissionValidator.cs) additionally checks gathering issuance cursors. [ExcavationSnapshot.ValidateForWorld](../../../Runtime/src/OpenFantasy.Core/Terrain/ExcavationSnapshot.cs) requires exact completed-extraction coverage. |
| Retired prefixes and surviving lot suffixes retain increasing revisions and exact provenance. | `ValidateLotHistory`, `ValidateSourceClaims`, [MaterialLotSelection](../../../Runtime/src/OpenFantasy.Core/Production/MaterialLotSelection.cs). A new lot ID cannot legalize spending an already retired source/output interval. |
| Direct raw retirement closes geographical material partitions; transformed retirement must not expand raw ancestry twice. | [ProductionRetirementHistory](../../../Runtime/src/OpenFantasy.Core/Production/ProductionRetirementHistory.cs), [TerrainMaterialProduction](../../../Runtime/src/OpenFantasy.Core/Geography/TerrainMaterialProduction.cs), [TerrainMaterialState.ValidateCoverage](../../../Runtime/src/OpenFantasy.Core/Geography/TerrainMaterialState.cs). Existing successor validation forbids removing or rewriting history. |
| Every consumed dose joins exactly one completed care operation. | [WorldCareAdmission.Validate](../../../Runtime/src/OpenFantasy.Core/Worlds/WorldCareAdmission.cs), [WorldRestorativeAdmission.Validate](../../../Runtime/src/OpenFantasy.Core/Worlds/WorldRestorativeAdmission.cs). Ordinary use, acknowledgment and treatment have separate bounded collections; ordinary count/revision is presently derived from retained records. |
| Resource archives retain the complete CareSnapshot and require its historical records to survive. | [WorldResourceArchiveAnchor](../../../Runtime/src/OpenFantasy.Core/Worlds/WorldResourceArchiveAnchor.cs), [WorldResourceAdmission](../../../Runtime/src/OpenFantasy.Core/Worlds/WorldResourceAdmission.cs). Pruning care under the existing anchor breaks accepted continuity. |
| A resource cut is scheduled before interpreting all potential resource writers. | [WorldResourceContinuation](../../../Runtime/src/OpenFantasy.Core/Worlds/WorldResourceContinuation.cs). Its present seal requires nonempty resource history; material-only crafting can exhaust its envelope without any resource effects. |
| Durable archive authenticity comes from actual stored bytes matched to the replayed closing root. | [WorldResourceArchiveReplay.Verify](../../../Runtime/src/OpenFantasy.Hosting/Worlds/WorldResourceArchiveReplay.cs), [SqliteWorldAuthority.Commit](../../../Runtime/src/OpenFantasy.Hosting/Worlds/SqliteWorldAuthority.cs). A digest or regenerated seal alone is insufficient. |

[ProductionIdentityRegistry](../../../Runtime/src/OpenFantasy.Core/Production/ProductionIdentityRegistry.cs)
and complete-world admission also assign roles and pin definitions to historical batch, lot, command
and consumption identities. Archiving their records must not release those identities for reuse or
remove role conflicts with geography, structure or control identities.

## Viable options and tradeoffs

| Option | Safe scope and limitation | Decision |
| --- | --- | --- |
| Archive only closed production components; retain all batches reachable from live outputs. | Preserve full provenance and exact raw-retirement coverage. Helps isolated craft-and-consume loops, but a live recursive recipe chain or installed output can keep historical dependencies resident. It does not remove the batch-bound problem generally. | Possible explicitly bounded intermediate experiment; not the production continuation claim. |
| Archive complete closed boundaries and import only verified live rights into the next segment. | Current state retains bounded live inventory/output budgets and bounded exact coverage summaries. Full operation/provenance history stays retrievable; recursive ancestry need not remain resident. Requires coordinated material, care, resource and composed-owner validation. | **Recommended architecture.** |
| Build a paged persistent material-right/provenance ledger with authenticated range and ancestry queries. | Handles larger fragmented coverage and historical queries without keeping all facts in memory. Requires a broader storage/cache/query consistency contract and recovery protocol. | A scaling extension if measured bounds of the frontier design demand it; unnecessary as the first implementation. |

## Proposed frontier and validation

A proposed `WorldMaterialCareAnchor` is extracted only from a complete, validated closing world. It
binds world/branch, admission and format versions, boundary sequence, archive ordinal and exact bytes.
Its public construction must not accept arbitrary source totals or a caller-provided digest as proof.
Continued decoding requires the exact retrieved anchor; historical authentication still requires replay
from admitted genesis. The head stores a reference and bounded continuation state, not archive bytes,
a recursively nested root, or an ever-growing in-memory ancestor chain.

The anchor contains or references these verified facts:

1. **Live rights at the cut.** Exact raw and transformed lot IDs, custodian, item/version,
   interval origin, revision, protection and reservation. This is a validation basis, not a second
   mutable inventory owner. Current custody and production lots retain ownership of spendable contents.
2. **Imported output budgets.** For each pre-cut output still live, retain its original batch/output
   identity, definition pin and exact exported unspent interval set. Retain the producing operation's
   recipe/version and a retrievable witness sufficient for consumers such as installed construction.
   The archived segment preserves the full producing batch and its transitive inputs. Export only
   surviving rights: using the original recipe's full output count would remint its spent prefix.
3. **Raw issuance and retirement coverage.** Retain exact normalized source interval coverage with
   source/item pins and actual issuance-owner boundaries. Gathering cursors, completed excavation and
   funded terrain allocations have different issuance contracts. Generic source capacity cannot be
   treated as an issued prefix. Past custodian legality and allocation constraints must be checked at
   the closing boundary and bound to its admitted policies. Exact archive records retain that history.
4. **Care and resource boundary.** Current progression, permanent definition/permission grants and
   revocations, cumulative care/production totals and the bounded witnesses still referenced by a live
   obligation/current encounter. Completed operation records move into the same authenticated closure.
   Ordinary and aftermath records are not interchangeable.

Segment validation uses exact interval equations, not aggregate quantity equality:

- Imported raw rights plus **actually issued** post-cut rights equal current live raw rights plus
  post-cut direct raw retirements, disjointly and without gaps in each applicable issued partition.
- Each imported transformed budget equals its current live intervals plus post-cut recipe-input and
  consumption retirements. A prior spent interval is outside that budget even if its old batch exists.
- Each new batch's full outputs equal live intervals plus later retirements, using the existing exact
  recipe and acyclic dependency rules. Inputs can reference verified imported output rights or earlier
  batches in this segment. They cannot reference an arbitrary historical batch merely by ID/hash.
- Pre-cut direct raw retirement plus segment retirement remains the authoritative evidence for terrain,
  extraction and receiving-capacity reconciliation. An imported crafted output's raw ancestry is
  historical backing, not newly retired or newly available raw stock.

The anchor is bounded by explicit counts/bytes for live lots, imported outputs, open obligations,
source coverage and required external consumer witnesses. The 4,096 combined live-lot bound provides
one frontier bound; it does not by itself prove a bound on arbitrary interval fragmentation. Canonical
coverage may merge adjacent intervals only when their required meaning/pins are identical. If an exact
coverage set cannot fit its qualified envelope, continuation fails closed as a host capacity condition;
it must not collapse holes, use totals, discard evidence or advertise unlimited lifetime. Paged coverage
is the extension point. Qualification must establish the envelope for every admitted source family.

Preserve cumulative production/care/world revisions. Introduce explicit base counts/revisions plus
inline segment operations where records currently define those counters. A cut is representation
change, not a new craft, heal, consent, clock tick or inventory mutation. Preserve lot/storage revisions
and captured-command meanings. Ordinary successor rules remain append-only within their segment;
a separate exact closure transition authorizes moving records, rather than weakening ordinary
`ValidateSuccessor` into accepting deletion.

## Care/resource coupling and scheduling

The new admission must join archived and inline consumption/effect records without accepting missing,
duplicate or retroactively inserted operations. Completed effects cannot be paid again; new records
must follow the sealed sequence/ordinal boundary. Permanent durable identity/receipt tables remain
authoritative for retired commands, batches, lots and created IDs even when no longer in the live index.
Anchor counts do not replace those records or authenticate a new action.

Flush/rebase resource evidence with the material/care cut, including a material-only boundary with no
new HP/MP effects. Replace the resource anchor's dependence on entire historical CareSnapshot lists
with the explicit jointly validated baseline. Keep immutable grants and revocation witnesses, and
preserve any current episode/obligation witness necessary to validate resumed care and actual timing.
Do not retain every completed use inside an anchor under another collection name.

For a first implementation, allow the joint cut only when there is no pending excavation or unresolved
care acknowledgment. Schedule before acknowledgment if its future treatment needs headroom.
[WorldCareActions.EvaluateAcknowledgment](../../../Runtime/src/OpenFantasy.Core/Worlds/WorldCareActions.cs)
already recognizes acknowledgment, treatment and material capacity together; continuation must keep
that guarantee. Do not strand an accepted pending obligation by discovering capacity only at treatment.

Reserve all potential writers in a selected batch, including craft, construction completion, ordinary
uses and aftermath operations. Cut before interpreting any member, never halfway through accepted
commands. Keep cumulative observational revisions stable across the representation cut so an already
captured exact dose does not become a different request.

**Avoid a selection deadlock:** current public preparation withholds intents at full retained capacity.
A new fresh profile should schedule preemptive cuts before an accepted batch can leave the relevant
headroom exhausted, including the acknowledgment/treatment pair. Alternatively, specify an explicit
serialized non-gameplay continuation transaction. Do not require the user to submit an impossible
healing request to trigger maintenance. The precise headroom formula and publication kind are required
implementation decisions with executable boundary tests, not permission to weaken current preflight.

## Minimum complete implementation boundary

1. **Pure owner contracts and versioning.** New explicit production/care continuation interpretation,
   bounded frontier/coverage/export records, canonical codecs, exact decode-context requirements and
   cumulative counters. Preserve Production V1/V2, Care V1/V2 and all existing profile bytes/meanings.
   Admit a successor profile explicitly; no silent mutation of lived profile25/26 saves.
2. **Complete material composition.** Update selection, consumption, crafting, identity/provenance
   validation and retirement evidence together. The coupled 256-batch limit is part of the increment.
   Lossless current-right validation must pass before any archived provenance becomes nonresident.
3. **Dependent owners.** Bind the same anchor through all nested production-bearing geography and
   liquid codecs/reconciliation, and verify complete construction references. No “production-only”
   head may bypass these owners. The details below are mandatory pending implementation work.
4. **World closure.** Coordinated care/material/resource capacity planning, closed-boundary extraction,
   complete successor admission, potential-writer reservation and atomic rollback. Provide honest
   continuation-needed/host-capacity diagnostics without converting a development bound into a game rule.
5. **Durable host and retrieval.** Explicit storage support, writer-only insertion, real archive retrieval,
   journal replay, exact expected closure scheduling and permanent identity/receipt preservation.
6. **Application and acceptance.** Preserve self/helper authorization, exact captured offers, pause,
   Wait/aftermath boundaries and stock visibility; prove actual raw/crafted use after continuation and
   separate process reopening. Update timeless architecture docs in this same implementation change.

### Dependent owner audit

- [ConstructionSnapshot.ValidateForWorld](../../../Runtime/src/OpenFantasy.Core/Construction/ConstructionSnapshot.cs)
  directly finds `Installed.ProductionBatchId` in `production.Batches`, checks its command, actor,
  custodian, recipe and raw inputs, and requires its live reserved guard lot. Keep a qualified imported
  batch witness or a typed archived lookup that proves those exact facts. Do not unreserve, recreate or
  discard installed output. Construction-only batches still need exactly one completed project.
- [TerrainMaterialProduction](../../../Runtime/src/OpenFantasy.Core/Geography/TerrainMaterialProduction.cs),
  [TerrainMaterialRegistry](../../../Runtime/src/OpenFantasy.Core/Geography/TerrainMaterialRegistry.cs) and
  [ExcavationSnapshot](../../../Runtime/src/OpenFantasy.Core/Terrain/ExcavationSnapshot.cs) need the complete
  direct-raw partition across the cut. Preserve representation-custodian exclusions and issued-prefix
  proofs, not merely global source conservation.
- [WatershedRunoffCapacityState](../../../Runtime/src/OpenFantasy.Core/Geography/WatershedRunoffCapacityState.cs)
  retains production and verifies append-only successor hashes; prepared requests pin their exact
  inventory/production basis. [LiquidReservoirState](../../../Runtime/src/OpenFantasy.Core/Geography/LiquidReservoirState.cs)
  and [ReservoirTransportState](../../../Runtime/src/OpenFantasy.Core/Geography/ReservoirTransportState.cs)
  compose that evidence. Rebase pending requests through the exact closure without changing reservation,
  timing, quantity, geometry or transport effects. Existing ordinary reconciliation cannot authorize it.
- [GeographyProductionCodec](../../../Runtime/src/OpenFantasy.Core/Geography/GeographyProductionCodec.cs)
  currently selects complete V1/V2 owners. Every containing terrain, runoff, capacity, reservoir and
  transport format must accept an explicit continuation context/version or refuse it; no legacy codec
  may silently flatten or drop the anchor. Do not leave duplicate nested unbounded histories in the head.

## Required atomic host protocol

Preserve the established durable model: under the exclusive writer transaction, derive the expected
closing boundary from actual current state, build the continued candidate and action outcomes, and
write archive bytes, accepted batch, receipts, permanent identities, head and checkpoint together.
The archive binds closing sequence N, publishing sequence N+1 and the publishing before-hash. Preview
performs no writes. If a material and resource cut share a publication, their linkage must be explicit
and unambiguous; use a coordinated archive representation or separate versioned rows, not an overloaded
schema7 row whose existing interpretation changes silently.

Recovery replays actual commands and compares actual retrieved rows to the exact expected closing
bytes, decoding with the prior verified bounded context. Missing, altered, reordered, unscheduled,
wrong-scope or trailing material/care evidence fails recovery. Archive traversal may be streamed; the
head and validation context must not retain the full chain. Full historical provenance remains
retrievable. Storage/archive/journal envelope exhaustion is an explicit host stop, not an actor denial
or permission to delete identities. This proposal does not solve unlimited disk or full journal lifetime.

## Executable qualification required before acceptance

- Preserve golden V1/V2 and profile25/26 bytes, old rejections, admission and replay; refuse unsupported
  continued data in old codecs. New canonical round-trips require real retrieved matching anchors.
- Earn more than 256 consumed doses through actual finite admitted supply and accepted operations;
  include raw and crafted stock, two consecutive cuts and correct total revisions. Stress calibration
  is labeled separately from original source statistics and game balance.
- Cross a cut with a partially consumed raw lot, a partially consumed transformed output, multiple
  outputs, changed custody, reserved/installed output and a surviving crafted output whose ancestry
  spans more than 256 batches. Continue crafting from that output across another cut. Bound resident
  state and demonstrate no embedded ancestry chain.
- Attempt reuse of archived source/output intervals under new lot IDs; erase spent prefixes; fabricate
  issuance from capacity; overclaim an exported output budget; substitute recipe/item pins; rewrite
  custody/lot revisions; introduce cycles; delete an ancestral segment; reuse old command/batch/claim
  identities. Each is refused without partial effects or regenerated material.
- Prove exact terrain/excavation coverage and construction/liquid/reservation behavior across the same
  boundary, including pending scheduled runoff and transformed inputs. No isolated potion test closes
  those composed-owner gates.
- At boundary capacity, select multiple potential writers, including a later refusal and a late
  complete-admission rollback. Prove one pre-batch cut and no claim, time or receipt duplication.
  Prove the acknowledgment/treatment headroom guarantee and no blocked-offer maintenance deadlock.
- Preserve active/revoked permissions, archived historical grants, ordinary uses and aftermath
  witnesses; reject retroactive insertion and repeat healing. Reopen, retrieve archives and reuse exact
  receipts without new consequences. Public views expose neither provenance internals nor helper control.
- Exercise external process termination at actual durable before/after signals. Verify exact old/new
  root bytes, archive rows, receipt and identity sets; retry commits once before publication and refuses
  duplicate effects after publication. Managed exception/reopen tests do not substitute for SIGKILL.
- Render real self-use and helper-owned aid after a cut, then reopen in a separate process with pause
  intact. Preserve existing controls, full-dose helper preference and no-focus/shared-desktop rules.

A bounded implementation fixture can extend the fresh stress admission in
[OrdinaryCareArchiveFixture](../../../Runtime/tests/OpenFantasy.Hosting.Tests/Care/OrdinaryCareArchiveFixture.cs):
admit at least 520 finite, physically stored doses and enough initial maximum health to remain alive
after more than 520 doses' worth of actual seeded combat damage. Earn the deficit through real Wait,
attack, victory and release commands, then apply 520 accepted uses across two cuts. Assert the complete
damage sum, dose intervals and cumulative revisions; never edit lived HP, supply or history to fill
the window. Include original-recipe crafting at a boundary, but keep the separate >256-crafted-batch
and surviving-ancestry proof: 520 raw doses plus one craft cannot establish either. These authored
stress parameters prove protocol behavior, not original-stat game balance.

## Retained evidence, not evidence for this proposal

[Ordinary care](../ordinary-care/README.md) and its
[verification](../ordinary-care/qualification/verification.json) retain actual permission, consumption,
resource-cut, receipt and recovery proofs. The
[restorative interface packet](../restorative-interface/README.md) and
[verification](../restorative-interface/qualification/verification.json) retain typed capacity,
actual earned crafting, public controls, helper policy and native reopen evidence. Their resource cuts
retain material/care collections; none demonstrates their lossless continuation.

[Durable resource archives](../durable-resource-archives/README.md) and
[verification](../durable-resource-archives/qualification/verification.json) retain the schema7 atomic
archive/replay and profile25 external-crash precedent. These results are referenced, not rerun here,
and do not qualify a new format, material frontier, profile26 external crash or lifetime ledger.

This investigation changes only this design proposal. No implementation, builds, tests or commits form
part of its source-review assignment. Once an implementation is accepted, its timeless contracts belong
in the maintained production/care/resource/geography/construction/persistence architecture references;
this packet retains design choice, risks and proof history.
