"""Real process interruption around selected retreat publications through the normal composed-world authority.

For every exported case (the queued retreat, a standing mid-route sample and the disengaging sample) the closed save
taken just before the publication is copied three times. One copy executes the exact host-selected step normally. The
other two run the existing `--probe-world-step-crash` seam, which stops at the authority's own BeforeCommit or
AfterCommit transaction boundary and waits; this tool then SIGKILLs only that child process. Recovery is the ordinary
full verified journal recovery of `--inspect-recorded-world`; an uncommitted step is retried and every committed one is
refused as a duplicate without changing bytes. Logs, recovered copies and the exact source fixtures are preserved for
RetreatDurabilityResultTests, which compares every recovered journal with the unbroken originals.
"""
import argparse
import datetime
import hashlib
import json
import os
import queue
import shutil
import subprocess
import threading
import time
import traceback
from pathlib import Path

LIMITS = dict(runSeconds=60, signalSeconds=60, killSeconds=10, aggregateSeconds=420)


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def closed(path):
    return not Path(str(path) + '-wal').exists() and not Path(str(path) + '-shm').exists()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('dotnet', 'fixtures', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    started = time.monotonic()
    runtime = Path(__file__).resolve().parents[1]
    fixtures, output = args.fixtures.resolve(), args.output.resolve()
    require(not output.exists(), 'Never replace an earlier result.')
    work = output.parent / ('retreat-durability-' + datetime.datetime.now(datetime.timezone.utc).strftime('%H%M%S%f'))
    require(not work.is_relative_to(fixtures), 'Work must stay outside the source fixtures.')
    work.mkdir(parents=True)
    host = runtime / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll'
    command = [str(args.dotnet.resolve()), str(host)]
    env = dict(os.environ, DOTNET_ROOT=str(args.dotnet.resolve().parent))
    report = dict(status='running', workDirectory=str(work), fixtures=str(fixtures), limits=LIMITS, runs=[], processKills=[],
                  cases=[], startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    originals = {str(path.relative_to(fixtures)): digest(path) for path in fixtures.rglob('*') if path.is_file()}

    def log_paths(label):
        index = len(report['runs'])
        return work / f'{index:02d}-{label}.stdout.log', work / f'{index:02d}-{label}.stderr.log'

    def run(label, *arguments, fail=False):
        stdout, stderr = log_paths(label)
        row = dict(label=label, arguments=list(map(str, arguments)), stdout=str(stdout), stderr=str(stderr))
        report['runs'].append(row)
        start = time.monotonic()
        with stdout.open('xb') as out, stderr.open('xb') as err:
            done = subprocess.run(command + list(map(str, arguments)), cwd=runtime, env=env, stdout=out, stderr=err,
                                  timeout=LIMITS['runSeconds'])
        row.update(seconds=round(time.monotonic() - start, 3), exitCode=done.returncode)
        text, errors = stdout.read_text(errors='replace'), stderr.read_text(errors='replace')
        require(row['seconds'] <= LIMITS['runSeconds'] and not errors, label + ' wrote errors: ' + errors[:400])
        if fail:
            require(done.returncode != 0 and text.startswith('Inspection failed:'), label + ' was not refused: ' + text[:400])
            return None
        require(done.returncode == 0, label + ' failed: ' + text[:400])
        return json.loads(text)

    def kill(label, path, step, boundary):
        stdout, stderr = log_paths(label)
        errors = stderr.open('xb')
        process = subprocess.Popen(command + ['--probe-world-step-crash', str(path), str(step), boundary], cwd=runtime, env=env,
                                   stdout=subprocess.PIPE, stderr=errors, text=True)
        lines = queue.Queue()
        reader = threading.Thread(target=lambda: lines.put(process.stdout.readline()), daemon=True)
        row = dict(case=label, boundary=boundary, pid=process.pid, database=str(path), stdout=str(stdout), stderr=str(stderr))
        report['processKills'].append(row)
        start = time.monotonic()
        reader.start()
        try:
            line = lines.get(timeout=LIMITS['signalSeconds'])
            stdout.write_text(line)
            row.update(signalSeconds=round(time.monotonic() - start, 3), signal=line.strip())
            expected = 'BeforeCommit' if boundary == 'before' else 'AfterCommit'
            require(json.loads(line) == {'Boundary': expected}, label + ' did not reach ' + expected)
            require(process.poll() is None, label + ' exited before it was killed.')
            start = time.monotonic()
            process.kill()  # SIGKILL of this tool's own child only; no finally, flush or Dispose runs in it.
            row.update(exitCode=process.wait(timeout=LIMITS['killSeconds']), killSeconds=round(time.monotonic() - start, 3))
            require(row['exitCode'] == -9, label + ' was not terminated by SIGKILL.')
        finally:
            if process.poll() is None:
                process.kill()
                process.wait(timeout=LIMITS['killSeconds'])
            reader.join(timeout=1)
            process.stdout.close()
            errors.close()
        require(not Path(stderr).read_text(errors='replace'), label + ' wrote errors before the kill.')

    def check(value, revision, state_hash, label):
        require(value.get('Revision', value.get('RecordedRevision')) == revision, label + ' recovered another revision.')
        require(value['StateHash'].lower() == state_hash.lower(), label + ' recovered another root.')
        require(value['ProfileVersion'] == 34, label + ' changed its interpretation.')

    try:
        manifest = json.loads((fixtures / 'manifest.json').read_text())
        require(manifest['Kind'] == 'retreat-durability-fixtures-v1' and len(manifest['Cases']) == 3, 'Unexpected fixture manifest.')
        for case in manifest['Cases']:
            require(time.monotonic() - started < LIMITS['aggregateSeconds'], 'Aggregate durability bound exceeded.')
            label, sequence = case['Label'], case['Sequence']
            before, step = fixtures / case['Before'], fixtures / case['Step']
            require(closed(before), label + ' fixture save is not closed.')
            row = dict(label=label, sequence=sequence, kind=case['Kind'], before=str(before), step=str(step),
                       unbrokenAfter=str(fixtures / case['After']))
            for boundary in ('normal', 'before', 'after'):
                path = work / f'{label}-{boundary}.sqlite'
                shutil.copyfile(before, path)
                if boundary == 'normal':
                    check(run(f'{label}-normal', '--execute-world-step', path, step), sequence, case['AfterHash'], label + ' normal')
                else:
                    kill(f'{label}-{boundary}', path, step, boundary)
                    retained = sequence - 1 if boundary == 'before' else sequence
                    expected = case['BeforeHash'] if boundary == 'before' else case['AfterHash']
                    check(run(f'{label}-recover-{boundary}', '--inspect-recorded-world', path, retained), retained, expected,
                          f'{label} recovered after a {boundary} kill')
                    require(closed(path), label + ' recovery left an open journal.')
                    if boundary == 'before':
                        recovered = work / f'{label}-before-recovered.sqlite'
                        shutil.copyfile(path, recovered)
                        row['beforeRecovered'] = str(recovered)
                        check(run(f'{label}-retry', '--execute-world-step', path, step), sequence, case['AfterHash'],
                              label + ' retried after a before kill')
                frozen = digest(path)
                run(f'{label}-{boundary}-duplicate', '--execute-world-step', path, step, fail=True)
                require(digest(path) == frozen and closed(path), label + ' duplicate changed the save.')
                row[boundary] = str(path)
            report['cases'].append(row)
        require(len(report['processKills']) == 6 and {row['boundary'] for row in report['processKills']} == {'before', 'after'},
                'Every case needs both process kills.')
        require(originals == {str(path.relative_to(fixtures)): digest(path) for path in fixtures.rglob('*') if path.is_file()},
                'Source fixtures changed.')
        report['aggregateSeconds'] = round(time.monotonic() - started, 3)
        require(report['aggregateSeconds'] < LIMITS['aggregateSeconds'], 'Aggregate durability bound exceeded.')
        report.update(status='passed', manifest=str(fixtures / 'manifest.json'),
                      limitations=['Kills the Host child at the authority transaction boundaries it reports; it does not '
                                   'interrupt SQLite inside a page write or model power loss.',
                                   'One fresh profile-34 world; no lived-save migration, multiplayer or party retreat.'])
    except Exception as error:
        report.update(status='failed', failure=str(error), traceback=traceback.format_exc())
    finally:
        report['sourceFixturesUnchanged'] = originals == {str(path.relative_to(fixtures)): digest(path)
                                                          for path in fixtures.rglob('*') if path.is_file()}
        report['artifactSha256'] = {path.name: digest(path) for path in work.iterdir() if path.is_file()}
        report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({key: report.get(key) for key in ('status', 'failure', 'workDirectory', 'aggregateSeconds')}, indent=2))
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
