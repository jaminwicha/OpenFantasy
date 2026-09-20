"""Qualify profile-13/14 physical member creation through real Host commits and kills.

The managed authority supplies closed pre/post-completion fixtures. Independent OFC
reads check the complete root, original material backing and derived member identities.
Profile 14 also retains and independently verifies the complete immutable load policy.
This bounded probe does not qualify collapse, falling, damage, or occupant settlement.
"""
import argparse
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
import traceback
import uuid

from check_live_excavation import digest, fields, number


def frame(tag, *values):
    values = [tag.encode(), *values]
    return b'OFC\0' + struct.pack('>II', 1, len(values)) + b''.join(
        struct.pack('>I', len(value)) + value for value in values)


def read_frame(data, tag, count=None):
    result = fields(data)
    assert result[0] == tag.encode()
    assert count is None or len(result) == count
    return result


def member_identity(structure, ordinal):
    return hashlib.sha256(frame('structure.member.v1', bytes.fromhex(structure),
                                ordinal.to_bytes(8, 'big'))).digest()[:16].hex()


def source_claim(lot):
    return dict(LotId=lot[1].hex(), DefinitionId=lot[3].hex(), DefinitionVersion=lot[4].hex(),
                SourceId=lot[5].hex(), Start=number(lot[6]), End=number(lot[7]), Revision=number(lot[8]))


def policy_witness(data, spatial, definition_bytes):
    """Read the complete policy independently; do not substitute a derived load result."""
    assert len(data) <= 4 * 1024 * 1024
    policy = read_frame(data, 'construction.load-policy.v1', 6)
    scope = read_frame(policy[1], 'structure.terrain-scope', 4)
    assert scope[1:] == spatial[2:5] and len(set(scope[1:])) == 3
    assert all(len(value) == 16 for value in scope[1:])
    templates = [read_frame(value, 'construction.bearing-template.v1', 5)
                 for value in read_frame(policy[2], 'structure.policy-templates')[1:]]
    foundations = [read_frame(value, 'structure.stable-terrain-bearing', 9)
                   for value in read_frame(policy[3], 'structure.policy-foundations')[1:]]
    bodies = [read_frame(value, 'structure.body-mass', 4)
              for value in read_frame(policy[4], 'structure.policy-bodies')[1:]]
    items = [read_frame(value, 'structure.item-mass', 4)
             for value in read_frame(policy[5], 'structure.policy-items')[1:]]
    assert 1 <= len(templates) <= 16 and 1 <= len(foundations) <= 4096
    assert len(bodies) <= 64 and len(items) <= 256
    for catalog in [templates, foundations, bodies, items]:
        ids = [value[1] for value in catalog]
        assert all(len(value) == 16 for value in ids) and ids == sorted(set(ids))
    definitions_bytes = [definition_bytes] if isinstance(definition_bytes, bytes) else list(definition_bytes)
    assert 1 <= len(definitions_bytes) <= 16
    definitions = [read_frame(value, 'construction.definition', 12) for value in definitions_bytes]
    definition_ids = [value[2] for value in definitions]
    assert all(len(value) == 16 for value in definition_ids) and definition_ids == sorted(set(definition_ids))
    by_definition = {value[2]: value for value in definitions}
    assert {value[1]: value[2].hex() for value in templates} == {
        value[2]: digest(encoded) for value, encoded in zip(definitions, definitions_bytes)}
    for template in templates:
        definition = by_definition[template[1]]
        boxes = [[number(coordinate, True) for coordinate in read_frame(value, 'construction.member', 7)[1:]]
                 for value in read_frame(definition[8], 'construction.members')[1:]]
        assert len(template[2]) == 32
        ratings = [read_frame(value, 'structure.member-rating', 5)
                   for value in read_frame(template[3], 'structure.member-ratings')[1:]]
        assert 1 <= len(ratings) == len(boxes) <= 32
        for ordinal, rating in enumerate(ratings):
            assert number(rating[1]) == ordinal and number(rating[2]) > 0
            assert 1 <= number(rating[3]) <= 8000 and 1 <= number(rating[4]) <= 32
            box = boxes[ordinal]
            assert max(box[3] - box[0], box[4] - box[1]) <= number(rating[3])
        joints = [read_frame(value, 'structure.member-joint', 4)
                  for value in read_frame(template[4], 'structure.member-joints')[1:]]
        assert len(joints) <= 1024
        pairs = [(number(value[1]), number(value[2])) for value in joints]
        assert pairs == sorted(set(pairs))
        incoming, outgoing = [0] * len(ratings), [[] for _ in ratings]
        for joint, (upper, lower) in zip(joints, pairs):
            assert upper != lower and 0 <= upper < len(ratings) and 0 <= lower < len(ratings)
            kind = number(joint[3])
            assert kind in [0, 1]
            high, low = boxes[upper], boxes[lower]
            x, y, z = [max(0, min(high[axis + 3], low[axis + 3]) - max(high[axis], low[axis]))
                       for axis in range(3)]
            area = (x * y if high[2] == low[5] else 0) if kind == 0 else (
                y * z if high[3] == low[0] or high[0] == low[3] else
                x * z if high[4] == low[1] or high[1] == low[4] else 0)
            assert high[2] > 0 and area > 0
            incoming[lower] += 1
            outgoing[upper].append(lower)
        ready = [index for index, count in enumerate(incoming) if count == 0]
        visited = 0
        while ready:
            current = ready.pop()
            visited += 1
            for lower in outgoing[current]:
                incoming[lower] -= 1
                if incoming[lower] == 0:
                    ready.append(lower)
        assert visited == len(ratings)
    cells, sources, material_pins = set(), set(), {}
    for anchor in foundations:
        cell = tuple(number(value, True) for value in anchor[2:5])
        assert cell not in cells and anchor[5] not in sources
        cells.add(cell)
        sources.add(anchor[5])
        assert all(-(1 << 63) <= coordinate * 256 < (coordinate + 1) * 256 <= (1 << 63) - 1
                   for coordinate in cell)
        assert len(anchor[5]) == len(anchor[6]) == 16 and len(anchor[7]) == 32 and number(anchor[8]) > 0
        assert material_pins.setdefault(anchor[6], anchor[7]) == anchor[7]
    for value in bodies + items:
        assert len(value[2]) == 32 and number(value[3]) > 0
    body_pins = {value[1]: value[2].hex() for value in bodies}
    for body_bytes in read_frame(spatial[6], 'world.bodies')[1:]:
        body = read_frame(body_bytes, 'actor.body-definition', 7)
        assert body_pins[body[2]] == digest(body_bytes)
    actor_loads = []
    for presence_bytes in read_frame(spatial[7], 'world.presences')[1:]:
        presence = read_frame(presence_bytes, 'world.presence', 9)
        binding = read_frame(presence[2], 'actor.form-binding', 9)
        assert number(binding[1]) == 1 and len(binding[2]) == 16
        actor_loads.append(dict(ActorId=binding[2].hex(),
                               ElementId=hashlib.sha256(frame('structure.actor-load.v1', binding[2])).digest()[:16].hex()))
    foundation_ids = [value[1].hex() for value in foundations]
    actor_ids = [value['ElementId'] for value in actor_loads]
    assert len(set(foundation_ids + actor_ids)) == len(foundations) + len(actor_loads)
    diagnostic = dict(ContentHash=digest(data), TemplateCount=len(templates), FoundationCount=len(foundations),
                      BodyDefinitionCount=len(bodies), SupplementalItemCount=len(items))
    identities = dict(FoundationIds=foundation_ids, ActorLoads=actor_loads)
    return diagnostic, identities


def state_witness(data):
    root = read_frame(data, 'world.snapshot')
    profile = number(root[1])
    assert profile in [13, 14] and len(root) == {13: 18, 14: 19}[profile]
    spatial = read_frame(root[3], 'world.spatial', 8)
    assert number(spatial[1]) == 3
    authority = read_frame(root[2], 'authority.counted-state', 9)
    custody = read_frame(authority[3], 'custody.snapshot.v1', 6)
    construction = read_frame(root[15], 'construction.snapshot', 7)
    assert number(construction[1]) == 1
    world, branch = construction[2], construction[3]
    assert len(world) == len(branch) == 16
    assert spatial[2:4] == custody[1:3] == [world, branch]
    production = read_frame(root[12], 'production.snapshot', 8)
    assert production[2:4] == [world, branch]
    projects = read_frame(construction[6], 'construction.projects', 2)
    project = read_frame(projects[1], 'construction.project', 17)
    definitions = read_frame(construction[5], 'construction.definitions', 2)
    definition = read_frame(definitions[1], 'construction.definition', 12)
    assert project[3] == definition[2] and project[4].hex() == digest(definitions[1])
    member_geometry = read_frame(definition[8], 'construction.members', 13)
    volumes = []
    for value in member_geometry[1:]:
        box = read_frame(value, 'construction.member', 7)
        low_x, low_y, low_z, high_x, high_y, high_z = [number(item, True) for item in box[1:]]
        assert low_x < high_x and low_y < high_y and low_z < high_z
        volumes.append((high_x - low_x) * (high_y - low_y) * (high_z - low_z))
    recipe = read_frame(definition[5], 'craft.recipe', 6)
    assert definition[5] in read_frame(production[5], 'production.recipes')[1:]
    physical_recipe = read_frame(recipe[3], 'production.recipe', 9)
    terms = [read_frame(value, 'production.recipe-term', 6)
             for value in read_frame(physical_recipe[8], 'production.recipe-terms')[2:]]
    outputs = [term for term in terms if number(term[2]) != 1]
    output_index = number(definition[6])
    installed_term = outputs[output_index]
    assert number(installed_term[2]) == 2 and number(installed_term[5]) == 1
    materials = read_frame(physical_recipe[7], 'production.material-catalog')[2:]
    output_materials = [value for value in materials if fields(value)[3] == installed_term[3]]
    assert len(output_materials) == 1 and digest(output_materials[0]) == installed_term[4].hex()
    material = read_frame(output_materials[0], 'production.material', 8)
    output_mass, output_volume = number(material[5]), number(material[6])
    assert sum(volumes) == output_volume
    member_masses = [output_mass * volume // output_volume for volume in volumes]
    remainder = output_mass - sum(member_masses)
    assert 0 <= remainder < len(member_masses)
    member_masses = [mass + (index < remainder) for index, mass in enumerate(member_masses)]

    physical = read_frame(root[17], 'construction.physical.v1', 6)
    assert len(root[17]) <= 256 * 1024 and physical[1].hex() == digest(root[15])
    assert physical[2:4] == [world, branch]
    rows = read_frame(physical[5], 'construction.physical.members.v1')
    assert len(rows) <= 1025
    installed = None
    if project[15]:
        placement = read_frame(project[15], 'construction.installed', 6)
        installed = dict(StructureId=placement[1].hex(), GuardLotId=placement[2].hex(),
                         BatchId=placement[4].hex(), CommandId=placement[5].hex())
        assert number(placement[3]) == 0 and number(physical[4]) == 1 and len(rows) == 13
    else:
        assert number(physical[4]) == 0 and rows == [b'construction.physical.members.v1']
    members = []
    for ordinal, value in enumerate(rows[1:]):
        row = read_frame(value, 'construction.physical.member.v1', 9)
        assert installed is not None and row[1].hex() == installed['StructureId']
        assert number(row[2]) == ordinal and number(row[3]) == 0 and number(row[4]) == 10_000
        assert [number(item, True) for item in row[5:8]] == [0, 0, 0] and number(row[8]) == 1
        members.append(dict(ElementId=member_identity(row[1].hex(), ordinal), StructureId=row[1].hex(),
                            MemberIndex=ordinal, Revision=0, ConditionParts=10_000, Disposition='Standing',
                            Displacement=dict(X=0, Y=0, Z=0), InstalledLotId=installed['GuardLotId'],
                            MassMilligrams=member_masses[ordinal], VolumeMicrolitres=volumes[ordinal]))

    raw_lots = [read_frame(value, 'custody.source-lot.v1', 11)
                for value in read_frame(custody[5], 'custody.lots.v1')[1:]]
    reserved = [source_claim(lot) for lot in raw_lots if lot[2] == project[8] and lot[10] == project[9]]
    retired = []
    if installed is not None:
        batches = [read_frame(value, 'production.batch', 9)
                   for value in read_frame(production[6], 'production.batches')[1:]]
        selected = [batch for batch in batches if batch[2].hex() == installed['BatchId']]
        assert len(selected) == 1 and selected[0][3].hex() == installed['CommandId']
        assert selected[0][4:6] == [project[6], project[8]]
        for value in read_frame(selected[0][8], 'production.retired')[1:]:
            ingredient = read_frame(value, 'production.ingredient', 8)
            assert number(ingredient[2]) == 1
            claim = read_frame(ingredient[7], 'production.source-claim', 5)
            retired.append(dict(LotId=ingredient[3].hex(), DefinitionId=ingredient[5].hex(),
                                DefinitionVersion=ingredient[6].hex(), SourceId=claim[2].hex(),
                                Start=number(claim[3]), End=number(claim[4]), Revision=number(ingredient[4])))
        assert not reserved and sum(value['End'] - value['Start'] for value in retired) == 6
        lots = [read_frame(value, 'production.lot', 10)
                for value in read_frame(production[7], 'production.lots')[1:]]
        guards = [lot for lot in lots if lot[2].hex() == installed['GuardLotId']]
        assert len(guards) == 1 and guards[0][3] == project[8] and guards[0][9] == project[9]
        assert number(guards[0][7]) == number(guards[0][8]) == 0
        claim = read_frame(guards[0][6], 'production.output-claim', 6)
        assert claim[2].hex() == installed['BatchId']
        assert [number(item) for item in claim[3:6]] == [output_index, 0, 1]
        assert not any(lot[1].hex() in {value['LotId'] for value in retired} for lot in raw_lots)

    witness = dict(ProfileVersion=profile, WorldId=world.hex(), BranchId=branch.hex(), Revision=number(root[8]),
                AdvancingSteps=number(root[9]), StateHash=digest(data), ConstructionHash=digest(root[15]),
                ProductionHash=digest(root[12]), InventoryHash=digest(root[2]), CustodyHash=digest(authority[3]),
                SourcesHash=digest(authority[7]), SourceLotsHash=digest(custody[5]), TerrainHash=digest(spatial[5]),
                ExcavationHash=digest(root[16]), PhysicalHash=digest(root[17]), PhysicalRevision=number(physical[4]),
                ProjectId=project[2].hex(), Installed=installed, Members=members,
                ReservedSourceClaims=sorted(reserved, key=lambda value: value['LotId']),
                RetiredSourceClaims=sorted(retired, key=lambda value: value['LotId']))
    if profile == 14:
        witness['StructuralPolicy'], witness['PolicyIdentities'] = policy_witness(root[18], spatial, definitions[1])
    return witness


def ledger_hash(rows):
    return digest(json.dumps([[value.hex() if isinstance(value, bytes) else value for value in row]
                              for row in rows], separators=(',', ':')).encode())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['dotnet', 'fixture', 'output']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--configuration', default='Debug')
    args = parser.parse_args()
    runtime = Path(__file__).resolve().parents[1]
    dotnet, fixture, output = args.dotnet.resolve(), args.fixture.resolve(), args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    work = output.parent / ('construction-physical-' + uuid.uuid4().hex)
    command = [str(dotnet), str(runtime / 'src/OpenFantasy.Host/bin' / args.configuration / 'net10.0/OpenFantasy.Host.dll')]
    env = dict(os.environ, DOTNET_ROOT=str(dotnet.parent))
    report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope='Profile-13 initial physical member creation; exact source retirement, identity rows, history, retries and two real commit-boundary kills',
                  workDirectory=str(work), runs=[], crashes=[], independentStates=[])

    def run(name, *arguments, exit_code=0):
        selected = command + [str(value) for value in arguments]
        record = dict(name=name, command=selected)
        report['runs'].append(record)
        try:
            result = subprocess.run(selected, cwd=runtime, env=env, capture_output=True, text=True, timeout=30)
        except subprocess.TimeoutExpired as error:
            record.update(timedOut=True, timeoutSeconds=30, stdout=str(error.stdout or ''), stderr=str(error.stderr or ''))
            raise
        record.update(exitCode=result.returncode, stdout=result.stdout, stderr=result.stderr)
        assert result.returncode == exit_code and not result.stderr, record
        if exit_code == 0:
            return json.loads(result.stdout)
        assert result.stdout.startswith('Inspection failed:'), record
        return None

    def inspect_database(database, prefix, reference=None):
        with closing(sqlite3.connect(database.as_uri() + '?mode=ro&immutable=1', uri=True)) as connection:
            head = connection.execute('SELECT sequence,state,hash FROM world_head').fetchone()
            assert number(head[0]) == expected[prefix + 'Revision']
            assert digest(head[1]) == head[2].hex() == expected[prefix + 'Hash']
            witness = state_witness(head[1])
            assert witness['ProfileVersion'] == profile
            assert witness['WorldId'] == expected['WorldId'] and witness['BranchId'] == expected['BranchId']
            for key in ['ConstructionHash', 'ProductionHash', 'InventoryHash', 'CustodyHash']:
                assert witness[key] == expected[prefix + key], key
            identities = connection.execute('SELECT identity,sequence,kind FROM world_occupied_identity ORDER BY identity').fetchall()
            genesis_row = connection.execute('SELECT state,hash FROM world_genesis').fetchone()
            assert genesis_row[0] == genesis_bytes and genesis_row[1].hex() == digest(genesis_bytes)
            if profile == 14:
                assert fields(head[1])[18] == fields(genesis_bytes)[18]
                assert witness['StructuralPolicy']['ContentHash'] == expected['PolicyHash']
                reserved_ids = set(witness['PolicyIdentities']['FoundationIds']) | {
                    value['ElementId'] for value in witness['PolicyIdentities']['ActorLoads']}
                assert not reserved_ids.intersection(member_ids)
                assert not any(row[0].hex() in reserved_ids for row in identities)
            commands = connection.execute('SELECT principal,command_id,sequence,command,receipt FROM world_command_outcomes '
                                          'ORDER BY principal,command_id').fetchall()
            journal = connection.execute('SELECT sequence,kind,cause_id,advancing_steps,accepted_batch,outcomes,before_hash,after_hash '
                                         'FROM world_commits ORDER BY sequence').fetchall()
            committed = prefix == 'Post'
            member_rows = []
            for identity in member_ids:
                rows = [row for row in identities if row[0].hex() == identity]
                if committed:
                    assert len(rows) == 1 and number(rows[0][1]) == expected['PostRevision'] and rows[0][2] == 4
                    member_rows.append(dict(ElementId=identity, Sequence=number(rows[0][1]), Kind=rows[0][2]))
                else:
                    assert not rows
            receipts = [row for row in commands if row[1].hex() == expected['CommandId']]
            if committed:
                assert [value['ElementId'] for value in witness['Members']] == member_ids
                assert witness['PhysicalRevision'] == 1 and witness['PhysicalHash'] == expected['PhysicalHash']
                assert witness['Installed']['StructureId'] == expected['StructureId']
                assert witness['Installed']['CommandId'] == expected['CommandId']
                assert len(receipts) == 1 and number(receipts[0][2]) == expected['PostRevision']
                assert receipts[0][3] == action_bytes and receipts[0][4] == bytes.fromhex(expected['ReceiptHex'])
            else:
                assert witness['Members'] == [] and witness['PhysicalRevision'] == 0 and not receipts
                assert sum(value['End'] - value['Start'] for value in witness['ReservedSourceClaims']) == 6
            ledgers = dict(identities=identities, commands=commands, journal=journal)
            if reference is not None:
                assert ledgers == reference
        report['independentStates'].append(dict(database=str(database), **witness,
            MemberIdentityRows=member_rows, IdentityRows=len(identities), IdentityLedgerHash=ledger_hash(identities),
            ReceiptRows=len(commands), ReceiptLedgerHash=ledger_hash(commands), JournalRows=len(journal), JournalHash=ledger_hash(journal)))
        return witness, ledgers

    def check_report(value, witness):
        assert value['ProfileVersion'] == profile and value['StateHash'] == witness['StateHash']
        assert value['StructuralPolicy'] == witness.get('StructuralPolicy')
        physical = value['ConstructionPhysical']
        assert physical['Revision'] == witness['PhysicalRevision']
        assert physical['ContentHash'] == witness['PhysicalHash'] and physical['ConstructionHash'] == witness['ConstructionHash']
        assert len(physical['Members']) == len(witness['Members'])
        for actual, independent in zip(physical['Members'], witness['Members']):
            for key, expected_value in independent.items():
                if key == 'Displacement':
                    assert all(actual[key][axis] == coordinate for axis, coordinate in expected_value.items())
                else:
                    assert actual[key] == expected_value, key

    def history(database, item, prefix):
        value = run(f'{database.stem} history {item["Revision"]}', '--inspect-recorded-world', database, item['Revision'])
        assert value['Contract'] == 'recorded-world-state-v1' and value['ProfileVersion'] == profile
        assert value['RecordedRevision'] == item['Revision'] and value['StateHash'] == item['Hash']
        assert value['SourceHeadRevision'] == expected[prefix + 'Revision'] and value['SourceHeadHash'] == expected[prefix + 'Hash']
        assert value['StructuralPolicy'] == genesis.get('StructuralPolicy')
        if item['Revision'] in retained:
            check_report(value, retained[item['Revision']])
        else:
            assert item['Revision'] < expected['PostRevision']
            assert value['ConstructionPhysical']['Revision'] == 0 and value['ConstructionPhysical']['Members'] == []

    def history_and_retry(database):
        before = digest(database.read_bytes())
        for item in reversed(expected['History']):
            history(database, item, 'Post')
        run(f'{database.stem} refuse committed completion retry', '--execute-world-step', database, step_path, exit_code=1)
        inspect_database(database, 'Post', after_ledgers)
        assert digest(database.read_bytes()) == before
        return before

    try:
        work.mkdir()
        expected = json.loads((fixture / 'expected.json').read_text())
        profile = expected.get('ProfileVersion', 13)
        assert profile in [13, 14]
        if profile == 14:
            report['scope'] = ('Profile-14 durable load policy and stable physical completion; exact source retirement, '
                               'identity rows, history, retries and two real commit-boundary kills; no collapse')
        assert expected['PostRevision'] == expected['PreRevision'] + 1 and expected['MemberCount'] == 12
        assert expected['PhysicalBeforeRevision'] == 0 and expected['PhysicalAfterRevision'] == 1
        report['fixtureSha256'] = {path.name: digest(path.read_bytes()) for path in sorted(fixture.iterdir()) if path.is_file()}
        assert not any(name.endswith(('-wal', '-shm', '-journal')) for name in report['fixtureSha256']), 'Fixture must be closed.'
        genesis_bytes = (fixture / 'genesis.bin').read_bytes()
        member_ids = [member_identity(expected['StructureId'], ordinal) for ordinal in range(12)]
        assert len(set(member_ids)) == 12 and member_ids == expected['ExpectedMemberIds']
        report['independentMemberIds'] = member_ids
        step_path = fixture / 'completion-step.bin'
        step_bytes = step_path.read_bytes()
        step = read_frame(step_bytes, 'world.step-batch', 10)
        assert number(step[1]) == profile and number(step[5]) == expected['PreRevision'] and number(step[7]) == 1
        assert step[3].hex() == expected['WorldId'] and step[4].hex() == expected['BranchId']
        assert read_frame(step[8], 'world.step-movement') == [b'world.step-movement']
        actions = read_frame(step[9], 'world.step-actions', 2)
        action_bytes = actions[1]
        action = read_frame(action_bytes, 'world.actor-action', 10)
        intent = read_frame(action[9], 'world.action.construction', 7)
        assert number(action[1]) == 5 and number(intent[1]) == 1
        assert action[2].hex() == expected['CommandId'] and action[5].hex() == expected['Builder']
        assert action[6:8] == step[3:5]
        report.update(independentStepHash=digest(step_bytes), independentActionHash=digest(action_bytes))
        before, before_ledgers = inspect_database(fixture / 'before-completion.sqlite', 'Pre')
        after, after_ledgers = inspect_database(fixture / 'after-completion.sqlite', 'Post')
        assert intent[2].hex() == before['ProjectId']
        assert number(step[6]) == before['AdvancingSteps'] and after['AdvancingSteps'] == before['AdvancingSteps'] + 1
        assert before['ReservedSourceClaims'] == after['RetiredSourceClaims']
        for key in ['SourcesHash', 'TerrainHash', 'ExcavationHash']:
            assert before[key] == after[key], key
        assert set(before_ledgers['identities']).issubset(set(after_ledgers['identities']))
        new_rows = set(after_ledgers['identities']) - set(before_ledgers['identities'])
        assert all(number(row[1]) == expected['PostRevision'] for row in new_rows)
        assert len(after_ledgers['commands']) == len(before_ledgers['commands']) + 1
        assert len(after_ledgers['journal']) == len(before_ledgers['journal']) + 1
        genesis = state_witness(genesis_bytes)
        assert genesis['ProfileVersion'] == profile
        assert genesis['Revision'] == genesis['AdvancingSteps'] == genesis['PhysicalRevision'] == 0 and not genesis['Members']
        assert genesis['WorldId'] == expected['WorldId'] and genesis['BranchId'] == expected['BranchId']
        if profile == 14:
            assert genesis['StructuralPolicy'] == before['StructuralPolicy'] == after['StructuralPolicy']
            assert genesis['PolicyIdentities'] == before['PolicyIdentities'] == after['PolicyIdentities']
            assert genesis['StructuralPolicy']['ContentHash'] == expected['PolicyHash']
            report['policyIdentityCoverage'] = dict(**genesis['PolicyIdentities'], CommittedIdentityRows=0,
                scope='Declarations and derived actor-load identities in retained genesis; this schema has no sequence-zero identity rows')
        retained = {0: genesis, expected['PreRevision']: before, expected['PostRevision']: after}
        expected_history = {item['Revision']: item['Hash'] for item in expected['History']}
        assert len(expected_history) == len(expected['History'])
        assert all(expected_history[revision] == witness['StateHash'] for revision, witness in retained.items())
        report['genesisWitness'] = genesis

        normal = work / 'normal.sqlite'
        shutil.copyfile(fixture / 'before-completion.sqlite', normal)
        history(normal, dict(Revision=expected['PreRevision'], Hash=expected['PreHash']), 'Pre')
        value = run('ordinary physical construction completion', '--execute-world-step', normal, step_path)
        assert value['Revision'] == expected['PostRevision']
        check_report(value, after)
        inspect_database(normal, 'Post', after_ledgers)
        report['ordinaryReadOnlyDatabaseSha256'] = history_and_retry(normal)
        for boundary in ['before', 'after']:
            database = work / (boundary + '.sqlite')
            shutil.copyfile(fixture / 'before-completion.sqlite', database)
            selected = command + ['--probe-world-step-crash', str(database), str(step_path), boundary]
            process = subprocess.Popen(selected, cwd=runtime, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
            signals = queue.Queue()
            reader = threading.Thread(target=lambda: signals.put(process.stdout.readline()), daemon=True)
            reader.start()
            crash = dict(boundary=boundary, command=selected, pid=process.pid, signalTimeoutSeconds=30,
                         termination='external process.kill; no graceful Dispose')
            report['crashes'].append(crash)
            try:
                signal = signals.get(timeout=30)
                crash['signal'] = signal
                assert json.loads(signal) == {'Boundary': 'BeforeCommit' if boundary == 'before' else 'AfterCommit'}
                process.kill()
            finally:
                if process.poll() is None:
                    process.kill()
                stdout, stderr = process.communicate(timeout=10)
                crash.update(exitCode=process.returncode, stdout=stdout, stderr=stderr)
                reader.join(timeout=1)
            assert process.returncode != 0 and not stderr
            prefix = 'Pre' if boundary == 'before' else 'Post'
            history(database, dict(Revision=expected[prefix + 'Revision'], Hash=expected[prefix + 'Hash']), prefix)
            inspect_database(database, prefix, before_ledgers if prefix == 'Pre' else after_ledgers)
            if prefix == 'Pre':
                value = run('retry rolled-back physical completion', '--execute-world-step', database, step_path)
                assert value['Revision'] == expected['PostRevision']
                check_report(value, after)
            inspect_database(database, 'Post', after_ledgers)
            crash['readOnlyDatabaseSha256'] = history_and_retry(database)
            print(f'{boundary}-commit physical completion kill and history: passed', flush=True)
        report['status'] = 'passed'
    except Exception as error:
        report.update(status='failed', failure=repr(error), traceback=traceback.format_exc())
    finally:
        if 'fixtureSha256' in report:
            changed = [name for name, value in report['fixtureSha256'].items()
                       if not (fixture / name).is_file() or digest((fixture / name).read_bytes()) != value]
            report['fixtureFilesChanged'] = changed
            if changed:
                report.update(status='failed', artifactFailure='Read-only fixture artifacts changed.')
        report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2) + '\n')
    print('construction physical: ' + report['status'], flush=True)
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
