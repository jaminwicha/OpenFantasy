"""Independent public resource-observation bytes; no application imports or private causes."""
import hashlib
import json
import struct


def u64(n):
    return struct.pack('>Q', n)


def identity(n):
    return n.to_bytes(16, 'big')


def frame(tag, *fields):
    fields = (tag.encode('utf-8'), *fields)
    return b'OFC\0' + struct.pack('>II', 1, len(fields)) + b''.join(
        struct.pack('>I', len(field)) + field for field in fields)


def encode_entry(row):
    return frame('observation.resource', u64(1), identity(row['disclosureId']),
                 u64(row['disclosureRevision']), identity(row['knownSubjectId']),
                 identity(row['materialId']), bytes.fromhex(row['materialVersion']),
                 u64(int(row['quantity'])), u64(row['basis']), u64(row['confidence']),
                 struct.pack('>q', int(row['acquiredDays'])), u64(row['acquiredMilliseconds']),
                 b'' if row['knownReporterId'] is None else identity(row['knownReporterId']))


def main():
    audience = dict(principal=1, session=2, world=3, branch=4, actor=5, controlRevision=7)
    audience_bytes = frame('observation.audience', u64(1), *(identity(audience[k]) for k in
        ('principal', 'session', 'world', 'branch', 'actor')), u64(audience['controlRevision']))
    material_version = hashlib.sha256(b'known-wood-definition-v1').hexdigest()
    rows = [dict(disclosureId=11, disclosureRevision=1, knownSubjectId=20, materialId=30,
                 materialVersion=material_version, quantity='6', basis=1, confidence=900,
                 acquiredDays='-2', acquiredMilliseconds=7, knownReporterId=None),
            dict(disclosureId=12, disclosureRevision=3, knownSubjectId=21, materialId=30,
                 materialVersion=material_version, quantity='99', basis=2, confidence=350,
                 acquiredDays='1', acquiredMilliseconds=8, knownReporterId=22),
            dict(disclosureId=13, disclosureRevision=4, knownSubjectId=23, materialId=30,
                 materialVersion=material_version, quantity='18446744073709551615', basis=5,
                 confidence=0, acquiredDays='-9223372036854775808',
                 acquiredMilliseconds=86399999, knownReporterId=None)]
    samples = []
    for name, revision, entries in [('empty', 0, []), ('known', 4, rows), ('cleared', 5, [])]:
        payload = frame('observation.snapshot', u64(1), audience_bytes, identity(9), u64(revision),
                        frame('observation.resources', u64(1), *(encode_entry(row) for row in entries)))
        samples.append(dict(name=name, epoch=9, disclosureRevision=revision, entries=entries,
                            hex=payload.hex(), hash=hashlib.sha256(payload).hexdigest()))
    print(json.dumps(dict(format='observations-v1', audience=audience, cases=samples), indent=2))


if __name__ == '__main__':
    main()
