"""Validate current-source evidence and explicit dependency-preserving reuse; never overwrite raw reports."""
import importlib.util,json,re,sys
from pathlib import Path
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('check',P/'check.py');check=importlib.util.module_from_spec(spec);spec.loader.exec_module(check)
check.preserve()
source=check.sources();binaries=check.base.assemblies()
def read(mode):
 path=Path(json.loads((P/f'.latest-{mode}.json').read_text())['path'])
 return path,json.loads(path.read_text())
def changed(a,b):return sorted(k for k in a.keys()|b.keys() if a.get(k)!=b.get(k))
new_tests={'Beta/Runtime/tests/OpenFantasy.Hosting.Tests/Care/DispositionContinuation/CareDispositionContinuationJournalTests.cs','Beta/Runtime/tests/OpenFantasy.Hosting.Tests/Care/DispositionContinuation/DispositionContinuationFixture.cs'}
client_changes={'Beta/Runtime/client/scripts/CareDispositions/CareDispositionPlayProbe.cs','Beta/Runtime/client/scripts/FarmGame.cs','Beta/Runtime/tools/check_native_care_dispositions.py'}
test_dll='Beta/Runtime/tests/OpenFantasy.Hosting.Tests/bin/Debug/net8.0/OpenFantasy.Hosting.Tests.dll'
client_dll='Beta/Runtime/client/.godot/mono/temp/bin/Debug/OpenFantasy.Client.dll'
reports={};reuse={};passed=set()
for mode in ['build','decisions','native-fixtures','native','continuation','client-managed','native-auth']:
 path,data=read(mode);assert data['accepted'],mode
 ds=changed(data['source'],source);db=changed(data['binaries'],binaries)
 if mode in ('decisions','native-fixtures'):
  assert set(ds)==new_tests|client_changes,(mode,ds)
  assert set(db)=={test_dll,client_dll},(mode,db)
  project=(check.base.R/'tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj').read_text()
  assert 'FarmGame.cs' not in project and 'CareDispositionPlayProbe.cs' not in project
  reason='Only two excluded continuation types and native-only files changed. Selected test/fixture sources, linked client files, discovery configuration, Core/Hosting and all dependency DLLs are exact. The new continuation types have no shared discovery hooks and are referenced only by their three selected cases; those were rerun. Native source was separately requalified.'
 elif mode=='native':
  assert set(ds)==new_tests and db==[test_dll],(ds,db)
  reason='Only the excluded continuation test classes and Hosting test DLL changed. Native client/Core/Hosting binaries, launch/probe/checker source and prepared fixture bytes remain exact. Result authentication is rerun on current test DLL.'
 else:
  assert not ds and not db,(mode,ds,db)
  reason=None
 if reason:reuse[mode]={'sourceDifferences':ds,'binaryDifferences':db,'reason':reason}
 reports[mode]={'path':str(path.relative_to(P)),'sha256':check.base.sha(path),'outcomes':data.get('outcomes'),'accepted':True}
 passed.update(row['name'] for row in data.get('cases',[]) if row['outcome']=='Passed')
 assert not data.get('unrunCases',[]),mode
native_path,native=read('native');n=json.loads(Path(native['nativeReport']).read_text())
assert n['status']=='passed' and len(n['runs'])==6 and len(n['results'])==3
for pins in [n['executionPins'],n['fixturePins']]:
 for name,digest in pins.items():assert check.base.sha(Path(name))==digest,name
images=[]
for result in n['results']:
 assert check.base.sha(Path(result['save']))==result['finalDatabaseSha256']
 for report in [result['report'],result['reopenReport']]:
  d=json.loads(Path(report).read_text());assert d['Status']=='passed'
  images.extend(d['Images'])
assert len(images)==15
missing=[];links=0
for path in (check.base.R/'docs/architecture').glob('*.md'):
 for raw in re.findall(r'\[[^\]]+\]\(([^)]+)\)',path.read_text()):
  target=raw.strip('<>').split('#')[0]
  if not target or '://' in target or target.startswith('mailto:'):continue
  links+=1
  if not (path.parent/target).exists():missing.append((str(path),target))
assert not missing,missing
(P/'EVIDENCE_REUSE.json').write_text(json.dumps(reuse,indent=2)+'\n')
(P/'ARCHITECTURE_LINKS.json').write_text(json.dumps({'checkedLocalLinks':links,'missing':missing},indent=2)+'\n')
pins=json.loads((P/'PRESERVATION.json').read_text())
final={'status':'bounded managed, rendered native and journal authentication passed; full campaign and remaining gates open','checkpoint':pins['checkpoint'],'sourceHead':pins['sourceHead'],'milestone':'codex/milestone-care-disposition-qualification-20261002','reports':reports,'distinctPassingCases':len(passed),'newPassingCases':sum(any(part in name for part in ['.Care.DispositionContinuation.','.Client.CareDispositionFixtureSelectionTests.','.Worlds.NativeCareDispositions.']) for name in passed),'renderedProcesses':len(n['runs']),'nativeRoutes':len(n['results']),'renderedImages':len(images),'allSelectedAggregatesPassed':True,'protectedOldTestFiles':len(pins['protected']['tests']),'protectedSpecs':len(pins['protected']['specs']),'predecessorIndexSha256':pins['predecessorIndexSha256'],'architectureLinks':links,'dependencyReuse':'EVIDENCE_REUSE.json','failedReportsRetained':[str(path.relative_to(P)) for path in sorted((P/'qualification').glob('*/report.json')) if not json.loads(path.read_text()).get('accepted')],'limits':['native begins at genuinely prepared pending care; no native combat/approach or network/multiplayer qualification','background injected controls do not qualify physical devices or OS focus','actual pending-cut/dispose/re-cut and separate treatment frontier; no disposition-only maximum pressure or maximum composite-frame proof','crash injection, native pending-after-cut, hostile survival/restart/co-player policy, lived-save migration and production selection remain open','no production history latency, full-platform, art/human playtest or full campaign acceptance','no external Claude request or credit offload']}
(P/'FINAL_CHECKS.json').write_text(json.dumps(final,indent=2)+'\n')
print(json.dumps({k:v for k,v in final.items() if k!='reports'},indent=2))
