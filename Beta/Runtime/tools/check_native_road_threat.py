"""Runs the native road-threat journey over exported fixtures: three device routes and one separate paused reopen.

The keyboard route starts from the fresh farm fixture (farm/, commit zero): it earns Wren's reward through the dialogue
keys, walks to Marla, buys one ration through the stall panel and walks the outbound lane to the road. The controller and
pointer routes start from the declared near-road fixture, paused on the lane seven cells south of the bandit camp with
one earned-and-bought ration. Every route copies its closed fixture save, launches the explicit NativeRoadThreat entry
with the disposable road-threat probe and checks its report: no warning while the chief is unobserved, the warning once
it is observed three cells away before contact, the chief's own entry at actual reach, one current first opening at the
ready boundary (never closed and retried) with the same timing at commit, device-specific Retreat selection, a cancelled
inspection, the committed route presented as ordinary 32-subunit steps, the disengagement notice and no automatic
reentry. The keyboard route also strikes once and takes the chief's real hit, walks a detour around the camp, travels to
Kegga and sells the carried lot through the stall panel; its save is reopened in a separate paused process. The manifest
names every route for NativeRoadThreatResultTests to authenticate against the complete saved journals.
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

ENTRY_SCENE = 'res://scenes/NativeRoadThreat.tscn'
ROUTES = ('keyboard', 'controller', 'pointer')
LIMITS = dict(openingSeconds=30, keyboardActiveSeconds=720, keyboardProcessSeconds=750, deviceActiveSeconds=300,
              deviceProcessSeconds=330, reopenProcessSeconds=90, aggregateSeconds=900)
IMAGES = ('warning', 'engaged', 'retreat-inspected', 'retreating', 'disengaged', 'final')
KEYBOARD_IMAGES = ('farm-start', 'wren-reward', 'brasshollow', 'buy-review', 'marla-bought', 'road', 'hit', 'detoured', 'gearford',
                   'sell-review', 'sold')
FIXTURE_FILES = ('save.sqlite', 'genesis.ofc', 'before.ofc', 'metadata.json')


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
    require(observed['Kind'] == 'native-road-threat-v1' and observed['Mode'] == mode and observed['Status'] == 'passed',
            mode + ' probe did not pass: ' + str(observed.get('Failure')))
    require(observed['OpeningSeconds'] < LIMITS['openingSeconds'], mode + ' opening exceeded its bound.')
    active = LIMITS['keyboardActiveSeconds'] if mode == 'keyboard' else LIMITS['deviceActiveSeconds']
    require(observed['ProbeMilliseconds'] < active * 1000, mode + ' probe exceeded its bound.')
    require(observed['Paused'], mode + ' did not end paused.')
    if rendered:
        images = {Path(path).name for path in observed['Images']}
        stages = ('reopen',) if mode == 'reopen' else IMAGES + (KEYBOARD_IMAGES if mode == 'keyboard' else ())
        expected = {mode + '.' + stage + '.png' for stage in stages}
        require(expected <= images and all(Path(path).is_file() for path in observed['Images']),
                mode + ' rendered evidence incomplete: ' + ', '.join(sorted(expected - images)))


def journey(observed, mode):
    """The warning, the chief's own entry, the device-specific Retreat and the aftermath."""
    unobserved = observed['Unobserved']
    require(unobserved is not None and unobserved['Rows'] == 0 and 'Danger' not in unobserved['Status'],
            mode + ' disclosed the chief before it was observed.')
    require('Danger nearby' in (observed['Warning'] or '') and 'Bandit Chief' in observed['Warning']
            and 'level 7' in (observed['WarningDetail'] or '') and 'attacks you' in observed['WarningDetail'],
            mode + ' did not warn before contact.')
    sighted = observed['WarningObservation']
    require(sighted is not None and sighted['ReachSubunits'] < sighted['DistanceSubunits'] <= 768 and sighted['ChiefDrawn']
            and sighted['ChiefInField'] and sighted['ChiefSpriteVisible'] and sighted['PlayerSpriteVisible'],
            mode + ' warning did not show both complete sprites clear of the HUD before contact.')
    field = sighted['Field']
    for label in ('ChiefSprite', 'PlayerSprite'):
        sprite = sighted[label]
        require(sprite['Width'] > 0 and sprite['Height'] > 0
                and field['X'] <= sprite['X'] and field['Y'] <= sprite['Y']
                and sprite['X'] + sprite['Width'] <= field['X'] + field['Width']
                and sprite['Y'] + sprite['Height'] <= field['Y'] + field['Height'],
                mode + ' pre-contact ' + label + ' falls outside the visible field.')
    approach = observed['Approach']
    require(len(approach) >= 2 and all(a['X'] == b['X'] and a['Y'] - b['Y'] > 0 and (a['Y'] - b['Y']) % 32 == 0
                                       for a, b in zip(approach, approach[1:])), mode + ' approach was not ordinary northward samples.')
    engagement = observed['Engagement']
    require(engagement is not None and engagement['EncounterCount'] == 1 and engagement['Distance'] <= engagement['Reach'],
            mode + ' was not engaged by the chief within its reach.')
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
    openings = observed['Openings']
    require([row['Stage'] for row in openings] == ['inspect', 'commit'] and all(row['CanExecute'] for row in openings)
            and openings[0]['Timing'] == openings[1]['Timing'],
            mode + ' wheel openings were retried, stale or at different boundaries: ' + json.dumps(openings))
    require(any(check.startswith('Cancelling the inspected Retreat sends nothing') for check in observed['Checks']),
            mode + ' did not confirm that cancelling sent nothing.')
    positions = observed['Positions']
    require(len(positions) >= 2 and all(abs(b['X'] - a['X']) + abs(b['Y'] - a['Y']) == 32 for a, b in zip(positions, positions[1:])),
            mode + ' presented retreat motion was not ordinary samples.')
    record = observed['Record']
    require(record is not None and record['Status'] == 'Disengaged' and record['SamplesTaken'] == len(positions) - 1,
            mode + ' did not disengage on its committed route.')
    require('broke away' in (observed['Notice'] or ''), mode + ' did not announce the disengagement.')
    require(observed['HoldOff'] is not None and observed['HoldOff']['EncounterCount'] == 1 and observed['EncounterCount'] == 1,
            mode + ' re-entered automatically after the retreat.')
    if mode == 'keyboard':
        hit = observed['Hit']
        require(hit is not None and hit['After'] < hit['Before'] and observed['FinalHp'] == hit['After'],
                'The keyboard route did not take and retain the chief\'s real hit.')
        trade = observed['Trade']
        require(trade is not None and trade['RationsBefore'] == 1 and trade['RationsAfter'] == 0
                and trade['BalanceAfter'] == trade['BalanceBefore'] + trade['Total'] and observed['CarriedRations'] == 0,
                'The keyboard route did not sell the carried ration at Kegga.')
        selling = [row for row in observed['Inputs'] if row.get('Stage') == 'sell']
        require(selling and not [row for row in selling if 'Controller' in row], 'Keyboard sale inputs are missing.')
        # The native join: earned at the farm, bought at Marla's stall, then carried along the lane by held keys.
        purchase = observed['Purchase']
        require(observed['Start'] == 'farm' and (observed['Reward'] or 0) > 0 and purchase is not None and purchase['Units'] == 1
                and purchase['BalanceBefore'] == observed['Reward']
                and purchase['BalanceAfter'] == purchase['BalanceBefore'] - purchase['Total']
                and trade['BalanceBefore'] == purchase['BalanceAfter'] and trade['LotId'] == purchase['LotId'],
                'The keyboard route did not earn, buy and carry its own ration from the farm.')
        talk = [row.get('Key') for row in observed['Inputs'] if row.get('Stage') == 'wren' and 'Key' in row]
        require(talk == ['E', 'G', 'G', 'E'], 'Wren was not answered through the dialogue keys: ' + json.dumps(talk))
        buying = [row for row in observed['Inputs'] if row.get('Stage') == 'buy']
        require(buying and not [row for row in buying if 'Controller' in row], 'Keyboard purchase inputs are missing.')
        # Wren may already stand beside the farm start; the walks to Marla, out along the lane and up to the chief are required.
        walked = {row.get('Stage') for row in observed['WalkInputs'] if 'Hold' in row}
        require({'walk-to-marla', 'outbound', 'warning', 'approach'} <= walked,
                'The keyboard route did not walk its own way from the farm to the chief: ' + ', '.join(sorted(walked)))
    else:
        require(observed['Trade'] is None and observed['Hit'] is None and observed['CarriedRations'] == 1
                and observed['Start'] == 'road' and observed['Purchase'] is None and observed['Reward'] is None,
                mode + ' must not claim a strike, a purchase or a trade.')


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
    work = Path(tempfile.mkdtemp(prefix='native-road-threat-', dir=output.parent))
    source = args.fixtures.resolve()
    project = (client / 'project.godot').read_bytes()
    sdk = args.dotnet.resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get('PATH', ''))
    report = dict(status='running', workDirectory=str(work), rendered=args.rendered, entryScene=ENTRY_SCENE, runs=[], results=[],
                  startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(), limits=LIMITS,
                  limitations=['Injected keyboard, controller-button and pointer events through the ordinary input map; no '
                               'physical device or OS focus. Walking uses held keys on every route; the device route owns the '
                               'Retreat selection, cancellation and confirmation.',
                               'The keyboard route begins at the fresh farm fixture and owns the reward, purchase and outbound '
                               'walk. The controller and pointer fixtures are earned, bought and walked to road cell (346,318) by '
                               'ordinary managed cutoffs; those routes begin there. Only the keyboard route strikes, detours and '
                               'sells at Kegga.',
                               'One host, one player, one camp chief; no pursuit, leash, reward, respawn, escort wage, party '
                               'retreat, multiplayer, production art or human acceptance claim.'])
    try:
        farm = source / 'farm'
        fixture_paths = [directory / name for directory in (source, farm) for name in FIXTURE_FILES]
        pinned = {str(path): digest(path) for path in fixture_paths}
        require(not any(Path(str(directory / 'save.sqlite') + suffix).exists() for directory in (source, farm) for suffix in ('-wal', '-shm')),
                'Fixture databases must be closed.')
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

        def fixture(mode):
            """The keyboard journey and its reopening use the fresh farm fixture; the device routes the near-road one."""
            return farm if mode in ('keyboard', 'reopen') else source

        def launch_probe(save, prefix, mode, timeout, label):
            command = [str(args.godot.resolve())] + ([] if args.rendered else ['--headless'])
            command += ['--path', str(launch), ENTRY_SCENE, '--', '--cargo-threat-probe', str(save), str(prefix),
                        str(fixture(mode) / 'genesis.ofc'), str(fixture(mode) / 'metadata.json'), mode]
            if args.rendered:
                command.append('--background-render')
            run(command, runtime, env, work, label, timeout, report)
            return json.loads(prefix.read_text())

        observations = {}
        for mode in ROUTES:
            require(time.monotonic() - started < LIMITS['aggregateSeconds'], 'Aggregate native bound exceeded before ' + mode)
            save, prefix = work / (mode + '.sqlite'), work / (mode + '.json')
            shutil.copyfile(fixture(mode) / 'save.sqlite', save)
            timeout = LIMITS['keyboardProcessSeconds'] if mode == 'keyboard' else LIMITS['deviceProcessSeconds']
            remaining = LIMITS['aggregateSeconds'] - (time.monotonic() - started)
            observed = launch_probe(save, prefix, mode, min(timeout, remaining), mode + '-active')
            verify(observed, mode, args.rendered)
            journey(observed, mode)
            observations[mode] = observed
            report['results'].append(dict(mode=mode, save=str(save), genesis=str(fixture(mode) / 'genesis.ofc'),
                                          before=str(fixture(mode) / 'before.ofc'), metadata=str(fixture(mode) / 'metadata.json'),
                                          report=str(prefix), finalDatabaseSha256=digest(save), images=sorted(observed['Images'])))
        keyboard = work / 'keyboard.sqlite'
        frozen = digest(keyboard)
        remaining = LIMITS['aggregateSeconds'] - (time.monotonic() - started)
        reopened = launch_probe(keyboard, work / 'reopen.json', 'reopen', min(LIMITS['reopenProcessSeconds'], remaining),
                                'keyboard-reopen')
        verify(reopened, 'reopen', args.rendered)
        require(digest(keyboard) == frozen, 'The paused reopen changed the saved world.')
        saved = observations['keyboard']
        require(all(reopened[key] == saved[key] for key in ('FinalFeet', 'Record', 'FinalBalance', 'FinalHp', 'EncounterCount'))
                and reopened['CarriedRations'] == saved['CarriedRations'] == 0, 'The reopened world differs from the saved journey.')
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
