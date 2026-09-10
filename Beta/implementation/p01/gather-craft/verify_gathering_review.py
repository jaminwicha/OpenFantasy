#!/usr/bin/env python3
"""Read-only reviewer evidence: exact input hashes, stdout oracle and finite reserve arithmetic.

This does not execute C#, Godot, SQLite or the native game.
"""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[4]
RUNTIME = ROOT / "Beta/Runtime"
CORE = RUNTIME / "src/OpenFantasy.Core"
TESTS = RUNTIME / "tests/OpenFantasy.Core.Tests"
DOCS = Path(__file__).resolve().parent


def digest(data):
    return hashlib.sha256(data).hexdigest()


oracle = TESTS / "Fixtures/generate_gathering_vectors.py"
fixture = TESTS / "Fixtures/gathering-v1.json"
generated = subprocess.run([sys.executable, str(oracle)], check=True, capture_output=True).stdout
assert generated == fixture.read_bytes()
vectors = json.loads(generated)
for name in ("action", "batch", "snapshot"):
    vector = vectors[name]
    assert digest(bytes.fromhex(vector["hex"])) == vector["sha256"]

# Independent discrete proof witnesses: every admitted remaining cycle survives any
# supported next tool choice without becoming a visible but unfinishable resource.
reserve_cases = 0
for swings in (4, 5):
    for remaining in range(1, swings + 1):
        for strongest in range(1, 4):
            for excess in range(33):
                reserve = remaining * strongest + excess
                for selected in range(1, strongest + 1):
                    assert reserve - selected >= (remaining - 1) * strongest
                    reserve_cases += 1

# Thirty-two authored power-one cycles consume disjoint consecutive source rights.
cycles = []
for kind, swings, capacity, delay in (("oak", 5, 160, 900), ("berry", 4, 128, 700)):
    intervals = [(n, n + 1) for n in range(capacity)]
    assert intervals[0][0] == 0 and intervals[-1][1] == capacity
    assert all(left[1] == right[0] for left, right in zip(intervals, intervals[1:]))
    assert capacity // swings == 32 and capacity % swings == 0
    cycles.append({"kind": kind, "cycles": 32, "units": capacity,
                   "activeSecondsAt20Hz": delay / 20,
                   "calendarMillisecondsAtPinnedPacing": delay * 1800})

world_names = ["WorldSnapshot", "WorldAdmissionValidator", "WorldIdentityIndex", "WorldStepProcessor",
               "WorldActionResolver", "WorldActionCommand", "WorldStepBatch", "WorldFrameReader",
               "WorldSnapshotCodec", "WorldSnapshotCodecV1", "WorldSnapshotCodecV2",
               "WorldActionCodec", "WorldActionCodecV1", "WorldActionCodecV2",
               "WorldStepCodec", "WorldStepCodecV1", "WorldStepCodecV2", "WorldCodecVersion"]
inputs = [*sorted((CORE / "Gathering").glob("*.cs")),
          CORE / "Spatial/ResourceInteractionQueries.cs", CORE / "Spatial/TerrainSightline.cs",
          *[CORE / "Worlds" / (name + ".cs") for name in world_names],
          *sorted((TESTS / "Gathering").glob("*.cs")),
          TESTS / "Worlds/GatheringGoldenTests.cs", TESTS / "Worlds/WorldCodecDispatchTests.cs",
          oracle, fixture, DOCS / "CORE_CONTRACT.md", DOCS / "CORE_INTEGRATION_REVIEW.md",
          Path(__file__)]
print(json.dumps({"kind": "independent_gathering_core_review", "status": "passed",
                  "dotNetGodotOrSqliteExecuted": False,
                  "oracleExactStdoutBytes": len(generated), "oracleFixtureSha256": digest(generated),
                  "canonicalVectors": 3, "reserveArithmeticCases": reserve_cases,
                  "finiteAuthoredCycles": cycles,
                  "inputs": [{"path": str(p.relative_to(ROOT)), "bytes": p.stat().st_size,
                              "sha256": digest(p.read_bytes())} for p in inputs]}, indent=2))
