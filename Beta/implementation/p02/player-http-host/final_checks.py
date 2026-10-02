"""Reconcile executed scope without rewriting earlier failures or skipped test reports."""
import hashlib,importlib.util,json,subprocess,sys,xml.etree.ElementTree as ET
from pathlib import Path
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('checks',P/'check.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
m.preserve()
reports={mode:Path(json.loads((P/('.latest-'+mode+'.json')).read_text())['path']) for mode in ('build','server','network','content','content-evidence','shared')}
data={mode:json.loads(path.read_text()) for mode,path in reports.items()}
source=m.sources();binaries=m.baseline.assemblies()
for mode,value in data.items():
 assert value['source']==source and value['binaries']==binaries and value['sourceUnchanged'],mode
 assert value['exitCode']==0 and value['seconds']<value['limitSeconds'],mode
 if mode!='content':assert value['accepted'],mode
passed={};skipped={}
for mode,path in reports.items():
 if mode=='build':continue
 rows=ET.parse(path.parent/'checks.trx').findall('.//{*}UnitTestResult')
 assert all(row.get('outcome') in ('Passed','NotExecuted') for row in rows),mode
 passed[mode]={row.get('testName') for row in rows if row.get('outcome')=='Passed'}
 skipped[mode]={row.get('testName') for row in rows if row.get('outcome')=='NotExecuted'}
# The original broad content invocation remains incomplete. Its sole missing case is supplied by
# the separate execution with its authenticated retained fixture and the same source/binaries.
assert skipped['content']==passed['content-evidence'] and len(skipped['content'])==1
assert data['content']['outcomes']=={'Passed':156,'NotExecuted':1}
assert not skipped['content-evidence']
priorP=P.parent/'player-queries'
prior=json.loads(Path(json.loads((priorP/'.latest-network.json').read_text())['path']).read_text())
assert set(prior['cases'])<=passed['network'] and len(prior['cases'])==188
assert len(passed['shared'])==9
allpassed=set().union(*passed.values())
pins=json.loads((P/'PRESERVATION.json').read_text())
result={'acceptedBoundedScope':True,'sourceHead':pins['sourceHead'],'reports':{k:{'path':str(v.relative_to(P)),'sha256':m.baseline.sha(v)} for k,v in reports.items()},'passedByGroup':{k:len(v) for k,v in passed.items()},'distinctPassed':len(allpassed),'allPriorNetworkingCasesPassed':188,'unrunPlatformCases':sorted(skipped['server']),'contentSelectionDisposition':'Original report remains rejected: 156 passes plus one missing fixture case. Separate current-build execution passes that exact one case on original pinned saved journey. Aggregate covers all157 selected cases.','protectedFiles':{k:len(v) for k,v in pins['protected'].items()},'predecessorIndexSha256':pins['predecessorIndexSha256'],'source':source,'binaries':binaries,'scope':'Local macOS TLS/listener/provisioning/process integration plus affected managed regressions. No full game, native multiplayer, Windows/Linux execution or production qualification.','retainedFailures':sorted(str(p.relative_to(P)) for p in (P/'qualification').glob('*/report.json') if not json.loads(p.read_text()).get('accepted',False))}
(P/'FINAL_CHECKS.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({k:v for k,v in result.items() if k not in ('source','binaries')},indent=2))
