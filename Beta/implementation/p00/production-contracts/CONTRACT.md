# Finite ordinary-material production definitions

This retained P00 increment defines versioned material calibrations and one bounded recipe operation, following [technical design §§3, 6 and 8](../../../spec-v1_1/TECHNICAL_DESIGN.md). It validates physical units, material pins, explicit input/output/waste terms and exact mass accounting. It does **not** execute crafting, reserve or consume inventory, create output lots, award experience, advance work, simulate heat or transport source claims.

The implementation uses concrete immutable values in `OpenFantasy.Core.Production`. One recipe is a single operation with no graph edges, recursion or automatic repetition. This is a deliberately bounded first implementation of the eventual typed process family, not a generic graph framework or a replacement for the full staged crafting/mining/alchemy specification.

## Measures and finite definitions

The types have no implicit conversions between units:

- `MassMilligrams` contains an unsigned 64-bit nonnegative mass. Adding masses and multiplying by unit count use checked arithmetic.
- `VolumeMicrolitres` contains an unsigned 64-bit nonnegative occupied volume with the same checked operations. Occupied volume can change when materials are processed; this contract does not invent a volume-conservation equation.

  Recipe construction validates per-unit volume but does not multiply and sum total occupied volume or adjudicate storage capacity. A valid mass-balanced definition is not proof of physical executability. Actual process admission must check volume products/totals and receiver capacity, or use an explicitly qualified wider/chunked accounting scheme, before creating or placing physical lots. No arithmetic wrap or omitted volume is permitted in that later admission.
- `HardnessRating` is a UInt32 ordinal resistance rating in the pinned game rule definition. It is **not Mohs hardness**, pressure, energy or an excavation duration. Tool capability/power comparison and actual mining formulas remain later typed rules.
- `WorkUnits` is a UInt64 count of abstract deterministic work. It has no conversion to wall time, exploration time, tactical readiness or input presses. Future process owners must specify capability, cost and applicable exposure rates before advancing work.

Zero-valued measures are representable value objects; a counted **ordinary material definition** requires positive mass and occupied volume per unit, and a recipe requires positive work. Hardness zero is representable. Negative quantities cannot enter these unsigned types. Overflow rejects, rather than wrapping, clamping or silently losing material.

`MaterialDefinition` contains a `PackageId`, stable 128-bit definition identity, stable key, per-unit mass/volume and hardness. Keys contain 1–128 lowercase ASCII letters, digits, underscores or hyphens and start with a letter. Its version is full SHA-256 of its canonical bytes, binding every field. `Pin()` returns the existing immutable `CountedItemDefinition` identity/version pair; it creates no item or source interval.

`MaterialCatalog` defensively copies at most **64** definitions, requires unique IDs and unique package/key pairs, sorts by canonical definition ID and resolves exact identity/version pins. A duplicate ID, ambiguous key, missing material or changed version rejects. The count is a qualification limit for this finite operation; future catalogs must grow through a qualified format/profile or paging policy, never silent truncation.

## One recipe operation

`RecipeTerm` specifies **Input (1)**, **Output (2)** or **Waste (3)**, an exact counted-material pin and a positive UInt64 count. `MaterialRecipeDefinition` retains its own package/identity/key, positive required work, optional original recipe-payload hash and at most **64** terms. It requires at least one input and one output. Waste is optional when every input milligram remains in ordinary outputs; any loss must be represented by an explicit waste term.

Each material ID may occur only once **per role**. Repeated same-role terms reject instead of being double counted. The same material may appear in different roles for a single finite conversion/repack, but every occurrence must resolve to the same accepted definition version. A conflicting pin rejects even across roles. This does not imply that an input/output identity pairing can duplicate a real lot.

The constructor resolves every pin and computes:

```text
input mass = sum(input unit mass × input count)
output mass = sum(output unit mass × output count)
waste mass = sum(waste unit mass × waste count)
require input mass == output mass + waste mass
```

All products, each subtotal and the final output-plus-waste addition are checked UInt64 operations. Missing inputs/outputs, zero counts/work, unknown roles, unsupported references and accounting overflow reject. There is no `AllowMassLoss`, magic bypass or unnamed sink. Fire, heat exchange, gases, liquids and magic need future typed source/sink/reaction operations with explicit inputs and outputs; they cannot be represented by quietly disabling conservation.

Accepted recipes copy and sort terms by role then canonical material ID. They retain only the material definitions they actually reference, in ID order. An unrelated available catalog entry does not change the recipe's identity. Canonical wire input must already contain exactly this used material set and order; an extra unused definition rejects instead of being discarded during decode.

The recipe's new version binds all fields, all used material definitions and exact pins/counts. Its optional `SourceRecipePayloadHash` is **content lineage metadata**, separate from the new version. It is not a material source claim, export allowance or proof that physical calibration came from the original game.

## Source recipe versus authored qualification values

The representative original `craft_rations` payload is preserved exactly in the existing source packet and in the fixture's separately labeled source record:

```json
{"id":"craft_rations","output":"rations","outputCount":2,"inputs":[{"item":"berries","count":2},{"item":"wood","count":1}]}
```

Its original inventory payload hash is `c384027c76855ade482105233cadd07ea930a265f73e12beab852de1cc756491`. Original counts remain **two berries plus one wood producing two rations**. The source contains no mass, volume, hardness, duration/work or waste declaration. The source model defaults an omitted station to `None`; this definition increment does not invent or enforce a new station requirement.

A separate `openfantasy.calibration` recipe illustrates accounting with the original counts plus **one newly authored residue waste term**. The following are qualification inputs, **not original values or accepted gameplay balance**:

| Counted material | Mass per unit (mg) | Occupied volume per unit (µL) | Hardness rating | Recipe role/count |
|---|---:|---:|---:|---|
| Berries | 20,000 | 30,000 | 1 | Input ×2 |
| Wood | 50,000 | 100,000 | 8 | Input ×1 |
| Rations | 40,000 | 45,000 | 2 | Output ×2 |
| Residue | 10,000 | 25,000 | 0 | Waste ×1 |

The illustrative work requirement is **120 work units**. Inputs total 90,000 mg; rations retain 80,000 mg and residue retains 10,000 mg. Occupied input volume is 160,000 µL while output plus waste occupies 115,000 µL. That difference is valid under the ordinary-material definition; it is not evidence that a fluid solver or actual recipe geometry works.

This calibration deliberately does not settle whether wood is fuel, packaging or another actual process ingredient. Choosing that interpretation requires explicit future steps/costs and, for combustion, real source/sink accounting. The original recipe remains unchanged. See [the readable calibration record](craft-rations-calibration.json); no source-packet files are rewritten by this work.

## Custody and provenance boundary

The existing `CountedSourceLot` represents an **untransformed** counted lot with one exact source interval. Output item count cannot substitute for the provenance of multiple input materials. These definition types therefore offer no `Craft`, `CreateLot`, source-interval conversion, inventory mutation or export method.

Actual crafting must consume/reserve legitimate input lots and preserve their source-claim union through outputs **and waste**, without multiplying export rights or discarding provenance through byproducts. Splitting, merging, disposing of waste, recycling and later transformations require accountable claim partitions/lineage. A new output definition hash cannot become a new independent geological or renewable source identity. Unsupported transformed lineage must refuse cross-branch export rather than being relabeled as an untransformed `CountedSourceLot`.

Exact mass accounting is necessary but does not prove provenance, custody, protected-item policy, station/tool/worker eligibility, quantity availability, quality, food safety, energy conservation or actual job completion. The future authority transaction must validate those rules against real revisions, record partial work/interruptions and commit inputs, outputs, costs, claims, receipts and scheduler changes atomically.

## Canonical format

The existing primitive frame remains `OFC` plus NUL, UInt32 big-endian framing version **1**, UInt32 big-endian field count including the tag, then UInt32 byte length and bytes for each field. Tags/text are exact strict UTF-8. All numeric fields below are eight unsigned big-endian bytes; hardness must also fit UInt32. IDs are 16 canonical bytes, version hashes 32 bytes. Optional original payload lineage is an empty field or exactly 32 bytes.

| Tag | Ordered fields after tag |
|---|---|
| `production.material` | version `1`; package text; definition ID; stable key text; mass per unit; volume per unit; hardness |
| `production.material-catalog` | version `1`; zero or more complete material frames sorted by ID |
| `production.recipe-term` | version `1`; role; material definition ID; exact material version hash; positive count |
| `production.recipe-terms` | version `1`; complete term frames sorted by role then material ID |
| `production.recipe` | version `1`; package; recipe ID; stable key; positive work; optional original payload hash; complete used-material catalog frame; complete terms frame |

The complete recipe is limited to **65,536 bytes**, its material catalog to **40,960**, each material to **512**, the term set to **16,384**, and each term to **192**. Tags are at most 64 bytes and package/key text at most 128. Count/length limits are checked before untrusted field expansion. Unknown framing/contract versions, tags/roles, malformed UTF-8, bad widths, duplicate/unsorted entries, unused definitions, stale pins, invalid units, broken conservation, overflow, truncation and trailing bytes reject. No decoder normalizes a corrupt definition into accepted content.

## Retained verification and remaining work

[generate_production_vectors.py](../../../Runtime/tests/OpenFantasy.Core.Tests/Fixtures/generate_production_vectors.py) is an independent Python standard-library oracle and prints JSON to stdout. Verification compares stdout against frozen [production-v1.json](../../../Runtime/tests/OpenFantasy.Core.Tests/Fixtures/production-v1.json); expected bytes are never regenerated in place during verification. The oracle separately preserves the original recipe payload/hash, spells all material/recipe frames, derives version hashes and supplies **30 malformed recipes**.

The retained tests compare exact material/recipe bytes and hashes, reproduce round trips, check every incomplete recipe prefix and oversized frame, preserve original counts separately from calibration/waste, enforce mass accounting while allowing occupied-volume changes, reject missing/stale/conflicting pins and duplicate identities/roles, verify defensive copies, check exact 64/65 bounds and early termination, and exercise integer limits/overflow. The coordinator reported the full current pinned integration passing **194 tests** after these files stabilized, including the retained Production tests. That build/test execution belongs to the coordinator; this worker independently reproduced the Python stdout fixture and matched its source recipe payload/hash to the existing preserved source packet. This document does not claim unrun runtime or gameplay qualification.

Remaining work includes multi-stage process graphs and bounded iterations, real tools/catalysts/stations, capabilities and worker roles, knowledge/substitution predicates, environment/heat/energy, work/exposure rates, interruption, quality/discovery, protected ingredients, transformed custody/provenance, actual harvesting/mining/fuel/byproducts, crafting execution and human balance/presentation acceptance. These remain full-scope requirements rather than implicit behaviors of this first recipe definition.
