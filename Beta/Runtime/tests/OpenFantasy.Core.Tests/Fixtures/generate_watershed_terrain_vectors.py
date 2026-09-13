"""Independent finite-footprint geometry and ancestry oracle, using integer interval enumeration."""
import hashlib
import json
import struct
import sys
sys.dont_write_bytecode = True
from generate_watershed_network_vectors import network, fields
from generate_watershed_vectors import frame, i64


def example(name, source, scale, vertical, datum, bounds):
    packet = bytes.fromhex(source['preparedHex'])
    members = fields(packet)[6]
    count, offset, footprints = struct.unpack_from('>I', members)[0], 4, []
    for _ in range(count):
        size = struct.unpack_from('>I', members, offset)[0]
        offset += 4
        basin = fields(members[offset:offset + size])
        offset += size
        request = fields(basin[1])
        area = fields(request[5])
        mx, my = [int.from_bytes(v, 'big', signed=True) for v in area[1:3]]
        width, depth = [int.from_bytes(v, 'big') for v in area[3:]]
        for index in range(width * depth):
            natural, _, _, _, _, parcel = struct.unpack_from('>qqiii16s', basin[4], index * 44)
            x, y = mx + index % width, my + index // width
            footprints.append((x * scale, (x + 1) * scale - 1, y * scale, (y + 1) * scale - 1,
                               natural * vertical + datum, basin[2], index, basin[3], parcel))
    assert offset == len(members)
    assert all(-2**63 <= v <= 2**63 - 1 for f in footprints for v in f[:5])
    x0, y0, z0, width, depth, height = bounds
    rows, substrate = b'', 0
    # Membership comes from inclusive interval search, independent of production inverse floor lookup.
    for y in range(y0, y0 + depth):
        for x in range(x0, x0 + width):
            candidates = [f for f in footprints if f[0] <= x <= f[1] and f[2] <= y <= f[3]]
            assert len(candidates) == 1
            xmin, _, ymin, _, surface, basin, index, formation, parcel = candidates[0]
            rows += basin + struct.pack('>I', index) + formation + parcel + struct.pack('>qII', surface, x - xmin, y - ymin)
            substrate += sum(z <= surface for z in range(z0, z0 + height))
    mapping = frame('geography.watershed.terrain-mapping.v1', struct.pack('>Q', scale), struct.pack('>Q', vertical), i64(datum))
    area = frame('geography.watershed.terrain-bounds.v1', *[i64(v) for v in bounds[:3]],
                 *[struct.pack('>Q', v) for v in bounds[3:]])
    prepared = frame('geography.watershed.terrain-window.v1', packet, mapping, area, rows)
    return dict(name=name, preparedHex=prepared.hex(), preparedHash=hashlib.sha256(prepared).hexdigest(),
                mappingHash=hashlib.sha256(mapping).hexdigest(), substrateCells=substrate, columnCount=width * depth)


def generate():
    shapes = [(-2, -2, 2, 4, 3, 0, 1), (0, -2, 2, 4, 0, 0, 0)]
    base = network('base', shapes, [(1, 0), 16])
    fork = network('fork', shapes, [(1, 0), 16], branch=888)
    extreme = network('extreme', [(2**63-2, -2**63, 1, 1, 1, 0, 0), (2**63-1, -2**63, 1, 1, 0, 0, 0)], [(1, 0), 2])
    relief = network('relief', [(0, 0, 3, 3, 30, 5, 2), (3, 0, 3, 3, 0, 8, 8)], [(1, 0), 18])
    return dict(format='watershed-terrain-v1', provenance='Python stdlib explicit footprint interval search and cell-height comparison', examples=[
        example('negative-seam', base, 3, 2, -1, (-6, -6, -4, 12, 12, 12)),
        example('branch', fork, 3, 2, -1, (-6, -6, -4, 12, 12, 12)),
        example('lower-window', base, 3, 2, -1, (-6, -6, -4, 12, 12, 6)),
        example('upper-window', base, 3, 2, -1, (-6, -6, 2, 12, 12, 6)),
        example('extreme', extreme, 1, 1, 0, (2**63-2, -2**63, -1, 2, 1, 3)),
        example('natural-relief', relief, 2, 1, -30, (0, 0, -8, 12, 6, 16))])


if __name__ == '__main__':
    print(json.dumps(generate(), indent=2))
