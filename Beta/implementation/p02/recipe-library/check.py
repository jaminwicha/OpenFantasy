"""Serialized, source-pinned recipe library qualification; no provider requests or automatic retries."""
import datetime,fcntl,hashlib,importlib.util,json,os,signal,subprocess,sys,time
from pathlib import Path
import xml.etree.ElementTree as ET
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent
ROOT=P.parents[3]
spec=importlib.util.spec_from_file_location('base',P.parent/'shared-player-sessions/check.py')
base=importlib.util.module_from_spec(spec);spec.loader.exec_module(base)
def sources(mode=None):
 all_sources=base.sources()
 all_sources.update({str(path.relative_to(ROOT)):base.sha(path) for path in base.R.iterdir() if path.is_file() and path.suffix in ('.props','.targets','.json')})
 if mode in ('hosting-build','codec'):
  project=base.R/'tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj'
  links={str((project.parent/node.attrib['Include']).resolve().relative_to(ROOT)) for node in ET.parse(project).findall('.//Compile') if 'Include' in node.attrib}
  all_sources={name:value for name,value in all_sources.items() if name in links or any(part in name for part in ('/src/OpenFantasy.Core/','/src/OpenFantasy.Hosting/','/tests/OpenFantasy.Hosting.Tests/','/content/')) or Path(name).parent==Path('Beta/Runtime')}
 return all_sources|{'Beta/Runtime/OpenFantasy.sln':base.sha(base.R/'OpenFantasy.sln')}
def binaries(mode=None):
 all_binaries=base.assemblies()
 if mode in ('hosting-build','codec'):
  return {name:value for name,value in all_binaries.items() if any(part in name for part in ('/src/OpenFantasy.Core/','/src/OpenFantasy.Hosting/','/tests/OpenFantasy.Hosting.Tests/'))}
 return all_binaries

def preserve():
 pins=json.loads((P/'PRESERVATION.json').read_text())
 for group in pins['protected'].values():
  for name,digest in group.items():
   if name in pins.get('permittedTestRefactors',{}):
    archived=pins['permittedTestRefactors'][name];assert base.sha(ROOT/archived['original'])==digest,name
    original=(ROOT/archived['original']).read_text();current=(ROOT/name).read_text()
    additions=[line for line in current.splitlines(keepends=True) if line not in original.splitlines(keepends=True)]
    assert all(line.strip().startswith('<Compile Include="../../client/scripts/') and line.strip().endswith('/>') for line in additions),name
    for line in additions:current=current.replace(line,'',1)
    assert current==original,name
   else:assert base.sha(ROOT/name)==digest,name
 assert hashlib.sha256(subprocess.check_output(['git','diff','--cached','--binary','--','.',':!Beta'],cwd=ROOT)).hexdigest()==pins['predecessorIndexSha256']
 assert subprocess.check_output(['git','rev-parse',pins['checkpoint']],cwd=ROOT,text=True).strip()==pins['sourceHead']
def main(mode):
 filters={
  'core':('Core',None),
  'client-managed':('Hosting','(FullyQualifiedName~Client&FullyQualifiedName!~Native)|FullyQualifiedName~Networking.Selection|FullyQualifiedName~Simulation.ControlPermissions'),
  'new-managed':('Hosting','FullyQualifiedName~Client.RecipeLibrary|FullyQualifiedName~PublicMaterialDescriptions'),
  'codec':('Hosting','FullyQualifiedName~Networking.PlayerObservation|FullyQualifiedName~Networking.Selection|FullyQualifiedName~Networking.RecoveryOutcome|FullyQualifiedName~Networking.ControllerOutcome|FullyQualifiedName~PublicMaterialDescriptions'),
  'native-recipes':('Server','FullyQualifiedName~RecipeLibraryNative'),
  'native-production':('Server','FullyQualifiedName~SelectedProductionNative'),
  'native-gifts':('Server','FullyQualifiedName~SelectedGiftsNative'),
  'native-selection':('Server','FullyQualifiedName~SelectedControlNative'),
  'native-roster':('Server','FullyQualifiedName~NativeRosterJourneyTests'),
  'native-compatibility':('Server','FullyQualifiedName~NativePlayerProcessTests')}




 assert mode in ('build','hosting-build') or mode in filters
 building=mode in ('build','hosting-build')
 with Path('/tmp/openfantasy-retained-resource-integration.lock').open('w') as lock:
  fcntl.flock(lock,fcntl.LOCK_EX)
  out=P/'qualification'/(mode+'-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'));out.mkdir(parents=True)
  source=sources(mode);report={'checkerSha256':base.sha(P/'check.py'),'baseCheckerSha256':base.sha(P.parent/'shared-player-sessions/check.py'),'mode':mode,'source':source,'tracing':'--trace' in sys.argv,'limitSeconds':240 if building else 600,'systemLoadAverageAtStart':os.getloadavg()};started=time.monotonic()
  cmd=[base.DOTNET,'build',str(base.R/('tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj' if mode=='hosting-build' else 'OpenFantasy.sln')),'--no-restore','--nologo']
  if not building:
   suite,selected=filters[mode]
   cmd=[base.DOTNET,'test',str(base.R/f'tests/OpenFantasy.{suite}.Tests/OpenFantasy.{suite}.Tests.csproj'),'--no-build','--no-restore','--nologo','--logger','console;verbosity=detailed','--logger','trx;LogFileName=checks.trx','--results-directory',str(out)]
   if selected:cmd.extend(['--filter',selected])
  report['command']=cmd
  report['nativeInputs']={name:os.environ[name] for name in ('OPENFANTASY_NATIVE_CARE_DISPOSITION_FIXTURES','OPENFANTASY_NATIVE_CARE_DISPOSITION_RESULTS','OPENFANTASY_CARE_DISPOSITION_DURABILITY_FIXTURES','OPENFANTASY_CARE_DISPOSITION_DURABILITY_RESULTS','OPENFANTASY_NATIVE_ARCHIVED_CARE_DISPOSITION_FIXTURES','OPENFANTASY_NATIVE_ARCHIVED_CARE_DISPOSITION_RESULTS','OPENFANTASY_NATIVE_CONTROL_RETURN_FIXTURES') if name in os.environ}
  try:
   preserve()
   if not building:
    bpath=Path(json.loads((P/('.latest-hosting-build.json' if mode=='codec' else '.latest-build.json')).read_text())['path']);b=json.loads(bpath.read_text())
    assert b['accepted'] and b['source']==source and b['binaries']==binaries(mode),'Tests require a successful current-source build and its actual binaries'
    report['buildReport']=str(bpath)
   env=os.environ|{'OPENFANTASY_DOTNET':base.DOTNET}
   if mode.startswith('native-'):
    godot=Path('/Users/alucinor/.local/share/openfantasy/toolchains/godot-4.6/Godot_mono.app/Contents/MacOS/Godot')
    assert godot.is_file()
    env['OPENFANTASY_GODOT']=str(godot)
    env['OPENFANTASY_NATIVE_PLAYER_EVIDENCE']=str(out/'native')
    report['nativeTool']={'path':str(godot),'sha256':base.sha(godot)}
   env.pop('OPENFANTASY_HOST_TIMING',None)
   env.pop('OPENFANTASY_RUNTIME_SCHEDULING',None)
   if report['tracing']:
    assert not building
    env['OPENFANTASY_HOST_TIMING']=str(out)
    env['OPENFANTASY_RUNTIME_SCHEDULING']=str(out)
   with (out/'command.log').open('w') as log:
    process=subprocess.Popen(cmd,cwd=base.R,env=env,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
    try:process.wait(timeout=report['limitSeconds'])
    except subprocess.TimeoutExpired:os.killpg(process.pid,signal.SIGKILL);process.wait();raise
   report['exitCode']=process.returncode;report['sourceUnchanged']=source==sources(mode);report['binaries']=binaries(mode);preserve()
   if not building:
    rows=ET.parse(out/'checks.trx').findall('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult')
    report['outcomes']={k:sum(r.get('outcome')==k for r in rows) for k in {r.get('outcome') for r in rows}}
    report['cases']=[{'name':r.get('testName'),'outcome':r.get('outcome')} for r in rows]
    skipped={r.get('testName') for r in rows if r.get('outcome')=='NotExecuted'}
    expected={'OpenFantasy.Server.Tests.WindowsInvitationTests.Invitation_created_under_readable_parent_has_only_protected_owner_access'} if mode in ('server','server-managed','server-compatibility') and os.name!='nt' else set()
    report['unrunCases']=sorted(skipped);report['expectedSkips']=skipped==expected
   report['accepted']=process.returncode==0 and report['sourceUnchanged'] and (building or report['expectedSkips'] and report['outcomes'].get('Passed',0)>0 and set(report['outcomes'])<={'Passed','NotExecuted'})
  except Exception as error:report.update(accepted=False,error=repr(error))
  report['seconds']=time.monotonic()-started;report['accepted']=report['accepted'] and report['seconds']<report['limitSeconds']
  (out/'report.json').write_text(json.dumps(report,indent=2)+'\n');(P/('.latest-'+mode+'.json')).write_text(json.dumps({'path':str(out/'report.json')})+'\n')
  print(json.dumps({k:v for k,v in report.items() if k not in ('source','binaries','command','cases')},indent=2));print('evidence:',out)
  if not report['accepted'] and (out/'command.log').exists():print((out/'command.log').read_text()[-5500:])
  return 0 if report['accepted'] else 1
if __name__=='__main__':sys.exit(main(sys.argv[1]))
