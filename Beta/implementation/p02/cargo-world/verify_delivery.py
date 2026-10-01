"""Verify that the integrated cargo proof refers to one unchanged source/build and preserved inputs."""
import importlib.util, json
from pathlib import Path
P=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('checks',P/'run_checks.py');checks=importlib.util.module_from_spec(spec);spec.loader.exec_module(checks)
source=checks.sources();assemblies=checks.assemblies();reports={};names=set()
for kind in ('build','unit','journey','regression','saved-audit'):
    path,data=checks.latest(kind)
    assert data['sourceSha256']==source,(kind,'stale source')
    assert data['assemblySha256']==assemblies,(kind,'stale binaries')
    assert data['sourceUnchanged'] and data['elapsedSeconds']<data['limitSeconds'],kind
    if kind!='build':
        assert set(data['outcomes'])=={'Passed'},(kind,'failed or skipped tests')
        assert data['buildReportSha256']==reports['build']['sha256'],(kind,'different build')
    names.update(data.get('testNames',[]))
    reports[kind]={'path':str(path),'sha256':checks.sha(path),'outcomes':data.get('outcomes'), 'elapsedSeconds':data['elapsedSeconds']}
_,journey=checks.latest('journey');_,audit=checks.latest('saved-audit')
assert audit['journeyReportSha256']==reports['journey']['sha256'],'Audit examined another journey'
assert audit['binariesUnchanged'],'Audit binaries changed'
artifacts=journey['artifactSha256'];evidence=Path(journey['journeyEvidence'])
assert all(checks.sha(evidence/name)==value for name,value in artifacts.items()),'Journey artifacts changed'
assert audit['journeyArtifactSha256']==artifacts,'Audit input artifacts differ'
report={'accepted':True,'checks':reports,'sourceFiles':len(source),'assemblies':len(assemblies),'distinctManagedCases':len(names),'journeyArtifacts':artifacts,'preservation':checks.preservation(),'sourceSha256':source,'assemblySha256':assemblies}
(P/'FINAL_CHECKS.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({k:v for k,v in report.items() if k not in ('sourceSha256','assemblySha256','journeyArtifacts')},indent=2))
