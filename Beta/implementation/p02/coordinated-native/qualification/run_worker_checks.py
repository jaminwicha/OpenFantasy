from pathlib import Path
import os,subprocess,json,sys,hashlib,datetime,time
root=Path('/Users/alucinor/Workspace/Projects/OpenFantasy')
q=root/'Beta/implementation/p02/coordinated-native/qualification'
dotnet='/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet'
kind,label=sys.argv[1:3]
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def write(name,value): (q/name).write_text(json.dumps(value,indent=2)+'\n')
env=os.environ.copy()
if kind=='build':
    paths=subprocess.check_output(['rg','--files','Beta/Runtime'],cwd=root,text=True).splitlines()
    paths=sorted(p for p in paths if Path(p).suffix in ['.cs','.csproj','.props','.targets','.sln','.json'])
    write('source-pins-'+label+'.json',{'scope':'C# source, project/build and JSON files before this integration build; not all Godot scenes or art assets.','files':{p:sha(root/p) for p in paths}})
    command=[dotnet,'build','Beta/Runtime/OpenFantasy.sln','--no-restore','-m:1','-p:UseSharedCompilation=false','--verbosity','quiet']
else:
    project='OpenFantasy.Hosting.Tests'
    command=[dotnet,'test','Beta/Runtime/tests/'+project+'/'+project+'.csproj','--no-build','--no-restore']
    command+=['--filter','FullyQualifiedName~CoordinatedNative']
    env['OPENFANTASY_COORDINATED_NATIVE_SOURCE']=str(root/'Beta/implementation/p02/care-definition-digests/qualification/durable-hosting-final')
    env['OPENFANTASY_COORDINATED_NATIVE_EVIDENCE']=str(q/('fixtures-'+label))
    command+=['--logger','trx;LogFileName='+label+'.trx','--results-directory',str(q)]
    env['OPENFANTASY_COORDINATED_EVIDENCE']=str(q/('durable-'+label))
    env['OPENFANTASY_CARE_COMPATIBILITY_EVIDENCE']=str(q/('care-legacy-'+label))
    env['OPENFANTASY_MATERIAL_CARE_STRESS_EVIDENCE']=str(q/('earned-supply-'+label))
    if kind in ['core','baseline','hosting']:
        target=q/('legacy-'+label)
        for var,sub in [('OPENFANTASY_EXCAVATION_EVIDENCE','excavation'),('OPENFANTASY_TRANSPORT_EVIDENCE','transport'),('OPENFANTASY_TERRAIN_MATERIAL_EVIDENCE','terrain'),('OPENFANTASY_MATERIAL_REGISTRY_EVIDENCE','registry'),('OPENFANTASY_MATERIAL_PRODUCTION_EVIDENCE','production'),('OPENFANTASY_CONSUMPTION_GEOGRAPHY_EVIDENCE','consumption')]:
            env[var]=str(target/sub);(target/sub).mkdir(parents=True,exist_ok=True)
record={'startedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'kind':kind,'command':command,'environment':{k:v for k,v in env.items() if k.startswith('OPENFANTASY_') and k.endswith('EVIDENCE')}}
write(label+'-command.json',record); start=time.monotonic()
with (q/(label+'.log')).open('w') as log: result=subprocess.run(command,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT)
record.update({'exitCode':result.returncode,'elapsedSeconds':time.monotonic()-start,'finishedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()})
write(label+'-command.json',record)
if kind=='build' and result.returncode==0:
    files=[]
    for project in ['OpenFantasy.Core.Tests','OpenFantasy.Hosting.Tests']:
        folder=root/'Beta/Runtime/tests'/project/'bin/Debug/net8.0'
        for name in ['OpenFantasy.Core.dll','OpenFantasy.Hosting.dll',project+'.dll',project+'.deps.json',project+'.runtimeconfig.json']:
            p=folder/name
            if p.exists(): files.append(p)
    write('execution-pins-'+label+'.json',{'scope':'Built execution files after '+label,'files':{str(p):sha(p) for p in files}})
text=(q/(label+'.log')).read_text();print(text[-18000:]);raise SystemExit(result.returncode)
