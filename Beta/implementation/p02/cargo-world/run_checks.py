"""Lead-owned, bounded checks for the additive cargo-world assignment; no provider calls."""
import collections, fcntl, hashlib, json, os, signal, subprocess, sys, time, uuid
from pathlib import Path
import xml.etree.ElementTree as ET
P=Path(__file__).resolve().parent
ROOT=P.parents[3]
R=ROOT/'Beta/Runtime'
DOTNET='/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet'
MODES=('build','unit','journey','regression')
NS='{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}'
def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def sources():
    return {str(p.relative_to(ROOT)):sha(p) for base in ('src','tests','client','content','tools') for p in (R/base).rglob('*')
            if p.is_file() and p.suffix in ('.cs','.csproj','.props','.targets','.json','.tscn','.tres','.svg','.godot','.py')
            and not set(p.relative_to(R).parts).intersection(('obj','bin','.godot','__pycache__'))}
def assemblies():
    return {str(p.relative_to(ROOT)):sha(p) for base in ('src','tests') for p in (R/base).glob('*/bin/Debug/net*/OpenFantasy*.dll')}
def preservation():
    pins=json.loads((P/'PRESERVATION.json').read_text())
    protected=json.loads((P/'PROTECTED_RUNTIME.json').read_text())
    for label, entries in [('runtime',protected),('tests',pins['existingTests']),('specs',pins['frozenSpecs'])]:
        changed=[p for p,h in entries.items() if not (ROOT/p).is_file() or sha(ROOT/p)!=h]
        assert not changed,(label,changed)
    assert sha(pins['sourceDatabase'])==pins['sourceDatabaseSha256'],'Original source DB changed'
    index=subprocess.check_output(['git','diff','--cached','--binary','--','.',':!Beta'],cwd=ROOT)
    assert hashlib.sha256(index).hexdigest()==pins['predecessorIndexSha256'],'Predecessor index changed'
    assert subprocess.check_output(['git','rev-parse',pins['checkpoint']],cwd=ROOT,text=True).strip()==pins['sourceHead']
    return {'existingRuntimeFiles':len(protected),'existingTests':len(pins['existingTests']),'frozenSpecs':len(pins['frozenSpecs']),'protectedBytesPreserved':True}
def latest(kind):
    p=Path(json.loads((P/('.latest-'+kind+'.json')).read_text())['path']);d=json.loads(p.read_text())
    assert d['accepted'] and d['kind']==kind,'Missing accepted '+kind
    return p,d
def main():
    assert len(sys.argv)==2 and sys.argv[1] in MODES
    kind=sys.argv[1]
    (P/'qualification').mkdir(parents=True,exist_ok=True)
    with Path('/tmp/openfantasy-retained-resource-integration.lock').open('w') as lock:
        fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
        out=P/'qualification'/(kind+'-'+time.strftime('%Y%m%dT%H%M%SZ',time.gmtime())+'-'+uuid.uuid4().hex[:5]);out.mkdir()
        report={'kind':kind,'sourceSha256':sources(),'commands':[],'limitSeconds':240 if kind=='build' else 600}
        start=time.monotonic();print('evidence: '+str(out),flush=True)
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
            if proc.returncode:print('\n'.join(log.read_text(errors='replace').splitlines()[-55:]),flush=True)
            assert proc.returncode==0,'Command failed; retained log '+str(log)
        try:
            report['preservationBefore']=preservation()
            if kind=='build':run([DOTNET,'build',str(R/'OpenFantasy.sln'),'--no-restore','-m:1','-p:UseSharedCompilation=false','--verbosity','quiet'])
            else:
                build_path,built=latest('build')
                assert built['sourceSha256']==report['sourceSha256'] and built['assemblySha256']==assemblies(),'Source/binaries differ from accepted build'
                report.update(buildReport=str(build_path),buildReportSha256=sha(build_path))
                if kind=='unit':selection='FullyQualifiedName~Content.Cargo&FullyQualifiedName!~CargoCircuitJourneyTests&FullyQualifiedName!~CargoCircuitEvidenceTests'
                elif kind=='journey':
                    selection='FullyQualifiedName~CargoCircuitJourneyTests'
                    evidence=out/'journey';evidence.mkdir();env['OPENFANTASY_CARGO_CIRCUIT_EVIDENCE']=str(evidence);report['journeyEvidence']=str(evidence)
                else:
                    selection='(FullyQualifiedName~Simulation|FullyQualifiedName~Client|FullyQualifiedName~WorldExchange|FullyQualifiedName~MaterialProposal|FullyQualifiedName~Content)&FullyQualifiedName!~NativeTradingResultTests&FullyQualifiedName!~NativeGiftResultTests&FullyQualifiedName!~RetainedGiftResultTests&FullyQualifiedName!~NativeTradingBoundaryResultTests&FullyQualifiedName!~CargoCircuitJourneyTests&FullyQualifiedName!~CargoCircuitEvidenceTests'
                run([DOTNET,'test',str(R/'tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj'),'--no-build','--no-restore','--filter',selection,'--logger','console;verbosity=detailed','--logger','trx;LogFileName='+kind+'.trx','--results-directory',str(out)])
                rows=[row.attrib for trx in out.glob('*.trx') for row in ET.parse(trx).findall('.//'+NS+'UnitTestResult')]
                assert rows and all(row['outcome']=='Passed' for row in rows),'Missing/failed/skipped tests'
                if kind in ('unit','journey'):assert all('.Content.Cargo.' in row['testName'] for row in rows),'Wrong focused cases'
                report['outcomes']=dict(collections.Counter(row['outcome'] for row in rows));report['testNames']=[row['testName'] for row in rows]
                if kind=='journey':
                    artifacts=[p for p in evidence.rglob('*') if p.is_file()]
                    assert artifacts and any(p.suffix in ('.sqlite','.sqlite3') for p in artifacts),'Missing durable journey exports'
                    report['artifactSha256']={str(p.relative_to(evidence)):sha(p) for p in artifacts}
            report['assemblySha256']=assemblies()
            if kind!='build':assert report['assemblySha256']==built['assemblySha256'],'Binaries changed during checks'
            report['preservationAfter']=preservation()
        except Exception as error:report['error']=repr(error)
        report['elapsedSeconds']=round(time.monotonic()-start,3)
        report['sourceUnchanged']=sources()==report['sourceSha256']
        report['accepted']=not report.get('error') and report['sourceUnchanged'] and report['elapsedSeconds']<report['limitSeconds']
        path=out/'report.json';path.write_text(json.dumps(report,indent=2)+'\n')
        if report['accepted']:(P/('.latest-'+kind+'.json')).write_text(json.dumps({'path':str(path)})+'\n')
        print(json.dumps({k:v for k,v in report.items() if k not in ('sourceSha256','assemblySha256','commands','testNames','artifactSha256')},indent=2),flush=True)
        return 0 if report['accepted'] else 1
if __name__=='__main__':sys.exit(main())
