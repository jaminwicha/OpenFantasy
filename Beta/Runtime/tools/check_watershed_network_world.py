"""Exercise admitted macro facts through the real composed-world host and abrupt process death."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import queue
import struct
import subprocess
import threading
import uuid


PROFILES = {
    'network': (7, 'network.bin', b'world.watershed-network-admission.v1', '--create-watershed-network-world'),
    'reservoir': (8, 'reservoirs.bin', b'world.reservoir-admission.v1', '--create-reservoir-world'),
    'transport': (9, 'transport.bin', b'world.reservoir-transport-admission.v1', '--create-reservoir-transport-world'),
}


def frame(fields):
    return b'OFC\0' + struct.pack('>II', 1, len(fields)) + b''.join(
        struct.pack('>I', len(field)) + field for field in fields)


def fields(data):
    assert data[:8] == b'OFC\0\0\0\0\1'
    count = struct.unpack_from('>I', data, 8)[0]
    assert 1 <= count <= 11
    result, offset = [], 12
    for _ in range(count):
        size = struct.unpack_from('>I', data, offset)[0]
        offset += 4
        result.append(data[offset:offset + size])
        offset += size
    assert offset == len(data)
    return result


def verify_admission_frame(fixture, profile="network"):
    """Independent outer wire/pin oracle; existing inner domain payloads are retained byte-for-byte."""
    profile_version, artifact, tag, _ = PROFILES[profile]
    watershed = (fixture / artifact).read_bytes()
    basis = (fixture / 'basis.bin').read_bytes()
    path = [fields(basis)]
    for version in [5, 4, 3]:
        assert path[-1][:2] == [b'world.snapshot', struct.pack('>Q', version)]
        path.append(fields(path[-1][2]))
    assert path[-1][:2] == [b'world.snapshot', struct.pack('>Q', 1)]
    path[-1][5] = hashlib.sha256(frame([tag, path[-1][5],
                                      hashlib.sha256(watershed).digest()])).digest()
    nested = frame(path.pop())
    for parent in reversed(path):
        parent[2] = nested
        nested = frame(parent)
    expected = frame([b'world.snapshot', struct.pack('>Q', profile_version), nested, watershed])
    assert expected == (fixture / 'genesis.bin').read_bytes()
    return hashlib.sha256(expected).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', type=Path, required=True)
    parser.add_argument('--fixture', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--profile', choices=list(PROFILES), default='network')
    args = parser.parse_args()
    version, artifact_name, _, create_option = PROFILES[args.profile]
    root = Path(__file__).resolve().parents[1]
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    work = output.parent / (args.profile + '-world-' + uuid.uuid4().hex)
    work.mkdir()
    database = work / 'world.sqlite'
    fixture = args.fixture.resolve()
    expected = json.loads((fixture / 'expected.json').read_text())
    dotnet = args.dotnet.resolve()
    command = [str(dotnet), str(root / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll')]
    env = dict(os.environ, DOTNET_ROOT=str(dotnet.parent))
    report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope=args.profile + '; actual composed-world host create, commits, kill/recover and exact history',
                  fixtureSha256={p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(fixture.iterdir()) if p.is_file()},
                  runs=[], crashes=[])

    def run(name, *arguments, exit_code=0):
        selected = command + [str(value) for value in arguments]
        try:
            result = subprocess.run(selected, cwd=root, env=env, capture_output=True, text=True, timeout=30)
        except subprocess.TimeoutExpired as error:
            def retained(value):
                return value.decode(errors='replace') if isinstance(value, bytes) else value or ''
            report['runs'].append(dict(name=name, command=selected, timedOut=True, timeoutSeconds=30,
                                       stdout=retained(error.stdout), stderr=retained(error.stderr)))
            raise
        report['runs'].append(dict(name=name, command=selected, exitCode=result.returncode,
                                   stdout=result.stdout, stderr=result.stderr))
        if result.returncode != exit_code or (exit_code == 0 and result.stderr):
            raise RuntimeError(name + ': unexpected host result')
        return json.loads(result.stdout) if exit_code == 0 else None

    def assert_reservoir(value, revision):
        if version < 8:
            return
        assert value['Reservoirs']['ContentHash'] == expected['ReservoirHashes'][revision]
        if 'ReservoirStates' in expected:
            for key, wanted in expected['ReservoirStates'][revision].items():
                assert value['Reservoirs'][key] == wanted, (revision, key, value['Reservoirs'][key], wanted)

    def assert_transport(value, revision):
        if version != 9:
            return
        actual = value['Transport']
        assert actual['ContentHash'] == expected['TransportHashes'][revision]
        for key, wanted in expected['TransportStates'][revision].items():
            observed = (sum(s['Count'] for s in actual['Statuses'] if s['Status'] == 'Completed')
                        if key == 'CompletedCount' else actual[key])
            assert observed == wanted, (revision, key, observed, wanted)
        by_custodian = {c['CustodianId']: c for c in actual['Contents']}
        source = by_custodian[expected['SourceCustodian']]
        receiver = by_custodian[expected['ReceiverCustodian']]
        # These are independently known fixture quantities, not planner-generated throughput totals.
        assert source['ActualUnits'] == (1300 if revision < 5 else 700)
        assert receiver['ActualUnits'] == (50 if revision < 5 else 650)
        assert source['ActualUnits'] + receiver['ActualUnits'] == 1350
        assert source['ActualMicrolitres'] + receiver['ActualMicrolitres'] == 1_350_000_000

    def verify_transport_frames():
        if version != 9:
            return
        owner = fields((fixture / 'transport.bin').read_bytes())
        assert owner[0] == b'geography.reservoir-transport.v1' and len(owner) == 6
        assert struct.unpack('>Q', owner[1])[0] == 0 and len(owner[2]) == 12
        assert len(owner[4]) == 96
        assert struct.unpack_from('>qqqqqQ', owner[4], 48) == (0, 0, 1, 0, 0, 600)
        assert fields(owner[5]) == [b'geography.transport-stages.v1']
        scheduled = fields(fields((fixture / 'step-1.bin').read_bytes())[9])[1]
        command_fields = fields(scheduled)
        assert command_fields[:2] == [b'world.actor-action', struct.pack('>Q', 7)]
        action = fields(command_fields[9])
        assert action[:2] == [b'world.action.schedule-runoff', struct.pack('>Q', 1)]
        assert action[2] == hashlib.sha256((fixture / 'transport.bin').read_bytes()).digest()
        assert len(action[6]) == 32 and action[6][16:] == owner[4][:16]
        def milliseconds(instant):
            days, part = struct.unpack('>qi', instant)
            return days * 86400000 + part
        assert milliseconds(action[5]) - milliseconds(owner[2]) == 3600
        # The crash-tested transfer is autonomous due work, not a second material action.
        assert fields(fields((fixture / 'step-5.bin').read_bytes())[9]) == [b'world.step-actions']
        report['independentTransportContract'] = dict(gateUnitsPerStep=600, scheduledDelayMs=3600,
                                                     autonomousCrashRevision=5, conservedSourceUnits=1350)

    def verify_control_frames():
        if 'Actions' not in expected:
            return
        for revision, wanted in enumerate(expected['Actions'], start=1):
            batch = fields((fixture / f'step-{revision}.bin').read_bytes())
            actions = fields(batch[9])
            assert actions[0] == b'world.step-actions' and len(actions) == 2
            command_fields = fields(actions[1])
            assert command_fields[:2] == [b'world.actor-action', struct.pack('>Q', 6)]
            action = fields(command_fields[9])
            assert action[:3] == [b'world.action.runoff-commitment', struct.pack('>Q', 1), struct.pack('>Q', wanted['Kind'])]
            assert action[3].hex() == wanted['ExpectedHash']
            assert struct.unpack('>Q', action[4])[0] == wanted['ExpectedRevision']
            assert action[5].hex() == wanted['Reservation']
            if wanted['Kind'] == 1:
                assert len(action) == 9 and len(action[6]) == len(action[7]) == 12
                assert len(action[8]) == 72
                row = action[8]
                assert struct.unpack_from('>Q', row, 16)[0] == 3000
                assert struct.unpack_from('>QQ', row, 40) == (1300, 1900)
            else:
                assert len(action) == 6
        report['independentControlFrames'] = len(expected['Actions'])

    def assert_history(revision):
        value = run('history ' + str(revision), '--inspect-recorded-world', database, revision)
        assert value['ProfileVersion'] == version and value['RecordedRevision'] == revision
        assert value['StateHash'] == expected['WorldHashes'][revision]
        assert value['WatershedNetwork']['ContentHash'] == expected['NetworkHash']
        assert_reservoir(value, revision)
        assert_transport(value, revision)
        assert value['HasReadingHold'] == (revision in expected.get('HeldRevisions', [expected['HeldRevision']]))
        return value

    try:
        verify_control_frames()
        verify_transport_frames()
        report['independentAdmissionHash'] = verify_admission_frame(fixture, args.profile)
        assert report['independentAdmissionHash'] == expected['WorldHashes'][0]
        created = run('create', create_option, database, fixture / 'basis.bin', fixture / artifact_name)
        assert created['StateHash'] == expected['WorldHashes'][0]
        assert created['WatershedNetwork']['ContentHash'] == expected['NetworkHash']
        run('refuse existing destination', create_option, database,
            fixture / 'basis.bin', fixture / artifact_name, exit_code=1)
        assert_history(0)
        def execute(revision):
            value = run('step ' + str(revision), '--execute-world-step', database, fixture / f'step-{revision}.bin')
            assert value['Revision'] == revision and value['StateHash'] == expected['WorldHashes'][revision]
            assert value['WatershedNetwork']['ContentHash'] == expected['NetworkHash']
            assert_reservoir(value, revision)
            assert_transport(value, revision)

        crash_revision = expected.get('CrashRevision', 1)
        assert 1 <= crash_revision <= expected['FinalRevision']
        for revision in range(1, crash_revision):
            execute(revision)
        for boundary, revision in [('before', crash_revision - 1), ('after', crash_revision)]:
            selected = command + ['--probe-world-step-crash', str(database), str(fixture / f'step-{crash_revision}.bin'), boundary]
            process = subprocess.Popen(selected, cwd=root, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
            lines = queue.Queue()
            threading.Thread(target=lambda: lines.put(process.stdout.readline()), daemon=True).start()
            crash = dict(boundary=boundary, command=selected, pid=process.pid, signalTimeoutSeconds=30)
            report['crashes'].append(crash)
            try:
                signal = lines.get(timeout=30)
                crash['signal'] = signal
                assert json.loads(signal)['Boundary'] == ('BeforeCommit' if boundary == 'before' else 'AfterCommit')
                process.kill()
            finally:
                if process.poll() is None:
                    process.kill()
                stdout, stderr = process.communicate(timeout=10)
                crash.update(exitCode=process.returncode, stdout=stdout, stderr=stderr)
            assert process.returncode != 0
            assert_history(revision)
        run('refuse duplicate committed step', '--execute-world-step', database, fixture / f'step-{crash_revision}.bin', exit_code=1)
        for revision in range(crash_revision + 1, expected['FinalRevision'] + 1):
            execute(revision)
        before = hashlib.sha256(database.read_bytes()).hexdigest()
        for revision in expected.get('HistoryRevisions', [expected['FinalRevision'], 0, expected['HeldRevision'], 1]):
            value = assert_history(revision)
            assert value['SourceHeadRevision'] == expected['FinalRevision']
            assert value['SourceHeadHash'] == expected['WorldHashes'][-1]
        assert hashlib.sha256(database.read_bytes()).hexdigest() == before
        assert all(hashlib.sha256((fixture / name).read_bytes()).hexdigest() == digest
                   for name, digest in report['fixtureSha256'].items())
        report['databaseSha256'] = before
        report['database'] = str(database.relative_to(output.parent))
        report['status'] = 'passed'
    except Exception as error:
        report['status'] = 'failed'
        report['failure'] = repr(error)
    finally:
        report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2) + '\n')
    print(args.profile + ' world: ' + report['status'], flush=True)
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
