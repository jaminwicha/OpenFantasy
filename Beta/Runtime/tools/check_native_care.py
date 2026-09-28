"""Qualify native patient acknowledgment, finite helper care and paused process reopening.

Reads actual schema7/profile25 bytes, source pins, commands, receipts and material/resource
witnesses. Historical roots and gameplay legality are authenticated by the real Host replay;
this script does not claim an independent damage-law or complete-root replay implementation.
"""
import argparse
from contextlib import closing
import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import sqlite3
import subprocess
import tempfile
import time
import traceback

from check_construction_physical import read_frame
from check_encounter_actions import components
from check_live_excavation import fields, number


CONTROLS = ('wheel', 'keyboard', 'controller', 'pointer')
RECORDS_HASH = '9bfc3372954475402ea2fab30dab9de29b2a7c800f2fb61ab654d2efc81e973b'
POTION_HASH = '3b7a5fdd562795e58d68a11007763b88cd25d40841c739c4fef9495161c097aa'
RECIPE_HASH = 'dd98e32ef5f0442f9d1c37d10d431aab74226a77f73bc6858a5075e4dad41dc2'


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def sha(value):
    return hashlib.sha256(value).digest()


def one(values, message):
    values = list(values)
    require(len(values) == 1, message)
    return values[0]


def root(data):
    value = read_frame(data, 'world.snapshot', 24)
    require(number(value[1]) == 25, 'Native care requires exact profile25')
    return value


def database(path):
    with closing(sqlite3.connect(path.as_uri() + '?mode=ro', uri=True)) as connection:
        connection.execute('BEGIN')
        require(connection.execute('PRAGMA application_id').fetchone()[0] == 0x4F464157, 'Wrong world application')
        require(connection.execute('PRAGMA user_version').fetchone()[0] == 7, 'Wrong care storage schema')
        require(connection.execute('PRAGMA quick_check').fetchall() == [('ok',)], 'SQLite integrity failure')
        require(not connection.execute('PRAGMA foreign_key_check').fetchall(), 'Broken stored reference')
        return dict(
            genesis=connection.execute('SELECT state,hash FROM world_genesis WHERE singleton=1').fetchone(),
            head=connection.execute('SELECT sequence,advancing_steps,state,hash FROM world_head WHERE singleton=1').fetchone(),
            journal=connection.execute('SELECT sequence,kind,cause_id,advancing_steps,accepted_batch,outcomes,before_hash,after_hash FROM world_commits ORDER BY sequence').fetchall(),
            commands=connection.execute('SELECT principal,command_id,sequence,command,receipt FROM world_command_outcomes ORDER BY principal,command_id').fetchall(),
            identities=connection.execute('SELECT identity,sequence,kind FROM world_occupied_identity ORDER BY identity').fetchall(),
            archives=connection.execute('SELECT world_id,branch_id,ordinal,boundary_sequence,publication_sequence,boundary_hash,state FROM world_resource_segments ORDER BY publication_sequence').fetchall())


def custody(value):
    authority = read_frame(value[2], 'authority.counted-state', 9)
    stock = read_frame(authority[3], 'custody.snapshot.v1', 6)
    lots = [read_frame(encoded, 'custody.source-lot.v1', 11) for encoded in read_frame(stock[5], 'custody.lots.v1')[1:]]
    controls = {row[3]: row[1:3] for encoded in read_frame(authority[5], 'authority.controls')[1:]
                for row in [read_frame(encoded, 'authority.control', 4)]}
    return authority, lots, controls


def combat(value, actions):
    selected = list(value)
    selected[21] = actions
    return components(selected)


def verify_source(initial, final, runtime, agreement, remedy):
    records_bytes = (runtime / 'content/legacy/aldermere-first-loop/records.json').read_bytes()
    require(sha(records_bytes).hex() == RECORDS_HASH, 'Retained source packet changed')
    records = {row['targetKey']: row for row in json.loads(records_bytes)['records']}
    potion = records['openfantasy.aldermere:items/potion']
    require(potion['sourceIdentity']['payloadSha256'] == POTION_HASH and remedy[3].hex() == POTION_HASH
            and number(remedy[4]) == potion['sourcePayload']['healHp'] == 40
            and number(remedy[5]) == 256, 'Remedy differs from retained Potion or declared range')
    life = read_frame(final[21], 'encounter.lifecycle.v1', 17)
    _, abilities, grants, _, people, stats = combat(initial, life[16])
    player = agreement[2]
    keys = ('hp', 'mp', 'attack', 'magic', 'defense', 'speed')
    source_player = records['openfantasy.aldermere:classes/squire']['sourcePayload']['baseStats']
    source_wolf = records['openfantasy.aldermere:monsters/bramble_wolf']['sourcePayload']['stats']
    require(stats[player] == [source_player[key] for key in keys]
            and number(people[player][10]) == source_player['hp'], 'Player initial statistics/HP were calibrated away from source')
    scenarios = [read_frame(encoded, 'encounter.scenario.v1', 8)
                 for encoded in read_frame(life[12], 'encounter.scenarios.v1')[1:]]
    for scenario in scenarios:
        require(stats[scenario[5]] == [source_wolf[key] for key in keys]
                and number(people[scenario[5]][10]) == source_wolf['hp'], 'Wolf statistics differ from source')
    for ability in abilities.values():
        key = 'abilities/strike' if ability[2] == b'strike' else 'monsters/bramble_wolf'
        require(ability[4].hex() == records['openfantasy.aldermere:' + key]['sourceIdentity']['payloadSha256'],
                'Ability lost its retained source pin')
    require(grants[player][0] == 1 and grants[agreement[3]][0] == 0, 'Patient/helper combat roles changed')
    source_recipe_bytes = (runtime / 'content/legacy/aldermere-first-loop/source-support/data/recipes.json').read_bytes()
    require(sha(source_recipe_bytes).hex() == '0c81bcf33088668a7837e77cf01640cbfd2070e2a8caf9fa13ebdeb1e032a6b1',
            'Retained recipe file changed')
    source_recipe = json.loads(source_recipe_bytes)['recipes'][0]
    require(source_recipe == dict(id='craft_potion', output='potion', outputCount=1, inputs=[dict(item='berries', count=3)]),
            'Original Potion recipe differs')
    production = read_frame(initial[12], 'production.snapshot', 9)
    recipes = [read_frame(encoded, 'craft.recipe', 6) for encoded in read_frame(production[5], 'production.recipes')[1:]]
    selected = one((value for value in recipes if read_frame(value[3], 'production.recipe', 9)[4] == b'craft_potion'),
                   'Missing unique actual Potion recipe')
    physical = read_frame(selected[3], 'production.recipe', 9)
    require(physical[6].hex() == RECIPE_HASH, 'Potion recipe is not pinned to the original payload')
    terms = [read_frame(encoded, 'production.recipe-term', 6)
             for encoded in read_frame(physical[8], 'production.recipe-terms')[2:]]
    require(sorted((number(term[2]), number(term[5])) for term in terms) == [(1, 3), (2, 1)], 'Potion recipe counts changed')
    bindings = [read_frame(encoded, 'craft.item-physical', 6) for encoded in read_frame(selected[4], 'craft.bindings')[1:]]
    require(any(value[2:4] == [remedy[2], remedy[3]] for value in bindings), 'Recipe output is a different Potion item')
    berry = one((value for value in bindings if value[2] != remedy[2]), 'Potion must reuse one berry binding')
    require(berry[2].hex() == '2038079b78c92d2f77fb1443eed73661'
            and berry[3].hex() == '2a558e18e3a30b09446b6f0b25da2c2bcbca7ef8676459eab071ff27c8a286c6', 'Berry identity was duplicated or replaced')
    require(all(not read_frame(production[index], tag)[1:] for index, tag in
                [(6, 'production.batches'), (7, 'production.lots'), (8, 'production.consumed')]), 'Fresh content includes earned production')
    return dict(sourceRecordsSha256=RECORDS_HASH, potionPayloadSha256=POTION_HASH, recipePayloadSha256=RECIPE_HASH,
                playerSourceHp=source_player['hp'], wolfSourceHp=source_wolf['hp'])


def verify_history(path, runtime, *, pending_continuation=False):
    saved = database(path)
    genesis_bytes, genesis_hash = saved['genesis']
    final_sequence, final_steps, final_bytes, final_hash = saved['head']
    require(sha(genesis_bytes) == genesis_hash and sha(final_bytes) == final_hash, 'Invalid endpoint checksum')
    initial, final = root(genesis_bytes), root(final_bytes)
    require(final[8:10] == [final_sequence, final_steps] and not saved['archives'], 'Unexpected endpoint or archive in bounded native journey')
    initial_care, care = (read_frame(value[22], 'care.snapshot', 9) for value in (initial, final))
    agreement = read_frame(one(read_frame(initial_care[6], 'care.agreements')[1:], 'Expected one prior agreement'), 'care.agreement.v1', 6)
    remedy = read_frame(one(read_frame(initial_care[5], 'care.remedies')[1:], 'Expected one pinned remedy'), 'care.remedy.v1', 6)
    require(initial_care[5:7] == care[5:7] and not read_frame(initial_care[7], 'care.acknowledgments')[1:]
            and not read_frame(initial_care[8], 'care.treatments')[1:], 'Authored care changed or began earned')
    acknowledgment = read_frame(one(read_frame(care[7], 'care.acknowledgments')[1:], 'Expected one actual acknowledgment'), 'care.acknowledgment.v1', 9)
    treatment = read_frame(one(read_frame(care[8], 'care.treatments')[1:], 'Expected one actual treatment'), 'care.treatment.v1', 12)
    player, helper = agreement[2:4]
    require(acknowledgment[3] == agreement[1] and treatment[3] == acknowledgment[1], 'Care participants or prior permission changed')
    source = verify_source(initial, final, runtime, agreement, remedy)
    initial_inventory, initial_lots, controls = custody(initial)
    final_inventory, final_lots, final_controls = custody(final)
    require(controls == final_controls and controls[player] != controls[helper], 'Actual patient/helper controller bindings were conflated')
    require(initial_inventory[2] == final_inventory[2] and initial_inventory[7] == final_inventory[7], 'Care invented source rights or capacity')
    stocked = one((lot for lot in initial_lots if lot[3:5] == [remedy[2], remedy[3]]), 'Expected finite raw Potion stock')
    require(number(stocked[7]) - number(stocked[6]) == 2, 'Fresh helper stock is not exactly two doses')
    remaining = [lot for lot in final_lots if lot[3:5] == [remedy[2], remedy[3]]]
    require(sum(number(lot[7]) - number(lot[6]) for lot in remaining) == 1
            and all(lot[2] == stocked[2] and lot[5] == stocked[5] for lot in remaining), 'Treatment did not consume exactly one existing dose')
    production = read_frame(final[12], 'production.snapshot', 9)
    require(not read_frame(production[6], 'production.batches')[1:] and not read_frame(production[7], 'production.lots')[1:],
            'Native raw care fabricated crafting or derived stock')
    claim = read_frame(one(read_frame(production[8], 'production.consumed')[1:], 'Expected one consumed dose'), 'production.consumed-item', 8)
    ingredient = read_frame(claim[7], 'production.ingredient', 8)
    interval = read_frame(ingredient[7], 'production.source-claim', 5)
    require(claim[2] == treatment[4] and claim[3] == treatment[2] and claim[4:7] == [helper, player, stocked[2]], 'Consumption ownership differs')
    require(number(ingredient[2]) == 1 and ingredient[3] == stocked[1] and ingredient[5:7] == [remedy[2], remedy[3]]
            and interval[2] == stocked[5] and number(interval[4]) - number(interval[3]) == 1, 'Dose lineage differs from admitted stock')
    ranges = sorted([(number(interval[3]), number(interval[4]))] + [(number(lot[6]), number(lot[7])) for lot in remaining])
    require(ranges[0][0] == number(stocked[6]) and ranges[-1][1] == number(stocked[7])
            and all(left[1] == right[0] for left, right in zip(ranges, ranges[1:])), 'Potion source intervals overlap or lose rights')
    require(number(treatment[9]) == 40 and number(treatment[11]) > 0, 'Treatment lost capped healing or helper debt')

    rows, prior_hash, commands = saved['journal'], genesis_hash, []
    require([number(row[0]) for row in rows] == list(range(1, number(final_sequence) + 1)), 'Journal sequence incomplete')
    care_rows = [row for row in rows if row[1] == 9]
    require(len(care_rows) == 2 and number(care_rows[1][0]) == number(care_rows[0][0]) + 1, 'Care requires two separate consecutive actual commits')
    ack_revision, treatment_revision = (number(row[0]) for row in care_rows)
    require(number(acknowledgment[5]) == ack_revision and number(treatment[5]) == treatment_revision, 'Care record sequence differs from journal')
    selected_commands = {}
    player_attacks, earlier_attacks = [], []
    for row in rows:
        sequence, kind, cause, steps, batch_bytes, outcome_bytes, before_hash, after_hash = row
        batch = read_frame(batch_bytes, 'world.step-batch', 10)
        require(number(batch[1]) == 25 and number(batch[7]) == kind and batch[2] == cause
                and number(batch[5]) + 1 == number(sequence) and before_hash == prior_hash, 'Wrong profile/sequence/hash chain')
        prior_hash = after_hash
        actions = read_frame(batch[9], 'world.step-actions')[1:]
        outcomes = read_frame(outcome_bytes, 'world.action-outcomes')[1:]
        require(len(actions) == len(outcomes), 'Command and receipt counts differ')
        by_command = {read_frame(value, 'world.action-receipt', 10)[2]: value for value in outcomes}
        for encoded in actions:
            action = read_frame(encoded, 'world.actor-action', 10)
            receipt_bytes = by_command[action[2]]
            receipt = read_frame(receipt_bytes, 'world.action-receipt', 10)
            require(action[3:5] == controls[action[5]] and action[6:8] == batch[3:5], 'Command impersonates another controller/scope')
            require(receipt[3:6] == [action[3], *batch[3:5]] and receipt[6] == sha(encoded)
                    and receipt[7] == sequence and number(receipt[8]) == 1 and number(receipt[9]) == 0, 'Actual command receipt differs')
            commands.append((action[3], action[2], sequence, encoded, receipt_bytes))
            selected_commands[action[2]] = (number(sequence), action)
            if kind == 9:
                require(len(actions) == 1 and not read_frame(batch[8], 'world.step-movement')[1:], 'Care batch contains competing activity')
            if action[5] == player and number(action[1]) == 11:
                if number(sequence) > treatment_revision:
                    player_attacks.append(action[2])
                else:
                    require(pending_continuation and number(sequence) < ack_revision,
                            'Player attack was not part of the declared pre-care combat setup')
                    earlier_attacks.append(action[2])
    require(prior_hash == final_hash and sorted(commands) == saved['commands'], 'Head chain or exact receipt ledger differs')
    ack = selected_commands[acknowledgment[2]][1]
    administer = selected_commands[treatment[2]][1]
    require(ack[5] == player and administer[5] == helper and number(ack[1]) == number(administer[1]) == 13, 'Patient and helper commands conflated')
    ack_intent = read_frame(ack[9], 'world.action.acknowledge-care', 6)
    remedy_intent = read_frame(administer[9], 'world.action.administer-remedy', 9)
    require(ack_intent[2] == agreement[1] and ack_intent[3] == acknowledgment[4] and ack_intent[5] == acknowledgment[6], 'Acknowledgment altered its captured outcome')
    require(remedy_intent[2] == acknowledgment[1], 'Helper acted without this patient acknowledgment')
    require(len(player_attacks) == 1, 'Expected one deliberate resumed player attack')
    history = read_frame(final[23], 'world.resource-history', 7)
    require(number(history[1]) == 1, 'Unexpected continued history in short native journey')
    evidence = [fields(encoded) for encoded in read_frame(history[6], 'world.resource-evidence')[1:]]
    attacks = [value for value in evidence if value[0] == b'world.resource-attack.v1']
    require(all(len(value) == 4 for value in attacks), 'Malformed retained attack witness')
    restored_evidence = one((value for value in evidence if value[0] == b'world.resource-treatment.v1'),
                            'Treatment has no unique resource witness')
    require(restored_evidence[1:] == [treatment[2], treatment[1]], 'Treatment resource witness selects another command')
    resolutions = [read_frame(value[3], 'encounter.action-resolution.v1', 15) for value in attacks]
    resumed = one((value for value in resolutions if value[4] == player_attacks[0]), 'Resumed attack has no actual resource witness')
    require(resumed[3] == player and number(resumed[7]) == 2 and number(resumed[8]) > 0
            and number(resumed[1]) > treatment_revision, 'Resumed attack did not damage another actor after care')
    defeat = read_frame(acknowledgment[8], 'encounter.action-resolution.v1', 15)
    require(defeat[5] == player and number(defeat[10]) == 0 and number(defeat[8]) > 0
            and any(value == defeat for value in resolutions), 'Care has no actual retained lethal attack')
    return dict(source=source, playerId=player.hex(), helperId=helper.hex(), wolfId=resumed[5].hex(),
                acknowledgmentRevision=ack_revision, treatmentRevision=treatment_revision,
                resumedAttackRevision=number(resumed[1]), finalRevision=number(final_sequence), finalHash=final_hash.hex(),
                acknowledgmentCommand=acknowledgment[2].hex(), helperCommand=treatment[2].hex(),
                consumptionId=claim[2].hex(), restoredHp=number(treatment[9]), helperDelay=number(treatment[11]),
                earlierPlayerAttacks=len(earlier_attacks),
                retainedCommandReceipts=len(commands), expectedHashes={str(number(row[0])): row[7].hex() for row in rows})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('dotnet', 'godot', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--rendered', action='store_true')
    parser.add_argument('--controls', choices=(*CONTROLS, 'all'), default='wheel')
    parser.add_argument('--pending-save', type=Path,
                        help='Copy an actual production care save with a pending acknowledgment; prove paused reopen then readiness')
    args = parser.parse_args()
    runtime = Path(__file__).resolve().parents[1]
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix='native-care-', dir=output.parent))
    sdk = args.dotnet.resolve().parent
    env = dict(os.environ, DOTNET_ROOT=str(sdk), PATH=str(sdk) + os.pathsep + os.environ.get('PATH', ''))
    client = runtime / 'client'
    project_bytes = (client / 'project.godot').read_bytes()
    require(args.pending_save is None or args.controls == 'wheel', 'Pending continuation has its own fixed three-process sequence')
    controls = ('pending',) if args.pending_save else CONTROLS if args.controls == 'all' else (args.controls,)
    report = dict(status='running', rendered=args.rendered, controls=list(controls), workDirectory=str(work), runs=[], journeys={},
                  originalProjectSha256=sha(project_bytes).hex(),
                  startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope='Injected native controls, actual schema7/profile25 care publication and paused second-process recovery.',
                  limits=dict(nativeProcessSeconds=180, nativeProbeSeconds=150, recordedProcessSeconds=30,
                              maximumNativeProcesses=(3 if args.pending_save else 2) * len(controls),
                              maximumRecordedProcesses=5 * len(controls)),
                  limitations=['Historical roots and gameplay legality use real Host replay; this is not independent full-root or damage-law reconstruction.',
                               'Controlled background rendering/injected input does not establish physical-device, OS-focus, art, performance or full-phase acceptance.'])

    def run(label, command, limit):
        started = time.monotonic()
        item = dict(label=label, command=list(map(str, command)), timeoutSeconds=limit)
        report['runs'].append(item)
        stdout, stderr = work / (label + '.stdout.log'), work / (label + '.stderr.log')
        item.update(stdoutFile=str(stdout), stderrFile=str(stderr))
        try:
            with stdout.open('wb') as out, stderr.open('wb') as err:
                result = subprocess.run(item['command'], cwd=runtime, env=env, stdout=out, stderr=err, timeout=limit)
            item['exitCode'] = result.returncode
        except subprocess.TimeoutExpired:
            item['timedOut'] = True
            raise
        finally:
            item['seconds'] = time.monotonic() - started
        text, errors = stdout.read_text(), stderr.read_text()
        require(item['seconds'] < limit and result.returncode == 0 and 'ERROR:' not in text + errors, 'Process failed at ' + label)
        return text

    try:
        source_paths = [Path(__file__).resolve(), runtime / 'client/scripts/CarePlayProbe.cs',
                        runtime / 'tools/check_construction_physical.py', runtime / 'tools/check_encounter_actions.py',
                        runtime / 'tools/check_live_excavation.py']
        source_paths += [runtime / directory / ('OpenFantasy.' + assembly + '.dll')
                         for directory, assemblies in [
                             ('src/OpenFantasy.Host/bin/Debug/net10.0', ('Host', 'Core', 'Hosting')),
                             ('client/.godot/mono/temp/bin/Debug', ('Client', 'Core', 'Hosting'))]
                         for assembly in assemblies]
        report['sourceSha256'] = {str(path): sha(path.read_bytes()).hex() for path in source_paths}
        launch_client = client
        if args.rendered:
            launch_client = work / 'background-client'
            launch_client.mkdir()
            for path in client.iterdir():
                if path.name != 'project.godot':
                    (launch_client / path.name).symlink_to(path, target_is_directory=path.is_dir())
            config = project_bytes.decode()
            require(config.count('[display]') == 1 and 'window/size/no_focus' not in config, 'Startup no-focus fixture no longer matches project')
            (launch_client / 'project.godot').write_text(config.replace('[display]', '[display]\nwindow/size/no_focus=true', 1))
        for control in controls:
            world, prefix = work / (control + '.sqlite3'), work / (control + '.json')
            if args.pending_save:
                source = args.pending_save.resolve()
                require(source.is_file() and not any(Path(str(source) + suffix).exists() for suffix in ('-wal', '-shm')),
                        'Pending source must be a closed SQLite backup without sidecars')
                report['pendingSource'] = dict(path=str(source), sha256=sha(source.read_bytes()).hex())
                shutil.copyfile(source, world)
            observations = {}
            phases = ('pending', 'resume', 'reopen') if args.pending_save else ('fresh', 'reopen')
            for phase in phases:
                before = sha(world.read_bytes()) if phase in ('pending', 'reopen') else None
                command = [args.godot.resolve()]
                if not args.rendered:
                    command.append('--headless')
                command += ['--path', launch_client, '--', '--care-probe', world, prefix,
                            control if phase == 'fresh' else phase, '--background-render']
                run(control + '-' + phase, command, 180)
                observed = json.loads((work / (control + '.' + ('fresh' if phase == 'resume' else phase) + '.json')).read_text())
                observations[phase] = observed
                require(observed['Kind'] == 'native-care-v1' and observed['Status'] == 'passed'
                        and observed['Mode'] == (control if phase == 'fresh' else phase)
                        and observed['FocusRequests'] == 0 and observed['IsLocallyPaused']
                        and observed['DurationMilliseconds'] < 150000, 'Incomplete native care result')
                if args.rendered:
                    require(observed['Display'] != 'headless' and observed['Unfocusable'] and not observed['NativeFocused']
                            and not observed['ObservedNativeFocus'] and observed['StartupNoFocus'], 'Native focus isolation failed')
                if phase == 'pending':
                    require(sha(world.read_bytes()) == before and observed['InitialHash'] == observed['FinalHash'],
                            'Paused pending reopen changed the exact save or public state')
                if phase == 'resume':
                    require(observed['InitialHash'] == observations['pending']['FinalHash'],
                            'A second process changed pending care before readiness')
                if phase == 'reopen':
                    require(sha(world.read_bytes()) == before and observed['InitialHash'] == observations['resume' if args.pending_save else 'fresh']['FinalHash']
                            and observed['InitialHash'] == observed['FinalHash'], 'Second process changed the exact paused save/public state')
            active = observations['resume' if args.pending_save else 'fresh']
            selected_inputs = [value for value in active['Inputs']
                               if value['Stage'] == ('resume-pending' if args.pending_save else 'acknowledge-' + control)]
            require(selected_inputs, 'No actual care input recorded for requested control')
            input_key = 'PointerControl' if control == 'pointer' else 'Key' if control in ('keyboard', 'pending') else 'Controller'
            require(any(input_key in value for value in selected_inputs), 'Requested input form was not exercised')
            verification = verify_history(world, runtime, pending_continuation=args.pending_save is not None)
            report['journeys'][control] = verification
            frozen = sha(world.read_bytes())
            ack, treated = verification['acknowledgmentRevision'], verification['treatmentRevision']
            revisions = sorted({ack - 1, ack, treated, verification['resumedAttackRevision'], verification['finalRevision']})
            boundaries = {}
            for revision in revisions:
                command = [args.dotnet.resolve(), runtime / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll',
                           '--inspect-recorded-world', world, str(revision)]
                value = json.loads(run(control + '-recorded-' + str(revision), command, 30))
                require(value['ProfileVersion'] == 25 and value['RecordedRevision'] == revision
                        and value['SourceHeadRevision'] == verification['finalRevision']
                        and value['SourceHeadHash'] == verification['finalHash']
                        and value['StateHash'] == verification['expectedHashes'][str(revision)], 'Recorded replay does not match actual journal/source pin')
                boundaries[revision] = value
            before_care, acknowledged, restored = (boundaries[value] for value in (ack - 1, ack, treated))
            def combatant(value, actor):
                return one((row for row in value['EncounterActions']['Combatants'] if row['ActorId'] == actor), 'Missing actual combatant')
            def clock(value):
                life = value['EncounterLifecycle']
                return value['Calendar'], value['AdvancingSteps'], life['ExplorationSteps'], life['TacticalTicks']
            require(clock(before_care) == clock(acknowledged) == clock(restored), 'Acknowledgment/treatment granted world or tactical time')
            require(before_care['EncounterLifecycle']['Phase'] == acknowledged['EncounterLifecycle']['Phase'] == 'Outcome'
                    and restored['EncounterLifecycle']['Phase'] == 'Active', 'Care changed incorrect lifecycle phases')
            require(combatant(before_care, verification['playerId'])['CurrentHp'] == 0
                    and combatant(acknowledged, verification['playerId'])['CurrentHp'] == 0
                    and combatant(restored, verification['playerId'])['CurrentHp'] == 40, 'Acknowledgment restored HP or treatment failed exact restoration')
            require(before_care['EncounterActions']['LastResolution'] == acknowledged['EncounterActions']['LastResolution']
                    == restored['EncounterActions']['LastResolution'], 'Care rewrote original lethal damage')
            debt = one((value for value in restored['EncounterLifecycle']['Debts'] if value['ActorId'] == verification['helperId']), 'Missing helper debt')
            require(debt['RemainingDelay'] == verification['helperDelay'], 'Treatment did not retain actual helper cost')
            positions = lambda value: {row['ActorId']: (row['Position'], row['PresenceRevision']) for row in value['Actors']}
            require(positions(before_care) == positions(acknowledged) == positions(restored), 'Care moved or substituted a body')
            attack = boundaries[verification['resumedAttackRevision']]['EncounterActions']['LastResolution']
            require(attack['ActorId'] == verification['playerId'] and attack['TargetId'] == verification['wolfId']
                    and attack['Status'] == 'Damaged' and attack['Damage'] > 0, 'Recorded resumed attack was not real damage')
            require(sha(world.read_bytes()) == frozen, 'Read-only Host history inspection changed the save')
            verification['recordedBoundariesVerified'] = revisions
            del verification['expectedHashes']
            if args.rendered:
                stages = ('pending', 'pending-before-resume', 'restoration', 'treated', 'reopen') if args.pending_save else (
                    'defeat', 'care-inspection', 'restoration', 'treated', 'reopen')
                expected = {control + '.' + stage + '.png' for stage in stages}
                require({path.name for path in work.glob(control + '.*.png')} == expected, 'Missing rendered care evidence')
            if args.pending_save:
                require(sha(source.read_bytes()).hex() == report['pendingSource']['sha256'], 'Pending-care source backup changed')
        require((client / 'project.godot').read_bytes() == project_bytes, 'Shared project configuration changed')
        require(report['sourceSha256'] == {str(path): sha(path.read_bytes()).hex() for path in source_paths},
                'Verifier, Host or native probe changed during qualification')
        report.update(status='passed')
    except Exception as error:
        report.update(status='failed', failure=str(error), traceback=traceback.format_exc())
    finally:
        report['originalProjectUnchanged'] = (client / 'project.godot').read_bytes() == project_bytes
        if not report['originalProjectUnchanged']:
            report['status'] = 'failed'
        report['artifactSha256'] = {str(path.relative_to(output.parent)): sha(path.read_bytes()).hex()
                                   for path in sorted(work.iterdir()) if path.is_file()}
        report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2) + '\n')
    print('native care: ' + report['status'], flush=True)
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
