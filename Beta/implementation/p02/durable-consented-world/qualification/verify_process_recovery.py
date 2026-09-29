"""Qualify paired consent publication using complete ledgers and externally terminated Host processes.

Gameplay expectations come from actual managed world transactions. This independently checks atomic
storage and exact recovery, without treating a fault exception or a hash alone as process-kill proof.
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
TABLES = {
    "world_genesis": "singleton", "world_head": "singleton", "world_commits": "sequence",
    "world_command_outcomes": "principal,command_id", "world_occupied_identity": "identity",
    "world_checkpoints": "sequence", "world_material_care_segments": "publication_sequence",
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


def root(data):
    values = frame(data, "world.snapshot", 24)
    require(number(values[1]) == 28, "Expected explicit consented profile28")
    return values


def world_scope(data):
    inventory = frame(root(data)[2], "authority.counted-state", 9)
    custody = frame(inventory[3], "custody.snapshot.v1", 6)
    world, branch = custody[1:3]
    require(world != branch and len(world) == len(branch) == 16, "Invalid root scope")
    return [world, branch]


def hashes(directory):
    return {str(path.relative_to(directory)): digest(path.read_bytes()).hex()
            for path in sorted(directory.rglob("*")) if path.is_file()}


def execution_hashes(directory):
    return {path.name: digest(path.read_bytes()).hex() for path in sorted(directory.iterdir())
            if path.is_file() and path.suffix in (".dll", ".json", ".dylib")}


def database(path, sealed_fixture=False):
    if sealed_fixture:
        wal = Path(str(path) + "-wal")
        require(not wal.exists() or wal.stat().st_size == 0,
                "An immutable fixture must be a complete standalone backup without pending WAL")
    # Read-only WAL connections can still create or update shared-memory sidecars. Sealed backup
    # inputs require immutable reads; recovered outputs still read their actual WAL normally.
    uri = path.as_uri() + ("?mode=ro&immutable=1" if sealed_fixture else "?mode=ro")
    with closing(sqlite3.connect(uri, uri=True)) as connection:
        require(connection.execute("PRAGMA integrity_check").fetchall() == [("ok",)], "SQLite integrity failed")
        require(connection.execute("PRAGMA foreign_key_check").fetchall() == [], "SQLite foreign keys failed")
        require(connection.execute("PRAGMA user_version").fetchone() == (8,), "Wrong world schema")
        names = {row[0] for row in connection.execute("SELECT name FROM sqlite_master WHERE type='table'")}
        require(names == set(TABLES), "Unexpected or missing authoritative table")
        return {table: connection.execute("SELECT * FROM " + table + " ORDER BY " + order).fetchall()
                for table, order in TABLES.items()}


def check_database(value, expected):
    state = root(expected)
    scope = world_scope(expected)
    require(world_scope(value["world_genesis"][0][1]) == scope, "Genesis scope differs from head")
    require(value["world_head"] == [(1, state[8], state[9], expected, digest(expected))], "Wrong complete head")
    rows = value["world_commits"]
    require(len(rows) == number(state[8]), "Journal is not complete")
    prior = digest(value["world_genesis"][0][1])
    for sequence, row in enumerate(rows, 1):
        require(number(row[0]) == sequence and row[6] == prior, "Discontinuous commit chain")
        prior = row[7]
    require(prior == digest(expected), "Journal head hash differs")
    archives = value["world_material_care_segments"]
    for ordinal, row in enumerate(archives):
        world, branch, index, boundary, publication, hash_value, encoded = row
        closing = root(encoded)
        require(number(index) == ordinal and number(publication) == number(boundary) + 1, "Bad archive order")
        require(closing[8] == boundary and digest(encoded) == hash_value, "Bad complete closing bytes")
        commit = rows[number(publication) - 1]
        require(commit[6] == hash_value, "Archive differs from publishing prior hash")
        require([world, branch] == scope == world_scope(encoded), "Archive/root/genesis scope disagreement")
    return len(archives)


def check_report(report, expected, source_head=None):
    values = root(expected)
    require(report["ProfileVersion"] == 28 and report["StateHash"].lower() == digest(expected).hex(), "Wrong reported root")
    require(report.get("Revision", report.get("RecordedRevision")) == number(values[8]), "Wrong reported revision")
    require(report["AdvancingSteps"] == number(values[9]), "Wrong earned time")
    if source_head is not None:
        require(report["SourceHeadRevision"] == number(root(source_head)[8])
                and report["SourceHeadHash"].lower() == digest(source_head).hex(), "Wrong source-head pin")


def check_pair(value, batch, expected):
    state = root(expected)
    step = frame(batch, "world.step-batch", 10)
    require(number(step[1]) == 28 and number(step[5]) + 1 == number(state[8]), "Wrong pair/profile revision")
    require(step[3:5] == world_scope(expected), "Batch scope differs from its complete root")
    encoded = frame(step[9], "world.step-actions")[1:]
    require(len(encoded) == 2, "Process probe requires exactly two independently recorded actions")
    commands = [frame(item, "world.actor-action", 10) for item in encoded]
    tags = []
    for command in commands:
        intent = command[9]
        length = struct.unpack_from(">I", intent, 12)[0]
        tags.append(intent[16:16 + length])
    require(set(tags) == {b"world.action.material-offer", b"world.action.material-acceptance"}, "Expected an offer/acceptance pair")
    require(commands[0][2] != commands[1][2] and commands[0][3] != commands[1][3]
            and commands[0][5] != commands[1][5], "Pair needs independent command, principal and actor identities")
    require(value["world_commits"][-1][4] == batch, "Published another batch")
    receipt_rows = {row[1]: row for row in value["world_command_outcomes"]}
    identity_rows = {row[0]: row for row in value["world_occupied_identity"]}
    outcomes = frame(value["world_commits"][-1][5], "world.action-outcomes")[1:]
    require(len(outcomes) == 2, "The publication must retain both exact outcomes")
    for command, canonical in zip(commands, encoded):
        require(command[6:8] == step[3:5], "Command and batch scope disagree")
        row = receipt_rows.get(command[2])
        require(row is not None and row[:4] == (command[3], command[2], state[8], canonical), "Wrong exact command row")
        receipt = frame(row[4], "world.action-receipt", 10)
        require(row[4] in outcomes and receipt[2:8] == [command[2], command[3], command[6], command[7], digest(canonical), state[8]]
                and number(receipt[8]) == 1 and number(receipt[9]) == 0, "Wrong successful paired receipt")
        require(command[2] in identity_rows and identity_rows[command[2]][1:] == (state[8], 2),
                "Missing permanent command identity or incorrect role")
    return [command[2].hex() for command in commands]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("dotnet", "host", "before-db", "after-db", "batch", "after-root", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    dotnet, host, before_path, reference_path, batch_path, after_path, output = (
        getattr(args, name).resolve() for name in ("dotnet", "host", "before_db", "after_db", "batch", "after_root", "output"))
    require(all(path.is_file() for path in (dotnet, host, before_path, reference_path, batch_path, after_path)),
            "Required executable or fixture absent")
    require(not output.exists(), "Preserve every prior run")
    output.mkdir(parents=True)
    fixtures = {str(path): digest(path.read_bytes()).hex() for path in (before_path, reference_path, batch_path, after_path)}
    binaries = execution_hashes(host.parent)
    report = {"status": "running", "runs": [], "crashes": [], "completedCases": [],
              "startedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "fixtureHashes": fixtures, "executionHashes": binaries,
              "limitations": ["Privileged selected-batch atomicity, not public two-party transport or asynchronous offers.",
                              "Managed fixture supplies gameplay expectations; ledger comparison independently checks publication.",
                              "SIGKILL is not power-loss, native, retained-save migration or production-scale acceptance."]}
    runner = HostRunner(dotnet, host, output, report)
    try:
        before_db = database(before_path, sealed_fixture=True)
        require(len(before_db["world_head"]) == 1, "Missing prior head")
        before = before_db["world_head"][0][3]
        after, batch = after_path.read_bytes(), batch_path.read_bytes()
        old_count = check_database(before_db, before)
        reference_db = database(reference_path, sealed_fixture=True)
        require(check_database(reference_db, after) == old_count + 1, "Managed fixture must publish one actual joint archive")
        check_pair(reference_db, batch, after)
        require(number(root(after)[8]) == number(root(before)[8]) + 1, "Pair must publish once")
        # Normal execution must match the separate managed fixture in every authoritative table.
        # Both killed cases then repeat that exact comparison after actual process termination.
        normal = output / "normal.sqlite3"
        shutil.copy2(before_path, normal)
        check_report(runner.run("normal", "--execute-world-step", normal, batch_path), after)
        after_db = database(normal)
        require(after_db == reference_db, "Normal Host publication differs from the managed fixture's complete ledger")
        new_count = check_database(after_db, after)
        require(new_count == old_count + 1, "Pair must actually publish one joint archive")
        require(after_db["world_material_care_segments"][-1][-1] == before, "Archive must be the exact prior root")
        pair_ids = check_pair(after_db, batch, after)
        before_ids = {row[0].hex() for row in before_db["world_occupied_identity"]}
        require(not before_ids.intersection(pair_ids), "Pair command identities were already occupied")
        report.update(archiveCountBefore=old_count, archiveCountAfter=new_count, pairCommandIds=pair_ids)
        for boundary in ("normal", "before", "after"):
            path = output / (boundary + ".sqlite3")
            if boundary != "normal":
                shutil.copy2(before_path, path)
                runner.kill(boundary, path, batch_path, boundary)
                retained = before if boundary == "before" else after
                check_report(runner.run(boundary + "-reopen", "--inspect-recorded-world", path,
                                        number(root(retained)[8])), retained, retained)
                require(database(path) == (before_db if boundary == "before" else after_db), "Mixed ledger after external process loss")
                if boundary == "before":
                    check_report(runner.run(boundary + "-retry", "--execute-world-step", path, batch_path), after)
            require(database(path) == after_db, "Recovered authoritative tables differ")
            runner.run(boundary + "-duplicate", "--execute-world-step", path, batch_path, expected_exit=1)
            require(database(path) == after_db, "Duplicate command changed state")
            check_report(runner.run(boundary + "-history", "--inspect-recorded-world", path,
                                    number(root(before)[8])), before, after)
            require(database(path) == after_db, "Historical retrieval changed state")
            check_pair(database(path), batch, after)
            report["completedCases"].append({"boundary": boundary, "database": str(path)})
        report["status"] = "passed"
    except Exception:
        report["status"] = "failed"
        report["error"] = traceback.format_exc()
        raise
    finally:
        report["fixturesUnchanged"] = all(digest(Path(path).read_bytes()).hex() == value for path, value in fixtures.items())
        report["executionUnchanged"] = binaries == execution_hashes(host.parent)
        if not report["fixturesUnchanged"] or not report["executionUnchanged"]:
            report["status"] = "failed"
        report["externalKills"] = sum(item.get("exitCode") == -signal.SIGKILL for item in report["crashes"])
        (output / "result.json").write_text(json.dumps(report, indent=2) + "\n")
    require(report["status"] == "passed", "Qualification failed")
    print(json.dumps({key: report[key] for key in ("status", "externalKills", "fixturesUnchanged", "executionUnchanged")}))


if __name__ == "__main__":
    main()
