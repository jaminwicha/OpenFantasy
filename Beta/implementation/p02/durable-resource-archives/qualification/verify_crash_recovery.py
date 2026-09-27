"""Qualify schema7 archive/care publication with actual Host processes and SIGKILL.

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
FIXTURES = ("archive", "raw-care", "crafted-care")
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
    require(number(values[1]) == 25, "Expected exact profile25")
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
        care = frame(closing_root[22], "care.snapshot", 9)
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


def check_selected_step(before, after, batch, before_db, after_db, name):
    initial, final = root(before), root(after)
    step = frame(batch, "world.step-batch", 10)
    require(number(step[1]) == 25 and step[5:7] == initial[8:10], "Selected step basis differs")
    require(number(final[8]) == number(initial[8]) + 1, "Selected step does not publish exactly once")
    added = [row for row in after_db["world_commits"] if row[0] == final[8]]
    require(len(added) == 1 and added[0][4] == batch and added[0][6:8] == (digest(before), digest(after)),
            "Selected committed batch or hashes differ")
    commands = frame(step[9], "world.step-actions")[1:]
    outcomes = frame(added[0][5], "world.action-outcomes")[1:]
    selected_rows = [row for row in after_db["world_command_outcomes"] if row[2] == final[8]]
    require(len(commands) == len(outcomes) == len(selected_rows), "Selected receipt count differs")
    receipts = {frame(value, "world.action-receipt", 10)[2]: value for value in outcomes}
    for encoded in commands:
        action = frame(encoded, "world.actor-action", 10)
        receipt = receipts[action[2]]
        fields = frame(receipt, "world.action-receipt", 10)
        require(fields[3:6] == [action[3], *step[3:5]] and fields[6] == digest(encoded), "Receipt identity/hash differs")
        require(fields[7] == final[8] and number(fields[8]) == 1 and number(fields[9]) == 0,
                "Fixture selected action was not applied")
        require((action[3], action[2], final[8], encoded, receipt) in selected_rows, "Durable command receipt differs")
    prior_commands = set(before_db["world_command_outcomes"])
    require(set(after_db["world_command_outcomes"]) == prior_commands | set(selected_rows), "Earlier receipts changed")
    if name == "archive":
        require(len(after_db["world_resource_segments"]) == len(before_db["world_resource_segments"]) + 1,
                "Fixture did not publish one archive")
        require(after_db["world_resource_segments"][-1][6] == before, "Published archive is not exact prior root")
        return {"selectedReceipts": len(selected_rows), "archivePublished": True}
    old_care, new_care = (frame(value[22], "care.snapshot", 9) for value in (initial, final))
    require(old_care[7] == new_care[7], "Treatment altered prior acknowledgments")
    old_treatments = set(frame(old_care[8], "care.treatments")[1:])
    new_treatments = set(frame(new_care[8], "care.treatments")[1:])
    require(old_treatments < new_treatments and len(new_treatments - old_treatments) == 1, "Expected one new treatment")
    treatment = frame(next(iter(new_treatments - old_treatments)), "care.treatment.v1", 12)
    old_production, new_production = (frame(value[12], "production.snapshot", 9) for value in (initial, final))
    old_claims = set(frame(old_production[8], "production.consumed")[1:])
    new_claims = set(frame(new_production[8], "production.consumed")[1:])
    require(old_claims < new_claims and len(new_claims - old_claims) == 1, "Expected one consumed dose")
    claim = frame(next(iter(new_claims - old_claims)), "production.consumed-item", 8)
    require(claim[2] == treatment[4] and claim[3] == treatment[2] and treatment[5] == final[8],
            "Treatment/consumption/command linkage differs")
    ingredient = frame(claim[7], "production.ingredient", 8)
    kind = 1 if name == "raw-care" else 2
    require(number(ingredient[2]) == kind, "Wrong care dose provenance")
    interval = frame(ingredient[7], "production.source-claim" if kind == 1 else "production.output-claim",
                     5 if kind == 1 else 6)
    require(number(interval[-1]) - number(interval[-2]) == 1, "Treatment consumed other than one dose")
    return {"selectedReceipts": len(selected_rows), "newTreatments": 1, "consumedDoses": 1,
            "consumptionId": claim[2].hex(), "hpAfter": number(treatment[9])}


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
    require(report["ProfileVersion"] == 25 and report["StateHash"].lower() == digest(expected).hex(), "Host reported a different root")
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
    output.mkdir(parents=True, exist_ok=False)
    originals = file_hashes(fixtures)
    report = {"status": "running", "startedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "runs": [], "crashes": [], "fixtures": {}, "hostSha256": digest(host.read_bytes()).hex(),
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
            report["fixtures"][name] = check_selected_step(before, after, batch, before_db, after_db, name)
            if name == "archive":
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
        report.update(status="passed", externalKills=len(report["crashes"]), completeFixturePairs=len(FIXTURES))
    except BaseException as error:
        report.update(status="failed", error=repr(error), traceback=traceback.format_exc())
    finally:
        report["fixturesUnchanged"] = originals == file_hashes(fixtures)
        report["hostUnchanged"] = report["hostSha256"] == digest(host.read_bytes()).hex()
        if not report["fixturesUnchanged"] or not report["hostUnchanged"]:
            report["status"] = "failed"
        report["finishedUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        report["artifacts"] = file_hashes(output)
        (output / "result.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps({key: report[key] for key in ("status", "fixturesUnchanged", "hostUnchanged")}))
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
