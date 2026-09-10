"""Independent integer/OFC oracle for gathering component and profile-two request bytes."""
import hashlib
import json
import struct


def number(n):
    return struct.pack(">Q", n)


def identity(n):
    return n.to_bytes(16, "big")


def frame(tag, *fields):
    fields = (tag.encode(),) + fields
    return b"OFC\0" + struct.pack(">II", 1, len(fields)) + b"".join(struct.pack(">I", len(v)) + v for v in fields)


def result(value):
    return {"hex": value.hex(), "sha256": hashlib.sha256(value).hexdigest()}


intent = frame("world.action.gather", number(1), identity(501), number(3), identity(901), number(7))
action = frame("world.actor-action", number(2), identity(700), identity(20), identity(30), identity(10),
               identity(1), identity(2), number(9), intent)
movement = frame("world.control-sample", identity(20), identity(30), identity(10), struct.pack(">q", -32), struct.pack(">q", 0))
batch = frame("world.step-batch", number(2), identity(999), identity(1), identity(2), number(80), number(77), number(1),
              frame("world.step-movement", movement), frame("world.step-actions", action))
node_definition = frame("gather.node-definition", number(1), identity(601), number(1), identity(801), bytes.fromhex("aa" * 32),
                        number(5), number(900), number(20), number(1))
node = frame("gather.node-state", number(1), identity(501), node_definition, identity(701), number(160),
             struct.pack(">q", -256), struct.pack(">q", 768), struct.pack(">q", 0), number(3), number(3), number(2), b"")
tool = frame("gather.tool", number(1), identity(802), bytes.fromhex("bb" * 32), number(1), number(1))
rules = frame("gather.rules", number(1), number(1000), number(10), number(400), number(448), number(20))
work = frame("gather.actor-work", number(1), identity(10), number(1000))
snapshot = frame("gather.snapshot", number(1), identity(1), identity(2), number(77), rules,
                 frame("gather.nodes", node), frame("gather.tools", tool), frame("gather.actors", work))
print(json.dumps({"contract": "gathering-v1-and-world-actions-v2", "action": result(action),
                  "batch": result(batch), "snapshot": result(snapshot)}, indent=2))
