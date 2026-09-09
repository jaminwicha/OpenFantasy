#!/usr/bin/env python3
"""Independent standard-library command wire reference; never imports production code."""
import hashlib
import json
import struct


def number(value):
    return struct.pack(">Q", value)


def identity(value):
    return value.to_bytes(16, "big")


def frame(tag, *fields):
    fields = [tag.encode("utf-8"), *fields]
    return b"OFC\0" + struct.pack(">II", 1, len(fields)) + b"".join(
        struct.pack(">I", len(field)) + field for field in fields)


def revision(kind, target, value):
    return frame("command.expected-revision", number(1), number(kind), target, number(value))


ids = {name: identity(index) for index, name in enumerate(
    ["command", "principal", "originSession", "actor", "world", "branch", "lot", "source", "destination"], 1)}
revision_values = [(2, ids["branch"], 12), (1, ids["lot"], 7), (1, ids["source"], 9)]
revision_fields = [revision(*value) for value in sorted(revision_values)]
revisions = frame("command.expected-revisions", number(1), *revision_fields)
payload_fields = [number(1), ids["lot"], ids["source"], ids["destination"], number(7)]
payload = frame("command.transfer-lot", *payload_fields)
fields = [number(1), *(ids[name] for name in ["command", "principal", "originSession", "actor", "world", "branch"]), revisions, payload]
command = frame("command.envelope", *fields)
content_hash = hashlib.sha256(command).digest()
receipt_fields = [number(1), ids["command"], ids["principal"], ids["world"], ids["branch"], content_hash, number(42), number(1), number(0)]
applied_receipt = frame("command.receipt", *receipt_fields)
rejected_fields = receipt_fields[:-2] + [number(2), number(1)]
rejected_receipt = frame("command.receipt", *rejected_fields)


def replace_field(values, index, value):
    result = list(values)
    result[index] = value
    return result


def invalid_command(name, expected_failure, data):
    return {"name": name, "failure": expected_failure, "hex": data.hex()}


invalid_commands = [
    invalid_command("unknown protocol", "UnsupportedVersion", frame("command.envelope", *replace_field(fields, 0, number(2)))),
    invalid_command("unknown envelope type", "UnknownType", frame("command.unknown", *fields)),
    invalid_command("short identity", "InvalidValue", frame("command.envelope", *replace_field(fields, 1, b"\0" * 15))),
    invalid_command("short protocol integer", "InvalidValue", frame("command.envelope", *replace_field(fields, 0, b"\1"))),
    invalid_command("unknown payload type", "UnknownType", frame("command.envelope", *replace_field(fields, 8, frame("command.unknown", *payload_fields)))),
    invalid_command("unknown payload version", "UnsupportedVersion", frame("command.envelope", *replace_field(fields, 8, frame("command.transfer-lot", *replace_field(payload_fields, 0, number(2)))))),
    invalid_command("zero transfer quantity", "InvalidValue", frame("command.envelope", *replace_field(fields, 8, frame("command.transfer-lot", *replace_field(payload_fields, 4, number(0)))))),
    invalid_command("same custodians", "InvalidValue", frame("command.envelope", *replace_field(fields, 8, frame("command.transfer-lot", *replace_field(payload_fields, 3, ids["source"]))))),
    invalid_command("duplicate payload field", "NonCanonical", frame("command.envelope", *replace_field(fields, 8, frame("command.transfer-lot", *payload_fields, number(7))))),
    invalid_command("duplicate envelope field", "NonCanonical", frame("command.envelope", *fields, payload)),
    invalid_command("repeated revision target", "NonCanonical", frame("command.envelope", *replace_field(fields, 7, frame("command.expected-revisions", number(1), revision_fields[0], revision_fields[0])))),
    invalid_command("unsorted revisions", "NonCanonical", frame("command.envelope", *replace_field(fields, 7, frame("command.expected-revisions", number(1), *reversed(revision_fields))))),
    invalid_command("unknown revision kind", "UnknownType", frame("command.envelope", *replace_field(fields, 7, frame("command.expected-revisions", number(1), revision(3, ids["lot"], 1))))),
    invalid_command("wrong branch revision", "InvalidValue", frame("command.envelope", *replace_field(fields, 7, frame("command.expected-revisions", number(1), revision(2, ids["world"], 1))))),
    invalid_command("unknown revision set version", "UnsupportedVersion", frame("command.envelope", *replace_field(fields, 7, frame("command.expected-revisions", number(2))))),
    invalid_command("unknown nested canonical version", "UnsupportedVersion", frame("command.envelope", *replace_field(fields, 8, payload[:4] + struct.pack(">I", 2) + payload[8:]))),
    invalid_command("too many revisions", "LimitExceeded", frame("command.envelope", *replace_field(fields, 7, frame("command.expected-revisions", number(1), *(revision(1, identity(index), 0) for index in range(65)))))),
    invalid_command("trailing outer bytes", "TrailingData", command + b"\0"),
    invalid_command("trailing nested bytes", "TrailingData", frame("command.envelope", *replace_field(fields, 8, payload + b"\0"))),
    invalid_command("invalid magic", "InvalidFrame", b"BAD!" + command[4:]),
    invalid_command("unknown canonical version", "UnsupportedVersion", command[:4] + struct.pack(">I", 2) + command[8:]),
    invalid_command("unbounded field count", "LimitExceeded", command[:8] + struct.pack(">I", 0xffffffff) + command[12:]),
    invalid_command("unbounded tag length", "LimitExceeded", command[:12] + struct.pack(">I", 0xffffffff) + command[16:]),
]
invalid_receipts = [
    invalid_command("unknown receipt version", "UnsupportedVersion", frame("command.receipt", *replace_field(receipt_fields, 0, number(2)))),
    invalid_command("zero commit", "InvalidValue", frame("command.receipt", *replace_field(receipt_fields, 6, number(0)))),
    invalid_command("unknown status", "UnknownType", frame("command.receipt", *replace_field(receipt_fields, 7, number(3)))),
    invalid_command("unknown rejection reason", "UnknownType", frame("command.receipt", *replace_field(receipt_fields, 8, number(5)))),
    invalid_command("applied with rejection", "InvalidValue", frame("command.receipt", *replace_field(receipt_fields, 8, number(1)))),
    invalid_command("rejected without reason", "InvalidValue", frame("command.receipt", *replace_field(receipt_fields, 7, number(2)))),
    invalid_command("short hash", "InvalidValue", frame("command.receipt", *replace_field(receipt_fields, 5, b"\0" * 31))),
    invalid_command("duplicate receipt field", "NonCanonical", frame("command.receipt", *receipt_fields, number(0))),
]
fixture = {
    "format": "commands-v1", "provenance": "Python standard library, independent of production C#",
    "ids": {key: value.hex() for key, value in ids.items()},
    "expectedRevisions": [{"kind": kind, "id": target.hex(), "revision": str(value)} for kind, target, value in revision_values],
    "quantity": "7", "commandHex": command.hex(), "contentHash": content_hash.hex(),
    "appliedReceiptHex": applied_receipt.hex(), "appliedResultId": hashlib.sha256(applied_receipt).digest()[:16].hex(),
    "rejectedReceiptHex": rejected_receipt.hex(), "rejectedResultId": hashlib.sha256(rejected_receipt).digest()[:16].hex(),
    "branchMismatchReceiptHex": frame("command.receipt", *replace_field(receipt_fields, 4, ids["world"])).hex(),
    "invalidCommands": invalid_commands, "invalidReceipts": invalid_receipts,
}
print(json.dumps(fixture, indent=2))
