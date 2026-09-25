"""Independently qualify an earned profile-21 terrain breach and finite liquid flow.

The oracle follows individual custody-backed units, reconstructs terrain topology and exact
post-breach archives, then checks real Host history, execution, retry and external commit kills.
This finite fixture does not qualify pressure, wet occupants, structural loads or fluid scale.
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
from check_liquid_cell_flow import ROW, cell_key, encode_rows, instant, portions, signed, u64
from check_terrain_materials import members


TOTAL_UNITS = 6000
UNIT_VOLUME = 1_000_000
CELL_CAPACITY = 1000
DOWNWARD = 25
LATERAL = 4
DIVIDER = (116, 329, 0)
DAY = 86_400_000


def sha(data):
    return hashlib.sha256(data).digest()


def root_of(data):
    root = read_frame(data, 'world.snapshot', 20)
    assert number(root[1]) == 21
    assert len(data) - len(root[18]) + 1_400_000 <= 16 * 1024 * 1024
    return root


def authority(root):
    value = read_frame(root[2], 'authority.counted-state', 9)
    custody = read_frame(value[3], 'custody.snapshot.v1', 6)
    lots = [read_frame(row, 'custody.source-lot.v1', 11)
            for row in read_frame(custody[5], 'custody.lots.v1')[1:]]
    return value, custody, lots


def backing(root):
    """Read actual raw/produced custody rights, independently of all quantity assertions."""
    reservoirs = read_frame(root[16], 'geography.liquid-reservoirs.v1', 4)
    capacity = read_frame(reservoirs[1], 'geography.watershed.runoff-capacity.v1', 8)
    assert capacity[2] == root[2] and capacity[3] == root[12]
    assert not capacity[5] and capacity[6] and len(capacity[6]) % 16 == 0
    domain = read_frame(capacity[1], 'geography.watershed.runoff-domain.v1', 8)
    physical = read_frame(domain[6], 'production.material', 8)
    assert number(physical[6]) == UNIT_VOLUME
    geometries = [frame(row[0].decode('ascii'), *row[1:]) for row in members(reservoirs[2])]
    owners, nominal, columns = {}, set(), []
    for encoded in geometries:
        geometry = read_frame(encoded, 'geography.reservoir-geometry.v1', 8)
        reservoir, store, spill = geometry[4], geometry[5], number(geometry[6], True)
        assert store not in owners and spill == 1
        owners[store] = reservoir
        for x, y, bottom in struct.iter_unpack('>qqq', geometry[7]):
            columns.append((reservoir, x, y, bottom, spill))
            nominal.update(cell_key(reservoir, x, y, z) for z in range(bottom, spill))
    assert len(owners) == 1 and len(nominal) == 19
    admitted, _, lots = authority(root)
    sources = {row[1]: row for row in (read_frame(value, 'authority.source', 5)
               for value in read_frame(admitted[7], 'authority.sources')[1:])}
    rights = set()

    def add(store, origin, identity, start, end):
        if store not in owners:
            return
        assert 0 < end - start <= TOTAL_UNITS
        tokens = {(owners[store], origin, identity, unit) for unit in range(start, end)}
        assert not rights.intersection(tokens)
        rights.update(tokens)
        assert len(rights) <= TOTAL_UNITS

    for lot in lots:
        if lot[2] in owners:
            assert lot[3:5] == domain[4:6]
            source = sources[lot[5]]
            assert source[2:4] == lot[3:5] and number(lot[7]) <= number(source[4])
        add(lot[2], 1, lot[5], number(lot[6]), number(lot[7]))
    production = read_frame(root[12], 'production.snapshot', 8)
    for encoded in read_frame(production[7], 'production.lots')[1:]:
        lot = read_frame(encoded, 'production.lot', 10)
        claim = read_frame(lot[6], 'production.output-claim', 6)
        if lot[3] in owners:
            assert lot[4:6] == domain[4:6]
        add(lot[3], 2, lot[2], 0, number(claim[5]) - number(claim[4]))
    assert len(rights) == TOTAL_UNITS
    return nominal, rights, columns, sha(capacity[1]), geometries


def terrain(root):
    spatial = read_frame(root[3], 'world.spatial', 8)
    result = []
    for encoded in read_frame(spatial[5], 'world.regions')[1:]:
        region = read_frame(encoded, 'terrain.region.v1', 7)
        bounds = read_frame(region[4], 'terrain.bounds.v1', 7)
        minimum = tuple(number(value, True) for value in bounds[1:4])
        size = tuple(number(value) for value in bounds[4:7])
        rules = read_frame(region[3], 'terrain.rules.v1', 7)
        assert number(rules[1], True) == 0 and number(rules[2]) == 0, 'Oracle is for the authored flat fixture.'
        assert len(region[6]) % 4 == 0
        indices = [entry[0] for entry in struct.iter_unpack('>I', region[6])]
        assert indices == sorted(set(indices)) and all(index < size[0] * size[1] * size[2] for index in indices)
        edits = {(minimum[0] + index % size[0], minimum[1] + index // size[0] % size[1],
                  minimum[2] + index // (size[0] * size[1])) for index in indices}
        result.append(dict(encoded=encoded, fields=region, minimum=minimum, size=size, rules=rules, edits=edits))
    assert len(result) == 2
    assert len({(entry['fields'][1], entry['fields'][2], entry['fields'][3]) for entry in result}) == 1
    return sorted(result, key=lambda entry: tuple(reversed(entry['minimum'])))


def topology(root, backing_state):
    nominal, _, columns, material_domain, geometries = backing_state
    regions = terrain(root)
    seals = read_frame(root[17], 'geography.reservoir-seal-policy.v1', 3)
    assert len(seals[2]) % 56 == 0
    rules = {seals[2][offset:offset + 48]: number(seals[2][offset + 48:offset + 56])
             for offset in range(0, len(seals[2]), 56)}
    assert rules and all(0 < thickness <= 1000 for thickness in rules.values())

    def known(position):
        selected = [entry for entry in regions if all(low <= value < low + width
                    for low, value, width in zip(entry['minimum'], position, entry['size']))]
        assert len(selected) == 1, ('Missing or overlapping terrain', position)
        entry = selected[0]
        z = position[2]
        solid = z <= 0 and position not in entry['edits']
        material_index = 4 if z == 0 else 5 if z >= -number(entry['rules'][3]) else 6
        pin = read_frame(entry['rules'][material_index], 'terrain.material-pin.v1', 3)
        return solid, b''.join(pin[1:])

    opened = {cell for cell in nominal if not known((cell[3], cell[2], cell[1]))[0]}
    for cell in nominal:
        reservoir, z, y, x = cell
        solid, material = known((x, y, z))
        if solid:
            assert material in rules
        for dx, dy, dz in ((-1, 0, 0), (1, 0, 0), (0, -1, 0), (0, 1, 0), (0, 0, -1)):
            if cell_key(reservoir, x + dx, y + dy, z + dz) not in nominal:
                boundary_solid, boundary_material = known((x + dx, y + dy, z + dz))
                assert boundary_solid and boundary_material in rules
    for reservoir, x, y, bottom, spill in columns:
        for z in range(bottom, spill):
            if cell_key(reservoir, x, y, z) in opened:
                assert all(not known((x, y, above))[0] for above in range(z + 1, spill + 1))

    def cells_bytes(cells):
        return b''.join(struct.pack('>16sqqq', value[0], value[3], value[2], value[1]) for value in sorted(cells))

    encoded = frame('geography.terrain-liquid-topology.v1', material_domain,
                    frame('geography.terrain-liquid-geometries.v1', *(sha(value) for value in geometries)),
                    sha(root[17]), frame('geography.terrain-liquid-regions.v1', *(sha(entry['encoded']) for entry in regions)),
                    cells_bytes(nominal), cells_bytes(opened))
    return opened, sha(encoded)


def witness(root):
    backing_state = backing(root)
    domain, topology_pin = topology(root, backing_state)
    rights = backing_state[1]
    cells = read_frame(root[18], 'geography.liquid-cells.v2', 8)
    assert len(root[18]) <= 1_400_000 and len(cells[7]) % ROW.size == 0
    assert cells[4] == sha(root[16]) and cells[6] == topology_pin
    assert cells[2:4] == root[6:8] and number(cells[1]) == number(root[9])
    profile = read_frame(cells[5], 'geography.liquid-flow-profile.v1', 3)
    assert [number(value) for value in profile[1:]] == [DOWNWARD, LATERAL]
    placement = {}
    for reservoir, x, y, z, origin, identity, start, end in ROW.iter_unpack(cells[7]):
        cell = cell_key(reservoir, x, y, z)
        assert cell in domain and origin in (1, 2) and 0 < end - start <= TOTAL_UNITS
        for unit in range(start, end):
            token = reservoir, origin, identity, unit
            assert token in rights and token not in placement
            placement[token] = cell
    assert set(placement) == rights and encode_rows(placement) == cells[7]
    quantities = Counter(placement.values())
    assert all(amount <= CELL_CAPACITY for amount in quantities.values())
    rows = [dict(ReservoirId=cell[0].hex(), X=cell[3], Y=cell[2], Z=cell[1], Units=quantities[cell],
                 Microlitres=quantities[cell] * UNIT_VOLUME) for cell in sorted(domain)]
    owner = next(iter(domain))[0].hex()
    pieces = [dict(ReservoirId=cell[0].hex(), X=cell[3], Y=cell[2], Z=cell[1],
                   Origin='RawSource' if origin == 1 else 'ProducedLot', OriginId=identity.hex(),
                   Start=start, End=end, Quantity=end - start)
              for cell, origin, identity, start, end in portions(placement)]
    diagnostic = dict(PlacementInterpretation='retained-terrain-liquid-cells-v2', TopologyHash=topology_pin.hex(),
                      OpenCapacityMicrolitres=len(domain) * CELL_CAPACITY * UNIT_VOLUME,
                      ContentHash=digest(root[18]), ReservoirHash=digest(root[16]), ProfileHash=digest(cells[5]),
                      Boundary=dict(Days=number(cells[2], True), MillisecondsWithinDay=number(cells[3])),
                      Revision=number(cells[1]), DownwardUnitsPerFace=DOWNWARD, LateralUnitsPerFace=LATERAL,
                      UnitsPerCell=CELL_CAPACITY, MicrolitresPerUnit=UNIT_VOLUME, DomainCellCount=len(domain),
                      PortionCount=len(pieces), ActualUnits=TOTAL_UNITS, ActualMicrolitres=TOTAL_UNITS * UNIT_VOLUME,
                      Contents=[dict(ReservoirId=owner, DomainCellCount=len(domain), ActualUnits=TOTAL_UNITS,
                                     ActualMicrolitres=TOTAL_UNITS * UNIT_VOLUME)], Cells=rows, Portions=pieces)
    return placement, diagnostic, domain, rights


def advance(placement, domain):
    """Integer-token oracle: all promises share starting stock and starting free-space budgets."""
    starting = Counter(placement.values())
    spent, incoming, deliveries = Counter(), Counter(), []

    def propose(donor, receiver, wanted):
        quantity = min(wanted, starting[donor] - spent[donor], CELL_CAPACITY - starting[receiver] - incoming[receiver])
        assert quantity >= 0
        if quantity:
            deliveries.append((donor, receiver, quantity))
            spent[donor] += quantity
            incoming[receiver] += quantity

    for cell in sorted(domain):
        reservoir, z, y, x = cell
        below = reservoir, z - 1, y, x
        if below in domain:
            propose(cell, below, DOWNWARD)
    faces = sorted((cell, neighbor) for cell in domain
                   for neighbor in ((cell[0], cell[1], cell[2], cell[3] + 1),
                                    (cell[0], cell[1], cell[2] + 1, cell[3])) if neighbor in domain)
    assert len(faces) == len(set(faces))
    for first, second in faces:
        difference = starting[first] - starting[second]
        if difference:
            donor, receiver = (first, second) if difference > 0 else (second, first)
            propose(donor, receiver, min(abs(difference) // 2, LATERAL))
    original = defaultdict(deque)
    for token in sorted(placement):
        original[placement[token]].append(token)
    result, moved = dict(placement), set()
    for donor, receiver, amount in deliveries:
        for _ in range(amount):
            token = original[donor].popleft()
            assert token not in moved
            moved.add(token)
            result[token] = receiver
    assert set(result) == set(placement) and Counter(result.values()) == +(starting - spent + incoming)
    return result


def next_idle(root, placement):
    """Reconstruct a post-breach empty ordinary commit, including personal time and effort."""
    result = list(root)
    days, milliseconds = divmod(instant(root[6], root[7]) + 1800, DAY)
    result[6:10] = [signed(days), u64(milliseconds), u64(number(root[8]) + 1), u64(number(root[9]) + 1)]
    residents = []
    for encoded in read_frame(root[4], 'world.residents')[1:]:
        resident = read_frame(encoded, 'world.resident', 13)
        personal_days, personal_ms = divmod(number(resident[10]) * DAY + number(resident[11]) + 1800, DAY)
        resident[10:12] = [u64(personal_days), u64(personal_ms)]
        residents.append(frame('world.resident', *resident[1:]))
    result[4] = frame('world.residents', *residents)
    gathering = read_frame(root[11], 'gather.snapshot', 9)
    gathering[4] = result[9]
    actors = []
    for encoded in read_frame(gathering[8], 'gather.actors')[1:]:
        actor = read_frame(encoded, 'gather.actor-work', 4)
        actors.append(frame('gather.actor-work', *actor[1:3], u64(min(1000, number(actor[3]) + 10))))
    gathering[8] = frame('gather.actors', *actors)
    result[11] = frame('gather.snapshot', *gathering[1:])
    cells = read_frame(root[18], 'geography.liquid-cells.v2', 8)
    result[18] = frame('geography.liquid-cells.v2', u64(number(cells[1]) + 1), result[6], result[7],
                       *cells[4:7], encode_rows(placement))
    return result


def database(path):
    with closing(sqlite3.connect(path.as_uri() + '?mode=ro&immutable=1', uri=True)) as connection:
        assert connection.execute('PRAGMA user_version').fetchone()[0] == 3
        assert connection.execute('PRAGMA quick_check').fetchone()[0] == 'ok'
        assert not connection.execute('PRAGMA foreign_key_check').fetchall()
        head = connection.execute('SELECT sequence,advancing_steps,state,hash FROM world_head').fetchone()
        genesis = connection.execute('SELECT state,hash FROM world_genesis').fetchone()
        assert digest(head[2]) == head[3].hex() and digest(genesis[0]) == genesis[1].hex()
        root = root_of(head[2])
        assert number(root[8]) == number(head[0]) and number(root[9]) == number(head[1])
        ledgers = dict(
            identities=connection.execute('SELECT identity,sequence,kind FROM world_occupied_identity ORDER BY identity').fetchall(),
            commands=connection.execute('SELECT principal,command_id,sequence,command,receipt FROM world_command_outcomes ORDER BY principal,command_id').fetchall(),
            journal=connection.execute('SELECT sequence,kind,cause_id,advancing_steps,accepted_batch,outcomes,before_hash,after_hash FROM world_commits ORDER BY sequence').fetchall(),
            checkpoints=connection.execute('SELECT sequence,advancing_steps,state,hash FROM world_checkpoints ORDER BY sequence').fetchall())
    return dict(head=head, genesis=genesis, ledgers=ledgers)


def verify_ledgers(saved, hashes, world, branch, breach):
    identities, commands, work_revisions = [], [], []
    for revision, row in enumerate(saved['ledgers']['journal'], 1):
        sequence, kind, cause, steps, encoded, outcomes, before_hash, after_hash = row
        batch = read_frame(encoded, 'world.step-batch', 10)
        assert number(sequence) == number(steps) == revision and kind == 1
        assert [number(batch[1]), number(batch[5]), number(batch[6]), number(batch[7])] == [21, revision - 1, revision - 1, 1]
        assert batch[2] == cause and batch[3:5] == [world, branch]
        assert before_hash.hex() == hashes[revision - 1] and after_hash.hex() == hashes[revision]
        identities.append((cause, sequence, 1))
        selected = read_frame(batch[9], 'world.step-actions')[1:]
        receipts = read_frame(outcomes, 'world.action-outcomes')[1:]
        assert len(selected) == len(receipts) <= 1
        if revision > breach:
            assert not selected and read_frame(batch[8], 'world.step-movement') == [b'world.step-movement']
        for command, receipt in zip(selected, receipts):
            action = read_frame(command, 'world.actor-action', 10)
            intent = read_frame(action[9], 'world.action.excavate', 8)
            assert number(action[1]) == 8 and action[6:8] == [world, branch]
            assert number(intent[1]) == 1 and number(intent[3]) == len(work_revisions)
            predicted = frame('world.action-receipt', u64(1), action[2], action[3], world, branch,
                              sha(command), sequence, u64(1), u64(0))
            assert receipt == predicted
            commands.append((action[3], action[2], sequence, command, receipt))
            identities.extend([(action[2], sequence, 2), (sha(receipt)[:16], sequence, 3)])
            work_revisions.append(revision)
            if revision == breach:
                identities.append((sha(frame('entity.spawned', action[2], u64(0)))[:16], sequence, 4))
    assert len(saved['ledgers']['journal']) == number(saved['head'][0])
    assert saved['ledgers']['commands'] == sorted(commands)
    assert saved['ledgers']['identities'] == sorted(identities)
    assert len(set(identity[0] for identity in identities)) == len(identities)
    for sequence, steps, state, state_hash in saved['ledgers']['checkpoints']:
        revision = number(sequence)
        assert number(steps) == revision and digest(state) == state_hash.hex() == hashes[revision]
        assert number(root_of(state)[8]) == revision
    return work_revisions


def verify_extraction(before, after, batch_bytes):
    old_site = read_frame(read_frame(before[19], 'terrain.excavation.v2', 2)[1], 'terrain.excavation-site.v2', 13)
    new_site = read_frame(read_frame(after[19], 'terrain.excavation.v2', 2)[1], 'terrain.excavation-site.v2', 13)
    assert old_site[:10] == new_site[:10] and old_site[11:] == new_site[11:]
    assert struct.unpack('>qqq', old_site[2]) == DIVIDER
    assert [number(old_site[9]), number(old_site[10]), number(new_site[10]), number(old_site[12])] == [3, 2, 3, 2]
    before_regions, after_regions = terrain(before), terrain(after)
    differences = []
    for old, new in zip(before_regions, after_regions):
        assert old['fields'][:5] == new['fields'][:5]
        if old['edits'] != new['edits']:
            assert new['edits'] == old['edits'] | {DIVIDER} and DIVIDER not in old['edits']
            assert number(new['fields'][5]) == number(old['fields'][5]) + 1
            differences.append(DIVIDER)
        else:
            assert old['encoded'] == new['encoded']
    assert differences == [DIVIDER]
    old_spatial, new_spatial = [read_frame(root[3], 'world.spatial', 8) for root in (before, after)]
    assert old_spatial[:5] == new_spatial[:5] and old_spatial[6:] == new_spatial[6:]
    old_authority, old_custody, old_lots = authority(before)
    new_authority, new_custody, new_lots = authority(after)
    assert old_authority[:3] == new_authority[:3] and old_authority[4:] == new_authority[4:]
    assert number(new_custody[3]) == number(old_custody[3]) + 1
    old_by_id = {lot[1]: lot for lot in old_lots}
    assert all(lot == next(value for value in new_lots if value[1] == identity) for identity, lot in old_by_id.items())
    created = [lot for lot in new_lots if lot[1] not in old_by_id]
    assert len(created) == 1 and not any(lot[5] == old_site[4] for lot in old_lots)
    batch = read_frame(batch_bytes, 'world.step-batch', 10)
    actions = read_frame(batch[9], 'world.step-actions', 2)
    action = read_frame(actions[1], 'world.actor-action', 10)
    assert action[5] == old_site[5]
    created_lot = created[0]
    residents = [read_frame(value, 'world.resident', 13) for value in read_frame(before[4], 'world.residents')[1:]]
    bag = next(resident[2] for resident in residents if resident[1] == action[5])
    material = read_frame(old_site[7], 'production.material', 8)
    quantity = 1_000_000_000 // number(material[6])
    assert quantity * number(material[6]) == 1_000_000_000
    assert created_lot[1] == sha(frame('entity.spawned', action[2], u64(0)))[:16]
    assert created_lot[2] == bag and b''.join(created_lot[3:5]) == old_site[6]
    assert created_lot[5] == old_site[4] and [number(value) for value in created_lot[6:10]] == [0, quantity, 0, 0]
    assert created_lot[10] == b''
    old_stores = read_frame(old_custody[4], 'custody.storages.v1')[1:]
    new_stores = read_frame(new_custody[4], 'custody.storages.v1')[1:]
    assert len(old_stores) == len(new_stores)
    for encoded_old, encoded_new in zip(old_stores, new_stores):
        old, new = [read_frame(value, 'custody.storage.v1', 4) for value in (encoded_old, encoded_new)]
        assert old[:3] == new[:3]
        assert number(new[3]) == number(old[3]) + (1 if old[1] == bag else 0)
    assert instant(after[6], after[7]) == instant(before[6], before[7]) + 1800
    for component in (5, 10, 12, 13, 14, 15, 17):
        assert before[component] == after[component]
    predicted = next_idle(before, {})
    assert after[4] == predicted[4] and after[6:10] == predicted[6:10]
    previous_gathering = read_frame(before[11], 'gather.snapshot', 9)
    final_gathering = read_frame(after[11], 'gather.snapshot', 9)
    assert previous_gathering[:4] == final_gathering[:4] and previous_gathering[5:8] == final_gathering[5:8]
    assert number(final_gathering[4]) == number(previous_gathering[4]) + 1
    for old, new in zip(read_frame(previous_gathering[8], 'gather.actors')[1:], read_frame(final_gathering[8], 'gather.actors')[1:]):
        old, new = read_frame(old, 'gather.actor-work', 4), read_frame(new, 'gather.actor-work', 4)
        assert old[:3] == new[:3]
        effort = number(old[3]) - (400 if old[2] == action[5] else 0)
        assert 0 <= effort and number(new[3]) == min(1000, effort + 10)
    return dict(CommandId=action[2].hex(), SourceLotId=created_lot[1].hex(), SourceId=old_site[4].hex(),
                SiteId=old_site[1].hex(), Quantity=quantity, MassMilligrams=quantity * number(material[5]))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('dotnet', 'fixture', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    runtime = Path(__file__).resolve().parents[1]
    dotnet, fixture, output = args.dotnet.resolve(), args.fixture.resolve(), args.output.resolve()
    assert not output.is_relative_to(fixture), 'Output cannot mutate fixture artifacts.'
    output.parent.mkdir(parents=True, exist_ok=True)
    work = output.parent / ('terrain-liquid-breach-' + uuid.uuid4().hex)
    assert not work.is_relative_to(fixture)
    work.mkdir()
    command = [str(dotnet), str(runtime / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll')]
    env = dict(os.environ, DOTNET_ROOT=str(dotnet.parent))
    report = dict(status='running', profile=21, startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  workDirectory=str(work), runs=[], crashes=[],
                  scope='Finite earned breach, exact token flow and post-breach history, preserved ledgers and external commit atomicity')
    originals = {path.name: digest(path.read_bytes()) for path in fixture.iterdir() if path.is_file()}
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

    def inspect(name, path, revision, head):
        value = run(name, '--inspect-recorded-world', path, revision)
        check_report(value, revision)
        assert value['Contract'] == 'recorded-world-state-v1' and value['RecordedRevision'] == revision
        assert value['SourceHeadRevision'] == head and value['SourceHeadHash'].lower() == hashes[head]
        assert value['AdvancingSteps'] == revision and value['HasReadingHold'] is False
        assert value['Calendar'] == diagnostics[revision]['Boundary']

    def check_report(value, revision):
        assert value['ProfileVersion'] == 21 and value['StateHash'].lower() == hashes[revision]
        assert value['WorldId'] == world.hex() and value['BranchId'] == branch.hex()
        assert value['LiquidCells'] == diagnostics[revision]
        assert value['Reservoirs'] is None and value['Transport'] is None
        assert value['ReservoirSeals']['ContentHash'] == digest(roots[revision][17])

    def kill_at_boundary(path, boundary):
        selected = command + ['--probe-world-step-crash', str(path), str(fixture / 'breach-step.bin'), boundary]
        process = subprocess.Popen(selected, cwd=runtime, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        messages = queue.Queue()
        reader = threading.Thread(target=lambda: messages.put(process.stdout.readline()), daemon=True)
        reader.start()
        record = dict(command=selected, boundary=boundary, pid=process.pid, signalTimeoutSeconds=30,
                      killTimeoutSeconds=5, termination='external process.kill; no graceful Dispose')
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
        before_revision, breach, final = [expected[key] for key in ('BeforeBreachRevision', 'BreachRevision', 'FinalRevision')]
        assert expected['ProfileVersion'] == 21 and (before_revision, breach, final) == (164, 165, 175)
        hashes = {entry['Revision']: entry['Hash'].lower() for entry in expected['States']}
        assert len(hashes) == len(expected['States']) == final + 1 and sorted(hashes) == list(range(final + 1))
        states = {int(path.stem.split('-')[1]): path.read_bytes() for path in fixture.glob('state-*.bin')}
        assert set(states) == {0, expected['PartialRevision'], before_revision, breach, final}
        roots = {revision: root_of(state) for revision, state in states.items()}
        spatial = read_frame(roots[0][3], 'world.spatial', 8)
        world, branch = spatial[2:4]
        first_placement, _, initial_domain, rights = witness(roots[0])
        first_quantities = Counter(first_placement.values())
        assert {(cell[3], cell[2], cell[1]): amount for cell, amount in first_quantities.items()} == {
            (x, 329, 0): 1000 for x in range(110, 116)}
        assert advance(first_placement, initial_domain) == first_placement
        diagnostics, placements = {}, {}
        for revision, root in roots.items():
            assert digest(states[revision]) == hashes[revision] and number(root[8]) == number(root[9]) == revision
            assert instant(root[6], root[7]) == instant(roots[0][6], roots[0][7]) + revision * 1800
            placement, diagnostic, domain, actual_rights = witness(root)
            assert actual_rights == rights
            placements[revision], diagnostics[revision] = placement, diagnostic
            if revision < breach:
                assert domain == initial_domain and placement == first_placement
        opened_domain = witness(roots[breach])[2]
        reservoir = next(iter(initial_domain))[0]
        assert opened_domain == initial_domain | {cell_key(reservoir, *DIVIDER)}
        assert placements[breach] == advance(first_placement, opened_domain)
        assert Counter(placements[breach].values())[cell_key(reservoir, 116, 329, 0)] == 4
        assert Counter(placements[breach].values())[cell_key(reservoir, 117, 329, 0)] == 0
        step = (fixture / 'breach-step.bin').read_bytes()
        report['extraction'] = verify_extraction(roots[before_revision], roots[breach], step)
        for revision in range(breach + 1, final + 1):
            placement = advance(placements[revision - 1], opened_domain)
            candidate = next_idle(roots[revision - 1], placement)
            encoded = frame('world.snapshot', *candidate[1:])
            assert digest(encoded) == hashes[revision], ('Independent complete post-breach root', revision)
            if revision in states:
                assert states[revision] == encoded
            states[revision], roots[revision] = encoded, candidate
            actual, diagnostic, _, actual_rights = witness(candidate)
            assert actual == placement and actual_rights == rights
            placements[revision], diagnostics[revision] = placement, diagnostic
        assert Counter(placements[breach + 1].values())[cell_key(reservoir, 117, 329, -1)] == 0
        assert Counter(placements[breach + 2].values())[cell_key(reservoir, 117, 329, -1)] == 2
        assert Counter(placements[final].values())[cell_key(reservoir, 118, 329, -1)] > 0
        # The supplied quantity assertions are cross-checked only after computing the oracle.
        for entry in expected['States']:
            revision = entry['Revision']
            selected = revision if revision >= breach else 0
            rows = diagnostics[selected]['Cells']
            assert entry['Cells'] == [{key: row[key] for key in ('X', 'Y', 'Z', 'Units')} for row in rows]
            assert entry['Topology'].lower() == diagnostics[selected]['TopologyHash']
        full_source = fixture / 'world.sqlite'
        if not full_source.exists():
            full_source = fixture / 'fullworld.sqlite'
        full = database(full_source)
        before = database(fixture / 'before-breach.sqlite')
        assert full['head'][2] == states[final] and before['head'][2] == states[before_revision]
        assert full['genesis'] == before['genesis'] == (states[0], sha(states[0]))
        work_revisions = verify_ledgers(full, hashes, world, branch, breach)
        assert len(work_revisions) == 3 and work_revisions[0] == expected['PartialRevision'] and work_revisions[-1] == breach
        assert verify_ledgers(before, hashes, world, branch, breach) == work_revisions[:2]
        assert full['ledgers']['journal'][breach - 1][4] == step
        for entry in expected['States']:
            assert entry['Work'] == sum(revision <= entry['Revision'] for revision in work_revisions)

        history_db = work / 'full-history.sqlite'
        shutil.copyfile(full_source, history_db)
        untouched = digest(history_db.read_bytes())
        for revision in [final, *range(final - 1, breach - 1, -1), before_revision, expected['PartialRevision'], 0]:
            inspect('exact history ' + str(revision), history_db, revision, final)
        assert database(history_db) == full and digest(history_db.read_bytes()) == untouched
        normal = work / 'normal.sqlite'
        shutil.copyfile(fixture / 'before-breach.sqlite', normal)
        value = run('real final excavation and flow', '--execute-world-step', normal, fixture / 'breach-step.bin')
        check_report(value, breach)
        after = database(normal)
        assert after['head'][2] == states[breach] and after['genesis'] == before['genesis']
        verify_ledgers(after, hashes, world, branch, breach)
        for name in ('identities', 'commands', 'journal', 'checkpoints'):
            index = 1 if name == 'identities' else 2 if name == 'commands' else 0
            assert after['ledgers'][name] == [row for row in full['ledgers'][name] if number(row[index]) <= breach]
        saved = digest(normal.read_bytes())
        run('duplicate final step refused', '--execute-world-step', normal, fixture / 'breach-step.bin', exit_code=1)
        assert database(normal) == after and digest(normal.read_bytes()) == saved
        for boundary in ('before', 'after'):
            path = work / (boundary + '-kill.sqlite')
            shutil.copyfile(fixture / 'before-breach.sqlite', path)
            kill_at_boundary(path, boundary)
            selected = before_revision if boundary == 'before' else breach
            inspect(boundary + ' kill recovery', path, selected, selected)
            assert database(path) == (before if boundary == 'before' else after)
            if boundary == 'before':
                check_report(run('retry after precommit kill', '--execute-world-step', path, fixture / 'breach-step.bin'), breach)
            else:
                previous_bytes = digest(path.read_bytes())
                run('duplicate after durable kill refused', '--execute-world-step', path, fixture / 'breach-step.bin', exit_code=1)
                assert digest(path.read_bytes()) == previous_bytes
            assert database(path) == after

        changed = dict(placements[final])
        quantities = Counter(changed.values())
        token = min(changed)
        destination = next(cell for cell in sorted(opened_domain) if cell != changed[token] and quantities[cell] < CELL_CAPACITY)
        changed[token] = destination
        modified = list(roots[final])
        cell_fields = read_frame(modified[18], 'geography.liquid-cells.v2', 8)
        modified[18] = frame('geography.liquid-cells.v2', *cell_fields[1:7], encode_rows(changed))
        assert witness(modified)[0] == changed and changed != placements[final]
        altered = frame('world.snapshot', *modified[1:])
        tampered = work / 'changed-head-placement.sqlite'
        shutil.copyfile(full_source, tampered)
        with closing(sqlite3.connect(tampered)) as connection:
            connection.execute('UPDATE world_head SET state=?,hash=?', (altered, sha(altered)))
            connection.commit()
        saved = digest(tampered.read_bytes())
        run('conserved canonical head replacement rejected by history', '--inspect-recorded-world', tampered, final, exit_code=1)
        assert digest(tampered.read_bytes()) == saved
        report.update(status='passed', materialUnits=TOTAL_UNITS, workRevisions=work_revisions,
                      independentlyReconstructedRevisions=list(range(breach + 1, final + 1)),
                      independentLiquidStates={str(revision): diagnostics[revision] for revision in sorted(diagnostics)},
                      ledgerHashes={name: ledger_hash(rows) for name, rows in after['ledgers'].items()},
                      changedHeadHash=digest(altered))
    except Exception:
        report.update(status='failed', failure=traceback.format_exc())
    finally:
        current = {path.name: digest(path.read_bytes()) for path in fixture.iterdir() if path.is_file()}
        report['fixturesUnchanged'] = originals == current
        if not report['fixturesUnchanged']:
            report.update(status='failed', artifactFailure='Fixture artifacts changed during qualification.')
        report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        report['artifactSha256'] = {str(path.relative_to(output.parent)): digest(path.read_bytes()) for path in work.iterdir() if path.is_file()}
        output.write_text(json.dumps(report, indent=2) + '\n')
    print('terrain liquid breach: ' + report['status'], flush=True)
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
