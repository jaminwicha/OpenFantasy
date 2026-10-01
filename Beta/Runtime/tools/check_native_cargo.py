#!/usr/bin/env python3
"""Qualify the native cargo route: keyboard travel and trading across the finite cargo world, then a separate paused reopen.

The exported fixture is immutable input; each run uses a disposable copy. Managed NativeCargoResultTests authenticate the
actual journal, parties, prices, balances, lot intervals and coordinates separately; rendered reports alone are insufficient.
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

ENTRY_SCENE = 'res://scenes/NativeCargo.tscn'
ACTIVE_IMAGES = ('farm-start', 'farm-offers', 'brasshollow', 'brasshollow-bought', 'corridor-boundary', 'gearford',
                 'gearford-stall-choice', 'gearford-sold', 'gearford-history', 'return-history', 'final')
REOPEN_IMAGES = ('reopen', 'reopen-final')
ONBOARDING_IMAGES = ('onboarding-start', 'wren-reward', 'bank', 'water', 'marla')
LIMITS = dict(probeSeconds=720, processSeconds=750, openingSeconds=30, reopenProcessSeconds=90,
              onboardingProbeSeconds=300, onboardingProcessSeconds=330)
# Window focus requests a probe or entry could make; the audit is of source text, not a runtime counter.
FOCUS_REQUEST_PATTERNS = ('MoveToForeground', 'GetWindow().GrabFocus', 'WindowRequestAttention', 'RequestAttention(')
UI_STAGES = ('buy-marla', 'choose-kegga', 'sell-kegga', 'history-kegga', 'history-marla')
WALK_STAGES = ('walk-to-brasshollow', 'walk-outbound', 'walk-homeward')


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def keyboard_only(rows):
    """Every injected UI event is a keyboard event: no controller or pointer route was substituted."""
    foreign = [row for row in rows if any(key in row for key in ('Controller', 'PointerControl', 'PointerSector', 'PointerHover', 'Wheel'))]
    require(not foreign, 'Another device route was substituted: ' + json.dumps(foreign[:3]))
    require(all(row.get('Route', 'keyboard') == 'keyboard' for row in rows), 'An input row names another route.')


def chosen(rows, stage):
    return [(row['Dropdown'], row['Selected'], row['Text']) for row in rows
            if row.get('Stage') == stage and 'Dropdown' in row and 'Selected' in row and 'RowReached' not in row]


def verify_report(observed, mode, rendered, limit_ms, kind='native-cargo-v1'):
    require(observed['Kind'] == kind and observed['Status'] == 'passed' and observed['Mode'] == mode
            and observed['Route'] == 'keyboard' and observed['IsLocallyPaused']
            and observed['DurationMilliseconds'] < limit_ms, 'Incomplete native cargo report: ' + mode)
    focus = observed['Focus']
    if rendered:
        # Observed window state sampled every frame; no focus-request count is claimed by the probe.
        require(focus['Unfocusable'] and not focus['NativeFocused'] and not focus['ObservedNativeFocus']
                and focus['ObservedFocusTransitions'] == 0 and focus['FramesObserved'] > 0 and focus['StartupNoFocus'],
                'Background isolation failed: ' + json.dumps(focus))


def focus_source_audit(client):
    """Source audit, not a measurement: no cargo probe or entry requests OS window focus or attention."""
    files = sorted((client / 'scripts' / 'Cargo').glob('*.cs')) + [client / 'scripts' / 'NativeCargoEntry.cs']
    found = {str(path): [pattern for pattern in FOCUS_REQUEST_PATTERNS if pattern in path.read_text()] for path in files}
    require(not any(found.values()), 'A cargo probe requests window focus: ' + json.dumps(found))
    return dict(kind='source-audit', patterns=list(FOCUS_REQUEST_PATTERNS), files=sorted(found))


def verify_rendered(samples, minimum, label):
    require(len(samples) >= minimum, f'{label}: rendered samples are missing ({len(samples)} < {minimum}).')
    for sample in samples:
        require(sample['Pixels'] is not None and sample['Drawn'] is not None,
                f"{label}: sample {sample['Label']} lacks framebuffer pixels or the drawn terrain record.")


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
    work = Path(tempfile.mkdtemp(prefix='native-cargo-', dir=output.parent))
    source = args.fixtures.resolve()
    project = (client / 'project.godot').read_bytes()
    sdk = args.dotnet.resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get('PATH', ''))
    report = dict(status='running', workDirectory=str(work), rendered=args.rendered, entryScene=ENTRY_SCENE, runs=[], results=[],
                  startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(), limits=LIMITS, coverage={},
                  limitations=['Walking holds injected arrow keys through the ordinary input map; read-only route advice only chooses '
                               'which key is held, and the world validates every step.',
                               'Stall choice, buying, showing, selling and history use the production wheel and stall panel with '
                               'injected keyboard events along the panel focus chain.',
                               'Focus is injected through the fixture hook; no OS focus or physical device is exercised. '
                               'Window focus is observed state; the absence of focus requests is a source audit.',
                               'The onboarding run uses the production --cargo selection on a fresh disposable save path.',
                               'Keyboard only: controller and pointer cargo routes, archive-only inspection in this world and '
                               'retained-save migration are not exercised here.',
                               'Finite admitted regions only: no streaming, frontier, threat/escort, NPC travel, multiplayer, art or '
                               'production acceptance.'])
    try:
        fixture_paths = [source / name for name in ('save.sqlite', 'genesis.ofc', 'before.ofc', 'metadata.json')]
        pinned = {str(path): digest(path) for path in fixture_paths}
        require(not any(Path(str(source / 'save.sqlite') + suffix).exists() for suffix in ('-wal', '-shm')),
                'Fixture database must be closed.')
        binaries = list((client / '.godot/mono/temp/bin/Debug').glob('OpenFantasy*.dll'))
        require(len(binaries) >= 3, 'Missing built client assemblies.')
        paths = [Path(__file__).resolve(), client / 'project.godot', args.dotnet.resolve(), args.godot.resolve()] + binaries
        paths += [path for directory in ('scripts', 'scenes', 'assets/ui', 'assets/cargo-circuit') for path in (client / directory).rglob('*')
                  if path.is_file() and path.suffix in ('.cs', '.tscn', '.svg', '.tres', '.png', '.json')]
        report['executionPins'] = {str(path): digest(path) for path in paths}
        report['fixturePins'] = pinned
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
        save, prefix = work / 'keyboard.sqlite', work / 'keyboard.json'
        shutil.copyfile(source / 'save.sqlite', save)
        observations = {}
        for phase, timeout in (('active', LIMITS['processSeconds']), ('reopen', LIMITS['reopenProcessSeconds'])):
            frozen = digest(save) if phase == 'reopen' else None
            command = [str(args.godot.resolve())] + ([] if args.rendered else ['--headless'])
            command += ['--path', str(launch), ENTRY_SCENE, '--', '--cargo-probe', str(save), str(prefix),
                        str(source / 'genesis.ofc'), str(source / 'metadata.json'), 'keyboard' if phase == 'active' else 'reopen']
            if args.rendered:
                command.append('--background-render')
            run(command, runtime, env, work, 'keyboard-' + phase, timeout, report)
            observed = json.loads((work / ('keyboard.' + phase + '.json')).read_text())
            observations[phase] = observed
            verify_report(observed, 'keyboard' if phase == 'active' else 'reopen', args.rendered,
                          LIMITS['probeSeconds' if phase == 'active' else 'reopenProcessSeconds'] * 1000)
            if phase == 'reopen':
                require(digest(save) == frozen and observed['InitialHash'] == observed['FinalHash']
                        == observations['active']['FinalHash'], 'Paused reopen changed save/public state.')
                require(observed['FinalFeet'] == observations['active']['FinalFeet'], 'Reopened coordinates differ.')
        # The ordinary production --cargo selection on a fresh disposable save: onboarding, mining and water, then Marla.
        onboarding_save, onboarding_prefix = work / 'onboarding.sqlite', work / 'onboarding.json'
        require(not onboarding_save.exists(), 'The onboarding save must not exist before the production opening.')
        command = [str(args.godot.resolve())] + ([] if args.rendered else ['--headless'])
        command += ['--path', str(launch), ENTRY_SCENE, '--', '--cargo-onboarding-probe', str(onboarding_save), str(onboarding_prefix)]
        if args.rendered:
            command.append('--background-render')
        run(command, runtime, env, work, 'onboarding', LIMITS['onboardingProcessSeconds'], report)
        onboarding = json.loads(onboarding_prefix.read_text())
        verify_report(onboarding, 'onboarding', args.rendered, LIMITS['onboardingProbeSeconds'] * 1000, 'native-cargo-onboarding-v1')
        require(onboarding['Entry'] == '--cargo' and onboarding['FinalBalance'] > 0, 'Onboarding did not earn the reward.')
        keyboard_only(onboarding['Inputs'] + onboarding['WalkInputs'])
        require({'wren-quest', 'mine-divider'} <= {row['Stage'] for row in onboarding['Inputs'] if 'Key' in row},
                'Onboarding keyboard input is missing at the quest or mining stage.')
        active, reopened = observations['active'], observations['reopen']
        require(active['HistoryRead'] is not None and active['HistoryRead']['Frames'] >= 5
                and active['HistoryRead']['LongestFrameSeconds'] < 1.0, 'No frame progress during the saved-history read.')
        require(active['StallChoice'] is not None, 'The pinned-stall route explanation was not observed.')
        if args.rendered:
            verify_rendered(active['Rendered'], 4, 'active')
            verify_rendered(reopened['Rendered'], 1, 'reopen')
            verify_rendered(onboarding['Rendered'], 4, 'onboarding')
            require(any(sample['Pixels']['Water'] is not None for sample in onboarding['Rendered']),
                    'No rendered water pixel was sampled after mining.')
            images = {Path(path).name for path in onboarding['Images']}
            missing_images = {'onboarding.' + stage + '.png' for stage in ONBOARDING_IMAGES} - images
            require(not missing_images, 'Rendered onboarding evidence incomplete: ' + ', '.join(sorted(missing_images)))
        report['focusSourceAudit'] = focus_source_audit(client)
        keyboard_only(active['Inputs'] + reopened['Inputs'] + active['WalkInputs'])
        stages = {row['Stage'] for row in active['Inputs'] if 'Key' in row or 'Dropdown' in row}
        missing = [stage for stage in UI_STAGES if stage not in stages]
        require(not missing, 'Keyboard stall input missing at ' + ', '.join(missing))
        holds = {row['Stage'] for row in active['WalkInputs'] if 'Hold' in row}
        require(all(stage in holds for stage in WALK_STAGES), 'Held-key walking missing at a travel stage.')
        walks = {walk['Label']: walk for walk in active['Walks']}
        require(walks['brasshollow-to-gearford']['Transitions'] == 13 and walks['gearford-to-brasshollow']['Transitions'] == 13,
                'The corridor was not crossed region by region in both directions.')
        require(len(active['Agreement']) >= 30 and len(reopened['Agreement']) >= 1, 'Projection agreement samples are missing.')
        require([row[0] for row in chosen(active['Inputs'], 'choose-kegga')] == ['MarketStall'], 'Kegga was not chosen in the selector.')
        require([row[0] for row in chosen(active['Inputs'], 'history-marla')][:1] == ['MarketStall'], 'Marla was not chosen on return.')
        require(any('sold 2 rations' in row[2] for row in chosen(active['Inputs'], 'history-kegga')), 'The sale record row was not chosen.')
        require(any('bought 2 rations' in row[2] for row in chosen(reopened['Inputs'], 'reopen')), 'Reopened purchase row was not chosen.')
        require(any('sold 2 rations' in row[2] for row in chosen(reopened['Inputs'], 'reopen')), 'Reopened sale row was not chosen.')
        if args.rendered:
            images = {Path(path).name for path in active['Images'] + reopened['Images']}
            expected = {'keyboard.' + stage + '.png' for stage in ACTIVE_IMAGES + REOPEN_IMAGES}
            require(expected <= images and all(Path(path).is_file() for path in active['Images'] + reopened['Images']),
                    'Rendered cargo evidence incomplete: ' + ', '.join(sorted(expected - images)))
        report['coverage'] = dict(route='keyboard', uiStages=sorted(stages), walkStages=sorted(holds),
                                  walks=active['Walks'], agreementSamples=len(active['Agreement']),
                                  checks=active['Checks'] + reopened['Checks'], physicalDevice=False)
        report['results'].append(dict(mode='keyboard', save=str(save), genesis=str(source / 'genesis.ofc'),
            before=str(source / 'before.ofc'), metadata=str(source / 'metadata.json'), report=str(work / 'keyboard.active.json'),
            reopenReport=str(work / 'keyboard.reopen.json'), finalDatabaseSha256=digest(save),
            buyQuoteId=active['BuyQuoteId'], sellQuoteId=active['SellQuoteId'], images=sorted(active['Images'] + reopened['Images'])))
        require(pinned == {str(path): digest(path) for path in fixture_paths}, 'Exported fixture mutated.')
        require(report['executionPins'] == {str(path): digest(path) for path in paths}, 'Execution sources or binaries changed.')
        report['onboarding'] = dict(mode='onboarding', save=str(onboarding_save), report=str(onboarding_prefix),
                                    finalDatabaseSha256=digest(onboarding_save), seedText=onboarding['SeedText'],
                                    images=sorted(onboarding['Images']))
        manifest = work / 'native-manifest.json'
        manifest.write_text(json.dumps(dict(results=report['results'], onboarding=report['onboarding']), indent=2) + '\n')
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
