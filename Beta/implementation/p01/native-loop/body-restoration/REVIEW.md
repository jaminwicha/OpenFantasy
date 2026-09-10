# Independent body and language restoration review

Reviewed at 2026-09-10T00:23:20.957961+00:00. Reviewer: `/root/history_contracts`; implementation author: `/root/command_contracts`.

**Result: no blocking defect found in this bounded restoration increment.** The new readers restore the existing canonical immutable values with explicit limits and reject malformed alternatives. This review made no application or test edits.

## Scope and reasoning

- Read both decoder/reader pairs, the three new test files, the approved vocalization enum and existing condition-test adjustment, the encoder/value-constructor dependencies, and the two restoration contracts. The table below binds the exact reviewed inputs. The implementation author’s retained evidence hashes also matched the actual files when checked independently.
- Checked each fixed frame against its encoder: tag, version, field count, order, scalar widths and complete byte consumption. Collection readers bound counts before expansion; each nested record has an independent byte bound. No unbounded recursive parsing is present. Unsigned field lengths are checked against bounded remaining bytes before casting or slicing.
- Body slot decoding uses strict UTF-8 followed by the existing lowercase ASCII grammar. Constructors enforce a single connected root, known parents, no cycles, positive signed dimensions, compatible anatomical functions and feasible locomotion declarations. The wire readers reject duplicate/reordered slots, functions, modes and injuries before constructors could normalize their order.
- Conditions and bindings match both definition identity and the full SHA-256 pin. Independent condition/binding payloads still need `ValidateCondition` during composition to match their form instance. Optional identity encoding preserves absent versus present all-zero values. Existing factories reject incompatible form nature and actor/form/body/tether instance aliases; global admission remains separate.
- Injury restoration retains causal records and existing deterministic descendant suppression. Vocalization is explicitly anatomical and independent of Hearing: the tests cover intact voice, deafness, lost voice function, missing voice ancestor and a harmless scar. The original function 1–9 expectations remain intact; unchanged fixtures explicitly lack the new function. This does not establish expressive language knowledge or authorize a speech action.
- Language restoration preserves exact actor identity, revision, rules hash, channel-definition hashes and explicit Unfamiliar entries. Channel ordering compares language ID, register ID and communication mode. No channel fallback, inferred cultural membership or pin normalization is introduced. Profile queries retain their existing subject/rules/recorded-definition checks.
- Returned objects retain values or immutable object references, not input byte buffers. Malformed value-constructor exceptions are translated to `FormatException`; a null caller-provided body definition remains `ArgumentNullException`. Long geometry dimensions can be valid stored anatomy without qualifying them for spatial admission.

## Independent executed checks

Executed each original Python oracle through its stdout interface and compared every byte to the frozen fixture. No fixture or expected bytes were regenerated on disk. These are independent oracle reproductions, not execution of the C# decoders.

| Oracle | Bytes | SHA-256 |
|---|---:|---|
| actor | 29,232 | `42594f115d5d31b3814bebe1107510609b6390d6abb30873d2a4ac0655dd4fee` |
| language | 13,226 | `8dfccbf02965e8f495821bdae8c834999b400413be68a59d6d4e6db5869a276a` |

Independently recomputed framing sizes in Python using `12 + 4 + UTF8(tag).Length + sum(4 + fieldLength)` for every nested frame. All bounds below contain the computed sizes. The body figures are conservative envelopes: invalid anatomy combinations and both optional binding IDs are deliberately counted together; their inclusion in the envelope does not make them accepted values. The language maximum is exact.

| Frame | Computed bytes | Reader limit |
|---|---:|---:|
| Body functions (ten-value envelope) | 156 | 160 |
| Body part (two 64-byte slots) | 363 | 384 |
| 64-part collection | 23,520 | 24,000 |
| Body clearance | 72 | 80 |
| Locomotion record | 56 | 64 |
| Five-mode collection | 345 | 384 |
| Body definition envelope | 24,030 | 24,576 |
| Injury (64-byte slot, present function) | 165 | 192 |
| 256-injury collection | 43,299 | 44,000 |
| Body condition envelope | 43,427 | 49,152 |
| Binding (both optional IDs counted) | 194 | 256 |
| Language channel | 84 | 84 |
| Language entry | 180 | 180 |
| 256-entry collection | 47,150 | 47,150 |
| Language profile | 47,280 | 47,280 |

## Test assessment and limits

The 16 new body Facts and seven new language Facts exercise independent fixture roundtrips/hashes, malformed frames and every incomplete representative prefix, maximum positive constructions, noncanonical ordering and duplicates, invalid enums/widths/topology/nature, exact body pins, wrong language query pins, optional-ID shape, immutable input ownership and the voice/hearing distinction. The maximum body construction uses 64 long-named parts, signed-64 maximum dimensions, eight compatible physical functions and 256 injuries; the language construction actually encodes and restores 256 entries at the exact public bound. Negative frame helpers construct mutations independently of the production encoder.

No .NET or Godot build was run by this reviewer. The coordinator reported that the new body/language cases compiled and passed in an integrated Core run with 310 passing cases and one unrelated Spatial assertion failure. That reported result is coordinator evidence, not an independently executed pass count. The Spatial assertion was corrected separately under spatial ownership, preserving missing-terrain precedence; this document does not claim a later integrated rerun.

This acceptance is limited to restoration. It does not prove accepted content pins, actor/control admission, global identity uniqueness, lifecycle or inventory transfer, native speech/listening delivery, durable world composition or old-binary support for newly added enum values. Unknown enum values continue to fail closed; old definitions retain their original bytes and hashes. Native composition must select accepted definitions and apply the documented independent form, spatial, perception/expression and authority checks.

## Exact input hashes

The 35 files below were hashed directly from the workspace after review. Paths are repository-relative. Any subsequent source or contract change requires reassessing its affected conclusions.

| Path | Bytes | SHA-256 |
|---|---:|---|
| `Beta/Runtime/src/OpenFantasy.Core/Actors/ActorFormBinding.cs` | 3228 | `f7c27d278f63a6bc40f92e1ec58e2bd4882cee1be3e118518a47191eb9544670` |
| `Beta/Runtime/src/OpenFantasy.Core/Actors/BodyConditionSnapshot.cs` | 3821 | `6d8289ca0f6b3957d098dc87b68e1a1600bdd73cede6822fd9d08641fefe77c5` |
| `Beta/Runtime/src/OpenFantasy.Core/Actors/BodyContractDecodingV1.cs` | 10440 | `bc6c338394f91a35a6d728c05752ce7d17921075d4f73b3235e30e28a45194b9` |
| `Beta/Runtime/src/OpenFantasy.Core/Actors/BodyContractEncodingV1.cs` | 3503 | `ab68380d3511228b7cefe073330288b0c58a72f2370525ffbbdb3d800431e15c` |
| `Beta/Runtime/src/OpenFantasy.Core/Actors/BodyContractFrameReader.cs` | 3838 | `ff6148d5467a5ed0da6e2bb799856d8967521afb5006cc9c9b035353c24ed8c1` |
| `Beta/Runtime/src/OpenFantasy.Core/Actors/BodyDefinition.cs` | 4993 | `878dc032f0ef6228a421a3a72290c4590466ee44fcde66fa0eec6199d0188423` |
| `Beta/Runtime/src/OpenFantasy.Core/Actors/BodyDimensions.cs` | 740 | `5e10d7357d870e806860b67f70fdf5b3b2799d414df6f879127ffc5f0ff3912f` |
| `Beta/Runtime/src/OpenFantasy.Core/Actors/BodyFunctions.cs` | 587 | `706eba0f04369804a23f9fbc7ec0d97b45f16a4c2e94ff2e72ee882f802e65cf` |
| `Beta/Runtime/src/OpenFantasy.Core/Actors/BodyInjury.cs` | 1230 | `d0b586c749e4af33d26c1015009c2d3e3c8ec0de53c1183c9de52cdc87c17f44` |
| `Beta/Runtime/src/OpenFantasy.Core/Actors/BodyPartDefinition.cs` | 1205 | `6a5a8d72e56647ae3d2e39e745ddacc7d63e7d3e9e9a79d3663b66da97b0a99b` |
| `Beta/Runtime/src/OpenFantasy.Core/Actors/BodySlotKey.cs` | 779 | `b42549c0cace6c4c36f71a3976cff7835d671a77088e2036d0c932ea28a28536` |
| `Beta/Runtime/src/OpenFantasy.Core/Actors/LocomotionRequirement.cs` | 1238 | `acbe6648581a4b701ea79fe394b7ff7a1f25cb66fda2791eb5488ad0be25121e` |
| `Beta/Runtime/src/OpenFantasy.Core/CanonicalEncodingV1.cs` | 2342 | `1e8871ebbf3aece0316fabdcdbf1432c02240e053fa5b50f7bdf70223f46f4c4` |
| `Beta/Runtime/src/OpenFantasy.Core/Content/ContentHash.cs` | 915 | `279bcb92c2a19dffa74bcb9f1a7f13e709b2a2a7943ccc87e454f6e726d3bc45` |
| `Beta/Runtime/src/OpenFantasy.Core/EntityId.cs` | 4821 | `85211ece11a8715cd530ad5abf7c922935e7a4b0505ab3bffbaa369759c7190c` |
| `Beta/Runtime/src/OpenFantasy.Core/Language/ActorComprehensionProfile.cs` | 3198 | `789717e6c637bb790915c1949d0d76fa20446ae6549ae4be152e2d29c48cf130` |
| `Beta/Runtime/src/OpenFantasy.Core/Language/ComprehensionBands.cs` | 414 | `70ea0751f6225d5ec1881c0dfd829e1adefc1f60698c8fdfe295ac18aa39b3f1` |
| `Beta/Runtime/src/OpenFantasy.Core/Language/ComprehensionQueryResult.cs` | 1106 | `ca0c9b0ac8a335461942f391b6bc8775735b5ada5b117610f08edd92dc3a520a` |
| `Beta/Runtime/src/OpenFantasy.Core/Language/ComprehensionRequirement.cs` | 958 | `d754dfc9db84763943bb726c925fcdad658043632a02308d1e0b334e4ae19900` |
| `Beta/Runtime/src/OpenFantasy.Core/Language/LanguageChannelKey.cs` | 1114 | `971c54c1dadbd74665bc8734ebb53163af48a1a6f2c54352514fd77dc839301f` |
| `Beta/Runtime/src/OpenFantasy.Core/Language/LanguageComprehensionEntry.cs` | 823 | `1109529c20fd6b099ddecd14c475b8e7574e4d021ec7eb5eb8cf17a199514831` |
| `Beta/Runtime/src/OpenFantasy.Core/Language/LanguageContractDecodingV1.cs` | 3409 | `ab036124bb3e3fba98404061d1d8108e3b6e50b6013bbf664eef8af8706122b3` |
| `Beta/Runtime/src/OpenFantasy.Core/Language/LanguageContractEncodingV1.cs` | 1330 | `9cbb5e0d328854134219073cc25682e3a1edd81d6953eef6b1fd7ab1e3ffffdc` |
| `Beta/Runtime/src/OpenFantasy.Core/Language/LanguageContractFrameReader.cs` | 2856 | `b54eca029c5250f9cb84b9e5261c24fffe44b9b93dcdafd33f49d7a228a88832` |
| `Beta/Runtime/tests/OpenFantasy.Core.Tests/Actors/BodyConditionTests.cs` | 7676 | `9ebc8a73086ca19ce8b128053e95305e08c67f90652111083ff518c71c5501d7` |
| `Beta/Runtime/tests/OpenFantasy.Core.Tests/Actors/BodyContractDecodingTests.cs` | 22752 | `64d3e9f3c1e27f4c0f0cda52c7a3bc6e161936c00b4c9cb3525eb7f1d6a5fe03` |
| `Beta/Runtime/tests/OpenFantasy.Core.Tests/Actors/BodyDecodingTestFrames.cs` | 2324 | `9eeec98daca5ac23857ccdcef8234d82ee2370477f4e0cef93e35b7af10f0b39` |
| `Beta/Runtime/tests/OpenFantasy.Core.Tests/Fixtures/actors-v1.json` | 29232 | `42594f115d5d31b3814bebe1107510609b6390d6abb30873d2a4ac0655dd4fee` |
| `Beta/Runtime/tests/OpenFantasy.Core.Tests/Fixtures/generate_actor_vectors.py` | 7275 | `d800e5fb096f7b0d67b4bfc3e3637051c63123897df205e4d4f8cf43d9e22824` |
| `Beta/Runtime/tests/OpenFantasy.Core.Tests/Fixtures/generate_language_vectors.py` | 4302 | `076b3b2d765a34e8ed7b0a2a38cd23a70bfe107a59149105b78fd4315f29797a` |
| `Beta/Runtime/tests/OpenFantasy.Core.Tests/Fixtures/language-v1.json` | 13226 | `8dfccbf02965e8f495821bdae8c834999b400413be68a59d6d4e6db5869a276a` |
| `Beta/Runtime/tests/OpenFantasy.Core.Tests/Language/LanguageDecodingTests.cs` | 10284 | `039dbc4472a2c8aefd7316904f656b41a362ba302aaa60e06267049a49d59b65` |
| `Beta/implementation/p01/native-loop/body-restoration/CONTRACT.md` | 7421 | `9812c4bcd924505904ed3a6236c516d3bd42f39814b0c79647c9b165f7fe681d` |
| `Beta/implementation/p01/native-loop/body-restoration/EVIDENCE.json` | 7405 | `21b8d4ae9b4b1dd8391c4f8c961c6e7bd5b9569b0a8b6157bd47ab476007f2bb` |
| `Beta/implementation/p01/native-loop/body-restoration/LANGUAGE_RESTORATION.md` | 3024 | `44e064666df9db8dabb8a72f6f2de1bccd54edd34fec1f0470141fef6b59bdeb` |
