# Actor-scoped resource knowledge projections

9 September 2026. Retained P00 behavior for RQ-040–046/059/071/078/079/097/108/110/114/115/120/121/135/145/173. This implements a bounded full projection of one actor's acquired resource-count claims. It establishes concrete ownership, redaction and public revision fields before P01/P02 authority and networking work. It does not implement perception, authentication, belief formation, dialogue, market simulation, transport, a durable outbox or a complete knowledge archive.

## Authority inputs and disclosed claims

`ObservationScope` names one world, branch and actor. `ObservationAudience` additionally names the principal, session and positive control revision. Construction supplies values, not rights. The host must resolve the current binding from authenticated membership/control state, independently of the client's request. Player and NPC planners can use the same scoped knowledge; neither gets authoritative custody as a substitute for acquired information.

`ResourceObservationView` contains one **claim** about a known subject and exact known counted-material definition/version, quantity, acquisition timestamp, actor-local disclosure identity/revision, knowledge basis, confidence and an optional known reporter. A zero count is a real claim of zero, not absence. Maximum UInt64 count is representable. Basis is DirectObservation, Report, Belief, Inference or DreamImpression. No basis is encoded as administrator truth. Direct observations may be incomplete or affected by supported perception effects; reports and dreams cannot silently become verified inventory. Confidence 0..1000 is a declared assessment scale, not a calibrated probability or proof.

Subject, material and reporter identities must already be known to the actor. An unidentified object cannot disclose its actual hidden item/affix through the definition field. The acquisition owner must resolve apparent identity and permitted precision before construction. The projector has no lookup of hidden custody, definitions, affiliations or world facts and cannot independently prove identification. Omitted reporter means unknown/not disclosed; an all-zero but present identity remains distinct from omission. Only a report may name its known reporter. The acquisition timestamp is part of what the actor learned, not a lookup of the described event's secret true time.

`CommittedResourceObservation` holds the actor scope, disclosed view, private source-event provenance and `RecordedCommitSequence`. The sequence names the durable commit that recorded **this exact version of the actor's acquired knowledge**, not the older event being described or an earlier version's initial acquisition. It must be positive. A caller claiming a number cannot prove that the commit occurred; P01 must establish it from the actual committed store. The source-event identity is private provenance and never becomes the public disclosure identity. Server containers are ordinary sealed classes, avoiding record-generated diagnostics that print all private fields.

## Finite actor journal and binding checks

`ResourceKnowledgeSnapshot` consumes one actor's complete current resource-knowledge view, a disclosure epoch/revision and a pinned authoritative head. Every record must have the same world/branch/observer scope, a positive recorded commit at or before that head, and a positive disclosure revision at or before the declared public revision. Disclosure IDs and revisions must each be unique. Future, uncommitted, foreign and duplicate records reject the entire candidate. Construction does not silently skip records or return a partial page.

Entries sort by actor-local disclosure revision and are defensively copied. The present limit is 1024 entries in this actor-specific operation; the first excess entry rejects without enumerating an unbounded source. Never scan a global hidden-event stream under this limit: unrelated actors or hidden activity must not alter visible truncation, continuation or rejection behavior. This bound does not permit deleting an actor's causal history or other knowledge. Larger current views require qualified expansion/paging and retained archive semantics.

All private source-event IDs are checked against every public identity known locally: disclosure IDs, subjects, material definitions, reporters, scope and epoch. Cross-record aliasing rejects as well as same-record aliasing. The projector additionally checks principal/session identities against private sources. Whole-world insertion must still establish identity domains/collision freedom and acquisition legitimacy; the local intersection check is a concrete additional defense, not a global identity registry.

`ResourceObservationProjector.Project(request, currentBinding, snapshot)` requires exact equality across the current principal/session/world/branch/actor/control revision and the requested audience, plus exact journal scope. A missing/revoked binding, stale control revision, mismatched scope or audience/provenance alias produces the same `ObservationAccessException` message: “The observation view is unavailable.” It contains no requested/actual private identity or reason. No public projection is returned on failure. A permitted projection retains only the immutable disclosed views and known audience/disclosure fields, never the journal object, private causes or authoritative head.

## Public revisions and recovery

The public epoch and revision belong to this actor's disclosure stream. They must be assigned and persisted when its visible knowledge changes, not derived from global commit counts, hidden events, worker order or private source ordinals. The inserting authority must preserve stable disclosure identities. This increment validates supplied candidates but does not advance these counters or prove their transactional history.

The output is a **complete replacement of the resource-knowledge view** for the exact audience and disclosure epoch. It is not a delta and not the whole actor model. An empty view clears this domain; an empty revision-zero initial view is valid. Gaps in retained entry revisions are valid after visible revisions/removals, with real historical causes retained elsewhere. An unchanged view under a newer hidden authoritative head has identical canonical bytes and hash. Private source metadata changes alone likewise cannot alter public bytes or diagnostic serialization of the public DTO.

Clients must apply only packets for their current binding/session and follow the declared epoch/revision recovery protocol. Revocation does not erase data already received. An already-created immutable projection remains encodable; actual delivery and cache reuse must revalidate the current binding. This increment does not implement client application, cache eviction, transport races, authenticated reconnect or atomically published outbox delivery. P01/P02 must connect the binding, accepted snapshot and emission to actual committed state; P03 must test late packets, handoff and rejoin. A constructed full snapshot does not prove these operations occurred.

## Canonical public format

The encoder accepts only `ResourceObservationProjection`. Existing `OFC\0` framing uses big-endian UInt32 framing version/count/lengths. Each frame below begins with UInt64 contract version 1; IDs are 16 bytes, hashes 32, numeric values UInt64 except signed acquired days. An absent reporter is an empty field; acquisition milliseconds remain normalized 0..86,399,999.

| Frame | Fields after version |
|---|---|
| `observation.audience` | principal; session; world; branch; actor; control revision |
| `observation.resource` | disclosure ID; disclosure revision; known subject; known material ID; material version; quantity; basis; confidence; acquired days; acquired milliseconds; optional known reporter |
| `observation.resources` | ordered complete resource frames |
| `observation.snapshot` | audience frame; disclosure epoch; disclosure revision; resources frame |

SHA-256 of these bytes hashes only the public projection. It never hashes a prefiltered global journal or includes hidden counts, global revisions, private source commits/events, suppression reasons or secret diagnostic text. The output has no arbitrary string/payload field through which a world snapshot or unrestricted dialogue transcript can be attached. A bounded decoder and actual client application are still required before accepting untrusted wire input.

## Retained cases and next integration

The independent Python stdout oracle supplies three public vectors: empty initial view, a three-entry view with direct/reported/dream claims, and an empty later replacement. It exercises negative acquisition dates, minimum signed day, maximum quantity, optional reporter and noncontiguous visible revisions. The C# tests compare exact bytes/hashes, hidden-head/private-cause noninterference, cross-actor isolation, every audience dimension, stale/report preservation, invalid provenance aliasing, future/duplicate rejection, immutable collections, finite enumeration and explicit clearing. The coordinator and independent critic retain actual results separately.

The six-wood example demonstrates one disclosed claim; it is not an executed Wren observation, a custody proof or an imported quest. Later acquisition must connect real senses, body functions, language, testimony, uncertainty, learning and causal records. A successful comprehension query alone neither admits a report nor verifies it. NPC motives, preferences, commitments, directed relationships and actual feasible decisions remain required cognition work. Native UI and AI proposals must consume permitted projections; they cannot query private source state to make a nicer explanation.
