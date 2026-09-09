"""Independent hashlib oracle for package identity; imports no application code."""
import hashlib
import json
import struct


def frame(tag, *fields):
    fields = [tag.encode(), *fields]
    return b"OFC\0" + struct.pack(">II", 1, len(fields)) + b"".join(
        struct.pack(">I", len(field)) + field for field in fields
    )


payload = b'{"kind":"source-preservation-fixture","schema":1}\n'
payload_hash = hashlib.sha256(payload).hexdigest()
base = frame("package.manifest.v1", b"openfantasy.rules", bytes.fromhex(payload_hash))
base_hash = hashlib.sha256(base).hexdigest()
dependency = frame("package.dependency.v1", b"openfantasy.rules", bytes.fromhex(base_hash))
dependent = frame("package.manifest.v1", b"openfantasy.aldermere", bytes.fromhex(payload_hash), dependency)
print(json.dumps(dict(payloadText=payload.decode(), payloadSha256=payload_hash,
                     baseCanonicalHex=base.hex(), baseVersion=base_hash,
                     dependentCanonicalHex=dependent.hex(),
                     dependentVersion=hashlib.sha256(dependent).hexdigest()), indent=2))
