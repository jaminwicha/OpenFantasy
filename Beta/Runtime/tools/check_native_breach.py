"""Exercise the finite breach through actual native controls across three separately reopened processes."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
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
    work = Path(tempfile.mkdtemp(prefix='native-breach-', dir=output.parent))
    sdk = Path(args.dotnet).resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get('PATH', ''))
    report = dict(status='running', rendered=args.rendered, startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope='Injected native input, partial excavation restart, final breach, conserved rendered flow and paused restart; no physical-device/art/full-phase acceptance',
                  runs=[], observations={})
    try:
        for phase in ['fresh', 'breach', 'reopen']:
            database = work / 'world.sqlite'
            base = work / 'journey.json'
            before = hashlib.sha256(database.read_bytes()).hexdigest() if phase == 'reopen' else None
            command = [str(Path(args.godot).resolve())]
            if not args.rendered:
                command.append('--headless')
            command += ['--path', str(root / 'client'), '--', '--breach-probe', str(database), str(base), phase, '--background-render']
            result = subprocess.run(command, cwd=root, env=env, capture_output=True, text=True, timeout=210)
            report['runs'].append(dict(phase=phase, command=command, exitCode=result.returncode, stdout=result.stdout, stderr=result.stderr))
            path = work / ('journey.' + phase + '.json')
            failed = work / ('journey.' + phase + '.failed.json')
            if path.exists() or failed.exists():
                report['observations'][phase] = json.loads((path if path.exists() else failed).read_text())
            if result.returncode or 'ERROR:' in result.stdout + result.stderr:
                raise RuntimeError('Native breach failed at ' + phase)
            observed = report['observations'][phase]
            if observed.get('Status') != 'passed' or observed.get('FocusRequests') != 0 or not observed.get('IsLocallyPaused'):
                raise RuntimeError('Incomplete native breach result at ' + phase)
            if phase == 'reopen' and hashlib.sha256(database.read_bytes()).hexdigest() != before:
                raise RuntimeError('Paused reopening changed save bytes')
        expected_images = {'journey.' + stage + '.png' for stage in
                           ['divider', 'mining-wheel', 'fresh', 'flowing', 'breach', 'reopen']}
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
    print('native breach: ' + report['status'], flush=True)
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
