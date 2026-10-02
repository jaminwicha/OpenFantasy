"""Verify retained qualification pins and summarize the selected scope without changing reports."""
import hashlib,importlib.util,json,re,sys
from pathlib import Path
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('check',P/'check.py');check=importlib.util.module_from_spec(spec);spec.loader.exec_module(check)
check.preserve()
reports={};passed=set();skipped=set()
for mode in ['build','quote-replay','history-regression','network','server-managed']:
 path=Path(json.loads((P/f'.latest-{mode}.json').read_text())['path'])
 data=json.loads(path.read_text())
 assert data['accepted'],mode
 assert data['source']==check.sources(),mode
 assert data['binaries']==check.base.assemblies(),mode
 reports[mode]={'path':str(path.relative_to(P)),'sha256':check.base.sha(path),'outcomes':data.get('outcomes'),'accepted':data['accepted']}
 passed.update(row['name'] for row in data.get('cases',[]) if row['outcome']=='Passed')
 skipped.update(data.get('unrunCases',[]))
baseline=json.loads((P/'qualification/quote-cost-20261002T194203338479Z/report.json').read_text())
current=json.loads(Path(json.loads((P/'.latest-quote-replay.json').read_text())['path']).read_text())
probe='Beta/Runtime/tests/OpenFantasy.Hosting.Tests/HistoryQueries/ArchivedQuoteReplayCostTests.cs'
assert baseline['source'][probe]==current['source'][probe]
missing=[];count=0
for path in (check.base.R/'docs/architecture').glob('*.md'):
 for raw in re.findall(r'\[[^\]]+\]\(([^)]+)\)',path.read_text()):
  target=raw.strip('<>').split('#')[0]
  if not target or '://' in target or target.startswith('mailto:'):continue
  count+=1
  if not (path.parent/target).exists():missing.append({'source':str(path.relative_to(check.ROOT)),'target':target})
assert not missing,missing
(P/'ARCHITECTURE_LINKS.json').write_text(json.dumps({'checkedLocalLinks':count,'missing':missing},indent=2)+'\n')
optimization=json.loads((P/'OPTIMIZATION_CHECKPOINT.json').read_text())
import subprocess
assert subprocess.check_output(['git','rev-parse',optimization['checkpoint']],cwd=check.ROOT,text=True).strip()==optimization['snapshot']
preservation=json.loads((P/'PRESERVATION.json').read_text())
final={'status':'selected managed regression passed; production and historical query timing gates remain open','sourceHead':preservation['sourceHead'],'checkpoint':preservation['checkpoint'],'optimizationCheckpoint':json.loads((P/'OPTIMIZATION_CHECKPOINT.json').read_text()),'milestone':'codex/milestone-archived-quote-replay-20261002','reports':reports,'distinctPassingCases':len(passed),'newPassingCases':sum('.HistoryQueries.' in name for name in passed),'expectedUnrunCases':sorted(skipped),'allSelectedAggregatesPassed':True,'protectedOldTestFiles':len(preservation['protected']['tests']),'protectedSpecs':len(preservation['protected']['specs']),'predecessorIndexSha256':preservation['predecessorIndexSha256'],'finalSourceAndBinariesVerified':True,'unchangedCostProbeSha256':current['source'][probe],'checkerSha256':check.base.sha(P/'check.py'),'architectureLinks':count,'failedReportsRetained':[str(path.relative_to(P)) for path in sorted((P/'qualification').glob('*/report.json')) if not json.loads(path.read_text()).get('accepted')],'scopeLimits':['fixed two-query allocation measurement, not peak memory or production latency guarantee','historical archived-query timeout causes remain unexplained; complete replay is still linear in history','no new multi-cut open-carry quote-query proof','no new native, platform, Core full-suite, production scale, care capacity/crash, migration or full campaign acceptance']}
(P/'FINAL_CHECKS.json').write_text(json.dumps(final,indent=2)+'\n')
print(json.dumps({k:v for k,v in final.items() if k not in ('reports','optimizationCheckpoint')},indent=2))
