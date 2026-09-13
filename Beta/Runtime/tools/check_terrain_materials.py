"""Independent interval/format oracle and real read-only host inspections of funded terrain materials."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import struct
import subprocess
import tempfile


def fields(data):
    assert data[:8] == b'OFC\0\0\0\0\1'
    count = struct.unpack_from('>I', data, 8)[0]
    result, offset = [], 12
    for _ in range(count):
        length = struct.unpack_from('>I', data, offset)[0]
        offset += 4
        assert offset + length <= len(data)
        result.append(data[offset:offset + length])
        offset += length
    assert offset == len(data)
    return result


def frame(values):
    return b'OFC\0' + struct.pack('>II', 1, len(values)) + b''.join(struct.pack('>I', len(v)) + v for v in values)


def u64(value):
    return struct.pack('>Q', value)


def integer(value, signed=False):
    assert len(value) == 8
    return int.from_bytes(value, 'big', signed=signed)


def members(data):
    count = struct.unpack_from('>I', data)[0]
    offset, result = 4, []
    for _ in range(count):
        length = struct.unpack_from('>I', data, offset)[0]
        offset += 4
        result.append(fields(data[offset:offset + length]))
        offset += length
    assert offset == len(data)
    return result


def oracle(data):
    outer = fields(data)
    assert outer[0] == b'geography.terrain-material-state.v1' and len(outer) == 4
    deposit, authority = fields(outer[1]), fields(outer[2])
    assert len(deposit) == 14 and deposit[0] == b'geography.terrain-material-deposit.v1'
    terrain = fields(deposit[1])
    network, mapping = fields(terrain[1]), fields(terrain[2])
    basin = next(b for b in members(network[6]) if b[2] == deposit[2])
    bounds = fields(fields(basin[1])[5])
    index, depth, side = integer(deposit[3]), integer(deposit[4]), integer(mapping[1])
    x = (integer(bounds[1], True) + index % integer(bounds[3])) * side
    y = (integer(bounds[2], True) + index // integer(bounds[3])) * side
    surface = struct.unpack_from('>q', basin[4], index * 44)[0] * integer(mapping[2]) + integer(mapping[3], True)
    bottom = surface - depth + 1
    start, end, units = integer(deposit[10]), integer(deposit[11]), integer(deposit[8])
    assert end - start == side * side * depth * units
    physical = fields(deposit[7])
    assert integer(physical[6]) * units == 1_000_000_000
    active = list(struct.iter_unpack('>qqq', outer[3]))
    assert active == sorted(set(active), key=lambda c: (c[2], c[1], c[0])) and len(active) <= 4096
    active_ranges = []
    for cx, cy, cz in active:
        assert x <= cx < x + side and y <= cy < y + side and bottom <= cz <= surface
        ordinal = ((cz - bottom) * side + cy - y) * side + cx - x
        a = start + ordinal * units
        active_ranges.append((a, a + units))
    custody = fields(authority[3])
    assert custody[1:3] == network[1:3]
    lots = [fields(l) for l in fields(custody[5])[1:]]
    lots.sort(key=lambda l: (l[5], integer(l[6])))
    cursor, totals = start, [0, 0, 0]
    for lot in lots:
        if lot[5] != deposit[9]:
            continue
        a, b = max(start, integer(lot[6])), min(end, integer(lot[7]))
        if a >= b:
            continue
        assert a == cursor and lot[3:5] == deposit[5:7]
        cursor = b
        owner = 0 if lot[2] == deposit[12] else 1 if lot[2] == deposit[13] else 2
        if owner == 0:
            assert all(b <= lo or a >= hi for lo, hi in active_ranges)
        if owner == 1:
            assert any(lo <= a < b <= hi for lo, hi in active_ranges)
            assert (a, b) == (integer(lot[6]), integer(lot[7]))
        totals[owner] += b - a
    assert cursor == end and sum(totals) == end - start
    return dict(ContentHash=hashlib.sha256(data).hexdigest(), Revision=integer(custody[3]),
                Start=start, End=end, CoarseQuantity=totals[0], DetailedQuantity=totals[1], OtherQuantity=totals[2],
                ActiveCellCount=len(active), LotCount=len(lots), UnitsPerCell=units,
                MassMilligrams=integer(physical[5]) * (end - start), VolumeMicrolitres=integer(physical[6]) * (end - start),
                Minimum=dict(X=x, Y=y, Z=bottom), Maximum=dict(X=x + side - 1, Y=y + side - 1, Z=surface))


def replace(data, path, value):
    values = fields(data)
    index = path[0]
    values[index] = replace(values[index], path[1:], value) if len(path) > 1 else value
    return frame(values)


def coarse_archive(data, fragmented):
    outer = fields(data)
    deposit, authority = fields(outer[1]), fields(outer[2])
    custody = fields(authority[3])
    start, end = integer(deposit[10]), integer(deposit[11])
    intervals = [(start, (start + end) // 2), ((start + end) // 2, end)] if fragmented else [(start, end)]
    lots = [frame([b'custody.source-lot.v1', (100000 + i).to_bytes(16, 'big'), deposit[12], deposit[5], deposit[6],
                   deposit[9], u64(a), u64(b), u64(0), u64(0), b'']) for i, (a, b) in enumerate(intervals)]
    custody[3] = u64(0)
    storages = fields(custody[4])
    custody[4] = frame([storages[0]] + [replace(s, [3], u64(0)) for s in storages[1:]])
    custody[5] = frame([b'custody.lots.v1'] + lots)
    authority[3] = frame(custody)
    outer[2], outer[3] = frame(authority), b''
    return frame(outer)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', type=Path, required=True)
    parser.add_argument('--fixture', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    output, dotnet = args.output.resolve(), args.dotnet.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix='material-inspections-', dir=output.parent))
    report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(), runs=[],
                  scope='Standalone finite material archives: independent interval/outer-format oracle and real host. No world writes.')

    def inspect(name, data, valid=True):
        path = work / (name + '.bin')
        path.write_bytes(data)
        command = [str(dotnet), str(root / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll'),
                   '--inspect-terrain-materials', str(path)]
        try:
            result = subprocess.run(command, cwd=root, env=dict(os.environ, DOTNET_ROOT=str(dotnet.parent)),
                                    capture_output=True, text=True, timeout=30)
        except subprocess.TimeoutExpired as error:
            report['runs'].append(dict(name=name, timedOut=True, timeoutSeconds=30,
                                       stdout=str(error.stdout or ''), stderr=str(error.stderr or '')))
            raise
        report['runs'].append(dict(name=name, inputSha256=hashlib.sha256(data).hexdigest(), command=command,
                                  exitCode=result.returncode, stdout=result.stdout, stderr=result.stderr))
        assert path.read_bytes() == data and not result.stderr
        assert result.returncode == (0 if valid else 1), (name, result.stdout, result.stderr)
        if valid:
            actual = json.loads(result.stdout)
            assert actual['Contract'] == 'terrain-material-state-v1' and actual['Scope'] == 'standalone-funded-material'
            expected = oracle(data)
            for key, value in expected.items():
                assert actual[key] == value, (name, key, actual[key], value)
            report['runs'][-1]['independentOracle'] = expected
        else:
            assert result.stdout.startswith('Inspection failed:')

    try:
        for name in ['partial-transfer', 'lot-cap', 'active-cap']:
            inspect(name, (args.fixture / (name + '.bin')).read_bytes())
        data = (args.fixture / 'partial-transfer.bin').read_bytes()
        assert (oracle(data)['CoarseQuantity'], oracle(data)['DetailedQuantity'], oracle(data)['OtherQuantity']) == (70, 6, 4)
        inspect('python-coarse', coarse_archive(data, False))
        inspect('python-fragmented-coarse', coarse_archive(data, True))
        for name, path, value in [('wrong-units', [1, 8], u64(9)), ('wrong-budget', [1, 11], u64(91)),
                                  ('wrong-item-pin', [1, 6], bytes(32)), ('mass-overflow', [1, 7, 5], u64(2**64-1)),
                                  ('physical-role-alias', [1, 7, 3], fields(fields(data)[1])[5]),
                                  ('missing-cell-binding', [3], b''), ('unknown-contract', [0], b'unsupported')]:
            inspect(name, replace(data, path, value), False)
        active = fields(data)[3]
        inspect('duplicate-cell', replace(data, [3], active + active), False)
        outer, deposit = fields(data), fields(fields(data)[1])
        lot_rows = fields(fields(fields(outer[2])[3])[5])
        fine_index = next(i for i in range(1, len(lot_rows)) if fields(lot_rows[i])[2] == deposit[13])
        coarse_index = next(i for i in range(1, len(lot_rows)) if fields(lot_rows[i])[2] == deposit[12])
        inspect('active-coarse-overlap', replace(data, [2, 3, 5, fine_index, 2], deposit[12]), False)
        inspect('funded-source-hole', replace(data, [2, 3, 5, coarse_index, 6], u64(21)), False)
        crossing = replace(data, [2, 3, 5, fine_index, 7], u64(21))
        crossing = replace(crossing, [2, 3, 5, coarse_index, 6], u64(21))
        inspect('fine-crosses-cell', crossing, False)
        inspect('future-lot-revision', replace(data, [2, 3, 5, fine_index, 8], u64(999)), False)
        inspect('future-storage-revision', replace(data, [2, 3, 4, 1, 3], u64(999)), False)
        for length in [0, 11, len(data) - 1]:
            inspect('truncated-' + str(length), data[:length], False)
        inspect('trailing', data + b'\0', False)
        inspect('over-budget', bytes(5_710_001), False)
        changed = bytearray(data); changed[7] = 2
        inspect('unknown-frame-version', changed, False)
        report['status'] = 'passed'
    except Exception as error:
        report['status'], report['failure'] = 'failed', repr(error)
        raise
    finally:
        report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        output.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(dict(status=report['status'], inspections=len(report['runs']), report=str(output))))


if __name__ == '__main__':
    main()
