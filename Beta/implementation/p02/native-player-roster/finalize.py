"""Pin completed named evidence without relabeling failed exploratory runs."""
import hashlib,importlib.util,json,subprocess,sys
from pathlib import Path
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('checks',P/'check.py');checks=importlib.util.module_from_spec(spec);spec.loader.exec_module(checks)
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
checks.preserve();source=checks.sources();binaries=checks.base.assemblies()
modes=['build','roster','construction','network','presentation','server','native','native-roster']
reports={};passed=set();counts={};unrun=[]
for mode in modes:
 path=Path(json.loads((P/('.latest-'+mode+'.json')).read_text())['path']);r=json.loads(path.read_text())
 assert r['accepted'] and r['source']==source and r['binaries']==binaries,(mode,'unqualified or changed dependencies')
 reports[mode]={'path':str(path),'sha256':sha(path)}
 counts[mode]=r.get('outcomes',{})
 passed.update(row['name'] for row in r.get('cases',[]) if row['outcome']=='Passed');unrun.extend(r.get('unrunCases',[]))
pins=json.loads((P/'PRESERVATION.json').read_text());links=json.loads((P/'DOC_LINKS.json').read_text());assert not links['broken']
failures=[]
for path in sorted((P/'qualification').glob('*/report.json')):
 r=json.loads(path.read_text())
 if r.get('accepted') is False:failures.append({'path':str(path),'sha256':sha(path),'mode':r['mode'],'outcomes':r.get('outcomes'),'error':r.get('error')})
archives=json.loads((P/'EVIDENCE_ARCHIVES.json').read_text())
for row in archives['archives']:assert sha(P/row['archive'])==row['sha256']
artifacts={str(f):sha(f) for f in Path(reports['native']['path']).parent.rglob('*') if f.is_file() and f.suffix in ('.png','.json','.log') and f.name!='report.json'}
result={'acceptedBoundedScope':True,'sourceHead':pins['sourceHead'],'checkpoint':pins['checkpoint'],'source':source,'binaries':binaries,'reports':reports,'counts':counts,'distinctPassed':len(passed),'passedCases':sorted(passed),'unrunPlatformCases':sorted(set(unrun)),'protectedCounts':{name:len(rows) for name,rows in pins['protected'].items()},'predecessorIndexSha256':pins['predecessorIndexSha256'],'architectureLinksResolved':links['checked'],'checkerSha256':{'check.py':sha(P/'check.py'),'finalize.py':sha(P/'finalize.py')},'nativeArtifactSha256':artifacts,'retainedFailures':failures,'nativeRosterArchives':archives,'nativeProcessCount':7,'scope':'Actual4Godot/TLS players, physical assembly, required-human hold through close/rejoin, all4 own Wait choices, wolf victory, all4 outcome readings and exact save history/ownership; 21 authenticated native commands. Separate real four-roster construction and automatic v2 archive/reopen proofs. Seven successful native processes including two retained solo routes. Original stale-observation failure retained and fixture barrier repaired; no production rules changed. Windows ACL, recorded30s disconnect/controller handoff, physical devices, other platforms, human art/usability and full campaign/production gates remain open.'}
(P/'FINAL_CHECKS.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:result[k] for k in ['counts','distinctPassed','protectedCounts','architectureLinksResolved']},indent=2))
