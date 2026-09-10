"""Independent profile-three action/step and migration-request framing; stdout only."""
import hashlib
import json
import struct
from pathlib import Path


def n(value): return struct.pack('>Q', value)
def identity(value): return value.to_bytes(16, 'big')
def digest(value): return hashlib.sha256(value).digest()


def frame(tag, *fields):
    fields = (tag.encode(),) + fields
    return b'OFC\0' + struct.pack('>II', 1, len(fields)) + b''.join(struct.pack('>I', len(x)) + x for x in fields)


def parts(value):
    output=[]
    offset=12
    for _ in range(struct.unpack('>I',value[8:12])[0]):
        length=struct.unpack('>I',value[offset:offset+4])[0]
        offset+=4
        output.append(value[offset:offset+length])
        offset+=length
    assert offset==len(value)
    return output


def replace(value,index,field):
    fields=parts(value)
    fields[index]=field
    return frame(fields[0].decode(),*fields[1:])


prior_path=Path(__file__).with_name('production-ledger-v1.json')
prior=json.loads(prior_path.read_bytes())
empty=bytes.fromhex(prior['empty']['hex'])
recipe_hash=bytes.fromhex(prior['recipe']['sha256'])
source=digest(b'wire-source-admission')
head=digest(b'wire-source-head')
target=digest(frame('world.production-admission',n(1),source,empty))


def ingredient(kind,lot,revision,quantity):
    return frame('world.craft.ingredient',n(kind),identity(lot),n(revision),n(quantity))


def action(command=6000,actor=10,selections=None):
    selections=selections if selections is not None else [ingredient(1,70,3,2),ingredient(1,72,4,1)]
    intent=frame('world.action.craft',n(1),identity(5202),recipe_hash,n(5),identity(60),n(7),frame('world.craft.ingredients',*selections))
    return frame('world.actor-action',n(3),identity(command),identity(20),identity(30),identity(actor),identity(1),identity(2),n(9),intent)


raw=action()
derived=action(6001,11,[ingredient(2,9000,6,1),ingredient(1,9001,0,1)])
movement=frame('world.control-sample',identity(20),identity(30),identity(10),struct.pack('>q',-32),struct.pack('>q',0))
second_movement=frame('world.control-sample',identity(21),identity(31),identity(11),struct.pack('>q',0),struct.pack('>q',32))


def batch(actions=(raw,derived),moves=(movement,second_movement),kind=1):
    return frame('world.step-batch',n(3),identity(7000),identity(1),identity(2),n(80),n(77),n(kind),
                 frame('world.step-movement',*moves),frame('world.step-actions',*actions))


migration=frame('world.production-migration',n(1),identity(8000),identity(1),identity(2),n(80),n(77),head,source,target,empty)
malformed=[]
def bad(kind,name,value): malformed.append({'kind':kind,'name':name,'hex':value.hex()})
for kind,value in [('action',raw),('batch',batch()),('migration',migration)]:
    bad(kind,'trailing',value+b'\0')
    bad(kind,'wrong-tag',replace(value,0,b'unknown.tag'))
    bad(kind,'framing-version',value[:7]+b'\x02'+value[8:])
    bad(kind,'contract-version',replace(value,1,n(99)))
    bad(kind,'too-many-fields',value[:8]+b'\xff\xff\xff\xff'+value[12:])
for name,selections in [
    ('empty-selections',[]),('zero-quantity',[ingredient(1,70,0,0)]),('unknown-family',[ingredient(3,70,0,1)]),
    ('duplicate-lot',[ingredient(1,70,0,1),ingredient(2,70,0,1)]),
    ('unordered-lots',[ingredient(1,72,0,1),ingredient(1,70,0,2)]),
    ('too-many-selections',[ingredient(1,9000+i,0,1) for i in range(65)])]: bad('action',name,action(selections=selections))
bad('batch','unordered-actions',batch(actions=(derived,raw)))
bad('batch','duplicate-actor',batch(actions=(raw,action(6002))))
bad('batch','unordered-movement',batch(moves=(second_movement,movement)))
bad('batch','duplicate-movement',batch(moves=(movement,movement)))
bad('batch','held-movement',batch(kind=2))
bad('batch','empty-held',batch(actions=(),moves=(),kind=2))
bad('batch','unknown-kind',batch(kind=3))
bad('batch','action-scope-mismatch',batch(actions=(replace(raw,6,identity(99)),)))
bad('migration','wrong-target-admission',replace(migration,9,digest(b'wrong')))
bad('migration','wrong-component-scope',replace(migration,3,identity(99)))
bad('migration','advanced-empty-ledger',replace(migration,10,replace(empty,4,n(1))))
bad('migration','populated-ledger',replace(migration,10,bytes.fromhex(prior['crafted']['hex'])))
bad('migration','steps-greater-than-revision',replace(migration,6,n(81)))
bad('migration','cause-alias-world',replace(migration,2,identity(1)))


def vector(value): return {'hex':value.hex(),'sha256':digest(value).hex(),'bytes':len(value)}
print(json.dumps({'contract':'world-production-v1','dependencies':{'productionLedgerSha256':digest(prior_path.read_bytes()).hex()},
                  'sourceAdmission':source.hex(),'expectedHead':head.hex(),'targetAdmission':target.hex(),
                  'rawAction':vector(raw),'derivedAction':vector(derived),'advancingBatch':vector(batch()),
                  'heldBatch':vector(batch(actions=(raw,),moves=(),kind=2)),'migration':vector(migration),
                  'malformed':malformed},indent=2))
