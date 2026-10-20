"""Exercise native client settings: change them through the real controls, then restore them in a second process."""
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
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    output = args.output.resolve()
    if output.exists():
        raise SystemExit("Never replace an earlier result.")
    work = output.parent / ("native-settings-" + uuid.uuid4().hex)
    work.mkdir(parents=True)
    save, settings, probe = work / "world.sqlite", work / "settings.cfg", work / "settings.json"
    sdk = Path(args.dotnet).resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get("PATH", ""))
    report = dict(startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope="Native settings key, controls, suppressed walking, saved file and second-process restoration",
                  status="running", runs=[])
    try:
        for stage in ("change", "reopen"):
            command = [str(Path(args.godot).resolve()), "--headless", "--path", str(root / "client"), "--",
                       "--settings-probe", str(save), str(settings), str(probe), stage]
            result = subprocess.run(command, cwd=root, env=env, capture_output=True, text=True, timeout=60)
            report["runs"].append(dict(stage=stage, command=command, exitCode=result.returncode,
                                       stdout=result.stdout, stderr=result.stderr))
            combined = result.stdout + result.stderr
            if result.returncode or "ERROR:" in combined or "SCRIPT ERROR:" in combined:
                raise RuntimeError(f"The native {stage} probe failed: " + combined[-2000:])
            observed = json.loads((probe if stage == "change" else probe.with_suffix(".reopen.json")).read_text())
            if observed["Status"] != "passed":
                raise RuntimeError(f"The native {stage} probe reported {observed['Status']}: {observed.get('Failure')}")
            report[stage] = observed
        report["artifactSha256"] = {str(p.relative_to(output.parent)): hashlib.sha256(p.read_bytes()).hexdigest()
                                    for p in sorted(work.rglob("*")) if p.is_file()}
        report["status"] = "passed"
    except Exception as error:
        report["status"] = "failed"
        report["failure"] = str(error)
    finally:
        report["finishedUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2) + "\n")
    print("native settings: " + report["status"], flush=True)
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
