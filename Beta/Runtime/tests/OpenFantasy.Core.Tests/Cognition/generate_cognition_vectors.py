#!/usr/bin/env python3
"""Independent cognition framing oracle; print reference JSON without overwriting retained evidence.

Fixture: Wren (actor 10) observed a source, waited without a tool, then received the player's (actor 20)
qualifying delivery and adopted work on her funded project. The player holds empty cognition.
"""
import hashlib
import json
import struct

WORLD, BRANCH, WREN, PLAYER, QUEST, PROJECT, SOURCE = 1, 2, 10, 20, 300, 400, 501
OBSERVE_COMMAND, DELIVERY_COMMAND = 701, 703
OBSERVED, DELIVERED = 801, 802
ITEM_RECEIVED, DELIVERY_RECEIVED, GATHER_OUTCOME, SOURCE_OBSERVED = 1, 2, 3, 4
AWAIT_SUPPLY, WORK_FUNDED_PROJECT = 1, 3
ADOPTED, SUPERSEDED = 1, 3
NONE, MISSING_TOOL = 0, 1
EVIDENCE_ACQUIRED, FALLBACK = 1, 4
POLICY = b'cognition.delayed-supply.v2'


def number(value):
    return struct.pack('>Q', value)


def signed(value):
    return struct.pack('>q', value)


def identity(value):
    return value.to_bytes(16, 'big')


def frame(tag, *values):
    fields = [tag.encode(), *values]
    return b'OFC\0' + struct.pack('>II', 1, len(fields)) + b''.join(struct.pack('>I', len(v)) + v for v in fields)


def instant(days, milliseconds):
    return frame('cognition.instant', number(1), signed(days), number(milliseconds))


def evidence(evidence_id, kind, command, counterpart, subject, quantity, at):
    return frame('cognition.evidence', number(1), identity(evidence_id), number(kind), identity(command),
                 identity(counterpart) if counterpart is not None else b'', identity(subject), number(quantity), at)


def intention(kind, subject, at, *basis):
    return frame('cognition.intention', number(1), number(kind), identity(subject), at,
                 frame('cognition.basis', *(identity(value) for value in basis)))


def memory(kind, subject, outcome, at, reason, basis=(), policy=POLICY):
    return frame('cognition.memory', number(1), number(kind), identity(subject), number(outcome), at, number(reason),
                 policy, frame('cognition.basis', *(identity(value) for value in basis)))


def wake(due, cause, suppression=0):
    return frame('cognition.wake', number(1), number(due), number(cause), number(suppression))


def actor(actor_id, revision, acquired, current, memories, pending=b''):
    return frame('cognition.actor', number(1), identity(actor_id), number(revision),
                 frame('cognition.acquired', *acquired), current, frame('cognition.memories', *memories), pending)


def snapshot(revision, *actors, version=1):
    return frame('cognition.snapshot', number(version), identity(WORLD), identity(BRANCH), number(revision),
                 frame('cognition.actors', *actors))


def vector(name, value):
    return {'name': name, 'hex': value.hex(), 'sha256': hashlib.sha256(value).hexdigest()}


observed = evidence(OBSERVED, SOURCE_OBSERVED, OBSERVE_COMMAND, None, SOURCE, 3, instant(0, 1000))
delivered = evidence(DELIVERED, DELIVERY_RECEIVED, DELIVERY_COMMAND, PLAYER, QUEST, 4, instant(0, 5000))
working = intention(WORK_FUNDED_PROJECT, PROJECT, instant(0, 5000), DELIVERED)
remembered = [memory(AWAIT_SUPPLY, PROJECT, ADOPTED, instant(0, 0), MISSING_TOOL),
              memory(AWAIT_SUPPLY, PROJECT, SUPERSEDED, instant(0, 5000), NONE),
              memory(WORK_FUNDED_PROJECT, PROJECT, ADOPTED, instant(0, 5000), NONE, basis=[DELIVERED])]
wren = actor(WREN, 4, [observed, delivered], working, remembered, wake(200, FALLBACK))
player = actor(PLAYER, 0, [], b'', [])

valid = [vector('empty', snapshot(0)), vector('wren_working', snapshot(5, wren, player))]
invalid = [
    vector('unknown_version', snapshot(5, wren, player, version=2)),
    vector('actors_out_of_order', snapshot(5, player, wren)),
    vector('duplicate_actor', snapshot(5, wren, wren)),
    vector('evidence_out_of_order', snapshot(5, actor(WREN, 3, [delivered, observed], working, remembered))),
    vector('duplicate_evidence', snapshot(5, actor(WREN, 3, [observed, observed], b'', []))),
    vector('unknown_evidence_kind', snapshot(5, actor(WREN, 1, [evidence(OBSERVED, 9, OBSERVE_COMMAND, None, SOURCE, 3,
                                                                       instant(0, 1000))], b'', []))),
    vector('delivery_without_counterpart', snapshot(5, actor(WREN, 1, [evidence(DELIVERED, DELIVERY_RECEIVED,
        DELIVERY_COMMAND, None, QUEST, 4, instant(0, 5000))], b'', []))),
    vector('delivery_of_nothing', snapshot(5, actor(WREN, 1, [evidence(DELIVERED, DELIVERY_RECEIVED, DELIVERY_COMMAND,
                                                                      PLAYER, QUEST, 0, instant(0, 5000))], b'', []))),
    vector('observation_with_counterpart', snapshot(5, actor(WREN, 1, [evidence(OBSERVED, SOURCE_OBSERVED,
        OBSERVE_COMMAND, PLAYER, SOURCE, 3, instant(0, 1000))], b'', []))),
    vector('self_counterpart', snapshot(5, actor(WREN, 1, [evidence(DELIVERED, DELIVERY_RECEIVED, DELIVERY_COMMAND,
                                                                   WREN, QUEST, 4, instant(0, 5000))], b'', []))),
    vector('basis_not_held', snapshot(5, actor(WREN, 3, [observed], working, remembered))),
    vector('work_without_basis', snapshot(5, actor(WREN, 3, [observed, delivered],
                                                   intention(WORK_FUNDED_PROJECT, PROJECT, instant(0, 5000)), []))),
    vector('memory_out_of_order', snapshot(5, actor(WREN, 3, [observed, delivered], working, list(reversed(remembered))))),
    vector('unknown_memory_reason', snapshot(5, actor(WREN, 1, [], b'', [memory(AWAIT_SUPPLY, PROJECT, ADOPTED,
                                                                               instant(0, 0), 99)]))),
    vector('milliseconds_past_day', snapshot(5, actor(WREN, 1, [evidence(OBSERVED, SOURCE_OBSERVED, OBSERVE_COMMAND,
        None, SOURCE, 3, instant(0, 86_400_000))], b'', []))),
    vector('evidence_beyond_bound', snapshot(5, actor(WREN, 65, [evidence(1000 + index, SOURCE_OBSERVED,
        OBSERVE_COMMAND, None, SOURCE, 3, instant(0, index)) for index in range(65)], b'', []))),
    vector('memory_basis_not_held', snapshot(5, actor(WREN, 1, [observed], b'', [memory(AWAIT_SUPPLY, PROJECT, ADOPTED,
                                                                                    instant(0, 0), NONE, basis=[899])]))),
    vector('ending_with_basis', snapshot(5, actor(WREN, 1, [observed], b'', [memory(AWAIT_SUPPLY, PROJECT, SUPERSEDED,
                                                                                instant(0, 0), NONE, basis=[OBSERVED])]))),
    vector('empty_policy_version', snapshot(5, actor(WREN, 1, [], b'', [memory(AWAIT_SUPPLY, PROJECT, ADOPTED,
                                                                               instant(0, 0), NONE, policy=b'')]))),
    vector('invalid_utf8_policy_version', snapshot(5, actor(WREN, 1, [], b'', [memory(AWAIT_SUPPLY, PROJECT, ADOPTED,
                                                                                      instant(0, 0), NONE, policy=b'\xff')]))),
    vector('unknown_wake_cause', snapshot(5, actor(WREN, 1, [], b'', [], wake(10, 9)))),
    vector('unknown_wake_suppression', snapshot(5, actor(WREN, 1, [], b'', [], wake(10, EVIDENCE_ACQUIRED, 7)))),
    vector('short_identity', snapshot(5, frame('cognition.actor', number(1), identity(WREN)[1:], number(0),
                                                frame('cognition.acquired'), b'', frame('cognition.memories'), b''))),
]
for item in list(valid):
    value = bytes.fromhex(item['hex'])
    invalid.append(vector(item['name'] + '_trailing', value + b'\0'))
    invalid.append(vector(item['name'] + '_truncated', value[:-1]))
print(json.dumps({'contract': 'cognition-v1', 'valid': valid, 'invalid': invalid}, indent=2))
