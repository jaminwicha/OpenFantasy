"""Independent Python/hashlib oracle for the frozen v1 byte contract.

Run this script to stdout and compare with reference-v1.json. It imports no
production code and deliberately uses a separate, simple encoding implementation.
"""
import hashlib
import json
import struct


def frame(tag, *fields):
    parts = [tag.encode("utf-8"), *fields]
    return b"OFC\0" + struct.pack(">II", 1, len(parts)) + b"".join(
        struct.pack(">I", len(part)) + part for part in parts
    )


def u64(value):
    return struct.pack(">Q", value)


def utf8(value):
    return value.encode("utf-8", errors="strict")


seed = bytes.fromhex("000102030405060708090a0b0c0d0e0f")
owner = bytes.fromhex("00112233445566778899aabbccddeeff")
generated = []
for version, address, role, ordinal in [
    ("geology/1", "realm:0/cell:-1,2,3", "ore-vein", 7),
    ("v1", "á/水/🌲", "npc", 0),
    ("ab", "c", "entity", 18446744073709551615),
    ("a", "bc", "entity", 18446744073709551615),
]:
    payload = frame("entity.generated", seed, utf8(version), utf8(address), utf8(role), u64(ordinal))
    generated.append(dict(version=version, address=address, role=role, ordinal=str(ordinal),
                          payloadHex=payload.hex(), id=hashlib.sha256(payload).hexdigest()[:32]))
spawn_payload = frame("entity.spawned", owner, u64(42))
key_fields = (seed, utf8("rules/1"), utf8("combat.hit"), owner)
key_payload = frame("rng.sha256-counter.key", *key_fields)

def draw(counter):
    payload = frame("rng.sha256-counter.draw", *key_fields, u64(counter))
    return int.from_bytes(hashlib.sha256(payload).digest()[:8], "big")

bound = 2**63 + 1
threshold = 2**64 % bound
start = next(counter for counter in range(100) if draw(counter) < threshold)
counter = start
while draw(counter) < threshold:
    counter += 1

exhaustion_domain = "rng.exhaustion/0"
for suffix in range(100):
    exhaustion_domain = f"rng.exhaustion/{suffix}"
    payload = frame("rng.sha256-counter.draw", seed, utf8("rules/1"), utf8(exhaustion_domain), owner, u64(2**64 - 1))
    exhaustion_word = int.from_bytes(hashlib.sha256(payload).digest()[:8], "big")
    if exhaustion_word < threshold:
        break
else:
    raise RuntimeError("Could not find the reference exhaustion case")

result = dict(
    encodingVersion=1, seedHex=seed.hex(), responsibleId=owner.hex(),
    generated=generated,
    spawned=dict(ordinal="42", payloadHex=spawn_payload.hex(),
                 id=hashlib.sha256(spawn_payload).hexdigest()[:32]),
    random=dict(rules="rules/1", domain="combat.hit",
                keyFingerprint=hashlib.sha256(key_payload).hexdigest(),
                words=[str(draw(i)) for i in range(8)],
                maxCounterWord=str(draw(2**64-1)),
                rejectedDraw=dict(startCounter=str(start), exclusiveMax=str(bound),
                                  result=str(draw(counter) % bound), nextCounter=str(counter+1)),
                exhaustionRejectedDraw=dict(domain=exhaustion_domain, word=str(exhaustion_word),
                                            exclusiveMax=str(bound)))
)
print(json.dumps(result, ensure_ascii=False, indent=2))
