"""Qualify saved fixed-reservoir containment through the real Host and commit-boundary kills.

Checks complete canonical states and SQLite ledgers independently of the managed assertion runner.
Does not qualify physical gates, displacement, hydraulic strength, immersion or rendered water.
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
import sqlite3
import subprocess
import threading
import time
import traceback
import uuid

from check_construction_physical import frame, ledger_hash, read_frame
from check_live_excavation import digest, number
from check_liquid_reservoirs import oracle as reservoir_oracle


def database(path):
    with closing(sqlite3.connect(path.as_uri() + '?mode=ro&immutable=1', uri=True)) as connection:
        assert connection.execute('PRAGMA user_version').fetchone()[0] == 3
        head = connection.execute('SELECT sequence,state,hash FROM world_head').fetchone()
        genesis = connection.execute('SELECT state,hash FROM world_genesis').fetchone()
        assert digest(head[1]) == head[2].hex() and digest(genesis[0]) == genesis[1].hex()
        root = read_frame(head[1], 'world.snapshot', 18)
        assert len(root) == 18 and number(root[1]) == 19 and number(root[8]) == number(head[0])
        ledgers = dict(
            identities=connection.execute('SELECT identity,sequence,kind FROM world_occupied_identity ORDER BY identity').fetchall(),
            commands=connection.execute('SELECT principal,command_id,sequence,command,receipt FROM world_command_outcomes ORDER BY principal,command_id').fetchall(),
            journal=connection.execute('SELECT sequence,kind,cause_id,advancing_steps,accepted_batch,outcomes,before_hash,after_hash FROM world_commits ORDER BY sequence').fetchall())
    return head[1], genesis[0], ledgers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('dotnet', 'fixture', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    runtime = Path(__file__).resolve().parents[1]
    dotnet, fixture, output = args.dotnet.resolve(), args.fixture.resolve(), args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    work = output.parent / ('contained-reservoir-' + uuid.uuid4().hex)
    work.mkdir()
    command = [str(dotnet), str(runtime / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll')]
    env = dict(os.environ, DOTNET_ROOT=str(dotnet.parent))
    report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  runs=[], crashes=[], workDirectory=str(work), profile=19,
                  scope='Fixed nominal-vessel containment, real material action, complete history and external atomicity checks')
    originals = {p.name: digest(p.read_bytes()) for p in fixture.iterdir() if p.is_file()}

    def run(name, *arguments, exit_code=0):
        selected = command + [str(value) for value in arguments]
        record = dict(name=name, command=selected)
        report['runs'].append(record)
        started = time.monotonic()
        try:
            result = subprocess.run(selected, cwd=runtime, env=env, capture_output=True, text=True, timeout=30)
        except subprocess.TimeoutExpired as error:
            record.update(timedOut=True, timeoutSeconds=30, stdout=str(error.stdout or ''), stderr=str(error.stderr or ''))
            raise
        record.update(elapsedSeconds=time.monotonic() - started, exitCode=result.returncode,
                      stdout=result.stdout, stderr=result.stderr)
        assert result.returncode == exit_code and not result.stderr, record
        if exit_code == 0:
            return json.loads(result.stdout)
        assert result.stdout.startswith('Inspection failed:'), record

    def inspect(name, path, revision, expected):
        result = run(name, '--inspect-recorded-world', path, revision)
        assert result['ProfileVersion'] == 19 and result['StateHash'].lower() == digest(expected)
        assert result['ReservoirSeals']['ContentHash'] == digest(policy)
        return result

    def kill_at_boundary(path, boundary):
        selected = command + ['--probe-world-step-crash', str(path), str(fixture / 'step.bin'), boundary]
        process = subprocess.Popen(selected, cwd=runtime, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        messages = queue.Queue()
        threading.Thread(target=lambda: messages.put(process.stdout.readline()), daemon=True).start()
        record = dict(command=selected, boundary=boundary, pid=process.pid)
        report['crashes'].append(record)
        try:
            line = messages.get(timeout=30)
            record['notification'] = line
            assert json.loads(line)['Boundary'] == ('BeforeCommit' if boundary == 'before' else 'AfterCommit')
            assert process.poll() is None
            process.kill()
            record['exitCode'] = process.wait(timeout=5)
            record['stderr'] = process.stderr.read()
            assert record['exitCode'] != 0 and not record['stderr']
        finally:
            if process.poll() is None:
                process.kill()
                process.wait(timeout=5)
            process.stdout.close()
            process.stderr.close()

    try:
        genesis, after, policy = [(fixture / name).read_bytes() for name in ('genesis.bin', 'after.bin', 'policy.bin')]
        first = read_frame(genesis, 'world.snapshot', 18)
        final = read_frame(after, 'world.snapshot', 18)
        assert len(first) == len(final) == 18 and number(first[1]) == number(final[1]) == 19
        assert number(first[8]) == 0 and number(final[8]) == 1
        assert number(final[9]) == number(first[9]) + 1
        assert number(final[7]) == number(first[7]) + 1800
        assert first[17] == final[17] == policy
        report['independentReservoirs'] = [reservoir_oracle(root[16]) for root in (first, final)]
        normal = work / 'normal.sqlite'
        created = run('fresh admission', '--create-contained-reservoir-world', normal, fixture / 'basis.bin', fixture / 'policy.bin')
        assert created['StateHash'].lower() == digest(genesis)
        before_db = database(normal)
        assert before_db[0] == before_db[1] == genesis
        normal_before = work / 'before.sqlite'
        shutil.copyfile(normal, normal_before)
        result = run('real material step', '--execute-world-step', normal, fixture / 'step.bin')
        assert result['StateHash'].lower() == digest(after)
        after_db = database(normal)
        assert after_db[0] == after and after_db[1] == genesis
        journal = after_db[2]['journal']
        assert len(journal) == 1 and journal[0][4] == (fixture / 'step.bin').read_bytes()
        assert journal[0][6].hex() == digest(genesis) and journal[0][7].hex() == digest(after)
        assert len(after_db[2]['commands']) == 1 and not before_db[2]['commands']
        receipt = after_db[2]['commands'][0][4]
        fields = read_frame(receipt, 'world.action-receipt', 10)
        assert number(fields[7]) == 1 and number(fields[9]) == 0
        expected = json.loads((fixture / 'expected.json').read_text())
        if 'ReceiptHex' in expected:
            assert receipt.hex() == expected['ReceiptHex'].lower()
        saved = digest(normal.read_bytes())
        for revision, state in [(0, genesis), (1, after), (0, genesis)]:
            inspect('exact history ' + str(revision), normal, revision, state)
        run('duplicate selection refused', '--execute-world-step', normal, fixture / 'step.bin', exit_code=1)
        assert database(normal) == after_db and digest(normal.read_bytes()) == saved
        for boundary in ('before', 'after'):
            path = work / (boundary + '-kill.sqlite')
            shutil.copyfile(normal_before, path)
            kill_at_boundary(path, boundary)
            selected = genesis if boundary == 'before' else after
            inspect(boundary + ' kill recovery', path, 0 if boundary == 'before' else 1, selected)
            assert database(path) == (before_db if boundary == 'before' else after_db)
            if boundary == 'before':
                run('retry after precommit kill', '--execute-world-step', path, fixture / 'step.bin')
            else:
                run('duplicate after durable kill refused', '--execute-world-step', path, fixture / 'step.bin', exit_code=1)
            assert database(path) == after_db
        # This independently valid whole-cell policy remains detectably different from saved history.
        policy_fields = read_frame(policy, 'geography.reservoir-seal-policy.v1', 3)
        rows = bytearray(policy_fields[2]); thickness = int.from_bytes(rows[48:56], 'big')
        rows[48:56] = (thickness - 1 if thickness > 1 else 2).to_bytes(8, 'big')
        changed_policy = frame('geography.reservoir-seal-policy.v1', policy_fields[1], bytes(rows))
        changed = list(final); changed[17] = changed_policy
        changed_state = frame('world.snapshot', *changed[1:])
        tampered = work / 'changed-head-policy.sqlite'; shutil.copyfile(normal, tampered)
        with closing(sqlite3.connect(tampered)) as connection:
            connection.execute('UPDATE world_head SET state=?,hash=?', (changed_state, hashlib.sha256(changed_state).digest()))
            connection.commit()
        run('policy replacement cannot rewrite history', '--inspect-recorded-world', tampered, 1, exit_code=1)
        # Upgrade rejects an already admitted19 basis; no permissive fallback or duplicate policy owner.
        run('repeated fresh admission refused', '--create-contained-reservoir-world', work / 'repeat.sqlite', fixture / 'genesis.bin', fixture / 'policy.bin', exit_code=1)
        report['stateHashes'] = dict(genesis=digest(genesis), after=digest(after), policy=digest(policy))
        report['ledgerHashes'] = {name: ledger_hash(rows) for name, rows in after_db[2].items()}
        assert originals == {p.name: digest(p.read_bytes()) for p in fixture.iterdir() if p.is_file()}
        report['fixturesUnchanged'] = True
        report['status'] = 'passed'
    except Exception:
        report['failure'] = traceback.format_exc()
    finally:
        report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        report['artifactSha256'] = {str(p.relative_to(output.parent)): digest(p.read_bytes()) for p in work.iterdir() if p.is_file()}
        output.write_text(json.dumps(report, indent=2) + '\n')
    print('reservoir containment: ' + report['status'], flush=True)
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
