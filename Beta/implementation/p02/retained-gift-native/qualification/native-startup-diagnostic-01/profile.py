from pathlib import Path
import json,shutil,tempfile,hashlib,subprocess,os,time,re,signal,importlib.util
root=Path('/Users/alucinor/Workspace/Projects/OpenFantasy');runtime=root/'Beta/Runtime';packet=root/'Beta/implementation/p02/retained-gift-native';out=packet/'qualification/native-startup-diagnostic-01';out.mkdir()
baseReport=json.loads((root/'Beta/implementation/p02/retained-resource-encoding/qualification/measure-20260930T025414Z/result.json').read_text());base=Path(baseReport['afterTree']['path'])
work=Path(tempfile.mkdtemp(prefix='openfantasy-retained-native-profile-'));copy=work/'runtime'
shutil.copytree(base,copy,ignore=shutil.ignore_patterns('bin','obj','*.log'))
shutil.copytree(runtime/'client',copy/'client',ignore=lambda folder,names:['temp'] if Path(folder).name=='mono' else [n for n in names if n in ('bin','obj')])
(copy/'tools').mkdir();shutil.copyfile(runtime/'tools/check_retained_gifts.py',copy/'tools/check_retained_gifts.py')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
report={'kind':'isolated instrumented native startup diagnostic, not acceptance','private':str(work),'baselineMeasurement':str(root/'Beta/implementation/p02/retained-resource-encoding/qualification/measure-20260930T025414Z/result.json'),'productionSources':{str(f):sha(f) for b in ('src','client/scripts') for f in (runtime/b).rglob('*.cs') if not {'obj','bin','.godot'}.intersection(f.parts)},'commands':[],'nativeLimits':{'opening':30,'probe':150,'process':180}}
p='global::OpenFantasy.Core.NativeStartupProbe.'
def inject(relative,pattern,payload):
 f=copy/'client/scripts'/relative;s=f.read_text();matches=list(re.finditer(pattern,s));assert len(matches)==1,(relative,pattern,len(matches));end=matches[0].end();f.write_text(s[:end]+'\n        '+payload+s[end:])
inject('Bootstrap.cs',r'public override async void _Ready\(\)\n    \{',p+'Mark("bootstrap-ready");')
inject('FarmGame.cs',r'public override void _Ready\(\)\n    \{','using var cost = '+p+'Begin("game-ready", true);')
inject('FarmGame.cs',r'public override void _Process\(double delta\)\n    \{',p+'MarkOnce("first-process-frame");')
inject('FarmGame.cs',r'if \(_opening is \{ IsCompleted: true \}\)\n        \{',p+'MarkOnce("opening-task-observed");')
inject('FarmGame.cs',r'private void Present\(LocalWorldView view\)\n    \{','using var cost = '+p+'Begin("client-present", true);')
inject('FarmClientSession.cs',r'internal static Task<FarmClientSession> OpenRetainedGiftsAsync\(string savePath, string originalGenesisPath,\n        ushort targetProfileVersion\) => Task.Run\(\(\) =>\n    \{','using var cost = '+p+'Begin("client-open-retained", true);')
inject('FarmClientSession.cs',r'private static FarmClientSession Attach\(SqliteWorldAuthority authority,\n        Func<WorldSnapshot, WorldResident, WorldResident> selectCompanion, bool gifts = false\)\n    \{','using var cost = '+p+'Begin("client-attach", true);')
inject('RetainedGiftPlayProbe.cs',r'public override async void _Ready\(\)\n    \{',p+'Mark("probe-ready");')
inject('RetainedGiftPlayProbe.cs',r'_openingMilliseconds = _driver.ElapsedMilliseconds;',p+'Mark("probe-ready-condition");')
sdk=Path('/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401');env=dict(os.environ,DOTNET_ROOT=str(sdk),PATH=str(sdk)+os.pathsep+os.environ.get('PATH',''));start=time.monotonic()
def run(label,cmd,limit):
 t=time.monotonic();entry={'label':label,'command':list(map(str,cmd)),'limit':limit};report['commands'].append(entry)
 with (out/(label+'.stdout.log')).open('x') as stdout,(out/(label+'.stderr.log')).open('x') as stderr:
  child=subprocess.Popen(cmd,cwd=copy,env=env,stdout=stdout,stderr=stderr,start_new_session=True)
  try:child.wait(timeout=limit)
  except BaseException:os.killpg(child.pid,signal.SIGKILL);child.wait();entry['timedOut']=True
  entry.update(exitCode=child.returncode,seconds=time.monotonic()-t)
 print(entry,flush=True);return child.returncode
try:
 assert run('build',[sdk/'dotnet','build','client/OpenFantasy.Client.csproj','-p:RestoreLockedMode=true','-m:1','-p:UseSharedCompilation=false','--verbosity','quiet'],240)==0
 report['instrumentedAssemblies']={str(f):sha(f) for f in (copy/'client/.godot/mono/temp/bin/Debug').glob('OpenFantasy*.dll')}
 env['OPENFANTASY_NATIVE_STARTUP_REPORT']=str(out/'costs.json')
 run('native',['python3',copy/'tools/check_retained_gifts.py','--dotnet',sdk/'dotnet','--godot','/Users/alucinor/.local/share/openfantasy/toolchains/godot-4.6/Godot_mono.app/Contents/MacOS/Godot','--fixtures',packet/'qualification/fixture-20260930T021706Z-76a6b/fixtures','--output',out/'native.json','--rendered'],400)
except Exception as e:report['error']=repr(e)
finally:
 report['sourceUnchanged']=all(sha(Path(f))==h for f,h in report['productionSources'].items());report['seconds']=time.monotonic()-start;(out/'report.json').write_text(json.dumps(report,indent=2)+'\n');print('report',out/'report.json',flush=True)
