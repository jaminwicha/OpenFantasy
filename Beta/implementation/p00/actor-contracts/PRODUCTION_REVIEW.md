# Independent finite production contract review

Review date: 9 September 2026. Reviewer: the actor/history-contract worker, independently reviewing the production implementation. Outcome: **no blocking correctness findings within the declared P00 definition, pin and mass-accounting scope**.

## Static scope and conclusion

I read the nine production application/codec files, three C# test/support files, independent Python oracle, retained fixture, contract and original recipe source listed below. The review checked:

- Distinct mass, occupied-volume, hardness-rating and work-unit meanings; positive ordinary-material unit mass/volume, positive work/counts and checked mass products/subtotals/final accounting.
- Immutable copied catalogs/terms, bounded enumeration, unique material identities and package/key names, explicit term roles and exact material version resolution, including conflicting pins across roles.
- Exact input mass equals output plus explicit waste mass, with no unnamed loss, arbitrary bypass or output claim generation. Occupied volume is deliberately not conserved.
- Deterministic material/role ordering and version hashing that includes calibrations, exact pins, counts, work and optional source-payload lineage; unrelated catalog entries are excluded from the used material set.
- Strict bounded decoding: primitive widths, exact hashes, versions/types, allowed roles, canonical definition keys, UTF-8, sorted unique nested collections, absent required materials, unused extra definitions, truncation, overflow and nested/outer trailing bytes.
- The current untransformed custody boundary: recipe definitions cannot manufacture output source intervals or treat an output count as transformed multi-input provenance.
- Tests covering the independent vectors, source/calibration separation, mass versus volume, missing/conflicting pins, read-only copies, exact capacity limits, early enumeration termination and arithmetic failure behavior.

One clarification was raised during review: the constructor validates per-unit volume but does not multiply/sum aggregate occupied volume or prove that real receivers have capacity. The coordinator confirmed that this belongs to actual process admission and added an explicit paragraph under `VolumeMicrolitres` in the final contract. I inspected that wording. It requires checked volume products/totals/capacity or a qualified wider/chunked representation before physical lots, without wrapping, omission or a false volume-conservation law. This scope concern is closed; no production code correction was required.

No production source or test edits were made by this reviewer.

## Actual executed evidence

I executed the independent Python standard-library oracle and compared its stdout byte for byte with the frozen fixture: **92,438 bytes matched**. The oracle imports no application code and includes 30 malformed recipe vectors.

I separately read the actual original `data/recipes.json` record at `/recipes/1` and verified equality with the retained fixture payload. Its canonical JSON payload SHA-256 is `c384027c76855ade482105233cadd07ea930a265f73e12beab852de1cc756491`. The original two-berries/one-wood/two-rations counts remain distinct from newly authored material calibration and residue. This payload hash is separate from the complete source-file hash in the table.

I did **not** run .NET builds/tests, actual crafting, Godot, storage admission or durable custody transitions. The coordinator reported its integrated test pass separately; the central verification checkpoint owns executed C# evidence. Static inspection of tests is not an independent execution claim.

The reviewed definitions do not prove station/tool/worker eligibility, container capacity, real ingredient availability, energy/heat conservation, process exposure, partial jobs, quality, food safety, transformed provenance, cross-branch export, actual crafting or player acceptance. The finite 64-definition/64-term bounds qualify one definition operation, not the production catalog's eventual size. Full process graphs and admission remain retained future work.

## Exact reviewed files

Paths are repository-relative. SHA-256 values cover complete file bytes, including the clarified contract.

| File | SHA-256 |
| --- | --- |
| `Beta/Runtime/src/OpenFantasy.Core/Production/CanonicalProductionReader.cs` | `bd1c02fff863e00b581bbe222fae98ba7a2d47493d2ac0716a2acb2abb079b77` |
| `Beta/Runtime/src/OpenFantasy.Core/Production/MaterialCatalog.cs` | `14c2c026e8df79134c2046d9daf5b84d2f69a48da74fb35e352eb16d999c8dd4` |
| `Beta/Runtime/src/OpenFantasy.Core/Production/MaterialDefinition.cs` | `ae5e62794834506c243f5e11893d0f93347827381ae8ea4d33d9c038c56de705` |
| `Beta/Runtime/src/OpenFantasy.Core/Production/MaterialMeasures.cs` | `19fe02f068d4ada83494b759fe4ace37cb69c0ff97d9e14237869fcc1f27bc32` |
| `Beta/Runtime/src/OpenFantasy.Core/Production/MaterialRecipeCodecV1.cs` | `4c9c291c7a2c3e28efa5f617c88f0d3ef231f23eff000c50e478e19008a8c9bc` |
| `Beta/Runtime/src/OpenFantasy.Core/Production/MaterialRecipeDefinition.cs` | `47665e2842ddd388365bba7ec33d48a48b98c81e0d7cd9a3c355657339dd5331` |
| `Beta/Runtime/src/OpenFantasy.Core/Production/ProductionDefinitionRules.cs` | `6fe783474fb6e35f4b05c4c7603bafd452493ce94c6f6a9d955c29021226058e` |
| `Beta/Runtime/src/OpenFantasy.Core/Production/ProductionFormatException.cs` | `3301c2c797a57eea03cea1fc05c6ee66eaf795b8a37c8a7d1a6eb8b1c287f742` |
| `Beta/Runtime/src/OpenFantasy.Core/Production/RecipeTerm.cs` | `d3bf4717a3c639da0f726ed1017c402c4de86fb7c33b94a76f8120b286d286be` |
| `Beta/Runtime/tests/OpenFantasy.Core.Tests/Fixtures/generate_production_vectors.py` | `3b97855164d8c32f8e593465b82c8a47dc3135d79a7c2170e77a36f1a78b6887` |
| `Beta/Runtime/tests/OpenFantasy.Core.Tests/Fixtures/production-v1.json` | `9387beda856d2b91ac197808d54d24fc8d4bbcee1ab6e452a87ee1e020d4aaa2` |
| `Beta/Runtime/tests/OpenFantasy.Core.Tests/Production/MaterialRecipeCodecTests.cs` | `b5324a90db75f38ab38924bc60cd32ae93423b6c0aa152ba87a4c7e334377f0d` |
| `Beta/Runtime/tests/OpenFantasy.Core.Tests/Production/MaterialRecipeTests.cs` | `8f7e1ea414c8ff357565b786c746589320dfe9f036c6818f4a23ef324cf09a3b` |
| `Beta/Runtime/tests/OpenFantasy.Core.Tests/Production/ProductionFixture.cs` | `4e94e45fee0a2cbd35d94ed54adecf4c0386f83a0ae0fc42769683ec2a969b45` |
| `Beta/implementation/p00/production-contracts/CONTRACT.md` | `94eddf5287567ff3a50cc437d0cbbb0393fe95cc05c1f56a7398209303af2a72` |
| `data/recipes.json` | `0c81bcf33088668a7837e77cf01640cbfd2070e2a8caf9fa13ebdeb1e032a6b1` |

Reviewed set: 16 files. Aggregate SHA-256: `07bb910ecad3700310885e720eb3f7e0711cf8143275ab98dc3fd54bfe5e6449`.

The aggregate hashes the UTF-8 concatenation of each sorted repository-relative path, a NUL byte, its lowercase hexadecimal SHA-256 and a newline. It identifies the exact review scope; later changed files require an appropriate review refresh.
