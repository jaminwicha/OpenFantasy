"""Independent reservoir fill/capacity checks through the real read-only host.

Uses integer binary search for the volume-height inverse, separate from managed floor-band filling.
No terrain walls, hydraulic dynamics, liquid taxonomy, world admission or performance proof is inferred.
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
from check_runoff_capacity import oracle as capacity_oracle


def fill(columns, spill, volume):
    bottom = min(c[2] for c in columns)
    capacity = sum(spill - c[2] for c in columns) * 1_000_000_000
    assert 0 <= volume <= capacity
    lo, hi = 0, (spill - bottom) * 1_000_000_000
    def demand(height): return sum(max(0, height - (c[2] - bottom) * 1_000_000_000) for c in columns)
    while lo < hi:
        middle = (lo + hi + 1) // 2
        if demand(middle) <= volume: lo = middle
        else: hi = middle - 1
    quantities = [max(0, lo - (c[2] - bottom) * 1_000_000_000) for c in columns]
    remainder = volume - sum(quantities); left = remainder
    for index, column in enumerate(columns):
        if left and (column[2] - bottom) * 1_000_000_000 <= lo:
            quantities[index] += 1; left -= 1
    assert left == 0 and sum(quantities) == volume
    surface, fraction = bottom + lo // 1_000_000_000, lo % 1_000_000_000
    witness = struct.pack('>QqQQ', volume, surface, fraction, remainder) + b''.join(u64(q) for q in quantities)
    return dict(VolumeMicrolitres=volume, SurfaceMetres=surface, SurfaceBillionths=fraction, RemainderMicrolitres=remainder), witness


def oracle(data):
    outer = fields(data); assert len(outer) == 4 and outer[0] == b'geography.liquid-reservoirs.v1'
    owner = fields(outer[1]); capacity = capacity_oracle(outer[1]); domain = fields(owner[1])
    unit_volume = integer(fields(domain[6])[6]); inventory = fields(owner[2]); custody = fields(inventory[3])
    existing, pins = {}, {}
    for encoded in fields(custody[5])[1:]:
        lot = fields(encoded); existing[lot[2]] = existing.get(lot[2], 0) + integer(lot[7]) - integer(lot[6])
        pins.setdefault(lot[2], []).append(lot[3:5])
    if owner[3]:
        for encoded in fields(fields(owner[3])[7])[1:]:
            lot = fields(encoded); claim = fields(lot[6])
            existing[lot[3]] = existing.get(lot[3], 0) + integer(claim[5]) - integer(claim[4])
            pins.setdefault(lot[3], []).append(lot[4:6])
    incoming = {bytes.fromhex(r['CustodianId']): r['IncomingUnits'] for r in capacity['Receivers']}
    reservoir_report, witnesses, total = [], b'', 0
    for geometry in members(outer[2]):
        assert geometry[0] == b'geography.reservoir-geometry.v1' and len(geometry) == 8
        reservoir, custodian, spill = geometry[4], geometry[5], integer(geometry[6], True)
        columns = list(struct.iter_unpack('>qqq', geometry[7])); total += len(columns)
        assert columns == sorted(columns, key=lambda c: (c[1], c[0]))
        assert len({c[:2] for c in columns}) == len(columns)
        assert all(1 <= spill - c[2] <= 1024 for c in columns)
        assert all(pin == domain[4:6] for pin in pins.get(custodian, []))
        actual_units, held = existing.get(custodian, 0), incoming.get(custodian, 0)
        actual, actual_bytes = fill(columns, spill, actual_units * unit_volume)
        projected, projected_bytes = fill(columns, spill, (actual_units + held) * unit_volume)
        witnesses += reservoir + u64(actual_units) + u64(held) + actual_bytes + projected_bytes
        reservoir_report.append(dict(ReservoirId=reservoir.hex(), CustodianId=custodian.hex(), SpillZ=spill,
            Columns=len(columns), CapacityMicrolitres=sum(spill - c[2] for c in columns) * 1_000_000_000,
            ExistingUnits=actual_units, ReservedIncomingUnits=held, Actual=actual, Projected=projected))
    assert total <= 4096 and witnesses == outer[3]
    return dict(ContentHash=hashlib.sha256(data).hexdigest(), CapacityHash=hashlib.sha256(outer[1]).hexdigest(),
        Revision=capacity['Revision'], TotalColumns=total, Reservoirs=reservoir_report)


def geometry_change(data, index, field, value):
    collection = members(fields(data)[2]); collection[index][field] = value
    encoded = [frame(g) for g in collection]
    return replace(data, [2], struct.pack('>I', len(encoded)) + b''.join(struct.pack('>I', len(g)) + g for g in encoded))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ['dotnet', 'fixture', 'output']: parser.add_argument('--' + key, type=Path, required=True)
    args = parser.parse_args(); root = Path(__file__).resolve().parents[1]
    dotnet, output = args.dotnet.resolve(), args.output.resolve(); output.parent.mkdir(parents=True, exist_ok=True)
    report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(), runs=[])
    with tempfile.TemporaryDirectory(prefix='reservoir-inspections-') as temporary:
        def inspect(name, data, valid=False):
            path = Path(temporary) / (name + '.bin'); path.write_bytes(data)
            command = [str(dotnet), str(root / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll'), '--inspect-liquid-reservoirs', str(path)]
            run = dict(name=name, inputSha256=hashlib.sha256(data).hexdigest(), byteLength=len(data), command=command)
            report['runs'].append(run)
            try:
                result = subprocess.run(command, cwd=root, env=dict(os.environ, DOTNET_ROOT=str(dotnet.parent)),
                    capture_output=True, text=True, timeout=30)
            except subprocess.TimeoutExpired as error:
                run.update(timedOut=True, timeoutSeconds=30, stdout=str(error.stdout or ''), stderr=str(error.stderr or '')); raise
            run.update(exitCode=result.returncode, stdout=result.stdout, stderr=result.stderr)
            assert not result.stderr and path.read_bytes() == data
            assert result.returncode == (0 if valid else 1), (name, result.stdout, result.stderr)
            if valid:
                actual, expected = json.loads(result.stdout), oracle(data)
                assert actual['Contract'] == 'liquid-reservoirs-v1' and actual['Scope'] == 'standalone-watertight-open-vessels'
                for key, value in expected.items(): assert actual[key] == value, (name, key, actual[key], value)
                run['independentOracle'] = expected
            else: assert result.stdout.startswith('Inspection failed:')
        try:
            for name in ['two-held', 'stacked', 'canceled-buffer', 'crafted-source', 'maximum-columns']:
                inspect(name, (args.fixture / (name + '.bin')).read_bytes(), True)
            data = (args.fixture / 'two-held.bin').read_bytes(); outer = fields(data); geometries = members(outer[2])
            changes = [
                ('source-overfull', 0, 6, struct.pack('>q', 1)),
                ('receiver-capacity-mismatch', 1, 6, struct.pack('>q', 3)),
                ('overlapping-support', 1, 7, geometries[0][7]),
                ('wrong-realm', 0, 3, bytes(16)),
                ('identity-alias', 0, 4, geometries[0][5]),
                ('duplicate-custodian', 1, 5, geometries[0][5]),
                ('duplicate-columns', 0, 7, geometries[0][7] * 2),
                ('perched-pocket', 0, 7, b''.join(struct.pack('>qqq', *c) for c in [(10, 0, -1), (11, 0, 1), (12, 0, 0)])),
                ('disconnected-support', 0, 7, struct.pack('>qqq', 10, 0, 0) + struct.pack('>qqq', 12, 0, 0)),
                ('excessive-depth', 0, 7, struct.pack('>qqq', 0, 0, -1023)),
                ('partial-column', 0, 7, b'x'),
            ]
            for name, index, field, value in changes: inspect(name, geometry_change(data, index, field, value))
            inspect('empty-collection', replace(data, [2], struct.pack('>I', 0)))
            inspect('excessive-count', replace(data, [2], struct.pack('>I', 257)))
            inspect('changed-fill', replace(data, [3], outer[3][:-1] + bytes([outer[3][-1] ^ 1])))
            first_length = struct.unpack_from('>I', outer[2], 4)[0]
            inspect('changed-geometry-length', replace(data, [2], outer[2][:4] + struct.pack('>I', first_length + 1) + outer[2][8:]))
            reversed_geometries = [frame(g) for g in reversed(geometries)]
            inspect('noncanonical-geometry-order', replace(data, [2], struct.pack('>I', 2) + b''.join(struct.pack('>I', len(g)) + g for g in reversed_geometries)))
            maximum = (args.fixture / 'maximum-columns.bin').read_bytes()
            maximum_columns = members(fields(maximum)[2])[0][7]
            inspect('global-column-excess', geometry_change(maximum, 0, 7, maximum_columns + struct.pack('>qqq', 4095, 20, 0)))
            inspect('noncanonical-column-order', geometry_change(maximum, 0, 7,
                b''.join(reversed([maximum_columns[i:i+24] for i in range(0, len(maximum_columns), 24)]))))
            for name, changed in [('truncated', data[:-1]), ('trailing', data + b'\0'), ('oversized', bytes(24_000_001))]:
                inspect(name, changed)
            report['status'] = 'passed'
        except Exception as error:
            report.update(status='failed', failure=repr(error)); raise
        finally:
            report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
            output.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(dict(status=report['status'], inspections=len(report['runs']), report=str(output))))


if __name__ == '__main__': main()
