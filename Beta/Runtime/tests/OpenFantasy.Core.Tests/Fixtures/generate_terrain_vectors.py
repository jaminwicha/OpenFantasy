#!/usr/bin/env python3
"""Independent integer/hash terrain v1 oracle. Writes canonical JSON only to stdout."""
import hashlib
import json
import struct


def u64(value):
    return struct.pack(">Q", value)


def i64(value):
    return struct.pack(">q", value)


def identity(value):
    return value.to_bytes(16, "big")


def frame(tag, *fields):
    fields = (tag.encode("ascii"),) + fields
    return b"OFC\x00" + struct.pack(">II", 1, len(fields)) + b"".join(
        struct.pack(">I", len(field)) + field for field in fields)


def digest(data):
    return hashlib.sha256(data).digest()


def pin(role, number):
    return {"definitionId": identity(number).hex(), "version": digest(("terrain-" + role).encode()).hex()}


PINS = [pin("surface", 101), pin("soil", 102), pin("stone", 103)]
SCOPE = {"worldId": identity(1).hex(), "branchId": identity(2).hex(), "realmId": identity(3).hex()}
SEED = identity(987654321).hex()


def encode_pin(value):
    return frame("terrain.material-pin.v1", bytes.fromhex(value["definitionId"]), bytes.fromhex(value["version"]))


def encode_rules(rules):
    return frame("terrain.rules.v1", i64(rules["baseSurfaceZ"]), u64(rules["relief"]),
                 u64(rules["soilDepth"]), *(encode_pin(value) for value in rules["materials"]))


def encode_scope(scope):
    return frame("terrain.scope.v1", *(bytes.fromhex(scope[key]) for key in ("worldId", "branchId", "realmId")))


def encode_bounds(bounds):
    return frame("terrain.bounds.v1", *(i64(value) for value in bounds["minimum"]),
                 *(u64(value) for value in bounds["size"]))


def encode_snapshot(example, revision=0, indices=()):
    return frame("terrain.region.v1", encode_scope(example["scope"]), bytes.fromhex(example["seed"]),
                 encode_rules(example["rules"]), encode_bounds(example["bounds"]), u64(revision),
                 b"".join(struct.pack(">I", value) for value in indices))


def generate(example):
    scope, rules, bounds = example["scope"], example["rules"], example["bounds"]
    context = frame("terrain.generation.v1", bytes.fromhex(scope["worldId"]), bytes.fromhex(scope["realmId"]),
                    bytes.fromhex(example["seed"]), digest(encode_rules(rules)))
    cells = []
    for dz in range(bounds["size"][2]):
        z = bounds["minimum"][2] + dz
        for dy in range(bounds["size"][1]):
            y = bounds["minimum"][1] + dy
            for dx in range(bounds["size"][0]):
                x = bounds["minimum"][0] + dx
                gx, rx = divmod(x, 8)
                gy, ry = divmod(y, 8)
                weighted = 0
                for ox, wx in ((0, 8 - rx), (1, rx)):
                    for oy, wy in ((0, 8 - ry), (1, ry)):
                        sample = int.from_bytes(digest(frame("terrain.relief.v1", context,
                                                           i64(gx + ox), i64(gy + oy)))[:8], "big")
                        weighted += (sample % (2 * rules["relief"] + 1)) * wx * wy
                surface = rules["baseSurfaceZ"] + weighted // 64 - rules["relief"]
                stratum = 0 if z > surface else 1 if z == surface else 2 if z >= surface - rules["soilDepth"] else 3
                cell = {"coordinate": [x, y, z], "stratum": stratum, "surfaceZ": surface,
                        "formationId": None, "sourceId": None, "material": None}
                if stratum:
                    formation = digest(frame("terrain.formation.v1", context, i64(x), i64(y), u64(stratum)))[:16]
                    cell.update(formationId=formation.hex(),
                                sourceId=digest(frame("terrain.source.v1", formation, i64(z)))[:16].hex(),
                                material=rules["materials"][stratum - 1])
                cells.append(cell)
    return cells


def example(name, minimum, size, base=0, relief=0, soil=2):
    result = {"name": name, "scope": SCOPE, "seed": SEED,
              "rules": {"baseSurfaceZ": base, "relief": relief, "soilDepth": soil, "materials": PINS},
              "bounds": {"minimum": minimum, "size": size}}
    result["rulesCanonicalHex"] = encode_rules(result["rules"]).hex()
    result["rulesSha256"] = digest(encode_rules(result["rules"])).hex()
    result["cells"] = generate(result)
    solid = [index for index, cell in enumerate(result["cells"]) if cell["stratum"]]
    edits = solid[:2]
    result["states"] = []
    for name, revision, indices in (("initial", 0, []), ("excavated", 1, edits),
                                    ("maximum-revision", (1 << 64) - 1, edits)):
        if not indices and revision:
            continue
        data = encode_snapshot(result, revision, indices)
        result["states"].append({"name": name, "revision": revision, "excavatedIndices": indices,
                                  "canonicalHex": data.hex(), "sha256": digest(data).hex()})
    return result


def malformed(example):
    scope = encode_scope(example["scope"])
    seed = bytes.fromhex(example["seed"])
    rules = encode_rules(example["rules"])
    bounds = encode_bounds(example["bounds"])
    fields = [scope, seed, rules, bounds, u64(1), struct.pack(">II", 0, 1)]
    result = []

    def add(name, data):
        result.append({"name": name, "canonicalHex": data.hex()})

    def replace(name, index, value):
        altered = fields[:]
        altered[index] = value
        add(name, frame("terrain.region.v1", *altered))

    good = frame("terrain.region.v1", *fields)
    add("bad-magic", b"BAD!" + good[4:])
    add("unsupported-framing-version", good[:4] + struct.pack(">I", 2) + good[8:])
    add("extra-top-field", frame("terrain.region.v1", *fields, b""))
    add("unknown-tag", frame("terrain.region.v2", *fields))
    add("trailing-byte", good + b"\x00")
    add("truncated", good[:-1])
    add("oversized-field-length", good[:12] + b"\xff" * 4 + good[16:])
    replace("short-seed", 1, seed[:-1])
    replace("short-revision", 4, b"\x00")
    replace("initial-revision-with-edits", 4, u64(0))
    replace("partial-index", 5, b"\x00")
    replace("duplicate-index", 5, struct.pack(">II", 0, 0))
    replace("reversed-index", 5, struct.pack(">II", 1, 0))
    replace("index-outside-region", 5, struct.pack(">I", len(example["cells"])))
    replace("maximum-index", 5, b"\xff" * 4)
    void = next(index for index, cell in enumerate(example["cells"]) if cell["stratum"] == 0)
    replace("excavated-original-void", 5, struct.pack(">I", void))
    for name, base, relief, soil in (("unknown-relief", 0, 9, 2), ("zero-soil", 0, 0, 0),
                                    ("oversized-soil", 0, 0, 17), ("surface-overflow", (1 << 63) - 1, 1, 1),
                                    ("soil-underflow", -(1 << 63), 0, 1)):
        replace(name, 2, frame("terrain.rules.v1", i64(base), u64(relief), u64(soil), *(encode_pin(pin) for pin in PINS)))
    for name, minimum, sizes in (("zero-width", [0, 0, 0], [0, 1, 1]),
                                  ("excess-width", [0, 0, 0], [33, 1, 1]),
                                  ("excess-height", [0, 0, 0], [1, 1, 17]),
                                  ("extent-overflow", [(1 << 63) - 1, 0, 0], [2, 1, 1])):
        replace(name, 3, encode_bounds({"minimum": minimum, "size": sizes}))
    conflict = dict(PINS[1], definitionId=PINS[0]["definitionId"])
    replace("conflicting-material-versions", 2, frame("terrain.rules.v1", i64(0), u64(0), u64(2),
                                                      encode_pin(PINS[0]), encode_pin(conflict), encode_pin(PINS[2])))
    replace("scope-trailing", 0, scope + b"\x00")
    replace("rules-trailing", 2, rules + b"\x00")
    replace("bounds-trailing", 3, bounds + b"\x00")
    return result


def main():
    examples = [example("flat", [-2, -1, -4], [4, 2, 7]),
                example("negative-lattice", [-9, -9, -3], [3, 2, 6], relief=2, soil=1),
                example("positive-lattice", [7, 7, -4], [3, 2, 8], relief=3),
                example("signed-coordinate-extremes", [-(1 << 63), (1 << 63) - 2, -(1 << 63)], [2, 2, 2]),
                example("upper-surface-envelope", [-1, 0, (1 << 63) - 3], [2, 1, 3],
                        base=(1 << 63) - 9, relief=8, soil=1)]
    print(json.dumps({"contract": "terrain-v1", "examples": examples, "malformed": malformed(examples[0])}, indent=2))


if __name__ == "__main__":
    main()
