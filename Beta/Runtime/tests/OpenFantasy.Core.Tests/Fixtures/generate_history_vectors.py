"""Independent exact history request oracle; uses no application code."""
import hashlib
import json
import struct


def frame(tag, fields):
    fields = [tag.encode("ascii"), *fields]
    return b"OFC\0" + struct.pack(">II", 1, len(fields)) + b"".join(
        struct.pack(">I", len(field)) + field for field in fields
    )


def request(case):
    fields = [bytes.fromhex(case[name]) for name in ("world", "branch", "realm")]
    fields += [struct.pack(">q", int(case[name])) for name in ("x", "y", "z")]
    fields += [struct.pack(">qI", int(case[name][0]), case[name][1])
               for name in ("origin", "head", "requested")]
    fields += [struct.pack(">Q", int(case["pinnedRevision"]))]
    fields += [b"" if case["cursor"] is None else struct.pack(">Q", int(case["cursor"]))]
    encoded = frame("history.request.v1", fields)
    return dict(**case, canonicalHex=encoded.hex(), byteLength=len(encoded),
                sha256=hashlib.sha256(encoded).hexdigest())


base = dict(world="00112233445566778899aabbccddeeff",
            branch="fedcba98765432100123456789abcdef",
            realm="00000000000000000000000000000003",
            x="-9223372036854775808", y="9223372036854775807", z="-257",
            origin=["-9223372036854775808", 0],
            head=["9223372036854775807", 86399999],
            requested=["-1", 86399999], pinnedRevision="18446744073709551615")
cases = [request(dict(**base, name="all-same-instant-commits", cursor=None)),
         request(dict(**base, name="earlier-commit-boundary", cursor="42")),
         request(dict(**base, name="empty-committed-prefix", cursor="0"))]
print(json.dumps(dict(contract="history.request.v1", cases=cases), indent=2))
