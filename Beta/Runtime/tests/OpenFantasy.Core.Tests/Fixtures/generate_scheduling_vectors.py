#!/usr/bin/env python3
"""Independent calendar queue oracle. Print JSON; never overwrite retained expectations."""
import hashlib
import json
import struct


def number(value):
    return struct.pack(">Q", value)


def signed(value):
    return struct.pack(">q", value)


def identity(value):
    return value.to_bytes(16, "big")


def frame(tag, *fields):
    fields = [tag.encode("utf-8"), *fields]
    return b"OFC\0" + struct.pack(">II", 1, len(fields)) + b"".join(
        struct.pack(">I", len(field)) + field for field in fields)


def entry(event, days, millis, phase, domain, actor, work, continuation):
    return dict(eventId=identity(event).hex(), days=str(days), milliseconds=millis, phase=phase,
                domainId=identity(domain).hex(), actorId=identity(actor).hex(),
                workItemId=identity(work).hex(), continuationOrdinal=str(continuation))


def order(value):
    return (int(value["days"]), value["milliseconds"], value["phase"], value["domainId"],
            value["actorId"], value["eventId"])


def work_fields(value):
    return [number(1), bytes.fromhex(value["eventId"]), signed(int(value["days"])),
            number(value["milliseconds"]), number(value["phase"]), bytes.fromhex(value["domainId"]),
            bytes.fromhex(value["actorId"]), bytes.fromhex(value["workItemId"]),
            number(int(value["continuationOrdinal"]))]


def work_bytes(value):
    return frame("scheduler.calendar-work", *work_fields(value))


def replace(values, index, value):
    result = list(values)
    result[index] = value
    return result


scope = dict(worldId=identity(101).hex(), branchId=identity(102).hex(), clockId=identity(103).hex())
revision = 41
input_entries = [
    entry(8, 1, 0, 10, 1, 10, 108, 0),
    entry(6, 0, 0, 30, 1, 10, 106, 2),
    entry(3, 0, 0, 10, 1, 20, 103, 0),
    entry(1, -1, 86_399_999, 40, 2, 10, 101, (1 << 64) - 1),
    entry(7, 0, 0, 50, 1, 10, 107, 3),
    entry(5, 0, 0, 10, 1, 10, 105, 0),
    entry(2, 0, 0, 10, 2, 10, 102, 0),
    entry(4, 0, 0, 10, 1, 10, 104, 0),
    entry(10, (1 << 63) - 1, 86_399_999, 10, 1, 10, 110, 0),
    entry(9, -(1 << 63), 0, 50, 1, 10, 109, 0),
]
entries = sorted(input_entries, key=order)
header = [number(1), *(bytes.fromhex(scope[name]) for name in ["worldId", "branchId", "clockId"]), number(revision)]
encoded_entries = list(map(work_bytes, entries))
snapshot = frame("scheduler.calendar-queue", *header, *encoded_entries)
selected = [value for value in entries if (int(value["days"]), value["milliseconds"]) <= (0, 0) and value["phase"] == 10][:2]
selected_ids = [value["eventId"] for value in selected]
pending = [value for value in entries if value["eventId"] not in selected_ids]
replacement = frame("scheduler.calendar-queue", *replace(header, 4, number(revision + 1)), *map(work_bytes, pending))
empty = frame("scheduler.calendar-queue", *replace(header, 4, number(0)))


def invalid(name, failure, data):
    return dict(name=name, failure=failure, hex=data.hex())


def with_entry(data):
    return frame("scheduler.calendar-queue", *header, data)


fields = work_fields(entries[0])
invalid_snapshots = [
    invalid("bad magic", "InvalidFrame", b"BAD!" + snapshot[4:]),
    invalid("unknown framing", "UnsupportedVersion", snapshot[:4] + struct.pack(">I", 2) + snapshot[8:]),
    invalid("unknown queue tag", "UnknownType", frame("scheduler.unknown", *header)),
    invalid("unknown queue version", "UnsupportedVersion", frame("scheduler.calendar-queue", *replace(header, 0, number(2)))),
    invalid("short scope identity", "InvalidValue", frame("scheduler.calendar-queue", *replace(header, 1, b"\0" * 15))),
    invalid("noncanonical ordering", "NonCanonical", frame("scheduler.calendar-queue", *header, *reversed(encoded_entries))),
    invalid("duplicate event", "NonCanonical", frame("scheduler.calendar-queue", *header, encoded_entries[0], encoded_entries[0])),
    invalid("duplicate event different time", "NonCanonical", frame("scheduler.calendar-queue", *header, encoded_entries[0], frame("scheduler.calendar-work", *replace(fields, 2, signed(0))))),
    invalid("unknown work type", "UnknownType", with_entry(frame("scheduler.unknown", *fields))),
    invalid("unknown work version", "UnsupportedVersion", with_entry(frame("scheduler.calendar-work", *replace(fields, 0, number(2))))),
    invalid("short signed day", "InvalidValue", with_entry(frame("scheduler.calendar-work", *replace(fields, 2, b"\0" * 7)))),
    invalid("out of range day milliseconds", "InvalidValue", with_entry(frame("scheduler.calendar-work", *replace(fields, 3, number(86_400_000))))),
    invalid("maximum integer milliseconds", "InvalidValue", with_entry(frame("scheduler.calendar-work", *replace(fields, 3, number((1 << 64) - 1))))),
    invalid("command phase forbidden", "UnknownType", with_entry(frame("scheduler.calendar-work", *replace(fields, 4, number(20))))),
    invalid("unknown phase", "UnknownType", with_entry(frame("scheduler.calendar-work", *replace(fields, 4, number((1 << 64) - 1))))),
    invalid("extra work field", "NonCanonical", with_entry(frame("scheduler.calendar-work", *fields, number(0)))),
    invalid("nested trailing bytes", "TrailingData", with_entry(encoded_entries[0] + b"\0")),
    invalid("trailing snapshot bytes", "TrailingData", snapshot + b"\0"),
    invalid("too many retained entries", "LimitExceeded", snapshot[:8] + struct.pack(">I", 1031) + snapshot[12:]),
    invalid("unbounded field length", "LimitExceeded", snapshot[:12] + struct.pack(">I", 0xffffffff) + snapshot[16:]),
]
print(json.dumps(dict(
    format="scheduling-v1", provenance="Python standard-library reference; independent of production C#",
    scope=scope, revision=str(revision), entries=input_entries,
    canonicalEventOrder=[value["eventId"] for value in entries],
    snapshotHex=snapshot.hex(), snapshotSha256=hashlib.sha256(snapshot).hexdigest(),
    selection=dict(cutoffDays="0", cutoffMilliseconds=0, phase=10, maximumCount=2,
                   selectedEventIds=selected_ids, pendingCount=len(pending), hasMoreEligibleWork=True),
    replacementHex=replacement.hex(), replacementSha256=hashlib.sha256(replacement).hexdigest(),
    emptySnapshotHex=empty.hex(), invalidSnapshots=invalid_snapshots,
), indent=2))
