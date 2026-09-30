"""Independently compare closed SQLite ledgers; opaque bytes are not semantic replay proof."""
import argparse
import hashlib
import json
from pathlib import Path
import sqlite3


SOURCE_SHA256 = "814f0afc093031d1c9308d3e1c89683b2afbdda0076b2c636b084d2e4fb23773"
TABLES = {
    "world_checkpoints", "world_command_outcomes", "world_commits", "world_genesis",
    "world_head", "world_material_care_segments", "world_occupied_identity",
}


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def open_closed(path):
    # An immutable reader never creates SHM/WAL files and must not ignore uncheckpointed data.
    for suffix in ("-wal", "-shm", "-journal"):
        assert not Path(str(path) + suffix).exists(), "Require a closed checkpointed copy: " + str(path)
    return sqlite3.connect(path.as_uri() + "?mode=ro&immutable=1", uri=True)


def ordered_rows(database, table):
    columns = database.execute('PRAGMA table_info("' + table + '")').fetchall()
    keys = [column[1] for column in sorted(columns, key=lambda column: column[5]) if column[5]]
    assert keys, "Expected a primary key: " + table
    order = ",".join('"' + key + '"' for key in keys)
    rows = database.execute('SELECT * FROM "' + table + '" ORDER BY ' + order).fetchall()
    key_indices = [next(i for i, column in enumerate(columns) if column[1] == key) for key in keys]
    return {tuple(row[i] for i in key_indices): row for row in rows}


def rows_hash(rows):
    # Explicit type tags keep integers, strings, NULL and blob values unambiguous.
    canonical = [
        [["blob", value.hex()] if isinstance(value, bytes) else [type(value).__name__, value] for value in row]
        for row in rows
    ]
    return hashlib.sha256(json.dumps(canonical, separators=(",", ":")).encode()).hexdigest()


def audit(source_path, target_path, require_further_cut=True):
    source_hash = sha256(source_path)
    target_hash = sha256(target_path)
    assert source_hash == SOURCE_SHA256, "This acceptance requires the actual original retained source."
    assert source_path != target_path, "The upgraded destination must be a distinct copy."
    evidence = {}
    source = open_closed(source_path)
    target = open_closed(target_path)
    try:
        source_schema = dict(source.execute("SELECT name,sql FROM sqlite_master WHERE type='table'"))
        target_schema = dict(target.execute("SELECT name,sql FROM sqlite_master WHERE type='table'"))
        assert set(source_schema) == TABLES and target_schema == source_schema, "Unexpected table/schema change."
        assert source.execute("PRAGMA user_version").fetchone() == target.execute("PRAGMA user_version").fetchone(), "Schema version changed."
        for table in sorted(TABLES - {"world_head"}):
            before, after = ordered_rows(source, table), ordered_rows(target, table)
            missing = [key for key, row in before.items() if after.get(key) != row]
            assert not missing, "Historical rows removed or changed: " + table
            if table == "world_genesis":
                assert after == before, "Genesis replaced or duplicated."
            evidence[table] = {"sourceRows": len(before), "targetRows": len(after),
                               "retainedRowsSha256": rows_hash(before.values())}
        old_head = source.execute("SELECT sequence,advancing_steps,hash FROM world_head").fetchone()
        new_head = target.execute("SELECT sequence,advancing_steps,hash FROM world_head").fetchone()
        old_sequence, old_steps = (int.from_bytes(value, "big") for value in old_head[:2])
        sequence, steps = (int.from_bytes(value, "big") for value in new_head[:2])
        assert (old_sequence, old_steps) == (1579, 808)
        assert sequence > old_sequence and steps >= old_steps, "No actual continued ledger."
        transition = target.execute(
            "SELECT kind,advancing_steps,before_hash,after_hash,cause_id FROM world_commits WHERE sequence=?",
            ((old_sequence + 1).to_bytes(8, "big"),)).fetchone()
        assert transition is not None and transition[0] == 3, "First appended entry is not explicit migration."
        assert transition[1] == old_head[1] and transition[2] == old_head[2], "Migration changed time or source boundary."
        later_archives = target.execute(
            "SELECT count(*) FROM world_material_care_segments WHERE publication_sequence>?",
            ((old_sequence + 1).to_bytes(8, "big"),)).fetchone()[0]
        if require_further_cut:
            assert later_archives > 0, "No archive published after the migration entry."
        evidence["archivesPublishedAfterMigration"] = later_archives
        evidence["transition"] = {"sequence": old_sequence + 1, "advancingSteps": old_steps,
                                  "causeId": transition[4].hex(), "afterHash": transition[3].hex()}
        evidence["targetHead"] = {"sequence": sequence, "advancingSteps": steps, "hash": new_head[2].hex()}
    finally:
        source.close()
        target.close()
    assert sha256(source_path) == source_hash and sha256(target_path) == target_hash, "Read audit changed file bytes."
    return {"sourceSha256": source_hash, "targetSha256": target_hash, "exactPrefix": evidence,
            "semanticReplay": "Requires the separate Core/Host qualification; SQL equality alone is insufficient."}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=lambda value: Path(value).resolve())
    parser.add_argument("target", type=lambda value: Path(value).resolve())
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--transition-only", action="store_true")
    arguments = parser.parse_args()
    report = {"accepted": False}
    try:
        report.update(audit(arguments.source, arguments.target, not arguments.transition_only))
        report["accepted"] = True
    except Exception as error:
        report["error"] = repr(error)
    with arguments.output.open("x") as output:
        json.dump(report, output, indent=2)
        output.write("\n")
    print(json.dumps({key: value for key, value in report.items() if key != "exactPrefix"}, indent=2))
    raise SystemExit(0 if report["accepted"] else 1)
