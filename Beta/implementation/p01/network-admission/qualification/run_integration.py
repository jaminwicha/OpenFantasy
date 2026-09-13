"""Retained bounded integration campaign for admitted watershed networks."""
import datetime, hashlib, json, os, subprocess, sys, time, xml.etree.ElementTree as ET
from pathlib import Path
root = Path(__file__).resolve().parents[4] / 'Runtime'
out = Path(__file__).resolve().parent / 'integration'
out.mkdir(exist_ok=False)
dotnet = Path('/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet')
godot = Path('/Users/alucinor/.local/share/openfantasy/toolchains/godot-4.6/Godot_mono.app/Contents/MacOS/Godot')
env = dict(os.environ, DOTNET_ROOT=str(dotnet.parent), PATH=str(dotnet.parent)+os.pathsep+os.environ.get('PATH',''),
           OPENFANTASY_WATERSHED_NETWORK_WORLD_EVIDENCE=str(out/'operator-fixture'))
exclude = {'.godot','bin','obj','artifacts','TestResults','__pycache__'}
inputs = {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(root.rglob('*'))
          if p.is_file() and not any(x in exclude for x in p.relative_to(root).parts)}
report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
              scope='Bounded network admission: managed build/regression, host inspection, real process-kill recovery and native foundation/farm',
              inputSha256=inputs, steps=[])
def run(name, command, timeout=180):
    start=time.monotonic()
    step=dict(name=name, command=[str(x) for x in command], timeoutSeconds=timeout)
    report['steps'].append(step)
    try:
        result=subprocess.run(step['command'], cwd=root, env=env, capture_output=True, text=True, timeout=timeout)
        step.update(exitCode=result.returncode, stdout=result.stdout, stderr=result.stderr)
        if result.returncode or 'ERROR:' in result.stdout+result.stderr:
            raise RuntimeError(name+' failed')
    except subprocess.TimeoutExpired as e:
        retained=lambda v: v.decode(errors='replace') if isinstance(v,bytes) else v or ''
        step.update(exitCode=None, timedOut=True, stdout=retained(e.stdout), stderr=retained(e.stderr))
        raise
    finally:
        step['elapsedSeconds']=time.monotonic()-start
    print(name+': passed', flush=True)
    return result.stdout
try:
    build=run('managed-build',[dotnet,'build','OpenFantasy.sln','--no-restore','-c','Debug'])
    assert '0 Warning(s)' in build and '0 Error(s)' in build
    for project, name in [('OpenFantasy.Core.Tests','core'),('OpenFantasy.Hosting.Tests','hosting')]:
        run(name+'-tests',[dotnet,'test','tests/'+project+'/'+project+'.csproj','--no-build','--no-restore',
                          '--logger','trx;LogFileName='+name+'.trx','--results-directory',out/'tests'])
        counters=ET.parse(out/'tests'/f'{name}.trx').getroot().find('.//{*}Counters').attrib
        assert counters['total']==counters['passed']==counters['executed'] and counters['notExecuted']=='0'
        report[name+'Tests']=counters
    run('host-inspections',[sys.executable,root/'tools/check_contract_inspections.py','--dotnet',dotnet,'--output',out/'host-inspections.json'],60)
    run('network-world',[sys.executable,root/'tools/check_watershed_network_world.py','--dotnet',dotnet,
                         '--fixture',out/'operator-fixture','--output',out/'network-world.json'])
    host=run('host-foundation',[dotnet,root/'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll','--inspect-foundation'],60)
    native=run('native-foundation',[godot,'--headless','--path',root/'client','--','--smoke-test'],60)
    def projection(body):
        rows=[json.loads(line) for line in body.splitlines() if line.startswith('{"Contract":')]
        assert len(rows)==1 and rows[0]['Runtime']
        return rows[0]
    h,n=projection(host),projection(native)
    assert all(h[k]==n[k] for k in ('Contract','Entity','Sample','Day','Millisecond'))
    assert (h['Day'],h['Millisecond'])==(0,0)
    run('native-farm',[sys.executable,root/'tools/check_native_farm.py','--dotnet',dotnet,'--godot',godot,'--output',out/'native-farm.json'])
    assert all(hashlib.sha256((root/p).read_bytes()).hexdigest()==h for p,h in inputs.items()), 'runtime inputs changed during integration'
    report['status']='passed'
except Exception as e:
    report['status']='failed'
    report['failure']=repr(e)
finally:
    report['finishedUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
    (out/'verification.json').write_text(json.dumps(report,indent=2)+'\n')
print('integration: '+report['status'],flush=True)
sys.exit(0 if report['status']=='passed' else 1)
