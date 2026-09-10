"""Qualify actual native farm shutdown during startup, without a rendering claim."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import platform
import subprocess
import sys
import uuid


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", type=Path, required=True)
    parser.add_argument("--godot", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--repetitions", type=int, default=2, choices=range(1, 5))
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    dotnet, godot = args.dotnet.resolve(), args.godot.resolve()
    destination = args.output.resolve()
    destination.parent.mkdir(parents=True, exist_ok=True)
    runs = destination.parent / (destination.stem + "-runs") / uuid.uuid4().hex
    runs.mkdir(parents=True)
    env = dict(os.environ, DOTNET_ROOT=str(dotnet.parent), DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1")
    env["PATH"] = str(dotnet.parent) + os.pathsep + env.get("PATH", "")
    report = dict(status="running", startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope="Native headless startup shutdown and exact fresh-process shared-service reopen",
                  platform=platform.platform(), runsDirectory=str(runs), repetitions=args.repetitions,
                  dotnetRoot=env["DOTNET_ROOT"], pathPrefix=str(dotnet.parent), steps=[], observations=[])

    def run(name, command):
        argv = [str(value) for value in command]
        try:
            result = subprocess.run(argv, cwd=root, env=env, text=True, capture_output=True, timeout=40)
        except subprocess.TimeoutExpired as error:
            def as_text(value):
                return value.decode(errors="replace") if isinstance(value, bytes) else value or ""
            report["steps"].append(dict(name=name, command=argv, timeout=True,
                                        stdout=as_text(error.stdout), stderr=as_text(error.stderr)))
            raise RuntimeError("Native lifecycle process timed out: " + name) from error
        report["steps"].append(dict(name=name, command=argv, exitCode=result.returncode,
                                    stdout=result.stdout, stderr=result.stderr))
        output = result.stdout + result.stderr
        if result.returncode or any(token in output for token in ("ERROR:", "SCRIPT ERROR:", "Unhandled exception")):
            raise RuntimeError("Native lifecycle process failed: " + name + "\n" + output)
        print(name + ": PASS", flush=True)
        return output.strip()

    def native(name, save, result_path, mode):
        run(name, [godot, "--headless", "--path", root / "client", "--", "--farm-lifecycle", save, result_path, mode])
        value = json.loads(result_path.read_text())
        if value.get("Kind") != "native-farm-lifecycle-v1" or value.get("Mode") != mode:
            raise RuntimeError("Native lifecycle report has the wrong identity: " + name)
        if value.get("DisplayDriver") != "headless" or not value.get("Runtime"):
            raise RuntimeError("Native lifecycle report has no actual runtime/display evidence: " + name)
        expected_status = "initiated" if mode == "close-opening" else "passed"
        if value.get("Status") != expected_status:
            raise RuntimeError("Native lifecycle stage did not complete correctly: " + name)
        if mode != "inspect" and value.get("ActionBeforePublicReadiness") is not True:
            raise RuntimeError("Lifecycle action did not occur before public readiness: " + name)
        if mode != "close-opening":
            observed = value["Observation"]
            if (observed["Revision"], observed["AdvancingSteps"], observed["PlayerWood"], observed["ReadingHold"]) != (0, 0, 8, False):
                raise RuntimeError("Lifecycle inspection changed the admitted startup state: " + name)
            if observed["LockingMode"] != "exclusive" or len(observed["StateHash"]) != 64:
                raise RuntimeError("Lifecycle inspection did not establish exclusive exact-state reopening: " + name)
            report["observations"].append(dict(name=name, reportPath=str(result_path), **observed))
        return value

    try:
        pins = json.loads((root / "toolchain.json").read_text())
        if run("sdk-version", [dotnet, "--version"]) != pins["sdk"]:
            raise RuntimeError("The SDK does not match its exact pin")
        if run("editor-version", [godot, "--version"]) != pins["godot"]:
            raise RuntimeError("The .NET editor does not match its exact pin")
        for index in range(args.repetitions):
            for mode in ("remove-opening", "close-opening"):
                prefix = str(index + 1) + "-" + mode
                save = runs / (prefix + ".sqlite3")
                result_path = runs / (prefix + ".json")
                native(prefix, save, result_path, mode)
                # A separate fresh process checks the final save even when the removal probe inspected it already.
                native(prefix + "-reopen", save, runs / (prefix + "-reopen.json"), "inspect")
        if len({value["StateHash"] for value in report["observations"]}) != 1:
            raise RuntimeError("Startup lifecycle modes did not retain the same exact admitted genesis")
        report["status"] = "passed"
    except Exception as error:
        report["status"] = "failed"
        report["failure"] = str(error)
        print(str(error), file=sys.stderr)
    finally:
        report["finishedUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        report["artifactSha256"] = {str(path.relative_to(runs)): hashlib.sha256(path.read_bytes()).hexdigest()
                                    for path in sorted(runs.rglob("*")) if path.is_file()}
        destination.write_text(json.dumps(report, indent=2) + "\n")
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    sys.exit(main())
