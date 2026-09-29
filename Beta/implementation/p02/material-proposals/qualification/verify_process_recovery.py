"""Qualify saved proposal publication using complete ledgers and externally terminated Host processes.

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
    require(number(values[1]) == 30, "Expected explicit saved-proposal profile30")
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
    require(report["ProfileVersion"] == 30 and report["StateHash"].lower() == digest(expected).hex(), "Wrong reported root")
    require(report.get("Revision", report.get("RecordedRevision")) == number(values[8]), "Wrong reported revision")
    require(report["AdvancingSteps"] == number(values[9]), "Wrong earned time")
    if source_head is not None:
        require(report["SourceHeadRevision"] == number(root(source_head)[8])
                and report["SourceHeadHash"].lower() == digest(source_head).hex(), "Wrong source-head pin")



def encode_frame(tag, *fields):
    values = [tag.encode(), *fields]
    return b"OFC\0" + struct.pack(">II", 1, len(values)) + b"".join(
        struct.pack(">I", len(value)) + value for value in values)


def spawned(cause, ordinal):
    return digest(encode_frame("entity.spawned", cause, ordinal.to_bytes(8, "big")))[:16]


def proposals(data):
    joint = frame(root(data)[22], "world.coordinated-proposal-care.v1", 6)
    book = frame(joint[5], "world.material-proposals.v1", 3)
    return (frame(book[1], "world.material-proposals.open")[1:],
            frame(book[2], "world.material-proposals.outcomes")[1:])


def check_submission(before_db, value, batch, expected, command_bytes, receipt_bytes, metadata):
    before = before_db["world_head"][0][3]
    state = root(expected)
    step = frame(batch, "world.step-batch", 10)
    require(number(step[1]) == 30 and number(step[5]) + 1 == number(state[8]), "Wrong proposal/profile revision")
    require(step[3:5] == world_scope(expected), "Batch scope differs from its root")
    require(frame(step[9], "world.step-actions")[1:] == [command_bytes], "Expected one actual submitted command")
    command = frame(command_bytes, "world.actor-action", 10)
    require(number(command[1]) == 16 and command[6:8] == step[3:5], "Wrong command version or scope")
    require(value["world_commits"][-1][4] == batch, "Published another batch")
    require(value["world_commits"][:-1] == before_db["world_commits"], "Earlier journal changed")
    prior_receipts = {(r[0], r[1]): r for r in before_db["world_command_outcomes"]}
    receipts = {(r[0], r[1]): r for r in value["world_command_outcomes"]}
    require(all(receipts.get(key) == row for key, row in prior_receipts.items()), "Earlier creation/command receipt changed")
    key = (command[3], command[2])
    require(set(receipts) - set(prior_receipts) == {key}, "Publication must add just the submitted command")
    require(receipts[key] == (command[3], command[2], state[8], command_bytes, receipt_bytes), "Wrong exact command/receipt row")
    receipt = frame(receipt_bytes, "world.action-receipt", 10)
    require(receipt[2:8] == [command[2], command[3], command[6], command[7], digest(command_bytes), state[8]]
            and number(receipt[8]) == 1 and number(receipt[9]) == 0, "Expected successful exact terminal receipt")
    require(frame(value["world_commits"][-1][5], "world.action-outcomes")[1:] == [receipt_bytes], "Wrong journal outcome")
    before_open, before_closed = proposals(before)
    after_open, after_closed = proposals(expected)
    length = struct.unpack_from(">I", command[9], 12)[0]
    tag = command[9][16:16 + length].decode()
    if tag == "world.action.material-proposal-create":
        intent = frame(command[9], tag, 4)
        require(len(after_open) == len(before_open) + 1 and after_closed == before_closed,
                "Creation must add one offer without closing another")
        added = [entry for entry in after_open if entry not in before_open]
        require(len(added) == 1 and all(entry in after_open for entry in before_open), "Creation lost an earlier offer")
        proposal = frame(added[0], "world.material-proposal.v1", 7)
        grant = frame(proposal[1], "authority.consented-material.command", 6)
        terms = frame(intent[2], "authority.consented-material.terms", 14)
        require(grant[2:5] == command[2:5] and grant[5] == intent[2], "Saved grant differs from actual sender submission")
        require(proposal[2:4] == command[3:5] and proposal[4:6] == state[8:10] and proposal[6] == intent[3],
                "Wrong saved delivery, creation boundary or expiry")
        require(terms[4] == command[5] and terms[2:4] == step[3:5], "Wrong creating participant or scope")
        require(root(before)[2] == state[2] and root(before)[12] == state[12], "Offer creation changed material owners")
        entity = spawned(command[2], 0x4D500001)
        kind = "creation"
    elif tag == "world.action.material-proposal-accept":
        intent = frame(command[9], tag, 6)
        require(len(after_closed) == len(before_closed) + 1 and after_closed[:-1] == before_closed,
                "Acceptance must append exactly one outcome")
        outcome = frame(after_closed[-1], "world.material-proposal-outcome.v1", 6)
        require(outcome[1] in before_open and number(outcome[2]) == 1 and outcome[3] == state[8]
                and outcome[4] == command[2], "Acceptance did not settle its original open proposal")
        require(after_open == [entry for entry in before_open if entry != outcome[1]], "Acceptance changed other open offers")
        proposal = frame(outcome[1], "world.material-proposal.v1", 7)
        grant = frame(proposal[1], "authority.consented-material.command", 6)
        require(intent[2] == spawned(grant[2], 0x4D500001) and intent[3] == digest(proposal[1])
                and intent[4] == grant[5], "Acceptance changed its exact saved grant")
        terms = frame(intent[4], "authority.consented-material.terms", 14)
        require(terms[5] == command[5] and terms[2:4] == step[3:5], "Wrong accepting participant or scope")
        settlement = frame(outcome[5], "world.material-proposal-settlement.v1", 14)
        approval = frame(settlement[2], "authority.consented-material.acceptance", 8)
        require(number(settlement[1]) == 0 and approval[2:5] == command[2:5]
                and approval[5:] == [grant[2], intent[3], intent[4]]
                and settlement[3:5] == command[3:5], "Wrong actual recipient approval evidence")
        require(number(settlement[9]) - number(settlement[8]) == number(terms[12]), "Wrong moved quantity")
        entity = spawned(grant[2], 0)
        require(settlement[5] == entity, "The qualification fixture must execute its pinned partial split")
        require((grant[3], grant[2]) in prior_receipts, "Missing immutable creation receipt before acceptance")
        kind = "acceptance"
    else:
        raise AssertionError("The process fixture must create or accept a saved offer")
    before_ids = {row[0]: row for row in before_db["world_occupied_identity"]}
    ids = {row[0]: row for row in value["world_occupied_identity"]}
    require(all(ids.get(identity) == row for identity, row in before_ids.items()), "Earlier permanent identities changed")
    require(number(receipt[1]) == 1, "Wrong receipt format version")
    expected_ids = {step[2]: 1, command[2]: 2, digest(receipt_bytes)[:16]: 3, entity: 4}
    require(len(expected_ids) == 4 and set(ids) - set(before_ids) == set(expected_ids), "Wrong newly occupied identities")
    for identity, role in expected_ids.items():
        require(ids[identity] == (identity, state[8], role), "Wrong permanent identity role or publication")
    role_names = {"CommitCause": 1, "Command": 2, "Result": 3, "SpawnedEntity": 4}
    require({bytes.fromhex(row["identity"]): role_names[row["kind"]] for row in metadata["identities"]} == expected_ids,
            "Managed identity metadata differs from independently decoded roles")
    require(metadata["profileVersion"] == 30 and metadata["schemaVersion"] == 8
            and metadata["beforeRevision"] == number(root(before)[8]) and metadata["beforeHash"] == digest(before).hex()
            and metadata["afterRevision"] == number(state[8]) and metadata["afterHash"] == digest(expected).hex(),
            "Fixture metadata differs from actual roots")
    return {"operation": kind, "commandId": command[2].hex(), "newIdentityCount": 4}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("dotnet", "host", "fixture", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    dotnet, host, fixture, output = (getattr(args, name).resolve() for name in ("dotnet", "host", "fixture", "output"))
    names = ["before.sqlite3", "after.sqlite3", "before.ofc", "after.ofc", "batch.ofc", "command.ofc", "receipt.ofc", "metadata.json"]
    require(dotnet.is_file() and host.is_file() and all((fixture / name).is_file() for name in names), "Required executable or fixture absent")
    require(not output.exists(), "Preserve every prior run")
    output.mkdir(parents=True)
    fixtures = {str(fixture / name): digest((fixture / name).read_bytes()).hex() for name in names}
    binaries = execution_hashes(host.parent)
    report = {"status": "running", "runs": [], "crashes": [], "completedCases": [],
              "startedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "fixtureHashes": fixtures, "executionHashes": binaries,
              "limitations": ["Actual selected-batch process loss, not a network handshake or native input.",
                              "Managed fixture supplies full gameplay expectations; independent parsing checks lifecycle, exact ledgers and identities.",
                              "SIGKILL is not hardware power loss, retained-save migration or production scale."]}
    runner = HostRunner(dotnet, host, output, report)
    try:
        before_path, batch_path = fixture / "before.sqlite3", fixture / "batch.ofc"
        before, after, batch, command, receipt = ((fixture / name).read_bytes() for name in
                                                  ("before.ofc", "after.ofc", "batch.ofc", "command.ofc", "receipt.ofc"))
        metadata = json.loads((fixture / "metadata.json").read_text())
        before_db, reference_db = database(before_path, True), database(fixture / "after.sqlite3", True)
        old_count, new_count = check_database(before_db, before), check_database(reference_db, after)
        require(new_count in (old_count, old_count + 1), "Unexpected archive publication count")
        if new_count != old_count:
            require(reference_db["world_material_care_segments"][-1][-1] == before, "Archive must contain the exact prior root")
        transition = check_submission(before_db, reference_db, batch, after, command, receipt, metadata)
        report.update(transition, archiveCountBefore=old_count, archiveCountAfter=new_count)
        normal = output / "normal.sqlite3"
        shutil.copy2(before_path, normal)
        check_report(runner.run("normal", "--execute-world-step", normal, batch_path), after)
        after_db = database(normal)
        require(after_db == reference_db, "Actual normal Host publication differs from the managed complete ledger")
        for boundary in ("normal", "before", "after"):
            path = output / (boundary + ".sqlite3")
            if boundary != "normal":
                shutil.copy2(before_path, path)
                runner.kill(boundary, path, batch_path, boundary)
                retained = before if boundary == "before" else after
                check_report(runner.run(boundary + "-reopen", "--inspect-recorded-world", path,
                                        number(root(retained)[8])), retained, retained)
                require(database(path) == (before_db if boundary == "before" else after_db), "Mixed ledger after process loss")
                if boundary == "before":
                    check_report(runner.run(boundary + "-retry", "--execute-world-step", path, batch_path), after)
            require(database(path) == after_db, "Recovered authoritative tables differ")
            runner.run(boundary + "-duplicate", "--execute-world-step", path, batch_path, expected_exit=1)
            require(database(path) == after_db, "Duplicate command changed state")
            check_report(runner.run(boundary + "-history", "--inspect-recorded-world", path,
                                    number(root(before)[8])), before, after)
            require(database(path) == after_db, "Historical retrieval changed state")
            check_submission(before_db, database(path), batch, after, command, receipt, metadata)
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
    print(json.dumps({key: report[key] for key in ("status", "operation", "externalKills", "fixturesUnchanged", "executionUnchanged")}))


if __name__ == "__main__":
    main()
