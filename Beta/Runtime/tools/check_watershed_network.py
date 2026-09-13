"""Run the real host inspector against independently encoded networks and invalid archives. No world writes."""
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
    work = Path(tempfile.mkdtemp(prefix='network-inspections-', dir=output.parent))
    report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope='Read-only real host archive inspection; no saved-world admission or fluid qualification', runs=[])
    dotnet = args.dotnet.resolve()
    command = [str(dotnet), str(root / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll'), '--inspect-watershed-network']

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
        for name in ['watershed', 'watershed_network']:
            encoded = subprocess.check_output(['python3', str(fixtures / ('generate_' + name + '_vectors.py'))])
            assert encoded == (fixtures / (name.replace('_', '-') + '-v1.json')).read_bytes()
        cases = json.loads((fixtures / 'watershed-network-v1.json').read_text())['examples']
        for case in cases:
            value = inspect(case['name'], bytes.fromhex(case['preparedHex']))
            assert value['Contract'] == 'watershed-network-v1' and value['Scope'] == 'finite-macro-candidate'
            assert value['ContentHash'] == case['preparedHash']
            assert value['BasinCount'] == case['basinCount'] and value['ColumnCount'] == case['columnCount']
            assert value['SharedFaceCount'] == len(case['faceIds'])
            assert [(t['BasinId'], t['ContributingColumns'], t['ColumnCapacity']) for t in value['TerminalContributions']] == [
                (t['basinId'], t['count'], t['capacity']) for t in case['terminalContributions']]
        data = bytes.fromhex(cases[0]['preparedHex'])
        for field in range(1, 10):
            changed = bytearray(data)
            changed[field_offset(changed, field)] ^= 0x80
            inspect('tamper-field-' + str(field), changed, 1)
        for length in [0, 11, 15, len(data) - 1]:
            inspect('truncate-' + str(length), data[:length], 1)
        inspect('trailing', data + b'\0', 1)
        inspect('over-budget', bytes(4_000_001), 1)
        changed = bytearray(data); changed[7] = 2
        inspect('unknown-frame-version', changed, 1)
        # Terminal union has no receiver or receiving column; unused fields must remain canonical zero.
        changed = bytearray(data); start = field_offset(changed, 7)
        for index in range(cases[0]['basinCount']):
            row = start + index * 44
            if struct.unpack_from('>I', changed, row + 16)[0] == 2:
                changed[row + 20] = 1
                break
        inspect('terminal-unused-receiver', changed, 1)
        maximum = inspect('maximum', (args.maximum_fixture / 'maximum.bin').read_bytes())
        expected = json.loads((args.maximum_fixture / 'expected.json').read_text())
        for key, value in expected.items():
            assert maximum[key] == value, (key, maximum[key], value)
        assert sum(t['ContributingColumns'] for t in maximum['TerminalContributions']) == 65_536
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
