"""Independent world action/step wire oracle. Qualification inputs, stdout only; no runtime imports."""
import hashlib
import json
import struct

U64_MAX = (1 << 64) - 1

def identity(value):
    return ((0x1020304050607080 << 64) | value).to_bytes(16, 'big')

def number(value):
    return struct.pack('>Q', value)

def signed(value):
    return struct.pack('>q', value)

def frame_fields(fields):
    return b'OFC\0' + struct.pack('>II', 1, len(fields)) + b''.join(struct.pack('>I', len(v)) + v for v in fields)

def frame(tag, *fields):
    return frame_fields([tag.encode('ascii'), *fields])

def fields(encoded):
    count, = struct.unpack_from('>I', encoded, 8)
    offset, result = 12, []
    for _ in range(count):
        size, = struct.unpack_from('>I', encoded, offset)
        offset += 4
        result.append(encoded[offset:offset + size])
        offset += size
    assert offset == len(encoded)
    return result

def replace(encoded, index, value):
    rows = fields(encoded)
    rows[index] = value
    return frame_fields(rows)

def result(name, encoded, **values):
    return dict(name=name, **values, canonicalHex=encoded.hex(), sha256=hashlib.sha256(encoded).hexdigest())

def action(name, actor, command, intent_type, **intent_values):
    intent = dict(type=intent_type, **intent_values)
    if intent_type == 'speak':
        payload = frame('world.action.speak', number(1), bytes.fromhex(intent['targetActorId']), number(int(intent['expectedTargetRevision'])))
    elif intent_type == 'close':
        payload = frame('world.action.close-dialogue', number(1), bytes.fromhex(intent['dialogueId']))
    else:
        payload = frame('world.action.give', number(1), bytes.fromhex(intent['targetActorId']), number(int(intent['expectedTargetRevision'])),
                        bytes.fromhex(intent['lotId']), number(int(intent['expectedLotRevision'])), bytes.fromhex(intent['sourceCustodianId']),
                        number(int(intent['expectedSourceRevision'])), number(int(intent['quantity'])))
    encoded = frame('world.actor-action', number(1), identity(command), identity(201), identity(301), identity(actor), identity(1), identity(2), number(U64_MAX), payload)
    return result(name, encoded, commandId=identity(command).hex(), principalId=identity(201).hex(), originSessionId=identity(301).hex(),
                  actorId=identity(actor).hex(), worldId=identity(1).hex(), branchId=identity(2).hex(), expectedActorRevision=str(U64_MAX), intent=intent)

actions = [action('speak', 101, 401, 'speak', targetActorId=identity(109).hex(), expectedTargetRevision=str(U64_MAX)),
           action('give', 103, 403, 'give', targetActorId=identity(109).hex(), expectedTargetRevision='7', lotId=identity(501).hex(),
                  expectedLotRevision=str(U64_MAX), sourceCustodianId=identity(601).hex(), expectedSourceRevision='9', quantity=str(U64_MAX)),
           action('close', 102, 402, 'close', dialogueId=identity(701).hex())]
by_name = {v['name']: v for v in actions}

def movement(actor, x, y):
    return dict(principalId=identity(201).hex(), sessionId=identity(302).hex(), actorId=identity(actor).hex(), deltaX=x, deltaY=y)

def movement_bytes(v):
    return frame('world.control-sample', bytes.fromhex(v['principalId']), bytes.fromhex(v['sessionId']), bytes.fromhex(v['actorId']), signed(v['deltaX']), signed(v['deltaY']))

def batch(name, cause, kind, movement_values, action_names, revision=17, steps=11):
    movement_values = sorted(movement_values, key=lambda v: v['actorId'])
    action_names = sorted(action_names, key=lambda v: by_name[v]['actorId'])
    encoded = frame('world.step-batch', number(1), identity(cause), identity(1), identity(2), number(revision), number(steps), number(kind),
                    frame('world.step-movement', *map(movement_bytes, movement_values)),
                    frame('world.step-actions', *(bytes.fromhex(by_name[n]['canonicalHex']) for n in action_names)))
    return result(name, encoded, causeId=identity(cause).hex(), worldId=identity(1).hex(), branchId=identity(2).hex(), expectedRevision=str(revision),
                  expectedAdvancingSteps=str(steps), kind=kind, movement=movement_values, actionNames=action_names)

batches = [batch('empty-advancing', 801, 1, [], []),
           batch('combined-advancing', 802, 1, [movement(104, 0, -32), movement(103, 0, 32), movement(102, -32, 0), movement(101, 32, 0)], ['give', 'speak', 'close']),
           batch('held-actions', 803, 2, [], ['close', 'speak']),
           batch('maximum-counters', 804, 1, [movement(101, -32, 0)], [], U64_MAX, U64_MAX)]

malformed = []
def bad(kind, name, encoded):
    malformed.append(dict(kind=kind, name=name, canonicalHex=encoded.hex()))

speak, give, close = [bytes.fromhex(v['canonicalHex']) for v in actions]
combined = bytes.fromhex(batches[1]['canonicalHex'])
for kind, encoded, max_size in [('action', speak, 1024), ('batch', combined, 262144)]:
    bad(kind, 'unknown-schema-version', replace(encoded, 1, number(2)))
    bad(kind, 'trailing-byte', encoded + b'\0')
    bad(kind, 'extra-field', frame_fields(fields(encoded) + [b'']))
    bad(kind, 'identity-width', replace(encoded, 2, b'\0' * 15))
    bad(kind, 'number-width', replace(encoded, 1, b'\0' * 7))
    bad(kind, 'wrong-tag', replace(encoded, 0, b'world.unknown'))
    for offset, label in [(0, 'magic'), (4, 'framing-version'), (8, 'field-count'), (12, 'field-size')]:
        bad(kind, label, encoded[:offset] + struct.pack('>I', 0xffffffff) + encoded[offset + 4:])
for index in range(3, 8):
    bad('action', 'cause-alias-field-' + str(index), replace(speak, 2, fields(speak)[index]))
for name, payload in [('unknown-intent', replace(fields(speak)[9], 0, b'world.action.future')),
                      ('invalid-utf8-intent', replace(fields(speak)[9], 0, b'\xc0\xaf')),
                      ('unknown-intent-version', replace(fields(speak)[9], 1, number(2))),
                      ('zero-quantity', replace(fields(give)[9], 8, number(0))),
                      ('extra-intent-field', frame_fields(fields(fields(speak)[9]) + [b''])),
                      ('trailing-intent-byte', fields(speak)[9] + b'\0'),
                      ('oversized-intent', b'\0' * 513),
                      ('wrong-target-width', replace(fields(speak)[9], 2, b'\0' * 17)),
                      ('wrong-quantity-width', replace(fields(give)[9], 8, b'\0' * 9))]:
    bad('action', name, replace(speak, 9, payload))
movement_rows, action_rows = fields(fields(combined)[8]), fields(fields(combined)[9])
for index, rows in [(8, movement_rows), (9, action_rows)]:
    bad('batch', 'unsorted-collection-' + str(index), replace(combined, index, frame_fields([rows[0], *reversed(rows[1:])])) )
    bad('batch', 'duplicate-collection-' + str(index), replace(combined, index, frame_fields([rows[0], rows[1], rows[1]])))
    bad('batch', 'oversized-collection-' + str(index), replace(combined, index, frame_fields([rows[0], *([rows[1]] * 65)])))
    bad('batch', 'wrong-collection-tag-' + str(index), replace(combined, index, frame_fields([b'world.unknown', *rows[1:]])))
    bad('batch', 'trailing-collection-' + str(index), replace(combined, index, fields(combined)[index] + b'\0'))
for x, y in [(0, 0), (32, 32), (31, 0), (64, 0), (-(1 << 63), 0)]:
    sample = replace(replace(movement_rows[1], 4, signed(x)), 5, signed(y))
    bad('batch', f'invalid-movement-{x}-{y}', replace(combined, 8, frame('world.step-movement', sample)))
for name, sample in [('movement-extra-field', frame_fields(fields(movement_rows[1]) + [b''])),
                     ('movement-trailing', movement_rows[1] + b'\0'), ('movement-scalar-width', replace(movement_rows[1], 4, b'\0' * 7)),
                     ('movement-identity-width', replace(movement_rows[1], 1, b'\0' * 17))]:
    bad('batch', name, replace(combined, 8, frame('world.step-movement', sample)))
for name, row in [('foreign-world-action', replace(speak, 6, identity(99))), ('foreign-branch-action', replace(speak, 7, identity(99))),
                  ('command-is-batch-cause', replace(speak, 2, fields(combined)[2]))]:
    bad('batch', name, replace(combined, 9, frame('world.step-actions', row)))
bad('batch', 'repeated-command-id-different-actors', replace(combined, 9, frame('world.step-actions', speak, replace(close, 2, fields(speak)[2]))))
for value in (0, 3, U64_MAX):
    bad('batch', 'invalid-kind-' + str(value), replace(combined, 7, number(value)))
for index in (3, 4):
    bad('batch', 'batch-cause-alias-' + str(index), replace(combined, 2, fields(combined)[index]))
bad('batch', 'held-with-movement', replace(combined, 7, number(2)))
bad('batch', 'empty-held', replace(bytes.fromhex(batches[0]['canonicalHex']), 7, number(2)))

print(json.dumps(dict(format='world-actions-v1', provenance='Authored wire qualification only; no delivery authentication or gameplay execution proof',
                     actions=actions, batches=batches, malformed=malformed), indent=2))
