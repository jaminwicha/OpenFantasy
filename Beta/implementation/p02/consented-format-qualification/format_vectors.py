"""Independent canonical vectors for the consented world formats.

Standard library only. This generator never imports, invokes or reads the output of a runtime
codec. Every expected byte follows from the semantic facts below and the documented frame rule:
magic "OFC\\0", big-endian u32 encoding version 1, u32 field count including the tag, then each
field as a u32 length and its bytes.

Two kinds of entry are written:
- vectors: complete frames whose every field is an explicit semantic fact.
- compositions: NEW framing around opaque owner payloads. Literal segments (header, tag, versions,
  fixed-width length prefixes) are independent; every slot is imported by the consuming test from
  the named origin and is NOT independently encoded here. The example pins one synthetic filling.
"""
from hashlib import sha256
import json
from pathlib import Path
from struct import pack
import sys

HERE = Path(__file__).resolve().parent
TARGET = HERE.parents[3] / "Beta/Runtime/tests/OpenFantasy.Core.Tests/Fixtures/consented-world-formats-v1.json"


def number(value):
    return pack(">Q", value)


def signed(value):
    return pack(">q", value)


def identity(suffix):
    """Fixed semantic identities shared with the consented-material-v1 fixture facts."""
    return pack(">QQ", 0x0102030405060708, 0x1122334455660000 + suffix)


def journey(value):
    """Identities of the Core consented material journey fixture: high word zero."""
    return pack(">QQ", 0, value)


def digest(data):
    return sha256(data).digest()


def frame(tag, *fields):
    values = [tag.encode("utf-8"), *fields]
    return b"OFC\0" + pack(">II", 1, len(values)) + b"".join(pack(">I", len(v)) + v for v in values)


def vector(data):
    return {"length": len(data), "hex": data.hex(), "sha256": sha256(data).hexdigest()}


def terms(world, branch, sender, recipient, kind, lot, definition, version, source, destination, quantity, revision):
    return frame("authority.consented-material.terms", number(1), world, branch, sender, recipient,
                 number(kind), lot, definition, version, source, destination, number(quantity), number(revision))


def command(command_id, principal, session, approved):
    return frame("authority.consented-material.command", number(1), command_id, principal, session, approved)


def acceptance(acceptance_id, principal, session, command_id, command_bytes, approved):
    return frame("authority.consented-material.acceptance", number(1), acceptance_id, principal, session,
                 command_id, digest(command_bytes), approved)


def action(command_id, principal, session, actor, world, branch, revision, intent):
    return frame("world.actor-action", number(15), command_id, principal, session, actor, world, branch,
                 number(revision), intent)


def evidence(sequence, offer_index, acceptance_index, command_bytes, sender, acceptance_bytes, recipient,
             moved_lot, rights_owner, output_index, start, end, label):
    hashes = [digest(("consented-format-v1/" + label + "/" + name).encode("utf-8")) for name in
              ("original-inventory", "original-production", "candidate-inventory", "candidate-production")]
    return frame("world.consented-transfer.v1", number(sequence), number(offer_index), number(acceptance_index),
                 command_bytes, sender[0], sender[1], acceptance_bytes, recipient[0], recipient[1], moved_lot,
                 rights_owner, number(output_index), number(start), number(end), *hashes)


def spawned(cause, ordinal):
    return digest(frame("entity.spawned", cause, number(ordinal)))[:16]


def composition(tag, parts, example, origin, limits):
    """parts: ("literal", bytes) | ("fixed", slot, width) | ("variable", slot)."""
    encoded_tag = tag.encode("utf-8")
    literal = b"OFC\0" + pack(">II", 1, len(parts) + 1) + pack(">I", len(encoded_tag)) + encoded_tag
    segments, fields, assembled = [], [], b""
    for part in parts:
        if part[0] == "literal":
            literal += pack(">I", len(part[1])) + part[1]
            fields.append(part[1])
            continue
        value = example[part[1]]
        if part[0] == "fixed":
            assert len(value) == part[2], part
            literal += pack(">I", part[2])
        segments.append({"hex": literal.hex()})
        assembled += literal
        literal = b""
        if part[0] == "fixed":
            segments.append({"slot": part[1], "width": part[2]})
            assembled += value
        else:
            segments.append({"slot": part[1], "variable": True})
            assembled += pack(">I", len(value)) + value
        fields.append(value)
    if literal:
        segments.append({"hex": literal.hex()})
        assembled += literal
    data = frame(tag, *fields)
    assert assembled == data, tag
    return {"origin": origin, "limits": limits, "segments": segments,
            "example": {"slots": {name: value.hex() for name, value in example.items()}, **vector(data)}}


def build():
    world, branch, sender, recipient = identity(1), identity(2), identity(23), identity(24)
    material = digest(b"consented-gift-golden-material-v1")
    raw_terms = terms(world, branch, sender, recipient, 1, identity(5), identity(10), material,
                      identity(3), identity(4), 4, 2)
    produced_terms = terms(world, branch, sender, recipient, 2, identity(6), identity(10), material,
                           identity(3), identity(4), 3, 7)
    sender_delivery, recipient_delivery = (identity(21), identity(22)), (identity(31), identity(32))

    raw_command = command(identity(20), *sender_delivery, raw_terms)
    raw_acceptance = acceptance(identity(30), *recipient_delivery, identity(20), raw_command, raw_terms)
    produced_command = command(identity(60), *sender_delivery, produced_terms)
    produced_acceptance = acceptance(identity(61), *recipient_delivery, identity(60), produced_command, produced_terms)

    offer = action(identity(20), *sender_delivery, sender, world, branch, 5,
                   frame("world.action.material-offer", number(1), raw_terms, number(6)))
    accepted = action(identity(30), *recipient_delivery, recipient, world, branch, 6,
                      frame("world.action.material-acceptance", number(1), identity(20), digest(raw_command),
                            raw_terms, number(5)))
    step = frame("world.step-batch", number(28), identity(40), world, branch, number(9), number(8), number(1),
                 frame("world.step-movement",
                       frame("world.control-sample", identity(26), identity(27), identity(25), signed(32), signed(0)),
                       frame("world.control-sample", identity(29), identity(33), identity(28), signed(0), signed(-32))),
                 frame("world.step-actions", offer, accepted))

    evidence_raw = evidence(10, 0, 1, raw_command, sender_delivery, raw_acceptance, recipient_delivery,
                            identity(5), identity(50), 0, 12, 16, "A")
    child = spawned(identity(60), 0)
    evidence_produced = evidence(11, 2, 5, produced_command, sender_delivery, produced_acceptance,
                                 recipient_delivery, child, identity(51), 1, 0, 3, "B")
    history_tag = "world.consented-material-history.v1"
    history = frame(history_tag, evidence_raw, evidence_produced)
    history_hash = sha256(frame(history_tag, digest(evidence_raw), digest(evidence_produced))).hexdigest()
    empty_history_hash = sha256(frame(history_tag)).hexdigest()

    # The first ordered operation of the fresh Core journey: the giver's whole wood lot.
    wood_terms = terms(journey(1), journey(2), journey(10), journey(11), 1, journey(72), journey(5002),
                       digest(b"craft-test-wood"), journey(60), journey(61), 4, 0)
    wood_command = command(journey(50001), journey(20), journey(30), wood_terms)
    wood_acceptance = acceptance(journey(50002), journey(21), journey(31), journey(50001), wood_command, wood_terms)
    operation = frame("production.material-operation", number(1), number(10), number(1), journey(50001),
                      frame("material.consented-transfer", wood_command, journey(20), journey(30), wood_acceptance,
                            journey(21), journey(31), journey(72)))
    operations = frame("production.material-operations", operation)

    vectors = {
        "termsRaw": raw_terms, "termsProduced": produced_terms, "actionOffer": offer,
        "actionAcceptance": accepted, "stepBatch": step, "evidenceRaw": evidence_raw,
        "evidenceProduced": evidence_produced, "history": history, "historyEmpty": frame(history_tag),
        "operationV10Raw": operation, "operationsV10": operations,
    }

    opaque = lambda name, size=24: digest(("consented-format-v1/opaque/" + name).encode("utf-8"))[:size]
    reference_example = {"worldId": world, "branchId": branch, "ordinal": number(1), "boundarySequence": number(9),
                         "boundaryHash": opaque("closing-root", 32)}
    reference = composition("world.material-care-reference.v1", [
        ("fixed", "worldId", 16), ("fixed", "branchId", 16), ("fixed", "ordinal", 8),
        ("fixed", "boundarySequence", 8), ("fixed", "boundaryHash", 32)], reference_example,
        "Public facts of the actual WorldMaterialCareArchiveReference.",
        "boundaryHash is SHA-256 of the actual opaque closing root; the root itself is not independently encoded.")
    reference_bytes = bytes.fromhex(reference["example"]["hex"])
    anchor_example = {"reference": reference_bytes, "admissionVersion": opaque("admission", 32),
                      "latestCareSequence": number(7), "productionContext": opaque("production-context", 32),
                      "careContext": opaque("care-context", 32), "progression": opaque("progression", 32),
                      "quests": opaque("quests", 32), "lifecycle": opaque("lifecycle", 32),
                      "sourceBaseline": opaque("source-baseline", 32)}
    anchor = composition("world.material-care-anchor.v1", [
        ("variable", "reference"), ("fixed", "admissionVersion", 32), ("fixed", "latestCareSequence", 8),
        ("fixed", "productionContext", 32), ("fixed", "careContext", 32), ("fixed", "progression", 32),
        ("fixed", "quests", 32), ("fixed", "lifecycle", 32), ("fixed", "sourceBaseline", 32)], anchor_example,
        "Public hashes of the actual anchor and closing owners; reference from the reference composition.",
        "Every 32-byte slot is an opaque runtime owner hash. SHA-256 of the frame is the anchor identity.")
    binding_example = {"reference": reference_bytes, "anchorHash": digest(bytes.fromhex(anchor["example"]["hex"]))}
    binding = composition("world.material-care-binding.v1", [("variable", "reference"), ("fixed", "anchorHash", 32)],
                          binding_example, "Reference and anchor compositions filled with actual public facts.",
                          "At most 512 bytes; empty (zero-length field, no frame) before the first cut.")
    coordination_example = {"care": opaque("care"), "sources": opaque("sources"),
                            "binding": bytes.fromhex(binding["example"]["hex"]), "history": history}
    coordination = composition("world.coordinated-consented-care.v1", [
        ("variable", "care"), ("variable", "sources"), ("variable", "binding"), ("variable", "history")],
        coordination_example,
        "care and sources are fields 1 and 2 of the actual root's coordination frame (previously qualified owners).",
        "care/sources are opaque; binding and history are the NEW contracts covered by their own entries.")
    owners = ["inventory", "space", "residents", "admissionVersion", "calendarDays", "calendarMilliseconds",
              "revision", "advancingSteps", "dialogues", "gathering", "production", "progression", "quests",
              "construction", "reservoirs", "reservoirSeals", "liquidCells", "excavation", "pacing",
              "encounterLifecycle", "coordination", "resourceHistory"]
    snapshot_example = {name: opaque(name) for name in owners}
    snapshot_example["coordination"] = bytes.fromhex(coordination["example"]["hex"])
    snapshot = composition("world.snapshot", [("literal", number(28))] + [("variable", name) for name in owners],
                           snapshot_example,
                           "Fields 2..23 of the actual encoded root of the existing ordinary care fixture graph.",
                           "Only the 24-field envelope, tag and profile number 28 are independent. Every owner "
                           "payload except coordination is opaque legacy bytes with its own earlier qualification.")
    production_example = {"worldId": world, "branchId": branch, "revision": number(3),
                          "contextHash": opaque("context", 32), "totalBatches": number(2),
                          "totalConsumed": number(1), "batches": frame("production.batches"),
                          "lots": frame("production.lots"), "consumed": frame("production.consumed")}
    production_parts = [("fixed", "worldId", 16), ("fixed", "branchId", 16), ("fixed", "revision", 8),
                        ("fixed", "contextHash", 32), ("fixed", "totalBatches", 8), ("fixed", "totalConsumed", 8),
                        ("variable", "batches"), ("variable", "lots"), ("variable", "consumed")]
    production_v11 = composition("production.snapshot", [("literal", number(11))] + production_parts,
                                 production_example,
                                 "Public facts of the actual continued ProductionSnapshot and its context hash.",
                                 "contextHash (interpretation 9) and the three collections are opaque runtime bytes.")
    production_v10 = composition("production.snapshot", [
        ("literal", number(10)), ("literal", journey(1)), ("literal", journey(2)), ("literal", number(1)),
        ("fixed", "contextHash", 32), ("literal", number(0)), ("literal", number(0)), ("literal", number(1)),
        ("literal", frame("production.batches")), ("literal", frame("production.lots")),
        ("literal", frame("production.consumed")), ("literal", operations)],
        {"contextHash": opaque("journey-context", 32)},
        "Context hash of the actual Core journey's consented continuation (interpretation 8).",
        "Only contextHash is opaque. Revision, counters, empty collections and the operation are independent.")

    return {
        "contract": "consented-world-formats-v1",
        "provenance": "Generated by Beta/implementation/p02/consented-format-qualification/format_vectors.py "
                      "from explicit semantic facts; no runtime codec output is read.",
        "facts": {"historyContentHash": history_hash, "historyEmptyContentHash": empty_history_hash,
                  "splitChild": child.hex(), "offerMaterialCommandHash": sha256(raw_command).hexdigest()},
        "vectors": {name: vector(data) for name, data in vectors.items()},
        "compositions": {"archiveReference": reference, "archiveAnchor": anchor, "archiveBinding": binding,
                         "coordinationV28": coordination, "snapshotV28": snapshot,
                         "productionV11": production_v11, "productionV10": production_v10},
    }


def main():
    if len(sys.argv) != 2 or sys.argv[1] not in ("--write", "--check"):
        print("Select --write or --check.")
        return 2
    expected = json.dumps(build(), indent=2) + "\n"
    if sys.argv[1] == "--write":
        TARGET.write_text(expected)
        print("wrote", TARGET, sha256(expected.encode("utf-8")).hexdigest())
        return 0
    if not TARGET.exists() or TARGET.read_text() != expected:
        print("MISMATCH", TARGET)
        return 1
    print("match", TARGET, sha256(expected.encode("utf-8")).hexdigest())
    return 0


if __name__ == "__main__":
    sys.exit(main())
