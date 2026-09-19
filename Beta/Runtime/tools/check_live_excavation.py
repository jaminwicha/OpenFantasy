"""Exercise earned excavation work, exact history and actual kills around final extraction.

The fixture's closed genesis database comes from SqliteWorldAuthority.CreateNew in the
managed test. All subsequent writes use the actual host; the counted-only create CLI is
not treated as a composed-world creator. No save migration or performance claim is made.
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
import struct
import subprocess
import threading
import traceback
import uuid


def fields(data):
    assert 12 <= len(data) <= 16 * 1024 * 1024
    assert data[:8] == b'OFC\0\0\0\0\1'
    count = struct.unpack_from('>I', data, 8)[0]
    assert 1 <= count <= (len(data) - 12) // 4
    result, offset = [], 12
    for _ in range(count):
        assert offset + 4 <= len(data)
        size = struct.unpack_from('>I', data, offset)[0]
        offset += 4
        assert offset + size <= len(data)
        result.append(data[offset:offset + size])
        offset += size
    assert offset == len(data)
    return result


def number(data, signed=False):
    assert len(data) == 8
    return int.from_bytes(data, 'big', signed=signed)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def state_witness(data, expected):
    """Independent retained-byte checks for labor, the excavation edit and counted yield."""
    outer = fields(data)
    assert len(outer) == 4 and outer[:2] == [b'world.snapshot', (10).to_bytes(8, 'big')]
    excavation = fields(outer[3])
    assert excavation[0] == b'terrain.excavation.v1' and len(excavation) == 2
    site = fields(excavation[1])
    assert len(site) == 12 and site[0] == b'terrain.excavation-site.v1'
    assert site[1].hex() == expected['SiteId'] and site[4].hex() == expected['CountedSourceId']
    target = tuple(expected['Target'][axis] for axis in ['X', 'Y', 'Z'])
    assert struct.unpack('>qqq', site[2]) == target and number(site[9]) == 3
    basis = fields(outer[2])
    for version in [5, 4]:
        assert basis[:2] == [b'world.snapshot', version.to_bytes(8, 'big')]
        basis = fields(basis[2])
    assert basis[:2] == [b'world.snapshot', (3).to_bytes(8, 'big')]
    gathering = fields(basis[3])
    assert gathering[0] == b'gather.snapshot'
    effort = [fields(row) for row in fields(gathering[8])[1:]
              if fields(row)[2].hex() == expected['Player']]
    assert len(effort) == 1
    basis = fields(basis[2])
    assert basis[:2] == [b'world.snapshot', (1).to_bytes(8, 'big')]
    spatial = fields(basis[3])
    selected = []
    for region in fields(spatial[5])[1:]:
        terrain = fields(region)
        assert terrain[0] == b'terrain.region.v1'
        bounds = fields(terrain[4])
        minimum = tuple(number(value, True) for value in bounds[1:4])
        size = tuple(number(value) for value in bounds[4:7])
        if all(low <= value < low + width for low, value, width in zip(minimum, target, size)):
            local = tuple(value - low for value, low in zip(target, minimum))
            index = (local[2] * size[1] + local[1]) * size[0] + local[0]
            assert len(terrain[6]) % 4 == 0
            edits = [entry[0] for entry in struct.iter_unpack('>I', terrain[6])]
            selected.append((digest(region), index in edits))
    assert len(selected) == 1
    custody = fields(fields(basis[2])[3])
    lots = [fields(row) for row in fields(custody[5])[1:]]
    destination = expected['DestinationId']
    storages = [fields(row) for row in fields(custody[4])[1:] if fields(row)[1].hex() == destination]
    assert len(storages) == 1
    return dict(Revision=number(basis[8]), WorldHash=digest(data), ExcavationHash=digest(outer[3]),
                TerrainArchiveHash=selected[0][0], CompletedWork=number(site[10]), Effort=number(effort[0][3]),
                IsExcavated=selected[0][1],
                YieldUnits=sum(number(lot[7]) - number(lot[6]) for lot in lots if lot[5].hex() == expected['CountedSourceId']),
                DestinationRevision=number(storages[0][3]),
                SourceItemQuantity=sum(number(lot[7]) - number(lot[6]) for lot in lots if lot[2].hex() == destination))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['dotnet', 'fixture', 'output']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--configuration', default='Debug')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    dotnet, fixture, output = args.dotnet.resolve(), args.fixture.resolve(), args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    work = output.parent / ('excavation-world-' + uuid.uuid4().hex)
    work.mkdir()
    expected = json.loads((fixture / 'expected.json').read_text())
    host = root / 'src/OpenFantasy.Host/bin' / args.configuration / 'net10.0/OpenFantasy.Host.dll'
    command = [str(dotnet), str(host)]
    env = dict(os.environ, DOTNET_ROOT=str(dotnet.parent))
    report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope='Managed-authority genesis; actual host earned work, two final-commit process kills and exact history',
                  workDirectory=str(work), runs=[], crashes=[], independentStates=[],
                  fixtureSha256={path.name: digest(path.read_bytes()) for path in sorted(fixture.iterdir()) if path.is_file()})

    def run(name, *arguments, exit_code=0):
        selected = command + [str(value) for value in arguments]
        record = dict(name=name, command=selected)
        report['runs'].append(record)
        try:
            result = subprocess.run(selected, cwd=root, env=env, capture_output=True, text=True, timeout=30)
        except subprocess.TimeoutExpired as error:
            record.update(timedOut=True, timeoutSeconds=30, stdout=str(error.stdout or ''), stderr=str(error.stderr or ''))
            raise
        record.update(exitCode=result.returncode, stdout=result.stdout, stderr=result.stderr)
        assert result.returncode == exit_code and not result.stderr, record
        return json.loads(result.stdout) if exit_code == 0 else None

    def inspect_head(database, revision):
        with closing(sqlite3.connect(database.as_uri() + '?mode=ro', uri=True)) as connection:
            row = connection.execute('SELECT sequence, state, hash FROM world_head').fetchone()
            assert number(row[0]) == revision and digest(row[1]) == row[2].hex()
            witness = state_witness(row[1], expected)
            assert witness == expected['States'][revision], (witness, expected['States'][revision])
            receipts = connection.execute('SELECT command_id, sequence, receipt FROM world_command_outcomes ORDER BY sequence').fetchall()
            expected_receipts = [value for value in expected['Receipts'] if value['Revision'] <= revision]
            assert [dict(CommandId=value[0].hex(), Revision=number(value[1]), ReceiptHex=value[2].hex())
                    for value in receipts] == expected_receipts
        report['independentStates'].append(dict(database=database.name, **witness))

    def history(database, revision, head):
        value = run(f'{database.stem} history {revision}', '--inspect-recorded-world', database, revision)
        assert value['ProfileVersion'] == 10 and value['RecordedRevision'] == revision
        assert value['StateHash'] == expected['WorldHashes'][revision]
        assert value['SourceHeadRevision'] == head and value['SourceHeadHash'] == expected['WorldHashes'][head]
        player = next(actor for actor in value['Actors'] if actor['ActorId'] == expected['Player'])
        assert player['SourceItemQuantity'] == expected['States'][revision]['SourceItemQuantity']

    try:
        assert state_witness((fixture / 'genesis.bin').read_bytes(), expected) == expected['States'][0]
        action_revisions = []
        for revision in range(1, expected['FinalRevision'] + 1):
            step = fields((fixture / f'step-{revision}.bin').read_bytes())
            assert len(step) == 10 and step[:2] == [b'world.step-batch', (10).to_bytes(8, 'big')]
            assert number(step[5]) == number(step[6]) == revision - 1 and number(step[7]) == 1
            assert fields(step[8]) == [b'world.step-movement']
            actions = fields(step[9])
            assert actions[0] == b'world.step-actions'
            if len(actions) > 1:
                assert len(actions) == 2
                action, intent = fields(actions[1]), fields(fields(actions[1])[9])
                assert len(action) == 10 and action[:2] == [b'world.actor-action', (8).to_bytes(8, 'big')]
                assert len(intent) == 8 and intent[:2] == [b'world.action.excavate', (1).to_bytes(8, 'big')]
                assert intent[2].hex() == expected['SiteId']
                assert number(intent[3]) == len(action_revisions)
                assert number(intent[7]) == expected['States'][revision - 1]['DestinationRevision']
                action_revisions.append(revision)
        assert action_revisions == [41, 82, 123]
        report['independentActionRevisions'] = action_revisions
        prefix = work / 'prefix.sqlite'
        shutil.copyfile(fixture / 'genesis.sqlite', prefix)
        history(prefix, 0, 0)
        inspect_head(prefix, 0)
        for revision in range(1, expected['CompletionRevision']):
            value = run(f'prefix step {revision}', '--execute-world-step', prefix, fixture / f'step-{revision}.bin')
            assert value['ProfileVersion'] == 10 and value['Revision'] == revision
            assert value['StateHash'] == expected['WorldHashes'][revision]
            if revision in [40, 41, 81, 82, 122]:
                inspect_head(prefix, revision)
            if revision % 20 == 0:
                print(f'earned-work prefix: {revision}', flush=True)
        completion = expected['CompletionRevision']
        for boundary in ['before', 'after']:
            database = work / (boundary + '.sqlite')
            shutil.copyfile(prefix, database)
            selected = command + ['--probe-world-step-crash', str(database), str(fixture / f'step-{completion}.bin'), boundary]
            process = subprocess.Popen(selected, cwd=root, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
            signals = queue.Queue()
            reader = threading.Thread(target=lambda: signals.put(process.stdout.readline()), daemon=True)
            reader.start()
            crash = dict(boundary=boundary, command=selected, pid=process.pid, signalTimeoutSeconds=30,
                         termination='external process.kill; no graceful Dispose')
            report['crashes'].append(crash)
            try:
                signal = signals.get(timeout=30)
                crash['signal'] = signal
                assert json.loads(signal) == {'Boundary': 'BeforeCommit' if boundary == 'before' else 'AfterCommit'}
                process.kill()
            finally:
                if process.poll() is None:
                    process.kill()
                stdout, stderr = process.communicate(timeout=10)
                crash.update(exitCode=process.returncode, stdout=stdout, stderr=stderr)
                reader.join(timeout=1)
            assert process.returncode != 0 and not stderr
            recovered = completion - 1 if boundary == 'before' else completion
            history(database, recovered, recovered)
            inspect_head(database, recovered)
            if boundary == 'before':
                value = run('retry rolled-back extraction', '--execute-world-step', database, fixture / f'step-{completion}.bin')
                assert value['Revision'] == completion and value['StateHash'] == expected['WorldHashes'][completion]
            inspect_head(database, completion)
            before = digest(database.read_bytes())
            run(f'{boundary} refuse committed extraction retry', '--execute-world-step', database,
                fixture / f'step-{completion}.bin', exit_code=1)
            for revision in expected['HistoryRevisions']:
                history(database, revision, completion)
            assert digest(database.read_bytes()) == before
            crash['readOnlyDatabaseSha256'] = before
            print(f'{boundary}-commit process kill and history: passed', flush=True)
        assert all(digest((fixture / name).read_bytes()) == value for name, value in report['fixtureSha256'].items())
        report['status'] = 'passed'
    except Exception as error:
        report.update(status='failed', failure=repr(error), traceback=traceback.format_exc())
    finally:
        report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2) + '\n')
    print('live excavation: ' + report['status'], flush=True)
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
