#!/usr/bin/env python3
"""Read-only source-copy and native-resource checks; does not execute Godot or render images."""
import datetime
import hashlib
import json
from pathlib import Path
import re
import struct


def digest(data):
    return hashlib.sha256(data).hexdigest()


def verify():
    runtime = Path(__file__).resolve().parents[4] / "Runtime"
    repo = runtime.parent.parent
    assets = runtime / "client/assets/native-farm"
    provenance = json.loads((assets / "PROVENANCE.json").read_bytes())
    assert len(provenance["copiedFiles"]) == 8
    png_count = 0
    for item in provenance["copiedFiles"]:
        original = (runtime / item["source"]).read_bytes()
        copied = (runtime / item["copy"]).read_bytes()
        assert copied == original
        assert len(copied) == item["bytes"]
        assert digest(copied) == item["sha256"]
        if item["copy"].endswith(".png"):
            assert copied[:8] == b"\x89PNG\r\n\x1a\n"
            dimensions = struct.unpack(">II", copied[16:24])
            assert dimensions == ((256, 144) if item["copy"].endswith("/tiles.png") else (48, 96))
            png_count += 1
    source = provenance["sourceWorld"]
    source_bytes = (runtime / source["path"]).read_bytes()
    assert digest(source_bytes) == source["sha256"]
    world = json.loads(source_bytes)
    walkable = {tile["id"]: tile["walkable"] for tile in world["tileset"]}
    original_rows = []
    for y in range(320, 352):
        row = []
        runs = world["rows"][y]
        assert len(runs) % 2 == 0
        for i in range(0, len(runs), 2):
            assert runs[i + 1] > 0
            row.extend([runs[i]] * runs[i + 1])
        assert len(row) == world["width"] == 512
        original_rows.append(row[96:128])
    displayed = [[tile if walkable[tile] else 0 for tile in row] for row in original_rows]
    assert original_rows == provenance["originalTileRows"]
    assert displayed == provenance["displayTileRows"]
    replacements = sum(not walkable[tile] for row in original_rows for tile in row)
    assert replacements == provenance["replacedBlockingTileCount"] == 165
    assert set(tile for row in displayed for tile in row) == {0, 3, 4, 11, 24, 27, 28}
    witness = provenance["sourceRendererWitness"]
    assert digest((repo / witness["path"]).read_bytes()) == witness["sha256"]
    witness = provenance["sourcePaperdollWitness"]
    assert digest((runtime / witness["path"]).read_bytes()) == witness["sha256"]
    witness = provenance["sourceFrameWitness"]
    assert digest((runtime / witness["path"]).read_bytes()) == witness["sha256"]
    witness = provenance["sourceFacingWitness"]
    assert digest((repo / witness["path"]).read_bytes()) == witness["sha256"]
    resource = (assets / "farm_visual.tres").read_text()
    assert digest(resource.encode()) == provenance["resourceSha256"]
    assert '[gd_resource type="Resource" load_steps=8 format=3]' in resource
    referenced = re.findall(r'\[ext_resource type="Texture2D" path="res://([^"]+)" id="([^"]+)"\]', resource)
    assert len(referenced) == png_count == 7
    assert {name for _, name in referenced} == {"tiles", "squire", "body", "hair", "outfit_a", "outfit_b", "accessory"}
    for path, name in referenced:
        assert (runtime / "client" / path).is_file()
        assert f'metadata/{name} = ExtResource("{name}")' in resource
    ground_text = re.search(r'metadata/ground_ids = PackedInt32Array\(([^)]*)\)', resource)[1]
    assert [int(value.strip()) for value in ground_text.split(",")] == [tile for row in displayed for tile in row]
    palette_text = re.search(r'metadata/palette_json = (.*)', resource)[1]
    assert json.loads(palette_text) == (assets / "appearance.json").read_text()
    assert 'metadata/source_appearance = PackedInt32Array(0, 5, 5, 13, 8, 10, 10, 3)' in resource
    assert provenance["frameGeometry"] == {"width": 16, "height": 24, "columns": 3, "rows": 4,
        "scale": 2, "directionRows": ["down", "up", "left", "right"], "walkColumns": [0, 1, 2, 1]}
    scripts = [runtime / "client/scripts/FarmCanvas.cs"] + sorted((runtime / "client/scripts/Presentation").glob("*.cs"))
    for script in scripts:
        text = script.read_text()
        assert "WorldSnapshot" not in text and "InspectForHost" not in text and "OpenFantasy.Hosting" not in text
    inputs = sorted(scripts + list(assets.rglob("*.png")) + list(assets.glob("*.json"))
                    + [assets / "farm_visual.tres", Path(__file__).resolve(), Path(__file__).with_name("CANVAS.md")])
    return {"schemaVersion": 1, "executedAtUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "kind": "executed_independent_source_copy_and_native_resource_data_verification",
        "godotOrDotNetExecuted": False, "originalCopiesVerified": 8, "pngHeadersVerified": png_count,
        "exactSourceCropCells": 1024, "retainedWalkableSourceCells": 1024-replacements,
        "blockingPropsExplicitlyReplacedWithGrass": replacements, "exportedTextureReferences": len(referenced),
        "publicProjectionDependencyCheck": True,
        "inputs": [{"path": str(path.relative_to(repo)), "bytes": path.stat().st_size,
                    "sha256": digest(path.read_bytes())} for path in inputs],
        "limits": ["This does not compile/import the resource or execute the C# compositor.",
                   "Actual native display, movement animation and user art acceptance remain coordinator/user gates."]}


if __name__ == "__main__":
    print(json.dumps(verify(), ensure_ascii=False, indent=2))
