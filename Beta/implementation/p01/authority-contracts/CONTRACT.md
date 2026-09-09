# Counted transfer authority

This P01 increment performs actual immutable transfer adjudication against explicit current control, custody access and finite source admission. It calls the existing `CustodyTransferPlanner` only after access checks, then returns a complete candidate state and receipt for the caller's next world commit. The portable core performs no IO, authentication handshake, durable write, retry lookup, actor simulation or content migration.

The runtime family is `OpenFantasy.Core.Authority`. Its state is private authority data, including access and inventory facts; it is not a client projection. The host must publish only the committed receipt and permitted observations, never serialize the full candidate into an ordinary gameplay response.

## Concrete state and admission

`CountedAuthorityState` takes a complete `CustodySnapshot`, positive `admittedUnitCapacity`, explicit actor identities, control bindings, custodian policies, admitted sources and optional reservation identities. It copies and canonically sorts all inputs. These actor IDs are counted-domain participants, including uncontrolled NPCs; they do not stand in for full actor anatomy, needs, cognition or lifecycle state.

The existing custody snapshot supplies world, branch, branch revision, every included storage and its complete counted lots. The state adds these concrete rules:

- `ActorControlBinding(PrincipalId, SessionId, ActorId)` supplies one current controller per admitted actor. One principal/session may control several actors. A session cannot name different principals. An actor can remain admitted with no current control binding.
- `CustodianAccessPolicy(CustodianId, OwnerActorId, DepositRule)` gives exactly one admitted withdrawal owner to every included storage. `OwnerOnly` accepts deposits from that owner; `AnyControlledActor` also accepts deposits from another actor whose current control was admitted. Public receiving grants no withdrawal right. These policies are a concrete finite-domain authorization rule, not a simulation of theft, law, consent negotiations, offices or delegated mandates.
- Every storage requires positive counted-unit capacity. These integer budgets are not mass, volume, stack count or physical packing measurements.
- `AdmittedCountedSource(SourceId, Definition, UnitCapacity)` defines a positive, finite half-open source interval `[0, UnitCapacity)` and one exact `CountedItemDefinition` identity/version. Every lot must resolve to that admitted source and exact pin, and its interval must fit. Existing custody validation also rejects overlapping intervals, absent storage and excessive stored units.
- The sum of **all admitted source capacities**, including sources with no current lots, must fit `admittedUnitCapacity`. Accumulation uses UInt128 so a UInt64 wrap cannot disguise excessive admission. This is a finite world/branch profile budget measured in counted units. It does not assert that unlike items have equal physical size or that other branches have been globally accounted.
- One definition identity must use one accepted version across all admitted sources. Sources remain admitted when their current custody is empty; moving a lot cannot forget a reservoir's identity or its original budget.
- A non-null lot `ReservedBy` must resolve to an explicitly admitted reservation identity. These are opaque reservation/job identities; this increment neither creates jobs nor releases their reservations. Reserved lots remain unavailable to this transfer action.

Known world, branch, actor, principal, session, custodian, lot, source, definition and reservation identities must be disjoint **between kinds**. Repeating the same actor reference or the same versioned material definition is legitimate; treating a source as an actor, or a session as a principal, is not. Canonical entity ID zero remains valid if it does not conflict. Within each identity collection or keyed row set, duplicates reject.

Current qualification limits are 1,024 actors, 1,024 control bindings, 1,024 admitted sources, 4,096 reservation identities, plus the existing custody limits of 256 storages and 4,096 lots. The first excessive enumeration entry rejects before continuing. These are finite profile/format limits, not permission to discard world actors, inventories or source rights. Broader or paged authority needs a qualified extension and completeness contract.

State construction and decoding validate the consistency of supplied admission data. They do not prove that a caller is allowed to invent a reservoir or install new control policies. Initial admission and any later policy change are trusted host operations that must be committed through their appropriate authority transaction. This transfer processor cannot create, expand, remove or change source admission, control bindings, owners, deposit rules or reservation identities.

## Authentication, access and fresh decisions

`TrustedCommandDelivery(PrincipalId, SessionId)` is supplied separately by the host's authenticated delivery adapter. Constructing the value does not authenticate a connection. `state.CanControl(delivery, actorId)` checks those supplied identities against the current binding and performs no network or credential verification.

The exact decision API is:

```csharp
AuthorityDecision decision = new CountedTransferAuthority().Decide(
    state, command, trustedDelivery, nextWorldCommit);
```

For a **fresh admission**, the processor requires this branch, matching submitted and authenticated principal, immutable command origin session equal to the current delivery session, and current actor control. It then requires that actor to own the declared source storage and be permitted to deposit into the declared destination. These gates run before looking up the submitted lot or evaluating any expected revision. Missing/inaccessible source or destination policy and invalid control/scope all receive the same public `NotAuthorized` rejection.

The caller must dispatch a command to the correct world's service **before allocating a commit**. A command or supplied commit for a different world is a programmer/dispatch precondition failure (`ArgumentException`), because a valid receipt cannot attach another world's command to this world's commit identity. The host must turn cross-world input into its generic transport rejection; it must not publish exception text. A wrong branch within the selected world produces the ordinary uniform `NotAuthorized` candidate receipt.

After access succeeds, expected revision targets must be exactly the branch, selected lot, source storage and destination storage. Extra revision targets always reject, even if the caller guessed a valid hidden entity and its current revision. This avoids a success/failure oracle for unrelated private objects. The planner then checks the actual revisions, lot membership, reservation/equipment restrictions, quantity and destination capacity, and constructs the real full or split transfer candidate.

All unavailable private resources, revision disagreements, unsupported expected-target sets, known identity collisions and checked revision overflow receive the same public `ResourceUnavailable` reason. A profile already at 4,096 lots rejects a partial transfer that would allocate another lot, while a whole-lot transfer remains eligible. This explicit capacity guard does not catch unrelated programming exceptions or drop existing lots. No exact private conflict details are attached to the receipt. This is a semantic disclosure boundary, not a cryptographic constant-time guarantee.

An accepted command cause ID cannot alias an admitted identity. A derived split child is checked against the complete current authority identity registry, including uncontrolled actors, other controllers' principals/sessions, unused admitted sources/definitions and reservation identities. The existing planner also checks its own custody and envelope identities. The host must still enforce its broader world/historical identity registry; an identity absent from this finite state cannot be proven globally unused by this processor.

Humans and NPCs use the same envelope, authenticated controller context, ownership/deposit rules and planner. No human/NPC discriminator bypasses a rule. Favorite, heirloom and user-reserved designations retain the existing explicit-transfer semantics and survive partition; equipped lots and active job reservations reject. This action does not implement automatic ingredient selection or authorize a protection override for crafting.

## Candidate, durability and retry boundary

`AuthorityDecision` exposes:

- `BaseStateHash`: the full original authority hash, binding access/source admission as well as custody.
- `Candidate`: the complete applied replacement, or the exact original state object for a rejection.
- `Receipt`: applied/rejected `CommandReceipt` with the caller-supplied positive world commit identity.
- `MovedLotId`: actual moved/split identity for applied decisions; null for rejection.

An applied transfer advances custody and affected storage/lot revisions according to the existing planner, preserving the exact source intervals and pin. A rejection does not advance custody revision or alter access/source data. Both kinds of receipt can be durably recorded by the host. The host must atomically commit the candidate, receipt, command identity/content, commit sequence and any other required authority effects, verify the base against current persisted state, and publish only after durable success. Merely constructing `nextWorldCommit` or a receipt provides no durability or monotonic sequence proof.

Retries are owned by the durable store. It authenticates the current delivery and applicable receipt-access policy, locates the world/principal/command ID key, compares exact canonical content and returns the prior committed receipt. An immutable original envelope is not rewritten with a reconnect session; branch or other content changes under the same key are conflicts. `CanControl` is available for checking an installed current binding independently of the old envelope's origin. Reconnect itself grants no control: installation/revocation of bindings requires the host's proper authority transition. The processor's `Decide` method is for fresh admissions; rerunning it on a previously applied envelope can produce a stale-state rejection and is not a substitute for durable retry lookup.

The processor neither extracts new stock nor creates fresh source claims. It handles only the existing untransformed single-source lots. These branch-local checks are not a global transit ledger, cross-branch export allowance, geology/renewal proof, mixed-material provenance system or migration of original items. The qualification stock of eight wood units and its capacities are explicit test data.

## Exact wire format

The existing canonical framing remains unchanged: `OFC` plus NUL, framing version 1 as UInt32 big-endian, UInt32 field count including tag, followed by UInt32 byte lengths and exact field bytes. IDs are 16 canonical bytes; hashes are 32 bytes; numeric values are eight unsigned big-endian bytes. There is no text normalization, field omission or JSON persistence substitute.

| Tag | Ordered fields after tag |
|---|---|
| `authority.counted-state` | authority version `1`; admitted capacity; complete custody v1 frame; actor collection; control collection; policy collection; source collection; reservation collection |
| `authority.actors` | actor IDs in strictly increasing canonical order |
| `authority.controls` | complete control rows ordered by actor ID |
| `authority.control` | principal ID; session ID; actor ID |
| `authority.policies` | complete policy rows ordered by custodian ID |
| `authority.policy` | custodian ID; owner actor ID; deposit rule (`1` OwnerOnly, `2` AnyControlledActor) |
| `authority.sources` | complete source rows ordered by source ID |
| `authority.source` | source ID; definition ID; exact definition version; source unit capacity |
| `authority.reservations` | admitted reservation IDs in strictly increasing canonical order |

The outer authority version binds all nested authority row schemas. The existing custody v1 bytes and tags are embedded unchanged; its decoder validates that complete subframe. World/branch/revision occur once in that custody payload, avoiding two competing copies.

`AuthorityEncodingV1.MaximumStateByteLength` is 1,500,000. Nested collection byte bounds are 20,544 actors, 100,000 controls, 23,000 policies, 128,000 sources and 81,984 reservations; one authority record is at most 128 bytes. The embedded custody frame retains its existing 942,753-byte maximum. Limits are checked before expansion; unknown versions/types/rules, bad widths/counts, unsorted/duplicate rows, inconsistent admission, truncation and trailing bytes fail with `AuthorityFormatException`. A corrupt nested custody frame is also rejected through that authority exception boundary. Decode reconstructs and validates concrete state; it never discards malformed rows or grants new permissions through defaults.

## Retained verification

[generate_authority_vectors.py](../../../Runtime/tests/OpenFantasy.Core.Tests/Fixtures/generate_authority_vectors.py) is an independent Python standard-library oracle. It emits stdout without writing expected files. Its frozen [authority-v1.json](../../../Runtime/tests/OpenFantasy.Core.Tests/Fixtures/authority-v1.json) includes original/candidate authority frames and hashes, exact command/applied/rejected receipt bytes, owner-only and reserved reload states, and **47 deliberately invalid state frames**. The original frame is 1,229 bytes and the split candidate 1,438 bytes. Worker execution independently reproduced the 174,601-byte fixture exactly; fixture SHA-256 is `db99a9ce599ddd768f0e9b94a8feed3ce433f83a98854a63ef653b01db6bfa74`.

Retained C# tests cover real full/split transfers, unchanged rejection state, source budgets/pins, identity kinds, current control and revocation, receiving consent, actor/controller parity, extra-revision privacy probes, known child collisions, source/protection/reservation retention, wrong scope, revision overflow, defensive copies, canonical permutations, maximum profile sizes and early enumeration stop, exact independent vectors, strict reload behavior, every incomplete original prefix and oversized input. Executed .NET results and durable/crash/retry qualification belong to the coordinator; this worker does not claim an unrun build or persistence test.

Remaining work includes the durable service's atomic commit/retry/global identity enforcement; control-change and admission commands; full actor state and lifecycle rights; spatial/perception/action eligibility; actual source extraction and renewal; reservation lifecycle; physical storage; transformed production claims; temporal transit; content import and playable presentation. These obligations remain explicit rather than being implied by a successful local transfer decision.
