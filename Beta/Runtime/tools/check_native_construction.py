"""Qualify preserved quest progress, real NPC construction, reading holds, collision and process recovery."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import uuid

from check_native_quests import verify_inventory, verify_reward


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def snapshot(report, key):
    return json.loads(report[key])


def project(value):
    projects = value["Construction"]
    if (len(projects) != 1 or projects[0]["RequiredWork"] != 200
            or projects[0]["Site"] != dict(X=27213, Y=83072, Z=256)
            or projects[0]["Work"] != dict(X=27264, Y=83328, Z=256)):
        raise RuntimeError("The public construction calibration is not the qualified guard")
    return projects[0]


def actor(value, identity=None):
    retained = value["Prior"]["Retained"]
    identity = identity or retained["Player"]
    return next(item for item in retained["Actors"] if item["Id"] == identity)


def time_of(value):
    retained = value["Prior"]["Retained"]
    return retained["Day"], retained["Millisecond"]


def verify_report(mode, report):
    if (report.get("Kind") != "native-farm-construction-v1" or report.get("Status") != "passed"
            or report.get("Mode") != mode or not report.get("Runtime") or not report.get("Checks")
            or not report.get("Readiness") or report.get("IsLocallyPaused") is not True
            or report.get("InitialPausedMilliseconds", 0) < 600
            or report.get("FinalPausedMilliseconds", 0) < 600
            or not 0 <= report.get("FocusRequests", -1) <= 8
            or not 0 < report.get("DurationMilliseconds", 0) < 125_000):
        raise RuntimeError("Incomplete native construction result: " + mode)
    actions = []
    for event in report["Inputs"]:
        action = event["Action"]
        actions.append(action)
        if action not in ("pause_world", "stop_movement") and (event["IsLocallyPaused"] or not event["IsWindowFocused"]):
            raise RuntimeError("An ordinary construction input bypassed readiness or focus")
    if mode in ("upgrade", "fresh"):
        if actions.count("talk") != (3 if mode == "fresh" else 2) or actions.count("pause_world") != 2:
            raise RuntimeError("The work interruption did not use ordinary E/E and pause/resume controls")
    elif mode == "finish":
        if (actions.count("pause_world") != 2 or "walk_left" not in actions
                or "walk_up" not in actions or "walk_right" not in actions):
            raise RuntimeError("The completion stage did not exercise ordinary movement and pause controls")
    elif actions:
        raise RuntimeError("The completed recovery stage unexpectedly issued a gameplay input")


def verify_retained_player(source, value):
    prior = value["Prior"]
    verify_reward(prior)
    if prior["Progression"] != source["Progression"] or prior["Quests"] != source["Quests"]:
        raise RuntimeError("Construction changed the completed player quest or its reward")
    for field in ("World", "Branch", "Player", "Quantity", "Custodian", "StorageRevision", "Lots", "Tools", "Recipes"):
        if prior["Retained"][field] != source["Retained"][field]:
            raise RuntimeError("Construction changed retained player state: " + field)


def verify_journey(report, source, first_mode="upgrade"):
    upgrade, finish, reopen = (report[mode] for mode in (first_mode, "finish", "reopen"))
    admitted, partial = snapshot(upgrade, "Initial"), snapshot(upgrade, "Final")
    before_finish, completed = snapshot(finish, "Initial"), snapshot(finish, "Final")
    recovered, recovered_final = snapshot(reopen, "Initial"), snapshot(reopen, "Final")
    if admitted["Prior"] != source or project(admitted)["CompletedWork"] != 0 or project(admitted)["IsCompleted"]:
        raise RuntimeError("Admission changed prior progress or performed premature work")
    if partial != before_finish or completed != recovered or recovered != recovered_final:
        raise RuntimeError("Construction recovery did not retain the exact previous complete public snapshot")
    if not 40 <= project(partial)["CompletedWork"] < 200 or project(partial)["IsCompleted"]:
        raise RuntimeError("The first process did not retain meaningful unfinished construction")
    for value in (completed, recovered):
        if project(value)["CompletedWork"] != 200 or not project(value)["IsCompleted"]:
            raise RuntimeError("The completed guard was not restored at exactly 200 work")
    observations = {key: json.loads(value) for key, value in upgrade["Observations"].items()}
    if (observations["ReadingStart"] != observations["ReadingEnd"]
            or upgrade["ReadingMilliseconds"] < 600
            or not observations["ReadingStart"]["Prior"]["Retained"]["HasReadingHold"]):
        raise RuntimeError("A real conversation did not hold the public world and ongoing work")
    builder = project(admitted)["Builder"]
    working = observations["BeforeTalk"]
    if (actor(admitted, builder) == actor(working, builder)
            or any(actor(working, builder)[key] != project(working)["Work"][key] for key in ("X", "Y", "Z"))
            or not 0 < project(working)["CompletedWork"] < 200):
        raise RuntimeError("Wren did not actually travel to the work site and perform unfinished work")
    if (time_of(partial) <= time_of(observations["ReadingEnd"])
            or project(partial)["CompletedWork"] <= project(observations["ReadingEnd"])["CompletedWork"]):
        raise RuntimeError("Closing the conversation did not resume both time and the same project")
    measured = {key: json.loads(value) for key, value in finish["Observations"].items()}
    approach, blocked, around = (measured[key] for key in ("BeforeCollision", "BlockedByGuard", "AfterWalkAround"))
    if (actor(approach)["X"] != 27520 or actor(approach)["Y"] != 83072
            or actor(blocked)["X"] != 27424 or actor(blocked)["Y"] != 83072
            or actor(around)["X"] >= 27123 or actor(around)["Y"] != 82944
            or finish["CollisionReadyMilliseconds"] < 900
            or time_of(blocked) <= time_of(approach) or time_of(around) <= time_of(blocked)):
        raise RuntimeError("The native collision and walk-around observations do not prove the actual body boundary")
    for other in blocked["Prior"]["Retained"]["Actors"]:
        if (other["Id"] != blocked["Prior"]["Retained"]["Player"] and abs(other["Y"] - 83072) < 140
                and 27212 < other["X"] < 27604):
            raise RuntimeError("Another actor could account for the measured collision")
    for value in (admitted, partial, before_finish, completed, recovered, recovered_final, *observations.values(), *measured.values()):
        verify_retained_player(source, value)
    for value in (admitted, partial):
        if value["Prior"]["Retained"]["CraftingRevision"] != source["Retained"]["CraftingRevision"]:
            raise RuntimeError("Unfinished work committed a premature material transformation")
    if completed["Prior"]["Retained"]["CraftingRevision"] != source["Retained"]["CraftingRevision"] + 1:
        raise RuntimeError("Completion did not commit exactly one material transformation")


def verify_fresh_quests(report):
    if (report.get("Kind") != "native-farm-quests-v1" or report.get("Status") != "passed"
            or report.get("Mode") != "fresh" or report.get("IsLocallyPaused") is not True
            or not report.get("Checks") or not report.get("Readiness")):
        raise RuntimeError("The actual fresh-profile quest preparation did not pass")
    initial, final = snapshot(report, "Initial"), snapshot(report, "Final")
    if (initial["Retained"]["CraftingRevision"] != 0 or initial["Retained"]["Quantity"] != 10
            or initial["Progression"]["Level"] != 1 or initial["Progression"]["Experience"] != 0
            or initial["Progression"]["CurrencyBalance"] != 0 or initial["Quests"]):
        raise RuntimeError("The fresh journey started with prior progress or disclosed quests")
    verify_inventory(final, report, 1, 6)
    verify_reward(final)
    if not final["Retained"]["HasReadingHold"] or final["Retained"]["Dialogue"] is None:
        raise RuntimeError("Fresh quest completion did not preserve its reading hold for zero-work recovery")
    outside = report.get("OutsideQuestAction", {})
    if (outside.get("ObservationMilliseconds", 0) < 300 or outside.get("Samples", 0) < 2
            or outside.get("HasPendingActionAfterInput") is not False
            or outside.get("HasPendingActionAfterObservation") is not False):
        raise RuntimeError("Early unaddressed G did not retain a bounded no-pending-action observation")
    before, after = snapshot(outside, "Before"), snapshot(outside, "After")
    for observed in (before, after):
        if (observed["Retained"]["HasReadingHold"] or observed["Retained"]["Dialogue"] is not None
                or observed["Progression"] != initial["Progression"] or observed["Quests"] != initial["Quests"]):
            raise RuntimeError("Unaddressed G changed the initial quest, progression or conversation")
        for field in ("Quantity", "Custodian", "StorageRevision", "Lots", "Tools", "CraftingRevision"):
            if observed["Retained"][field] != initial["Retained"][field]:
                raise RuntimeError("Unaddressed G changed initial player stock: " + field)
    actions = []
    for event in report["Inputs"]:
        action = event["Action"]
        actions.append(action)
        if action not in ("pause_world", "stop_movement") and (event["IsLocallyPaused"] or not event["IsWindowFocused"]):
            raise RuntimeError("Fresh quest input bypassed ordinary readiness or focus")
    if (actions.count("gather") != 2 or actions.count("craft_rations") != 1
            or actions.count("talk") != 1 or actions.count("give_wood") != 3
            or actions.count("pause_world") != 2 or not any(action.startswith("walk_") for action in actions)):
        raise RuntimeError("The fresh world did not use the exact actual gather/craft/talk/accept/deliver input journey")
    early = [event for event in report["Inputs"] if event["Action"] == "give_wood" and event["Stage"] == "OutsideQuestContext"]
    if len(early) != 1 or actions.index("give_wood") >= actions.index("gather"):
        raise RuntimeError("The fresh-world regression did not press ordinary G before harvesting and before speaking")


def run_native(root, args, env, runs, mode, arguments):
    command = [str(Path(args.godot).resolve())]
    if not args.rendered:
        command.append("--headless")
    command += ["--path", str(root / "client"), "--", *map(str, arguments)]
    result = subprocess.run(command, cwd=root, env=env, capture_output=True, text=True, timeout=125)
    runs.append(dict(mode=mode, command=command, exitCode=result.returncode, stdout=result.stdout, stderr=result.stderr))
    if result.returncode or "ERROR:" in result.stdout + result.stderr:
        raise RuntimeError("Native " + mode + " failed: " + result.stdout + result.stderr)


def run_construction_family(root, args, env, report, source, destination, probe, first_mode, retained_source_hash=None):
    for mode in (first_mode, "finish", "reopen"):
        run_native(root, args, env, report["runs"], mode,
                   ["--farm-construction", source, destination, probe, mode])
        observed = json.loads(probe.with_suffix("." + mode + ".json").read_text())
        verify_report(mode, observed)
        report[mode] = observed
        if retained_source_hash is not None and sha256(source) != retained_source_hash:
            raise RuntimeError("Construction modified the retained original quest database")
        if args.rendered and (observed["DisplayDriver"] == "headless" or not probe.with_suffix("." + mode + ".png").is_file()):
            raise RuntimeError("Missing actual rendered construction capture: " + mode)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", required=True)
    parser.add_argument("--godot", required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--rendered", action="store_true")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    output = args.output.resolve()
    work = output.parent / ("native-construction-" + uuid.uuid4().hex)
    work.mkdir(parents=True)
    preparation = work / "source-quest-verification.json"
    destination, probe = work / "construction.sqlite", work / "construction.json"
    sdk = Path(args.dotnet).resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get("PATH", ""))
    report = dict(startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(), status="running", runs=[],
                  rendered=args.rendered, scope="Both a preserved copied quest save and a fresh construction world; actual gathering/crafting/quest inputs, native NPC work, reading hold, partial/completed recovery and material collision. Scripted behavior, not art acceptance.")
    try:
        binaries = [root / "client" / ".godot" / "mono" / "temp" / "bin" / "Debug" / name
                    for name in ("OpenFantasy.Client.dll", "OpenFantasy.Core.dll", "OpenFantasy.Hosting.dll")]
        report["runtimeSha256"] = {str(path.relative_to(root)): sha256(path) for path in binaries}
        excluded = {".godot", "bin", "obj", "artifacts", "TestResults", "__pycache__"}
        sources = sorted(path for path in root.rglob("*") if path.is_file()
                         and not any(part in excluded for part in path.relative_to(root).parts))
        report["sourceSha256"] = {str(path.relative_to(root)): sha256(path) for path in sources}
        report["godotSha256"] = sha256(Path(args.godot).resolve())
        command = [sys.executable, str(root / "tools" / "check_native_quests.py"), "--dotnet", args.dotnet,
                   "--godot", args.godot, "--output", str(preparation)]
        if args.rendered:
            command.append("--rendered")
        result = subprocess.run(command, cwd=root, env=env, capture_output=True, text=True, timeout=430)
        report["preparationRun"] = dict(command=command, exitCode=result.returncode, stdout=result.stdout, stderr=result.stderr)
        if result.returncode:
            raise RuntimeError("The actual gather/craft/quest source journey did not pass: " + result.stdout + result.stderr)
        prepared = json.loads(preparation.read_text())
        if prepared["status"] != "passed":
            raise RuntimeError("The source quest verifier did not retain a passing result")
        prepared_command = prepared["runs"][0]["command"]
        source = Path(prepared_command[prepared_command.index("--farm-quests") + 2])
        source_hash = sha256(source)
        report["sourceQuestPath"] = str(source)
        report["sourceQuestSha256"] = source_hash
        probe.with_suffix(".source.json").write_text(json.dumps(prepared["reopen"], indent=2) + "\n")
        report["sourcePreparationSha256"] = sha256(preparation)
        run_construction_family(root, args, env, report, source, destination, probe, "upgrade", source_hash)
        verify_journey(report, json.loads(prepared["reopen"]["Final"]))
        fresh_directory = work / "fresh"
        fresh_directory.mkdir()
        absent_predecessor = fresh_directory / "unused-predecessor.sqlite"
        fresh_save = fresh_directory / "construction.sqlite"
        fresh_quest_probe = fresh_directory / "quest.json"
        fresh_probe = fresh_directory / "construction.json"
        fresh = report["freshJourney"] = dict(runs=[], savePath=str(fresh_save))
        run_native(root, args, env, fresh["runs"], "fresh-quests",
                   ["--farm-quests", absent_predecessor, fresh_save, fresh_quest_probe, "fresh"])
        fresh_quests = json.loads(fresh_quest_probe.with_suffix(".fresh.json").read_text())
        verify_fresh_quests(fresh_quests)
        if args.rendered and (fresh_quests["DisplayDriver"] == "headless" or not fresh_quest_probe.with_suffix(".fresh.png").is_file()):
            raise RuntimeError("Missing actual fresh-world quest capture")
        fresh["questPreparation"] = fresh_quests
        fresh["beforeConstructionSaveSha256"] = sha256(fresh_save)
        fresh_probe.with_suffix(".source.json").write_text(json.dumps(fresh_quests, indent=2) + "\n")
        run_construction_family(root, args, env, fresh, absent_predecessor, fresh_save, fresh_probe, "fresh")
        verify_journey(fresh, json.loads(fresh_quests["Final"]), "fresh")
        if absent_predecessor.exists() or sha256(source) != source_hash:
            raise RuntimeError("The fresh journey created a fake predecessor or changed the separate retained source")
        for hashes in (report["runtimeSha256"], report["sourceSha256"]):
            if any(sha256(root / path) != expected for path, expected in hashes.items()):
                raise RuntimeError("Qualification source or executed artifacts changed during native construction")
        report["status"] = "passed"
    except Exception as error:
        report["status"] = "failed"
        report["failure"] = str(error)
    finally:
        report["artifactSha256"] = {str(path.relative_to(output.parent)): sha256(path)
                                   for path in sorted(work.rglob("*")) if path.is_file()}
        report["finishedUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2) + "\n")
    print("native construction: " + report["status"], flush=True)
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
