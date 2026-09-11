"""Qualify the native quick wheel and its exact paused-world reopening using disposable saves."""
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
    work = Path(tempfile.mkdtemp(prefix='native-wheel-', dir=output.parent))
    sdk = Path(args.dotnet).resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get('PATH', ''))
    report = dict(status='running', rendered=args.rendered, startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope='Native wheel input, inspection, real gathering and exact reopened world; injected events are not physical device acceptance', runs=[], observations={})
    try:
        for phase in ['fresh', 'reopen']:
            scene_report = work / (phase + '.json')
            database = work / 'world.sqlite'
            if phase == 'reopen':
                before = hashlib.sha256(database.read_bytes()).hexdigest()
            command = [str(Path(args.godot).resolve())]
            if not args.rendered:
                command.append('--headless')
            command += ['--path', str(root/'client'), '--', '--farm-wheel', str(database), str(scene_report), phase]
            result = subprocess.run(command, cwd=root, env=env, capture_output=True, text=True, timeout=45)
            report['runs'].append(dict(phase=phase, command=command, exitCode=result.returncode, stdout=result.stdout, stderr=result.stderr))
            if scene_report.exists():
                report['observations'][phase] = json.loads(scene_report.read_text())
            if result.returncode or 'ERROR:' in result.stdout + result.stderr:
                raise RuntimeError('Native wheel failed at ' + phase)
            observation = report['observations'][phase]
            if observation.get('Status') != 'passed' or not observation.get('Runtime') or len(observation.get('Checks', [])) < (3 if phase == 'fresh' else 1):
                raise RuntimeError('Incomplete native wheel result')
            if phase == 'reopen':
                if observation['FinalView'] != report['observations']['fresh']['FinalView'] or hashlib.sha256(database.read_bytes()).hexdigest() != before:
                    raise RuntimeError('Reopening changed the paused world or save bytes')
        if args.rendered and len(list(work.glob('*.png'))) != 4:
            raise RuntimeError('Missing rendered wheel states')
        report['status'] = 'passed'
    except Exception as error:
        report['status'], report['failure'] = 'failed', str(error)
    finally:
        report['artifactSha256'] = {str(p.relative_to(output.parent)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(work.iterdir()) if p.is_file()}
        report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2)+'\n')
    print('native wheel: '+report['status'], flush=True)
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
