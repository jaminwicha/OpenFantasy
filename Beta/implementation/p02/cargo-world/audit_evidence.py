"""Lead-owned independent authentication of a completed cargo circuit; copies saves before opening."""
import collections, datetime, fcntl, importlib.util, json, os, pathlib, signal, subprocess, sys, time, uuid
import xml.etree.ElementTree as ET
P=pathlib.Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('checks',P/'run_checks.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
with pathlib.Path('/tmp/openfantasy-retained-resource-integration.lock').open('w') as lock:
    fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
    out=P/'qualification'/('saved-audit-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')+'-'+uuid.uuid4().hex[:5]);out.mkdir()
    print('evidence: '+str(out),flush=True)
    report={'kind':'saved-audit','sourceSha256':m.sources(),'assemblySha256':m.assemblies(),'limitSeconds':600};start=time.monotonic()
    try:
        report['preservationBefore']=m.preservation()
        bp,b=m.latest('build');assert b['sourceSha256']==m.sources() and b['assemblySha256']==m.assemblies(),'Build is stale'
        jp,j=m.latest('journey');evidence=pathlib.Path(j['journeyEvidence'])
        assert j['sourceSha256']==report['sourceSha256'] and j['assemblySha256']==report['assemblySha256'],'Journey source/binaries differ from audit build'
        assert all(m.sha(evidence/path)==h for path,h in j['artifactSha256'].items()),'Journey artifacts changed'
        report.update(buildReport=str(bp),buildReportSha256=m.sha(bp),journeyReport=str(jp),journeyReportSha256=m.sha(jp),journeyArtifactSha256=j['artifactSha256'])
        env=dict(os.environ,DOTNET_ROOT=str(pathlib.Path(m.DOTNET).parent),PATH=str(pathlib.Path(m.DOTNET).parent)+os.pathsep+os.environ.get('PATH',''))
        for key in list(env):
            if key.startswith('OPENFANTASY_') and any(x in key for x in ('FIXTURES','RESULTS','EVIDENCE','SOURCE')):env.pop(key)
        env['OPENFANTASY_CARGO_CIRCUIT_RESULTS']=str(evidence)
        cmd=[m.DOTNET,'test',str(m.R/'tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj'),'--no-build','--no-restore','--filter','FullyQualifiedName~CargoCircuitEvidenceTests','--logger','console;verbosity=detailed','--logger','trx;LogFileName=audit.trx','--results-directory',str(out)]
        report['command']=cmd
        with (out/'0.log').open('w') as log:
            proc=subprocess.Popen(cmd,cwd=m.ROOT,env=env,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
            try:proc.wait(timeout=600-(time.monotonic()-start))
            except BaseException:
                os.killpg(proc.pid,signal.SIGKILL);proc.wait();raise
        report['exitCode']=proc.returncode
        assert proc.returncode==0,'Evidence authentication failed'
        rows=[row.attrib for row in ET.parse(out/'audit.trx').findall('.//'+m.NS+'UnitTestResult')]
        assert len(rows)==1 and all(r['outcome']=='Passed' for r in rows),'Missing/failed/skipped authentication'
        report['outcomes']=dict(collections.Counter(r['outcome'] for r in rows));report['testNames']=[r['testName'] for r in rows]
        assert all(m.sha(evidence/path)==h for path,h in j['artifactSha256'].items()),'Original evidence mutated'
        report['preservationAfter']=m.preservation()
    except Exception as error:report['error']=repr(error)
    report['elapsedSeconds']=round(time.monotonic()-start,3);report['sourceUnchanged']=report['sourceSha256']==m.sources();report['binariesUnchanged']=report['assemblySha256']==m.assemblies()
    report['accepted']=not report.get('error') and report['sourceUnchanged'] and report['binariesUnchanged'] and report['elapsedSeconds']<600
    path=out/'report.json';path.write_text(json.dumps(report,indent=2)+'\n')
    if report['accepted']:(P/'.latest-saved-audit.json').write_text(json.dumps({'path':str(path)})+'\n')
    else:print('\n'.join((out/'0.log').read_text(errors='replace').splitlines()[-40:]),flush=True)
    print(json.dumps({k:v for k,v in report.items() if k not in ('sourceSha256','assemblySha256','journeyArtifactSha256','command','testNames')},indent=2),flush=True)
    sys.exit(0 if report['accepted'] else 1)
