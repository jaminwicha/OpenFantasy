"""Authenticate the selected care qualification, preservation, images and retained failures."""
import importlib.util,json,sys
from pathlib import Path
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('qualification',P/'check.py')
check=importlib.util.module_from_spec(spec);spec.loader.exec_module(check)
check.preserve()
pins=json.loads((P/'PRESERVATION.json').read_text())
project='Beta/Runtime/tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj'
original=check.ROOT/pins['permittedTestRefactors'][project]['original']
project_bytes=(check.ROOT/project).read_bytes()
for name in ('FarmCarePresentation','FarmCareLibraryModel','FarmClientSession.Care'):
    addition=('    <Compile Include="../../client/scripts/'+name+'.cs" Link="Client/'+name+'.cs" />\n').encode()
    assert project_bytes.count(addition)==1
    project_bytes=project_bytes.replace(addition,b'')
assert project_bytes==original.read_bytes()
modes=['build','hosting-build','new-managed','client-managed','codec','care-managed','core',
       'native-care','home-fixtures','home-native','home-auth','native-trading','native-recipes','native-compatibility']
expected={'new-managed':32,'client-managed':275,'codec':183,'care-managed':26,'core':1885,'native-care':1,
          'home-fixtures':3,'home-auth':1,'native-trading':1,'native-recipes':1,'native-compatibility':2}
reports={};passing=set()
visual=json.loads((P/'WHEEL_DETAIL_CORRECTION.json').read_text())
scene=check.ROOT/visual['source']
assert check.base.sha(scene)==visual['afterSha256']
assert check.hashlib.sha256(scene.read_bytes().replace(visual['replacementBytes'].encode(),visual['exactRemovedBytes'].encode())).hexdigest()==visual['beforeSha256']
for mode in modes:
    path=Path(json.loads((P/('.latest-'+mode+'.json')).read_text())['path'])
    report=json.loads(path.read_text())
    current=check.sources(mode)
    full_match=report['source']==current and report['binaries']==check.binaries(mode)
    dependencies=full_match
    if mode in visual['reusedModes']:
        old=dict(current);old[visual['source']]=visual['beforeSha256']
        dependencies=report['source']==old and report['binaries']==check.binaries(mode)
    checker=report['checkerSha256']==check.base.sha(P/'check.py') and report['baseCheckerSha256']==check.base.sha(P.parent/'shared-player-sessions/check.py')
    tool=report.get('nativeTool')
    tool_match=not tool or tool['sha256']==check.base.sha(Path(tool['path']))
    if mode in ('build','hosting-build'):
        log=(path.parent/'command.log').read_text()
        outcomes='0 Warning(s)' in log and '0 Error(s)' in log
    elif mode=='home-native':
        native=json.loads((path.parent/'native.json').read_text())
        outcomes=native['status']=='passed' and len(native['journeys'])==5
        outcomes=outcomes and report['nativeReportSha256']==check.base.sha(path.parent/'native.json')
        outcomes=outcomes and all(check.base.sha(path.parent/name)==digest for name,digest in native['artifactSha256'].items())
    else:
        outcomes=set(report.get('outcomes',{}))=={'Passed'} and report['outcomes']['Passed']>0
        if mode in expected:outcomes=outcomes and report['outcomes']=={'Passed':expected[mode]}
    valid=bool(report['accepted'] and dependencies and checker and tool_match and outcomes)
    reports[mode]={'path':str(path.relative_to(P)),'sha256':check.base.sha(path),'verified':valid,
        'matchesRelevantSourceAndBinaries':dependencies,'matchesFullRecordedSnapshot':full_match,
        'narrowSceneReuse':'WHEEL_DETAIL_CORRECTION.json' if mode in visual['reusedModes'] else None,'matchesCheckerAndTool':bool(checker and tool_match),
        'expectedOutcomes':outcomes,'outcomes':report.get('outcomes'),'seconds':report['seconds']}
    if valid:passing.update(case['name'] for case in report.get('cases',[]) if case['outcome']=='Passed')
retained=[]
for path in sorted((P/'qualification').glob('*/report.json')):
    report=json.loads(path.read_text())
    if not report['accepted']:
        retained.append({'path':str(path.relative_to(P)),'sha256':check.base.sha(path),
            'mode':report['mode'],'outcomes':report.get('outcomes'),'error':report.get('error')})
images=json.loads((P/'INSPECTED_IMAGES.json').read_text())['images']
for item in images:assert check.base.sha(P/item['path'])==item['sha256']
native=(P/reports['native-care']['path']).parent/'native/restorative-choices'
journey=json.loads((native/'journey.json').read_text())
artifacts=[{'path':str(path.relative_to(P)),'sha256':check.base.sha(path)}
           for path in sorted(native.rglob('*')) if path.is_file() and (path.suffix in ('.ofc','.sqlite3')
           or path.name in ('journey.json','receipts.json','identities.json','journal.json','preparation.txt'))]
new_cases=sorted(name for name in passing if any(part in name for part in
    ('.Client.RestorativeChoices.','.Networking.CarePermissionDescriptions.','.RestorativeChoicesNative.')))
all_passed=all(report['verified'] for report in reports.values())
result={'allCurrentSelectedReportsPassed':all_passed,'sharedCareNativeQualified':all_passed and journey['Status']=='passed',
    'fullRegressionAccepted':False,'fullPhaseAccepted':False,'fullCoPlayerFeatureAccepted':False,
    'nativeFullMedicineCatalogQualified':False,'runtimePermissionGrantOrRegrantImplemented':False,
    'originalNativeGiftOpeningFailureExplained':False,'movementTimingGateClosed':False,
    'productionHistoryLatencyGateClosed':False,'profile40ProcessKillQualified':False,
    'fullHistoryDiscoveryImplemented':False,'physicalDeviceAcceptance':False,'humanUsabilityAcceptance':False,'artAcceptance':False,
    'sourceHead':pins['sourceHead'],'preChangeCheckpoint':pins['checkpoint'],'milestone':'codex/milestone-restorative-choices-20261003',
    'protectedCounts':{name:len(paths) for name,paths in pins['protected'].items()},
    'onlyExistingTestProjectChange':'Three additive care source links; subtracting exact lines restores archived original bytes.',
    'predecessorIndexSha256':pins['predecessorIndexSha256'],'checkerSha256':check.base.sha(P/'check.py'),
    'finalizerSha256':check.base.sha(P/'finalize.py'),'distinctCasesPassingOnVerifiedDependencies':len(passing),
    'newCases':new_cases,'reports':reports,'retainedFailedAttempts':retained,
    'journey':{'path':str((native/'journey.json').relative_to(P)),**journey},'authenticatedNativeArtifacts':artifacts,
    'leadInspectedImages':images,'narrowSceneEvidenceReuse':visual,'review':'REVIEW.md','externalClaudeRequestExecuted':False,
    'limits':[
        'Native preparation is journaled original-stat combat, ordinary movement, gathering and crafting; not native gestures.',
        'The native content has one self-use policy and one ordinary grant; multiple/identical-term choices are actual admitted managed contrasts.',
        'The native helper is out of reach; original managed stocked eligible-helper tests establish same-cutoff withdrawal suppression.',
        'Host stop/reopen and original-genesis replay do not claim a second selected native process epoch or process-kill recovery.',
        'All five original home restorative routes include separate native process reopen and managed journal authentication.',
        'Older movement/history latency and raw-gift opening failures remain unexplained; full release reliability stays open.',
        'Background rendered/injected input is not physical-device, OS-focus, human, final-art or cross-platform acceptance.',
        'Full history discovery, aftermath native breadth, sustained work, composite maxima and complete campaign remain open.']}
(P/'FINAL_CHECKS.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({key:result[key] for key in ('allCurrentSelectedReportsPassed','sharedCareNativeQualified','distinctCasesPassingOnVerifiedDependencies')},indent=2))
if not all_passed or len(new_cases)!=33:
    print('New cases:',len(new_cases));sys.exit(1)
