# Source audit and chosen implementation boundary

Read-only independent audits inspected construction and terrain/liquid consumers at `00127c4`.
Their recommendations below are implementation constraints, not executed qualification.

## Construction

`ConstructionSnapshot.ValidateForWorld` requires the exact installed guard lot and original batch:
command, builder, custodian, recipe/version, and all source-only ingredients. Guards retain quantity
one, exact output `[0,1)`, original revision, project reservation and no protection. Every construction
recipe producer must belong to one completed project, including imported producers after a cut.
Quest funding, access, tool pins, builder ownership, advancing steps and experienced work remain exact.

The generic producer witness retains a canonical batch hash but no ingredients. Hash-only resolution
cannot establish the raw-only condition. A bounded set of at most 32 original construction batches,
with at most 64 direct source ingredients each, can retain these exact facts without recursive
ancestry. Imported records must match the actual basis witness hash and identity/recipe pins.

V4 ordinary craft spends unreserved inputs and makes unreserved outputs. Construction has different
rights: it retires only the exact project-reserved stock and reserves its installed output. A separate
opaque completion proof and typed installation operation are required; widening generic Craft would
change permission semantics. Select an explicit V5 interpretation and preserve V1–V4.

Physical snapshots already preserve existing member condition/backing and reconcile new completions.
Bearing models and carried-mass calculations derive their facts from admitted construction; they need
correct historical backing admission, not another provenance ledger. Prove installed backing counts
once and byproducts remain carried material.

A supplied source-world clock is not automatic proof of fresh work. Keep a bounded per-builder
accepted cutoff, require a later actual advancing/experienced-time frame, and refuse duplicate,
backward or historically occupied work commands. Partial work must not create a material operation;
completion returns all affected owners atomically. Refusal preserves the actual supplied owners.

## Terrain and liquids: mandatory subsequent work

- Terrain material validation needs imported direct raw retirements plus current direct retirements.
  Those facts have source/item/custodian pins; they lack historical lot IDs/revisions. Do not fabricate
  complete `RetiredIngredient` records. Keep the legacy complete-history API strict.
- Cross-allocation coarse/fine custodians cannot retire allocated source material. This prohibition
  must inspect imported ranges too, so a cut cannot hide forbidden retirement.
- `TerrainMaterialPartition.Build` mutates custody, split/coalesced identities and both representation
  storage revisions while retaining production. V4 cannot replay it. It needs a typed representation
  transition, not simulated ordinary transfers or removal of the continuation guard.
- Runoff requests, prepared runoff, capacity owners and reservoirs each hash a production-bearing
  frame. All nested codecs need explicit continuation interpretation and exact context/proof threading;
  changing only an outer codec is insufficient.
- A pure capacity cut must re-prepare pending obligations using their identical domain, reservation,
  source intervals, forcing window and inventory. Contributions, incoming totals, projections,
  capacities and logical revision stay exact even though representation hashes change. Old plans
  become stale. Ordinary reconciliation must still reject a context swap.
- Liquid-cell flow changes placement, not material custody. It can prove actual continued mutation
  after a qualified reservoir rebase. Placement cannot coexist with pending runoff, so pending
  preservation/cancellation and no-pending flow require compatible separate journeys.
- Reserved reservoir transport moves intervals, clears reservations and changes custody revisions;
  neither V4 raw transfer nor delivery has that meaning. A purpose-qualified typed transition is needed.
- Excavation partial work spends gathering effort without issuing material; completion additionally
  issues source rights and removes terrain. Both stages need a joined contract, distinct from gathering
  proof. Existing unsupported-effort refusal remains a required control.
- Continued terrain crafting must pass the permanent historical identity predicate through its wrapper
  to `InstantCraftPlanner`, rather than relying only on preliminary identity checks.

All complete-world admission remains guarded pending coordinated material/care/resource and Host
archive qualification. Neither audit establishes playable continued worlds, archive authenticity,
production scale, or native acceptance.
