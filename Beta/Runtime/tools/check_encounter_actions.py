"""Independent source/stat/RNG/full-root oracle and real commit-kill combat qualification."""
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

from check_construction_physical import frame, read_frame
from check_live_excavation import number
from check_liquid_cell_flow import encode_rows, instant, signed, u64
from check_terrain_liquid_breach import advance, DAY
from check_live_encounter_pacing import sha, timing_of, liquid_witness, database

RULES = ('encounter.physical-actions.v1;damage=max(1,floor(max(1,floor(attack*powerTenths/10)-floor(defense/2))*variation/1000));'
         'variation=900+uniform[0,201);delay=ceil(10000*numerator/(denominator*normalizedSpeed));'
         'invalidated=no-mana-no-draw;hp-owned-by-progression')
DOMAIN = b'encounter.physical-damage.v1'


def root_of(data):
    root = read_frame(data, 'world.snapshot', 22)
    assert number(root[1]) == 23
    assert len(data) - len(root[18]) - len(root[20]) - len(root[21]) + 1_400_000 + 18 * 1024 + 128 * 1024 <= 16 * 1024 * 1024
    return root


def components(root):
    actions = read_frame(root[21], 'encounter.actions.v1', 13)
    assert actions[2] == sha(RULES.encode())
    abilities = {value[1]: value for raw in read_frame(actions[8], 'encounter.abilities.v1')[1:]
                 for value in [read_frame(raw, 'encounter.ability.v1', 12)]}
    grants = {value[1]: (number(value[2]), [value[3][i:i+16] for i in range(0, len(value[3]), 16)])
              for raw in read_frame(actions[9], 'encounter.combatants.v1')[1:]
              for value in [read_frame(raw, 'encounter.combatant.v1', 4)]}
    progression = read_frame(root[13], 'progression.snapshot', 9)
    actors = {value[2]: value for raw in read_frame(progression[7], 'progression.actors')[1:]
              for value in [read_frame(raw, 'progression.actor', 12)]}
    profiles = {value[2]: value for raw in read_frame(progression[5], 'progression.profiles')[1:]
                for value in [read_frame(raw, 'progression.stat-profile', 9)]}
    stats = {}
    for actor, value in actors.items():
        profile = profiles[value[4]]
        assert sha(frame('progression.stat-profile', *profile[1:])) == value[5]
        base = [number(v) for v in read_frame(profile[6], 'progression.stats', 8)[2:]]
        if number(profile[5]) == 1:
            growth = [int.from_bytes(v, 'big', signed=True) for v in read_frame(profile[7], 'progression.modifiers', 8)[2:]]
            heritage = [int.from_bytes(v, 'big', signed=True) for v in read_frame(profile[8], 'progression.modifiers', 8)[2:]]
            base = [max(minimum, b + g * (number(value[8]) - 1) + h)
                    for b, g, h, minimum in zip(base, growth, heritage, (1, 0, 1, 0, 0, 1))]
        stats[actor] = base
    return actions, abilities, grants, progression, actors, stats


def draw(actions):
    random = read_frame(actions[11], 'encounter.action-random.v1', 5)
    key = [actions[6], actions[2].hex().encode(), DOMAIN, actions[5]]
    assert number(random[1]) == 1 and random[2] == sha(frame('rng.sha256-counter.key', *key))
    counter = number(random[3])
    assert number(random[4]) == 0
    while True:
        value = int.from_bytes(sha(frame('rng.sha256-counter.draw', *key, u64(counter)))[:8], 'big')
        if counter == (1 << 64) - 1:
            exhausted = 1
        else:
            counter += 1
            exhausted = 0
        if value >= (1 << 64) % 201:
            return 900 + value % 201, frame('encounter.action-random.v1', *random[1:3], u64(counter), u64(exhausted))
        assert not exhausted


def predict(before, batch_bytes):
    batch = read_frame(batch_bytes, 'world.step-batch', 10)
    pacing, timing, clocks, choices = timing_of(before)
    assert number(batch[1]) == 23 and batch[3:5] == pacing[1:3]
    assert number(batch[5]) == number(before[8]) and number(batch[6]) == number(before[9])
    assert read_frame(batch[8], 'world.step-movement') == [b'world.step-movement']
    kind, sequence = number(batch[7]), number(before[8]) + 1
    actions, abilities, grants, progression, people, stats = components(before)
    assert actions[3:6] == [*pacing[1:3], timing[4]]
    assert set(grants) == set(people) == {actor[0] for actor in clocks}
    active = {actor for actor, (side, _) in grants.items() if side and number(people[actor][10])}
    assert len({grants[actor][0] for actor in active}) > 1
    due = [actor for actor in clocks if actor[0] in active and actor[2] == 0]
    selected = {choice[0] for choice in choices}
    required = [actor for actor in due if actor[3] == 1 and actor[0] not in selected]
    pending = {read_frame(command, 'world.actor-action')[5]: command
               for command in read_frame(actions[10], 'encounter.pending-actions.v1')[1:]}
    commands = read_frame(batch[9], 'world.step-actions')[1:]
    old_pending = len(pending)
    receipts, delta = [], 0
    timing_revision = number(timing[5])
    authority = read_frame(before[2], 'authority.counted-state', 9)
    controls = {row[3]: row[1:3] for raw in read_frame(authority[5], 'authority.controls')[1:]
                for row in [read_frame(raw, 'authority.control', 4)]}
    spatial = read_frame(before[3], 'world.spatial', 8)
    presences = {read_frame(row[2], 'actor.form-binding', 9)[2]: row
                 for raw in read_frame(spatial[7], 'world.presences')[1:]
                 for row in [read_frame(raw, 'world.presence', 9)]}
    if kind == 6:
        assert commands
        for encoded in commands:
            command = read_frame(encoded, 'world.actor-action', 10)
            assert number(command[1]) in (10, 11)
            assert command[3:5] == controls[command[5]] and command[8] == presences[command[5]][7]
            ability_request = number(command[1]) == 11
            intent = read_frame(command[9], 'world.action.queue-ability' if ability_request else 'world.action.queue-wait',
                                6 if ability_request else 4)
            assert number(intent[1]) == 1
            assert command[5] in {actor[0] for actor in due} and command[5] not in selected
            assert intent[2] == timing[4] and number(intent[3]) == timing_revision and command[6:8] == pacing[1:3]
            selected.add(command[5])
            if ability_request:
                assert intent[4] in grants[command[5]][1] and intent[5] in active
                assert grants[command[5]][0] != grants[intent[5]][0]
                assert number(people[command[5]][11]) >= number(abilities[intent[4]][9])
                assert number(people[command[5]][8]) >= number(abilities[intent[4]][10])
                source, target = [presences[actor] for actor in (command[5], intent[5])]
                assert source[6] == target[6]
                dx, dy = [number(target[index], True) - number(source[index], True) for index in (4, 5)]
                assert dx * dx + dy * dy <= number(abilities[intent[4]][6]) ** 2
                pending[command[5]] = encoded
            choices.append((command[5], command[2], 2 if ability_request else 1, sha(encoded)))
            receipts.append(frame('world.action-receipt', u64(1), command[2], command[3], *pacing[1:3],
                                  sha(encoded), u64(sequence), u64(1), u64(0)))
        timing[5] = u64(timing_revision + len(commands))
        if len(pending) != old_pending:
            actions[7] = u64(number(actions[7]) + 1)
    elif kind == 7:
        assert not commands and not required and due
        actor, speed, _, _ = due[0]
        choice = next(value for value in choices if value[0] == actor)
        delay = (10000 + speed - 1) // speed
        target = ability_id = b''
        damage = hp_before = hp_after = mana = variation = 0
        status = 1
        if choice[2] == 2:
            command = read_frame(pending[actor], 'world.actor-action', 10)
            assert command[2] == choice[1] and sha(pending[actor]) == choice[3]
            intent = read_frame(command[9], 'world.action.queue-ability', 6)
            ability_id, target = intent[4:6]
            ability = abilities[ability_id]
            assert target in active and grants[target][0] != grants[actor][0]
            numerator, denominator = 10000 * number(ability[7]), speed * number(ability[8])
            delay = (numerator + denominator - 1) // denominator
            variation, actions[11] = draw(actions)
            base_damage = max(1, stats[actor][2] * number(ability[5]) // 10 - stats[target][4] // 2)
            hp_before = number(people[target][10])
            damage = min(hp_before, max(1, base_damage * variation // 1000))
            hp_after = hp_before - damage
            mana = number(ability[9])
            people[target][3] = u64(number(people[target][3]) + 1)
            people[target][10] = u64(hp_after)
            if mana:
                people[actor][3] = u64(number(people[actor][3]) + 1)
                people[actor][11] = u64(number(people[actor][11]) - mana)
            progression[4] = u64(number(progression[4]) + 1)
            status = 2
        outcome = len({side for aid, (side, _) in grants.items() if side and number(people[aid][10])}) <= 1
        cancelled = sorted(value[1] for value in choices if value[0] != actor
                           and (outcome or number(people[value[0]][10]) == 0))
        clocks = [(aid, s, delay if aid == actor else debt, control) for aid, s, debt, control in clocks]
        choices = [value for value in choices if value[1] != choice[1] and value[1] not in cancelled]
        pending = {aid: cmd for aid, cmd in pending.items()
                   if read_frame(cmd, 'world.actor-action')[2] not in [choice[1]] + cancelled}
        timing[5] = u64(timing_revision + 1)
        actions[7] = u64(number(actions[7]) + 1)
        actions[12] = frame('encounter.action-resolution.v1', u64(sequence), timing[5], actor, choice[1], target, ability_id,
                            u64(status), u64(damage), u64(hp_before), u64(hp_after), u64(mana), u64(delay), u64(variation), b''.join(cancelled))
    else:
        assert kind == 5 and not commands and not due
        delta = 50
        timing[5:7] = [u64(timing_revision + 1), u64(number(timing[6]) + 5)]
        clocks = [(aid, speed, max(0, debt - 5), control) for aid, speed, debt, control in clocks]
    actions[10] = frame('encounter.pending-actions.v1', *(pending[aid] for aid in sorted(pending)))
    progression[7] = frame('progression.actors', *(frame('progression.actor', *people[aid][1:]) for aid in sorted(people)))
    timing[7] = frame('encounter.actors.v1', b''.join(struct.pack('>16sQQQ', *actor) for actor in clocks))
    timing[8] = frame('encounter.decisions.v1', b''.join(struct.pack('>16s16sQ32s', *choice) for choice in sorted(choices)))
    pacing[5] = frame('encounter.timing.v1', *timing[1:])
    progress = read_frame(pacing[4], 'exposure.progress.v1', 10)
    assert number(progress[4]) == 1 and number(progress[5]) == 1800
    total = number(progress[6]) * DAY + number(progress[7]) + delta
    intervals, remainder = divmod(total, 1800)
    old_intervals = number(progress[8])
    progress[6:10] = [u64(total // DAY), u64(total % DAY), u64(intervals), u64(remainder)]
    pacing[4] = frame('exposure.progress.v1', *progress[1:])
    result = list(before)
    result[20:22] = [frame('world.pacing.v1', *pacing[1:]), frame('encounter.actions.v1', *actions[1:])]
    result[13] = frame('progression.snapshot', *progression[1:])
    day, milliseconds = divmod(instant(before[6], before[7]) + delta, DAY)
    result[6:10] = [signed(day), u64(milliseconds), u64(sequence), u64(number(before[9]) + (kind == 5))]
    residents = []
    for encoded in read_frame(before[4], 'world.residents')[1:]:
        resident = read_frame(encoded, 'world.resident', 13)
        personal = number(resident[10]) * DAY + number(resident[11]) + delta
        resident[10:12] = [u64(personal // DAY), u64(personal % DAY)]
        residents.append(frame('world.resident', *resident[1:]))
    result[4] = frame('world.residents', *residents)
    placement, _, domain, _ = liquid_witness(before)
    if intervals != old_intervals:
        assert intervals == old_intervals + 1
        gathering = read_frame(before[11], 'gather.snapshot', 9)
        gathering[4] = u64(intervals)
        effort = []
        for encoded in read_frame(gathering[8], 'gather.actors')[1:]:
            actor = read_frame(encoded, 'gather.actor-work', 4)
            effort.append(frame('gather.actor-work', *actor[1:3], u64(min(1000, number(actor[3]) + 10))))
        gathering[8] = frame('gather.actors', *effort)
        result[11] = frame('gather.snapshot', *gathering[1:])
        placement = advance(placement, domain)
        cells = read_frame(before[18], 'geography.liquid-cells.v2', 8)
        boundary_day, boundary_ms = divmod(instant(cells[2], cells[3]) + 1800, DAY)
        result[18] = frame('geography.liquid-cells.v2', u64(intervals), signed(boundary_day), u64(boundary_ms), *cells[4:7], encode_rows(placement))
    return frame('world.snapshot', *result[1:]), sorted(receipts, key=lambda encoded: read_frame(encoded, 'world.action-receipt')[2])


def action_report(root):
    actions, _, grants, _, people, stats = components(root)
    random = read_frame(actions[11], 'encounter.action-random.v1', 5)
    last = None
    if actions[12]:
        row = read_frame(actions[12], 'encounter.action-resolution.v1', 15)
        last = dict(WorldSequence=number(row[1]), TimingRevision=number(row[2]), ActorId=row[3].hex(),
                    CommandId=row[4].hex(), TargetId=row[5].hex() if row[5] else None,
                    AbilityId=row[6].hex() if row[6] else None,
                    Status={1: 'Wait', 2: 'Damaged', 3: 'Invalidated'}[number(row[7])],
                    Damage=number(row[8]), TargetHpBefore=number(row[9]), TargetHpAfter=number(row[10]),
                    ManaSpent=number(row[11]), Delay=number(row[12]), VariationPermille=number(row[13]),
                    Cancelled=[row[14][i:i+16].hex() for i in range(0, len(row[14]), 16)])
    return dict(Hash=sha(root[21]).hex(), Revision=number(actions[7]), RulesVersion=actions[2].hex(),
                RandomCounter=number(random[3]), RandomExhausted=bool(number(random[4])),
                IsOutcome=len({side for actor, (side, _) in grants.items() if side and number(people[actor][10])}) <= 1,
                Combatants=[dict(ActorId=actor.hex(), Side=side, CurrentHp=number(people[actor][10]),
                                 CurrentMp=number(people[actor][11]), MaxHp=stats[actor][0],
                                 IsIncapacitated=number(people[actor][10]) == 0,
                                 Abilities=[ability.hex() for ability in ability_ids])
                            for actor, (side, ability_ids) in grants.items()], LastResolution=last)


def source_contract(root, runtime, expected):
    packet = runtime / 'content/legacy/aldermere-first-loop'
    raw = (packet / 'records.json').read_bytes()
    assert sha(raw).hex() == '9bfc3372954475402ea2fab30dab9de29b2a7c800f2fb61ab654d2efc81e973b'
    records = {row['targetKey']: row for row in json.loads(raw)['records']}
    payloads = {}
    for key, digest in (
        ('abilities/strike', '974a3c16135eddfc9679deb3929c862476c78b62f38fc79cb7253aba7e02f70a'),
        ('classes/squire', 'b6defa5b6c7a49d13fe3297938c41952c78b22de5ba2187138654d84b2507cf9'),
        ('monsters/bramble_wolf', 'd8639a45b5219163e148fde39537388df45db56148bfcaaa8ebb58ea52e6d967')):
        record = records['openfantasy.aldermere:' + key]
        payload = record['sourcePayload']
        assert record['sourceIdentity']['payloadSha256'] == digest
        assert sha(json.dumps(payload, sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode()).hex() == digest
        payloads[key] = payload
    actions, abilities, grants, progression, people, stats = components(root)
    pacing, timing, clocks, choices = timing_of(root)
    assert number(root[8]) == number(root[9]) == number(actions[7]) == number(timing[5]) == number(timing[6]) == 0
    assert not choices and read_frame(actions[10], 'encounter.pending-actions.v1')[1:] == [] and not actions[12]
    random = read_frame(actions[11], 'encounter.action-random.v1', 5)
    assert number(random[3]) == number(random[4]) == 0
    assert actions[3:6] == [*pacing[1:3], timing[4]]
    spatial = read_frame(root[3], 'world.spatial', 8)
    assert all(read_frame(encoded, 'terrain.region.v1', 7)[2] == actions[6]
               for encoded in read_frame(spatial[5], 'world.regions')[1:])
    assert [value.hex() for value in actions[3:7]] == [expected[key] for key in ('WorldId', 'BranchId', 'EncounterId', 'RngSeed')]
    assert actions[2].hex() == expected['RulesHash']
    by_key = {ability[2].decode(): ability for ability in abilities.values()}
    assert set(by_key) == {'strike', 'bramble_wolf_bite'}
    strike, bite = by_key['strike'], by_key['bramble_wolf_bite']
    assert strike[4].hex() == records['openfantasy.aldermere:abilities/strike']['sourceIdentity']['payloadSha256']
    assert bite[4].hex() == records['openfantasy.aldermere:monsters/bramble_wolf']['sourceIdentity']['payloadSha256']
    assert [number(value) for value in strike[5:]] == [10, 448, 300, 300, 0, 1, 3]
    assert [number(value) for value in bite[5:]] == [10, 352, 400, 300, 0, 1, 11]
    player = next(actor for actor, (side, ids) in grants.items() if side == 1 and ids == [strike[1]])
    wolf = next(actor for actor, (side, ids) in grants.items() if side == 2 and ids == [bite[1]])
    wren = next(actor for actor, (side, ids) in grants.items() if side == 0 and not ids)
    assert set(grants) == {player, wolf, wren} == set(people)
    assert [actor.hex() for actor in (player, wolf, wren, strike[1], bite[1])] == [expected[key] for key in ('Player', 'Wolf', 'Wren', 'Strike', 'Bite')]
    squire = payloads['classes/squire']['baseStats']
    monster = payloads['monsters/bramble_wolf']
    order = ('hp', 'mp', 'attack', 'magic', 'defense', 'speed')
    assert stats[player] == [squire[key] for key in order]
    assert stats[wolf] == [monster['stats'][key] for key in order]
    assert stats[wren] == [1, 0, 0, 0, 0, 1]
    for actor in people:
        assert number(people[actor][3]) == number(people[actor][9]) == 0
        assert [number(people[actor][10]), number(people[actor][11])] == stats[actor][:2]
    assert number(people[player][8]) == 1 and number(people[wolf][8]) == monster['level']
    assert all(speed == stats[actor][5] * 10 and debt == 0 for actor, speed, debt, _ in clocks)
    assert number(progression[4]) == 0
    assert len(liquid_witness(root)[0]) == 6000
    return player, wolf, wren


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('dotnet', 'fixture', 'output'):
        parser.add_argument('--' + name, required=True, type=Path)
    args = parser.parse_args()
    runtime = Path(__file__).resolve().parents[1]
    fixture, output = args.fixture.resolve(), args.output.resolve()
    work = output.parent / ('encounter-actions-' + uuid.uuid4().hex)
    assert not work.is_relative_to(fixture) and not output.is_relative_to(fixture)
    work.mkdir(parents=True)
    command = [str(args.dotnet.resolve()), str(runtime / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll')]
    env = dict(os.environ, DOTNET_ROOT=str(args.dotnet.resolve().parent))
    report = dict(status='running', workDirectory=str(work), runs=[], crashes=[])
    originals = {p.name: sha(p.read_bytes()).hex() for p in fixture.iterdir() if p.is_file()}
    report['sourceFixtureSha256'] = originals

    def run(name, *arguments, exit_code=0):
        record = dict(name=name, arguments=list(map(str, arguments)), timeoutSeconds=30)
        report['runs'].append(record)
        started = time.monotonic()
        try:
            result = subprocess.run(command + list(map(str, arguments)), cwd=runtime, env=env,
                                    capture_output=True, text=True, timeout=30)
        except subprocess.TimeoutExpired as error:
            record.update(seconds=time.monotonic() - started, timedOut=True,
                          stdout=as_text(error.stdout), stderr=as_text(error.stderr))
            raise
        record.update(exitCode=result.returncode, seconds=time.monotonic() - started,
                      stdout=result.stdout, stderr=result.stderr)
        assert record['seconds'] <= 30 and result.returncode == exit_code and not result.stderr, record
        if exit_code:
            assert result.stdout.startswith('Inspection failed:'), record
            return None
        return json.loads(result.stdout)

    def kill_at_boundary(path, step_file, boundary, label):
        process = subprocess.Popen(command + ['--probe-world-step-crash', str(path), str(step_file), boundary],
                                   cwd=runtime, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        messages = queue.Queue()
        reader = threading.Thread(target=lambda: messages.put(process.stdout.readline()), daemon=True)
        record = dict(name=label, boundary=boundary, pid=process.pid, signalTimeoutSeconds=30, killTimeoutSeconds=5)
        report['crashes'].append(record)
        started = time.monotonic()
        reader.start()
        try:
            line = messages.get(timeout=30)
            record.update(signalSeconds=time.monotonic() - started, signal=line)
            assert record['signalSeconds'] <= 30
            assert json.loads(line) == {'Boundary': 'BeforeCommit' if boundary == 'before' else 'AfterCommit'}
            assert process.poll() is None
            killed = time.monotonic()
            process.kill()
            record.update(exitCode=process.wait(timeout=5), killSeconds=time.monotonic() - killed)
            record['stderr'] = process.stderr.read()
            assert record['killSeconds'] <= 5 and record['exitCode'] != 0 and not record['stderr']
        finally:
            if process.poll() is None:
                process.kill()
                process.wait(timeout=5)
            reader.join(timeout=1)
            process.stdout.close()
            process.stderr.close()

    try:
        expected = json.loads((fixture / 'expected.json').read_text())
        assert expected['ProfileVersion'] == 23
        states = {int(p.stem.split('-')[1]): p.read_bytes() for p in fixture.glob('state-*.bin')}
        final = max(states)
        assert 1 <= final <= 1000 and set(states) == set(range(final + 1))
        assert (fixture / expected['GenesisFile']).read_bytes() == states[0]
        assert expected['Revision'] == final and expected['Hash'] == sha(states[final]).hex()
        player, wolf, wren = source_contract(root_of(states[0]), runtime, expected)
        receipts, roots = {}, {0: root_of(states[0])}
        ticks = resolutions = 0
        for revision in range(1, final + 1):
            batch = (fixture / f'step-{revision}.bin').read_bytes()
            predicted, receipts[revision] = predict(roots[revision - 1], batch)
            assert predicted == states[revision], f'Independent complete root differs at {revision}'
            roots[revision] = root_of(predicted)
            kind = number(read_frame(batch, 'world.step-batch', 10)[7])
            ticks += kind == 5
            resolutions += kind == 7
        assert len(expected['States']) == final + 1
        for index, row in enumerate(expected['States']):
            assert row['Revision'] == index and row['Hash'] == sha(states[index]).hex()
            assert row['StateFile'] == f'state-{index}.bin'
            assert row['StepFile'] == (f'step-{index}.bin' if index else None)
        # Exact root reconstruction preserves every field not affected by timing/resources.
        # These explicit assertions make material, body, excavation and currency scope visible.
        for index in (2, 3, 5, 10, 12, 14, 15, 16, 17, 19):
            assert roots[0][index] == roots[final][index], f'Unrelated owner changed: root field {index}'
        first_components, last_components = components(roots[0]), components(roots[final])
        assert first_components[3][8] == last_components[3][8]  # Exact finite currency ledger.
        assert first_components[4][wren] == last_components[4][wren]
        assert sum(number(row[10]) == 0 for row in last_components[4].values()) == 1
        assert action_report(roots[final])['IsOutcome']
        assert not timing_of(roots[final])[3]
        assert read_frame(last_components[0][10], 'encounter.pending-actions.v1')[1:] == []
        assert all(first_components[4][actor][8:10] == last_components[4][actor][8:10] for actor in (player, wolf, wren))

        saved = database(fixture / expected['WorldFile'])
        identities, commands, journals = [], [], []
        for revision in range(1, final + 1):
            accepted = (fixture / f'step-{revision}.bin').read_bytes()
            batch = read_frame(accepted, 'world.step-batch', 10)
            sequence = u64(revision)
            journals.append((sequence, number(batch[7]), batch[2], roots[revision][9], accepted,
                             frame('world.action-outcomes', *receipts[revision]), sha(states[revision - 1]), sha(states[revision])))
            identities.append((batch[2], sequence, 1))
            by_id = {read_frame(receipt, 'world.action-receipt')[2]: receipt for receipt in receipts[revision]}
            for encoded in read_frame(batch[9], 'world.step-actions')[1:]:
                action = read_frame(encoded, 'world.actor-action', 10)
                receipt = by_id[action[2]]
                commands.append((action[3], action[2], sequence, encoded, receipt))
                identities.extend([(action[2], sequence, 2), (sha(receipt)[:16], sequence, 3)])
        assert len({identity[0] for identity in identities}) == len(identities)
        assert len(expected['Commands']) == len(commands)
        exported_commands = []
        for row in expected['Commands']:
            encoded, receipt = bytes.fromhex(row['CommandHex']), bytes.fromhex(row['ReceiptHex'])
            action = read_frame(encoded, 'world.actor-action', 10)
            assert [value.hex() for value in action[2:6]] == [row[key] for key in ('CommandId', 'PrincipalId', 'SessionId', 'ActorId')]
            exported_commands.append((action[3], action[2], u64(row['CommitSequence']), encoded, receipt))
        assert sorted(exported_commands) == sorted(commands)

        def check_database(path, revision):
            actual = database(path)
            assert actual['head'] == (u64(revision), roots[revision][9], states[revision], sha(states[revision]))
            assert actual['genesis'] == (states[0], sha(states[0]))
            assert actual['journal'] == journals[:revision]
            assert actual['commands'] == sorted(row for row in commands if number(row[2]) <= revision)
            assert actual['identities'] == sorted(row for row in identities if number(row[1]) <= revision)
            assert actual['checkpoints'] == [row for row in saved['checkpoints'] if number(row[0]) <= revision]
            for sequence, steps, state, digest in actual['checkpoints']:
                index = number(sequence)
                assert state == states[index] and digest == sha(state) and steps == roots[index][9]
            return actual

        check_database(fixture / expected['WorldFile'], final)
        first, before_first = expected['FirstStrikeRevision'], expected['BeforeStrikeRevision']
        lethal, before_lethal = expected['LethalRevision'], expected['BeforeLethalRevision']
        assert first == before_first + 1 and lethal == before_lethal + 1 == final
        for prefix, revision in (('BeforeStrike', before_first), ('FirstStrike', first), ('BeforeLethal', before_lethal), ('Lethal', lethal)):
            assert expected[prefix + 'Hash'] == sha(states[revision]).hex()
        assert action_report(roots[first])['LastResolution']['Status'] == 'Damaged'
        assert action_report(roots[first])['LastResolution']['TargetHpAfter'] > 0
        assert read_frame(components(roots[before_first])[0][10], 'encounter.pending-actions.v1')[1:]
        assert roots[before_first][6:8] == roots[first][6:8]
        assert action_report(roots[lethal])['LastResolution']['TargetHpAfter'] == 0
        assert roots[before_lethal][6:8] == roots[lethal][6:8]
        assert not action_report(roots[before_lethal])['IsOutcome']

        def check(value, revision, source_head=None):
            root = roots[revision]
            assert value['ProfileVersion'] == 23 and value.get('Revision', value.get('RecordedRevision')) == revision
            assert value['WorldId'] == expected['WorldId'] and value['BranchId'] == expected['BranchId']
            assert value['StateHash'].lower() == sha(states[revision]).hex()
            assert value['AdvancingSteps'] == number(root[9])
            assert value['Calendar'] == dict(Days=number(root[6], True), MillisecondsWithinDay=number(root[7]))
            if 'SourceHeadRevision' in value:
                head = revision if source_head is None else source_head
                assert value['SourceHeadRevision'] == head and value['SourceHeadHash'].lower() == sha(states[head]).hex()
            pacing, timing, clocks, choices = timing_of(root)
            progress = read_frame(pacing[4], 'exposure.progress.v1', 10)
            assert value['Pacing']['Hash'] == sha(root[20]).hex()
            assert value['Pacing']['Calendar'] == value['Calendar']
            assert value['Pacing']['ProcessIntervals'] == number(progress[8])
            assert value['Pacing']['RemainderMilliseconds'] == number(progress[9])
            cells = read_frame(root[18], 'geography.liquid-cells.v2', 8)
            assert value['Pacing']['ProcessBoundary'] == dict(Days=number(cells[2], True), MillisecondsWithinDay=number(cells[3]))
            encounter = value['Pacing']['Encounter']
            assert encounter['Hash'] == sha(pacing[5]).hex() and encounter['Id'] == timing[4].hex()
            assert encounter['Revision'] == number(timing[5]) and encounter['ElapsedTacticalUnits'] == number(timing[6])
            assert encounter['Actors'] == [dict(Id=actor.hex(), NormalizedSpeed=speed, RemainingDelay=debt,
                                               Control={1: 'RequiredHuman', 2: 'Automatic'}[control])
                                           for actor, speed, debt, control in clocks]
            assert encounter['Decisions'] == [dict(ActorId=actor.hex(), CommandId=identity.hex(),
                                                   Kind={1: 'Wait', 2: 'Action'}[kind], CommandHash=pin.hex())
                                              for actor, identity, kind, pin in choices]
            assert value['EncounterActions'] == action_report(root)
            assert value['LiquidCells'] == liquid_witness(root)[1]

        created = work / 'created.sqlite'
        check(run('source-admitted actual complete world', '--create-encounter-world', created,
                  expected['SeedText'], expected['PlayerName']), 0)
        check_database(created, 0)
        for revision in sorted({0, 1, before_first, first, before_lethal, lethal}):
            check(run('recorded exact queued/strike/lethal boundary', '--inspect-recorded-world',
                      fixture / expected['WorldFile'], revision), revision, source_head=final)
        for label, before_revision, after_revision, database_name, step_name in (
            ('first-strike', before_first, first, expected['BeforeStrikeFile'], expected['StrikeStepFile']),
            ('lethal', before_lethal, lethal, expected['BeforeLethalFile'], expected['LethalStepFile'])):
            step_file = fixture / step_name
            assert step_file.read_bytes() == (fixture / f'step-{after_revision}.bin').read_bytes()
            check_database(fixture / database_name, before_revision)
            for boundary in ('normal', 'before', 'after'):
                path = work / (label + '-' + boundary + '.sqlite')
                shutil.copyfile(fixture / database_name, path)
                if boundary == 'normal':
                    check(run(label + ' actual execution', '--execute-world-step', path, step_file), after_revision)
                else:
                    kill_at_boundary(path, step_file, boundary, label)
                    retained = before_revision if boundary == 'before' else after_revision
                    check(run(label + ' recover external ' + boundary + '-commit kill',
                              '--inspect-recorded-world', path, retained), retained)
                    check_database(path, retained)
                    if boundary == 'before':
                        check(run(label + ' precommit retry exactly once', '--execute-world-step', path, step_file), after_revision)
                baseline = check_database(path, after_revision)
                before_duplicate = sha(path.read_bytes())
                run(label + ' duplicate resolution refused', '--execute-world-step', path, step_file, exit_code=1)
                assert sha(path.read_bytes()) == before_duplicate and database(path) == baseline

        tampered = work / 'changed-history.sqlite'
        shutil.copyfile(fixture / expected['WorldFile'], tampered)
        with closing(sqlite3.connect(tampered)) as connection:
            # Head/canonical archive remain valid; full historical validation must catch this.
            connection.execute('UPDATE world_commits SET after_hash=? WHERE sequence=?', (bytes(32), u64(first)))
            connection.commit()
        tampered_bytes = sha(tampered.read_bytes())
        run('historical strike tamper refused', '--inspect-recorded-world', tampered, final, exit_code=1)
        assert sha(tampered.read_bytes()) == tampered_bytes
        report.update(status='passed', independentCompleteRoots=len(states), journalEntries=final,
                      commandReceipts=len(commands), physicalResolutions=resolutions, tacticalTicks=ticks,
                      firstStrikeRevision=first, lethalRevision=lethal, externalKills=4,
                      preservedWaterUnits=6000, sourceBackedStatsAndAbilities=True,
                      unchangedMaterialsBodiesCurrencyAndJobs=True,
                      limitations=['This fixture contains accepted stationary attacks, not invalidated actions or injury transitions.',
                                   'Initial clearance and sightline admission use actual Host creation; this oracle does not implement a second terrain collision engine.',
                                   'Fresh opposed admission only; no played encounter entry/release, corpse, loot, respawn or native presentation.'],
                      scope='Source-admitted stationary physical duel, exact HP/RNG/debt/time/carry/receipts and SQLite recovery; '
                            'HP0 is incapacitation, not death, loot, release, native combat or production scale')
    except BaseException as error:
        report.update(status='failed', error=repr(error), traceback=traceback.format_exc())
        raise
    finally:
        retained = {p.name: sha(p.read_bytes()).hex() for p in fixture.iterdir() if p.is_file()}
        report['sourceFixturesUnchanged'] = retained == originals
        if retained != originals:
            report.update(status='failed', sourceMutation='Fixture file set or bytes changed during qualification')
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(report, indent=2) + '\n')
    assert report['sourceFixturesUnchanged'], report
    print(json.dumps({key: report[key] for key in ('status', 'independentCompleteRoots', 'journalEntries', 'externalKills')}))


def as_text(value):
    return value.decode(errors='replace') if isinstance(value, bytes) else value or ''


if __name__ == '__main__':
    main()
