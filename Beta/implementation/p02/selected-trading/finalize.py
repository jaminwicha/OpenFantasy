"""Reconcile scoped trading evidence, exact preservation and retained failures."""
import importlib.util
import json
from pathlib import Path
import sys

sys.dont_write_bytecode = True
PACKET = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('qualification', PACKET / 'check.py')
check = importlib.util.module_from_spec(spec)
spec.loader.exec_module(check)
check.preserve()
pins = json.loads((PACKET / 'PRESERVATION.json').read_text())
project = 'Beta/Runtime/tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj'
original = check.ROOT / pins['permittedTestRefactors'][project]['original']
project_bytes = (check.ROOT / project).read_bytes()
for name in ('FarmMarketPresentation', 'FarmMarketQueryLifetime'):
    addition = ('    <Compile Include="../../client/scripts/' + name + '.cs" Link="Client/' + name + '.cs" />\n').encode()
    assert project_bytes.count(addition) == 1
    project_bytes = project_bytes.replace(addition, b'')
assert project_bytes == original.read_bytes()

modes = ['build', 'hosting-build', 'new-managed', 'client-managed', 'codec', 'selected-queries', 'core',
         'native-trading', 'home-fixtures', 'home-native', 'home-auth',
         'boundary-fixtures', 'boundary-native', 'boundary-auth',
         'native-construction', 'native-recipes', 'native-production', 'native-gifts',
         'native-selection', 'native-compatibility']
expected = {'new-managed': 9, 'client-managed': 256, 'core': 1885, 'native-trading': 1,
            'native-construction': 1, 'native-recipes': 1, 'native-production': 2,
            'native-gifts': 1, 'native-selection': 1, 'native-compatibility': 2}
reports = {}
passing = set()
footer = json.loads((PACKET / 'FOOTER_CORRECTION.json').read_text())
scene = check.ROOT / footer['source']
assert check.base.sha(scene) == footer['afterSha256']
prior_scene = scene.read_bytes().replace(footer['replacementBytes'].encode(), footer['exactRemovedBytes'].encode())
assert check.hashlib.sha256(prior_scene).hexdigest() == footer['beforeSha256']
for mode in modes:
    path = Path(json.loads((PACKET / ('.latest-' + mode + '.json')).read_text())['path'])
    report = json.loads(path.read_text())
    current_source = check.sources(mode)
    same_binaries = report['binaries'] == check.binaries(mode)
    full_match = report['source'] == current_source and same_binaries
    same = full_match
    if mode in footer['reusedModes']:
        before_source = dict(current_source)
        before_source[footer['source']] = footer['beforeSha256']
        same = same_binaries and report['source'] == before_source
    same_checker = report['checkerSha256'] == check.base.sha(PACKET / 'check.py') and report['baseCheckerSha256'] == check.base.sha(PACKET.parent / 'shared-player-sessions/check.py')
    tool = report.get('nativeTool')
    same_tool = not tool or check.base.sha(Path(tool['path'])) == tool['sha256']
    if mode in ('build', 'hosting-build'):
        outcomes = '0 Warning(s)' in (path.parent / 'command.log').read_text() and '0 Error(s)' in (path.parent / 'command.log').read_text()
    elif mode in ('home-native', 'boundary-native'):
        native = json.loads((path.parent / 'native.json').read_text())
        outcomes = native['status'] == 'passed' and report['nativeReportSha256'] == check.base.sha(path.parent / 'native.json')
        outcomes = outcomes and all(check.base.sha(Path(native['workDirectory']) / name) == digest
                                    for name, digest in native['artifactSha256'].items())
    else:
        outcomes = set(report.get('outcomes', {})) == {'Passed'} and report['outcomes']['Passed'] > 0
        if mode in expected:
            outcomes = outcomes and report['outcomes'] == {'Passed': expected[mode]}
    valid = bool(report['accepted'] and same and same_checker and same_tool and outcomes)
    reports[mode] = {'path': str(path.relative_to(PACKET)), 'sha256': check.base.sha(path),
        'verified': valid, 'matchesRelevantSourceAndBinaries': same,
        'matchesFullRecordedSnapshot': full_match,
        'narrowSceneTextReuse': 'FOOTER_CORRECTION.json' if mode in footer['reusedModes'] else None,
        'matchesCheckerAndTool': bool(same_checker and same_tool), 'expectedOutcomes': outcomes,
        'seconds': report['seconds'], 'outcomes': report.get('outcomes'),
        'dependencyFamily': 'Hosting and exact linked client files' if mode in ('hosting-build', 'codec', 'new-managed', 'client-managed') else 'Full solution'}
    if valid:
        passing.update(case['name'] for case in report.get('cases', []) if case['outcome'] == 'Passed')

retained = []
for path in sorted((PACKET / 'qualification').glob('*/report.json')):
    report = json.loads(path.read_text())
    if not report['accepted']:
        retained.append({'path': str(path.relative_to(PACKET)), 'sha256': check.base.sha(path),
            'mode': report['mode'], 'outcomes': report.get('outcomes'), 'error': report.get('error')})
images = json.loads((PACKET / 'INSPECTED_IMAGES.json').read_text())['images']
for item in images:
    assert check.base.sha(PACKET / item['path']) == item['sha256']
native = (PACKET / reports['native-trading']['path']).parent / 'native/selected-trading'
journey = json.loads((native / 'journey.json').read_text())
artifacts = [{'path': str(path.relative_to(PACKET)), 'sha256': check.base.sha(path)}
             for path in sorted(native.rglob('*')) if path.is_file() and
             (path.suffix in ('.ofc', '.sqlite3') or path.name in
              ('journey.json', 'receipts.json', 'identities.json', 'journal.json', 'preparation.txt'))]
all_passed = all(report['verified'] for report in reports.values())
new_cases = sorted(name for name in passing if '.Client.SelectedTrading.' in name or '.SelectedTradingNative.' in name)
result = {
    'allCurrentSelectedReportsPassed': all_passed,
    'sharedSelectedTradingNativeQualified': all_passed and journey['Status'] == 'passed',
    'fullRegressionAccepted': False, 'fullPhaseAccepted': False, 'fullCoPlayerFeatureAccepted': False,
    'originalNativeGiftOpeningFailureExplained': False, 'movementTimingGateClosed': False,
    'productionHistoryLatencyGateClosed': False, 'profile40ProcessKillQualified': False,
    'nativeSelectedHistoryLifecycleQualified': False, 'fullHistoryDiscoveryImplemented': False,
    'physicalDeviceAcceptance': False, 'humanUsabilityAcceptance': False, 'artAcceptance': False,
    'sourceHead': pins['sourceHead'], 'preChangeCheckpoint': pins['checkpoint'],
    'milestone': 'codex/milestone-selected-trading-20261003',
    'protectedCounts': {name: len(paths) for name, paths in pins['protected'].items()},
    'onlyExistingTestProjectChange': 'Two additive market source links; exact archived original compared byte for byte.',
    'predecessorIndexSha256': pins['predecessorIndexSha256'],
    'checkerSha256': check.base.sha(PACKET / 'check.py'), 'finalizerSha256': check.base.sha(PACKET / 'finalize.py'),
    'distinctCasesPassingOnVerifiedDependencies': len(passing), 'newCases': new_cases,
    'reports': reports, 'retainedFailedAttempts': retained,
    'journey': {'path': str((native / 'journey.json').relative_to(PACKET)), **journey},
    'authenticatedNativeArtifacts': artifacts, 'leadInspectedImages': images,
    'review': 'REVIEW.md', 'externalClaudeRequestExecuted': False,
    'narrowSceneTextEvidenceReuse': footer,
    'limits': [
        'Actual four-human Cargo world; ordinary journaled quest funding and travel are preparation, not native gestures or substitute genesis.',
        'Trade-only purchase, exact received-lot showing/retraction/sale, separate review and owner-return invalidation use the shared stall.',
        'Full replay from original genesis includes genuine scheduled NPC construction; additional world changes require exact causal accounting.',
        'Host stop/reopen replay does not claim a second native process epoch or a profile40 process-kill recovery proof.',
        'PrivateHistory remains independent; remembered observed IDs do not implement history enumeration or an independent history-only browser.',
        'Prior gift opening and movement/history timing failures remain unexplained; later passes do not close those reliability gates.',
        'Controlled background rendering and injected input do not prove physical devices, OS focus, human usability, final art or other platforms.',
        'Complete campaign, sustained work, production scale/composite maxima and broader domain coverage remain open.'
    ]
}
(PACKET / 'FINAL_CHECKS.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({key: result[key] for key in ('allCurrentSelectedReportsPassed', 'sharedSelectedTradingNativeQualified', 'distinctCasesPassingOnVerifiedDependencies')}, indent=2))
if not all_passed or len(new_cases) != 10:
    print('New cases:', len(new_cases))
    sys.exit(1)
