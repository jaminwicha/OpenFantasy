"""Finalize bounded live-host presence evidence; no closure of full actor selection or production gates."""
import hashlib
import importlib.util
import json
import re
import sys
from pathlib import Path
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('qualification',P/'check.py')
check=importlib.util.module_from_spec(spec);spec.loader.exec_module(check)
check.preserve()
source,binaries=check.sources(),check.base.assemblies()
reports,passed,unrun={},set(),set()
for mode in ['build','core','presence','controllers','effective','player-routing','server-compatibility']:
 path=Path(json.loads((P/f'.latest-{mode}.json').read_text())['path']);data=json.loads(path.read_text())
 assert data['accepted'] and data['source']==source and data['binaries']==binaries,mode
 assert data['seconds']<data['limitSeconds'],mode
 if mode=='build':
  log=(path.parent/'command.log').read_text();assert '0 Warning(s)' in log and '0 Error(s)' in log
 else:
  assert data['expectedSkips'] and data['outcomes'].get('Passed',0)>0
  assert set(data['outcomes'])<={'Passed','NotExecuted'}
  passed.update(row['name'] for row in data['cases'] if row['outcome']=='Passed')
  unrun.update(data['unrunCases'])
 reports[mode]={'path':str(path.relative_to(P)),'sha256':check.base.sha(path),'seconds':data['seconds'],'outcomes':data.get('outcomes'),'accepted':True}
assert reports['presence']['outcomes']['Passed']==8
assert unrun=={'OpenFantasy.Server.Tests.WindowsInvitationTests.Invitation_created_under_readable_parent_has_only_protected_owner_access'}
normal_path=P/'qualification/server-compatibility-20261003T023156547590Z/report.json'
normal=json.loads(normal_path.read_text())
assert not normal['accepted'] and not normal['tracing'] and normal['source']==source and normal['binaries']==binaries
assert normal['outcomes']=={'Failed':1,'Passed':75,'NotExecuted':1}
assert [row['name'] for row in normal['cases'] if row['outcome']=='Failed']==['OpenFantasy.Server.Tests.PlayerRosterHostTests.Four_production_players_connect_move_and_reconnect_over_real_tls_without_converting_npcs']
assert json.loads((P/reports['server-compatibility']['path']).read_text())['tracing']
reports['server-compatibility']['claim']='instrumented diagnostic only; original uninstrumented gate remains failed'
inherited=json.loads((P/'INHERITED_CORE_INPUTS.json').read_text())
prior=P/inherited['packet']/inherited['report'];assert check.base.sha(prior)==inherited['reportSha256']
assert inherited['acceptedPreviously'] and inherited['coverage']=={'Passed':1885}
for name,digest in inherited['source'].items():assert source.get(name)==digest,name
assert reports['core']['outcomes']=={'Passed':1885}
changed_core_binaries=[name for name,digest in inherited['binaries'].items() if binaries.get(name)!=digest]
assert changed_core_binaries
links=0
paths=list((check.base.R/'docs/architecture').glob('*.md'))+[check.ROOT/'Beta/implementation/AGENT_HANDOFF.md',P/'RESULT.md',P/'DESIGN.md',P/'REVIEW.md']
for path in paths:
 for raw in re.findall(r'\[[^\]]+\]\(([^)]+)\)',path.read_text()):
  target=raw.strip('<>').split('#')[0]
  if not target or '://' in target or target.startswith('mailto:'):continue
  assert (path.parent/target).exists() or (path.parent/target).resolve()==P/'FINAL_CHECKS.json',(path,target)
  links+=1
pins=json.loads((P/'PRESERVATION.json').read_text())
result={'status':'bounded local host custody verified; uninstrumented server timing gate and full actor selection remain open',
 'milestone':'codex/milestone-live-control-presence-20261003',
 'checkpoint':pins['checkpoint'],'sourceHead':pins['sourceHead'],'reports':reports,
 'freshDistinctPassingCases':len(passed),'newPassingCases':reports['presence']['outcomes']['Passed'],
 'freshHostingPassingCases':154,'serverCompatibilityGateClosed':False,
 'normalServerFailure':{'report':str(normal_path.relative_to(P)),'sha256':check.base.sha(normal_path),'outcomes':normal['outcomes'],'seconds':normal['seconds']},
 'coreRevalidation':{'priorPacket':inherited['packet'],'priorReport':inherited['report'],'priorReportSha256':inherited['reportSha256'],'passingCases':1885,'unchangedSourceFiles':len(inherited['source']),'changedBinaries':changed_core_binaries,'acceptance':'fresh current-binary Core suite; no binary-equivalence claim'},
 'protectedTestsAndFixtures':len(pins['protected']['tests']),'protectedSpecs':len(pins['protected']['specs']),
 'predecessorIndexSha256':pins['predecessorIndexSha256'],'checkedLinks':links,'unrunCases':sorted(unrun),
 'toolPins':{str(path.relative_to(check.ROOT)):check.base.sha(path) for path in [P/'check.py',P/'finalize.py',P.parent/'shared-player-sessions/check.py']},
 'retainedLocalFailures':[str(path.relative_to(P)) for path in sorted((P/'qualification').glob('*/report.json')) if not json.loads(path.read_text())['accepted']],
 'limits':['Internal assignment API only; no public local/TLS/Godot actor-switching interface or selection generations.',
 'Consent and accepted paid gameplay preparation use actual trusted journal commands; they do not prove selected-port input.',
 'In-memory actual admission is not TLS/rendered/device acceptance.',
 'Complete previous storage aggregate is retained as previous-source evidence, not relabeled a fresh aggregate.',
 'Profile40 actual process-kill, archive-selection, native, human, full phase and production gates remain open.',
 'Earlier timing failures remain unresolved; no deadlines, movement leases or acceptance thresholds changed.',
 'No external provider request or claimed Claude credit saving.']}
(P/'FINAL_CHECKS.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({k:result[k] for k in ['status','freshDistinctPassingCases','newPassingCases','protectedTestsAndFixtures','protectedSpecs','checkedLinks']},indent=2))
