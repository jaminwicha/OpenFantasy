# Actor-owned language and register comprehension

This P00 increment implements bounded, immutable comprehension profiles and exact band queries from [Light language integration](../../../spec-v1_1/LANGUAGE_AND_AFTERLIFE.md#light-language-integration), alongside the [technical design knowledge boundary](../../../spec-v1_1/TECHNICAL_DESIGN.md). It supplies retained primitives for player and NPC actors without implementing dialogue, translation, learning, localization, perception or knowledge mutation.

## Expected behaviors defined before implementation

- Two actors may have different comprehension of the same language/register/mode, without copying another party member's knowledge.
- Nuanced common speech does not improve an unfamiliar written or signed channel, another language, or a separate professional cant.
- A professional cant can exceed an actor's common-register band without granting guild membership, trust, hidden affiliations or the truth of a statement.
- Missing exact channels are unfamiliar. An explicitly recorded unfamiliar channel remains distinct metadata from no record, while both have the same available band.
- A required band is met at equality and exceeded at higher bands. A requirement of `Unfamiliar` declares no learned-comprehension prerequisite; it does not mean the actor understands an utterance.
- Wrong subject identity, wrong rule pin and a conflicting recorded channel-definition pin reject without changing the profile.
- Input order and caller collection changes cannot change retained profiles; duplicate channels, unknown enum values and excessive input reject.

## Concrete identities and pins

`LanguageChannelKey` contains separate stable language and register IDs plus a `CommunicationMode`: Spoken, Signed or Written. It is ordered by canonical language ID, then register ID, then mode. These are receptive channels: a band does not grant the bodily ability or permission to perceive, speak, sign or write. A signed language can have its own language identity; the presence of the Signed mode does not collapse distinct signed languages into one universal channel.

The same language may have several supported registers or local dialect definitions. This primitive does not infer relationships between them. Content must define the actual supported language/register/mode combinations and any later explicitly taught or effect-mediated relationships. A definition pin is scoped to the exact channel; it is not a claim that arbitrary combinations are valid content.

`LanguageComprehensionEntry` binds one exact channel-definition version to one of four ordered bands:

| Band | Encoded value |
| --- | ---: |
| Unfamiliar | 0 |
| Practical | 1 |
| Conversational | 2 |
| Nuanced | 3 |

`ActorComprehensionProfile` contains the owning actor ID, unsigned 64-bit profile revision, exact comprehension-rule version and a copied read-only set of entries. The rule pin binds interpretation of the band scale; entry pins bind the referenced channel definitions. An empty profile and revision zero are valid metadata. The highest unsigned revision is representable; this immutable value performs no revision increment.

All pins use the existing canonical SHA-256 `ContentHash`. Construction checks their value shape, not their authenticity or presence in a live catalog. Content admission must independently resolve the definitions/rules. A profile is an evaluated knowledge snapshot, not the complete learning record. The future authority must retain lessons, discoveries, effects and causes, and connect revisions to their committed changes without treating a hand-constructed profile as proof that an actor learned anything.

## Exact query and narrow result

`ComprehensionRequirement` carries an exact channel key, expected definition pin and minimum band. `ActorComprehensionProfile.Query(subjectActorId, expectedRulesVersion, requirement)` performs these checks in order:

1. Validate required arguments and match the asserted subject to the profile's actor.
2. Match the expected rule pin exactly.
3. Look up only the exact language/register/mode key.
4. If an entry exists, require its definition pin to match exactly, even for a zero-band requirement or a recorded unfamiliar entry.
5. Return the recorded band or Unfamiliar for an absent channel, and compare it inclusively against the required band.

An absent channel does not establish that the requested definition exists. The caller must already have admitted the required definition. The query never changes records, substitutes another pin, treats stale knowledge as freshly interpreted, or borrows a nearby channel.

`ComprehensionQueryResult` contains only the subject actor, profile revision, rule pin, requirement, available band and whether an exact entry was recorded. `MeetsBandRequirement` is a band comparison, not a statement that a message was observed, translated, believed, true or understood in its full context. The result does not contain the profile's other channels or a private knowledge transcript.

The subject ID check is **local consistency, not authenticated requester authorization**. The host must still filter by the current principal's permitted actor/knowledge view before serialization or UI presentation. Passing a matching actor ID to this utility does not authorize seeing that actor's profile. Content key/version validity and query permission remain separate admission checks.

## Separation from culture, presentation and powers

Profiles have no species, culture, nationality, faction or localization-grant fields. A localized sentence on screen cannot improve comprehension. Shared starting-region communication, accessible context, willing interpreters and ordinary essential-service paths remain content/progression requirements; they are not implemented by silently adding fluency to every profile.

This primitive does not perform phonetic garbling, translation, dialogue generation or natural-language parsing. It cannot reveal secret membership through cant, bypass cryptography, prove sincerity or expose world facts. Listening, reading, observer scope, beliefs and social permission remain independent checks.

Supported magical translation or learning may later compose through declared actor effects and real causes/costs. That does not imply blanket fluency, secret-knowledge access or a separate provider request during replay. Another character relaying information must perform an actual permitted action; shared party UI or another player's knowledge cannot update profiles automatically.

## Bounds, immutability and encoding

The current profile limit is 256 distinct channel entries. This bounds one retained profile operation and rejects the first excess entry; it is not permission to discard learned languages or cap the world's language catalog. A richer shipping profile must use qualified capacity expansion or an explicit preserved representation. Duplicate keys reject even when their pins or bands differ. All named enum values are validated; unknown values are not normalized into Unfamiliar.

The profile copies its input and returns a read-only collection of immutable entries, keys and pins. Entries sort by language/register/mode independently of enumeration order. Query results retain immutable requirement metadata. Encoding returns a fresh byte array; modifying it cannot change a profile.

`LanguageContractEncodingV1` encodes already validated values using the existing `OFC\0` version-1 frame with big-endian framing version/count/field lengths. The contract version is unsigned 64-bit `1`; revisions, modes and bands are unsigned 64-bit fields. IDs are 16 canonical bytes and hashes are 32 bytes.

| Frame | Fields after tag |
| --- | --- |
| `language.channel` | language ID; register ID; mode |
| `language.comprehension-entry` | complete channel frame; definition-version hash; band |
| `language.comprehension-entries` | zero or more entry frames in canonical key order |
| `language.comprehension-profile` | contract version; actor ID; revision; rule-version hash; complete entries frame |

`ComputeContentHash()` is SHA-256 over the complete profile frame, binding every identity, mode, pin, revision and band. Explicit unfamiliar entries remain encoded and therefore differ from omitted entries. There is no runtime decoder or untrusted JSON import in this increment. A future loader must bound fields/counts, reject unknown versions and validate keys, pins and applicable content before accepting authority state. The fixture JSON reader is test support only.

## Retained qualification evidence

`generate_language_vectors.py` independently uses Python `struct` and `hashlib`, imports no application code and prints to stdout. Its frozen fixture contains three authored actor profiles and eleven query cases, including initial/maximum revisions, different actors, modes, cants, absent channels and explicit unfamiliar entries. The fixture's descriptor pins are authored qualification metadata, not a production language catalog or copied cultural content.

The eleven C# tests compare exact profile bytes/hashes and expected query results, all sixteen band-threshold combinations, actor/rule/definition mismatches, stale zero-band records, duplicate keys, canonical ID/mode ordering, copied state, encoded-buffer independence, profile hash sensitivity, null/unknown inputs and exact 256/257 bounds with early enumeration termination. The coordinator owns actual integrated .NET execution and commits; this document alone does not claim that unrun qualifications passed.

Remaining work includes authoritative learning/cause records, content rule/channel admission, permission-scoped dialogue projections, observer/perception checks, real interpretation/relay actions, source content mapping, supported spell effects, beginner-path communication witnesses, native cultural expression, localization/accessibility integration and human comprehension/usability acceptance.
