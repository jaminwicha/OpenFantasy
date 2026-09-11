"""Qualify actual gather/craft inputs, a preserved-copy quest upgrade, funded rewards and process recovery."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import uuid


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def quantity(snapshot, definition):
    return sum(lot["Quantity"] for lot in snapshot["Retained"]["Lots"] if lot["Definition"] == definition)


def time_of(snapshot):
    retained = snapshot["Retained"]
    return retained["Day"], retained["Millisecond"]


def verify_report(mode, observed):
    if (observed.get("Kind") != "native-farm-quests-v1" or observed.get("Status") != "passed"
            or observed.get("Mode") != mode or not observed.get("Runtime")
            or not observed.get("Checks") or not observed.get("Readiness")
            or observed.get("IsLocallyPaused") is not True):
        raise RuntimeError("Incomplete native quest result: " + mode)
    if observed.get("FocusRequests") != 0:
        raise RuntimeError("The native probe attempted to reclaim desktop focus")
    inputs = observed.get("Inputs", [])
    for event in inputs:
        if event["Action"] not in ("pause_world", "hud_pause", "stop_movement"):
            if event["IsLocallyPaused"] or not event["IsWindowFocused"]:
                raise RuntimeError("An ordinary input bypassed the recorded readiness/focus gate")
    actions = [event["Action"] for event in inputs]
    if mode == "prepare":
        if actions.count("gather") != 2 or actions.count("craft_rations") != 1:
            raise RuntimeError("Preparation did not use exactly two gather inputs and one craft input")
    elif mode == "upgrade":
        if actions.count("give_wood") != 2 or actions.count("talk") != 1:
            raise RuntimeError("Quest upgrade did not use ordinary talk/accept/complete inputs")
    elif actions.count("give_wood") != 1 or actions.count("talk") != 1:
        raise RuntimeError("Recovery did not attempt ordinary duplicate G and close E")
    if not any(action.startswith("walk_") for action in actions):
        raise RuntimeError("Native mode did not exercise ordinary movement: " + mode)


def verify_inventory(snapshot, observed, expected_wood, expected_total):
    retained = snapshot["Retained"]
    recipe = next(recipe for recipe in retained["Recipes"] if recipe["Key"] == "craft_rations")
    products = {term["Definition"] for term in recipe["Terms"] if term["Role"] == 2}
    waste = {term["Definition"] for term in recipe["Terms"] if term["Role"] == 3}
    # Role values are checked below against the declared two products / one waste shape as well.
    if len(products) != 1 or len(waste) != 1:
        raise RuntimeError("The ration recipe's public product/waste roles are not the qualified profile")
    if (retained["Quantity"] != expected_total or retained["CraftingRevision"] != 1
            or quantity(snapshot, observed["WoodDefinition"]) != expected_wood
            or quantity(snapshot, observed["BerryDefinition"]) != 0
            or sum(quantity(snapshot, definition) for definition in products) != 2
            or sum(quantity(snapshot, definition) for definition in waste) != 1
            or len(retained["Tools"]) != 2):
        raise RuntimeError("The actual retained inventory does not match the completed production/delivery accounting")
    lot_by_id = {lot["Id"]: lot for lot in retained["Lots"]}
    if any(tool["Id"] not in lot_by_id or lot_by_id[tool["Id"]]["Quantity"] != 1 for tool in retained["Tools"]):
        raise RuntimeError("The public tools are not retained as actual owned one-unit lots")


def verify_reward(snapshot):
    progression = snapshot["Progression"]
    expected = dict(Level=3, Experience=38, Hp=78, MaxHp=78, Mp=14, MaxMp=14, CurrencyBalance=200, CurrencyUnit="gil")
    if progression is None or any(progression.get(key) != value for key, value in expected.items()):
        raise RuntimeError("The quest reward did not retain exact source leveling, vitals and 200 gil")
    quests = snapshot["Quests"]
    if (len(quests) != 1 or quests[0]["Status"] != 2 or quests[0]["ParticipationRevision"] != 2
            or quests[0]["EligibleQuantity"] != 1):
        raise RuntimeError("The actual quest completion/remaining delivery eligibility is not retained")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", required=True)
    parser.add_argument("--godot", required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--rendered", action="store_true")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    output = args.output.resolve()
    work = output.parent / ("native-quests-" + uuid.uuid4().hex)
    work.mkdir(parents=True)
    source, destination, probe = work / "production.sqlite", work / "quests.sqlite", work / "quests.json"
    sdk = Path(args.dotnet).resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get("PATH", ""))
    report = dict(startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope="Three native processes: real gathering/crafting, copied quest admission, ordinary acceptance/delivery and exact rewarded recovery",
                  rendered=args.rendered, status="running", runs=[])
    try:
        binaries = [root / "client" / ".godot" / "mono" / "temp" / "bin" / "Debug" / name
                    for name in ("OpenFantasy.Client.dll", "OpenFantasy.Core.dll", "OpenFantasy.Hosting.dll")]
        report["runtimeSha256"] = {str(path.relative_to(root)): sha256(path) for path in binaries}
        report["godotSha256"] = sha256(Path(args.godot).resolve())
        source_hash = None
        for mode in ("prepare", "upgrade", "reopen"):
            command = [str(Path(args.godot).resolve())]
            if not args.rendered:
                command.append("--headless")
            command += ["--path", str(root / "client"), "--", "--farm-quests", str(source), str(destination), str(probe), mode]
            result = subprocess.run(command, cwd=root, env=env, capture_output=True, text=True, timeout=140)
            report["runs"].append(dict(mode=mode, command=command, exitCode=result.returncode,
                                       stdout=result.stdout, stderr=result.stderr))
            if result.returncode or "ERROR:" in result.stdout + result.stderr:
                raise RuntimeError("Native quest " + mode + " failed: " + result.stdout + result.stderr)
            observed = json.loads(probe.with_suffix("." + mode + ".json").read_text())
            verify_report(mode, observed)
            report[mode] = observed
            if mode == "prepare":
                source_hash = sha256(source)
            elif sha256(source) != source_hash:
                raise RuntimeError("The retained production source was modified by quest migration or continuation")
        prepared = json.loads(report["prepare"]["Final"])
        upgrade_initial = json.loads(report["upgrade"]["Initial"])
        completed = json.loads(report["upgrade"]["Final"])
        reopened = json.loads(report["reopen"]["Initial"])
        continued = json.loads(report["reopen"]["Final"])
        if prepared["Retained"] != upgrade_initial["Retained"]:
            raise RuntimeError("Quest upgrade changed preexisting public state")
        if completed != reopened:
            raise RuntimeError("Rewarded state failed exact recovery before resume")
        if prepared["Progression"] is not None or prepared["Quests"]:
            raise RuntimeError("Preparation accidentally started in the quest profile")
        if (upgrade_initial["Progression"]["Level"] != 1 or upgrade_initial["Progression"]["Experience"] != 0
                or upgrade_initial["Progression"]["CurrencyBalance"] != 0):
            raise RuntimeError("Migration granted rewards before acceptance and delivery")
        verify_inventory(prepared, report["prepare"], 7, 12)
        for snapshot, mode in ((completed, "upgrade"), (continued, "reopen")):
            verify_inventory(snapshot, report[mode], 1, 6)
            verify_reward(snapshot)
        if not completed["Retained"]["HasReadingHold"] or completed["Retained"]["Dialogue"] is None:
            raise RuntimeError("Acceptance/delivery did not retain the active reading hold")
        if continued["Retained"]["HasReadingHold"] or continued["Retained"]["Dialogue"] is not None:
            raise RuntimeError("Reopened E input did not release the conversation")
        if time_of(continued) <= time_of(reopened):
            raise RuntimeError("Reopened ordinary movement did not resume calendar advancement")
        player = continued["Retained"]["Player"]
        before_actor = next(actor for actor in reopened["Retained"]["Actors"] if actor["Id"] == player)
        after_actor = next(actor for actor in continued["Retained"]["Actors"] if actor["Id"] == player)
        if (after_actor["X"] - before_actor["X"])**2 + (after_actor["Y"] - before_actor["Y"])**2 < 128**2:
            raise RuntimeError("Recovered player did not retain meaningful actual movement")
        if args.rendered:
            for mode in ("prepare", "upgrade", "reopen"):
                if not probe.with_suffix("." + mode + ".png").is_file():
                    raise RuntimeError("Missing native rendered capture: " + mode)
                if report[mode]["DisplayDriver"] == "headless":
                    raise RuntimeError("A rendered qualification ran headless")
        if any(sha256(root / path) != expected for path, expected in report["runtimeSha256"].items()):
            raise RuntimeError("The executed runtime artifacts changed during native qualification")
        report["retainedSourceSha256"] = source_hash
        report["artifactSha256"] = {str(path.relative_to(output.parent)): sha256(path)
                                     for path in sorted(work.iterdir()) if path.is_file()}
        report["status"] = "passed"
    except Exception as error:
        report["status"] = "failed"
        report["failure"] = str(error)
    finally:
        report["finishedUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2) + "\n")
    print("native quests: " + report["status"], flush=True)
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
