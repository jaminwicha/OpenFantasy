"""Verify the dated, unimplemented Beta 1.1 specification snapshot; see README.md."""
from pathlib import Path
import hashlib,json,re,zipfile
b=Path(__file__).resolve().parents[1];root=b.parent;s=b/'spec-v1_1';research=b/'research-v0_9';out=s/'SPEC_VALIDATION.json'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();st=lambda t:hashlib.sha256(t.encode()).hexdigest()
d=json.loads((s/'REQUIREMENTS.json').read_text());req={r['id']:r for r in d['requirements']};assert len(req)==len(d['requirements'])==d['requirement_count']==181
assert set(req)=={f'RQ-{i:03}' for i in range(1,182)}
source_records=0
for src in d['source_order']:
 p=b/src['path'];assert sha(p)==src['sha256'],p;x=json.loads(p.read_text())
 for key in ['requirements','overrides','additions']:
  for record in x.get(key,[]):
   assert any(r['document']==src['path'] and r['record']==record for r in req[record['id']]['source_revisions']),(p,record['id']);source_records+=1
for r in req.values():assert r['source_revisions'] and r['implementation_status'].startswith('Not implemented')
oldr=json.loads((b/'spec-v1_0/REQUIREMENTS.json').read_text())['requirements'];assert list(req.values())[:150]==oldr,'Prior150requirementrecords changed'
c=json.loads((s/'COVERAGE.json').read_text());cov=c['coverage'];ph={p['id']:p for p in c['phases']};assert len(ph)==15 and len(cov)==c['requirement_count']==181
oldcov=json.loads((b/'spec-v1_0/COVERAGE.json').read_text())['coverage'];
for current,prior in zip(cov[:150],oldcov):
 expected=dict(prior)
 if current['requirement_id']=='RQ-123':expected['concrete_validation']=expected['concrete_validation'].replace('all 150 requirements','all 181 requirements')
 assert current==expected,('Unexpected priorcoverage change',current['requirement_id'])
for p in ph.values():
 for dep in p['depends_on']:assert dep in ph and int(dep[1:])<int(p['id'][1:])
def ancestry(pid):
 out=set()
 for dep in ph[pid]['depends_on']:out.add(dep);out.update(ancestry(dep))
 return out
for r in cov:
 assert r['introduction_phase']==r['full_completion_phase'] or r['introduction_phase'] in ancestry(r['full_completion_phase']),(r['requirement_id'],'missing actual phase dependency')
for r in cov:
 assert r['requirement']==req[r['requirement_id']]['requirement'] and r['requirement_text_sha256']==st(r['requirement'])
 assert r['introduction_phase'] in ph and r['full_completion_phase'] in ph and r['introduction_phase']<=r['full_completion_phase']
 assert r['responsible_role'] and r['concrete_validation'] and r['independent_review'] and not r['evidence_paths']
 for ref in r['design_documents']:assert (s/ref.split('#')[0]).exists(),ref
cases={}
for domain in c['cross_cutting_design_domains']:
 for r in domain.get('case_phase_map',[]):
  cid=r['case_id'];assert cid not in cases,cid;cases[cid]=r
  assert r['introduction_phase'] in ph and r['full_integration_phase'] in ph and r['introduction_phase']<=r['full_integration_phase'];assert not r['evidence_paths'] and r['concrete_validation']
  assert r['introduction_phase']==r['full_integration_phase'] or r['introduction_phase'] in ancestry(r['full_integration_phase']),(cid,'missing case dependency')
for r in cov[150:]:
 for cid in r.get('retained_case_ids',[]):assert cid in cases,(r['requirement_id'],cid)
for i in range(1,23):assert f'SYN-{i:02}' in cases
rc=json.loads((research/'RESEARCH_COVERAGE.json').read_text());rr={r['id']:r for r in rc['requirements']};topics={t['topic']:t for t in rc['topics']}
assert len(rr)==181 and set(rr)==set(req) and len(topics)==35
assert rc['source_requirements_sha256']==sha(s/'REQUIREMENTS.json')
for rid,r in rr.items():
 assert r['requirement']==req[rid]['requirement'] and r['requirement_sha256']==st(req[rid]['requirement']) and r['topics']
 assert set(r['topics'])=={t['topic'] for t in topics.values() if rid in t['requirements']}
for t in topics.values():
 assert t['application'] and t['retained_proofs'] and t['evidence_limit']
 for ref in t['artifacts']:assert (research/ref.split('#')[0]).is_file(),ref
assert len(rc['dossiers'])==9 and len(rc['retained_cases'])==48
for x in rc['dossiers']:
 p=research/x['artifact'];assert sha(p)==x['content_sha256'] and len(p.read_text().split())==x['word_count']
for x in rc['retained_cases']:
 assert x['case_id'] in cases,x['case_id']
 assert cases[x['case_id']]['concrete_validation']==x['required_proof'],(x['case_id'],'exact case text')
archivecount=0;arch=json.loads((b/'archive/index.json').read_text())['versions']
for v in arch:
 mp=b/'archive'/v['manifest'];m=json.loads(mp.read_text());assert len(m['files'])==v['file_count']
 for e in m['files']:
  p=mp.parent/e['archived_path'];assert sha(p)==e['sha256'] and p.stat().st_size==e['bytes'];archivecount+=1
frozen=0
for p in (b/'spec-v1_0').iterdir():
 if p.is_file():assert sha(p)==sha(b/'archive/specification-v1.0-2026-09-08/spec-v1_0'/p.name),p;frozen+=1
iv=json.loads((b/'content-migration/original-content-inventory-2026-09-08.json').read_text())
with zipfile.ZipFile(b/'content-migration/original-source-2026-09-08.zip') as z:
 for f in iv['files']:
  assert sha(root/f['path'])==f['sha256'] and hashlib.sha256(z.read(f['path'])).hexdigest()==f['sha256'],f['path']
ec=root.parent/'GameDevAIEcosystem';em=json.loads((ec/'manifests/2026-09-08-portable-skills.json').read_text());assert len({x['target'] for x in em['changes']})==len(em['changes'])
for x in em['changes']:assert sha(Path(x['target']))==x['after_sha256'],x['target']
# Review source fingerprints without editing external projects.
source_files=[]
for p in research.glob('local-*-review.md'):
 for line in p.read_text().splitlines():
  m=re.match(r'^\| (.+?) \| `([a-f0-9]{64})` \|$',line)
  if not m:continue
  label=m.group(1);m2=re.search(r'\]\(([^)]+)\)',label)
  if m2:fp=Path(re.sub(r':\d+$','',m2.group(1)))
  else:fp=root.parent/label.strip('`')
  assert fp.is_file(),fp;assert sha(fp)==m.group(2),(fp,'audited source drift');source_files.append(fp)
# Current links/anchors/table integrity; historical files are preserved separately.
md=sorted(set([b/'tools/README.md']+list(s.glob('*.md'))+list(research.glob('*.md'))+[b/'README.md',b/'archive/README.md',b/'ecosystem-update-record-2026-09-08.md']))
def anchors(p):
 out=set()
 for h in re.findall(r'^#{1,6}\s+(.+)$',p.read_text(),re.M):out.add(re.sub(r'[^\w\-]','',h.lower().strip().replace(' ','-')))
 return out
links=0;external=set();issues=[];tableissues=[]
for p in md:
 for target in re.findall(r'\]\(([^)]+)\)',p.read_text()):
  target=target.strip('<>')
  if target.startswith(('http://','https://')):external.add(target);continue
  if target.startswith(('codex:','mailto:')):continue
  path,_,anchor=target.partition('#');path=re.sub(r':\d+$','',path);q=p.parent/path if path else p
  if q==out:continue
  if not q.exists():issues.append((str(p.relative_to(b)),target));continue
  if anchor and q.suffix=='.md' and anchor not in anchors(q):issues.append((str(p.relative_to(b)),target,'anchor'))
  links+=1
 # Catch current hand-written decision/validation table width drift.
 if p.name in ['DECISIONS.md','VALIDATION.md','LOCAL_REUSE_DECISIONS.md']:
  width=None
  for n,line in enumerate(p.read_text().splitlines(),1):
   if line.startswith('|'):
    w=len(re.split(r'(?<!\\)\|',line))
    if width is None:width=w
    elif w!=width:tableissues.append((p.name,n,width,w))
   else:width=None
assert not issues,issues;assert not tableissues,tableissues
jsoncount=0
for p in b.rglob('*.json'):json.loads(p.read_text());jsoncount+=1
dec=set(re.findall(r'^\| (D-\d+) \|',(s/'DECISIONS.md').read_text(),re.M));gates=set(re.findall(r'^\| (Q-\d+) ',(s/'DECISIONS.md').read_text(),re.M));assert len(dec)==63 and len(gates)==9
critic_hashes=[]
for fn in ['CRITIC_REVIEW.md','INTEGRATION_CRITIC.md']:
 p=s/fn
 assert '<!-- REVIEW_HASH_TABLE -->' not in p.read_text(), (fn, 'unfinished hash appendix')
 before=len(critic_hashes)
 for ref,digest in re.findall(r'^\| \[[^\]]+\]\(([^)]+)\) \| `([0-9a-f]{64})` \|$',p.read_text(),re.M):
  q=p.parent/ref;assert sha(q)==digest,(fn,ref,'critic hash stale');critic_hashes.append((fn,ref))
 assert len(critic_hashes)>before, (fn, 'missing final snapshot hashes')
report=dict(date='2026-09-08',version='1.1-consolidated-specification',scope='Mechanical document traceability, preserved source/archives and instruction integrity; no Beta runtime certification',checks=dict(unique_requirements=181,source_register_hashes=len(d['source_order']),source_records_preserved_exactly=source_records,prior150_requirement_records_and_phase_mappings_unchanged=True,prior_coverage_only_revision="RQ-123 validation denominator150→181; prior archive unchanged",coverage_rows_exact_text_and_hash=181,ordered_production_phases=15,selected_decisions=len(dec),qualification_gates=len(gates),research_requirements=181,research_topic_groups=35,new_research_dossiers=9,new_research_words=rc['dossier_word_count'],new_research_cases_defined_not_run=48,mapped_cross_domain_cases_defined_not_run=len(cases),syn_chains_defined_not_run=22,archive_editions=len(arch),archived_files_hash_and_size_verified=archivecount,frozen_spec10_files_unchanged=frozen,original_source_files_unchanged_and_zip_preserved=len(iv['files']),local_source_fingerprints_checked=len(source_files),ecosystem_target_hashes_match=len(em['changes']),json_files_parsed=jsoncount,local_markdown_targets_checked=links,distinct_external_links_in_checked_documents=len(external),local_link_anchor_table_issues=[],critic_input_snapshot_hash_rows_checked_by_coordinator=len(critic_hashes)),document_sha256={str(p.relative_to(b)):sha(p) for p in sorted(set(md+list(s.glob('*.json'))+[research/'RESEARCH_COVERAGE.json'])) if p!=out},verification_scripts={str(p.relative_to(b)):sha(p) for p in sorted((b/'tools').glob('verify_*.py'))},limitations=['All Beta implementation/import/native art/animation/audio/playtest/performance/export and qualification scenarios remain unrun.','One source-only LifeFormzFable continuation test passed; this does not qualify a Beta adaptation or the entire source suite.','Research evidence has bounded source scopes and access limitations; external URLs are inventoried here, not all refetched by this script.','Constraint checks and witnesses do not prove human comprehension, fun or all seed/permutation outcomes.','Skill hashes/frontmatter establish synchronized instruction integrity, not a measured model-performance benefit.','Independent critics delivered substantive reviews and verified corrections before an agent usage-limit interruption. Final snapshot hashes and packaging were completed by the coordinator; they do not imply another independent review or approve future edits.'])
out.write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report['checks'],indent=2))
