"""Join current-build evidence without treating an incomplete selection as a pass."""
import hashlib,json,re,sys
from pathlib import Path
import xml.etree.ElementTree as ET
sys.dont_write_bytecode=True
import check

P=Path(__file__).resolve().parent
NS='{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}'
def read(path):return json.loads(path.read_text())
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def cases(path):
 return {row.get('testName'):row.get('outcome') for row in ET.parse(path.parent/'checks.trx').findall('.//'+NS+'UnitTestResult')}

check.preserve()
source=check.sources();binaries=check.baseline.assemblies()
modes=('build','server','network','presentation','presentation-extra','presentation-evidence','shared','native','boundary-fixtures','boundary-native','boundary-auth')
reports={mode:Path(read(P/('.latest-'+mode+'.json'))['path']) for mode in modes}
records={mode:read(path) for mode,path in reports.items()}
for mode,row in records.items():
 assert row['source']==source and row['binaries']==binaries and row['sourceUnchanged'],mode
 if mode not in ('presentation','presentation-evidence'):assert row['accepted'],mode
 assert row['seconds']<row['limitSeconds'],mode
outcomes={mode:cases(path) for mode,path in reports.items() if mode not in ('build','boundary-native')}
missing={name for name,outcome in outcomes['presentation'].items() if outcome!='Passed'}
assert len(missing)==6
assert missing=={name for mode in ('presentation-evidence','boundary-auth') for name,outcome in outcomes[mode].items() if outcome=='Passed'}
assert {name for name,outcome in outcomes['presentation-evidence'].items() if outcome!='Passed'}==set(outcomes['boundary-auth'])
passed={name for rows in outcomes.values() for name,outcome in rows.items() if outcome=='Passed'}
previous=read(P.parent/'player-client/FINAL_CHECKS.json')
covered={}
for mode in ('server','network'):
 old=P.parent/'player-client'/previous['reports'][mode]['path']
 assert sha(old)==previous['reports'][mode]['sha256']
 names={name for name,outcome in cases(old).items() if outcome=='Passed'}
 assert names<=passed;covered[mode]=len(names)
oldClient=P.parent/'native-accepted-care/qualification/client-regression-20261002T041714Z-dc2b5/report.json'
clientNames=set(read(oldClient)['testNames']);assert clientNames<=passed
covered['priorClientRegression']=len(clientNames)
links=0
for page in (check.ROOT/'Beta/Runtime/docs/architecture').glob('*.md'):
 for target in re.findall(r'\]\(([^)]+)\)',page.read_text()):
  if target.startswith(('https:','http:','#','mailto:')):continue
  target=target.split('#')[0]
  if target:assert (page.parent/target).exists(),(page,target);links+=1
native=reports['native'].parent
for route in ('headless','rendered'):
 row=read(native/route/'native-player.json');assert row['Status']=='passed' and len(row['Submissions'])==2
 if route=='rendered':assert row['InitialFocusGuard'] and not row['ObservedNativeFocus']
protected=read(P/'PRESERVATION.json')
result={
 'acceptedBoundedScope':True,'sourceHead':protected['sourceHead'],'checkpoint':protected['checkpoint'],
 'source':source,'binaries':binaries,
 'reports':{mode:{'path':str(path.relative_to(P)),'sha256':sha(path),'acceptedOriginal':records[mode]['accepted']} for mode,path in reports.items()},
 'counts':{mode:sum(outcome=='Passed' for outcome in rows.values()) for mode,rows in outcomes.items()},
 'distinctPassed':len(passed),'allPriorCasesPassed':covered,'newSessionAndNativeCases':14,
 'presentationDisposition':'Original broad selection remains incomplete: 155 passes, two absent-manifest failures and four absent-manifest skips. Current-build retained-journal execution passes five exact cases; the old trading-boundary report fails its initial public-view hash. Fresh fixtures and three native boundary scenarios pass the unchanged sixth authenticator. Additional client selection covers the prior client regression outside the Client namespace. Neither incomplete original report is relabeled as passed.',
 'unrunPlatformCases':records['server']['unrunPlatformCases'],
 'protectedCounts':{k:len(v) for k,v in protected['protected'].items()},
 'predecessorIndexSha256':protected['predecessorIndexSha256'],'architectureLinksResolved':links,
 'checkerSha256':{name:sha(P/name) for name in ('check.py','retained.py','finalize.py')},
 'nativeArtifactSha256':{str(path.relative_to(P)):sha(path) for path in native.glob('*/*') if path.is_file()},
 'freshNativeProcesses':5,
 'retainedFailures':[str(path.relative_to(P)) for path in sorted((P/'qualification').glob('*/report.json')) if not read(path)['accepted']],
 'scope':'macOS current-source native participant binding, real TLS/headless/background Godot journeys, host recovery and affected managed regression. Retained journal checks authenticate old recorded journeys using current binaries; they are not fresh execution of those old UI routes. Native1–4-player, solo envelope parity, recorded disconnect/reconnect, other platforms and full production/human gates remain open.'
}
check.preserve()
(P/'FINAL_CHECKS.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({k:result[k] for k in ('acceptedBoundedScope','counts','distinctPassed','allPriorCasesPassed','protectedCounts','architectureLinksResolved')},indent=2))
