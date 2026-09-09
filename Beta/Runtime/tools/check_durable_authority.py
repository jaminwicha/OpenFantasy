"""Qualify real local authority commits, abrupt process death and the Godot native dependency."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import queue
import struct
import subprocess
import sys
import tempfile
import threading


def receipt_at_first_commit(hex_value):
    raw = bytes.fromhex(hex_value)
    count = struct.unpack_from(">I", raw, 8)[0]
    position = 12
    fields = []
    for _ in range(count):
        length = struct.unpack_from(">I", raw, position)[0]
        position += 4
        fields.append(raw[position:position + length])
        position += length
    assert position == len(raw) and fields[0] == b"command.receipt"
    assert len(fields) == 10 and len(fields[7]) == 8
    fields[7] = struct.pack(">Q", 1)
    return (raw[:12] + b"".join(struct.pack(">I", len(value)) + value for value in fields)).hex()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", required=True, type=Path)
    parser.add_argument("--godot", type=Path)
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Debug")
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    host = root / "src/OpenFantasy.Host/bin" / args.configuration / "net10.0/OpenFantasy.Host.dll"
    fixture_path = root / "tests/OpenFantasy.Core.Tests/Fixtures/authority-v1.json"
    fixture = json.loads(fixture_path.read_text())
    expected_receipt = receipt_at_first_commit(fixture["appliedReceiptHex"])
    env = dict(os.environ, DOTNET_ROOT=str(args.dotnet.resolve().parent))
    env["PATH"] = str(args.dotnet.resolve().parent) + os.pathsep + env.get("PATH", "")
    report = dict(scope="Local counted authority; actual kill/reopen/retry and optional headless Godot native loading",
                  configuration=args.configuration, status="running", steps=[], crashCases=[],
                  fixtureSha256=hashlib.sha256(fixture_path.read_bytes()).hexdigest())

    def run(name, command, success=True):
        result = subprocess.run([str(value) for value in command], env=env, cwd=root,
                                capture_output=True, text=True, timeout=30)
        report["steps"].append(dict(name=name, exitCode=result.returncode, stdout=result.stdout, stderr=result.stderr))
        if (result.returncode == 0) != success:
            raise RuntimeError(name + ": unexpected exit\n" + result.stdout + result.stderr)
        if success and ("ERROR:" in result.stdout + result.stderr):
            raise RuntimeError(name + ": engine reported an error")
        values = [json.loads(line) for line in result.stdout.splitlines() if line.startswith('{"Contract":')]
        if success:
            if len(values) != 1 or values[0].get("Contract") != "durable-counted-authority-v1":
                raise RuntimeError(name + ": missing or duplicated result")
            return values[0]

    def cli(name, *arguments, success=True):
        return run(name, [args.dotnet, host, *arguments], success)

    def assert_state(value, committed, receipt=False):
        assert value["CommitSequence"] == int(committed)
        assert value["StateHash"] == fixture["candidateHash" if committed else "originalHash"]
        assert value["CustodyRevision"] == (8 if committed else 7)
        database = value["Database"]
        assert database["Version"] == "3.53.4" and database["SourceId"]
        assert database["JournalMode"] == "wal" and database["Synchronous"] == 2
        assert database["ForeignKeys"] == 1 and database["LockingMode"] == "exclusive"
        if receipt:
            assert value["ReceiptHex"] == expected_receipt

    try:
        with tempfile.TemporaryDirectory(prefix="openfantasy-durable-") as temporary:
            folder = Path(temporary)
            genesis = folder / "genesis.bin"
            command = folder / "transfer.bin"
            genesis.write_bytes(bytes.fromhex(fixture["originalHex"]))
            command.write_bytes(bytes.fromhex(fixture["commandHex"]))
            principal, session = f"{21:032x}", f"{22:032x}"
            database = folder / "normal.sqlite"
            assert_state(cli("create", "--create-world", database, genesis), False)
            cli("create-refuses-existing", "--create-world", database, genesis, success=False)
            first = cli("transfer", "--execute-local-command", database, command, principal, session)
            assert_state(first, True, receipt=True)
            retry = cli("restart-retry", "--execute-local-command", database, command, principal, session)
            assert retry == first
            cli("retry-requires-current-delivery", "--execute-local-command", database, command,
                principal, f"{999:032x}", success=False)
            assert_state(cli("unchanged-after-refused-delivery", "--inspect-world", database), True)

            for boundary in ("before", "after"):
                database = folder / (boundary + ".sqlite")
                assert_state(cli(boundary + "-create", "--create-world", database, genesis), False)
                process = subprocess.Popen([str(args.dotnet), str(host), "--probe-commit-crash",
                    str(database), str(command), principal, session, boundary], cwd=root, env=env,
                    stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
                marker_queue = queue.Queue()
                reader = threading.Thread(target=lambda: marker_queue.put(process.stdout.readline()), daemon=True)
                reader.start()
                try:
                    marker = marker_queue.get(timeout=15)
                    expected = "BeforeCommit" if boundary == "before" else "AfterCommit"
                    assert json.loads(marker) == {"Boundary": expected}
                    cli(boundary + "-competing-host-refused", "--inspect-world", database, success=False)
                    process.kill()
                    stdout, stderr = process.communicate(timeout=10)
                    assert process.returncode != 0
                    report["crashCases"].append(dict(boundary=boundary, marker=marker.strip(),
                        exitCode=process.returncode, stdout=stdout, stderr=stderr,
                        termination="external process.kill; no graceful Dispose"))
                finally:
                    if process.poll() is None:
                        process.kill()
                        process.communicate(timeout=10)
                    reader.join(timeout=1)
                recovered = cli(boundary + "-recover", "--inspect-world", database)
                assert_state(recovered, boundary == "after")
                result = cli(boundary + "-retry", "--execute-local-command", database, command, principal, session)
                assert_state(result, True, receipt=True)
                assert result == first
                assert cli(boundary + "-repeat-retry", "--execute-local-command", database, command, principal, session) == first

            if args.godot:
                database = folder / "godot.sqlite"
                result = run("godot-native-durable-authority", [args.godot, "--headless", "--path", root / "client",
                    "--", "--authority-smoke", database, genesis, command])
                assert_state(result, True, receipt=True)
                assert result == first
                assert_state(cli("dedicated-reopens-godot-world", "--inspect-world", database), True)
                report["godotNativeAuthority"] = result
            report["dedicatedNativeAuthority"] = first
            report["expectedFirstReceiptHex"] = expected_receipt
        report["status"] = "passed"
    except Exception as error:
        report["status"] = "failed"
        report["failure"] = repr(error)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + "\n")
    print(report["status"] + ": " + str(args.output))
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    sys.exit(main())
