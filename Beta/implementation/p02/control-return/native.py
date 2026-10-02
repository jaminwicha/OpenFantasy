"""Serialize native import/rendering against the same source-pinned build as managed qualification."""
import datetime,fcntl,importlib.util,json,os,subprocess,sys,time
from pathlib import Path
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('check',P/'check.py')
check=importlib.util.module_from_spec(spec);spec.loader.exec_module(check)
base=check.base
GODOT='/Users/alucinor/.local/share/openfantasy/toolchains/godot-4.6/Godot_mono.app/Contents/MacOS/Godot'


def main():
 mode=sys.argv[2] if len(sys.argv)>2 else 'native'
 assert mode in ('native','native-original')
 with Path('/tmp/openfantasy-retained-resource-integration.lock').open('w') as lock:
  fcntl.flock(lock,fcntl.LOCK_EX)
  out=P/'qualification'/(mode+'-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'))
  out.mkdir(parents=True)
  source=check.sources();binaries=base.assemblies();report={'source':source,'binaries':binaries,'accepted':False}
  build=Path(json.loads((P/'.latest-build.json').read_text())['path']);report['buildReport']=str(build)
  started=time.monotonic()
  try:
   check.preserve();data=json.loads(build.read_text())
   assert data['accepted'] and data['source']==source and data['binaries']==binaries
   sdk=str(Path(base.DOTNET).parent);env=os.environ|{'DOTNET_ROOT':sdk,'PATH':sdk+os.pathsep+os.environ.get('PATH','')}
   # Retain import completion and strict error/deadline checks; allow editor initialization to settle.
   report['importCommand']=[GODOT,'--headless','--path',str(base.R/'client'),'--import','--quit-after','120']
   report['importLimitSeconds']=180
   with (out/'import.log').open('w') as log:
    result=subprocess.run(report['importCommand'],cwd=base.R,env=env,stdout=log,stderr=subprocess.STDOUT,timeout=180)
   assert result.returncode==0 and 'ERROR:' not in (out/'import.log').read_text(),'Godot import failed'
   command=['python3',str(base.R/'tools/check_native_care_dispositions.py'),'--dotnet',base.DOTNET,'--godot',GODOT,'--fixtures',sys.argv[1],'--output',str(out/'native.json')]
   report['command']=command
   with (out/'command.log').open('w') as log:
    result=subprocess.run(command,cwd=base.R,env=env,stdout=log,stderr=subprocess.STDOUT,timeout=915)
   native=json.loads((out/'native.json').read_text());report['nativeReport']=str(out/'native.json')
   report['nativeStatus']=native['status'];report['manifest']=native.get('managedAuthenticationManifest')
   report['accepted']=result.returncode==0 and native['status']=='passed'
  except Exception as error:report['error']=repr(error)
  finally:
   report['sourceUnchanged']=source==check.sources();report['binariesUnchanged']=binaries==base.assemblies()
   report['accepted']=report['accepted'] and report['sourceUnchanged'] and report['binariesUnchanged']
   check.preserve();report['seconds']=time.monotonic()-started
   (out/'report.json').write_text(json.dumps(report,indent=2)+'\n')
   (P/f'.latest-{mode}.json').write_text(json.dumps({'path':str(out/'report.json')})+'\n')
  print(json.dumps({key:value for key,value in report.items() if key not in ('source','binaries')},indent=2))
  return 0 if report['accepted'] else 1


if __name__=='__main__':sys.exit(main())
