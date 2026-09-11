#!/usr/bin/env python3
"""Run the retained input adapter against a fresh world in a separate Godot process."""

import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", required=True)
    parser.add_argument("--godot", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix="native-input-", dir=output.parent))
    scene_report = work / "input.json"
    sdk = Path(args.dotnet).resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk),
               PATH=str(sdk) + os.pathsep + os.environ.get("PATH", ""))
    report = {
        "startedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "scope": "Native headless input events and focus handlers against real authoritative movement/dialogue",
        "limitations": "Injected events do not establish physical controller, OS focus switching or visual acceptance.",
        "status": "running",
    }
    command = [str(Path(args.godot).resolve()), "--headless", "--path", str(root / "client"),
               "--", "--farm-input", str(work / "world.sqlite"), str(scene_report)]
    try:
        result = subprocess.run(command, cwd=root, env=env, capture_output=True, text=True, timeout=60)
        report["run"] = {"command": command, "exitCode": result.returncode,
                         "stdout": result.stdout, "stderr": result.stderr}
        if result.returncode or "ERROR:" in result.stdout + result.stderr:
            raise RuntimeError("The native input process failed; inspect its retained output.")
        observed = json.loads(scene_report.read_text())
        if (observed.get("Kind") != "native-input-focus-v1" or observed.get("Status") != "passed"
                or not observed.get("Runtime") or len(observed.get("Checks", [])) < 7):
            raise RuntimeError("The native input result is incomplete.")
        report["observed"] = observed
        report["status"] = "passed"
    except Exception as error:
        report["status"] = "failed"
        report["failure"] = str(error)
    finally:
        report["artifactSha256"] = {str(p.relative_to(output.parent)): hashlib.sha256(p.read_bytes()).hexdigest()
                                    for p in sorted(work.iterdir()) if p.is_file()}
        report["finishedUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2) + "\n")
    print("native input: " + report["status"], flush=True)
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
