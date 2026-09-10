#!/usr/bin/env python3
"""Independent integer/rational boundary checks; does not execute the C# implementation."""
import json
from fractions import Fraction
from itertools import product

LONG_MIN, LONG_MAX = -(1 << 63), (1 << 63) - 1
counts = dict(floor_cases=0, half_open_intervals=0, cardinal_sweeps=0, closed_segment_cases=0)
values = set(range(-2049, 2050)) | {LONG_MIN + n for n in range(513)} | {LONG_MAX - n for n in range(513)}
for value in sorted(values):
    trunc = (abs(value) // 256) * (-1 if value < 0 else 1)
    remainder = value - trunc * 256
    adjusted = trunc - (remainder < 0)
    assert adjusted == value // 256
    assert LONG_MIN < adjusted - 1 < LONG_MAX
    counts['floor_cases'] += 1
for minimum in range(-513, 514, 17):
    for width in (1, 2, 127, 128, 255, 256, 257, 511, 512, 2048):
        maximum = minimum + width
        indexed = set(range(minimum // 256, (maximum - 1) // 256 + 1))
        touched = {cell for cell in range(minimum // 256 - 1, maximum // 256 + 2)
                   if max(minimum, cell * 256) < min(maximum, (cell + 1) * 256)}
        assert indexed == touched
        counts['half_open_intervals'] += 1
        for delta in (-256, -32, -1, 1, 32, 256):
            sweep = set(range(min(minimum, minimum + delta), max(maximum, maximum + delta)))
            sampled = set()
            for step in range(min(0, delta), max(0, delta) + 1):
                sampled.update(range(minimum + step, maximum + step))
            assert sweep == sampled
            counts['cardinal_sweeps'] += 1

def intersects(a, b, cell):
    """Exact rational intervals for a closed segment intersecting a closed cell cube."""
    intervals = [(Fraction(0), Fraction(1))]
    for start, end, coordinate in zip(a, b, cell):
        low, high = coordinate * 512, (coordinate + 1) * 512
        if end == start:
            if not low <= start <= high:
                return False
        else:
            t0, t1 = Fraction(low - start, end - start), Fraction(high - start, end - start)
            intervals.append((min(t0, t1), max(t0, t1)))
    return max(i[0] for i in intervals) <= min(i[1] for i in intervals)

for a, b in [((0, 0, 256), (1024, 1024, 256)), ((-512, 0, 512), (512, 0, 512)),
             ((-511, -1, 257), (513, 1023, 771)), ((-1536, 1, 1), (0, 1, 2048)),
             ((1, 1, 1), (1, 1, 1)), ((255, 255, 255), (768, 512, 1024))]:
    ranges = [range(min(x, y) // 512 - 2, max(x, y) // 512 + 3) for x, y in zip(a, b)]
    for cell in product(*ranges):
        result = intersects(a, b, cell)
        assert result == intersects(b, a, cell)
        if result:
            for start, end, coordinate in zip(a, b, cell):
                lo, hi = min(start, end), max(start, end)
                assert lo // 512 - (lo % 512 == 0) <= coordinate <= hi // 512
        for offset in (-((1 << 63) // 512) + 16, ((1 << 63) // 512) - 16):
            translated_a = tuple(n + offset * 512 for n in a)
            translated_b = tuple(n + offset * 512 for n in b)
            assert result == intersects(translated_a, translated_b, tuple(n + offset for n in cell))
        counts['closed_segment_cases'] += 1
print(json.dumps({'status': 'passed', 'scope': 'independent mathematical checks, not C# execution', **counts}, indent=2))
