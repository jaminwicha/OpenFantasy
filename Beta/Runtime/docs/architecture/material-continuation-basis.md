# Material continuation basis

## Domain contract and authority boundary

[ProductionContinuationBasis](../../src/OpenFantasy.Core/Production/Continuation/ProductionContinuationBasis.cs)
extracts an immutable observation of material rights from a counted inventory and its production
owner. `Capture` calls `ProductionSnapshot.ValidateForAuthority` before deriving any records. World,
branch, exact inventory/production hashes and absolute production/custody revisions bind the result
to those supplied owners. Capture neither edits them nor changes their active history windows.

Structural material validation is distinct from authenticated world admission. A source's capacity
limits its possible units; it does not prove which units gathering or excavation has actually issued.
The [complete world](../../src/OpenFantasy.Core/Worlds/WorldAdmissionValidator.cs), source owners and
durable command history establish those facts. A captured basis, checksum or batch witness cannot
independently authorize spending, restore an inventory, grant control or prove an archive's origin.

## Surviving lots and output rights

The basis copies every surviving raw and transformed lot with its identity, custodian, exact item
pin, source/output interval, revision, protection and reservation. Those individual lots preserve
possession and selection restrictions. Capture performs no transfer, unreservation, revision reset
or new issuance.

[ImportedOutputBudget](../../src/OpenFantasy.Core/Production/Continuation/ImportedOutputBudget.cs)
groups transformed lots by producing batch and output index. Its intervals contain only the live
union, sorted and coalesced where adjacent. Previously consumed or recrafted intervals stay outside
that union even when the original recipe produced a larger quantity. Gaps cannot be filled by totals.
`Covers` checks a proposed interval against the same batch, output index and item definition/version;
it does not establish permission to spend the covered lot.

Output budgets may combine adjacent rights held in different lots or custody. The separate copied
lots remain necessary: coverage is a provenance constraint, while custody, protection, reservation
and expected revisions constrain an action. A budget must never replace those selection checks.

## Direct retirement and producer witnesses

[DirectRawRetirementRange](../../src/OpenFantasy.Core/Production/Continuation/DirectRawRetirementRange.cs)
records raw ingredients retired by actual retained recipes or consumption claims. Normalization
merges adjacent intervals only for the same source, item pin and retiring custodian. Holes and
custodian boundaries remain explicit. Retirement grants no live right. Consuming or recrafting a
produced output does not retire its raw ancestry again; that direct disposition already belongs to
the producing recipe's history.

For each producer with surviving output, a
[ProductionBatchWitness](../../src/OpenFantasy.Core/Production/Continuation/ProductionBatchWitness.cs)
retains its batch, command, actor, custodian, recipe/version and hash of the complete canonical batch.
The witness contains no ingredient list, predecessor root or recursive ancestry. Entirely disposed
producers need no live-output witness in this observation. Their detailed provenance still belongs
to the complete source history and, under a qualified continuation protocol, retrievable archives.

A witness hash identifies a record; it does not authenticate that record or supply its ingredients.
Consumers such as installed construction require the full facts they validate.
[Construction continuation](construction-continuation.md) retains bounded original source-only batch
records matched to these witnesses for structural component validation. Authenticating their durable
origin still requires actual archive retrieval and complete-world replay.

## Bounds and canonical observation

Combined surviving lots, output budgets and producer witnesses each have a 4,096-record envelope.
Direct raw retirement has its own 16,640-range envelope, derived from the complete bounded production
owner's maximum recipe inputs and consumption claims. Raw retirement fragmentation is not bounded
by surviving lot count: disjoint historical holes or alternating custodians can remain after all
associated live lots disappear. This finite capture bound establishes no unlimited lifetime promise.

[The encoding](../../src/OpenFantasy.Core/Production/Continuation/ProductionContinuationBasisEncoding.cs)
uses deterministic ordering, explicit versioned frames, exact definition pins and an 8 MiB byte
envelope. `Encode` returns fresh observation bytes; `ContentHash` hashes that representation. There
is no standalone decoder or public constructor accepting caller-provided grants. Recreating the
observation requires its actual inventory and production owners. Encoding does not select a world
profile, migrate a save or remove a retained collection limit.

## Continuation composition contract

A material continuation must bind these observed rights to a verified complete closing world and
actual retrieved history. Successor validation must conserve imported rights, genuinely new issuance
and subsequent dispositions, retain historical identities and preserve cumulative revisions. Care
causes and resource effects must close at the same boundary as the consumed material they reference.
Terrain, excavation, construction and nested liquid owners must recognize the same interpretation.

The capture operation supplies no such transition. [Production continuation](production-continuation.md)
uses a separately bound context for structural material operations; it does not grant coordinated
world publication. The complete production V1/V2 owners and their append-only successor checks retain
their own contracts. See [item consumption](item-consumption.md)
and [resource archive continuation](resource-archive-continuation.md) for the publication, replay,
capacity-scheduling and cross-owner obligations.
