"""Independent authored body/form qualification oracle, not imported species statistics."""
import hashlib
import json
import struct


def identity(value):
    return value.to_bytes(16, "big")


def number(value):
    return struct.pack(">Q", int(value))


def signed(value):
    return struct.pack(">q", int(value))


def frame(tag, *fields):
    fields = [tag.encode("ascii"), *fields]
    return b"OFC\0" + struct.pack(">II", 1, len(fields)) + b"".join(
        struct.pack(">I", len(field)) + field for field in fields)


def part(slot, parent, dimensions, functions=()):
    return dict(slot=slot, parent=parent, dimensions=list(map(str, dimensions)), functions=list(functions))


def part_bytes(value):
    return frame("actor.body-part", value["slot"].encode(),
                 b"" if value["parent"] is None else value["parent"].encode(),
                 *map(signed, value["dimensions"]),
                 frame("actor.body-functions", *map(number, sorted(value["functions"]))))


def definition(name, identifier, nature, clearance, parts, locomotion):
    parts = sorted(parts, key=lambda value: value["slot"])
    locomotion = sorted(locomotion, key=lambda value: value[0])
    encoded = frame("actor.body-definition", number(1), identity(identifier), number(nature),
                    frame("actor.body-clearance", *map(signed, clearance)),
                    frame("actor.body-parts", *map(part_bytes, parts)),
                    frame("actor.locomotion-requirements", *[
                        frame("actor.locomotion", number(mode), number(minimum)) for mode, minimum in locomotion]))
    return dict(name=name, definitionId=identity(identifier).hex(), nature=nature,
                clearance=list(map(str, clearance)), parts=parts, locomotion=locomotion,
                canonicalHex=encoded.hex(), versionSha256=hashlib.sha256(encoded).hexdigest())


humanoid = definition("humanoid-candidate", 1, 1, [180, 140, 464], [
    part("torso", None, [128, 80, 192]),
    part("head", "torso", [64, 64, 80], [2]),
    part("left-eye", "head", [4, 4, 4], [1]), part("right-eye", "head", [4, 4, 4], [1]),
    part("left-arm", "torso", [32, 32, 140]), part("right-arm", "torso", [32, 32, 140]),
    part("left-hand", "left-arm", [24, 40, 16], [3]), part("right-hand", "right-arm", [24, 40, 16], [3]),
    part("left-foot", "torso", [32, 64, 24], [4]), part("right-foot", "torso", [32, 64, 24], [4]),
], [[1, 2]])
quadruped = definition("quadruped-mount-anatomy-candidate", 2, 1, [220, 600, 460], [
    part("trunk", None, [200, 400, 240]), part("head", "trunk", [80, 140, 120], [1, 2]),
    *[part(name, "trunk", [40, 40, 180], [4]) for name in
      ("fore-left", "fore-right", "hind-left", "hind-right")],
], [[1, 3]])
avian = definition("avian-candidate", 3, 1, [640, 180, 280], [
    part("trunk", None, [80, 120, 140]), part("head", "trunk", [56, 56, 56], [1, 2]),
    part("left-foot", "trunk", [24, 48, 24], [4]), part("right-foot", "trunk", [24, 48, 24], [4]),
    part("left-wing", "trunk", [280, 80, 16], [5]), part("right-wing", "trunk", [280, 80, 16], [5]),
], [[1, 2], [2, 2]])
serpent = definition("reptilian-serpent-candidate", 4, 1, [130, 1600, 150], [
    part("body", None, [128, 1400, 128], [6, 7]), part("head", "body", [100, 160, 120], [1, 2]),
], [[3, 1], [4, 1]])
spirit = definition("incorporeal-presence-candidate", 5, 2, [160, 160, 450], [
    part("presence", None, [160, 160, 450], [8, 9]),
], [[5, 1]])
definitions = [humanoid, quadruped, avian, serpent, spirit]


def injury(identifier, cause, slot, kind, lost=None):
    return dict(injuryId=identity(identifier).hex(), causeId=identity(cause).hex(), slot=slot,
                kind=kind, lostFunction=lost)


def injury_bytes(value):
    return frame("actor.body-injury", bytes.fromhex(value["injuryId"]), bytes.fromhex(value["causeId"]),
                 value["slot"].encode(), number(value["kind"]),
                 b"" if value["lostFunction"] is None else number(value["lostFunction"]))


def condition(name, definition, form, injuries):
    injuries = sorted(injuries, key=lambda value: value["injuryId"])
    encoded = frame("actor.body-condition", number(1), identity(form), bytes.fromhex(definition["definitionId"]),
                    bytes.fromhex(definition["versionSha256"]), frame("actor.body-injuries", *map(injury_bytes, injuries)))
    parts = {value["slot"]: value for value in definition["parts"]}
    missing = {value["slot"] for value in injuries if value["kind"] == 2}

    def absent(slot):
        return slot in missing or (parts[slot]["parent"] is not None and absent(parts[slot]["parent"]))

    active = {str(function): sorted(value["slot"] for value in parts.values() if function in value["functions"]
               and not absent(value["slot"]) and not any(record["slot"] == value["slot"]
               and record["lostFunction"] == function for record in injuries)) for function in range(1, 10)}
    support_function = {1: 4, 2: 5, 3: 6, 4: 7, 5: 9}
    eligible = [mode for mode, minimum in definition["locomotion"] if len(active[str(support_function[mode])]) >= minimum]
    return dict(name=name, definitionId=definition["definitionId"], formInstanceId=identity(form).hex(),
                injuries=injuries, functionalSlots=active, locomotion=eligible,
                canonicalHex=encoded.hex(), sha256=hashlib.sha256(encoded).hexdigest())


conditions = [condition("intact-humanoid", humanoid, 101, []),
              condition("scar-is-cosmetic", humanoid, 101, [injury(201, 301, "head", 1)]),
              condition("eye-and-arm-loss", humanoid, 101, [injury(202, 302, "left-eye", 2),
                        injury(203, 302, "right-arm", 2), injury(204, 303, "right-hand", 3, 3)]),
              condition("one-wing-disabled", avian, 103, [injury(205, 304, "left-wing", 3, 5)]),
              condition("legless-serpent", serpent, 104, []),
              condition("spectral-motion-disabled", spirit, 105, [injury(206, 305, "presence", 3, 9)])]


def binding(name, definition, form, kind, body=None, tether=None):
    encoded = frame("actor.form-binding", number(1), identity(401), identity(form),
                    bytes.fromhex(definition["definitionId"]), bytes.fromhex(definition["versionSha256"]), number(kind),
                    b"" if body is None else identity(body), b"" if tether is None else identity(tether))
    return dict(name=name, actorId=identity(401).hex(), formInstanceId=identity(form).hex(),
                definitionId=definition["definitionId"], kind=kind,
                physicalBodyId=None if body is None else identity(body).hex(),
                tetherAnchorId=None if tether is None else identity(tether).hex(),
                canonicalHex=encoded.hex(), sha256=hashlib.sha256(encoded).hexdigest())


bindings = [binding("corporeal-reference", humanoid, 101, 1, body=501),
            binding("same-actor-independent-spirit-reference", spirit, 105, 2),
            binding("same-actor-tethered-projection-reference", spirit, 106, 3, tether=501)]
print(json.dumps(dict(format="actors-v1", provenance="Authored P00 qualification data; no legacy stat conversion or accepted species/asset catalog",
                     definitions=definitions, conditions=conditions, bindings=bindings), indent=2))
