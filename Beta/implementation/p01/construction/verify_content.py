#!/usr/bin/env python3
"""Independent source and integer geometry checks; stdout evidence only, no C# execution."""
import hashlib
import itertools
import json
from pathlib import Path
import struct
import zipfile

ROOT = Path(__file__).resolve().parents[3]
CONTENT = ROOT / 'Runtime/content'
manifest_path = CONTENT / 'construction-farm/admission-v1.json'
raw = manifest_path.read_bytes()
manifest = json.loads(raw)
assert hashlib.sha256(raw).hexdigest() == '6bce3173a4f5aadc1378cd69b687b74b9af54243221c33183299a686d0da6f0e'
for witness in manifest['sourceWitnesses']:
    assert hashlib.sha256((CONTENT / witness['path']).read_bytes()).hexdigest() == witness['sha256']
world_path = CONTENT / manifest['sourceWitnesses'][0]['path']
world = json.loads(world_path.read_bytes())
farm = world['regions'][18]
assert farm == dict(id='farm_verdance_small', x=100, y=320, w=9, h=7)
assert hashlib.sha256(json.dumps(farm, sort_keys=True, separators=(',', ':')).encode()).hexdigest() == manifest['sourceFarm']['payloadSha256']
def tile(x,y):
    row=world['rows'][y]
    cursor=0
    for value,count in zip(row[::2],row[1::2]):
        if cursor <= x < cursor+count:
            return value
        cursor += count
    raise AssertionError('incomplete source row')
assert tile(106,324)==27 and tile(106,325)==0 and tile(104,323)==24
for x,y in manifest['construction']['gateCells']+manifest['construction']['exitCells']:
    assert tile(x,y)==0
items=json.loads((CONTENT/'legacy/aldermere-first-loop/source-support/data/items.json').read_bytes())['items']
assert not any(any(word in item.get('name','').lower() for word in ['saw','chisel','mallet','hammer','joinery']) for item in items)
assert items[17]['id']=='copper_axe' and items[17]['tool']=='Axe'
with zipfile.ZipFile(ROOT/'content-migration/original-source-2026-09-08.zip') as archive:
    path=next(name for name in archive.namelist() if name.endswith('src/Client/WorldRenderer.cs'))
    assert archive.read(path)==(CONTENT/'construction-farm/source-support/WorldRenderer.cs.txt').read_bytes()
atlas=(CONTENT/manifest['presentation']['atlas']).read_bytes()
assert atlas[:8]==b'\x89PNG\r\n\x1a\n'
assert struct.unpack('>II',atlas[16:24])==(256,144)
boxes=manifest['construction']['membersMillimetres']
def overlaps(a,b):
    return all(a[i]<b[i+3] and a[i+3]>b[i] for i in range(3))
assert len(boxes)==10 and not any(overlaps(a,b) for a,b in itertools.combinations(boxes,2))
volume=sum((b[3]-b[0])*(b[4]-b[1])*(b[5]-b[2]) for b in boxes)
assert volume==540000 and 6*50000==270000+3*10000
# Independently require connected positive-area contact, not rounded collision overlap.
def face(a,b):
    return any((a[i+3]==b[i] or a[i]==b[i+3]) and all(a[j]<b[j+3] and a[j+3]>b[j] for j in range(3) if j!=i) for i in range(3))
seen={0}
while True:
    new=seen|{j for i in seen for j in range(len(boxes)) if face(boxes[i],boxes[j])}
    if new==seen: break
    seen=new
assert len(seen)==10 and min(b[2] for b in boxes)==0
origin=manifest['construction']['siteSubunits']
collision=[[*[origin[i]+b[i]*256//1000 for i in range(3)],*[origin[i]-(-b[i+3]*256//1000) for i in range(3)]] for b in boxes]
envelope=[*[min(b[i] for b in collision) for i in range(3)],*[max(b[i+3] for b in collision) for i in range(3)]]
assert envelope==[27213,83056,256,27316,83089,319]
def body(p): return [p[0]-90,p[1]-70,p[2],p[0]+90,p[1]+70,p[2]+464]
def clear(a,b,obstacles):
    av,bv=body(a),body(b)
    sweep=[*[min(av[i],bv[i]) for i in range(3)],*[max(av[i+3],bv[i+3]) for i in range(3)]]
    assert (a[0]==b[0]) != (a[1]==b[1])
    assert a[2]==b[2]==256 and abs(a[0]-b[0])+abs(a[1]-b[1])<=256
    # Complete admitted flat region: z0 is solid support and cellsz1..15 are void.
    assert sweep[0]>=96*256 and sweep[3]<=128*256 and sweep[1]>=320*256 and sweep[4]<=352*256
    assert sweep[2]==256 and sweep[5]<=16*256
    return not any(overlaps(sweep,o) for o in obstacles)
paths=[]
for x in (104,105):
    # Fromwork106,325, travelwest alonggrass325 and south through exactsourcegap326 tooutside327.
    cells=[(106,325),*[(i,325) for i in range(105,x-1,-1)],(x,326),(x,327)]
    positions=[(cx*256+128,cy*256+128,256) for cx,cy in cells]
    assert all(tile(cx,cy)==0 for cx,cy in cells)
    for obstacles in ([],collision):
        assert all(clear(a,b,obstacles) for a,b in zip(positions,positions[1:]))
    paths.append(cells)
def frame(tag,*fields):
    fields=(tag.encode(),)+fields
    return b'OFC\0'+struct.pack('>II',1,len(fields))+b''.join(struct.pack('>I',len(value))+value for value in fields)
def generated(seed,domain,address,role):
    return hashlib.sha256(frame('entity.generated',seed,domain.encode(),address.encode(),role.encode(),struct.pack('>Q',0))).digest()[:16]
keys=[manifest['tool']['definitionKey'],manifest['tool']['qualificationKey'],manifest['installedItem']['definitionKey'],manifest['installedMaterial']['definitionKey'],manifest['materialTransform']['definitionKey'],manifest['materialTransform']['physicalDefinitionKey'],manifest['construction']['definitionKey'],manifest['construction']['workUnitKey']]
definition_ids={key:generated(b'openfantasy.authored-content','construction-farm.definitions.v1',key,'definition').hex() for key in keys}
seed=hashlib.sha256(frame('farm-work.seed.v1',b'river')).hexdigest().encode()
world_id=generated(seed,'native-farm.instances.v1','world','instance')
branch_id=generated(seed,'native-farm.instances.v1','branch.main','instance')
scope=frame('construction-farm.instance-scope',world_id,branch_id)
roles=[manifest['tool']['sourceRole'],manifest['tool']['lotRole'],manifest['construction']['projectRole']]
instance_ids={role:generated(scope,'construction-farm.instances.v1',role,'instance').hex() for role in roles}
assert len(set(definition_ids.values())|set(instance_ids.values()))==11
print(json.dumps({'scope':'Independent Python static source and full-body geometry, no C# or live occupancy execution',
    'manifestSha256':hashlib.sha256(raw).hexdigest(),'definitionIds':definition_ids,'riverInstanceIds':instance_ids,'sourceWitnesses':len(manifest['sourceWitnesses']),
    'physicalMembers':len(boxes),'physicalVolumeMicrolitres':volume,'inputMassMilligrams':300000,
    'guardMassMilligrams':270000,'offcutMassMilligrams':30000,'collisionEnvelopeSubunits':envelope,
    'protectedStaticPathsBeforeAndAfter':paths,'bodySubunits':[180,140,464],
    'limitation':'Current actors, terrain edits and search-budget admission are checked by runtime, not inferred from this static layout.'},indent=2))
