"""Exercise a live coupled intact group, saved work, history and six external commit-boundary kills.

Independent canonical-frame and SQLite ledger checks accompany actual Host replay. Uses closed
managed funded-world fixtures, never constructs an alternate database authority or relaxes deadlines.
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
import subprocess
import threading
import time
import traceback
import uuid

from check_construction_physical import frame, ledger_hash, read_frame
from check_live_excavation import digest, number
from check_structural_settlement import state_witness


def database(path):
    with closing(sqlite3.connect(path.as_uri() + '?mode=ro&immutable=1', uri=True)) as connection:
        assert connection.execute('PRAGMA user_version').fetchone()[0] == 4
        head = connection.execute('SELECT sequence,state,hash FROM world_head').fetchone()
        assert digest(head[1]) == head[2].hex() and number(head[0]) == number(read_frame(head[1], 'world.snapshot')[8])
        genesis = connection.execute('SELECT state,hash FROM world_genesis').fetchone()
        assert digest(genesis[0]) == genesis[1].hex()
        ledgers = dict(
            identities=connection.execute('SELECT identity,sequence,kind FROM world_occupied_identity ORDER BY identity').fetchall(),
            commands=connection.execute('SELECT principal,command_id,sequence,command,receipt FROM world_command_outcomes ORDER BY principal,command_id').fetchall(),
            journal=connection.execute('SELECT sequence,kind,cause_id,advancing_steps,accepted_batch,outcomes,before_hash,after_hash FROM world_commits ORDER BY sequence').fetchall())
    return head[1], genesis[0], ledgers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('dotnet', 'fixture', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--configuration', default='Debug')
    args = parser.parse_args()
    runtime = Path(__file__).resolve().parents[1]
    dotnet, fixture, output = args.dotnet.resolve(), args.fixture.resolve(), args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    work = output.parent / ('coupled-settlement-' + uuid.uuid4().hex)
    work.mkdir()
    command = [str(dotnet), str(runtime / 'src/OpenFantasy.Host/bin' / args.configuration / 'net10.0/OpenFantasy.Host.dll')]
    env = dict(os.environ, DOTNET_ROOT=str(dotnet.parent))
    report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  runs=[], crashes=[], independentStates=[], workDirectory=str(work),
                  unqualifiedClaims=['Whole-root CPU bound', 'Unequal trajectories, fracture or harmful impact',
                                     'Rendered or device presentation', 'Production acceptance'])
    originals = {path.name: digest(path.read_bytes()) for path in fixture.iterdir() if path.is_file()}

    def run(name, *arguments, exit_code=0):
        selected = command + [str(value) for value in arguments]
        record = dict(name=name, command=selected)
        report['runs'].append(record)
        started = time.monotonic()
        try:
            result = subprocess.run(selected, cwd=runtime, env=env, capture_output=True, text=True, timeout=30)
        except subprocess.TimeoutExpired as error:
            record.update(timedOut=True, timeoutSeconds=30, elapsedSeconds=time.monotonic() - started,
                          stdout=str(error.stdout or ''), stderr=str(error.stderr or ''))
            raise
        record['elapsedSeconds'] = time.monotonic() - started
        record.update(exitCode=result.returncode, stdout=result.stdout, stderr=result.stderr)
        assert result.returncode == exit_code and not result.stderr, record
        if exit_code == 0:
            return json.loads(result.stdout)
        assert result.stdout.startswith('Inspection failed:')

    def compare(path, reference):
        actual = database(path)
        assert actual == reference, str(path)
        return actual

    try:
        expected = json.loads((fixture / 'expected.json').read_text())
        paths = [fixture / expected['BeforeFile']] + [fixture / name for name in expected['PendingFiles']] + [fixture / expected['AfterFile']]
        batches = [fixture / expected['TriggerFile']] + [fixture / name for name in expected['ContinuationFiles']]
        refs = [database(path) for path in paths]
        assert len(paths) == len(batches) + 1 and len(batches) > 2
        roots = [read_frame(value[0], 'world.snapshot', 23) for value in refs]
        assert all(len(root) == 23 and number(root[1]) == 17 for root in roots)
        assert all(value[1] == (fixture / 'genesis.bin').read_bytes() for value in refs)
        policy = read_frame(roots[0][20], 'world.consequence-policy.v1', 2)
        budget = number(policy[1])
        assert budget == 1 and all(root[20] == roots[0][20] for root in roots)
        coupled = read_frame(roots[0][22], 'construction.coupled-settlement-policy.v1', 2)
        assert number(coupled[1]) == 2 and all(root[22] == roots[0][22] for root in roots)
        before, after = state_witness(refs[0][0], expected), state_witness(refs[-1][0], expected)
        trigger = read_frame(batches[0].read_bytes(), 'world.step-batch', 10)
        action = read_frame(trigger[9], 'world.step-actions', 2)[1]
        action_fields = read_frame(action, 'world.actor-action', 10)
        assert action_fields[2].hex() == expected['CommandId']
        assert not roots[0][21] and not roots[-1][21]
        pin = None
        for index, root in enumerate(roots[1:-1]):
            assert all(value == roots[0][field] for field, value in enumerate(root[:21]) if field != 8)
            job = read_frame(root[21], 'world.pending-excavation.v2', 7)
            assert job[1] == action and job[2] == trigger[2]
            assert number(job[3]) == before['Revision'] and job[4].hex() == before['StateHash']
            assert number(job[5]) == index and number(root[8]) == before['Revision'] + 1 + index
            cursor = read_frame(job[6], 'construction.settlement-resolution.v2', 4)
            assert len(job[6]) <= 512 and number(cursor[2]) == index * budget and number(cursor[3]) == 0
            pin = cursor[1] if pin is None else pin
            assert cursor[1] == pin
        for index, path in enumerate(batches):
            batch = read_frame(path.read_bytes(), 'world.step-batch', 10)
            old, new = refs[index], refs[index + 1]
            assert number(batch[1]) == 17 and number(batch[5]) == number(roots[index][8])
            assert number(batch[6]) == before['AdvancingSteps'] and batch[3:5] == trigger[3:5]
            assert read_frame(batch[8], 'world.step-movement', 1) == [b'world.step-movement']
            assert number(batch[7]) == (1 if index == 0 else 4)
            if index:
                assert read_frame(batch[9], 'world.step-actions', 1) == [b'world.step-actions']
            journal = new[2]['journal'][-1]
            assert new[2]['journal'][:-1] == old[2]['journal']
            assert number(journal[0]) == before['Revision'] + index + 1 and journal[1] == number(batch[7])
            assert journal[2] == batch[2] and journal[4] == path.read_bytes()
            assert journal[6].hex() == digest(old[0]) and journal[7].hex() == digest(new[0])
            assert number(journal[3]) == before['AdvancingSteps'] + (index == len(batches) - 1)
            new_ids = set(new[2]['identities']) - set(old[2]['identities'])
            assert set(old[2]['identities']).issubset(new[2]['identities'])
            ids = {(row[0], row[2]) for row in new_ids}
            terminal = index == len(batches) - 1
            if terminal:
                receipt = bytes.fromhex(expected['ReceiptHex'])
                receipt_fields = read_frame(receipt, 'world.action-receipt', 10)
                assert number(receipt_fields[1]) == 1 and receipt_fields[2:4] == action_fields[2:4]
                assert receipt_fields[4:6] == action_fields[6:8] and receipt_fields[6].hex() == digest(action)
                assert number(receipt_fields[7]) == number(journal[0])
                assert number(receipt_fields[8]) == 1 and number(receipt_fields[9]) == 0
                assert set(old[2]['commands']).issubset(new[2]['commands'])
                assert len(new[2]['commands']) == len(old[2]['commands']) + 1
                spawned = hashlib.sha256(frame('entity.spawned', action_fields[2], (0).to_bytes(8, 'big'))).digest()[:16]
                assert ids == {(batch[2], 1), (hashlib.sha256(receipt).digest()[:16], 3), (spawned, 4)}
                assert read_frame(journal[5], 'world.action-outcomes', 2)[1] == receipt
                appended = set(new[2]['commands']) - set(old[2]['commands'])
                assert len(appended) == 1
                row = appended.pop()
                assert row[0:2] == (action_fields[3], action_fields[2]) and row[3:] == (action, receipt)
                assert number(row[2]) == number(journal[0])
            else:
                assert ids == ({(batch[2], 1), (action_fields[2], 2)} if index == 0 else {(batch[2], 1)})
                assert new[2]['commands'] == old[2]['commands']
                assert read_frame(journal[5], 'world.action-outcomes', 1) == [b'world.action-outcomes']
            assert all(number(row[1]) == number(journal[0]) for row in new_ids)
        assert after['AdvancingSteps'] == before['AdvancingSteps'] + 1
        assert after['CalendarMilliseconds'] == before['CalendarMilliseconds'] + 1800
        assert after['CompletedWork'] == before['CompletedWork'] + 1 == after['RequiredWork']
        assert not before['Issued'] and len(after['Issued']) == 1
        source_lot = spawned.hex()
        destination = before['Residents'][expected['Player']]['InventoryCustodianId']
        assert source_lot == expected['SourceLotId']
        assert after['Issued'][0] == dict(LotId=source_lot, CustodianId=destination,
            DefinitionPin=after['MaterialPin'], SourceId=expected['SourceId'], Start=0,
            End=after['YieldQuantity'], Revision=0, Protection=0, ReservedBy='')
        assert source_lot not in before['SourceLotPins']
        assert after['SourceLotPins'] == {**before['SourceLotPins'], source_lot: after['SourceLotPins'][source_lot]}
        assert after['CustodyRevision'] == before['CustodyRevision'] + 1
        assert set(before['Storages']) == set(after['Storages'])
        for custodian, old in before['Storages'].items():
            new = after['Storages'][custodian]
            assert new['UnitCapacity'] == old['UnitCapacity']
            assert new['Revision'] == old['Revision'] + (custodian == destination)
        assert not before['IsExcavated'] and after['IsExcavated']
        assert before['Region'] == after['Region'] and before['Index'] == after['Index']
        assert before['Target'] == after['Target'] and len(before['Regions']) == len(after['Regions'])
        for index, (old, new) in enumerate(zip(before['Regions'], after['Regions'])):
            assert old['BaseHash'] == new['BaseHash']
            if index == before['Region']:
                assert new['Revision'] == old['Revision'] + 1
                assert new['Edits'] == sorted(old['Edits'] + [before['Index']])
            else:
                assert old == new
        for field in ('ConstructionHash', 'ProductionHash', 'SourcesHash', 'Installed', 'MaterialPin',
                      'YieldQuantity', 'Controls', 'StructuralPolicy', 'StructuralSettlementPolicy'):
            assert before[field] == after[field], field
        assert len(before['Installed']) == len(after['Installed']) == 2
        assert len({member['StructureId'] for member in before['Members']}) == 2
        assert after['PhysicalRevision'] == before['PhysicalRevision'] + 1
        assert len(before['Members']) == len(after['Members']) > 0
        for old, new in zip(before['Members'], after['Members']):
            assert all(old[key] == new[key] for key in ('ElementId', 'StructureId', 'MemberIndex', 'ConditionParts',
                                                       'InstalledLotId', 'MassMilligrams', 'VolumeMicrolitres'))
            assert new['Revision'] == old['Revision'] + 1 and new['Displacement']['Z'] == old['Displacement']['Z'] - 256
            assert new['Disposition'] == 'Resting'
            assert new['Displacement']['X'] == old['Displacement']['X'] and new['Displacement']['Y'] == old['Displacement']['Y']
        assert set(before['Actors']) == set(after['Actors'])
        assert set(before['Residents']) == set(after['Residents'])
        for actor, old in before['Actors'].items():
            new = after['Actors'][actor]
            assert old['BindingHash'] == new['BindingHash'] and old['ConditionHash'] == new['ConditionHash']
            assert new['Effort'] == min(1000, old['Effort'] - (400 if actor == expected['Player'] else 0) + 10)
            assert after['Residents'][actor]['InventoryCustodianId'] == before['Residents'][actor]['InventoryCustodianId']
            assert after['Residents'][actor]['ExperiencedMilliseconds'] == before['Residents'][actor]['ExperiencedMilliseconds'] + 1800
            assert new['Feet']['X'] == old['Feet']['X'] and new['Feet']['Y'] == old['Feet']['Y']
            assert new['Feet']['Z'] == old['Feet']['Z'] - (256 if actor == expected['Builder'] else 0)
            assert new['Revision'] == old['Revision'] + (actor == expected['Builder'])
        for path, reference in zip(paths, refs):
            report['independentStates'].append(dict(file=path.name, hash=digest(reference[0]),
                revision=number(read_frame(reference[0], 'world.snapshot')[8]),
                ledgerHashes={name: ledger_hash(rows) for name, rows in reference[2].items()}))
        normal = work / 'normal.sqlite'
        shutil.copyfile(paths[0], normal)
        for index, batch in enumerate(batches):
            value = run(f'ordinary transition {index}', '--execute-world-step', normal, batch)
            assert value['StateHash'] == digest(refs[index + 1][0])
            compare(normal, refs[index + 1])
        normal_hash = digest(normal.read_bytes())
        for item in expected['History']:
            value = run(f'exact history {item["Revision"]}', '--inspect-recorded-world', normal, item['Revision'])
            assert value['StateHash'] == item['Hash'] and value['SourceHeadHash'] == after['StateHash']
        run('accepted trigger replay refuses', '--execute-world-step', normal, batches[0], exit_code=1)
        run('accepted completion replay refuses', '--execute-world-step', normal, batches[-1], exit_code=1)
        assert digest(normal.read_bytes()) == normal_hash
        compare(normal, refs[-1])
        # Actual OS kills cover before and after the trigger, a saved query prefix, and final publication.
        for stage, index in (('trigger', 0), ('continuation', 1), ('terminal', len(batches) - 1)):
            for boundary in ('before', 'after'):
                path = work / f'{stage}-{boundary}.sqlite'
                shutil.copyfile(paths[index], path)
                selected = command + ['--probe-world-step-crash', str(path), str(batches[index]), boundary]
                process = subprocess.Popen(selected, cwd=runtime, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
                signals = queue.Queue()
                reader = threading.Thread(target=lambda: signals.put(process.stdout.readline()), daemon=True)
                reader.start()
                crash = dict(stage=stage, boundary=boundary, command=selected, pid=process.pid, signalTimeoutSeconds=30)
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
                assert process.returncode < 0 and not stderr
                retained = index + (boundary == 'after')
                revision = number(roots[retained][8])
                value = run(f'{stage} {boundary} recovery', '--inspect-recorded-world', path, revision)
                assert value['StateHash'] == digest(refs[retained][0])
                compare(path, refs[retained])
                for remaining in range(retained, len(batches)):
                    run(f'{stage} {boundary} resume {remaining}', '--execute-world-step', path, batches[remaining])
                compare(path, refs[-1])
                print(f'{stage} {boundary} kill/recovery: passed', flush=True)
        report['status'] = 'passed'
    except Exception as error:
        report.update(status='failed', failure=repr(error), traceback=traceback.format_exc())
    finally:
        changed = [name for name, value in originals.items() if digest((fixture / name).read_bytes()) != value]
        if changed:
            report.update(status='failed', changedFixtures=changed)
        report.update(finishedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                      ordinaryHostCalls=len(report['runs']), actualProcessKills=sum(row.get('exitCode', 0) < 0 for row in report['crashes']),
                      fixtureSha256=originals)
        output.write_text(json.dumps(report, indent=2) + '\n')
    print('coupled settlement: ' + report['status'], flush=True)
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
