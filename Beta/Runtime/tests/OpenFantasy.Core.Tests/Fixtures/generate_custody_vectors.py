"""Independent counted-source transfer byte oracle; no production code imports or output writes."""
import hashlib
import json
import struct


def frame(tag, *fields):
    fields = [tag.encode(), *fields]
    return b'OFC\0' + struct.pack('>II', 1, len(fields)) + b''.join(
        struct.pack('>I', len(field)) + field for field in fields)


def number(value):
    return struct.pack('>Q', value)


def identity(value):
    return value.to_bytes(16, 'big')


def storage(owner, capacity, revision):
    return frame('custody.storage.v1', identity(owner), number(capacity), number(revision))


def lot(lot_id, owner, start, end, revision):
    return frame('custody.source-lot.v1', lot_id, identity(owner), identity(10),
                 hashlib.sha256(b'wood-source-v1').digest(), identity(6), number(start), number(end),
                 number(revision), number(0), b'')


def snapshot(revision, storages, lots):
    return frame('custody.snapshot.v1', identity(1), identity(2), number(revision),
                 frame('custody.storages.v1', *storages), frame('custody.lots.v1', *lots))


child = hashlib.sha256(frame('entity.spawned', identity(20), number(0))).digest()[:16]
original = snapshot(7, [storage(3, 10, 4), storage(4, 8, 5)], [lot(identity(5), 3, 10, 18, 3)])
ordered = sorted([(identity(5), lot(identity(5), 3, 16, 18, 4)), (child, lot(child, 4, 10, 16, 0))])
candidate = snapshot(8, [storage(3, 10, 5), storage(4, 8, 6)], [value for _, value in ordered])
print(json.dumps({'scenario': 'six counted wood units partitioned from eight; no quest completion claim',
                  'childId': child.hex(), 'originalHex': original.hex(), 'candidateHex': candidate.hex(),
                  'originalHash': hashlib.sha256(original).hexdigest(),
                  'candidateHash': hashlib.sha256(candidate).hexdigest()}, indent=2))
