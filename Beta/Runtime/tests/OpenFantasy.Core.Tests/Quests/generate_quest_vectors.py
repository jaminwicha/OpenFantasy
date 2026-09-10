#!/usr/bin/env python3
"""Independent quest framing oracle; print reference JSON without overwriting retained evidence."""
import hashlib
import json
import struct


def number(value):
    return struct.pack('>Q', value)


def identity(value):
    return value.to_bytes(16, 'big')


def frame(tag, *values):
    fields = [tag.encode(), *values]
    return b'OFC\0' + struct.pack('>II', 1, len(fields)) + b''.join(struct.pack('>I', len(v)) + v for v in fields)


def replace(encoded, index, value):
    position, fields = 12, []
    for _ in range(struct.unpack('>I', encoded[8:12])[0]):
        length = struct.unpack('>I', encoded[position:position + 4])[0]
        position += 4
        fields.append(encoded[position:position + length])
        position += length
    assert position == len(encoded)
    fields[index] = value
    return frame(fields[0].decode(), *fields[1:])


def instant(day, millis):
    return frame('quest.instant', number(1), struct.pack('>q', day), number(millis))


currency = frame('progression.currency', number(1), identity(2203), b'gil', b'Gil', b'gil')
reward = frame('progression.reward', number(1), identity(2210), b'wood_reward', number(420), currency, identity(2206), number(200))
definition = frame('quest.delivery-definition', number(1), identity(2211), b'wood_delivery', b'Orchard supplies',
                   b'Bring six wood.', identity(11), number(1), identity(80), hashlib.sha256(b'admission-test-pin').digest(),
                   number(6), identity(2000), identity(2001), reward)
accepted = frame('quest.participation', number(1), identity(10), identity(2211), identity(2400), instant(0, 32400000), b'', b'')
completed = frame('quest.participation', number(1), identity(10), identity(2211), identity(2400), instant(0, 32400000), identity(2401), instant(0, 32400000))


def snapshot(revision, *entries):
    return frame('quest.snapshot', number(1), identity(1), identity(2), number(revision),
                 frame('quest.definitions', definition), frame('quest.participations', *entries))


def command(complete=False):
    fields = [number(1), number(2 if complete else 1), identity(2211), hashlib.sha256(definition).digest(),
              identity(11), number(0), number(1 if complete else 0), number(0)]
    if complete:
        fields.extend([number(0), frame('quest.selected-lots', frame('quest.source-selection', identity(70), number(0), number(6)))])
    return frame('world.actor-action', number(4), identity(2401 if complete else 2400), identity(100), identity(110),
                 identity(10), identity(1), identity(2), number(0), frame('world.action.quest', *fields))


def vector(kind, name, value):
    return {'kind': kind, 'name': name, 'hex': value.hex(), 'sha256': hashlib.sha256(value).hexdigest()}


valid = [vector('definition', 'wood_delivery', definition), vector('snapshot', 'empty', snapshot(0)),
         vector('snapshot', 'accepted', snapshot(1, accepted)), vector('snapshot', 'completed', snapshot(2, completed)),
         vector('action', 'accept', command()), vector('action', 'complete', command(True))]
invalid = [vector('definition', 'minimum_zero', replace(definition, 7, number(0))),
           vector('definition', 'quantity_zero', replace(definition, 10, number(0))),
           vector('definition', 'minimum_overflow', replace(definition, 7, number(2**32))),
           vector('definition', 'invalid_utf8', replace(definition, 4, b'\xff')),
           vector('snapshot', 'wrong_transition_total', snapshot(0, accepted)),
           vector('snapshot', 'duplicate_actor_quest', snapshot(2, accepted, accepted)),
           vector('snapshot', 'unpaired_completion', snapshot(2, replace(completed, 7, b''))),
           vector('snapshot', 'backdated_completion', snapshot(2, replace(completed, 7, instant(-1, 0)))),
           vector('snapshot', 'unknown_quest', snapshot(1, replace(accepted, 3, identity(9000)))),
           vector('snapshot', 'duplicate_causes', snapshot(2, replace(completed, 6, identity(2400)))),
           vector('action', 'unknown_action_version', replace(command(), 1, number(5)))]
for item in list(valid):
    value = bytes.fromhex(item['hex'])
    invalid.append(vector(item['kind'], item['name'] + '_trailing', value + b'\0'))
print(json.dumps({'contract': 'quest-v1', 'valid': valid, 'invalid': invalid}, indent=2))
