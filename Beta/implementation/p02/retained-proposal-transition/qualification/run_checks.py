"""Reproducible retained-transition checks. Test modes take an exact accepted build report.
The retained fixture is read as bytes then copied by tests; the original database is not opened.
Do not overlap with this task's other builds, tests or rendered probes.
"""
import collections,fcntl,hashlib,json,os,signal,subprocess,sys,time,traceback,uuid
from pathlib import Path
import xml.etree.ElementTree as ET
TASK=Path(__file__).resolve().parent
PACKET=TASK.parent; ROOT=PACKET.parents[3]
C={'sourceFixture':os.environ.get('OPENFANTASY_RETAINED_PROPOSAL_SOURCE',str(ROOT/'Beta/implementation/p02/care-definition-digests/qualification/durable-hosting-final'))}
DOTNET='/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet'
NS='{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}'
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def sources():
 return {str(p.relative_to(ROOT)):digest(p) for base in ('src','tests','client/scripts') for p in (ROOT/'Beta/Runtime'/base).rglob('*') if p.is_file() and p.suffix in ('.cs','.csproj','.props','.targets','.json') and not set(p.parts).intersection(('obj','bin','.godot'))}
def assemblies():
 return {str(p.relative_to(ROOT)):digest(p) for base in ('src','tests') for p in (ROOT/'Beta/Runtime'/base).glob('*/bin/Debug/net8.0/OpenFantasy*.dll')}
def preservation():
 pins=json.loads((PACKET/'PRESERVATION.json').read_text())
 for group in ('existingTests','frozenSpecs'):
  changed=[p for p,h in pins[group].items() if not (ROOT/p).is_file() or digest(ROOT/p)!=h]
  assert not changed,(group,changed)
 assert digest(Path(pins['sourceDatabase']))==pins['sourceDatabaseSha256'],'Original database changed'
 assert hashlib.sha256(subprocess.check_output(['git','diff','--cached','--binary','--','.',':!Beta'],cwd=ROOT)).hexdigest()==pins['predecessorIndexSha256'],'Predecessor staging changed'
 assert subprocess.check_output(['git','rev-parse',pins['checkpoint']],cwd=ROOT,text=True).strip()==pins['sourceHead'],'Checkpoint changed'
 return {'existingTests':len(pins['existingTests']),'frozenSpecs':len(pins['frozenSpecs']),'sourceAndIndexPreserved':True}
def main():
 kind=sys.argv[1]; assert kind in ('build','focused','regression','core','sessions','hosting','native-auth')
 assert len(sys.argv)==(2 if kind=='build' else 3),'Pass the exact accepted build report for testing'
 with (Path('/tmp')/'openfantasy-retained-transition-checks.lock').open('w') as lock:
  fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
  label=kind+'-'+time.strftime('%Y%m%dT%H%M%SZ',time.gmtime())+'-'+uuid.uuid4().hex[:5]
  evidence=PACKET/'qualification'/label;evidence.mkdir()
  report={'kind':kind,'sourceSha256':sources(),'limitSeconds':240 if kind=='build' else 600,'commands':[]}
  start=time.monotonic()
  try:
   report['preservation']=preservation()
   if kind=='build': commands=[[DOTNET,'build','Beta/Runtime/OpenFantasy.sln','--no-restore','-m:1','-p:UseSharedCompilation=false','--verbosity','quiet']]
   else:
    build_path=Path(sys.argv[2]).resolve(); built=json.loads(build_path.read_text())
    assert built['kind']=='build' and built['accepted'],'Require accepted build evidence'
    report['buildReport']=str(build_path);report['buildReportSha256']=digest(build_path)
    assert built['sourceSha256']==report['sourceSha256'] and built['assemblySha256']==assemblies(),'Sources/assemblies differ from successful build'
    selection='FullyQualifiedName~OpenFantasy.Core.Tests' if kind=='core' else 'FullyQualifiedName~OpenFantasy.Hosting.Tests.Simulation|FullyQualifiedName~OpenFantasy.Hosting.Tests.Client' if kind=='sessions' else 'FullyQualifiedName~ProposalMigration' if kind=='focused' else 'FullyQualifiedName~Migration|FullyQualifiedName~Upgrade|FullyQualifiedName~MaterialProposal|FullyQualifiedName~CoordinatedWorldContinuation|FullyQualifiedName~CareContinuationAdmission|FullyQualifiedName~DurableMaterialCare'
    if kind=='native-auth': selection='FullyQualifiedName~OpenFantasy.Hosting.Tests.Client.NativeGiftResultTests'
    if kind in ('hosting','sessions','native-auth'):
     manifest=os.environ.get('OPENFANTASY_NATIVE_GIFT_RESULTS')
     assert manifest and Path(manifest).is_file(),'Supply OPENFANTASY_NATIVE_GIFT_RESULTS for selected native journal authentication'
     report['nativeManifest']={'path':manifest,'sha256':digest(Path(manifest))}
    if kind=='hosting': selection='(FullyQualifiedName~Migration|FullyQualifiedName~Upgrade|FullyQualifiedName~MaterialProposal|FullyQualifiedName~CoordinatedWorldContinuation|FullyQualifiedName~CareContinuationAdmission|FullyQualifiedName~DurableMaterialCare|FullyQualifiedName~OpenFantasy.Hosting.Tests.Simulation|FullyQualifiedName~OpenFantasy.Hosting.Tests.Client)&FullyQualifiedName!~ProposalMigration'
    commands=[[DOTNET,'test','Beta/Runtime/tests/'+project+'/'+project+'.csproj','--no-build','--no-restore','--filter',selection,'--logger','console;verbosity=detailed','--logger','trx;LogFileName='+project+'.trx','--results-directory',str(evidence)] for project in (('OpenFantasy.Core.Tests',) if kind=='core' else ('OpenFantasy.Hosting.Tests',) if kind in ('sessions','hosting','native-auth') else ('OpenFantasy.Core.Tests','OpenFantasy.Hosting.Tests'))]
   env=dict(os.environ);env['OPENFANTASY_RETAINED_PROPOSAL_SOURCE']=C['sourceFixture']
   report['environmentInputs']={name:env[name] for name in ('OPENFANTASY_RETAINED_PROPOSAL_SOURCE','OPENFANTASY_RETAINED_PROPOSAL_EVIDENCE','OPENFANTASY_NATIVE_GIFT_RESULTS') if name in env}
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
  print(json.dumps({key:value for key,value in report.items() if key not in ('sourceSha256','assemblySha256','commands','testIds')},indent=2))
  print('evidence: '+str(evidence))
  return 0 if report['accepted'] else 1
if __name__=='__main__': sys.exit(main())
