"""Rendered exact-offer cancellation/decline and separate paused reopens; journals authenticate separately."""
import argparse
import json
import os
import shutil
import tempfile
import time
import traceback
from pathlib import Path
from check_native_encounter_outcome import digest, require, run

MODES = ('cancel-keyboard', 'cancel-pointer', 'decline-controller')
LIMITS = dict(openingSeconds=30, activeSeconds=150, processSeconds=180,
              reopenProcessSeconds=90, aggregateSeconds=900)
ENTRY = 'res://scenes/NativeCareDisposition.tscn'


def verify(value, mode):
    require((value['Kind'], value['Mode'], value['Status']) == ('native-care-disposition-v1', mode, 'passed'),
            'Native probe failed: ' + str(value.get('Failure')))
    require(value['OpeningSeconds'] < LIMITS['openingSeconds']
            and value['ProbeMilliseconds'] < LIMITS['activeSeconds'] * 1000, 'Native deadline exceeded.')
    require(value['Paused'] and value['FocusRequests'] == 0 and value['StartupNoFocus'] and value['Unfocusable']
            and not value['ObservedNativeFocus'] and not value['NativeFocused'] and value['Display'] != 'headless',
            'Background focus or rendered terminal state is unqualified.')
    require(value['FinalCareStatus'] == ('Declined' if 'decline' in mode else 'Cancelled'), 'Wrong closure shown.')
    require(value['FinalPatientHp'] == 0 and value['FinalEncounterPhase'] == 'Outcome'
            and not value['ObservedHealthCue'] and not value['SubmissionOverflow'], 'Closure changed bodily/outcome state.')
    submissions = value['Submissions']
    if mode.startswith('reopen-'):
        require(not submissions and value['InitialViewHash'] == value['FinalViewHash'], 'Reopen changed state or submitted work.')
    else:
        require(len(submissions) == 1 and not submissions[0]['IsRetry'], 'Expected one exact native decision.')
        require(submissions[0]['IntentType'] == ('DeclinePendingCareIntent' if 'decline' in mode else 'CancelPendingCareIntent'),
                'Wrong native decision submitted.')
        require(value['SelectedCommandBytes'] == submissions[0]['CommandBytes'] and submissions[0]['ReceiptBytes'],
                'Missing exact selected command/receipt.')
        require(value['Inputs'], 'Missing native input evidence.')
    require(value['Images'] and all(Path(path).is_file() and Path(path).stat().st_size > 0 for path in value['Images']),
            'Missing rendered images.')
    hud_labels = {'reopen'} if mode.startswith('reopen-') else {'pending', 'closed'}
    require(hud_labels <= {row['Label'] for row in value['HudBounds']}, 'Missing HUD stages.')
    if not mode.startswith('reopen-'):
        require({'paused-inspection', 'selected'} <= {row['Label'] for row in value['WheelBounds']}, 'Missing inspected wheel stages.')
    for row in value['HudBounds'] + value['WheelBounds']:
        require(not row['Failures'] and row['Controls']
                and all(control['Inside'] and control['Complete'] for control in row['Controls']), 'Clipped native controls.')


def main():
    parser = argparse.ArgumentParser()
    for name in ('dotnet', 'godot', 'fixtures', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    runtime = Path(__file__).resolve().parents[1]
    client = runtime / 'client'
    source, output = args.fixtures.resolve(), args.output.resolve()
    require(not output.exists(), 'Never overwrite native evidence.')
    output.parent.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix='native-care-dispositions-', dir=output.parent))
    report = dict(status='running', limits=LIMITS, workDirectory=str(work), runs=[], results=[])
    start = time.monotonic()
    project = (client / 'project.godot').read_bytes()
    try:
        files = [source / name for name in ('save.sqlite', 'genesis.ofc', 'before.ofc', 'metadata.json')]
        fixture_pins = {str(path): digest(path) for path in files}
        require(not any(Path(str(source / 'save.sqlite') + suffix).exists() for suffix in ('-wal', '-shm')), 'Fixture remains open.')
        paths = [Path(__file__).resolve(), Path(__file__).with_name('check_native_encounter_outcome.py'), client / 'project.godot']
        paths += list((client / '.godot/mono/temp/bin/Debug').glob('OpenFantasy*.dll'))
        paths += [path for directory in ('scripts', 'scenes', 'assets') for path in (client / directory).rglob('*')
                  if path.is_file() and path.suffix in ('.cs', '.tscn', '.png', '.svg')]
        report['executionPins'] = {str(path): digest(path) for path in paths}
        report['fixturePins'] = fixture_pins
        launch = work / 'background-client'
        launch.mkdir()
        for path in client.iterdir():
            if path.name != 'project.godot':
                (launch / path.name).symlink_to(path, target_is_directory=path.is_dir())
        config = project.decode()
        require(config.count('[display]') == 1 and 'window/size/no_focus' not in config, 'Unexpected display configuration.')
        (launch / 'project.godot').write_text(config.replace('[display]', '[display]\nwindow/size/no_focus=true', 1))
        sdk = args.dotnet.resolve().parent
        env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get('PATH', ''))
        for route in MODES:
            save = work / (route + '.sqlite')
            shutil.copyfile(source / 'save.sqlite', save)
            prior = None
            for mode, bound in ((route, LIMITS['processSeconds']), ('reopen-' + route, LIMITS['reopenProcessSeconds'])):
                path = work / (mode + '.json')
                command = [str(args.godot.resolve()), '--path', str(launch), ENTRY, '--', '--care-disposition-probe',
                           str(save), str(path), str(source / 'genesis.ofc'), str(source / 'metadata.json'), mode, '--background-render']
                remaining = LIMITS['aggregateSeconds'] - (time.monotonic() - start)
                require(remaining > 0, 'Aggregate deadline expired.')
                run(command, runtime, env, work, mode, min(bound, remaining), report)
                observed = json.loads(path.read_text())
                verify(observed, mode)
                require(not any(Path(str(save) + suffix).exists() for suffix in ('-wal', '-shm')), 'Native save remains open.')
                if prior is None:
                    prior = observed
                    row = dict(mode=route, save=str(save), sourceSave=str(source / 'save.sqlite'), genesis=str(source / 'genesis.ofc'),
                               before=str(source / 'before.ofc'), metadata=str(source / 'metadata.json'), report=str(path),
                               finalDatabaseSha256=digest(save))
                    report['results'].append(row)
                else:
                    require(row['finalDatabaseSha256'] == digest(save), 'Paused reopen changed saved bytes.')
                    require(prior['FinalViewHash'] == observed['InitialViewHash'] == observed['FinalViewHash'], 'Reopen changed public state.')
                    row['reopenReport'] = str(path)
        require(fixture_pins == {str(path): digest(path) for path in files}, 'Source fixture changed.')
        require(report['executionPins'] == {str(path): digest(path) for path in paths}, 'Execution inputs changed.')
        require(time.monotonic() - start < LIMITS['aggregateSeconds'], 'Aggregate deadline exceeded.')
        manifest = work / 'native-manifest.json'
        manifest.write_text(json.dumps(dict(results=report['results']), indent=2) + '\n')
        report.update(status='passed', managedAuthenticationManifest=str(manifest))
    except Exception as error:
        report.update(status='failed', failure=str(error), traceback=traceback.format_exc())
    finally:
        report['seconds'] = time.monotonic() - start
        report['originalProjectUnchanged'] = project == (client / 'project.godot').read_bytes()
        if not report['originalProjectUnchanged']:
            report['status'] = 'failed'
        output.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({key: report.get(key) for key in ('status', 'failure', 'workDirectory', 'managedAuthenticationManifest')}))
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
