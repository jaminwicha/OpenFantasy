"""Check normalized registries through an independent ownership oracle and the real read-only host."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import struct
import subprocess
import tempfile
from check_terrain_materials import fields, frame, integer, oracle as material_oracle, replace, u64


def allocations(data):
    count = struct.unpack_from('>I', data)[0]
    offset, result = 4, []
    for _ in range(count):
        identity = data[offset:offset + 16]; offset += 16
        length = struct.unpack_from('>I', data, offset)[0]; offset += 4
        binding = data[offset:offset + length]; offset += length
        count = struct.unpack_from('>I', data, offset)[0]; offset += 4
        active = data[offset:offset + count * 24]; offset += count * 24
        assert len(identity) == 16 and len(binding) == length and len(active) == count * 24
        result.append([identity, binding, active])
    assert offset == len(data)
    return result


def encode_allocations(rows):
    return struct.pack('>I', len(rows)) + b''.join(identity + struct.pack('>I', len(binding)) + binding
        + struct.pack('>I', len(active) // 24) + active for identity, binding, active in rows)


def oracle(data):
    outer = fields(data)
    assert outer[0] == b'geography.terrain-material-registry.v1' and len(outer) == 4
    rows = allocations(outer[3])
    assert 1 <= len(rows) <= 64 and [r[0] for r in rows] == sorted({r[0] for r in rows})
    total = dict(CoarseQuantity=0, DetailedQuantity=0, OtherQuantity=0, AllocatedQuantity=0, ActiveCellCount=0)
    seen, states = [], []
    for identity, encoded, active in rows:
        binding = fields(encoded)
        assert binding[0] == b'geography.terrain-material-binding.v1' and len(binding) == 13
        deposit = frame([b'geography.terrain-material-deposit.v1', outer[1]] + binding[1:])
        state = material_oracle(frame([b'geography.terrain-material-state.v1', deposit, outer[2], active]))
        for prior_binding, prior in seen:
            assert binding[8] != prior_binding[8] or state['End'] <= prior['Start'] or state['Start'] >= prior['End']
            assert not all(state['Minimum'][axis] <= prior['Maximum'][axis]
                and prior['Minimum'][axis] <= state['Maximum'][axis] for axis in ['X', 'Y', 'Z'])
        seen.append((binding, state))
        for key in ['CoarseQuantity', 'DetailedQuantity', 'OtherQuantity', 'ActiveCellCount']:
            total[key] += state[key]
        total['AllocatedQuantity'] += state['End'] - state['Start']
        states.append(dict(AllocationId=identity.hex(), SourceId=binding[8].hex(), Start=state['Start'], End=state['End'],
                           ActiveCells=state['ActiveCellCount']))
    assert total['ActiveCellCount'] <= 4096
    assert sum(total[k] for k in ['CoarseQuantity', 'DetailedQuantity', 'OtherQuantity']) == total['AllocatedQuantity']
    custody = fields(fields(outer[2])[3])
    network = fields(fields(outer[1])[1])
    return dict(total, ContentHash=hashlib.sha256(data).hexdigest(), Revision=integer(custody[3]),
                Allocations=len(rows), NetworkColumns=len(network[8]) // 4,
                LotCount=len(fields(custody[5])) - 1, AllocationStates=states)


def python_coarse(data):
    outer = fields(data)
    rows = allocations(outer[3])
    first, last = fields(rows[0][1]), fields(rows[-1][1])
    assert first[8] == last[8]
    lot = frame([b'custody.source-lot.v1', (999000).to_bytes(16, 'big'), first[11], first[4], first[5], first[8],
                 first[9], last[10], u64(0), u64(0), b''])
    outer[2] = replace(outer[2], [3, 5], frame([b'custody.lots.v1', lot]))
    outer[2] = replace(outer[2], [3, 3], u64(0))
    storages = fields(fields(fields(outer[2])[3])[4])
    outer[2] = replace(outer[2], [3, 4], frame([storages[0]] + [replace(s, [3], u64(0)) for s in storages[1:]]))
    outer[3] = encode_allocations([[r[0], r[1], b''] for r in rows])
    return frame(outer)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', type=Path, required=True)
    parser.add_argument('--fixture', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root, output, dotnet = Path(__file__).resolve().parents[1], args.output.resolve(), args.dotnet.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix='registry-inspections-', dir=output.parent))
    report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(), runs=[],
                  scope='Read-only normalized material ownership, independent interval/spatial overlap oracle; no durable world mutation.')

    def inspect(name, data, valid=True):
        path = work / (name + '.bin'); path.write_bytes(data)
        command = [str(dotnet), str(root / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll'), '--inspect-material-registry', str(path)]
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
            value = json.loads(result.stdout)
            assert value['Contract'] == 'terrain-material-registry-v1' and value['Scope'] == 'standalone-shared-material-ownership'
            expected = oracle(data)
            for key, wanted in expected.items():
                assert value[key] == wanted, (name, key, value[key], wanted)
            report['runs'][-1]['independentOracle'] = expected
        else:
            assert result.stdout.startswith('Inspection failed:')

    try:
        for name in ['two-allocations', 'interior-transfer', 'maximum-network', 'active-cap']:
            inspect(name, (args.fixture / (name + '.bin')).read_bytes())
        data = (args.fixture / 'two-allocations.bin').read_bytes()
        inspect('python-shared-coarse', python_coarse(data))
        rows = allocations(fields(data)[3])
        for name, changed in [('reverse-allocations', rows[::-1]), ('duplicate-identity', [rows[0], [rows[0][0], *rows[1][1:]]]),
                              ('missing-active-binding', [[r[0], r[1], b''] for r in rows])]:
            inspect(name, replace(data, [3], encode_allocations(changed)), False)
        for name, field, replacement in [('reuse-source', 9, fields(rows[0][1])[9]),
                                          ('overlap-space', 2, fields(rows[0][1])[2]),
                                          ('item-pin-conflict', 5, bytes(32)),
                                          ('coarse-fine-role-conflict', 12, fields(rows[0][1])[11])]:
            modified = [list(r) for r in rows]
            modified[1][1] = replace(modified[1][1], [field], replacement)
            if name == 'reuse-source':
                modified[1][1] = replace(modified[1][1], [10], fields(rows[0][1])[10])
            inspect(name, replace(data, [3], encode_allocations(modified)), False)
        physical = [list(r) for r in rows]
        physical[1][1] = replace(physical[1][1], [6, 5], u64(250_000_001))
        inspect('physical-version-conflict', replace(data, [3], encode_allocations(physical)), False)
        identity = [list(r) for r in rows]; identity[0][0] = fields(rows[0][1])[8]
        identity.sort(key=lambda r: r[0])
        inspect('allocation-source-role-alias', replace(data, [3], encode_allocations(identity)), False)
        for count in [0, 65, 2**32 - 1]:
            inspect('allocation-count-' + str(count), replace(data, [3], struct.pack('>I', count)), False)
        oversized_binding = bytearray(fields(data)[3]); oversized_binding[20:24] = struct.pack('>I', 8193)
        inspect('binding-size-limit', replace(data, [3], oversized_binding), False)
        inspect('wrong-branch', replace(data, [2, 3, 2], (998999).to_bytes(16, 'big')), False)
        cap = (args.fixture / 'active-cap.bin').read_bytes()
        excessive = allocations(fields(cap)[3]); excessive[1][2] = struct.pack('>qqq', 0, 0, 0)
        inspect('global-cell-budget', replace(cap, [3], encode_allocations(excessive)), False)
        for length in [0, 11, len(data) - 1]:
            inspect('truncate-' + str(length), data[:length], False)
        inspect('trailing', data + b'\0', False)
        inspect('oversized', bytes(6_250_001), False)
        changed = bytearray(data); changed[7] = 2
        inspect('unknown-version', changed, False)
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
