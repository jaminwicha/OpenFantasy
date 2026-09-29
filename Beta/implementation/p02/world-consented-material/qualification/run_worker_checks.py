from pathlib import Path
import datetime,hashlib,json,subprocess,sys,xml.etree.ElementTree as ET
q=Path(__file__).resolve().parent;root=q.parents[4]
kind=sys.argv[1];assert kind in ("build","focused","core")
label=kind+"-"+datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
dotnet="/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet"
if kind=="build":command=[dotnet,"build","Beta/Runtime/OpenFantasy.sln","--no-restore","-m:1","-p:UseSharedCompilation=false","--verbosity","quiet"]
else:
 command=[dotnet,"test","Beta/Runtime/tests/OpenFantasy.Core.Tests/OpenFantasy.Core.Tests.csproj","--no-build","--no-restore","--logger","trx;LogFileName="+label+".trx","--results-directory",str(q)]
 if kind=="focused":command += ["--filter","FullyQualifiedName~Consented"]
def pins():return {str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for folder in ("src","client/scripts","tests") for p in (root/"Beta/Runtime"/folder).rglob("*.cs") if not any(v in p.parts for v in ("bin","obj",".godot"))}
before=pins();record={"command":command,"sourceSha256":before,"limitSeconds":240 if kind=="build" else 600}
try:
 with (q/(label+".log")).open("x") as log:result=subprocess.run(command,cwd=root,stdout=log,stderr=subprocess.STDOUT,timeout=record["limitSeconds"])
 record["exitCode"]=result.returncode
 if kind!="build":
  rows=ET.parse(q/(label+".trx")).findall(".//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult");outcomes=[r.attrib["outcome"] for r in rows];record["outcomes"]={v:outcomes.count(v) for v in set(outcomes)};record["distinctTestIds"]=len({r.attrib["testId"] for r in rows});assert outcomes and all(v=="Passed" for v in outcomes),"No tests or nonpassing outcome"
except Exception as error:record["error"]=repr(error)
record["sourceUnchanged"]=before==pins();(q/(label+".json")).write_text(json.dumps(record,indent=2)+"\n");print((q/(label+".log")).read_text()[-6000:]);print({k:v for k,v in record.items() if k!="sourceSha256"});sys.exit(0 if record.get("exitCode")==0 and not record.get("error") and record["sourceUnchanged"] else 1)
