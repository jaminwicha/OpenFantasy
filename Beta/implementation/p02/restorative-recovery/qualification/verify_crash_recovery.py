"""Qualify profile26 ordinary-care/schema7 publication with actual Host processes and SIGKILL.

Compares complete stored roots and ledgers with exported before/after fixtures, and
independently checks their framing, archive references, care claims and receipts.
This is atomicity/recovery evidence, not independent gameplay-law reconstruction.
"""
import argparse
from contextlib import closing
import datetime
import hashlib
import json
import os
from pathlib import Path
import queue
import shutil
import signal
import sqlite3
import struct
import subprocess
import threading
import time
import traceback


RUN_SECONDS = 30
KILL_SECONDS = 5
MAX_STATE_BYTES = 16 * 1024 * 1024
FIXTURES = ("raw-self", "crafted-self", "helper", "revoke", "archive-helper")
TABLES = {
    "world_genesis": "singleton",
    "world_head": "singleton",
    "world_commits": "sequence",
    "world_command_outcomes": "principal,command_id",
    "world_occupied_identity": "identity",
    "world_checkpoints": "sequence",
    "world_resource_segments": "publication_sequence",
}


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def digest(value):
    return hashlib.sha256(value).digest()


def number(value):
    require(len(value) == 8, "Expected an unsigned eight-byte number")
    return int.from_bytes(value, "big")


def frame(data, tag, count=None):
    require(12 <= len(data) <= MAX_STATE_BYTES, "Frame exceeds retained bounds")
    require(data[:4] == b"OFC\0", "Unknown canonical frame signature")
    version, size = struct.unpack_from(">II", data, 4)
    require(version == 1 and 1 <= size <= 65536, "Unknown frame version/count")
    values, offset = [], 12
    for _ in range(size):
        require(offset + 4 <= len(data), "Truncated frame length")
        length = struct.unpack_from(">I", data, offset)[0]
        offset += 4
        require(offset + length <= len(data), "Truncated frame field")
        values.append(data[offset:offset + length])
        offset += length
    require(offset == len(data), "Trailing canonical bytes")
    require(values[0] == tag.encode(), "Unexpected frame tag: " + tag)
    require(count is None or len(values) == count, "Wrong field count: " + tag)
    return values


def root(data):
    values = frame(data, "world.snapshot", 24)
    require(number(values[1]) == 26, "Expected exact profile26")
    return values


def file_hashes(directory):
    return {str(path.relative_to(directory)): digest(path.read_bytes()).hex()
            for path in sorted(directory.rglob("*")) if path.is_file()}


def copy_closed(source, destination):
    for suffix in ("-wal", "-journal"):
        sidecar = Path(str(source) + suffix)
        require(not sidecar.exists() or sidecar.stat().st_size == 0,
                "Fixture requires a closed checkpointed database: " + str(source))
    shutil.copyfile(source, destination)


def database(path):
    # Read WAL-aware after the Host has actually recovered and closed this working copy.
    with closing(sqlite3.connect(path.as_uri() + "?mode=ro", uri=True)) as connection:
        connection.execute("BEGIN")
        require(connection.execute("PRAGMA user_version").fetchone()[0] == 7, "Wrong schema version")
        require(connection.execute("PRAGMA application_id").fetchone()[0] == 0x4F464157, "Wrong application ID")
        require(connection.execute("PRAGMA quick_check").fetchall() == [("ok",)], "SQLite integrity failure")
        require(not connection.execute("PRAGMA foreign_key_check").fetchall(), "Broken foreign key")
        tables = {row[0] for row in connection.execute("SELECT name FROM sqlite_schema WHERE type='table'")}
        require(tables == set(TABLES), "Unexpected durable table inventory")
        result = {name: connection.execute("SELECT * FROM " + name + " ORDER BY " + order).fetchall()
                  for name, order in TABLES.items()}
        result["schema"] = connection.execute("SELECT type,name,tbl_name,sql FROM sqlite_schema ORDER BY type,name").fetchall()
        return result


def check_database(value, expected, archives):
    state = root(expected)
    require(value["world_head"] == [(1, state[8], state[9], expected, digest(expected))], "Complete stored head differs")
    require(len(value["world_genesis"]) == 1, "Missing unique genesis")
    genesis = value["world_genesis"][0]
    require(genesis[0] == 1 and digest(genesis[1]) == genesis[2], "Genesis checksum differs")
    rows = value["world_commits"]
    require([number(row[0]) for row in rows] == list(range(1, number(state[8]) + 1)), "Journal sequence is incomplete")
    by_sequence = {row[0]: row for row in rows}
    prior = genesis[2]
    for row in rows:
        require(row[6] == prior, "Journal hash chain differs")
        prior = row[7]
    require(prior == digest(expected), "Journal does not reach expected head")
    segments = value["world_resource_segments"]
    require(len(segments) == archives, "Archive count differs")
    for ordinal, row in enumerate(segments):
        world, branch, selected, boundary, publication, hash_value, encoded = row
        require(number(selected) == ordinal and number(publication) == number(boundary) + 1,
                "Archive order or publication boundary differs")
        require(digest(encoded) == hash_value and by_sequence[publication][6] == hash_value,
                "Archive bytes or publishing commit differ")
        closing_root = root(encoded)
        care = frame(closing_root[22], "care.snapshot", 10)
        require(care[2:4] == [world, branch] and closing_root[8] == boundary, "Archive scope or sequence differs")
        history = frame(closing_root[23], "world.resource-history")
        if ordinal == 0:
            require(number(history[1]) == 1, "First closing archive must retain inline history")
        else:
            check_reference(history, segments[ordinal - 1])
    if segments:
        check_reference(frame(state[23], "world.resource-history"), segments[-1])
    for sequence, steps, encoded, hash_value in value["world_checkpoints"]:
        require(digest(encoded) == hash_value, "Checkpoint checksum differs")
        selected = root(encoded)
        require(selected[8:10] == [sequence, steps], "Checkpoint clocks differ")
        require(hash_value == (genesis[2] if number(sequence) == 0 else by_sequence[sequence][7]),
                "Checkpoint is absent from committed history")


def check_reference(history, archive):
    require(number(history[1]) == 2 and len(history) == 4, "Missing continued history")
    reference = frame(history[2], "world.resource-archive-reference.v1", 6)
    require(reference[1:] == [archive[0], archive[1], archive[2], archive[3], archive[5]],
            "Resource history selects a different archive")


def one_new(before, after, tag):
    old, new = set(frame(before, tag)[1:]), set(frame(after, tag)[1:])
    require(old < new and len(new - old) == 1, "Expected one added record: " + tag)
    return next(iter(new - old))


def history_body(encoded):
    fields = frame(encoded, "world.resource-history")
    if number(fields[1]) == 2:
        fields = frame(fields[3], "world.resource-history", 7)
    require(number(fields[1]) == 1 and len(fields) == 7, "Unknown resource history body")
    return fields


def actor_map(progression):
    return {frame(value, "progression.actor", 12)[2]: frame(value, "progression.actor", 12)
            for value in frame(progression[7], "progression.actors")[1:]}


def check_dose_retirement(old_inventory, new_inventory, old_production, new_production, claim, ingredient, interval):
    old_custody, new_custody = (frame(value[3], "custody.snapshot.v1", 6) for value in (old_inventory, new_inventory))
    require(old_custody[1:3] == new_custody[1:3] and number(new_custody[3]) == number(old_custody[3]) + 1,
            "Dose retirement changed scope or failed to increment custody")
    old_storages, new_storages = ({frame(value, "custody.storage.v1", 4)[1]: frame(value, "custody.storage.v1", 4)
                                for value in frame(custody[4], "custody.storages.v1")[1:]}
                               for custody in (old_custody, new_custody))
    require(set(old_storages) == set(new_storages) and claim[6] in old_storages, "Dose custody is not admitted")
    for identity, previous in old_storages.items():
        current = new_storages[identity]
        require(previous[1:3] == current[1:3] and number(current[3]) == number(previous[3]) + (identity == claim[6]),
                "Dose changed another storage or its capacity")
    old_raw, new_raw = ({frame(value, "custody.source-lot.v1", 11)[1]: value
                        for value in frame(custody[5], "custody.lots.v1")[1:]}
                       for custody in (old_custody, new_custody))
    old_made, new_made = ({frame(value, "production.lot", 10)[2]: value
                          for value in frame(production[7], "production.lots")[1:]}
                         for production in (old_production, new_production))
    selected = ingredient[3]
    if number(ingredient[2]) == 1:
        require(selected in old_raw and old_made == new_made, "Raw dose changed transformed custody")
        lot = frame(old_raw[selected], "custody.source-lot.v1", 11)
        require(lot[2] == claim[6] and lot[3:5] == ingredient[5:7] and lot[8] == ingredient[4]
                and lot[5:8] == interval[2:] and number(lot[7]) - number(lot[6]) == 1,
                "Raw dose lacks exact owned source interval, definition or revision")
        require(new_raw == {key: value for key, value in old_raw.items() if key != selected},
                "Raw dose was not retired exactly once or changed another lot")
    else:
        require(selected in old_made and old_raw == new_raw, "Crafted dose changed raw custody")
        lot = frame(old_made[selected], "production.lot", 10)
        require(lot[3] == claim[6] and lot[4:6] == ingredient[5:7] and lot[7] == ingredient[4]
                and lot[6] == ingredient[7], "Crafted dose lacks exact owned output interval or revision")
        require(new_made == {key: value for key, value in old_made.items() if key != selected},
                "Crafted dose was not retired exactly once or changed another lot")
        batches = [frame(value, "production.batch", 9) for value in frame(old_production[6], "production.batches")[1:]]
        matching = [value for value in batches if value[2] == interval[2]]
        require(len(matching) == 1 and matching[0][4:6] == [claim[4], claim[6]],
                "Fixture dose was not actually crafted by its supplier in its own custody")


def check_selected_step(before, after, batch, before_db, after_db, name, metadata):
    initial, final = root(before), root(after)
    step = frame(batch, "world.step-batch", 10)
    require(number(step[1]) == 26 and step[5:7] == initial[8:10], "Selected step basis differs")
    require(number(step[7]) == 1, "Ordinary care requires a normal advancing step")
    require(number(final[8]) == number(initial[8]) + 1, "Selected step must publish exactly once")
    require(number(final[9]) == number(initial[9]) + 1, "Ordinary care must advance one exploration step")
    require(number(final[6]) * 86400000 + number(final[7])
            == number(initial[6]) * 86400000 + number(initial[7]) + 1800, "Ordinary care has the wrong duration")
    require(initial[3] == final[3], "Ordinary care changed spatial state")
    added = [row for row in after_db["world_commits"] if row[0] == final[8]]
    require(len(added) == 1 and added[0][4] == batch and added[0][6:8] == (digest(before), digest(after)),
            "Selected committed batch or hashes differ")
    commands = frame(step[9], "world.step-actions")[1:]
    outcomes = frame(added[0][5], "world.action-outcomes")[1:]
    selected_rows = [row for row in after_db["world_command_outcomes"] if row[2] == final[8]]
    require(len(commands) == len(outcomes) == len(selected_rows) == 1, "Expected exactly one selected receipt")
    action = frame(commands[0], "world.actor-action", 10)
    receipt = frame(outcomes[0], "world.action-receipt", 10)
    require(number(action[1]) == 14 and receipt[2] == action[2], "Wrong ordinary command or receipt identity")
    require(receipt[3:6] == [action[3], *step[3:5]] and receipt[6] == digest(commands[0]), "Receipt scope/hash differs")
    require(receipt[7] == final[8] and number(receipt[8]) == 1 and number(receipt[9]) == 0, "Selected action was not applied")
    require((action[3], action[2], final[8], commands[0], outcomes[0]) == selected_rows[0], "Durable command receipt differs")
    require(action[2].hex() == metadata["commandId"] and digest(commands[0]).hex() == metadata["commandHash"],
            "Selected fixture command differs")
    require(action[5].hex() == metadata["selectedActorId"], "Selected supplier differs")
    require(set(after_db["world_command_outcomes"]) == set(before_db["world_command_outcomes"]) | set(selected_rows),
            "Earlier receipts changed")
    old_inventory, new_inventory = (frame(value[2], "authority.counted-state", 9) for value in (initial, final))
    controls = [frame(value, "authority.control", 4) for value in frame(old_inventory[5], "authority.controls")[1:]]
    require(any(value[1:] == [action[3], action[4], action[5]] for value in controls), "Supplier lacked actual control")
    require(old_inventory[:3] == new_inventory[:3] and old_inventory[4:] == new_inventory[4:],
            "Ordinary care changed admitted source supply, control, policy or identities")
    old_care, new_care = (frame(value[22], "care.snapshot", 10) for value in (initial, final))
    require(number(old_care[1]) == number(new_care[1]) == 2, "Wrong ordinary care interpretation")
    require(old_care[5:9] == new_care[5:9], "Ordinary use changed catalogs or aftermath history")
    require(number(new_care[4]) == number(old_care[4]) + 1, "Care revision must advance once")
    old_ordinary, new_ordinary = (frame(value[9], "care.ordinary", 6) for value in (old_care, new_care))
    require(old_ordinary[3] == new_ordinary[3] and number(new_ordinary[2]) == number(old_ordinary[2]) + 1,
            "Ordinary definitions/revision differ")
    old_production, new_production = (frame(value[12], "production.snapshot", 9) for value in (initial, final))
    old_progression, new_progression = (frame(value[13], "progression.snapshot", 9) for value in (initial, final))
    require(old_production[5:7] == new_production[5:7], "Using a dose changed its recipe or production lineage")
    require(old_progression[5:7] == new_progression[5:7] and old_progression[8] == new_progression[8],
            "Care changed stat definitions, level curves or funded currency")
    old_actors, new_actors = actor_map(old_progression), actor_map(new_progression)
    require(set(old_actors) == set(new_actors), "Care replaced actor identities")
    patient = bytes.fromhex(metadata["playerActorId"])
    require(all(old_actors[key] == new_actors[key] for key in old_actors if key != patient), "Care altered another actor")
    previous, current = old_actors[patient], new_actors[patient]
    require(previous[4:10] == current[4:10] and previous[11] == current[11], "Care changed profile/level/XP/MP")
    require(number(previous[10]) == metadata["beforeHp"] and number(current[10]) == metadata["afterHp"], "HP metadata differs")
    require(number(previous[11]) == metadata["beforeMp"] and number(current[11]) == metadata["afterMp"], "MP metadata differs")
    archive = name == "archive-helper"
    require(len(before_db["world_resource_segments"]) == 0
            and len(after_db["world_resource_segments"]) == (1 if archive else 0), "Unexpected archive publication")
    if archive:
        require(after_db["world_resource_segments"][0][6] == before, "Archive differs from exact pre-use boundary")
    if name == "revoke":
        require(initial[2] == final[2] and initial[12:14] == final[12:14] and initial[23] == final[23],
                "Permission revocation spent material or changed resources")
        require(old_ordinary[5] == new_ordinary[5], "Revocation added an ordinary use")
        old_permissions = frame(old_ordinary[4], "care.permissions")[1:]
        new_permissions = frame(new_ordinary[4], "care.permissions")[1:]
        require(len(old_permissions) == len(new_permissions) == 1, "Expected one exact patient permission")
        old_permission, new_permission = (frame(value, "care.permission.v1", 3) for value in (old_permissions[0], new_permissions[0]))
        require(old_permission[1] == new_permission[1] and old_permission[2] == b"", "Revocation replaced an earlier grant or revocation")
        grant = frame(new_permission[1], "care.permission-grant.v1", 6)
        revoked = frame(new_permission[2], "care.permission-revocation.v1", 7)
        intent = frame(action[9], "world.action.revoke-care-permission", 5)
        require(action[5] == patient == grant[2] == revoked[4] and revoked[2] == action[2]
                and revoked[3] == grant[1] == intent[2] and revoked[5] == final[8] and number(revoked[6]) == 0,
                "Revocation has the wrong patient, grant, command or world position")
        require(number(intent[3]) == 0 and intent[4] == digest(old_permission[1]), "Selected permission pin differs")
        return {"selectedReceipts": 1, "permissionRevoked": True, "consumedDoses": 0, "archivePublished": False}
    require(old_ordinary[4] == new_ordinary[4], "Ordinary use changed patient permission")
    use = frame(one_new(old_ordinary[5], new_ordinary[5], "care.ordinary-uses"), "care.ordinary-use.v1", 18)
    claim = frame(one_new(old_production[8], new_production[8], "production.consumed"), "production.consumed-item", 8)
    intent = frame(action[9], "world.action.restorative-use", 11)
    require(use[2] == claim[3] == action[2] and use[3] == claim[4] == action[5]
            and use[4] == claim[5] == patient == intent[2] and use[10] == claim[2]
            and use[11] == final[8] and number(use[12]) == 0, "Use/material/request/position linkage differs")
    require(use[5:7] == intent[3:5] and use[13] == previous[3] == intent[7]
            and use[15] == previous[10] and use[16] == current[10] and number(use[17]) == 1800,
            "Use lost exact definition, patient revision, HP or time pins")
    require(number(current[3]) == number(previous[3]) + 1 and number(current[10]) == number(previous[10]) + 40,
            "Ordinary dose must restore exactly the earned full40 deficit")
    require(number(new_production[4]) == number(old_production[4]) + 1
            and number(new_progression[4]) == number(old_progression[4]) + 1, "Material/resource owner revision differs")
    selected_ingredient = frame(intent[10], "world.care.ingredient", 5)
    ingredient = frame(claim[7], "production.ingredient", 8)
    kind = 1 if name in ("raw-self", "archive-helper") else 2
    require(number(ingredient[2]) == kind and ingredient[2:5] == [selected_ingredient[1], selected_ingredient[2], selected_ingredient[3]]
            and number(selected_ingredient[4]) == 1, "Exact selected dose or provenance differs")
    interval = frame(ingredient[7], "production.source-claim" if kind == 1 else "production.output-claim", 5 if kind == 1 else 6)
    require(number(interval[-1]) - number(interval[-2]) == 1, "Expected one consumed unit")
    check_dose_retirement(old_inventory, new_inventory, old_production, new_production, claim, ingredient, interval)
    require(use[3] == patient if name.endswith("self") else use[3] == bytes.fromhex(metadata["helperActorId"]), "Wrong actual supplier")
    if name.endswith("self"):
        require(use[7:10] == [b"", b"", b""] and len(frame(intent[5], "world.restorative.permission")) == 1,
                "Self-use invented permission")
    else:
        permission = frame(frame(old_ordinary[4], "care.permissions")[1], "care.permission.v1", 3)
        grant = frame(permission[1], "care.permission-grant.v1", 6)
        selected_permission = frame(intent[5], "world.restorative.permission", 4)
        require(permission[2] == b"" and grant[2:4] == [patient, action[5]] and use[7] == grant[1]
                and use[8] == digest(permission[1]) and number(use[9]) == 0
                and selected_permission[1:] == use[7:10], "Helper use lost active patient permission")
    evidence = frame(history_body(final[23])[6], "world.resource-evidence")[1:]
    selected_evidence = frame(evidence[-1], "world.resource-restorative-use.v1", 3)
    require(selected_evidence[1:] == [commands[0], use[1]], "Resource evidence lost exact command/use")
    if archive:
        require(len(evidence) == 1 and len(frame(history_body(initial[23])[6], "world.resource-evidence")) == 257,
                "Use did not continue the actual256-command resource boundary")
    return {"selectedReceipts": 1, "newUses": 1, "consumedDoses": 1, "archivePublished": archive,
            "consumptionId": claim[2].hex(), "hpBefore": number(previous[10]), "hpAfter": number(current[10])}


class HostRunner:
    def __init__(self, dotnet, host, output, report):
        self.command = [str(dotnet), str(host)]
        self.environment = dict(os.environ, DOTNET_ROOT=str(dotnet.parent))
        self.output, self.report = output, report

    def run(self, label, *arguments, expected_exit=0):
        command = self.command + list(map(str, arguments))
        record = {"label": label, "command": command, "timeoutSeconds": RUN_SECONDS}
        self.report["runs"].append(record)
        stdout, stderr = self.output / (label + ".stdout"), self.output / (label + ".stderr")
        record.update(stdout=str(stdout), stderr=str(stderr))
        started = time.monotonic()
        try:
            with stdout.open("wb") as out, stderr.open("wb") as err:
                done = subprocess.run(command, env=self.environment, stdout=out, stderr=err, timeout=RUN_SECONDS)
            record.update(seconds=time.monotonic() - started, exitCode=done.returncode)
            require(record["seconds"] <= RUN_SECONDS, label + " exceeded its deadline")
            require(done.returncode == expected_exit and not stderr.read_bytes(), label + " returned unexpected output/status")
            text = stdout.read_text()
            if expected_exit:
                require(text.startswith("Inspection failed:"), label + " did not report the expected refusal")
                return None
            return json.loads(text)
        except subprocess.TimeoutExpired:
            record.update(timedOut=True, seconds=time.monotonic() - started)
            raise

    def kill(self, label, path, batch, boundary):
        command = self.command + ["--probe-world-step-crash", str(path), str(batch), boundary]
        stdout, stderr = self.output / (label + ".stdout"), self.output / (label + ".stderr")
        record = {"label": label, "command": command, "boundary": boundary,
                  "signalTimeoutSeconds": RUN_SECONDS, "killTimeoutSeconds": KILL_SECONDS,
                  "stdout": str(stdout), "stderr": str(stderr)}
        self.report["crashes"].append(record)
        messages = queue.Queue()
        with stdout.open("w") as out, stderr.open("wb") as err:
            process = subprocess.Popen(command, env=self.environment, stdout=subprocess.PIPE, stderr=err, text=True)
            record["pid"] = process.pid
            def receive():
                line = process.stdout.readline()
                out.write(line)
                out.flush()
                messages.put(line)
            reader = threading.Thread(target=receive, daemon=True)
            started = time.monotonic()
            reader.start()
            try:
                line = messages.get(timeout=RUN_SECONDS)
                record.update(signalSeconds=time.monotonic() - started, notification=line)
                require(record["signalSeconds"] <= RUN_SECONDS, "Boundary signal exceeded its deadline")
                expected = "BeforeCommit" if boundary == "before" else "AfterCommit"
                require(json.loads(line) == {"Boundary": expected}, "Host did not reach the selected transaction boundary")
                require(process.poll() is None, "Crash target already exited")
                started = time.monotonic()
                process.kill()
                record.update(exitCode=process.wait(timeout=KILL_SECONDS), killSeconds=time.monotonic() - started)
                require(record["exitCode"] == -signal.SIGKILL and record["killSeconds"] <= KILL_SECONDS,
                        "Target was not killed at the requested boundary")
            finally:
                if process.poll() is None:
                    process.kill()
                    process.wait(timeout=KILL_SECONDS)
                reader.join(timeout=1)
                require(not reader.is_alive(), "Boundary reader failed to settle after target exit")
                out.write(process.stdout.read())
                process.stdout.close()
        require(not stderr.read_bytes(), "Crash target emitted stderr")


def check_report(report, expected, source_head=None):
    values = root(expected)
    require(report["ProfileVersion"] == 26 and report["StateHash"].lower() == digest(expected).hex(), "Host reported a different root")
    require(report.get("Revision", report.get("RecordedRevision")) == number(values[8]), "Host reported a different revision")
    require(report["AdvancingSteps"] == number(values[9]), "Host reported different earned time")
    if source_head is not None:
        require(report["SourceHeadRevision"] == number(root(source_head)[8])
                and report["SourceHeadHash"].lower() == digest(source_head).hex(), "Recorded reader lost its source-head pin")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("dotnet", "host", "fixtures", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    dotnet, host, fixtures, output = (getattr(args, name).resolve() for name in ("dotnet", "host", "fixtures", "output"))
    require(args.dotnet.is_absolute() and args.host.is_absolute(), "Pass absolute dotnet and Host paths")
    require(dotnet.is_file() and host.is_file() and fixtures.is_dir(), "Required runtime or fixtures are absent")
    require(not output.is_relative_to(fixtures) and not fixtures.is_relative_to(output), "Keep evidence output separate from immutable fixtures")
    execution_paths = [host, host.parent / "OpenFantasy.Core.dll", host.parent / "OpenFantasy.Hosting.dll",
                       host.with_suffix(".deps.json"), host.with_suffix(".runtimeconfig.json"),
                       Path(__file__).resolve(), dotnet]
    require(all(path.is_file() for path in execution_paths), "An execution dependency is absent")
    execution_pins = {str(path): digest(path.read_bytes()).hex() for path in execution_paths}
    output.mkdir(parents=True, exist_ok=False)
    originals = file_hashes(fixtures)
    report = {"status": "running", "startedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "runs": [], "crashes": [], "fixtures": {}, "completedCases": [],
              "fixturesDirectory": str(fixtures), "hostSha256": digest(host.read_bytes()).hex(),
              "executionPins": execution_pins,
              "limitations": ["Expected gameplay roots originate in the managed fixture; this checks storage and recovery, not independent gameplay laws.",
                              "Process SIGKILL qualification does not establish power-loss, hardware, throughput, native UI, or whole-game acceptance."]}
    runner = HostRunner(dotnet, host, output, report)
    try:
        for name in FIXTURES:
            selected = fixtures / name
            metadata = json.loads((selected / "metadata.json").read_text())
            before, after, batch = ((selected / filename).read_bytes() for filename in ("before.ofc", "after.ofc", "batch.ofc"))
            require(metadata["fixture"] == name, "Fixture metadata name differs")
            for label, encoded in (("before", before), ("after", after)):
                require(metadata[label + "Revision"] == number(root(encoded)[8])
                        and metadata[label + "Hash"] == digest(encoded).hex(), "Fixture metadata root differs")
            fixture_copies = []
            for label in ("before", "after"):
                path = output / (name + "-expected-" + label + ".sqlite3")
                copy_closed(selected / (label + ".sqlite3"), path)
                fixture_copies.append(database(path))
            before_db, after_db = fixture_copies
            check_database(before_db, before, metadata["archiveCountBefore"])
            check_database(after_db, after, metadata["archiveCountAfter"])
            report["fixtures"][name] = check_selected_step(before, after, batch, before_db, after_db, name, metadata)
            if name == "archive-helper":
                require(metadata["newArchiveOrdinal"] == number(after_db["world_resource_segments"][-1][2]), "Fixture archive ordinal differs")
            for boundary in ("normal", "before", "after"):
                label = name + "-" + boundary
                path = output / (label + ".sqlite3")
                copy_closed(selected / "before.sqlite3", path)
                if boundary == "normal":
                    check_report(runner.run(label, "--execute-world-step", path, selected / "batch.ofc"), after)
                else:
                    runner.kill(label, path, selected / "batch.ofc", boundary)
                    retained = before if boundary == "before" else after
                    check_report(runner.run(label + "-reopen", "--inspect-recorded-world", path, number(root(retained)[8])), retained, retained)
                    require(database(path) == (before_db if boundary == "before" else after_db), "Killed transaction retained a partial publication")
                    if boundary == "before":
                        check_report(runner.run(label + "-retry", "--execute-world-step", path, selected / "batch.ofc"), after)
                require(database(path) == after_db, "Committed database differs from complete expected tables")
                runner.run(label + "-duplicate", "--execute-world-step", path, selected / "batch.ofc", expected_exit=1)
                require(database(path) == after_db, "Duplicate replay altered durable effects or receipts")
                check_report(runner.run(label + "-retained-before", "--inspect-recorded-world", path, number(root(before)[8])), before, after)
                require(database(path) == after_db, "Recorded replay changed durable tables")
                report["completedCases"].append({"fixture": name, "boundary": boundary, "database": str(path)})
            # A complete root cannot substitute for the selected durable receipt or identity.
            for corruption in ("receipt", "identity", *(["archive"] if name == "archive-helper" else [])):
                path = output / (name + "-missing-" + corruption + ".sqlite3")
                copy_closed(selected / "after.sqlite3", path)
                with closing(sqlite3.connect(path)) as connection:
                    if corruption == "receipt":
                        cursor = connection.execute("DELETE FROM world_command_outcomes WHERE command_id=?", (bytes.fromhex(metadata["commandId"]),))
                    elif corruption == "identity":
                        cursor = connection.execute("DELETE FROM world_occupied_identity WHERE identity=?", (bytes.fromhex(metadata["commandId"]),))
                    else:
                        cursor = connection.execute("DELETE FROM world_resource_segments")
                    require(cursor.rowcount == 1, "Corruption must remove exactly the selected retained record")
                    connection.commit()
                changed = path.read_bytes()
                runner.run(name + "-refuse-missing-" + corruption, "--inspect-recorded-world", path,
                           number(root(after)[8]), expected_exit=1)
                require(path.read_bytes() == changed, "Refused recovery modified the corrupted save")
        report.update(status="passed", externalKills=len(report["crashes"]), completeFixturePairs=len(FIXTURES))
    except BaseException as error:
        report.update(status="failed", error=repr(error), traceback=traceback.format_exc())
    finally:
        report["fixturesUnchanged"] = originals == file_hashes(fixtures)
        report["executionFinalPins"] = {str(path): digest(path.read_bytes()).hex() if path.is_file() else None
                                        for path in execution_paths}
        report["executionUnchanged"] = execution_pins == report["executionFinalPins"]
        report["hostUnchanged"] = report["hostSha256"] == report["executionFinalPins"][str(host)]
        if not report["fixturesUnchanged"] or not report["hostUnchanged"] or not report["executionUnchanged"]:
            report["status"] = "failed"
        report["finishedUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        report["artifacts"] = file_hashes(output)
        (output / "result.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps({key: report[key] for key in ("status", "fixturesUnchanged", "hostUnchanged", "executionUnchanged")}))
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
