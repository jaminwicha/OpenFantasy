from pathlib import Path
import datetime,hashlib,json,subprocess,sys,xml.etree.ElementTree as ET
q=Path(__file__).resolve().parent;root=q.parents[4]
kind=sys.argv[1];assert kind in ('build','test')
label=kind+'-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
dotnet='/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet'
project='Beta/Runtime/OpenFantasy.sln' if kind=='build' else 'Beta/Runtime/tests/OpenFantasy.Core.Tests/OpenFantasy.Core.Tests.csproj'
command=[dotnet,kind,project,'--no-restore']
if kind=='build': command += ['-m:1','-p:UseSharedCompilation=false','--verbosity','quiet']
else: command += ['--no-build','--filter','FullyQualifiedName~Production|FullyQualifiedName~Authority|FullyQualifiedName~Custody','--logger','trx;LogFileName='+label+'.trx','--results-directory',str(q)]
pins={str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for folder in ('Beta/Runtime/src','Beta/Runtime/tests/OpenFantasy.Core.Tests') for p in (root/folder).rglob('*.cs') if 'bin' not in p.parts and 'obj' not in p.parts}
record={'command':command,'sourceSha256':pins,'limitSeconds':240 if kind=='build' else 420}
try:
 with (q/(label+'.log')).open('x') as log: result=subprocess.run(command,cwd=root,stdout=log,stderr=subprocess.STDOUT,timeout=record['limitSeconds'])
 record['exitCode']=result.returncode
 if kind=='test':
  rows=ET.parse(q/(label+'.trx')).findall('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult')
  outcomes=[r.attrib['outcome'] for r in rows];record['outcomes']={v:outcomes.count(v) for v in set(outcomes)}
  assert outcomes and all(v=='Passed' for v in outcomes),'Tests absent, skipped or failed.'
except Exception as error:record['error']=repr(error)
record['sourceUnchanged']=all(hashlib.sha256((root/p).read_bytes()).hexdigest()==v for p,v in pins.items())
(q/(label+'.json')).write_text(json.dumps(record,indent=2)+'\n')
print((q/(label+'.log')).read_text()[-5000:])
print({k:v for k,v in record.items() if k!='sourceSha256'})
sys.exit(0 if record.get('exitCode')==0 and not record.get('error') and record['sourceUnchanged'] else 1)
