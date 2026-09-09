#!/usr/bin/env python3
"""Run the independent command mutation probe in an isolated temporary build."""
import argparse
import hashlib
import json
from pathlib import Path
import platform
import shutil
import subprocess
import tempfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
RUNTIME = ROOT / 'Beta/Runtime'


def fingerprint(path):
    return {'path': str(path.relative_to(ROOT)), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}


def run():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', default='/usr/local/share/dotnet/dotnet')
    parser.add_argument('--output', type=Path, default=HERE / 'RESULTS.json')
    args = parser.parse_args()
    fixture = RUNTIME / 'tests/OpenFantasy.Core.Tests/Fixtures/commands-v1.json'
    inputs = sorted((RUNTIME / 'src/OpenFantasy.Core/Commands').glob('*.cs'))
    inputs += [RUNTIME / 'src/OpenFantasy.Core/EntityId.cs', RUNTIME / 'src/OpenFantasy.Core/CanonicalEncodingV1.cs', fixture]
    inputs += [HERE / 'Probe.cs', HERE / 'Probe.csproj', HERE / 'run_probe.py']
    before = [fingerprint(path) for path in inputs]
    with tempfile.TemporaryDirectory(prefix='openfantasy-command-review-') as temporary:
        directory = Path(temporary)
        for name in ('Probe.cs', 'Probe.csproj'):
            shutil.copyfile(HERE / name, directory / name)
        (directory / 'global.json').write_text(json.dumps({'sdk': {'version': '8.0.413', 'rollForward': 'disable'}}))
        sdk = subprocess.run([args.dotnet, '--version'], cwd=directory, text=True, capture_output=True, check=True)
        command = [args.dotnet, 'run', '--project', str(directory / 'Probe.csproj'), '--configuration', 'Release', f'-p:RuntimeRoot={RUNTIME}', '--', str(fixture)]
        result = subprocess.run(command, cwd=directory, text=True, capture_output=True, check=True)
    after = [fingerprint(path) for path in inputs]
    if before != after:
        raise RuntimeError('Reviewed source changed while the probe ran; rerun against stable inputs.')
    parsed = json.loads(result.stdout.strip().splitlines()[-1])
    report = {
        'schemaVersion': 1, 'scope': 'Independent parser mutation and canonical re-encoding observation; no gameplay or supported-toolchain qualification',
        'sdk': sdk.stdout.strip(), 'targetFramework': 'net8.0', 'platform': platform.platform(),
        'procedure': 'Each single-bit mutation of retained command/applied/rejected golden frames plus 10000 deterministic random frames (System.Random seed19873). Every accepted value must re-encode to the exact same bytes; only typed CommandFormatException rejections are tolerated.',
        'results': parsed, 'exitCode': result.returncode, 'stderr': result.stderr, 'reviewedInputs': before,
        'limitations': ['This uses available SDK8.0.413 solely as a separate reviewer observation; the coordinator owns final pinned SDK10.0.401 integrated validation.', 'Parsing does not prove admission/authentication, persistent retry semantics, inventory transfer, real world state or performance.', 'The deterministic random probe is bounded exploration, not exhaustive coverage of all input byte strings.'],
    }
    args.output.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({'sdk': report['sdk'], **parsed}, indent=2))


if __name__ == '__main__':
    run()
