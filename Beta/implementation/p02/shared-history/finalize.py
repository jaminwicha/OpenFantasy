"""Verify current-source shared History evidence and preservation without upgrading its bounded claim."""
import importlib.util,json,subprocess,sys,re
from pathlib import Path
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('checks',P/'check.py'); checks=importlib.util.module_from_spec(spec);spec.loader.exec_module(checks)
base=checks.base; ROOT=checks.ROOT
checks.preserve()
expected=json.loads((P/'EXPECTED_CHECKS.json').read_text())
source=checks.sources(); binaries=base.assemblies(); cases=set(); reports={}; inherited={}
for mode in ('build','hosting-build',*expected):
 path=Path(json.loads((P/('.latest-'+mode+'.json')).read_text())['path']);report=json.loads(path.read_text())
 assert report['accepted'] and report['sourceUnchanged'],mode
 changed_source={name for name in report['source'].keys() | source.keys() if report['source'].get(name)!=source.get(name)}
 changed_binaries={name for name in report['binaries'].keys() | binaries.keys() if report['binaries'].get(name)!=binaries.get(name)}
 if mode=='new-managed' and changed_source:
  assert changed_source <= {'Beta/Runtime/tests/OpenFantasy.Server.Tests/Client/SharedHistoryNative/NativeSharedHistoryEvidence.cs','Beta/Runtime/tests/OpenFantasy.Server.Tests/Client/SharedHistoryNative/NativeSharedHistoryTests.cs','Beta/Runtime/client/scripts/FarmHistoryPanel.cs'},changed_source
  assert all('/tests/OpenFantasy.Server.Tests/' in name or '/client/.godot/' in name for name in changed_binaries),changed_binaries
  inherited[mode]={'claim':'20 managed History cases on byte-identical Hosting/Core test dependencies and linked client source. Only unlinked native Server test assertions and the Godot-only detail presenter changed: complete structural equality, a serialized post-pause baseline, and shorter redundant prose with truthful definition tooltips.',
   'changedSourcesOutsideDependency':sorted(changed_source),'changedBinariesOutsideDependency':sorted(changed_binaries)}
 else:
  assert not changed_source,mode
  if mode!='build':assert not changed_binaries,mode
 assert report['checkerSha256']==base.sha(P/'check.py') and report['baseCheckerSha256']==base.sha(P.parent/'shared-player-sessions/check.py'),mode
 log=path.parent/'command.log';assert log.is_file()
 if mode in ('build','hosting-build'):
  assert '0 Warning(s)' in log.read_text() and '0 Error(s)' in log.read_text(),mode
 else:
  assert report['outcomes']=={'Passed':expected[mode]},mode
  cases.update(row['name'] for row in report['cases']);assert (path.parent/'checks.trx').is_file()
 reports[mode]={'report':str(path.relative_to(ROOT)),'sha256':base.sha(path),'logSha256':base.sha(log),'seconds':report['seconds'],'outcomes':report.get('outcomes'), 'trxSha256':base.sha(path.parent/'checks.trx') if (path.parent/'checks.trx').exists() else None}
subprocess.run(['git','diff','--check','--','Beta'],cwd=ROOT,check=True)
native_path=Path(json.loads((P/'.latest-native-history.json').read_text())['path']).parent/'native/shared-history'
visual=json.loads((P/'VISUAL_REVIEW.json').read_text())
assert visual['acceptedWithinScope'] and len(visual['images'])==4
for item in visual['images']:
 image=ROOT/item['image']; state=ROOT/item['state']
 assert image.is_relative_to(native_path) and state.is_relative_to(native_path)
 assert base.sha(image)==item['sha256'] and base.sha(state)==item['stateSha256']
 assert item['viewport']=={'X':0,'Y':0,'Width':960,'Height':540}
native_artifacts={str(f.relative_to(ROOT)):base.sha(f) for f in native_path.rglob('*') if f.is_file()}
assert native_artifacts and (native_path/'read-only-baseline.json').is_file() and (native_path/'browsing-start.ofc').is_file()
refs=[]
for name in ('private-history-directory.md','player-client.md','presentation-and-interaction.md'):
 path=ROOT/'Beta/Runtime/docs/architecture'/name
 for target in re.findall(r'\]\(([^)]+)\)',path.read_text()):
  if '://' in target or target.startswith('#'):continue
  f=(path.parent/target.split('#')[0]).resolve();assert f.exists(),(str(path),target)
 refs.append({'path':str(path.relative_to(ROOT)),'sha256':base.sha(path)})
failures=[]
for path in (P/'qualification').glob('*/report.json'):
 d=json.loads(path.read_text())
 if not d.get('accepted'):failures.append({'path':str(path.relative_to(ROOT)),'sha256':base.sha(path),'error':d.get('error'),'outcomes':d.get('outcomes')})
result={'accepted':True,'scope':'Shared independently scoped native History browsing and terminal details, with exact opening/page/row lifetimes, real TLS late delivery and bounded controlled-background UI evidence. No latency/phase/release acceptance.','source':source,'binaries':binaries,'checkerSha256':base.sha(P/'check.py'),'finalizerSha256':base.sha(P/'finalize.py'),'reports':reports,'dependencyCheckedReuse':inherited,'distinctPassingCases':len(cases),'cases':sorted(cases),'nativeArtifacts':native_artifacts,'visualReviewSha256':base.sha(P/'VISUAL_REVIEW.json'),'architecture':refs,'preservationSha256':base.sha(P/'PRESERVATION.json'),'retainedFailures':failures,'unqualified':['native paid-quote details, multi-page scrolling and full historical catalog/navigation breadth','permitted historical material-name catalogs without requiring Inventory/Trade','final icon-led production skin and all-resolution/tooltips breadth','production history latency/scale','full profile40 process-kill recovery','previous unexplained history/movement timing and gift-opening failures','physical/human/art/platform/full campaign gates']}
(P/'FINAL_CHECKS.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({k:result[k] for k in ('accepted','scope','distinctPassingCases','retainedFailures')},indent=2))
