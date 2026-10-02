"""Authenticate the exact previous four-native-player database through the new assemblies."""
import datetime,fcntl,hashlib,importlib.util,json,os,shutil,signal,sqlite3,subprocess,sys,time,tarfile
from pathlib import Path
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('checks',P/'check.py');checks=importlib.util.module_from_spec(spec);spec.loader.exec_module(checks)
base=checks.base
with Path('/tmp/openfantasy-retained-resource-integration.lock').open('w') as lock:
 fcntl.flock(lock,fcntl.LOCK_EX)
 started=time.monotonic();out=P/'qualification'/('retained-native-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir()
 report={'limitSeconds':600,'scope':'Exact retained profile35 four-native-player history; no fresh native run'}
 try:
  checks.preserve();source=checks.sources();bpath=Path(json.loads((P/'.latest-build.json').read_text())['path']);build=json.loads(bpath.read_text())
  assert build['accepted'] and source==build['source'] and base.assemblies()==build['binaries']
  report['buildReport']=str(bpath)
  pins=json.loads((P/'PRESERVATION.json').read_text());manifestPath='Beta/implementation/p02/native-player-roster/EVIDENCE_ARCHIVES.json'
  raw=subprocess.check_output(['git','show',pins['sourceHead']+':'+manifestPath],cwd=checks.ROOT)
  assert raw==(checks.ROOT/manifestPath).read_bytes()
  archive=next(a for a in json.loads(raw)['archives'] if '132644829951Z' in a['archive'])
  archivePath=P.parent/'native-player-roster'/archive['archive']
  assert base.sha(archivePath)==archive['sha256']
  with tarfile.open(archivePath,'r:gz') as saved:
   selected={name:saved.extractfile(name).read() for name in ['roster/native-roster.sqlite3','roster/genesis.ofc','roster/journey.json']}
  for name,contents in selected.items():assert hashlib.sha256(contents).hexdigest()==archive['files'][name],name
  copy=out/'retained-world.sqlite3';copy.write_bytes(selected['roster/native-roster.sqlite3'])
  connection=sqlite3.connect('file:'+str(copy)+'?mode=ro',uri=True)
  row=connection.execute('select sequence,hash from world_head where singleton=1').fetchone();connection.close()
  revision=int.from_bytes(row[0],'big');expectedHash=row[1].hex()
  command=[base.DOTNET,str(base.R/'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll'),'--inspect-recorded-world',str(copy),str(revision)]
  with (out/'command.log').open('w') as log:
   process=subprocess.Popen(command,cwd=base.R,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
   try:process.wait(timeout=600)
   except subprocess.TimeoutExpired:os.killpg(process.pid,signal.SIGKILL);process.wait();raise
  assert process.returncode==0,(out/'command.log').read_text()[-2000:]
  view=json.loads((out/'command.log').read_text())
  assert view['ProfileVersion']==35 and view['RecordedRevision']==revision and view['SourceHeadRevision']==revision
  assert view['StateHash']==expectedHash and view['SourceHeadHash']==expectedHash
  assert base.sha(archivePath)==archive['sha256']
  assert checks.sources()==source and base.assemblies()==build['binaries'];checks.preserve()
  report.update(accepted=True,sourceRevision=pins['sourceHead'],originalDatabaseSha256=archive['files']['roster/native-roster.sqlite3'],profile=35,revision=revision,stateHash=expectedHash)
 except Exception as error:report.update(accepted=False,error=repr(error))
 report['seconds']=time.monotonic()-started;report['accepted']=report['accepted'] and report['seconds']<600
 (out/'report.json').write_text(json.dumps(report,indent=2)+'\n');(P/'.latest-retained-native.json').write_text(json.dumps({'path':str(out/'report.json')})+'\n')
 print(json.dumps(report,indent=2));print('evidence:',out)
 sys.exit(0 if report['accepted'] else 1)
