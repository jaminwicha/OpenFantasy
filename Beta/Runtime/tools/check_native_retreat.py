"""Runs the native retreat journey over an exported fixture: three device routes and one separate paused reopen.

Each route copies the closed fixture save, launches the explicit NativeRetreat entry with the disposable retreat probe
and checks its report: one current first opening at the ready boundary (never closed and retried), device-specific
Retreat selection, a cancelled inspection that sends nothing, the committed route presented as ordinary 32-subunit
steps, the disengagement notice and held-key travel. The keyboard route then walks to Marla and buys one ration
through the stall panel. Its save is reopened in a separate paused process. The manifest names every route for
NativeRetreatResultTests to authenticate against the saved journals.
"""
import argparse
import datetime
import hashlib
import json
import os
import shutil
import subprocess
import tempfile
import time
import traceback
from pathlib import Path

ENTRY_SCENE = 'res://scenes/NativeRetreat.tscn'
ROUTES = ('keyboard', 'controller', 'pointer')
LIMITS = dict(openingSeconds=30, probeSeconds=300, processSeconds=330, reopenProcessSeconds=90, aggregateSeconds=900)
IMAGES = ('retreat-inspected', 'retreating', 'disengaged', 'final')
TRADE_IMAGES = ('marla', 'buy-review', 'bought')


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def run(command, cwd, env, work, label, timeout, report):
    stdout, stderr = work / (label + '.stdout.log'), work / (label + '.stderr.log')
    row = dict(label=label, command=command, timeoutSeconds=timeout, stdout=str(stdout), stderr=str(stderr))
    report['runs'].append(row)
    start = time.monotonic()
    try:
        with stdout.open('xb') as out, stderr.open('xb') as err:
            result = subprocess.run(command, cwd=cwd, env=env, stdout=out, stderr=err, timeout=timeout)
        row['exitCode'] = result.returncode
    finally:
        row['seconds'] = round(time.monotonic() - start, 3)
    require(row['seconds'] < timeout and result.returncode == 0
            and 'ERROR:' not in stdout.read_text(errors='replace') + stderr.read_text(errors='replace'), 'Native failure at ' + label)


def verify(observed, mode, rendered):
    require(observed['Kind'] == 'native-retreat-v2' and observed['Mode'] == mode and observed['Status'] == 'passed',
            mode + ' probe did not pass: ' + str(observed.get('Failure')))
    require(observed['OpeningSeconds'] < LIMITS['openingSeconds'], mode + ' opening exceeded its bound.')
    require(observed['ProbeMilliseconds'] < LIMITS['probeSeconds'] * 1000, mode + ' probe exceeded its bound.')
    require(observed['Paused'], mode + ' did not end paused.')
    if rendered:
        images = {Path(path).name for path in observed['Images']}
        stages = ('reopen',) if mode == 'reopen' else IMAGES + (TRADE_IMAGES if mode == 'keyboard' else ())
        expected = {mode + '.' + stage + '.png' for stage in stages}
        require(expected <= images and all(Path(path).is_file() for path in observed['Images']),
                mode + ' rendered evidence incomplete: ' + ', '.join(sorted(expected - images)))


def device_inputs(observed, mode):
    """The Retreat selection and commit were made on the named device route."""
    rows = [row for row in observed['Inputs'] if row.get('Stage') in ('inspect', 'cancel', 'commit')]
    if mode == 'keyboard':
        keys = [row.get('Key') for row in rows if 'Key' in row]
        require('V' in keys and 'Up' in keys and 'Enter' in keys, 'Keyboard Retreat selection inputs are missing.')
    elif mode == 'controller':
        buttons = [row.get('Controller') for row in rows if 'Controller' in row]
        require({'RightStick', 'DpadUp', 'B', 'A'} <= set(buttons), 'Controller Retreat selection inputs are missing.')
        require(not [row for row in rows if 'Key' in row], 'The controller route used keyboard input for Retreat.')
    else:
        require(any('PointerSector' in row and row['PointerSector'] == 0 for row in rows), 'Pointer sector selection is missing.')
        require({row.get('PointerControl') for row in rows if 'PointerControl' in row} >= {'CloseWheel', 'ConfirmChoice'},
                'Pointer cancel/confirm clicks are missing.')
    require(observed['Inspected'] is not None and 'To cell' in observed['Inspected']['Name'] + observed['Inspected']['Details']
            and 's walk' in observed['Inspected']['Details'], mode + ' inspection did not explain the destination and walk.')
    # Exactly two openings, inspect then commit, each capturing the current executable Retreat: no close-and-retry.
    openings = observed['Openings']
    require([row['Stage'] for row in openings] == ['inspect', 'commit'] and all(row['CanExecute'] for row in openings),
            mode + ' wheel openings were retried or stale: ' + json.dumps(openings))
    # The opponent may queue its own choice meanwhile; the player's own boundary is checked right after cancelling.
    require(any(check.startswith('Cancelling the inspected Retreat sends nothing') for check in observed['Checks']),
            mode + ' did not confirm that cancelling sent nothing.')
    positions = observed['Positions']
    require(len(positions) >= 2 and all(abs(b['X'] - a['X']) + abs(b['Y'] - a['Y']) == 32 for a, b in zip(positions, positions[1:])),
            mode + ' presented retreat motion was not ordinary samples.')
    record = observed['Record']
    require(record is not None and record['Status'] == 'Disengaged' and record['SamplesTaken'] == len(positions) - 1,
            mode + ' did not disengage on its committed route.')
    require('broke away' in (observed['Notice'] or ''), mode + ' did not announce the disengagement.')
    if mode == 'keyboard':
        trade = observed['Trade']
        require(trade is not None and trade['RationsBefore'] == 1 and trade['RationsAfter'] == 2
                and trade['BalanceAfter'] == trade['BalanceBefore'] - trade['Total'] and observed['CarriedRations'] == 2,
                'The keyboard route did not buy one ration after retreating.')
        buying = [row for row in observed['Inputs'] if row.get('Stage') == 'buy']
        require(buying and not [row for row in buying if 'Controller' in row], 'Keyboard purchase inputs are missing.')
    else:
        require(observed['Trade'] is None, mode + ' must not claim a trade.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('dotnet', 'godot', 'fixtures', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--rendered', action='store_true')
    args = parser.parse_args()
    started = time.monotonic()
    runtime = Path(__file__).resolve().parents[1]
    client = runtime / 'client'
    output = args.output.resolve()
    require(not output.exists(), 'Never replace an earlier result.')
    output.parent.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix='native-retreat-', dir=output.parent))
    source = args.fixtures.resolve()
    project = (client / 'project.godot').read_bytes()
    sdk = args.dotnet.resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get('PATH', ''))
    report = dict(status='running', workDirectory=str(work), rendered=args.rendered, entryScene=ENTRY_SCENE, runs=[], results=[],
                  startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(), limits=LIMITS,
                  limitations=['Injected keyboard, controller-button and pointer events through the ordinary input map; no '
                               'physical device or OS focus. Target selection and engagement on the pointer route use keys.',
                               'The fixture is earned and bought by ordinary managed cutoffs and starts beside the wolf. Only '
                               'the keyboard route travels to Marla and buys natively after the retreat; controller and '
                               'pointer routes walk back over the retreated ground.',
                               'One host, one player, two-sided farm wolf encounter; no party retreat, pursuit, bandit-camp threat, '
                               'multiplayer, production art or human acceptance claim.'])
    try:
        fixture_paths = [source / name for name in ('save.sqlite', 'genesis.ofc', 'before.ofc', 'metadata.json')]
        pinned = {str(path): digest(path) for path in fixture_paths}
        require(not any(Path(str(source / 'save.sqlite') + suffix).exists() for suffix in ('-wal', '-shm')),
                'Fixture database must be closed.')
        binaries = list((client / '.godot/mono/temp/bin/Debug').glob('OpenFantasy*.dll'))
        require(len(binaries) >= 3, 'Missing built client assemblies.')
        paths = [Path(__file__).resolve(), client / 'project.godot', args.dotnet.resolve(), args.godot.resolve()] + binaries
        paths += [path for directory in ('scripts', 'scenes', 'assets/ui') for path in (client / directory).rglob('*')
                  if path.is_file() and path.suffix in ('.cs', '.tscn', '.svg')]
        report['executionPins'] = {str(path): digest(path) for path in paths}
        launch = client
        if args.rendered:
            # A disposable project copy carries the no-focus window setting before any window exists; production stays unchanged.
            launch = work / 'background-client'
            launch.mkdir()
            for path in client.iterdir():
                if path.name != 'project.godot':
                    (launch / path.name).symlink_to(path, target_is_directory=path.is_dir())
            config = project.decode()
            require(config.count('[display]') == 1 and 'window/size/no_focus' not in config, 'No-focus configuration changed.')
            (launch / 'project.godot').write_text(config.replace('[display]', '[display]\nwindow/size/no_focus=true', 1))

        def launch_probe(save, prefix, mode, timeout, label):
            command = [str(args.godot.resolve())] + ([] if args.rendered else ['--headless'])
            command += ['--path', str(launch), ENTRY_SCENE, '--', '--cargo-retreat-probe', str(save), str(prefix),
                        str(source / 'genesis.ofc'), str(source / 'metadata.json'), mode]
            if args.rendered:
                command.append('--background-render')
            run(command, runtime, env, work, label, timeout, report)
            return json.loads(prefix.read_text())

        observations = {}
        for mode in ROUTES:
            require(time.monotonic() - started < LIMITS['aggregateSeconds'], 'Aggregate native bound exceeded before ' + mode)
            save, prefix = work / (mode + '.sqlite'), work / (mode + '.json')
            shutil.copyfile(source / 'save.sqlite', save)
            observed = launch_probe(save, prefix, mode, LIMITS['processSeconds'], mode + '-active')
            verify(observed, mode, args.rendered)
            device_inputs(observed, mode)
            observations[mode] = observed
            report['results'].append(dict(mode=mode, save=str(save), genesis=str(source / 'genesis.ofc'),
                                          before=str(source / 'before.ofc'), metadata=str(source / 'metadata.json'),
                                          report=str(prefix), finalDatabaseSha256=digest(save), images=sorted(observed['Images'])))
        keyboard = work / 'keyboard.sqlite'
        frozen = digest(keyboard)
        reopened = launch_probe(keyboard, work / 'reopen.json', 'reopen', LIMITS['reopenProcessSeconds'], 'keyboard-reopen')
        verify(reopened, 'reopen', args.rendered)
        require(digest(keyboard) == frozen, 'The paused reopen changed the saved world.')
        require(reopened['FinalFeet'] == observations['keyboard']['FinalFeet']
                and reopened['Record'] == observations['keyboard']['Record']
                and reopened['FinalBalance'] == observations['keyboard']['FinalBalance']
                and reopened['CarriedRations'] == observations['keyboard']['CarriedRations'] == 2,
                'The reopened world differs from the saved journey.')
        report['reopen'] = dict(save=str(keyboard), report=str(work / 'reopen.json'), images=sorted(reopened['Images']))
        require(pinned == {str(path): digest(path) for path in fixture_paths}, 'Exported fixture mutated.')
        require(report['executionPins'] == {str(path): digest(path) for path in paths}, 'Execution sources or binaries changed.')
        report['aggregateSeconds'] = round(time.monotonic() - started, 3)
        require(report['aggregateSeconds'] < LIMITS['aggregateSeconds'], 'Aggregate native bound exceeded.')
        manifest = work / 'native-manifest.json'
        manifest.write_text(json.dumps(dict(results=report['results'], reopen=report['reopen']), indent=2) + '\n')
        report.update(status='passed', managedAuthenticationManifest=str(manifest))
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
