"""Real process interruption around profile-35 outcome readings and world-owned completion.

For every exported case (the player's reading, the other human's reading and the completion boundary) the closed save
taken just before the publication is copied three times. One copy executes the exact selected step normally. The other
two run the existing `--probe-world-step-crash` seam, which stops at the authority's own BeforeCommit or AfterCommit
transaction boundary and waits; this tool then SIGKILLs only that child process. Recovery is the ordinary full verified
journal recovery of `--inspect-recorded-world`; an uncommitted step is retried and every committed one is refused as a
duplicate without changing bytes. Logs, recovered copies and the exact source fixtures are preserved for
EncounterOutcomeDurabilityResultTests, which compares every recovered journal with the unbroken originals.
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

LIMITS = dict(runSeconds=30, signalSeconds=30, killSeconds=10, aggregateSeconds=600)
LABELS = ['read-player', 'read-human', 'complete']


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
    work = output.parent / ('encounter-outcome-durability-' + datetime.datetime.now(datetime.timezone.utc).strftime('%H%M%S%f'))
    require(not work.is_relative_to(fixtures), 'Work must stay outside the source fixtures.')
    work.mkdir(parents=True)
    host = runtime / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll'
    command = [str(args.dotnet.resolve()), str(host)]
    env = dict(os.environ, DOTNET_ROOT=str(args.dotnet.resolve().parent))
    report = dict(status='running', workDirectory=str(work), fixtures=str(fixtures), limits=LIMITS, runs=[], processKills=[],
                  cases=[], startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  execution=dict(dotnet=str(args.dotnet.resolve()), host=str(host), hostSha256=digest(host)))
    originals = {str(path.relative_to(fixtures)): digest(path) for path in fixtures.rglob('*') if path.is_file()}

    def log_paths(label):
        index = len(report['runs']) + len(report['processKills'])
        return work / f'{index:02d}-{label}.stdout.log', work / f'{index:02d}-{label}.stderr.log'

    def run(label, *arguments, fail=False):
        stdout, stderr = log_paths(label)
        row = dict(label=label, arguments=list(map(str, arguments)), stdout=str(stdout), stderr=str(stderr))
        report['runs'].append(row)
        start = time.monotonic()
        with stdout.open('xb') as out, stderr.open('xb') as err:
            done = subprocess.run(command + list(map(str, arguments)), cwd=runtime, env=env, stdout=out, stderr=err,
                                  timeout=LIMITS['runSeconds'])
        elapsed = time.monotonic() - start
        row.update(seconds=round(elapsed, 3), exitCode=done.returncode)
        text, errors = stdout.read_text(errors='replace'), stderr.read_text(errors='replace')
        require(elapsed < LIMITS['runSeconds'], label + ' finished only after the per-operation bound.')
        require(not errors, label + ' wrote errors: ' + errors[:400])
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
            signal_elapsed = time.monotonic() - start
            stdout.write_text(line)
            row.update(signalSeconds=round(signal_elapsed, 3), signal=line.strip())
            # A successful wait is still bounded: the measured interval itself must lie within the unchanged limit.
            require(signal_elapsed < LIMITS['signalSeconds'], label + ' reached its boundary only after the signal bound.')
            expected = 'BeforeCommit' if boundary == 'before' else 'AfterCommit'
            require(json.loads(line) == {'Boundary': expected}, label + ' did not reach ' + expected)
            require(process.poll() is None, label + ' exited before it was killed.')
            start = time.monotonic()
            process.kill()  # SIGKILL of this tool's own child only; no finally, flush or Dispose runs in it.
            row['exitCode'] = process.wait(timeout=LIMITS['killSeconds'])
            kill_elapsed = time.monotonic() - start
            row['killSeconds'] = round(kill_elapsed, 3)
            require(kill_elapsed < LIMITS['killSeconds'], label + ' terminated only after the kill bound.')
            require(row['exitCode'] == -9, label + ' was not terminated by SIGKILL.')
            row['successfulWaitsWithinBounds'] = True
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
        require(value['ProfileVersion'] == 35, label + ' changed its interpretation.')

    try:
        manifest = json.loads((fixtures / 'manifest.json').read_text())
        require(manifest['Kind'] == 'encounter-outcome-durability-fixtures-v1'
                and [case['Label'] for case in manifest['Cases']] == LABELS, 'Unexpected fixture manifest.')
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
        # Re-assert every recorded successful wait against its bound, so the report itself carries the checked evidence.
        require(all(row.get('successfulWaitsWithinBounds') and row['signalSeconds'] < LIMITS['signalSeconds']
                    and row['killSeconds'] < LIMITS['killSeconds'] for row in report['processKills']),
                'A recorded signal or kill wait exceeded its bound.')
        require(all(row['seconds'] < LIMITS['runSeconds'] for row in report['runs']), 'A recorded operation exceeded its bound.')
        report['successfulWaitChecks'] = dict(signals=len(report['processKills']), kills=len(report['processKills']),
                                              operations=len(report['runs']))
        require(originals == {str(path.relative_to(fixtures)): digest(path) for path in fixtures.rglob('*') if path.is_file()},
                'Source fixtures changed.')
        report['aggregateSeconds'] = round(time.monotonic() - started, 3)
        require(report['aggregateSeconds'] < LIMITS['aggregateSeconds'], 'Aggregate durability bound exceeded.')
        report.update(status='passed', manifest=str(fixtures / 'manifest.json'),
                      limitations=['Kills the Host child at the authority transaction boundaries it reports; it does not '
                                   'interrupt SQLite inside a page write or model power loss.',
                                   'One fresh declared two-human profile-35 fixture driven by a local managed session; no '
                                   'network, device, lived-save migration or native presentation is implied.'])
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
