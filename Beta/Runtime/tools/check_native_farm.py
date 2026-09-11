"""Exercise the actual Godot farm input/view adapter and reopen its shared durable world."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import sqlite3
import subprocess
import uuid


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", required=True)
    parser.add_argument("--godot", required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--rendered", action="store_true")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    output = args.output.resolve()
    work = output.parent / ("native-farm-" + uuid.uuid4().hex)
    work.mkdir(parents=True)
    database, scene_report = work / "world.sqlite", work / "walkthrough.json"
    sdk = Path(args.dotnet).resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get("PATH", ""))
    report = dict(startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope="Native local movement, addressed dialogue hold, finite handover and exact paused-view reopen",
                  rendered=args.rendered, status="running", runs=[])
    try:
        for stage in ("create", "reopen"):
            command = [str(Path(args.godot).resolve())]
            if not args.rendered:
                command.append("--headless")
            command += ["--path", str(root / "client"), "--", "--farm-walkthrough",
                        str(database), str(scene_report), stage]
            result = subprocess.run(command, cwd=root, env=env, capture_output=True, text=True, timeout=60)
            report["runs"].append(dict(stage=stage, command=command, exitCode=result.returncode,
                                       stdout=result.stdout, stderr=result.stderr))
            if result.returncode or "ERROR:" in result.stdout + result.stderr or "SCRIPT ERROR:" in result.stdout + result.stderr:
                raise RuntimeError("The native " + stage + " probe failed: " + result.stdout + result.stderr)
            path = scene_report if stage == "create" else scene_report.with_suffix(".reopen.json")
            observed = json.loads(path.read_text())
            if observed.get("Status") != "passed" or observed.get("Stage") != stage or not observed.get("Runtime"):
                raise RuntimeError("The native probe did not produce a complete result")
            report[stage] = observed
        if report["create"]["Final"] != report["reopen"]["Final"]:
            raise RuntimeError("The native saved projection changed across process restart")
        initial, final = [json.loads(report["create"][key]) for key in ("Initial", "Final")]
        if initial["Wood"] != 8 or final["Wood"] != 2 or final["Dialogue"] is not None:
            raise RuntimeError("The handover or conversation completion did not match its admitted action")
        if initial["Actors"] == final["Actors"]:
            raise RuntimeError("The movement adapter produced no committed presence changes")
        report["recordedHistory"] = check_recorded_history(root, args.dotnet, database, env)
        if args.rendered:
            for suffix in ("dialogue", "final"):
                if not scene_report.with_suffix("." + suffix + ".png").is_file():
                    raise RuntimeError("Missing native rendered capture: " + suffix)
        report["artifactSha256"] = {str(p.relative_to(output.parent)): hashlib.sha256(p.read_bytes()).hexdigest()
                                     for p in sorted(work.iterdir()) if p.is_file()}
        report["status"] = "passed"
    except Exception as error:
        report["status"] = "failed"
        report["failure"] = str(error)
    finally:
        report["finishedUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2) + "\n")
    print("native farm: " + report["status"], flush=True)
    return 0 if report["status"] == "passed" else 1


def check_recorded_history(root, dotnet, database, env):
    """Inspect the same closed native save through the operator; never alter a gameplay save."""
    before = hashlib.sha256(database.read_bytes()).hexdigest()
    connection = sqlite3.connect(database.as_uri() + "?mode=ro", uri=True)
    try:
        head_bytes, head_hash = connection.execute("SELECT sequence,hash FROM world_head").fetchone()
        head = int.from_bytes(head_bytes, "big")
        genesis_hash = connection.execute("SELECT hash FROM world_genesis").fetchone()[0].hex()
        first_hash = connection.execute("SELECT after_hash FROM world_commits ORDER BY sequence LIMIT 1").fetchone()[0].hex()
    finally:
        connection.close()
    checks = []
    host = root / "src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll"
    for revision, expected_hash in ((head, head_hash.hex()), (0, genesis_hash), (1, first_hash), (head, head_hash.hex())):
        command = [str(dotnet), str(host), "--inspect-recorded-world", str(database), str(revision)]
        result = subprocess.run(command, cwd=root, env=env, capture_output=True, text=True, timeout=60)
        if result.returncode:
            raise RuntimeError("Recorded history inspection failed: " + result.stdout + result.stderr)
        observed = json.loads(result.stdout)
        checks.append(observed)
        if (observed.get("Contract") != "recorded-world-state-v1"
                or observed.get("RecordedRevision") != revision or observed.get("StateHash") != expected_hash
                or observed.get("SourceHeadRevision") != head or observed.get("SourceHeadHash") != head_hash.hex()):
            raise RuntimeError("Recorded history disagreed with the exact committed boundary")
    if checks[0] != checks[-1]:
        raise RuntimeError("Reverse historical inspection changed the prepared state")
    for invalid in (str(head + 1), "-1", "18446744073709551616"):
        result = subprocess.run([str(dotnet), str(host), "--inspect-recorded-world", str(database), invalid],
                                cwd=root, env=env, capture_output=True, text=True, timeout=60)
        if result.returncode != 1 or not result.stdout.startswith("Inspection failed:"):
            raise RuntimeError("Invalid historical boundary was not rejected by the operator")
    after = hashlib.sha256(database.read_bytes()).hexdigest()
    if before != after:
        raise RuntimeError("Historical inspection changed the native save bytes")
    return dict(status="passed", checks=checks, invalidBoundariesRejected=3, databaseSha256Before=before,
                databaseSha256After=after)


if __name__ == "__main__":
    raise SystemExit(main())
