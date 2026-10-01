"""Paid-world publication qualification against real externally killed Host processes.
Managed transactions supply expected gameplay states; an independent SQL comparison checks every
published authoritative table. This is not power-loss, network-authentication or native UI proof.
"""
import argparse, datetime, fcntl, hashlib, importlib.util, json, shutil, sqlite3, traceback
from pathlib import Path
from contextlib import closing

HERE = Path(__file__).resolve().parent
HELPER = HERE.parent / 'durable-consented-world/qualification/verify_process_recovery.py'
spec = importlib.util.spec_from_file_location('retained_process_helpers', HELPER)
helper = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helper)
require, frame, number = helper.require, helper.frame, helper.number

def digest(data): return hashlib.sha256(data).digest()

def encode_frame(fields):
    return b'OFC\0' + (1).to_bytes(4, 'big') + len(fields).to_bytes(4, 'big') + b''.join(
        len(value).to_bytes(4, 'big') + value for value in fields)

def changed_open_price(data, profile):
    """Independently alter only one quoted price, preserving frame shape and outer checksums."""
    state = root(data, profile)
    coordination = frame(state[22], 'world.coordinated-paid-care.v1', 7)
    book = frame(coordination[6], 'world.paid-exchange.v1', 4)
    pending = frame(book[1], 'world.paid-exchange.open')
    require(len(pending) > 1, 'The corruption contrast requires an actual open quote')
    issued = frame(pending[1], 'world.paid-quote.v1', 4)
    quote = frame(issued[1], 'markets.spot.quote')
    require(number(quote[1]) == 1 and len(quote) >= 27, 'Unknown pinned quote format')
    quote[21] = (number(quote[21]) + 1).to_bytes(8, 'big')
    issued[1] = encode_frame(quote)
    pending[1] = encode_frame(issued)
    book[1] = encode_frame(pending)
    coordination[6] = encode_frame(book)
    state[22] = encode_frame(coordination)
    return encode_frame(state)

def refuse_sql_corruption(runner, report, output, before_path, after_path, before, after, profile, acceptance):
    """A recomputed checksum, missing receipt or missing identity cannot replace journal authentication."""
    cases = (
        ('changed-quoted-price', before_path, before,
         'UPDATE world_head SET state=?,hash=? WHERE singleton=1',
         lambda: (changed_open_price(before, profile), digest(changed_open_price(before, profile))),
         'The stored world head does not match exact replay.'),
        ('missing-paid-receipt', after_path, after,
         'DELETE FROM world_command_outcomes WHERE command_id=?', lambda: (bytes.fromhex(acceptance),),
         'The retained actor-command table is incomplete.'),
        ('missing-paid-identity', after_path, after,
         'DELETE FROM world_occupied_identity WHERE identity=?', lambda: (bytes.fromhex(acceptance),),
         'The retained world-identity table is incomplete.'),
    )
    report['corruptions'] = []
    for label, source, state, sql, values, reason in cases:
        path = output / (label + '.sqlite3')
        shutil.copy2(source, path)
        with closing(sqlite3.connect(path)) as connection:
            with connection:
                require(connection.execute(sql, values()).rowcount == 1, 'Corruption did not change its exact owned row')
        corrupted = helper.database(path)
        runner.run(label, '--inspect-recorded-world', path, number(root(state, profile)[8]), expected_exit=1)
        response = (output / (label + '.stdout')).read_text()
        require(reason in response, 'Corruption was not refused for the intended replay boundary: ' + response)
        require(helper.database(path) == corrupted, 'Refused inspection changed authoritative SQL rows')
        report['corruptions'].append({'label': label, 'reason': reason, 'refusedWithoutMutation': True})

def root(data, profile):
    fields = frame(data, 'world.snapshot', 24)
    require(number(fields[1]) == profile, 'Wrong explicitly selected paid profile')
    return fields

def scope(data, profile):
    inventory = frame(root(data, profile)[2], 'authority.counted-state', 9)
    custody = frame(inventory[3], 'custody.snapshot.v1', 6)
    require(len(custody[1]) == len(custody[2]) == 16 and custody[1] != custody[2], 'Invalid world scope')
    return custody[1:3]

def check_database(database, expected, profile):
    state = root(expected, profile)
    identity = scope(expected, profile)
    require(database['world_head'] == [(1, state[8], state[9], expected, digest(expected))], 'Incomplete or wrong world head')
    genesis = database['world_genesis'][0][1]
    require(scope(genesis, profile) == identity, 'Genesis scope differs')
    prior = digest(genesis)
    rows = database['world_commits']
    require(len(rows) == number(state[8]), 'Incomplete journal')
    for sequence, row in enumerate(rows, 1):
        require(number(row[0]) == sequence and row[6] == prior, 'Discontinuous commit chain')
        prior = row[7]
    require(prior == digest(expected), 'Wrong journal head')
    archives = database['world_material_care_segments']
    for ordinal, row in enumerate(archives):
        world, branch, index, boundary, publication, hash_value, encoded = row
        closing = root(encoded, profile)
        require(number(index) == ordinal and number(publication) == number(boundary) + 1, 'Wrong archive boundary/order')
        require(closing[8] == boundary and digest(encoded) == hash_value, 'Wrong complete archive bytes')
        require(rows[number(publication) - 1][6] == hash_value, 'Archive differs from actual publishing prior head')
        require([world, branch] == identity == scope(encoded, profile), 'Archive scope mismatch')
    return len(archives)

def check_report(report, expected, profile, source_head=None):
    state = root(expected, profile)
    require(report['ProfileVersion'] == profile and report['StateHash'].lower() == digest(expected).hex(), 'Wrong reported root')
    require(report.get('Revision', report.get('RecordedRevision')) == number(state[8]), 'Wrong reported revision')
    require(report['AdvancingSteps'] == number(state[9]), 'Wrong earned world time')
    if source_head is not None:
        require(report['SourceHeadRevision'] == number(root(source_head, profile)[8])
                and report['SourceHeadHash'].lower() == digest(source_head).hex(), 'Wrong historical source-head pin')

def check_acceptance(database, batch, expected, profile):
    state = root(expected, profile)
    step = frame(batch, 'world.step-batch', 10)
    require(number(step[1]) == profile and number(step[5]) + 1 == number(state[8]), 'Wrong selected step/revision')
    require(step[3:5] == scope(expected, profile), 'Step scope mismatch')
    actions = frame(step[9], 'world.step-actions')[1:]
    require(len(actions) == 1, 'The crash probe must isolate one actual paid acceptance')
    action = frame(actions[0], 'world.actor-action', 10)
    require(number(action[1]) == 17, 'Expected explicit paid action codec')
    intent = frame(action[9], 'world.action.spot-quote-accept', 4)
    require(number(intent[1]) == 1, 'Unknown paid acceptance intent version')
    require(database['world_commits'][-1][4] == batch, 'Published a different batch')
    row = next((row for row in database['world_command_outcomes'] if row[1] == action[2]), None)
    require(row is not None and row[:4] == (action[3], action[2], state[8], actions[0]), 'Wrong command receipt row')
    receipt = frame(row[4], 'world.action-receipt', 10)
    outcomes = frame(database['world_commits'][-1][5], 'world.action-outcomes')[1:]
    require(len(outcomes) == 1 and outcomes[0] == row[4], 'Paid receipt must be the exact published outcome')
    require(receipt[2:8] == [action[2], action[3], action[6], action[7], digest(actions[0]), state[8]]
            and number(receipt[8]) == 1 and number(receipt[9]) == 0, 'Acceptance did not succeed exactly once')
    identities = {row[0]: row for row in database['world_occupied_identity']}
    require(action[2] in identities and identities[action[2]][1:] == (state[8], 2), 'Missing permanent acceptance command')
    return action[2].hex()

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('dotnet', 'host', 'before-db', 'after-db', 'batch', 'after-root', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--profile', type=int, required=True)
    parser.add_argument('--expected-new-archives', type=int, choices=(0, 1), required=True)
    args = parser.parse_args()
    with Path('/tmp/openfantasy-retained-resource-integration.lock').open('w') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        execute(args)

def execute(args):
    dotnet, host, before_path, reference_path, batch_path, after_path, output = (
        getattr(args, name).resolve() for name in ('dotnet', 'host', 'before_db', 'after_db', 'batch', 'after_root', 'output'))
    require(all(path.is_file() for path in (dotnet, host, before_path, reference_path, batch_path, after_path)), 'Missing required fixture/executable')
    require(not output.exists(), 'Every prior run must remain preserved')
    output.mkdir(parents=True)
    fixtures = {str(path): digest(path.read_bytes()).hex() for path in (before_path, reference_path, batch_path, after_path, HELPER, Path(__file__))}
    binaries = helper.execution_hashes(host.parent)
    report = {'status': 'running', 'profile': args.profile, 'runs': [], 'crashes': [], 'completedCases': [],
              'startedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'fixtureHashes': fixtures,
              'executionHashes': binaries, 'expectedNewArchives': args.expected_new_archives,
              'limits': {'hostSeconds': helper.RUN_SECONDS, 'killSeconds': helper.KILL_SECONDS},
              'limitations': ['Privileged local selected-batch publication; no network authentication.',
                             'Managed fixture supplies gameplay expectation; full SQL rows are independently compared.',
                             'SIGKILL is not physical power loss, native gameplay or production-scale acceptance.']}
    runner = helper.HostRunner(dotnet, host, output, report)
    try:
        before_db = helper.database(before_path, sealed_fixture=True)
        before = before_db['world_head'][0][3]
        after, batch = after_path.read_bytes(), batch_path.read_bytes()
        before_count = check_database(before_db, before, args.profile)
        reference = helper.database(reference_path, sealed_fixture=True)
        after_count = check_database(reference, after, args.profile)
        require(after_count == before_count + args.expected_new_archives, 'Wrong actual automatic archive delta')
        acceptance_id = check_acceptance(reference, batch, after, args.profile)
        require(number(root(after, args.profile)[8]) == number(root(before, args.profile)[8]) + 1, 'Acceptance must publish one world revision')
        require(acceptance_id not in {row[0].hex() for row in before_db['world_occupied_identity']}, 'Acceptance was already occupied')
        normal = output / 'normal.sqlite3'
        shutil.copy2(before_path, normal)
        check_report(runner.run('normal', '--execute-world-step', normal, batch_path), after, args.profile)
        actual = helper.database(normal)
        require(actual == reference, 'Host publication differs from the expected complete authoritative ledger')
        if args.expected_new_archives:
            require(actual['world_material_care_segments'][-1][-1] == before, 'Archive is not the exact closing root')
        for boundary in ('normal', 'before', 'after'):
            path = output / (boundary + '.sqlite3')
            if boundary != 'normal':
                shutil.copy2(before_path, path)
                runner.kill(boundary, path, batch_path, boundary)
                retained = before if boundary == 'before' else after
                check_report(runner.run(boundary + '-reopen', '--inspect-recorded-world', path,
                    number(root(retained, args.profile)[8])), retained, args.profile, retained)
                require(helper.database(path) == (before_db if boundary == 'before' else actual), 'Mixed owners/receipts/history after process loss')
                if boundary == 'before':
                    check_report(runner.run(boundary + '-retry', '--execute-world-step', path, batch_path), after, args.profile)
            require(helper.database(path) == actual, 'Recovered authoritative tables differ')
            runner.run(boundary + '-duplicate', '--execute-world-step', path, batch_path, expected_exit=1)
            require(helper.database(path) == actual, 'Duplicate selected batch changed state')
            check_report(runner.run(boundary + '-history', '--inspect-recorded-world', path,
                number(root(before, args.profile)[8])), before, args.profile, after)
            require(helper.database(path) == actual, 'Historical lookup mutated authority')
            check_acceptance(helper.database(path), batch, after, args.profile)
            report['completedCases'].append({'boundary': boundary, 'database': str(path)})
        refuse_sql_corruption(runner, report, output, before_path, reference_path, before, after, args.profile, acceptance_id)
        report.update(status='passed', acceptanceCommandId=acceptance_id, archiveCountBefore=before_count, archiveCountAfter=after_count)
    except Exception:
        report.update(status='failed', error=traceback.format_exc())
        raise
    finally:
        report['fixturesUnchanged'] = all(digest(Path(path).read_bytes()).hex() == value for path, value in fixtures.items())
        report['executionUnchanged'] = binaries == helper.execution_hashes(host.parent)
        if not report['fixturesUnchanged'] or not report['executionUnchanged']: report['status'] = 'failed'
        report['externalKills'] = sum(item.get('exitCode') == -9 for item in report['crashes'])
        (output / 'result.json').write_text(json.dumps(report, indent=2) + '\n')
    require(report['status'] == 'passed', 'Qualification failed')
    print(json.dumps({key: report[key] for key in ('status', 'externalKills', 'fixturesUnchanged', 'executionUnchanged')}))

if __name__ == '__main__': main()
