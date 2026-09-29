"""Independent fixed-layout byte vectors; never imports or invokes the runtime codec."""
from hashlib import sha256
import json
from pathlib import Path
from struct import pack

def number(n):
    return pack(">Q", n)

def identity(n):
    return pack(">QQ", 0x0102030405060708, 0x1122334455660000 + n)

def frame(tag, *fields):
    values = [tag.encode("utf-8"), *fields]
    return b"OFC\0" + pack(">II", 1, len(values)) + b"".join(pack(">I", len(v)) + v for v in values)

def vector(data):
    return {"length": len(data), "hex": data.hex(), "sha256": sha256(data).hexdigest()}

examples = {}
for name, kind in [("countedSource", 1), ("produced", 2)]:
    terms = frame("authority.consented-material.terms", number(1),
        identity(1), identity(2), identity(23), identity(24), number(kind), identity(5),
        identity(10), sha256(b"consented-gift-golden-material-v1").digest(), identity(3), identity(4),
        number(0x0102030405060708), number(0x8182838485868788))
    command = frame("authority.consented-material.command", number(1), identity(20), identity(21), identity(22), terms)
    acceptance = frame("authority.consented-material.acceptance", number(1), identity(30), identity(31),
        identity(32), identity(20), sha256(command).digest(), terms)
    examples[name] = {"terms": vector(terms), "command": vector(command), "acceptance": vector(acceptance)}

if __name__ == "__main__":
    import sys
    Path(sys.argv[1]).write_text(json.dumps({"contract": "consented-material-v1", "examples": examples}, indent=2) + "\n")
    print({k: {n: v["length"] for n, v in fields.items()} for k, fields in examples.items()})
