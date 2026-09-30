from pathlib import Path
import importlib.util,shutil,tempfile,subprocess,os,hashlib,json,time,signal
root=Path('/Users/alucinor/Workspace/Projects/OpenFantasy'); packet=root/'Beta/implementation/p02/retained-gift-native'; runtime=root/'Beta/Runtime'
path=root/'Beta/implementation/p02/native-startup-profile/profile_native_startup.py'
spec=importlib.util.spec_from_file_location('profile',path); profile=importlib.util.module_from_spec(spec);spec.loader.exec_module(profile)
out=packet/'qualification/replay-profile-01';out.mkdir();work=Path(tempfile.mkdtemp(prefix='openfantasy-retained-profile-'));copy=work/'runtime';copy.mkdir()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
inputs={str(p):sha(p) for p in (runtime/'src').rglob('*') if p.is_file() and p.suffix in ('.cs','.csproj','.json') and not {'bin','obj'}.intersection(p.parts)}
report={'kind':'isolated instrumented full-replay diagnostic; NOT native acceptance','private':str(work),'sourcePins':inputs,'runs':[],'startedUtc':time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime())}
try:
 shutil.copytree(runtime/'src',copy/'src',ignore=shutil.ignore_patterns('bin','obj'))
 shutil.copytree(runtime/'content',copy/'content')
 for n in ('Directory.Build.props','global.json','NuGet.Config'):shutil.copyfile(runtime/n,copy/n)
 profile.ANCHORS=[a for a in profile.ANCHORS if not a[0].startswith('client/')]
 for relative,signature,label in [('Worlds/WorldResourceHistoryCodec.cs','public static byte[] Encode(WorldResourceHistory value','codec-world-resource'),('Worlds/WorldResourceHistoryCodecV1.cs','public static byte[] Encode(WorldResourceHistory value','codec-world-resource-body'),('Progression/ResourceHistory/ActorResourceHistoryCodecV1.cs','public static byte[] Encode(ActorResourceHistory history','codec-actor-resource-history'),('Worlds/WorldMaterialProposalCodec.cs','public static byte[] Encode(WorldMaterialProposalBook book','codec-proposals')]:
  profile.ANCHORS.append(('src/OpenFantasy.Core/'+relative,profile.sig(signature),profile.scope(label),False))
 report['applied'],report['skipped']=profile.instrument(copy,out)
 sdk=Path('/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401');env=dict(os.environ,DOTNET_ROOT=str(sdk),PATH=str(sdk)+os.pathsep+os.environ.get('PATH',''))
 source=packet/'qualification/fixture-20260930T021706Z-76a6b/fixtures/save.sqlite';db=work/'world.sqlite';shutil.copyfile(source,db);report['databaseSha256']=sha(source)
 def run(label,cmd,limit,env):
  t=time.monotonic();r={'label':label,'command':list(map(str,cmd)),'limitSeconds':limit,'loadBefore':os.getloadavg()};report['runs'].append(r)
  with (out/(label+'.stdout.log')).open('x') as stdout,(out/(label+'.stderr.log')).open('x') as stderr:
   p=subprocess.Popen(cmd,cwd=copy,env=env,stdout=stdout,stderr=stderr,start_new_session=True)
   try:p.wait(timeout=limit)
   except BaseException:
    os.killpg(p.pid,signal.SIGKILL);p.wait();r['timedOut']=True
   r.update(exitCode=p.returncode,seconds=time.monotonic()-t,loadAfter=os.getloadavg())
  print(r,flush=True);assert p.returncode==0
 run('build',[sdk/'dotnet','build','src/OpenFantasy.Host/OpenFantasy.Host.csproj','-p:RestoreLockedMode=true','-m:1','-p:UseSharedCompilation=false','--verbosity','quiet'],240,env)
 report['assemblies']={str(p):sha(p) for p in (copy/'src/OpenFantasy.Host/bin/Debug/net10.0').glob('OpenFantasy*.dll')}
 env['OPENFANTASY_NATIVE_STARTUP_REPORT']=str(out/'costs.json')
 run('replay',[sdk/'dotnet',copy/'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll','--inspect-recorded-world',db,'1739'],120,env)
 report['databaseUnchanged']=sha(db)==sha(source)
 report['complete']=True
except Exception as e:report['error']=repr(e)
finally:
 report['sourceUnchanged']=all(sha(Path(p))==h for p,h in inputs.items());(out/'report.json').write_text(json.dumps(report,indent=2)+'\n');print('report',out/'report.json',flush=True)
