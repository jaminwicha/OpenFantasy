# Canonical body restoration

`BodyContractDecodingV1` restores the existing canonical body-definition, condition and form-binding formats for persisted actor presence. It uses the unchanged `BodyContractEncodingV1` frames, tags, ordering, units and version. The owner also approved the additive anatomical value `BodyFunction.Vocalization = 10` for actual spoken expression. Existing definitions and their expected bytes remain unchanged; new vocal capacity must be explicitly present in a new admitted definition. No encoder or expected-fixture regeneration is part of this increment.

The public API is:

```csharp
BodyDefinition definition = BodyContractDecodingV1.DecodeDefinition(definitionBytes);
BodyConditionSnapshot condition = BodyContractDecodingV1.DecodeCondition(conditionBytes, exactDefinition);
ActorFormBinding binding = BodyContractDecodingV1.DecodeBinding(bindingBytes, exactDefinition);
binding.ValidateCondition(condition);
```

For condition/binding restoration, `exactDefinition` must be the host's selected accepted definition. The stored definition ID **and full SHA-256 version** must match it. Merely decoding a definition does not admit that content, install an actor or grant control. The final `ValidateCondition` call remains necessary when composing a binding and condition, because the two independent payloads could otherwise name different form instances. Actor existence, global identity uniqueness, lifecycle, custody and current controller authority remain the presence/authority owner's responsibilities.

The decoder reconstructs concrete immutable values through their existing constructors. Definitions must retain a bounded connected tree with exactly one root, known parents, no cycles, positive signed-64 dimensions, supported anatomical functions and feasible declared locomotion requirements. Part slots use the exact existing lowercase ASCII key grammar. UTF-8 is decoded strictly; invalid text, non-ASCII slots, unsupported values and implicit normalization reject.

Wire order is checked before construction: body slots in ordinal string order; part functions and locomotion modes in numeric order; injuries in canonical identity order. Duplicates and reordered input reject instead of being silently sorted into valid bytes. Empty permitted collections remain explicit frames; omitted required fields are errors.

Condition restoration checks every referenced slot and lost anatomical function against the exact definition, including the existing corporeal/incorporeal injury constraints. Missing ancestor parts continue to suppress descendant functions; scars retain function. Injury restoration performs no new damage, healing, death or resource mutation. Anatomical functions do not define every possible learned or magical ability, and anatomical locomotion eligibility is not permission to traverse actual terrain.

Vocalization is independent of receptive hearing and language comprehension. A deaf actor may retain a functioning voice; a vocal-function injury or missing ancestor suppresses expression even if hearing remains. A scar alone does not remove the function. The native action must separately require the speaker's explicit vocal capacity and admitted expressive channel, the listener's hearing and receptive channel, and its other real action conditions. This restoration API alone does not authorize speaking or guarantee that a listener heard or understood it.

Binding restoration requires a supported kind and exact optional-field shape: corporeal forms have a physical body and no tether; independent spirits have neither; tethered projections have a tether and no physical body. The existing factories then validate definition nature and distinct actor/form/body/tether instance roles. An absent optional ID is a zero-length field. A present all-zero ID remains sixteen bytes and is preserved as present.

## Strict bounds

Public byte limits are `MaximumDefinitionByteLength = 24_576`, `MaximumConditionByteLength = 49_152`, and `MaximumBindingByteLength = 256`. Nested limits cover 64 parts, ten known functions per part, five known locomotion modes and 256 injuries. Actual body-nature constraints further restrict compatible combinations. Length/count limits are checked before expansion; the reader has no recursive unbounded format.

The bounds cover a conservative upper envelope of the unchanged wire format:

| Frame | Conservative encoded bytes | Reader bound |
|---|---:|---:|
| Functions, all ten enum values | 156 | 160 |
| Part, both slot keys 64 bytes | 363 | 384 |
| 64-part collection | 23,520 | 24,000 |
| Clearance dimensions | 72 | 80 |
| Five locomotion requirements | 345 | 384 |
| Complete definition | 24,030 | 24,576 |
| Injury, 64-byte slot and present lost function | 165 | 192 |
| 256-injury collection | 43,299 | 44,000 |
| Complete condition | 43,427 | 49,152 |
| Binding, conservatively counting both optional IDs | 194 | 256 |

Some envelope combinations are deliberately more generous than valid anatomy: all ten functions cannot coexist under one existing body nature, and both binding optional IDs cannot be present together. This calculation proves the bounds do not exclude a valid encoding; it does not admit those impossible combinations.

Unknown framing or body versions, tags, extra/missing fields, wrong scalar widths, noncanonical order, excessive lengths/counts, invalid topology/pins and top/nested trailing data reject with `FormatException`. A null `exactDefinition` is a caller error and remains `ArgumentNullException`. Returned values retain no mutable input byte array. No malformed input is partially published or replaced by defaults.

## Verification

The unchanged independent `generate_actor_vectors.py` stdout reproduces the existing `actors-v1.json` fixture exactly: **29,232 bytes**, SHA-256 `42594f115d5d31b3814bebe1107510609b6390d6abb30873d2a4ac0655dd4fee`. It supplies five representative definitions, six conditions and three bindings. The worker executed this byte comparison; the oracle imports no production code.

Sixteen new C# tests cover exact fixture roundtrips and hashes; restored injury functions and locomotion eligibility; all three binding kinds; stale/mismatched definition pins; cycles/missing parents/multiple roots; slot/function/mode/injury ordering and duplicates; enum/range/nature incompatibility; malformed UTF-8 and scalar widths; optional identity shape; maximum long-slot/dimension/part and injury cases; nested trailing data; every incomplete representative prefix; oversized frames; present-zero identity; immutable restoration; and independent vocal/hearing loss, missing-parent and scar behavior. The maximum positive construction checks complement the existing independent fixtures; they do not generate replacement expected fixture bytes. The approved narrow update to the existing condition test preserves all original function 1–9 assertions and explicitly asserts no vocalization for unchanged older fixture definitions.

The coordinator owns .NET build/test execution and native-loop integration. No .NET build, shared project/verifier edit, fixture mutation or Git operation was performed by this worker. The only existing actor application change is the explicitly approved additive vocalization enum value. This restoration increment alone does not qualify a persisted playable actor, a rendered animation or a durable cross-domain action.
