#!/usr/bin/env python3
"""Drive the native retained-world gift journey and its separate paused reopening.

The exported retained fixture is immutable input; each run copies it. Managed RetainedGiftResultTests
authenticate the saved journal against the pinned original source through OPENFANTASY_RETAINED_GIFT_RESULTS;
rendered reports alone are insufficient. Injected background input is never physical/OS/human acceptance.
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

OPENING_SECONDS = 30
PROBE_SECONDS = 150
PROCESS_SECONDS = 180
FIXTURE_FILES = ('save.sqlite', 'genesis.ofc', 'transition.ofc', 'before.ofc', 'metadata.json')
RENDERED_STAGES = {'active': ('pack', 'restoration', 'final'), 'reopen': ('history', 'reopen')}


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def execution_paths(client, dotnet, godot):
    paths = [Path(__file__).resolve(), client / 'project.godot', dotnet, godot]
    paths += [path for directory in ('scripts', 'scenes', 'assets/ui') for path in (client / directory).rglob('*')
              if path.is_file() and path.suffix in ('.cs', '.tscn', '.svg')]
    assemblies = list((client / '.godot/mono/temp/bin/Debug').glob('OpenFantasy*.dll'))
    require(len(assemblies) >= 3, 'Missing built client assemblies.')
    return paths + assemblies


def background_launch(client, work, project):
    """A disposable launch directory applies no-focus before window creation; shared settings stay unchanged."""
    launch = work / 'background-client'
    launch.mkdir()
    for path in client.iterdir():
        if path.name != 'project.godot':
            (launch / path.name).symlink_to(path, target_is_directory=path.is_dir())
    config = project.decode()
    require(config.count('[display]') == 1 and 'window/size/no_focus' not in config, 'No-focus configuration changed.')
    (launch / 'project.godot').write_text(config.replace('[display]', '[display]\nwindow/size/no_focus=true', 1))
    return launch


def run_phase(args, launch, runtime, env, work, save, prefix, fixture, phase, report):
    command = [str(args.godot.resolve())]
    if not args.rendered:
        command.append('--headless')
    command += ['--path', str(launch), '--', '--retained-gift-probe', str(save), str(prefix),
                str(fixture / 'genesis.ofc'), str(fixture / 'metadata.json'),
                'keyboard' if phase == 'active' else 'reopen', '--background-render']
    stdout, stderr = work / (phase + '.stdout.log'), work / (phase + '.stderr.log')
    row = dict(label=phase, command=command, timeoutSeconds=PROCESS_SECONDS, stdout=str(stdout), stderr=str(stderr))
    report['runs'].append(row)
    start = time.monotonic()
    result = None
    try:
        with stdout.open('xb') as out, stderr.open('xb') as err:
            result = subprocess.run(command, cwd=runtime, env=env, stdout=out, stderr=err, timeout=PROCESS_SECONDS)
        row['exitCode'] = result.returncode
    finally:
        row['seconds'] = round(time.monotonic() - start, 3)
    require(row['seconds'] < PROCESS_SECONDS and result.returncode == 0
            and 'ERROR:' not in stdout.read_text() + stderr.read_text(), 'Native failure at ' + phase)
    observed = json.loads((work / ('journey.' + phase + '.json')).read_text())
    require(observed['Kind'] == 'native-retained-gifts-v1' and observed['Status'] == 'passed'
            and observed['Mode'] == ('keyboard' if phase == 'active' else 'reopen')
            and observed['IsLocallyPaused'] and observed['FocusRequests'] == 0
            and observed['OpeningMilliseconds'] < OPENING_SECONDS * 1000
            and observed['DurationMilliseconds'] < PROBE_SECONDS * 1000, 'Incomplete native retained gift report.')
    require(all(wait['Seconds'] < wait['LimitSeconds'] for wait in observed['Waits']), 'A successful wait exceeded its limit.')
    if args.rendered:
        require(observed['Display'] != 'headless' and observed['Unfocusable'] and not observed['NativeFocused']
                and not observed['ObservedNativeFocus'] and observed['StartupNoFocus'], 'Background isolation failed.')
        require({path.name for path in work.glob('journey.*.png')} >= {'journey.' + stage + '.png' for stage in RENDERED_STAGES[phase]},
                'Rendered retained gift evidence incomplete at ' + phase)
    return observed


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('dotnet', 'godot', 'fixtures', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--rendered', action='store_true')
    args = parser.parse_args()
    runtime = Path(__file__).resolve().parents[1]
    client = runtime / 'client'
    output = args.output.resolve()
    require(not output.exists(), 'Never replace an earlier result.')
    output.parent.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix='native-retained-gifts-', dir=output.parent))
    source = args.fixtures.resolve()
    project = (client / 'project.godot').read_bytes()
    sdk = args.dotnet.resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get('PATH', ''))
    report = dict(status='running', workDirectory=str(work), rendered=args.rendered, runs=[], results=[],
                  startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  limits=dict(openingSeconds=OPENING_SECONDS, probeSeconds=PROBE_SECONDS,
                              processSeconds=PROCESS_SECONDS, maximumNativeProcesses=2),
                  limitations=['Managed labeled preparation precedes native input; see the fixture metadata.',
                               'Injected keyboard events with explicit Control focus; no physical-device, OS-focus, art or human acceptance.',
                               'RetainedGiftResultTests must authenticate the saved journal against the pinned original source.',
                               'No process-kill or transition-boundary interruption is exercised.'])
    try:
        fixture_paths = [source / name for name in FIXTURE_FILES]
        pinned = {str(path): digest(path) for path in fixture_paths}
        require(not any(Path(str(source / 'save.sqlite') + suffix).exists() for suffix in ('-wal', '-shm')),
                'Fixture database must be closed.')
        paths = execution_paths(client, args.dotnet.resolve(), args.godot.resolve())
        report['executionPins'] = {str(path): digest(path) for path in paths}
        report['fixturePins'] = pinned
        report['runtime'] = {tool: subprocess.run([str(path), '--version'], cwd=runtime, env=env, capture_output=True,
                                                  text=True, timeout=30).stdout.strip()
                             for tool, path in (('dotnet', args.dotnet.resolve()), ('godot', args.godot.resolve()))}
        # Every native process works on a new copy; the exported fixture itself is never opened for writing.
        fixture = work / 'fixture'
        fixture.mkdir()
        for path in fixture_paths:
            shutil.copyfile(path, fixture / path.name)
        save, prefix = work / 'journey.sqlite', work / 'journey.json'
        shutil.copyfile(fixture / 'save.sqlite', save)
        launch = background_launch(client, work, project) if args.rendered else client
        active = run_phase(args, launch, runtime, env, work, save, prefix, fixture, 'active', report)
        frozen = digest(save)
        reopen = run_phase(args, launch, runtime, env, work, save, prefix, fixture, 'reopen', report)
        require(digest(save) == frozen and reopen['InitialHash'] == reopen['FinalHash'] == active['FinalHash'],
                'Paused reopening changed the save or public state.')
        require(any('Key' in row for row in active['Inputs'] if row['Stage'] == 'accept')
                and any('Key' in row for row in active['Inputs'] if row['Stage'] == 'self-use'), 'Requested keyboard input missing.')
        require(pinned == {str(path): digest(path) for path in fixture_paths}, 'Exported fixture mutated.')
        require(report['executionPins'] == {str(path): digest(path) for path in paths}, 'Execution sources or binaries changed.')
        report['results'].append(dict(mode='keyboard', save=str(save), genesis=str(fixture / 'genesis.ofc'),
                                      transition=str(fixture / 'transition.ofc'), before=str(fixture / 'before.ofc'),
                                      metadata=str(fixture / 'metadata.json'), report=str(work / 'journey.active.json'),
                                      reopenReport=str(work / 'journey.reopen.json'), finalDatabaseSha256=digest(save)))
        manifest = work / 'native-manifest.json'
        manifest.write_text(json.dumps(dict(results=report['results']), indent=2) + '\n')
        report.update(status='passed', managedAuthenticationManifest=str(manifest),
                      nextStep='Run RetainedGiftResultTests with OPENFANTASY_RETAINED_GIFT_RESULTS and OPENFANTASY_RETAINED_PROPOSAL_SOURCE.')
    except Exception as error:
        report.update(status='failed', failure=str(error), traceback=traceback.format_exc())
    finally:
        report['originalProjectUnchanged'] = project == (client / 'project.godot').read_bytes()
        if not report['originalProjectUnchanged']:
            report['status'] = 'failed'
        report['artifactSha256'] = {path.name: digest(path) for path in work.iterdir() if path.is_file()}
        report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({key: report.get(key) for key in ('status', 'failure', 'workDirectory', 'managedAuthenticationManifest')}, indent=2))
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
