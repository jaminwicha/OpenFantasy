#!/usr/bin/env python3
"""Independent read-only source/admission checks; emits evidence to stdout, never executes C#."""
import hashlib
import json
import math
from pathlib import Path
import struct
import zipfile

ROOT = Path(__file__).resolve().parents[4]
CONTENT = ROOT / "Beta/Runtime/content"
EXPECTED = "261e86948438c7bd8ad34e001c918c40053acedf82041a3f89909a54db095918"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode()


def frame(tag, *fields):
    fields = (tag.encode(),) + fields
    return b"OFC\0" + struct.pack(">II", 1, len(fields)) + b"".join(struct.pack(">I", len(x)) + x for x in fields)


def definition_id(key):
    return digest(frame("entity.generated", b"openfantasy.authored-content", b"quest-farm.definitions.v1",
                        key.encode(), b"definition", struct.pack(">Q", 0)))[:32]


def instance_id(seed, domain, role):
    return hashlib.sha256(frame("entity.generated", seed, domain.encode(), role.encode(),
                                b"instance", struct.pack(">Q", 0))).digest()[:16]


path = CONTENT / "quest-farm/admission-v1.json"
raw = path.read_bytes()
assert len(raw) <= 16384 and digest(raw) == EXPECTED
manifest = json.loads(raw)
assert digest((CONTENT / "production-farm/admission-v1.json").read_bytes()) == manifest["productionAdmissionSha256"]
gather = json.loads((CONTENT / "adventure-farm/admission-v1.json").read_bytes())
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
records = {record["targetKey"]: record for record in json.loads(records_raw)["records"]}
for selected in manifest["sourceRecords"]:
    record = records[selected["targetKey"]]
    assert record["sourceIdentity"] == selected["sourceIdentity"]
    original = record["sourceIdentity"]
    assert digest(canonical(record["sourcePayload"])) == original["payloadSha256"]
    group, index = original["jsonPointer"].strip("/").split("/")
    source = json.loads((packet / "source-support" / original["path"]).read_bytes())
    assert source[group][int(index)] == record["sourcePayload"]
    assert original["inventoryId"] == original["path"] + "#" + original["jsonPointer"]
for witness in manifest["sourceWitnesses"]:
    data = (CONTENT / witness["path"]).read_bytes()
    assert len(data) == witness["bytes"] and digest(data) == witness["sha256"]
supplement = manifest["supplementalSource"]
archive_path = ROOT / "Beta/content-migration/original-source-2026-09-08.zip"
assert digest(archive_path.read_bytes()) == supplement["archiveSha256"]
with zipfile.ZipFile(archive_path) as archive:
    original_entity = archive.read(supplement["path"])
assert digest(original_entity) == supplement["sha256"]
assert original_entity == (CONTENT / supplement["runtimeCopy"]).read_bytes()

quest = records[manifest["quest"]["sourceTargetKey"]]["sourcePayload"]
squire = records[manifest["playerProfile"]["sourceTargetKey"]]["sourcePayload"]
assert quest["targetCount"] == manifest["project"]["unitCapacity"] == 6
assert quest["rewardXp"] == manifest["quest"]["expectedExperience"] == 420
assert quest["rewardGil"] == manifest["currency"]["reserveBalance"] == manifest["currency"]["admittedSupply"] == 200
assert quest["minLevel"] == manifest["playerProfile"]["initialLevel"] == 1
assert manifest["playerProfile"]["initialExperience"] == manifest["wrenProfile"]["initialExperience"] == 0
assert manifest["playerProfile"]["initialGil"] == manifest["wrenProfile"]["initialGil"] == 0
assert manifest["wrenProfile"]["baseStats"] == [1, 0, 0, 0, 0, 1]
assert manifest["wrenProfile"]["growth"] == manifest["wrenProfile"]["heritageModifiers"] == [0] * 6
assert manifest["playerProfile"]["heritageKey"] == "human"
assert manifest["playerProfile"]["heritageModifiers"] == [0] * 6

thresholds = [math.isqrt(level ** 3 * 10000) for level in range(1, 10)]
assert manifest["levelCurve"]["thresholds"] == thresholds
level, xp = 1, quest["rewardXp"]
while level < 10 and xp >= thresholds[level - 1]:
    xp -= thresholds[level - 1]
    level += 1
stats = {key: value + squire["growth"][key] * (level - 1) for key, value in squire["baseStats"].items()}
assert level == 3 and xp == 38 and stats == {"hp": 78, "mp": 14, "attack": 18, "magic": 6, "defense": 12, "speed": 10}
base = json.loads((CONTENT / "native-farm/admission-v1.json").read_bytes())
wood_id = digest(frame("entity.generated", b"openfantasy.authored-content", b"native-farm.definitions.v1",
                       b"items/wood", b"definition", struct.pack(">Q", 0)))[:32]
assert wood_id == manifest["quest"]["itemDefinitionId"]
assert digest(canonical(base["wood"])) == manifest["quest"]["itemVersion"]
keys = [manifest[group]["definitionKey"] for group in ("playerProfile", "wrenProfile", "levelCurve", "currency")]
identities = {key: definition_id(key) for key in keys}
assert len(set(identities.values())) == 4 and wood_id not in identities.values()
river_seed = digest(frame("farm-work.seed.v1", b"river")).encode()
river_world = instance_id(river_seed, "native-farm.instances.v1", "world")
river_branch = instance_id(river_seed, "native-farm.instances.v1", "branch.main")
river_scope = frame("quest-farm.instance-scope", river_world, river_branch)
river_instances = {role: instance_id(river_scope, "quest-farm.instances.v1", role).hex() for role in
                   ("quest.project-custodian", "quest.project-reservation", "quest.reward-reserve", "quest.wren-saplings", "quest.wren-reward")}
print(json.dumps({"scope": "Independent Python source, preserved archive bytes, thresholds and admission identity checks; no C# execution.",
                  "manifestSha256": EXPECTED, "manifestBytes": len(raw), "retainedClosureFiles": len(closure["files"]),
                  "selectedSourceRecords": len(manifest["sourceRecords"]), "sourceWitnesses": len(manifest["sourceWitnesses"]),
                  "supplementalEntitySha256": digest(original_entity), "definitionIds": identities,
                  "riverWorld": river_world.hex(), "riverBranch": river_branch.hex(), "riverInstanceIds": river_instances,
                  "thresholds": thresholds, "firstReward": {"level": level, "residualXp": xp, "stats": stats},
                  "actorGil": [0, 0], "reserveGil": 200, "projectCapacity": 6}, indent=2))
