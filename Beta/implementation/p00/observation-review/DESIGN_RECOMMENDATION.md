# Independent observation-projection recommendation

9 September 2026. Reviewer: portable-core critic, independent of the coordinator implementing Observations. This reviewer owns this directory only. Runtime code and other review artifacts are unchanged.

## Small executable scope

Project a bounded full replacement of one actor's committed, acquired counted-resource observations for one presently bound principal/session. A useful fixture contrasts a direct observation of six wood with another actor's reported or inferred count. Do not present custody's current omniscient stock as knowledge, and do not imply that the simple fixture completes Wren's quest.

A concrete current principal/session/world/branch/controlled-actor binding is compared with the requested audience. A stale, absent, revoked or mismatched binding produces one generic failure; an `AccessAllowed` boolean is insufficient. These immutable binding values still depend on future authenticated host construction and do not authenticate their own caller.

The journal is actor-specific and scoped before enumeration. It contains committed observation records with positive source commits at or before an internal pinned authoritative head. It preserves the actor's disclosure epoch/revision and independently persisted disclosure IDs, rather than exposing global event sequence values. Bounded limits apply to the selected actor's journal, never to a global stream whose hidden population could affect visible truncation or failure. Full replacement avoids pretending to implement deletion-aware deltas or a complete history stream.

## Public and private data

Keep source event IDs, authoritative source/head revisions, global candidate/suppression counts, denied reasons and hashes of the unfiltered source private. A newer hidden-only global commit must not alter the public bytes/hash at the same actor disclosure cursor. Exposed disclosure IDs must not alias private source event IDs or encode a global sequence.

The public DTO contains only copied whitelisted semantic fields: already-known audience scope, local disclosure epoch/revision/ID, already-known subject/item definition, quantity, acquired-at knowledge timestamp, explicit epistemic basis, declared confidence and an optional already-known reporter. Direct observation, report, belief, inference and dream impression remain distinct; no ordinary record is administrator truth. A direct observation is evidence from acquisition, not a guarantee that current stock remains the same. A report's timestamp is when the actor acquired the report, not a hidden event's actual time. Confidence is a declared assessment, not a calibrated probability.

Disclosed IDs and item-definition pins must already be known to the observer. An unidentified potion's true kind, hidden affixes, concealed owner or undiscovered quest identity cannot be smuggled through a definition hash or subject ID. The trusted acquisition layer must establish this; the projector cannot infer identification from inventory custody. No generic serialization or automatic record diagnostic should expose private source records. Encode only the public projection schema.

Invalid future/uncommitted, conflicting duplicate, wrong-scope or over-budget journals fail before any partial page is returned. Unknown authorization targets and stale controls must not produce public errors containing private IDs, suppressed counts or different existence reasons.

## Required negative and continuation checks

- Principal, session, world, branch, actor and control-revision mismatches all fail generically. Revocation cannot reuse a cached grant/projection.
- Another actor's observations never enter the selected output or its hash; wrong-scope input rejects before enumerating private records.
- Vary private source event IDs, valid source commits and the internal head while holding public knowledge fixed; public bytes/hash remain identical.
- No public bytes or diagnostic projection expose private source event/commit/head metadata, candidate counts or suppression reasons.
- Known direct observations, reports, beliefs, inferences and dreams preserve their basis, confidence, reporter and acquired-at date. Later world changes do not silently refresh stale knowledge or promote reports into truth.
- Positive committed-source bounds, disclosure-revision bounds, duplicate/conflicting IDs and exact source scope validate. Invalid journals emit no partial projection.
- Empty full replacement is valid; input ordering does not change canonical output; arrays/records cannot mutate accepted projections; bounds reject without truncation.
- Exact public golden bytes are independently specified. Permission filtering occurs before the public encoder receives data, not after a private object was serialized.

## Requirements and limits

The technical design requires filtering before serialization, distinction between observation/belief/inference/administrator truth, absent hidden inventory/coordinates/memberships in ordinary packets and diagnostics, actor-acquired knowledge shared by player/NPC legality, and publication only after durable commit. The current identity, immutable content pins, WorldInstant and command-commit types can be reused without importing the whole custody or history snapshot into a projection.

This increment can prove deterministic audience checks, actor partitioning, committed-input shape validation and public-field noninterference. It does not prove initial authentication, perception/knowledge acquisition, durable outbox publication, deltas/reconnect, an omniscient NPC planner, field identification, live market behavior or human-readable UI presentation.
