"""Serialized source-pinned delegated-control recovery checks; preserves old tests, specs and predecessor index."""
import datetime, fcntl, hashlib, importlib.util, json, os, signal, subprocess, sys, time
from pathlib import Path
import xml.etree.ElementTree as ET
sys.dont_write_bytecode = True
P = Path(__file__).resolve().parent
ROOT = P.parents[3]
spec = importlib.util.spec_from_file_location('base', P.parent/'shared-player-sessions/check.py')
base = importlib.util.module_from_spec(spec); spec.loader.exec_module(base)
FILTERS = {
 'archive-managed': ('Server', 'FullyQualifiedName~DelegatedRecoveryArchiveTests'),
 'process-crash': ('Server', 'FullyQualifiedName~DelegatedRecoveryProcessTests'),
 'native-recovery': ('Server', 'FullyQualifiedName~DelegatedRecoveryNativeTests'),
 'control-hosting': ('Hosting', 'FullyQualifiedName~CoPlayer|FullyQualifiedName~EffectiveControl|FullyQualifiedName~ActorSelection'),
 'recovery-storage': ('Hosting', 'FullyQualifiedName~ControllerRecovery'),
 'session-transport': ('Server', 'FullyQualifiedName~PlayerSessionQueryTests|FullyQualifiedName~RemoteQueryJoinTests|FullyQualifiedName~ConnectedControl'),
 'native-compatibility': ('Server', 'FullyQualifiedName~NativePlayerProcessTests'),
}

def sources():
 return base.sources() | {str(f.relative_to(ROOT)):base.sha(f) for f in base.R.iterdir() if f.is_file() and f.suffix in ('.sln','.props','.targets','.json')}
def preserve():
 pins=json.loads((P/'PRESERVATION.json').read_text())
 for name,digest in pins['protected'].items():
  assert base.sha(ROOT/name)==digest,name
 assert hashlib.sha256(subprocess.check_output(['git','diff','--cached','--binary','--','.',':!Beta'],cwd=ROOT)).hexdigest()==pins['predecessorIndexSha256']
 assert subprocess.check_output(['git','rev-parse',pins['checkpoint']],cwd=ROOT,text=True).strip()==pins['sourceHead']
def main(mode):
 assert mode in ('build','hosting-build') or mode in FILTERS
 building=mode in ('build','hosting-build')
 with Path('/tmp/openfantasy-retained-resource-integration.lock').open('w') as lock:
  fcntl.flock(lock, fcntl.LOCK_EX)
  out=P/'qualification'/(mode+'-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')); out.mkdir(parents=True)
  report={'mode':mode,'source':sources(),'checkerSha256':base.sha(P/'check.py'),'baseCheckerSha256':base.sha(P.parent/'shared-player-sessions/check.py'),'limitSeconds':240 if building else 600,'systemLoadAverageAtStart':os.getloadavg()}; started=time.monotonic()
  target=base.R/('tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj' if mode=='hosting-build' else 'OpenFantasy.sln')
  cmd=[base.DOTNET,'build',str(target),'--no-restore','--nologo']
  if not building:
   suite,selected=FILTERS[mode]
   cmd=[base.DOTNET,'test',str(base.R/f'tests/OpenFantasy.{suite}.Tests/OpenFantasy.{suite}.Tests.csproj'),'--no-build','--no-restore','--nologo','--filter',selected,'--logger','console;verbosity=detailed','--logger','trx;LogFileName=checks.trx','--results-directory',str(out)]
  report['command']=cmd
  try:
   preserve()
   if not building:
    bp=Path(json.loads((P/'.latest-hosting-build.json').read_text())['path']); b=json.loads(bp.read_text())
    assert b['accepted'] and b['source']==report['source'] and b['binaries']==base.assemblies(), 'Tests require matching successful build and binaries'
    report['buildReport']=str(bp); report['buildReportSha256']=base.sha(bp)
   env=os.environ|{'OPENFANTASY_DOTNET':base.DOTNET,'OPENFANTASY_DELEGATED_RECOVERY_EVIDENCE':str(out/'recovery')}
   if mode.startswith('native-'):
    godot=Path('/Users/alucinor/.local/share/openfantasy/toolchains/godot-4.6/Godot_mono.app/Contents/MacOS/Godot')
    assert godot.is_file()
    env['OPENFANTASY_GODOT']=str(godot)
    env['OPENFANTASY_NATIVE_PLAYER_EVIDENCE']=str(out/'native')
    report['nativeTool']={'path':str(godot),'sha256':base.sha(godot)}
   env.pop('OPENFANTASY_HOST_TIMING',None); env.pop('OPENFANTASY_RUNTIME_SCHEDULING',None)
   with (out/'command.log').open('w') as log:
    child=subprocess.Popen(cmd,cwd=base.R,env=env,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
    try: child.wait(timeout=report['limitSeconds'])
    except subprocess.TimeoutExpired: os.killpg(child.pid,signal.SIGKILL); child.wait(); raise
   report.update(exitCode=child.returncode,sourceUnchanged=sources()==report['source'],binaries=base.assemblies())
   preserve()
   if not building:
    rows=ET.parse(out/'checks.trx').findall('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult')
    report['cases']=[{'name':r.get('testName'),'outcome':r.get('outcome')} for r in rows]
    report['outcomes']={k:sum(r.get('outcome')==k for r in rows) for k in {r.get('outcome') for r in rows}}
   report['accepted']=child.returncode==0 and report['sourceUnchanged'] and (building or report['outcomes'].get('Passed',0)>0 and set(report['outcomes'])=={'Passed'})
  except Exception as error: report.update(accepted=False,error=repr(error))
  report['seconds']=time.monotonic()-started; report['accepted']=report['accepted'] and report['seconds']<report['limitSeconds']
  (out/'report.json').write_text(json.dumps(report,indent=2)+'\n'); (P/('.latest-'+mode+'.json')).write_text(json.dumps({'path':str(out/'report.json')})+'\n')
  print(json.dumps({k:v for k,v in report.items() if k not in ('source','binaries','cases','command')},indent=2)); print('evidence:',out)
  if not report['accepted'] and (out/'command.log').exists():print((out/'command.log').read_text()[-5500:])
  return 0 if report['accepted'] else 1
if __name__=='__main__':sys.exit(main(sys.argv[1]))
