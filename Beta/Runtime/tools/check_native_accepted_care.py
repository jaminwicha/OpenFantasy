"""Full-health native approach, accepted care, second defeat and separate paused reopen.

This rendered/input gate exports a manifest; the separate managed result test authenticates
its complete journals. Neither gate alone qualifies accepted care.
"""
import argparse
import json
import os
import shutil
import tempfile
import time
import traceback
from pathlib import Path
from check_native_encounter_outcome import digest, require, run, verify_hud, inside, overlaps

LIMITS = dict(openingSeconds=30, activeSeconds=300, processSeconds=330, reopenProcessSeconds=90, aggregateSeconds=900)
ENTRY = 'res://scenes/NativeEncounterOutcome.tscn'


def verify(observed, mode, rendered):
    require((observed['Kind'], observed['Mode'], observed['Status']) == ('native-encounter-outcome-v1', mode, 'passed'),
            'Probe failed: ' + str(observed.get('Failure')))
    require(observed['OpeningSeconds'] < LIMITS['openingSeconds'] and observed['ProbeMilliseconds'] < LIMITS['activeSeconds'] * 1000,
            'Opening or active bound exceeded.')
    require(observed['Paused'] and not observed['SubmissionOverflow'] and observed['FinalHp'] == 0
            and observed['FinalTerminal'] and observed['FinalStage'] == 'Completed' and observed['EncounterCount'] == 1,
            'The same episode did not reach the saved terminal outcome.')
    if mode == 'care-accepted':
        require(observed['WalkInputs'] and observed['Waits'] > 1 and observed['AcceptedCare']['HealedHp'] > 0,
                'Missing ordinary approach, combat or observed healing.')
        intents = [row['IntentType'] for row in observed['Submissions']]
        require(intents[0] == 'EnterEncounterIntent' and intents[-1] == 'AcknowledgeEncounterOutcomeIntent'
                and intents.count('AcknowledgeCareIntent') == 1
                and all(intent in ('EnterEncounterIntent', 'QueueEncounterWaitIntent', 'AcknowledgeCareIntent',
                                   'AcknowledgeEncounterOutcomeIntent') for intent in intents), 'Unexpected command route.')
        require(len(observed['Selections']) == len(observed['Submissions']), 'Missing pre-input selection.')
    else:
        require(not observed['Submissions'] and not observed['Selections'], 'Paused reopen submitted a command.')
    if rendered:
        labels = ('healthy', 'entered', 'care-offered', 'treated', 'second-outcome', 'terminal', 'final') if mode == 'care-accepted' else ('reopen',)
        images = {Path(path).name for path in observed['Images']}
        require({mode + '.' + label + '.png' for label in labels} <= images, 'Missing rendered stages.')
        require(all(Path(path).is_file() and Path(path).stat().st_size > 0 for path in observed['Images']), 'Missing image bytes.')
        expected_hud = {'care-offered', 'treated', 'second-outcome', 'terminal'} if mode == 'care-accepted' else {'reopen'}
        require(expected_hud <= {row['Label'] for row in observed['HudBounds']}, 'Missing HUD bounds.')
        for hud in observed['HudBounds']:
            verify_hud(hud, mode)
        for panel in observed['Panels']:
            require(inside(panel['Panel'], panel['Viewport']) and inside(panel['Panel'], panel['Field'])
                    and panel['PlayerSprite'] is not None and panel['OpponentSprite'] is not None
                    and not overlaps(panel['Panel'], panel['PlayerSprite']) and not overlaps(panel['Panel'], panel['OpponentSprite'])
                    and (panel['TargetBar'] is None or not overlaps(panel['Panel'], panel['TargetBar']))
                    and inside(panel['Body'], panel['Panel']) and panel['VisibleLines'] >= panel['Lines'] > 0,
                    'An outcome panel clips text or obscures required actors.')


def main():
    parser = argparse.ArgumentParser()
    for arg in ('dotnet', 'godot', 'fixtures', 'output'):
        parser.add_argument('--' + arg, type=Path, required=True)
    parser.add_argument('--rendered', action='store_true')
    args = parser.parse_args()
    started = time.monotonic()
    runtime = Path(__file__).resolve().parents[1]
    client = runtime / 'client'
    source, output = args.fixtures.resolve(), args.output.resolve()
    require(not output.exists(), 'Do not overwrite evidence.')
    output.parent.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix='native-accepted-care-', dir=output.parent))
    report = dict(status='running', workDirectory=str(work), limits=LIMITS, runs=[], results=[])
    project = (client / 'project.godot').read_bytes()
    sdk = args.dotnet.resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get('PATH', ''))
    try:
        fixture_files = [source / name for name in ('save.sqlite', 'genesis.ofc', 'before.ofc', 'metadata.json', 'manifest.json')]
        pins = {str(path): digest(path) for path in fixture_files}
        require(not any(Path(str(source / 'save.sqlite') + suffix).exists() for suffix in ('-wal', '-shm')), 'Fixture is open.')
        paths = [Path(__file__).resolve(), Path(__file__).with_name('check_native_encounter_outcome.py'), client / 'project.godot']
        paths += list((client / '.godot/mono/temp/bin/Debug').glob('OpenFantasy*.dll'))
        paths += [f for directory in ('scripts', 'scenes', 'assets') for f in (client / directory).rglob('*')
                  if f.is_file() and f.suffix in ('.cs', '.tscn', '.svg', '.png')]
        report['executionPins'] = {str(path): digest(path) for path in paths}
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
        save = work / 'care.sqlite'
        shutil.copyfile(source / 'save.sqlite', save)
        observations = {}
        frozen = None
        for mode, bound in (('care-accepted', LIMITS['processSeconds']), ('care-reopen', LIMITS['reopenProcessSeconds'])):
            prefix = work / (mode + '.json')
            command = [str(args.godot.resolve())] + ([] if args.rendered else ['--headless'])
            command += ['--path', str(launch), ENTRY, '--', '--cargo-outcomes-probe', str(save), str(prefix),
                        str(source / 'genesis.ofc'), str(source / 'metadata.json'), mode]
            if args.rendered:
                command.append('--background-render')
            remaining = LIMITS['aggregateSeconds'] - (time.monotonic() - started)
            require(remaining > 0, 'Aggregate deadline expired.')
            run(command, runtime, env, work, mode, min(bound, remaining), report)
            observed = json.loads(prefix.read_text())
            verify(observed, mode, args.rendered)
            require(not any(Path(str(save) + suffix).exists() for suffix in ('-wal', '-shm')), 'Save remains open.')
            observations[mode] = observed
            if frozen is None:
                frozen = digest(save)
                report['results'].append(dict(mode=mode, save=str(save), genesis=str(source / 'genesis.ofc'),
                    before=str(source / 'before.ofc'), metadata=str(source / 'metadata.json'), report=str(prefix),
                    finalDatabaseSha256=frozen))
            else:
                require(digest(save) == frozen, 'Paused reopening changed the saved journal.')
                report['reopen'] = dict(save=str(save), report=str(prefix))
        for key in ('FinalFeet', 'FinalHp', 'EncounterCount', 'CompletedAtSequence', 'FinalResult', 'FinalStandingSide', 'FinalTerminal'):
            require(observations['care-accepted'][key] == observations['care-reopen'][key], 'Reopen changed ' + key)
        require(pins == {str(path): digest(path) for path in fixture_files}, 'Source fixture changed.')
        require(report['executionPins'] == {str(path): digest(path) for path in paths}, 'Execution inputs changed.')
        report['aggregateSeconds'] = time.monotonic() - started
        require(report['aggregateSeconds'] < LIMITS['aggregateSeconds'], 'Aggregate deadline exceeded.')
        manifest = work / 'native-manifest.json'
        manifest.write_text(json.dumps(dict(results=report['results'], reopen=report['reopen']), indent=2) + '\n')
        report.update(status='passed', managedAuthenticationManifest=str(manifest))
    except Exception as error:
        report.update(status='failed', failure=str(error), traceback=traceback.format_exc())
    finally:
        report['originalProjectUnchanged'] = project == (client / 'project.godot').read_bytes()
        if not report['originalProjectUnchanged']:
            report['status'] = 'failed'
        output.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({key: report.get(key) for key in ('status', 'failure', 'workDirectory', 'managedAuthenticationManifest')}))
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
