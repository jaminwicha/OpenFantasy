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


def verify_admission_frame(fixture):
    """Independent outer wire/pin oracle; existing inner domain payloads are retained byte-for-byte."""
    watershed = (fixture / 'network.bin').read_bytes()
    basis = (fixture / 'basis.bin').read_bytes()
    path = [fields(basis)]
    for version in [5, 4, 3]:
        assert path[-1][:2] == [b'world.snapshot', struct.pack('>Q', version)]
        path.append(fields(path[-1][2]))
    assert path[-1][:2] == [b'world.snapshot', struct.pack('>Q', 1)]
    path[-1][5] = hashlib.sha256(frame([b'world.watershed-network-admission.v1', path[-1][5],
                                      hashlib.sha256(watershed).digest()])).digest()
    nested = frame(path.pop())
    for parent in reversed(path):
        parent[2] = nested
        nested = frame(parent)
    expected = frame([b'world.snapshot', struct.pack('>Q', 7), nested, watershed])
    assert expected == (fixture / 'genesis.bin').read_bytes()
    return hashlib.sha256(expected).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', type=Path, required=True)
    parser.add_argument('--fixture', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    work = output.parent / ('watershed-network-world-' + uuid.uuid4().hex)
    work.mkdir()
    database = work / 'world.sqlite'
    fixture = args.fixture.resolve()
    expected = json.loads((fixture / 'expected.json').read_text())
    dotnet = args.dotnet.resolve()
    command = [str(dotnet), str(root / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll')]
    env = dict(os.environ, DOTNET_ROOT=str(dotnet.parent))
    report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope='Admitted watershed network; actual composed-world host create, commits, kill/recover and exact history',
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

    def assert_history(revision):
        value = run('history ' + str(revision), '--inspect-recorded-world', database, revision)
        assert value['ProfileVersion'] == 7 and value['RecordedRevision'] == revision
        assert value['StateHash'] == expected['WorldHashes'][revision]
        assert value['WatershedNetwork']['ContentHash'] == expected['NetworkHash']
        assert value['HasReadingHold'] == (revision == expected['HeldRevision'])
        return value

    try:
        report['independentAdmissionHash'] = verify_admission_frame(fixture)
        assert report['independentAdmissionHash'] == expected['WorldHashes'][0]
        created = run('create', '--create-watershed-network-world', database, fixture / 'basis.bin', fixture / 'network.bin')
        assert created['StateHash'] == expected['WorldHashes'][0]
        assert created['WatershedNetwork']['ContentHash'] == expected['NetworkHash']
        run('refuse existing destination', '--create-watershed-network-world', database,
            fixture / 'basis.bin', fixture / 'network.bin', exit_code=1)
        assert_history(0)
        for boundary, revision in [('before', 0), ('after', 1)]:
            selected = command + ['--probe-world-step-crash', str(database), str(fixture / 'step-1.bin'), boundary]
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
        run('refuse duplicate committed step', '--execute-world-step', database, fixture / 'step-1.bin', exit_code=1)
        for revision in range(2, expected['FinalRevision'] + 1):
            value = run('step ' + str(revision), '--execute-world-step', database, fixture / f'step-{revision}.bin')
            assert value['Revision'] == revision and value['StateHash'] == expected['WorldHashes'][revision]
            assert value['WatershedNetwork']['ContentHash'] == expected['NetworkHash']
        before = hashlib.sha256(database.read_bytes()).hexdigest()
        for revision in [expected['FinalRevision'], 0, expected['HeldRevision'], 1]:
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
    print('watershed network world: ' + report['status'], flush=True)
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
