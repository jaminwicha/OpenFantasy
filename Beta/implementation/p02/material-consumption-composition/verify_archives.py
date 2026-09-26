"""Independent fixture checks for direct retirement and nested consumption ownership.

This reads retained test output. Diagnostic synthetic rows are never persisted or submitted as game
state. The oracle does not authenticate commands, reconstruct recipe mass, or admit complete worlds.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / 'Runtime' / 'tools'))
from check_terrain_materials import fields, frame, integer, replace, u64
from check_material_registry import allocations, oracle as raw_registry_oracle
from check_runoff_capacity import oracle as capacity_oracle


def production(data):
    value = fields(data)
    assert len(value) == 9 and value[0] == b'production.snapshot' and integer(value[1]) == 2
    consumed = fields(value[8])
    assert consumed[0] == b'production.consumed'
    claims, retirements = [], []
    for batch_data in fields(value[6])[1:]:
        batch = fields(batch_data)
        retirements.extend((batch[5], fields(item)) for item in fields(batch[8])[1:])
    for item in consumed[1:]:
        claim = fields(item)
        assert len(claim) == 8 and claim[0] == b'production.consumed-item' and integer(claim[1]) == 1
        assert all(len(claim[index]) == 16 for index in range(2, 7))
        claims.append(claim)
        retirements.append((claim[6], fields(claim[7])))
    assert len({claim[2] for claim in claims}) == len(claims)
    direct = []
    for custodian, ingredient in retirements:
        assert ingredient[0] == b'production.ingredient'
        if integer(ingredient[2]) != 1:
            continue  # Its source ancestry was already retired in the producing batches.
        interval = fields(ingredient[7])
        assert interval[0] == b'production.source-claim'
        source, lo, hi = interval[2], integer(interval[3]), integer(interval[4])
        assert lo < hi
        assert all(source != s or hi <= a or lo >= b for _, s, a, b, _ in direct)
        direct.append((custodian, source, lo, hi, ingredient))
    return value, claims, direct


def registry(data):
    root = fields(data)
    assert len(root) == 5 and root[0] == b'geography.terrain-material-registry.v3'
    state, claims, direct = production(root[4])
    custody = fields(fields(root[2])[3])
    assert state[2:4] == custody[1:3]
    raw = fields(custody[5])
    synthetic = []
    for custodian, source, lo, hi, ingredient in direct:
        assert integer(ingredient[4]) < integer(custody[3])
        # Separate diagnostic custody records retired rights only for the existing spatial oracle.
        synthetic.append(frame([b'custody.source-lot.v1', hashlib.sha256(frame(ingredient)).digest()[:16],
            bytes.fromhex('ff' * 16), ingredient[5], ingredient[6], source, u64(lo), u64(hi), u64(0), u64(0), b'']))
    augmented = replace(root[2], [3, 5], frame(raw + synthetic))
    result = raw_registry_oracle(frame([b'geography.terrain-material-registry.v1', root[1], augmented, root[3]]))
    retired = 0
    bindings = [fields(encoded) for _, encoded, _ in allocations(root[3])]
    representation = {binding[index] for binding in bindings for index in (11, 12)}
    for binding in bindings:
        for custodian, source, lo, hi, _ in direct:
            if source != binding[8]:
                continue
            amount = max(0, min(integer(binding[10]), hi) - max(integer(binding[9]), lo))
            if amount:
                assert custodian not in representation
                retired += amount
    result['OtherQuantity'] -= retired
    result.update(RetiredQuantity=retired, ConsumptionClaims=len(claims),
        ContentHash=hashlib.sha256(data).hexdigest())
    assert sum(result[key] for key in ('CoarseQuantity', 'DetailedQuantity', 'OtherQuantity', 'RetiredQuantity')) == result['AllocatedQuantity']
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--fixtures', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    data = {p.stem: p.read_bytes() for p in args.fixtures.glob('*.bin')}
    result = {'status': 'running', 'scope': __doc__, 'archives': {k: hashlib.sha256(v).hexdigest() for k, v in data.items()}}
    try:
        material = registry(data['TerrainMaterialRegistry-new'])
        assert material['AllocatedQuantity'] == 160 and material['RetiredQuantity'] == 3
        assert material['ConsumptionClaims'] == 1
        single = fields(data['TerrainMaterialState-new'])
        assert single[0] == b'geography.terrain-material-state.v3'
        assert single[4] == fields(data['TerrainMaterialRegistry-new'])[4]
        crafted = registry(data['crafted-consumption'])
        assert crafted['AllocatedQuantity'] == 160 and crafted['RetiredQuantity'] == 4
        assert crafted['ConsumptionClaims'] == 1
        capacity = fields(data['WatershedRunoffCapacityState-new'])
        runoff = fields(data['PreparedWatershedRunoff-new'])
        reservoirs = fields(data['LiquidReservoirState-new'])
        transport = fields(data['ReservoirTransportState-new'])
        assert capacity[0] == b'geography.watershed.runoff-capacity.v2'
        assert runoff[0] == b'geography.watershed.runoff.v2'
        request = fields(runoff[1])
        assert request[0] == b'geography.watershed.runoff-request.v2'
        assert request[3] == capacity[2] and request[4] == capacity[3]
        assert reservoirs[0] == b'geography.liquid-reservoirs.v2' and reservoirs[1] == frame(capacity)
        assert transport[0] == b'geography.reservoir-transport.v2' and transport[3] == frame(reservoirs)
        _, claims, direct = production(capacity[3])
        assert len(claims) == 1 and sum(hi - lo for _, _, lo, hi, _ in direct) == 7
        # Capacity arithmetic is unchanged; relabel only this diagnostic envelope for the old oracle.
        capacity_check = capacity_oracle(frame([b'geography.watershed.runoff-capacity.v1'] + capacity[1:]))
        assert any(receiver['ExistingUnits'] == 993 for receiver in capacity_check['Receivers'])
        result.update(status='passed', raw_material=material, produced_material=crafted,
            capacity_receivers=capacity_check['Receivers'], nested_production_ownership_equal=True)
    except Exception as error:
        result.update(status='failed', failure=repr(error))
        raise
    finally:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps({'status': result['status'], 'archives': len(data), 'output': str(args.output)}))


if __name__ == '__main__':
    main()
