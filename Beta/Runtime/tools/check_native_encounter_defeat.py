"""Qualify deliberate native Wait, actual player defeat and exact paused second-process recovery."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time
import traceback

from check_construction_physical import frame, read_frame
from check_encounter_actions import components
from check_encounter_lifecycle import action_root, database, lifecycle, predict, root_of
from check_liquid_cell_flow import u64
from check_live_encounter_pacing import sha, timing_of
from check_live_excavation import number


def require(value, message):
    if not value:
        raise AssertionError(message)


def identity(value):
    return value['High'].to_bytes(8, 'big') + value['Low'].to_bytes(8, 'big')


def verify_history(path, observation, runtime):
    """Reconstruct combat independently; pre-entry NPC geometry remains Host-verified context."""
    saved = database(path)
    genesis, initial_hash = saved['genesis']
    final_sequence, final_steps, final_bytes, final_hash = saved['head']
    require(sha(genesis) == initial_hash and sha(final_bytes) == final_hash, 'Invalid retained endpoint hash')
    initial, final = root_of(genesis), root_of(final_bytes)
    life = lifecycle(final)
    require(number(life[10]) == 5 and number(life[9]) == 1, 'Defeat must retain the first actual outcome')
    entry_rows = [row for row in saved['journal'] if row[1] == 8]
    require(len(entry_rows) == 3, 'Expected entry and two staging boundaries, without fabricated release')
    entry = number(entry_rows[0][0])
    require([number(row[0]) for row in entry_rows] == list(range(entry, entry + 3)), 'Noncontiguous entry/staging')
    player = identity(observation['Observations'][0]['View']['PlayerActorId'])
    entry_batch = read_frame(entry_rows[0][4], 'world.step-batch', 10)
    entered = read_frame(read_frame(entry_batch[9], 'world.step-actions')[1], 'world.actor-action', 10)
    entry_intent = read_frame(entered[9], 'world.action.enter-encounter', 6)
    scenarios = [read_frame(raw, 'encounter.scenario.v1', 8)
                 for raw in read_frame(life[12], 'encounter.scenarios.v1')[1:]]
    scenario = next(value for value in scenarios if value[1] == entry_intent[2])
    require(scenario[2] == b'bramble_wolf_first' and scenario[4] == player, 'Wrong original east confrontation')
    wolf = scenario[5]

    # Original source witnesses, never a modified-health or altered-stat defeat fixture.
    raw = (runtime / 'content/legacy/aldermere-first-loop/records.json').read_bytes()
    require(sha(raw).hex() == '9bfc3372954475402ea2fab30dab9de29b2a7c800f2fb61ab654d2efc81e973b', 'Source content changed')
    records = {row['targetKey']: row for row in json.loads(raw)['records']}
    _, abilities, grants, original_progression, original_people, stats = components(action_root(initial, life[16]))
    keys = ('hp', 'mp', 'attack', 'magic', 'defense', 'speed')
    source_player = records['openfantasy.aldermere:classes/squire']['sourcePayload']['baseStats']
    source_wolf = records['openfantasy.aldermere:monsters/bramble_wolf']['sourcePayload']['stats']
    require(stats[player] == [source_player[key] for key in keys], 'Player statistics differ from retained source')
    require(number(original_people[player][10]) == source_player['hp'], 'Player did not begin at original HP')
    for value in scenarios:
        require(stats[value[5]] == [source_wolf[key] for key in keys]
                and number(original_people[value[5]][10]) == source_wolf['hp'], 'Wolf source statistics changed')
    for ability in abilities.values():
        key = 'abilities/strike' if ability[2] == b'strike' else 'monsters/bramble_wolf'
        require(ability[4].hex() == records['openfantasy.aldermere:' + key]['sourceIdentity']['payloadSha256'],
                'Ability no longer uses its original source pin')
    require([actor for actor, (side, _) in grants.items() if side == 1] == [player], 'Unexpected player combat side')

    current = initial
    identities, command_rows, waits, damages = [], [], [], []
    player_choices = []
    boundary_hashes = {0: initial_hash.hex()}
    for sequence, kind, cause, steps, batch_bytes, outcome_bytes, before_hash, after_hash in saved['journal']:
        revision = number(sequence)
        batch = read_frame(batch_bytes, 'world.step-batch', 10)
        require(number(batch[1]) == 24 and number(batch[7]) == kind and batch[2] == cause, 'Wrong profile/batch owner')
        previous = current
        if revision < entry:
            require(kind == 1 and not read_frame(batch[9], 'world.step-actions')[1:], 'Unexpected work before entry')
            # Source NPC wandering is accepted by the actual Host, not reimplemented here.
            # All other components are independently advanced. Its final spatial bytes are
            # pinned by the exact pre-entry hash and separately checked through Host replay.
            ordinary = list(batch)
            ordinary[8] = frame('world.step-movement')
            predicted, receipts = predict(current, frame('world.step-batch', *ordinary[1:]))
            current = root_of(predicted)
            if revision == entry - 1:
                current[3] = final[3]
                predicted = frame('world.snapshot', *current[1:])
                require(sha(predicted) == after_hash, 'Exact pre-entry root reconstruction failed')
                boundary_hashes[revision] = after_hash.hex()
        else:
            require(read_frame(batch[8], 'world.step-movement')[1:] == [], 'Encounter accepted an ordinary movement sample')
            require(sha(frame('world.snapshot', *current[1:])) == before_hash, 'Broken exact combat before-root pin')
            predicted, receipts = predict(current, batch_bytes)
            current = root_of(predicted)
            require(sha(predicted) == after_hash, 'Independent complete combat root differs at ' + str(revision))
            boundary_hashes[revision] = after_hash.hex()
        require(current[8] == sequence and current[9] == steps, 'Stored revision/time mismatch')
        require(outcome_bytes == frame('world.action-outcomes', *receipts), 'Receipt frame differs from independent admission')
        identities.append((cause, sequence, 1))
        by_command = {read_frame(receipt, 'world.action-receipt')[2]: receipt for receipt in receipts}
        for encoded in read_frame(batch[9], 'world.step-actions')[1:]:
            command = read_frame(encoded, 'world.actor-action', 10)
            receipt = by_command[command[2]]
            command_rows.append((command[3], command[2], sequence, encoded, receipt))
            identities.extend([(command[2], sequence, 2), (sha(receipt)[:16], sequence, 3)])
            if kind == 8:
                require(revision == entry and number(command[1]) == 12, 'Unexpected lifecycle effect')
                identities.append((entry_intent[3], sequence, 4))
            elif command[5] == player:
                require(number(command[1]) == 10, 'Native defeat submitted a player action other than Wait')
                player_choices.append(command[2])
            else:
                require(command[5] == wolf and number(command[1]) == 11, 'Bystander or unsupported automatic action queued')
        if kind == 7:
            old_actions = read_frame(lifecycle(previous)[16], 'encounter.actions.v1', 13)
            actions = read_frame(lifecycle(current)[16], 'encounter.actions.v1', 13)
            resolved = read_frame(actions[12], 'encounter.action-resolution.v1', 15)
            _, _, clocks, _ = timing_of(current)
            if resolved[3] == player:
                require(number(resolved[7]) == 1 and not resolved[5] and not resolved[6]
                        and all(number(resolved[index]) == 0 for index in (8, 9, 10, 11, 13)), 'Wait fabricated damage or resources')
                speed = next(speed for actor, speed, _, _ in clocks if actor == player)
                delay = (10000 + speed - 1) // speed
                require(number(resolved[12]) == delay and next(debt for actor, _, debt, _ in clocks if actor == player) == delay,
                        'Resolved Wait did not charge exact standard delay')
                require(old_actions[11] == actions[11] and previous[13] == current[13], 'Wait drew RNG or changed HP/MP')
                waits.append(dict(revision=revision, commandId=resolved[4].hex(), delay=delay))
            else:
                require(resolved[3] == wolf and resolved[5] == player and number(resolved[7]) == 2
                        and number(resolved[8]) > 0, 'Only actual wolf damage may cause this defeat')
                damages.append(dict(revision=revision, damage=number(resolved[8]), hpBefore=number(resolved[9]),
                                    hpAfter=number(resolved[10]), delay=number(resolved[12])))
    require(frame('world.snapshot', *current[1:]) == final_bytes and current[8] == final_sequence
            and current[9] == final_steps, 'Exact final root differs')
    require(saved['identities'] == sorted(identities) and saved['commands'] == sorted(command_rows), 'Identity or command ledger differs')
    require(number(saved['journal'][-1][0]) == number(final_sequence) and saved['journal'][-1][1] == 7,
            'Post-outcome input published an unexpected commit')
    require(waits and damages and len(player_choices) == len(observation['WaitChoices']), 'Native deliberate inputs and accepted queues differ')
    _, _, _, final_progression, people, _ = components(action_root(final, life[16]))
    require(number(people[player][10]) == 0 and number(people[wolf][10]) == source_wolf['hp'], 'Wrong actual defeat')
    require(sum(row['damage'] for row in damages) == source_player['hp'], 'Damage does not conserve the original HP loss')
    for actor, original in original_people.items():
        retained = people[actor]
        require(original[1:3] == retained[1:3] and original[4:10] == retained[4:10] and original[11] == retained[11],
                'Progression, identity, source calibration or MP changed')
        if actor != player:
            require(original == retained, 'A wolf or bystander changed resources during player Wait')
    require(original_progression[8] == final_progression[8], 'Currency/reward supply changed')
    for field in (2, 5, 10, 12, 14, 15, 16, 17, 19):
        require(initial[field] == final[field], 'Retained material/work/policy owner changed at field ' + str(field))
    original_space, final_space = (read_frame(root[3], 'world.spatial', 8) for root in (initial, final))
    require(original_space[1:7] == final_space[1:7], 'Terrain, scope or body definitions changed')
    def bodies(space):
        return {read_frame(row[2], 'actor.form-binding', 9)[2]: row[1:4] + row[8:9]
                for raw in read_frame(space[7], 'world.presences')[1:]
                for row in [read_frame(raw, 'world.presence', 9)]}
    require(bodies(original_space) == bodies(final_space), 'A body/condition/home/identity was removed or substituted')
    require(len(people) == 4, 'Complete resident set changed')
    return dict(entryRevision=entry, finalRevision=number(final_sequence), finalHash=final_hash.hex(),
                reconstructedEncounterRoots=number(final_sequence) - entry + 1, preEntryOrdinarySteps=entry - 1,
                playerId=player.hex(), wolfId=wolf.hex(), playerWaitsQueued=len(player_choices), playerWaitsResolved=len(waits),
                waits=waits, wolfDamage=damages, tacticalTicks=number(life[7]), retainedActors=4,
                sourceContentSha256=sha(raw).hex(), commandReceipts=len(command_rows), identities=len(identities),
                boundaryHashes={str(revision): boundary_hashes.get(revision) for revision in
                                [entry - 1, entry, entry + 1, entry + 2, number(final_sequence)]},
                limitation='Pre-entry NPC movement is verified by real Host replay and the exact pre-entry root pin; only entry onward is independently reconstructed.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', type=Path, required=True)
    parser.add_argument('--godot', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--rendered', action='store_true')
    args = parser.parse_args()
    runtime = Path(__file__).resolve().parents[1]
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix='native-encounter-defeat-', dir=output.parent))
    sdk = args.dotnet.resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get('PATH', ''))
    client = runtime / 'client'
    original_project = (client / 'project.godot').read_bytes()
    report = dict(status='running', rendered=args.rendered, workDirectory=str(work), runs=[],
                  startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope='Original-source native Wait and actual player incapacity, blocked outcome input, exact second-process paused reopen; no invented release/recovery or physical-device/art acceptance')

    def run(label, command, limit):
        started = time.monotonic()
        item = dict(label=label, command=list(map(str, command)), timeoutSeconds=limit)
        report['runs'].append(item)
        stdout = stderr = ''
        try:
            result = subprocess.run(item['command'], cwd=runtime, env=env, capture_output=True, text=True, timeout=limit)
            item['exitCode'] = result.returncode
            stdout, stderr = result.stdout, result.stderr
        except subprocess.TimeoutExpired as error:
            stdout = error.stdout or b''
            stderr = error.stderr or b''
            if isinstance(stdout, bytes): stdout = stdout.decode(errors='replace')
            if isinstance(stderr, bytes): stderr = stderr.decode(errors='replace')
            item['timedOut'] = True
            raise
        finally:
            item['seconds'] = time.monotonic() - started
            for name, value in [('stdout', stdout), ('stderr', stderr)]:
                path = work / (label + '.' + name + '.log')
                path.write_text(value)
                item[name + 'File'] = str(path)
        require(item['seconds'] < limit and result.returncode == 0 and 'ERROR:' not in stdout + stderr,
                'Process failed at ' + label)
        return stdout

    try:
        launch_client = client
        if args.rendered:
            launch_client = work / 'background-client'
            launch_client.mkdir()
            for path in client.iterdir():
                if path.name != 'project.godot':
                    (launch_client / path.name).symlink_to(path, target_is_directory=path.is_dir())
            config = original_project.decode()
            require(config.count('[display]') == 1 and 'window/size/no_focus' not in config,
                    'Native startup no-focus fixture no longer matches the original project')
            (launch_client / 'project.godot').write_text(config.replace('[display]', '[display]\nwindow/size/no_focus=true', 1))
        world = work / 'world.sqlite'
        base = work / 'journey.json'
        observations = {}
        for mode in ['fresh', 'reopen']:
            before = sha(world.read_bytes()) if mode == 'reopen' else None
            command = [args.godot.resolve()]
            if not args.rendered: command.append('--headless')
            command += ['--path', launch_client, '--', '--encounter-defeat-probe', world, base, mode, '--background-render']
            run(mode, command, 180)
            path = work / ('journey.' + mode + '.json')
            observed = json.loads(path.read_text())
            observations[mode] = observed
            require(observed['Status'] == 'passed' and observed['FocusRequests'] == 0
                    and observed['IsLocallyPaused'] and observed['DurationMilliseconds'] < 150000, 'Incomplete native defeat result')
            if args.rendered:
                require(observed['Display'] != 'headless' and observed['Unfocusable'] and not observed['NativeFocused']
                        and not observed['ObservedNativeFocus'] and observed['StartupNoFocus'], 'Native focus isolation failed')
            if mode == 'reopen':
                require(sha(world.read_bytes()) == before and observed['InitialHash'] == observations['fresh']['FinalHash']
                        and observed['FinalHash'] == observed['InitialHash'], 'Second-process outcome was not exact and immutable')
        report['inputFormsReached'] = list(dict.fromkeys(choice['Control'] for choice in observations['fresh']['WaitChoices']))
        require(report['inputFormsReached'] and report['inputFormsReached'][0] == 'wheel', 'No confirmed wheel Wait observed')
        report['oracle'] = verify_history(world, observations['fresh'], runtime)
        frozen = sha(world.read_bytes())
        oracle = report['oracle']
        revisions = [oracle['entryRevision'] - 1, oracle['entryRevision'], oracle['entryRevision'] + 1,
                     oracle['entryRevision'] + 2, oracle['finalRevision']]
        boundaries = []
        for revision in revisions:
            command = [args.dotnet.resolve(), runtime / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll',
                       '--inspect-recorded-world', world, str(revision)]
            state = json.loads(run('recorded-' + str(revision), command, 30))
            require(state['RecordedRevision'] == revision and state['SourceHeadRevision'] == oracle['finalRevision']
                    and state['SourceHeadHash'] == oracle['finalHash'], 'Incorrect recorded source pin')
            expected_hash = oracle['boundaryHashes'][str(revision)]
            if expected_hash is not None: require(state['StateHash'] == expected_hash, 'Recorded complete root differs from oracle')
            boundaries.append(state)
        require([state['EncounterLifecycle']['Phase'] for state in boundaries]
                == ['Exploration', 'Confrontation', 'Staging', 'Active', 'Outcome'], 'Incorrect exact phase progression')
        positions = lambda state: {actor['ActorId']: (actor['Position'], actor['PresenceRevision']) for actor in state['Actors']}
        require(all(positions(state) == positions(boundaries[0]) for state in boundaries[1:]), 'Entry/combat changed an authoritative body position')
        require(all(state['Calendar'] == boundaries[0]['Calendar'] for state in boundaries[1:4]), 'Entry/staging granted time')
        require(sha(world.read_bytes()) == frozen, 'Read-only historical inspection changed the save')
        report['exactRecordedBoundaries'] = revisions
        if args.rendered:
            expected = {'journey.' + stage + '.png' for stage in
                        ['waiting', 'costed-wait', 'outcome', 'defeat-inspection', 'fresh', 'reopen']}
            require({path.name for path in work.glob('*.png')} == expected, 'Missing rendered defeat states')
        require((client / 'project.godot').read_bytes() == original_project, 'Shared project configuration changed')
        report['originalProjectSha256'] = sha(original_project).hex()
        report['status'] = 'passed'
    except Exception as error:
        report.update(status='failed', failure=str(error), traceback=traceback.format_exc())
    finally:
        report['artifactSha256'] = {str(path.relative_to(output.parent)): hashlib.sha256(path.read_bytes()).hexdigest()
                                   for path in sorted(work.iterdir()) if path.is_file()}
        report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2) + '\n')
    print('native encounter defeat: ' + report['status'], flush=True)
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
