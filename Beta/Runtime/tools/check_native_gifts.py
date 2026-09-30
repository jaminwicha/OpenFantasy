#!/usr/bin/env python3
"""Qualify injected native gift controls and separate-process paused reopening.

The exported fixtures are immutable input. Managed NativeGiftResultTests authenticate
actual journal identities and effects separately; rendered reports alone are insufficient.
"""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import time
import traceback


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('dotnet', 'godot', 'fixtures', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--mode', choices=('all', 'keyboard', 'controller', 'pointer'), default='all')
    parser.add_argument('--rendered', action='store_true')
    args = parser.parse_args()
    runtime = Path(__file__).resolve().parents[1]
    client = runtime / 'client'
    output = args.output.resolve()
    require(not output.exists(), 'Never replace an earlier result.')
    output.parent.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix='native-gifts-', dir=output.parent))
    source = args.fixtures.resolve()
    project = (client / 'project.godot').read_bytes()
    modes = ('keyboard', 'controller', 'pointer') if args.mode == 'all' else (args.mode,)
    sdk = args.dotnet.resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get('PATH', ''))
    report = dict(status='running', workDirectory=str(work), rendered=args.rendered, runs=[], results=[],
                  startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  limits=dict(processSeconds=180, probeSeconds=150, maximumNativeProcesses=len(modes)*2),
                  limitations=['Forms use injected keyboard dropdowns and explicit Control focus; the selected mode qualifies wheel commitment.',
                               'Independent recorded-boundary authentication remains required.',
                               'No physical-device, OS-focus, art, retained-save migration or production acceptance.'])
    try:
        fixture_paths = [source / name for name in ('save.sqlite', 'genesis.ofc', 'before.ofc', 'metadata.json')]
        pinned = {str(path): digest(path) for path in fixture_paths}
        require(not any(Path(str(source / 'save.sqlite') + suffix).exists() for suffix in ('-wal', '-shm')),
                'Fixture database must be closed.')
        paths = [Path(__file__).resolve(), client / 'project.godot', args.dotnet.resolve(), args.godot.resolve()]
        paths += [path for directory in ('scripts', 'scenes', 'assets/ui') for path in (client / directory).rglob('*')
                  if path.is_file() and path.suffix in ('.cs', '.tscn', '.svg')]
        paths += list((client / '.godot/mono/temp/bin/Debug').glob('OpenFantasy*.dll'))
        require(len(list((client / '.godot/mono/temp/bin/Debug').glob('OpenFantasy*.dll'))) >= 3, 'Missing built client assemblies.')
        report['executionPins'] = {str(path): digest(path) for path in paths}
        report['fixturePins'] = pinned
        launch = client
        if args.rendered:
            launch = work / 'background-client'
            launch.mkdir()
            for path in client.iterdir():
                if path.name != 'project.godot':
                    (launch / path.name).symlink_to(path, target_is_directory=path.is_dir())
            config = project.decode()
            require(config.count('[display]') == 1 and 'window/size/no_focus' not in config, 'No-focus configuration changed.')
            (launch / 'project.godot').write_text(config.replace('[display]', '[display]\nwindow/size/no_focus=true', 1))
        for mode in modes:
            save, prefix = work / (mode + '.sqlite'), work / (mode + '.json')
            shutil.copyfile(source / 'save.sqlite', save)
            observations = {}
            for phase in ('active', 'reopen'):
                frozen = digest(save) if phase == 'reopen' else None
                command = [str(args.godot.resolve())]
                if not args.rendered:
                    command.append('--headless')
                command += ['--path', str(launch), '--', '--gift-probe', str(save), str(prefix),
                            str(source / 'genesis.ofc'), mode if phase == 'active' else 'reopen', '--background-render']
                label = mode + '-' + phase
                stdout, stderr = work / (label + '.stdout.log'), work / (label + '.stderr.log')
                row = dict(label=label, command=command, timeoutSeconds=180, stdout=str(stdout), stderr=str(stderr))
                report['runs'].append(row)
                start = time.monotonic()
                try:
                    with stdout.open('xb') as out, stderr.open('xb') as err:
                        result = subprocess.run(command, cwd=runtime, env=env, stdout=out, stderr=err, timeout=180)
                    row['exitCode'] = result.returncode
                finally:
                    row['seconds'] = round(time.monotonic()-start, 3)
                require(row['seconds'] < 180 and result.returncode == 0
                        and 'ERROR:' not in stdout.read_text()+stderr.read_text(), 'Native failure at ' + label)
                observed = json.loads((work / (mode + '.' + phase + '.json')).read_text())
                observations[phase] = observed
                require(observed['Kind'] == 'native-gifts-v1' and observed['Status'] == 'passed'
                        and observed['Mode'] == (mode if phase == 'active' else 'reopen')
                        and observed['IsLocallyPaused'] and observed['FocusRequests'] == 0
                        and observed['DurationMilliseconds'] < 150000, 'Incomplete native gift report.')
                if args.rendered:
                    require(observed['Display'] != 'headless' and observed['Unfocusable'] and not observed['NativeFocused']
                            and not observed['ObservedNativeFocus'] and observed['StartupNoFocus'], 'Background isolation failed.')
                if phase == 'reopen':
                    require(digest(save) == frozen and observed['InitialHash'] == observed['FinalHash']
                            == observations['active']['FinalHash'], 'Paused reopen changed save/public state.')
            key = dict(keyboard='Key', controller='Controller', pointer='PointerControl')[mode]
            for action in ('reply', 'create', 'withdraw'):
                require(any(key in row for row in observations['active']['Inputs'] if row['Stage'] == action+'-'+mode),
                        'Requested wheel input missing: ' + action)
            require(observations['active']['ReplyStatus'] == ('Declined' if mode == 'pointer' else 'Accepted'), 'Wrong reply route.')
            if args.rendered:
                require({path.name for path in work.glob(mode+'.*.png')} ==
                        {mode+'.'+stage+'.png' for stage in ('inspection', 'pack', 'history', 'final', 'reopen')},
                        'Rendered gift evidence incomplete.')
            report['results'].append(dict(mode=mode, save=str(save), genesis=str(source/'genesis.ofc'),
                before=str(source/'before.ofc'), metadata=str(source/'metadata.json'), report=str(work/(mode+'.active.json')),
                reopenReport=str(work/(mode+'.reopen.json')), finalDatabaseSha256=digest(save)))
        require(pinned == {str(path): digest(path) for path in fixture_paths}, 'Earned fixture mutated.')
        require(report['executionPins'] == {str(path): digest(path) for path in paths}, 'Execution sources or binaries changed.')
        manifest = work / 'native-manifest.json'
        manifest.write_text(json.dumps(dict(results=report['results']), indent=2)+'\n')
        report.update(status='passed', managedAuthenticationManifest=str(manifest))
    except Exception as error:
        report.update(status='failed', failure=str(error), traceback=traceback.format_exc())
    finally:
        report['originalProjectUnchanged'] = project == (client/'project.godot').read_bytes()
        if not report['originalProjectUnchanged']:
            report['status'] = 'failed'
        report['artifactSha256'] = {path.name: digest(path) for path in work.iterdir() if path.is_file()}
        report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps({key: report.get(key) for key in ('status', 'failure', 'workDirectory', 'managedAuthenticationManifest')}, indent=2))
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
