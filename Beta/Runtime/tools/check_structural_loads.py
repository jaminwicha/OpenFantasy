"""Independent integer load oracle and fresh-process standalone Host qualification.

The fixture's occupied world is an unchanged byte witness, not an authority opened by this tool.
Caller-supplied graph and dependency pin are diagnostic inputs, not authenticated world policy.
No collapse, debris, material retirement, actor movement or other world publication is performed.
"""
import argparse
import datetime
import hashlib
import heapq
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import tempfile


MAXIMUM_BYTES = 16 * 1024 * 1024
REASONS = {1: 'Removed', 2: 'Unsupported', 3: 'ConditionLost', 4: 'Overloaded'}


def fields(data, tag=None, count=None):
    assert len(data) >= 12 and data[:8] == b'OFC\0\0\0\0\1'
    field_count = struct.unpack_from('>I', data, 8)[0]
    assert field_count <= (len(data) - 12) // 4
    result, offset = [], 12
    for _ in range(field_count):
        assert offset + 4 <= len(data)
        length = struct.unpack_from('>I', data, offset)[0]
        offset += 4
        assert offset + length <= len(data)
        result.append(data[offset:offset + length])
        offset += length
    assert offset == len(data)
    if tag is not None:
        assert result and result[0] == tag
    if count is not None:
        assert len(result) == count
    return result


def frame(values):
    return b'OFC\0' + struct.pack('>II', 1, len(values)) + b''.join(
        struct.pack('>I', len(value)) + value for value in values)


def number(value, width=8):
    return value.to_bytes(width, 'big')


def integer(value, width=8):
    assert len(value) == width
    return int.from_bytes(value, 'big')


def replace(data, path, value):
    values = fields(data)
    index = path[0]
    values[index] = replace(values[index], path[1:], value) if len(path) > 1 else value
    return frame(values)


def digest(data):
    return hashlib.sha256(data).hexdigest()


class Graph:
    """Reads declared elements and bearings; derives its own deterministic DAG order."""

    def __init__(self, data):
        assert len(data) <= MAXIMUM_BYTES
        outer = fields(data, b'structure.load-graph.v1', 3)
        rows = fields(outer[1], b'structure.elements')[1:]
        assert 1 <= len(rows) <= 4096
        self.elements, self.edges = {}, {}
        for row in rows:
            value = fields(row, b'structure.element', 8)
            identity = value[1]
            assert len(identity) == 16 and identity not in self.elements
            own, applied, capacity, condition, foundation, minimum = map(integer, value[2:])
            assert capacity > 0 and condition <= 10000 and foundation in (0, 1)
            assert minimum == 0 if foundation else 1 <= minimum <= 32
            self.elements[identity] = dict(own=own, applied=applied, capacity=capacity,
                condition=condition, foundation=bool(foundation), minimum=minimum)
            self.edges[identity] = []
        self.identities = list(self.elements)
        assert self.identities == sorted(self.identities)
        rows = fields(outer[2], b'structure.bearings')[1:]
        assert len(rows) <= 4096 * 32
        roles, pairs, bearing_ids = set(self.identities), set(), []
        incoming = dict.fromkeys(self.identities, 0)
        for row in rows:
            value = fields(row, b'structure.bearing', 5)
            identity, supported, supporting = value[1:4]
            weight = integer(value[4])
            assert all(len(key) == 16 for key in (identity, supported, supporting))
            assert identity not in roles and supported in self.elements and supporting in self.elements
            assert supported != supporting and weight > 0 and (supported, supporting) not in pairs
            assert not self.elements[supported]['foundation']
            roles.add(identity)
            pairs.add((supported, supporting))
            bearing_ids.append(identity)
            self.edges[supported].append((identity, supporting, weight))
            assert len(self.edges[supported]) <= 32
            incoming[supporting] += 1
        assert bearing_ids == sorted(bearing_ids)
        ready = [identity for identity in self.identities if incoming[identity] == 0]
        heapq.heapify(ready)
        self.order = []
        while ready:
            identity = heapq.heappop(ready)
            self.order.append(identity)
            for _, supporting, _ in self.edges[identity]:
                incoming[supporting] -= 1
                if incoming[supporting] == 0:
                    heapq.heappush(ready, supporting)
        assert len(self.order) == len(self.identities), 'Bearing graph must be acyclic'
        self.data, self.bearing_count = data, len(bearing_ids)
        self.total = sum(self.mass(identity) for identity in self.identities)

    def mass(self, identity):
        element = self.elements[identity]
        return element['own'] + element['applied']


def removed_ids(checkpoint, graph):
    rows = fields(fields(checkpoint, b'structure.resolution.v1', 10)[3], b'structure.removed')[1:]
    assert rows == sorted(set(rows)) and all(identity in graph.elements for identity in rows)
    return rows


def encode_state(graph, pin, removed, pass_number, cursor, evaluations, complete, failures, loads):
    encoded_failures = [frame([b'structure.failure', identity, number(reason), number(load, 16), number(failed_pass)])
        for identity, (reason, load, failed_pass) in sorted(failures.items())]
    encoded_loads = [frame([b'structure.pending-load', identity, number(loads[identity], 16)])
        for identity in graph.identities]
    return frame([b'structure.resolution.v1', bytes.fromhex(pin), graph.data,
        frame([b'structure.removed', *removed]), number(pass_number), number(cursor), number(evaluations),
        number(int(complete)), frame([b'structure.failures', *encoded_failures]),
        frame([b'structure.pending-loads', *encoded_loads])])


def oracle(graph, removed, pin):
    failures = {identity: (1, graph.mass(identity), 0) for identity in removed}
    evaluations = 0
    for pass_number in range(1, len(graph.identities) + 2):
        excluded = set(failures)
        loads = {identity: 0 if identity in excluded else graph.mass(identity) for identity in graph.identities}
        for identity in graph.order:
            if identity in excluded:
                continue
            evaluations += 1
            element = graph.elements[identity]
            edges = [edge for edge in graph.edges[identity] if edge[1] not in excluded]
            reason = (3 if element['condition'] == 0 else
                2 if not element['foundation'] and len(edges) < element['minimum'] else
                4 if loads[identity] > element['capacity'] * element['condition'] // 10000 else None)
            if reason is not None:
                assert identity not in failures
                failures[identity] = (reason, loads[identity], pass_number)
            elif not element['foundation']:
                weight = sum(edge[2] for edge in edges)
                shares = [loads[identity] * edge[2] // weight for edge in edges]
                remainder = loads[identity] - sum(shares)
                assert 0 <= remainder < len(edges)
                for index, (_, supporting, _) in enumerate(edges):
                    loads[supporting] += shares[index] + (1 if index < remainder else 0)
        if len(failures) == len(excluded):
            break
    else:
        raise AssertionError('Monotonic failures exceeded the finite pass bound')
    assert evaluations <= len(graph.identities) * (len(graph.identities) + 1)
    active = {identity: loads[identity] for identity in graph.identities if identity not in failures}
    foundations = sum(load for identity, load in active.items() if graph.elements[identity]['foundation'])
    failed_mass = sum(graph.mass(identity) for identity in failures)
    assert foundations + failed_mass == graph.total
    completed = encode_state(graph, pin, removed, pass_number, len(graph.identities), evaluations, True, failures, loads)
    return completed, dict(
        GraphHash=digest(graph.data), DependencyPin=pin, CompletedArchiveHash=digest(completed),
        Pass=pass_number, Cursor=len(graph.identities), TotalEvaluations=evaluations, IsComplete=True,
        Conservation=dict(TotalInputMassMilligrams=str(graph.total), AcceptedFoundationLoadMilligrams=str(foundations),
            FailedInputMassMilligrams=str(failed_mass), AccountedInputMassMilligrams=str(foundations + failed_mass)),
        Failures=[dict(ElementId=identity.hex(), Reason=REASONS[reason], LoadMilligrams=str(load), Pass=failed_pass)
            for identity, (reason, load, failed_pass) in sorted(failures.items())],
        ActiveLoads=[dict(ElementId=identity.hex(), LoadMilligrams=str(load)) for identity, load in active.items()])


def file_hashes(directory):
    result = {}
    for path in sorted(directory.rglob('*')):
        if path.is_file():
            with path.open('rb') as stream:
                hasher = hashlib.sha256()
                for block in iter(lambda: stream.read(1024 * 1024), b''):
                    hasher.update(block)
                result[str(path.relative_to(directory))] = hasher.hexdigest()
    return result


def fixture_identity(value):
    return number(value['High']).hex() + number(value['Low']).hex()


def verify_fixture(expected, independently_computed):
    conservation = independently_computed['Conservation']
    for source, destination in [('TotalInputMass', 'TotalInputMassMilligrams'),
            ('AcceptedFoundationLoad', 'AcceptedFoundationLoadMilligrams'), ('FailedInputMass', 'FailedInputMassMilligrams')]:
        assert str(expected[source]) == conservation[destination]
    assert expected['InputPin'] == independently_computed['DependencyPin']
    for name in ('Pass', 'TotalEvaluations'):
        assert expected[name] == independently_computed[name]
    fixture_failures = [dict(ElementId=fixture_identity(value['ElementId']), Reason=REASONS[value['Reason']],
        LoadMilligrams=str(value['Load']), Pass=value['Pass']) for value in expected['Failures']]
    assert fixture_failures == independently_computed['Failures']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ('dotnet', 'fixture', 'output'):
        parser.add_argument('--' + key, type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    dotnet, fixture, output = args.dotnet.resolve(), args.fixture.resolve(), args.output.resolve()
    assert fixture.is_dir() and not output.is_relative_to(fixture), 'Keep reports outside the unchanged fixture'
    output.parent.mkdir(parents=True, exist_ok=True)
    report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        scope='Standalone failure-plan qualification; no world publication or world-policy authentication',
        fixture=str(fixture), fixtureHashesBefore=file_hashes(fixture), timeoutSeconds=30, runs=[])
    failure = None
    with tempfile.TemporaryDirectory(prefix='structural-loads-') as temporary:
        work = Path(temporary)

        def inspect(name, checkpoint, graph_data, pin, expected_result=None):
            checkpoint_path, graph_path = work / (name + '.bin'), work / (name + '-graph.bin')
            checkpoint_path.write_bytes(checkpoint)
            graph_path.write_bytes(graph_data)
            command = [str(dotnet), str(root / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll'),
                '--inspect-structural-resolution', str(checkpoint_path), str(graph_path), pin]
            run = dict(name=name, inputSha256=digest(checkpoint), graphSha256=digest(graph_data),
                byteLength=len(checkpoint), graphByteLength=len(graph_data), dependencyPin=pin, command=command)
            report['runs'].append(run)
            try:
                result = subprocess.run(command, cwd=root, env=dict(os.environ, DOTNET_ROOT=str(dotnet.parent)),
                    capture_output=True, text=True, timeout=30)
            except subprocess.TimeoutExpired as error:
                def captured(value):
                    return value.decode(errors='replace') if isinstance(value, bytes) else value or ''
                run.update(timedOut=True, stdout=captured(error.stdout), stderr=captured(error.stderr))
                raise
            run.update(exitCode=result.returncode, stdout=result.stdout, stderr=result.stderr)
            assert checkpoint_path.read_bytes() == checkpoint and graph_path.read_bytes() == graph_data
            assert not result.stderr, (name, result.stderr)
            assert result.returncode == (0 if expected_result is not None else 1), (name, result.stdout)
            if expected_result is None:
                assert result.stdout.startswith('Inspection failed:'), (name, result.stdout)
                return
            actual = json.loads(result.stdout)
            outer = fields(checkpoint, b'structure.resolution.v1', 10)
            initial = dict(Pass=integer(outer[4]), Cursor=integer(outer[5]),
                TotalEvaluations=integer(outer[6]), IsComplete=bool(integer(outer[7])))
            assert actual['Contract'] == 'structural-load-resolution-v1'
            assert actual['Scope'] == 'standalone-failure-plan-diagnostic'
            assert actual['WorldConsequencesPublished'] is False
            assert actual['Authority'] == 'Caller-supplied graph and dependency pin are not authenticated world policy.'
            assert actual['InputCheckpointHash'] == digest(checkpoint) and actual['Initial'] == initial
            assert actual['ElementCount'] == len(graph.identities) and actual['BearingCount'] == graph.bearing_count
            for name in ('Conservation', 'Failures', 'ActiveLoads'):
                assert actual[name] == expected_result[name], name
            wanted = {key: value for key, value in expected_result.items()
                if key not in ('Conservation', 'Failures', 'ActiveLoads')}
            wanted['AdditionalEvaluations'] = wanted['TotalEvaluations'] - initial['TotalEvaluations']
            assert actual['Result'] == wanted, (actual['Result'], wanted)
            run['independentOracleMatched'] = True

        try:
            required = {'graph.bin', 'pending.bin', 'completed.bin', 'expected.json', 'occupied-world.sqlite'}
            assert required.issubset(report['fixtureHashesBefore'])
            graph_bytes = (fixture / 'graph.bin').read_bytes()
            pending = (fixture / 'pending.bin').read_bytes()
            complete = (fixture / 'completed.bin').read_bytes()
            expected = json.loads((fixture / 'expected.json').read_text())
            pin = expected['InputPin']
            assert len(pin) == 64 and bytes.fromhex(pin).hex() == pin
            graph = Graph(graph_bytes)
            removed = removed_ids(pending, graph)
            assert removed_ids(complete, graph) == removed
            for checkpoint in (pending, complete):
                outer = fields(checkpoint, b'structure.resolution.v1', 10)
                assert len(checkpoint) <= MAXIMUM_BYTES and outer[1] == bytes.fromhex(pin) and outer[2] == graph_bytes
            assert integer(fields(pending)[7]) == 0 and integer(fields(complete)[7]) == 1
            oracle_archive, computed = oracle(graph, removed, pin)
            assert complete == oracle_archive, 'Completed fixture differs from independently encoded final state'
            verify_fixture(expected, computed)
            report['independentOracle'] = computed
            report['canonicalCompletedArchiveMatched'] = True
            inspect('pending', pending, graph_bytes, pin, computed)
            inspect('completed', complete, graph_bytes, pin, computed)

            element = fields(fields(fields(graph_bytes)[1])[1])
            capacity = integer(element[4])
            changed_graph_bytes = replace(graph_bytes, [1, 1, 4], number(capacity + 1 if capacity < 2**64 - 1 else capacity - 1))
            changed_graph = Graph(changed_graph_bytes)
            initial_failures = {identity: (1, changed_graph.mass(identity), 0) for identity in removed}
            initial_loads = {identity: 0 if identity in initial_failures else changed_graph.mass(identity)
                for identity in changed_graph.identities}
            forged = encode_state(changed_graph, pin, removed, 1, 0, 0, False, initial_failures, initial_loads)
            inspect('copied-pin-substituted-graph', forged, graph_bytes, pin)
            bad_pin = ('0' if pin[0] != '0' else '1') + pin[1:]
            inspect('wrong-expected-pin', pending, graph_bytes, bad_pin)
            inspect('changed-retained-pin', replace(pending, [1], bytes.fromhex(bad_pin)), graph_bytes, pin)
            first_load = fields(fields(fields(pending)[9])[1])[2]
            inspect('changed-load', replace(pending, [9, 1, 2], number(integer(first_load, 16) ^ 1, 16)), graph_bytes, pin)
            cursor = (integer(fields(pending)[5]) + 1) % (len(graph.identities) + 1)
            inspect('changed-cursor', replace(pending, [5], number(cursor)), graph_bytes, pin)
            failures = fields(fields(complete)[8])
            changed_index = next(index for index, row in enumerate(failures[1:], 1)
                if integer(fields(row)[2]) != 1)
            load = integer(fields(failures[changed_index])[3], 16)
            inspect('changed-first-failure', replace(complete, [8, changed_index, 3], number(load + 1, 16)), graph_bytes, pin)
            for name, malformed in [('truncated', pending[:-1]), ('trailing', pending + b'\0'),
                    ('oversized', bytes(MAXIMUM_BYTES + 1))]:
                inspect(name, malformed, graph_bytes, pin)
            for name, malformed in [('graph-truncated', graph_bytes[:-1]), ('graph-trailing', graph_bytes + b'\0'),
                    ('graph-oversized', bytes(MAXIMUM_BYTES + 1))]:
                inspect(name, pending, malformed, pin)
            report['status'] = 'passed'
        except Exception as error:
            failure = error
            report.update(status='failed', failure=repr(error))
        finally:
            report['fixtureHashesAfter'] = file_hashes(fixture)
            report['allFixtureFilesUnchanged'] = report['fixtureHashesBefore'] == report['fixtureHashesAfter']
            if not report['allFixtureFilesUnchanged']:
                failure = AssertionError('Fixture files changed during read-only qualification')
                report.update(status='failed', failure=repr(failure))
            if failure is not None:
                retained = output.parent / (output.stem + '-' + work.name + '-failed-inputs')
                shutil.copytree(work, retained)
                report['retainedFailureInputs'] = str(retained)
            report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
            output.write_text(json.dumps(report, indent=2) + '\n')
    if failure is not None:
        raise failure
    print(json.dumps(dict(status=report['status'], inspections=len(report['runs']), report=str(output))))


if __name__ == '__main__':
    main()
