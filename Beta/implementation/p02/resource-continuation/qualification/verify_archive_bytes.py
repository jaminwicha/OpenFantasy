"""Independent OFC framing, archive linkage and complete actor-continuity oracle (no CLR)."""
from pathlib import Path
import hashlib
import json
import struct
import sys


def frame(data, tag=None):
    assert len(data) >= 12 and data[:8] == b'OFC\0\0\0\0\1'
    count = struct.unpack_from('>I', data, 8)[0]
    assert count <= 4097
    fields, offset = [], 12
    for _ in range(count):
        assert offset + 4 <= len(data)
        size = struct.unpack_from('>I', data, offset)[0]
        offset += 4
        assert size <= len(data) - offset
        fields.append(data[offset:offset + size])
        offset += size
    assert offset == len(data)
    if tag is not None:
        assert fields[0] == tag.encode()
    return fields


def number(value):
    assert len(value) == 8
    return int.from_bytes(value, 'big')


def actors(progression):
    values = frame(progression[7], 'progression.actors')[1:]
    result = {frame(value, 'progression.actor')[2]: value for value in values}
    assert len(result) == len(values)
    return result


def verify(genesis, segments, current):
    first = frame(genesis, 'world.snapshot')
    scope = frame(first[13], 'progression.snapshot')[2:4]
    prior = None
    commands, last_position, total = set(), (0, 0), 0
    records = []
    for ordinal, raw in enumerate([*segments, current]):
        root = frame(raw, 'world.snapshot')
        assert len(root) == 24 and number(root[1]) == 25
        envelope = frame(root[23], 'world.resource-history')
        if ordinal == 0:
            assert number(envelope[1]) == 1
            body = envelope
        else:
            assert len(envelope) == 4 and number(envelope[1]) == 2
            reference = frame(envelope[2], 'world.resource-archive-reference.v1')
            assert reference[1:3] == scope and number(reference[3]) == ordinal - 1
            closing = frame(prior, 'world.snapshot')
            assert number(reference[4]) == number(closing[8])
            assert reference[5] == hashlib.sha256(prior).digest()
            body = frame(envelope[3], 'world.resource-history')
            assert body[4] == closing[13] and body[3] == closing[8]
        assert len(body) == 7 and number(body[1]) == 1 and body[2] == first[5]
        basis = frame(body[4], 'progression.snapshot')
        history = frame(body[5], 'progression.resource-history')
        assert history[2:4] == basis[2:4] == scope
        assert history[4] == body[3] and history[5] == hashlib.sha256(body[4]).digest()
        heads = actors(basis)
        batches = frame(history[6], 'progression.resource-batches')[1:]
        evidence = frame(body[6], 'world.resource-evidence')[1:]
        assert len(batches) == len(evidence) <= 256
        effects = 0
        for encoded in batches:
            batch = frame(encoded, 'progression.resource-batch')
            position = number(batch[2]), number(batch[3])
            assert position > last_position and number(body[3]) < position[0] <= number(root[8])
            assert batch[4] not in commands
            commands.add(batch[4]); last_position = position
            for effect_ordinal, encoded_effect in enumerate(frame(batch[5], 'progression.resource-changes')[1:]):
                effect = frame(encoded_effect, 'progression.resource-change')
                before, after = frame(effect[6], 'progression.actor'), frame(effect[7], 'progression.actor')
                assert number(effect[2]) == effect_ordinal
                assert before[2] == after[2] and heads[before[2]] == effect[6]
                assert number(after[3]) == number(before[3]) + 1
                heads[after[2]] = effect[7]; effects += 1
        assert effects <= 1024
        progression = frame(root[13], 'progression.snapshot')
        assert heads == actors(progression)
        assert number(progression[4]) == number(basis[4]) + len(batches)
        records.append({'sha256': hashlib.sha256(raw).hexdigest(), 'boundary': number(root[8]),
                        'basis_sequence': number(body[3]), 'batches': len(batches), 'effects': effects})
        total += len(batches); prior = raw
    assert total == 520 and [value['batches'] for value in records] == [256, 256, 8]
    return records


if __name__ == '__main__':
    directory = Path(sys.argv[1])
    genesis = (directory / 'genesis.ofc').read_bytes()
    segments = [(directory / f'segment-{index}.ofc').read_bytes() for index in range(2)]
    current = (directory / 'continued.ofc').read_bytes()
    records = verify(genesis, segments, current)
    refused = 0
    for broken in ([segments[1], segments[0]], [segments[0][:-1], segments[1]],
                   [segments[0], segments[0]], [segments[0], segments[1] + b'\0']):
        try:
            verify(genesis, broken, current)
        except (AssertionError, IndexError, struct.error):
            refused += 1
        else:
            raise AssertionError('Corrupt archive sequence accepted')
    print(json.dumps({'verified_segments_and_current': records, 'resource_batches': 520,
                      'corrupt_archive_sequences_refused': refused,
                      'scope': 'Independent exact framing, reference chain and full actor revision continuity; not host authentication or gameplay damage arithmetic.'}, indent=2))
