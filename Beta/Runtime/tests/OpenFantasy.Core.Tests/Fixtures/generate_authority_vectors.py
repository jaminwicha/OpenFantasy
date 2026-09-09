"""Independent standard-library authority oracle; emits stdout and never writes expected bytes."""
import hashlib
import json
import struct


def frame(tag, *fields):
    values = [tag.encode(), *fields]
    return b'OFC\0' + struct.pack('>II', 1, len(values)) + b''.join(
        struct.pack('>I', len(value)) + value for value in values)


def number(value):
    return struct.pack('>Q', value)


def identity(value):
    return value.to_bytes(16, 'big')


WOOD = hashlib.sha256(b'wood-source-v1').digest()


def storage(owner, capacity, revision):
    return frame('custody.storage.v1', identity(owner), number(capacity), number(revision))


def lot(lot_id, owner, start, end, revision, reservation=b''):
    return frame('custody.source-lot.v1', lot_id, identity(owner), identity(10), WOOD,
                 identity(6), number(start), number(end), number(revision), number(0), reservation)


def custody(revision=7, lots=None, storages=None):
    return frame('custody.snapshot.v1', identity(1), identity(2), number(revision),
                 frame('custody.storages.v1', *(storages if storages is not None else [storage(3, 10, 4), storage(4, 8, 5)])),
                 frame('custody.lots.v1', *(lots if lots is not None else [lot(identity(5), 3, 10, 18, 3)])))


def control(principal=21, session=22, actor=23):
    return frame('authority.control', identity(principal), identity(session), identity(actor))


def policy(custodian, owner, rule):
    return frame('authority.policy', identity(custodian), identity(owner), number(rule))


def source(source_id=6, definition=10, version=WOOD, capacity=20):
    return frame('authority.source', identity(source_id), identity(definition), version, number(capacity))


def authority(stock=None, actors=None, controls=None, policies=None, sources=None, reservations=None, capacity=1000, version=1):
    return frame('authority.counted-state', number(version), number(capacity), stock if stock is not None else custody(),
                 frame('authority.actors', *(actors if actors is not None else [identity(23), identity(24)])),
                 frame('authority.controls', *(controls if controls is not None else [control()])),
                 frame('authority.policies', *(policies if policies is not None else [policy(3, 23, 1), policy(4, 24, 2)])),
                 frame('authority.sources', *(sources if sources is not None else [source()])),
                 frame('authority.reservations', *(reservations if reservations is not None else [])))


def command():
    revisions = [frame('command.expected-revision', number(1), number(kind), identity(target), number(revision))
                 for kind, target, revision in [(1, 3, 4), (1, 4, 5), (1, 5, 3), (2, 2, 7)]]
    return frame('command.envelope', number(1), identity(20), identity(21), identity(22), identity(23), identity(1), identity(2),
                 frame('command.expected-revisions', number(1), *revisions),
                 frame('command.transfer-lot', number(1), identity(5), identity(3), identity(4), number(6)))


def receipt(status, reason):
    return frame('command.receipt', number(1), identity(20), identity(21), identity(1), identity(2),
                 hashlib.sha256(command()).digest(), number(11), number(status), number(reason))


def fields(encoded):
    count = struct.unpack_from('>I', encoded, 8)[0]
    cursor = 12
    result = []
    for _ in range(count):
        length = struct.unpack_from('>I', encoded, cursor)[0]
        cursor += 4
        result.append(encoded[cursor:cursor + length])
        cursor += length
    assert cursor == len(encoded)
    return result


def replace_field(encoded, index, value):
    values = fields(encoded)
    values[index] = value
    return frame(values[0].decode(), *values[1:])


original = authority()
child = hashlib.sha256(frame('entity.spawned', identity(20), number(0))).digest()[:16]
candidate_lots = sorted([(identity(5), lot(identity(5), 3, 16, 18, 4)), (child, lot(child, 4, 10, 16, 0))])
candidate = authority(stock=custody(8, [value for _, value in candidate_lots], [storage(3, 10, 5), storage(4, 8, 6)]))
owner_only = authority(policies=[policy(3, 23, 1), policy(4, 24, 1)])
reserved = authority(stock=custody(lots=[lot(identity(5), 3, 10, 18, 3, identity(80))]), reservations=[identity(80)])

invalid = {
    'unknown_contract_version': authority(version=2),
    'unknown_frame_version': original[:4] + struct.pack('>I', 2) + original[8:],
    'wrong_magic': b'BAD\0' + original[4:],
    'unknown_state_type': frame('authority.unknown', *fields(original)[1:]),
    'trailing_byte': original + b'\0',
    'truncated_last_field': original[:-1],
    'too_many_top_fields': original[:8] + struct.pack('>I', 10) + original[12:],
    'giant_declared_field': original[:12] + struct.pack('>I', 0xffffffff) + original[16:],
    'zero_admission_capacity': authority(capacity=0),
    'budget_below_source': authority(capacity=19),
    'missing_source': authority(sources=[]),
    'source_below_lot_end': authority(sources=[source(capacity=17)]),
    'zero_source_capacity': authority(sources=[source(capacity=0)]),
    'duplicate_source': authority(sources=[source(), source()]),
    'unsorted_sources': authority(sources=[source(7), source()]),
    'inconsistent_definition_version': authority(sources=[source(), source(7, version=hashlib.sha256(b'changed').digest())]),
    'source_pin_differs_from_lot': authority(sources=[source(version=hashlib.sha256(b'changed').digest())]),
    'short_definition_hash': authority(sources=[source(version=WOOD[:-1])]),
    'admission_sum_overflow_attempt': authority(capacity=2**64 - 1, sources=[source(capacity=2**64 - 1), source(7, capacity=1)]),
    'source_aliases_actor': authority(sources=[source(), source(23, capacity=1)]),
    'duplicate_actors': authority(actors=[identity(23), identity(23), identity(24)]),
    'unsorted_actors': authority(actors=[identity(24), identity(23)]),
    'actor_aliases_world': authority(actors=[identity(1), identity(23), identity(24)]),
    'actor_aliases_principal': authority(actors=[identity(21), identity(23), identity(24)]),
    'actor_aliases_definition': authority(actors=[identity(10), identity(23), identity(24)]),
    'short_actor_id': authority(actors=[identity(23)[:-1], identity(24)]),
    'missing_owner_actor': authority(actors=[identity(23)]),
    'missing_policy': authority(policies=[policy(3, 23, 1)]),
    'duplicate_policy': authority(policies=[policy(3, 23, 1), policy(3, 23, 1)]),
    'unsorted_policies': authority(policies=[policy(4, 24, 2), policy(3, 23, 1)]),
    'absent_policy_storage': authority(policies=[policy(3, 23, 1), policy(99, 24, 2)]),
    'unknown_deposit_rule': authority(policies=[policy(3, 23, 1), policy(4, 24, 3)]),
    'duplicate_actor_control': authority(controls=[control(), control()]),
    'absent_controlled_actor': authority(controls=[control(actor=99)]),
    'two_principals_one_session': authority(controls=[control(), control(principal=31, actor=24)]),
    'unsorted_controls': authority(controls=[control(actor=24), control()]),
    'session_aliases_principal': authority(controls=[control(session=21)]),
    'unknown_reservation': authority(stock=custody(lots=[lot(identity(5), 3, 10, 18, 3, identity(80))])),
    'reservation_aliases_actor': authority(reservations=[identity(23)]),
    'duplicate_reservation': authority(reservations=[identity(80), identity(80)]),
    'unsorted_reservations': authority(reservations=[identity(81), identity(80)]),
    'zero_storage_capacity': authority(stock=custody(storages=[storage(3, 10, 4), storage(4, 0, 5)])),
    'nested_custody_trailing_data': authority(stock=custody() + b'\0'),
    'unknown_control_type': authority(controls=[frame('authority.unknown-control', identity(21), identity(22), identity(23))]),
    'extra_control_field': authority(controls=[frame('authority.control', identity(21), identity(22), identity(23), identity(99))]),
    'too_many_actor_fields': replace_field(original, 4, frame('authority.actors', *[identity(index + 100) for index in range(1025)])),
    'short_capacity_integer': replace_field(original, 2, number(1000)[1:]),
}

result = {
    'schemaVersion': 1,
    'scenario': 'Explicit actor control, owner withdrawal and public receiving; six source wood units moved from eight. Qualification state, not imported gameplay.',
    'childId': child.hex(),
    'originalHex': original.hex(), 'originalHash': hashlib.sha256(original).hexdigest(),
    'candidateHex': candidate.hex(), 'candidateHash': hashlib.sha256(candidate).hexdigest(),
    'ownerOnlyHex': owner_only.hex(), 'reservedHex': reserved.hex(),
    'commandHex': command().hex(),
    'appliedReceiptHex': receipt(1, 0).hex(),
    'notAuthorizedReceiptHex': receipt(2, 2).hex(),
    'invalidStates': [{'name': name, 'hex': value.hex()} for name, value in invalid.items()],
}
print(json.dumps(result, indent=2))
