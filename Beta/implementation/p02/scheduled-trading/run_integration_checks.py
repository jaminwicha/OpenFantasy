"""Bounded paid-world implementation checks. Test modes take an exact accepted build report.
The retained fixture is read as bytes then copied by tests; the original database is not opened.
Do not overlap with this task's other builds, tests or rendered probes.
"""
import collections,fcntl,hashlib,json,os,signal,subprocess,sys,time,traceback,uuid
from pathlib import Path
import xml.etree.ElementTree as ET
TASK=Path(__file__).resolve().parent
PACKET=TASK; ROOT=Path('/Users/alucinor/Workspace/Projects/OpenFantasy')
C={'sourceFixture':os.environ.get('OPENFANTASY_RETAINED_PROPOSAL_SOURCE',str(ROOT/'Beta/implementation/p02/care-definition-digests/qualification/durable-hosting-final'))}
DOTNET='/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet'
NS='{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}'
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def sources():
 return {str(p.relative_to(ROOT)):digest(p) for base in ('src','tests','client/scripts') for p in (ROOT/'Beta/Runtime'/base).rglob('*') if p.is_file() and p.suffix in ('.cs','.csproj','.props','.targets','.json') and not set(p.parts).intersection(('obj','bin','.godot'))}
def assemblies():
 return {str(p.relative_to(ROOT)):digest(p) for base in ('src','tests') for p in (ROOT/'Beta/Runtime'/base).glob('*/bin/Debug/net*/OpenFantasy*.dll')}
def preservation():
 pins=json.loads((PACKET/'PRESERVATION.json').read_text())
 for group in ('existingTests','frozenSpecs'):
  changed=[p for p,h in pins[group].items() if not (ROOT/p).is_file() or digest(ROOT/p)!=h]
  assert not changed,(group,changed)
 assert digest(Path(pins['sourceDatabase']))==pins['sourceDatabaseSha256'],'Original database changed'
 assert hashlib.sha256(subprocess.check_output(['git','diff','--cached','--binary','--','.',':!Beta'],cwd=ROOT)).hexdigest()==pins['predecessorIndexSha256'],'Predecessor staging changed'
 assert subprocess.check_output(['git','rev-parse',pins['checkpoint']],cwd=ROOT,text=True).strip()==pins['sourceHead'],'Checkpoint changed'
 core=json.loads((PACKET/'CORE_PRESERVATION.json').read_text())
 assert all(digest(ROOT/p)==h for p,h in core.items()),'Core source changed outside this application assignment'
 return {'existingTests':len(pins['existingTests']),'frozenSpecs':len(pins['frozenSpecs']),'sourceAndIndexPreserved':True}
def main():
 (PACKET/'qualification').mkdir(parents=True,exist_ok=True)
 kind=sys.argv[1]; assert kind in ('build','focused-core','focused-host','full-core','new-core','new-host','legacy-consented-host','legacy-care-host','legacy-proposal-host','session-host')
 assert kind in ('build','focused-core','focused-host','full-core','new-core','new-host','legacy-consented-host','legacy-care-host','legacy-proposal-host','session-host'), 'Only the assigned checks are allowed'
 assert len(sys.argv)==2
 with (Path('/tmp')/'openfantasy-retained-resource-integration.lock').open('w') as lock:
  fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
  label=kind+'-'+time.strftime('%Y%m%dT%H%M%SZ',time.gmtime())+'-'+uuid.uuid4().hex[:5]
  evidence=PACKET/'qualification'/label;evidence.mkdir()
  report={'kind':kind,'sourceSha256':sources(),'limitSeconds':240 if kind=='build' else 600,'commands':[]}
  print('evidence: '+str(evidence),flush=True)
  start=time.monotonic()
  try:
   report['preservation']=preservation()
   if kind=='build': commands=[[DOTNET,'build','Beta/Runtime/OpenFantasy.sln','--no-restore','-m:1','-p:UseSharedCompilation=false','--verbosity','quiet']]
   else:
    build_path=Path(json.loads((TASK/'.latest-build.json').read_text())['path']); built=json.loads(build_path.read_text())
    assert built['kind']=='build' and built['accepted'],'Require accepted build evidence'
    report['buildReport']=str(build_path);report['buildReportSha256']=digest(build_path)
    assert built['sourceSha256']==report['sourceSha256'] and built['assemblySha256']==assemblies(),'Sources/assemblies differ from successful build'
    selections={
     'new-core':'FullyQualifiedName~WorldExchange',
     'new-host':'FullyQualifiedName~WorldExchange',
     'legacy-consented-host':'FullyQualifiedName~ConsentedWorld',
     'legacy-care-host':'FullyQualifiedName~WorldResource|FullyQualifiedName~CoordinatedWorld',
     'legacy-proposal-host':'FullyQualifiedName~MaterialProposal|FullyQualifiedName~SpotExchangeWorldAdmission',
     'full-core':'FullyQualifiedName~OpenFantasy.Core.Tests',
     'focused-core':'FullyQualifiedName~WorldExchange|FullyQualifiedName~Markets|FullyQualifiedName~WorldResource|FullyQualifiedName~ResourceHistory|FullyQualifiedName~MaterialProposal|FullyQualifiedName~WorldCoordinated|FullyQualifiedName~MaterialCare|FullyQualifiedName~ConsentedWorld',
     'session-host':'FullyQualifiedName~Simulation|FullyQualifiedName~WorldExchange.Scheduled',
     'focused-host':'FullyQualifiedName~WorldExchange|FullyQualifiedName~SpotExchangeWorldAdmission|FullyQualifiedName~MaterialProposal|FullyQualifiedName~WorldResource|FullyQualifiedName~CoordinatedWorld|FullyQualifiedName~ConsentedWorld'}
    project='OpenFantasy.Hosting.Tests' if kind.endswith('-host') else 'OpenFantasy.Core.Tests'
    commands=[[DOTNET,'test','Beta/Runtime/tests/'+project+'/'+project+'.csproj','--no-build','--no-restore','--filter',selections[kind],'--logger','console;verbosity=detailed','--logger','trx;LogFileName='+project+'.trx','--results-directory',str(evidence)]]
   env=dict(os.environ);env['OPENFANTASY_RETAINED_PROPOSAL_SOURCE']=C['sourceFixture']
   # No native or source-world fixture is mutated by this component check.
   report['environmentInputs']={name:env[name] for name in ('OPENFANTASY_PAID_WORLD_FIXTURES','OPENFANTASY_RETAINED_PROPOSAL_SOURCE','OPENFANTASY_RETAINED_PROPOSAL_EVIDENCE','OPENFANTASY_NATIVE_GIFT_RESULTS','OPENFANTASY_RETAINED_GIFT_RESULTS','OPENFANTASY_RETAINED_GIFT_FIXTURES') if name in env}
   for i,cmd in enumerate(commands):
    logfile=evidence/(str(i)+'.log')
    with logfile.open('x') as out:
     process=subprocess.Popen(cmd,cwd=ROOT,env=env,stdout=out,stderr=subprocess.STDOUT,start_new_session=True)
     try: process.wait(timeout=max(1,report['limitSeconds']-(time.monotonic()-start)))
     except BaseException:
      try: os.killpg(process.pid,signal.SIGKILL)
      except ProcessLookupError: pass
      process.wait()
      raise
    report['commands'].append({'command':cmd,'exitCode':process.returncode,'log':str(logfile)})
    if process.returncode: print('\n'.join(logfile.read_text(errors='replace').splitlines()[-70:]))
    assert process.returncode==0,'Command failed; log retained'
   if kind!='build':
    results=[]
    for trx in evidence.glob('*.trx'):
     rows=ET.parse(trx).findall('.//'+NS+'UnitTestResult')
     assert rows,'Empty required test selection: '+str(trx)
     if not kind.startswith('legacy-') and kind!='session-host': assert any('.WorldExchange.' in row.attrib['testName'] for row in rows), 'No new paid-world cases executed'
     results += [(row.attrib['testId'],row.attrib['outcome']) for row in rows]
    report['outcomes']=dict(collections.Counter(outcome for _,outcome in results));report['testIds']=[key for key,_ in results]
    assert results and all(outcome=='Passed' for _,outcome in results),'Failed or skipped required test'
   report['assemblySha256']=assemblies();report['preservationAfter']=preservation()
   if kind!='build': assert report['assemblySha256']==built['assemblySha256'],'Assemblies changed during verification'
  except Exception as ex:
   report['error']=repr(ex)
  report['elapsedSeconds']=round(time.monotonic()-start,3);report['sourceUnchanged']=sources()==report['sourceSha256']
  report['accepted']=not report.get('error') and report['sourceUnchanged']
  (evidence/'report.json').write_text(json.dumps(report,indent=2)+'\n')
  if report['accepted'] and kind=='build': (TASK/'.latest-build.json').write_text(json.dumps({'path':str(evidence/'report.json')}))
  print(json.dumps({key:value for key,value in report.items() if key not in ('sourceSha256','assemblySha256','commands','testIds')},indent=2))
  print('evidence: '+str(evidence))
  return 0 if report['accepted'] else 1
if __name__=='__main__': sys.exit(main())
