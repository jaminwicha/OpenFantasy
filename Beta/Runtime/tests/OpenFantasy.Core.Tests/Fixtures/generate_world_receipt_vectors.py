"""Independent receipt oracle; uses frozen independent command vectors as input data, stdout only."""
import hashlib
import json
import struct
from pathlib import Path

commands = json.loads(Path(__file__).with_name('world-actions-v1.json').read_text())['actions']
commands = {row['name']: row for row in commands}

def number(value):
    return struct.pack('>Q', value)

def frame_fields(fields):
    return b'OFC\0' + struct.pack('>II', 1, len(fields)) + b''.join(struct.pack('>I', len(v)) + v for v in fields)

def frame(tag, *fields):
    return frame_fields([tag.encode('ascii'), *fields])

def fields(encoded):
    count, = struct.unpack_from('>I', encoded, 8)
    result, offset = [], 12
    for _ in range(count):
        size, = struct.unpack_from('>I', encoded, offset)
        offset += 4
        result.append(encoded[offset:offset + size])
        offset += size
    assert offset == len(encoded)
    return result

def replace(encoded, index, value):
    values = fields(encoded)
    values[index] = value
    return frame_fields(values)

def receipt(name, command_name, sequence, rejection):
    command = commands[command_name]
    command_hash = hashlib.sha256(bytes.fromhex(command['canonicalHex'])).digest()
    assert command_hash.hex() == command['sha256']
    status = 1 if rejection == 0 else 2
    encoded = frame('world.action-receipt', number(1), *(bytes.fromhex(command[key]) for key in ['commandId', 'principalId', 'worldId', 'branchId']),
                    command_hash, number(sequence), number(status), number(rejection))
    digest = hashlib.sha256(encoded).hexdigest()
    return dict(name=name, commandName=command_name, commandId=command['commandId'], principalId=command['principalId'], worldId=command['worldId'],
                branchId=command['branchId'], commandHash=command_hash.hex(), commitSequence=str(sequence), status=status, rejection=rejection,
                canonicalHex=encoded.hex(), sha256=digest, resultId=digest[:32])

receipts = [receipt('applied', 'speak', 1, 0), receipt('not-authorized', 'close', 17, 1),
            receipt('unavailable', 'give', (1 << 64) - 1, 2), receipt('revision-conflict', 'speak', 18, 3),
            receipt('out-of-reach', 'speak', 19, 4), receipt('unable-to-communicate', 'speak', 20, 5)]
by_name = {row['name']: row for row in receipts}

def batch(name, names):
    names = sorted(names, key=lambda n: by_name[n]['commandId'])
    encoded = frame('world.action-outcomes', *(bytes.fromhex(by_name[n]['canonicalHex']) for n in names))
    return dict(name=name, receiptNames=names, canonicalHex=encoded.hex(), sha256=hashlib.sha256(encoded).hexdigest())

batches = [batch('empty', []), batch('three-outcomes', ['unavailable', 'not-authorized', 'applied'])]
malformed = []
def bad(kind, name, encoded):
    malformed.append(dict(kind=kind, name=name, canonicalHex=encoded.hex()))

applied = bytes.fromhex(receipts[0]['canonicalHex'])
rejected = bytes.fromhex(receipts[1]['canonicalHex'])
combined = bytes.fromhex(batches[1]['canonicalHex'])
for kind, encoded in [('receipt', applied), ('batch', combined)]:
    bad(kind, 'trailing-byte', encoded + b'\0')
    bad(kind, 'wrong-tag', replace(encoded, 0, b'world.unknown'))
    for offset, label in [(0, 'magic'), (4, 'framing-version'), (8, 'field-count'), (12, 'field-size')]:
        bad(kind, label, encoded[:offset] + struct.pack('>I', 0xffffffff) + encoded[offset + 4:])
for index in range(2, 6):
    for size in (15, 17):
        bad('receipt', f'identity-width-{index}-{size}', replace(applied, index, b'\0' * size))
for size in (31, 33):
    bad('receipt', 'hash-width-' + str(size), replace(applied, 6, b'\0' * size))
for name, index, value in [('version', 1, 2), ('zero-commit', 7, 0), ('status-zero', 8, 0), ('unknown-status', 8, 3),
                          ('extreme-status', 8, (1 << 64) - 1), ('unknown-rejection', 9, 6), ('extreme-rejection', 9, (1 << 64) - 1),
                          ('applied-with-rejection', 9, 1), ('rejected-without-reason', 8, 2)]:
    bad('receipt', name, replace(applied, index, number(value)))
bad('receipt', 'extra-field', frame_fields(fields(applied) + [b'']))
for index in (1, 7, 8, 9):
    bad('receipt', 'scalar-width-' + str(index), replace(applied, index, b'\0' * 7))
rows = fields(combined)
bad('batch', 'unsorted', frame_fields([rows[0], *reversed(rows[1:])]))
bad('batch', 'duplicate', frame_fields([rows[0], rows[1], rows[1]]))
bad('batch', 'sixty-five-entries', frame_fields([rows[0], *([rows[1]] * 65)]))
bad('batch', 'nested-trailing-byte', frame_fields([rows[0], rows[1] + b'\0']))
bad('batch', 'nested-inconsistent', frame_fields([rows[0], replace(rejected, 9, number(0))]))
bad('batch', 'nested-oversized', frame_fields([rows[0], b'\0' * 513]))
print(json.dumps(dict(format='world-receipts-v1', provenance='Independent qualification bytes, not durable commit or authenticated receipt evidence',
                     receipts=receipts, batches=batches, malformed=malformed), indent=2))
