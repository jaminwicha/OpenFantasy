"""Exercise real native gathering inputs, paused recovery and continuing resource regrowth."""
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
    work = output.parent / ("native-gathering-" + uuid.uuid4().hex)
    work.mkdir(parents=True)
    save, probe_report = work / "world.sqlite", work / "gathering.json"
    sdk = Path(args.dotnet).resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get("PATH", ""))
    report = dict(startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope="Named native gather input, actual finite toolkit and yield, depletion, pause, process recovery and regrowth",
                  rendered=args.rendered, status="running", runs=[])
    try:
        for stage in ("create", "reopen"):
            command = [str(Path(args.godot).resolve())]
            if not args.rendered:
                command.append("--headless")
            command += ["--path", str(root / "client"), "--", "--farm-gathering", str(save), str(probe_report), stage]
            result = subprocess.run(command, cwd=root, env=env, capture_output=True, text=True, timeout=125)
            report["runs"].append(dict(stage=stage, command=command, exitCode=result.returncode,
                                       stdout=result.stdout, stderr=result.stderr))
            if result.returncode or "ERROR:" in result.stdout + result.stderr:
                raise RuntimeError("Native gathering " + stage + " failed: " + result.stdout + result.stderr)
            observed = json.loads((probe_report if stage == "create" else probe_report.with_suffix(".reopen.json")).read_text())
            if observed.get("Status") != "passed" or observed.get("Stage") != stage or not observed.get("Runtime"):
                raise RuntimeError("Incomplete native result")
            report[stage] = observed
        if report["create"]["Final"] != report["reopen"]["Initial"]:
            raise RuntimeError("Saved work projection did not restore exactly")
        first = json.loads(report["create"]["Initial"])
        harvested = json.loads(report["create"]["Final"])
        regrown = json.loads(report["reopen"]["Final"])
        if (first["Quantity"], harvested["Quantity"], regrown["Quantity"]) != (10, 19, 19):
            raise RuntimeError("Finite gathering or regrowth inventory accounting failed")
        if args.rendered:
            for suffix in ("oak-stump", "gathered", "regrown"):
                if not probe_report.with_suffix("." + suffix + ".png").is_file():
                    raise RuntimeError("Missing actual native capture: " + suffix)
        report["artifactSha256"] = {str(p.relative_to(output.parent)): hashlib.sha256(p.read_bytes()).hexdigest()
                                     for p in sorted(work.iterdir()) if p.is_file()}
        report["status"] = "passed"
    except Exception as error:
        report["status"] = "failed"
        report["failure"] = str(error)
    finally:
        report["finishedUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2) + "\n")
    print("native gathering: " + report["status"], flush=True)
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
