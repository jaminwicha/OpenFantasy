"""Bounded serialized build/test evidence for shared player sessions; no external model calls."""
import datetime,fcntl,hashlib,json,subprocess,sys,time,os,signal
from pathlib import Path
import xml.etree.ElementTree as ET
P=Path(__file__).resolve().parent
ROOT=P.parents[3]
R=ROOT/'Beta/Runtime'
DOTNET='/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet'
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def sources():
 return {str(p.relative_to(ROOT)):sha(p) for base in ('src','tests','client','content','tools') for p in (R/base).rglob('*') if p.is_file() and p.suffix in ('.cs','.csproj','.props','.targets','.json','.tscn','.tres','.svg','.godot','.py','.png') and not set(p.relative_to(R).parts).intersection(('obj','bin','.godot','__pycache__'))}
def assemblies():return {str(p.relative_to(ROOT)):sha(p) for base in ('src','tests','client/.godot/mono/temp') for p in (R/base).rglob('OpenFantasy*.dll') if '/obj/' not in str(p) and '/bin/' in str(p)}
def preserve():
 d=json.loads((P/'PRESERVATION.json').read_text())
 for group in ('Beta/Runtime/tests','Beta/spec-v1_1'):
  for path,expected in d[group].items():assert sha(ROOT/path)==expected,path
 assert hashlib.sha256(subprocess.check_output(['git','diff','--cached','--binary','--','.',':!Beta'],cwd=ROOT)).hexdigest()==d['predecessorIndexSha256']
 assert subprocess.check_output(['git','rev-parse',d['checkpoint']],cwd=ROOT,text=True).strip()==d['sourceHead']
def main(mode):
 with Path('/tmp/openfantasy-retained-resource-integration.lock').open('w') as lock:
  fcntl.flock(lock,fcntl.LOCK_EX)
  out=P/'qualification'/(mode+'-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir(parents=True)
  filters={'focused':'FullyQualifiedName~SharedPlayers|FullyQualifiedName~Simulation.LocalWorldSessionTests|FullyQualifiedName~Simulation.EncounterPacingSessionTests|FullyQualifiedName~Simulation.EncounterCombatSessionTests|FullyQualifiedName~Simulation.ConsequenceSessionTests',
   'regression':'(FullyQualifiedName~Simulation|FullyQualifiedName~Client|FullyQualifiedName~WorldExchange|FullyQualifiedName~MaterialProposal|FullyQualifiedName~Content)&FullyQualifiedName!~NativeTradingResultTests&FullyQualifiedName!~NativeGiftResultTests&FullyQualifiedName!~RetainedGiftResultTests&FullyQualifiedName!~NativeTradingBoundaryResultTests&FullyQualifiedName!~NativeCargoResultTests&FullyQualifiedName!~CargoCircuitJourneyTests&FullyQualifiedName!~CargoCircuitEvidenceTests&FullyQualifiedName!~NativeRetreatResultTests&FullyQualifiedName!~RetreatDurabilityResultTests&FullyQualifiedName!~RetreatDurabilityFixtureTests&FullyQualifiedName!~NativeRoadThreatResultTests&FullyQualifiedName!~NativeEncounterOutcomeResultTests&FullyQualifiedName!~NativeEncounterOutcomeFixtureTests&FullyQualifiedName!~EncounterOutcomeDurabilityResultTests&FullyQualifiedName!~EncounterOutcomeDurabilityFixtureTests'}
  cmd=[DOTNET,'build',str(R/'OpenFantasy.sln'),'--no-restore','--nologo'] if mode=='build' else [DOTNET,'test',str(R/'tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj'),'--no-build','--no-restore','--nologo','--filter',filters[mode],'--logger','console;verbosity=detailed','--logger','trx;LogFileName=checks.trx','--results-directory',str(out)]
  report={'mode':mode,'source':sources(),'command':cmd,'limitSeconds':240 if mode=='build' else 600};t=time.monotonic()
  try:
   preserve()
   if mode!='build':
    buildPath=Path(json.loads((P/'.latest-build.json').read_text())['path']);build=json.loads(buildPath.read_text())
    assert build['accepted'] and build['source']==report['source'] and build['binaries']==assemblies(),'Tests require the successful current-source build and its actual binaries'
    report.update(buildReport=str(buildPath),buildReportSha256=sha(buildPath))
   with (out/'command.log').open('w') as log:
    r=subprocess.Popen(cmd,cwd=R,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
    try:r.wait(timeout=report['limitSeconds'])
    except subprocess.TimeoutExpired:
     os.killpg(r.pid,signal.SIGTERM)
     try:r.wait(timeout=3)
     except subprocess.TimeoutExpired:os.killpg(r.pid,signal.SIGKILL);r.wait()
     raise
    report['exitCode']=r.returncode
   report['binaries']=assemblies();report['seconds']=time.monotonic()-t;report['sourceUnchanged']=sources()==report['source'];preserve()
   if mode!='build':
    nodes=ET.parse(out/'checks.trx').findall('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult');report['outcomes']={v:sum(n.get('outcome')==v for n in nodes) for v in set(n.get('outcome') for n in nodes)};report['cases']=[n.get('testName') for n in nodes]
   report['accepted']=r.returncode==0 and report['sourceUnchanged'] and report['seconds']<report['limitSeconds'] and (mode=='build' or set(report['outcomes'])=={'Passed'})
  except Exception as e:report.update(accepted=False,error=repr(e),seconds=time.monotonic()-t)
  (out/'report.json').write_text(json.dumps(report,indent=2)+'\n');(P/('.latest-'+mode+'.json')).write_text(json.dumps({'path':str(out/'report.json')})+'\n')
  print(json.dumps({k:v for k,v in report.items() if k not in ('source','binaries','command','cases')},indent=2));print('evidence:',out)
  if not report['accepted'] and (out/'command.log').exists():print((out/'command.log').read_text()[-4500:])
  return 0 if report['accepted'] else 1
if __name__=='__main__':sys.exit(main(sys.argv[1]))
