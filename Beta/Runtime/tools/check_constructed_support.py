"""Probe explicit climbing or ordinary stair traversal through two abrupt commit-boundary kills.

The closed pre-step fixture is created by the managed authority. Independent OFC reads
check the direct complete root, actor feet, earned effort and retained installed construction.
The probe does not claim independent structural-load or physical-device qualification.
"""
import argparse
from contextlib import closing
import datetime
import json
import os
from pathlib import Path
import queue
import shutil
import sqlite3
import subprocess
import threading
import traceback
import uuid

from check_live_excavation import digest, fields, number


def state_witness(data, player, profile_version=11):
    spatial_version = {11: 2, 12: 3}[profile_version]
    root = fields(data)
    assert len(root) == 17 and root[:2] == [b'world.snapshot', profile_version.to_bytes(8, 'big')]
    spatial = fields(root[3])
    assert len(spatial) == 8 and spatial[:2] == [b'world.spatial', spatial_version.to_bytes(8, 'big')]
    presences = fields(spatial[7])
    assert presences[0] == b'world.presences'
    selected = []
    for value in presences[1:]:
        presence = fields(value)
        assert len(presence) == 9 and presence[0] == b'world.presence'
        binding = fields(presence[2])
        assert binding[0] == b'actor.form-binding'
        if binding[2].hex() == player:
            selected.append(presence)
    assert len(selected) == 1
    feet = dict(zip(['X', 'Y', 'Z'], [number(value, True) for value in selected[0][4:7]]))
    feet['Cell'] = {axis: feet[axis] // 256 for axis in ['X', 'Y', 'Z']}
    gathering = fields(root[11])
    assert gathering[0] == b'gather.snapshot'
    workers = fields(gathering[8])
    assert workers[0] == b'gather.actors'
    efforts = [fields(value) for value in workers[1:] if fields(value)[2].hex() == player]
    assert len(efforts) == 1 and efforts[0][0] == b'gather.actor-work'
    construction = fields(root[15])
    assert len(construction) == 7 and construction[:2] == [b'construction.snapshot', (1).to_bytes(8, 'big')]
    projects = fields(construction[6])
    assert projects[0] == b'construction.projects'
    installed = []
    for value in projects[1:]:
        project = fields(value)
        assert len(project) == 17 and project[0] == b'construction.project'
        if project[15]:
            placement = fields(project[15])
            assert len(placement) == 6 and placement[0] == b'construction.installed'
            installed.append(dict(ProjectId=project[2].hex(), StructureId=placement[1].hex(),
                                  GuardLotId=placement[2].hex(), CompletionCommandId=placement[5].hex()))
    assert installed, 'Ascent fixture must retain at least one actually installed construction project.'
    assert fields(root[16])[0] == b'terrain.excavation.v1'
    authority = fields(root[2])
    assert authority[0] == b'authority.counted-state'
    controls = fields(authority[5])
    assert controls[0] == b'authority.controls'
    deliveries = []
    for value in controls[1:]:
        control = fields(value)
        assert len(control) == 4 and control[0] == b'authority.control'
        if control[3].hex() == player:
            deliveries.append(dict(PrincipalId=control[1].hex(), SessionId=control[2].hex()))
    return dict(Revision=number(root[8]), AdvancingSteps=number(root[9]), StateHash=digest(data),
                Feet=feet, Effort=number(efforts[0][3]), ActorRevision=number(selected[0][7]),
                ConstructionHash=digest(root[15]), Installed=installed, ProductionHash=digest(root[12]),
                AuthorizedDeliveries=deliveries,
                AuthorityHash=digest(root[2]), TerrainHash=digest(spatial[5]), ExcavationHash=digest(root[16]))


def verify_climb_step(data, expected):
    step = fields(data)
    assert len(step) == 10 and step[:2] == [b'world.step-batch', (11).to_bytes(8, 'big')]
    assert number(step[5]) == expected['PreRevision'] and number(step[7]) == 1
    movement = fields(step[8])
    assert 1 <= len(movement) <= 2 and movement[0] == b'world.step-movement'
    actions = fields(step[9])
    assert len(actions) == 2 and actions[0] == b'world.step-actions'
    action = fields(actions[1])
    assert len(action) == 10 and action[:2] == [b'world.actor-action', (9).to_bytes(8, 'big')]
    assert action[2].hex() == expected['CommandId'] and action[5].hex() == expected['Player']
    suppressed = []
    if len(movement) == 2:
        sample = fields(movement[1])
        assert len(sample) == 6 and sample[0] == b'world.control-sample'
        assert sample[1:4] == action[3:6]
        assert [number(value, True) for value in sample[4:6]] == [0, 32]
        suppressed.append(dict(ActorId=sample[3].hex(), DeltaX=0, DeltaY=32))
    intent = fields(action[9])
    assert len(intent) == 5 and intent[:2] == [b'world.action.climb', (1).to_bytes(8, 'big')]
    delta = [number(value, True) for value in intent[2:5]]
    assert (delta[0] == 0) != (delta[1] == 0) and 0 < abs(delta[2]) <= 256
    assert all(abs(value) <= 256 for value in delta)
    assert delta == [expected['PostFeet'][axis] - expected['PreFeet'][axis] for axis in ['X', 'Y', 'Z']]
    assert expected['PostRevision'] == expected['PreRevision'] + 1
    return actions[1], dict(Samples=suppressed, ClimbDelta=dict(zip(['X', 'Y', 'Z'], delta)),
                            ExpectedPostFeet=expected['PostFeet'], ExpectedOrdinaryDisplacement=dict(X=0, Y=0))


def verify_ordinary_step(data, expected):
    step = fields(data)
    assert len(step) == 10 and step[:2] == [b'world.step-batch', (12).to_bytes(8, 'big')]
    assert number(step[5]) == expected['PreRevision'] and number(step[7]) == 1
    assert fields(step[9]) == [b'world.step-actions']
    movement = fields(step[8])
    assert len(movement) == 2 and movement[0] == b'world.step-movement'
    sample = fields(movement[1])
    assert len(sample) == 6 and sample[0] == b'world.control-sample'
    assert sample[3].hex() == expected['Player']
    delta_x, delta_y = [number(value, True) for value in sample[4:6]]
    assert (delta_x, delta_y) in [(0, 32), (0, -32), (32, 0), (-32, 0)]
    assert expected['PostFeet']['X'] == expected['PreFeet']['X'] + delta_x
    assert expected['PostFeet']['Y'] == expected['PreFeet']['Y'] + delta_y
    delta_z = expected['PostFeet']['Z'] - expected['PreFeet']['Z']
    assert 0 < abs(delta_z) <= 64
    assert expected['PostRevision'] == expected['PreRevision'] + 1
    assert expected['EffortAfter'] == min(1000, expected['EffortBefore'] + 10)
    return dict(ActorId=sample[3].hex(), PrincipalId=sample[1].hex(), SessionId=sample[2].hex(),
                DeltaX=delta_x, DeltaY=delta_y, ResolvedDeltaZ=delta_z,
                ExpectedAdvancingSteps=number(step[6]), ExpectedPostFeet=expected['PostFeet'],
                EffortBefore=expected['EffortBefore'], EffortAfter=expected['EffortAfter'])


def receipt_ledger_witness(connection):
    rows = connection.execute('SELECT principal, command_id, sequence, command, receipt '
                              'FROM world_command_outcomes ORDER BY principal, command_id').fetchall()
    encoded = json.dumps([[value.hex() for value in row] for row in rows], separators=(',', ':')).encode()
    return dict(ReceiptLedgerHash=digest(encoded), ReceiptLedgerRows=len(rows))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['dotnet', 'fixture', 'output']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--configuration', default='Debug')
    args = parser.parse_args()
    runtime = Path(__file__).resolve().parents[1]
    dotnet, fixture, output = args.dotnet.resolve(), args.fixture.resolve(), args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    work = output.parent / ('constructed-support-' + uuid.uuid4().hex)
    command = [str(dotnet), str(runtime / 'src/OpenFantasy.Host/bin' / args.configuration / 'net10.0/OpenFantasy.Host.dll')]
    env = dict(os.environ, DOTNET_ROOT=str(dotnet.parent))
    report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope='Actual host ascent with exact retained construction, positions, effort, receipts and two commit-boundary kills',
                  workDirectory=str(work), runs=[], crashes=[], independentStates=[])

    def run(name, *arguments, exit_code=0):
        selected = command + [str(value) for value in arguments]
        record = dict(name=name, command=selected)
        report['runs'].append(record)
        try:
            result = subprocess.run(selected, cwd=runtime, env=env, capture_output=True, text=True, timeout=30)
        except subprocess.TimeoutExpired as error:
            record.update(timedOut=True, timeoutSeconds=30, stdout=str(error.stdout or ''), stderr=str(error.stderr or ''))
            raise
        record.update(exitCode=result.returncode, stdout=result.stdout, stderr=result.stderr)
        assert result.returncode == exit_code and not result.stderr, record
        if exit_code == 0:
            return json.loads(result.stdout)
        assert result.stdout.startswith('Inspection failed:'), record
        return None

    def inspect_head(database, committed):
        prefix = 'Post' if committed else 'Pre'
        with closing(sqlite3.connect(database.as_uri() + '?mode=ro', uri=True)) as connection:
            row = connection.execute('SELECT sequence, state, hash FROM world_head').fetchone()
            assert number(row[0]) == expected[prefix + 'Revision']
            assert digest(row[1]) == row[2].hex() == expected[prefix + 'Hash']
            witness = state_witness(row[1], expected['Player'], profile_version)
            assert witness['Revision'] == expected[prefix + 'Revision'] and witness['Feet'] == expected[prefix + 'Feet']
            assert witness['Effort'] == expected['EffortAfter' if committed else 'EffortBefore']
            witness.update(receipt_ledger_witness(connection))
            if profile_version == 11:
                receipts = connection.execute('SELECT sequence, command, receipt FROM world_command_outcomes WHERE command_id=?',
                                              (bytes.fromhex(expected['CommandId']),)).fetchall()
                if committed:
                    assert len(receipts) == 1 and number(receipts[0][0]) == expected['PostRevision']
                    assert receipts[0][1] == action_bytes and receipts[0][2].hex() == expected['ReceiptHex']
                else:
                    assert not receipts
            elif baseline is not None:
                assert witness['ReceiptLedgerHash'] == baseline['ReceiptLedgerHash']
                assert witness['ReceiptLedgerRows'] == baseline['ReceiptLedgerRows']
            if committed:
                for key in ['AuthorityHash', 'TerrainHash', 'ConstructionHash', 'Installed', 'ProductionHash', 'ExcavationHash']:
                    assert witness[key] == baseline[key], key
                assert witness['AdvancingSteps'] == baseline['AdvancingSteps'] + 1
                assert witness['ActorRevision'] == baseline['ActorRevision'] + 1
                if profile_version == 12:
                    assert witness['Effort'] == min(1000, baseline['Effort'] + 10)
        report['independentStates'].append(dict(database=database.name, **witness))
        return witness

    def history(database, revision, state_hash, committed):
        prefix = 'Post' if committed else 'Pre'
        value = run(f'{database.stem} history {revision}', '--inspect-recorded-world', database, revision)
        assert value['Contract'] == 'recorded-world-state-v1' and value['ProfileVersion'] == profile_version
        assert value['RecordedRevision'] == revision and value['StateHash'] == state_hash
        assert value['SourceHeadRevision'] == expected[prefix + 'Revision']
        assert value['SourceHeadHash'] == expected[prefix + 'Hash']
        player = next(actor for actor in value['Actors'] if actor['ActorId'] == expected['Player'])
        if revision == expected['PreRevision']:
            assert all(player['Position'][axis] == expected['PreFeet'][axis] for axis in ['X', 'Y', 'Z'])
        if revision == expected['PostRevision']:
            assert all(player['Position'][axis] == expected['PostFeet'][axis] for axis in ['X', 'Y', 'Z'])

    def check_history_and_retry(database):
        before = digest(database.read_bytes())
        revisions = list(reversed(expected['History']))
        for prefix in ['Post', 'Pre']:
            if not any(item['Revision'] == expected[prefix + 'Revision'] for item in revisions):
                revisions.append(dict(Revision=expected[prefix + 'Revision'], Hash=expected[prefix + 'Hash']))
        for item in revisions:
            history(database, item['Revision'], item['Hash'], True)
        run(f'{database.stem} refuse committed {operation} retry', '--execute-world-step', database, step_path, exit_code=1)
        inspect_head(database, True)
        assert digest(database.read_bytes()) == before
        return before

    try:
        work.mkdir()
        expected = json.loads((fixture / 'expected.json').read_text())
        profile_version = expected.get('ProfileVersion', 11)
        assert profile_version in [11, 12]
        operation = 'climb' if profile_version == 11 else 'ordinary stair step'
        seed_path = fixture / ('pre-climb.sqlite' if profile_version == 11 else 'midstairs.sqlite')
        step_path = fixture / ('climb-step.bin' if profile_version == 11 else 'ordinary-step.bin')
        report['profileVersion'] = profile_version
        report['operation'] = operation
        report['fixtureSha256'] = {path.name: digest(path.read_bytes()) for path in sorted(fixture.iterdir()) if path.is_file()}
        step_bytes = step_path.read_bytes()
        report['independentStepHash'] = digest(step_bytes)
        if profile_version == 11:
            action_bytes, suppressed_movement = verify_climb_step(step_bytes, expected)
            report['independentActionHash'] = digest(action_bytes)
            report['suppressedMovementWitness'] = suppressed_movement
        else:
            ordinary_movement = verify_ordinary_step(step_bytes, expected)
            report['ordinaryMovementWitness'] = ordinary_movement
            report['scope'] = 'Actual host ordinary stairs with exact construction, positions, accrued effort, unchanged receipt ledger and two commit-boundary kills'
        normal = work / 'normal.sqlite'
        shutil.copyfile(seed_path, normal)
        baseline = None
        baseline = inspect_head(normal, False)
        if profile_version == 12:
            delivery = {key: ordinary_movement[key] for key in ['PrincipalId', 'SessionId']}
            assert delivery in baseline['AuthorizedDeliveries']
            assert ordinary_movement['ExpectedAdvancingSteps'] == baseline['AdvancingSteps']
        history(normal, expected['PreRevision'], expected['PreHash'], False)
        value = run(operation, '--execute-world-step', normal, step_path)
        assert value['ProfileVersion'] == profile_version and value['Revision'] == expected['PostRevision']
        assert value['StateHash'] == expected['PostHash']
        inspect_head(normal, True)
        report['ordinaryReadOnlyDatabaseSha256'] = check_history_and_retry(normal)
        for boundary in ['before', 'after']:
            database = work / (boundary + '.sqlite')
            shutil.copyfile(seed_path, database)
            selected = command + ['--probe-world-step-crash', str(database), str(step_path), boundary]
            process = subprocess.Popen(selected, cwd=runtime, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
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
            committed = boundary == 'after'
            prefix = 'Post' if committed else 'Pre'
            history(database, expected[prefix + 'Revision'], expected[prefix + 'Hash'], committed)
            inspect_head(database, committed)
            if not committed:
                value = run('retry rolled-back ' + operation, '--execute-world-step', database, step_path)
                assert value['Revision'] == expected['PostRevision'] and value['StateHash'] == expected['PostHash']
            inspect_head(database, True)
            crash['readOnlyDatabaseSha256'] = check_history_and_retry(database)
            print(f'{boundary}-commit {operation} kill and history: passed', flush=True)
        assert all(digest((fixture / name).read_bytes()) == value for name, value in report['fixtureSha256'].items())
        report['status'] = 'passed'
    except Exception as error:
        report.update(status='failed', failure=repr(error), traceback=traceback.format_exc())
    finally:
        report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2) + '\n')
    print('constructed support: ' + report['status'], flush=True)
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
