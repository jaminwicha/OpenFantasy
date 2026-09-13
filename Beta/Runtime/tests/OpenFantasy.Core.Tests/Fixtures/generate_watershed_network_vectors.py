"""Independent archive encoding and per-origin route walking for composed drainage, without production calls."""
import hashlib
import json
import struct
import sys

sys.dont_write_bytecode = True
from generate_watershed_vectors import example, frame, identity, i64


def fields(data):
    assert data[:8] == b'OFC\0\0\0\0\1'
    count, offset, result = struct.unpack_from('>I', data, 8)[0], 12, []
    for _ in range(count):
        size = struct.unpack_from('>I', data, offset)[0]
        offset += 4
        result.append(data[offset:offset + size])
        offset += size
    assert offset == len(data)
    return result


def network(name, shapes, destinations, branch=2):
    basins, columns, addresses = {}, {}, {}
    for i, shape in enumerate(shapes):
        value = example(str(i), *shape, external_boundary=100 + i, branch_id=branch)
        packet = bytes.fromhex(value['preparedHex'])
        parts = fields(packet)
        basin_id = parts[2]
        x, y, width, depth, _, _, outlet = shape
        rows = [struct.unpack_from('>qqiii16s', parts[4], j * 44) for j in range(width * depth)]
        basins[i] = dict(id=basin_id, packet=packet, outlet=outlet, rows=rows)
        for j, row in enumerate(rows):
            key = (i, j)
            columns[key] = row
            address = (x + j % width, y + j // width)
            assert address not in addresses
            addresses[address] = key

    # Enumerate each origin's complete route. No basin topological pass or accumulation by drainage rank.
    counts = {key: 0 for key in columns}
    terminal_counts = {}
    for origin in columns:
        at, visited = origin, set()
        while True:
            assert at not in visited
            visited.add(at)
            counts[at] += 1
            parent = columns[at][2]
            if parent >= 0:
                at = (at[0], parent)
            elif isinstance(destinations[at[0]], tuple):
                at = destinations[at[0]]
            else:
                terminal_counts[at[0]] = terminal_counts.get(at[0], 0) + 1
                break
    assert sum(terminal_counts.values()) == len(columns)
    for i, count in terminal_counts.items():
        assert count <= destinations[i]

    order = sorted(basins, key=lambda i: basins[i]['id'])
    members = struct.pack('>I', len(order)) + b''.join(struct.pack('>I', len(basins[i]['packet'])) + basins[i]['packet'] for i in order)
    closures = b''
    for i in order:
        target = destinations[i]
        if isinstance(target, tuple):
            receiver, index = target
            closures += basins[i]['id'] + struct.pack('>I', 1) + basins[receiver]['id'] + struct.pack('>II', index, 0)
        else:
            closures += basins[i]['id'] + struct.pack('>I', 2) + bytes(16) + struct.pack('>II', 0, target)
    composed = b''.join(struct.pack('>I', counts[(i, j)]) for i in order for j in range(len(basins[i]['rows'])))
    shared, face_ids = b'', []
    for first in sorted(addresses, key=lambda p: (p[1], p[0])):
        for second in [(first[0] + 1, first[1]), (first[0], first[1] + 1)]:
            if second not in addresses:
                continue
            a, b = addresses[first], addresses[second]
            if a[0] == b[0]:
                continue
            face_id = hashlib.sha256(frame('geography.watershed.network.face.v1', identity(1), identity(3), identity(900),
                i64(first[0]), i64(first[1]), i64(second[0]), i64(second[1]))).digest()[:16]
            direction = 1 if a[1] == basins[a[0]]['outlet'] and destinations[a[0]] == b else (
                2 if b[1] == basins[b[0]]['outlet'] and destinations[b[0]] == a else 0)
            shared += face_id + basins[a[0]]['id'] + struct.pack('>I', a[1]) + basins[b[0]]['id'] + struct.pack('>I', b[1])
            shared += struct.pack('>qqI', columns[a][0], columns[b][0], direction)
            face_ids.append(face_id.hex())
    packet = frame('geography.watershed.network.prepared.v1', identity(1), identity(branch), identity(3), identity(4), identity(900),
                   members, closures, composed, shared)
    return dict(name=name, preparedHex=packet.hex(), preparedHash=hashlib.sha256(packet).hexdigest(),
                basinCount=len(basins), columnCount=len(columns), faceIds=face_ids,
                terminalContributions=[dict(basinId=basins[i]['id'].hex(), count=terminal_counts[i], capacity=destinations[i])
                                       for i in order if i in terminal_counts])


def generate():
    fan_in = [(0, 0, 2, 2, 10, 0, 1), (2, 0, 3, 2, 5, 0, 2),
              (5, 0, 2, 2, 0, 0, 0), (2, -2, 3, 2, 10, 0, 3)]
    links = [(1, 0), (2, 0), 20, (1, 0)]
    return dict(format='watershed-network-v1', provenance='Independent Python stdlib archive encoder and origin-path enumeration', examples=[
        network('fan-in', fan_in, links), network('branch', fan_in, links, branch=888),
        network('relief', [(0, 0, 3, 3, 30, 5, 2), (3, 0, 3, 3, 0, 8, 8)], [(1, 0), 18]),
        network('extreme-face', [(2**63 - 2, -2**63, 1, 1, 1, 0, 0), (2**63 - 1, -2**63, 1, 1, 0, 0, 0)], [(1, 0), 2]),
        network('separate-terminals', [(-10, 0, 2, 2, 0, 0, 0), (10, 0, 2, 2, 0, 0, 0)], [4, 4])])


if __name__ == '__main__':
    print(json.dumps(generate(), indent=2))
