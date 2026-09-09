#!/usr/bin/env python3
"""Independent fixture reproduction and integer-domain review; does not execute C#."""
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import platform
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[4]
FIXTURES = ROOT / 'Beta/Runtime/tests/OpenFantasy.Core.Tests/Fixtures'
MINIMUM = -(1 << 63)
MAXIMUM = (1 << 63) - 1


def digest(data):
    return hashlib.sha256(data).hexdigest()


def csharp_floor_division(coordinate):
    # Reproduce only the reviewed signed-division adjustment; compare with Python Euclidean division.
    quotient = abs(coordinate) // 8
    if coordinate < 0:
        quotient = -quotient
    remainder = coordinate - quotient * 8
    if remainder < 0:
        quotient -= 1
        remainder += 8
    return quotient, remainder


def main():
    command = [sys.executable, str(FIXTURES / 'generate_terrain_vectors.py')]
    run = subprocess.run(command, capture_output=True, check=True,
                         env={**os.environ, 'PYTHONDONTWRITEBYTECODE': '1'})
    retained = (FIXTURES / 'terrain-v1.json').read_bytes()
    assert run.stdout == retained, 'Independent generator stdout differs from retained fixture.'
    fixture = json.loads(retained)

    coordinates = set(range(-128, 129))
    coordinates.update(MINIMUM + offset for offset in range(129))
    coordinates.update(MAXIMUM - offset for offset in range(129))
    random_state = 0x239afb0248ed1327
    for _ in range(10_000):
        random_state = (6364136223846793005 * random_state + 1442695040888963407) & ((1 << 64) - 1)
        coordinates.add(random_state - (1 << 63))
    for coordinate in coordinates:
        quotient, remainder = csharp_floor_division(coordinate)
        assert (quotient, remainder) == divmod(coordinate, 8)
        assert 0 <= remainder <= 7
        assert MINIMUM <= quotient <= MAXIMUM
        assert MINIMUM <= quotient + 1 <= MAXIMUM

    interpolation_cases = 0
    for relief in range(9):
        for fraction_x in range(8):
            for fraction_y in range(8):
                weights = [(8 - fraction_x) * (8 - fraction_y), fraction_x * (8 - fraction_y),
                           (8 - fraction_x) * fraction_y, fraction_x * fraction_y]
                assert sum(weights) == 64 and all(weight >= 0 for weight in weights)
                # Nonnegative linear weights attain extrema at endpoint corner samples.
                for mask in range(16):
                    weighted = sum(weight * (2 * relief if mask & (1 << index) else 0)
                                   for index, weight in enumerate(weights))
                    assert 0 <= weighted <= 1024
                    assert -relief <= weighted // 64 - relief <= relief
                    interpolation_cases += 1

    indexing_cases = 0
    regions = [((MINIMUM, MINIMUM, MINIMUM), (32, 32, 16)),
               ((MAXIMUM - 31, MAXIMUM - 31, MAXIMUM - 15), (32, 32, 16)),
               ((-19, -13, -11), (32, 32, 16)), ((-3, -5, -7), (4, 3, 2))]
    for minimum, dimensions in regions:
        width, depth, height = dimensions
        for index in range(width * depth * height):
            point = (minimum[0] + index % width,
                     minimum[1] + index // width % depth,
                     minimum[2] + index // (width * depth))
            assert all(MINIMUM <= component <= MAXIMUM for component in point)
            restored = (((point[2] - minimum[2]) * depth + point[1] - minimum[1]) * width
                        + point[0] - minimum[0])
            assert restored == index
            indexing_cases += 1

    nonvoid_cells = 0
    source_ids = {}
    snapshot_count = 0
    for example in fixture['examples']:
        assert digest(bytes.fromhex(example['rulesCanonicalHex'])) == example['rulesSha256']
        context = (example['scope']['worldId'], example['scope']['realmId'], example['seed'], example['rulesSha256'])
        for cell in example['cells']:
            if not cell['stratum']:
                assert cell['sourceId'] is None and cell['formationId'] is None and cell['material'] is None
                continue
            nonvoid_cells += 1
            address = (context, tuple(cell['coordinate']))
            assert source_ids.setdefault(cell['sourceId'], address) == address
            assert cell['sourceId'] != cell['formationId']
        for snapshot in example['states']:
            assert digest(bytes.fromhex(snapshot['canonicalHex'])) == snapshot['sha256']
            assert snapshot['excavatedIndices'] == sorted(set(snapshot['excavatedIndices']))
            assert all(example['cells'][index]['stratum'] for index in snapshot['excavatedIndices'])
            snapshot_count += 1

    inputs = sorted((ROOT / 'Beta/Runtime/src/OpenFantasy.Core/Terrain').glob('*.cs'))
    inputs += sorted((ROOT / 'Beta/Runtime/tests/OpenFantasy.Core.Tests/Terrain').glob('*.cs'))
    inputs += [FIXTURES / 'generate_terrain_vectors.py', FIXTURES / 'terrain-v1.json',
               ROOT / 'Beta/implementation/p01/terrain-foundation/CONTRACT.md', Path(__file__).resolve()]
    result = {
        'schemaVersion': 1,
        'reviewKind': 'independent_static_review_and_executed_python_checks_not_csharp_execution',
        'executedAtUtc': datetime.now(timezone.utc).isoformat(),
        'python': platform.python_version(),
        'proof': {'oracleMatchesRetainedBytes': True, 'fixtureBytes': len(retained),
                  'fixtureSha256': digest(retained), 'generationExamples': len(fixture['examples']),
                  'expectedCells': sum(len(example['cells']) for example in fixture['examples']),
                  'nonvoidSourceCellsChecked': nonvoid_cells, 'snapshotStates': snapshot_count,
                  'malformedVectorsInspectedNotExecutedInCSharp': len(fixture['malformed']),
                  'signedFloorComparisons': len(coordinates), 'weightEnvelopeCases': interpolation_cases,
                  'boundsIndexInverseCases': indexing_cases},
        'inputs': [{'path': str(path.relative_to(ROOT)), 'bytes': path.stat().st_size,
                    'sha256': digest(path.read_bytes())} for path in inputs],
        'limits': ['No .NET build or tests run by this reviewer.',
                   'Python arithmetic checks support static C# review; they are not execution of runtime methods.',
                   'Sample source-identity checks are not a mathematical uniqueness or global collision proof.',
                   'No macro terrain, geology, history, fluid, physical support, mining-yield or playable traversal qualification.']}
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
