"""Reconcile completed, source-bound qualification; refuse missing or failed evidence."""
import hashlib, importlib.util, json, re, sys
from pathlib import Path
from urllib.parse import unquote
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
ROOT=P.parents[3]
spec=importlib.util.spec_from_file_location('checks',P/'check.py');checks=importlib.util.module_from_spec(spec);spec.loader.exec_module(checks)
def read(path):return json.loads(path.read_text())
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def relative(path):return str(path.relative_to(ROOT))
checks.preserve();source=checks.sources();binaries=checks.assemblies();reports={};cases={};native=0;kills=0
modes={'build':P,'focused':P,'regression':P}
for kind in ('core','outcome-unit','care-fixtures','care-native','care-auth','outcome-fixtures','outcome-native','outcome-auth','fixtures','road-native','road-auth','native','native-auth','retained-auth','durability','outcome-durability'):
 modes[kind]=P/'native-evidence'
for kind,base in modes.items():
 path=Path(read(base/('.latest-'+kind+'.json'))['path']);d=read(path)
 assert d['accepted'] and d.get('sourceUnchanged'),(kind,'unaccepted')
 measured=d.get('binaries',d.get('assemblySha256'))
 if kind=='core':
  prefixes=('Beta/Runtime/src/OpenFantasy.Core/','Beta/Runtime/tests/OpenFantasy.Core.Tests/')
  coreBefore={k:v for k,v in d['sourceSha256'].items() if k.startswith(prefixes)}
  assert coreBefore=={k:v for k,v in source.items() if k.startswith(prefixes)},'Core dependencies changed'
  measured={k:v for k,v in measured.items() if k.startswith(prefixes)}
 else:
  assert d.get('source',d.get('sourceSha256'))==source,(kind,'source changed')
 assert measured and all(binaries.get(k)==v for k,v in measured.items()),(kind,'binaries changed')
 seconds=d.get('seconds',d.get('elapsedSeconds'));assert seconds<d['limitSeconds'],kind
 if 'outcomes' in d:assert set(d['outcomes'])=={'Passed'},kind
 reports[kind]={'path':relative(path),'sha256':sha(path),'seconds':seconds,'outcomes':d.get('outcomes')}
 for name in d.get('cases',d.get('testNames',[])):cases[name]=relative(path)
 for field in ('careReport','outcomeReport','roadReport','retreatReport'):
  if field not in d:continue
  artifact=Path(d[field]);assert sha(artifact)==d[field+'Sha256'],kind
  observed=read(artifact);assert observed['status']=='passed';native+=len(observed['runs'])
 kills+=d.get('actualProcessKills',0)
prior=read(P.parent/'native-accepted-care/FINAL_CHECKS.json');old=set(prior['testEvidenceByName']);missing=old-set(cases);assert not missing,sorted(missing)
assert native==15 and kills==12,(native,kills)
pins=read(P/'PRESERVATION.json')
result={'status':'qualified_bounded_shared_player_host','scope':'Up to four authenticated in-process player ports share one clock, authority and NPC scheduler; per-player input/views/readiness, simultaneous combat and exact outcome/reconnect recovery. No network or native multiple-client acceptance.',
 'currentReports':reports,'distinctManagedCases':len(cases),'coreCases':sum(n.startswith('OpenFantasy.Core.Tests.') for n in cases),'hostingCases':sum(n.startswith('OpenFantasy.Hosting.Tests.') for n in cases),
 'coreReuse':'CARE_REPAIR_DEPENDENCY.json: exact Core/source/test assemblies retained across the Host-only care correction','previousQualifiedCasesCovered':len(old),'newCases':sorted(set(cases)-old),'freshNativeProcesses':native,'freshActualProcessKills':kills,'testEvidenceByName':cases,
 'preservation':{'existingTests':len(pins['Beta/Runtime/tests']),'frozenSpecs':len(pins['Beta/spec-v1_1']),'predecessorIndexSha256':pins['predecessorIndexSha256']},
 'remaining':['Native multiple clients, authenticated network transport and zone chat','Journaled confirmed disconnect, 30-second allowance and safe controller handoff','Independent encounter pockets, lived migration and no-survivor resolver reachability','Full P01/P02 and campaign/production, platforms/devices, art/audio and human acceptance']}
(P/'FINAL_CHECKS.json').write_text(json.dumps(result,indent=2)+'\n')
paths=[ROOT/p for p in ('Beta/Runtime/docs/architecture/hosted-player-connections.md','Beta/Runtime/docs/architecture/time-and-encounters.md','Beta/Runtime/docs/architecture/encounter-outcome.md','Beta/Runtime/docs/architecture/aftermath-care.md','Beta/Runtime/docs/architecture/README.md','Beta/Runtime/README.md','Beta/implementation/AGENT_HANDOFF.md')]
paths+=list(P.glob('*.md'));counts={};broken=[]
for file in paths:
 count=0
 for link in re.findall(r'(?<!!)\[[^\]]*\]\(([^)]+)\)',file.read_text()):
  link=unquote(link.split('#')[0].strip('<>'))
  if not link or '://' in link or link.startswith('mailto:'):continue
  count+=1
  if not (file.parent/link).resolve().exists():broken.append((relative(file),link))
 counts[relative(file)]=count
assert not broken,broken
(P/'DOCUMENT_LINKS.json').write_text(json.dumps({'counts':counts,'missing':broken},indent=2)+'\n')
print(json.dumps({k:v for k,v in result.items() if k not in ('testEvidenceByName','currentReports')},indent=2))
print('Resolved document links:',sum(counts.values()))
