"""Bounded affected checks; retain every label, command, outcome, source and execution pin."""
from pathlib import Path
import hashlib, json, os, subprocess, sys, time, xml.etree.ElementTree as ET
q=Path(__file__).resolve().parent
root=q.parents[4]
dotnet='/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet'
kind,label=sys.argv[1:3]
output=q/(label+'.json')
if output.exists(): raise SystemExit('Existing label cannot be overwritten.')
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
source={str(p.relative_to(root)):sha(p) for folder in ['src','client/scripts','tests'] for p in (root/'Beta/Runtime'/folder).rglob('*.cs') if not any(x in p.parts for x in ['bin','obj','.godot'])}
env=os.environ.copy()
if kind=='build':
 command=[dotnet,'build','Beta/Runtime/OpenFantasy.sln','--no-restore','-m:1','-p:UseSharedCompilation=false','--verbosity','quiet']
else:
 if kind=='core':
  project='OpenFantasy.Core.Tests';selection='FullyQualifiedName~Progression|FullyQualifiedName~Care|FullyQualifiedName~WorldCodecDispatchTests'
  env['OPENFANTASY_RESOURCE_HISTORY_FIXTURES']=str(q/('resource-history-'+label))
 elif kind=='hosting':
  project='OpenFantasy.Hosting.Tests';selection='FullyQualifiedName~WorldResourceJourneyTests|FullyQualifiedName~CoordinatedNativeSelectionTests'
 else: raise SystemExit('Unknown check.')
 command=[dotnet,'test','Beta/Runtime/tests/'+project+'/'+project+'.csproj','--no-build','--no-restore','--filter',selection,'--logger','trx;LogFileName='+label+'.trx','--results-directory',str(q)]
r={'kind':kind,'command':command,'sourceSha256':source,'boundSeconds':600,'environment':{k:v for k,v in env.items() if k.startswith('OPENFANTASY_')}};start=time.monotonic()
try:
 with (q/(label+'.log')).open('x') as log:
  result=subprocess.run(command,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT,timeout=600)
 r['exitCode']=result.returncode
 if kind!='build':
  rows=ET.parse(q/(label+'.trx')).findall('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult')
  outcomes=[x.attrib['outcome'] for x in rows];r['outcomes']={x:outcomes.count(x) for x in sorted(set(outcomes))}
  if not outcomes or any(x!='Passed' for x in outcomes):r['qualificationError']='Required substantive checks did not all pass.'
 if kind=='core' and r.get('exitCode')==0:
  old=root/'Beta/implementation/p02/resource-history/qualification/fixtures';new=q/('resource-history-'+label)
  r['independentOriginalBytes']={name:{'sha256':sha(old/name),'matches':(old/name).read_bytes()==(new/name).read_bytes()} for name in ['basis.bin','current.bin','history.bin']}
  if not all(x['matches'] for x in r['independentOriginalBytes'].values()):r['qualificationError']='Original resource oracle bytes changed.'
except Exception as error:r['error']=repr(error)
finally:
 r['elapsedSeconds']=time.monotonic()-start;r['sourcesUnchanged']=all(sha(root/name)==value for name,value in source.items())
 r['executionSha256']={str(p.relative_to(root)):sha(p) for p in (root/'Beta/Runtime').rglob('OpenFantasy.*.dll') if any(x in p.parts for x in ['bin','temp'])}
 output.write_text(json.dumps(r,indent=2)+'\n');print((q/(label+'.log')).read_text()[-1500:]);print({k:v for k,v in r.items() if k in ['exitCode','elapsedSeconds','sourcesUnchanged','error','qualificationError','outcomes','independentOriginalBytes']})
raise SystemExit(0 if r.get('exitCode')==0 and r['sourcesUnchanged'] and not r.get('error') and not r.get('qualificationError') else 1)
