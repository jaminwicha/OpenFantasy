"""Exercise gathering, a preserved-copy upgrade, crafting and continuation in three native processes."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
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
    work = output.parent / ("native-production-" + uuid.uuid4().hex)
    work.mkdir(parents=True)
    source, destination, probe = work / "gathered.sqlite", work / "production.sqlite", work / "production.json"
    sdk = Path(args.dotnet).resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get("PATH", ""))
    report = dict(startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope="Native named inputs, gathered save, preserved-copy profile upgrade, two exact crafts and process recovery",
                  rendered=args.rendered, status="running", runs=[])
    try:
        source_hash = None
        for mode in ("prepare", "upgrade", "reopen"):
            command = [str(Path(args.godot).resolve())]
            if not args.rendered:
                command.append("--headless")
            command += ["--path", str(root / "client"), "--", "--farm-production", str(source), str(destination), str(probe), mode]
            result = subprocess.run(command, cwd=root, env=env, capture_output=True, text=True, timeout=110)
            report["runs"].append(dict(mode=mode, command=command, exitCode=result.returncode,
                                       stdout=result.stdout, stderr=result.stderr))
            if result.returncode or "ERROR:" in result.stdout + result.stderr:
                raise RuntimeError("Native production " + mode + " failed: " + result.stdout + result.stderr)
            observed = json.loads(probe.with_suffix("." + mode + ".json").read_text())
            if observed.get("Status") != "passed" or observed.get("Mode") != mode or not observed.get("Runtime") or observed.get("FocusRequests") != 0:
                raise RuntimeError("Incomplete native result")
            report[mode] = observed
            if mode == "prepare":
                source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
            elif hashlib.sha256(source.read_bytes()).hexdigest() != source_hash:
                raise RuntimeError("The retained gathered save was modified by production migration/continuation")
        prepared = json.loads(report["prepare"]["Final"])
        upgraded_before = json.loads(report["upgrade"]["Initial"])
        if prepared["Retained"] != upgraded_before["Retained"]:
            raise RuntimeError("Upgrade changed the retained public world")
        if report["upgrade"]["Final"] != report["reopen"]["Initial"]:
            raise RuntimeError("Crafted state failed exact process recovery")
        for mode, quantity, revision in (("prepare", 12, None), ("upgrade", 12, 1), ("reopen", 14, 2)):
            final = json.loads(report[mode]["Final"])
            if final["Retained"]["Quantity"] != quantity or final["CraftingRevision"] != revision:
                raise RuntimeError("Unexpected actual counted inventory/production revision at " + mode)
        if args.rendered:
            for mode in ("prepare", "upgrade", "reopen"):
                if not probe.with_suffix("." + mode + ".png").is_file():
                    raise RuntimeError("Missing native capture: " + mode)
        report["retainedSourceSha256"] = source_hash
        report["artifactSha256"] = {str(path.relative_to(output.parent)): hashlib.sha256(path.read_bytes()).hexdigest()
                                     for path in sorted(work.iterdir()) if path.is_file()}
        report["status"] = "passed"
    except Exception as error:
        report["status"] = "failed"
        report["failure"] = str(error)
    finally:
        report["finishedUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2) + "\n")
    print("native production: " + report["status"], flush=True)
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
