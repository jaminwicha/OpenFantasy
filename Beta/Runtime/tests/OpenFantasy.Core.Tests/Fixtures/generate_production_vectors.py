#!/usr/bin/env python3
"""Independent ordinary-material recipe oracle. Stdout only; no production code imported."""
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


def replace(values, index, value):
    result = list(values)
    result[index] = value
    return result


source_recipe = {"id": "craft_rations", "output": "rations", "outputCount": 2,
                 "inputs": [{"item": "berries", "count": 2}, {"item": "wood", "count": 1}]}
source_hash = hashlib.sha256(json.dumps(source_recipe, sort_keys=True, separators=(",", ":"),
                                      ensure_ascii=False).encode()).digest()
assert source_hash.hex() == "c384027c76855ade482105233cadd07ea930a265f73e12beab852de1cc756491"
package = "openfantasy.calibration"
materials = [
    dict(id=identity(201).hex(), key="berries", massMilligrams="20000", volumeMicrolitres="30000", hardness=1),
    dict(id=identity(202).hex(), key="wood", massMilligrams="50000", volumeMicrolitres="100000", hardness=8),
    dict(id=identity(203).hex(), key="rations", massMilligrams="40000", volumeMicrolitres="45000", hardness=2),
    dict(id=identity(204).hex(), key="residue", massMilligrams="10000", volumeMicrolitres="25000", hardness=0),
]


def material_fields(material):
    return [number(1), package.encode(), bytes.fromhex(material["id"]), material["key"].encode(),
            number(int(material["massMilligrams"])), number(int(material["volumeMicrolitres"])),
            number(material["hardness"])]


material_frames = [frame("production.material", *material_fields(value)) for value in materials]
for value, encoded in zip(materials, material_frames):
    value["canonicalHex"] = encoded.hex()
    value["version"] = hashlib.sha256(encoded).hexdigest()
terms = [dict(role=1, materialId=materials[0]["id"], version=materials[0]["version"], count="2"),
         dict(role=1, materialId=materials[1]["id"], version=materials[1]["version"], count="1"),
         dict(role=2, materialId=materials[2]["id"], version=materials[2]["version"], count="2"),
         dict(role=3, materialId=materials[3]["id"], version=materials[3]["version"], count="1")]


def term_fields(term):
    return [number(1), number(term["role"]), bytes.fromhex(term["materialId"]),
            bytes.fromhex(term["version"]), number(int(term["count"]))]


def term_bytes(term):
    return frame("production.recipe-term", *term_fields(term))


term_frames = list(map(term_bytes, terms))
catalog = frame("production.material-catalog", number(1), *material_frames)
term_set = frame("production.recipe-terms", number(1), *term_frames)
recipe_id = identity(210)
fields = [number(1), package.encode(), recipe_id, b"craft_rations", number(120), source_hash, catalog, term_set]
recipe = frame("production.recipe", *fields)


def invalid(name, failure, data):
    return dict(name=name, failure=failure, hex=data.hex())


def altered(index, data):
    return frame("production.recipe", *replace(fields, index, data))


def changed_material(index, field, data):
    replacement = frame("production.material", *replace(material_fields(materials[index]), field, data))
    return altered(6, frame("production.material-catalog", number(1), *replace(material_frames, index, replacement)))


def changed_term(index, field, data):
    replacement = frame("production.recipe-term", *replace(term_fields(terms[index]), field, data))
    return altered(7, frame("production.recipe-terms", number(1), *replace(term_frames, index, replacement)))


unused = dict(id=identity(205).hex(), key="unused", massMilligrams="1", volumeMicrolitres="1", hardness=0)
invalid_recipes = [
    invalid("bad magic", "InvalidFrame", b"BAD!" + recipe[4:]),
    invalid("unknown frame version", "UnsupportedVersion", recipe[:4] + struct.pack(">I", 2) + recipe[8:]),
    invalid("unknown recipe version", "UnsupportedVersion", altered(0, number(2))),
    invalid("unknown recipe type", "UnknownType", frame("production.unknown", *fields)),
    invalid("zero required work", "InvalidValue", altered(4, number(0))),
    invalid("short source hash", "InvalidValue", altered(5, source_hash[:-1])),
    invalid("invalid recipe text", "InvalidValue", altered(3, b"\xff")),
    invalid("noncanonical package", "InvalidValue", altered(1, b"Upper.Case")),
    invalid("unsorted materials", "NonCanonical", altered(6, frame("production.material-catalog", number(1), *reversed(material_frames)))),
    invalid("duplicate materials", "NonCanonical", altered(6, frame("production.material-catalog", number(1), material_frames[0], *material_frames))),
    invalid("unused material", "NonCanonical", altered(6, frame("production.material-catalog", number(1), *material_frames, frame("production.material", *material_fields(unused))))),
    invalid("missing material reference", "InvalidValue", altered(6, frame("production.material-catalog", number(1), *material_frames[:-1]))),
    invalid("zero unit mass", "InvalidValue", changed_material(0, 4, number(0))),
    invalid("zero occupied volume", "InvalidValue", changed_material(0, 5, number(0))),
    invalid("oversized hardness rating", "InvalidValue", changed_material(0, 6, number(1 << 32))),
    invalid("unsupported material version", "UnsupportedVersion", changed_material(0, 0, number(2))),
    invalid("stale definition pin", "InvalidValue", changed_term(0, 3, b"\0" * 32)),
    invalid("unknown material role", "UnknownType", changed_term(0, 1, number(4))),
    invalid("zero material count", "InvalidValue", changed_term(0, 4, number(0))),
    invalid("overflowing input mass", "InvalidValue", changed_term(0, 4, number((1 << 64) - 1))),
    invalid("duplicate same role", "NonCanonical", altered(7, frame("production.recipe-terms", number(1), term_frames[0], *term_frames))),
    invalid("unsorted terms", "NonCanonical", altered(7, frame("production.recipe-terms", number(1), *reversed(term_frames)))),
    invalid("unaccounted discarded mass", "InvalidValue", altered(7, frame("production.recipe-terms", number(1), *term_frames[:-1]))),
    invalid("mass created in outputs", "InvalidValue", changed_term(2, 4, number(3))),
    invalid("missing input", "InvalidValue", altered(7, frame("production.recipe-terms", number(1), *term_frames[2:]))),
    invalid("missing output", "InvalidValue", altered(7, frame("production.recipe-terms", number(1), *term_frames[:2], term_frames[3]))),
    invalid("unexpected outer field", "NonCanonical", frame("production.recipe", *fields, number(0))),
    invalid("trailing outer bytes", "TrailingData", recipe + b"\0"),
    invalid("unbounded field length", "LimitExceeded", recipe[:12] + struct.pack(">I", 0xffffffff) + recipe[16:]),
    invalid("too many terms", "LimitExceeded", altered(7, term_set[:8] + struct.pack(">I", 67) + term_set[12:])),
]
print(json.dumps(dict(
    format="production-v1", sourceRecipe=dict(inventoryId="data/recipes.json#/recipes/1",
        payload=source_recipe, payloadSha256=source_hash.hex()),
    calibration=dict(status="new authored qualification values; not original values or accepted gameplay balance",
        package=package, definitionId=recipe_id.hex(), key="craft_rations", requiredWorkUnits="120",
        materials=materials, terms=terms, inputMassMilligrams="90000", outputMassMilligrams="80000",
        wasteMassMilligrams="10000"),
    recipeHex=recipe.hex(), recipeVersion=hashlib.sha256(recipe).hexdigest(), invalidRecipes=invalid_recipes,
), indent=2))
