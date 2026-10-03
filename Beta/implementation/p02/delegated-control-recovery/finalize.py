"""Bind delegated recovery claims to final source, exact cases and retained artifacts."""
import importlib.util,json,re,subprocess,sys
from pathlib import Path
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('checks',P/'check.py'); checks=importlib.util.module_from_spec(spec); spec.loader.exec_module(checks)
base=checks.base; ROOT=checks.ROOT
checks.preserve(); source=checks.sources(); binaries=base.assemblies()
expected=json.loads((P/'EXPECTED_CHECKS.json').read_text()); reports={}; cases=set(); artifacts={}
for mode in ('build','hosting-build',*expected):
 path=Path(json.loads((P/('.latest-'+mode+'.json')).read_text())['path']); r=json.loads(path.read_text())
 assert r['accepted'] and r['sourceUnchanged'] and r['source']==source,mode
 if mode!='build':assert r['binaries']==binaries,mode
 assert r['checkerSha256']==base.sha(P/'check.py') and r['baseCheckerSha256']==base.sha(P.parent/'shared-player-sessions/check.py'),mode
 log=path.parent/'command.log';assert log.is_file()
 if mode in ('build','hosting-build'):
  assert '0 Warning(s)' in log.read_text() and '0 Error(s)' in log.read_text(),mode
 else:
  assert r['outcomes']=={'Passed':expected[mode]},mode
  cases.update(c['name'] for c in r['cases']); assert (path.parent/'checks.trx').is_file()
 reports[mode]={'path':str(path.relative_to(ROOT)),'sha256':base.sha(path),'logSha256':base.sha(log),'seconds':r['seconds'],'outcomes':r.get('outcomes'),'trxSha256':base.sha(path.parent/'checks.trx') if (path.parent/'checks.trx').exists() else None}
 if mode in ('archive-managed','process-crash','native-recovery'):
  for directory in ('native','recovery'):
   for f in (path.parent/directory).rglob('*'):
    if f.is_file():artifacts[str(f.relative_to(ROOT))]=base.sha(f)
visual=json.loads((P/'VISUAL_REVIEW.json').read_text()); assert visual['acceptedWithinScope'] and visual['images']
for item in visual['images']:
 assert artifacts[item['image']]==item['sha256']
 assert artifacts[item['state']]==item['stateSha256']
subprocess.run(['git','diff','--check','--','Beta'],cwd=ROOT,check=True)
failures=[]
for f in sorted((P/'qualification').glob('*/report.json')):
 r=json.loads(f.read_text())
 if not r.get('accepted'):failures.append({'path':str(f.relative_to(ROOT)),'sha256':base.sha(f),'outcomes':r.get('outcomes'),'error':r.get('error')})
result={'accepted':True,'scope':'Actual delegated paid-obligation archive/host-storage-process recovery and bounded native continuation. Does not qualify listener/socket death, production latency or full phase/campaign.','source':source,'binaries':binaries,'checkerSha256':base.sha(P/'check.py'),'finalizerSha256':base.sha(P/'finalize.py'),'reports':reports,'distinctPassingCases':len(cases),'cases':sorted(cases),'artifacts':artifacts,'preservationSha256':base.sha(P/'PRESERVATION.json'),'visualReviewSha256':base.sha(P/'VISUAL_REVIEW.json'),'retainedFailures':failures}
(P/'FINAL_CHECKS.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({k:result[k] for k in ('accepted','scope','distinctPassingCases','retainedFailures')},indent=2))
