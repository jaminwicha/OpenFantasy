"""Qualify one atomic profile-15 excavation and safe rigid settlement through the Host.

Uses closed managed-authority fixtures, ordinary apply/retry/history, and two actual
external kills at the existing commit boundaries. Independent OFC reads check material
backing, policies, excavation issuance, member/rider motion and one advancing interval.
Pending operations, damaging falls and unbounded collapse are outside this probe.
"""
import argparse
from contextlib import closing
import datetime
import hashlib
import json
import math
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

from check_construction_physical import frame, ledger_hash, member_identity, policy_witness, read_frame
from check_live_excavation import digest, fields, number


def position(values):
    return dict(zip(('X', 'Y', 'Z'), values))


def same_position(actual, expected):
    assert all(actual[axis] == expected[axis] for axis in ('X', 'Y', 'Z'))


def flight_milliseconds(drop, gravity):
    numerator, denominator = drop * 2 * 1000 * 1_000_000, gravity * 256
    result = math.isqrt(numerator // denominator)
    return result + (denominator * result * result < numerator)


def settlement_policy(data):
    assert len(data) <= 256
    row = read_frame(data, 'construction.settlement-policy.v1', 5)
    maximum, actor, member, gravity = [number(value) for value in row[1:]]
    assert 1 <= maximum <= 256 and 0 <= actor <= maximum and 0 <= member <= maximum
    assert 1 <= gravity <= 1_000_000
    assert gravity * 1800 * 1800 * 256 >= maximum * 2 * 1000 * 1_000_000
    assert frame('construction.settlement-policy.v1', *row[1:]) == data
    return dict(ContentHash=digest(data), MaximumDropSubunits=maximum, SafeActorDropSubunits=actor,
                SafeMemberDropSubunits=member, GravityMillimetresPerSecondSquared=gravity)


def state_witness(data, expected):
    root = read_frame(data, 'world.snapshot')
    profile = number(root[1])
    assert profile in (14, 15, 16, 17, 18) and len(root) == {14: 19, 15: 20, 16: 22, 17: 23, 18: 24}[profile]
    if profile == 18:
        assert number(root[23]) == 2
    spatial = read_frame(root[3], 'world.spatial', 8)
    assert number(spatial[1]) == (4 if profile in (17, 18) else 3)
    authority = read_frame(root[2], 'authority.counted-state', 9)
    custody = read_frame(authority[3], 'custody.snapshot.v1', 6)
    construction = read_frame(root[15], 'construction.snapshot', 7)
    production = read_frame(root[12], 'production.snapshot', 8)
    world, branch = construction[2:4]
    assert spatial[2:4] == custody[1:3] == production[2:4] == [world, branch]
    assert world.hex() == expected['WorldId'] and branch.hex() == expected['BranchId']
    if profile not in (17, 18):
        definition_bytes = read_frame(construction[5], 'construction.definitions', 2)[1]
        definition = read_frame(definition_bytes, 'construction.definition', 12)
        project = read_frame(read_frame(construction[6], 'construction.projects', 2)[1], 'construction.project', 17)
        assert project[3] == definition[2] and project[4].hex() == digest(definition_bytes)
        boxes = [[number(value, True) for value in read_frame(row, 'construction.member', 7)[1:]]
                 for row in read_frame(definition[8], 'construction.members')[1:]]
        assert 1 <= len(boxes) <= 32
        volumes = []
        for box in boxes:
            assert all(box[axis] < box[axis + 3] for axis in range(3))
            volumes.append((box[3] - box[0]) * (box[4] - box[1]) * (box[5] - box[2]))
        recipe = read_frame(definition[5], 'craft.recipe', 6)
        assert definition[5] in read_frame(production[5], 'production.recipes')[1:]
        physical_recipe = read_frame(recipe[3], 'production.recipe', 9)
        terms = [read_frame(value, 'production.recipe-term', 6)
                 for value in read_frame(physical_recipe[8], 'production.recipe-terms')[2:]]
        output_index = number(definition[6])
        output = [term for term in terms if number(term[2]) != 1][output_index]
        assert number(output[2]) == 2 and number(output[5]) == 1
        materials = [value for value in read_frame(physical_recipe[7], 'production.material-catalog')[2:]
                     if fields(value)[3] == output[3]]
        assert len(materials) == 1 and digest(materials[0]) == output[4].hex()
        material = read_frame(materials[0], 'production.material', 8)
        mass, volume = number(material[5]), number(material[6])
        assert sum(volumes) == volume
        masses = [mass * member_volume // volume for member_volume in volumes]
        remainder = mass - sum(masses)
        assert 0 <= remainder < len(masses)
        masses = [value + (ordinal < remainder) for ordinal, value in enumerate(masses)]

        physical = read_frame(root[17], 'construction.physical.v1', 6)
        assert len(root[17]) <= 256 * 1024 and physical[1].hex() == digest(root[15])
        assert physical[2:4] == [world, branch]
        rows = read_frame(physical[5], 'construction.physical.members.v1')[1:]
        assert len(rows) <= 1024
        installed = None
        if project[15]:
            placement = read_frame(project[15], 'construction.installed', 6)
            installed = dict(StructureId=placement[1].hex(), GuardLotId=placement[2].hex(),
                             GuardLotRevision=number(placement[3]), BatchId=placement[4].hex(),
                             CommandId=placement[5].hex(), ProjectCustodianId=project[8].hex())
            assert len(rows) == len(boxes)
            lots = [read_frame(value, 'production.lot', 10)
                    for value in read_frame(production[7], 'production.lots')[1:]]
            guards = [lot for lot in lots if lot[2] == placement[2]]
            assert len(guards) == 1 and guards[0][3] == project[8] and guards[0][9] == project[9]
            assert number(guards[0][7]) == number(placement[3]) and number(guards[0][8]) == 0
            claim = read_frame(guards[0][6], 'production.output-claim', 6)
            assert claim[2] == placement[4] and [number(value) for value in claim[3:]] == [output_index, 0, 1]
            batches = [read_frame(value, 'production.batch', 9)
                       for value in read_frame(production[6], 'production.batches')[1:]]
            backing = [batch for batch in batches if batch[2] == placement[4]]
            assert len(backing) == 1 and backing[0][3] == placement[5] and backing[0][4:6] == [project[6], project[8]]
        else:
            assert not rows
        members = []
        for ordinal, value in enumerate(rows):
            row = read_frame(value, 'construction.physical.member.v1', 9)
            assert installed is not None and row[1].hex() == installed['StructureId'] and number(row[2]) == ordinal
            disposition = number(row[8])
            assert disposition in (1, 2, 3) and number(row[4]) <= 10_000
            members.append(dict(ElementId=member_identity(row[1].hex(), ordinal), StructureId=row[1].hex(),
                                MemberIndex=ordinal, Revision=number(row[3]), ConditionParts=number(row[4]),
                                Disposition={1: 'Standing', 2: 'Falling', 3: 'Resting'}[disposition],
                                Displacement=position(number(value, True) for value in row[5:8]),
                                InstalledLotId=installed['GuardLotId'], MassMilligrams=masses[ordinal],
                                VolumeMicrolitres=volumes[ordinal]))
    else:
        # Coupled profiles keep every independent fabrication cohort and its complete template catalog.
        definition_bytes = read_frame(construction[5], 'construction.definitions')[1:]
        definitions = [read_frame(value, 'construction.definition', 12) for value in definition_bytes]
        projects = [read_frame(value, 'construction.project', 17)
                    for value in read_frame(construction[6], 'construction.projects')[1:]]
        assert 1 <= len(definitions) <= 16 and 1 <= len(projects) <= 32
        for ids in ([value[2] for value in definitions], [value[2] for value in projects]):
            assert all(len(value) == 16 for value in ids) and ids == sorted(set(ids))
        raw_lots = [read_frame(value, 'custody.source-lot.v1', 11)
                    for value in read_frame(custody[5], 'custody.lots.v1')[1:]]
        production_lots = [read_frame(value, 'production.lot', 10)
                           for value in read_frame(production[7], 'production.lots')[1:]]
        batches = [read_frame(value, 'production.batch', 9)
                   for value in read_frame(production[6], 'production.batches')[1:]]
        admitted_sources = {value[1]: value for value in (
            read_frame(row, 'authority.source', 5) for row in read_frame(authority[7], 'authority.sources')[1:])}
        calibrations = {}
        for definition, encoded in zip(definitions, definition_bytes):
            boxes = [[number(value, True) for value in read_frame(row, 'construction.member', 7)[1:]]
                     for row in read_frame(definition[8], 'construction.members')[1:]]
            assert 1 <= len(boxes) <= 32
            volumes = []
            for box in boxes:
                assert all(box[axis] < box[axis + 3] for axis in range(3))
                volumes.append((box[3] - box[0]) * (box[4] - box[1]) * (box[5] - box[2]))
            recipe = read_frame(definition[5], 'craft.recipe', 6)
            assert definition[5] in read_frame(production[5], 'production.recipes')[1:]
            physical_recipe = read_frame(recipe[3], 'production.recipe', 9)
            terms = [read_frame(value, 'production.recipe-term', 6)
                     for value in read_frame(physical_recipe[8], 'production.recipe-terms')[2:]]
            outputs = [value for value in terms if number(value[2]) != 1]
            output_index = number(definition[6])
            assert 0 <= output_index < len(outputs)
            output = outputs[output_index]
            assert number(output[2]) == 2 and number(output[5]) == 1
            materials = [value for value in read_frame(physical_recipe[7], 'production.material-catalog')[2:]
                         if fields(value)[3] == output[3]]
            assert len(materials) == 1 and digest(materials[0]) == output[4].hex()
            material = read_frame(materials[0], 'production.material', 8)
            mass, volume = number(material[5]), number(material[6])
            assert mass > 0 and volume > 0 and sum(volumes) == volume
            masses = [mass * member_volume // volume for member_volume in volumes]
            remainder = mass - sum(masses)
            assert 0 <= remainder < len(masses)
            masses = [value + (ordinal < remainder) for ordinal, value in enumerate(masses)]
            bindings = [read_frame(value, 'craft.item-physical', 6)
                        for value in read_frame(recipe[4], 'craft.bindings')[1:]]
            physical_items = {(value[4], value[5]): (value[2], value[3]) for value in bindings}
            assert len(physical_items) == len(bindings)
            inputs = [value for value in terms if number(value[2]) == 1]
            input_units = {physical_items[(value[3], value[4])]: number(value[5]) for value in inputs}
            assert len(input_units) == len(inputs) == 1
            calibrations[definition[2]] = dict(Version=digest(encoded), Definition=definition, Recipe=recipe,
                OutputIndex=output_index, OutputCount=len(outputs), OutputItem=physical_items[(output[3], output[4])],
                InputUnits=input_units, Masses=masses, Volumes=volumes)

        physical = read_frame(root[17], 'construction.physical.v1', 6)
        assert len(root[17]) <= 256 * 1024 and physical[1].hex() == digest(root[15])
        assert physical[2:4] == [world, branch] and number(physical[4]) <= number(root[8])
        rows = [read_frame(value, 'construction.physical.member.v1', 9)
                for value in read_frame(physical[5], 'construction.physical.members.v1')[1:]]
        row_keys = [(value[1], number(value[2])) for value in rows]
        assert len(rows) <= 1024 and row_keys == sorted(set(row_keys))
        installed, member_specs, retired_ranges = {}, {}, {}
        backing_lots, backing_batches, retired_lots = set(), set(), set()
        for project in projects:
            calibration = calibrations[project[3]]
            assert project[4].hex() == calibration['Version']
            if not project[15]:
                continue
            placement = read_frame(project[15], 'construction.installed', 6)
            structure = placement[1].hex()
            assert structure not in installed and placement[2] not in backing_lots and placement[4] not in backing_batches
            backing_lots.add(placement[2])
            backing_batches.add(placement[4])
            spawned = lambda ordinal: hashlib.sha256(frame('entity.spawned', placement[5], ordinal.to_bytes(8, 'big'))).digest()[:16]
            assert placement[1] == spawned(calibration['OutputCount'] + 1)
            assert placement[2] == spawned(calibration['OutputIndex'] + 1) and placement[4] == spawned(0)
            guard = [lot for lot in production_lots if lot[2] == placement[2]]
            assert len(guard) == 1 and guard[0][3] == project[8] and guard[0][9] == project[9]
            assert tuple(guard[0][4:6]) == calibration['OutputItem']
            assert number(guard[0][7]) == number(placement[3]) and number(guard[0][8]) == 0
            claim = read_frame(guard[0][6], 'production.output-claim', 6)
            assert claim[2] == placement[4] and [number(value) for value in claim[3:]] == [calibration['OutputIndex'], 0, 1]
            backing = [batch for batch in batches if batch[2] == placement[4]]
            assert len(backing) == 1 and backing[0][3] == placement[5] and backing[0][4:6] == [project[6], project[8]]
            assert backing[0][6] == calibration['Recipe'][2]
            assert backing[0][7].hex() == digest(calibration['Definition'][5])
            input_totals, retired = {}, []
            for value in read_frame(backing[0][8], 'production.retired')[1:]:
                ingredient = read_frame(value, 'production.ingredient', 8)
                assert number(ingredient[2]) == 1 and ingredient[3] not in retired_lots
                retired_lots.add(ingredient[3])
                claim = read_frame(ingredient[7], 'production.source-claim', 5)
                source_row = admitted_sources[claim[2]]
                start, end = number(claim[3]), number(claim[4])
                assert 0 <= start < end <= number(source_row[4]) and source_row[2:4] == ingredient[5:7]
                intervals = retired_ranges.setdefault(claim[2], [])
                assert all(end <= left or start >= right for left, right in intervals)
                intervals.append((start, end))
                assert not any(lot[1] == ingredient[3] or lot[5] == claim[2]
                               and start < number(lot[7]) and number(lot[6]) < end for lot in raw_lots)
                item = tuple(ingredient[5:7])
                input_totals[item] = input_totals.get(item, 0) + end - start
                retired.append(dict(LotId=ingredient[3].hex(), DefinitionId=ingredient[5].hex(),
                    DefinitionVersion=ingredient[6].hex(), SourceId=claim[2].hex(), Start=start, End=end,
                    Revision=number(ingredient[4])))
            assert input_totals == calibration['InputUnits']
            assert not any(lot[10] == project[9] for lot in raw_lots)
            installed[structure] = dict(StructureId=structure, GuardLotId=placement[2].hex(),
                GuardLotRevision=number(placement[3]), BatchId=placement[4].hex(), CommandId=placement[5].hex(),
                ProjectCustodianId=project[8].hex(), ProjectId=project[2].hex(), DefinitionId=project[3].hex(),
                RetiredSourceClaims=sorted(retired, key=lambda value: value['LotId']))
            for ordinal, (mass, volume) in enumerate(zip(calibration['Masses'], calibration['Volumes'])):
                member_specs[(placement[1], ordinal)] = (installed[structure], mass, volume)
        assert set(row_keys) == set(member_specs)
        members = []
        for row, (structure, ordinal) in zip(rows, row_keys):
            cohort, mass, volume = member_specs[(structure, ordinal)]
            disposition = number(row[8])
            assert disposition in (1, 2, 3) and number(row[4]) <= 10_000 and number(row[3]) <= number(physical[4])
            members.append(dict(ElementId=member_identity(structure.hex(), ordinal), StructureId=structure.hex(),
                MemberIndex=ordinal, Revision=number(row[3]), ConditionParts=number(row[4]),
                Disposition={1: 'Standing', 2: 'Falling', 3: 'Resting'}[disposition],
                Displacement=position(number(value, True) for value in row[5:8]),
                InstalledLotId=cohort['GuardLotId'], MassMilligrams=mass, VolumeMicrolitres=volume))

    actors = {}
    controls = {}
    for value in read_frame(authority[5], 'authority.controls')[1:]:
        row = read_frame(value, 'authority.control', 4)
        assert all(len(identity) == 16 for identity in row[1:]) and row[3].hex() not in controls
        controls[row[3].hex()] = dict(PrincipalId=row[1].hex(), SessionId=row[2].hex())
    for value in read_frame(spatial[7], 'world.presences')[1:]:
        row = read_frame(value, 'world.presence', 9)
        binding = read_frame(row[2], 'actor.form-binding', 9)
        actor = binding[2].hex()
        assert actor not in actors
        actors[actor] = dict(Feet=position(number(value, True) for value in row[4:7]), Revision=number(row[7]),
                             BindingHash=digest(row[2]), ConditionHash=digest(row[3]))
    gathering = read_frame(root[11], 'gather.snapshot', 9)
    assert gathering[2:4] == [world, branch] and number(gathering[4]) == number(root[9])
    rules = read_frame(gathering[5], 'gather.rules', 7)
    assert [number(value) for value in rules[1:5]] == [1, 1000, 10, 400]
    for value in read_frame(gathering[8], 'gather.actors')[1:]:
        row = read_frame(value, 'gather.actor-work', 4)
        actors[row[2].hex()]['Effort'] = number(row[3])
    residents = {}
    for value in read_frame(root[4], 'world.residents')[1:]:
        row = read_frame(value, 'world.resident', 13)
        residents[row[1].hex()] = dict(InventoryCustodianId=row[2].hex(),
            ExperiencedMilliseconds=number(row[10]) * 86_400_000 + number(row[11]))
    assert set(residents) == set(actors)
    sites = [read_frame(value, 'terrain.excavation-site.v1', 12)
             for value in read_frame(root[16], 'terrain.excavation.v1')[1:]]
    site = [value for value in sites if value[1].hex() == expected['SiteId']]
    assert len(site) == 1
    site = site[0]
    target = struct.unpack('>qqq', site[2])
    source = site[4].hex()
    assert source == expected['SourceId'] and site[5].hex() == expected['Player']
    assert site[3].hex() == expected['TerrainSourceId']
    calibration = read_frame(site[7], 'production.material', 8)
    unit_volume = number(calibration[6])
    assert unit_volume > 0 and 1_000_000_000 % unit_volume == 0
    quantity = 1_000_000_000 // unit_volume
    sources = [read_frame(value, 'authority.source', 5) for value in read_frame(authority[7], 'authority.sources')[1:]]
    admitted = [row for row in sources if row[1].hex() == source]
    assert len(admitted) == 1 and admitted[0][2] + admitted[0][3] == site[6]
    assert number(admitted[0][4]) == quantity
    lots = [read_frame(value, 'custody.source-lot.v1', 11)
            for value in read_frame(custody[5], 'custody.lots.v1')[1:]]
    storages = [read_frame(value, 'custody.storage.v1', 4)
                for value in read_frame(custody[4], 'custody.storages.v1')[1:]]
    issued = [dict(LotId=row[1].hex(), CustodianId=row[2].hex(), DefinitionPin=(row[3] + row[4]).hex(),
                   SourceId=row[5].hex(), Start=number(row[6]), End=number(row[7]), Revision=number(row[8]),
                   Protection=number(row[9]), ReservedBy=row[10].hex()) for row in lots if row[5].hex() == source]
    regions = []
    selected = []
    for value in read_frame(spatial[5], 'world.regions')[1:]:
        region = read_frame(value, 'terrain.region.v1', 7)
        bounds = read_frame(region[4], 'terrain.bounds.v1', 7)
        low = [number(value, True) for value in bounds[1:4]]
        size = [number(value) for value in bounds[4:7]]
        assert len(region[6]) % 4 == 0
        edits = [value[0] for value in struct.iter_unpack('>I', region[6])]
        assert edits == sorted(set(edits))
        regions.append(dict(BaseHash=digest(frame('terrain.region.v1', *region[1:5])),
                            Revision=number(region[5]), Edits=edits))
        if all(lo <= coordinate < lo + length for lo, coordinate, length in zip(low, target, size)):
            x, y, z = [coordinate - lo for coordinate, lo in zip(target, low)]
            index = (z * size[1] + y) * size[0] + x
            selected.append(dict(Region=len(regions) - 1, Index=index, IsExcavated=index in edits))
    assert len(selected) == 1
    load_policy, identities = policy_witness(root[18], spatial, definition_bytes)
    witness = dict(ProfileVersion=profile, WorldId=world.hex(), BranchId=branch.hex(), StateHash=digest(data),
                Revision=number(root[8]), AdvancingSteps=number(root[9]),
                CalendarMilliseconds=number(root[6], True) * 86_400_000 + number(root[7]),
                ConstructionHash=digest(root[15]), ProductionHash=digest(root[12]),
                InventoryHash=digest(root[2]), CustodyHash=digest(authority[3]), SourcesHash=digest(authority[7]),
                CustodyRevision=number(custody[3]),
                Storages={row[1].hex(): dict(UnitCapacity=number(row[2]), Revision=number(row[3])) for row in storages},
                SourceLotPins={row[1].hex(): digest(frame('custody.source-lot.v1', *row[1:])) for row in lots},
                SourceLotRevisions={row[1].hex(): number(row[8]) for row in lots},
                PhysicalHash=digest(root[17]), PhysicalRevision=number(physical[4]), Members=members, Installed=installed,
                Actors=actors, Controls=controls, Residents=residents, Regions=regions, Target=position(target), **selected[0],
                ExcavationHash=digest(root[16]), RequiredWork=number(site[9]), CompletedWork=number(site[10]),
                MaterialPin=site[6].hex(), YieldQuantity=quantity, Issued=issued,
                StructuralPolicy=load_policy, PolicyIdentities=identities,
                StructuralSettlementPolicy=settlement_policy(root[19]) if profile >= 15 else None)
    if profile in (17, 18):
        coupled = read_frame(root[22], 'construction.coupled-settlement-policy.v1', 2)
        assemblies = number(coupled[1])
        assert len(root[22]) <= 128 and 1 <= assemblies <= 32
        assert frame('construction.coupled-settlement-policy.v1', coupled[1]) == root[22]
        # This retained policy's common-trajectory limit is not the event cursor's scene-derived work bound.
        witness['CoupledSettlementPolicy'] = dict(ContentHash=digest(root[22]), MaximumAssemblies=assemblies,
                                                   MaximumWorkUnits=2 * assemblies * 32 + 2 * 64)
    if profile == 18:
        witness['DescentMode'] = 2
    return witness


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('dotnet', 'fixture', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--configuration', default='Debug')
    args = parser.parse_args()
    runtime = Path(__file__).resolve().parents[1]
    dotnet, fixture, output = args.dotnet.resolve(), args.fixture.resolve(), args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    work = output.parent / ('structural-settlement-' + uuid.uuid4().hex)
    command = [str(dotnet), str(runtime / 'src/OpenFantasy.Host/bin' / args.configuration / 'net10.0/OpenFantasy.Host.dll')]
    env = dict(os.environ, DOTNET_ROOT=str(dotnet.parent))
    report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        scope='Profile-15 atomic final excavation, safe member/rider settling, single issuance, history, retry and two commit-boundary kills',
        workDirectory=str(work), runs=[], crashes=[], independentStates=[], unresolvedExpectations=[],
        unqualifiedClaims=['Durable pending or time-sliced operations', 'Damaging impacts or fracture',
                           'Lateral motion or arbitrarily large collapse'])

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

    def read_database(database):
        with closing(sqlite3.connect(database.as_uri() + '?mode=ro&immutable=1', uri=True)) as connection:
            head = connection.execute('SELECT sequence,state,hash FROM world_head').fetchone()
            assert number(head[0]) == number(fields(head[1])[8]) and digest(head[1]) == head[2].hex()
            genesis = connection.execute('SELECT state,hash FROM world_genesis').fetchone()
            assert digest(genesis[0]) == genesis[1].hex()
            ledgers = dict(
                identities=connection.execute('SELECT identity,sequence,kind FROM world_occupied_identity ORDER BY identity').fetchall(),
                commands=connection.execute('SELECT principal,command_id,sequence,command,receipt FROM world_command_outcomes ORDER BY principal,command_id').fetchall(),
                journal=connection.execute('SELECT sequence,kind,cause_id,advancing_steps,accepted_batch,outcomes,before_hash,after_hash FROM world_commits ORDER BY sequence').fetchall())
        return head[1], genesis[0], ledgers

    def inspect_database(database, prefix, reference=None):
        data, stored_genesis, ledgers = read_database(database)
        witness = state_witness(data, expected)
        assert stored_genesis == genesis_bytes
        assert witness['ProfileVersion'] == 15
        assert witness['Revision'] == expected[prefix + 'Revision'] and witness['StateHash'] == expected[prefix + 'Hash']
        assert fields(data)[18:20] == fields(genesis_bytes)[18:20]
        assert witness['StructuralPolicy']['ContentHash'] == expected['PolicyHash']
        assert witness['StructuralSettlementPolicy']['ContentHash'] == expected['SettlementPolicyHash']
        for key in ('ConstructionHash', 'ProductionHash', 'InventoryHash', 'CustodyHash', 'PhysicalHash'):
            if prefix + key in expected:
                assert witness[key] == expected[prefix + key], key
        receipts = [row for row in ledgers['commands'] if row[1].hex() == expected['CommandId']]
        if prefix == 'Post':
            assert len(receipts) == 1 and number(receipts[0][2]) == expected['PostRevision']
            assert receipts[0][3] == action_bytes and receipts[0][4] == bytes.fromhex(expected['ReceiptHex'])
        else:
            assert not receipts
        if reference is not None:
            assert ledgers == reference
        report['independentStates'].append(dict(database=str(database), **witness,
            LedgerCounts={name: len(rows) for name, rows in ledgers.items()},
            LedgerHashes={name: ledger_hash(rows) for name, rows in ledgers.items()}))
        return witness, ledgers

    def check_report(value, witness):
        assert value['ProfileVersion'] == witness['ProfileVersion'] and value['StateHash'] == witness['StateHash']
        assert value['StructuralPolicy'] == witness['StructuralPolicy']
        assert value['StructuralSettlementPolicy'] == witness['StructuralSettlementPolicy']
        physical = value['ConstructionPhysical']
        assert physical['Revision'] == witness['PhysicalRevision'] and physical['ContentHash'] == witness['PhysicalHash']
        assert physical['ConstructionHash'] == witness['ConstructionHash'] and len(physical['Members']) == len(witness['Members'])
        for actual, retained_member in zip(physical['Members'], witness['Members']):
            for key, content in retained_member.items():
                if key == 'Displacement':
                    same_position(actual[key], content)
                else:
                    assert actual[key] == content, key
        if 'Actors' in value:
            assert {actor['ActorId'] for actor in value['Actors']} == set(witness['Actors'])
            for actor in value['Actors']:
                retained_actor = witness['Actors'][actor['ActorId']]
                same_position(actor['Position'], retained_actor['Feet'])
                assert actor['PresenceRevision'] == retained_actor['Revision']
            assert value['AdvancingSteps'] == witness['AdvancingSteps']
            assert value['Calendar']['Days'] * 86_400_000 + value['Calendar']['MillisecondsWithinDay'] == witness['CalendarMilliseconds']

    def history(database, item, prefix):
        value = run(f'{database.stem} history {item["Revision"]}', '--inspect-recorded-world', database, item['Revision'])
        assert value['Contract'] == 'recorded-world-state-v1' and value['ProfileVersion'] == 15
        assert value['RecordedRevision'] == item['Revision'] and value['StateHash'] == item['Hash']
        assert value['SourceHeadRevision'] == expected[prefix + 'Revision'] and value['SourceHeadHash'] == expected[prefix + 'Hash']
        assert value['StructuralPolicy'] == genesis['StructuralPolicy']
        assert value['StructuralSettlementPolicy'] == genesis['StructuralSettlementPolicy']
        if item['Revision'] in retained:
            check_report(value, retained[item['Revision']])

    def history_and_retry(database):
        before_hash = digest(database.read_bytes())
        for item in reversed(expected['History']):
            history(database, item, 'Post')
        run(f'{database.stem} refuse committed settlement retry', '--execute-world-step', database, step_path, exit_code=1)
        inspect_database(database, 'Post', after_ledgers)
        assert digest(database.read_bytes()) == before_hash
        return before_hash

    try:
        work.mkdir()
        expected = json.loads((fixture / 'expected.json').read_text())
        required = ['WorldId', 'BranchId', 'ProfileVersion', 'PreRevision', 'PreHash', 'PostRevision', 'PostHash',
                    'CommandId', 'ReceiptHex', 'Player', 'Builder', 'SiteId', 'SourceId', 'TerrainSourceId', 'DestinationCustodianId',
                    'StructureId', 'MemberIndex', 'ElementId', 'PolicyHash', 'SettlementPolicyHash', 'History',
                    'PrePlayerFeet', 'PostPlayerFeet', 'PreBuilderFeet', 'PostBuilderFeet', 'PreWork', 'PostWork',
                    'PreMemberRevision', 'PostMemberRevision', 'PreDisplacement', 'PostDisplacement',
                    'PhysicalBeforeRevision', 'PhysicalAfterRevision', 'LotId', 'Quantity']
        missing = [key for key in required if key not in expected]
        report['unresolvedExpectations'] = ['Missing fixture field: ' + key for key in missing]
        assert not missing, missing
        assert expected['ProfileVersion'] == 15 and expected['PostRevision'] == expected['PreRevision'] + 1
        assert expected['Player'] != expected['Builder']
        assert 3 <= len(expected['History']) <= 16
        report['fixtureSha256'] = {path.name: digest(path.read_bytes()) for path in sorted(fixture.iterdir()) if path.is_file()}
        assert not any(name.endswith(('-wal', '-shm', '-journal')) for name in report['fixtureSha256']), 'Fixture must be closed.'
        genesis_bytes = (fixture / 'genesis.bin').read_bytes()
        step_path = fixture / 'settlement-step.bin'
        step_bytes = step_path.read_bytes()
        step = read_frame(step_bytes, 'world.step-batch', 10)
        assert number(step[1]) == 15 and number(step[5]) == expected['PreRevision'] and number(step[7]) == 1
        assert step[3].hex() == expected['WorldId'] and step[4].hex() == expected['BranchId']
        movement = read_frame(read_frame(step[8], 'world.step-movement', 2)[1], 'world.control-sample', 6)
        assert movement[3].hex() == expected['Builder']
        assert number(movement[4], True) == 32 and number(movement[5], True) == 0
        action_bytes = read_frame(step[9], 'world.step-actions', 2)[1]
        action = read_frame(action_bytes, 'world.actor-action', 10)
        intent = read_frame(action[9], 'world.action.excavate', 8)
        assert number(action[1]) == 8 and number(intent[1]) == 1
        assert action[2].hex() == expected['CommandId'] and action[5].hex() == expected['Player']
        assert action[6:8] == step[3:5] and intent[2].hex() == expected['SiteId']
        report.update(independentStepHash=digest(step_bytes), independentActionHash=digest(action_bytes))
        before, before_ledgers = inspect_database(fixture / 'before-settlement.sqlite', 'Pre')
        after, after_ledgers = inspect_database(fixture / 'after-settlement.sqlite', 'Post')
        delivery = dict(PrincipalId=movement[1].hex(), SessionId=movement[2].hex())
        assert delivery == before['Controls'][expected['Builder']]
        report['suppressedMovement'] = dict(**delivery, ActorId=expected['Builder'], DeltaX=32, DeltaY=0,
            PreFeet=before['Actors'][expected['Builder']]['Feet'], PostFeet=after['Actors'][expected['Builder']]['Feet'])
        genesis = state_witness(genesis_bytes, expected)
        assert genesis['ProfileVersion'] == 15 and genesis['Revision'] == genesis['AdvancingSteps'] == 0
        assert genesis['StructuralPolicy'] == before['StructuralPolicy'] == after['StructuralPolicy']
        assert genesis['StructuralSettlementPolicy'] == before['StructuralSettlementPolicy'] == after['StructuralSettlementPolicy']
        assert number(step[6]) == before['AdvancingSteps'] and after['AdvancingSteps'] == before['AdvancingSteps'] + 1
        assert after['CalendarMilliseconds'] == before['CalendarMilliseconds'] + 1800
        assert number(intent[3]) == before['CompletedWork'] and number(action[8]) == before['Actors'][expected['Player']]['Revision']
        assert after['CompletedWork'] == before['CompletedWork'] + 1 == after['RequiredWork']
        assert not before['IsExcavated'] and after['IsExcavated']
        for key in ('ConstructionHash', 'ProductionHash', 'SourcesHash', 'Installed', 'MaterialPin', 'YieldQuantity'):
            assert before[key] == after[key], key
        assert before['Installed']['StructureId'] == expected['StructureId']
        assert before['PhysicalRevision'] + 1 == after['PhysicalRevision']
        assert before['PhysicalRevision'] == expected['PhysicalBeforeRevision']
        assert after['PhysicalRevision'] == expected['PhysicalAfterRevision']
        assert before['CompletedWork'] == expected['PreWork'] and after['CompletedWork'] == expected['PostWork']
        for prefix, witness in (('Pre', before), ('Post', after)):
            for name in ('Player', 'Builder'):
                same_position(witness['Actors'][expected[name]]['Feet'], expected[prefix + name + 'Feet'])
            members = [row for row in witness['Members'] if row['ElementId'] == expected['ElementId']]
            assert len(members) == 1 and members[0]['Revision'] == expected[prefix + 'MemberRevision']
            same_position(members[0]['Displacement'], expected[prefix + 'Displacement'])
        assert before['Region'] == after['Region'] and before['Index'] == after['Index']
        for index, (old, new) in enumerate(zip(before['Regions'], after['Regions'])):
            assert old['BaseHash'] == new['BaseHash']
            if index == before['Region']:
                assert number(intent[4]) == old['Revision'] and new['Revision'] == old['Revision'] + 1
                assert new['Edits'] == sorted(old['Edits'] + [before['Index']])
            else:
                assert old == new
        assert len(before['Regions']) == len(after['Regions'])
        spawned = hashlib.sha256(frame('entity.spawned', action[2], (0).to_bytes(8, 'big'))).digest()[:16].hex()
        assert not before['Issued'] and len(after['Issued']) == 1
        issued = after['Issued'][0]
        assert issued == dict(LotId=spawned, CustodianId=expected['DestinationCustodianId'], DefinitionPin=after['MaterialPin'],
            SourceId=expected['SourceId'], Start=0, End=after['YieldQuantity'], Revision=0, Protection=0, ReservedBy='')
        assert spawned == expected['LotId'] and after['YieldQuantity'] == expected['Quantity']
        assert after['SourceLotPins'] == {**before['SourceLotPins'], spawned: after['SourceLotPins'][spawned]}
        assert spawned not in before['SourceLotPins']
        assert after['CustodyRevision'] == before['CustodyRevision'] + 1
        assert set(before['Storages']) == set(after['Storages'])
        for custodian, old in before['Storages'].items():
            new = after['Storages'][custodian]
            assert old['UnitCapacity'] == new['UnitCapacity']
            assert new['Revision'] == old['Revision'] + (custodian == expected['DestinationCustodianId'])
        assert number(intent[6]) == before['SourceLotRevisions'][intent[5].hex()]
        assert number(intent[7]) == before['Storages'][expected['DestinationCustodianId']]['Revision']
        report['independentIssuedLotId'] = spawned
        assert len(before['Members']) == len(after['Members']) > 0
        moved_members = []
        settlement = after['StructuralSettlementPolicy']
        for old, new in zip(before['Members'], after['Members']):
            for key in ('ElementId', 'StructureId', 'MemberIndex', 'ConditionParts', 'InstalledLotId', 'MassMilligrams', 'VolumeMicrolitres'):
                assert old[key] == new[key], key
            if old['Displacement'] != new['Displacement']:
                assert new['Revision'] == old['Revision'] + 1 and new['Disposition'] == 'Resting'
                assert old['Displacement']['X'] == new['Displacement']['X'] and old['Displacement']['Y'] == new['Displacement']['Y']
                drop = old['Displacement']['Z'] - new['Displacement']['Z']
                assert 0 < drop <= settlement['SafeMemberDropSubunits'] <= settlement['MaximumDropSubunits']
                flight = flight_milliseconds(drop, settlement['GravityMillimetresPerSecondSquared'])
                assert 0 < flight <= 1800
                moved_members.append(dict(ElementId=new['ElementId'], DropSubunits=drop, FlightMilliseconds=flight))
            else:
                assert old == new
        selected_member = member_identity(expected['StructureId'], expected['MemberIndex'])
        assert selected_member == expected['ElementId'] and selected_member in {value['ElementId'] for value in moved_members}
        rider = expected['Builder']
        assert set(before['Actors']) == set(after['Actors'])
        for actor, old in before['Actors'].items():
            new = after['Actors'][actor]
            assert old['BindingHash'] == new['BindingHash'] and old['ConditionHash'] == new['ConditionHash']
            debit = 400 if actor == expected['Player'] else 0
            assert new['Effort'] == min(1000, old['Effort'] - debit + 10)
            assert after['Residents'][actor]['ExperiencedMilliseconds'] == before['Residents'][actor]['ExperiencedMilliseconds'] + 1800
            if actor == rider:
                assert old['Feet']['X'] == new['Feet']['X'] and old['Feet']['Y'] == new['Feet']['Y']
                drop = old['Feet']['Z'] - new['Feet']['Z']
                assert drop == 256 and drop <= settlement['SafeActorDropSubunits'] and new['Revision'] == old['Revision'] + 1
                assert drop == next(value['DropSubunits'] for value in moved_members if value['ElementId'] == selected_member)
            else:
                assert old['Feet'] == new['Feet'] and old['Revision'] == new['Revision']
        report['settledMembers'] = moved_members
        receipt_bytes = bytes.fromhex(expected['ReceiptHex'])
        receipt = read_frame(receipt_bytes, 'world.action-receipt', 10)
        assert number(receipt[1]) == 1 and receipt[2] == action[2] and receipt[3] == action[3]
        assert receipt[4:6] == action[6:8] and receipt[6].hex() == digest(action_bytes)
        assert number(receipt[7]) == expected['PostRevision'] and number(receipt[8]) == 1 and number(receipt[9]) == 0
        new_ids = set(after_ledgers['identities']) - set(before_ledgers['identities'])
        assert set(before_ledgers['identities']).issubset(after_ledgers['identities'])
        expected_ids = {(step[2], 1), (action[2], 2), (hashlib.sha256(receipt_bytes).digest()[:16], 3), (bytes.fromhex(spawned), 4)}
        assert len(expected_ids) == 4 and {(row[0], row[2]) for row in new_ids} == expected_ids
        assert all(number(row[1]) == expected['PostRevision'] for row in new_ids)
        member_rows = []
        for member in after['Members']:
            rows = [row for row in after_ledgers['identities'] if row[0].hex() == member['ElementId']]
            assert len(rows) == 1 and rows[0][2] == 4 and number(rows[0][1]) <= expected['PreRevision']
            assert rows[0] in before_ledgers['identities']
            member_rows.append(dict(ElementId=member['ElementId'], Sequence=number(rows[0][1]), Kind=rows[0][2]))
        report['retainedMemberIdentityRows'] = member_rows
        assert len(after_ledgers['commands']) == len(before_ledgers['commands']) + 1
        assert len(after_ledgers['journal']) == len(before_ledgers['journal']) + 1
        assert set(before_ledgers['commands']).issubset(after_ledgers['commands'])
        assert after_ledgers['journal'][:-1] == before_ledgers['journal']
        journal = after_ledgers['journal'][-1]
        assert number(journal[0]) == expected['PostRevision'] and journal[1] == 1 and journal[2] == step[2]
        assert number(journal[3]) == after['AdvancingSteps'] and journal[4] == step_bytes
        assert read_frame(journal[5], 'world.action-outcomes', 2)[1] == receipt_bytes
        assert journal[6].hex() == before['StateHash'] and journal[7].hex() == after['StateHash']
        retained = {0: genesis, expected['PreRevision']: before, expected['PostRevision']: after}
        history_pins = {item['Revision']: item['Hash'] for item in expected['History']}
        assert len(history_pins) == len(expected['History'])
        assert all(history_pins[revision] == witness['StateHash'] for revision, witness in retained.items())
        report['genesisWitness'] = genesis

        normal = work / 'normal.sqlite'
        shutil.copyfile(fixture / 'before-settlement.sqlite', normal)
        history(normal, dict(Revision=expected['PreRevision'], Hash=expected['PreHash']), 'Pre')
        value = run('ordinary final excavation and structural settlement', '--execute-world-step', normal, step_path)
        assert value['Revision'] == expected['PostRevision']
        check_report(value, after)
        inspect_database(normal, 'Post', after_ledgers)
        report['ordinaryReadOnlyDatabaseSha256'] = history_and_retry(normal)
        for boundary in ('before', 'after'):
            database = work / (boundary + '.sqlite')
            shutil.copyfile(fixture / 'before-settlement.sqlite', database)
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
                value = run('retry rolled-back settlement', '--execute-world-step', database, step_path)
                assert value['Revision'] == expected['PostRevision']
                check_report(value, after)
            inspect_database(database, 'Post', after_ledgers)
            crash['readOnlyDatabaseSha256'] = history_and_retry(database)
            print(f'{boundary}-commit settlement kill and history: passed', flush=True)
        if (fixture / 'legacy-expected.json').is_file():
            control = json.loads((fixture / 'legacy-expected.json').read_text())
            check_old14(control, fixture, work, run, read_database, expected, report)
            expected_calls = 3 * len(expected['History']) + 10 + len(control['History']) + 2
        else:
            report['unresolvedExpectations'].append('Optional profile-14 refusal control was not supplied.')
            expected_calls = 3 * len(expected['History']) + 10
        report['expectedHostInvocationCount'] = expected_calls
        assert len(report['runs']) + len(report['crashes']) == expected_calls
        assert len(report['crashes']) == 2 and all(value['exitCode'] < 0 for value in report['crashes'])
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
        report.update(finishedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                      hostInvocationCount=len(report['runs']) + len(report['crashes']),
                      actualProcessKillCount=sum(value.get('exitCode', 0) < 0 for value in report['crashes']),
                      unresolvedExpectationCount=len(report['unresolvedExpectations']),
                      unqualifiedClaimCount=len(report['unqualifiedClaims']))
        output.write_text(json.dumps(report, indent=2) + '\n')
    print('structural settlement: ' + report['status'], flush=True)
    return 0 if report['status'] == 'passed' else 1


def check_old14(control, fixture, work, run, read_database, expected, report):
    """The old interpreter may reject an action durably; it must never publish settlement."""
    source = fixture / 'legacy-before.sqlite'
    step = fixture / 'legacy-step.bin'
    assert control['ProfileVersion'] == 14 and 3 <= len(control['History']) <= 16
    database = work / 'old14.sqlite'
    shutil.copyfile(source, database)
    pre_bytes, genesis, pre_ledgers = read_database(database)
    before = state_witness(pre_bytes, expected)
    assert before['ProfileVersion'] == 14 and before['StateHash'] == control['PreHash']
    assert genesis == (fixture / 'legacy-genesis.bin').read_bytes()
    assert before['Revision'] == control['PreRevision']
    value = run('profile-14 retains unsupported-excavation refusal', '--execute-world-step', database, step)
    post_bytes, retained_genesis, post_ledgers = read_database(database)
    after = state_witness(post_bytes, expected)
    assert genesis == retained_genesis and after['ProfileVersion'] == 14
    assert after['StateHash'] == control['PostHash'] == value['StateHash']
    assert after['Revision'] == control['PostRevision']
    reference_bytes, reference_genesis, reference_ledgers = read_database(fixture / 'legacy-after.sqlite')
    assert post_bytes == reference_bytes and retained_genesis == reference_genesis and post_ledgers == reference_ledgers
    assert value['StructuralSettlementPolicy'] is None and value['StructuralPolicy'] == before['StructuralPolicy']
    assert after['Revision'] == before['Revision'] + 1 and after['AdvancingSteps'] == before['AdvancingSteps'] + 1
    assert after['CalendarMilliseconds'] == before['CalendarMilliseconds'] + 1800
    for key in ('ConstructionHash', 'ProductionHash', 'InventoryHash', 'SourcesHash', 'PhysicalHash',
                'Regions', 'ExcavationHash', 'Issued', 'Members', 'StructuralPolicy'):
        assert before[key] == after[key], key
    for actor, old in before['Actors'].items():
        new = after['Actors'][actor]
        assert old['Feet'] == new['Feet'] and old['Revision'] == new['Revision']
        assert old['ConditionHash'] == new['ConditionHash'] and new['Effort'] == min(1000, old['Effort'] + 10)
    step_fields = read_frame(step.read_bytes(), 'world.step-batch', 10)
    assert number(step_fields[1]) == 14 and number(step_fields[7]) == 1
    action = read_frame(step_fields[9], 'world.step-actions', 2)[1]
    command_id = read_frame(action, 'world.actor-action', 10)[2]
    receipts = [row for row in post_ledgers['commands'] if row[1] == command_id]
    assert len(receipts) == 1 and receipts[0][3] == action
    assert command_id.hex() == control['CommandId'] and receipts[0][4] == bytes.fromhex(control['ReceiptHex'])
    receipt = read_frame(receipts[0][4], 'world.action-receipt', 10)
    assert number(receipt[8]) == 2 and number(receipt[9]) == 2
    assert len(post_ledgers['commands']) == len(pre_ledgers['commands']) + 1
    assert len(post_ledgers['journal']) == len(pre_ledgers['journal']) + 1
    new_ids = set(post_ledgers['identities']) - set(pre_ledgers['identities'])
    assert len(new_ids) == 3 and {row[2] for row in new_ids} == {1, 2, 3}
    unchanged = digest(database.read_bytes())
    for item in reversed(control['History']):
        revision = item['Revision']
        value = run(f'profile-14 history {revision}', '--inspect-recorded-world', database, revision)
        assert value['ProfileVersion'] == 14 and value['StateHash'] == item['Hash']
        assert value['SourceHeadHash'] == after['StateHash'] and value['SourceHeadRevision'] == after['Revision']
        assert value['StructuralSettlementPolicy'] is None
    run('profile-14 rejected-action retry remains occupied', '--execute-world-step', database, step, exit_code=1)
    assert digest(database.read_bytes()) == unchanged
    assert read_database(database)[2] == post_ledgers
    report['old14Control'] = dict(status='passed', Before=before, After=after,
                                  readOnlyDatabaseSha256=unchanged, ReceiptHex=receipts[0][4].hex())


if __name__ == '__main__':
    raise SystemExit(main())
