"""Independent V2 consumption vectors; reuses retained independent V1 recipe bytes as data.

Pure Python, no runtime invocation or C# imports. Writes JSON to stdout only.
"""
import hashlib
import json
from pathlib import Path
import struct


def number(value):
    return struct.pack(">Q", value)


def identity(value):
    return value if isinstance(value, bytes) else value.to_bytes(16, "big")


def frame(tag, *values):
    values = (tag.encode(),) + values
    return b"OFC\0" + struct.pack(">II", 1, len(values)) + b"".join(
        struct.pack(">I", len(value)) + value for value in values)


def fields(value):
    assert value[:8] == b"OFC\0\0\0\0\1"
    offset, result = 12, []
    for _ in range(struct.unpack(">I", value[8:12])[0]):
        length = struct.unpack(">I", value[offset:offset + 4])[0]
        offset += 4
        result.append(value[offset:offset + length])
        offset += length
    assert offset == len(value)
    return result


def replace(value, index, replacement):
    parts = fields(value)
    parts[index] = replacement
    return frame(parts[0].decode(), *parts[1:])


def spawned(command, ordinal):
    return hashlib.sha256(frame("entity.spawned", identity(command), number(ordinal))).digest()[:16]


def raw(start, end):
    return frame("production.source-claim", number(1), identity(71), number(start), number(end))


def output(start, end):
    return frame("production.output-claim", number(1), spawned(6000, 0), number(0), number(start), number(end))


def claim(command, ingredient):
    return frame("production.consumed-item", number(1), spawned(command, 0), identity(command),
                 identity(10), identity(11), identity(60), ingredient)


def snapshot(revision, batches, lots, claims):
    return frame("production.snapshot", number(2), identity(1), identity(2), number(revision), recipes,
                 frame("production.batches", *sorted(batches, key=lambda b: fields(b)[2])),
                 frame("production.lots", *sorted(lots, key=lambda b: fields(b)[2])),
                 frame("production.consumed", *sorted(claims, key=lambda b: fields(b)[2])))


def vector(value):
    return {"hex": value.hex(), "sha256": hashlib.sha256(value).hexdigest(), "bytes": len(value)}


old = json.loads(Path(__file__).with_name("production-ledger-v1.json").read_text())
empty_v1 = bytes.fromhex(old["empty"]["hex"])
crafted_v1 = bytes.fromhex(old["crafted"]["hex"])
assert hashlib.sha256(empty_v1).hexdigest() == old["empty"]["sha256"]
assert hashlib.sha256(crafted_v1).hexdigest() == old["crafted"]["sha256"]
recipes = fields(empty_v1)[5]
batch = fields(fields(crafted_v1)[6])[1]
ingredients = fields(fields(batch)[8])[1:]
berry_ingredient = next(i for i in ingredients if fields(i)[3] == identity(70))
wood_ingredient = next(i for i in ingredients if fields(i)[3] == identity(72))
lots = fields(fields(crafted_v1)[7])[1:]
ration = next(i for i in lots if fields(i)[2] == spawned(6000, 1))
waste = next(i for i in lots if fields(i)[2] == spawned(6000, 2))
ration_parts = fields(ration)
ration_ingredient = frame("production.ingredient", number(1), number(2), spawned(6000, 1),
                          number(0), ration_parts[4], ration_parts[5], output(0, 1))
remaining_ration = replace(replace(ration, 6, output(1, 2)), 7, number(1))
raw_claim = claim(8000, berry_ingredient)
produced_claim = claim(8001, ration_ingredient)
later_berries = replace(replace(berry_ingredient, 4, number(1)), 7, raw(2, 4))
later_batch = replace(batch, 8, frame("production.retired", later_berries, wood_ingredient))
enabled = snapshot(0, [], [], [])
raw_consumed = snapshot(1, [], [], [raw_claim])
produced_consumed = snapshot(2, [batch], [remaining_ration, waste], [produced_claim])
mixed = snapshot(3, [later_batch], [remaining_ration, waste], [raw_claim, produced_claim])
invalid = [
    {"name": "recipe reuses a consumed raw prefix", "hex": snapshot(
        3, [batch], [remaining_ration, waste], [raw_claim, produced_claim]).hex()},
    {"name": "consumed produced output silently dropped", "hex": snapshot(
        2, [batch], [remaining_ration, waste], []).hex()},
]
print(json.dumps({"contract": "production-consumption-v2",
                  "basis": "independent retained production-ledger-v1 recipe/batch bytes",
                  "enabled": vector(enabled), "raw": vector(raw_consumed),
                  "produced": vector(produced_consumed), "mixed": vector(mixed),
                  "malformed": invalid}, indent=2))
