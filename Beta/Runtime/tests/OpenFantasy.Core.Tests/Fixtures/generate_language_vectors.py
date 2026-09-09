"""Independent actor-comprehension oracle; authored qualification metadata, stdout only."""
import hashlib
import json
import struct


def identity(value):
    return value.to_bytes(16, "big")


def number(value):
    return struct.pack(">Q", value)


def frame(tag, *fields):
    fields = [tag.encode("ascii"), *fields]
    return b"OFC\0" + struct.pack(">II", 1, len(fields)) + b"".join(
        struct.pack(">I", len(field)) + field for field in fields)


rules_hash = hashlib.sha256(b"qualification-comprehension-bands.v1:unfamiliar,practical,conversational,nuanced").digest()


def channel(language, register, mode):
    return dict(languageId=identity(language).hex(), registerId=identity(register).hex(), mode=mode)


def key(value):
    return value["languageId"], value["registerId"], value["mode"]


def channel_bytes(value):
    return frame("language.channel", bytes.fromhex(value["languageId"]), bytes.fromhex(value["registerId"]), number(value["mode"]))


def definition_hash(value):
    # An explicit fixture descriptor pin, separate from real content/catalog validation.
    return hashlib.sha256(frame("qualification.language-channel-definition", channel_bytes(value))).hexdigest()


def entry(language, register, mode, band):
    value = channel(language, register, mode)
    return dict(channel=value, definitionVersion=definition_hash(value), band=band)


def entry_bytes(value):
    return frame("language.comprehension-entry", channel_bytes(value["channel"]),
                 bytes.fromhex(value["definitionVersion"]), number(value["band"]))


def profile(name, actor, revision, entries):
    entries = sorted(entries, key=lambda value: key(value["channel"]))
    encoded = frame("language.comprehension-profile", number(1), identity(actor), number(revision), rules_hash,
                    frame("language.comprehension-entries", *map(entry_bytes, entries)))
    return dict(name=name, actorId=identity(actor).hex(), revision=str(revision), rulesVersion=rules_hash.hex(),
                entries=entries, canonicalHex=encoded.hex(), sha256=hashlib.sha256(encoded).hexdigest())


profiles = [profile("actor-a", 101, (1 << 64) - 1,
                    [entry(1, 10, 3, 2), entry(1, 11, 1, 1), entry(1, 10, 1, 3), entry(1, 11, 3, 0)]),
            profile("actor-b", 102, 7,
                    [entry(1, 10, 1, 1), entry(1, 10, 2, 2), entry(1, 11, 1, 3), entry(2, 10, 1, 1)]),
            profile("unrecorded-actor", 103, 0, [])]
by_name = {value["name"]: value for value in profiles}


def query(name, profile_name, language, register, mode, minimum):
    selected = by_name[profile_name]
    selected_channel = channel(language, register, mode)
    found = next((value for value in selected["entries"] if key(value["channel"]) == key(selected_channel)), None)
    available = 0 if found is None else found["band"]
    return dict(name=name, profile=profile_name, channel=selected_channel,
                definitionVersion=definition_hash(selected_channel), minimumBand=minimum,
                expectedAvailableBand=available, expectedHasRecordedEntry=found is not None,
                expectedMeetsBandRequirement=available >= minimum)


queries = [query("exact-nuanced-spoken-threshold", "actor-a", 1, 10, 1, 3),
           query("spoken-does-not-upgrade-written", "actor-a", 1, 10, 3, 3),
           query("spoken-does-not-grant-sign", "actor-a", 1, 10, 2, 1),
           query("common-register-does-not-upgrade-cant", "actor-a", 1, 11, 1, 2),
           query("recorded-unfamiliar-remains-unfamiliar", "actor-a", 1, 11, 3, 1),
           query("related-language-is-not-inherited", "actor-a", 2, 10, 1, 1),
           query("other-actor-has-own-spoken-band", "actor-b", 1, 10, 1, 2),
           query("other-actor-knows-sign", "actor-b", 1, 10, 2, 2),
           query("cant-can-exceed-common-register", "actor-b", 1, 11, 1, 3),
           query("no-language-prerequisite-is-not-understanding", "unrecorded-actor", 1, 10, 1, 0),
           query("unrecorded-practical-requirement-fails", "unrecorded-actor", 1, 10, 1, 1)]
print(json.dumps(dict(format="language-v1",
                     provenance="Authored actor-scoped qualification metadata; no species, culture, localization or translation grants",
                     profiles=profiles, queries=queries), indent=2))
