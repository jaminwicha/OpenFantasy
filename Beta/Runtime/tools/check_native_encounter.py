"""Exercise the in-place encounter through actual native controls across three separately reopened processes."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sqlite3
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', required=True)
    parser.add_argument('--godot', required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--rendered', action='store_true')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix='native-encounter-', dir=output.parent))
    sdk = Path(args.dotnet).resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get('PATH', ''))
    client = root / 'client'
    original_project = (client / 'project.godot').read_bytes()
    launch_client = client
    if args.rendered:
        # This setting must apply before the native window is created. Never temporarily change
        # the shared project or other running games. Resources/compiled assemblies stay identical.
        launch_client = work / 'background-client'
        launch_client.mkdir()
        for entry in client.iterdir():
            if entry.name != 'project.godot':
                (launch_client / entry.name).symlink_to(entry, target_is_directory=entry.is_dir())
        config = original_project.decode().replace('[display]', '[display]\nwindow/size/no_focus=true', 1)
        (launch_client / 'project.godot').write_text(config)
    report = dict(status='running', rendered=args.rendered, startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope='Injected native input, entry, actual attack, required-choice holds, mid-duel restart, real outcome release and paused restart; no physical-device/art/full-phase acceptance',
                  runs=[], observations={})
    try:
        for phase in ['fresh', 'duel', 'reopen']:
            database = work / 'world.sqlite'
            base = work / 'journey.json'
            before = hashlib.sha256(database.read_bytes()).hexdigest() if phase == 'reopen' else None
            command = [str(Path(args.godot).resolve())]
            if not args.rendered:
                command.append('--headless')
            command += ['--path', str(launch_client), '--', '--encounter-probe', str(database), str(base), phase, '--background-render']
            result = subprocess.run(command, cwd=root, env=env, capture_output=True, text=True, timeout=180)
            report['runs'].append(dict(phase=phase, command=command, exitCode=result.returncode, stdout=result.stdout, stderr=result.stderr))
            path = work / ('journey.' + phase + '.json')
            failed = work / ('journey.' + phase + '.failed.json')
            if path.exists() or failed.exists():
                report['observations'][phase] = json.loads((path if path.exists() else failed).read_text())
            if result.returncode or 'ERROR:' in result.stdout + result.stderr:
                raise RuntimeError('Native encounter failed at ' + phase)
            observed = report['observations'][phase]
            if observed.get('Status') != 'passed' or observed.get('FocusRequests') != 0 or not observed.get('IsLocallyPaused'):
                raise RuntimeError('Incomplete native encounter result at ' + phase)
            if args.rendered and (observed.get('Display') == 'headless' or not observed.get('Unfocusable')
                    or observed.get('NativeFocused') or observed.get('ObservedNativeFocus') or not observed.get('StartupNoFocus')):
                raise RuntimeError('Rendered encounter fixture did not preserve background focus isolation')
            if phase == 'reopen' and hashlib.sha256(database.read_bytes()).hexdigest() != before:
                raise RuntimeError('Paused reopening changed save bytes')
        # Compare exact committed boundaries, not a UI view sampled before the queued entry.
        connection = sqlite3.connect(database)
        try:
            lifecycle_revisions = [int.from_bytes(row[0], 'big') for row in connection.execute(
                'SELECT sequence FROM world_commits WHERE kind=8 ORDER BY sequence')]
        finally:
            connection.close()  # No competing reader during the Host's exclusive transaction.
        if len(lifecycle_revisions) != 4:
            raise RuntimeError('Expected entry, two staging boundaries and release')
        revisions = [lifecycle_revisions[0] - 1, *lifecycle_revisions[:3]]
        states = []
        for revision in revisions:
            command = [str(Path(args.dotnet).resolve()), str(root / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll'),
                       '--inspect-recorded-world', str(database), str(revision)]
            result = subprocess.run(command, cwd=root, env=env, capture_output=True, text=True, timeout=30)
            report.setdefault('recordedBoundaryRuns', []).append(dict(command=command, exitCode=result.returncode,
                stdout=result.stdout, stderr=result.stderr))
            if result.returncode:
                raise RuntimeError('Recorded boundary inspection failed')
            states.append(json.loads(result.stdout))
        positions = lambda state: {actor['ActorId']: actor['Position'] for actor in state['Actors']}
        if [state['EncounterLifecycle']['Phase'] for state in states] != ['Exploration', 'Confrontation', 'Staging', 'Active']:
            raise RuntimeError('Incorrect entry phase sequence')
        if any(positions(state) != positions(states[0]) or state['Calendar'] != states[0]['Calendar'] for state in states[1:]):
            raise RuntimeError('Entry or staging changed actual positions or granted time')
        report['exactEntryBoundariesVerified'] = revisions
        if (client / 'project.godot').read_bytes() != original_project:
            raise RuntimeError('The shared native project settings changed during qualification')
        report['originalProjectSha256'] = hashlib.sha256(original_project).hexdigest()
        expected_images = {'journey.' + stage + '.png' for stage in
                           ['entry', 'waiting', 'wait-inspection', 'damage', 'fresh', 'outcome', 'duel', 'reopen']}
        if args.rendered and {path.name for path in work.glob('*.png')} != expected_images:
            raise RuntimeError('Missing rendered breach states')
        report['status'] = 'passed'
    except Exception as error:
        report['status'], report['failure'] = 'failed', str(error)
    finally:
        report['artifactSha256'] = {str(path.relative_to(output.parent)): hashlib.sha256(path.read_bytes()).hexdigest()
                                   for path in sorted(work.iterdir()) if path.is_file()}
        report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2) + '\n')
    print('native encounter: ' + report['status'], flush=True)
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
