# Independent package and host contract review

Reviewer: the command-contract worker, reviewing the coordinator's `OpenFantasy.Core.Content` and host-inspection changes. The reviewer did not implement these files. This is a bounded source review plus the explicitly listed executed Python checks; it is not an independent .NET test run or a review of the reviewer's own command implementation.

**Disposition: no blocking defect identified in the reviewed package or host contract scope.**

## Review observations

- Package IDs use a bounded ASCII namespace grammar and ordinal comparison. Complete immutable SHA-256 versions bind namespace, exact payload digest and canonical dependency pins; payload bytes cannot be changed without validation failure.
- Manifest construction bounds enumeration at 65 attempted dependencies, rejects duplicate namespaces, copies inputs and exposes a read-only collection of immutable entries. Sorting does not mutate caller-owned data. Returned byte arrays do not expose internal backing storage.
- JSON has a 65,536-byte budget and depth limit. Exact required-property checking rejects unknown, duplicate and missing top-level/nested fields; fixed schema, primitive kinds, package IDs and hashes are validated. JSON whitespace/property order does not become identity; canonical binary does.
- Resolution bounds the input set at 257 attempted entries, rejects duplicate namespaces, missing dependencies, pin mismatches and dependency cycles, and emits each package once after its dependencies. Traversal roots and dependency lists have explicit ordinal ordering. Recursion depth is bounded by the 256-package set.
- The reviewed independent Python vector generator matches the established canonical framing and binds both a dependency-free package and a dependent package. Its fixture was reproduced byte for byte by the reviewer using Python; this did not execute C#.
- `ContractInspector` reads bounded local files. Command output is labeled structural validation and package output manifest/payload-byte validation. It does not admit a command, execute content, resolve a full package deployment, assert gameplay compatibility or manufacture a durable receipt. `HostApplication` handles anticipated parse/file/value failures without reporting successful inspection.
- Runtime application types remain small, cohesive and readable. Static encoding/resolution helpers are pure responsibilities; there is no new speculative interface hierarchy or shared mutable authority.

## Executed reviewer checks

The reviewer ran the retained package-vector Python generator and compared its complete stdout bytes with `packages-v1.json`: exact match. No generated repository file was overwritten.

As an additional source-preservation check, the reviewer ran `migration-closure/build_closure.py --check` and all 15 retained closure tests after the correction below. The check reproduced the packet byte for byte: 31 selected roots, 49 structured records, two code-defined archetypes, 171 typed edges, 29 assets and 48 source/support files. The source inventory remains 532 structured records, 10 indexed code-defined entries and 201 files. Those Python checks do not prove gameplay migration, native import or meaningful scene presentation.

The coordinator owns the final pinned SDK build/test run. The reviewer intentionally did not run a competing .NET build while host integration was in progress.

## Remaining qualification boundaries

Package shape/hash validity is not payload semantic validity, source licensing determination, permission to execute unrestricted code or proof that every gameplay definition/reference is accepted. P01 needs actual typed definitions, operation/capability limits, explicit overrides/incompatibilities and persistence/package handshake tests. The current command path remains a diagnostic file reader; authentication, receipt access, custody and durable command handling are still required.

The source closure is deliberately scoped to inspected reference rules and exact source fingerprints. The reviewer found that a newly discovered node-yield vehicle would miss its vehicle-specific asset/terrain expansion after the single semantic pass. Current retained yields were ordinary wood/ore items and unaffected, but the generalized closure claim needed a guard. The owner added `merge_after_semantic_expansion`, rejecting newly discovered nodes or vehicle items before merging any added row, while permitting already traversed identities. The reviewer read the correction and its new vehicle/node/rediscovery regression, then reran all **15 tests** and the byte-identical `--check`: all passed, with current packet bytes unchanged. **Finding closed.** Supporting a future recursive semantic expansion remains explicit implementation work, not silent omission.

## Reviewed input snapshot

Hashes bind the actual source, tests and oracle inspected here. Later semantic changes require follow-up review. Final coordinator builds and packaging checks are separate evidence.

| File | SHA-256 |
|---|---|
| [ContentHash.cs](../../../Runtime/src/OpenFantasy.Core/Content/ContentHash.cs) | `279bcb92c2a19dffa74bcb9f1a7f13e709b2a2a7943ccc87e454f6e726d3bc45` |
| [PackageDependency.cs](../../../Runtime/src/OpenFantasy.Core/Content/PackageDependency.cs) | `84662893a26e1cfdfe6df648c582d716395ea74c9167a07d953459d67e0d29ce` |
| [PackageId.cs](../../../Runtime/src/OpenFantasy.Core/Content/PackageId.cs) | `1292adfcc7493455a31b133b108f74d19389a577d0dd0b4df35f84738e406421` |
| [PackageManifest.cs](../../../Runtime/src/OpenFantasy.Core/Content/PackageManifest.cs) | `93350583e5f8f81602a5f34aae2106794f4fa514b0cd3ce2ddcd64e7790bd4e1` |
| [PackageManifestJson.cs](../../../Runtime/src/OpenFantasy.Core/Content/PackageManifestJson.cs) | `7da4c0625240779f25806d67eb5783dadccac76793c3e19e3d06a0cef5839655` |
| [PackageResolver.cs](../../../Runtime/src/OpenFantasy.Core/Content/PackageResolver.cs) | `b4829167b860093b4d100ce5010e572c342dfde951f257b5c051705ea0d7227b` |
| [PackageManifestTests.cs](../../../Runtime/tests/OpenFantasy.Core.Tests/Content/PackageManifestTests.cs) | `ed63957c0957eea33c77b00158d7f64c7c2078805c271cbbf2eb3a1e7c044c57` |
| [PackageResolverTests.cs](../../../Runtime/tests/OpenFantasy.Core.Tests/Content/PackageResolverTests.cs) | `53fdf1a45f0a1d91758a8174115ac19c08aa662079551ca0d992c278acb51616` |
| [generate_package_vectors.py](../../../Runtime/tests/OpenFantasy.Core.Tests/Fixtures/generate_package_vectors.py) | `e339369502ef0dfca3ec89d43120e75bfd39791095a1c8886dee0cb840d34624` |
| [packages-v1.json](../../../Runtime/tests/OpenFantasy.Core.Tests/Fixtures/packages-v1.json) | `c06944d20171b3da12d8deafca232db80dfa55de952e0015f1e9b1443096d034` |
| [ContractInspector.cs](../../../Runtime/src/OpenFantasy.Host/ContractInspector.cs) | `bb9e72674c1f87caff45368c1d58866969081df85fe4a5c51c3ef4937b076642` |
| [HostApplication.cs](../../../Runtime/src/OpenFantasy.Host/HostApplication.cs) | `6acab03d77e6a47faef409d838aece73511b90269e78c607dfb7a7b5e90731f9` |
| [build_closure.py](../migration-closure/build_closure.py) | `6c47f8b1ee423d224dae750e82b56dd061b51dc19cdbfb294f004d76d00318f5` |
| [test_closure.py](../migration-closure/test_closure.py) | `e1a707e7a9c860aff3ee26fdc4ad83392f3af2add6bc812e36765f43c9c61f38` |
