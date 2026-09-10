"""Independent finite construction wire oracle. Standard library only; JSON to stdout.

This reproduces the source-free test calibration, not native content admission.
It never reads C# or an expected fixture, and performs its own physical-volume proof.
"""
import hashlib
import json
import struct


def n(value):
    return struct.pack('>Q', value)


def signed(value):
    return struct.pack('>q', value)


def identity(value):
    return value if isinstance(value, bytes) else (10000 + value).to_bytes(16, 'big')


def frame(tag, *values):
    values = (tag.encode('utf-8'),) + values
    return b'OFC\0' + struct.pack('>II', 1, len(values)) + b''.join(struct.pack('>I', len(v)) + v for v in values)


def fields(value):
    offset = 12
    result = []
    for _ in range(struct.unpack('>I', value[8:12])[0]):
        length = struct.unpack('>I', value[offset:offset + 4])[0]
        offset += 4
        result.append(value[offset:offset + length])
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


pin = h(b'construction-fixture')
package = b'construction-fixture'
keys = [b'physical_wood', b'physical_guard', b'physical_offcuts']
materials = [frame('production.material', n(1), package, identity(10+i), key, n(mass), n(volume), n(1))
             for i, (key, mass, volume) in enumerate(zip(keys, [50000, 270000, 10000], [100000, 540000, 25000]))]
physical_pins = [(identity(10+i), h(value)) for i, value in enumerate(materials)]
item_pins = [(identity(20+i), pin) for i in range(3)]
terms = [frame('production.recipe-term', n(1), n(role), *physical_pins[i], n(count))
         for i, (role, count) in enumerate([(1, 6), (2, 1), (3, 3)])]
physical = frame('production.recipe', n(1), package, identity(30), b'physical_guard_assembly', n(1), b'',
                 frame('production.material-catalog', n(1), *materials), frame('production.recipe-terms', n(1), *terms))
recipe = frame('craft.recipe', n(1), identity(31), physical,
               frame('craft.bindings', *(frame('craft.item-physical', n(1), *item_pins[i], *physical_pins[i]) for i in range(3))),
               frame('craft.allocations', *(frame('craft.output-allocation', n(1), *item_pins[0], n(index), n(mass))
                                           for index, mass in [(0, 270000), (1, 10000)])))
tool = frame('construction.tool', n(1), identity(40), identity(41), pin, n(1))
member_coordinates = sorted([
    (0, 0, 0, 75, 5, 10), (85, 0, 0, 315, 5, 10), (325, 0, 0, 400, 5, 10), (0, 0, 10, 400, 5, 246),
    (75, -60, 0, 85, 65, 10), (315, -60, 0, 325, 65, 10), (75, -60, 10, 85, 0, 20),
    (75, 5, 10, 85, 65, 20), (315, -60, 10, 325, 0, 20), (315, 5, 10, 325, 65, 20)])
assert sum((d-a)*(e-b)*(f-c) for a,b,c,d,e,f in member_coordinates) == 540000
assert 6*50000 == 270000 + 3*10000
members = [frame('construction.member', *(signed(v) for v in box)) for box in member_coordinates]
rate = frame('construction.rate', n(1), identity(60), pin, n(1), n(1800))
definition = frame('construction.definition', n(1), identity(50), b'construction/guard', b'Seedling guard', recipe,
                   n(0), tool, frame('construction.members', *members), n(200), rate, n(1))


def position(x, y, z):
    return frame('construction.position', signed(x), signed(y), signed(z))


site = position(660, -256, 256)
work = position(512, 0, 256)
access_rows = [frame('construction.access-route', identity(2), work, position(768, 256, 256)),
               frame('construction.access-route', identity(2), work, position(768, 512, 256))]
access = frame('construction.access', *access_rows)


def progress(units):
    return frame('exposure.progress.v1', identity(70), identity(60), pin, n(1), n(1800), n(0), n(units*1800), n(units), n(0))


installed = frame('construction.installed', spawned(90, 3), spawned(90, 1), n(0), spawned(90, 0), identity(90))


def project(units=0):
    return frame('construction.project', n(1), identity(70), identity(50), h(definition), identity(2), identity(2),
                 identity(71), identity(72), identity(73), site, work, access, n(units), progress(units),
                 installed if units == 200 else b'', n(1))


def snapshot(units=0, project_bytes=None):
    return frame('construction.snapshot', n(1), identity(1), identity(3), n(units),
                 frame('construction.definitions', definition), frame('construction.projects', project_bytes or project(units)))


fresh, partial, completed = snapshot(), snapshot(40), snapshot(200)
action = frame('world.actor-action', n(5), identity(90), identity(91), identity(92), identity(2), identity(1), identity(3), n(8),
               frame('world.action.construction', n(1), identity(70), n(40), n(7), identity(80), n(0)))
action2 = replace(replace(action, 2, identity(95)), 5, identity(4))
movement = frame('world.control-sample', identity(91), identity(92), identity(2), signed(-32), signed(0))
batch = frame('world.step-batch', n(5), identity(96), identity(1), identity(3), n(50), n(45), n(1),
              frame('world.step-movement', movement), frame('world.step-actions', action, action2))
supply = frame('world.construction-tool-supply', n(1), identity(2), identity(74), identity(81), identity(41), pin, n(1), identity(80), n(1))
admission = frame('world.construction-admission', n(1), pin, fresh, frame('world.construction-tools', supply))
source_pin = h(b'construction-source-profile')
target_pin = h(frame('world.construction-admission-version', n(1), source_pin, admission))
migration = frame('world.construction-migration', n(1), identity(97), identity(1), identity(3), n(123), n(100),
                  h(b'construction-source-head'), source_pin, target_pin, admission)
malformed = []


def bad(kind, name, value):
    malformed.append({'kind': kind, 'name': name, 'hex': value.hex()})


for kind, value in [('definition', definition), ('snapshot', partial), ('action', action), ('batch', batch), ('admission', admission), ('migration', migration)]:
    bad(kind, 'wrong-magic', b'BAD\0' + value[4:])
    bad(kind, 'framing-version', value[:7] + b'\x02' + value[8:])
    bad(kind, 'unknown-contract-version', replace(value, 1, n(99)))
    bad(kind, 'trailing-byte', value + b'\0')
    bad(kind, 'excessive-field-count', value[:8] + struct.pack('>I', 0xffffffff) + value[12:])
bad('definition', 'unknown-tool-operation', replace(definition, 7, replace(tool, 5, n(99))))
bad('definition', 'duplicate-member', replace(definition, 8, frame('construction.members', *members, members[0])))
bad('definition', 'noncanonical-member-order', replace(definition, 8, frame('construction.members', *reversed(members))))
bad('definition', 'missing-physical-member', replace(definition, 8, frame('construction.members', *members[1:])))
bad('definition', 'unknown-stock-interpretation', replace(definition, 11, n(0)))
bad('definition', 'work-limit', replace(definition, 9, n(4097)))
bad('definition', 'unsupported-rate', replace(definition, 10, replace(rate, 5, n(1799))))
bad('definition', 'waste-installed-as-product', replace(definition, 6, n(1)))
bad('snapshot', 'scope-alias', replace(partial, 3, identity(1)))
bad('snapshot', 'snapshot-project-revision-mismatch', replace(partial, 4, n(39)))
bad('snapshot', 'project-pin-substitution', snapshot(40, replace(project(40), 4, pin)))
bad('snapshot', 'wrong-progress-subject', snapshot(40, replace(project(40), 14, replace(progress(40), 1, identity(2)))))
bad('snapshot', 'unearned-progress', snapshot(40, replace(project(40), 14, replace(progress(40), 7, n(0)))))
bad('snapshot', 'premature-installation', snapshot(40, replace(project(40), 15, installed)))
bad('snapshot', 'missing-final-installation', snapshot(200, replace(project(200), 15, b'')))
bad('snapshot', 'forged-installed-lot', snapshot(200, replace(project(200), 15, replace(installed, 2, identity(98)))))
bad('snapshot', 'unsupported-installed-lot-revision', snapshot(200, replace(project(200), 15, replace(installed, 3, n(1)))))
bad('snapshot', 'unsupported-stock-location', snapshot(40, replace(project(40), 16, n(2))))
bad('snapshot', 'duplicate-project', replace(partial, 6, frame('construction.projects', project(40), project(40))))
bad('snapshot', 'noncanonical-access-order', snapshot(40, replace(project(40), 12, frame('construction.access', *reversed(access_rows)))))
bad('action', 'unknown-intent-version', replace(action, 9, replace(fields(action)[9], 1, n(2))))
bad('action', 'identity-width', replace(action, 2, b'x'))
bad('batch', 'duplicate-actor', replace(batch, 9, frame('world.step-actions', action, action)))
bad('batch', 'action-order', replace(batch, 9, frame('world.step-actions', action2, action)))
bad('batch', 'held-movement', replace(batch, 7, n(2)))
bad('batch', 'unsupported-movement', replace(batch, 8, frame('world.step-movement', replace(movement, 4, signed(1)))))
bad('batch', 'wrong-command-scope', replace(batch, 9, frame('world.step-actions', replace(action, 6, identity(99)))))
bad('admission', 'nonfresh-progress', replace(admission, 3, partial))
bad('admission', 'missing-tool', replace(admission, 4, frame('world.construction-tools')))
bad('admission', 'fake-tool-budget', replace(admission, 4, frame('world.construction-tools', replace(supply, 7, n(2)))))
bad('admission', 'missing-carrying-allowance', replace(admission, 4, frame('world.construction-tools', replace(supply, 9, n(0)))))
bad('migration', 'target-pin-substitution', replace(migration, 9, pin))
bad('migration', 'steps-exceed-revision', replace(migration, 6, n(124)))


def vector(kind, value):
    return {'kind': kind, 'hex': value.hex(), 'sha256': h(value).hex(), 'bytes': len(value)}


print(json.dumps({'contract': 'construction-v1', 'definition': vector('definition', definition),
                  'fresh': vector('snapshot', fresh), 'partial': vector('snapshot', partial),
                  'completed': vector('snapshot', completed), 'action': vector('action', action),
                  'batch': vector('batch', batch), 'admission': vector('admission', admission),
                  'migration': vector('migration', migration), 'targetAdmissionVersion': target_pin.hex(),
                  'malformed': malformed}, indent=2))
