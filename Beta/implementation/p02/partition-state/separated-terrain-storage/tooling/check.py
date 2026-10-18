import subprocess,os,time,signal,json,sys
from pathlib import Path
root=Path(__file__).resolve().parents[6]
out=Path(sys.argv[1]).resolve();out.mkdir(parents=True,exist_ok=False)
name=sys.argv[2]
preflight=Path(sys.argv[5]).resolve()
import hashlib
pins=json.loads((preflight/'pins.json').read_text())
assert all(hashlib.sha256((root/path).read_bytes()).hexdigest()==digest for path,digest in pins.items()), 'Source/binary pins changed'
filters=json.loads((preflight/'filters.json').read_text())
assert sys.argv[4] in filters.values(), 'Filter was not preflighted'
family=next(k for k,v in filters.items() if v==sys.argv[4])
assert json.loads((preflight/(family+'-cases.json')).read_text()), 'Zero cases discovered'

cmd=['/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet','test',sys.argv[3],'--no-build','--no-restore','--filter',sys.argv[4],'--logger',f'trx;LogFileName={name}.trx','--results-directory',str(out)]
start=time.monotonic()
with (out/f'{name}.log').open('w') as log:
 p=subprocess.Popen(cmd,cwd=root/'Beta/Runtime',stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
 try: code=p.wait(timeout=180)
 except subprocess.TimeoutExpired:
  os.killpg(p.pid,signal.SIGKILL);p.wait();code=124
result=dict(command=cmd,exit_code=code,seconds=time.monotonic()-start,bound_seconds=180)
(out/f'{name}.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(result));print((out/f'{name}.log').read_text()[-6500:]);sys.exit(code)
