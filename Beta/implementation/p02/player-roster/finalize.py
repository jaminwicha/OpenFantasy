"""Pin completed named evidence without relabeling failed exploratory runs."""
import hashlib,importlib.util,json,subprocess,sys
from pathlib import Path
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('checks',P/'check.py');checks=importlib.util.module_from_spec(spec);spec.loader.exec_module(checks)
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
checks.preserve();source=checks.sources();binaries=checks.base.assemblies()
modes=['build','catalog','roster','network','core','query-diagnostic','network-regression','observation-compatibility','server','domain','sessions','presentation','native']
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
 if not r['accepted']:failures.append({'path':str(path),'sha256':sha(path),'mode':r['mode'],'outcomes':r.get('outcomes'),'error':r.get('error')})
native=Path(reports['native']['path']).parent
artifacts={str(f):sha(f) for f in native.rglob('*') if f.is_file() and f.suffix in ('.png','.json','.log') and f.name!='report.json'}
result={'acceptedBoundedScope':True,'sourceHead':pins['sourceHead'],'checkpoint':pins['checkpoint'],'source':source,'binaries':binaries,'reports':reports,'counts':counts,'distinctPassed':len(passed),'passedCases':sorted(passed),'unrunPlatformCases':sorted(set(unrun)),'protectedCounts':{name:len(rows) for name,rows in pins['protected'].items()},'predecessorIndexSha256':pins['predecessorIndexSha256'],'architectureLinksResolved':links['checked'],'checkerSha256':{'check.py':sha(P/'check.py'),'finalize.py':sha(P/'finalize.py')},'nativeArtifactSha256':artifacts,'retainedFailures':failures,'upstreamSkillSync':json.loads((P/'UPSTREAM_SCOPE_SYNC.json').read_text()),'scope':'Fresh1–4human cargo admission, finite owned provisions, unchanged solo selection, explicit bounded lifecyclev2; actual4TLS protocol clients move/reconnect/recover; separate single-native-client headless/background regressions. Failed exploratory aggregate is retained; selected passes do not explain its archived-query timeout or qualify production latency. Full4native combat/reconnect, construction completion with roster, v2 archive rollover and all full production/device/art/human gates remain open.'}
(P/'FINAL_CHECKS.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:result[k] for k in ['counts','distinctPassed','protectedCounts','architectureLinksResolved']},indent=2))
