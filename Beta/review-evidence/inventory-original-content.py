"""Read-only source inventory; writes a versioned planning snapshot under Beta.

This is not a Beta importer. Raw payloads remain in the snapshot; semantic migration
and playable reachability remain pending even when inventory coverage is complete.
"""
from pathlib import Path
import hashlib
import json
import re
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Beta" / "content-migration"
STAMP = "2026-09-08"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def main():
    OUT.mkdir(exist_ok=True)
    archive = OUT / f"original-source-{STAMP}.zip"
    manifest_path = OUT / f"original-content-inventory-{STAMP}.json"
    if archive.exists() or manifest_path.exists():
        raise SystemExit("Snapshot already exists; choose a new version instead of overwriting it.")
    files = []
    for folder in ("data", "assets", "docs", "src", "tools", "scenes"):
        files.extend(p for p in (ROOT / folder).rglob("*") if p.is_file()
                     and "__pycache__" not in p.parts
                     and p.suffix not in (".pyc", ".import"))
    files.extend(ROOT / p for p in ("README.md", "project.godot", "OpenFantasy.csproj",
                                   "OpenFantasy.sln", "icon.svg", ".gitignore")
                 if (ROOT / p).is_file())
    files = sorted(set(files))
    entries, records, sections = [], [], []
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as z:
        for p in files:
            rel = p.relative_to(ROOT).as_posix()
            raw = p.read_bytes()
            z.writestr(rel, raw)
            entries.append({"path": rel, "bytes": len(raw), "sha256": digest(raw),
                            "migration_status": "inventoried_not_ported"})
            if p.suffix == ".md":
                text = raw.decode()
                headings = list(re.finditer(r"^#{1,6} .+$", text, re.M))
                for i, h in enumerate(headings):
                    body = text[h.start():headings[i + 1].start() if i + 1 < len(headings) else len(text)]
                    sections.append({"source": rel, "line": text[:h.start()].count("\n") + 1,
                                     "heading": h.group(), "sha256": digest(body.encode()),
                                     "semantic_reconciliation": "pending"})
    counts = {}

    def add(kind, source, pointer, value, legacy_id=None):
        records.append({"inventory_id": f"{source}#{pointer}", "kind": kind,
                        "legacy_id": legacy_id, "source": source, "json_pointer": pointer,
                        "display_name": value.get("name") if isinstance(value, dict) else None,
                        "payload_sha256": digest(json.dumps(value, ensure_ascii=False, sort_keys=True,
                                                             separators=(",", ":")).encode()),
                        "beta_target_ids": [], "migration_status": "inventoried_not_ported",
                        "integration_evidence": [], "preserve_required": True})
        counts[kind] = counts.get(kind, 0) + 1

    for kind in ("classes", "abilities", "monsters", "items", "quests", "recipes", "npcs"):
        source = f"data/{kind}.json"
        for i, value in enumerate(json.loads((ROOT / source).read_text())[kind]):
            add(kind, source, f"/{kind}/{i}", value, value["id"])
    world = json.loads((ROOT / "data/world.json").read_text())
    for kind in ("tileset", "regions", "spawners", "nodes", "safeZones", "crystals"):
        for i, value in enumerate(world[kind]):
            add(kind, "data/world.json", f"/{kind}/{i}", value, value.get("id"))
    for kind in ("rows", "playerSpawn", "width", "height"):
        add("world_" + kind, "data/world.json", "/" + kind, world[kind])
    meta = json.loads((ROOT / "data/world_meta.json").read_text())
    for key, value in meta["towns"].items():
        add("towns", "data/world_meta.json", "/towns/" + key, value, key)
        for i, building in enumerate(value["buildings"]):
            add("buildings", "data/world_meta.json", f"/towns/{key}/buildings/{i}", building)
    for kind in ("farms", "homeless"):
        for i, value in enumerate(meta[kind]):
            add(kind, "data/world_meta.json", f"/{kind}/{i}", value, value.get("id"))
    appearance = json.loads((ROOT / "data/appearance.json").read_text())
    for kind, value in appearance.items():
        if isinstance(value, list):
            for i, part in enumerate(value):
                add("appearance_" + kind, "data/appearance.json", f"/{kind}/{i}", part,
                    part.get("name") if isinstance(part, dict) else None)
        else:
            add("appearance_" + kind, "data/appearance.json", "/" + kind, value)
    # Code-defined content is inventoried separately; this is deliberately not a C# parser.
    code_records = []
    for source, pattern, kind in (
        ("src/Sim/SiteGen/SiteGenerator.cs", r'\["([^"]+)"\] = new SiteRecipe', "site_recipe"),
        ("src/Sim/World.Gathering.cs", r'\["([^"]+)"\] = new NodeArchetype', "node_archetype"),
    ):
        text = (ROOT / source).read_text()
        for match in re.finditer(pattern, text):
            code_records.append({"kind": kind, "legacy_id": match[1], "source": source,
                                 "line": text[:match.start()].count("\n") + 1,
                                 "beta_target_ids": [], "migration_status": "inventoried_not_ported"})
    manifest = {
        "schema_version": 1, "date": STAMP,
        "scope": "Current OpenFantasy working tree, including staged milestone 8; Beta excluded",
        "source_head": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
        "source_working_tree_status": subprocess.check_output(["git", "status", "--short"], cwd=ROOT, text=True).splitlines(),
        "snapshot": archive.name, "snapshot_sha256": digest(archive.read_bytes()),
        "excluded": [".git history and internals", "Beta", "Godot/build/import caches", "Python bytecode", "external player saves"],
        "scope_limits": ["A file and JSON-record inventory is not exhaustive semantic understanding.",
                         "Documentation sections include superseded constraints and unimplemented concepts; reconcile individually.",
                         "Procedural families are preserved through generators, recipes, and future seed fixtures, not enumeration of infinite instances.",
                         "No executable import or Beta content integration has been implemented."],
        "counts": counts, "files": entries, "data_records": records,
        "code_defined_content": code_records, "document_sections": sections,
        "semantic_coverage_status": "pending_review",
        "playable_import_coverage": 0,
    }
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n")
    with zipfile.ZipFile(archive) as z:
        assert set(z.namelist()) == {e["path"] for e in entries}
        assert all(digest(z.read(e["path"])) == e["sha256"] for e in entries)
    print(json.dumps({"snapshot_files": len(entries), "data_records": len(records),
                      "document_sections": len(sections), "code_defined_records": len(code_records),
                      "counts": counts, "snapshot_hash_verified": True}, indent=2))


if __name__ == "__main__":
    main()
