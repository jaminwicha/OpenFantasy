"""Reuse the retained native/authentication gates, with this increment's exact preservation scope."""
import importlib.util,json,sys
from pathlib import Path
sys.dont_write_bytecode=True
P=Path(__file__).resolve().parent

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value

current=module('shared_checks',P/'check.py')
retained=module('retained_checks',P.parent/'native-accepted-care/run_checks.py')
if sys.argv[1] not in ('core','retained-auth','durability','outcome-durability','care-fixtures','care-native','care-auth','outcome-fixtures','outcome-native','outcome-auth','outcome-unit','fixtures','road-native','road-auth','native','native-auth'):
 raise SystemExit('Unsupported retained gate.')
path=Path(json.loads((P/'.latest-build.json').read_text())['path']);build=json.loads(path.read_text())
assert build['accepted'] and current.sources()==build['source'] and current.assemblies()==build['binaries'],'Build evidence is stale'
current.preserve()
root=P/'native-evidence';root.mkdir(exist_ok=True)
if sys.argv[1]=='retained-auth':
 original=P.parent/'native-accepted-care/RETAINED_NATIVE.json'
 copied=root/'RETAINED_NATIVE.json'
 if copied.exists():assert copied.read_bytes()==original.read_bytes(),'Retained manifest changed'
 else:copied.write_bytes(original.read_bytes())
 (root/'RETAINED_NATIVE_SOURCE.json').write_text(json.dumps({'source':str(original),'sourceSha256':current.sha(original),
  'copy':str(copied),'copySha256':current.sha(copied),'meaning':'Exact retained manifest; the unchanged verifier authenticates every referenced file.'},indent=2)+'\n')
retained.P=root;retained.BASE=root
retained_source=retained.sources();retained_binaries=retained.assemblies()
assert retained_source==build['source']
assert all(build['binaries'].get(k)==v for k,v in retained_binaries.items())
bridge=root/'accepted-build.json'
bridge.write_text(json.dumps({'kind':'build','accepted':True,'sourceSha256':retained_source,'assemblySha256':retained_binaries,
 'actualBuildReport':str(path),'actualBuildReportSha256':current.sha(path),
 'admission':'Format adapter for a completed unchanged build, not a new build or equivalence inference.'},indent=2)+'\n')
(root/'.latest-build.json').write_text(json.dumps({'path':str(bridge)})+'\n')
def preservation():
 current.preserve();pins=json.loads((P/'PRESERVATION.json').read_text())
 return {'existingTests':len(pins['Beta/Runtime/tests']),'frozenSpecs':len(pins['Beta/spec-v1_1']),
 'sourceHead':pins['sourceHead'],'unrelatedIndexPreserved':True}
retained.preservation=preservation
raise SystemExit(retained.main())
