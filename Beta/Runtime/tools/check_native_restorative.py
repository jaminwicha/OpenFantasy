#!/usr/bin/env python3
"""Continue earned ordinary-care fixtures through native controls and exact paused reopening.

The managed RestorativeNativeResultTests then authenticate the journal's exact effects.
This runner proves native input/render/reopen boundaries, not independent game-rule correctness.
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
    for option in ('dotnet', 'godot', 'fixtures', 'output'):
        parser.add_argument('--' + option, type=Path, required=True)
    parser.add_argument('--rendered', action='store_true')
    parser.add_argument('--journey', choices=('all', 'raw-pointer', 'raw-keyboard', 'crafted-controller', 'helper', 'revoke'), default='all')
    args = parser.parse_args()
    runtime = Path(__file__).resolve().parents[1]
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix='native-restorative-', dir=output.parent))
    client = runtime / 'client'
    project = (client / 'project.godot').read_bytes()
    sdk = args.dotnet.resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get('PATH', ''))
    routes = [('raw-pointer', 'raw-self', 'pointer'), ('raw-keyboard', 'raw-self', 'keyboard'),
              ('crafted-controller', 'crafted-self', 'controller'), ('helper', 'helper', 'helper'), ('revoke', 'revoke', 'revoke')]
    if args.journey != 'all':
        routes = [route for route in routes if route[0] == args.journey]
    report = dict(status='running', workDirectory=str(work), rendered=args.rendered, runs=[], journeys=[],
                  limits=dict(nativeProcessSeconds=180, nativeProbeSeconds=150, maximumNativeProcesses=2 * len(routes)),
                  startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope='Managed-earned source-stat injury and finite stock; native personal use, helper action, permission withdrawal and paused separate-process recovery.',
                  limitations=['Managed recorded-boundary authentication is required separately; UI observations alone do not prove authoritative item/time effects.',
                               'Injected input and background focus do not establish physical-device, OS-focus, art, performance or full-phase acceptance.'])

    def run(label, command):
        item = dict(label=label, command=list(map(str, command)), timeoutSeconds=180)
        report['runs'].append(item)
        stdout, stderr = work / (label + '.stdout.log'), work / (label + '.stderr.log')
        item.update(stdoutFile=str(stdout), stderrFile=str(stderr))
        started = time.monotonic()
        try:
            with stdout.open('wb') as out, stderr.open('wb') as err:
                result = subprocess.run(item['command'], cwd=runtime, env=env, stdout=out, stderr=err, timeout=180)
            item['exitCode'] = result.returncode
        except subprocess.TimeoutExpired:
            item['timedOut'] = True
            raise
        finally:
            item['seconds'] = time.monotonic() - started
        require(item['seconds'] < 180 and result.returncode == 0 and 'ERROR:' not in stdout.read_text() + stderr.read_text(),
                'Native process failed at ' + label)

    try:
        paths = [Path(__file__).resolve(), runtime / 'client/scripts/RestorativePlayProbe.cs']
        paths += [runtime / directory / ('OpenFantasy.' + assembly + '.dll')
                  for directory, assemblies in [('src/OpenFantasy.Host/bin/Debug/net10.0', ('Host', 'Core', 'Hosting')),
                                                ('client/.godot/mono/temp/bin/Debug', ('Client', 'Core', 'Hosting'))]
                  for assembly in assemblies]
        report['sourceSha256'] = {str(path): digest(path) for path in paths}
        launch = client
        if args.rendered:
            launch = work / 'background-client'
            launch.mkdir()
            for path in client.iterdir():
                if path.name != 'project.godot':
                    (launch / path.name).symlink_to(path, target_is_directory=path.is_dir())
            config = project.decode()
            require(config.count('[display]') == 1 and 'window/size/no_focus' not in config, 'Disposable no-focus configuration no longer matches')
            (launch / 'project.godot').write_text(config.replace('[display]', '[display]\nwindow/size/no_focus=true', 1))
        for label, fixture, control in routes:
            source = args.fixtures.resolve() / fixture
            input_files = [source / name for name in ('before.sqlite3', 'before.ofc', 'metadata.json')]
            pinned = {str(path): digest(path) for path in input_files}
            require(not any(Path(str(input_files[0]) + suffix).exists() for suffix in ('-wal', '-shm')), 'Fixture must be a closed backup')
            world, prefix = work / (label + '.sqlite3'), work / (label + '.json')
            shutil.copyfile(input_files[0], world)
            observations = {}
            for phase in ('active', 'reopen'):
                frozen = digest(world) if phase == 'reopen' else None
                command = [args.godot.resolve()]
                if not args.rendered:
                    command.append('--headless')
                command += ['--path', launch, '--', '--restorative-probe', world, prefix, source / 'metadata.json',
                            control if phase == 'active' else 'reopen', '--background-render']
                run(label + '-' + phase, command)
                observed = json.loads((work / (label + '.' + phase + '.json')).read_text())
                observations[phase] = observed
                require(observed['Kind'] == 'native-restorative-v1' and observed['Status'] == 'passed'
                        and observed['Mode'] == (control if phase == 'active' else 'reopen')
                        and observed['FocusRequests'] == 0 and observed['IsLocallyPaused']
                        and observed['DurationMilliseconds'] < 150000, 'Incomplete native restorative result')
                if args.rendered:
                    require(observed['Display'] != 'headless' and observed['Unfocusable'] and not observed['NativeFocused']
                            and not observed['ObservedNativeFocus'] and observed['StartupNoFocus'], 'Native background focus isolation failed')
                if phase == 'reopen':
                    require(digest(world) == frozen and observed['InitialHash'] == observed['FinalHash']
                            == observations['active']['FinalHash'], 'Separate paused process changed the save or public state')
            inputs = [row for row in observations['active']['Inputs'] if row['Stage'] == 'act-' + control]
            key = {'pointer': 'PointerControl', 'keyboard': 'Key', 'controller': 'Controller', 'revoke': 'PointerControl'}.get(control)
            if key:
                require(any(key in row for row in inputs), 'Requested native control was not exercised')
            else:
                require(inputs == [{'Stage': 'act-helper', 'Key': 'Escape'}] * 2,
                        'Helper journey must only resume then pause, without a player item action')
            if args.rendered:
                stages = ['inspection', 'final', 'reopen'] + ([] if control == 'revoke' else ['restoration'])
                require({path.name for path in work.glob(label + '.*.png')} == {label + '.' + stage + '.png' for stage in stages},
                        'Missing or unexpected rendered evidence')
            require(pinned == {str(path): digest(path) for path in input_files}, 'Earned input fixture changed')
            report['journeys'].append(dict(name=fixture, label=label, fixtureDirectory=str(source), database=str(world),
                                           activeReport=str(work / (label + '.active.json')), reopenReport=str(work / (label + '.reopen.json')),
                                           fixtureSha256=pinned, finalDatabaseSha256=digest(world)))
        require(report['sourceSha256'] == {str(path): digest(path) for path in paths}, 'Native source or binary changed during validation')
        report['status'] = 'passed'
        manifest = work / 'native-manifest.json'
        manifest.write_text(json.dumps(dict(journeys=report['journeys']), indent=2) + '\n')
        report['managedAuthenticationManifest'] = str(manifest)
    except Exception as error:
        report.update(status='failed', failure=str(error), traceback=traceback.format_exc())
    finally:
        report['originalProjectUnchanged'] = (client / 'project.godot').read_bytes() == project
        if not report['originalProjectUnchanged']:
            report['status'] = 'failed'
        report['artifactSha256'] = {str(path.relative_to(output.parent)): digest(path) for path in sorted(work.iterdir()) if path.is_file()}
        report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2) + '\n')
    print('native restorative: ' + report['status'], flush=True)
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
