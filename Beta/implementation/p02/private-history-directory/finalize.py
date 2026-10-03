"""Verify current-source directory evidence and preservation without upgrading its bounded claim."""
import importlib.util,json,subprocess,sys,re
from pathlib import Path
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('checks',P/'check.py'); checks=importlib.util.module_from_spec(spec);spec.loader.exec_module(checks)
base=checks.base; ROOT=checks.ROOT
checks.preserve()
api=json.loads((P/'API_CHECKPOINT.json').read_text())
assert subprocess.check_output(['git','rev-parse',api['ref']],cwd=ROOT,text=True).strip()==api['snapshot']
assert subprocess.check_output(['git','rev-parse',api['snapshot']+'^'],cwd=ROOT,text=True).strip()==api['parent']
expected={'new-managed':31,'new-tls':2,'selected':46,'legacy-queries':66,'client':36,'session-transport':13}
source=checks.sources(); binaries=base.assemblies(); cases=set(); reports={}
for mode in ('build','hosting-build','new-managed','new-tls','selected','legacy-queries','client','session-transport'):
 path=Path(json.loads((P/('.latest-'+mode+'.json')).read_text())['path']);report=json.loads(path.read_text())
 assert report['accepted'] and report['sourceUnchanged'],mode
 assert report['source']==source,mode
 assert report['checkerSha256']==base.sha(P/'check.py') and report['baseCheckerSha256']==base.sha(P.parent/'shared-player-sessions/check.py'),mode
 if mode!='build':assert report['binaries']==binaries,mode
 log=path.parent/'command.log';assert log.is_file()
 if mode in ('build','hosting-build'):
  assert '0 Warning(s)' in log.read_text() and '0 Error(s)' in log.read_text(),mode
 else:
  assert report['outcomes']=={'Passed':expected[mode]},mode
  cases.update(row['name'] for row in report['cases']);assert (path.parent/'checks.trx').is_file()
 reports[mode]={'report':str(path.relative_to(ROOT)),'sha256':base.sha(path),'logSha256':base.sha(log),'seconds':report['seconds'],'outcomes':report.get('outcomes'), 'trxSha256':base.sha(path.parent/'checks.trx') if (path.parent/'checks.trx').exists() else None}
subprocess.run(['git','diff','--check','--','Beta'],cwd=ROOT,check=True)
refs=[]
for name in ('README.md','private-history-directory.md','player-queries.md','player-client.md','selected-player-protocol.md','effective-control.md'):
 path=ROOT/'Beta/Runtime/docs/architecture'/name
 for target in re.findall(r'\]\(([^)]+)\)',path.read_text()):
  if '://' in target or target.startswith('#'):continue
  f=(path.parent/target.split('#')[0]).resolve();assert f.is_file(),(str(path),target)
 refs.append({'path':str(path.relative_to(ROOT)),'sha256':base.sha(path)})
failures=[]
for path in (P/'qualification').glob('*/report.json'):
 d=json.loads(path.read_text())
 if not d.get('accepted'):failures.append({'path':str(path.relative_to(ROOT)),'sha256':base.sha(path),'error':d.get('error'),'outcomes':d.get('outcomes')})
result={'accepted':True,'scope':'Authenticated bounded selected private-history directory, selected protocol and local/remote API. No native history UI or latency/phase/release acceptance.','source':source,'binaries':binaries,'checkerSha256':base.sha(P/'check.py'),'finalizerSha256':base.sha(P/'finalize.py'),'reports':reports,'distinctPassingCases':len(cases),'cases':sorted(cases),'architecture':refs,'preservationSha256':base.sha(P/'PRESERVATION.json'),'apiCheckpointSha256':base.sha(P/'API_CHECKPOINT.json'),'retainedFailures':failures,'unqualified':['native history browser and panel lifetimes','production history latency/scale','full profile40 process-kill recovery','previous unexplained history/movement timing and gift-opening failures','physical/human/art/platform/full campaign gates']}
(P/'FINAL_CHECKS.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({k:result[k] for k in ('accepted','scope','distinctPassingCases','retainedFailures')},indent=2))
