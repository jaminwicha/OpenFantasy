"""Independent origin-path runoff accounting, source funding and headroom checks through the real host.

This does not independently derive the terrain or claim hydraulic timing, climate, global reservation
ownership or geometric reservoir shape. Candidate generation is read-only.
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


def instant(data):
    days, milliseconds = struct.unpack('>qi', data)
    assert 0 <= milliseconds < 86_400_000
    return days * 86_400_000 + milliseconds, f'{days}:{milliseconds:08d}'


def oracle(data):
    outer = fields(data)
    assert outer[0] == b'geography.watershed.runoff.v1' and len(outer) == 4
    request = fields(outer[1])
    assert request[0] == b'geography.watershed.runoff-request.v1' and len(request) == 14
    network, mapping, authority = fields(request[1]), fields(request[2]), fields(request[3])
    custody = fields(authority[3]); raw = [fields(l) for l in fields(custody[5])[1:]]
    basins = {b[2]: b for b in members(network[6])}
    closures = {row[0]: row[1:] for row in struct.iter_unpack('>16sI16sii', network[7])}
    inputs = list(struct.iter_unpack('>16sQ16sQQ16s', request[12]))
    receivers = list(struct.iter_unpack('>16s16sQ', request[13]))
    assert set(basins) == {i[0] for i in inputs} and len(inputs) == len(basins)
    assert instant(request[7])[0] < instant(request[8])[0]
    unit_volume, side = integer(fields(request[11])[6]), integer(mapping[1])
    units, claims, total = {}, [], 0
    for basin, depth, source, start, end, storage in inputs:
        volume = depth * side * side * 1000
        assert volume % unit_volume == 0
        units[basin] = volume // unit_volume
        count = len(basins[basin][4]) // 44
        if not depth:
            assert (source, start, end, storage) == (bytes(16), 0, 0, bytes(16))
            continue
        assert end - start == units[basin] * count
        assert all(source != other or end <= lo or start >= hi for other, lo, hi, _ in claims)
        claims.append((source, start, end, storage)); total += end - start
        cursor = start
        for lot in sorted(raw, key=lambda l: integer(l[6])):
            lo, hi = max(start, integer(lot[6])), min(end, integer(lot[7]))
            if lot[5] != source or lo >= hi:
                continue
            assert lo == cursor and lot[2] == storage and lot[3:5] == request[9:11]
            assert lot[10] == request[5] and integer(lot[9]) == 0
            cursor = hi
        assert cursor == end
    for lot in raw:
        if lot[10] == request[5]:
            covered = sum(max(0, min(hi, integer(lot[7])) - max(lo, integer(lot[6])))
                for source, lo, hi, storage in claims if source == lot[5] and storage == lot[2])
            assert covered == integer(lot[7]) - integer(lot[6])
    # Follow each origin independently, unlike the managed topological accumulation pass.
    counts = {(bid, i): 0 for bid, basin in basins.items() for i in range(len(basin[4]) // 44)}
    terminal_totals = {bid: 0 for bid, closure in closures.items() if closure[0] == 2}
    for origin in counts:
        at, seen, amount = origin, set(), units[origin[0]]
        while True:
            assert at not in seen
            seen.add(at); counts[at] += amount
            downstream = struct.unpack_from('>i', basins[at[0]][4], at[1] * 44 + 16)[0]
            if downstream >= 0:
                at = at[0], downstream
            elif closures[at[0]][0] == 1:
                at = closures[at[0]][1], closures[at[0]][2]
            else:
                terminal_totals[at[0]] += amount
                break
    expected_columns = [counts[(bid, i)] for bid, basin in basins.items() for i in range(len(basin[4]) // 44)]
    assert [v[0] for v in struct.iter_unpack('>Q', outer[2])] == expected_columns
    assert sum(terminal_totals.values()) == total
    assert set(terminal_totals) == {r[0] for r in receivers}
    encoded_totals = list(struct.iter_unpack('>16s16sQ', outer[3]))
    assert encoded_totals == [(bid, storage, terminal_totals[bid]) for bid, storage, _ in receivers]
    existing = {}
    for lot in raw:
        existing[lot[2]] = existing.get(lot[2], 0) + integer(lot[7]) - integer(lot[6])
    if request[4]:
        for value in fields(fields(request[4])[7])[1:]:
            lot = fields(value); claim = fields(lot[6])
            existing[lot[3]] = existing.get(lot[3], 0) + integer(claim[5]) - integer(claim[4])
            if any(storage == lot[3] for _, storage, _ in receivers): assert lot[4:6] == request[9:11]
    storage_limits = {fields(s)[1]: integer(fields(s)[2]) for s in fields(custody[4])[1:]}
    grouped = {}
    for bid, storage, capacity in receivers:
        incoming, prior_capacity = grouped.get(storage, (0, capacity))
        assert capacity == prior_capacity
        grouped[storage] = incoming + terminal_totals[bid], capacity
    report_receivers = []
    for storage, (incoming, capacity) in sorted(grouped.items()):
        current, projected = existing.get(storage, 0), existing.get(storage, 0) + incoming
        assert projected <= storage_limits[storage] and projected * unit_volume <= capacity
        assert all(l[3:5] == request[9:11] for l in raw if l[2] == storage)
        report_receivers.append(dict(CustodianId=storage.hex(), ExistingUnits=current, IncomingUnits=incoming,
            ProjectedUnits=projected, ProjectedVolumeMicrolitres=projected * unit_volume, CapacityMicrolitres=capacity))
    return dict(ContentHash=hashlib.sha256(data).hexdigest(), RequestHash=hashlib.sha256(outer[1]).hexdigest(),
        NetworkHash=hashlib.sha256(request[1]).hexdigest(), ColumnCount=len(counts), Basins=len(basins), Revision=integer(custody[3]),
        ReservationId=request[5].hex(), Start=instant(request[7])[1], End=instant(request[8])[1], TotalInputUnits=total,
        TotalInputVolumeMicrolitres=total * unit_volume, Receivers=report_receivers,
        Terminals=[dict(BasinId=bid.hex(), CustodianId=storage.hex(), IncomingUnits=amount) for bid, storage, amount in encoded_totals])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', type=Path, required=True)
    parser.add_argument('--fixture', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root, output, dotnet = Path(__file__).resolve().parents[1], args.output.resolve(), args.dotnet.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(), runs=[],
        scope='Read-only source interval, weighted origin-path and physical/counted headroom checks; no hydraulic or global reservation qualification.')
    with tempfile.TemporaryDirectory(prefix='runoff-inspections-') as temporary:
        def inspect(name, data, valid=False):
            path = Path(temporary) / (name + '.bin'); path.write_bytes(data)
            command = [str(dotnet), str(root / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll'), '--inspect-runoff-budget', str(path)]
            run = dict(name=name, inputSha256=hashlib.sha256(data).hexdigest(), byteLength=len(data), command=command)
            report['runs'].append(run)
            try:
                result = subprocess.run(command, cwd=root, env=dict(os.environ, DOTNET_ROOT=str(dotnet.parent)),
                    capture_output=True, text=True, timeout=30)
            except subprocess.TimeoutExpired as error:
                run.update(timedOut=True, timeoutSeconds=30, stdout=str(error.stdout or ''), stderr=str(error.stderr or ''))
                raise
            run.update(exitCode=result.returncode, stdout=result.stdout, stderr=result.stderr)
            assert path.read_bytes() == data and not result.stderr
            assert result.returncode == (0 if valid else 1), (name, result.stdout, result.stderr)
            if valid:
                actual, expected = json.loads(result.stdout), oracle(data)
                assert actual['Contract'] == 'watershed-runoff-v1' and actual['Scope'] == 'standalone-funded-runoff-potential'
                for key, value in expected.items(): assert actual[key] == value, (name, key, actual[key], value)
                run['independentOracle'] = expected
            else:
                assert result.stdout.startswith('Inspection failed:')
        try:
            for name in ['unequal-runoff', 'shared-receiver', 'dry', 'crafted-occupancy', 'maximum-network']:
                inspect(name, (args.fixture / (name + '.bin')).read_bytes(), True)
            data = (args.fixture / 'unequal-runoff.bin').read_bytes()
            outer, request = fields(data), fields(fields(data)[1])
            for name, path, value in [('missing-input', [1, 12], request[12][:-72]),
                ('missing-terminal', [1, 13], b''), ('unreserved-funding', [1, 3, 3, 5, 1, 10], b''),
                ('funding-lot-removed', [1, 3, 3, 5], frame(fields(fields(fields(request[3])[3])[5])[:1] + fields(fields(fields(request[3])[3])[5])[2:])),
                ('reversed-time', [1, 8], request[7]), ('wrong-owner', [1, 6], request[5]),
                ('wrong-item-pin', [1, 10], bytes(32)), ('production-omission-trailing', [1, 4], b'bad')]:
                inspect(name, replace(data, path, value))
            inputs = list(struct.iter_unpack('>16sQ16sQQ16s', request[12]))
            wet = next(i for i, row in enumerate(inputs) if row[1] == 1000)
            other = next(i for i, row in enumerate(inputs) if row[1] == 2000)
            for name, index, values in [('fractional-unit', wet, {1:1}), ('source-overlap', other, {3:199, 4:599}),
                ('source-underfunded', wet, {4:199})]:
                changed = [list(r) for r in inputs]
                for k, v in values.items(): changed[index][k] = v
                inspect(name, replace(data, [1, 12], b''.join(struct.pack('>16sQ16sQQ16s', *r) for r in changed)))
            changed = bytearray(request[12]); dry = next(i for i, row in enumerate(inputs) if row[1] == 0); changed[dry * 72 + 24] = 1
            inspect('dry-hidden-funding', replace(data, [1, 12], changed))
            receiver = struct.unpack('>16s16sQ', request[13])[1]
            storages = fields(fields(fields(request[3])[3])[4])
            inspect('counted-headroom', replace(data, [1, 3, 3, 4], frame([storages[0]] + [
                replace(s, [2], u64(649)) if fields(s)[1] == receiver else s for s in storages[1:]])))
            inspect('physical-headroom', replace(data, [1, 13], request[13][:-8] + u64(649_999_999)))
            shared = (args.fixture / 'shared-receiver.bin').read_bytes(); shared_rows = fields(fields(shared)[1])[13]
            inspect('competing-terminals', replace(shared, [1, 13], b''.join(row[:32] + u64(500_000_000)
                for row in [shared_rows[i:i + 40] for i in range(0, len(shared_rows), 40)])))
            for field in [2, 3]:
                changed = bytearray(outer[field]); changed[-1] ^= 1
                inspect('changed-derived-field-' + str(field), replace(data, [field], changed))
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
