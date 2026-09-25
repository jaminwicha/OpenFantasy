"""Independent lifecycle/root reconstruction and real entry/release process-kill qualification."""
import argparse
from contextlib import closing
import json
import os
from pathlib import Path
import queue
import shutil
import sqlite3
import struct
import subprocess
import threading
import time
import traceback
import uuid

from check_encounter_actions import RULES, DOMAIN, components, predict as combat_predict
from check_construction_physical import frame, read_frame
from check_live_excavation import number
from check_liquid_cell_flow import encode_rows, instant, signed, u64
from check_terrain_liquid_breach import advance, DAY
from check_live_encounter_pacing import sha, timing_of, liquid_witness


def lifecycle(root):
    return read_frame(root[21], 'encounter.lifecycle.v1', 17)


def root_of(data):
    root = read_frame(data, 'world.snapshot', 22)
    life = lifecycle(root)
    assert number(root[1]) == 24 and number(life[1]) == 1
    assert number(root[8]) == number(life[5])
    assert number(root[9]) == number(life[6]) + number(life[7])
    pacing = read_frame(root[20], 'world.pacing.v1', 7)
    progress = read_frame(pacing[4], 'exposure.progress.v1', 10)
    assert number(progress[6]) * DAY + number(progress[7]) == number(life[6]) * 1800 + number(life[7]) * 50
    assert bool(pacing[5]) == (number(life[10]) != 1)
    assert len(data) - len(root[18]) - len(root[20]) - len(root[21]) + 1_400_000 + 18*1024 + 512*1024 <= 16*1024*1024
    return root


def debts(life):
    return {row[1]: number(row[2]) for raw in read_frame(life[13], 'encounter.readiness-debts.v1')[1:]
            for row in [read_frame(raw, 'encounter.readiness-debt.v1', 3)]}


def encode_debts(values):
    return frame('encounter.readiness-debts.v1', *(frame('encounter.readiness-debt.v1', actor, u64(values[actor]))
                 for actor in sorted(values)))


def action_root(root, action_bytes):
    result = list(root)
    result[1], result[21] = u64(23), action_bytes
    return result


def predict(before, batch_bytes):
    batch = read_frame(batch_bytes, 'world.step-batch', 10)
    life = lifecycle(before)
    pacing = read_frame(before[20], 'world.pacing.v1', 7)
    assert number(batch[1]) == 24 and batch[3:5] == life[2:4]
    assert batch[5:7] == before[8:10]
    assert read_frame(batch[8], 'world.step-movement')[1:] == []
    commands = read_frame(batch[9], 'world.step-actions')[1:]
    result = list(before)
    kind, phase, sequence = number(batch[7]), number(life[10]), number(life[5]) + 1
    receipts = []
    if kind in (5, 6, 7):
        assert phase == 4
        old_batch = list(batch)
        old_batch[1] = u64(23)
        rejected_for_debt = kind == 6 and len(commands) == 1 and debts(life)[read_frame(commands[0], 'world.actor-action', 10)[5]] > 0
        if rejected_for_debt:
            command = read_frame(commands[0], 'world.actor-action', 10)
            assert number(command[1]) == 11
            authority = read_frame(before[2], 'authority.counted-state', 9)
            controls = {row[3]: row[1:3] for raw in read_frame(authority[5], 'authority.controls')[1:]
                        for row in [read_frame(raw, 'authority.control', 4)]}
            assert command[3:5] == controls[command[5]] and command[6:8] == life[2:4]
            receipts = [frame('world.action-receipt', u64(1), command[2], command[3], *life[2:4],
                              sha(commands[0]), u64(sequence), u64(2), u64(2))]
            result = action_root(before, life[16])
        else:
            predicted, receipts = combat_predict(action_root(before, life[16]), frame('world.step-batch', *old_batch[1:]))
            result = read_frame(predicted, 'world.snapshot', 22)
        life[16] = result[21]
        _, _, clocks, _ = timing_of(result)
        life[13] = encode_debts({actor: debt for actor, _, debt, _ in clocks})
        life[7] = u64(number(life[7]) + (kind == 5))
        _, _, grants, _, people, _ = components(result)
        outcome = len({side for actor, (side, _) in grants.items() if side and number(people[actor][10])}) <= 1
        life[10] = u64(5 if outcome else 4)
    elif kind == 8:
        if not commands:
            assert phase in (2, 3)
            life[10] = u64(phase + 1)
        else:
            assert len(commands) == 1
            encoded = commands[0]
            command = read_frame(encoded, 'world.actor-action', 10)
            assert number(command[1]) == 12 and command[6:8] == life[2:4]
            authority = read_frame(before[2], 'authority.counted-state', 9)
            controls = {row[3]: row[1:3] for raw in read_frame(authority[5], 'authority.controls')[1:]
                        for row in [read_frame(raw, 'authority.control', 4)]}
            assert command[3:5] == controls[command[5]]
            if phase == 1:
                intent = read_frame(command[9], 'world.action.enter-encounter', 6)
                scenarios = {row[1]: row for raw in read_frame(life[12], 'encounter.scenarios.v1')[1:]
                             for row in [read_frame(raw, 'encounter.scenario.v1', 8)]}
                scenario = scenarios[intent[2]]
                assert scenario[4] == command[5] and intent[4] == life[9]
                episode = sha(frame('encounter.episode-identity.v1', *life[2:5], scenario[1],
                                    sha(frame('encounter.scenario.v1', *scenario[1:])), u64(1)))[:16]
                episode = episode[:8] + u64(int.from_bytes(episode[8:], 'big') ^ number(life[9]))
                assert intent[3] == episode
                abilities = frame('encounter.abilities.v1', *read_frame(life[11], 'encounter.lifecycle-abilities.v1')[1:])
                grants = frame('encounter.combatants.v1', *read_frame(scenario[7], 'encounter.scenario-combatants.v1')[1:])
                rules = sha(RULES.encode())
                random = frame('encounter.action-random.v1', u64(1),
                               sha(frame('rng.sha256-counter.key', life[4], rules.hex().encode(), DOMAIN, episode)), u64(0), u64(0))
                life[14:17] = [scenario[1], episode, frame('encounter.actions.v1', u64(1), rules, *life[2:4], episode,
                               life[4], u64(0), abilities, grants, frame('encounter.pending-actions.v1'), random, b'')]
                _, _, sides, _, people, stats = components(action_root(before, life[16]))
                resident_rows = [read_frame(raw, 'world.resident', 13) for raw in read_frame(before[4], 'world.residents')[1:]]
                # Resident controller is field four (Player1, Host2).
                control = {row[1]: number(row[4]) for row in resident_rows}
                clocks = [(actor, stats[actor][5] * 10, debt, control[actor]) for actor, debt in sorted(debts(life).items())]
                assert all(number(people[actor][10]) > 0 for actor, (side, _) in sides.items() if side)
                pacing[5] = frame('encounter.timing.v1', u64(1), *life[2:4], episode, u64(0), u64(0),
                                  frame('encounter.actors.v1', b''.join(struct.pack('>16sQQQ', *row) for row in clocks)),
                                  frame('encounter.decisions.v1', b''))
                progress = read_frame(pacing[4], 'exposure.progress.v1', 10)
                pacing[6] = progress[6] + progress[7]
                life[8:11] = [life[7], u64(number(life[9]) + 1), u64(2)]
            else:
                assert phase == 5
                intent = read_frame(command[9], 'world.action.release-encounter', 4)
                assert intent[2] == life[15] and intent[3] == sha(life[16])
                life[10] = u64(1)
                pacing[5:7] = [b'', b'']
            receipts = [frame('world.action-receipt', u64(1), command[2], command[3], *life[2:4],
                              sha(encoded), u64(sequence), u64(1), u64(0))]
        result[20] = frame('world.pacing.v1', *pacing[1:])
    else:
        assert kind == 1 and phase == 1 and not commands
        life[6] = u64(number(life[6]) + 1)
        life[13] = encode_debts({actor: max(0, debt - 180) for actor, debt in debts(life).items()})
        progress = read_frame(pacing[4], 'exposure.progress.v1', 10)
        total = number(progress[6]) * DAY + number(progress[7]) + 1800
        intervals, carry = divmod(total, 1800)
        progress[6:10] = [u64(total // DAY), u64(total % DAY), u64(intervals), u64(carry)]
        pacing[4] = frame('exposure.progress.v1', *progress[1:])
        result[20] = frame('world.pacing.v1', *pacing[1:])
        day, ms = divmod(instant(before[6], before[7]) + 1800, DAY)
        result[6:8] = [signed(day), u64(ms)]
        result[9] = u64(number(before[9]) + 1)
        residents = []
        for raw in read_frame(before[4], 'world.residents')[1:]:
            row = read_frame(raw, 'world.resident', 13)
            exposure = number(row[10]) * DAY + number(row[11]) + 1800
            row[10:12] = [u64(exposure // DAY), u64(exposure % DAY)]
            residents.append(frame('world.resident', *row[1:]))
        result[4] = frame('world.residents', *residents)
        gathering = read_frame(before[11], 'gather.snapshot', 9)
        gathering[4] = u64(intervals)
        actors = [read_frame(raw, 'gather.actor-work', 4) for raw in read_frame(gathering[8], 'gather.actors')[1:]]
        gathering[8] = frame('gather.actors', *(frame('gather.actor-work', *row[1:3], u64(min(1000, number(row[3]) + 10))) for row in actors))
        result[11] = frame('gather.snapshot', *gathering[1:])
        placement, _, domain, _ = liquid_witness(before)
        cells = read_frame(before[18], 'geography.liquid-cells.v2', 8)
        day, ms = divmod(instant(cells[2], cells[3]) + 1800, DAY)
        result[18] = frame('geography.liquid-cells.v2', u64(intervals), signed(day), u64(ms), *cells[4:7], encode_rows(advance(placement, domain)))
    life[5] = u64(sequence)
    result[1], result[8], result[21] = u64(24), u64(sequence), frame('encounter.lifecycle.v1', *life[1:])
    return frame('world.snapshot', *result[1:]), receipts


def database(path):
    with closing(sqlite3.connect(path.as_uri() + '?mode=ro&immutable=1', uri=True)) as connection:
        assert connection.execute('PRAGMA user_version').fetchone()[0] == 6
        assert connection.execute('PRAGMA quick_check').fetchone()[0] == 'ok'
        assert not connection.execute('PRAGMA foreign_key_check').fetchall()
        return dict(head=connection.execute('SELECT sequence,advancing_steps,state,hash FROM world_head').fetchone(),
                    genesis=connection.execute('SELECT state,hash FROM world_genesis').fetchone(),
                    journal=connection.execute('SELECT sequence,kind,cause_id,advancing_steps,accepted_batch,outcomes,before_hash,after_hash FROM world_commits ORDER BY sequence').fetchall(),
                    identities=connection.execute('SELECT identity,sequence,kind FROM world_occupied_identity ORDER BY identity').fetchall(),
                    commands=connection.execute('SELECT principal,command_id,sequence,command,receipt FROM world_command_outcomes ORDER BY principal,command_id').fetchall(),
                    checkpoints=connection.execute('SELECT sequence,advancing_steps,state,hash FROM world_checkpoints ORDER BY sequence').fetchall())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('dotnet', 'fixture', 'output'):
        parser.add_argument('--' + name, required=True, type=Path)
    args = parser.parse_args()
    fixture, output = args.fixture.resolve(), args.output.resolve()
    runtime = Path(__file__).resolve().parents[1]
    work = output.parent / ('encounter-lifecycle-' + uuid.uuid4().hex)
    assert not work.is_relative_to(fixture) and not output.is_relative_to(fixture)
    work.mkdir(parents=True)
    report = dict(status='running', workDirectory=str(work), runs=[], crashes=[])
    originals = {p.name: sha(p.read_bytes()).hex() for p in fixture.iterdir() if p.is_file()}
    command = [str(args.dotnet.resolve()), str(runtime/'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll')]
    env = dict(os.environ, DOTNET_ROOT=str(args.dotnet.resolve().parent))

    def run(label, *arguments, fail=False):
        row = dict(label=label, arguments=list(map(str, arguments)), timeoutSeconds=30)
        report['runs'].append(row)
        start = time.monotonic()
        done = subprocess.run(command + list(map(str, arguments)), cwd=runtime, env=env, capture_output=True, text=True, timeout=30)
        row.update(seconds=time.monotonic()-start, exitCode=done.returncode, stdout=done.stdout, stderr=done.stderr)
        assert row['seconds'] <= 30 and done.returncode == int(fail) and not done.stderr, row
        if fail:
            assert done.stdout.startswith('Inspection failed:')
            return None
        return json.loads(done.stdout)

    def kill(path, step, boundary):
        process = subprocess.Popen(command + ['--probe-world-step-crash', str(path), str(step), boundary],
                                   cwd=runtime, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        messages = queue.Queue()
        reader = threading.Thread(target=lambda: messages.put(process.stdout.readline()), daemon=True)
        row = dict(boundary=boundary, pid=process.pid, signalTimeoutSeconds=30, killTimeoutSeconds=5)
        report['crashes'].append(row)
        start = time.monotonic()
        reader.start()
        try:
            line = messages.get(timeout=30)
            row.update(signalSeconds=time.monotonic()-start, signal=line)
            assert row['signalSeconds'] <= 30 and json.loads(line) == {'Boundary': 'BeforeCommit' if boundary == 'before' else 'AfterCommit'}
            assert process.poll() is None
            start = time.monotonic()
            process.kill()
            row.update(exitCode=process.wait(timeout=5), killSeconds=time.monotonic()-start, stderr=process.stderr.read())
            assert row['exitCode'] != 0 and row['killSeconds'] <= 5 and not row['stderr']
        finally:
            if process.poll() is None:
                process.kill()
                process.wait(timeout=5)
            reader.join(timeout=1)
            process.stdout.close()
            process.stderr.close()

    try:
        states = {int(p.stem.split('-')[1]): p.read_bytes() for p in fixture.glob('state-*.bin')}
        last = max(states)
        assert set(states) == set(range(last+1)) and 1 <= last <= 1000
        roots, receipts = {0: root_of(states[0])}, {}
        for revision in range(1, last+1):
            predicted, receipts[revision] = predict(roots[revision-1], (fixture/f'step-{revision}.bin').read_bytes())
            assert predicted == states[revision], f'Independent complete root differs at {revision}'
            roots[revision] = root_of(predicted)
        # Independent source pins and actual stats for both instances, not just factory self-report.
        source_bytes = (runtime/'content/legacy/aldermere-first-loop/records.json').read_bytes()
        assert sha(source_bytes).hex() == '9bfc3372954475402ea2fab30dab9de29b2a7c800f2fb61ab654d2efc81e973b'
        records = {row['targetKey']: row for row in json.loads(source_bytes)['records']}
        initial_episode = next(root for root in roots.values() if number(lifecycle(root)[10]) == 2)
        life = lifecycle(initial_episode)
        _, abilities, grants, _, people, stats = components(action_root(initial_episode, life[16]))
        squire = records['openfantasy.aldermere:classes/squire']['sourcePayload']['baseStats']
        monster = records['openfantasy.aldermere:monsters/bramble_wolf']['sourcePayload']['stats']
        stat_keys = ('hp', 'mp', 'attack', 'magic', 'defense', 'speed')
        player = next(actor for actor, (side, _) in grants.items() if side == 1)
        scenarios = [read_frame(raw, 'encounter.scenario.v1', 8) for raw in read_frame(life[12], 'encounter.scenarios.v1')[1:]]
        assert stats[player] == [squire[key] for key in stat_keys]
        assert len(scenarios) == 2
        for scenario in scenarios:
            assert stats[scenario[5]] == [monster[key] for key in stat_keys]
            assert number(people[scenario[5]][10]) == monster['hp']
        for ability in abilities.values():
            key = 'abilities/strike' if ability[2] == b'strike' else 'monsters/bramble_wolf'
            assert ability[4].hex() == records['openfantasy.aldermere:'+key]['sourceIdentity']['payloadSha256']
        for field in (2, 3, 5, 10, 12, 14, 15, 16, 17, 19):
            assert roots[0][field] == roots[last][field]
        journals, identities, commands = [], [], []
        for revision in range(1, last+1):
            step = (fixture/f'step-{revision}.bin').read_bytes()
            batch = read_frame(step, 'world.step-batch', 10)
            seq = u64(revision)
            journals.append((seq, number(batch[7]), batch[2], roots[revision][9], step,
                             frame('world.action-outcomes', *receipts[revision]), sha(states[revision-1]), sha(states[revision])))
            identities.append((batch[2], seq, 1))
            by_command = {read_frame(receipt, 'world.action-receipt')[2]: receipt for receipt in receipts[revision]}
            for encoded in read_frame(batch[9], 'world.step-actions')[1:]:
                action = read_frame(encoded, 'world.actor-action', 10)
                receipt = by_command[action[2]]
                assert read_frame(receipt, 'world.action-receipt')[2] == action[2]
                commands.append((action[3], action[2], seq, encoded, receipt))
                identities.extend([(action[2], seq, 2), (sha(receipt)[:16], seq, 3)])
                if number(action[1]) == 12 and number(lifecycle(roots[revision-1])[10]) == 1:
                    identities.append((read_frame(action[9], 'world.action.enter-encounter', 6)[3], seq, 4))

        def check_database(path, revision):
            db = database(path)
            assert db['head'] == (u64(revision), roots[revision][9], states[revision], sha(states[revision]))
            assert db['genesis'] == (states[0], sha(states[0])) and db['journal'] == journals[:revision]
            assert db['identities'] == sorted(row for row in identities if number(row[1]) <= revision)
            assert db['commands'] == sorted(row for row in commands if number(row[2]) <= revision)
            for seq, steps, state, digest in db['checkpoints']:
                assert state == states[number(seq)] and digest == sha(state) and steps == roots[number(seq)][9]

        def check_report(value, revision):
            root = roots[revision]
            life = lifecycle(root)
            assert value['ProfileVersion'] == 24 and value['StateHash'].lower() == sha(states[revision]).hex()
            assert value.get('Revision', value.get('RecordedRevision')) == revision
            assert value['AdvancingSteps'] == number(root[9])
            assert value['Calendar'] == dict(Days=number(root[6], True), MillisecondsWithinDay=number(root[7]))
            assert value['EncounterLifecycle']['Hash'] == sha(root[21]).hex()
            assert value['EncounterLifecycle']['EncounterCount'] == number(life[9])
            assert value['EncounterLifecycle']['Debts'] == [dict(ActorId=actor.hex(), RemainingDelay=debt) for actor, debt in debts(life).items()]
            assert value['LiquidCells'] == liquid_witness(root)[1]

        check_database(fixture/'world.sqlite', last)
        created = work/'created.sqlite'
        check_report(run('fresh source content', '--create-encounter-journey', created, 'encounter-lifecycle-world', 'Traveler'), 0)
        check_database(created, 0)
        for label in ('entry', 'release'):
            prior_file, step_file = fixture/f'before-{label}.sqlite', fixture/f'{label}-step.bin'
            before = number(database(prior_file)['head'][0])
            after = before+1
            assert step_file.read_bytes() == (fixture/f'step-{after}.bin').read_bytes()
            check_database(prior_file, before)
            for boundary in ('normal', 'before', 'after'):
                path = work/f'{label}-{boundary}.sqlite'
                shutil.copyfile(prior_file, path)
                if boundary == 'normal':
                    check_report(run(label, '--execute-world-step', path, step_file), after)
                else:
                    kill(path, step_file, boundary)
                    retained = before if boundary == 'before' else after
                    check_report(run('recover '+label+' '+boundary, '--inspect-recorded-world', path, retained), retained)
                    check_database(path, retained)
                    if boundary == 'before':
                        check_report(run('retry uncommitted '+label, '--execute-world-step', path, step_file), after)
                check_database(path, after)
                digest = sha(path.read_bytes())
                run('duplicate '+label, '--execute-world-step', path, step_file, fail=True)
                assert sha(path.read_bytes()) == digest
        check_report(run('exact second episode', '--inspect-recorded-world', fixture/'world.sqlite', last), last)
        final = lifecycle(roots[last])
        assert number(final[9]) == 2 and number(final[8]) > 0 and any(debts(final).values())
        assert len(liquid_witness(roots[last])[0]) == 6000
        tampered = work/'tampered.sqlite'
        shutil.copyfile(fixture/'world.sqlite', tampered)
        with closing(sqlite3.connect(tampered)) as connection:
            connection.execute('UPDATE world_commits SET after_hash=? WHERE sequence=?', (bytes(32), u64(1)))
            connection.commit()
        digest = sha(tampered.read_bytes())
        run('changed entry history rejected', '--inspect-recorded-world', tampered, last, fail=True)
        assert sha(tampered.read_bytes()) == digest
        report.update(status='passed', independentCompleteRoots=len(states), journalEntries=last,
                      commandReceipts=len(commands), externalKills=4, preservedWaterUnits=6000,
                      scope='Complete stationary exploration/entry/staging/duel/release/reentry byte and ledger reconstruction.',
                      limitations=['Separate movement/work tests are not reconstructed by this oracle.',
                                   'Initial geometry/source admission uses actual Host creation; this is not a second collision engine.',
                                   'No native choreography, general hazards, death, loot, production scale or full-phase acceptance.'])
    except BaseException as error:
        report.update(status='failed', error=repr(error), traceback=traceback.format_exc())
        raise
    finally:
        report['sourceFixturesUnchanged'] = originals == {p.name: sha(p.read_bytes()).hex() for p in fixture.iterdir() if p.is_file()}
        if not report['sourceFixturesUnchanged']: report['status'] = 'failed'
        output.write_text(json.dumps(report, indent=2)+'\n')
    assert report['sourceFixturesUnchanged']
    print(json.dumps({key: report[key] for key in ('status', 'independentCompleteRoots', 'journalEntries', 'externalKills')}))


if __name__ == '__main__':
    main()
