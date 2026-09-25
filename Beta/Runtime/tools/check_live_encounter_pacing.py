"""Independent complete-root oracle and real SQLite/process-kill qualification for paced encounters."""
import argparse
from contextlib import closing
import hashlib
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
from check_terrain_liquid_breach import advance, witness, DAY


def sha(data):
    return hashlib.sha256(data).digest()


def root_of(data):
    root = read_frame(data, 'world.snapshot', 21)
    assert number(root[1]) == 22
    assert len(data) - len(root[18]) - len(root[20]) + 1_400_000 + 18 * 1024 <= 16 * 1024 * 1024
    return root


def timing_of(root):
    pacing = read_frame(root[20], 'world.pacing.v1', 7)
    timing = read_frame(pacing[5], 'encounter.timing.v1', 9)
    assert number(timing[1]) == 1 and timing[2:4] == pacing[1:3]
    actors = list(struct.iter_unpack('>16sQQQ', read_frame(timing[7], 'encounter.actors.v1', 2)[1]))
    choices = list(struct.iter_unpack('>16s16sQ32s', read_frame(timing[8], 'encounter.decisions.v1', 2)[1]))
    return pacing, timing, actors, choices


def liquid_witness(root):
    # The retained fluid solver owns its complete process boundary, distinct from the partial
    # world calendar. Reuse the independently implemented physical/provenance witness at that boundary.
    pacing = read_frame(root[20], 'world.pacing.v1', 7)
    progress = read_frame(pacing[4], 'exposure.progress.v1', 10)
    origin_day, origin_ms = struct.unpack('>qi', pacing[3])
    expected_day, expected_ms = divmod(origin_day * DAY + origin_ms + number(progress[8]) * 1800, DAY)
    cells = read_frame(root[18], 'geography.liquid-cells.v2', 8)
    assert cells[1:4] == [progress[8], signed(expected_day), u64(expected_ms)]
    physical = list(root)
    physical[6:8] = cells[2:4]
    physical[9] = cells[1]
    return witness(physical)


def predict(before, batch_bytes):
    batch = read_frame(batch_bytes, 'world.step-batch', 10)
    pacing, timing, actors, choices = timing_of(before)
    assert [number(batch[1]), number(batch[5]), number(batch[6])] == [22, number(before[8]), number(before[9])]
    assert batch[3:5] == pacing[1:3]
    assert read_frame(batch[8], 'world.step-movement') == [b'world.step-movement']
    commands = read_frame(batch[9], 'world.step-actions')[1:]
    kind = number(batch[7])
    revision, elapsed = number(timing[5]), number(timing[6])
    selected = {choice[0] for choice in choices}
    due = [actor for actor in actors if actor[2] == 0]
    required = [actor for actor in due if actor[3] == 1 and actor[0] not in selected]
    delta = 0
    receipts = []
    if kind == 6:
        assert commands
        for encoded in commands:
            command = read_frame(encoded, 'world.actor-action', 10)
            request = read_frame(command[9], 'world.action.queue-wait', 4)
            assert number(command[1]) == 10 and number(request[1]) == 1
            assert request[2] == timing[4] and number(request[3]) == revision
            assert command[6:8] == pacing[1:3] and number(command[8]) == 0
            assert command[5] in {actor[0] for actor in due} and command[5] not in selected
            selected.add(command[5])
            choices.append((command[5], command[2], 1, sha(encoded)))
            receipts.append(frame('world.action-receipt', u64(1), command[2], command[3], *pacing[1:3],
                                  sha(encoded), u64(number(before[8]) + 1), u64(1), u64(0)))
        timing[5] = u64(revision + len(commands))
    elif kind == 7:
        assert not commands and not required and due
        chosen = next(choice for choice in choices if choice[0] == due[0][0])
        assert chosen[2] == 1
        actors = [(aid, speed, (10000 + speed - 1) // speed if aid == chosen[0] else debt, control)
                  for aid, speed, debt, control in actors]
        choices.remove(chosen)
        timing[5] = u64(revision + 1)
    else:
        assert kind == 5 and not commands and not due
        delta = 50
        timing[5:7] = [u64(revision + 1), u64(elapsed + 5)]
        actors = [(aid, speed, max(0, debt - 5), control) for aid, speed, debt, control in actors]
    timing[7] = frame('encounter.actors.v1', b''.join(struct.pack('>16sQQQ', *actor) for actor in actors))
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
    result[20] = frame('world.pacing.v1', *pacing[1:])
    day, milliseconds = divmod(instant(before[6], before[7]) + delta, DAY)
    result[6:10] = [signed(day), u64(milliseconds), u64(number(before[8]) + 1), u64(number(before[9]) + (kind == 5))]
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
        result[18] = frame('geography.liquid-cells.v2', u64(intervals), signed(boundary_day), u64(boundary_ms),
                           *cells[4:7], encode_rows(placement))
    return frame('world.snapshot', *result[1:]), sorted(receipts, key=lambda encoded: read_frame(encoded, 'world.action-receipt')[2])


def database(path):
    with closing(sqlite3.connect(path.as_uri() + '?mode=ro&immutable=1', uri=True)) as connection:
        assert connection.execute('PRAGMA user_version').fetchone()[0] == 5
        assert connection.execute('PRAGMA quick_check').fetchone()[0] == 'ok'
        assert not connection.execute('PRAGMA foreign_key_check').fetchall()
        return {
            'head': connection.execute('SELECT sequence,advancing_steps,state,hash FROM world_head').fetchone(),
            'genesis': connection.execute('SELECT state,hash FROM world_genesis').fetchone(),
            'identities': connection.execute('SELECT identity,sequence,kind FROM world_occupied_identity ORDER BY identity').fetchall(),
            'commands': connection.execute('SELECT principal,command_id,sequence,command,receipt FROM world_command_outcomes ORDER BY principal,command_id').fetchall(),
            'journal': connection.execute('SELECT sequence,kind,cause_id,advancing_steps,accepted_batch,outcomes,before_hash,after_hash FROM world_commits ORDER BY sequence').fetchall(),
            'checkpoints': connection.execute('SELECT sequence,advancing_steps,state,hash FROM world_checkpoints ORDER BY sequence').fetchall()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('dotnet', 'fixture', 'output'):
        parser.add_argument('--' + name, required=True, type=Path)
    args = parser.parse_args()
    runtime = Path(__file__).resolve().parents[1]
    fixture, output = args.fixture.resolve(), args.output.resolve()
    work = output.parent / ('live-encounter-pacing-' + uuid.uuid4().hex)
    assert not work.is_relative_to(fixture)
    work.mkdir(parents=True)
    command = [str(args.dotnet.resolve()), str(runtime / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll')]
    env = dict(os.environ, DOTNET_ROOT=str(args.dotnet.resolve().parent))
    report = {'status': 'running', 'workDirectory': str(work), 'runs': [], 'crashes': []}
    originals = {p.name: sha(p.read_bytes()).hex() for p in fixture.iterdir() if p.is_file()}

    def run(name, *arguments, exit_code=0):
        record = {'name': name, 'timeoutSeconds': 30}
        report['runs'].append(record)
        started = time.monotonic()
        result = subprocess.run(command + list(map(str, arguments)), cwd=runtime, env=env,
                                capture_output=True, text=True, timeout=30)
        record.update(exitCode=result.returncode, seconds=time.monotonic() - started,
                      stdout=result.stdout, stderr=result.stderr)
        assert record['seconds'] <= 30 and result.returncode == exit_code and not result.stderr, record
        if exit_code:
            assert result.stdout.startswith('Inspection failed:'), record
            return None
        return json.loads(result.stdout)

    def kill_at_boundary(path, boundary):
        selected = command + ['--probe-world-step-crash', str(path), str(fixture / 'quantum-step.bin'), boundary]
        process = subprocess.Popen(selected, cwd=runtime, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        messages = queue.Queue()
        reader = threading.Thread(target=lambda: messages.put(process.stdout.readline()), daemon=True)
        reader.start()
        record = {'boundary': boundary, 'pid': process.pid, 'signalTimeoutSeconds': 30, 'killTimeoutSeconds': 5}
        report['crashes'].append(record)
        started = time.monotonic()
        try:
            line = messages.get(timeout=30)
            record['signalSeconds'] = time.monotonic() - started
            assert record['signalSeconds'] <= 30
            assert json.loads(line) == {'Boundary': 'BeforeCommit' if boundary == 'before' else 'AfterCommit'}
            assert process.poll() is None
            process.kill()
            record['exitCode'] = process.wait(timeout=5)
            record['stderr'] = process.stderr.read()
            assert record['exitCode'] != 0 and not record['stderr']
        finally:
            if process.poll() is None:
                process.kill()
                process.wait(timeout=5)
            reader.join(timeout=1)
            process.stdout.close()
            process.stderr.close()

    try:
        expected = json.loads((fixture / 'expected.json').read_text())
        states = {int(p.stem.split('-')[1]): p.read_bytes() for p in fixture.glob('state-*.bin')}
        final = max(states)
        assert set(states) == set(range(final + 1))
        receipts = {}
        for revision in range(1, final + 1):
            predicted, outcomes = predict(root_of(states[revision - 1]), (fixture / f'step-{revision}.bin').read_bytes())
            assert predicted == states[revision], f'Independent complete root differs at {revision}'
            receipts[revision] = outcomes
        saved = database(fixture / 'world.sqlite')
        assert saved['head'][2:] == (states[final], sha(states[final]))
        assert saved['genesis'] == (states[0], sha(states[0]))
        identities, commands = [], []
        for index, row in enumerate(saved['journal'], 1):
            sequence, kind, cause, steps, accepted, outcomes, before, after = row
            assert number(sequence) == index and accepted == (fixture / f'step-{index}.bin').read_bytes()
            batch = read_frame(accepted, 'world.step-batch', 10)
            assert kind == number(batch[7]) and cause == batch[2]
            assert number(steps) == number(root_of(states[index])[9])
            assert before == sha(states[index - 1]) and after == sha(states[index])
            assert outcomes == frame('world.action-outcomes', *receipts[index])
            identities.append((cause, sequence, 1))
            by_id = {read_frame(receipt, 'world.action-receipt')[2]: receipt for receipt in receipts[index]}
            for encoded in read_frame(batch[9], 'world.step-actions')[1:]:
                actor = read_frame(encoded, 'world.actor-action', 10)
                receipt = by_id[actor[2]]
                commands.append((actor[3], actor[2], sequence, encoded, receipt))
                identities.extend([(actor[2], sequence, 2), (sha(receipt)[:16], sequence, 3)])
        assert len(saved['journal']) == final and saved['commands'] == sorted(commands) and saved['identities'] == sorted(identities)
        for revision, steps, state, digest in saved['checkpoints']:
            index = number(revision)
            assert state == states[index] and digest == sha(state) and steps == root_of(state)[9]
        before_q, quantum = expected['BeforeQuantumRevision'], expected['QuantumRevision']
        first_placement = liquid_witness(root_of(states[0]))[0]
        assert liquid_witness(root_of(states[before_q]))[0] == first_placement
        assert liquid_witness(root_of(states[quantum]))[0] != first_placement
        assert root_of(states[0])[15] == root_of(states[final])[15] and root_of(states[0])[19] == root_of(states[final])[19]

        def check(value, revision):
            root = root_of(states[revision])
            assert value['ProfileVersion'] == 22 and value['StateHash'].lower() == sha(states[revision]).hex()
            assert value['Pacing']['Hash'] == sha(root[20]).hex()
            assert value['LiquidCells'] == liquid_witness(root)[1]

        created = work / 'created.sqlite'
        check(run('actual complete world creation', '--create-paced-world', created, fixture / 'basis.bin', expected['EncounterId']), 0)
        assert database(created)['genesis'] == saved['genesis']
        unchanged = sha((fixture / 'world.sqlite').read_bytes())
        for revision in sorted({0, 1, before_q, quantum, final}):
            check(run('recorded exact timing boundary', '--inspect-recorded-world', fixture / 'world.sqlite', revision), revision)
        assert sha((fixture / 'world.sqlite').read_bytes()) == unchanged
        for boundary in ('normal', 'before', 'after'):
            path = work / (boundary + '.sqlite')
            shutil.copyfile(fixture / 'before-quantum.sqlite', path)
            if boundary == 'normal':
                check(run('actual earned process interval', '--execute-world-step', path, fixture / 'quantum-step.bin'), quantum)
            else:
                kill_at_boundary(path, boundary)
                retained = before_q if boundary == 'before' else quantum
                check(run('recover external process kill', '--inspect-recorded-world', path, retained), retained)
                assert database(path)['head'][2] == states[retained]
                if boundary == 'before':
                    check(run('retry precommit interval exactly once', '--execute-world-step', path, fixture / 'quantum-step.bin'), quantum)
            assert database(path)['head'][2] == states[quantum]
            before_duplicate = sha(path.read_bytes())
            run('duplicate quantum refused', '--execute-world-step', path, fixture / 'quantum-step.bin', exit_code=1)
            assert sha(path.read_bytes()) == before_duplicate

        tampered = work / 'changed-history.sqlite'
        shutil.copyfile(fixture / 'world.sqlite', tampered)
        with closing(sqlite3.connect(tampered)) as connection:
            # Corrupt historical pacing while leaving current head correct; exact replay must refuse.
            connection.execute('UPDATE world_commits SET after_hash=? WHERE sequence=?', (bytes(32), u64(1)))
            connection.commit()
        run('historical timing tamper refused', '--inspect-recorded-world', tampered, final, exit_code=1)
        for name, digest in originals.items(): assert sha((fixture / name).read_bytes()).hex() == digest, name
        report.update(status='passed', independentCompleteRoots=len(states), journalEntries=final,
                      actualFlowAtQuantum=True, unchangedConstructionAndExcavation=True,
                      sourceFixtureSha256=originals, preservedWorldUnits=6000,
                      scope='Live coordinated timing/process intervals and exact SQLite history, not attacks, release or native combat')
    except BaseException as error:
        report.update(status='failed', error=repr(error), traceback=traceback.format_exc())
        raise
    finally:
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({key: report[key] for key in ('status', 'independentCompleteRoots', 'journalEntries', 'actualFlowAtQuantum')}))


if __name__ == '__main__':
    main()
