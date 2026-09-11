"""Qualify calendar-driven native lighting with explicitly dated disposable source-farm fixtures."""
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
    work = output.parent / ("native-daylight-" + uuid.uuid4().hex)
    work.mkdir(parents=True)
    sdk = Path(args.dotnet).resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get("PATH", ""))
    report = dict(startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(), status="running",
                  scope="Explicit-calendar fixtures, paused light, actual dusk/midnight steps and exact reopened appearance",
                  rendered=args.rendered, runs=[], observations={})
    anchors = {"dawn": (19_800_000, [.82, .72, .72, 1]), "day": (43_200_000, [1, 1, 1, 1]),
               "dusk": (66_600_000, [.81, .72, .76, 1]), "night": (82_800_000, [.50, .60, .78, 1]),
               "rollover": (86_391_000, [.50, .60, .78, 1])}
    try:
        for phase in [*anchors, "reopen"]:
            database = work / (("rollover" if phase == "reopen" else phase) + ".sqlite")
            observed_path = work / (phase + ".json")
            if phase == "reopen":
                saved_hash = hashlib.sha256(database.read_bytes()).hexdigest()
            command = [str(Path(args.godot).resolve())]
            if not args.rendered:
                command.append("--headless")
            command += ["--path", str(root / "client"), "--", "--farm-daylight", str(database), str(observed_path), phase]
            result = subprocess.run(command, cwd=root, env=env, capture_output=True, text=True, timeout=30)
            report["runs"].append(dict(phase=phase, command=command, exitCode=result.returncode,
                                       stdout=result.stdout, stderr=result.stderr))
            if observed_path.exists():
                report["observations"][phase] = json.loads(observed_path.read_text())
            if result.returncode or "ERROR:" in result.stdout + result.stderr:
                raise RuntimeError("Native daylight failed at " + phase)
            observed = report["observations"][phase]
            if (observed.get("Status") != "passed" or observed.get("Phase") != phase
                    or not observed.get("Runtime") or len(observed.get("Checks", [])) < 3
                    or observed.get("NominalDayMilliseconds") != 2_400_000):
                raise RuntimeError("Incomplete daylight evidence at " + phase)
            if phase in anchors:
                milliseconds, color = anchors[phase]
                if (observed["InitialCalendar"] != dict(Days=0, MillisecondsWithinDay=milliseconds)
                        or any(abs(a-b) > .00001 for a, b in zip(observed["InitialColor"], color))):
                    raise RuntimeError("The actual native light differs from its declared fixture anchor: " + phase)
            else:
                original = report["observations"]["rollover"]
                if any(observed[key] != original[key] for key in ("FinalWorldView", "FinalCalendar", "FinalColor")):
                    raise RuntimeError("Reopening changed the paused world or its light")
                if saved_hash != hashlib.sha256(database.read_bytes()).hexdigest():
                    raise RuntimeError("Reopening the paused fixture changed its save bytes")
                report["reopenedSaveSha256"] = saved_hash
            if args.rendered:
                stages = (("initial", "advanced") if phase in ("dusk", "rollover") else
                          ("initial", "target", "pack") if phase == "night" else ("initial",))
                for stage in stages:
                    if not observed_path.with_suffix("." + stage + ".png").is_file():
                        raise RuntimeError("Missing rendered daylight capture")
        report["status"] = "passed"
    except Exception as error:
        report["status"], report["failure"] = "failed", str(error)
    finally:
        report["artifactSha256"] = {str(p.relative_to(output.parent)): hashlib.sha256(p.read_bytes()).hexdigest()
                                   for p in sorted(work.iterdir()) if p.is_file()}
        report["finishedUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2) + "\n")
    print("native daylight: " + report["status"], flush=True)
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
