"""Pin this bounded integration's actual dependencies, outcomes and retained failures."""
import importlib.util,json,re,sys
from pathlib import Path
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('check',P/'check.py')
check=importlib.util.module_from_spec(spec);spec.loader.exec_module(check)
check.preserve();source=check.sources();binaries=check.base.assemblies()
reports={};raw={};passed=set();unrun=set();images=[];artifacts={}
expected={'control-network':7,'control-client':3,'survival':3,'control-native':1,
 'network':272,'controller':76,'client-managed':187,'server-managed':76,
 'retreat':16,'shared':26,'control-core':24,'roster-native':1,'native-auth':1}
for mode in ['build',*expected,'native-original']:
 path=Path(json.loads((P/f'.latest-{mode}.json').read_text())['path'])
 data=json.loads(path.read_text());raw[mode]=(path,data)
 assert data['accepted'] and data['source']==source and data['binaries']==binaries,mode
 if mode in expected:assert data['outcomes'].get('Passed')==expected[mode],mode
 if mode!='server-managed':assert not data.get('unrunCases',[]),mode
 passed.update(row['name'] for row in data.get('cases',[]) if row['outcome']=='Passed')
 unrun.update(data.get('unrunCases',[]))
 reports[mode]={'path':str(path.relative_to(P)),'sha256':check.base.sha(path),
   'seconds':data['seconds'],'outcomes':data.get('outcomes'),'accepted':True}
assert len(unrun)==1 and next(iter(unrun)).endswith('Invitation_created_under_readable_parent_has_only_protected_owner_access')
build=raw['build'][0].parent/'command.log'
assert '0 Warning(s)' in build.read_text() and '0 Error(s)' in build.read_text()

def pin(path):
 path=Path(path);assert path.exists(),path
 artifacts[str(path.relative_to(check.ROOT))]=check.base.sha(path)
 return path

for path in (P/'native-fixtures').iterdir():
 if path.is_file():pin(path)
survival=raw['survival'][1]
assert Path(survival['nativeInputs']['OPENFANTASY_NATIVE_CONTROL_RETURN_FIXTURES'])==P/'native-fixtures'
control=raw['control-native'][0].parent/'control-return'
journey=json.loads(pin(control/'journey.json').read_text())
assert journey['Status']=='passed' and journey['PreparedRevision']==35 and journey['RestoredRevision']==117
assert journey['Cutoffs']==82 and journey['ElapsedSeconds']<180
pin(control/'native-return.sqlite')
for name,state,paused in [('0002','Pending',True),('0011','Restored',True),('0015','Available',False)]:
 response=json.loads(pin(control/'rendered'/f'{name}.response.json').read_text())
 assert response['Status']=='passed' and response['ControlState']==state and response['LocallyPaused']==paused
 assert response['InitialFocusGuard'] and not response['NativeFocused'] and not response['ObservedNativeFocus']
 images.append(str(pin(control/'rendered'/f'{name}.png')))

roster=raw['roster-native'][0].parent/'roster'
journey=json.loads(pin(roster/'journey.json').read_text())
assert journey['Status']=='passed' and len(journey['Players'])==4 and journey['ElapsedMilliseconds']<240000
pin(roster/'genesis.ofc');pin(roster/'native-roster.sqlite3')
processes=list(roster.glob('*/report.json'))
assert len(processes)==5
rendered=0
for path in [control/'rendered/report.json',*processes]:
 data=json.loads(pin(path).read_text());assert data['Status']=='passed'
 if data['Display']!='headless':
  assert data['Display']=='macOS' and not data['ObservedNativeFocus'] and not data['NativeFocused']
  assert data['InitialFocusGuard'] and data['Unfocusable'];rendered+=1
for path in roster.glob('*/*.png'):images.append(str(pin(path)))
assert rendered==2 and len(images)==6

native=json.loads(pin(raw['native-original'][1]['nativeReport']).read_text())
assert native['status']=='passed' and native['originalProjectUnchanged'] and len(native['runs'])==6 and len(native['results'])==3
for group in ('executionPins','fixturePins'):
 for path,digest in native[group].items():assert check.base.sha(Path(path))==digest,path
for row in native['results']:
 assert check.base.sha(pin(row['save']))==row['finalDatabaseSha256']
 for kind in ('report','reopenReport'):
  data=json.loads(pin(row[kind]).read_text());assert data['Status']=='passed'
  for path in data['Images']:images.append(str(pin(path)))
assert len(images)==21
assert raw['native-auth'][1]['nativeInputs']['OPENFANTASY_NATIVE_CARE_DISPOSITION_RESULTS']==native['managedAuthenticationManifest']
pin(native['managedAuthenticationManifest'])

links=0
for path in (check.base.R/'docs/architecture').glob('*.md'):
 for rawlink in re.findall(r'\[[^\]]+\]\(([^)]+)\)',path.read_text()):
  target=rawlink.strip('<>').split('#')[0]
  if not target or '://' in target or target.startswith('mailto:'):continue
  links+=1;assert (path.parent/target).exists(),(path,target)
pins=json.loads((P/'PRESERVATION.json').read_text())
failed=[str(path.relative_to(P)) for path in sorted((P/'qualification').glob('*/report.json')) if not json.loads(path.read_text()).get('accepted')]
assert len(failed)==5,failed
newcases={row['name'] for mode in ('control-network','control-client','survival','control-native')
 for row in raw[mode][1]['cases'] if row['outcome']=='Passed'}
assert len(newcases)==14
toolpins={str(path.relative_to(check.ROOT)):check.base.sha(path) for path in
 [P/'check.py',P/'native.py',P/'finalize.py',P.parent/'shared-player-sessions/check.py']}
final={'status':'bounded observed-control-return integration passed; full campaign gates remain open',
 'checkpoint':pins['checkpoint'],'sourceHead':pins['sourceHead'],
 'milestone':'codex/milestone-control-return-20261002','reports':reports,
 'distinctPassingCases':len(passed),'newPassingCases':len(newcases),'unrunCases':sorted(unrun),
 'nativeProcesses':12,'renderedProcesses':8,'renderedImages':len(images),
 'nativeBoundary':'Managed hostile approach, damage, actual transport loss, recorded 30-second allowance and first paid retreat sample; native TLS continuation through remaining accepted route, observed restoration, deliberate Resume and exact original-genesis reopen.',
 'protectedTestsAndFixtures':len(pins['protected']['tests']),'protectedSpecs':len(pins['protected']['specs']),
 'predecessorIndexSha256':pins['predecessorIndexSha256'],'architectureLinks':links,
 'dependencyReuse':'None for selected qualification: all reports match the exact current source and binaries.',
 'artifactPins':artifacts,'qualificationToolPins':toolpins,'failedReportsRetained':failed,
 'limits':['not a full Core/Hosting suite or full phase/campaign acceptance',
 'restart allowance reconciliation and co-player policy remain open',
 'general party retreat, pursuit, rescue and survival policies remain open',
 'maximum composite/disposition-only capacity and production history latency remain open',
 'Windows/Linux/mobile, physical devices, OS-focus behavior, human usability and art acceptance remain open',
 'native hostile approach/loss/allowance preparation is not claimed by the native continuation test',
 'no external provider request or external-credit savings claim']}
(P/'FINAL_CHECKS.json').write_text(json.dumps(final,indent=2)+'\n')
print(json.dumps({k:v for k,v in final.items() if k not in ('reports','artifactPins')},indent=2))
