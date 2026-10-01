"""Lead-owned bounded checks for native trading refinement. No provider request."""
import collections, fcntl, hashlib, json, os, re, signal, subprocess, sys, time, uuid
from pathlib import Path
import xml.etree.ElementTree as ET
P = Path(__file__).resolve().parent
ROOT = P.parents[3]
R = ROOT / 'Beta/Runtime'
DOTNET = '/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet'
GODOT = '/Users/alucinor/.local/share/openfantasy/toolchains/godot-4.6/Godot_mono.app/Contents/MacOS/Godot'
NS = '{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}'
MODES = ('build', 'unit', 'regression', 'fixtures', 'native', 'native-auth', 'legacy-native', 'boundary-native', 'boundary-auth')
def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def sources():
    return {str(p.relative_to(ROOT)):sha(p) for base in ('src','tests','client','tools') for p in (R/base).rglob('*')
            if p.is_file() and p.suffix in ('.cs','.csproj','.props','.targets','.json','.tscn','.tres','.svg','.godot','.py')
            and not set(p.relative_to(R).parts).intersection(('obj','bin','.godot','__pycache__'))}
def assemblies():
    paths = [p for base in ('src','tests') for p in (R/base).glob('*/bin/Debug/net*/OpenFantasy*.dll')]
    paths += list((R/'client/.godot/mono/temp/bin/Debug').glob('OpenFantasy*.dll'))
    return {str(p.relative_to(ROOT)):sha(p) for p in paths}
def preservation():
    pins=json.loads((P/'PRESERVATION.json').read_text())
    project='Beta/Runtime/tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj'
    for group in ('existingTests','frozenSpecs'):
        assert not [p for p,h in pins[group].items() if p!=project and (not (ROOT/p).is_file() or sha(ROOT/p)!=h)], group+' changed'
    baseline=(P/'Hosting.Tests.csproj.original').read_bytes()
    actual=(ROOT/project).read_bytes()
    old_names=set(json.loads((P/'CLIENT_BASELINE_PATHS.json').read_text()))
    links=[]
    for line in actual.splitlines(keepends=True):
        if line in baseline.splitlines(keepends=True): continue
        match=re.fullmatch(rb'    <Compile Include="../../client/scripts/([A-Za-z0-9_.]+\.cs)" Link="Client/\1" />\r?\n',line)
        assert match, 'Only additive new client source links permitted in test project'
        name=match.group(1).decode()
        assert name not in old_names and (R/'client/scripts'/name).is_file(), 'Link must name a new owned client source'
        assert line not in links, 'Duplicate source link'
        links.append(line)
    stripped=actual
    for line in links: stripped=stripped.replace(line,b'',1)
    assert stripped==baseline, 'Existing test project bytes changed'
    for name in ('CORE_PRESERVATION.json','PROTECTED_APPLICATION.json'):
        entries=json.loads((P/name).read_text())
        changed=[p for p,h in entries.items() if not (ROOT/p).is_file() or sha(ROOT/p)!=h]
        assert not changed,(name,changed)
    assert sha(pins['sourceDatabase'])==pins['sourceDatabaseSha256'], 'Source database changed'
    index=subprocess.check_output(['git','diff','--cached','--binary','--','.',':!Beta'],cwd=ROOT)
    assert hashlib.sha256(index).hexdigest()==pins['predecessorIndexSha256'], 'Predecessor index changed'
    assert subprocess.check_output(['git','rev-parse',pins['checkpoint']],cwd=ROOT,text=True).strip()==pins['sourceHead'], 'Checkpoint changed'
    return {'existingTests':len(pins['existingTests']),'frozenSpecs':len(pins['frozenSpecs']),'newCompileLinks':len(links),'protectedBytesPreserved':True}
def load_latest(kind):
    path=Path(json.loads((P/('.latest-'+kind+'.json')).read_text())['path'])
    data=json.loads(path.read_text())
    assert data['accepted'] and data['kind']==kind, 'Missing accepted '+kind
    return path,data
def main():
    assert len(sys.argv)==2 and sys.argv[1] in MODES
    kind=sys.argv[1]
    (P/'qualification').mkdir(parents=True,exist_ok=True)
    with Path('/tmp/openfantasy-retained-resource-integration.lock').open('w') as lock:
        fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
        out=P/'qualification'/(kind+'-'+time.strftime('%Y%m%dT%H%M%SZ',time.gmtime())+'-'+uuid.uuid4().hex[:5]);out.mkdir()
        report={'kind':kind,'sourceSha256':sources(),'commands':[],'limitSeconds':240 if kind=='build' else 600}
        start=time.monotonic()
        print('evidence: '+str(out),flush=True)
        env=dict(os.environ,DOTNET_ROOT=str(Path(DOTNET).parent),PATH=str(Path(DOTNET).parent)+os.pathsep+os.environ.get('PATH',''))
        for key in list(env):
            if key.startswith('OPENFANTASY_') and any(x in key for x in ('FIXTURES','RESULTS','EVIDENCE','SOURCE')): env.pop(key)
        def run(cmd):
            log=out/(str(len(report['commands']))+'.log')
            row={'command':cmd,'log':str(log)};report['commands'].append(row)
            with log.open('x') as stream:
                proc=subprocess.Popen(cmd,cwd=ROOT,env=env,stdout=stream,stderr=subprocess.STDOUT,start_new_session=True)
                try: proc.wait(timeout=max(1,report['limitSeconds']-(time.monotonic()-start)))
                except BaseException:
                    try: os.killpg(proc.pid,signal.SIGKILL)
                    except ProcessLookupError: pass
                    proc.wait();raise
            row['exitCode']=proc.returncode
            if proc.returncode: print('\n'.join(log.read_text(errors='replace').splitlines()[-60:]),flush=True)
            assert proc.returncode==0, 'Command failed; retained log '+str(log)
            return log
        def test(selection,label):
            run([DOTNET,'test',str(R/'tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj'),'--no-build','--no-restore','--filter',selection,'--logger','console;verbosity=detailed','--logger','trx;LogFileName='+label+'.trx','--results-directory',str(out)])
        try:
            report['preservationBefore']=preservation()
            if kind=='build':
                for project in (R/'OpenFantasy.sln',R/'client/OpenFantasy.Client.csproj'):
                    run([DOTNET,'build',str(project),'--no-restore','-m:1','-p:UseSharedCompilation=false','--verbosity','quiet'])
                log=run([GODOT,'--headless','--path',str(R/'client'),'--import'])
                assert 'ERROR:' not in log.read_text(errors='replace'), 'Godot import error'
            else:
                build_path,built=load_latest('build')
                assert built['sourceSha256']==report['sourceSha256'] and built['assemblySha256']==assemblies(), 'Source/binaries differ from accepted build'
                report['buildReport']=str(build_path);report['buildReportSha256']=sha(build_path)
                if kind=='unit': test('(FullyQualifiedName~Client.Trading|FullyQualifiedName~Content.Trading)&FullyQualifiedName!~FixtureTests&FullyQualifiedName!~ResultTests','unit')
                elif kind=='regression': test('(FullyQualifiedName~Simulation|FullyQualifiedName~Client|FullyQualifiedName~WorldExchange|FullyQualifiedName~MaterialProposal|FullyQualifiedName~Content)&FullyQualifiedName!~NativeTradingResultTests&FullyQualifiedName!~NativeGiftResultTests&FullyQualifiedName!~RetainedGiftResultTests&FullyQualifiedName!~NativeTradingBoundaryResultTests','regression')
                elif kind=='fixtures':
                    for label,selection,var in (('trading','NativeTradingFixtureTests','OPENFANTASY_NATIVE_TRADING_FIXTURES'),('gifts','NativeGiftFixtureTests','OPENFANTASY_NATIVE_GIFT_FIXTURES')):
                        env[var]=str(out/label);test('FullyQualifiedName~'+selection,label)
                        assert (out/label/'save.sqlite').is_file() and (out/label/'genesis.ofc').is_file(), 'Missing closed fixture export'
                        env.pop(var)
                    env['OPENFANTASY_NATIVE_TRADING_BOUNDARY_FIXTURES']=str(out/'boundaries')
                    test('FullyQualifiedName~NativeTradingBoundaryFixtureTests','boundaries')
                    assert (out/'boundaries/manifest.json').is_file(), 'Missing boundary fixture manifest'
                    report['boundaryFixtures']=str(out/'boundaries')
                    report['tradingFixtures']=str(out/'trading');report['giftFixtures']=str(out/'gifts')
                elif kind in ('native','legacy-native'):
                    fixture_path,fixtures=load_latest('fixtures')
                    assert fixtures['sourceSha256']==report['sourceSha256'], 'Fixtures built from stale source'
                    report['fixtureReport']=str(fixture_path);report['fixtureReportSha256']=sha(fixture_path)
                    native_report=out/'native.json'
                    legacy=kind=='legacy-native'
                    # The unchanged gift journal verifier requires all three route results.
                    run(['python3',str(R/'tools'/('check_native_gifts.py' if legacy else 'check_native_trading.py')),'--dotnet',DOTNET,'--godot',GODOT,'--fixtures',fixtures['giftFixtures' if legacy else 'tradingFixtures'],'--output',str(native_report),'--mode','all','--rendered'])
                    observed=json.loads(native_report.read_text());assert observed['status']=='passed', 'Native report failed'
                    report['nativeReport']=str(native_report);report['nativeReportSha256']=sha(native_report)
                    report['managedAuthenticationManifest']=observed['managedAuthenticationManifest']
                    if legacy:
                        env['OPENFANTASY_NATIVE_GIFT_RESULTS']=observed['managedAuthenticationManifest']
                        test('FullyQualifiedName~NativeGiftResultTests','legacy-auth')
                elif kind=='boundary-native':
                    fixture_path,fixtures=load_latest('fixtures')
                    assert fixtures['sourceSha256']==report['sourceSha256'], 'Boundary fixtures from stale source'
                    native_report=out/'native.json'
                    run(['python3',str(R/'tools/check_native_trading_boundaries.py'),'--dotnet',DOTNET,'--godot',GODOT,'--fixtures',fixtures['boundaryFixtures'],'--output',str(native_report),'--rendered'])
                    observed=json.loads(native_report.read_text());assert observed['status']=='passed', 'Boundary native report failed'
                    report['nativeReport']=str(native_report);report['nativeReportSha256']=sha(native_report)
                    report['managedAuthenticationManifest']=observed['managedAuthenticationManifest']
                elif kind=='boundary-auth':
                    native_path,native=load_latest('boundary-native')
                    assert native['sourceSha256']==report['sourceSha256'], 'Boundary native run from stale source'
                    assert sha(native['nativeReport'])==native['nativeReportSha256'], 'Boundary report changed'
                    env['OPENFANTASY_NATIVE_TRADING_BOUNDARY_RESULTS']=native['managedAuthenticationManifest']
                    report['nativeEvidence']=str(native_path);report['nativeEvidenceSha256']=sha(native_path)
                    test('FullyQualifiedName~NativeTradingBoundaryResultTests','boundary-auth')
                elif kind=='native-auth':
                    native_path,native=load_latest('native')
                    assert native['sourceSha256']==report['sourceSha256'], 'Native run from stale source'
                    assert sha(native['nativeReport'])==native['nativeReportSha256'], 'Native report changed'
                    env['OPENFANTASY_NATIVE_TRADING_RESULTS']=native['managedAuthenticationManifest']
                    report['nativeEvidence']=str(native_path);report['nativeEvidenceSha256']=sha(native_path)
                    test('FullyQualifiedName~NativeTradingResultTests','native-auth')
            rows=[row.attrib for trx in out.glob('*.trx') for row in ET.parse(trx).findall('.//'+NS+'UnitTestResult')]
            if kind not in ('build','native','boundary-native'):
                assert rows and all(row['outcome']=='Passed' for row in rows), 'Missing/failed/skipped tests'
                if kind=='unit': assert any('.Client.Trading.' in row['testName'] or '.Content.Trading.' in row['testName'] for row in rows), 'No new trading cases'
                report['outcomes']=dict(collections.Counter(row['outcome'] for row in rows));report['testNames']=[row['testName'] for row in rows]
            report['assemblySha256']=assemblies()
            if kind!='build': assert report['assemblySha256']==built['assemblySha256'], 'Binaries changed during checks'
            report['preservationAfter']=preservation()
        except Exception as error: report['error']=repr(error)
        report['elapsedSeconds']=round(time.monotonic()-start,3)
        report['sourceUnchanged']=sources()==report['sourceSha256']
        report['accepted']=not report.get('error') and report['sourceUnchanged']
        path=out/'report.json';path.write_text(json.dumps(report,indent=2)+'\n')
        if report['accepted']: (P/('.latest-'+kind+'.json')).write_text(json.dumps({'path':str(path)})+'\n')
        print(json.dumps({k:v for k,v in report.items() if k not in ('sourceSha256','assemblySha256','commands','testNames')},indent=2),flush=True)
        return 0 if report['accepted'] else 1
if __name__=='__main__': sys.exit(main())
