"""Pin effective-command evidence; trusted-host gameplay is not rendered possession acceptance."""
import importlib.util,json,re,subprocess,sys
from pathlib import Path
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('check',P/'check.py')
check=importlib.util.module_from_spec(spec);spec.loader.exec_module(check)
check.preserve();source=check.sources();binaries=check.base.assemblies()
reports={};passed=set();newcases=set();unrun=set();reused={}
modes=['build','effective-core','effective-hosting','permissions-storage','core','storage','commerce','care-retreat','controller','network','shared','archive','server-compatibility']
for mode in modes:
 path=Path(json.loads((P/f'.latest-{mode}.json').read_text())['path'])
 data=json.loads(path.read_text())
 assert data['accepted'],mode
 if mode in ('core','permissions-core') and (data['source']!=source or data['binaries']!=binaries):
  # These are separate assemblies with no Hosting reference; the only later runtime repair is in Hosting.
  def family(items):return {k:v for k,v in items.items() if k.startswith(('Beta/Runtime/src/OpenFantasy.Core/','Beta/Runtime/tests/OpenFantasy.Core.Tests/')) or k=='Beta/Runtime/OpenFantasy.sln'}
  def core_binaries(items):return {k:v for k,v in items.items() if k.rsplit('/',1)[-1] in ('OpenFantasy.Core.dll','OpenFantasy.Core.Tests.dll')}
  assert family(data['source'])==family(source),mode
  old_binaries=core_binaries(data['binaries']);current_binaries=core_binaries(binaries)
  assert old_binaries and old_binaries==current_binaries,mode
  assert any(k.endswith('/OpenFantasy.Core.Tests.dll') for k in old_binaries)
  assert 'OpenFantasy.Hosting' not in (check.base.R/'tests/OpenFantasy.Core.Tests/OpenFantasy.Core.Tests.csproj').read_text()
  reused[mode]={'reason':'Core source, test source, solution and all Core/Core.Tests binary copies are byte-identical; later changes belong only to Hosting and its separate tests.',
   'sourceFamily':family(source),'binaryFamily':current_binaries,
   'changedPathsOutsideFamily':sorted(k for k in source.keys()|data['source'].keys() if source.get(k)!=data['source'].get(k))}
 else:assert data['source']==source and data['binaries']==binaries,mode
 rows=data.get('cases',[])
 passed.update(row['name'] for row in rows if row['outcome']=='Passed')
 if mode.startswith('effective-'):newcases.update(row['name'] for row in rows if row['outcome']=='Passed')
 unrun.update(data.get('unrunCases',[]))
 if mode!='server-compatibility':assert not data.get('unrunCases',[]),mode
 if mode=='build':
  log=(path.parent/'command.log').read_text()
  assert '0 Warning(s)' in log and '0 Error(s)' in log
 reports[mode]={'path':str(path.relative_to(P)),'sha256':check.base.sha(path),'seconds':data['seconds'],'outcomes':data.get('outcomes'),'accepted':True}
assert len(newcases)>0
assert len(unrun)==1 and next(iter(unrun)).endswith('Invitation_created_under_readable_parent_has_only_protected_owner_access')
links=0
for path in (check.base.R/'docs/architecture').glob('*.md'):
 for rawlink in re.findall(r'\[[^\]]+\]\(([^)]+)\)',path.read_text()):
  target=rawlink.strip('<>').split('#')[0]
  if not target or '://' in target or target.startswith('mailto:'):continue
  links+=1;assert (path.parent/target).exists(),(path,target)
pins=json.loads((P/'PRESERVATION.json').read_text())
archive=json.loads((P/'HANDOFF_ARCHIVE.json').read_text())
assert check.base.sha(check.ROOT/archive['archive'])==archive['sha256']
assert subprocess.check_output(['git','show',archive['sourceCommit']+':'+archive['source']],cwd=check.ROOT)==(check.ROOT/archive['archive']).read_bytes()
failures=[str(path.relative_to(P)) for path in sorted((P/'qualification').glob('*/report.json')) if not json.loads(path.read_text()).get('accepted')]
result={'status':'bounded effective co-player command authority; player selection and native takeover remain open',
 'checkpoint':pins['checkpoint'],'sourceHead':pins['sourceHead'],'milestone':'codex/milestone-effective-control-20261003',
 'reports':reports,'distinctPassingCases':len(passed),'newPassingCases':len(newcases),'newCases':sorted(newcases),
 'unrunCases':sorted(unrun),'actualHostKills':0,'nativeProcesses':0,
 'protectedTestsAndFixtures':len(pins['protected']['tests']),'protectedSpecs':len(pins['protected']['specs']),
 'predecessorIndexSha256':pins['predecessorIndexSha256'],'architectureLinks':links,'handoffArchive':archive,
 'qualificationToolPins':{str(path.relative_to(check.ROOT)):check.base.sha(path) for path in [P/'check.py',P/'finalize.py',P.parent/'shared-player-sessions/check.py']},
 'failedReportsRetained':failures,'inheritedCoreEvidence':reused,
 'limits':['Trusted-host effective control does not prove live grantee presence, player actor selection, private views or fresh readiness.',
 'Authenticated selection generations, observation-bound Resume and native TLS selection/handback remain required.',
 'No automatic production admission or lived-world profile40 migration; old profiles and frozen specs remain intact.',
 'Transaction fault injection and full reopen are managed evidence; no new process-kill, rendered or physical-device qualification is claimed.',
 'Full 1-4-player, platform, art, human, capacity, latency and campaign acceptance gates remain open.',
 'No external provider request or measured external-credit savings.']}
(P/'FINAL_CHECKS.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({k:v for k,v in result.items() if k not in ('reports','newCases','qualificationToolPins','inheritedCoreEvidence')},indent=2))
