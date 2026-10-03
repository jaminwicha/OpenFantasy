"""Continue a closed, earned adventure opening through native trade/danger/care/return and exact reopening.

The source save is copied and byte-pinned, never prepared or changed. This is a bounded keyboard
continuation, not the old road-threat aggregate or human/device/art acceptance.
"""
import argparse
import base64
from contextlib import closing
import hashlib
import json
import os
from pathlib import Path
import shutil
import sqlite3
import tempfile
import time
import traceback

from check_native_road_threat import digest, require, run

REQUIRED_IMAGES = {"retained-opening", "buy-review", "marla-bought", "road", "warning", "engaged",
                   "hit", "retreat-inspected", "retreating", "disengaged", "care-inspection", "treated",
                   "detoured", "gearford", "sell-review", "sold", "returned-work", "final"}


def verify_pins(pins):
    require(pins and all(Path(p).is_file() and digest(p) == h for p, h in pins.items()),
            "Missing or changed execution dependency.")


def closed_database(path):
    require(path.is_file() and not any(Path(str(path) + s).exists() for s in ("-wal", "-shm")),
            "Evidence database must be closed.")
    with closing(sqlite3.connect(f"file:{path}?mode=ro&immutable=1", uri=True)) as db:
        schema = db.execute("select type,name,tbl_name,sql from sqlite_schema order by type,name").fetchall()
        tables = [row[1] for row in schema if row[0] == "table"]
        rows = {name: sorted(db.execute('select * from "' + name.replace('"', '""') + '"').fetchall()) for name in tables}
        return {"schema": schema, "version": db.execute("pragma user_version").fetchone(), "tables": rows}


def verify_paused_database(before, after):
    """A live opening fences a new host generation, but this solo paused fixture cannot change other rows."""
    require(before["schema"] == after["schema"] and before["version"] == after["version"], "Paused opening changed the schema.")
    original, current = before["tables"], after["tables"]
    require(original.keys() == current.keys(), "Paused opening changed the table set.")
    for name in original:
        if name != "controller_recovery_segment":
            require(original[name] == current[name], "Paused opening changed retained rows: " + name)
    require(original["controller_allowances"] == [], "This solo reopening proof requires empty allowance rows.")
    a, b = original["controller_recovery_segment"], current["controller_recovery_segment"]
    require(len(a) == len(b) == 1 and a[0][0] == b[0][0] == 1, "Invalid recovery fence row.")
    first, last = int.from_bytes(a[0][1], "big"), int.from_bytes(b[0][1], "big")
    require(last == first + 1, "Reopening must advance the host fence exactly once.")
    activation = original["controller_recovery_activation"]
    require(len(activation) == 1, "Missing recovery activation.")
    _, policy, provenance, provenance_hash = activation[0]
    require(hashlib.sha256(provenance).digest() == provenance_hash, "Recovery provenance checksum changed.")
    for row in (a[0], b[0]):
        require(len(row[1]) == 8 and hashlib.sha256(policy + provenance + row[1] + b"\x00").digest() == row[2],
                "Recovery fence checksum does not authenticate its exact empty-allowance generation.")
    return {"generationBefore": first, "generationAfter": last,
            "unchangedRows": {name: len(rows) for name, rows in original.items() if name != "controller_recovery_segment"}}


def retained_rows(path):
    # Callers own closed databases. Immutable reads cannot create WAL/SHM beside retained evidence.
    with closing(sqlite3.connect(f"file:{path}?mode=ro&immutable=1", uri=True)) as db:
        return {table: db.execute(f"select * from {table} order by 1,2").fetchall()
                for table in ("world_genesis", "world_commits", "world_command_outcomes", "world_occupied_identity")}


def authenticate(save, observed, before):
    after = retained_rows(save)
    require(after["world_genesis"] == before["world_genesis"], "Original canonical genesis changed.")
    for table, rows in before.items():
        require(set(rows) <= set(after[table]), "Original historical rows changed: " + table)
    submissions = observed["Adventure"]["Submissions"]
    require(len(submissions) >= 6 and len({s["CommandId"] for s in submissions}) == len(submissions),
            "Missing or repeated native commands.")
    require({s["Action"] for s in submissions} >= {"Strike", "Retreat", "UseRestorative", "ConfirmTrade"},
            "Native submission evidence misses a required action.")
    with closing(sqlite3.connect(f"file:{save}?mode=ro&immutable=1", uri=True)) as db:
        for s in submissions:
            require(not s["IsRetry"] and s["Receipt"] and s["Receipt"]["Rejection"] == "None",
                    "Native action failed or retried: " + s["Action"])
            rows = db.execute("select receipt from world_command_outcomes where command=?",
                              (base64.b64decode(s["Command"]),)).fetchall()
            require(rows == [(base64.b64decode(s["Receipt"]["Bytes"]),)], "Saved exact command/receipt mismatch.")
    return {"authenticatedSubmissions": len(submissions), "preservedRows": {k: len(v) for k, v in before.items()}}


def verify_journey(o):
    require(o["Kind"] == "native-adventure-journey-v1" and o["Mode"] == "keyboard" and o["Status"] == "passed", str(o.get("Failure")))
    require(o["OpeningSeconds"] < 30 and o["ProbeMilliseconds"] < 690000 and o["Paused"], "Opening/journey bound or pause failed.")
    require(o["Unobserved"]["Rows"] == 0 and "Danger nearby" in o["Warning"], "Missing pre-contact warning.")
    sight = o["WarningObservation"]
    require(sight["ChiefSpriteVisible"] and sight["PlayerSpriteVisible"]
            and sight["ReachSubunits"] < sight["DistanceSubunits"] <= 768, "Warning visibility/range failed.")
    require(o["Engagement"]["EncounterCount"] == 1 and o["EncounterCount"] == 1, "Wrong encounter count.")
    require(o["Hit"]["After"] < o["Hit"]["Before"], "No real injury.")
    require([r["Stage"] for r in o["Openings"]] == ["inspect", "commit"]
            and all(r["CanExecute"] for r in o["Openings"])
            and o["Openings"][0]["Timing"] == o["Openings"][1]["Timing"], "Retreat capture changed or retried.")
    require(o["Record"]["Status"] == "Disengaged" and o["Record"]["SamplesTaken"] == len(o["Positions"]) - 1,
            "Retreat did not complete its observed route.")
    require(all(abs(b["X"] - a["X"]) + abs(b["Y"] - a["Y"]) == 32
                for a, b in zip(o["Positions"], o["Positions"][1:])), "Retreat motion skipped samples.")
    care, returned = o["Adventure"]["Care"], o["Adventure"]["Returned"]
    require(care["AfterHp"] == care["BeforeHp"] + care["ExpectedHealingHp"]
            and care["UnitsAfter"] == care["UnitsBefore"] - 1 and o["FinalHp"] == care["AfterHp"], "Finite care failed.")
    require(o["Purchase"]["LotId"] == o["Trade"]["LotId"] == o["Adventure"]["CargoLot"]
            and o["CarriedRations"] == 0 and o["Trade"]["BalanceAfter"] == o["Trade"]["BalanceBefore"] + o["Trade"]["Total"],
            "Cargo identity, sale or funds changed.")
    lots = returned["CraftedLots"]
    require(returned["CompletedProjects"] > 0 and sum(lot["Quantity"] for lot in lots) == 2
            and len({lot["LotId"] for lot in lots}) == len(lots),
            "Return lost original work or crafted supplies.")
    require(o["FinalFeet"] == {key: returned[key] for key in ("X", "Y", "Z")}
            and o["FinalBalance"] == returned["Balance"] == o["Trade"]["BalanceAfter"]
            and returned["Hp"] == o["FinalHp"], "Returned state disagrees with the final view.")
    require(not o["WindowFocusObserved"] and o["LogicalFocus"], "Background focus isolation failed.")
    require({Path(p).name for p in o["Images"]} >= {f"keyboard.{name}.png" for name in REQUIRED_IMAGES}
            and all(Path(p).is_file() for p in o["Images"]), "Rendered evidence missing.")
    require({c["Label"] for c in o["Captures"]} >= REQUIRED_IMAGES
            and all(c["Drawn"] and 0 <= c["Seconds"] < 5
                    and c["After"]["PostDrawEvents"] > c["Before"]["PostDrawEvents"] for c in o["Captures"]),
            "Rendered capture failed or exceeded its original five-second bound.")


def verify_reopen(reopened, observed):
    require(reopened["Kind"] == "native-adventure-journey-v1" and reopened["Mode"] == "reopen"
            and reopened["Status"] == "passed" and reopened["OpeningSeconds"] < 30 and reopened["Paused"]
            and reopened["Adventure"]["HistoryVerified"], "Reopening or original-history verification failed.")
    require(reopened["Adventure"]["PublicHash"] == observed["Adventure"]["PublicHash"], "Paused reopening changed the public state.")
    require(not reopened["WindowFocusObserved"] and reopened["LogicalFocus"]
            and reopened["Adventure"]["Submissions"] == [], "Reopening submitted input or lost background isolation.")
    require({Path(p).name for p in reopened["Images"]} >= {"reopen.reopen.png"}
            and all(Path(p).is_file() for p in reopened["Images"]), "Rendered reopening evidence missing.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-save", type=Path, required=True)
    parser.add_argument("--godot", type=Path, required=True)
    parser.add_argument("--dotnet", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    require(not args.output.exists(), "Never replace an earlier result.")
    runtime = Path(__file__).resolve().parents[1]
    client = runtime / "client"
    source = args.source_save.resolve()
    work = Path(tempfile.mkdtemp(prefix="openfantasy-adventure-journey-"))
    report = {"status": "running", "workDirectory": str(work), "sourceSave": str(source), "runs": []}
    started = time.monotonic()
    try:
        require(source.is_file() and not any(Path(str(source) + s).exists() for s in ("-wal", "-shm")), "Source must be a closed save.")
        source_hash = digest(source)
        report["sourceSha256"] = source_hash
        before = retained_rows(source)
        save = work / "adventure.sqlite"
        shutil.copy2(source, save)
        paths = [Path(__file__).resolve(), runtime / "tools/check_native_road_threat.py", client / "project.godot"]
        paths += [p for directory in (runtime / "src", client / "scripts", client / "scenes")
                  for p in directory.rglob("*") if p.is_file() and p.suffix in (".cs", ".csproj", ".tscn")
                  and not {"bin", "obj"}.intersection(p.parts)]
        paths += list((client / ".godot/mono/temp/bin/Debug").glob("OpenFantasy*.dll"))
        report["executionPins"] = {str(p): digest(p) for p in paths}
        launch = work / "background-client"
        launch.mkdir()
        for p in client.iterdir():
            if p.name != "project.godot": (launch / p.name).symlink_to(p, target_is_directory=p.is_dir())
        config = (client / "project.godot").read_text()
        require(config.count("[display]") == 1 and "window/size/no_focus" not in config, "Unexpected window configuration.")
        (launch / "project.godot").write_text(config.replace("[display]", "[display]\nwindow/size/no_focus=true", 1))
        env = dict(os.environ, DOTNET_ROOT=str(args.dotnet.resolve().parent))
        env["PATH"] = str(args.dotnet.resolve().parent) + os.pathsep + env.get("PATH", "")
        def probe(mode, limit):
            output = work / (mode + ".json")
            command = [str(args.godot.resolve()), "--path", str(launch), "--", "--adventure-journey", str(save), str(output), mode,
                       "--background-render"]
            run(command, runtime, env, work, mode, limit, report)
            return json.loads(output.read_text())
        observed = probe("keyboard", 720)
        verify_journey(observed)
        report["authentication"] = authenticate(save, observed, before)
        before_reopen = closed_database(save)
        shutil.copy2(save, work / "before-reopen.sqlite")
        report["beforeReopenSave"] = str(work / "before-reopen.sqlite")
        report["beforeReopenDatabaseSha256"] = digest(save)
        reopened = probe("reopen", 90)
        verify_reopen(reopened, observed)
        report["pausedReopening"] = verify_paused_database(before_reopen, closed_database(save))
        require(digest(source) == source_hash, "Source save changed.")
        verify_pins(report["executionPins"])
        report.update(status="passed", elapsedSeconds=time.monotonic()-started, finalDatabaseSha256=digest(save),
                      activeReport=str(work/"keyboard.json"), reopenReport=str(work/"reopen.json"))
    except Exception as error:
        report.update(status="failed", failure=str(error), traceback=traceback.format_exc())
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2)+"\n")
    print(json.dumps({k: report[k] for k in ("status", "workDirectory", "failure") if k in report}))
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
