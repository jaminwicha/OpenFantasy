from pathlib import Path
import subprocess,json,hashlib,xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[5]
# The script lives under Beta/implementation/p02/<packet>/qualification.
assert (root/'AGENTS.md').exists(),root
q=Path(__file__).resolve().parent
sha=lambda path:hashlib.sha256(Path(path).read_bytes()).hexdigest()
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
reports={}
for label in ('focused-final','hosting-focused','core-final','hosting-final'):
    tree=ET.parse(q/(label+'.trx')); counts={k:int(v) for k,v in tree.find('.//t:Counters',ns).attrib.items()}
    assert counts['total']==counts['passed']==counts['executed'] and counts['failed']==counts['notExecuted']==0,(label,counts)
    command=json.loads((q/(label+'-command.json')).read_text());assert command['exitCode']==0
    reports[label]={'counters':counts,'execution':command}
for name in ('source-pins-build-final.json','execution-pins-build-final.json'):
    pins=json.loads((q/name).read_text())
    for rel,digest in pins['files'].items():assert sha(root/rel)==digest,(name,rel)
before=json.loads((q/'preservation-before.json').read_text())
for ref,digest in before['milestones'].items():assert subprocess.check_output(['git','rev-parse',ref],cwd=root,text=True).strip()==digest,ref
for rel,digest in before['frozenSpecs'].items():assert sha(root/rel)==digest,rel
staged=hashlib.sha256(subprocess.check_output(['git','diff','--cached','--binary','--','.',':!Beta'],cwd=root)).hexdigest();assert staged==before['unrelatedStagedDiffSha256']
frames={}
for original,actual in [('care-legacy-before','care-legacy-core-final'),('world-legacy-before','care-legacy-hosting-final')]:
    for path in (q/original).glob('*.bin'):
        final=q/actual/path.name;assert final.read_bytes()==path.read_bytes(),path
        frames[path.name]={'bytes':path.stat().st_size,'sha256':sha(path)}
assert len(frames)==9
stress=json.loads((q/'earned-supply-hosting-final/earned-supply.json').read_text());assert stress['actualDamage']>=520*40 and stress['consumedDoses']==0 and stress['reopenedExactly']
result={'scope':'Standalone care frontier and actual full-world supply/earned-injury preparation. Complete-world continuation remains unimplemented.','reports':reports,'distinctManagedCases':reports['core-final']['counters']['total']+reports['hosting-final']['counters']['total'],'allSelectedCasesPassed':True,'compatibilityFrames':frames,'exactFrameCount':len(frames),'exactFrameBytes':sum(v['bytes'] for v in frames.values()),'preservedMilestones':len(before['milestones']),'preservedFrozenSpecs':len(before['frozenSpecs']),'unrelatedStagedDiffSha256':staged,'sourcePins':'source-pins-build-final.json','executionPins':'execution-pins-build-final.json','pinsMatch':True,'earnedSupply':stress,'architecture':json.loads((q/'architecture-links.json').read_text()),'review':'../REVIEW.md','failures':'../FAILURES.md'}
(q/'verification.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:result[k] for k in ['distinctManagedCases','allSelectedCasesPassed','exactFrameCount','exactFrameBytes','preservedMilestones','preservedFrozenSpecs','pinsMatch']},indent=2))
