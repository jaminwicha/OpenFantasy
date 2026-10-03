"""Serialized, source-pinned effective co-player control qualification; no provider requests or automatic retries."""
import datetime,fcntl,hashlib,importlib.util,json,os,signal,subprocess,sys,time
from pathlib import Path
import xml.etree.ElementTree as ET
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
ROOT=P.parents[3]
spec=importlib.util.spec_from_file_location('base',P.parent/'shared-player-sessions/check.py')
base=importlib.util.module_from_spec(spec);spec.loader.exec_module(base)
def sources():
 return base.sources()|{'Beta/Runtime/OpenFantasy.sln':base.sha(base.R/'OpenFantasy.sln')}
def preserve():
 pins=json.loads((P/'PRESERVATION.json').read_text())
 for group in pins['protected'].values():
  for name,digest in group.items():
   if name in pins.get('permittedTestRefactors',{}):
    archived=pins['permittedTestRefactors'][name];assert base.sha(ROOT/archived['original'])==digest,name
   else:assert base.sha(ROOT/name)==digest,name
 assert hashlib.sha256(subprocess.check_output(['git','diff','--cached','--binary','--','.',':!Beta'],cwd=ROOT)).hexdigest()==pins['predecessorIndexSha256']
 assert subprocess.check_output(['git','rev-parse',pins['checkpoint']],cwd=ROOT,text=True).strip()==pins['sourceHead']
def main(mode):
 filters={
 'effective-core':('Core','FullyQualifiedName~EffectiveControl|FullyQualifiedName~CoPlayerTrade'),
 'effective-hosting':('Hosting','FullyQualifiedName~Worlds.EffectiveControl|FullyQualifiedName~Worlds.CoPlayerTrade'),
 'permissions-core':('Core','FullyQualifiedName~ControllerPermissions'),
 'permissions-storage':('Hosting','FullyQualifiedName~Worlds.ControllerPermissions'),
 'commerce':('Hosting','FullyQualifiedName~WorldExchange|FullyQualifiedName~MaterialProposal|FullyQualifiedName~Markets.SpotExchangeWorldAdmission|FullyQualifiedName~HistoryQueries.WorldPaidQuoteReplaySelection'),
 'care-retreat':('Hosting','FullyQualifiedName~Care.OrdinaryCareWorldTests|FullyQualifiedName~Care.Dispositions|FullyQualifiedName~Recovery.RecoveryJournalTests|FullyQualifiedName~Simulation.Retreat.RetreatContrastTests|FullyQualifiedName~Simulation.Retreat.RetreatSessionTests|FullyQualifiedName~Simulation.Retreat.RetreatRootCodecTests|FullyQualifiedName~Simulation.RetreatCompletion'),
 'core':('Core',None),
 'storage':('Hosting','FullyQualifiedName~WorldStorage|FullyQualifiedName~Worlds.Controllers|FullyQualifiedName~Worlds.ControllerRecovery'),
 # Separate evidence for cases without terminal output in the retained timed-out aggregate; never replaces storage.
 'storage-tail':('Hosting','FullyQualifiedName~Event_interpretation_requires_fresh_admission_and_preserves_the_default_coupled_fixture|FullyQualifiedName~Successful_geometric_assessment_does_not_supply_the_excavators_spent_labor|FullyQualifiedName~Real_earned_stock_builds_walkable_decks_and_support_extraction_settles_only_the_removable_deck'),
 # Storage and network modes already cover Worlds.Controllers and Networking.ControllerTransport.
 'controller':('Hosting','FullyQualifiedName~Simulation.Controllers'),
 'network':('Hosting','FullyQualifiedName~Networking'),
 'shared':('Hosting','FullyQualifiedName~SharedPlayers|FullyQualifiedName~Simulation.LocalWorldSessionTests|FullyQualifiedName~Simulation.EncounterPacingSessionTests|FullyQualifiedName~Simulation.EncounterCombatSessionTests|FullyQualifiedName~Simulation.ConsequenceSessionTests'),
 'archive':('Hosting','FullyQualifiedName~Care.DispositionContinuation|FullyQualifiedName~Care.CoordinatedWorldContinuationTests|FullyQualifiedName~Care.CoordinatedIntegrationRegressionTests|FullyQualifiedName~Simulation.Retreat.RetreatArchiveCutTests'),
 'server-compatibility':('Server','FullyQualifiedName!~Native&FullyQualifiedName!~ControllerRecoveryProcessTests')}
 assert mode=='build' or mode in filters
 with Path('/tmp/openfantasy-retained-resource-integration.lock').open('w') as lock:
  fcntl.flock(lock,fcntl.LOCK_EX)
  out=P/'qualification'/(mode+'-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir(parents=True)
  source=sources();report={'mode':mode,'source':source,'tracing':'--trace' in sys.argv,'limitSeconds':240 if mode=='build' else 600};started=time.monotonic()
  cmd=[base.DOTNET,'build',str(base.R/'OpenFantasy.sln'),'--no-restore','--nologo']
  if mode!='build':
   suite,selected=filters[mode]
   cmd=[base.DOTNET,'test',str(base.R/f'tests/OpenFantasy.{suite}.Tests/OpenFantasy.{suite}.Tests.csproj'),'--no-build','--no-restore','--nologo','--logger','console;verbosity=detailed','--logger','trx;LogFileName=checks.trx','--results-directory',str(out)]
   if selected:cmd.extend(['--filter',selected])
  report['command']=cmd
  report['nativeInputs']={name:os.environ[name] for name in ('OPENFANTASY_NATIVE_CARE_DISPOSITION_FIXTURES','OPENFANTASY_NATIVE_CARE_DISPOSITION_RESULTS','OPENFANTASY_CARE_DISPOSITION_DURABILITY_FIXTURES','OPENFANTASY_CARE_DISPOSITION_DURABILITY_RESULTS','OPENFANTASY_NATIVE_ARCHIVED_CARE_DISPOSITION_FIXTURES','OPENFANTASY_NATIVE_ARCHIVED_CARE_DISPOSITION_RESULTS','OPENFANTASY_NATIVE_CONTROL_RETURN_FIXTURES') if name in os.environ}
  try:
   preserve()
   if mode!='build':
    bpath=Path(json.loads((P/'.latest-build.json').read_text())['path']);b=json.loads(bpath.read_text())
    assert b['accepted'] and b['source']==source and b['binaries']==base.assemblies(),'Tests require a successful current-source build and its actual binaries'
    report['buildReport']=str(bpath)
   env=os.environ|{'OPENFANTASY_DOTNET':base.DOTNET}
   env.pop('OPENFANTASY_HOST_TIMING',None)
   if report['tracing']:
    assert mode!='build'
    env['OPENFANTASY_HOST_TIMING']=str(out)
   with (out/'command.log').open('w') as log:
    process=subprocess.Popen(cmd,cwd=base.R,env=env,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
    try:process.wait(timeout=report['limitSeconds'])
    except subprocess.TimeoutExpired:os.killpg(process.pid,signal.SIGKILL);process.wait();raise
   report['exitCode']=process.returncode;report['sourceUnchanged']=source==sources();report['binaries']=base.assemblies();preserve()
   if mode!='build':
    rows=ET.parse(out/'checks.trx').findall('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult')
    report['outcomes']={k:sum(r.get('outcome')==k for r in rows) for k in {r.get('outcome') for r in rows}}
    report['cases']=[{'name':r.get('testName'),'outcome':r.get('outcome')} for r in rows]
    skipped={r.get('testName') for r in rows if r.get('outcome')=='NotExecuted'}
    expected={'OpenFantasy.Server.Tests.WindowsInvitationTests.Invitation_created_under_readable_parent_has_only_protected_owner_access'} if mode in ('server','server-managed','server-compatibility') and os.name!='nt' else set()
    report['unrunCases']=sorted(skipped);report['expectedSkips']=skipped==expected
   report['accepted']=process.returncode==0 and report['sourceUnchanged'] and (mode=='build' or report['expectedSkips'] and report['outcomes'].get('Passed',0)>0 and set(report['outcomes'])<={'Passed','NotExecuted'})
  except Exception as error:report.update(accepted=False,error=repr(error))
  report['seconds']=time.monotonic()-started;report['accepted']=report['accepted'] and report['seconds']<report['limitSeconds']
  (out/'report.json').write_text(json.dumps(report,indent=2)+'\n');(P/('.latest-'+mode+'.json')).write_text(json.dumps({'path':str(out/'report.json')})+'\n')
  print(json.dumps({k:v for k,v in report.items() if k not in ('source','binaries','command','cases')},indent=2));print('evidence:',out)
  if not report['accepted'] and (out/'command.log').exists():print((out/'command.log').read_text()[-5500:])
  return 0 if report['accepted'] else 1
if __name__=='__main__':sys.exit(main(sys.argv[1]))
