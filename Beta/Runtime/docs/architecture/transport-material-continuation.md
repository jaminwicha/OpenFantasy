# Reserved transport across material history boundaries

## Complete owner and representation cuts

[ReservoirTransportState](../../src/OpenFantasy.Core/Geography/ReservoirTransportState.cs) owns the
physical reservoir/capacity wrapper, fixed gate catalog, retained arrival journal and processed calendar
boundary. `PlanContinueTransport` selects Production V7 from this actual complete closing owner.
`PlanContinue` derives further segments with the same capabilities. Optional gathering and reserved
construction capabilities compose with transport; terrain representation V6 requires a separate joined
contract and cannot be silently included.

A cut preserves exact inventory, storage and material revisions, cumulative counters, every source hold,
incoming obligation, closed reservation, gate, route, stage status and resolution time. It neither delivers
water nor advances the clock. Its changed representation hash invalidates plans captured before the cut.
The [boundary observation](../../src/OpenFantasy.Core/Geography/TransportMaterialBoundary.cs) pins the
complete closing transport hash and fixed domain/geometry/gate catalog without retaining a recursive
parent-owner chain. The transport qualification belongs to that exact material context and catalog.

Generic Production cuts and independent lower-level capacity cuts refuse a V7 owner. A new transport
catalog cannot be created around arbitrarily continued Production. Ordinary physical reconciliation
retains the same context and operation prefix; it cannot replace a complete-owner cut. The finite stage
and closed-reservation journals remain retained across material cuts, including outcomes that made no
material operation. Material continuation does not make those journals unbounded.

## Delivery as a typed material operation

[ReservoirTransportEvidence](../../src/OpenFantasy.Core/Geography/ReservoirTransportEvidence.cs) is an
opaque observation created inside actual due-stage planning. It binds original funding, exact source and
receiver intervals, catalog, due/completion times, preceding transport/inventory/Production hashes, result
inventory and created fragments. The preceding transport hash observes the current per-stage owner at
the target boundary, including earlier same-step outcomes. It need not equal the published step's old hash.

The existing stage identity remains a journal role. Delivery has a distinct deterministic cause,
`DeliveryCause(stageId)`, using the reserved maximum spawn ordinal; fragment ordinals retain their
ordinary bounded range. Scheduling checks both prospective identities but spends only the stage identity.
Successful delivery returns the cause and any new fragments for permanent identity publication. Continued
scheduling and advancing require actual historical lookup, including no-op and reading-hold advances.
Pruned material records never release an old identity for reuse.

The live planner and ordered material replay share the same reserved-custody partition kernel. The first
fragment retains its original lot identity at the next revision; additional fragments derive from the stage.
All exact source intervals move once. Each touched storage and complete custody advance once, and one
V7 operation advances Production without changing craft/consumption counts. A dry stage records its
explicit delivery and custody revision with no touched storage or transferred quantity.

Replay verifies the exact preceding material prefix and fixed catalog, reruns the partition, and compares
the complete result and created identities. A fabricated balanced inventory or a genuine proof from a
different preceding Production history cannot substitute. Construction completion retains its own distinct
opaque evidence when both capabilities are selected.

## Atomic physical acceptance and time

Tentative fragments, delivery evidence, material history and capacity completion remain private until
every gate accepts the combined raw/produced final head. A rejected stage retains all source stock and
incoming holds, publishes no cause or fragment, and consumes no shared gate throughput. Canonical due
ordering allows a later independently funded stage to succeed when an earlier candidate fails.

The 512-operation active material envelope is unchanged. A full window blocks material delivery while
retaining its funding. A qualified cut opens another material window but preserves the processed boundary;
repeating that boundary grants no second allowance. Only the next accepted exploration step can retry.
Reading holds advance their selected boundary without transferring material. These rules preserve the
[transport clock and physical contracts](reservoir-transport.md).

## Explicit component archives and trust

[Transport V3](../../src/OpenFantasy.Core/Geography/ReservoirTransportCodecV3.cs) nests reservoir,
capacity and runoff/request V4 frames, retaining Production V7. Earlier geography V3 remains the
Production V3/V4/V5 interpretation; inline transport V1/V2 retain their original bytes and rules.
Unsupported capability downgrades refuse instead of discarding evidence. Existing collection and byte
bounds remain in force.

[GeographyProductionDependencies](../../src/OpenFantasy.Core/Geography/GeographyProductionDependencies.cs)
requires the actual complete transport owner for this interpretation. It pins the full current transport
hash in addition to the context, catalog qualification and separately supplied genuine delivery and optional
gathering/construction evidence. Exact hash validation also rejects structurally legal rewrites of gate
allowances, stage outcomes, revisions or time that material replay alone cannot detect. Capacity-only or
reservoir-only dependencies cannot grant this qualification.

These supplied dependencies qualify component reconstruction; they do not authenticate durable storage.
The integrating authority must retrieve actual closing history and proofs, maintain permanent identities,
and publish archive, receipt and world-head changes atomically. Complete-world continuation remains a
separate protocol joining resource/care, terrain/excavation, liquid placement and history ownership. This
component does not introduce a new world profile, save migration or presentation binding.
