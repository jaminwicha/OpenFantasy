#!/usr/bin/env python3
"""Independent cognition framing oracle; print reference JSON without overwriting retained evidence.

Fixtures: Wren (actor 10) observed a source, waited without a tool, then received the player's (actor 20)
qualifying delivery and adopted work on her funded project; the player holds empty cognition. Wren also waits while
her reason changes from a missing tool to spent effort, and gathers at a source whose cited first observation is kept
while newer uncited observations and gathering outcomes replace each other.
"""
import hashlib
import json
import struct

WORLD, BRANCH, WREN, PLAYER, QUEST, PROJECT, SOURCE = 1, 2, 10, 20, 300, 400, 501
OBSERVE_COMMAND, DELIVERY_COMMAND = 701, 703
OBSERVED, DELIVERED = 801, 802
ITEM_RECEIVED, DELIVERY_RECEIVED, GATHER_OUTCOME, SOURCE_OBSERVED = 1, 2, 3, 4
AWAIT_SUPPLY, GATHER_PERSONAL_STOCK, WORK_FUNDED_PROJECT = 1, 2, 3
ADOPTED, SUPERSEDED, RECONSIDERED = 1, 3, 4
NONE, MISSING_TOOL, INSUFFICIENT_EFFORT, READING_HOLD, AWAITING_SUPPLY = 0, 1, 4, 6, 8
GATHER_COMMAND = 702
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


def deferral(reason, at, policy=POLICY):
    return frame('cognition.deferral', number(1), number(reason), policy, at)


def wake(due, cause, suppression=0):
    return frame('cognition.wake', number(1), number(due), number(cause), number(suppression))


def actor(actor_id, revision, acquired, current, memories, pending=b'', deferring=b'', version=2):
    return frame('cognition.actor', number(version), identity(actor_id), number(revision),
                 frame('cognition.acquired', *acquired), current, deferring, frame('cognition.memories', *memories),
                 pending)


def snapshot(revision, *actors, version=2):
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
work_deferral = deferral(NONE, instant(0, 5000))
wren = actor(WREN, 4, [observed, delivered], working, remembered, wake(200, FALLBACK), work_deferral)
player = actor(PLAYER, 0, [], b'', [])

# The intention is kept while its reason changes; the change is one reconsidered memory and the current deferral.
awaiting = intention(AWAIT_SUPPLY, PROJECT, instant(0, 0))
reconsidered = actor(WREN, 3, [observed], awaiting,
                     [memory(AWAIT_SUPPLY, PROJECT, ADOPTED, instant(0, 0), MISSING_TOOL),
                      memory(AWAIT_SUPPLY, PROJECT, RECONSIDERED, instant(0, 3000), INSUFFICIENT_EFFORT)],
                     wake(120, FALLBACK), deferral(INSUFFICIENT_EFFORT, instant(0, 3000)))

# The gathering intention cites the first observation, which is kept; newer uncited entries replace each other.
gathering = intention(GATHER_PERSONAL_STOCK, SOURCE, instant(0, 1000), OBSERVED)
latest_observation = evidence(812, SOURCE_OBSERVED, OBSERVE_COMMAND, None, SOURCE, 2, instant(0, 3000))
latest_yield = evidence(813, GATHER_OUTCOME, GATHER_COMMAND, None, SOURCE, 1, instant(0, 4000))
cited_kept = actor(WREN, 6, [observed, latest_observation, latest_yield], gathering,
                   [memory(GATHER_PERSONAL_STOCK, SOURCE, ADOPTED, instant(0, 1000), AWAITING_SUPPLY, basis=[OBSERVED])],
                   wake(10, EVIDENCE_ACQUIRED), deferral(AWAITING_SUPPLY, instant(0, 1000)))

valid = [vector('empty', snapshot(0)), vector('wren_working', snapshot(5, wren, player)),
         vector('wren_reconsidered', snapshot(3, reconsidered)), vector('cited_observation_kept', snapshot(6, cited_kept))]
older_observation = evidence(811, SOURCE_OBSERVED, OBSERVE_COMMAND, None, SOURCE, 2, instant(0, 2000))
older_yield = evidence(810, GATHER_OUTCOME, GATHER_COMMAND, None, SOURCE, 1, instant(0, 2000))
invalid = [
    vector('retired_version', snapshot(5, wren, player, version=1)),
    vector('unknown_version', snapshot(5, wren, player, version=3)),
    vector('retired_actor_version', snapshot(5, actor(PLAYER, 0, [], b'', [], version=1))),
    vector('uncited_superseded_observation', snapshot(6, actor(WREN, 6, [observed, older_observation, latest_observation],
        gathering, [memory(GATHER_PERSONAL_STOCK, SOURCE, ADOPTED, instant(0, 1000), AWAITING_SUPPLY, basis=[OBSERVED])],
        b'', deferral(AWAITING_SUPPLY, instant(0, 1000))))),
    vector('uncited_superseded_gather_outcome', snapshot(6, actor(WREN, 6, [observed, older_yield, latest_yield],
        gathering, [memory(GATHER_PERSONAL_STOCK, SOURCE, ADOPTED, instant(0, 1000), AWAITING_SUPPLY, basis=[OBSERVED])],
        b'', deferral(AWAITING_SUPPLY, instant(0, 1000))))),
    vector('uncited_observations_without_intention', snapshot(2, actor(WREN, 2, [observed, latest_observation], b'', []))),
    vector('intention_without_deferral', snapshot(5, actor(WREN, 4, [observed, delivered], working, remembered))),
    vector('deferral_before_adoption', snapshot(5, actor(WREN, 4, [observed, delivered], working, remembered,
                                                         deferring=deferral(NONE, instant(0, 4999))))),
    vector('reading_hold_deferral', snapshot(3, actor(WREN, 1, [], awaiting, [], deferring=deferral(READING_HOLD,
                                                                                                    instant(0, 0))))),
    vector('unknown_deferral_reason', snapshot(3, actor(WREN, 1, [], awaiting, [], deferring=deferral(99, instant(0, 0))))),
    vector('deferral_without_intention', snapshot(3, actor(WREN, 1, [], b'', [],
                                                           deferring=deferral(MISSING_TOOL, instant(0, 0))))),
    vector('empty_deferral_policy', snapshot(3, actor(WREN, 1, [], awaiting, [],
                                                      deferring=deferral(MISSING_TOOL, instant(0, 0), policy=b'')))),
    vector('reconsidered_with_basis', snapshot(3, actor(WREN, 3, [observed], awaiting,
        [memory(AWAIT_SUPPLY, PROJECT, ADOPTED, instant(0, 0), MISSING_TOOL),
         memory(AWAIT_SUPPLY, PROJECT, RECONSIDERED, instant(0, 3000), INSUFFICIENT_EFFORT, basis=[OBSERVED])],
        b'', deferral(INSUFFICIENT_EFFORT, instant(0, 3000))))),
    vector('reconsidered_under_hold', snapshot(3, actor(WREN, 3, [], awaiting,
        [memory(AWAIT_SUPPLY, PROJECT, ADOPTED, instant(0, 0), MISSING_TOOL),
         memory(AWAIT_SUPPLY, PROJECT, RECONSIDERED, instant(0, 3000), READING_HOLD)],
        b'', deferral(MISSING_TOOL, instant(0, 0))))),
    vector('actors_out_of_order', snapshot(5, player, wren)),
    vector('duplicate_actor', snapshot(5, wren, wren)),
    vector('evidence_out_of_order', snapshot(5, actor(WREN, 3, [delivered, observed], working, remembered,
                                                      deferring=work_deferral))),
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
    vector('basis_not_held', snapshot(5, actor(WREN, 3, [observed], working, remembered, deferring=work_deferral))),
    vector('work_without_basis', snapshot(5, actor(WREN, 3, [observed, delivered],
                                                   intention(WORK_FUNDED_PROJECT, PROJECT, instant(0, 5000)), [],
                                                   deferring=work_deferral))),
    vector('memory_out_of_order', snapshot(5, actor(WREN, 3, [observed, delivered], working, list(reversed(remembered)),
                                                    deferring=work_deferral))),
    vector('unknown_memory_reason', snapshot(5, actor(WREN, 1, [], b'', [memory(AWAIT_SUPPLY, PROJECT, ADOPTED,
                                                                               instant(0, 0), 99)]))),
    vector('milliseconds_past_day', snapshot(5, actor(WREN, 1, [evidence(OBSERVED, SOURCE_OBSERVED, OBSERVE_COMMAND,
        None, SOURCE, 3, instant(0, 86_400_000))], b'', []))),
    vector('evidence_beyond_bound', snapshot(5, actor(WREN, 65, [evidence(1000 + index, SOURCE_OBSERVED,
        OBSERVE_COMMAND, None, 2000 + index, 3, instant(0, index)) for index in range(65)], b'', []))),
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
    vector('short_identity', snapshot(5, frame('cognition.actor', number(2), identity(WREN)[1:], number(0),
                                                frame('cognition.acquired'), b'', b'', frame('cognition.memories'), b''))),
]
for item in list(valid):
    value = bytes.fromhex(item['hex'])
    invalid.append(vector(item['name'] + '_trailing', value + b'\0'))
    invalid.append(vector(item['name'] + '_truncated', value[:-1]))
print(json.dumps({'contract': 'cognition-v2', 'valid': valid, 'invalid': invalid}, indent=2))
