"""Independent review evidence; reads frozen inputs and emits JSON to stdout only."""
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[4]
RUNTIME = ROOT / "Beta/Runtime"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def unpack(data, tag=None):
    assert data[:4] == b"OFC\0"
    version, count = struct.unpack(">II", data[4:12])
    assert version == 1 and count > 0
    values, offset = [], 12
    for _ in range(count):
        length = int.from_bytes(data[offset:offset + 4], "big")
        offset += 4
        assert offset + length <= len(data)
        values.append(data[offset:offset + length])
        offset += length
    assert offset == len(data)
    if tag:
        assert values[0] == tag.encode()
    return values


def number(data):
    assert len(data) == 8
    return int.from_bytes(data, "big")


def recipe_shape(encoded):
    recipe = unpack(encoded, "craft.recipe")
    physical = unpack(recipe[3], "production.recipe")
    materials = {}
    for encoded_material in unpack(physical[7], "production.material-catalog")[2:]:
        material = unpack(encoded_material, "production.material")
        materials[material[3]] = number(material[5])
    item_materials = {}
    for encoded_binding in unpack(recipe[4], "craft.bindings")[1:]:
        binding = unpack(encoded_binding, "craft.item-physical")
        item_materials[binding[2]] = binding[4]
    input_terms, outputs = {}, []
    for encoded_term in unpack(physical[8], "production.recipe-terms")[2:]:
        term = unpack(encoded_term, "production.recipe-term")
        role, material, count = number(term[2]), term[3], number(term[5])
        if role == 1:
            input_terms[material] = count
        else:
            outputs.append((material, count))
    allocations = [unpack(value, "craft.output-allocation")
                   for value in unpack(recipe[5], "craft.allocations")[1:]]
    for material, count in input_terms.items():
        total = sum(number(value[5]) * outputs[number(value[4])][1]
                    for value in allocations if item_materials[value[2]] == material)
        assert total == materials[material] * count
    for index, (material, _) in enumerate(outputs):
        assert sum(number(value[5]) for value in allocations if number(value[4]) == index) == materials[material]
    return {"id": recipe[2], "items": {item: materials[material] for item, material in item_materials.items()},
            "input_mass": sum(materials[material] * count for material, count in input_terms.items()),
            "outputs": [(materials[material], count) for material, count in outputs]}


def validate_ledger(encoded):
    state = unpack(encoded, "production.snapshot")
    recipes = {shape["id"]: shape for shape in
               (recipe_shape(value) for value in unpack(state[5], "production.recipes")[1:])}
    batches = {}
    claims, raw_intervals = {}, {}
    raw_mass = 0
    for encoded_batch in unpack(state[6], "production.batches")[1:]:
        batch = unpack(encoded_batch, "production.batch")
        recipe = recipes[batch[6]]
        batches[batch[2]] = recipe
        input_mass = 0
        for encoded_input in unpack(batch[8], "production.retired")[1:]:
            ingredient = unpack(encoded_input, "production.ingredient")
            claim = unpack(ingredient[7])
            mass = recipe["items"][ingredient[5]]
            if number(ingredient[2]) == 1:
                start, end = number(claim[3]), number(claim[4])
                raw_intervals.setdefault(claim[2], []).append((start, end))
                raw_mass += mass * (end - start)
            else:
                start, end = number(claim[4]), number(claim[5])
                claims.setdefault((claim[2], number(claim[3])), []).append((start, end))
            input_mass += mass * (end - start)
        assert input_mass == recipe["input_mass"]
        assert input_mass == sum(mass * count for mass, count in recipe["outputs"])
    live_mass = 0
    live_lots = unpack(state[7], "production.lots")[1:]
    for encoded_lot in live_lots:
        lot = unpack(encoded_lot, "production.lot")
        output = unpack(lot[6], "production.output-claim")
        index, start, end = number(output[3]), number(output[4]), number(output[5])
        claims.setdefault((output[2], index), []).append((start, end))
        live_mass += batches[output[2]]["outputs"][index][0] * (end - start)
    for batch, recipe in batches.items():
        for index, (_, count) in enumerate(recipe["outputs"]):
            cursor = 0
            for start, end in sorted(claims[(batch, index)]):
                assert start == cursor and end > start
                cursor = end
            assert cursor == count
    for intervals in raw_intervals.values():
        end = 0
        for start, next_end in sorted(intervals):
            assert start >= end and next_end > start
            end = next_end
    assert raw_mass == live_mass
    return {"batches": len(batches), "live_lots": len(live_lots),
            "retired_raw_mass_mg": raw_mass, "live_output_mass_mg": live_mass}


generator = RUNTIME / "tests/OpenFantasy.Core.Tests/Fixtures/generate_production_ledger_vectors.py"
fixture = generator.with_name("production-ledger-v1.json")
actual = subprocess.check_output([sys.executable, str(generator)], cwd=ROOT)
assert actual == fixture.read_bytes()
vectors = json.loads(actual)
for key in ("recipe", "repackRecipe", "empty", "crafted", "chained"):
    value = vectors[key]
    encoded = bytes.fromhex(value["hex"])
    assert len(encoded) == value["bytes"] and sha(encoded) == value["sha256"]
ledgers = {key: validate_ledger(bytes.fromhex(vectors[key]["hex"])) for key in ("empty", "crafted", "chained")}

paths = list((RUNTIME / "src/OpenFantasy.Core/Production").glob("*.cs"))
for name in ("WorldProductionMigration", "WorldProductionMigrationProcessor", "WorldProductionMigrationCodecV1",
             "WorldCraftResolver", "WorldStepProcessor", "WorldAdmissionValidator", "WorldSnapshotCodecV3",
             "WorldActionCodecV3", "WorldStepCodecV3"):
    paths.append(RUNTIME / f"src/OpenFantasy.Core/Worlds/{name}.cs")
paths.append(RUNTIME / "src/OpenFantasy.Core/Authority/CustodianAccessPolicy.cs")
for folder, names in (("Production", ("ProductionLedgerTests", "InstantCraftTests", "InstantCraftFixture", "ProductionGoldenTests")),
                      ("Worlds", ("ProductionWorldFixture", "ProductionWorldTests"))):
    paths.extend(RUNTIME / f"tests/OpenFantasy.Core.Tests/{folder}/{name}.cs" for name in names)
paths += [generator, fixture, Path(__file__).with_name("CORE_CONTRACT.md"), Path(__file__)]
print(json.dumps({"schema": "production-core-static-review.v1", "result": "passed",
                  "execution": "Independent Python only; .NET results belong to coordinator evidence.",
                  "fixture_stdout_bytes": len(actual), "fixture_sha256": sha(actual),
                  "positive_vectors": 5, "malformed_vectors_declared": len(vectors["malformed"]),
                  "independent_ledger_checks": ledgers,
                  "inputs": [{"path": str(path.relative_to(ROOT)), "sha256": sha(path.read_bytes())}
                             for path in sorted(paths)]}, indent=2))
