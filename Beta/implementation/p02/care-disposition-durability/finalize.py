"""Verify exact final dependencies, retained failures and bounded qualification scope."""
import importlib.util,json,re,sys
from pathlib import Path
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('check',P/'check.py');check=importlib.util.module_from_spec(spec);spec.loader.exec_module(check)
check.preserve();source=check.sources();binaries=check.base.assemblies()
reports={};passed=set();reuse={};natives=[]
layout={'Beta/Runtime/client/scenes/FarmHud.tscn','Beta/Runtime/client/scripts/FarmHud.cs','Beta/Runtime/client/scripts/CareDispositions/CareDispositionPlayProbe.cs'}
clientDll='Beta/Runtime/client/.godot/mono/temp/bin/Debug/OpenFantasy.Client.dll'
def changed(a,b):return sorted(k for k in a.keys()|b.keys() if a.get(k)!=b.get(k))
for mode in ['build','capacity','crash-fixtures','native-archived-fixtures','continuation','native-auth','dispositions-core','crash','crash-auth','native','native-archived-auth','native-original','client-managed']:
 path=Path(json.loads((P/f'.latest-{mode}.json').read_text())['path']);data=json.loads(path.read_text())
 assert data['accepted'],mode
 ds=changed(data['source'],source);db=changed(data['binaries'],binaries)
 if ds or db:
  assert mode in ('capacity','crash-fixtures','native-archived-fixtures','continuation','dispositions-core','crash','crash-auth','client-managed'),mode
  expected=layout-{'Beta/Runtime/client/scenes/FarmHud.tscn'} if mode=='client-managed' else layout
  assert set(ds)==expected and db==[clientDll],(mode,ds,db)
  project=(check.base.R/'tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj').read_text();assert 'FarmHud.cs' not in project and 'CareDispositionPlayProbe.cs' not in project
  reuse[mode]={'sourceDifferences':ds,'binaryDifferences':db,'reason':'Only the Godot HUD scene/script, native visibility probe and client assembly changed. Core, Hosting, Host, selected tests, fixtures, checker and dependency assemblies remain exact. These managed/storage selections neither link FarmHud nor load its scene. Both rendered native aggregates and current native authentication are rerun.'}
 else:assert data['source']==source and data['binaries']==binaries
 assert not data.get('unrunCases',[]),mode
 passed.update(row['name'] for row in data.get('cases',[]) if row['outcome']=='Passed')
 reports[mode]={'path':str(path.relative_to(P)),'sha256':check.base.sha(path),'seconds':data['seconds'],'outcomes':data.get('outcomes'),'accepted':True}
 if mode in ('native','native-original'):natives.append(json.loads(Path(data['nativeReport']).read_text()))
 if mode=='crash':crash=json.loads(Path(data['crashReport']).read_text())
images=[]
assert len(natives)==2
for native in natives:
 assert len(native['runs'])==6 and len(native['results'])==3 and native['status']=='passed'
 for pins in [native['executionPins'],native['fixturePins']]:
  for path,digest in pins.items():assert check.base.sha(Path(path))==digest,path
 for row in native['results']:
  assert check.base.sha(Path(row['save']))==row['finalDatabaseSha256']
  for name in ['report','reopenReport']:
   d=json.loads(Path(row[name]).read_text());assert d['Status']=='passed';images.extend(d['Images'])
assert len(images)==30 and all(Path(path).exists() for path in images)
for path,digest in crash['executionPins'].items():assert check.base.sha(Path(path))==digest,path
assert crash['status']=='passed' and crash['sourceFixturesUnchanged'] and crash['executionUnchanged']
assert len(crash['processKills'])==10 and len(crash['cases'])==5
assert all(row['exitCode']==-9 for row in crash['processKills'])
missing=[];links=0
for path in (check.base.R/'docs/architecture').glob('*.md'):
 for raw in re.findall(r'\[[^\]]+\]\(([^)]+)\)',path.read_text()):
  target=raw.strip('<>').split('#')[0]
  if not target or '://' in target or target.startswith('mailto:'):continue
  links+=1
  if not (path.parent/target).exists():missing.append((str(path),target))
assert not missing,missing
pins=json.loads((P/'PRESERVATION.json').read_text());refactors=pins['permittedTestRefactors']
changes=[name for name,digest in pins['protected']['tests'].items() if check.base.sha(check.ROOT/name)!=digest]
assert set(changes)==set(refactors)
for name,data in refactors.items():data['finalSha256']=check.base.sha(check.ROOT/name)
final={'status':'bounded care durability, pressure and archived native qualification passed; remaining campaign gates open','checkpoint':pins['checkpoint'],'sourceHead':pins['sourceHead'],'milestone':'codex/milestone-care-disposition-durability-20261002','reports':reports,'distinctPassingManagedCases':len(passed),'newPassingManagedCases':sum(any(part in name for part in ['.Care.DispositionCapacity.','.Care.DispositionDurability.','.Worlds.NativeArchivedCareDispositions.']) for name in passed),'transactionFaults':10,'externalProcessKills':10,'renderedProcesses':12,'renderedImages':30,'actualAlternatingDispositions':128,'actualClosingResourceBatches':255,'closingFrameBytes':687591,'continuedFrameBytes':74944,'careContinuationBytes':1183,'protectedTestsAndFixtures':len(pins['protected']['tests']),'byteIdenticalOldTestsAndFixtures':len(pins['protected']['tests'])-len(changes),'explicitAdditiveTestRefactors':refactors,'protectedSpecs':len(pins['protected']['specs']),'predecessorIndexSha256':pins['predecessorIndexSha256'],'architectureLinks':links,'dependencyReuse':'EVIDENCE_REUSE.json','visuallyRejectedReports':'VISUAL_REJECTIONS.json','failedReportsRetained':[str(path.relative_to(P)) for path in sorted((P/'qualification').glob('*/report.json')) if not json.loads(path.read_text()).get('accepted')],'limits':['resource pressure cuts before the disposition limit; not maximum disposition-only world capacity or maximum composite-world frame','SIGKILL at reported transaction boundaries; not power-loss/partial-page storage proof','rendered native starts after managed preparation and one real cut; physical devices, OS focus, platforms and human/art acceptance remain open','hostile survival, restart/co-player policy, authenticated lived-save migration and production selection remain open','history-query production latency and prior unexplained failures remain separate','no full-suite, full-phase or full-campaign acceptance; no external provider call or credit offload']}
(P/'EVIDENCE_REUSE.json').write_text(json.dumps(reuse,indent=2)+'\n')
(P/'FINAL_CHECKS.json').write_text(json.dumps(final,indent=2)+'\n')
print(json.dumps({k:v for k,v in final.items() if k not in ('reports','explicitAdditiveTestRefactors')},indent=2))
