"""Authenticate existing native journals with current binaries, preserving the original artifacts."""
import hashlib,json,shutil
from pathlib import Path

def verify(rows):
 for row in rows.values():
  for name,expected in row['fileSha256'].items():
   assert hashlib.sha256(Path(name).read_bytes()).hexdigest()==expected,name

def prepare(packet,out,env):
 rows=json.loads((packet/'RETAINED_NATIVE.json').read_text());verify(rows)
 names={'cargo':'CARGO','trading':'TRADING','gifts':'GIFT','retreat':'RETREAT','road':'ROAD_THREAT','boundary':'TRADING_BOUNDARY'}
 for label,row in rows.items():
  target=out/label;target.mkdir()
  def relocate(value):
   if isinstance(value,dict):return {k:relocate(v) for k,v in value.items()}
   if isinstance(value,list):return [relocate(v) for v in value]
   if isinstance(value,str) and value in row['fileSha256'] and Path(value).suffix in ('.sqlite','.sqlite3','.db'):
    assert not Path(value+'-wal').exists() and not Path(value+'-shm').exists(),'Original journal must be closed'
    destination=target/(hashlib.sha256(value.encode()).hexdigest()[:12]+Path(value).suffix)
    if not destination.exists():shutil.copy2(value,destination)
    assert hashlib.sha256(destination.read_bytes()).hexdigest()==row['fileSha256'][value]
    return str(destination)
   return value
  manifest=target/'manifest.json';manifest.write_text(json.dumps(relocate(json.loads(Path(row['manifest']).read_text())),indent=2)+'\n')
  env['OPENFANTASY_NATIVE_'+names[label]+'_RESULTS']=str(manifest)
 return rows
