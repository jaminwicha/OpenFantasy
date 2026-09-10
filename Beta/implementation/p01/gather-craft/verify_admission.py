#!/usr/bin/env python3
"""Read-only source/admission/asset checks; this does not execute the C# world."""
import hashlib
import json
from pathlib import Path
import struct

ROOT = Path(__file__).resolve().parents[4]
RUNTIME = ROOT / "Beta/Runtime"
PACKET = RUNTIME / "content/legacy/aldermere-first-loop"
ADMISSION = RUNTIME / "content/adventure-farm/admission-v1.json"
EXPECTED_HASH = "f90505e21a578b6e8dd0df78ea195e1c2ee9e5451fee22779480b76ab04239cd"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode()


def original_at(document, pointer):
    value = document
    for segment in pointer.split("/")[1:]:
        value = value[int(segment)] if isinstance(value, list) else value[segment]
    return value


def frame(domain, value):
    fields = [domain.encode(), value.encode()]
    return b"OFC\0" + struct.pack(">II", 1, len(fields)) + b"".join(struct.pack(">I", len(v)) + v for v in fields)


raw = ADMISSION.read_bytes()
assert len(raw) <= 16384 and digest(raw) == EXPECTED_HASH
admission = json.loads(raw)
assert digest((RUNTIME / "content/native-farm/admission-v1.json").read_bytes()) == admission["baseAdmissionSha256"]
assert digest((PACKET / "closure-manifest.json").read_bytes()) == admission["closureSha256"]
records_raw = (PACKET / "records.json").read_bytes()
assert digest(records_raw) == admission["recordsSha256"]
packet = json.loads(records_raw)
records = {r["targetKey"]: r for r in packet["records"]}
for selected in admission["sourceRecords"]:
    record = records[selected["targetKey"]]
    identity = selected["sourceIdentity"]
    assert record["sourceIdentity"] == identity
    assert digest(canonical(record["sourcePayload"])) == identity["payloadSha256"]
    original = json.loads((PACKET / "source-support" / identity["path"]).read_bytes())
    assert original_at(original, identity["jsonPointer"]) == record["sourcePayload"]
for witness in admission["sourceWitnesses"]:
    data = (PACKET / witness["path"]).read_bytes()
    assert len(data) == witness["bytes"] and digest(data) == witness["sha256"]

world = json.loads((PACKET / "source-support/data/world.json").read_bytes())
node_budget = 0
for node in admission["nodes"]:
    source = records[node["sourceSpawnerTargetKey"]]["sourcePayload"]
    rectangle = source["region"]
    x, y = node["tileX"], node["tileY"]
    assert rectangle["x"] <= x < rectangle["x"] + rectangle["w"]
    assert rectangle["y"] <= y < rectangle["y"] + rectangle["h"]
    assert 96 <= x <= 127 and 320 <= y <= 351 and node["feetZ"] == 256
    row = world["rows"][y]
    cells = [tile for i in range(0, len(row), 2) for tile in [row[i]] * row[i + 1]]
    assert len(cells) == 512 and world["tileset"][cells[x]]["walkable"]
    assert not any(z["x"] <= x < z["x"] + z["w"] and z["y"] <= y < z["y"] + z["h"] for z in world["safeZones"])
    assert source["respawnTicks"] == node["regrowthDelaySteps"]
    assert node["completeCycles"] == 32
    node_budget += node["completeCycles"] * node["swingsPerCycle"]
assert node_budget == 288
assert admission["starterKit"]["playerStorageUnitCapacity"] == 32
assert [(t["kind"], t["power"], t["quantity"]) for t in admission["starterKit"]["tools"]] == [(1, 1, 1), (2, 1, 1)]
assert digest(frame("farm-work.seed.v1", "river")) == "b7079362de39c95808fed1ec4e86edb85fd7c257a4e5c36bbd66ff0eb31d5461"
assert digest(frame("farm-work.seed.v1", "é" * 128)) == "1d160f1ecd419ce5e6371b58aeb4506cdf31b21d4c43efc947591e16282c90bf"

assets = RUNTIME / "client/assets/adventure-farm"
provenance = json.loads((assets / "PROVENANCE.json").read_bytes())
for entry in provenance["copiedFiles"]:
    original = (RUNTIME / entry["source"]).read_bytes()
    copy = (RUNTIME / entry["copy"]).read_bytes()
    assert copy == original and len(copy) == entry["bytes"] and digest(copy) == entry["sha256"]
    assert struct.unpack(">II", copy[16:24]) == (48, 96)
resource = (assets / "nodes.tres").read_text()
assert all(f'path="res://assets/adventure-farm/{name}"' in resource for name in ["node_oak.png", "node_berry.png"])

inputs = [ADMISSION, *sorted((RUNTIME / "src/OpenFantasy.Hosting/Content").glob("GatheringFarm*.cs")),
          RUNTIME / "tests/OpenFantasy.Hosting.Tests/Content/GatheringFarmAdmissionTests.cs",
          RUNTIME / "client/scripts/Presentation/FarmGatherNodeView.cs",
          RUNTIME / "client/scripts/Presentation/FarmVisualAssets.cs", *sorted(p for p in assets.iterdir() if p.suffix in {".png", ".tres", ".json"}),
          Path(__file__)]
print(json.dumps({"kind": "independent_gathering_admission_data_verification", "status": "passed", "dotNetOrGodotExecuted": False,
                  "sourceRecords": len(admission["sourceRecords"]), "sourceWitnesses": len(admission["sourceWitnesses"]),
                  "representativeNodes": 2, "nodeFiniteUnits": node_budget, "totalAdmittedUnits": 298,
                  "canonicalSeedVectors": 2, "exactOriginalAssetCopies": 2,
                  "inputs": [{"path": str(p.relative_to(ROOT)), "bytes": p.stat().st_size, "sha256": digest(p.read_bytes())} for p in inputs]}, indent=2))
