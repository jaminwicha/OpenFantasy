"""Independently check actual read-only boundary diagnostics on retained terrain and funded construction worlds."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import struct
import subprocess
import datetime


def fields(data):
    assert data[:4] == b'OFC\0'
    version, count = struct.unpack_from('>II', data, 4)
    assert version == 1
    result, offset = [], 12
    for _ in range(count):
        size, = struct.unpack_from('>I', data, offset)
        offset += 4
        result.append(data[offset:offset + size])
        offset += size
    assert offset == len(data)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', required=True, type=Path)
    parser.add_argument('--fixture', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    fixture, output = args.fixture.resolve(), args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    command = [str(args.dotnet.resolve()), str(root / 'src/OpenFantasy.Host/bin/Debug/net10.0/OpenFantasy.Host.dll')]
    env = dict(os.environ, DOTNET_ROOT=str(args.dotnet.resolve().parent))
    report = dict(status='running', startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  scope='Exact recorded terrain and real NPC-built tank boundary assessment; no authority mutation or pressure-strength claim', runs=[], databases=[])

    def run(database, revision, policy, success=True):
        selected = command + ['--inspect-reservoir-boundaries', str(database), str(revision), str(policy)]
        result = subprocess.run(selected, env=env, cwd=root, capture_output=True, text=True, timeout=30)
        report['runs'].append(dict(command=selected, exitCode=result.returncode, stdout=result.stdout, stderr=result.stderr))
        assert result.returncode == (0 if success else 1), result.stdout + result.stderr
        if not success:
            assert result.stdout.startswith('Inspection failed:')
            return None
        value = json.loads(result.stdout)
        assert value['Contract'] == 'reservoir-boundary-assessment-v1'
        assert value['RecordedRevision'] == revision and value['WorldHash'] == value['StateHash']
        assert value['PolicyHash'] == hashlib.sha256(policy.read_bytes()).hexdigest()
        return value

    def preserved(database, before):
        after = hashlib.sha256(database.read_bytes()).hexdigest()
        assert before == after
        report['databases'].append(dict(path=str(database), sha256Before=before, sha256After=after))

    try:
        policy = fixture / 'seal-policy.bin'
        wire = fields(policy.read_bytes())
        assert len(wire) == 3 and wire[0] == b'geography.reservoir-seal-policy.v1' and len(wire[1]) == 48
        assert len(wire[2]) == 3 * 56
        assert all(struct.unpack_from('>Q', wire[2], i + 48)[0] == 1000 for i in range(0, len(wire[2]), 56))
        database = fixture / 'contained-world.sqlite'
        before = hashlib.sha256(database.read_bytes()).hexdigest()
        for revision in [1, 0, 1]:
            value = run(database, revision, policy)
            assert value['Completed'] and value['IsContained']
            assert value['SourceHeadRevision'] == 1
            for vessel in value['Results']:
                assert vessel['IsContained'] and not vessel['Failures']
                assert (vessel['InteriorCells'], vessel['BoundaryFaces'], vessel['TerrainSealedFaces'], vessel['ConstructedSealedFaces']) == (2, 9, 9, 0)
        run(database, 2, policy, success=False)
        run(database, '-1', policy, success=False)
        preserved(database, before)
        for variant in ['sealed', 'slit', 'displaced', 'intruding']:
            database = fixture / (variant + '-construction.sqlite')
            policy = fixture / (variant + '-policy.bin')
            expected = json.loads((fixture / (variant + '-revisions.json')).read_text())
            before = hashlib.sha256(database.read_bytes()).hexdigest()
            for revision in [expected['CompletedRevision'], expected['BeforeWorkRevision'], 0, expected['CompletedRevision']]:
                value = run(database, revision, policy)
                complete = revision == expected['CompletedRevision']
                assert value['Completed']
                assert value['IsContained'] == (complete and variant == 'sealed')
                assert value['SourceHeadRevision'] == expected['CompletedRevision']
                assert value['SourceHeadHash'] == expected['WorldHash']
                tank, receiver = value['Results']
                assert tank['ReservoirId'].endswith('000000000000001e')
                assert receiver['IsContained'] and receiver['TerrainSealedFaces'] == 9
                assert (tank['InteriorCells'], tank['BoundaryFaces'], tank['TerrainSealedFaces']) == (2, 9, 1)
                assert tank['ConstructedSealedFaces'] == ((8 if variant == 'sealed' else 6) if complete else 0)
                assert tank['Failures'].get('UnsealedWall', 0) == ((0 if variant == 'sealed' else 2) if complete else 8)
                assert tank['Failures'].get('InteriorConstruction', 0) == (2 if complete and variant == 'intruding' else 0)
                if complete:
                    assert value['WorldHash'] == expected['WorldHash'] and value['ReservoirHash'] == expected['ReservoirHash']
            preserved(database, before)
        report['independentGeometry'] = dict(cellMetres=1, waterDepthMetres=2, boundaryFacesPerVessel=9,
            terrainFloorFacesPerTank=1, tankWallFaces=8, slitMillimetres=1,
            slitUncoveredSquareMillimetresPerFace=1000, failedWestFacesForEachDefectiveTank=2,
            note='Fixture sheet is synthetic calibrated stock; exact member coverage and material provenance are tested, not hydraulic strength.')
        report['status'] = 'passed'
    except Exception as error:
        report['status'] = 'failed'
        report['failure'] = repr(error)
    finally:
        report['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        report['fixtureSha256'] = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(fixture.iterdir()) if p.is_file()}
        output.write_text(json.dumps(report, indent=2) + '\n')
    print('reservoir boundaries: ' + report['status'], flush=True)
    return 0 if report['status'] == 'passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
