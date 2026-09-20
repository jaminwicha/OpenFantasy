"""Qualify read-only event descent on an exact funded historical world.

Independent retained-byte, material-mass and integer-impact checks accompany actual
Host inspections. This probe performs no world writes, live falls or crash tests.
"""
import argparse
from contextlib import closing
import datetime
import json
import math
import os
from pathlib import Path
import shutil
import sqlite3
import struct
import subprocess
import time
import traceback
import uuid

from check_construction_physical import frame, read_frame
from check_live_excavation import digest, number
from check_structural_settlement import state_witness


def uint(value):
    return value.to_bytes(8, 'big')


def full_position(value):
    coordinates = {axis: value[axis] for axis in ('X', 'Y', 'Z')}
    return dict(**coordinates, Cell={axis: coordinate // 256 for axis, coordinate in coordinates.items()})


def read_database(path):
    with closing(sqlite3.connect(path.as_uri() + '?mode=ro&immutable=1', uri=True)) as connection:
        assert connection.execute('PRAGMA user_version').fetchone()[0] == 4
        head = connection.execute('SELECT sequence,state,hash FROM world_head').fetchone()
        genesis = connection.execute('SELECT state,hash FROM world_genesis').fetchone()
        assert digest(head[1]) == head[2].hex()
        assert number(head[0]) == number(read_frame(head[1], 'world.snapshot')[8])
        assert digest(genesis[0]) == genesis[1].hex()
        counts = {table: connection.execute('SELECT COUNT(*) FROM ' + table).fetchone()[0]
                  for table in ('world_commits', 'world_command_outcomes', 'world_occupied_identity')}
    return head[1], genesis[0], counts


def actor_masses(root):
    """Actual body + carried raw/transformed goods, excluding installed backing."""
    authority = read_frame(root[2], 'authority.counted-state', 9)
    custody = read_frame(authority[3], 'custody.snapshot.v1', 6)
    production = read_frame(root[12], 'production.snapshot', 8)
    construction = read_frame(root[15], 'construction.snapshot', 7)
    policy = read_frame(root[18], 'construction.load-policy.v1', 6)
    bodies = {(row[1], row[2]): number(row[3]) for row in (
        read_frame(value, 'structure.body-mass', 4)
        for value in read_frame(policy[4], 'structure.policy-bodies')[1:])}
    items = {(row[1], row[2]): number(row[3]) for row in (
        read_frame(value, 'structure.item-mass', 4)
        for value in read_frame(policy[5], 'structure.policy-items')[1:])}
    for encoded in read_frame(production[5], 'production.recipes')[1:]:
        recipe = read_frame(encoded, 'craft.recipe', 6)
        physical = read_frame(recipe[3], 'production.recipe', 9)
        materials = {}
        for value in read_frame(physical[7], 'production.material-catalog')[2:]:
            material = read_frame(value, 'production.material', 8)
            materials[(material[3], bytes.fromhex(digest(value)))] = number(material[5])
        for value in read_frame(recipe[4], 'craft.bindings')[1:]:
            binding = read_frame(value, 'craft.item-physical', 6)
            mass = materials[tuple(binding[4:6])]
            assert mass > 0 and items.setdefault(tuple(binding[2:4]), mass) == mass
    residents = [read_frame(value, 'world.resident', 13)
                 for value in read_frame(root[4], 'world.residents')[1:]]
    carried = {row[1]: {row[2]} for row in residents}
    installed = set()
    for value in read_frame(construction[6], 'construction.projects')[1:]:
        project = read_frame(value, 'construction.project', 17)
        assert number(project[16]) == 1
        carried[project[6]].add(project[8])
        if project[15]:
            installed.add(read_frame(project[15], 'construction.installed', 6)[2])
    spatial = read_frame(root[3], 'world.spatial', 8)
    result = {}
    for value in read_frame(spatial[7], 'world.presences')[1:]:
        presence = read_frame(value, 'world.presence', 9)
        binding = read_frame(presence[2], 'actor.form-binding', 9)
        actor = binding[2]
        mass = bodies[tuple(binding[4:6])]
        for encoded in read_frame(custody[5], 'custody.lots.v1')[1:]:
            lot = read_frame(encoded, 'custody.source-lot.v1', 11)
            if lot[2] in carried[actor]:
                mass += items[tuple(lot[3:5])] * (number(lot[7]) - number(lot[6]))
        for encoded in read_frame(production[7], 'production.lots')[1:]:
            lot = read_frame(encoded, 'production.lot', 10)
            if lot[3] in carried[actor] and lot[2] not in installed:
                claim = read_frame(lot[6], 'production.output-claim', 6)
                mass += items[tuple(lot[4:6])] * (number(claim[5]) - number(claim[4]))
        assert 0 < mass <= (1 << 64) - 1
        result[actor.hex()] = mass
    return result


def fixture_witness(fixture, expected, basis):
    root = read_frame(basis, 'world.snapshot', 23)
    assert number(root[1]) == expected['ProfileVersion'] == 17 and not root[21]
    assert number(root[8]) == expected['SourceRevision'] and digest(basis) == expected['SourceHash']
    spatial = read_frame(root[3], 'world.spatial', 8)
    assert number(spatial[1]) == 4
    sites = [read_frame(value, 'terrain.excavation-site.v1', 12)
             for value in read_frame(root[16], 'terrain.excavation.v1')[1:]]
    site = [row for row in sites if row[1].hex() == expected['SiteId']]
    assert len(site) == 1
    site = site[0]
    assert number(site[10]) == 0
    selector = dict(expected, SourceId=site[4].hex(), Player=site[5].hex(), TerrainSourceId=site[3].hex())
    witness = state_witness(basis, selector)
    assert not witness['Issued'] and not witness['IsExcavated']
    assert len(witness['Installed']) == 2 and len(witness['Members']) == 3
    for key, index in (('PolicyHash', 18), ('SettlementPolicyHash', 19), ('CoupledPolicyHash', 22)):
        assert expected[key] == digest(root[index])
    target = struct.unpack('>qqq', site[2])
    originals = read_frame(spatial[5], 'world.regions')[1:]
    candidates = [fixture / ('candidate-region-' + str(index) + '.bin') for index in range(len(originals))]
    assert set(fixture.glob('candidate-region-*.bin')) == set(candidates)
    assert [digest(path.read_bytes()) for path in candidates] == expected['CandidateRegionHashes']
    changed = 0
    for original, path in zip(originals, candidates):
        previous = read_frame(original, 'terrain.region.v1', 7)
        proposed = read_frame(path.read_bytes(), 'terrain.region.v1', 7)
        assert previous[1:5] == proposed[1:5]
        bounds = read_frame(previous[4], 'terrain.bounds.v1', 7)
        low, size = [number(value, True) for value in bounds[1:4]], [number(value) for value in bounds[4:7]]
        if all(origin <= coordinate < origin + length for origin, coordinate, length in zip(low, target, size)):
            local = [coordinate - origin for origin, coordinate in zip(low, target)]
            index = (local[2] * size[1] + local[1]) * size[0] + local[0]
            old_edits = [item[0] for item in struct.iter_unpack('>I', previous[6])]
            assert index not in old_edits
            assert proposed[6] == b''.join(struct.pack('>I', value) for value in sorted(old_edits + [index]))
            assert number(proposed[5]) == number(previous[5]) + 1
            changed += 1
        else:
            assert original == path.read_bytes()
    assert changed == 1
    cursor_bytes = (fixture / 'complete-cursor.bin').read_bytes()
    assert len(cursor_bytes) <= 512
    cursor = read_frame(cursor_bytes, 'construction.descent-cursor.v1', 4)
    assert cursor[1].hex() == expected['InputPin'] and len(cursor[1]) == 32
    assert number(cursor[2]) == expected['CompletedWorkUnits'] and number(cursor[3]) == 1
    assert frame('construction.descent-cursor.v1', *cursor[1:]) == cursor_bytes
    witness['ActorMasses'] = actor_masses(root)
    return root, witness


def verify_assessment(actual, expected, root, witness):
    assert actual['Contract'] == 'construction-descent-assessment-v1' and actual['PublicationAuthority'] is False
    for key in ('WorldId', 'BranchId', 'SiteId', 'InputPin', 'PolicyHash', 'SettlementPolicyHash', 'CoupledPolicyHash',
                'CompletedWorkUnits', 'MaximumTotalWorkUnits', 'IsComplete', 'IsFailed'):
        assert actual[key] == expected[key], key
    assert actual['IsComplete'] is True and actual['IsFailed'] is False and actual['FailureReason'] is None
    assert actual['SourceHeadRevision'] == actual['RecordedRevision'] == expected['SourceRevision']
    assert actual['SourceHeadHash'] == actual['StateHash'] == expected['SourceHash']
    assert actual['ImpactDemands'] == expected['ImpactDemands']
    assert 0 < actual['CompletedWorkUnits'] <= actual['MaximumTotalWorkUnits']
    demands = {row['BodyId']: row for row in actual['ImpactDemands']}
    assert len(demands) == len(actual['ImpactDemands']) == 3
    structures = {member['StructureId'] for member in witness['Members']}
    assert {identity for identity, row in demands.items() if not row['IsActor']} == structures
    masses = {structure: sum(member['MassMilligrams'] for member in witness['Members']
                             if member['StructureId'] == structure) for structure in structures}
    masses.update(witness['ActorMasses'])
    policy = witness['StructuralSettlementPolicy']
    gravity = policy['GravityMillimetresPerSecondSquared']
    for identity, row in demands.items():
        assert row['MassMilligrams'] == masses[identity] and row['DropSubunits'] == 256
        assert row['EnergyDenominator'] == 256
        assert int(row['EnergyNumeratorPicojoules']) == masses[identity] * gravity * row['DropSubunits'] * 1000
        numerator, denominator = row['DropSubunits'] * 2 * 1000 * 1_000_000, gravity * 256
        milliseconds = math.isqrt(numerator // denominator)
        milliseconds += denominator * milliseconds * milliseconds < numerator
        assert row['FlightMilliseconds'] == milliseconds
        assert row['IsHarmless'] == (row['DropSubunits'] <= policy[
            'SafeActorDropSubunits' if row['IsActor'] else 'SafeMemberDropSubunits']) is True
    events = actual['Events']
    event_ids = [identity for event in events for identity in event['BodyIds']]
    assert sorted(event_ids) == sorted(demands) and all(event['DropSubunits'] == 256 for event in events)
    assert all(event['BodyIds'] == sorted(set(event['BodyIds'])) for event in events)

    physical = read_frame(root[17], 'construction.physical.v1', 6)
    translated = []
    expected_members = []
    for encoded, member in zip(read_frame(physical[5], 'construction.physical.members.v1')[1:], witness['Members']):
        row = read_frame(encoded, 'construction.physical.member.v1', 9)
        drop = demands[member['StructureId']]['DropSubunits']
        displacement = dict(member['Displacement'], Z=member['Displacement']['Z'] - drop)
        expected_members.append(dict(member, Revision=member['Revision'] + 1, Disposition='Resting',
                                     Displacement=full_position(displacement)))
        translated.append(frame('construction.physical.member.v1', row[1], row[2], uint(number(row[3]) + 1),
                                row[4], row[5], row[6], (number(row[7], True) - drop).to_bytes(8, 'big', signed=True), uint(3)))
    translated_bytes = frame('construction.physical.v1', *physical[1:4], uint(number(physical[4]) + 1),
                             frame('construction.physical.members.v1', *translated))
    candidate = actual['CandidatePhysical']
    assert candidate == dict(Revision=witness['PhysicalRevision'] + 1, ContentHash=digest(translated_bytes),
                             ConstructionHash=witness['ConstructionHash'], Members=expected_members)
    assert candidate['ContentHash'] == expected['ResultPhysicalHash']
    actors = {row['ActorId']: row for row in actual['CandidateActors']}
    expected_actors = {row['ActorId']: row for row in expected['ResultActors']}
    assert len(actors) == len(actual['CandidateActors']) and set(actors) == set(witness['Actors']) == set(expected_actors)
    for identity, before in witness['Actors'].items():
        drop = demands.get(identity, {}).get('DropSubunits', 0)
        after = actors[identity]
        assert after == dict(ActorId=identity, Feet=full_position(dict(before['Feet'], Z=before['Feet']['Z'] - drop)),
                             Revision=before['Revision'] + (drop > 0), ConditionHash=before['ConditionHash'])
        assert after['Feet'] == expected_actors[identity]['Feet'] and after['Revision'] == expected_actors[identity]['Revision']
        assert expected_actors[identity]['BindingHash'] == before['BindingHash']
        assert expected_actors[identity]['ConditionHash'] == before['ConditionHash']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('dotnet', 'fixture', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    runtime = Path(__file__).resolve().parents[1]
    dotnet, fixture, output = args.dotnet.resolve(), args.fixture.resolve(), args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    work = output.parent / ('event-descent-' + uuid.uuid4().hex)
    work.mkdir()
    command = [str(dotnet), str(runtime / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll')]
    env = dict(os.environ, DOTNET_ROOT=str(dotnet.parent))
    report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope='Read-only funded historical event descent and exact impact demands',
                  workDirectory=str(work), runs=[], unqualifiedClaims=[
                      'Live publication or policy mutation', 'Crash recovery or commit boundaries',
                      'Harmful response, fracture, debris, or full-game simulation', 'Completed-site control fixture'])
    selected = None

    def run(name, *arguments, exit_code=0):
        selected = command + [str(value) for value in arguments]
        record = dict(name=name, command=selected, timeoutSeconds=30)
        report['runs'].append(record)
        started = time.monotonic()
        try:
            result = subprocess.run(selected, cwd=runtime, env=env, capture_output=True, text=True, timeout=30)
        except subprocess.TimeoutExpired as error:
            def retained(value):
                return value.decode(errors='replace') if isinstance(value, bytes) else value or ''
            record.update(timedOut=True, stdout=retained(error.stdout), stderr=retained(error.stderr))
            raise
        finally:
            record['elapsedSeconds'] = time.monotonic() - started
        record.update(exitCode=result.returncode, stdout=result.stdout, stderr=result.stderr)
        assert result.returncode == exit_code and not result.stderr, name
        if exit_code:
            assert result.stdout.startswith('Inspection failed:'), name
            return None
        return json.loads(result.stdout)

    try:
        fixture_hashes = {path.name: digest(path.read_bytes()) for path in sorted(fixture.iterdir()) if path.is_file()}
        report['fixtureSha256'] = fixture_hashes
        expected = json.loads((fixture / 'expected.json').read_text())
        basis = (fixture / 'basis.bin').read_bytes()
        source = fixture / 'basis.sqlite'
        assert not Path(str(source) + '-wal').exists() and not Path(str(source) + '-shm').exists()
        selected = work / 'assessment.sqlite'
        shutil.copyfile(source, selected)
        before_hash = digest(selected.read_bytes())
        report['databaseSha256Before'] = before_hash
        head, genesis, ledger_counts = read_database(selected)
        assert head == basis
        root, witness = fixture_witness(fixture, expected, basis)
        report['independentWitness'] = dict(StateHash=digest(basis), SourceRevision=number(root[8]),
            OriginalPhysicalHash=witness['PhysicalHash'], Backing=witness['Installed'], Members=witness['Members'],
            ActorMasses=witness['ActorMasses'], LedgerCounts=ledger_counts,
            InventoryHash=witness['InventoryHash'], ProductionHash=witness['ProductionHash'],
            SourcesHash=witness['SourcesHash'], PolicyHash=expected['PolicyHash'])
        revision, site = expected['SourceRevision'], expected['SiteId']
        first = run('assess-selected', '--inspect-construction-descent', selected, revision, site)
        verify_assessment(first, expected, root, witness)
        assert run('assess-selected-repeat', '--inspect-construction-descent', selected, revision, site) == first
        recorded = run('recorded-selected', '--inspect-recorded-world', selected, revision)
        assert recorded['SourceHeadRevision'] == recorded['RecordedRevision'] == revision
        assert recorded['SourceHeadHash'] == recorded['StateHash'] == expected['SourceHash']
        assert recorded['ConstructionPhysical']['ContentHash'] == witness['PhysicalHash']

        # Earlier history is a distinct read-only proposal: the unbuilt world has no falling
        # assemblies or riders. It cannot inherit the later head's material or impact demands.
        earlier = run('assess-unbuilt-genesis', '--inspect-construction-descent', selected, 0, site)
        assert earlier['RecordedRevision'] == 0 and earlier['StateHash'] == digest(genesis)
        assert earlier['SourceHeadRevision'] == revision and earlier['SourceHeadHash'] == expected['SourceHash']
        assert earlier['PublicationAuthority'] is False and earlier['IsComplete'] is True and earlier['IsFailed'] is False
        assert earlier['ImpactDemands'] == earlier['Events'] == [] and earlier['CandidatePhysical']['Members'] == []
        assert earlier['CandidatePhysical']['ContentHash'] == digest(read_frame(genesis, 'world.snapshot', 23)[17])
        assert earlier['InputPin'] != first['InputPin']
        for key in ('WorldId', 'BranchId', 'SiteId', 'PolicyHash', 'SettlementPolicyHash', 'CoupledPolicyHash'):
            assert earlier[key] == expected[key]
        all_sites = {row[1].hex() for row in (read_frame(value, 'terrain.excavation-site.v1', 12)
                    for value in read_frame(root[16], 'terrain.excavation.v1')[1:])}
        bad_site = 'ffffffffffffffffffffffffffffffff'
        assert bad_site not in all_sites
        run('reject-unknown-site', '--inspect-construction-descent', selected, revision, bad_site, exit_code=1)
        run('reject-future-revision', '--inspect-construction-descent', selected, revision + 1, site, exit_code=1)
        run('reject-negative-revision', '--inspect-construction-descent', selected, '-1', site, exit_code=1)
        assert run('recorded-selected-final', '--inspect-recorded-world', selected, revision) == recorded
        assert read_database(selected) == (head, genesis, ledger_counts)
        assert digest(selected.read_bytes()) == before_hash
        assert {path.name: digest(path.read_bytes()) for path in sorted(fixture.iterdir()) if path.is_file()} == fixture_hashes
        report.update(status='passed', databaseSha256Before=before_hash, databaseSha256After=digest(selected.read_bytes()),
                      readOnlyBytesUnchanged=True, invocationCount=len(report['runs']), assessment=first)
    except Exception:
        report.update(status='failed', failureTraceback=traceback.format_exc(), invocationCount=len(report['runs']))
    finally:
        if selected is not None and selected.is_file():
            report['databaseSha256After'] = digest(selected.read_bytes())
        if fixture.is_dir():
            report['fixtureSha256After'] = {path.name: digest(path.read_bytes())
                                           for path in sorted(fixture.iterdir()) if path.is_file()}
        report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(dict(status=report['status'], output=str(output), invocations=len(report['runs']))))
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
