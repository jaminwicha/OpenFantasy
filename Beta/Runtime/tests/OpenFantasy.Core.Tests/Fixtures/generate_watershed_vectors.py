"""Independent stdlib encoder and sorted-frontier reference for finite watershed preparation v1."""
import hashlib
import json
import struct


def frame(tag, *values):
    fields = [tag.encode(), *values]
    return b'OFC\0' + struct.pack('>II', 1, len(fields)) + b''.join(struct.pack('>I', len(v)) + v for v in fields)


def u64(n): return struct.pack('>Q', n)
def i64(n): return struct.pack('>q', n)
def identity(n): return n.to_bytes(16, 'big')


def example(name, x, y, width, depth, base, relief, outlet, seed=4, external_boundary=5, branch_id=2):
    world, branch, realm, seed_bytes, external = [identity(n) for n in [1, branch_id, 3, seed, external_boundary]]
    bounds = frame('geography.watershed.bounds.v1', i64(x), i64(y), u64(width), u64(depth))
    rules = frame('geography.watershed.rules.v1', i64(base), u64(relief))
    context = frame('geography.watershed.context.v1', world, realm, seed_bytes, bounds, rules, u64(outlet), external)

    def derive(role, ordinal):
        return hashlib.sha256(frame('geography.watershed.identity.v1', context, role.encode(), u64(ordinal))).digest()[:16]

    count = width * depth
    elevations = []
    for index in range(count):
        digest = hashlib.sha256(frame('geography.watershed.relief.v1', context, u64(index))).digest()
        elevations.append(base - relief if index == outlet else base + int.from_bytes(digest[:8], 'big') % (2 * relief + 1) - relief)

    def neighbors(index):
        return sorted(n for n in {index-width, index-1, index+1, index+width}
                      if 0 <= n < count and abs(n % width - index % width) + abs(n // width - index // width) == 1)

    # A sorted list, independently of the production priority queue. Discovery fixes the parent;
    # pop order supplies the secondary rank that makes flat spill surfaces acyclic.
    parent = [None] * count
    spill, rank = [0] * count, [0] * count
    parent[outlet], spill[outlet] = -1, elevations[outlet]
    pending, order = [(spill[outlet], outlet)], []
    while pending:
        pending.sort()
        _, current = pending.pop(0)
        rank[current] = len(order)
        order.append(current)
        for neighbor in neighbors(current):
            if parent[neighbor] is not None:
                continue
            parent[neighbor] = current
            spill[neighbor] = max(spill[current], elevations[neighbor])
            pending.append((spill[neighbor], neighbor))
    contribution = [1] * count
    for current in reversed(order[1:]):
        contribution[parent[current]] += contribution[current]
    records = b''.join(struct.pack('>qqiii', elevations[i], spill[i], parent[i], rank[i], contribution[i])
                       + derive('substrate-parcel', i) for i in range(count))
    request = frame('geography.watershed.request.v1', world, branch, realm, seed_bytes, bounds, rules, u64(outlet), external)
    encoded = frame('geography.watershed.prepared.v1', request, derive('basin', 0), derive('formation', 0), records)
    faces = [{'first': i, 'second': n, 'id': derive('boundary', i * 4096 + n).hex()}
             for i in range(count) for n in neighbors(i) if n > i]
    return dict(name=name, minimumX=x, minimumY=y, width=width, depth=depth, baseElevation=base,
                relief=relief, outletIndex=outlet, seed=seed_bytes.hex(), rulesHash=hashlib.sha256(rules).hexdigest(),
                preparedHex=encoded.hex(), preparedHash=hashlib.sha256(encoded).hexdigest(), boundaries=faces)


def generate():
    return dict(format='watershed-v1', provenance='Independent Python stdlib, no production C# calls', examples=[
        example('negative-grid', -2, -1, 5, 4, 10, 8, 2),
        example('flat-plateau', 0, 0, 3, 3, 0, 0, 8, 17),
        example('single-column', -1, -3, 1, 4, 5, 3, 0),
        example('signed-boundary', 2**63-1, -2**63, 1, 1, -2**63, 0, 0)])


if __name__ == '__main__':
    print(json.dumps(generate(), indent=2))
