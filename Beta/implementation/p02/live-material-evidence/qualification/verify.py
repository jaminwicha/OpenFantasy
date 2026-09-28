from pathlib import Path
import datetime,hashlib,json,re,subprocess,xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[5]
q=Path(__file__).resolve().parent
packet=q.parent
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
preserved=json.loads((packet/'preservation.json').read_text())
assert hashlib.sha256(subprocess.check_output(['git','diff','--cached','--binary','--','.',':!Beta'],cwd=root)).hexdigest()==preserved['predecessorIndexSha256']
for name,digest in preserved['frozenSpecs'].items(): assert sha(root/name)==digest,name
reconciled=json.loads((packet/'milestone-reconciliation.json').read_text())
original_inventory=subprocess.check_output(['git','show',reconciled['sourceRevision']+':'+reconciled['sourcePath']],cwd=root)
assert hashlib.sha256(original_inventory).hexdigest()==reconciled['sourceSha256']
assert len(reconciled['milestones'])==71
assert subprocess.check_output(['git','rev-parse',preserved['checkpoint']],cwd=root,text=True).strip()==preserved['head']
wip=json.loads((packet/'host-checkpoint.json').read_text())
assert subprocess.check_output(['git','rev-parse',wip['ref']],cwd=root,text=True).strip()==wip['commit']
for entry in reconciled['milestones']:
 name,revision=entry.split()
 assert subprocess.check_output(['git','rev-parse',name],cwd=root,text=True).strip()==revision,name
build=json.loads((q/'build-04-command.json').read_text());assert build['exitCode']==0
for kind in ['source','execution']:
 pins=json.loads((q/(kind+'-pins-build-04.json')).read_text())['files']
 for name,digest in pins.items(): assert sha(root/name)==digest,name
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
reports={}
for name in ['integration-02','core-final','hosting-final']:
 tree=ET.parse(q/(name+'.trx'))
 counters={key:int(value) for key,value in tree.find('t:ResultSummary/t:Counters',ns).attrib.items()}
 assert counters['total']>0 and counters['passed']==counters['total'] and not counters['failed'] and not counters['notExecuted'],(name,counters)
 command=json.loads((q/(name+'-command.json')).read_text());assert command['exitCode']==0
 reports[name]={'counters':counters,'execution':command}
 if name!='integration-02':assert '--filter' not in command['command'],name
assert reports['integration-02']['counters']['total']==21
assert reports['core-final']['counters']['total']==1577
compat=[]
prior=root/'Beta/implementation/p02/world-material-care-continuation/qualification'
for beforeFolder,afterFolder in [('care-legacy-before','care-legacy-core-final'),('world-legacy-before','care-legacy-hosting-final')]:
 for before in sorted((prior/beforeFolder).glob('*.bin')):
  after=q/afterFolder/before.name
  assert before.read_bytes()==after.read_bytes(),before.name
  compat.append({'file':before.name,'bytes':before.stat().st_size,'sha256':sha(after),'retainedBaseline':str(before.relative_to(root)),'current':str(after.relative_to(root))})
assert len(compat)==9 and sum(item['bytes'] for item in compat)==638645,compat
links=0;pages=['README.md','live-material-evidence.md','production-continuation.md','resource-archive-continuation.md','world-composition.md']
for name in pages:
 p=root/'Beta/Runtime/docs/architecture'/name
 for url in re.findall(r'\]\(([^)]+)\)',p.read_text()):
  if '://' in url or url.startswith('#'):continue
  assert (p.parent/url.split('#')[0]).exists(),(name,url)
  links+=1
result={'scope':'Optional profile26 live material/work observations and Host reconstruction from a completely verified journal. No new world/production/storage format or coordinated material continuation.',
 'verifiedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'build':build,'reports':reports,
 'distinctManagedCases':reports['core-final']['counters']['total']+reports['hosting-final']['counters']['total'],
 'newIntegrationCases':reports['integration-02']['counters']['total'],'compatibilityFrames':compat,
 'compatibilityBytes':sum(item['bytes'] for item in compat),'preservedMilestones':len(reconciled['milestones']),
 'milestoneInventory':'milestone-reconciliation.json; original empty inventory retained and corrected against prior committed evidence',
 'preservedFrozenSpecs':len(preserved['frozenSpecs']),'predecessorIndexSha256':preserved['predecessorIndexSha256'],
 'architecture':{'pages':len(pages),'resolvingLocalFileLinks':links},
 'sourceAndExecutionMatchBuild':'build-04','limitations':['No coordinated world27/ProductionV9/schema8 continuation enabled.','No new production throughput, native UI/device or human acceptance claim.','Public read-failure recovery flag reviewed in source; direct corruption tests exercise the replay reader.']}
(q/'verification.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({key:result[key] for key in ['distinctManagedCases','newIntegrationCases','compatibilityBytes','preservedMilestones','preservedFrozenSpecs','architecture']},indent=2))
