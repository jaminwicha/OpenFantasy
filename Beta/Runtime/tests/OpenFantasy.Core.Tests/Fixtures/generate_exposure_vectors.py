"""Independent exact exposure arithmetic and canonical framing oracle; stdout only."""
import hashlib
import json
import struct


def frame(tag,*fields):
    values=[tag.encode(),*fields]
    return b'OFC\0'+struct.pack('>II',1,len(values))+b''.join(struct.pack('>I',len(v))+v for v in values)


def num(value):return struct.pack('>Q',value)


def identity(value):return value.to_bytes(16,'big')


def state(milliseconds,numerator,denominator):
    units,remainder=divmod(milliseconds*numerator,denominator)
    days,within=divmod(milliseconds,86400000)
    encoded=frame('exposure.progress.v1',identity(1),identity(2),hashlib.sha256(b'exposure-rules-v1').digest(),
                  num(numerator),num(denominator),num(days),num(within),num(units),num(remainder))
    return {'milliseconds':str(milliseconds),'numerator':str(numerator),'denominator':str(denominator),
            'units':str(units),'remainder':str(remainder),'hex':encoded.hex(),'hash':hashlib.sha256(encoded).hexdigest()}


print(json.dumps({'cases':[state(1,1,3),state(10,1,3),state(86400001,7,13),
                           state((2**64-1)*86400000,1,2**64-1)],
                  'explorationStepsPerDay':48000,'tacticalUnitsPerExposureSecond':100},indent=2))
