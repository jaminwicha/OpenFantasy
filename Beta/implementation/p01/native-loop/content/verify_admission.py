#!/usr/bin/env python3
"""Read-only independent source/admission verification; JSON report goes to stdout."""
import datetime
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET


def digest(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")


def verify():
    runtime = Path(__file__).resolve().parents[4] / "Runtime"
    repo = runtime.parent.parent
    content = runtime / "content"
    packet = content / "legacy/aldermere-first-loop"
    admission_path = content / "native-farm/admission-v1.json"
    admission = json.loads(admission_path.read_bytes())
    presentation_path = content / "native-farm/presentation-v1.json"
    presentation = json.loads(presentation_path.read_bytes())
    application = runtime / "src/OpenFantasy.Hosting/Content"
    project_path = runtime / "src/OpenFantasy.Hosting/OpenFantasy.Hosting.csproj"
    resource = ET.fromstring(project_path.read_bytes()).find(".//EmbeddedResource")
    assert resource is not None
    assert resource.attrib == {"Include": "../../content/**/*",
                               "LogicalName": "OpenFantasy.Content/%(RecursiveDir)%(Filename)%(Extension)",
                               "WithCulture": "false"}
    selected_pin = re.search(r'ExpectedVersion = "([a-f0-9]{64})"',
                             (application / "FarmAdmissionDefinition.cs").read_text())[1]
    assert digest(admission_path.read_bytes()) == selected_pin
    assert digest(presentation_path.read_bytes()) == admission["presentationSha256"]
    manifest_path = content / admission["packageManifest"]
    manifest = json.loads(manifest_path.read_bytes())
    assert digest(manifest_path.read_bytes()) == "9a826b9698d7e17dd34d77e849bac2ada30b6d96168cd5df2e99e5fab672eb66"
    assert manifest["id"] == "openfantasy.aldermere" and not manifest["dependencies"]
    assert manifest["payloadSha256"] == admission["packagePayloadSha256"]
    assert digest((packet / "closure-manifest.json").read_bytes()) == manifest["payloadSha256"]
    assert digest((packet / "records.json").read_bytes()) == admission["recordsSha256"]
    closure = json.loads((packet / "closure-manifest.json").read_bytes())
    records = json.loads((packet / "records.json").read_bytes())["records"]
    assert len(records) == 49
    assert len(closure["files"]) <= 64
    assert sum(item["bytes"] for item in closure["files"]) <= 1_048_576
    assert len({item["path"] for item in closure["files"]}) == len(closure["files"])
    for item in closure["files"]:
        data = (packet / item["path"]).read_bytes()
        assert 0 < len(data) == item["bytes"] <= 262_144
        assert digest(data) == item["sha256"]
    by_key = {record["targetKey"]: record for record in records}
    assert len(by_key) == len(records)
    assert len(admission["sourceRecords"]) == 4
    selected_hashes = []
    for required in admission["sourceRecords"]:
        record = by_key[required["targetKey"]]
        identity = record["sourceIdentity"]
        assert identity == required["sourceIdentity"]
        assert digest(canonical(record["sourcePayload"])) == identity["payloadSha256"]
        assert identity["inventoryId"] == identity["path"] + "#" + identity["jsonPointer"]
        original = json.loads((packet / "source-support" / identity["path"]).read_bytes())
        key, index = identity["jsonPointer"].strip("/").split("/")
        assert original[key][int(index)] == record["sourcePayload"]
        selected_hashes.append({"source": identity["inventoryId"], "sha256": identity["payloadSha256"]})
    wren = by_key["openfantasy.aldermere:npcs/farmer_wren"]["sourcePayload"]
    farm = by_key["openfantasy.aldermere:regions/farm_verdance_small"]["sourcePayload"]
    wood = by_key["openfantasy.aldermere:items/wood"]["sourcePayload"]
    quest = by_key["openfantasy.aldermere:quests/wren_orchard"]["sourcePayload"]
    assert (wren["name"], wren["tileX"], wren["tileY"], wren["wander"]) == ("Wren", 103, 322, 2)
    assert wren["dialog"] == "Just me, the rows, and the crows. Suits me."
    assert presentation["sourceAppearance"] == wren["appearance"]
    assert presentation["sourceSprite"] == wren["sprite"] == ""
    assert presentation["sourceNpc"] == by_key["openfantasy.aldermere:npcs/farmer_wren"]["sourceIdentity"]
    assert len(presentation["assets"]) == 5 and len(presentation["sourceSemantics"]) == 3
    for item in presentation["assets"] + presentation["sourceSemantics"]:
        assert digest((content / item["path"]).read_bytes()) == item["sha256"]
    rectangle = admission["place"]["sourceRectangle"]
    assert rectangle == {"x": farm["x"], "y": farm["y"], "width": farm["w"], "depth": farm["h"]}
    player = admission["player"]
    assert (player["tileX"], player["tileY"], player["feetZ"]) == (101, 325, 256)
    assert player["initialWood"] == 8 and admission["wren"]["initialWood"] == 0
    assert wood["id"] == quest["targetItem"] == "wood" and wood["kind"] == "Material"
    assert quest["giver"] == wren["id"]
    assert quest["targetCount"] == admission["deliveryProof"]["count"] == 6
    assert (101-103)**2 + (325-322)**2 > 9
    assert (101-103)**2 + (324-322)**2 <= 9
    assert player["controllerKind"] == 1 and admission["wren"]["controllerKind"] == 2
    assert player["comprehensionBand"] == admission["wren"]["comprehensionBand"] == 2
    assert admission["language"]["mode"] == 1
    assert admission["body"]["clearance"] == [180, 140, 464]
    assert len(admission["body"]["parts"]) == 15
    assert any(10 in part["functions"] for part in admission["body"]["parts"])
    assert admission["terrain"]["minimum"] == [96, 320, 0]
    assert admission["terrain"]["size"] == [32, 32, 16]
    assert admission["terrain"]["relief"] == admission["terrain"]["baseSurfaceZ"] == 0
    assert admission["initialCalendar"] == {"day": 0, "millisecondsWithinDay": 32_400_000}
    assert admission["initialRevision"] == admission["initialAdvancingSteps"] == admission["initialExperiencedMilliseconds"] == 0
    assert not admission["initialDialogues"]
    inputs = sorted(list(application.glob("*.cs"))
                    + list((runtime / "tests/OpenFantasy.Hosting.Tests/Content").glob("*.cs"))
                    + list((content / "native-farm").glob("*.json"))
                    + [project_path, Path(__file__).resolve(), Path(__file__).with_name("CONTRACT.md")])
    return {
        "schemaVersion": 1,
        "executedAtUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "kind": "executed_independent_python_source_and_admission_validation",
        "applicationExecuted": False,
        "workerDotNetBuilds": 0,
        "embeddedResourceDeclarationVerified": True,
        "actualManifestResourceExecutionVerifiedByThisScript": False,
        "admissionSha256": selected_pin,
        "packetFilesVerified": len(closure["files"]),
        "packetBytes": sum(item["bytes"] for item in closure["files"]),
        "selectedPayloadsVerified": selected_hashes,
        "presentationAssetsVerified": len(presentation["assets"]),
        "presentationSemanticsVerified": len(presentation["sourceSemantics"]),
        "initialPlayerTile": [101, 325],
        "initialWrenTile": [103, 322],
        "spawnOutsideRange": True,
        "oneCellNorthInsideRange": True,
        "declaredFiniteWood": {"player": 8, "wren": 0},
        "inputs": [{"path": str(path.relative_to(repo)), "bytes": path.stat().st_size,
                    "sha256": digest(path.read_bytes())} for path in inputs],
        "limits": ["No C# decoder, world factory, store or Godot execution by this Python check.",
                   "Exact source preservation and authored admission are not full quest, geography or final art acceptance."]
    }


if __name__ == "__main__":
    print(json.dumps(verify(), ensure_ascii=False, indent=2))
