"""Independent OFC/mass/lineage vectors. Pure Python stdout; never reads C# or overwrites expected fixtures."""
import hashlib
import json
import struct


def n(value):
    return struct.pack('>Q', value)


def identity(value):
    return value if isinstance(value, bytes) else value.to_bytes(16, 'big')


def frame(tag, *fields):
    fields = (tag.encode(),) + fields
    return b'OFC\0' + struct.pack('>II', 1, len(fields)) + b''.join(struct.pack('>I', len(v)) + v for v in fields)


def fields(value):
    offset = 12
    result = []
    for _ in range(struct.unpack('>I', value[8:12])[0]):
        length = struct.unpack('>I', value[offset:offset+4])[0]
        offset += 4
        result.append(value[offset:offset+length])
        offset += length
    assert offset == len(value)
    return result


def replace(value, index, field):
    parts = fields(value)
    parts[index] = field
    return frame(parts[0].decode(), *parts[1:])


def h(value):
    return hashlib.sha256(value).digest()


def spawned(command, ordinal):
    return h(frame('entity.spawned', identity(command), n(ordinal)))[:16]


package = b'openfantasy.crafting-tests'
source_hash = bytes.fromhex('c384027c76855ade482105233cadd07ea930a265f73e12beab852de1cc756491')
keys = ['berries', 'wood', 'rations', 'offcuts']
item_pins = [(identity(5001+i), h(('craft-test-'+key).encode())) for i, key in enumerate(keys)]
materials = [frame('production.material', n(1), package, identity(5101+i), key.encode(), n(mass), n(volume), n(hardness))
             for i, (key, mass, volume, hardness) in enumerate(zip(keys, [20000,50000,40000,10000], [30000,100000,45000,25000], [1,8,2,0]))]
physical_pins = [(identity(5101+i), h(value)) for i, value in enumerate(materials)]


def term(role, index, count):
    return frame('production.recipe-term', n(1), n(role), *physical_pins[index], n(count))


def physical_recipe(recipe_id, key, indexes, terms, source=b''):
    return frame('production.recipe', n(1), package, identity(recipe_id), key.encode(), n(1), source,
                 frame('production.material-catalog', n(1), *(materials[i] for i in indexes)),
                 frame('production.recipe-terms', n(1), *terms))


def craft_recipe(recipe_id, physical, indexes, allocations):
    return frame('craft.recipe', n(1), identity(recipe_id), physical,
                 frame('craft.bindings', *(frame('craft.item-physical', n(1), *item_pins[i], *physical_pins[i]) for i in indexes)),
                 frame('craft.allocations', *(frame('craft.output-allocation', n(1), *item_pins[i], n(out), n(mass)) for out, i, mass in allocations)))


physical = physical_recipe(5201, 'craft_rations', range(4), [term(1,0,2),term(1,1,1),term(2,2,2),term(3,3,1)], source_hash)
recipe = craft_recipe(5202, physical, range(4), [(0,0,20000),(0,1,20000),(1,1,10000)])
repack = craft_recipe(5204, physical_recipe(5203, 'repack', [2], [term(1,2,1),term(2,2,1)]), [2], [(0,2,40000)])


def raw(source, start, end):
    return frame('production.source-claim', n(1), identity(source), n(start), n(end))


def output(batch, index, start, end):
    return frame('production.output-claim', n(1), identity(batch), n(index), n(start), n(end))


def retired(kind, lot, revision, item, claim):
    return frame('production.ingredient', n(1), n(kind), identity(lot), n(revision), *item_pins[item], claim)


def batch(command, recipe_id, recipe_bytes, ingredients):
    return frame('production.batch', n(1), spawned(command,0), identity(command), identity(10), identity(60),
                 identity(recipe_id), h(recipe_bytes), frame('production.retired', *sorted(ingredients, key=lambda value: fields(value)[3])))


def lot(lot_id, item, claim, revision=0):
    return frame('production.lot', n(1), identity(lot_id), identity(60), *item_pins[item], claim, n(revision), n(0), b'')


def snapshot(revision, recipes, batches, lots):
    return frame('production.snapshot', n(1), identity(1), identity(2), n(revision),
                 frame('production.recipes', *sorted(recipes, key=lambda value: fields(value)[2])),
                 frame('production.batches', *sorted(batches, key=lambda value: fields(value)[2])),
                 frame('production.lots', *sorted(lots, key=lambda value: fields(value)[2])))


first_batch = batch(6000,5202,recipe,[retired(1,70,0,0,raw(71,0,2)),retired(1,72,0,1,raw(73,0,1))])
first_lot = lot(spawned(6000,1),2,output(spawned(6000,0),0,0,2))
waste_lot = lot(spawned(6000,2),3,output(spawned(6000,0),1,0,1))
second_batch = batch(6001,5204,repack,[retired(2,spawned(6000,1),0,2,output(spawned(6000,0),0,0,1))])
remainder = lot(spawned(6000,1),2,output(spawned(6000,0),0,1,2),1)
second_lot = lot(spawned(6001,1),2,output(spawned(6001,0),0,0,1))
empty = snapshot(0,[recipe],[],[])
crafted = snapshot(1,[recipe],[first_batch],[first_lot,waste_lot])
chained = snapshot(2,[recipe,repack],[first_batch,second_batch],[remainder,waste_lot,second_lot])
malformed = []


def bad(name, value):
    malformed.append({'name':name,'hex':value.hex()})


bad('wrong-magic',b'BAD\0'+crafted[4:])
bad('framing-version',crafted[:7]+b'\x02'+crafted[8:])
bad('trailing',crafted+b'\0')
bad('contract-version',replace(crafted,1,n(2)))
bad('scope-alias',replace(crafted,3,identity(1)))
bad('missing-recipe',replace(crafted,5,frame('production.recipes')))
bad('duplicate-recipe',replace(crafted,5,frame('production.recipes',recipe,recipe)))
bad('recipe-order',replace(chained,5,frame('production.recipes',repack,recipe)))
bad('duplicate-batch',replace(crafted,6,frame('production.batches',first_batch,first_batch)))
bad('duplicate-lot',snapshot(1,[recipe],[first_batch],[first_lot,first_lot,waste_lot]))
bad('discarded-waste',snapshot(1,[recipe],[first_batch],[first_lot]))
bad('forged-batch-id',snapshot(1,[recipe],[replace(first_batch,2,identity(8000))],[first_lot,waste_lot]))
bad('unknown-protection',snapshot(1,[recipe],[first_batch],[replace(first_lot,8,n(16)),waste_lot]))
bad('invalid-reservation-width',snapshot(1,[recipe],[first_batch],[replace(first_lot,9,b'x'),waste_lot]))
bad('absent-source-batch',snapshot(1,[recipe],[first_batch],[replace(first_lot,6,output(8000,0,0,2)),waste_lot]))
bad('excess-output',snapshot(1,[recipe],[first_batch],[replace(first_lot,6,output(spawned(6000,0),0,0,3)),waste_lot]))
bad('unknown-output-index',snapshot(1,[recipe],[first_batch],[replace(first_lot,6,output(spawned(6000,0),64,0,2)),waste_lot]))
bad('wrong-product-pin',snapshot(1,[recipe],[first_batch],[replace(first_lot,5,h(b'wrong')),waste_lot]))
original_inputs=fields(fields(first_batch)[8])[1:]
for name, altered in [('unknown-ingredient-kind',replace(original_inputs[0],2,n(9))),
                      ('empty-retirement',replace(original_inputs[0],7,raw(71,0,0))),
                      ('wrong-input-pin',replace(original_inputs[0],6,h(b'wrong')))]:
    changed=replace(first_batch,8,frame('production.retired',altered,original_inputs[1]))
    bad(name,snapshot(1,[recipe],[changed],[first_lot,waste_lot]))
allocation_values=fields(fields(recipe)[5])[1:]
wrong_allocation=replace(allocation_values[0],5,n(19000))
wrong_recipe=replace(recipe,5,frame('craft.allocations',wrong_allocation,*allocation_values[1:]))
bad('underallocated-input',snapshot(1,[wrong_recipe],[first_batch],[first_lot,waste_lot]))


def vector(value):
    return {'hex':value.hex(),'sha256':h(value).hex(),'bytes':len(value)}


print(json.dumps({'contract':'production-ledger-v1','recipe':vector(recipe),'repackRecipe':vector(repack),
                  'empty':vector(empty),'crafted':vector(crafted),'chained':vector(chained),'malformed':malformed},indent=2))
