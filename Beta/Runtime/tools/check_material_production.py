"""Independent direct-retirement interval accounting and real read-only host inspection.

The interval oracle does not reimplement recipe mass allocation or the derived-output DAG.
Retired raw claims are represented as temporary oracle-only rows to reuse the spatial checks;
these rows are never submitted to the host or written as game state.
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
from check_material_registry import allocations, encode_allocations, oracle as raw_registry_oracle
from check_terrain_materials import fields, frame, integer, replace, u64


def oracle(data):
    outer = fields(data)
    assert len(outer) == 5 and outer[0] == b'geography.terrain-material-registry.v2'
    production = fields(outer[4])
    assert len(production) == 8 and production[0] == b'production.snapshot' and integer(production[1]) == 1
    authority, custody = fields(outer[2]), fields(fields(outer[2])[3])
    assert production[2:4] == custody[1:3]
    raw = fields(custody[5])
    claims, synthetic = [], []
    for batch_bytes in fields(production[6])[1:]:
        batch = fields(batch_bytes)
        for ingredient_bytes in fields(batch[8])[1:]:
            ingredient = fields(ingredient_bytes)
            if integer(ingredient[2]) != 1:
                continue
            claim = fields(ingredient[7])
            assert claim[0] == b'production.source-claim'
            source, lo, hi = claim[2], integer(claim[3]), integer(claim[4])
            assert lo < hi
            assert all(source != s or hi <= a or lo >= b for s, a, b in claims)
            claims.append((source, lo, hi))
            # A distinct analysis-only custodian counts retirement outside live terrain storage.
            synthetic.append(frame([b'custody.source-lot.v1', hashlib.sha256(ingredient_bytes).digest()[:16], bytes.fromhex('ff' * 16),
                ingredient[5], ingredient[6], source, u64(lo), u64(hi), u64(0), u64(0), b'']))
    augmented = replace(outer[2], [3, 5], frame(raw + synthetic))
    expected = raw_registry_oracle(frame([b'geography.terrain-material-registry.v1', outer[1], augmented, outer[3]]))
    retired = 0
    for _, encoded, _ in allocations(outer[3]):
        binding = fields(encoded)
        source, start, end = binding[8], integer(binding[9]), integer(binding[10])
        retired += sum(max(0, min(end, b) - max(start, a)) for s, a, b in claims if s == source)
    expected['OtherQuantity'] -= retired
    expected.update(RetiredQuantity=retired, LotCount=len(raw) - 1, ContentHash=hashlib.sha256(data).hexdigest(),
        ProductionRevision=integer(production[4]), ProductionBatches=len(fields(production[6])) - 1,
        DerivedLotCount=len(fields(production[7])) - 1)
    assert sum(expected[k] for k in ['CoarseQuantity', 'DetailedQuantity', 'OtherQuantity', 'RetiredQuantity']) == expected['AllocatedQuantity']
    return expected


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', type=Path, required=True)
    parser.add_argument('--fixture', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root, output, dotnet = Path(__file__).resolve().parents[1], args.output.resolve(), args.dotnet.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(), runs=[],
        scope='Standalone archives; independent direct-raw retirement/spatial ownership oracle. Recipe mass and derived DAG use existing managed proofs.')
    with tempfile.TemporaryDirectory(prefix='material-production-') as temporary:
        def inspect(name, data, valid=False):
            path = Path(temporary) / (name + '.bin')
            path.write_bytes(data)
            command = [str(dotnet), str(root / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll'),
                '--inspect-material-production', str(path)]
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
                value, expected = json.loads(result.stdout), oracle(data)
                assert value['Contract'] == 'terrain-material-registry-v2'
                for key, wanted in expected.items():
                    assert value[key] == wanted, (name, key, value[key], wanted)
                run['independentOracle'] = expected
            else:
                assert result.stdout.startswith('Inspection failed:'), (name, result.stdout)
        try:
            for name, retired in [('attached', 0), ('crafted-reloaded', 4), ('chained-prefixes', 8), ('combined-lot-cap', 4)]:
                data = (args.fixture / (name + '.bin')).read_bytes()
                assert oracle(data)['RetiredQuantity'] == retired
                inspect(name, data, True)
            data = (args.fixture / 'crafted-reloaded.bin').read_bytes()
            outer = fields(data)
            binding = fields(allocations(outer[3])[0][1])
            for name, value in [('coarse-consumption', binding[11]), ('fine-consumption', binding[12])]:
                inspect(name, replace(data, [4, 6, 1, 5], value))
            inspect('proof-removed', frame(outer[:4]))
            inspect('raw-profile-downgrade', frame([b'geography.terrain-material-registry.v1'] + outer[1:]))
            inspect('unknown-production-version', replace(data, [4, 1], u64(2)))
            inspect('future-production-revision', replace(data, [4, 4], u64(2**64 - 1)))
            inspect('production-branch-mismatch', replace(data, [4, 3], bytes.fromhex('ee' * 16)))
            inspect('lost-retirement', replace(replace(data, [4, 6], frame([b'production.batches'])), [4, 7], frame([b'production.lots'])))
            claim = fields(fields(fields(fields(fields(outer[4])[6])[1])[8])[1])
            source = fields(claim[7])
            inspect('shift-retired-range', replace(replace(data, [4, 6, 1, 8, 1, 7, 3], u64(integer(source[3]) + 1)),
                [4, 6, 1, 8, 1, 7, 4], u64(integer(source[4]) + 1)))
            rows = allocations(outer[3])
            rows[1][0] = fields(fields(fields(outer[4])[6])[1])[2]
            rows.sort(key=lambda r: r[0])
            inspect('allocation-batch-alias', replace(data, [3], encode_allocations(rows)))
            raw = fields(fields(fields(outer[2])[3])[5])
            revived = frame([b'custody.source-lot.v1', (150000).to_bytes(16, 'big'), fields(fields(fields(outer[4])[6])[1])[5],
                claim[5], claim[6], source[2], source[3], source[4], u64(0), u64(0), b''])
            live = raw[1:] + [revived]; live.sort(key=lambda b: fields(b)[1])
            inspect('revived-consumed-source', replace(data, [2, 3, 5], frame([raw[0]] + live)))
            storages = fields(fields(fields(outer[2])[3])[4])
            away = fields(fields(fields(outer[4])[6])[1])[5]
            inspect('combined-storage-capacity', replace(data, [2, 3, 4], frame([storages[0]] + [
                replace(s, [2], u64(3)) if fields(s)[1] == away else s for s in storages[1:]])))
            cap = (args.fixture / 'combined-lot-cap.bin').read_bytes()
            lots = fields(fields(fields(fields(cap)[2])[3])[5])
            extra = fields(lots[-1]); extra[1] = (200000).to_bytes(16, 'big'); extra[6:8] = [u64(4500), u64(4501)]
            more = lots[1:] + [frame(extra)]; more.sort(key=lambda b: fields(b)[1])
            inspect('combined-lot-overflow', replace(cap, [2, 3, 5], frame([lots[0]] + more)))
            for name, changed in [('truncated', data[:-1]), ('trailing', data + b'\0'), ('oversized', bytes(23_100_001))]:
                inspect(name, changed)
            report['status'] = 'passed'
        except Exception as error:
            report.update(status='failed', failure=repr(error))
            raise
        finally:
            report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
            output.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(dict(status=report['status'], inspections=len(report['runs']), report=str(output))))


if __name__ == '__main__':
    main()
