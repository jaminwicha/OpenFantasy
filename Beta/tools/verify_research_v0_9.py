"""Verify the dated Beta research 0.9 ledger; see README.md."""
from pathlib import Path
import json,re,hashlib
b=Path(__file__).resolve().parents[1]/'research-v0_9';d=json.loads((b/'RESEARCH_COVERAGE.json').read_text());old=json.loads((b.parent/'research-v0_8/RESEARCH_COVERAGE.json').read_text());req=json.loads((b.parent/'spec-v1_1/REQUIREMENTS.json').read_text());rm={x['id']:x for x in req['requirements']};rr={x['id']:x for x in d['requirements']};tm={x['topic']:x for x in d['topics']}
assert len(rr)==len(d['requirements'])==d['requirement_count']==req['requirement_count']
assert set(rr)==set(rm)
assert len(tm)==len(d['topics'])==d['topic_count']
for x in d['requirements']:
 assert x['requirement']==rm[x['id']]['requirement'],x['id']
 assert x['requirement_sha256']==hashlib.sha256(x['requirement'].encode()).hexdigest(),x['id']
 assert set(x['topics'])=={t['topic'] for t in d['topics'] if x['id'] in t['requirements']},x['id']
 assert x['topics']
for t in d['topics']:
 assert len(t['requirements'])==len(set(t['requirements'])),t['topic']
 for rid in t['requirements']:assert rid in rr and t['topic'] in rr[rid]['topics'],rid
 for a in t['artifacts']:assert (b/a).exists(),(t['topic'],a)
for i,t in enumerate(old['topics']):
 n=d['topics'][i];assert t['topic']==n['topic'];assert set(t['requirements']).issubset(n['requirements'])
 for k in ['application','retained_proofs','evidence_limit']:assert t[k]==n[k],(t['topic'],k)
for x in old['requirements']:assert x['requirement_sha256']==rr[x['id']]['requirement_sha256'],x['id']
assert len(d['dossiers'])==d['dossier_count']
assert sum(x['word_count'] for x in d['dossiers'])==d['dossier_word_count']
for x in d['dossiers']:
 f=b/x['artifact'];assert len(f.read_text().split())==x['word_count'],f
 assert hashlib.sha256(f.read_bytes()).hexdigest()==x['content_sha256'],f
cases=d['retained_cases'];assert len(cases)==len({x['case_id'] for x in cases})
assert set(x['case_id'] for x in cases)=={cid for x in d['dossiers'] for cid in x['retained_case_ids']}
for x in cases:assert (b/x['artifact']).exists() and not x['evidence_paths'] and x['status'].startswith('Unrun')
counts=d['case_counts'];assert counts['new_total_unrun_beta_cases']==len(cases)
assert counts['new_web_informed_cases']==sum(x['evidence_origin']=='web' for x in cases)
assert counts['new_local_adaptation_cases']==sum(x['evidence_origin']=='local' for x in cases)
assert len(d['executed_evidence'])==1 and counts['executed_local_source_tests']==1 and counts['executed_beta_runtime_cases']==0
for a in re.findall(r'\]\(([^)]+)\)',(b/'README.md').read_text()):
 if not a.startswith(('https://','http://','#')):assert (b/a.split('#')[0]).exists(),a
print('PASS structural traceability only:',len(rr),'exact texts/hashes;',len(tm),'bidirectional topics;25 original groups and150 hashes preserved;',len(d['dossiers']),'dossier hashes/',d['dossier_word_count'],'words;',len(cases),'unique unrun cases;1 local source test;0 Beta runtime tests;all links resolve.')
print('Current status:',d.get('status'))
