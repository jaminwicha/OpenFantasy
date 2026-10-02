"""Explicitly copy-activate the retained native save; preserve original fixtures and canonical rows."""
import fcntl,importlib.util,json,os,shutil,sqlite3,subprocess,sys,time
from pathlib import Path
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('check',P/'check.py')
check=importlib.util.module_from_spec(spec);spec.loader.exec_module(check)
with Path('/tmp/openfantasy-retained-resource-integration.lock').open('w') as lock:
 fcntl.flock(lock,fcntl.LOCK_EX)
 source=check.sources();binaries=check.base.assemblies();check.preserve()
 build=Path(json.loads((P/'.latest-build.json').read_text())['path'])
 data=json.loads(build.read_text());assert data['accepted'] and data['source']==source and data['binaries']==binaries
 original=P.parent/'control-return/native-fixtures';output=P/'native-fixtures';output.mkdir()
 pins={str(path.relative_to(check.ROOT)):check.base.sha(path) for path in original.iterdir() if path.is_file()}
 for path in original.glob('*.ofc'):shutil.copy2(path,output/path.name)
 shutil.copy2(original/'manifest.json',output/'original-manifest.json')
 command=[check.base.DOTNET,str(check.base.R/'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll'),
  '--enable-controller-recovery',str(original/'pending-retreat.sqlite'),str(output/'pending-retreat.sqlite')]
 started=time.monotonic();report={'source':source,'binaries':binaries,'buildReport':str(build),'command':command,'accepted':False,'originalPins':pins}
 try:
  result=subprocess.run(command,capture_output=True,text=True,timeout=60)
  (output/'activation.stdout.json').write_text(result.stdout);(output/'activation.stderr.log').write_text(result.stderr)
  assert result.returncode==0 and json.loads(result.stdout)['SupportsControllerRecovery']
  with sqlite3.connect('file:'+str(original/'pending-retreat.sqlite')+'?mode=ro',uri=True) as old,sqlite3.connect('file:'+str(output/'pending-retreat.sqlite')+'?mode=ro',uri=True) as new:
   tables=[name for (name,) in old.execute("SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'world_%' ORDER BY name")]
   assert tables==['world_checkpoints','world_command_outcomes','world_commits','world_genesis',
    'world_head','world_material_care_segments','world_occupied_identity']
   for name in tables:assert list(old.execute('SELECT * FROM '+name))==list(new.execute('SELECT * FROM '+name)),name
   assert new.execute('PRAGMA user_version').fetchone()[0]==10
   report['identicalCanonicalTables']=tables
  report['accepted']=True
 except Exception as error:report['error']=repr(error)
 finally:
  report['seconds']=time.monotonic()-started
  report['originalUnchanged']=all(check.base.sha(check.ROOT/path)==digest for path,digest in pins.items())
  report['sourceUnchanged']=source==check.sources();report['binariesUnchanged']=binaries==check.base.assemblies()
  report['accepted']=report['accepted'] and report['seconds']<60 and report['originalUnchanged'] and report['sourceUnchanged'] and report['binariesUnchanged']
  check.preserve();(output/'migration.json').write_text(json.dumps(report,indent=2)+'\n')
 print(json.dumps({key:value for key,value in report.items() if key not in ('source','binaries')},indent=2))
 sys.exit(0 if report['accepted'] else 1)
