"""Qualify coordinated publication using complete ledgers and externally terminated Host processes.

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
    require(number(values[1]) == 27, "Expected explicit coordinated profile27")
    return values


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
        require(world != branch and len(world) == len(branch) == 16, "Bad scope")
    return len(archives)


def check_report(report, expected, source_head=None):
    values = root(expected)
    require(report["ProfileVersion"] == 27 and report["StateHash"].lower() == digest(expected).hex(), "Wrong reported root")
    require(report.get("Revision", report.get("RecordedRevision")) == number(values[8]), "Wrong reported revision")
    require(report["AdvancingSteps"] == number(values[9]), "Wrong earned time")
    if source_head is not None:
        require(report["SourceHeadRevision"] == number(root(source_head)[8])
                and report["SourceHeadHash"].lower() == digest(source_head).hex(), "Wrong source-head pin")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("dotnet", "host", "fixtures", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    dotnet, host, fixtures, output = (getattr(args, name).resolve() for name in ("dotnet", "host", "fixtures", "output"))
    require(dotnet.is_file() and host.is_file() and fixtures.is_dir(), "Required runtime/fixtures absent")
    require(not output.exists(), "Keep every prior run; output must be new")
    require(not output.is_relative_to(fixtures) and not fixtures.is_relative_to(output), "Separate evidence from fixtures")
    output.mkdir(parents=True)
    originals, binaries = hashes(fixtures), execution_hashes(host.parent)
    report = {"status": "running", "startedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "runs": [], "crashes": [], "completedCases": [], "fixturesDirectory": str(fixtures),
              "fixtureHashes": originals, "executionHashes": binaries,
              "limitations": ["Managed fixture supplies expected gameplay; this is storage/atomicity qualification.",
                              "No native UI, device, latency or general production throughput acceptance."]}
    runner = HostRunner(dotnet, host, output, report)
    try:
        before, after, batch = (fixtures.joinpath(name).read_bytes() for name in ("before.ofc", "after.ofc", "batch.ofc"))
        metadata = json.loads((fixtures / "metadata.json").read_text())
        require(metadata["actualDoses"] > 256 and metadata["profileVersion"] == 27, "Wrong finite-supply journey")
        before_db = database(fixtures / "before.sqlite3", sealed_fixture=True)
        after_db = database(fixtures / "after.sqlite3", sealed_fixture=True)
        old_count, new_count = check_database(before_db, before), check_database(after_db, after)
        require(new_count == old_count + 1 and old_count >= 1, "Expected a later joint cut")
        require(number(root(after)[8]) == number(root(before)[8]) + 1, "Selected transaction must publish once")
        require(after_db["world_commits"][-1][4] == batch, "Fixture published another batch")
        require(after_db["world_material_care_segments"][-1][-1] == before, "Archive is not exact prior root")
        report.update(archiveCountBefore=old_count, archiveCountAfter=new_count)
        for boundary in ("normal", "before", "after"):
            path = output / (boundary + ".sqlite3")
            shutil.copy2(fixtures / "before.sqlite3", path)
            if boundary == "normal":
                check_report(runner.run(boundary, "--execute-world-step", path, fixtures / "batch.ofc"), after)
            else:
                runner.kill(boundary, path, fixtures / "batch.ofc", boundary)
                retained = before if boundary == "before" else after
                check_report(runner.run(boundary + "-reopen", "--inspect-recorded-world", path,
                                        number(root(retained)[8])), retained, retained)
                require(database(path) == (before_db if boundary == "before" else after_db),
                        "Externally terminated transaction retained a mixed ledger")
                if boundary == "before":
                    check_report(runner.run(boundary + "-retry", "--execute-world-step", path, fixtures / "batch.ofc"), after)
            require(database(path) == after_db, "Recovered transaction differs in an authoritative table")
            runner.run(boundary + "-duplicate", "--execute-world-step", path, fixtures / "batch.ofc", expected_exit=1)
            require(database(path) == after_db, "Duplicate command changed authoritative state")
            check_report(runner.run(boundary + "-history", "--inspect-recorded-world", path,
                                    number(root(before)[8])), before, after)
            require(database(path) == after_db, "Historical retrieval changed state")
            report["completedCases"].append({"boundary": boundary, "database": str(path)})
        report["status"] = "passed"
    except Exception:
        report["status"] = "failed"
        report["error"] = traceback.format_exc()
        raise
    finally:
        report["fixturesUnchanged"] = originals == hashes(fixtures)
        report["executionUnchanged"] = binaries == execution_hashes(host.parent)
        if not report["fixturesUnchanged"] or not report["executionUnchanged"]:
            report["status"] = "failed"
        report["externalKills"] = sum(item.get("exitCode") == -signal.SIGKILL for item in report["crashes"])
        (output / "result.json").write_text(json.dumps(report, indent=2) + "\n")
    require(report["status"] == "passed", "Qualification or pin check failed")
    print(json.dumps({key: report[key] for key in ("status", "externalKills", "fixturesUnchanged", "executionUnchanged")}))


if __name__ == "__main__":
    main()
