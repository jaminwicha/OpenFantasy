#!/usr/bin/env python3
"""Run the native market refusal journeys (expired, unfunded, moved issuer) over closed boundary fixtures.

The prepared saves are immutable input; each journey runs in a fresh copy on the keyboard route. Managed
NativeTradingBoundaryResultTests authenticate the saved journals, refusals and recoveries independently;
rendered reports alone are insufficient.
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

ENTRY_SCENE = 'res://scenes/NativeTrading.tscn'
SCENARIOS = ('expired', 'unfunded', 'moved')
ROUTE = 'keyboard'
IMAGES = ('review', 'refused', 'recovered')
REFUSALS = dict(expired='expired', unfunded='You need', moved=('moved since', 'within reach'))


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


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
    work = Path(tempfile.mkdtemp(prefix='native-trading-boundaries-', dir=output.parent))
    source = args.fixtures.resolve()
    project = (client / 'project.godot').read_bytes()
    sdk = args.dotnet.resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get('PATH', ''))
    report = dict(status='running', workDirectory=str(work), rendered=args.rendered, entryScene=ENTRY_SCENE, route=ROUTE,
                  runs=[], results=[], coverage={}, startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  limits=dict(processSeconds=180, probeSeconds=150, openingSeconds=30, maximumNativeProcesses=len(SCENARIOS)),
                  limitations=['All three refusal journeys share the injected keyboard route; the normal journey qualifies '
                               'keyboard, controller and pointer selection.',
                               'Fixtures are closed real-authority saves prepared by ordinary session cutoffs; review, refusal, '
                               'attempted confirmation and recovery are native.',
                               'A refused confirmation is never submitted, so the journal holds no refusal receipt; managed tests '
                               'establish the reason from the recorded state at the attempt.',
                               'No physical device, OS focus, multiplayer Wait, art or production acceptance.'])
    try:
        manifest = json.loads((source / 'manifest.json').read_text())
        require(manifest['kind'] == 'native-trading-boundary-fixtures-v1' and set(manifest['scenarios']) == set(SCENARIOS),
                'Boundary fixture manifest is incomplete.')
        fixture_paths = [source / scenario / name for scenario in SCENARIOS
                         for name in ('save.sqlite', 'genesis.ofc', 'before.ofc', 'metadata.json')] + [source / 'manifest.json']
        pinned = {str(path): digest(path) for path in fixture_paths}
        require(not any(Path(str(source / scenario / 'save.sqlite') + suffix).exists()
                        for scenario in SCENARIOS for suffix in ('-wal', '-shm')), 'Fixture databases must be closed.')
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
        for scenario in SCENARIOS:
            fixture = source / scenario
            save, prefix = work / (scenario + '.sqlite'), work / (scenario + '.json')
            shutil.copyfile(fixture / 'save.sqlite', save)
            command = [str(args.godot.resolve())]
            if not args.rendered:
                command.append('--headless')
            command += ['--path', str(launch), ENTRY_SCENE, '--', '--trading-boundary-probe', str(save), str(prefix),
                        str(fixture / 'genesis.ofc'), scenario, ROUTE]
            if args.rendered:
                command.append('--background-render')
            stdout, stderr = work / (scenario + '.stdout.log'), work / (scenario + '.stderr.log')
            row = dict(label=scenario, command=command, timeoutSeconds=180, stdout=str(stdout), stderr=str(stderr))
            report['runs'].append(row)
            start = time.monotonic()
            try:
                with stdout.open('xb') as out, stderr.open('xb') as err:
                    result = subprocess.run(command, cwd=runtime, env=env, stdout=out, stderr=err, timeout=180)
                row['exitCode'] = result.returncode
            finally:
                row['seconds'] = round(time.monotonic() - start, 3)
            require(row['seconds'] < 180 and result.returncode == 0
                    and 'ERROR:' not in stdout.read_text() + stderr.read_text(), 'Native failure at ' + scenario)
            observed = json.loads(prefix.read_text())
            require(observed['Kind'] == 'native-trading-boundary-v1' and observed['Status'] == 'passed'
                    and observed['Scenario'] == scenario and observed['Route'] == ROUTE and observed['IsLocallyPaused']
                    and observed['FocusRequests'] == 0 and observed['DurationMilliseconds'] < 150000, 'Incomplete boundary report.')
            expected = REFUSALS[scenario]
            require(any(text in observed['Refusal'] for text in ((expected,) if isinstance(expected, str) else expected)),
                    'The refusal does not name the intended reason: ' + scenario)
            require(observed['AttemptStep'] is not None and observed['QuoteId'] and observed['RecoveredQuoteId'],
                    'Refusal or recovery evidence missing.')
            require(all('Controller' not in row and 'PointerControl' not in row for row in observed['Inputs']),
                    'Another route was substituted.')
            attempts = [row for row in observed['Inputs'] if row['Stage'] == 'refuse' and row.get('Received') == 'ConfirmTrade']
            require(attempts and all(row['Focused'] for row in attempts), 'The refused Confirm did not receive the route activation.')
            if args.rendered:
                require(observed['Display'] != 'headless' and observed['Unfocusable'] and not observed['NativeFocused']
                        and not observed['ObservedNativeFocus'] and observed['StartupNoFocus'], 'Background isolation failed.')
                require({path.name for path in work.glob(scenario + '.*.png')} == {scenario + '.' + image + '.png' for image in IMAGES},
                        'Rendered boundary evidence incomplete.')
            report['coverage'][scenario] = dict(refusal=observed['Refusal'], attemptStep=observed['AttemptStep'],
                                                quote=observed['QuoteId'], recoveredQuote=observed['RecoveredQuoteId'],
                                                checks=observed['Checks'], physicalDevice=False)
            report['results'].append(dict(scenario=scenario, save=str(save), genesis=str(fixture / 'genesis.ofc'),
                                          before=str(fixture / 'before.ofc'), metadata=str(fixture / 'metadata.json'),
                                          report=str(prefix), finalDatabaseSha256=digest(save),
                                          images=sorted(str(path) for path in work.glob(scenario + '.*.png'))))
        require(pinned == {str(path): digest(path) for path in fixture_paths}, 'Boundary fixture mutated.')
        require(report['executionPins'] == {str(path): digest(path) for path in paths}, 'Execution sources or binaries changed.')
        native_manifest = work / 'native-manifest.json'
        native_manifest.write_text(json.dumps(dict(results=report['results']), indent=2) + '\n')
        report.update(status='passed', managedAuthenticationManifest=str(native_manifest))
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
