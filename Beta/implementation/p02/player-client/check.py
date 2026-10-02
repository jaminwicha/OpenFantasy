"""Bounded serialized checks for the player networking increment; preserve previous tests and specs."""
import datetime,fcntl,hashlib,importlib.util,json,os,signal,subprocess,sys,time
from pathlib import Path
import xml.etree.ElementTree as ET
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
ROOT=P.parents[3]
spec=importlib.util.spec_from_file_location('baseline',P.parent/'shared-player-sessions/check.py')
baseline=importlib.util.module_from_spec(spec);spec.loader.exec_module(baseline)
def sources():
 result=baseline.sources()
 result['Beta/Runtime/OpenFantasy.sln']=baseline.sha(baseline.R/'OpenFantasy.sln')
 return result
def preserve():
 pins=json.loads((P/'PRESERVATION.json').read_text())
 for rows in pins['protected'].values():
  for path,expected in rows.items():assert baseline.sha(ROOT/path)==expected,path
 assert hashlib.sha256(subprocess.check_output(['git','diff','--cached','--binary','--','.',':!Beta'],cwd=ROOT)).hexdigest()==pins['predecessorIndexSha256']
 assert subprocess.check_output(['git','rev-parse',pins['checkpoint']],cwd=ROOT,text=True).strip()==pins['sourceHead']
def main(mode):
 assert mode in ('build','network','server','client','shared')
 with Path('/tmp/openfantasy-retained-resource-integration.lock').open('w') as lock:
  fcntl.flock(lock,fcntl.LOCK_EX)
  out=P/'qualification'/(mode+'-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir(parents=True)
  filters={'network':'FullyQualifiedName~Networking|FullyQualifiedName~PlayerObservation','shared':'FullyQualifiedName~SharedPlayers','server':'FullyQualifiedName~OpenFantasy.Server.Tests','client':'FullyQualifiedName~PlayerProtocolClientTests'}
  source=sources();report={'mode':mode,'source':source,'limitSeconds':240 if mode in ('restore','build') else 120 if mode=='network' else 600};start=time.monotonic()
  cmd=[baseline.DOTNET,'restore',str(baseline.R/'OpenFantasy.sln'),'--use-lock-file','--nologo'] if mode=='restore' else [baseline.DOTNET,'build',str(baseline.R/'OpenFantasy.sln'),'--no-restore','--nologo'] if mode=='build' else [baseline.DOTNET,'test',str(baseline.R/('tests/OpenFantasy.Server.Tests/OpenFantasy.Server.Tests.csproj' if mode in ('server','client') else 'tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj')),'--no-build','--no-restore','--nologo','--filter',filters[mode],'--logger','console;verbosity=detailed','--logger','trx;LogFileName=checks.trx','--results-directory',str(out)]
  report['command']=cmd
  try:
   preserve()
   if mode not in ('restore','build'):
    buildPath=Path(json.loads((P/'.latest-build.json').read_text())['path']);build=json.loads(buildPath.read_text())
    assert build['accepted'] and build['source']==source and build['binaries']==baseline.assemblies(),'A successful current-source build is required'
    report['buildReport']=str(buildPath)
   env=os.environ|{'OPENFANTASY_DOTNET':baseline.DOTNET}
   with (out/'command.log').open('w') as log:
    proc=subprocess.Popen(cmd,cwd=baseline.R,env=env,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
    try:proc.wait(timeout=report['limitSeconds'])
    except subprocess.TimeoutExpired:
     os.killpg(proc.pid,signal.SIGKILL);proc.wait();raise
    report['exitCode']=proc.returncode
   report['binaries']=baseline.assemblies();report['sourceUnchanged']=source==sources();preserve()
   if mode=='restore':
    after=sources();changed={name for name in set(source)|set(after) if source.get(name)!=after.get(name)}
    allowed={'Beta/Runtime/src/OpenFantasy.Server/packages.lock.json','Beta/Runtime/tests/OpenFantasy.Server.Tests/packages.lock.json','Beta/Runtime/src/OpenFantasy.Host/packages.lock.json'}
    report['restoredLockFiles']=sorted(changed);report['expectedRestoreChanges']=changed<=allowed
    report['restoredSource']=after
   if mode not in ('restore','build'):
    rows=ET.parse(out/'checks.trx').findall('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult')
    report['outcomes']={outcome:sum(row.get('outcome')==outcome for row in rows) for outcome in {row.get('outcome') for row in rows}}
    report['cases']=[row.get('testName') for row in rows]
    skipped={row.get('testName') for row in rows if row.get('outcome')=='NotExecuted'}
    platform_case='OpenFantasy.Server.Tests.DedicatedHostProcessTests.Actual_host_process_handles_sigterm_and_releases_its_saved_world' if os.name=='nt' else 'OpenFantasy.Server.Tests.WindowsInvitationTests.Invitation_created_under_readable_parent_has_only_protected_owner_access'
    expected_skips={platform_case} if mode=='server' else set()
    report['unrunPlatformCases']=sorted(skipped)
    report['expectedPlatformSkips']=skipped==expected_skips
   report['accepted']=proc.returncode==0 and (report['sourceUnchanged'] or mode=='restore' and report.get('expectedRestoreChanges',False)) and (mode in ('restore','build') or report['expectedPlatformSkips'] and report['outcomes'].get('Passed',0)>0 and set(report['outcomes'])<={'Passed','NotExecuted'})
  except Exception as error:report.update(accepted=False,error=repr(error))
  report['seconds']=time.monotonic()-start;report['accepted']=report['accepted'] and report['seconds']<report['limitSeconds']
  (out/'report.json').write_text(json.dumps(report,indent=2)+'\n');(P/('.latest-'+mode+'.json')).write_text(json.dumps({'path':str(out/'report.json')})+'\n')
  print(json.dumps({k:v for k,v in report.items() if k not in ('source','restoredSource','binaries','command','cases')},indent=2));print('evidence:',out)
  if not report['accepted'] and (out/'command.log').exists():print((out/'command.log').read_text()[-4000:])
  return 0 if report['accepted'] else 1
if __name__=='__main__':sys.exit(main(sys.argv[1]))
