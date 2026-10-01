"""Bounded road-threat integration checks; no provider requests and no foreground control."""
import collections, fcntl, hashlib, json, os, re, signal, subprocess, sys, time, uuid, shutil
from pathlib import Path
import xml.etree.ElementTree as ET
P=Path(__file__).resolve().parent
BASE=P.parent/'road-threat'
ROOT=P.parents[3];R=ROOT/'Beta/Runtime'
DOTNET='/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet'
GODOT='/Users/alucinor/.local/share/openfantasy/toolchains/godot-4.6/Godot_mono.app/Contents/MacOS/Godot'
MODES=('build','unit','regression','fixtures','native','native-auth','legacy-native','legacy-auth','core','cargo-native','cargo-auth','durability','retained-auth','road-native','road-auth')
NS='{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}'
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def sources():
 return {str(p.relative_to(ROOT)):sha(p) for base in ('src','tests','client','content','tools') for p in (R/base).rglob('*') if p.is_file() and p.suffix in ('.cs','.csproj','.props','.targets','.json','.tscn','.tres','.svg','.godot','.py','.png') and not set(p.relative_to(R).parts).intersection(('obj','bin','.godot','__pycache__'))}
def assemblies():
 paths=[p for base in ('src','tests') for p in (R/base).glob('*/bin/Debug/net*/OpenFantasy*.dll')]+list((R/'client/.godot/mono/temp/bin/Debug').glob('OpenFantasy*.dll'))
 return {str(p.relative_to(ROOT)):sha(p) for p in paths}
def preservation():
 for path,expected in json.loads((P/'PROTECTED_REGRESSIONS.json').read_text()).items():
  assert (ROOT/path).is_file() and sha(ROOT/path)==expected,'Lead regression changed: '+path
 pins=json.loads((BASE/'PRESERVATION.json').read_text());project='Beta/Runtime/tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj'
 for group in ('existingTests','frozenSpecs'):
  assert not [p for p,h in pins[group].items() if p != project and (not (ROOT/p).is_file() or sha(ROOT/p)!=h)],group+' changed'
 baseline=(BASE/'Hosting.Tests.csproj.original').read_bytes();actual=(ROOT/project).read_bytes();old=set(json.loads((BASE/'CLIENT_BASELINE_PATHS.json').read_text()));links=[]
 for line in actual.splitlines(keepends=True):
  if line in baseline.splitlines(keepends=True):continue
  match=re.fullmatch(rb'    <Compile Include="../../client/scripts/([A-Za-z0-9_./]+\.cs)" Link="Client/([A-Za-z0-9_./]+\.cs)" />\r?\n',line)
  assert match,'Only additive new client source links permitted'
  relative=match.group(1).decode();assert '..' not in relative and relative not in old and (R/'client/scripts'/relative).is_file(),'Link must name a new client source'
  assert line not in links,'Duplicate source link';links.append(line)
 stripped=actual
 for line in links:stripped=stripped.replace(line,b'',1)
 assert stripped==baseline,'Existing test project changed'
 for name in ('PROTECTED_APPLICATION.json',):
  entries=json.loads((BASE/name).read_text());assert not [p for p,h in entries.items() if not (ROOT/p).is_file() or sha(ROOT/p)!=h],name+' changed'
 for row in pins['copiedAssets']['copiedFiles']:
  assert sha(ROOT/row['source'])==row['sha256'] and sha(ROOT/row['copy'])==row['sha256'],'Imported source art changed'
 assert sha(pins['sourceDatabase'])==pins['sourceDatabaseSha256'],'Original database changed'
 assert hashlib.sha256(subprocess.check_output(['git','diff','--cached','--binary','--','.',':!Beta'],cwd=ROOT)).hexdigest()==pins['predecessorIndexSha256'],'Predecessor staging changed'
 assert subprocess.check_output(['git','rev-parse',pins['checkpoint']],cwd=ROOT,text=True).strip()==pins['sourceHead'],'Checkpoint changed'
 return {'existingTests':len(pins['existingTests']),'frozenSpecs':len(pins['frozenSpecs']),'newCompileLinks':len(links),'protectedBytesPreserved':True}
def imported_assets():
 client=R/'client';verified={}
 for source in client.rglob('*'):
  if not source.is_file() or source.suffix not in ('.png','.svg') or '.godot' in source.parts:continue
  metadata=Path(str(source)+'.import');assert metadata.is_file(),'Missing import '+str(source)
  remap=re.search(r'^path="res://(.+)"$',metadata.read_text(),re.M);assert remap,'Missing remap '+str(source)
  destination=client/remap[1];assert destination.is_file(),'Missing imported resource '+str(destination)
  hashes=destination.with_suffix('.md5');assert hashes.is_file(),'Missing import hash '+str(source)
  for label,file in [('source',source),('dest',destination)]:
   expected=re.search(r'^'+label+r'_md5="([a-f0-9]+)"$',hashes.read_text(),re.M)
   assert expected and hashlib.md5(file.read_bytes()).hexdigest()==expected[1],'Stale import '+str(file)
  verified[str(source.relative_to(ROOT))]={'sourceSha256':sha(source),'resourceSha256':sha(destination)}
 assert len(verified)==40,'The reviewed graphical import inventory changed; review its new scope'
 return verified
def latest(kind):
 p=Path(json.loads((P/('.latest-'+kind+'.json')).read_text())['path']);d=json.loads(p.read_text());assert d['accepted'] and d['kind']==kind,'Missing accepted '+kind;return p,d
def main():
 assert len(sys.argv)==2 and sys.argv[1] in MODES;kind=sys.argv[1];(P/'qualification').mkdir(parents=True,exist_ok=True)
 with Path('/tmp/openfantasy-retained-resource-integration.lock').open('w') as lock:
  fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
  out=P/'qualification'/(kind+'-'+time.strftime('%Y%m%dT%H%M%SZ',time.gmtime())+'-'+uuid.uuid4().hex[:5]);out.mkdir();start=time.monotonic()
  report={'kind':kind,'sourceSha256':sources(),'commands':[],'limitSeconds':240 if kind=='build' else 900 if kind in ('native','cargo-native','road-native') else 600};print('evidence: '+str(out),flush=True)
  env=dict(os.environ,DOTNET_ROOT=str(Path(DOTNET).parent),PATH=str(Path(DOTNET).parent)+os.pathsep+os.environ.get('PATH',''))
  for key in list(env):
   if key.startswith('OPENFANTASY_') and any(x in key for x in ('FIXTURES','RESULTS','EVIDENCE','SOURCE')):env.pop(key)
  def run(cmd):
   log=out/(str(len(report['commands']))+'.log');row={'command':cmd,'log':str(log)};report['commands'].append(row)
   with log.open('x') as stream:
    proc=subprocess.Popen(cmd,cwd=ROOT,env=env,stdout=stream,stderr=subprocess.STDOUT,start_new_session=True)
    try:proc.wait(timeout=max(1,report['limitSeconds']-(time.monotonic()-start)))
    except BaseException:
     try:os.killpg(proc.pid,signal.SIGKILL)
     except ProcessLookupError:pass
     proc.wait();raise
   row['exitCode']=proc.returncode
   if proc.returncode:print('\n'.join(log.read_text(errors='replace').splitlines()[-45:]),flush=True)
   assert proc.returncode==0,'Command failed; retained '+str(log)
   return log
  def test(selection,label):run([DOTNET,'test',str(R/'tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj'),'--no-build','--no-restore','--filter',selection,'--logger','console;verbosity=detailed','--logger','trx;LogFileName='+label+'.trx','--results-directory',str(out)])
  try:
   report['preservationBefore']=preservation()
   if kind=='build':
    for project in (R/'OpenFantasy.sln',R/'client/OpenFantasy.Client.csproj'):run([DOTNET,'build',str(project),'--no-restore','-m:1','-p:UseSharedCompilation=false','--verbosity','quiet'])
    log=run([GODOT,'--headless','--path',str(R/'client'),'--editor','--quit-after','30']);assert 'ERROR:' not in log.read_text(errors='replace'),'Godot import/editor error'
    editor=log.read_text(errors='replace');assert 'first_scan_filesystem' in editor and 'loading_editor_layout' in editor and editor.count('[ DONE ]')>=2,'Editor did not finish initialization'
    report['importedAssets']=imported_assets()
   else:
    bp,b=latest('build');assert b['sourceSha256']==report['sourceSha256'] and b['assemblySha256']==assemblies(),'Source or binaries differ from accepted build';report.update(buildReport=str(bp),buildReportSha256=sha(bp))
    if kind=='core':run([DOTNET,'test',str(R/'tests/OpenFantasy.Core.Tests/OpenFantasy.Core.Tests.csproj'),'--no-build','--no-restore','--logger','console;verbosity=detailed','--logger','trx;LogFileName=core.trx','--results-directory',str(out)])
    elif kind=='unit':test('(FullyQualifiedName~Client.RoadThreat|FullyQualifiedName~Simulation.RoadThreat|FullyQualifiedName~Content.RoadThreat)&FullyQualifiedName!~FixtureTests&FullyQualifiedName!~ResultTests','unit')
    elif kind=='regression':test('(FullyQualifiedName~Simulation|FullyQualifiedName~Client|FullyQualifiedName~WorldExchange|FullyQualifiedName~MaterialProposal|FullyQualifiedName~Content)&FullyQualifiedName!~NativeTradingResultTests&FullyQualifiedName!~NativeGiftResultTests&FullyQualifiedName!~RetainedGiftResultTests&FullyQualifiedName!~NativeTradingBoundaryResultTests&FullyQualifiedName!~NativeCargoResultTests&FullyQualifiedName!~CargoCircuitJourneyTests&FullyQualifiedName!~CargoCircuitEvidenceTests&FullyQualifiedName!~NativeRetreatResultTests&FullyQualifiedName!~RetreatDurabilityResultTests&FullyQualifiedName!~RetreatDurabilityFixtureTests&FullyQualifiedName!~NativeRoadThreatResultTests','regression')
    elif kind=='retained-auth':
     evidence=json.loads((P/'RETAINED_NATIVE.json').read_text());report['retainedManifests']={}
     for label,row in evidence.items():
      assert all(Path(f).is_file() and sha(f)==h for f,h in row['fileSha256'].items()),'Retained native evidence changed'
      document=json.loads(Path(row['manifest']).read_text());copies=out/label;copies.mkdir()
      def relocate(value):
       if isinstance(value,dict):return {k:relocate(v) for k,v in value.items()}
       if isinstance(value,list):return [relocate(v) for v in value]
       if isinstance(value,str) and value in row['fileSha256'] and Path(value).suffix in ('.sqlite','.sqlite3','.db'):
        original=Path(value);assert not Path(value+'-wal').exists() and not Path(value+'-shm').exists(),'Retained fixture is not closed'
        destination=copies/(hashlib.sha256(value.encode()).hexdigest()[:12]+original.suffix)
        if not destination.exists():shutil.copy2(original,destination)
        assert sha(destination)==row['fileSha256'][value];return str(destination)
       return value
      manifest=copies/'manifest.json';manifest.write_text(json.dumps(relocate(document),indent=2)+'\n')
      var={'retreat':'OPENFANTASY_NATIVE_RETREAT_RESULTS','cargo':'OPENFANTASY_NATIVE_CARGO_RESULTS','trading':'OPENFANTASY_NATIVE_TRADING_RESULTS','gifts':'OPENFANTASY_NATIVE_GIFT_RESULTS'}[label.split('-')[0]]
      selection={'retreat':'NativeRetreatResultTests','cargo':'NativeCargoResultTests','trading':'NativeTradingResultTests','gifts':'NativeGiftResultTests'}[label.split('-')[0]]
      env[var]=str(manifest);test('FullyQualifiedName~'+selection,'retained-'+label);env.pop(var)
      assert all(sha(f)==h for f,h in row['fileSha256'].items()),'Retained evidence changed during authentication'
      report['retainedManifests'][label]={'source':row['manifest'],'copiedManifest':str(manifest),'sourceFiles':len(row['fileSha256'])}
    elif kind=='durability':
     env['OPENFANTASY_RETREAT_DURABILITY_FIXTURES']=str(out/'fixtures')
     test('FullyQualifiedName~RetreatDurabilityFixtureTests','durability-fixtures')
     env.pop('OPENFANTASY_RETREAT_DURABILITY_FIXTURES')
     manifest=out/'fixtures/manifest.json';assert manifest.is_file(),'Missing durability fixture manifest'
     original={str(f.relative_to(out/'fixtures')):sha(f) for f in (out/'fixtures').rglob('*') if f.is_file()}
     result=out/'durability.json'
     run(['python3',str(R/'tools/check_retreat_durability.py'),'--dotnet',DOTNET,'--fixtures',str(out/'fixtures'),'--output',str(result)])
     evidence=json.loads(result.read_text());assert evidence['status']=='passed','Durability tool did not pass'
     kills=evidence['processKills'];assert len(kills)>=2 and all(row['exitCode']==-9 and row['pid']>0 for row in kills),'Missing genuine killed fixture processes'
     assert {'before','after'}<=set(row['boundary'] for row in kills),'Both transaction boundaries required'
     assert original=={str(f.relative_to(out/'fixtures')):sha(f) for f in (out/'fixtures').rglob('*') if f.is_file()},'Durability source fixtures changed'
     env['OPENFANTASY_RETREAT_DURABILITY_RESULTS']=str(result)
     test('FullyQualifiedName~RetreatDurabilityResultTests','durability-auth')
     env.pop('OPENFANTASY_RETREAT_DURABILITY_RESULTS')
     report.update(durabilityReport=str(result),durabilityReportSha256=sha(result),durabilityFixtureSha256=original,actualProcessKills=len(kills))
    elif kind=='fixtures':
     for label,selection,var in [('road','NativeRoadThreatFixtureTests','OPENFANTASY_NATIVE_ROAD_THREAT_FIXTURES'),('retreat','NativeRetreatFixtureTests','OPENFANTASY_NATIVE_RETREAT_FIXTURES'),('cargo','NativeCargoFixtureTests','OPENFANTASY_NATIVE_CARGO_FIXTURES'),('trading','NativeTradingFixtureTests','OPENFANTASY_NATIVE_TRADING_FIXTURES'),('gifts','NativeGiftFixtureTests','OPENFANTASY_NATIVE_GIFT_FIXTURES')]:
      env[var]=str(out/label);test('FullyQualifiedName~'+selection,label);assert (out/label/'save.sqlite').is_file() and (out/label/'genesis.ofc').is_file(),'Missing closed fixture export';env.pop(var);report[label+'Fixtures']=str(out/label)
     report['fixtureSha256']={str(f.relative_to(out)):sha(f) for label in ('road','retreat','cargo','trading','gifts') for f in (out/label).rglob('*') if f.is_file()}
    elif kind in ('native','cargo-native','legacy-native','road-native'):
     fp,f=latest('fixtures');assert f['sourceSha256']==report['sourceSha256'] and f['assemblySha256']==assemblies(),'Stale fixtures';assert all(sha(fp.parent/n)==h for n,h in f['fixtureSha256'].items()),'Fixture bytes changed';report.update(fixtureReport=str(fp),fixtureReportSha256=sha(fp));labels=['road'] if kind=='road-native' else ['retreat'] if kind=='native' else ['cargo'] if kind=='cargo-native' else ['trading','gifts']
     for label in labels:
      nr=out/(label+'.json');cmd=['python3',str(R/'tools'/('check_native_'+('road_threat' if label=='road' else label)+'.py')),'--dotnet',DOTNET,'--godot',GODOT,'--fixtures',f[label+'Fixtures'],'--output',str(nr),'--rendered']
      if label not in ('cargo','retreat','road'):cmd+=['--mode','all']
      run(cmd);observed=json.loads(nr.read_text());assert observed['status']=='passed',label+' native failed';report[label+'Report']=str(nr);report[label+'ReportSha256']=sha(nr)
      assert 'managedAuthenticationManifest' in observed,'Missing authentication manifest';report[label+'AuthenticationManifest']=observed['managedAuthenticationManifest']
     assert all(sha(fp.parent/n)==h for n,h in f['fixtureSha256'].items()),'Native run changed source fixture bytes'
    elif kind in ('native-auth','cargo-auth','legacy-auth','road-auth'):
     np,n=latest('road-native' if kind=='road-auth' else 'native' if kind=='native-auth' else 'cargo-native' if kind=='cargo-auth' else 'legacy-native');assert n['sourceSha256']==report['sourceSha256'] and n['assemblySha256']==assemblies(),'Native evidence is stale';report.update(nativeReport=str(np),nativeReportSha256=sha(np))
     for label in (['road'] if kind=='road-auth' else ['retreat'] if kind=='native-auth' else ['cargo'] if kind=='cargo-auth' else ['trading','gifts']):
      assert sha(n[label+'Report'])==n[label+'ReportSha256'],'Native report changed'
      selection={'road':'NativeRoadThreatResultTests','retreat':'NativeRetreatResultTests','cargo':'NativeCargoResultTests','trading':'NativeTradingResultTests','gifts':'NativeGiftResultTests'}[label];var={'road':'OPENFANTASY_NATIVE_ROAD_THREAT_RESULTS','retreat':'OPENFANTASY_NATIVE_RETREAT_RESULTS','cargo':'OPENFANTASY_NATIVE_CARGO_RESULTS','trading':'OPENFANTASY_NATIVE_TRADING_RESULTS','gifts':'OPENFANTASY_NATIVE_GIFT_RESULTS'}[label];env[var]=n[label+'AuthenticationManifest'];test('FullyQualifiedName~'+selection,label);env.pop(var)
    if kind in ('core','unit','regression','fixtures','native-auth','cargo-auth','legacy-auth','durability','retained-auth','road-auth'):
     rows=[row.attrib for trx in out.glob('*.trx') for row in ET.parse(trx).findall('.//'+NS+'UnitTestResult')];assert rows and all(row['outcome']=='Passed' for row in rows),'Missing/failed/skipped tests';report['outcomes']=dict(collections.Counter(row['outcome'] for row in rows));report['testNames']=[row['testName'] for row in rows]
    assert b['assemblySha256']==assemblies(),'Binaries changed during check'
   report['assemblySha256']=assemblies();report['preservationAfter']=preservation()
  except Exception as error:report['error']=repr(error)
  report['elapsedSeconds']=round(time.monotonic()-start,3);report['sourceUnchanged']=report['sourceSha256']==sources();report['accepted']=not report.get('error') and report['sourceUnchanged'] and report['elapsedSeconds']<report['limitSeconds'];path=out/'report.json';path.write_text(json.dumps(report,indent=2)+'\n')
  if report['accepted']:(P/('.latest-'+kind+'.json')).write_text(json.dumps({'path':str(path)})+'\n')
  print(json.dumps({k:v for k,v in report.items() if k not in ('sourceSha256','assemblySha256','fixtureSha256','commands','testNames','importedAssets')},indent=2),flush=True);return 0 if report['accepted'] else 1
if __name__=='__main__':sys.exit(main())
