#!/usr/bin/env python3
"""Independent parser/arithmetic oracle for the retained six-effect calibration, not world admission."""
from pathlib import Path
import hashlib
import json
import struct
import sys


def frame(data, tag, count=None):
    assert len(data) <= 2 * 1024 * 1024
    assert data[:8] == b'OFC\0\0\0\0\1' and len(data) >= 12
    fields, offset = [], 12
    n = struct.unpack('>I', data[8:12])[0]
    assert 1 <= n <= 1025
    for _ in range(n):
        assert offset + 4 <= len(data)
        length = struct.unpack('>I', data[offset:offset+4])[0]
        offset += 4
        assert offset + length <= len(data)
        fields.append(data[offset:offset+length])
        offset += length
    assert offset == len(data) and fields[0] == tag.encode()
    assert count is None or len(fields) == count
    return fields


def number(data):
    assert len(data) == 8
    return struct.unpack('>Q', data)[0]


def actor(data):
    a = frame(data, 'progression.actor', 12)
    assert number(a[1]) == 1 and len(a[2]) == 16
    return a


def check(basis, current, archive):
    b = frame(basis, 'progression.snapshot', 9)
    c = frame(current, 'progression.snapshot', 9)
    h = frame(archive, 'progression.resource-history', 7)
    assert number(h[1]) == 1 and h[2:4] == b[2:4] == c[2:4]
    assert number(h[4]) == 20 and h[5] == hashlib.sha256(basis).digest()
    assert b[5:7] == c[5:7] and b[8] == c[8]  # Definitions and all currency unchanged.
    states = {a[2]: a for a in map(actor, frame(b[7], 'progression.actors')[1:])}
    batches = frame(h[6], 'progression.resource-batches')[1:]
    assert len(batches) == 6
    expected = [(0, 50, 1, 0, 10, 10), (1, 7, 1, 0, 17, 10),
                (2, 3, 1, 0, 17, 7), (3, 25, 1, 25, 17, 7),
                (3, 75, 2, 0, 69, 12), (0, 6, 2, 0, 63, 12)]
    commands = set()
    for index, raw_batch in enumerate(batches):
        batch = frame(raw_batch, 'progression.resource-batch', 6)
        assert number(batch[1]) == 1 and number(batch[2]) == 21 + index and number(batch[3]) == 0
        assert len(batch[4]) == 16 and batch[4] not in commands
        commands.add(batch[4])
        changes = frame(batch[5], 'progression.resource-changes', 2)
        change = frame(changes[1], 'progression.resource-change', 8)
        assert number(change[1]) == 1 and number(change[2]) == 0
        cause = frame(change[3], 'progression.resource-cause', 5)
        assert number(cause[1]) == 1
        assert cause[2] == (301).to_bytes(16, 'big') and cause[3] == (300).to_bytes(16, 'big')
        assert cause[4] == hashlib.sha256(b'resource-rule').digest()
        before, after = actor(change[6]), actor(change[7])
        assert before == states[before[2]] and after[2] == before[2]
        assert after[4:8] == before[4:8] and number(after[3]) == number(before[3]) + 1
        op, amount = number(change[4]), number(change[5])
        level, xp, hp, mp = map(number, before[8:12])
        if op == 0:
            hp = max(0, hp - amount)
        elif op == 1:
            hp = min(60 + 9 * (level - 1), hp + amount)
        elif op == 2:
            assert mp >= amount
            mp -= amount
        elif op == 3:
            xp += amount
            # This calibration stays within the first two levels.
            if xp >= 100:
                assert level == 1 and xp == 100
                level, xp, hp, mp = 2, 0, 69, 12
        else:
            raise AssertionError('unsupported fixture operation')
        assert tuple(map(number, after[8:12])) == (level, xp, hp, mp)
        assert (op, amount, level, xp, hp, mp) == expected[index]
        states[after[2]] = after
    actual = {a[2]: a for a in map(actor, frame(c[7], 'progression.actors')[1:])}
    assert states == actual and number(c[4]) == number(b[4]) + 6
    return {'batches': 6, 'actorRevisions': 6, 'finalHp': 63, 'finalMp': 12, 'level': 2}


def encode_fields(fields):
    return b'OFC\0\0\0\0\1' + struct.pack('>I', len(fields)) + b''.join(struct.pack('>I', len(f)) + f for f in fields)


def run(folder):
    b, c, h = [(folder / name).read_bytes() for name in ['basis.bin', 'current.bin', 'history.bin']]
    result = check(b, c, h)
    root = frame(h, 'progression.resource-history')
    batches = frame(root[6], 'progression.resource-batches')
    missing = root.copy(); missing[6] = encode_fields(batches[:4] + batches[5:])
    reordered = root.copy(); reordered[6] = encode_fields([batches[0], batches[2], batches[1], *batches[3:]])
    tampered = root.copy(); tampered[5] = bytes(32)
    refused = []
    for label, candidate in [('missing-xp', encode_fields(missing)), ('reordered', encode_fields(reordered)),
                             ('wrong-basis', encode_fields(tampered)), ('truncated', h[:-1]), ('trailing', h+b'\0')]:
        try:
            check(b, c, candidate)
        except (AssertionError, ValueError, struct.error, KeyError):
            refused.append(label)
        else:
            raise AssertionError(f'Oracle accepted {label}')
    result.update({'refused': refused, 'scope': 'six-effect calibration only; no world authentication or journal replay',
                   'files': {p.name: {'sha256': hashlib.sha256(p.read_bytes()).hexdigest(), 'bytes': p.stat().st_size}
                             for p in sorted(folder.glob('*.bin'))}})
    return result


if __name__ == '__main__':
    print(json.dumps(run(Path(sys.argv[1])), indent=2))
