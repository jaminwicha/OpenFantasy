#!/usr/bin/env python3
"""Independent integer/byte oracle for the bounded timing component, not a live-world combat proof."""
import argparse
import base64
import copy
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import time

U64_MAX = (1 << 64) - 1
PIN = hashlib.sha256(b"independent encounter action").digest()


def number(value):
    return struct.pack(">Q", value)


def identity(value):
    return value.to_bytes(16, "big")


def frame(tag, *fields):
    fields = (tag.encode(), *fields)
    return b"OFC\0" + struct.pack(">II", 1, len(fields)) + b"".join(struct.pack(">I", len(field)) + field for field in fields)


def archive(state):
    actors = b"".join(identity(a[0]) + number(a[1]) + number(a[2]) + number(a[3]) for a in state["actors"])
    decisions = b"".join(identity(d[0]) + identity(d[1]) + number(d[2]) + PIN for d in state["decisions"])
    return frame("encounter.timing.v1", number(1), identity(1), identity(2), identity(3),
                 number(state["revision"]), number(state["units"]),
                 frame("encounter.actors.v1", actors), frame("encounter.decisions.v1", decisions))


def describe(state):
    chosen = {d[0] for d in state["decisions"]}
    return {
        "World": identity(1).hex(), "Branch": identity(2).hex(), "Encounter": identity(3).hex(),
        "Revision": state["revision"], "ElapsedTacticalUnits": state["units"],
        "Hash": hashlib.sha256(archive(state)).hexdigest(),
        "DueActors": [identity(a[0]).hex() for a in state["actors"] if a[2] == 0],
        "RequiredDecisions": [identity(a[0]).hex() for a in state["actors"] if a[2] == 0 and a[3] == 1 and a[0] not in chosen],
        "Actors": [{"Id": identity(a[0]).hex(), "NormalizedSpeed": a[1], "RemainingDelay": a[2],
                    "StandardDelay": (10000 + a[1] - 1) // a[1], "Control": {1: "RequiredHuman", 2: "Automatic"}[a[3]]}
                   for a in state["actors"]],
        "Decisions": [{"Actor": identity(d[0]).hex(), "Id": identity(d[1]).hex(),
                       "Kind": {1: "Wait", 2: "Action"}[d[2]], "Pin": PIN.hex()} for d in state["decisions"]]}


def proposal(state, confirm):
    due = [a for a in state["actors"] if a[2] == 0]
    chosen = {d[0]: d for d in state["decisions"]}
    required = [a for a in due if a[3] == 1 and a[0] not in chosen]
    following = copy.deepcopy(state)
    decision = None
    if required:
        status = "RequiredDecisionMissing"
    elif confirm and not due:
        status = "NoDueActors"
    elif confirm and due[0][0] not in chosen:
        status = "AutomaticDecisionMissing"
    elif confirm:
        status = "Confirmed"
        actor_id = due[0][0]
        decision = identity(chosen[actor_id][1]).hex()
        following["actors"] = [(a[0], a[1], (10000 + a[1] - 1) // a[1] if a[0] == actor_id else a[2], a[3])
                               for a in state["actors"]]
        following["decisions"] = [d for d in state["decisions"] if d[0] != actor_id]
    elif due:
        status = "AutomaticDecisionMissing" if any(a[0] not in chosen for a in due) else "ActionBoundaryPending"
    else:
        status = "Advanced"
        following["units"] += 5
        following["actors"] = [(a[0], a[1], max(0, a[2] - 5), a[3]) for a in state["actors"]]
    accepted = status in ("Advanced", "Confirmed")
    if accepted:
        following["revision"] += 1
        if following["revision"] > U64_MAX or following["units"] > U64_MAX:
            return {"Available": False, "Refusal": "Timing counter capacity exhausted; no proposal was published."}, state
    return {"Available": True, "Status": status, "IsAccepted": accepted,
            "ExperiencedMilliseconds": 50 if status == "Advanced" else 0,
            "ConfirmedDecision": decision, "State": describe(following),
            "ArchiveBase64": base64.b64encode(archive(following)).decode()}, following


def state(actors, decisions=(), revision=0, units=0):
    return {"actors": list(actors), "decisions": list(decisions), "revision": revision, "units": units}


def exposure_vectors():
    vectors = []
    for name, bound, elapsed, initial_ms, numerator, denominator in (
            ("partial-before-tick", 0, 5, 1799, 1, 1800),
            ("late-bound-no-retroactive-time", 100, 185, 19, 7, 13),
            ("zero-rate-large-clock", 0, U64_MAX, 1, 0, 7),
            ("held-nonzero-carry", 50, 50, 1799, 1, 1800)):
        clock = state([(10, 137, 73, 1)], revision=elapsed // 5, units=elapsed)
        total_ms = initial_ms + (elapsed - bound) * 10
        completed, remainder = divmod(total_ms * numerator, denominator)
        rules = hashlib.sha256(b"exposure-encounter-test-v1").digest()
        progress = frame("exposure.progress.v1", identity(10), identity(20), rules, number(numerator),
                         number(denominator), number(total_ms // 86400000), number(total_ms % 86400000),
                         number(completed), number(remainder))
        encoded = frame("encounter.exposure.v1", number(1), identity(1), identity(2), identity(3), number(bound),
                        number(initial_ms // 86400000), number(initial_ms % 86400000), number(clock["revision"]),
                        number(elapsed), hashlib.sha256(archive(clock)).digest(), progress)
        vectors.append({"name": name, "clockHex": archive(clock).hex(), "hex": encoded.hex(),
                        "hash": hashlib.sha256(encoded).hexdigest(), "boundAt": str(bound),
                        "initialMs": str(initial_ms), "numerator": str(numerator), "denominator": str(denominator),
                        "completed": str(completed), "remainder": str(remainder), "elapsedMs": str(total_ms)})
    return {"producer": "Python arbitrary-precision arithmetic and independently framed bytes", "cases": vectors}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet")
    parser.add_argument("--host")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--write-exposure-vectors", type=Path)
    args = parser.parse_args()
    if args.write_exposure_vectors:
        args.write_exposure_vectors.write_text(json.dumps(exposure_vectors(), indent=2) + "\n")
        if not args.output:
            return
    if not all((args.dotnet, args.host, args.output)):
        parser.error("Host checks require --dotnet, --host and --output")
    args.output.mkdir(parents=True, exist_ok=True)
    records = []

    def inspect(name, data, expected=None, scope=(1, 2, 3), reject=False):
        path = args.output / (name + ".bin")
        path.write_bytes(data)
        before = hashlib.sha256(data).hexdigest()
        started = time.monotonic()
        result = subprocess.run([args.dotnet, args.host, "--inspect-encounter-timing", str(path),
                                 *(identity(part).hex() for part in scope)], capture_output=True, text=True, timeout=10)
        elapsed = time.monotonic() - started
        record = {"case": name, "exit": result.returncode, "seconds": round(elapsed, 4),
                  "inputSha256": before, "stdout": result.stdout, "stderr": result.stderr}
        records.append(record)
        (args.output / "host-results.json").write_text(json.dumps(records, indent=2) + "\n")
        assert elapsed <= 10, name + " exceeded unchanged process bound"
        assert hashlib.sha256(path.read_bytes()).hexdigest() == before, name + " input mutated"
        assert result.returncode == (1 if reject else 0), name + ": " + result.stdout + result.stderr
        if reject:
            assert result.stdout.startswith("Inspection failed: "), name
            return None
        report = json.loads(result.stdout)
        assert report["WorldConsequencesPublished"] is False, name
        assert report["InputHash"] == before, name
        assert report["Initial"] == describe(expected), name + " initial differs"
        assert report["Advance"] == proposal(expected, False)[0], name + " advance differs"
        assert report["Confirmation"] == proposal(expected, True)[0], name + " confirmation differs"
        return report

    samples = {
        "required-human-blocks-earlier-auto": state([(10, 137, 0, 2), (20, 71, 0, 1)], [(10, 110, 2)]),
        "all-chosen-canonical-confirm": state([(10, 137, 0, 2), (20, 71, 0, 1)], [(10, 110, 2), (20, 120, 1)]),
        "missing-automatic-decision": state([(10, 100, 0, 2), (20, 100, 0, 1)], [(20, 120, 1)]),
        "partial-delay-and-clamping": state([(10, 137, 17, 1), (20, 71, 1, 2)], revision=7, units=35),
        "slowest-standard-delay": state([(10, 1, 0, 1)], [(10, 110, 1)]),
        "fastest-standard-delay": state([(10, (1 << 32) - 1, 0, 1)], [(10, 110, 2)]),
        "counter-exhaustion": state([(10, 100, 17, 1)], revision=U64_MAX),
        "maximum-complete-clock": state([(i, 100, 0, 1) for i in range(100, 228)],
                                        [(i, i + 1000, 2) for i in range(100, 228)], revision=U64_MAX, units=U64_MAX),
    }
    for name, sample in samples.items():
        inspect(name, archive(sample), sample)

    continued = state([(10, 137, 17, 1)], revision=7, units=35)
    encoded = archive(continued)
    for index in range(6):
        report = inspect("separate-process-resume-" + str(index), encoded, continued)
        encoded = base64.b64decode(report["Advance"]["ArchiveBase64"])
        continued = proposal(continued, False)[1]
    assert continued["units"] == 55 and continued["actors"][0][2] == 0

    valid = archive(samples["all-chosen-canonical-confirm"])
    malformed = {
        "truncated": valid[:-1], "trailing": valid + b"\0", "empty": b"",
        "oversized": b"\0" * 16385,
        "no-actors": archive(state([])),
        "129-actors": archive(state([(i, 100, 0, 1) for i in range(100, 229)])),
        "zero-speed": archive(state([(10, 0, 0, 1)])),
        "speed-width-overflow": archive(state([(10, 1 << 32, 0, 1)])),
        "unknown-control": archive(state([(10, 100, 0, 3)])),
        "unknown-kind": archive(state([(10, 100, 0, 1)], [(10, 110, 3)])),
        "unordered-actors": archive(state([(20, 100, 0, 1), (10, 100, 0, 1)])),
        "duplicate-actors": archive(state([(10, 100, 0, 1), (10, 100, 0, 2)])),
        "actor-scope-alias": archive(state([(3, 100, 0, 1)])),
        "decision-scope-alias": archive(state([(10, 100, 0, 1)], [(10, 3, 1)])),
        "not-due-decision": archive(state([(10, 100, 1, 1)], [(10, 110, 1)])),
        "clock-not-whole-tick": archive(state([(10, 100, 1, 1)], revision=1, units=1)),
        "clock-exceeds-revisions": archive(state([(10, 100, 1, 1)], revision=1, units=10)),
        "unsupported-framing-version": valid[:7] + b"\2" + valid[8:],
    }
    for name, data in malformed.items():
        inspect(name, data, reject=True)
    for index in range(3):
        wrong_scope = [1, 2, 3]
        wrong_scope[index] = 4
        inspect("foreign-scope-" + str(index), valid, scope=wrong_scope, reject=True)
    summary = {"passed": True, "hostProcesses": len(records), "independentSampleStates": len(samples),
               "separateProcessContinuations": 6, "rejectedInputs": len(malformed) + 3,
               "maximumClockBytes": len(archive(samples["maximum-complete-clock"])),
               "worldEffectsOrDatabaseRecoveryClaimed": False}
    (args.output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
    print(json.dumps(summary))


if __name__ == "__main__":
    main()
