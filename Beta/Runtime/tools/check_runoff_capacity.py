"""Independent outstanding-volume accounting through the real standalone capacity inspector.

The oracle follows basin closures to sinks and counts existing raw/derived contents once. It does
not reproduce local drainage, authenticate publication, or claim flow timing or durable world admission.
"""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import struct
import subprocess
import tempfile
from check_terrain_materials import fields, frame, integer, members, replace, u64
from check_runoff_budgets import instant


def oracle(data):
    outer = fields(data)
    assert len(outer) == 8 and outer[0] == b'geography.watershed.runoff-capacity.v1'
    domain = fields(outer[1]); network = fields(domain[1])
    basins = {b[2]: len(b[4]) // 44 for b in members(network[6])}
    closures = {r[0]: r[1:] for r in struct.iter_unpack('>16sI16sii', network[7])}
    terminals = {b: (s, c) for b, s, c in struct.iter_unpack('>16s16sQ', domain[7])}
    side, unit_volume = integer(fields(domain[2])[1]), integer(fields(domain[6])[6])
    authority = fields(outer[2]); custody = fields(authority[3])
    raw = [fields(l) for l in fields(custody[5])[1:]]
    existing, capacities, held = {}, {}, {}
    for storage, capacity in terminals.values():
        assert storage not in capacities or capacities[storage] == capacity
        capacities[storage] = capacity; held[storage] = 0
    for lot in raw:
        existing[lot[2]] = existing.get(lot[2], 0) + integer(lot[7]) - integer(lot[6])
    if outer[3]:
        for value in fields(fields(outer[3])[7])[1:]:
            lot = fields(value); claim = fields(lot[6])
            existing[lot[3]] = existing.get(lot[3], 0) + integer(claim[5]) - integer(claim[4])
    stride = 40 + 72 * len(basins)
    assert len(outer[5]) % stride == 0 and len(outer[5]) // stride <= 16
    pending, identities = [], set()
    for offset in range(0, len(outer[5]), stride):
        row = outer[5][offset:offset + stride]; reservation = row[:16]
        assert reservation not in identities; identities.add(reservation)
        start, end = instant(row[16:28]), instant(row[28:40]); assert start[0] < end[0]
        total, funding = 0, []
        for basin, depth, source, lo, hi, storage in struct.iter_unpack('>16sQ16sQQ16s', row[40:]):
            volume = side * side * depth * 1000 * basins[basin]
            assert volume % unit_volume == 0
            amount = volume // unit_volume; total += amount
            if depth:
                assert hi - lo == amount
                cursor = lo
                for lot in sorted(raw, key=lambda l: integer(l[6])):
                    left, right = max(lo, integer(lot[6])), min(hi, integer(lot[7]))
                    if lot[5] != source or left >= right: continue
                    assert cursor == left and lot[2] == storage and lot[10] == reservation
                    assert lot[3:5] == domain[4:6] and integer(lot[9]) == 0
                    cursor = right
                assert cursor == hi
                funding.append((source, lo, hi))
            else:
                assert (source, lo, hi, storage) == (bytes(16), 0, 0, bytes(16))
            seen = set()
            while closures[basin][0] != 2:
                assert basin not in seen; seen.add(basin); basin = closures[basin][1]
            held[terminals[basin][0]] += amount
        for lot in raw:
            if lot[10] == reservation:
                coverage = sum(max(0, min(hi, integer(lot[7])) - max(lo, integer(lot[6])))
                    for source, lo, hi in funding if source == lot[5])
                assert coverage == integer(lot[7]) - integer(lot[6])
        pending.append(dict(ReservationId=reservation.hex(), Start=start[1], End=end[1], TotalInputUnits=total))
    assert len(outer[6]) % 16 == 0
    closed = [outer[6][i:i+16] for i in range(0, len(outer[6]), 16)]
    assert len(set(closed)) == len(closed) and not identities.intersection(closed)
    limits = {fields(s)[1]: integer(fields(s)[2]) for s in fields(custody[4])[1:]}
    expected, witnesses = [], []
    for storage in sorted(capacities):
        current, incoming = existing.get(storage, 0), held[storage]
        projected, capacity = current + incoming, capacities[storage]
        volume = projected * unit_volume
        assert projected <= limits[storage] and volume <= capacity
        witnesses.append((storage, current, incoming, projected, volume, capacity))
        expected.append(dict(CustodianId=storage.hex(), ExistingUnits=current, IncomingUnits=incoming,
            ProjectedUnits=projected, ProjectedVolumeMicrolitres=volume, CapacityMicrolitres=capacity))
    assert list(struct.iter_unpack('>16sQQQQQ', outer[7])) == witnesses
    return dict(ContentHash=hashlib.sha256(data).hexdigest(), DomainHash=hashlib.sha256(outer[1]).hexdigest(),
        Revision=integer(outer[4]), CustodyRevision=integer(custody[3]), Pending=pending,
        Closed=[c.hex() for c in closed], Receivers=expected)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ['dotnet', 'fixture', 'output']: parser.add_argument('--' + key, type=Path, required=True)
    args = parser.parse_args(); root = Path(__file__).resolve().parents[1]
    dotnet, output = args.dotnet.resolve(), args.output.resolve(); output.parent.mkdir(parents=True, exist_ok=True)
    report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(), runs=[])
    with tempfile.TemporaryDirectory(prefix='runoff-capacity-') as temporary:
        def inspect(name, data, valid=False):
            path = Path(temporary) / (name + '.bin'); path.write_bytes(data)
            command = [str(dotnet), str(root / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll'), '--inspect-runoff-capacity', str(path)]
            run = dict(name=name, inputSha256=hashlib.sha256(data).hexdigest(), byteLength=len(data), command=command)
            report['runs'].append(run)
            try:
                result = subprocess.run(command, cwd=root, env=dict(os.environ, DOTNET_ROOT=str(dotnet.parent)),
                    capture_output=True, text=True, timeout=30)
            except subprocess.TimeoutExpired as error:
                run.update(timedOut=True, timeoutSeconds=30, stdout=str(error.stdout or ''), stderr=str(error.stderr or '')); raise
            run.update(exitCode=result.returncode, stdout=result.stdout, stderr=result.stderr)
            assert path.read_bytes() == data and not result.stderr
            assert result.returncode == (0 if valid else 1), (name, result.stdout, result.stderr)
            if valid:
                actual, expected = json.loads(result.stdout), oracle(data)
                assert actual['Contract'] == 'watershed-runoff-capacity-v1' and actual['Scope'] == 'standalone-capacity-owner'
                for key, value in expected.items(): assert actual[key] == value, (name, key, actual[key], value)
                run['independentOracle'] = expected
            else:
                assert result.stdout.startswith('Inspection failed:')
        try:
            for name in ['two-held', 'one-canceled', 'crafted-held', 'empty-closed', 'maximum-pending']:
                inspect(name, (args.fixture / (name + '.bin')).read_bytes(), True)
            data = (args.fixture / 'two-held.bin').read_bytes(); outer = fields(data)
            stride = len(outer[5]) // 2
            mutations = [
                ('duplicate-pending', [5], outer[5][:stride] * 2),
                ('noncanonical-order', [5], outer[5][stride:] + outer[5][:stride]),
                ('too-many-pending', [5], outer[5][:stride] * 17),
                ('closed-active', [6], outer[5][:16]),
                ('partial-closed', [6], b'x'),
                ('too-many-closed', [6], outer[5][:16] * 4097),
                ('missing-pending', [5], outer[5][:stride]),
                ('unreserved-source', [2, 3, 5, 1, 10], b''),
                ('invalid-production', [3], b'broken'),
                ('wrong-owner', [1, 3], outer[5][:16]),
                ('wrong-item-pin', [1, 5], bytes(32)),
                ('receiver-overflow', [1, 7], fields(outer[1])[7][:-8] + u64(1_000_000_000)),
                ('witness-mismatch', [7], outer[7][:-1] + bytes([outer[7][-1] ^ 1])),
            ]
            for name, path, value in mutations: inspect(name, replace(data, path, value))
            for name, changed in [('truncated', data[:-1]), ('trailing', data + b'\0'), ('oversized', bytes(23_500_001))]:
                inspect(name, changed)
            report['status'] = 'passed'
        except Exception as error:
            report.update(status='failed', failure=repr(error)); raise
        finally:
            report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
            output.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(dict(status=report['status'], inspections=len(report['runs']), report=str(output))))


if __name__ == '__main__':
    main()
