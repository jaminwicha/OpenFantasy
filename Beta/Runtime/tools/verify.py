"""Build and exercise the retained development foundation, not a production game."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default=shutil.which("dotnet"))
    parser.add_argument("--godot", required=True, help="Exact .NET editor executable")
    parser.add_argument("--forbidden-token", action="append", default=[])
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    if not args.dotnet:
        parser.error("Specify an installed SDK executable with --dotnet")
    root = Path(__file__).resolve().parents[1]
    artifacts = root / "artifacts"
    artifacts.mkdir(exist_ok=True)
    test_results = artifacts / "tests" / uuid.uuid4().hex
    destination = args.output or artifacts / "verification.json"
    destination.parent.mkdir(parents=True, exist_ok=True)
    dotnet = Path(args.dotnet).resolve()
    godot = Path(args.godot).resolve()
    env = dict(os.environ, DOTNET_ROOT=str(dotnet.parent),
               DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1")
    env["PATH"] = str(dotnet.parent) + os.pathsep + env.get("PATH", "")
    env["OPENFANTASY_WATERSHED_EVIDENCE"] = str(test_results / "watershed-fixture")
    env["OPENFANTASY_WATERSHED_NETWORK_EVIDENCE"] = str(test_results / "watershed-network-fixture")
    env["OPENFANTASY_WATERSHED_NETWORK_WORLD_EVIDENCE"] = str(test_results / "watershed-network-world-fixture")
    env["OPENFANTASY_WATERSHED_TERRAIN_EVIDENCE"] = str(test_results / "watershed-terrain-fixture")
    env["OPENFANTASY_TERRAIN_MATERIAL_EVIDENCE"] = str(test_results / "terrain-material-fixture")
    env["OPENFANTASY_RUNOFF_EVIDENCE"] = str(test_results / "runoff-fixture")
    env["OPENFANTASY_RUNOFF_CAPACITY_EVIDENCE"] = str(test_results / "runoff-capacity-fixture")
    env["OPENFANTASY_EXCAVATION_EVIDENCE"] = str(test_results / "excavation-fixture")
    env["OPENFANTASY_CONSTRUCTED_SUPPORT_EVIDENCE"] = str(test_results / "constructed-support-fixture")
    env["OPENFANTASY_CONSTRUCTION_PHYSICAL_EVIDENCE"] = str(test_results / "construction-physical-fixture")
    env["OPENFANTASY_COLLAPSE_MOTION_EVIDENCE"] = str(test_results / "collapse-motion-fixture")
    env["OPENFANTASY_STRUCTURAL_LOAD_EVIDENCE"] = str(test_results / "structural-load-fixture")
    env["OPENFANTASY_STAIR_NAVIGATION_EVIDENCE"] = str(test_results / "stair-navigation-fixture")
    env["OPENFANTASY_RESERVOIR_BOUNDARY_EVIDENCE"] = str(test_results / "reservoir-boundary-fixture")
    env["OPENFANTASY_RESERVOIR_TRANSPORT_EVIDENCE"] = str(test_results / "reservoir-transport-fixture")
    env["OPENFANTASY_RUNOFF_COMMAND_EVIDENCE"] = str(test_results / "runoff-command-fixture")
    env["OPENFANTASY_RESERVOIR_WORLD_EVIDENCE"] = str(test_results / "reservoir-world-fixture")
    env["OPENFANTASY_RESERVOIR_EVIDENCE"] = str(test_results / "reservoir-fixture")
    env["OPENFANTASY_MATERIAL_PRODUCTION_EVIDENCE"] = str(test_results / "material-production-fixture")
    env["OPENFANTASY_MATERIAL_REGISTRY_EVIDENCE"] = str(test_results / "material-registry-fixture")
    report = dict(startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope="Local core/hosting tests, durable counted authority and Godot binding smoke",
                  status="running", namingScanRequested=bool(args.forbidden_token), steps=[])

    def run(name, command, timeout=180):
        try:
            result = subprocess.run([str(x) for x in command], cwd=root, env=env,
                                    capture_output=True, text=True, timeout=timeout)
        except subprocess.TimeoutExpired as error:
            def retained(value):
                return value.decode(errors="replace") if isinstance(value, bytes) else value or ""
            report["steps"].append(dict(name=name, command=[str(x) for x in command],
                exitCode=None, timedOut=True, timeoutSeconds=timeout,
                stdout=retained(error.stdout), stderr=retained(error.stderr)))
            raise
        report["steps"].append(dict(name=name, command=[str(x) for x in command],
                                    exitCode=result.returncode,
                                    stdout=result.stdout, stderr=result.stderr))
        print(name + ": " + ("PASS" if result.returncode == 0 else "FAIL"), flush=True)
        if result.returncode:
            raise RuntimeError(result.stdout + result.stderr)
        return result.stdout + result.stderr

    try:
        pins = json.loads((root / "toolchain.json").read_text())
        if run("sdk-version", [dotnet, "--version"]).strip() != pins["sdk"]:
            raise RuntimeError("The installed SDK does not match the exact pin")
        run("installed-runtimes", [dotnet, "--list-runtimes"])
        if run("editor-version", [godot, "--version"]).strip() != pins["godot"]:
            raise RuntimeError("The editor does not match the exact .NET pin")
        excluded = {".godot", "bin", "obj", "artifacts", "TestResults", "__pycache__"}
        sources = sorted(p for p in root.rglob("*") if p.is_file()
                         and not any(part in excluded for part in p.relative_to(root).parts))
        binary_files = []
        for path in sources:
            relative = str(path.relative_to(root))
            try:
                body = path.read_text(encoding="utf-8-sig")
            except UnicodeDecodeError:
                if path.suffix in {".cs", ".csproj", ".json", ".py", ".md", ".txt", ".tscn", ".godot"}:
                    raise RuntimeError("A required UTF-8 text input could not be decoded: " + relative)
                binary_files.append(relative)
                body = ""
            for token in args.forbidden_token:
                if token.casefold() in body.casefold() or token.casefold() in relative.casefold():
                    raise RuntimeError(f"Forbidden token in {path.relative_to(root)}")
        report["binaryInputsHashedWithoutTextScan"] = binary_files
        report["inputSha256"] = {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest()
                                  for p in sources}
        for fixture, generator in (("reference-v1", "reference"), ("commands-v1", "command"),
                                   ("packages-v1", "package"), ("custody-v1", "custody"),
                                   ("scheduling-v1", "scheduling"), ("history-v1", "history"),
                                   ("custody-invalid-v1", "custody_invalid"), ("exposure-v1", "exposure"),
                                   ("production-v1", "production"), ("actors-v1", "actor"),
                                   ("observations-v1", "observation"), ("language-v1", "language"),
                                   ("authority-v1", "authority"), ("terrain-v1", "terrain"), ("watershed-v1", "watershed"),
                                   ("watershed-network-v1", "watershed_network"),
                                   ("watershed-terrain-v1", "watershed_terrain"),
                                   ("world-actions-v1", "world_action"), ("world-receipts-v1", "world_receipt"),
                                   ("gathering-v1", "gathering"), ("production-ledger-v1", "production_ledger"),
                                   ("world-production-v1", "world_production")):
            vector_file = root / "tests/OpenFantasy.Core.Tests/Fixtures" / (fixture + ".json")
            reference = run("independent-python-" + fixture, [sys.executable,
                            vector_file.with_name("generate_" + generator + "_vectors.py")])
            if json.loads(reference) != json.loads(vector_file.read_text()):
                raise RuntimeError("Independent vectors differ from frozen fixture: " + fixture)
        for directory, fixture, generator in (("Progression", "progression-v1", "progression"),
                                                ("Quests", "quest-v1", "quest"),
                                                ("Construction", "construction-v1", "construction")):
            vector_file = root / "tests/OpenFantasy.Core.Tests" / directory / (fixture + ".json")
            reference = run("independent-python-" + fixture, [sys.executable,
                            vector_file.with_name("generate_" + generator + "_vectors.py")])
            if json.loads(reference) != json.loads(vector_file.read_text()):
                raise RuntimeError("Independent vectors differ from frozen fixture: " + fixture)
        run("locked-restore", [dotnet, "restore", "OpenFantasy.sln", "--locked-mode"])
        run("debug-build", [dotnet, "build", "OpenFantasy.sln", "--no-restore", "-c", "Debug"])
        for project, label in (("OpenFantasy.Core.Tests", "core"), ("OpenFantasy.Hosting.Tests", "hosting")):
            run(label + "-tests", [dotnet, "test", "tests/" + project + "/" + project + ".csproj",
                                  "--no-build", "--no-restore", "-c", "Debug", "--logger",
                                  "trx;LogFileName=" + label + ".trx", "--results-directory", test_results])
            counters = ET.parse(test_results / (label + ".trx")).find(".//{*}Counters")
            if counters is None:
                raise RuntimeError("The test report has no result counters: " + label)
            counts = {key: int(value) for key, value in counters.attrib.items()}
            if (counts.get("executed", 0) <= 0 or counts.get("total") != counts["executed"]
                    or counts.get("passed") != counts["executed"]
                    or any(counts.get(key, 0) for key in ("failed", "error", "timeout", "aborted"))):
                raise RuntimeError("The test report contains missing, failed or unexecuted cases: " + label)
            report["testCounts" if label == "core" else "hostingTestCounts"] = counts
        host = run("host-foundation", [dotnet, "run", "--project", "src/OpenFantasy.Host",
                    "--no-build", "--no-restore", "--", "--inspect-foundation"])
        run("watershed-world-persistence", [sys.executable, root / "tools/check_watershed_world.py",
            "--dotnet", dotnet, "--fixture", test_results / "watershed-fixture",
            "--output", artifacts / "watershed-world.json"])
        run("watershed-network-world-persistence", [sys.executable, root / "tools/check_watershed_network_world.py",
            "--dotnet", dotnet, "--fixture", test_results / "watershed-network-world-fixture",
            "--output", artifacts / "watershed-network-world.json"])
        inspection_report = artifacts / "contract-inspections.json"
        run("reservoir-world-persistence", [sys.executable, root / "tools/check_watershed_network_world.py",
            "--dotnet", dotnet, "--fixture", test_results / "reservoir-world-fixture", "--profile", "reservoir",
            "--output", artifacts / "reservoir-world.json"])

        run("live-excavation-persistence", [sys.executable, root / "tools/check_live_excavation.py",
            "--dotnet", dotnet, "--fixture", test_results / "excavation-fixture",
            "--output", artifacts / "live-excavation.json"])

        run("construction-physical-persistence", [sys.executable, root / "tools/check_construction_physical.py",
            "--dotnet", dotnet, "--fixture", test_results / "construction-physical-fixture",
            "--output", artifacts / "construction-physical.json"])

        run("structural-load-planning", [sys.executable, root / "tools/check_structural_loads.py",
            "--dotnet", dotnet, "--fixture", test_results / "structural-load-fixture",
            "--output", artifacts / "structural-loads.json"])

        run("constructed-support-persistence", [sys.executable, root / "tools/check_constructed_support.py",
            "--dotnet", dotnet, "--fixture", test_results / "constructed-support-fixture",
            "--output", artifacts / "constructed-support.json"])
        run("stair-navigation-persistence", [sys.executable, root / "tools/check_constructed_support.py",
            "--dotnet", dotnet, "--fixture", test_results / "stair-navigation-fixture",
            "--output", artifacts / "stair-navigation.json"])

        run("reservoir-boundary-assessment", [sys.executable, root / "tools/check_reservoir_boundaries.py",
            "--dotnet", dotnet, "--fixture", test_results / "reservoir-boundary-fixture",
            "--output", artifacts / "reservoir-boundaries.json"])

        run("reservoir-transport-persistence", [sys.executable, root / "tools/check_watershed_network_world.py",
            "--dotnet", dotnet, "--fixture", test_results / "reservoir-transport-fixture", "--profile", "transport",
            "--output", artifacts / "reservoir-transport.json"])

        run("runoff-command-persistence", [sys.executable, root / "tools/check_watershed_network_world.py",
            "--dotnet", dotnet, "--fixture", test_results / "runoff-command-fixture", "--profile", "reservoir",
            "--output", artifacts / "runoff-commands.json"])

        run("watershed-network-inspections", [sys.executable, root / "tools/check_watershed_network.py",
            "--dotnet", dotnet, "--maximum-fixture", test_results / "watershed-network-fixture",
            "--output", artifacts / "watershed-network.json"])
        run("watershed-terrain-inspections", [sys.executable, root / "tools/check_watershed_terrain.py",
            "--dotnet", dotnet, "--maximum-fixture", test_results / "watershed-terrain-fixture",
            "--output", artifacts / "watershed-terrain.json"])
        run("terrain-material-inspections", [sys.executable, root / "tools/check_terrain_materials.py",
            "--dotnet", dotnet, "--fixture", test_results / "terrain-material-fixture",
            "--output", artifacts / "terrain-materials.json"])
        run("material-registry-inspections", [sys.executable, root / "tools/check_material_registry.py",
            "--dotnet", dotnet, "--fixture", test_results / "material-registry-fixture",
            "--output", artifacts / "material-registry.json"])
        run("runoff-inspections", [sys.executable, root / "tools/check_runoff_budgets.py",
            "--dotnet", dotnet, "--fixture", test_results / "runoff-fixture",
            "--output", artifacts / "runoff-budgets.json"])
        run("runoff-capacity-inspections", [sys.executable, root / "tools/check_runoff_capacity.py",
            "--dotnet", dotnet, "--fixture", test_results / "runoff-capacity-fixture",
            "--output", artifacts / "runoff-capacity.json"])
        run("reservoir-inspections", [sys.executable, root / "tools/check_liquid_reservoirs.py",
            "--dotnet", dotnet, "--fixture", test_results / "reservoir-fixture",
            "--output", artifacts / "liquid-reservoirs.json"])
        run("material-production-inspections", [sys.executable, root / "tools/check_material_production.py",
            "--dotnet", dotnet, "--fixture", test_results / "material-production-fixture",
            "--output", artifacts / "material-production.json"])
        run("host-contract-inspections", [sys.executable, root / "tools/check_contract_inspections.py",
            "--dotnet", dotnet, "--output", inspection_report])
        report["contractInspections"] = json.loads(inspection_report.read_text())
        # --import owns the wait-and-quit lifecycle; --quit separately requests exit after one iteration.
        imported = run("editor-import", [godot, "--headless", "--path", root / "client", "--import"])
        client = run("client-foundation", [godot, "--headless", "--path", root / "client", "--", "--smoke-test"], 60)

        def projection(output):
            values = [json.loads(line) for line in output.splitlines() if line.startswith('{"Contract":')]
            if len(values) != 1:
                raise RuntimeError("Expected exactly one foundation result")
            value = values[0]
            if not value.get("Runtime"):
                raise RuntimeError("Missing actual managed runtime observation")
            return value

        host_value, client_value = projection(host), projection(client)
        report["observedHost"], report["observedClient"] = host_value, client_value
        for field in ("Contract", "Entity", "Sample", "Day", "Millisecond"):
            if host_value[field] != client_value[field]:
                raise RuntimeError("Host/client disagreement in " + field)
        if (host_value["Day"], host_value["Millisecond"]) != (0, 0):
            raise RuntimeError("Historical midnight crossing failed")
        if any("ERROR:" in output or "SCRIPT ERROR:" in output for output in (imported, client)):
            raise RuntimeError("Godot reported an error despite its exit code")
        durable_report = artifacts / "durable-authority.json"
        run("durable-authority-qualification", [sys.executable, root / "tools/check_durable_authority.py",
            "--dotnet", dotnet, "--godot", godot, "--output", durable_report])
        report["durableAuthority"] = json.loads(durable_report.read_text())
        native_report = artifacts / "native-farm.json"
        run("native-farm-walkthrough", [sys.executable, root / "tools/check_native_farm.py",
            "--dotnet", dotnet, "--godot", godot, "--output", native_report])
        report["nativeFarm"] = json.loads(native_report.read_text())
        daylight_report = artifacts / "native-daylight.json"
        run("native-daylight", [sys.executable, root / "tools/check_native_daylight.py",
            "--dotnet", dotnet, "--godot", godot, "--output", daylight_report], 180)
        report["nativeDaylight"] = json.loads(daylight_report.read_text())
        input_report = artifacts / "native-input.json"
        run("native-input-focus", [sys.executable, root / "tools/check_native_input.py",
            "--dotnet", dotnet, "--godot", godot, "--output", input_report], 90)
        report["nativeInput"] = json.loads(input_report.read_text())
        wheel_report = artifacts / "native-wheel.json"
        run("native-quick-wheel", [sys.executable, root / "tools/check_native_wheel.py",
            "--dotnet", dotnet, "--godot", godot, "--output", wheel_report], 100)
        report["nativeWheel"] = json.loads(wheel_report.read_text())
        offers_report = artifacts / "native-offers.json"
        run("native-action-offers", [sys.executable, root / "tools/check_native_offers.py",
            "--dotnet", dotnet, "--godot", godot, "--output", offers_report], 100)
        report["nativeOffers"] = json.loads(offers_report.read_text())
        lifecycle_report = artifacts / "farm-lifecycle.json"
        run("native-farm-lifecycle", [sys.executable, root / "tools/check_farm_lifecycle.py",
            "--dotnet", dotnet, "--godot", godot, "--output", lifecycle_report])
        report["farmLifecycle"] = json.loads(lifecycle_report.read_text())
        gathering_report = artifacts / "native-gathering.json"
        run("native-farm-gathering", [sys.executable, root / "tools/check_native_gathering.py",
            "--dotnet", dotnet, "--godot", godot, "--output", gathering_report])
        report["nativeGathering"] = json.loads(gathering_report.read_text())
        production_report = artifacts / "native-production.json"
        run("native-farm-production", [sys.executable, root / "tools/check_native_production.py",
            "--dotnet", dotnet, "--godot", godot, "--output", production_report])
        report["nativeProduction"] = json.loads(production_report.read_text())
        quest_report = artifacts / "native-quests.json"
        run("native-farm-quests", [sys.executable, root / "tools/check_native_quests.py",
            "--dotnet", dotnet, "--godot", godot, "--output", quest_report])
        report["nativeQuests"] = json.loads(quest_report.read_text())
        construction_report = artifacts / "native-construction.json"
        run("native-farm-construction", [sys.executable, root / "tools/check_native_construction.py",
            "--dotnet", dotnet, "--godot", godot, "--output", construction_report], 900)
        report["nativeConstruction"] = json.loads(construction_report.read_text())
        for relative, expected in report["inputSha256"].items():
            if hashlib.sha256((root / relative).read_bytes()).hexdigest() != expected:
                raise RuntimeError("An input changed during verification: " + relative)
        final_sources = sorted(p for p in root.rglob("*") if p.is_file()
                               and not any(part in excluded for part in p.relative_to(root).parts))
        report["finalInputSha256"] = {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest()
                                       for p in final_sources}
        added = sorted(set(report["finalInputSha256"]) - set(report["inputSha256"]))
        if any(not path.endswith(".uid") for path in added):
            raise RuntimeError("Unexpected authored files appeared during verification: " + str(added))
        report["generatedUidFiles"] = added
        report["namingScanPassed"] = bool(args.forbidden_token)
        report["status"] = "passed"
    except Exception as error:
        report["status"] = "failed"
        report["failure"] = str(error)
        print(str(error), file=sys.stderr)
    finally:
        report["finishedUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        destination.write_text(json.dumps(report, indent=2) + "\n")
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    sys.exit(main())
