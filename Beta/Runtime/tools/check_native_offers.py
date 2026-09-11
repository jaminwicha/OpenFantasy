#!/usr/bin/env python3
"""Qualify target selection, pack inspection, action previews and actual gathering/crafting/dialogue."""

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
    parser.add_argument("--rendered", action="store_true", help="Use a separate rendered test window and retain screenshots")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix="native-offers-", dir=output.parent))
    scene_report = work / "offers.json"
    sdk = Path(args.dotnet).resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk),
               PATH=str(sdk) + os.pathsep + os.environ.get("PATH", ""))
    report = {
        "startedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "scope": "Native target selection, pack inspection/time/input, action previews and authoritative gathering/crafting/dialogue",
        "limitations": "Injected events do not establish physical controller, OS focus switching or visual acceptance.",
        "status": "running", "rendered": args.rendered,
    }
    command = [str(Path(args.godot).resolve())]
    if not args.rendered:
        command.append("--headless")
    command += ["--path", str(root / "client"), "--", "--farm-action-offers", str(work / "world.sqlite"), str(scene_report)]
    try:
        result = subprocess.run(command, cwd=root, env=env, capture_output=True, text=True, timeout=80)
        report["run"] = {"command": command, "exitCode": result.returncode,
                         "stdout": result.stdout, "stderr": result.stderr}
        if result.returncode or "ERROR:" in result.stdout + result.stderr:
            raise RuntimeError("The native action-offer process failed; inspect its retained output.")
        observed = json.loads(scene_report.read_text())
        if (observed.get("Kind") != "native-action-offers-v1" or observed.get("Status") != "passed"
                or not observed.get("Runtime") or len(observed.get("Checks", [])) < 12):
            raise RuntimeError("The native action-offer result is incomplete.")
        if args.rendered:
            pictures = list(work.glob("*.png"))
            if len(pictures) < 12 or observed.get("DisplayDriver") == "headless":
                raise RuntimeError("The rendered action-offer evidence is incomplete.")
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
    print("native action offers: " + report["status"], flush=True)
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
