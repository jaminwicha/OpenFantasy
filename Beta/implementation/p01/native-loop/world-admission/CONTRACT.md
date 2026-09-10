# Composed local-world admission

`WorldAdmissionValidator.Validate(WorldSnapshot, WorldIdentityIndex? previous = null)` validates the joins between the retained finite contracts and returns an immutable local identity index. The central task invokes it after the snapshot's immutable fields have been assigned and caches the result. Public construction and decoding perform fresh admission; an internal successor construction can supply the previous index.

## What is validated

The counted authority, spatial set and resident constructors already validate their own bounds, scope, actor sets, exact body bindings and primary inventory ownership. Admission joins their identities under explicit semantic roles, including world, branch, realm, actor, principal, session, custodian, lot, counted source, reservation, form, physical body, home place, body definition, injury, terrain formation/source, language/register, utterance, dialogue and named cause reference. Actor and custodian references resolve to their existing roles rather than creating another entity. Seeds and content hashes are value inputs, not entities added to the identity registry.

Incompatible roles cannot alias, even when each individual domain accepts them. Injury instances cannot repeat across bodies. Multiple records may reference the same causal event; its existence and committed semantics remain a journal responsibility. `Contains()` reports current admitted identities and named references, including terrain lineage retained after excavation. It does not cover absent history or authenticate the caller.

Counted inventory and terrain may share a material definition only with its exact version. Terrain source identities remain distinct from admitted counted-inventory sources. Excavating a cell does not mint inventory or confer rights to claim its source in this initial profile. Substrate cells retain source/formation/material fields, use their stratum's exact material pin, and share a formation only within the same XY column and stratum. Repeated source identities are rejected. The terrain generator/decoder remains responsible for producing the actual deterministic terrain; this join does not prove geological history or a coupled extraction transaction.

Shared body and utterance definition IDs require exact content pins. Utterance pins include channel, channel version and text through the existing canonical utterance encoder. A local comprehension rules pin is consistent across residents; each exact language/register/mode channel has one definition version. Distinct modes or registers do not imply fallback comprehension. Language and register identities remain different semantic roles.

Every resident, whether player or host controlled, requires an explicit current counted-authority control binding. Host autonomy receives no implicit control grant from the controller-kind label. Existing counted-domain checks retain one actor binding and one principal per session. The validator does not authenticate those supplied bindings.

An active dialogue resolves to the speaker's authored utterance and the existing listener/speaker actors. This local dialogue profile currently requires Spoken mode, current speaker `Vocalization`, current listener `Hearing`, explicit Conversational comprehension or better for both participants in that exact channel, and Reachable local interaction geometry. Other authored channel data can exist without an active dialogue. An impaired actor may retain an authored utterance; owning that text does not imply current expression ability. The speaker channel check is an explicit local profile prerequisite; receptive comprehension is not a general expressive-language skill grant, and no capacity is inferred from species, culture or appearance.

The root's advancing-step count cannot exceed its committed world revision. The counted inventory revision is a separate counter and is not compared with world revision. Existing session construction checks utterance references, listener uniqueness, limits and start time against the current calendar.

## Reuse and cost

Fresh admission visits all admitted terrain cells and builds an immutable terrain identity layer. Its cost and retained storage are linear in the terrain identity count: at most 16 regions of 16,384 cells, plus their formations and material pins. Temporary formation descriptors verify repeated formation references. The bounded dynamic side includes counted contents, bodies/injuries, controls, resident channels and dialogues.

A previous terrain layer is reusable only if the region count and every ordered region object reference are unchanged. Terrain snapshots are immutable. A new excavation snapshot, replaced region, reordered dependency set or freshly decoded equivalent region causes a full rebuild. There is no global cache, hash-only reuse, unbounded cache service or stale dynamic-index reuse.

When the terrain layer is reused, current dynamic identities and pins are still checked against it. The new dynamic identity map is separately owned, preserving the prior index. This avoids a full terrain scan for an otherwise unchanged 20 Hz local step. This is a complexity argument and a tested reuse condition, not a measured performance or memory-budget qualification.

## Verification boundaries

Retained tests under `tests/OpenFantasy.Core.Tests/Worlds/Admission*` cover cross-domain aliases, inventory/material pin disagreement, uncoupled terrain rights after excavation, player and host control omission, language roles/pins, utterance content changes, injury/cause identity rules, impaired and incomprehending dialogue participants, out-of-range and unsupported-channel sessions, independent revision counters and immutable terrain-layer reuse/invalidation.

The pinned Debug execution passed all 19 authored cases, plus nine other admission-named cases selected by the broader filter. `VALIDATION.json` records the exact names, current source and tested assembly hashes; `test-results/world-admission-debug.trx` retains runner evidence. This artifact does not claim full cognition, authored skill acquisition, source-budget extraction, generation lineage history, remote authorization, historical identity completeness or production-scale performance.
