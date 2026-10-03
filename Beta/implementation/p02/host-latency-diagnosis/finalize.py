"""Verify the selected reservation increment without closing historical latency or production gates."""
import importlib.util
import json
import re
import subprocess
import sys
from pathlib import Path

sys.dont_write_bytecode = True
P = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('qualification', P / 'check.py')
check = importlib.util.module_from_spec(spec)
spec.loader.exec_module(check)
check.preserve()
source, binaries = check.sources(), check.base.assemblies()
reports, passed, unrun = {}, set(), set()
for mode in ['build', 'reservation', 'core', 'storage', 'server-compatibility', 'effective-hosting']:
    path = Path(json.loads((P / f'.latest-{mode}.json').read_text())['path'])
    data = json.loads(path.read_text())
    assert data['accepted'] and data['source'] == source and data['binaries'] == binaries, mode
    assert data['seconds'] < data['limitSeconds'], mode
    if mode == 'build':
        log = (path.parent / 'command.log').read_text()
        assert '0 Warning(s)' in log and '0 Error(s)' in log
    else:
        assert data['expectedSkips'] and data['outcomes'].get('Passed', 0) > 0
        assert set(data['outcomes']) <= {'Passed', 'NotExecuted'}
        passed.update(row['name'] for row in data['cases'] if row['outcome'] == 'Passed')
        unrun.update(data['unrunCases'])
    reports[mode] = {'path': str(path.relative_to(P)), 'sha256': check.base.sha(path),
                     'seconds': data['seconds'], 'outcomes': data.get('outcomes'), 'accepted': True}
assert len(passed) == 2467
assert unrun == {'OpenFantasy.Server.Tests.WindowsInvitationTests.Invitation_created_under_readable_parent_has_only_protected_owner_access'}
measurement = json.loads((P / 'RESERVATION_MEASUREMENT.json').read_text())
for label in ['baseline', 'optimized']:
    row = measurement[label]
    path = P / row['report']
    assert check.base.sha(path) == row['reportSha256']
    assert json.loads(path.read_text())['accepted'] == (label == 'optimized')
    log = (path.parent / 'command.log').read_text()
    assert f"allocatedBytes={row['allocatedBytes']} " in log
    assert f"input={row['inputHash']} candidate={row['candidateHash']}" in log
for key in ['profile', 'warmups', 'decisions', 'reservations', 'maximumWork', 'inputHash', 'candidateHash', 'inputBytes', 'candidateBytes']:
    assert measurement['baseline'][key] == measurement['optimized'][key]
assert measurement['baseline']['allocatedBytes'] == 226050336
assert measurement['optimized']['allocatedBytes'] == 201974072
assert measurement['optimized']['decisions'] == 16
pins = json.loads((P / 'PRESERVATION.json').read_text())
checkpoint = 'codex/checkpoint-before-reservation-reuse-20261003T0155Z'
checkpoint_commit = subprocess.check_output(['git', 'rev-parse', checkpoint], cwd=check.ROOT, text=True).strip()
assert checkpoint_commit == '53cd1085311fbb592dd5a1486a20dce57df3ae7f'
links = 0
paths = list((check.base.R / 'docs/architecture').glob('*.md'))
paths += [check.ROOT / 'Beta/implementation/AGENT_HANDOFF.md', P / 'RESULT.md', P / 'BASELINE.md']
for path in paths:
    for raw in re.findall(r'\[[^\]]+\]\(([^)]+)\)', path.read_text()):
        target = raw.strip('<>').split('#')[0]
        if not target or '://' in target or target.startswith('mailto:'):
            continue
        assert (path.parent / target).exists(), (path, target)
        links += 1
result = {
    'status': 'bounded excavation reservation ownership qualified; historical timing causes and production gates remain open',
    'milestone': 'codex/milestone-excavation-reservation-reuse-20261003',
    'initialCheckpoint': pins['checkpoint'], 'initialSourceHead': pins['sourceHead'],
    'beforeRuntimeCheckpoint': checkpoint, 'beforeRuntimeCommit': checkpoint_commit,
    'reports': reports, 'distinctPassingCases': len(passed), 'newPassingCases': 1,
    'unrunCases': sorted(unrun), 'reservationMeasurement': measurement,
    'protectedTestsAndFixtures': len(pins['protected']['tests']), 'protectedSpecs': len(pins['protected']['specs']),
    'predecessorIndexSha256': pins['predecessorIndexSha256'], 'sourceFiles': len(source), 'checkedLinks': links,
    'sourceSha256': check.base.hashlib.sha256(json.dumps(source, sort_keys=True).encode()).hexdigest(),
    'toolPins': {str(path.relative_to(check.ROOT)): check.base.sha(path) for path in
                 [P / 'check.py', P / 'finalize.py', P.parent / 'shared-player-sessions/check.py']},
    'retainedLocalFailures': [str(path.relative_to(P)) for path in sorted((P / 'qualification').glob('*/report.json'))
                              if not json.loads(path.read_text())['accepted']],
    'priorUnexplainedFailures': '../effective-co-player-control/QUALIFICATION_STATUS.json',
    'limits': [
        '16 sibling fixed-workload decisions, not 16 sequential world transitions; old full storage tests separately prove complete chains.',
        'The 10.6508% allocation reduction is limited to the measured profile17 continuation, not whole-game memory or latency.',
        'Normal server and full storage selections pass unchanged bounds now; this does not explain their earlier failures.',
        'No movement renewal, lease extension, serialization change, validation removal or relaxed test gate.',
        'No native, process-kill, Windows ACL, physical-device, human, full phase or production-latency acceptance claim.',
        'No external provider request or external-credit savings.']
}
(P / 'FINAL_CHECKS.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({k: result[k] for k in ['status', 'distinctPassingCases', 'newPassingCases', 'protectedTestsAndFixtures', 'protectedSpecs', 'checkedLinks']}, indent=2))
