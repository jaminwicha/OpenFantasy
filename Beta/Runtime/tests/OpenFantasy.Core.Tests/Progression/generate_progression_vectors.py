#!/usr/bin/env python3
"""Independent integer/framing oracle. Print JSON; never overwrite a frozen fixture during verification."""
import hashlib
import json
import math
import struct


def number(value):
    return struct.pack('>Q', value)


def signed(value):
    return struct.pack('>q', value)


def identity(value):
    return value.to_bytes(16, 'big')


def frame(tag, *fields):
    fields = [tag.encode('utf-8'), *fields]
    return b'OFC\0' + struct.pack('>II', 1, len(fields)) + b''.join(
        struct.pack('>I', len(value)) + value for value in fields)


def fields_of(encoded):
    count = struct.unpack('>I', encoded[8:12])[0]
    result, position = [], 12
    for _ in range(count):
        size = struct.unpack('>I', encoded[position:position + 4])[0]
        position += 4
        result.append(encoded[position:position + size])
        position += size
    assert position == len(encoded)
    return result


def replace(encoded, index, value):
    values = fields_of(encoded)
    values[index] = value
    return frame(values[0].decode(), *values[1:])


def stats(values):
    return frame('progression.stats', number(1), *map(number, values))


def modifiers(values):
    return frame('progression.modifiers', number(1), *map(signed, values))


def profile(identifier, key, name, kind, basis, growth, heritage):
    return frame('progression.stat-profile', number(1), identity(identifier), key.encode(), name.encode(),
                 number(kind), stats(basis), modifiers(growth), modifiers(heritage))


def digest(value):
    return hashlib.sha256(value).digest()


zero = (0, 0, 0, 0, 0, 0)
basis = (60, 10, 12, 4, 8, 8)
growth = (9, 2, 3, 1, 2, 1)
squire = profile(100, 'human-squire.unequipped', 'Human Squire (unequipped)', 1, basis, growth, zero)
unassigned = profile(101, 'wren.unassigned', 'Unassigned', 2, (1, 0, 0, 0, 0, 1), zero, zero)
elf = profile(102, 'elf-squire.unequipped', 'Elf Squire (unequipped)', 1, basis, growth, (-8, 4, 0, 3, 0, 2))
thresholds = [math.isqrt(level ** 3 * 10000) for level in range(1, 10)]
curve = frame('progression.level-curve', number(1), identity(110), b'source.levels-1-10',
              frame('progression.thresholds', *map(number, thresholds)))
currency = frame('progression.currency', number(1), identity(120), b'gil', b'Gil', b'gil')
reward = frame('progression.reward', number(1), identity(130), b'wren.saplings.reward', number(420),
               currency, identity(200), number(200))


def account(identifier, kind, actor, balance, revision=0):
    return frame('progression.account', number(1), identity(identifier), number(kind),
                 b'' if actor is None else identity(actor), number(balance), number(revision))


def ledger(recipient=None):
    return frame('progression.currency-ledger', number(1), identity(1), identity(2), number(int(recipient is not None)),
                 currency, number(200), frame('progression.accounts',
                 account(200, 2, None, 200 if recipient is None else 0, int(recipient is not None)),
                 account(201, 1, 10, 200 if recipient == 10 else 0, int(recipient == 10)),
                 account(202, 1, 11, 200 if recipient == 11 else 0, int(recipient == 11))))


def actor(identifier, awarded=False):
    fixed = identifier == 11
    level, experience = 1, 420 if awarded else 0
    while level < 10 and experience >= thresholds[level - 1]:
        experience -= thresholds[level - 1]
        level += 1
    effective = (1, 0, 0, 0, 0, 1) if fixed else tuple(
        value + per_level * (level - 1) for value, per_level in zip(basis, growth))
    return frame('progression.actor', number(1), identity(identifier), number(int(awarded)),
                 identity(101 if fixed else 100), digest(unassigned if fixed else squire), identity(110), digest(curve),
                 number(level), number(experience), number(effective[0]), number(effective[1]))


def snapshot(recipient=None):
    return frame('progression.snapshot', number(1), identity(1), identity(2), number(int(recipient is not None)),
                 frame('progression.profiles', squire, unassigned), frame('progression.curves', curve),
                 frame('progression.actors', actor(10, recipient == 10), actor(11, recipient == 11)), ledger(recipient))


examples = []
for name, kind, encoded in [
    ('human_squire', 'profile', squire), ('unassigned', 'profile', unassigned), ('elf_squire', 'profile', elf),
    ('source_curve', 'curve', curve), ('gil', 'currency', currency), ('wren_reward', 'reward', reward),
    ('initial_ledger', 'ledger', ledger()), ('initial', 'snapshot', snapshot()),
    ('rewarded_player', 'snapshot', snapshot(10)), ('rewarded_npc', 'snapshot', snapshot(11)),
]:
    examples.append({'name': name, 'kind': kind, 'hex': encoded.hex(), 'sha256': digest(encoded).hex()})

bad = []


def malformed(name, kind, encoded):
    bad.append({'name': name, 'kind': kind, 'hex': encoded.hex()})


initial = snapshot()
malformed('unknown_wire_version', 'snapshot', initial[:4] + struct.pack('>I', 2) + initial[8:])
malformed('unknown_payload_version', 'snapshot', replace(initial, 1, number(2)))
malformed('unknown_tag', 'snapshot', replace(initial, 0, b'progression.unknown'))
malformed('trailing_byte', 'snapshot', initial + b'\0')
malformed('oversized_field_declaration', 'snapshot', initial[:12] + b'\xff\xff\xff\xff' + initial[16:])
malformed('duplicate_profile', 'snapshot', replace(initial, 5, frame('progression.profiles', squire, squire)))
malformed('reordered_profiles', 'snapshot', replace(initial, 5, frame('progression.profiles', unassigned, squire)))
malformed('reordered_actors', 'snapshot', replace(initial, 7, frame('progression.actors', actor(11), actor(10))))
malformed('missing_actor', 'snapshot', replace(initial, 7, frame('progression.actors', actor(10))))
malformed('unknown_profile_pin', 'snapshot', replace(initial, 7,
          frame('progression.actors', replace(actor(10), 5, bytes(32)), actor(11))))
malformed('unknown_curve_pin', 'snapshot', replace(initial, 7,
          frame('progression.actors', replace(actor(10), 7, bytes(32)), actor(11))))
malformed('unnormalized_experience', 'snapshot', replace(initial, 7,
          frame('progression.actors', replace(actor(10), 9, number(100)), actor(11))))
malformed('health_above_maximum', 'snapshot', replace(initial, 7,
          frame('progression.actors', replace(actor(10), 10, number(61)), actor(11))))
malformed('level_zero', 'snapshot', replace(initial, 7,
          frame('progression.actors', replace(actor(10), 8, number(0)), actor(11))))
malformed('minted_supply', 'ledger', replace(ledger(), 6, number(201)))
malformed('reordered_accounts', 'ledger', replace(ledger(), 7, frame('progression.accounts',
          account(202, 1, 11, 0), account(201, 1, 10, 0), account(200, 2, None, 200))))
malformed('reserve_impersonates_actor', 'ledger', replace(ledger(), 7, frame('progression.accounts',
          account(200, 2, 11, 200), account(201, 1, 10, 0), account(202, 1, 11, 0))))
malformed('account_aliases_actor', 'ledger', replace(ledger(), 7, frame('progression.accounts',
          account(10, 1, 10, 0), account(200, 2, None, 200), account(202, 1, 11, 0))))
malformed('unknown_profile_kind', 'profile', replace(squire, 5, number(99)))
malformed('fixed_profile_with_growth', 'profile', replace(unassigned, 7, modifiers(growth)))
malformed('invalid_utf8', 'profile', replace(squire, 3, b'\xff'))
malformed('oversized_key', 'profile', replace(squire, 3, b'x' * 129))
malformed('short_numeric_field', 'profile', replace(squire, 5, b'\x01'))
malformed('modifier_outside_int32', 'profile', replace(squire, 7,
          frame('progression.modifiers', number(1), signed(2**31), *map(signed, zero[1:]))))
malformed('zero_threshold', 'curve', replace(curve, 4, frame('progression.thresholds', number(0))))
malformed('too_many_levels', 'curve', replace(curve, 4, frame('progression.thresholds', *[number(1)] * 256)))
malformed('reward_aliases_reserve', 'reward', replace(reward, 2, identity(200)))
malformed('currency_blank_unit', 'currency', replace(currency, 5, b' '))
malformed('extra_reward_field', 'reward', frame('progression.reward', *fields_of(reward)[1:], number(0)))

print(json.dumps({'format': 'progression-v1', 'thresholds': thresholds, 'examples': examples, 'malformed': bad}, indent=2))
