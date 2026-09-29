"""Summarize completed checks without changing their scope, thresholds or original reports."""
from pathlib import Path
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[5]
q = Path(__file__).resolve().parent
prior = q.parents[1] / 'coordinated-world-continuation/qualification'
packet = q.parent

def read(path):
    return json.loads(path.read_text())

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def write(name, value):
    (q / name).write_text(json.dumps(value, indent=2) + '\n')

reports = {}
for name in ('core-final', 'hosting-final'):
    command = read(q / (name + '-command.json'))
    assert command['exitCode'] == 0, name
    counters = ET.parse(q / (name + '.trx')).find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters').attrib
    assert int(counters['total']) > 0 and counters['passed'] == counters['total'], (name, counters)
    reports[name] = {'counters': counters, 'command': command}
assert read(q / 'integration-build-01-command.json')['exitCode'] == 0
selection = read(q / 'planned-hosting-selection.json')
actual = ET.parse(q / 'hosting-final.trx').findall('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult')
actual_names = sorted(row.attrib['testName'] for row in actual)
assert actual_names == sorted(selection['tests']), 'Affected Hosting selection changed'
assert selection['filter'] in reports['hosting-final']['command']['command']
pins = read(q / 'source-pins-integration-build-01.json')['files']
assert all(digest(root / name) == value for name, value in pins.items())
execution = read(q / 'execution-pins-integration-build-01.json')['files']
assert all(digest(Path(name)) == value for name, value in execution.items())

comparison = []
for row in read(prior / 'legacy-core-comparison.json')['files']:
    relative = row['relative']
    assert digest(q / relative) == row['sha256'] == digest(prior / relative), relative
    comparison.append({'relative': relative, 'bytes': (q / relative).stat().st_size, 'sha256': row['sha256']})
for name in ('world-25.bin', 'world-26.bin'):
    relative = 'care-legacy-hosting-final/' + name
    assert (q / relative).read_bytes() == (prior / relative).read_bytes(), relative
    comparison.append({'relative': relative, 'bytes': (q / relative).stat().st_size, 'sha256': digest(q / relative)})
for name in ('before.ofc', 'after.ofc', 'final.ofc', 'batch.ofc'):
    relative = 'durable-hosting-final/' + name
    assert (q / relative).read_bytes() == (prior / relative).read_bytes(), relative
    comparison.append({'relative': relative, 'bytes': (q / relative).stat().st_size, 'sha256': digest(q / relative)})
write('canonical-comparison.json', {'status': 'passed', 'count': len(comparison),
    'bytes': sum(row['bytes'] for row in comparison), 'files': comparison,
    'scope': '49 retained legacy frames plus four actual coordinated journey roots/batch.'})

preservation = read(packet / 'preservation-before.json')
refs = dict(line.split(' ', 1) for line in subprocess.check_output(
    ['git', 'for-each-ref', '--format=%(refname:short) %(objectname)', 'refs/heads'], cwd=root, text=True).splitlines())
assert all(refs[name] == value for name, value in preservation['milestones'].items())
assert all(digest(root / name) == value for name, value in preservation['frozenSpecs'].items())
index = hashlib.sha256(subprocess.check_output(['git', 'diff', '--cached', '--binary', '--', '.', ':!Beta'], cwd=root)).hexdigest()
assert index == preservation['predecessorIndexSha256']
write('preservation-final.json', {'status': 'passed', 'milestoneCount': len(preservation['milestones']),
    'frozenSpecCount': len(preservation['frozenSpecs']), 'predecessorIndexSha256': index,
    'checkpoint': preservation['checkpoint'], 'sourceHead': preservation['head']})
crash = read(q / 'external-crash-01/result.json')
assert crash['fixturesUnchanged'] and crash['executionUnchanged']
write('verification.json', {'status': 'passed-managed-scope',
    'distinctTests': sum(int(reports[name]['counters']['total']) for name in ('core-final', 'hosting-final')),
    'hostingScope': '141 affected care and canonical-frame cases; not full Hosting',
    'reports': reports, 'build': 'integration-build-01', 'sourcePinsUnchanged': len(pins),
    'executionPinsUnchanged': len(execution), 'canonicalFixtureCount': len(comparison),
    'externalRecovery': {'status': crash['status'], 'externalKills': crash['externalKills'],
                         'report': 'external-crash-01/result.json'},
    'limitations': ['External recovery outcome is recorded separately; diagnostics do not replace acceptance.',
                    'Native post-cut use/reopen, production lifetime/latency, platforms/devices and human acceptance remain open.',
                    'Instrumented allocation is cumulative, not peak/live memory.']})
print(json.dumps({'managed': 'passed', 'distinctTests': read(q / 'verification.json')['distinctTests'],
                  'canonicalFiles': len(comparison), 'externalRecovery': crash['status']}))
