"""Malformed wire oracle derived only from the frozen independent custody byte fixture."""
import json
import struct
from pathlib import Path


def frame(fields):
    return b'OFC\0' + struct.pack('>II', 1, len(fields)) + b''.join(
        struct.pack('>I', len(field)) + field for field in fields)


def unpack(data):
    count = struct.unpack('>I', data[8:12])[0]
    result = []
    offset = 12
    for _ in range(count):
        size = struct.unpack('>I', data[offset:offset+4])[0]
        result.append(data[offset+4:offset+4+size])
        offset += 4 + size
    if offset != len(data):
        raise ValueError('Expected complete independent reference frame')
    return result


def number(value):
    return struct.pack('>Q', value)


reference = json.loads(Path(__file__).with_name('custody-v1.json').read_text())
original = bytes.fromhex(reference['originalHex'])
candidate = bytes.fromhex(reference['candidateHex'])
fields = unpack(original)
storages, lots = unpack(fields[4]), unpack(fields[5])
lot = unpack(lots[1])
cases = []


def add(name, failure, data):
    cases.append({'name': name, 'failure': failure, 'hex': data.hex()})


def changed(index, value):
    return frame([value if at == index else field for at, field in enumerate(fields)])


def changed_lot(index, value):
    entry = frame([value if at == index else field for at, field in enumerate(lot)])
    return changed(5, frame([lots[0], entry]))


add('magic', 'InvalidFrame', b'BAD!' + original[4:])
add('canonical version', 'UnsupportedVersion', original[:4] + struct.pack('>I', 2) + original[8:])
add('root field count', 'LimitExceeded', original[:8] + struct.pack('>I', 0xffffffff) + original[12:])
add('root tag', 'UnknownType', changed(0, b'custody.snapshot.v2'))
add('short identity', 'InvalidValue', changed(1, b'\0'*15))
add('short revision', 'InvalidValue', changed(3, b'\0'))
add('trailing root byte', 'TrailingData', original+b'\0')
add('extra root field', 'LimitExceeded', frame([*fields,b'']))
add('unsorted storage', 'NonCanonical', changed(4, frame([storages[0],*reversed(storages[1:])])) )
add('duplicate storage', 'NonCanonical', changed(4, frame([storages[0],storages[1],storages[1]])))
add('storage count limit', 'LimitExceeded', changed(4, fields[4][:8]+struct.pack('>I',258)+fields[4][12:]))
add('lot count limit', 'LimitExceeded', changed(5, fields[5][:8]+struct.pack('>I',4098)+fields[5][12:]))
add('short hash', 'InvalidValue', changed_lot(4, lot[4][:-1]))
add('reservation width', 'InvalidValue', changed_lot(10,b'\0'))
add('unknown protection', 'InvalidValue', changed_lot(9,number(16)))
add('protection truncation alias', 'InvalidValue', changed_lot(9,number(1<<32)))
add('empty claim', 'InvalidValue', changed_lot(7,lot[6]))
add('reversed claim', 'InvalidValue', changed_lot(7,number(9)))
add('unknown custodian', 'InvalidValue', changed_lot(2,(99).to_bytes(16,'big')))
add('lot storage identity collision', 'InvalidValue', changed_lot(1,(3).to_bytes(16,'big')))
add('short lot tag', 'UnknownType', changed_lot(0,b'lot'))
low_storage=unpack(storages[1]); low_storage[2]=number(7)
add('capacity exceeded', 'InvalidValue', changed(4,frame([storages[0],frame(low_storage),storages[2]])))
second=list(lot);second[1]=(7).to_bytes(16,'big');second[2]=(4).to_bytes(16,'big')
add('overlapping claims', 'InvalidValue', changed(5,frame([lots[0],lots[1],frame(second)])))
second[6]=number(18);second[7]=number(20);second[4]=bytes(32)
add('inconsistent source unit', 'InvalidValue', changed(5,frame([lots[0],lots[1],frame(second)])))
fields2=unpack(candidate);lots2=unpack(fields2[5]);fields2[5]=frame([lots2[0],*reversed(lots2[1:])])
add('unsorted lots', 'NonCanonical',frame(fields2))
add('duplicate lots', 'NonCanonical',changed(5,frame([lots[0],lots[1],lots[1]])))
add('declared impossible root length', 'LimitExceeded', original[:12]+struct.pack('>I',0xffffffff)+original[16:])
print(json.dumps({'source':'independent custody-v1 snapshot frames','cases':cases},indent=2))
