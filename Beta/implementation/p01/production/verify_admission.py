#!/usr/bin/env python3
"""Read-only source and physical-admission oracle. Does not execute the C# application."""
import hashlib
import json
from pathlib import Path
import struct

ROOT = Path(__file__).resolve().parents[4]
RUNTIME = ROOT / "Beta/Runtime"
CONTENT = RUNTIME / "content"
EXPECTED = "7419a27a9936c8157c98ab99537b2d4433b4d891a6ad9e61320346b551dfe7e1"


def digest(value):
    return hashlib.sha256(value).hexdigest()


def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode()


def number(value):
    return struct.pack(">Q", value)


def frame(tag, *fields):
    fields = (tag.encode(),) + fields
    return b"OFC\0" + struct.pack(">II", 1, len(fields)) + b"".join(struct.pack(">I", len(f)) + f for f in fields)


def identity(domain, key):
    return hashlib.sha256(frame("entity.generated", b"openfantasy.authored-content", domain.encode(),
                                key.encode(), b"definition", number(0))).digest()[:16]


manifest_path = CONTENT / "production-farm/admission-v1.json"
raw = manifest_path.read_bytes()
assert len(raw) <= 16384 and digest(raw) == EXPECTED
manifest = json.loads(raw)
gather_path = CONTENT / "adventure-farm/admission-v1.json"
assert digest(gather_path.read_bytes()) == manifest["gatheringAdmissionSha256"]
gather = json.loads(gather_path.read_bytes())
base = json.loads((CONTENT / "native-farm/admission-v1.json").read_bytes())
assert digest((CONTENT / "native-farm/admission-v1.json").read_bytes()) == gather["baseAdmissionSha256"]
packet = CONTENT / "legacy/aldermere-first-loop"
closure_raw = (packet / "closure-manifest.json").read_bytes()
assert digest(closure_raw) == gather["closureSha256"]
closure = json.loads(closure_raw)
assert len(closure["files"]) <= 64
for entry in closure["files"]:
    data = (packet / entry["path"]).read_bytes()
    assert len(data) == entry["bytes"] and digest(data) == entry["sha256"]
records_raw = (packet / "records.json").read_bytes()
assert digest(records_raw) == gather["recordsSha256"]
records = {r["targetKey"]: r for r in json.loads(records_raw)["records"]}
for selected in manifest["sourceRecords"]:
    record = records[selected["targetKey"]]
    assert record["sourceIdentity"] == selected["sourceIdentity"]
    assert digest(canonical(record["sourcePayload"])) == selected["sourceIdentity"]["payloadSha256"]
    source = selected["sourceIdentity"]
    original = json.loads((packet / "source-support" / source["path"]).read_bytes())
    group, index = source["jsonPointer"].strip("/").split("/")
    assert original[group][int(index)] == record["sourcePayload"]

items = {}
for entry in manifest["existingItems"]:
    key = entry["key"]
    original = base["wood"] if key == "items/wood" else next(v for v in gather["items"] if v["key"] == key)
    domain = "native-farm.definitions.v1" if key == "items/wood" else "adventure-farm.definitions.v1"
    assert identity(domain, key).hex() == entry["definitionId"]
    assert digest(canonical(original)) == entry["version"]
    items[key] = {"definitionId": entry["definitionId"], "version": entry["version"]}
for entry in manifest["outputItems"]:
    items[entry["key"]] = {"definitionId": identity("production-farm.definitions.v1", entry["key"]).hex(),
                           "version": digest(canonical(entry))}

materials = {}
for entry in manifest["physicalMaterials"]:
    identifier = identity("production-farm.definitions.v1", entry["definitionKey"])
    wire = frame("production.material", number(1), manifest["physicalPackage"].encode(), identifier,
                 entry["key"].encode(), number(entry["massMilligrams"]),
                 number(entry["volumeMicrolitres"]), number(entry["hardness"]))
    assert identifier.hex() != items[entry["itemKey"]]["definitionId"]
    materials[entry["itemKey"]] = {**entry, "definitionId": identifier.hex(), "version": digest(wire)}

recipe = manifest["recipe"]
assert recipe["requiredWork"] == 1
source_recipe = records[recipe["sourceTargetKey"]]["sourcePayload"]
inputs = [t for t in recipe["terms"] if t["role"] == 1]
outputs = sorted((t for t in recipe["terms"] if t["role"] != 1), key=lambda t: t["role"])
assert {t["itemKey"]: t["count"] for t in inputs} == {"items/" + t["item"]: t["count"] for t in source_recipe["inputs"]}
assert outputs[0]["count"] == source_recipe["outputCount"] == 2
assert outputs[0]["itemKey"] == "items/" + source_recipe["output"]
input_mass = sum(materials[t["itemKey"]]["massMilligrams"] * t["count"] for t in inputs)
output_mass = sum(materials[t["itemKey"]]["massMilligrams"] * t["count"] for t in outputs)
assert input_mass == output_mass == 90000
for term in inputs:
    allocated = sum(a["massPerOutputUnitMilligrams"] * outputs[a["outputIndex"]]["count"]
                    for a in recipe["allocations"] if a["inputItemKey"] == term["itemKey"])
    assert allocated == materials[term["itemKey"]]["massMilligrams"] * term["count"]
for index, term in enumerate(outputs):
    allocated = sum(a["massPerOutputUnitMilligrams"] for a in recipe["allocations"] if a["outputIndex"] == index)
    assert allocated == materials[term["itemKey"]]["massMilligrams"]
input_volume = sum(materials[t["itemKey"]]["volumeMicrolitres"] * t["count"] for t in inputs)
output_volume = sum(materials[t["itemKey"]]["volumeMicrolitres"] * t["count"] for t in outputs)
assert (input_volume, output_volume) == (160000, 115000)

input_paths = [manifest_path, *sorted((RUNTIME / "src/OpenFantasy.Hosting/Content").glob("ProductionFarm*.cs")),
               RUNTIME / "tests/OpenFantasy.Hosting.Tests/Content/ProductionFarmAdmissionTests.cs", Path(__file__)]
print(json.dumps({"kind": "production_farm_admission_data_oracle", "status": "passed", "dotNetOrGodotExecuted": False,
                  "sourceRecords": len(manifest["sourceRecords"]), "closureFiles": len(closure["files"]),
                  "inputMassMilligrams": input_mass, "outputAndWasteMassMilligrams": output_mass,
                  "inputVolumeMicrolitres": input_volume, "outputAndWasteVolumeMicrolitres": output_volume,
                  "items": items, "physicalMaterials": materials,
                  "inputs": [{"path": str(p.relative_to(ROOT)), "bytes": p.stat().st_size,
                              "sha256": digest(p.read_bytes())} for p in input_paths]}, indent=2))
