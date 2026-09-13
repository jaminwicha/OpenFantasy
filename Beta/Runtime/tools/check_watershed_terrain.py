"""Run the real host inspector against independently encoded terrain candidates and invalid archives. No world writes."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import struct
import subprocess
import tempfile


def field_offset(data, index):
    offset = 12
    for _ in range(index):
        offset += 4 + struct.unpack_from('>I', data, offset)[0]
    return offset + 4


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', type=Path, required=True)
    parser.add_argument('--maximum-fixture', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix='terrain-inspections-', dir=output.parent))
    report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope='Read-only real host archive inspection; no lived terrain admission, material rights or fluid qualification', runs=[])
    dotnet = args.dotnet.resolve()
    command = [str(dotnet), str(root / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll'), '--inspect-watershed-terrain']

    def inspect(name, data, expected_exit=0):
        path = work / (name + '.bin')
        path.write_bytes(data)
        selected = command + [str(path)]
        try:
            result = subprocess.run(selected, cwd=root, env=dict(os.environ, DOTNET_ROOT=str(dotnet.parent)),
                                    capture_output=True, text=True, timeout=30)
        except subprocess.TimeoutExpired as error:
            report['runs'].append(dict(name=name, timedOut=True, timeoutSeconds=30,
                stdout=str(error.stdout or ''), stderr=str(error.stderr or '')))
            raise
        report['runs'].append(dict(name=name, command=selected, inputSha256=hashlib.sha256(data).hexdigest(),
                                  exitCode=result.returncode, stdout=result.stdout, stderr=result.stderr))
        assert result.returncode == expected_exit, (name, result.stdout, result.stderr)
        assert not result.stderr, (name, result.stderr)
        assert path.read_bytes() == data, 'Inspection changed its input'
        if expected_exit == 0:
            return json.loads(result.stdout)
        assert result.stdout.startswith('Inspection failed:'), result.stdout

    try:
        fixtures = root / 'tests/OpenFantasy.Core.Tests/Fixtures'
        generated = subprocess.check_output(['python3', str(fixtures / 'generate_watershed_terrain_vectors.py')])
        assert generated == (fixtures / 'watershed-terrain-v1.json').read_bytes()
        cases = json.loads(generated)['examples']
        for case in cases:
            value = inspect(case['name'], bytes.fromhex(case['preparedHex']))
            assert value['Contract'] == 'watershed-terrain-v1' and value['Scope'] == 'dry-geometry-candidate'
            assert value['ContentHash'] == case['preparedHash'] and value['MappingHash'] == case['mappingHash']
            assert value['SubstrateCellCount'] == case['substrateCells'] and value['ColumnCount'] == case['columnCount']
        data = bytes.fromhex(cases[0]['preparedHex'])
        for field in range(1, 5):
            changed = bytearray(data)
            changed[field_offset(changed, field)] ^= 0x80
            inspect('tamper-field-' + str(field), changed, 1)
        for offset in [0, 16, 20, 36, 52, 60, 64]:
            changed = bytearray(data)
            changed[field_offset(changed, 4) + offset] ^= 0x80
            inspect('tamper-column-' + str(offset), changed, 1)
        for length in [0, 11, 15, len(data) - 1]:
            inspect('truncate-' + str(length), data[:length], 1)
        inspect('trailing', data + b'\0', 1)
        inspect('over-budget', bytes(4_100_001), 1)
        changed = bytearray(data); changed[7] = 2
        inspect('unknown-frame-version', changed, 1)
        # Change an otherwise valid mapping datum without updating its witnessed surfaces.
        changed = bytearray(data); start = field_offset(changed, 2)
        position = start + field_offset(changed[start:], 3)
        changed[position:position + 8] = struct.pack('>q', 0)
        inspect('changed-mapping-stale-columns', changed, 1)
        # Move a complete requested window outside the available macro footprint.
        changed = bytearray(data); start = field_offset(changed, 3)
        position = start + field_offset(changed[start:], 1)
        changed[position:position + 8] = struct.pack('>q', -7)
        inspect('missing-macro-coverage', changed, 1)
        maximum = inspect('maximum', (args.maximum_fixture / 'maximum.bin').read_bytes())
        expected = json.loads((args.maximum_fixture / 'expected.json').read_text())
        for key, value in expected.items():
            assert maximum[key] == value, (key, maximum[key], value)
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
