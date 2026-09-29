"""Run the affected integration or real-native authentication selection, preserving each label."""
from pathlib import Path
import datetime
import hashlib
import json
import os
import subprocess
import sys
import time
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[5]
output = Path(__file__).resolve().parent
kind, label = sys.argv[1:3]
record_path = output / (label + '-command.json')
if record_path.exists():
    raise SystemExit('Refusing to overwrite an existing qualification label.')
dotnet = Path('/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet')
project = root / 'Beta/Runtime/tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj'
env = os.environ.copy()
expected_passes = None
expected_skips = 0
source_pins = {}
if kind in ('fixture', 'native'):
    source = root / 'Beta/implementation/p02/care-definition-digests/qualification/durable-hosting-final'
    env['OPENFANTASY_COORDINATED_NATIVE_SOURCE'] = str(source)
    source_pins = {str(source / name): hashlib.sha256((source / name).read_bytes()).hexdigest()
                   for name in ('final.sqlite3', 'final.ofc', 'before.sqlite3', 'before.ofc',
                                'after.sqlite3', 'after.ofc', 'batch.ofc', 'metadata.json')}
if kind == 'affected':
    selection = '|'.join('FullyQualifiedName~' + name for name in (
        'OpenFantasy.Hosting.Tests.Client.', 'WorldStorage.WorldDatabaseTests',
        'WorldStorage.WorldAuthorityServiceTests', 'Care.CoordinatedNativeSelectionTests'))
elif kind == 'fixture':
    env['OPENFANTASY_COORDINATED_NATIVE_EVIDENCE'] = str(output / ('fixtures-' + label))
    selection = 'FullyQualifiedName~Care.CoordinatedNativeFixtureTests'
    expected_passes = 4
elif kind == 'absent-input':
    for key in ('OPENFANTASY_COORDINATED_NATIVE_SOURCE', 'OPENFANTASY_COORDINATED_NATIVE_RESULTS',
                'OPENFANTASY_COORDINATED_NATIVE_EVIDENCE'):
        env.pop(key, None)
    selection = 'FullyQualifiedName~Care.CoordinatedNativeFixtureTests|FullyQualifiedName~Care.CoordinatedNativeResultTests'
    expected_passes, expected_skips = 0, 5
elif kind in ('native', 'legacy-native'):
    manifest = Path(sys.argv[3]).resolve(strict=True)
    coordinated = kind == 'native'
    variable = 'OPENFANTASY_COORDINATED_NATIVE_RESULTS' if coordinated else 'OPENFANTASY_RESTORATIVE_NATIVE_RESULTS'
    env[variable] = str(manifest)
    selection = 'FullyQualifiedName~Care.' + ('CoordinatedNativeResultTests' if coordinated else 'RestorativeNativeResultTests')
    expected_passes = 1
else:
    raise SystemExit('Unknown integration selection.')
command = [str(dotnet), 'test', str(project), '--no-build', '--no-restore', '--filter', selection,
           '--logger', 'trx;LogFileName=' + label + '.trx', '--results-directory', str(output)]
record = dict(kind=kind, command=command, startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
              environment={key: value for key, value in env.items() if key.startswith('OPENFANTASY_')},
              sourceSha256=source_pins, expectedPasses=expected_passes, expectedSkips=expected_skips)
record_path.write_text(json.dumps(record, indent=2) + '\n')
start = time.monotonic()
with (output / (label + '.log')).open('x') as log:
    result = subprocess.run(command, cwd=root, env=env, stdout=log, stderr=subprocess.STDOUT)
record.update(exitCode=result.returncode, elapsedSeconds=time.monotonic() - start,
              finishedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())
failures = []
report = output / (label + '.trx')
if report.exists():
    tree = ET.parse(report)
    namespace = '{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}'
    counts = tree.find('.//' + namespace + 'Counters').attrib
    record['testCounters'] = counts
    # The adapter reports dynamic skips in rows while its aggregate notExecuted remains zero.
    outcomes = [row.attrib['outcome'] for row in tree.findall('.//' + namespace + 'UnitTestResult')]
    record['caseOutcomes'] = {outcome: outcomes.count(outcome) for outcome in sorted(set(outcomes))}
    passed, skipped = outcomes.count('Passed'), outcomes.count('NotExecuted')
    if (expected_passes is not None and passed != expected_passes) or (expected_passes is None and passed == 0):
        failures.append('The required test selection did not execute its expected passing cases.')
    if skipped != expected_skips:
        failures.append('Unexpected skipped cases: required fixture/native evidence cannot silently skip.')
else:
    failures.append('No TRX evidence was produced.')
if source_pins != {name: hashlib.sha256(Path(name).read_bytes()).hexdigest() for name in source_pins}:
    failures.append('The retained source changed during qualification.')
record['qualificationFailures'] = failures
record_path.write_text(json.dumps(record, indent=2) + '\n')
print((output / (label + '.log')).read_text()[-12000:])
if failures:
    print('\n'.join(failures))
raise SystemExit(result.returncode or (1 if failures else 0))
