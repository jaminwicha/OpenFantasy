"""Verify and pin the precise bounded recovery evidence; no acceptance inferred from test names."""
import importlib.util,json,re,sys
from pathlib import Path
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('check',P/'check.py')
check=importlib.util.module_from_spec(spec);spec.loader.exec_module(check)
check.preserve();source=check.sources();binaries=check.base.assemblies()
reports={};raw={};passed=set();unrun=set();artifacts={}
expected={'recovery-storage':31,'recovery-host':5,'recovery-process':8,'storage':457,
 'controller':81,'network':272,'shared':26,'server-compatibility':76,'control-core':24,'control-native':1}
for mode in ['build',*expected]:
 path=Path(json.loads((P/f'.latest-{mode}.json').read_text())['path'])
 data=json.loads(path.read_text());raw[mode]=(path,data)
 assert data['accepted'] and data['source']==source and data['binaries']==binaries,mode
 if mode in expected:assert data['outcomes'].get('Passed')==expected[mode],mode
 if mode!='server-compatibility':assert not data.get('unrunCases',[]),mode
 passed.update(row['name'] for row in data.get('cases',[]) if row['outcome']=='Passed')
 unrun.update(data.get('unrunCases',[]))
 reports[mode]={'path':str(path.relative_to(P)),'sha256':check.base.sha(path),
  'seconds':data['seconds'],'outcomes':data.get('outcomes'),'accepted':True}
assert len(unrun)==1 and next(iter(unrun)).endswith('Invitation_created_under_readable_parent_has_only_protected_owner_access')
build=(raw['build'][0].parent/'command.log').read_text()
assert '0 Warning(s)' in build and '0 Error(s)' in build
def pin(path):
 path=Path(path);assert path.is_file(),path
 artifacts[str(path.relative_to(check.ROOT))]=check.base.sha(path)
 return path

for name,digest in {
 'archived-cancel.after.sqlite':'0a22a7ede2fd679f51aa3187512d50f9758a0ffa74996b3589b1534d83bf9aa3',
 'pressure.genesis.ofc':'02dc0cf4f170c26d062a2230ef7bd6fdd34c1a6414b1f63c94f78fc73b1e7412'}.items():
 assert check.base.sha(pin(P.parent/'care-disposition-durability/crash-fixtures'/name))==digest

processes=raw['recovery-process'][0].parent/'process-kills'
assert len(list(processes.glob('*/result.json')))==8
for operation in ('disconnect','expire','reconnect','checkpoint'):
 for side in ('before','after'):
  root=processes/f'{operation}-{side}'
  result=json.loads(pin(root/'result.json').read_text())
  assert result['Status']=='passed' and result['Operation']==operation and result['Committed']==(side=='after')
  assert result['ExitCode']!=0 and result['ElapsedSeconds']<35
  for name in ('boundary.json','world.sqlite','genesis.ofc','before.ofc','stderr.log'):pin(root/name)
  if operation!='checkpoint':pin(root/'event.ofc')

migration=json.loads(pin(P/'native-fixtures/migration.json').read_text())
assert migration['accepted'] and migration['source']==source and migration['binaries']==binaries
assert migration['originalUnchanged'] and len(migration['identicalCanonicalTables'])==7
for name,digest in migration['originalPins'].items():
 # WAL/SHM are transient observations, not immutable source files after an operator opens a save.
 original=pin(P/'native-fixtures'/('source-'+name.rsplit('-',1)[1]+'-observation.bin')) if name.endswith(('-wal','-shm')) else check.ROOT/name
 assert check.base.sha(original)==digest,name
for path in (P/'native-fixtures').iterdir():
 if path.is_file():pin(path)
control=raw['control-native'][0].parent/'control-return'
journey=json.loads(pin(control/'journey.json').read_text())
assert journey['Status']=='passed' and journey['PreparedRevision']==35 and journey['RestoredRevision']==117
assert journey['Cutoffs']==82 and journey['ElapsedSeconds']<180
pin(control/'native-return.sqlite')
native=json.loads(pin(control/'rendered/report.json').read_text())
assert native['Status']=='passed' and native['Display']=='macOS' and native['Unfocusable']
assert native['InitialFocusGuard'] and not native['NativeFocused'] and not native['ObservedNativeFocus']
captures=[];states=[]
for path in sorted((control/'rendered').glob('*.response.json')):
 response=json.loads(path.read_text())
 if response.get('Stage')!='capture-control':continue
 pin(path);assert response['Status']=='passed' and response['InitialFocusGuard']
 assert not response['NativeFocused'] and not response['ObservedNativeFocus']
 state=response['ControlState'];states.append(state)
 assert response['LocallyPaused']==(state!='Available')
 captures.append(str(pin(path.with_name(path.name.replace('.response.json','.png'))).relative_to(check.ROOT)))
assert states==['Pending','Restored','Available']

links=0
for path in (check.base.R/'docs/architecture').glob('*.md'):
 for rawlink in re.findall(r'\[[^\]]+\]\(([^)]+)\)',path.read_text()):
  target=rawlink.strip('<>').split('#')[0]
  if not target or '://' in target or target.startswith('mailto:'):continue
  links+=1;assert (path.parent/target).exists(),(path,target)
pins=json.loads((P/'PRESERVATION.json').read_text())
failures=[str(path.relative_to(P)) for path in sorted((P/'qualification').glob('*/report.json')) if not json.loads(path.read_text()).get('accepted')]
newcases={row['name'] for mode in ('recovery-storage','recovery-host','recovery-process') for row in raw[mode][1]['cases'] if row['outcome']=='Passed'}
assert len(newcases)==44
final={'status':'bounded explicit controller allowance recovery passed; full campaign gates remain open',
 'checkpoint':pins['checkpoint'],'sourceHead':pins['sourceHead'],'milestone':'codex/milestone-allowance-recovery-20261002',
 'reports':reports,'distinctPassingCases':len(passed),'newPassingCases':len(newcases),'unrunCases':sorted(unrun),
 'actualHostKills':8,'nativeProcesses':1,'renderedImages':captures,
 'nativeBoundary':'Explicit copy activation of retained genuine autonomous retreat; actual TLS native pending/restored/Resume and original-genesis replay. Initial hostile approach/loss/allowance remains managed preparation.',
 'protectedTestsAndFixtures':len(pins['protected']['tests']),'protectedSpecs':len(pins['protected']['specs']),
 'predecessorIndexSha256':pins['predecessorIndexSha256'],'architectureLinks':links,
 'artifactPins':artifacts,'qualificationToolPins':{str(path.relative_to(check.ROOT)):check.base.sha(path) for path in
 [P/'check.py',P/'prepare-native.py',P/'finalize.py',P.parent/'shared-player-sessions/check.py']},
 'failedReportsRetained':failures,
 'limits':['Only durably measured lower-bound elapsed time survives a crash; unknown tail/offline time are uncharged and repeated crashes may extend grace.',
 'Explicit operator opt-in for schemas/profiles; no automatic production admission or general lived profile migration.',
 'Consented co-player policy, general party retreat/pursuit/rescue and survival remain open.',
 'Maximum composite/disposition capacity, production history latency, other platforms, physical devices, human/art and full campaign gates remain open.',
 'No external provider request or measured external-credit savings.']}
(P/'FINAL_CHECKS.json').write_text(json.dumps(final,indent=2)+'\n')
print(json.dumps({key:value for key,value in final.items() if key not in ('reports','artifactPins','qualificationToolPins')},indent=2))
