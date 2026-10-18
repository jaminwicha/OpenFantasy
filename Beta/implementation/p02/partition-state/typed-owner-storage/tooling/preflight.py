from pathlib import Path
import subprocess,os,time,signal,json,hashlib,sys
root=Path(__file__).resolve().parents[6]; runtime=root/'Beta/Runtime'
out=Path(sys.argv[1]).resolve();out.mkdir(parents=True,exist_ok=False)
dotnet='/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet'
filters={'core':'|'.join('FullyQualifiedName~'+n for n in ['OpenFantasy.Core.Tests.Construction.','OpenFantasy.Core.Tests.Spatial.','OpenFantasy.Core.Tests.Authority.','OpenFantasy.Core.Tests.Custody.','WorldResidentExposureBasisTests','WorldCodecDispatchTests','SnapshotCodecTests','WorldIdentity']), 'hosting':'|'.join('FullyQualifiedName~'+n for n in ['SeparatedTerrainStorageTests','ConstructionPublicationTests','ConstructionLocalityCoverageTests','ConstructionUpgradeTests','ConstructionWorkRecoveryTests','WorldAuthorityServiceTests','WorldDatabaseTests','WorldCanonicalFrameTests','ExcavationRecoveryTests','PhysicalConstructionWorldTests','PartyWithdrawalWorldTests','PartyWithdrawalTransitionTests'])}
if len(sys.argv) > 2:
 filters=json.loads(Path(sys.argv[2]).read_text())
 assert set(filters)=={'core','hosting'} and all(isinstance(value,str) and value.strip() for value in filters.values()), 'Two explicit nonempty test filters required'
def run(name,cmd):
 start=time.monotonic()
 with (out/(name+'.log')).open('w') as log:
  p=subprocess.Popen(cmd,cwd=runtime,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
  try:code=p.wait(timeout=180)
  except subprocess.TimeoutExpired:os.killpg(p.pid,signal.SIGKILL);p.wait();code=124
 result=dict(command=cmd,exit_code=code,seconds=time.monotonic()-start,bound_seconds=180)
 (out/(name+'.json')).write_text(json.dumps(result,indent=2)+'\n')
 if code:raise RuntimeError(result)
for name,project in [('core','OpenFantasy.Core.Tests'),('hosting','OpenFantasy.Hosting.Tests')]:
 csproj=f'tests/{project}/{project}.csproj'
 run(name+'-build',[dotnet,'build',csproj,'--no-restore'])
 run(name+'-discovery',[dotnet,'test',csproj,'--no-build','--no-restore','--list-tests','--filter',filters[name]])
 output=(out/(name+'-discovery.log')).read_text();cases=[l.strip() for l in output.splitlines() if l.startswith('    OpenFantasy.')]
 assert cases,(name,output)
 (out/(name+'-cases.json')).write_text(json.dumps(cases,indent=2)+'\n')
 print(name,'discovered',len(cases),flush=True)
# Optional exact Hosting case filters reuse this build and nonzero-discovery discipline.
if len(sys.argv) > 3:
 selected=json.loads(Path(sys.argv[3]).read_text())
 for name,filter in selected.items():
  assert name.startswith('hosting-') and all(c.isascii() and (c.isalnum() or c=='-') for c in name) and name not in filters
  assert isinstance(filter,str) and filter.strip() and filter not in filters.values()
  run(name+'-discovery',[dotnet,'test','tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj','--no-build','--no-restore','--list-tests','--filter',filter])
  output=(out/(name+'-discovery.log')).read_text();cases=[line.strip() for line in output.splitlines() if line.startswith('    OpenFantasy.')]
  assert cases,(name,output)
  (out/(name+'-cases.json')).write_text(json.dumps(cases,indent=2)+'\n')
  filters[name]=filter
  print(name,'discovered',len(cases),flush=True)
paths=[p for tree in ['src/OpenFantasy.Core','src/OpenFantasy.Hosting','tests/OpenFantasy.Core.Tests','tests/OpenFantasy.Hosting.Tests'] for p in (runtime/tree).rglob('*') if p.is_file() and not {'obj','bin'}.intersection(p.parts) and p.suffix in {'.cs','.csproj','.json'}]
paths += [p for p in runtime.glob('*') if p.suffix in {'.props','.targets','.json'}]
pins={str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}
for project in ['OpenFantasy.Core.Tests','OpenFantasy.Hosting.Tests']:
 for p in (runtime/'tests'/project/'bin/Debug/net8.0').glob('OpenFantasy.*.dll'): pins[str(p.relative_to(root))]=hashlib.sha256(p.read_bytes()).hexdigest()
(out/'pins.json').write_text(json.dumps(pins,indent=2)+'\n');(out/'filters.json').write_text(json.dumps(filters,indent=2)+'\n')
print('Pinned',len(pins),'source/content/project/binary inputs; validate lawful fixture routes before any longer journey.')
