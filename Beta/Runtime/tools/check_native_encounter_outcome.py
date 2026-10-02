"""Runs the native encounter-outcome journeys over exported fixtures: four routes and one separate paused reopen.

Each route copies its closed fixture save and launches the explicit NativeEncounterOutcome entry with the disposable
outcome probe. Fixture preparation is managed: the road fixtures earn, buy and walk to the road and stop in exploration
before any encounter; the care fixture walks beside the first wolf and performs the player's own managed entry, stopping
at the Active episode's first decision boundary. Everything after the fixture is native input. victory: the declared
wounded chief enters under its own control, E strikes resolve the episode, the panel is read while the world holds,
Continue (E) lets the world complete it, then held keys detour to Gearford and the carried ration is sold at Kegga.
defeat: the production chief incapacitates the player, the panel states why action and care are unavailable, Accept
defeat (E) completes the episode, the terminal panel's Save and leave closes the world normally, and a separate paused
process reopens the same terminal save. care and care-wheel (both from the one care fixture, so neither exercises a
native care-specific approach or entry): at 1 HP, G waits until the wolf's real bite and Wren's admitted care is shown;
care declines it with the HUD's Accept defeat button (which names no key, since E asks for care) and care-wheel through
the Actions wheel's right sector (V, Right, Enter). Every world command is observed at the client's submission boundary
with its presented offer, canonical command bytes and receipt. Rendered runs capture each observed state first, then
the panel's and the whole HUD's actual post-draw bounds against the viewport, unclipped text and both combatants. The
manifest names every route for NativeEncounterOutcomeResultTests to authenticate against the complete saved journals and
re-derived genesis bytes.
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

ENTRY_SCENE = 'res://scenes/NativeEncounterOutcome.tscn'
ROUTES = ('victory', 'defeat', 'care', 'care-wheel')
FIXTURES = dict(victory='victory', defeat='defeat', care='care', reopen='defeat')
FIXTURES['care-wheel'] = 'care'
LIMITS = dict(openingSeconds=30, keyboardActiveSeconds=720, keyboardProcessSeconds=750, deviceActiveSeconds=300,
              deviceProcessSeconds=330, reopenProcessSeconds=90, aggregateSeconds=900)
IMAGES = dict(victory=('victory-engaged', 'victory-outcome', 'victory-completed', 'gearford', 'sold', 'final'),
              defeat=('defeat-engaged', 'defeat-outcome', 'terminal'), care=('care-outcome', 'care-hover', 'care-declined', 'final'),
              reopen=('reopen',))
IMAGES['care-wheel'] = ('care-wheel-outcome', 'wheel', 'wheel-accept', 'care-wheel-declined', 'final')
# States whose whole HUD must be proven inside the viewport, unclipped and clear of the combatants.
HUD_STATES = dict(victory=('victory-outcome', 'victory-completed'), defeat=('defeat-outcome', 'terminal'),
                  care=('care-outcome', 'care-hover', 'care-declined'), reopen=('reopen',))
HUD_STATES['care-wheel'] = ('care-wheel-outcome', 'care-wheel-declined')
# The world commands each route's native input must submit, in order, by intent type.
READING = 'AcknowledgeEncounterOutcomeIntent'
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


def inside(inner, outer):
    return (outer['X'] <= inner['X'] and outer['Y'] <= inner['Y'] and inner['X'] + inner['Width'] <= outer['X'] + outer['Width']
            and inner['Y'] + inner['Height'] <= outer['Y'] + outer['Height'])


def overlaps(a, b):
    return a['X'] < b['X'] + b['Width'] and b['X'] < a['X'] + a['Width'] and a['Y'] < b['Y'] + b['Height'] and b['Y'] < a['Y'] + a['Height']


def verify(observed, mode, rendered):
    require(observed['Kind'] == 'native-encounter-outcome-v1' and observed['Mode'] == mode and observed['Status'] == 'passed',
            mode + ' probe did not pass: ' + str(observed.get('Failure')))
    require(observed['OpeningSeconds'] < LIMITS['openingSeconds'], mode + ' opening exceeded its bound.')
    active = LIMITS['keyboardActiveSeconds'] if mode == 'victory' else LIMITS['deviceActiveSeconds']
    require(observed['ProbeMilliseconds'] < active * 1000, mode + ' probe exceeded its bound.')
    if mode != 'defeat':
        require(observed['Paused'], mode + ' did not end paused.')
    if rendered:
        images = {Path(path).name for path in observed['Images']}
        expected = {mode + '.' + stage + '.png' for stage in IMAGES[mode]}
        require(expected <= images and all(Path(path).is_file() and Path(path).stat().st_size > 0 for path in observed['Images']),
                mode + ' rendered evidence incomplete: ' + ', '.join(sorted(expected - images)))
        panels = observed['Panels']
        require(panels, mode + ' recorded no panel bounds.')
        for panel in panels:
            # The actual post-draw panel lies in the field between the HUD header and footer, clear of the target bar and
            # of both combatants' rendered sprites, with every body line visible.
            require(inside(panel['Panel'], panel['Viewport']) and inside(panel['Panel'], panel['Field'])
                    and (panel['TargetBar'] is None or not overlaps(panel['Panel'], panel['TargetBar']))
                    and panel['PlayerSprite'] is not None and not overlaps(panel['Panel'], panel['PlayerSprite'])
                    and panel['OpponentSprite'] is not None and not overlaps(panel['Panel'], panel['OpponentSprite'])
                    and inside(panel['Body'], panel['Panel']) and panel['VisibleLines'] >= panel['Lines'] > 0,
                    mode + ' panel ' + panel['Label'] + ' is clipped or occludes the HUD or a combatant.')
        huds = {hud['Label']: hud for hud in observed['HudBounds']}
        require(set(HUD_STATES[mode]) <= set(huds), mode + ' HUD bounds missing: ' + ', '.join(sorted(set(HUD_STATES[mode]) - set(huds))))
        for hud in huds.values():
            verify_hud(hud, mode)


def verify_hud(hud, mode):
    """Re-checks one recorded post-draw HUD: panels and every visible text control inside the viewport and their panel,
    every line shown, and both drawn combatants inside the field between header and footer, clear of the target bar."""
    label = mode + ' HUD ' + hud['Label']
    viewport, field = hud['Viewport'], hud['Field']
    require(not hud['Failures'], label + ': ' + '; '.join(hud['Failures']))
    require(inside(hud['Header'], viewport) and inside(hud['Footer'], viewport), label + ' panel outside the viewport.')
    require(hud['TargetBar'] is None or inside(hud['TargetBar'], viewport), label + ' target bar outside the viewport.')
    require(hud['Controls'], label + ' recorded no controls.')
    for control in hud['Controls']:
        panel = hud[control['Panel']]
        require(inside(control['Rect'], viewport) and inside(control['Rect'], panel) and control['Inside'] and control['Complete'],
                label + ' control ' + control['Name'] + ' is outside its panel or clipped.')
        if control['Lines'] is not None:
            require(control['VisibleLines'] >= control['Lines'], label + ' label ' + control['Name'] + ' hides lines.')
        if control['TextWidth'] is not None:
            require(control['TextWidth'] <= control['Rect']['Width'] + 0.5, label + ' label ' + control['Name'] + ' is wider than its rect.')
    require(hud['PlayerSprite'] is not None, label + ' did not draw the player.')
    for sprite in (hud['PlayerSprite'], hud['OpponentSprite']):
        if sprite is None:
            continue
        require(inside(sprite, field) and not overlaps(sprite, hud['Header']) and not overlaps(sprite, hud['Footer'])
                and (hud['TargetBar'] is None or not overlaps(sprite, hud['TargetBar'])), label + ' covers a combatant.')


def verify_submissions(observed, mode):
    """Every world command came through one checked selection of a presented offer; nothing was retried or rejected."""
    submissions, selections = observed['Submissions'], observed['Selections']
    require(not observed['SubmissionOverflow'] and len(submissions) == len(selections) <= observed['SubmissionLimit'],
            mode + ' observed submissions differ from its checked selections.')
    require([row['CommandId'] for row in submissions] == [row['CommandId'] for row in selections], mode + ' selections out of order.')
    # The pre-input presentation, completed with the generated identity, is byte for byte the submitted command.
    require(all(selection['ExpectedCommandBytes'] == row['CommandBytes'] for selection, row in zip(selections, submissions)),
            mode + ' a submitted command is not its pre-input selection.')
    for row in submissions:
        receipt = row['Receipt']
        require(not row['IsRetry'] and row['Answer'] == 'receipt' and receipt is not None and receipt['Status'] == 'Applied'
                and receipt['CommandId'] == row['CommandId'] and len(row['CommandBytes']) > 0 and len(receipt['Bytes']) > 0,
                mode + ' submission ' + row['CommandId'] + ' was not applied exactly once.')
    types = [row['IntentType'] for row in submissions]
    require('AcknowledgeCareIntent' not in types, mode + ' requested care.')
    if mode == 'reopen':
        require(not submissions, 'The paused reopen submitted a command.')
        return
    require(types.count(READING) == 1, mode + ' did not submit exactly one outcome reading.')
    reading = types.index(READING)
    fight = 'QueueEncounterWaitIntent' if mode in ('care', 'care-wheel') else 'QueueEncounterAbilityIntent'
    require(reading >= 1 and all(value == fight for value in types[:reading]), mode + ' combat choices before the reading: ' + ', '.join(types))
    after = types[reading + 1:]
    require(len(after) == (2 if mode == 'victory' else 0), mode + ' unexpected commands after the reading: ' + ', '.join(after))
    stage = dict(victory='continue', defeat='accept', care='decline')
    stage['care-wheel'] = 'wheel-decline'
    require(submissions[reading]['Stage'] == stage[mode], mode + ' reading came from stage ' + submissions[reading]['Stage'])


def journey(observed, mode):
    keys = [row.get('Key') for row in observed['Inputs'] if 'Key' in row]
    require(not [row for row in observed['Inputs'] if 'Controller' in row], mode + ' used controller input.')
    if mode == 'victory':
        outcome, completed, trade = observed['Outcome'], observed['Completed'], observed['Trade']
        require(observed['Strikes'] >= 1 and 'E' in keys and outcome['Title'] == 'Victory' and outcome['Standing'] == 'YourSideStands',
                'The victory was not resolved by native strikes and read on the panel.')
        require(completed['Stage'] == 'Completed' and completed['EncounterCount'] == 1 and completed['CompletedAtSequence'],
                'Continue did not let the world complete the victory.')
        require(trade is not None and trade['RationsBefore'] == 1 and trade['RationsAfter'] == 0
                and trade['BalanceAfter'] == trade['BalanceBefore'] + trade['Total'] and observed['CarriedRations'] == 0,
                'The carried ration was not sold at Kegga after the victory.')
        walked = {row.get('Stage') for row in observed['WalkInputs'] if 'Hold' in row}
        require({'approach', 'detour', 'to-gearford'} <= walked, 'The victory route did not walk its own way: ' + ', '.join(sorted(walked)))
    elif mode == 'defeat':
        outcome, completed = observed['Outcome'], observed['Completed']
        require(observed['Strikes'] >= 1 and outcome['Title'] == 'Defeated' and outcome['Hp'] == 0
                and 'cannot move or act' in outcome['Status'] and 'reach you' not in outcome['Body'],
                'The defeat did not state why ordinary action and care are unavailable.')
        require(completed['Stage'] == 'Completed' and completed['Hp'] == 0 and observed['FinalTerminal'] is True
                and observed['SaveAndLeave'] is True, 'Accept defeat did not reach the terminal Save and leave state.')
    else:
        outcome, decline = observed['Outcome'], observed['Decline']
        require(observed['Waits'] >= 1 and 'G' in keys and outcome['Title'] == 'Defeated'
                and decline is not None and (decline['Helper'] + ' can help now') in outcome['Body']
                and 'Accepting declines that care' in outcome['Body'] and decline['Terminal'] is True,
                'Available care was not shown and deliberately declined.')
        require(decline['Button'] == 'Accept defeat', 'The HUD acceptance beside care still names a key: ' + decline['Button'])
        stage = 'decline' if mode == 'care' else 'wheel-decline'
        declined = [row for row in observed['Inputs'] if row.get('Stage') == stage]
        require(not [row for row in declined if row.get('Key') == 'E'], 'The decline must not press E, which requests care.')
        if mode == 'care':
            clicked = {row.get('PointerControl') for row in declined}
            require('ReleaseEncounter' in clicked and decline['Route'] == 'hud-pointer',
                    'The decline was not a pointer activation of the HUD Accept defeat button.')
        else:
            wheel_bounds = observed['WheelBounds']
            require([row['Label'] for row in wheel_bounds] == ['wheel', 'wheel-accept'], 'Missing wheel layout observations.')
            for row in wheel_bounds:
                require(not row['Failures'] and inside(row['Panel'], row['Viewport']), 'Wheel panel is clipped.')
                require(row['Controls'], 'Wheel controls were not measured.')
                for control in row['Controls']:
                    require(inside(control['Rect'], row['Panel']) and inside(control['Rect'], row['Viewport']),
                            'Wheel control is clipped: ' + control['Name'])
                    require(control['Lines'] is None or control['VisibleLines'] >= control['Lines'],
                            'Wheel control hides text: ' + control['Name'])
            require([row.get('Key') for row in declined if 'Key' in row] == ['V', 'Right', 'Enter']
                    and decline['Route'] == 'wheel-keyboard' and decline['Sector'] == 'Accept defeat',
                    'The decline was not V, Right, Enter on the wheel\'s Accept defeat sector.')


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
    work = Path(tempfile.mkdtemp(prefix='native-encounter-outcome-', dir=output.parent))
    source = args.fixtures.resolve()
    project = (client / 'project.godot').read_bytes()
    sdk = args.dotnet.resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get('PATH', ''))
    report = dict(status='running', workDirectory=str(work), rendered=args.rendered, entryScene=ENTRY_SCENE, runs=[], results=[],
                  startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(), limits=LIMITS,
                  limitations=['Injected keyboard and pointer events through the ordinary input map; no physical device or OS '
                               'focus. Background rendering is set in a disposable project copy before any window exists.',
                               'Fixture preparation is managed: the road fixtures earn, buy and walk to the road and stop in '
                               'exploration; the care fixture walks beside the first wolf and performs the player\'s own managed '
                               'entry, stopping at the Active first decision. Native input owns the road encounters\' entry by the '
                               'chief, every combat choice, resolution, reading, completion, decline, continuation and exit. The '
                               'care routes do not prove a native care-specific approach or entry or its timing.',
                               'Declared fresh calibrations (wounded chief, 1-HP player) are qualification parameters, not balance. '
                               'One local player; no multiplayer, network, accepted-care native route, no-survivor route, '
                               'production art or human acceptance claim.'])
    try:
        fixture_routes = sorted(set(FIXTURES[route] for route in ROUTES))
        fixture_paths = [source / route / name for route in fixture_routes for name in FIXTURE_FILES] + [source / 'manifest.json']
        pinned = {str(path): digest(path) for path in fixture_paths}
        require(not any(Path(str(source / route / 'save.sqlite') + suffix).exists() for route in fixture_routes for suffix in ('-wal', '-shm')),
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

        def launch_probe(save, prefix, mode, timeout, label):
            fixture = source / FIXTURES[mode]
            command = [str(args.godot.resolve())] + ([] if args.rendered else ['--headless'])
            command += ['--path', str(launch), ENTRY_SCENE, '--', '--cargo-outcomes-probe', str(save), str(prefix),
                        str(fixture / 'genesis.ofc'), str(fixture / 'metadata.json'), mode]
            if args.rendered:
                command.append('--background-render')
            run(command, runtime, env, work, label, timeout, report)
            return json.loads(prefix.read_text())

        observations = {}
        for mode in ROUTES:
            require(time.monotonic() - started < LIMITS['aggregateSeconds'], 'Aggregate native bound exceeded before ' + mode)
            fixture = source / FIXTURES[mode]
            save, prefix = work / (mode + '.sqlite'), work / (mode + '.json')
            shutil.copyfile(fixture / 'save.sqlite', save)
            timeout = LIMITS['keyboardProcessSeconds'] if mode == 'victory' else LIMITS['deviceProcessSeconds']
            remaining = LIMITS['aggregateSeconds'] - (time.monotonic() - started)
            observed = launch_probe(save, prefix, mode, min(timeout, remaining), mode + '-active')
            verify(observed, mode, args.rendered)
            verify_submissions(observed, mode)
            journey(observed, mode)
            require(not Path(str(save) + '-wal').exists() and not Path(str(save) + '-shm').exists(), mode + ' left its save open.')
            observations[mode] = observed
            report['results'].append(dict(mode=mode, fixture=FIXTURES[mode], save=str(save), genesis=str(fixture / 'genesis.ofc'),
                                          before=str(fixture / 'before.ofc'), metadata=str(fixture / 'metadata.json'),
                                          report=str(prefix), finalDatabaseSha256=digest(save), images=sorted(observed['Images'])))
        defeat = work / 'defeat.sqlite'
        frozen = digest(defeat)
        remaining = LIMITS['aggregateSeconds'] - (time.monotonic() - started)
        reopened = launch_probe(defeat, work / 'reopen.json', 'reopen', min(LIMITS['reopenProcessSeconds'], remaining), 'defeat-reopen')
        verify(reopened, 'reopen', args.rendered)
        verify_submissions(reopened, 'reopen')
        require(digest(defeat) == frozen, 'The paused reopen changed the saved world.')
        saved = observations['defeat']
        require(all(reopened[key] == saved[key] for key in ('FinalFeet', 'FinalHp', 'EncounterCount', 'CompletedAtSequence',
                                                            'FinalResult', 'FinalStandingSide', 'FinalTerminal', 'CarriedRations')),
                'The reopened world differs from the saved terminal defeat.')
        report['reopen'] = dict(save=str(defeat), report=str(work / 'reopen.json'), images=sorted(reopened['Images']))
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
