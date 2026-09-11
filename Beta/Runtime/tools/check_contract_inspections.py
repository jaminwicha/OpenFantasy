"""Exercise the built host's bounded diagnostic inputs and retained source package."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    host = root / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll'
    fixtures = root / 'tests/OpenFantasy.Core.Tests/Fixtures'
    commands = json.loads((fixtures / 'commands-v1.json').read_text())
    packages = json.loads((fixtures / 'packages-v1.json').read_text())
    custody = json.loads((fixtures / 'custody-v1.json').read_text())
    invalid_custody = json.loads((fixtures / 'custody-invalid-v1.json').read_text())
    dotnet = Path(args.dotnet).resolve()
    env = dict(os.environ, DOTNET_ROOT=str(dotnet.parent))
    report = {'scope': 'local structural diagnostics and source-packet bytes', 'status': 'running', 'cases': []}

    def run(name, arguments, expected=0):
        result = subprocess.run([str(dotnet), str(host), *map(str, arguments)],
                                cwd=root, env=env, capture_output=True, text=True, timeout=30)
        report['cases'].append({'name': name, 'expectedExit': expected, 'exitCode': result.returncode,
                                'stdout': result.stdout, 'stderr': result.stderr})
        if result.returncode != expected:
            raise RuntimeError(f'{name}: unexpected exit {result.returncode}: {result.stdout}{result.stderr}')
        if expected == 1 and not result.stdout.startswith('Inspection failed: '):
            raise RuntimeError(name + ': missing controlled validation failure')
        return result.stdout

    try:
        with tempfile.TemporaryDirectory(prefix='openfantasy-inspection-') as temporary:
            folder = Path(temporary)
            command_file = folder / 'command.bin'
            command_file.write_bytes(bytes.fromhex(commands['commandHex']))
            value = json.loads(run('canonical command', ['--inspect-command', command_file]))
            expected_ids = {'CommandId': 'command', 'PrincipalId': 'principal', 'OriginSessionId': 'originSession',
                            'ControlledActorId': 'actor', 'WorldId': 'world', 'BranchId': 'branch'}
            if (value['Contract'] != 'command-v1' or value['Scope'] != 'structural-validation'
                    or value['ContentHash'] != commands['contentHash']
                    or value['Intent']['Quantity'] != int(commands['quantity'])
                    or value['ExpectedRevisionCount'] != len(commands['expectedRevisions'])
                    or any(value[key] != commands['ids'][source] for key, source in expected_ids.items())):
                raise RuntimeError('Host command projection differs from independent fixture')
            for case in commands['invalidCommands']:
                command_file.write_bytes(bytes.fromhex(case['hex']))
                run('command: ' + case['name'], ['--inspect-command', command_file], 1)
            command_file.write_bytes(b'\0' * (16 * 1024 + 1))
            run('command byte limit', ['--inspect-command', command_file], 1)
            command_file.write_bytes(b'')
            run('empty command', ['--inspect-command', command_file], 1)
            run('missing file', ['--inspect-command', folder / 'absent.bin'], 1)
            run('unknown arguments', ['--not-a-command'], 2)
            run('missing argument', ['--inspect-command'], 2)
            run('usage', [])

            watershed = json.loads((fixtures / 'watershed-v1.json').read_text())
            basin_file = folder / 'basin.bin'
            for example in watershed['examples']:
                basin_file.write_bytes(bytes.fromhex(example['preparedHex']))
                value = json.loads(run('watershed: ' + example['name'], ['--inspect-watershed', basin_file]))
                if (value['Contract'] != 'watershed-preparation-v1' or value['Scope'] != 'finite-macro-candidate'
                        or value['ContentHash'] != example['preparedHash']
                        or value['ColumnCount'] != example['width'] * example['depth']
                        or value['OutletContributingColumns'] != value['ColumnCount']
                        or value['SharedBoundaryCount'] != len(example['boundaries'])):
                    raise RuntimeError('Host watershed projection differs from the independent preparation')
            corrupted = bytearray(basin_file.read_bytes())
            corrupted[-1] ^= 1
            basin_file.write_bytes(corrupted)
            run('watershed corrupted derivation', ['--inspect-watershed', basin_file], 1)
            basin_file.write_bytes(b'\0' * 200_001)
            run('watershed byte limit', ['--inspect-watershed', basin_file], 1)

            custody_file = folder / 'custody.bin'
            custody_file.write_bytes(bytes.fromhex(custody['candidateHex']))
            value = json.loads(run('canonical custody', ['--inspect-custody', custody_file]))
            if (value['ContentHash'] != custody['candidateHash'] or value['CustodianCount'] != 2
                    or value['LotCount'] != 2 or value['Revision'] != 8
                    or value['Scope'] != 'snapshot-invariants'):
                raise RuntimeError('Host custody projection differs from independent fixture')
            for case in invalid_custody['cases']:
                custody_file.write_bytes(bytes.fromhex(case['hex']))
                run('custody: ' + case['name'], ['--inspect-custody', custody_file], 1)
            with custody_file.open('wb') as oversized:
                oversized.truncate(942_754)
            run('custody byte limit', ['--inspect-custody', custody_file], 1)

            metadata = {'schemaVersion': 1, 'id': 'openfantasy.rules',
                        'payloadSha256': packages['payloadSha256'], 'dependencies': []}
            manifest_file, payload_file = folder / 'manifest.json', folder / 'payload.json'
            manifest_file.write_text(json.dumps(metadata))
            payload_file.write_text(packages['payloadText'])
            value = json.loads(run('canonical package', ['--inspect-package', manifest_file, payload_file]))
            if (value['Version'] != packages['baseVersion'] or value['PackageId'] != metadata['id']
                    or value['Scope'] != 'manifest-and-payload-bytes' or value['Dependencies'] != []):
                raise RuntimeError('Host package projection differs from independent fixture')
            payload_file.write_bytes(b'changed')
            run('package hash mismatch', ['--inspect-package', manifest_file, payload_file], 1)
            with payload_file.open('wb') as oversized:
                oversized.truncate(16 * 1024 * 1024 + 1)
            run('payload byte limit', ['--inspect-package', manifest_file, payload_file], 1)
            payload_file.write_text(packages['payloadText'])
            for label, content in (
                ('truncated manifest', b'{'), ('manifest byte limit', b' ' * 65_537),
                ('duplicate metadata', json.dumps(metadata).replace('{', '{"schemaVersion":1,', 1).encode()),
                ('unknown schema', json.dumps(dict(metadata, schemaVersion=2)).encode()),
                ('invalid namespace', json.dumps(dict(metadata, id='INVALID')).encode()),
            ):
                manifest_file.write_bytes(content)
                run(label, ['--inspect-package', manifest_file, payload_file], 1)

        closure = root / 'content/legacy/aldermere-first-loop'
        wrapper = root / 'content/packages/aldermere-first-loop.manifest.json'
        value = json.loads(run('retained source package', ['--inspect-package', wrapper, closure / 'closure-manifest.json']))
        packet = json.loads((closure / 'closure-manifest.json').read_text())
        expected = {'closure-manifest.json'}
        for entry in packet['files']:
            relative = PurePosixPath(entry['path'])
            if (relative.is_absolute() or '..' in relative.parts or str(relative) != entry['path']
                    or entry['path'] in expected):
                raise RuntimeError('Invalid or duplicate source packet path')
            expected.add(entry['path'])
            path = closure / relative
            if not path.resolve().is_relative_to(closure.resolve()):
                raise RuntimeError('Source packet path escapes its root')
            data = path.read_bytes()
            if len(data) != entry['bytes'] or hashlib.sha256(data).hexdigest() != entry['sha256']:
                raise RuntimeError('Source packet file drift: ' + entry['path'])
        actual = {str(path.relative_to(closure)) for path in closure.rglob('*') if path.is_file()}
        if actual != expected:
            raise RuntimeError('Source packet file set differs from manifest')
        report['retainedPacket'] = {'fileCount': len(expected), 'counts': packet['counts'],
                                    'packageVersion': value['Version'], 'payloadHash': value['PayloadHash']}
        report['status'] = 'passed'
    except Exception as error:
        report['status'] = 'failed'
        report['failure'] = str(error)
    finally:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({'status': report['status'], 'hostCases': len(report['cases']),
                      'failure': report.get('failure')}))
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
