"""Qualify profile-20 finite liquid flow through real Host commits, history and two external kills.

The independent oracle moves the fixture's 1,350 individual backed units, then reconstructs exact
canonical portions. This does not qualify rendered water, gates, breaches, immersion or scaling.
"""
import argparse
from collections import Counter, defaultdict, deque
from contextlib import closing
import datetime
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

from check_construction_physical import frame, ledger_hash, read_frame
from check_live_excavation import digest, number
from check_liquid_reservoirs import oracle as reservoir_oracle
from check_terrain_materials import members


STEPS = 12
TOTAL_UNITS = 1350
UNIT_VOLUME = 1_000_000
CELL_CAPACITY = 1000
RATE = 200
DAY_MILLISECONDS = 86_400_000
ROW = struct.Struct('>16sqqqQ16sQQ')


def u64(value):
    return value.to_bytes(8, 'big')


def signed(value):
    return value.to_bytes(8, 'big', signed=True)


def instant(days, milliseconds):
    return number(days, True) * DAY_MILLISECONDS + number(milliseconds)


def cell_key(reservoir, x, y, z):
    """Tuple comparison follows reservoir, Z, Y, X, independently of encoded row layout."""
    return reservoir, z, y, x


def portions(placement):
    grouped = defaultdict(list)
    for (reservoir, origin, identity, unit), cell in placement.items():
        assert reservoir == cell[0]
        grouped[(cell, origin, identity)].append(unit)
    result = []
    for (cell, origin, identity), units in sorted(grouped.items()):
        units.sort()
        start = previous = units[0]
        for unit in units[1:]:
            assert unit > previous
            if unit != previous + 1:
                result.append((cell, origin, identity, start, previous + 1))
                start = unit
            previous = unit
        result.append((cell, origin, identity, start, previous + 1))
    return result


def encode_rows(placement):
    return b''.join(ROW.pack(cell[0], cell[3], cell[2], cell[1], origin, identity, start, end)
                    for cell, origin, identity, start, end in portions(placement))


def backing(root):
    """Derive domain and exact unit rights from actual custody/Production, never cell assertions."""
    reservoirs = read_frame(root[16], 'geography.liquid-reservoirs.v1', 4)
    capacity = read_frame(reservoirs[1], 'geography.watershed.runoff-capacity.v1', 8)
    assert capacity[2] == root[2] and capacity[3] == root[12]
    assert capacity[5] == b'' and len(capacity[6]) > 0 and len(capacity[6]) % 16 == 0
    domain = read_frame(capacity[1], 'geography.watershed.runoff-domain.v1', 8)
    material = read_frame(domain[6], 'production.material', 8)
    assert number(material[6]) == UNIT_VOLUME
    owner_for_store, domain_cells = {}, set()
    for geometry in members(reservoirs[2]):
        assert geometry[0] == b'geography.reservoir-geometry.v1' and len(geometry) == 8
        reservoir, store, spill = geometry[4], geometry[5], number(geometry[6], True)
        assert store not in owner_for_store
        owner_for_store[store] = reservoir
        for x, y, bottom in struct.iter_unpack('>qqq', geometry[7]):
            assert 1 <= spill - bottom <= 1024
            domain_cells.update(cell_key(reservoir, x, y, z) for z in range(bottom, spill))
    assert 0 < len(domain_cells) <= 4096
    authority = read_frame(root[2], 'authority.counted-state', 9)
    custody = read_frame(authority[3], 'custody.snapshot.v1', 6)
    rights = set()

    def admit(store, origin, identity, start, end):
        if store not in owner_for_store:
            return
        assert 0 < end - start <= TOTAL_UNITS
        tokens = {(owner_for_store[store], origin, identity, unit) for unit in range(start, end)}
        assert not rights.intersection(tokens)
        rights.update(tokens)
        assert len(rights) <= TOTAL_UNITS

    for encoded in read_frame(custody[5], 'custody.lots.v1')[1:]:
        lot = read_frame(encoded, 'custody.source-lot.v1', 11)
        if lot[2] in owner_for_store:
            assert lot[3:5] == domain[4:6]
        admit(lot[2], 1, lot[5], number(lot[6]), number(lot[7]))
    production = read_frame(root[12], 'production.snapshot', 8)
    for encoded in read_frame(production[7], 'production.lots')[1:]:
        lot = read_frame(encoded, 'production.lot', 10)
        claim = read_frame(lot[6], 'production.output-claim', 6)
        if lot[3] in owner_for_store:
            assert lot[4:6] == domain[4:6]
        admit(lot[3], 2, lot[2], 0, number(claim[5]) - number(claim[4]))
    assert len(rights) == TOTAL_UNITS
    return domain_cells, rights


def placement_witness(root, domain, rights):
    encoded = root[18]
    cells = read_frame(encoded, 'geography.liquid-cells.v1', 7)
    assert len(encoded) <= 1_400_000 and len(cells[6]) % ROW.size == 0
    assert cells[4].hex() == digest(root[16])
    assert number(cells[1]) == number(root[9])
    assert cells[2:4] == root[6:8]
    profile = read_frame(cells[5], 'geography.liquid-flow-profile.v1', 3)
    assert number(profile[1]) == number(profile[2]) == RATE
    placement = {}
    for reservoir, x, y, z, origin, identity, start, end in ROW.iter_unpack(cells[6]):
        cell = cell_key(reservoir, x, y, z)
        assert cell in domain and origin in (1, 2) and 0 < end - start <= TOTAL_UNITS
        for unit in range(start, end):
            token = reservoir, origin, identity, unit
            assert token in rights and token not in placement
            placement[token] = cell
    assert set(placement) == rights
    quantities = Counter(placement.values())
    assert all(quantity <= CELL_CAPACITY for quantity in quantities.values())
    assert encode_rows(placement) == cells[6], 'Portions must already be normalized and canonically ordered.'
    cell_report = [dict(ReservoirId=cell[0].hex(), X=cell[3], Y=cell[2], Z=cell[1],
                        Units=quantities[cell], Microlitres=quantities[cell] * UNIT_VOLUME) for cell in sorted(domain)]
    contents = []
    for reservoir in sorted({cell[0] for cell in domain}):
        owned = [value for value in cell_report if value['ReservoirId'] == reservoir.hex()]
        amount = sum(value['Units'] for value in owned)
        contents.append(dict(ReservoirId=reservoir.hex(), DomainCellCount=len(owned), ActualUnits=amount,
                             ActualMicrolitres=amount * UNIT_VOLUME))
    portion_report = [dict(ReservoirId=cell[0].hex(), X=cell[3], Y=cell[2], Z=cell[1],
                           Origin='RawSource' if origin == 1 else 'ProducedLot', OriginId=identity.hex(),
                           Start=start, End=end, Quantity=end - start)
                      for cell, origin, identity, start, end in portions(placement)]
    diagnostic = dict(PlacementInterpretation='retained-liquid-cells-v1', ContentHash=digest(encoded),
                      ReservoirHash=digest(root[16]), ProfileHash=digest(cells[5]),
                      Boundary=dict(Days=number(cells[2], True), MillisecondsWithinDay=number(cells[3])),
                      Revision=number(cells[1]), DownwardUnitsPerFace=RATE, LateralUnitsPerFace=RATE,
                      UnitsPerCell=CELL_CAPACITY, MicrolitresPerUnit=UNIT_VOLUME,
                      DomainCellCount=len(domain), PortionCount=len(portion_report), ActualUnits=TOTAL_UNITS,
                      ActualMicrolitres=TOTAL_UNITS * UNIT_VOLUME, Contents=contents,
                      Cells=cell_report, Portions=portion_report)
    return placement, diagnostic


def advance_units(placement, domain):
    """Independent unit-token implementation: promise budgets first, then assign original tokens once."""
    starting = Counter(placement.values())
    spent, incoming, transfers = Counter(), Counter(), []

    def propose(donor, receiver, wanted):
        amount = min(wanted, starting[donor] - spent[donor], CELL_CAPACITY - starting[receiver] - incoming[receiver])
        assert amount >= 0
        if amount:
            transfers.append((donor, receiver, amount))
            spent[donor] += amount
            incoming[receiver] += amount

    for cell in sorted(domain):
        reservoir, z, y, x = cell
        below = reservoir, z - 1, y, x
        if below in domain:
            propose(cell, below, RATE)
    faces = []
    for cell in domain:
        reservoir, z, y, x = cell
        for adjacent in ((reservoir, z, y, x + 1), (reservoir, z, y + 1, x)):
            if adjacent in domain:
                faces.append((cell, adjacent))
    assert len(faces) == len(set(faces))
    for first, second in sorted(faces):
        difference = starting[first] - starting[second]
        if difference:
            donor, receiver = (first, second) if difference > 0 else (second, first)
            propose(donor, receiver, min(abs(difference) // 2, RATE))
    original_tokens = defaultdict(deque)
    for token in sorted(placement):
        original_tokens[placement[token]].append(token)
    result = dict(placement)
    moved = set()
    for donor, receiver, amount in transfers:
        for _ in range(amount):
            token = original_tokens[donor].popleft()
            assert token not in moved and placement[token] == donor
            moved.add(token)
            result[token] = receiver
    assert set(result) == set(placement) and all(value <= CELL_CAPACITY for value in Counter(result.values()).values())
    assert Counter(result.values()) == +(starting - spent + incoming)
    return result


def database(path):
    with closing(sqlite3.connect(path.as_uri() + '?mode=ro&immutable=1', uri=True)) as connection:
        assert connection.execute('PRAGMA user_version').fetchone()[0] == 3
        assert connection.execute('PRAGMA quick_check').fetchone()[0] == 'ok'
        assert not connection.execute('PRAGMA foreign_key_check').fetchall()
        head = connection.execute('SELECT sequence,advancing_steps,state,hash FROM world_head').fetchone()
        genesis = connection.execute('SELECT state,hash FROM world_genesis').fetchone()
        assert digest(head[2]) == head[3].hex() and digest(genesis[0]) == genesis[1].hex()
        root = read_frame(head[2], 'world.snapshot', 19)
        assert number(root[1]) == 20 and number(root[8]) == number(head[0]) and number(root[9]) == number(head[1])
        ledgers = dict(
            identities=connection.execute('SELECT identity,sequence,kind FROM world_occupied_identity ORDER BY identity').fetchall(),
            commands=connection.execute('SELECT principal,command_id,sequence,command,receipt FROM world_command_outcomes ORDER BY principal,command_id').fetchall(),
            journal=connection.execute('SELECT sequence,kind,cause_id,advancing_steps,accepted_batch,outcomes,before_hash,after_hash FROM world_commits ORDER BY sequence').fetchall(),
            checkpoints=connection.execute('SELECT sequence,advancing_steps,state,hash FROM world_checkpoints ORDER BY sequence').fetchall())
    return dict(head=head, genesis=genesis, ledgers=ledgers)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('dotnet', 'fixture', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    runtime = Path(__file__).resolve().parents[1]
    dotnet, fixture, output = args.dotnet.resolve(), args.fixture.resolve(), args.output.resolve()
    assert output.parent != fixture and not output.is_relative_to(fixture), 'Output cannot mutate fixture artifacts.'
    output.parent.mkdir(parents=True, exist_ok=True)
    work = output.parent / ('liquid-cell-flow-' + uuid.uuid4().hex)
    work.mkdir()
    command = [str(dotnet), str(runtime / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll')]
    env = dict(os.environ, DOTNET_ROOT=str(dotnet.parent))
    report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  runs=[], crashes=[], workDirectory=str(work), profile=20,
                  scope='Source-backed finite cell flow over twelve ordinary steps; exact history, ledgers and external commit atomicity')
    originals = {p.name: digest(p.read_bytes()) for p in fixture.iterdir() if p.is_file()}
    report['fixtureSha256'] = originals

    def run(name, *arguments, exit_code=0):
        selected = command + [str(value) for value in arguments]
        record = dict(name=name, command=selected, timeoutSeconds=30)
        report['runs'].append(record)
        started = time.monotonic()
        try:
            result = subprocess.run(selected, cwd=runtime, env=env, capture_output=True, text=True, timeout=30)
        except subprocess.TimeoutExpired as error:
            record.update(elapsedSeconds=time.monotonic() - started, timedOut=True,
                          stdout=str(error.stdout or ''), stderr=str(error.stderr or ''))
            raise
        record.update(elapsedSeconds=time.monotonic() - started, exitCode=result.returncode,
                      stdout=result.stdout, stderr=result.stderr)
        assert result.returncode == exit_code and not result.stderr, record
        if exit_code == 0:
            return json.loads(result.stdout)
        assert result.stdout.startswith('Inspection failed:'), record
        return None

    def check_report(value, index):
        assert value['ProfileVersion'] == 20 and value['StateHash'].lower() == digest(states[index])
        assert value['WorldId'] == world.hex() and value['BranchId'] == branch.hex()
        assert value['LiquidCells'] == diagnostics[index]
        assert value['Reservoirs'] is None and value['Transport'] is None
        assert value['ReservoirSeals']['ContentHash'] == digest(roots[index][17])

    def inspect(name, path, index, head_index):
        value = run(name, '--inspect-recorded-world', path, index)
        check_report(value, index)
        assert value['Contract'] == 'recorded-world-state-v1' and value['RecordedRevision'] == index
        assert value['SourceHeadRevision'] == head_index and value['SourceHeadHash'].lower() == digest(states[head_index])
        assert value['AdvancingSteps'] == index and value['HasReadingHold'] is False
        assert value['Calendar'] == diagnostics[index]['Boundary']

    def check_database(path, index):
        saved = database(path)
        assert saved['head'][2] == states[index] and saved['genesis'][0] == states[0]
        ledgers = saved['ledgers']
        assert ledgers['commands'] == []
        assert ledgers['identities'] == sorted((batches[i][2], u64(i + 1), 1) for i in range(index))
        assert len(ledgers['journal']) == index
        for i, row in enumerate(ledgers['journal']):
            assert row == (u64(i + 1), 1, batches[i][2], u64(i + 1), steps[i], frame('world.action-outcomes'),
                           hashlib.sha256(states[i]).digest(), hashlib.sha256(states[i + 1]).digest())
        for revision, advancing, state, state_hash in ledgers['checkpoints']:
            selected = number(revision)
            assert 0 <= selected <= index and number(advancing) == selected
            assert state == states[selected] and state_hash.hex() == digest(state)
        return saved

    def kill_at_boundary(path, boundary):
        selected = command + ['--probe-world-step-crash', str(path), str(fixture / 'step.bin'), boundary]
        process = subprocess.Popen(selected, cwd=runtime, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        messages = queue.Queue()
        reader = threading.Thread(target=lambda: messages.put(process.stdout.readline()), daemon=True)
        reader.start()
        record = dict(command=selected, boundary=boundary, pid=process.pid, signalTimeoutSeconds=30,
                      termination='external process.kill; no graceful Dispose')
        report['crashes'].append(record)
        started = time.monotonic()
        try:
            line = messages.get(timeout=30)
            record['notification'] = line
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
            record['elapsedSeconds'] = time.monotonic() - started

    try:
        expected = json.loads((fixture / 'expected.json').read_text())
        states = [(fixture / f'state-{i}.bin').read_bytes() for i in range(STEPS + 1)]
        steps = [(fixture / f'step-{i}.bin').read_bytes() for i in range(1, STEPS + 1)]
        assert (fixture / 'genesis.bin').read_bytes() == states[0]
        assert (fixture / 'after.bin').read_bytes() == states[1] and (fixture / 'step.bin').read_bytes() == steps[0]
        roots = [read_frame(state, 'world.snapshot', 19) for state in states]
        basis = read_frame((fixture / 'basis.bin').read_bytes(), 'world.snapshot', 18)
        assert number(basis[1]) == 19 and basis[2:5] == roots[0][2:5] and basis[6:] == roots[0][6:18]
        assert (fixture / 'liquid-cells.bin').read_bytes() == roots[0][18]
        admission = frame('world.liquid-cell-admission.v1', basis[5], hashlib.sha256(roots[0][18]).digest())
        assert roots[0][5] == hashlib.sha256(admission).digest()
        spatial = read_frame(roots[0][3], 'world.spatial')
        world, branch = spatial[2:4]
        domain, rights = backing(roots[0])
        report['independentReservoirs'] = reservoir_oracle(roots[0][16])
        placements, diagnostics = [], []
        for i, root in enumerate(roots):
            assert number(root[1]) == 20 and number(root[8]) == number(root[9]) == i
            assert instant(root[6], root[7]) == instant(roots[0][6], roots[0][7]) + i * 1800
            for component in (2, 5, 10, 12, 13, 14, 15, 16, 17):
                assert root[component] == roots[0][component], (i, component)
            assert backing(root) == (domain, rights)
            placement, diagnostic = placement_witness(root, domain, rights)
            placements.append(placement)
            diagnostics.append(diagnostic)
            if i:
                predicted = advance_units(placements[i - 1], domain)
                assert predicted == placement, ('independent unit placements', i)
                previous = read_frame(roots[i - 1][18], 'geography.liquid-cells.v1', 7)
                ticks = instant(previous[2], previous[3]) + 1800
                days, milliseconds = divmod(ticks, DAY_MILLISECONDS)
                encoded = frame('geography.liquid-cells.v1', u64(i), signed(days), u64(milliseconds),
                                previous[4], previous[5], encode_rows(predicted))
                assert root[18] == encoded, ('independent complete cell bytes', i)
        assert placements[0] != placements[1] != placements[2] != placements[3]
        # This fixture begins with an elevated channel front; the lower receiving pool becomes wet later.
        initially_dry = {cell for cell in domain if cell not in Counter(placements[0].values())}
        assert any(cell in initially_dry and cell[1] < max(start[1] for start in placements[0].values())
                   for cell in placements[-1].values())
        history = [{'Revision': i, 'Hash': digest(state)} for i, state in enumerate(states)]
        assert len(expected['History']) == len(history)
        assert {item['Revision']: item['Hash'].lower() for item in expected['History']} == {
            item['Revision']: item['Hash'] for item in history}
        batches = [read_frame(step, 'world.step-batch', 10) for step in steps]
        assert len({batch[2] for batch in batches}) == STEPS
        for i, batch in enumerate(batches):
            assert number(batch[1]) == 20 and batch[3:5] == [world, branch]
            assert number(batch[5]) == number(batch[6]) == i and number(batch[7]) == 1
            assert read_frame(batch[8], 'world.step-movement') == [b'world.step-movement']
            assert read_frame(batch[9], 'world.step-actions') == [b'world.step-actions']
        report['independentStates'] = [dict(Revision=i, StateHash=digest(state), LiquidCells=diagnostics[i])
                                       for i, state in enumerate(states)]
        normal = work / 'normal.sqlite'
        created = run('fresh liquid admission', '--create-liquid-cell-world', normal,
                      fixture / 'basis.bin', fixture / 'liquid-cells.bin')
        check_report(created, 0)
        before_db = check_database(normal, 0)
        normal_before = work / 'before.sqlite'
        shutil.copyfile(normal, normal_before)
        after_first = None
        for i in range(1, STEPS + 1):
            value = run(f'ordinary autonomous flow {i}', '--execute-world-step', normal, fixture / f'step-{i}.bin')
            check_report(value, i)
            current = check_database(normal, i)
            if i == 1:
                after_first = current
        final_db = check_database(normal, STEPS)
        saved_hash = digest(normal.read_bytes())
        for i in reversed(range(STEPS + 1)):
            inspect(f'exact history {i}', normal, i, STEPS)
        for selected in (1, STEPS):
            run(f'duplicate step {selected} refused', '--execute-world-step', normal, fixture / f'step-{selected}.bin', exit_code=1)
            assert check_database(normal, STEPS) == final_db
            assert digest(normal.read_bytes()) == saved_hash
        for boundary in ('before', 'after'):
            path = work / f'{boundary}-kill.sqlite'
            shutil.copyfile(normal_before, path)
            kill_at_boundary(path, boundary)
            selected = 0 if boundary == 'before' else 1
            inspect(f'{boundary} kill recovery', path, selected, selected)
            assert check_database(path, selected) == (before_db if selected == 0 else after_first)
            if selected == 0:
                value = run('retry after precommit kill', '--execute-world-step', path, fixture / 'step.bin')
                check_report(value, 1)
            else:
                unchanged = digest(path.read_bytes())
                run('duplicate after durable kill refused', '--execute-world-step', path, fixture / 'step.bin', exit_code=1)
                assert digest(path.read_bytes()) == unchanged
            assert check_database(path, 1) == after_first
            inspect(f'{boundary} kill exact genesis', path, 0, 1)

        # Preserve every actual unit, cell capacity and canonical row while changing one final location.
        changed = dict(placements[-1])
        quantities = Counter(changed.values())
        for token, donor in sorted(changed.items()):
            destinations = [cell for cell in sorted(domain) if cell[0] == donor[0] and cell != donor
                            and quantities[cell] < CELL_CAPACITY]
            if destinations:
                changed[token] = destinations[0]
                break
        assert changed != placements[-1]
        initial_fields = read_frame(roots[0][18], 'geography.liquid-cells.v1', 7)
        alternative_cells = frame('geography.liquid-cells.v1', *initial_fields[1:6], encode_rows(changed))
        alternative_path = work / 'valid-alternative-cells.bin'
        alternative_path.write_bytes(alternative_cells)
        alternative_db = work / 'valid-alternative.sqlite'
        alternative = run('changed placement is independently admissible', '--create-liquid-cell-world', alternative_db,
                          fixture / 'basis.bin', alternative_path)
        alternate_root = list(roots[0])
        alternate_root[18] = alternative_cells
        alternate_root[5] = hashlib.sha256(frame('world.liquid-cell-admission.v1', basis[5],
                                                hashlib.sha256(alternative_cells).digest())).digest()
        alternate_bytes = frame('world.snapshot', *alternate_root[1:])
        assert alternative['StateHash'] == digest(alternate_bytes)
        assert database(alternative_db)['head'][2] == alternate_bytes
        alternate_placement, alternate_report = placement_witness(alternate_root, domain, rights)
        assert alternate_placement == changed and alternative['LiquidCells'] == alternate_report
        final_fields = read_frame(roots[-1][18], 'geography.liquid-cells.v1', 7)
        modified_root = list(roots[-1])
        modified_root[18] = frame('geography.liquid-cells.v1', *final_fields[1:6], encode_rows(changed))
        assert placement_witness(modified_root, domain, rights)[0] == changed
        modified_state = frame('world.snapshot', *modified_root[1:])
        tampered = work / 'changed-head-placement.sqlite'
        shutil.copyfile(normal, tampered)
        with closing(sqlite3.connect(tampered)) as connection:
            connection.execute('UPDATE world_head SET state=?,hash=?', (modified_state, hashlib.sha256(modified_state).digest()))
            connection.commit()
        tampered_hash = digest(tampered.read_bytes())
        run('canonical placement replacement cannot rewrite history', '--inspect-recorded-world', tampered, STEPS, exit_code=1)
        assert digest(tampered.read_bytes()) == tampered_hash
        report['tamper'] = dict(changedStateHash=digest(modified_state), validAlternativeHash=digest(alternate_bytes),
                                preservedMaterialUnits=TOTAL_UNITS, rejection='history mismatch despite canonical conserved placement')
        run('repeated fresh admission refused', '--create-liquid-cell-world', work / 'repeat.sqlite',
            fixture / 'genesis.bin', fixture / 'liquid-cells.bin', exit_code=1)
        report['ledgerHashes'] = {name: ledger_hash(rows) for name, rows in final_db['ledgers'].items()}
        report['status'] = 'passed'
    except Exception:
        report.update(status='failed', failure=traceback.format_exc())
    finally:
        current_hashes = {p.name: digest(p.read_bytes()) for p in fixture.iterdir() if p.is_file()}
        report['fixturesUnchanged'] = originals == current_hashes
        if not report['fixturesUnchanged']:
            report.update(status='failed', artifactFailure='Fixture artifacts changed during qualification.')
        report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        report['artifactSha256'] = {str(p.relative_to(output.parent)): digest(p.read_bytes()) for p in work.iterdir() if p.is_file()}
        output.write_text(json.dumps(report, indent=2) + '\n')
    print('liquid cell flow: ' + report['status'], flush=True)
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
