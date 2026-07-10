#!/usr/bin/env python3
"""Composites the generated layer sheets into preview PNGs (the same math the
game's SpriteComposer uses) so sprite quality can be inspected offline.

Usage: python3 tools/preview_sprites.py [out_dir]
Emits: preset gallery sheet (all presets, all directions/frames) upscaled 6x.
"""
import json
import struct
import sys
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FW, FH = 16, 24


def read_png(p):
    data = Path(p).read_bytes()
    pos = 8
    w = h = None
    idat = b""
    while pos < len(data):
        ln = struct.unpack(">I", data[pos:pos + 4])[0]
        tag = data[pos + 4:pos + 8]
        chunk = data[pos + 8:pos + 8 + ln]
        if tag == b"IHDR":
            w, h = struct.unpack(">II", chunk[:8])
        elif tag == b"IDAT":
            idat += chunk
        pos += 12 + ln
    raw = zlib.decompress(idat)
    rows = []
    stride = w * 4
    i = 0
    for _ in range(h):
        f = raw[i]
        i += 1
        assert f == 0, f"unsupported PNG filter {f}"
        line = raw[i:i + stride]
        i += stride
        rows.append([tuple(line[x * 4:x * 4 + 4]) for x in range(w)])
    return w, h, rows


def write_png(path, width, height, pixels):
    raw = b"".join(b"\x00" + b"".join(struct.pack("4B", *px) for px in row) for row in pixels)

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    Path(path).parent.mkdir(parents=True, exist_ok=True)
    Path(path).write_bytes(b"\x89PNG\r\n\x1a\n"
                           + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
                           + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))
    print(f"wrote {path}")


MANIFEST = json.loads((ROOT / "data" / "appearance.json").read_text())
GRAY = MANIFEST["gray"]
LAYERS = {}


def layer(name):
    if name not in LAYERS:
        LAYERS[name] = read_png(ROOT / "assets" / "layers" / f"{name}.png")
    return LAYERS[name]


def tint(px, ramp):
    r, g, b, a = px
    if a == 0:
        return None
    # nearest gray index
    idx = min(range(4), key=lambda i: abs(GRAY[i] - r))
    c = ramp[idx]
    return (c[0], c[1], c[2], 255)


def compose(app):
    """app dict -> full sheet rows (48x96) RGBA."""
    out = [[(0, 0, 0, 0)] * (FW * 3) for _ in range(FH * 4)]
    stack = [("body", MANIFEST["skins"][app["skin"]])]
    stack.append((f"outfit_{app['outfit']:02d}_a", MANIFEST["outfitColors"][app["outfitA"]]))
    stack.append((f"outfit_{app['outfit']:02d}_b", MANIFEST["outfitColors"][app["outfitB"]]))
    stack.append((f"hair_{app['hairStyle']:02d}", MANIFEST["hairColors"][app["hairColor"]]))
    race = app.get("race", 0)
    if race > 0:
        ramp = MANIFEST["hairColors"][app["hairColor"]] if race == 2 else MANIFEST["skins"][app["skin"]]
        stack.append((f"race_{race:02d}", ramp))
    if app.get("acc", 0) > 0:
        stack.append((f"acc_{app['acc']:02d}", MANIFEST["outfitColors"][app["outfitB"]]))
    for name, ramp in stack:
        _, _, rows = layer(name)
        for y in range(FH * 4):
            for x in range(FW * 3):
                t = tint(rows[y][x], ramp)
                if t:
                    out[y][x] = t
    return out


def main():
    out_dir = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "docs" / "screenshots"
    presets = MANIFEST["presets"]
    S = 6
    pad = 2
    cols = len(presets)
    sheet_w = cols * (FW * 3 + pad) * S
    sheet_h = (FH * 4 + pad) * S
    big = [[(24, 26, 38, 255)] * sheet_w for _ in range(sheet_h)]
    for i, preset in enumerate(presets):
        rows = compose(preset)
        ox = i * (FW * 3 + pad) * S
        for y in range(FH * 4):
            for x in range(FW * 3):
                px = rows[y][x]
                if px[3] == 0:
                    continue
                for sy in range(S):
                    for sx in range(S):
                        big[y * S + sy][ox + x * S + sx] = px
    write_png(out_dir / "preset-gallery.png", sheet_w, sheet_h, big)


if __name__ == "__main__":
    main()
