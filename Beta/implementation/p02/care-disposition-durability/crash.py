"""Run process interruption with pinned build/fixtures and the shared heavy-check lock."""
import datetime,fcntl,importlib.util,json,subprocess,sys,time
from pathlib import Path
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('check',P/'check.py');check=importlib.util.module_from_spec(spec);spec.loader.exec_module(check)
with Path('/tmp/openfantasy-retained-resource-integration.lock').open('w') as lock:
 fcntl.flock(lock,fcntl.LOCK_EX)
 out=P/'qualification'/('crash-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir(parents=True)
 source=check.sources();binaries=check.base.assemblies();report={'source':source,'binaries':binaries,'accepted':False};started=time.monotonic()
 try:
  check.preserve();build=Path(json.loads((P/'.latest-build.json').read_text())['path']);report['buildReport']=str(build);data=json.loads(build.read_text())
  assert data['accepted'] and data['source']==source and data['binaries']==binaries
  command=['python3',str(check.base.R/'tools/check_care_disposition_durability.py'),'--dotnet',check.base.DOTNET,'--fixtures',str((P/'crash-fixtures').resolve()),'--output',str(out/'crash.json')];report['command']=command
  with (out/'command.log').open('w') as log:
   result=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT,timeout=615)
  raw=json.loads((out/'crash.json').read_text());report['crashReport']=str(out/'crash.json');report['status']=raw['status'];report['accepted']=result.returncode==0 and raw['status']=='passed'
 except Exception as error:report['error']=repr(error)
 finally:
  report['sourceUnchanged']=source==check.sources();report['binariesUnchanged']=binaries==check.base.assemblies();report['accepted']=report['accepted'] and report['sourceUnchanged'] and report['binariesUnchanged'];check.preserve();report['seconds']=time.monotonic()-started
  (out/'report.json').write_text(json.dumps(report,indent=2)+'\n');(P/'.latest-crash.json').write_text(json.dumps({'path':str(out/'report.json')})+'\n')
 print(json.dumps({k:v for k,v in report.items() if k not in ('source','binaries')},indent=2))
 if not report['accepted'] and (out/'command.log').exists():print((out/'command.log').read_text()[-4500:])
 sys.exit(0 if report['accepted'] else 1)
